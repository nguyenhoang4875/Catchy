from PySide6.QtCore import Qt, QAbstractTableModel, QModelIndex
import re
import os
LINE_NUMBER     = "line_number"
DATE_TIME       = "datetime"
TIME_STAMP      = "timestamp"
LOG_LEVEL       = "log_level"
PROCESS_NAME    = "process_name"
MESSAGE         = "message"
COLOR           = "color"

COL_DATETIME    = "Date Time"
COL_TIMESTAMP   = "Time Stamp"
COL_LOGLEVEL    = "Log Level"
COL_PROCESSNAME = "Process Name"
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

log_pattern = re.compile(
    r'(?P<datetime>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z) '
    r'\[(?P<timestamp>\d+\.\d+)\] '
    r'user\.(?P<log_level>\w+) '
    r'(?P<process_name>.*) '
    r'\[\] '
    r'(?P<message>.*)'
)

# Android logcat format: MM-DD HH:MM:SS.mmm  PID  TID LEVEL TAG: message
logcat_pattern = re.compile(
    r'(?P<datetime>\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)\s+'
    r'(?P<timestamp>\d+)\s+'
    r'\d+\s+'
    r'(?P<log_level>[VDIWEFS])\s+'
    r'(?P<process_name>[^:]+)\s*:\s*'
    r'(?P<message>.*)'
)



MAX_LOG_ROWS = 50_000

class LogModel(QAbstractTableModel):
    ColumnDatetime      = 0
    ColumnTimeStamp     = 1
    ColumnLogLevel      = 2
    ColumnProcessName   = 3
    ColumnMessage       = 4
    CountOfColumns      = 5

    # Ordered list of dict keys matching column indices — avoids if-elif in data()
    _DISPLAY_COLS = [DATE_TIME, TIME_STAMP, LOG_LEVEL, PROCESS_NAME, MESSAGE]

    def __init__(self, logData, parent=None):
        super().__init__(parent)
        self._column_names = [COL_DATETIME, COL_TIMESTAMP, COL_LOGLEVEL, COL_PROCESSNAME, COL_MESSAGE]
        self._controller = None
        self._log_data = list(logData) if logData is not None else []

    def setController(self, controller):
        self._controller = controller

    def _color_for_entry(self, log_entry, colors):
        process_color = colors.get(log_entry[PROCESS_NAME])
        if process_color:
            return process_color
        return LOG_LEVEL_COLORS.get(log_entry.get(LOG_LEVEL, ""), "")

    def loadLogFile(self, file_path, colors):
        print("loadLogFile: ", file_path)
        log_file_path = file_path
        parsed_log = []
        parsed_dict = {}
        lineCount = 0
        try:
            with open(log_file_path, 'r',encoding='utf-8') as file:
                for line in file:
                    match = log_pattern.match(line)
                    if match:
                        log_entry = match.groupdict()
                        log_entry[LINE_NUMBER]  = lineCount
                        log_entry[COLOR] = self._color_for_entry(log_entry, colors)

                        parsed_dict[lineCount] = log_entry
                        parsed_log.append(log_entry)
                        lineCount += 1
        except Exception as e:
            print(f"Error loading log file: {e}")
            app_log_path = os.path.join(ROOT_FOLDER, 'app.log')
            with open(app_log_path, 'w', encoding='utf-8') as file:
                file.write(f"Error loading log file: {e}")

        return (parsed_log, parsed_dict)

    def processLineData(self, lineData, colors):
        match = log_pattern.match(lineData)
        if match:
            log_entry = match.groupdict()
            log_entry[COLOR] = self._color_for_entry(log_entry, colors)
            return (True, log_entry)
        else:
            return (False, None)

    def processLineDataLogcat(self, lineData, colors):
        match = logcat_pattern.match(lineData)
        if match:
            log_entry = match.groupdict()
            log_entry[PROCESS_NAME] = log_entry[PROCESS_NAME].strip()
            log_entry[COLOR] = self._color_for_entry(log_entry, colors)
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
        if role == Qt.DecorationRole:
            return entry[COLOR]
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
        return {Qt.DisplayRole: b"display", Qt.DecorationRole: b"decoration", Qt.UserRole: b"lineNumber"}
    
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
        self._log_data[row][COLOR] = color
        top_left = self.index(row, 0)
        bottom_right = self.index(row, self.columnCount() - 1)
        self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole])

    def updateData(self, data):
        self.beginResetModel()
        self._log_data = data
        self.endResetModel()

    def setColorForProcessName(self, processName, color):
        for row in range(self.rowCount()):
            if processName == self._log_data[row][PROCESS_NAME]:
                self.setRowColor(row, color)

    def resetColorForProcessName(self, processName):
        for row in range(self.rowCount()):
            if processName == self._log_data[row][PROCESS_NAME]:
                self.setRowColor(row, self._color_for_entry(self._log_data[row], {}))
