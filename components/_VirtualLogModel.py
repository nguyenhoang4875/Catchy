from PySide6.QtCore import Qt, QAbstractTableModel, QModelIndex

from components._LogCache import LogCache
from components._LogFileIndexer import LogFileIndexer
from components._LogFileReader import LogFileReader
from components._LogParser import (
    COL_DATETIME,
    COL_LOGLEVEL,
    COL_MESSAGE,
    COL_PID,
    COL_TAG,
    COL_TID,
    COLOR,
    DATE_TIME,
    FILTER_COLOR,
    LEVEL_COLOR,
    LINE_NUMBER,
    LOG_LEVEL,
    MESSAGE,
    PID,
    TAG,
    TID,
    color_for_entry,
    filter_color_for_entry,
    format_log_line,
    level_color_for_entry,
    parse_logcat_line,
    parse_line,
)

ROLE_FILTER_COLOR = Qt.UserRole + 1
ROLE_LEVEL_COLOR = Qt.UserRole + 2


class VirtualLogModel(QAbstractTableModel):
    ColumnDatetime = 0
    ColumnPid = 1
    ColumnTid = 2
    ColumnLogLevel = 3
    ColumnTag = 4
    ColumnMessage = 5
    CountOfColumns = 6

    _DISPLAY_COLS = [DATE_TIME, PID, TID, LOG_LEVEL, TAG, MESSAGE]

    def __init__(self, cache_size=8000, parent=None):
        super().__init__(parent)
        self._column_names = [COL_DATETIME, COL_PID, COL_TID, COL_LOGLEVEL, COL_TAG, COL_MESSAGE]
        self._controller = None
        self._reader = LogFileReader()
        self._indexer = LogFileIndexer()
        self._cache = LogCache(cache_size)
        self._offsets = []
        self._line_count = 0
        self._colors = {}
        self._source_path = ""
        self._fallback_data = []

    def setController(self, controller):
        self._controller = controller

    @property
    def sourcePath(self):
        return self._source_path

    @property
    def isVirtualMode(self):
        return bool(self._offsets) and bool(self._source_path)

    def buildOffsets(self, file_path, progress_callback=None, cancel_check=None):
        return self._indexer.build_index(file_path, progress_callback=progress_callback, cancel_check=cancel_check)

    def activateIndexedSource(self, file_path, offsets, colors):
        if offsets is None:
            return False

        self.beginResetModel()
        self._reader.open(file_path)
        self._source_path = file_path
        self._offsets = list(offsets)
        self._line_count = len(self._offsets)
        self._colors = dict(colors or {})
        self._cache.clear()
        self._fallback_data = []
        self.endResetModel()
        return True

    def openLogFile(self, file_path, colors, progress_callback=None, cancel_check=None):
        offsets = self.buildOffsets(file_path, progress_callback=progress_callback, cancel_check=cancel_check)
        return self.activateIndexedSource(file_path, offsets, colors)

    def closeFile(self):
        self.beginResetModel()
        self._reader.close()
        self._source_path = ""
        self._offsets = []
        self._line_count = 0
        self._cache.clear()
        self._fallback_data = []
        self.endResetModel()

    def loadLogFile(self, file_path, colors):
        parsed_log = []
        parsed_dict = {}
        line_count = 0
        with open(file_path, "r", encoding="utf-8", errors="replace") as file:
            for line in file:
                parsed = parse_line(line.strip())
                if not parsed:
                    continue
                parsed[LINE_NUMBER] = line_count
                parsed[FILTER_COLOR] = filter_color_for_entry(parsed, colors)
                parsed[LEVEL_COLOR] = level_color_for_entry(parsed)
                parsed[COLOR] = color_for_entry(parsed, colors)
                parsed_dict[line_count] = parsed
                parsed_log.append(parsed)
                line_count += 1
        return parsed_log, parsed_dict

    def _parse_at_row(self, row):
        cached = self._cache.get(row)
        if cached is not None:
            return cached

        raw_line = self._reader.read_line(self._offsets[row])
        parsed = parse_line(raw_line)
        if parsed is None:
            parsed = {
                DATE_TIME: "",
                PID: "",
                TID: "",
                LOG_LEVEL: "",
                TAG: "",
                MESSAGE: raw_line,
            }

        parsed[LINE_NUMBER] = row
        parsed[FILTER_COLOR] = filter_color_for_entry(parsed, self._colors)
        parsed[LEVEL_COLOR] = level_color_for_entry(parsed)
        parsed[COLOR] = color_for_entry(parsed, self._colors)

        self._cache.put(row, parsed)
        return parsed

    def rowCount(self, parent=QModelIndex()):
        if self.isVirtualMode:
            return self._line_count
        return len(self._fallback_data)

    def columnCount(self, parent=QModelIndex()):
        return self.CountOfColumns

    def data(self, index, role=Qt.DisplayRole):
        if not index.isValid():
            return None

        row = index.row()
        if not 0 <= row < self.rowCount():
            return None

        if self.isVirtualMode:
            entry = self._parse_at_row(row)
        else:
            entry = self._fallback_data[row]

        if role == Qt.UserRole:
            return entry[LINE_NUMBER]
        if role == ROLE_FILTER_COLOR:
            return entry.get(FILTER_COLOR, "")
        if role == ROLE_LEVEL_COLOR:
            return entry.get(LEVEL_COLOR, "")
        if role == Qt.DecorationRole:
            return entry[COLOR]
        if role == Qt.DisplayRole:
            col = index.column()
            if 0 <= col < self.CountOfColumns:
                return entry[self._DISPLAY_COLS[col]]
        return None

    def getEntryAtLine(self, line_number):
        if line_number < 0 or line_number >= self.rowCount():
            return None
        if self.isVirtualMode:
            return self._parse_at_row(line_number)
        return self._fallback_data[line_number]

    def getMessageAtLine(self, line_number):
        entry = self.getEntryAtLine(line_number)
        if not entry:
            return ""
        return str(entry.get(MESSAGE, ""))

    def iterEntries(self):
        if self.isVirtualMode:
            for row in range(self.rowCount()):
                yield self._parse_at_row(row)
            return

        for entry in self._fallback_data:
            yield entry

    def headerData(self, section, orientation, role=Qt.DisplayRole):
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

    # Compatibility APIs used by Controller during live modes.
    def updateData(self, data):
        self.beginResetModel()
        self._reader.close()
        self._source_path = ""
        self._offsets = []
        self._line_count = 0
        self._cache.clear()
        self._fallback_data = list(data or [])
        self.endResetModel()

    def addRows(self, entries):
        if self.isVirtualMode:
            return
        if not entries:
            return
        first = len(self._fallback_data)
        last = first + len(entries) - 1
        self.beginInsertRows(QModelIndex(), first, last)
        self._fallback_data.extend(entries)
        self.endInsertRows()

    def addRow(self, lineData, index=QModelIndex()):
        if self.isVirtualMode:
            return True
        last_row = len(self._fallback_data)
        self.beginInsertRows(QModelIndex(), last_row, last_row)
        self._fallback_data.append(lineData)
        self.endInsertRows()
        return True

    def trimRows(self, count):
        if self.isVirtualMode:
            return
        if count <= 0:
            return
        count = min(count, len(self._fallback_data))
        self.beginRemoveRows(QModelIndex(), 0, count - 1)
        del self._fallback_data[:count]
        self.endRemoveRows()

    def processLineData(self, lineData, colors):
        parsed = parse_line(lineData)
        if not parsed:
            return False, None
        parsed[FILTER_COLOR] = filter_color_for_entry(parsed, colors)
        parsed[LEVEL_COLOR] = level_color_for_entry(parsed)
        parsed[COLOR] = color_for_entry(parsed, colors)
        return True, parsed

    def processLineDataLogcat(self, lineData, colors):
        parsed = parse_logcat_line(lineData)
        if not parsed:
            return False, None
        parsed[FILTER_COLOR] = filter_color_for_entry(parsed, colors)
        parsed[LEVEL_COLOR] = level_color_for_entry(parsed)
        parsed[COLOR] = color_for_entry(parsed, colors)
        return True, parsed

    def format_log_line(self, log_entry):
        return format_log_line(log_entry)

    def reapplyProcessColors(self, colors):
        self._colors = dict(colors or {})
        if self.isVirtualMode:
            self._cache.clear()
            if self.rowCount() > 0:
                top_left = self.index(0, 0)
                bottom_right = self.index(self.rowCount() - 1, self.columnCount() - 1)
                self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole])
            return

        if not self._fallback_data:
            return
        for row in range(self.rowCount()):
            self._fallback_data[row][FILTER_COLOR] = filter_color_for_entry(self._fallback_data[row], self._colors)
            self._fallback_data[row][LEVEL_COLOR] = level_color_for_entry(self._fallback_data[row])
            self._fallback_data[row][COLOR] = color_for_entry(self._fallback_data[row], self._colors)

        top_left = self.index(0, 0)
        bottom_right = self.index(self.rowCount() - 1, self.columnCount() - 1)
        self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole])

    def setColorForProcessName(self, processName, color):
        # Keep compatibility for existing controller calls; in virtual mode
        # color is computed dynamically from filter state.
        if self.isVirtualMode:
            return
        for row in range(self.rowCount()):
            if processName == self._fallback_data[row].get(TAG, ""):
                self._fallback_data[row][COLOR] = color
                top_left = self.index(row, 0)
                bottom_right = self.index(row, self.columnCount() - 1)
                self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole])

    def resetColorForProcessName(self, processName):
        if self.isVirtualMode:
            return
        for row in range(self.rowCount()):
            if processName == self._fallback_data[row].get(TAG, ""):
                self._fallback_data[row][COLOR] = color_for_entry(self._fallback_data[row], self._colors)
                top_left = self.index(row, 0)
                bottom_right = self.index(row, self.columnCount() - 1)
                self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole])
