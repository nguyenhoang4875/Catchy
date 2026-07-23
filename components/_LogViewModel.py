from PySide6.QtCore import Qt, QAbstractTableModel, QModelIndex
import re
import os
from components._Defines import BATCH_SIZE, IO_BUFFER_SIZE

LINE_NUMBER     = "line_number"
DATE_TIME       = "datetime"
PID             = "pid"
TID             = "tid"
LOG_LEVEL       = "log_level"
TAG             = "tag"
PROCESS_NAME    = "process_name"
MESSAGE         = "message"

COL_LINE        = "Line"
COL_DATETIME    = "Date Time"
COL_PID         = "PID"
COL_TID         = "TID"
COL_LOGLEVEL    = "Level"
COL_TAG         = "Tag"
COL_MESSAGE     = "Message"

LINE_NUMBER     = "line_number"

LOG_LEVEL_COLORS = {
    "V": "#4E7D96 ",
    "D": "#0086D6",
    "I": "#108528",
    "W": "#C47C00",
    "E": "#D32F2F",
    "F": "#A222B2",
}

ROOT_FOLDER = "C:/QtLogViewer"

# Regex patterns kept as fallback only — manual parsers are faster.
log_pattern = re.compile(
    r'(?P<datetime>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z) '
    r'\[(?P<pid>\d+\.\d+)\] '
    r'user\.(?P<log_level>\w+) '
    r'(?P<tag>.*?) '            # non-greedy to avoid backtracking
    r'\[\] '
    r'(?P<message>.*)'
)

compact_pattern = re.compile(
    r'\[(?P<datetime>[^\]]*)\]\s+'
    r'\[(?P<pid>[^\]]*)\]\s+'
    r'\[(?P<tid>[^\]]*)\]\s+'
    r'\[(?P<log_level>[^\]]*)\]\s+'
    r'\[(?P<tag>[^\]]*)\]\s+'
    r'\[(?P<message>.*)\]\s*$'
)

logcat_pattern = re.compile(
    r'(?P<datetime>(?:\d{4}-)?\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)\s+'
    r'(?P<pid>\d+)\s+'
    r'(?P<tid>\d+)\s+'
    r'(?P<log_level>[VDIWEFS])\s+'
    r'(?P<tag>[^:]+?)\s*:\s*'
    r'(?P<message>.*)'
)

_ALL_PATTERNS = [compact_pattern, logcat_pattern, log_pattern]

# ── Fast manual parsers (pure str.find — runs in C, no regex overhead) ──────

_LEVEL_CHARS = frozenset('VDIWEFS')

def _parse_iso_fast(line):
    """Regex-based parser for ISO format.
    Format: 2024-11-18T09:13:57.159802Z [PID] user.LEVEL TAG [] MESSAGE
    """
    m = log_pattern.match(line)
    if m is None:
        return None
    tag = m.group('tag').strip()
    return {
        DATE_TIME:    m.group('datetime'),
        PID:          m.group('pid'),
        TID:          '',
        LOG_LEVEL:    m.group('log_level'),
        TAG:          tag,
        PROCESS_NAME: tag,
        MESSAGE:      m.group('message'),
    }


def _skip_spaces(s, pos):
    """Advance pos past all space characters in s."""
    n = len(s)
    while pos < n and s[pos] == ' ':
        pos += 1
    return pos


def _read_digits(s, pos):
    """Return (value_str, end_pos) for a run of digits starting at pos.
    Returns (None, pos) when no digits are found.
    """
    n = len(s)
    start = pos
    while pos < n and s[pos].isdigit():
        pos += 1
    return (s[start:pos], pos) if pos > start else (None, start)

def _parse_compact_fast(line):
    """Manual parser for bracketed compact format."""
    # Format: [DateTime] [PID] [TID] [Level] [Tag] [Message]
    if not line.startswith('['):
        return None
    fields = []
    pos = 0
    n = len(line)
    for _ in range(6):
        start = line.find('[', pos)
        if start < 0:
            return None
        end = line.find(']', start + 1)
        if end < 0:
            return None
        fields.append(line[start + 1:end])
        pos = end + 1
    tag = fields[4].strip()
    return {
        DATE_TIME: fields[0].strip(),
        PID: fields[1].strip(),
        TID: fields[2].strip(),
        LOG_LEVEL: fields[3].strip(),
        TAG: tag,
        PROCESS_NAME: tag,
        MESSAGE: fields[5],
    }


def _parse_logcat_regex(line):
    """Regex-based parser for Android logcat threadtime format.

    Equivalent to _parse_logcat_fast but uses the pre-compiled logcat_pattern.
    Useful for correctness comparison / unit-testing against the fast parser.

    Supports both date prefixes:
      MM-DD HH:MM:SS.mmm  PID  TID L TAG: message
      YYYY-MM-DD HH:MM:SS.mmm  PID  TID L TAG: message
    """
    m = logcat_pattern.match(line)
    if m is None:
        return None
    tag = m.group('tag').strip()
    return {
        DATE_TIME:    m.group('datetime'),
        PID:          m.group('pid'),
        TID:          m.group('tid'),
        LOG_LEVEL:    m.group('log_level'),
        TAG:          tag,
        PROCESS_NAME: tag,
        MESSAGE:      m.group('message'),
    }


# Format enum for detect_format result
_FMT_ISO     = 1
_FMT_LOGCAT  = 2
_FMT_COMPACT = 3
_FMT_UNKNOWN = 0

_FAST_PARSERS = {
    _FMT_ISO: _parse_iso_fast,
    _FMT_LOGCAT: _parse_logcat_regex,
    _FMT_COMPACT: _parse_compact_fast,
}



class TagInternPool:
    """Pool frequently repeated tag strings to reduce memory usage."""
    __slots__ = ('_pool',)

    def __init__(self):
        self._pool = {}

    def intern(self, tag):
        existing = self._pool.get(tag)
        if existing is not None:
            return existing
        self._pool[tag] = tag
        return tag

    def clear(self):
        self._pool.clear()


_tag_pool = TagInternPool()


def detect_format(file_path):
    """Read first 20 lines, return format enum and regex fallback pattern."""
    try:
        with open(file_path, 'r', encoding='utf-8', errors='replace',
                  buffering=IO_BUFFER_SIZE) as f:
            sample = []
            for _ in range(20):
                ln = f.readline()
                if not ln:
                    break
                sample.append(ln.rstrip('\n\r'))
    except Exception:
        return _FMT_UNKNOWN, None

    if not sample:
        return _FMT_UNKNOWN, None

    # Try each fast parser
    for fmt, parser in _FAST_PARSERS.items():
        hits = sum(1 for s in sample if parser(s) is not None)
        if hits >= 1:
            return fmt, None

    # Fallback: try regex patterns
    for pattern in _ALL_PATTERNS:
        hits = sum(1 for s in sample if pattern.match(s))
        if hits >= 1:
            return _FMT_UNKNOWN, pattern

    return _FMT_UNKNOWN, None


ROLE_FILTER_COLOR = Qt.UserRole + 1
ROLE_LEVEL_COLOR = Qt.UserRole + 2

class LogModel(QAbstractTableModel):
    ColumnLine          = 0
    ColumnDatetime      = 1
    ColumnPid           = 2
    ColumnTid           = 3
    ColumnLogLevel      = 4
    ColumnTag           = 5
    ColumnMessage       = 6
    CountOfColumns      = 7

    # Ordered list of dict keys matching column indices — avoids if-elif in data()
    _DISPLAY_COLS = [LINE_NUMBER, DATE_TIME, PID, TID, LOG_LEVEL, TAG, MESSAGE]

    def __init__(self, logData, parent=None):
        super().__init__(parent)
        self._column_names = [COL_LINE, COL_DATETIME, COL_PID, COL_TID, COL_LOGLEVEL, COL_TAG, COL_MESSAGE]
        self._controller = None
        self._log_data = list(logData) if logData is not None else []
        # Lazy color computation state
        self._colors = {}           # current filter colors dict
        self._compiled_colors = []  # list of (pattern_str, compiled_re, color)
        self._filter_version = 0    # bumped on every filter change

    def setController(self, controller):
        self._controller = controller

    def setFilterColors(self, colors):
        """Update the filter-color mapping and invalidate the lazy cache."""
        self._colors = colors
        # Pre-compile filter regex patterns for fast color lookup.
        self._compiled_colors = []
        for tag_pattern, color in colors.items():
            pat_str = str(tag_pattern)
            try:
                compiled = re.compile(pat_str, re.IGNORECASE)
            except re.error:
                compiled = None
            self._compiled_colors.append((pat_str, compiled, color))
        self._filter_version += 1
        if self._log_data:
            top_left = self.index(0, 0)
            bottom_right = self.index(self.rowCount() - 1, self.columnCount() - 1)
            self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole, ROLE_FILTER_COLOR, ROLE_LEVEL_COLOR])

    @staticmethod
    def _tag_matches(pattern, value):
        if not pattern:
            return False

        try:
            return bool(re.search(pattern, value, re.IGNORECASE))
        except re.error:
            # Fall back to case-insensitive exact text matching for invalid regex patterns.
            return pattern.lower() == value.lower()

    def _color_for_entry(self, log_entry, colors):
        filter_color = self._filter_color_for_entry(log_entry, colors)
        if filter_color:
            return filter_color
        return self._level_color_for_entry(log_entry)

    def _filter_color_for_entry(self, log_entry, colors):
        process_name = log_entry.get(PROCESS_NAME) or ""
        if not process_name:
            return ""
        for pat_str, compiled_re, color in self._compiled_colors:
            if compiled_re is not None:
                if compiled_re.search(process_name):
                    return color
            elif pat_str.lower() == process_name.lower():
                return color
        return ""

    def _level_color_for_entry(self, log_entry):
        return LOG_LEVEL_COLORS.get(log_entry.get(LOG_LEVEL, ""), "")

    def _normalize_entry(self, log_entry):
        pid = log_entry.get(PID)
        log_entry[PID] = pid.strip() if pid else ''
        tid = log_entry.get(TID)
        log_entry[TID] = tid.strip() if tid else ''
        tag_raw = log_entry.get(TAG)
        tag = _tag_pool.intern(tag_raw.strip()) if tag_raw else ''
        log_entry[TAG] = tag
        lvl = log_entry.get(LOG_LEVEL)
        log_entry[LOG_LEVEL] = lvl.strip() if lvl else ''
        dt = log_entry.get(DATE_TIME)
        log_entry[DATE_TIME] = dt.strip() if dt else ''
        msg = log_entry.get(MESSAGE)
        log_entry[MESSAGE] = msg.strip() if msg else ''
        log_entry[PROCESS_NAME] = tag
        return log_entry

    def _parse_line(self, line):
        for pattern in _ALL_PATTERNS:
            match = pattern.match(line)
            if match:
                return self._normalize_entry(match.groupdict())
        return None

    def format_log_line(self, log_entry):
        return (
            f"[{log_entry.get(DATE_TIME, '')}] "
            f"[{log_entry.get(PID, '')}] "
            f"[{log_entry.get(TID, '')}] "
            f"[{log_entry.get(LOG_LEVEL, '')}] "
            f"[{log_entry.get(TAG, '')}] "
            f"[{log_entry.get(MESSAGE, '')}]"
        )

    def loadLogFile(self, file_path, colors, progress_callback=None, cancel_flag=None):
        """Load a log file and return all parsed entries.

        Uses fast manual parsers when possible, falling back to regex.
        """
        print("loadLogFile: ", file_path)

        fmt, fallback_pattern = detect_format(file_path)

        # Build the parse function based on detected format.
        fast_parser = _FAST_PARSERS.get(fmt)
        if fast_parser is not None:
            # Fast path: manual parser produces ready-to-use dicts.
            _intern = _tag_pool.intern
            _fp = fast_parser

            def parse_fn(line):
                d = _fp(line)
                if d is not None:
                    # Intern the tag for memory savings.
                    t = d[TAG]
                    interned = _intern(t)
                    if interned is not t:
                        d[TAG] = interned
                        d[PROCESS_NAME] = interned
                    return d
                # Rare fallback to regex
                return self._parse_line(line)
        elif fallback_pattern is not None:
            _pat = fallback_pattern
            _norm = self._normalize_entry

            def parse_fn(line):
                m = _pat.match(line)
                if m:
                    return _norm(m.groupdict())
                return self._parse_line(line)
        else:
            parse_fn = self._parse_line

        try:
            file_size = os.path.getsize(file_path)
        except OSError:
            file_size = 0

        all_entries = []
        _append = all_entries.append
        line_count = 1
        bytes_read = 0
        _progress_interval = 5000  # report progress every 5K lines for smooth updates

        try:
            with open(file_path, 'rb', buffering=IO_BUFFER_SIZE) as fh_bin:
                remainder = b''
                while True:
                    if cancel_flag and cancel_flag():
                        break

                    chunk = fh_bin.read(IO_BUFFER_SIZE)
                    if not chunk:
                        # Process remaining bytes
                        if remainder:
                            line_str = remainder.decode('utf-8', errors='replace').rstrip('\n\r')
                            entry = parse_fn(line_str)
                            if entry is not None:
                                entry[LINE_NUMBER] = line_count
                                _append(entry)
                                line_count += 1
                        break

                    bytes_read += len(chunk)
                    data = remainder + chunk
                    lines = data.split(b'\n')
                    remainder = lines.pop()  # last element is incomplete line

                    for raw_bytes in lines:
                        line_str = raw_bytes.decode('utf-8', errors='replace').rstrip('\r')
                        entry = parse_fn(line_str)
                        if entry is not None:
                            entry[LINE_NUMBER] = line_count
                            _append(entry)
                            line_count += 1

                        if progress_callback and line_count % _progress_interval == 0:
                            progress_callback(min(bytes_read / file_size, 0.99) if file_size else 0.0)

            if progress_callback:
                progress_callback(1.0)

        except Exception as e:
            print(f"Error loading log file: {e}")
            try:
                app_log_path = os.path.join(ROOT_FOLDER, 'app.log')
                with open(app_log_path, 'w', encoding='utf-8') as f:
                    f.write(f"Error loading log file: {e}")
            except Exception:
                pass

        return all_entries

    def processLineData(self, lineData, colors):
        log_entry = self._parse_line(lineData)
        if log_entry:
            return (True, log_entry)
        else:
            return (False, None)

    def processLineDataLogcat(self, lineData, colors):
        match = logcat_pattern.match(lineData)
        if match:
            log_entry = self._normalize_entry(match.groupdict())
            return (True, log_entry)
        else:
            return (False, None)
            
        
    def rowCount(self, parent=QModelIndex()):
        return len(self._log_data)

    def columnCount(self, parent=QModelIndex()):
        return self.CountOfColumns

    def data(self, index, role=Qt.DisplayRole):
        if not index.isValid():
            return None
        row = index.row()
        if not 0 <= row < len(self._log_data):
            return None
        entry = self._log_data[row]
        if role == Qt.UserRole:
            return entry[LINE_NUMBER]
        if role == ROLE_FILTER_COLOR:
            return self._filter_color_for_entry(entry, self._colors)
        if role == ROLE_LEVEL_COLOR:
            return self._level_color_for_entry(entry)
        if role == Qt.DecorationRole:
            return self._color_for_entry(entry, self._colors)
        if role == Qt.DisplayRole:
            col = index.column()
            if 0 <= col < self.CountOfColumns:
                return entry[self._DISPLAY_COLS[col]]
        return None

    def headerData(self, section, orientation, role=Qt.DisplayRole):
        """ Set the headers to be displayed. """
        if role != Qt.DisplayRole:
            return None
        if orientation == Qt.Horizontal and 0 <= section < len(self._column_names):
            return self._column_names[section]
        return None

    def roleNames(self):
        return {
            Qt.DisplayRole: b"display",
            Qt.DecorationRole: b"decoration",
            Qt.UserRole: b"lineNumber",
            ROLE_FILTER_COLOR: b"filterColor",
            ROLE_LEVEL_COLOR: b"levelColor",
        }
    
    def addRows(self, entries):
        """Batch-insert multiple rows at the end. One beginInsertRows/endInsertRows call."""
        if not entries:
            return
        first = len(self._log_data)
        last = first + len(entries) - 1
        self.beginInsertRows(QModelIndex(), first, last)
        self._log_data.extend(entries)
        self.endInsertRows()

    def addRow(self, lineData, index=QModelIndex()):
        """ Insert a single row at the end of the model. """
        last_row = len(self._log_data)
        self.beginInsertRows(QModelIndex(), last_row, last_row)
        self._log_data.append(lineData)
        self.endInsertRows()
        return True

    def trimRows(self, count):
        """Remove count rows from the front to enforce a max row cap."""
        if count <= 0:
            return
        count = min(count, len(self._log_data))
        self.beginRemoveRows(QModelIndex(), 0, count - 1)
        del self._log_data[:count]
        self.endRemoveRows()

    def setRowColor(self, row, color):
        # Trigger a repaint for this row (colors computed lazily in data())
        top_left = self.index(row, 0)
        bottom_right = self.index(row, self.columnCount() - 1)
        self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole])

    def updateData(self, data):
        self.beginResetModel()
        self._log_data = data
        self.endResetModel()

    def setColorForProcessName(self, processName, color):
        # Trigger repaint only — colors are computed lazily
        for row in range(self.rowCount()):
            if processName == self._log_data[row][PROCESS_NAME]:
                self.setRowColor(row, color)

    def resetColorForProcessName(self, processName):
        for row in range(self.rowCount()):
            if processName == self._log_data[row][PROCESS_NAME]:
                self.setRowColor(row, None)

    def reapplyProcessColors(self, colors):
        """Update filter colors and trigger repaint. Colors are computed lazily in data()."""
        self.setFilterColors(colors)
