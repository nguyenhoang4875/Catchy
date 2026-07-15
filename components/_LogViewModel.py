from PySide6.QtCore import Qt, QAbstractTableModel, QModelIndex
import os
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
    PROCESS_NAME,
    TAG,
    TID,
    color_for_entry,
    filter_color_for_entry,
    format_log_line,
    level_color_for_entry,
    parse_line,
    parse_logcat_line,
)

ROOT_FOLDER = "C:/QtLogViewer"



MAX_LOG_ROWS = 50_000
ROLE_FILTER_COLOR = Qt.UserRole + 1
ROLE_LEVEL_COLOR = Qt.UserRole + 2

class LogModel(QAbstractTableModel):
    ColumnDatetime      = 0
    ColumnPid           = 1
    ColumnTid           = 2
    ColumnLogLevel      = 3
    ColumnTag           = 4
    ColumnMessage       = 5
    CountOfColumns      = 6

    # Ordered list of dict keys matching column indices — avoids if-elif in data()
    _DISPLAY_COLS = [DATE_TIME, PID, TID, LOG_LEVEL, TAG, MESSAGE]

    def __init__(self, logData, parent=None):
        super().__init__(parent)
        self._column_names = [COL_DATETIME, COL_PID, COL_TID, COL_LOGLEVEL, COL_TAG, COL_MESSAGE]
        self._controller = None
        self._log_data = list(logData) if logData is not None else []

    def setController(self, controller):
        self._controller = controller

    def _color_for_entry(self, log_entry, colors):
        return color_for_entry(log_entry, colors)

    def _filter_color_for_entry(self, log_entry, colors):
        return filter_color_for_entry(log_entry, colors)

    def _level_color_for_entry(self, log_entry):
        return level_color_for_entry(log_entry)

    def _parse_line(self, line):
        return parse_line(line)

    def format_log_line(self, log_entry):
        return format_log_line(log_entry)

    def loadLogFile(self, file_path, colors):
        print("loadLogFile: ", file_path)
        log_file_path = file_path
        parsed_log = []
        parsed_dict = {}
        lineCount = 0
        try:
            with open(log_file_path, 'r',encoding='utf-8') as file:
                for line in file:
                    log_entry = self._parse_line(line.strip())
                    if log_entry:
                        log_entry[LINE_NUMBER]  = lineCount
                        log_entry[FILTER_COLOR] = self._filter_color_for_entry(log_entry, colors)
                        log_entry[LEVEL_COLOR] = self._level_color_for_entry(log_entry)
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
        log_entry = self._parse_line(lineData)
        if log_entry:
            log_entry[FILTER_COLOR] = self._filter_color_for_entry(log_entry, colors)
            log_entry[LEVEL_COLOR] = self._level_color_for_entry(log_entry)
            log_entry[COLOR] = self._color_for_entry(log_entry, colors)
            return (True, log_entry)
        else:
            return (False, None)

    def processLineDataLogcat(self, lineData, colors):
        log_entry = parse_logcat_line(lineData)
        if log_entry:
            log_entry[FILTER_COLOR] = self._filter_color_for_entry(log_entry, colors)
            log_entry[LEVEL_COLOR] = self._level_color_for_entry(log_entry)
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

    def reapplyProcessColors(self, colors):
        if not self._log_data:
            return

        for row in range(self.rowCount()):
            self._log_data[row][FILTER_COLOR] = self._filter_color_for_entry(self._log_data[row], colors)
            self._log_data[row][LEVEL_COLOR] = self._level_color_for_entry(self._log_data[row])
            self._log_data[row][COLOR] = self._color_for_entry(self._log_data[row], colors)

        top_left = self.index(0, 0)
        bottom_right = self.index(self.rowCount() - 1, self.columnCount() - 1)
        self.dataChanged.emit(top_left, bottom_right, [Qt.DecorationRole])
