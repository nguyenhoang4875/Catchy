from PySide6.QtCore import Qt, QAbstractTableModel, QModelIndex
import re
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
LINE_DEFAULT_COLOR = "#CCCCCC"

log_pattern = re.compile(
    r'(?P<datetime>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z) '
    r'\[(?P<timestamp>\d+\.\d+)\] '
    r'user\.(?P<log_level>\w+) '
    r'(?P<process_name>.*) '
    r'\[\] '
    r'(?P<message>.*)'
)


class LogModel(QAbstractTableModel):
    ColumnDatetime      = 0
    ColumnTimeStamp     = 1
    ColumnLogLevel      = 2
    ColumnProcessName   = 3
    ColumnMessage       = 4
    CountOfColumns      = 5

    def __init__(self, logData, parent=None):
        super().__init__(parent)
        self._column_names = [COL_DATETIME, COL_TIMESTAMP, COL_LOGLEVEL, COL_PROCESSNAME, COL_MESSAGE]

        if logData is not None:
            self._log_data = logData
        else:
            self._log_data = []

    def loadLogFile(self, file_path, colors):
        print("loadLogFile: ", file_path)
        log_file_path = file_path
        parsed_log = []
        parsed_dict = {}
        lineCount = 0
        with open(log_file_path, 'r',encoding='utf-8') as file:
            for line in file:
                match = log_pattern.match(line)
                if match:
                    log_entry = match.groupdict()
                    log_entry[LINE_NUMBER]  = lineCount

                    if log_entry[PROCESS_NAME] in colors.keys():
                        log_entry[COLOR] = colors[log_entry[PROCESS_NAME]]
                    else:
                        log_entry[COLOR] = LINE_DEFAULT_COLOR

                    parsed_dict[lineCount] = log_entry
                    parsed_log.append(log_entry)
                    lineCount += 1

        return (parsed_log, parsed_dict)

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
        
        if role == Qt.UserRole:
            return self._log_data[index.row()][LINE_NUMBER]
        
        if role == Qt.DecorationRole:
            return self._log_data[index.row()][COLOR]

        if role == Qt.DisplayRole:
            datetime    = self._log_data[index.row()][DATE_TIME]
            timestamp   = self._log_data[index.row()][TIME_STAMP]
            loglevel    = self._log_data[index.row()][LOG_LEVEL]
            processname = self._log_data[index.row()][PROCESS_NAME]
            message     = self._log_data[index.row()][MESSAGE]

            if index.column() == self.ColumnDatetime:
                return datetime
            elif index.column() == self.ColumnTimeStamp:
                return timestamp
            elif index.column() == self.ColumnLogLevel:
                return loglevel
            elif index.column() == self.ColumnProcessName:
                return processname
            elif index.column() == self.ColumnMessage:
                return message
            
        return None

    def headerData(self, section, orientation, role=Qt.DisplayRole):
        """ Set the headers to be displayed. """
        if role != Qt.DisplayRole:
            return None
        
        if orientation == Qt.Horizontal:
            if section == self.ColumnDatetime:
                return COL_DATETIME
            elif section == self.ColumnTimeStamp:
                return COL_TIMESTAMP
            elif section == self.ColumnLogLevel:
                return COL_LOGLEVEL
            elif section == self.ColumnProcessName:
                return COL_PROCESSNAME
            elif section == self.ColumnMessage:
                return COL_MESSAGE
        return None

    def roleNames(self):
        return {Qt.DisplayRole: b"display", Qt.DecorationRole: b"decoration", Qt.UserRole: b"lineNumber"}
    
    def addRow(self, lineData, index=QModelIndex()):
        
        """ Insert a row into the model. """
        last_row = len(self._log_data)
        self.beginInsertRows(QModelIndex(), last_row, last_row)

        self._log_data.insert(last_row, lineData)

        self.endInsertRows()
        return True

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
                self.setRowColor(row, LINE_DEFAULT_COLOR)
