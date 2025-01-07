# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Slot, QThread, Signal, Property, QModelIndex, QMimeData, QSortFilterProxyModel
from PySide6.QtGui import QGuiApplication, QClipboard
from PySide6.QtWidgets import QFileDialog
from components._FilterLog import FilterLog
from components._LogViewModel import LogModel
from components._Worker import Worker
from components._SearchLog import SearchLog
from components._Configurations import Configurations
from components._SortFilterProxyModel import SortFilterProxyModel
import pyperclip
import re
import os
import json
ROOT_FOLDER = "C:/QtLogViewer"

class Controller(QObject):
    showLoadingScreenChanged = Signal()
    logViewReadyChanged      = Signal()
    loadLogFileCompleted     = Signal()
    detailsTextChanged       = Signal()
    highlightLineNumChanged  = Signal()
    showNotification         = Signal(str, arguments=["message"])
    def __init__(self, parent=None):
        super().__init__(parent)
        self.create()
        self.filterLog          = FilterLog()
        self.logviewModel       = LogModel(logData=None)
        self._loadLogFileThread = QThread()
        self._showLoadingScreen = False
        self._searchLog         = SearchLog()
        self._logViewReady      = False

        self._configs           = Configurations()
        self._configs.loadLastSavedConfig()

        filterPath = self._configs.getConfigs()["filter"]["path"]
        self.filterLog.create(filterPath)
        self._originalFilters = self.filterLog.originalFilters()
        self._logDict           = {}
        self._detailsText       = ""
        self._highlightLineNum  = -1
        pass

    @Property(bool, notify=showLoadingScreenChanged)
    def showLoadingScreen(self):
        return self._showLoadingScreen
    
    @showLoadingScreen.setter
    def showLoadingScreen(self, val):
        self._showLoadingScreen = val
        self.showLoadingScreenChanged.emit()

    @Property(bool, notify=logViewReadyChanged)
    def logViewReady(self):
        return self._logViewReady
    
    @logViewReady.setter
    def logViewReady(self, val):
        self._logViewReady = val
        self.logViewReadyChanged.emit()

    @Property(str, notify=detailsTextChanged)
    def detailsText(self):
        return self._detailsText
    
    @detailsText.setter
    def detailsText(self, val):
        self._detailsText = val
        self.detailsTextChanged.emit()

    @Property(int, notify=highlightLineNumChanged)
    def highlightLineNum(self):
        return self._highlightLineNum
    
    @highlightLineNum.setter
    def highlightLineNum(self, val):
        self._highlightLineNum = val
        self.highlightLineNumChanged.emit()

    @Slot(int)
    def showLogDetails(self, line):
        print("showLogDetails: ", line)
        logline = self._logDict[line]
        strs = []
        keys = ["datetime", "timestamp", "log_level", "process_name", "message"]
        for key in logline.keys():
            if key in keys:
                strs.append(logline[key])
        self.detailsText = " ".join(strs)

    def getLogViewModel(self):
        return self.logviewModel
    
    def getFilterLog(self):
        return self.filterLog
    
    def getSearchLog(self):
        return self._searchLog

    @Slot()
    def openFileDialog(self):
        file_dialog = QFileDialog()
        file_dialog.setNameFilter("All files (*.*);;Log files (*.log)")
        if file_dialog.exec():
            selected_file = file_dialog.selectedFiles()[0]
            self.worker = Worker(self.loadLogFile, selected_file)
            self.worker.moveToThread(self._loadLogFileThread)
            self.worker.taskCompleted.connect(self.onLogFileLoaded)
            self._loadLogFileThread.started.connect(self.worker.run)
            self._loadLogFileThread.start()

    def loadLogFile(self, file_path):
        print("loadLogFile: ", file_path)
        return self.logviewModel.loadLogFile(file_path, self.filterLog.colors())

    @Slot(list)
    def onLogFileLoaded(self, result):
        self._loadLogFileThread.quit()
        self._loadLogFileThread.wait()
        (parsed_log, parsed_dict) = result
        self._logDict = parsed_dict
        self.logviewModel.updateData(parsed_log)

        self.logViewReady = True
        self.loadLogFileCompleted.emit()

    # FILER **********************************************************
    @Slot()
    def openFilterDialog(self):
        file_dialog = QFileDialog()
        file_dialog.setNameFilter("Log files (*.json)")
        if file_dialog.exec():
            selected_file = file_dialog.selectedFiles()[0]
            self.filterLog.loadFilterFromJson(selected_file)
            self._originalFilters = self.filterLog.originalFilters()
            self._configs.saveConfig("filter", {"path": selected_file})
            self.refreshColorFilters()
            pass

    @Slot()
    def applyFilterChanges(self):
        print("applyFilterChanges")
        differences = []
        changedFilters = self.filterLog.displayedFilters

        print("Original Filters: ", self._originalFilters)
        print("Changed Filters: ", changedFilters)

        for i, (item1, item2) in enumerate(zip(self._originalFilters, changedFilters)):
            diff = {"id": item1["id"], "differences": {}}
            for key in item1.keys():
                if item1[key] != item2[key]:
                    diff["differences"][key] = item2[key]
            if diff["differences"]:
                differences.append(diff)

        for diff in differences:
            print(f"Index {diff['id']} has differences:")
            itemId = diff['id']
            for key, values in diff['differences'].items():
                print(f"  - {key}: changed to {values}")
                if key == "color":
                    self.processUpdateColorOnTable(itemId, values)
                elif key == "enabled":
                    self.processEnableFilterOnTable(itemId)
        self.filterLog.refreshRegex()
        self._originalFilters = self.filterLog.originalFilters()

    @Slot(int, str)
    def updateColorFilter(self, id, color):
        print("updateColorFilter id {} color {}".format(id, color))
        # update color in filter log
        self.filterLog.updateColorFilter(id, color)
        pass

    def processUpdateColorOnTable(self, id, color):
        # update color in log view
        loadedFilters = self.filterLog.loadedFilters()
        processName = loadedFilters[id]["processName"]
        self.logviewModel.setColorForProcessName(processName, color)

    def refreshColorFilters(self):
        colors = self.filterLog.colors()
        for processName in colors:
            self.logviewModel.setColorForProcessName(processName, colors[processName])

    @Slot(int,bool)
    def enableFilter(self, id, enabled):
        self.filterLog.enableFilter(id, enabled)

    def processEnableFilterOnTable(self, id):
        processName = self.filterLog.displayedFilters[id]["processName"]
        color       = self.filterLog.displayedFilters[id]["color"]
        self.logviewModel.setColorForProcessName(processName, color)
        pass

    @Slot(str, str, str)
    def addFilter(self, name, processName, color):
        self.filterLog.addFilter(name, processName, color)
        self.refreshColorFilters()
        pass

    @Slot(int, str, str, bool, str)
    def updateFilter(self, id, name, processName, enabled, color):
        self.filterLog.updateFilter(id, name, processName, enabled, color)
        self.refreshColorFilters()
        pass

    @Slot(int)
    def removeFilter(self, id):
        processName = self.filterLog.displayedFilters[id]["processName"]
        self.filterLog.removeFilter(id)
        self.logviewModel.resetColorForProcessName(processName)
        pass
    # SEARCH **********************************************************
    @Slot(str)
    def setSearchRegex(self, pattern):
        print("setSearchRegex: ", pattern)
        self._searchLog.searchRegex = pattern
        pass

    @Slot(bool)
    def setShowSearchResults(self, val):
        print("setShowSearchResults: ", val)
        self._searchLog.showSearchResults = val
        pass

    @Slot(str, result=str)
    def hightlightSearchResults(self, line):
        
        match = re.search(self._searchLog.searchRegex.pattern(), line, flags=re.IGNORECASE)
        if match and self._searchLog.searchRegex.pattern() != "":
            return re.sub(self._searchLog.searchRegex.pattern(), self.replace_with_span, line, flags=re.IGNORECASE)
        else:
            return line

    def replace_with_span(self, match):
        return f"<span style='background-color: rgba(97, 165, 77, 0.6)'>{match.group(0)}</span>"
    
    @Slot(str)
    def copyToClipboard(self, strCopy):
        pyperclip.copy(strCopy)

    def showNoti(self,message):
        self.showNotification.emit(message)

    def create(self):
        if not os.path.exists(ROOT_FOLDER):
            os.makedirs(ROOT_FOLDER)
            print(f"Folder '{ROOT_FOLDER}' created.")
        else:
            print(f"Folder '{ROOT_FOLDER}' already exists.")

        filter_path = os.path.join(ROOT_FOLDER, "filter.json")
        if not os.path.exists(filter_path):
            config_data = {
                "id": 0,
                "name": "HOME",
                "processName": "com.webos.app.home",
                "enabled": True,
                "color": "#d5b6b6"
            },
            with open(filter_path, 'a') as file:
                json.dump(config_data, file, indent=4)
            print(f"File '{filter_path}' created.")
        else:
            print(f"File '{filter_path}' already exists.")
        

        file_path = os.path.join(ROOT_FOLDER, "savedConfig.json")
        if not os.path.exists(file_path):
            config_data = {
                "filter": {
                    "path": os.path.join(ROOT_FOLDER, "filter.json")
                },
                "remote": {}
            }
            with open(file_path, 'a') as file:
                json.dump(config_data, file, indent=4)
            print(f"File '{file_path}' created.")
        else:
            print(f"File '{file_path}' already exists.")
    
    


