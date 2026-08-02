# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Slot, QThread, Signal, Property, QModelIndex, QMimeData, QTimer
from PySide6.QtGui import QGuiApplication, QClipboard, QColor
from PySide6.QtWidgets import QFileDialog
from components._FilterLog import FilterLog
from components._LogViewModel import LogModel, LINE_NUMBER
from components._Worker import Worker
from components._SearchLog import SearchLog
from components._Configurations import Configurations
from components._Bookmark import Bookmark
from components._Toast import Toast, TOAST
from components._Helper import *
from components._SortFilterProxyModel import SortFilterProxyModel
from components._Defines import SOURCE_FILE, SOURCE_LOGCAT, WRITE_CHUNK_SIZE, IO_BUFFER_SIZE, MAX_LOGCAT_FILE_SIZE
import pyperclip
import re
import os
import json
import time
import atexit
import subprocess
import datetime
import shutil
from collections import deque
from pathlib import Path
ROOT_FOLDER     = "D:/CatchyLog"
STREAM_FLAG     = "D:/CatchyLog/stream.txt"

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
    loadProgressChanged         = Signal()
    loadingFileNameChanged      = Signal()
    isLoadingChanged            = Signal()
    isSavingChanged             = Signal()
    saveProgressChanged         = Signal()
    openedFileNameChanged       = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)
        self.create()
        self.filterLog              = FilterLog()
        self.logviewModel           = LogModel(logData=None)
        self.toast                  = Toast()
        self.helper                 = Helper()
        self.bookmark               = Bookmark()
        self._loadLogFileThread     = QThread()
        self._logcatThread          = QThread()
        self._filterThread          = QThread()
        self._searchThread          = QThread()
        self._logcatProcess         = None
        self._scrcpyProcess         = None
        self._showLoadingScreen     = False
        self._searchLog             = SearchLog()
        self._logViewReady          = False

        self._configs               = Configurations()
        self._configs.loadLastSavedConfig()

        filterPath = self._configs.getConfigs().get("filter", {}).get("path", "")
        if not filterPath or not os.path.isdir(os.path.dirname(filterPath)):
            filterPath = os.path.join(ROOT_FOLDER, "filter.json")
            self._configs.saveConfig("filter", {"path": filterPath})
        self.filterLog.create(filterPath)
        self._originalFilters = self.filterLog.originalFilters()
        self._nextLineNum       = 1
        self._trimmedOffset     = 0     # tracks rows trimmed from front for index calculation
        self._detailsText       = ""
        self._highlightLineNum  = -1
        self._logcatBuffer      = deque()
        self._loadCancelled     = False
        self._loadProgress      = 0.0
        self._loadingFileName   = ""
        self._openedFileName    = ""
        self._isLoading         = False
        self._isSaving          = False
        self._saveProgressVal   = 0.0

        self._logcatFilePath       = ""
        self._logcatFileQueue     = deque()
        self._logcatStreaming     = False
        self._logcatStreamThread  = QThread()
        self._pendingClearLog     = False

        self._logcatFlushTimer = QTimer(self)
        self._logcatFlushTimer.setInterval(100)
        self._logcatFlushTimer.timeout.connect(self._flushLogcatBuffer)

        self.helper.autoScrollDownChanged.connect(self._onAutoScrollDownChanged)
        
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
        self._stopLogcatProcess()
        self._stopScrcpyProcess()
        self._stop_thread(self._loadLogFileThread)
        self._stop_thread(self._logcatThread)
        self._stop_thread(self._logcatStreamThread)
        
        
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

    @Property(float, notify=loadProgressChanged)
    def loadProgress(self):
        return self._loadProgress

    @loadProgress.setter
    def loadProgress(self, val):
        self._loadProgress = val
        self.loadProgressChanged.emit()

    @Property(str, notify=loadingFileNameChanged)
    def loadingFileName(self):
        return self._loadingFileName

    @loadingFileName.setter
    def loadingFileName(self, val):
        self._loadingFileName = val
        self.loadingFileNameChanged.emit()

    @Property(str, notify=openedFileNameChanged)
    def openedFileName(self):
        return self._openedFileName

    @openedFileName.setter
    def openedFileName(self, val):
        self._openedFileName = val
        self.openedFileNameChanged.emit()

    @Property(bool, notify=isLoadingChanged)
    def isLoading(self):
        return self._isLoading

    @isLoading.setter
    def isLoading(self, val):
        self._isLoading = val
        self.isLoadingChanged.emit()

    @Property(bool, notify=isSavingChanged)
    def isSaving(self):
        return self._isSaving

    @isSaving.setter
    def isSaving(self, val):
        self._isSaving = val
        self.isSavingChanged.emit()

    @Property(float, notify=saveProgressChanged)
    def saveProgress(self):
        return self._saveProgressVal

    @saveProgress.setter
    def saveProgress(self, val):
        self._saveProgressVal = val
        self.saveProgressChanged.emit()

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
        # Do not reinitialize an active stream when device availability changes.
        if self._logcatThread.isRunning() or self._logcatStreamThread.isRunning():
            return

        if not shutil.which("adb"):
            self.showNotification.emit("adb not found in PATH. Please install Android SDK Platform Tools.")
            self._logSource = SOURCE_FILE
            self._configs.saveConfig("logSource", SOURCE_FILE)
            self.logSourceChanged.emit()
            return

        self.refreshAdbDeviceAvailability()
        if not self._hasAdbDevices:
            return

        now = datetime.datetime.now()
        current_time = now.strftime("%Y%m%d_%H%M%S") + f"_{now.microsecond // 1000:03d}"
        self._logcatFilePath = os.path.join(ROOT_FOLDER, f"logcat_{current_time}.log")
        self._logcatFileQueue = deque()
        self._logcatFileQueue.append(self._logcatFilePath)
        self._logcatStreaming = True

        self.logviewModel.updateData([])
        self._nextLineNum = 1
        self._trimmedOffset = 0
        self._logcatBuffer.clear()
        self.logViewReady = True
        self.helper.autoScrollDown = True
        self._logcatFlushTimer.start()

        # Writer thread: adb logcat → file
        self._logcatWorker = Worker(self._runLogcat)
        self._logcatWorker.moveToThread(self._logcatThread)
        self._logcatWorker.taskCompleted.connect(self._onLogcatStopped)
        self._logcatThread.started.connect(self._logcatWorker.run)
        self._logcatThread.start()

        # Reader thread: file → logcat buffer
        self._logcatStreamWorker = Worker(self._streamLogcatFile)
        self._logcatStreamWorker.moveToThread(self._logcatStreamThread)
        self._logcatStreamThread.started.connect(self._logcatStreamWorker.run)
        self._logcatStreamThread.start()

        self.toast.show(TOAST.INFO, "Reading Android logcat...")

    @Slot()
    def stopLogcat(self):
        print("stopLogcat")
        self._stopLogcatProcess()
        self.helper.autoScrollDown = False

    def _stopLogcatProcess(self):
        self._logcatFlushTimer.stop()
        self._logcatStreaming = False  # stop file reader
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
        if self._logcatStreamThread.isRunning():
            self._logcatStreamThread.quit()
            self._logcatStreamThread.wait()

    def _onAutoScrollDownChanged(self):
        if self.helper.autoScrollDown:
            if self._logcatStreaming:
                self._flushLogcatBuffer()
                self._logcatFlushTimer.start()
        else:
            self._logcatFlushTimer.stop()

    def _runLogcat(self):
        """Write adb logcat output to files, rotating every MAX_LOGCAT_FILE_SIZE bytes."""
        try:
            self._logcatProcess = subprocess.Popen(
                ["adb", "logcat", "-v", "threadtime"],
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                text=True,
                encoding="utf-8",
                errors="replace",
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            current_size = 0
            log_file = open(self._logcatFilePath, 'w', encoding='utf-8', buffering=1)
            try:
                for line in self._logcatProcess.stdout:
                    if self._logcatProcess.poll() is not None:
                        break
                    log_file.write(line)
                    current_size += len(line.encode('utf-8'))
                    if current_size >= MAX_LOGCAT_FILE_SIZE:
                        log_file.close()
                        now = datetime.datetime.now()
                        new_time = now.strftime("%Y%m%d_%H%M%S") + f"_{now.microsecond // 1000:03d}"
                        self._logcatFilePath = os.path.join(ROOT_FOLDER, f"logcat_{new_time}.log")
                        self._logcatFileQueue.append(self._logcatFilePath)
                        log_file = open(self._logcatFilePath, 'w', encoding='utf-8', buffering=1)
                        current_size = 0
            finally:
                log_file.close()
        except Exception as e:
            print(f"logcat error: {e}")

    def _streamLogcatFile(self):
        """Tail logcat files, following rotations from the writer thread."""
        file_index = 0
        file_path = self._logcatFileQueue[0]

        timeout = 5.0
        elapsed = 0.0
        while not os.path.exists(file_path) and elapsed < timeout:
            time.sleep(0.1)
            elapsed += 0.1

        if not os.path.exists(file_path):
            print(f"logcat file not found: {file_path}")
            return

        process_fn = lambda line, colors: self.logviewModel.processLineDataLogcat(line.rstrip("\n"), colors)
        file = None
        try:
            file = open(file_path, 'r', encoding='utf-8')
            file.seek(0, os.SEEK_END)
            while self._logcatStreaming:
                line = file.readline()
                if line:
                    colors = self.filterLog.colors()
                    result = process_fn(line, colors)
                    if result[0]:
                        self._logcatBuffer.append(result[1])
                else:
                    # Check if writer rotated to a new file
                    if len(self._logcatFileQueue) > file_index + 1:
                        file.close()
                        self._logcatBuffer.clear()
                        self._pendingClearLog = True
                        file_index += 1
                        file_path = self._logcatFileQueue[file_index]
                        while not os.path.exists(file_path) and self._logcatStreaming:
                            time.sleep(0.05)
                        if not self._logcatStreaming:
                            break
                        file = open(file_path, 'r', encoding='utf-8')
                    else:
                        time.sleep(0.1)
        except Exception as e:
            print(f"Error while streaming logcat file: {e}")
        finally:
            if file and not file.closed:
                file.close()

    def _flushLogcatBuffer(self):
        if self._pendingClearLog:
            self._pendingClearLog = False
            self.logviewModel.updateData([])
            self._nextLineNum = 1
            self._trimmedOffset = 0
        if not self._logcatBuffer:
            return
        entries = []
        while self._logcatBuffer:
            entries.append(self._logcatBuffer.popleft())
        self._assignLineNums(entries)
        self._batchInsert(entries)

    def _assignLineNums(self, entries):
        """Assign sequential line numbers. O(batch_size)."""
        for entry in entries:
            entry[LINE_NUMBER] = self._nextLineNum
            self._nextLineNum += 1

    def _batchInsert(self, entries):
        self.logviewModel.addRows(entries)

    def _onLogcatStopped(self, result):
        self._logcatThread.quit()
        self._logcatThread.wait()
        print("logcat stopped")

    @Slot(int)
    def showLogDetails(self, line):
        print("showLogDetails: ", line)
        idx = line - 1 - self._trimmedOffset
        if 0 <= idx < len(self.logviewModel._log_data):
            logline = self.logviewModel._log_data[idx]
            self.detailsText = (
                f"Line {line}:\t"
                f"{logline.get('message', '')}"
            )

    def getLogViewModel(self):
        return self.logviewModel
    
    def getFilterLog(self):
        return self.filterLog
    
    def getSearchLog(self):
        return self._searchLog
    
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
            self._loadFile(selected_file)

    @Slot(str)
    def openFileByPath(self, file_path):
        """Open a file by its path (used for drag-and-drop)."""
        if file_path.startswith("file:///"):
            file_path = file_path[8:]  # Remove file:/// prefix
        file_path = os.path.normpath(file_path)
        if os.path.isfile(file_path):
            self._loadFile(file_path)

    def _loadFile(self, file_path):
        self._loadCancelled = False
        self.loadingFileName = os.path.basename(file_path)
        self.loadProgress = 0.0
        self.isLoading = True
        self.showLoadingScreen = True
        self.logviewModel.updateData([])
        self._nextLineNum = 1
        self._trimmedOffset = 0

        self.worker = Worker(self._loadFileBatched, file_path)
        self.worker.batchLoaded.connect(self._onBatchLoaded)
        self.worker.moveToThread(self._loadLogFileThread)
        self.worker.taskCompleted.connect(self._onFileLoadComplete)
        self._loadLogFileThread.started.connect(self.worker.run)
        self._loadLogFileThread.start()

    @Slot()
    def cancelLoad(self):
        """Cancel an in-progress file load."""
        self._loadCancelled = True

    def _loadFileBatched(self, file_path):
        """Worker task: load entire file in worker thread, emitting progress only."""
        colors = self.filterLog.colors()
        self.logviewModel.setFilterColors(colors)

        def on_progress(pct):
            # Cross-thread signal carries only a float — no data copy overhead.
            self.worker.batchLoaded.emit(None, pct)

        entries = self.logviewModel.loadLogFile(
            file_path, colors,
            progress_callback=on_progress,
            cancel_flag=lambda: self._loadCancelled
        )
        return entries

    def _onBatchLoaded(self, entries, progress):
        """Slot called on main thread — only updates progress bar, no model changes."""
        self.loadProgress = progress

    def _onFileLoadComplete(self, all_entries):
        """Slot called when file loading finishes — single model reset."""
        self._loadLogFileThread.quit()
        self._loadLogFileThread.wait()
        # One beginResetModel/endResetModel is far faster than N×addRows.
        self.logviewModel.updateData(all_entries)
        self._nextLineNum = len(all_entries) + 1
        self._trimmedOffset = 0
        self.loadProgress = 1.0
        self.isLoading = False
        self.showLoadingScreen = False
        self.logViewReady = True
        self.openedFileName = self._loadingFileName
        self.loadLogFileCompleted.emit()
        self.toast.show(TOAST.INFO, f"Loaded {len(all_entries):,} records")

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
            self.isSaving = True
            self.saveProgress = 0.0
            self.worker = Worker(self._writeLogToFile, selected_file)
            self.worker.moveToThread(self._loadLogFileThread)
            self.worker.taskCompleted.connect(self.onLogFileSaved)
            self._loadLogFileThread.started.connect(self.worker.run)
            self._loadLogFileThread.start()

    def _writeLogToFile(self, file_path):
        """Write the current log data to a file in chunks for performance."""
        try:
            log_data = self.logviewModel._log_data
            total = len(log_data)
            if total == 0:
                return True

            with open(file_path, 'w', encoding='utf-8',
                      buffering=IO_BUFFER_SIZE) as file:
                for start in range(0, total, WRITE_CHUNK_SIZE):
                    end = min(start + WRITE_CHUNK_SIZE, total)
                    chunk_lines = []
                    for i in range(start, end):
                        chunk_lines.append(self.logviewModel.format_log_line(log_data[i]))
                    file.write('\n'.join(chunk_lines))
                    if end < total:
                        file.write('\n')
                    self._saveProgressVal = end / total
                    self.saveProgressChanged.emit()
                # Final newline
                file.write('\n')
            return True
        except Exception as e:
            print(f"Error saving log file: {e}")
            return False

    @Slot(bool)
    def onLogFileSaved(self, success):
        self._loadLogFileThread.quit()
        self._loadLogFileThread.wait()
        self.isSaving = False
        self.saveProgress = 0.0
        if success:
            self.toast.show(TOAST.INFO, "Log file saved successfully")
        else:
            self.toast.show(TOAST.ERROR, "Failed to save log file")

    def loadLogFile(self, file_path):
        """Load a file synchronously (used by streaming initial load)."""
        print("loadLogFile: ", file_path)
        colors = self.filterLog.colors()
        self.logviewModel.setFilterColors(colors)
        return self.logviewModel.loadLogFile(file_path, colors)

    @Slot(list)
    def onLogFileLoaded(self, result):
        self._loadLogFileThread.quit()
        self._loadLogFileThread.wait()
        parsed_log = result
        self._nextLineNum = len(parsed_log) + 1
        self._trimmedOffset = 0
        self.logviewModel.updateData(parsed_log)
        self.logViewReady = True
        self.loadLogFileCompleted.emit()
        
    def streamFile(self, file_path, process_fn=None, stop_flag_fn=None, target_buffer=None):
        print("streamFile: ", file_path)
        try:
            with open(file_path, 'r', encoding='utf-8') as file:
                file.seek(0, os.SEEK_END)
                while stop_flag_fn():
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
                            result = process_fn(line, colors)
                            if result[0]:
                                target_buffer.append(result[1])
                    else:
                        time.sleep(0.1)
        except Exception as e:
            print(f"Error while watching log file: {e}")

    def addLineLog(self, line):
        """Legacy single-line insert (kept for compatibility)."""
        (isSuccess, log_entry) = self.logviewModel.processLineData(line, self.filterLog.colors())
        if isSuccess:
            log_entry[LINE_NUMBER] = self._nextLineNum
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
    @Slot(int, QColor)
    def updateColorFilter(self, id, color):
        print("updateColorFilter id {} color {}".format(id, color))
        # update color in filter log
        self.filterLog.updateColorFilter(id, color)
        self.refreshColorFilters()
        pass

    def processUpdateColorOnTable(self, id, color):
        # Recompute row colors from all active filters to keep regex behavior consistent.
        self.refreshColorFilters()

    def refreshColorFilters(self):
        colors = self.filterLog.colors()
        self.logviewModel.reapplyProcessColors(colors)

    @Slot(int,bool)
    def enableFilter(self, id, enabled):
        self.filterLog.enableFilter(id, enabled)
        self.refreshColorFilters()

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

    @Slot(str, str, str)
    @Slot(str, str, QColor)
    def addFilter(self, tag, pid, color):
        if self._filterThread.isRunning():
            self._filterThread.quit()
            self._filterThread.wait()
        self._filterWorker = Worker(self.filterLog.addFilter, tag, pid, color)
        self._filterWorker.moveToThread(self._filterThread)
        self._filterWorker.taskCompleted.connect(self._onFilterOperationDone)
        self._filterThread.started.connect(self._filterWorker.run)
        self._filterThread.start()

    @Slot(int, str, str, bool, str)
    @Slot(int, str, str, bool, QColor)
    def updateFilter(self, id, tag, pid, enabled, color):
        if self._filterThread.isRunning():
            self._filterThread.quit()
            self._filterThread.wait()
        self._filterWorker = Worker(self.filterLog.updateFilter, id, tag, pid, enabled, color)
        self._filterWorker.moveToThread(self._filterThread)
        self._filterWorker.taskCompleted.connect(self._onFilterOperationDone)
        self._filterThread.started.connect(self._filterWorker.run)
        self._filterThread.start()

    def _onFilterOperationDone(self, result):
        self._filterThread.quit()
        self.refreshColorFilters()

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
        if self._searchThread.isRunning():
            self._searchThread.quit()
            self._searchThread.wait()
        self._searchWorker = Worker(self._doSearch, pattern)
        self._searchWorker.moveToThread(self._searchThread)
        self._searchWorker.taskCompleted.connect(self._onSearchDone)
        self._searchThread.started.connect(self._searchWorker.run)
        self._searchThread.start()

    def _doSearch(self, pattern):
        self._searchLog.applySearchQuery(pattern)
        return pattern

    def _onSearchDone(self, pattern):
        self._searchThread.quit()
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
                "logSource": "logcat"
            }
            with open(file_path, 'a') as file:
                json.dump(config_data, file, indent=4)
            print(f"File '{file_path}' created.")
        else:
            print(f"File '{file_path}' already exists.")
    
    @Slot()
    def clearLog(self):
        print("clearLog")
        self._logcatBuffer.clear()
        self.logviewModel.updateData([])
        self._nextLineNum = 1
        self._trimmedOffset = 0
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
    
    @Slot(int, result=str)
    def getLogMessage(self, lineNum):
        idx = lineNum - 1 - self._trimmedOffset
        if 0 <= idx < len(self.logviewModel._log_data):
            return self.logviewModel._log_data[idx].get("message", "")
        return ""
