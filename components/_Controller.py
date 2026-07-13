# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Slot, QThread, Signal, Property, QModelIndex, QMimeData, QSortFilterProxyModel, QTimer
from PySide6.QtGui import QGuiApplication, QClipboard
from PySide6.QtWidgets import QFileDialog
from components._FilterLog import FilterLog
from components._LogViewModel import LogModel
from components._Worker import Worker
from components._SearchLog import SearchLog
from components._Configurations import Configurations
from components._RemoteDeviceManager import *
from components._Bookmark import Bookmark
from components._Toast import Toast, TOAST
from components._Helper import *
from components._SortFilterProxyModel import SortFilterProxyModel
from components._Defines import SOURCE_FILE, SOURCE_LOGCAT, SOURCE_SSH
import pyperclip
import re
import os
import json
import time
import atexit
import subprocess
import datetime
import asyncio
import asyncssh
import shutil
from collections import deque
from pathlib import Path
ROOT_FOLDER     = "C:/QtLogViewer"
LINE_NUMBER     = "line_number"
STREAM_FLAG     = "C:/QtLogViewer/stream.txt"

class Controller(QObject):
    showLoadingScreenChanged    = Signal()
    logViewReadyChanged         = Signal()
    loadLogFileCompleted        = Signal()
    detailsTextChanged          = Signal()
    highlightLineNumChanged     = Signal()
    showNotification            = Signal(str, arguments=["message"])
    themeChanged                = Signal()
    showLessColumnsChanged      = Signal()
    showLogColorsChanged        = Signal()
    logSourceChanged            = Signal()
    adbDevicesAvailableChanged  = Signal()
    scrcpyAutoShowChanged       = Signal()
    scrcpyRunningChanged        = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)
        self.create()
        self.filterLog              = FilterLog()
        self.logviewModel           = LogModel(logData=None)
        self.remoteDeviceManager    = RemoteDeviceManager()
        self.toast                  = Toast()
        self.helper                 = Helper()
        self.bookmark               = Bookmark()
        self._loadLogFileThread     = QThread()
        self._streamLogFileThread   = QThread()
        self._connRDeviceThread     = QThread()
        self._pingHostThread        = QThread()
        self._logcatThread          = QThread()
        self._logcatProcess         = None
        self._scrcpyProcess         = None
        self._showLoadingScreen     = False
        self._searchLog             = SearchLog()
        self._logViewReady          = False

        self._configs               = Configurations()
        self._configs.loadLastSavedConfig()
        self.remoteDeviceManager.deviceList = self._configs.getConfigs()["remote"]["devices"]

        filterPath = self._configs.getConfigs()["filter"]["path"]
        self.filterLog.create(filterPath)
        self._originalFilters = self.filterLog.originalFilters()
        self._logDict           = {}
        self._nextLineNum       = 0
        self._detailsText       = ""
        self._highlightLineNum  = -1
        self._logcatBuffer      = deque()
        self._streamBuffer      = deque()

        self._logcatFlushTimer = QTimer(self)
        self._logcatFlushTimer.setInterval(100)
        self._logcatFlushTimer.timeout.connect(self._flushLogcatBuffer)

        self._streamFlushTimer = QTimer(self)
        self._streamFlushTimer.setInterval(100)
        self._streamFlushTimer.timeout.connect(self._flushStreamBuffer)
        
        
        self._streamingFilePath = ""
        self._theme = self._configs.getConfigs().get("theme", "light")
        self._showLessColumns = self._configs.getConfigs().get("showLessColumns", False)
        self._showLogColors = self._configs.getConfigs().get("showLogColors", True)
        self._logSource = self._configs.getConfigs().get("logSource", SOURCE_LOGCAT)
        self._hasAdbDevices = False
        self._scrcpyAutoShowEnabled = self._configs.getConfigs().get("scrcpyAutoShow", False)
        self._scrcpyRunning = False

        search_configs = self._configs.getConfigs().get("search", {})
        current_query = search_configs.get("currentQuery", "")
        previous_query = search_configs.get("previousQuery", "")
        history = search_configs.get("history", [])
        if not isinstance(history, list):
            history = []
        self._searchLog.restoreSearchState(current_query, previous_query, history)

        self._adbCheckTimer = QTimer(self)
        self._adbCheckTimer.setInterval(3000)
        self._adbCheckTimer.timeout.connect(self.refreshAdbDeviceAvailability)
        self._adbCheckTimer.start()
        self.refreshAdbDeviceAvailability()
        
        atexit.register(self.cleanup)

        if self._logSource == SOURCE_LOGCAT:
            self.startLogcat()
        pass
    
    def cleanup(self):
        # Code to execute when the instance is destroyed
        print("Controller instance is being destroyed")
        self.remoteDeviceManager.streaming = False
        self._stopLogcatProcess()
        self._stopScrcpyProcess()
        self._stop_thread(self._loadLogFileThread)
        self._stop_thread(self._streamLogFileThread)
        self._stop_thread(self._logcatThread)
        
        if (self.remoteDeviceManager.connectedDevice):
            self.requestDisconnectFromDevice()
        
        
    def _stop_thread(self, thread):
        if thread.isRunning():
            thread.quit()
            thread.wait()

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

    @Property(str, notify=themeChanged)
    def theme(self):
        return self._theme

    @theme.setter
    def theme(self, val):
        print("theme: ", val)
        self._theme = val
        self._configs.saveConfig("theme", val)
        self.themeChanged.emit()

    @Property(bool, notify=showLessColumnsChanged)
    def showLessColumns(self):
        return self._showLessColumns
    
    @showLessColumns.setter
    def showLessColumns(self, val):
        self._showLessColumns = val
        self._configs.saveConfig("showLessColumns", val)
        self.showLessColumnsChanged.emit()

    @Property(bool, notify=showLogColorsChanged)
    def showLogColors(self):
        return self._showLogColors

    @showLogColors.setter
    def showLogColors(self, val):
        self._showLogColors = val
        self._configs.saveConfig("showLogColors", val)
        self.showLogColorsChanged.emit()

    @Property(str, notify=logSourceChanged)
    def logSource(self):
        return self._logSource

    @logSource.setter
    def logSource(self, val):
        if self._logSource == val:
            return
        # Stop current source
        if self._logSource == SOURCE_LOGCAT:
            self._stopLogcatProcess()
        elif self._logSource == SOURCE_SSH:
            self.remoteDeviceManager.streaming = False
        self._logSource = val
        self._configs.saveConfig("logSource", val)
        self.logSourceChanged.emit()
        # Auto-start new source
        if val == SOURCE_LOGCAT:
            self.startLogcat()

    @Slot(str)
    def setLogSource(self, source):
        self.logSource = source

    @Property(bool, notify=adbDevicesAvailableChanged)
    def hasAdbDevices(self):
        return self._hasAdbDevices

    @hasAdbDevices.setter
    def hasAdbDevices(self, value):
        if self._hasAdbDevices == value:
            return
        self._hasAdbDevices = value
        self.adbDevicesAvailableChanged.emit()

    @Property(bool, notify=scrcpyAutoShowChanged)
    def scrcpyAutoShowEnabled(self):
        return self._scrcpyAutoShowEnabled

    @scrcpyAutoShowEnabled.setter
    def scrcpyAutoShowEnabled(self, value):
        if self._scrcpyAutoShowEnabled == value:
            return
        self._scrcpyAutoShowEnabled = value
        self._configs.saveConfig("scrcpyAutoShow", value)
        self.scrcpyAutoShowChanged.emit()

    @Property(bool, notify=scrcpyRunningChanged)
    def scrcpyRunning(self):
        return self._scrcpyRunning

    @scrcpyRunning.setter
    def scrcpyRunning(self, value):
        if self._scrcpyRunning == value:
            return
        self._scrcpyRunning = value
        self.scrcpyRunningChanged.emit()

    def _resolveScrcpyExecutable(self):
        candidate_paths = [
            Path(__file__).resolve().parent.parent / "assets" / "software" / "scrcpy" / "scrcpy.exe",
            Path(__file__).resolve().parent.parent / "assets" / "software" / "scrcpy.exe",
            Path(__file__).resolve().parent.parent / "scrcpy" / "scrcpy.exe",
            Path(__file__).resolve().parent.parent / "scrcpy.exe",
            Path(ROOT_FOLDER) / "scrcpy" / "scrcpy.exe",
            Path(ROOT_FOLDER) / "scrcpy.exe",
        ]

        for candidate in candidate_paths:
            if candidate.exists():
                return candidate

        which_result = shutil.which("scrcpy")
        if which_result:
            return Path(which_result)

        return None

    def _stopScrcpyProcess(self):
        if self._scrcpyProcess and self._scrcpyProcess.poll() is None:
            self._scrcpyProcess.terminate()
            try:
                self._scrcpyProcess.wait(timeout=2)
            except Exception:
                self._scrcpyProcess.kill()
        self._scrcpyProcess = None
        self.scrcpyRunning = False

    def _syncScrcpyState(self):
        is_running = self._scrcpyProcess is not None and self._scrcpyProcess.poll() is None
        self.scrcpyRunning = is_running

    def _runAdbShell(self, shell_args):
        try:
            result = subprocess.run(
                ["adb", "shell", *shell_args],
                capture_output=True,
                text=True,
                timeout=3,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            if result.returncode != 0:
                return ""
            return (result.stdout or "").strip()
        except Exception:
            return ""

    def _getCurrentAndroidPackage(self):
        dumpsys_window = self._runAdbShell(["dumpsys", "window"])
        if dumpsys_window:
            package_match = re.search(r"mCurrentFocus.*\s([A-Za-z0-9_.$]+)/", dumpsys_window)
            if package_match:
                return package_match.group(1)

        dumpsys_activity = self._runAdbShell(["dumpsys", "activity", "activities"])
        if dumpsys_activity:
            package_match = re.search(r"mResumedActivity.*\s([A-Za-z0-9_.$]+)/", dumpsys_activity)
            if package_match:
                return package_match.group(1)

        return ""

    def _getAndroidAppName(self, package_name):
        if not package_name:
            return ""
        package_dump = self._runAdbShell(["dumpsys", "package", package_name])
        if not package_dump:
            return ""

        # dumpsys may expose labels in multiple variants depending on Android version.
        name_match = re.search(r"application-label(?:-[^:]+)?:'([^']+)'", package_dump)
        if name_match:
            return name_match.group(1).strip()

        name_match = re.search(r'application-label(?:-[^:]+)?:"([^"]+)"', package_dump)
        if name_match:
            return name_match.group(1).strip()

        return ""

    @Slot()
    def openScrcpy(self):
        self._syncScrcpyState()

        if self.scrcpyRunning:
            self._stopScrcpyProcess()
            self.scrcpyAutoShowEnabled = False
            self.toast.show(TOAST.INFO, "SCRCPY stopped")
            return

        self.refreshAdbDeviceAvailability()

        if not self._hasAdbDevices:
            self.toast.show(TOAST.ERROR, "No Android device found for SCRCPY")
            return

        scrcpy_path = self._resolveScrcpyExecutable()
        if scrcpy_path is None:
            self.toast.show(TOAST.ERROR, "scrcpy.exe not found")
            return

        self.scrcpyAutoShowEnabled = True

        try:
            self._scrcpyProcess = subprocess.Popen(
                [str(scrcpy_path)],
                cwd=str(scrcpy_path.parent),
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            self.scrcpyRunning = True
        except Exception as e:
            self.toast.show(TOAST.ERROR, f"Failed to start SCRCPY: {e}")
            self.scrcpyRunning = False
            return

        self.toast.show(TOAST.INFO, "SCRCPY started")

    @Slot(str, str, result=bool)
    def checkCurrentAndroidFilter(self, filter_name, package_name):
        self.refreshAdbDeviceAvailability()
        if not self._hasAdbDevices:
            self.toast.show(TOAST.ERROR, "No Android device found")
            return False

        current_package = self._getCurrentAndroidPackage()
        if not current_package:
            self.toast.show(TOAST.WARNING, "Cannot detect current Android app")
            return False

        current_app_name = self._getAndroidAppName(current_package)
        normalized_filter_name = (filter_name or "").strip().lower()
        normalized_package_name = (package_name or "").strip().lower()
        normalized_current_name = (current_app_name or "").strip().lower()
        normalized_current_package = current_package.strip().lower()

        name_match = bool(normalized_filter_name) and normalized_filter_name == normalized_current_name
        package_match = bool(normalized_package_name) and normalized_package_name == normalized_current_package
        is_matched = name_match or package_match

        if is_matched:
            self.toast.show(
                TOAST.INFO,
                f"Filter matched: {current_app_name or current_package} ({current_package})"
            )
        else:
            self.toast.show(
                TOAST.WARNING,
                f"Filter not matched: {current_app_name or 'Unknown'} ({current_package})"
            )

        return is_matched

    @Slot()
    def refreshAdbDeviceAvailability(self):
        self._syncScrcpyState()
        was_connected = self._hasAdbDevices

        if not shutil.which("adb"):
            self.hasAdbDevices = False
            if was_connected and self._logSource == SOURCE_LOGCAT:
                self.logSource = SOURCE_FILE
            return

        try:
            result = subprocess.run(
                ["adb", "devices"],
                capture_output=True,
                text=True,
                timeout=2,
                creationflags=subprocess.CREATE_NO_WINDOW
            )
            lines = result.stdout.splitlines()[1:]
            has_devices = any("\tdevice" in line for line in lines)
            self.hasAdbDevices = has_devices
            if not was_connected and has_devices and self._scrcpyAutoShowEnabled:
                self.openScrcpy()
            if was_connected and not has_devices and self._logSource == SOURCE_LOGCAT:
                self.logSource = SOURCE_FILE
        except Exception:
            self.hasAdbDevices = False
            if was_connected and self._logSource == SOURCE_LOGCAT:
                self.logSource = SOURCE_FILE

    @Slot()
    def startLogcat(self):
        print("startLogcat")
        self.refreshAdbDeviceAvailability()
        if self._logcatThread.isRunning():
            return
        if not shutil.which("adb"):
            self.showNotification.emit("adb not found in PATH. Please install Android SDK Platform Tools.")
            self._logSource = SOURCE_FILE
            self._configs.saveConfig("logSource", SOURCE_FILE)
            self.logSourceChanged.emit()
            return
        self.logviewModel.updateData([])
        self._logDict = {}
        self._nextLineNum = 0
        self._logcatBuffer.clear()
        self.logViewReady = True
        self.helper.autoScrollDown = True
        self._logcatFlushTimer.start()
        self._logcatWorker = Worker(self._runLogcat)
        self._logcatWorker.moveToThread(self._logcatThread)
        self._logcatWorker.taskCompleted.connect(self._onLogcatStopped)
        self._logcatThread.started.connect(self._logcatWorker.run)
        self._logcatThread.start()
        self.toast.show(TOAST.INFO, "Reading Android logcat...")

    @Slot()
    def stopLogcat(self):
        print("stopLogcat")
        self._stopLogcatProcess()
        self.helper.autoScrollDown = False

    def _stopLogcatProcess(self):
        self._logcatFlushTimer.stop()
        if self._logcatProcess and self._logcatProcess.poll() is None:
            self._logcatProcess.terminate()
            try:
                self._logcatProcess.wait(timeout=3)
            except Exception:
                self._logcatProcess.kill()
        self._logcatProcess = None
        self._flushLogcatBuffer()  # drain remaining
        if self._logcatThread.isRunning():
            self._logcatThread.quit()
            self._logcatThread.wait()

    def _runLogcat(self):
        try:
            self._logcatProcess = subprocess.Popen(
                ["adb", "logcat", "-v", "threadtime"],
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                text=True,
                encoding="utf-8",
                errors="replace"
            )
            colors = self.filterLog.colors()
            for line in self._logcatProcess.stdout:
                if self._logcatProcess.poll() is not None:
                    break
                result = self.logviewModel.processLineDataLogcat(line.rstrip("\n"), colors)
                if result[0]:
                    self._logcatBuffer.append(result[1])
        except Exception as e:
            print(f"logcat error: {e}")

    def _flushLogcatBuffer(self):
        if not self._logcatBuffer:
            return
        entries = []
        while self._logcatBuffer:
            entries.append(self._logcatBuffer.popleft())
        self._assignLineNums(entries)
        self._batchInsert(entries)

    def _assignLineNums(self, entries):
        """Assign sequential line numbers and update _logDict. O(batch_size)."""
        for entry in entries:
            entry[LINE_NUMBER] = self._nextLineNum
            self._logDict[self._nextLineNum] = entry
            self._nextLineNum += 1

    def _batchInsert(self, entries):
        """Insert entries, trimming oldest rows if over MAX_LOG_ROWS."""
        from components._LogViewModel import MAX_LOG_ROWS
        current = self.logviewModel.rowCount()
        total = current + len(entries)
        if total > MAX_LOG_ROWS:
            excess = total - MAX_LOG_ROWS
            self.logviewModel.trimRows(excess)
        self.logviewModel.addRows(entries)

    def _onLogcatStopped(self, result):
        self._logcatThread.quit()
        self._logcatThread.wait()
        print("logcat stopped")

    @Slot(int)
    def showLogDetails(self, line):
        print("showLogDetails: ", line)
        logline = self._logDict[line]
        self.detailsText = self.logviewModel.format_log_line(logline)

    def getLogViewModel(self):
        return self.logviewModel
    
    def getFilterLog(self):
        return self.filterLog
    
    def getSearchLog(self):
        return self._searchLog
    
    def getRemoteDeviceManager(self):
        return self.remoteDeviceManager
    
    def getToast(self):
        return self.toast
    
    def getHelper(self):
        return self.helper
    
    def getBookmark(self):
        return self.bookmark

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

    @Slot()
    def saveLogFile(self):
        print("saveLogFile")
        # Generate default filename with date_time format
        current_time = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        default_filename = f"{current_time}.log"
        
        file_dialog = QFileDialog()
        file_dialog.setNameFilter("Log files (*.log);;All files (*.*)")
        file_dialog.setDefaultSuffix("log")
        file_dialog.setAcceptMode(QFileDialog.AcceptSave)
        file_dialog.selectFile(default_filename)
        
        if file_dialog.exec():
            selected_file = file_dialog.selectedFiles()[0]
            self.worker = Worker(self._writeLogToFile, selected_file)
            self.worker.moveToThread(self._loadLogFileThread)
            self.worker.taskCompleted.connect(self.onLogFileSaved)
            self._loadLogFileThread.started.connect(self.worker.run)
            self._loadLogFileThread.start()

    def _writeLogToFile(self, file_path):
        """Write the current log data to a file"""
        try:
            log_data = self.logviewModel._log_data
            with open(file_path, 'w', encoding='utf-8') as file:
                for log_entry in log_data:
                    log_line = self.logviewModel.format_log_line(log_entry) + "\n"
                    file.write(log_line)
            return True
        except Exception as e:
            print(f"Error saving log file: {e}")
            return False

    @Slot(bool)
    def onLogFileSaved(self, success):
        self._loadLogFileThread.quit()
        self._loadLogFileThread.wait()
        if success:
            self.toast.show(TOAST.INFO, "Log file saved successfully")
        else:
            self.toast.show(TOAST.ERROR, "Failed to save log file")

    def loadLogFile(self, file_path):
        print("loadLogFile: ", file_path)
        return self.logviewModel.loadLogFile(file_path, self.filterLog.colors())

    @Slot(list)
    def onLogFileLoaded(self, result):
        self._loadLogFileThread.quit()
        self._loadLogFileThread.wait()
        (parsed_log, parsed_dict) = result
        self._logDict = parsed_dict
        self._nextLineNum = len(parsed_dict)
        self.logviewModel.updateData(parsed_log)
        self.logViewReady = True
        self.loadLogFileCompleted.emit()
        
    def loadStreamingLogFile(self, file_path):
        print("loadStreamingLogFile: ", file_path)
        self.worker = Worker(self.loadLogFile, file_path)
        self.worker.moveToThread(self._loadLogFileThread)
        self.worker.taskCompleted.connect(self.onStreamingLogFileLoaded)
        self._loadLogFileThread.started.connect(self.worker.run)
        self._loadLogFileThread.start()
        pass
    
    @Slot(list)
    def onStreamingLogFileLoaded(self, result):
        self._loadLogFileThread.quit()
        self._loadLogFileThread.wait()
        (parsed_log, parsed_dict) = result
        self._logDict = parsed_dict
        self._nextLineNum = len(parsed_dict)
        self.logviewModel.updateData(parsed_log)
        self.logViewReady = True
        self.loadLogFileCompleted.emit()
        self.startStreamingLogFile()
    
    def startStreamingLogFile(self):
        print("startStreamingLogFile: ", self._streamingFilePath)
        self._streamFlushTimer.start()
        self.worker = None
        self.worker = Worker(self.streamFile, self._streamingFilePath)
        self.worker.moveToThread(self._streamLogFileThread)
        self.worker.taskCompleted.connect(self.onStreamFileStopped)
        self._streamLogFileThread.started.connect(self.worker.run)
        self._streamLogFileThread.start()

    def streamFile(self, file_path):
        print("streamFile: ", file_path)
        try:
            self.remoteDeviceManager.streaming = True
            with open(file_path, 'r', encoding='utf-8') as file:
                file.seek(0, os.SEEK_END)
                while self.remoteDeviceManager.streaming:
                    lines = []
                    while True:
                        new_line = file.readline()
                        if new_line:
                            lines.append(new_line)
                        else:
                            break
                    if lines:
                        colors = self.filterLog.colors()
                        for line in lines:
                            result = self.logviewModel.processLineData(line, colors)
                            if result[0]:
                                self._streamBuffer.append(result[1])
                    else:
                        time.sleep(0.1)
        except Exception as e:
            print(f"Error while watching log file: {e}")

    @Slot()
    def onStreamFileStopped(self):
        print("onStreamFileStopped")
        self.remoteDeviceManager.streaming = False
        self._streamFlushTimer.stop()
        self._flushStreamBuffer()  # drain remaining
        self._streamLogFileThread.quit()
        self._streamLogFileThread.wait()

    def _flushStreamBuffer(self):
        if not self._streamBuffer:
            return
        entries = []
        while self._streamBuffer:
            entries.append(self._streamBuffer.popleft())
        self._assignLineNums(entries)
        self._batchInsert(entries)

    def addLineLog(self, line):
        """Legacy single-line insert (kept for compatibility)."""
        (isSuccess, log_entry) = self.logviewModel.processLineData(line, self.filterLog.colors())
        if isSuccess:
            log_entry[LINE_NUMBER] = self._nextLineNum
            self._logDict[self._nextLineNum] = log_entry
            self._nextLineNum += 1
            self.logviewModel.addRow(log_entry)
        else:
            print(f"Error processing line: {line}")

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
        self.filterLog.refreshFilterProps()
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
        tag = loadedFilters[id]["tag"]
        self.logviewModel.setColorForProcessName(tag, color)

    def refreshColorFilters(self):
        colors = self.filterLog.colors()
        for tag in colors:
            self.logviewModel.setColorForProcessName(tag, colors[tag])

    @Slot(int,bool)
    def enableFilter(self, id, enabled):
        self.filterLog.enableFilter(id, enabled)

    def processEnableFilterOnTable(self, id):
        filter = None
        for f in self.filterLog.displayedFilters:
            if f["id"] == id:
                filter = f
                break
        
        if filter is None:
            return
        
        tag   = filter["tag"]
        color = filter["color"]
        self.logviewModel.setColorForProcessName(tag, color)
        pass

    @Slot(str, str, str, str, str)
    def addFilter(self, name, tag, pid, tid, color):
        self.filterLog.addFilter(name, tag, pid, tid, color)
        self.refreshColorFilters()
        pass

    @Slot(int, str, str, str, str, bool, str)
    def updateFilter(self, id, name, tag, pid, tid, enabled, color):
        self.filterLog.updateFilter(id, name, tag, pid, tid, enabled, color)
        self.refreshColorFilters()
        pass

    @Slot(int)
    def removeFilter(self, id):
        tag = None
        for f in self.filterLog.displayedFilters:
            if f["id"] == id:
                tag = f["tag"]
                break
        
        self.filterLog.removeFilter(id)
        self.logviewModel.resetColorForProcessName(tag)
        pass
    # SEARCH **********************************************************
    def _persistSearchState(self):
        self._configs.saveConfig("search", {
            "currentQuery": self._searchLog.searchRegex.pattern(),
            "previousQuery": self._searchLog.previousSearchQuery,
            "history": self._searchLog.searchHistory,
        })

    @Slot(str)
    def setSearchRegex(self, pattern):
        print("setSearchRegex: ", pattern)
        self._searchLog.searchRegex = pattern
        self._persistSearchState()
        pass

    @Slot(str)
    def executeSearch(self, pattern):
        print("executeSearch: ", pattern)
        self._searchLog.applySearchQuery(pattern)
        self._searchLog.showSearchResults = bool((pattern or "").strip())
        self._persistSearchState()

    @Slot(bool)
    def setShowSearchResults(self, val):
        print("setShowSearchResults: ", val)
        self._searchLog.showSearchResults = val
        pass

    @Slot(result=str)
    def getCurrentSearchQuery(self):
        return self._searchLog.searchRegex.pattern()

    @Slot(result=str)
    def getPreviousSearchQuery(self):
        return self._searchLog.previousSearchQuery

    @Slot(str, result=str)
    def getSearchHistoryHint(self, prefix):
        return self._searchLog.getSearchHint(prefix)

    @Slot(str, result=str)
    def hightlightSearchResults(self, line):
        if not self._searchLog.searchWords or not self._searchLog.showSearchResults:
            return line
        
        result_line = line
        
        # Highlight từng từ khóa với màu khác nhau
        for i, word in enumerate(self._searchLog.searchWords):
            if word:  # Kiểm tra từ khóa không rỗng
                color = self._searchLog.getColorForIndex(i)
                # Escape special regex characters để tránh lỗi
                escaped_word = re.escape(word)
                pattern = re.compile(escaped_word, re.IGNORECASE)
                result_line = pattern.sub(
                    lambda match: f"<span style='background-color: {color}'>{match.group(0)}</span>",
                    result_line
                )
        
        return result_line

    def replace_with_span_color(self, match, color):
        """Helper function để tạo span với màu cụ thể"""
        return f"<span style='background-color: {color}'>{match.group(0)}</span>"
    
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
                "tag": "com.webos.app.home",
                "pid": "",
                "tid": "",
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
                "theme": "light",
                "showLessColumns": False,
                "search": {
                    "currentQuery": "",
                    "previousQuery": "",
                    "history": []
                },
                "remote": {
                    "devices": [
                        {
                            "SSHGateway_IP": "localhost",
                            "SSHGateway_Port": 50222,
                            "SSHGateway_User": "root",
                            "host": "10.1.3.200",
                            "id": 1,
                            "isUseSSHGateway": True,
                            "name": "POIP1",
                            "port": 22,
                            "username": "root",
                            "remoteLogPath": "/host/log/messages"
                        },
                        {
                            "SSHGateway_IP": "192.168.105.100",
                            "SSHGateway_Port": 22,
                            "SSHGateway_User": "root",
                            "host": "10.1.3.200",
                            "id": 2,
                            "isUseSSHGateway": True,
                            "name": "CCIC",
                            "port": 22,
                            "username": "root",
                            "remoteLogPath": "/var/log/messages"
                        }
                    ]
                },
                "logSource": "logcat"
            }
            with open(file_path, 'a') as file:
                json.dump(config_data, file, indent=4)
            print(f"File '{file_path}' created.")
        else:
            print(f"File '{file_path}' already exists.")
    
    # REMOTE DEVICE MANAGER **********************************************************
    async def run_ssh_command_via_jump(self, host, port, username, password, jump_host, jump_port, jump_user, jump_password, command):
        try:
            # Connect to the jump host first
            async with asyncssh.connect(jump_host, port=jump_port, username=jump_user, password=jump_password, known_hosts=None) as jump_conn:
                # Use the jump host as a proxy for the final connection
                async with jump_conn.connect_ssh(host, port=port, username=username, password=password, known_hosts=None) as conn:
                    return await conn.run(command)
        except (OSError, asyncssh.Error) as e:
            print(f"SSH connection failed: {e}")
            
    async def run_ssh_command(self, host, port, username, password, command):
        try:
            # Connect to the SSH server
            async with asyncssh.connect(host, port=port, username=username, password=password) as conn:
                # Run the command on the remote server
                result = await conn.run(command, check=True)
                return result
        except (OSError, asyncssh.Error) as e:
            print(f"SSH connection failed: {e}")
    
    @Slot(int)
    def requestConnectToDevice(self, id):
        print("requestConnectToDevice id: ", id)
        self.remoteDeviceManager.connectedDevice = None
        device = next((x for x in self.remoteDeviceManager.deviceList if x['id'] == id), None)
    
        if device is None:
            print(f"Device with id {id} not found.")
            return
        
        print("Connecting to device: ", device)
        self.worker = None
        self.worker = Worker(self.startConnectToDevice, device)
        self.worker.moveToThread(self._connRDeviceThread)
        self.worker.taskCompleted.connect(self.connectToDeviceDone)
        self._connRDeviceThread.started.connect(self.worker.run)
        self._connRDeviceThread.start()
        # self.worker = None
        
    @Slot(bool)
    def connectToDeviceDone(self, success):
        print("connectToDeviceDone")
        self.remoteDeviceManager.connectProcessStatus   = IDLE
        self.remoteDeviceManager.connectingDevice       = None
        self._connRDeviceThread.quit()
        self._connRDeviceThread.wait()
        
        if success:
            self.toast.show(TOAST.INFO, "Device connected successfully")
            timer = QTimer(self)
            timer.setInterval(1000)
            timer.setSingleShot(True)
            timer.timeout.connect(lambda: {
                self.loadStreamingLogFile(self._streamingFilePath)
            })
            timer.start()
            
            self.worker = Worker(self.startPing)
            self.worker.moveToThread(self._pingHostThread)
            self.worker.taskCompleted.connect(self.pingConnectedDeviceDone)
            self._pingHostThread.started.connect(self.worker.run)
            self._pingHostThread.start()
            
        else:
            self.toast.show(TOAST.ERROR, "Failed to connect to device")
        pass
    
    def startPing(self):
        print("startPing")
        asyncio.run(self.pingConnectedDevice())
        
    @Slot()
    def pingConnectedDeviceDone(self):
        print("pingConnectedDeviceDone")
        self._pingHostThread.quit()
        self._pingHostThread.wait()
        pass
        
    def pingInterrupted(self):
        if self.remoteDeviceManager.hasConnection:
            self.toast.show(TOAST.ERROR, "Interrupted. Check your connection!!!")
            self.cleanUpWhenInterrupt()
        pass
    
    async def pingViaJumpHost(self, jump_host, target_host, command):
        try:
            # Connect to the jump host first
            async with asyncssh.connect(jump_host["host"], port=jump_host["port"], username=jump_host["user"], password=jump_host["password"], known_hosts=None) as jump_conn:
                # Use the jump host as a proxy for the final connection
                async with jump_conn.connect_ssh(target_host["host"], port=target_host["port"], username=target_host["user"], password=target_host["password"], known_hosts=None) as conn:
                    process = await conn.create_process(command)
                    try:
                        while self.remoteDeviceManager.hasConnection:
                            if jump_conn.is_closed():
                                raise asyncio.CancelledError()
                            await asyncio.sleep(5)
                    except asyncio.CancelledError:
                        print("Cancelled")
                        process.terminate()
                        await process.wait()
                        self.pingInterrupted()
                        raise
                    finally:
                        print("Cancelled")
                        process.terminate()
                        await process.wait()
                        self.pingInterrupted()
        except (OSError, asyncssh.Error) as e:
            print(f"SSH connection failed: {e}")
    
    async def pingHost(self, host, port, username, password, command):
        try:
            # Connect to the SSH server
            async with asyncssh.connect(host, port=port, username=username, password=password) as conn:
                process = await conn.create_process(command)
                try:
                    while self.remoteDeviceManager.hasConnection:
                        await process.stdout.readline()
                        await asyncio.sleep(5)
                except asyncio.CancelledError:
                    process.terminate()
                    await process.wait()
                    raise
                finally:
                    process.terminate()
                    await process.wait()
                    self.pingInterrupted()
        except (OSError, asyncssh.Error) as e:
            print(f"SSH connection failed")
    
    async def monitor_ssh_connection(self, ssh_client):
        while True:
            if ssh_client.done() or ssh_client.cancelled():
                print("SSH connection lost")
                break
            await asyncio.sleep(3)

    async def pingConnectedDevice(self):
        device = self.remoteDeviceManager.connectedDevice
        if device is None:
            return False
        
        user = device["username"]
        host = device["host"]
        port = device["port"]
        isUseSSHGateway = device["isUseSSHGateway"]
        
        jump_host = {
            "host": device["SSHGateway_IP"],
            "port": device["SSHGateway_Port"],
            "user": device["SSHGateway_User"],
            "password": "root"
        }
        
        target_host = {
            "host": device["host"],
            "port": device["port"],
            "user": device["username"],
            "password": "root"
        }
        
        ssh_cmd = "ls -l"
        result = None
        
        command_task = None
        monitor_task = None

        if not isUseSSHGateway:
            command_task = asyncio.create_task(self.pingHost(host,port, user, "root", ssh_cmd))
        else:
            command_task = asyncio.create_task(self.pingViaJumpHost(jump_host, target_host, ssh_cmd))
        
        monitor_task = asyncio.create_task(self.monitor_ssh_connection(command_task))
        
        done, pending = await asyncio.wait(
            [command_task, monitor_task],
            return_when=asyncio.FIRST_COMPLETED
        )

        for task in pending:
            task.cancel()

        await asyncio.gather(*pending, return_exceptions=True)
    def startConnectToDevice(self, device):
        print("startConnectToDevice: ", device)
        #ping ssh server
        user            = device["username"]
        host            = device["host"]
        port            = device["port"]
        remotePath      = device["remoteLogPath"]
        isUseSSHGateway = device["isUseSSHGateway"]
        
        current_time = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        temp_local_log_file = "{}/{}.log".format(ROOT_FOLDER, current_time)
        ssh_cmd = "ls -l"
        result = None
        if not isUseSSHGateway:
            self.remoteDeviceManager.connectProcessStatus   = INPROGRESS
            self.remoteDeviceManager.connectingDevice       = dict(device)
            result = asyncio.run(self.run_ssh_command(host, port, user, "", ssh_cmd))
        else:
            ssh_gateway_ip      = device["SSHGateway_IP"]
            ssh_gateway_port    = device["SSHGateway_Port"]
            ssh_gateway_user    = device["SSHGateway_User"]
            # print("device: ", dict(device))
            self.remoteDeviceManager.connectProcessStatus   = INPROGRESS
            self.remoteDeviceManager.connectingDevice       = dict(device)
            result = asyncio.run(self.run_ssh_command_via_jump(host, port, user, "root", ssh_gateway_ip, ssh_gateway_port, ssh_gateway_user, "root", ssh_cmd))
        
        success = False
        if result:
            self.remove_line_with_ip(ssh_gateway_ip)
            self.remove_line_with_ip(host)
            self.remoteDeviceManager.connectProcessStatus   = SUCCESS
            self.remoteDeviceManager.connectedDevice        = dict(device)
            ssh_gateway = "{}@{}".format(ssh_gateway_user, ssh_gateway_ip)
            ssh_host = "{}@{}".format(user, host)
            bash_path = Path(__file__).resolve().parent.parent / 'scripts/'
            print("bash_path: ", bash_path)
            script_name = "start_stream.sh"
            self._streamingFilePath = temp_local_log_file
            
            command = [
                'C:\\Program Files\\Git\\bin\\bash.exe',  # Path to bash
                '-c',  # Bash flag to execute the following command
                f'cd "{bash_path}" && ./{script_name} {ssh_host} {remotePath} {temp_local_log_file} {ssh_gateway} {str(ssh_gateway_port)}'
            ]
            
            subprocess.call(command)
            # subprocess.call([
            #     "cd {} && ".format(bash_path),
            #     'C:\Program Files\Git\\bin\\bash.exe', script_name,
            #     ssh_host,
            #     remotePath,
            #     temp_local_log_file,
            #     ssh_gateway,
            #     str(ssh_gateway_port)
            # ],
            # )
            # try:
            #     cmd = "cd {} && bash.exe {} {} {} {} {} {}".format(bash_path, script_name, ssh_host, remotePath, temp_local_log_file, ssh_gateway, str(ssh_gateway_port))
            #     os.system(cmd)
            # except Exception as e:
            #     print(f"Error while connecting to device: {e}")
            #     success = False
            
            success = True
        else:
            print("error")
            self.remoteDeviceManager.connectProcessStatus   = FAILED
        return success
            
    @Slot()
    def requestDisconnectFromDevice(self):
        print("requestDisconnectFromDevice")
        self._connRDeviceThread.quit()
        self._connRDeviceThread.wait()
        try:
            bash_path = Path(__file__).resolve().parent.parent / 'scripts/stop_stream.sh'
            subprocess.run([
                    'C:\Program Files\Git\\bin\\bash.exe', bash_path
            ],
            creationflags=subprocess.CREATE_NO_WINDOW)
            self.remoteDeviceManager.connectedDevice        = None
            self.remoteDeviceManager.connectProcessStatus   = IDLE
            self.remoteDeviceManager.connectingDevice       = None
            self.remoteDeviceManager.streaming              = False
            self.toast.show(TOAST.INFO, "Disconnected")
        except Exception as e:
            print(f"Error while disconnecting from device: {e}")
            return
        pass
    
    def cleanUpWhenInterrupt(self):
        print("cleanUpWhenInterrupt")
        try:
            bash_path = os.path.join(os.path.dirname(__file__), '../scripts/stop_stream.sh')
            subprocess.run([
                    'C:\Program Files\Git\\bin\\bash.exe', bash_path
            ],
            creationflags=subprocess.CREATE_NO_WINDOW)
            self.remoteDeviceManager.connectedDevice        = None
            self.remoteDeviceManager.connectProcessStatus   = IDLE
            self.remoteDeviceManager.connectingDevice       = None
            self.remoteDeviceManager.streaming              = False
        except Exception as e:
            print(f"Error while disconnecting from device: {e}")
            return
        pass

    @Slot()
    def startStreaming(self):
        print("startStreaming")
        self._streamLogFileThread.quit()
        self.startStreamingLogFile()
        self.toast.show(TOAST.INFO, "Start live log debugging")
        pass
    
    @Slot()
    def stopStreaming(self):
        print("stopStreaming")
        self.remoteDeviceManager.streaming = False
        self.toast.show(TOAST.INFO, "Stop live log debugging")
        pass
    
    @Slot()
    def clearLog(self):
        print("clearLog")
        self._logcatBuffer.clear()
        self._streamBuffer.clear()
        self.logviewModel.updateData([])
        self._logDict = {}
        self._nextLineNum = 0
        try:
            subprocess.run(
                ["adb", "logcat", "-c"],
                capture_output=True,
                timeout=3,
                creationflags=subprocess.CREATE_NO_WINDOW
            )
        except Exception as e:
            print(f"adb logcat -c failed: {e}")
        self.toast.show(TOAST.INFO, "Log cleared")
    
    @Slot(dict, int)
    def changeRemoteDeviceInfo(self, device, index):
        device["SSHGateway_Port"] = int(device["SSHGateway_Port"])
        device["port"] = int(device["port"])
        print("changeRemoteDeviceInfo: ", device)
        deviceList = self.remoteDeviceManager.deviceList
        deviceList[index] = device
        self.remoteDeviceManager.deviceList = deviceList
        self._configs.saveConfig("remote", {"devices": self.remoteDeviceManager.deviceList})
        pass
    
    @Slot(dict)
    def addRemoteDevice(self, device):
        device["SSHGateway_Port"] = int(device["SSHGateway_Port"])
        device["port"] = int(device["port"])

        if not device["remoteLogPath"].startswith('/'):
            device["remoteLogPath"] = '/' + device["remoteLogPath"]
        
        print("addRemoteDevice: ", device)
        deviceList = self.remoteDeviceManager.deviceList
        deviceList.append(device)
        self.remoteDeviceManager.deviceList = deviceList
        self._configs.saveConfig("remote", {"devices": self.remoteDeviceManager.deviceList})
        pass
    
    @Slot(int)
    def removeRemoteDevice(self, id):
        print("addRemoteDevice: ", id)
        deviceList = self.remoteDeviceManager.deviceList
        deviceList.remove(next((x for x in deviceList if x['id'] == id), None))
        self.remoteDeviceManager.deviceList = deviceList
        self._configs.saveConfig("remote", {"devices": self.remoteDeviceManager.deviceList})
        pass
    
    @Slot(int, result=str)
    def getLogMessage(self, lineNum):
        return self._logDict[lineNum]["message"]
    
    
    def remove_line_with_ip(self, ip_address):
        known_hosts_path = os.path.expanduser("~/.ssh/known_hosts")
        # Check if the known_hosts file exists
        if not os.path.exists(known_hosts_path):
            print(f"Error: {known_hosts_path} does not exist.")
            return

        # Read the contents of the known_hosts file
        with open(known_hosts_path, "r") as file:
            lines = file.readlines()

        # Remove lines that contain the specified IP address
        updated_lines = [line for line in lines if ip_address not in line]

        # Write the updated content back to the known_hosts file
        with open(known_hosts_path, "w") as file:
            file.writelines(updated_lines)

        print(f"Removed lines containing IP: {ip_address} from {known_hosts_path}")
