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
        self._detailsText       = ""
        self._highlightLineNum  = -1
        
        
        self._streamingFilePath = ""
        
        atexit.register(self.cleanup)
        pass
    
    def cleanup(self):
        # Code to execute when the instance is destroyed
        print("Controller instance is being destroyed")
        self.remoteDeviceManager.streaming = False
        self._stop_thread(self._loadLogFileThread)
        self._stop_thread(self._streamLogFileThread)
        
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
        self.logviewModel.updateData(parsed_log)
        self.logViewReady = True
        self.loadLogFileCompleted.emit()
        
        self.startStreamingLogFile()
        pass
    
    def startStreamingLogFile(self):
        print("startStreamingLogFile: ", self._streamingFilePath)
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
            # Open the log file and seek to the end
            with open(file_path, 'r', encoding='utf-8') as file:
                # Move the file pointer to the end of the file
                file.seek(0, os.SEEK_END)
                
                while self.remoteDeviceManager.streaming:
                    # Read new lines that have been added
                    new_line = file.readline()
                    if new_line:
                        self.addLineLog(new_line)  # Process the new line
                    else:
                        # Sleep for a short time if no new lines are available
                        time.sleep(0.1)
        except Exception as e:
            print(f"Error while watching log file: {e}")
        pass
    
    @Slot()
    def onStreamFileStopped(self):
        print("onStreamFileStopped")
        self.remoteDeviceManager.streaming = False
        self._streamLogFileThread.quit()
        self._streamLogFileThread.wait()
        pass
    
    def addLineLog(self, line):
        (isSuccess, log_entry) = self.logviewModel.processLineData(line, self.filterLog.colors())
        if isSuccess:
            try:
                maxLineNum = max(self._logDict.keys())
            except ValueError:
                maxLineNum = -1
            lineNum = maxLineNum + 1
            log_entry[LINE_NUMBER] = lineNum
            self.logviewModel.addRow(log_entry)
            self._logDict[lineNum] = log_entry
        else:
            print(f"Error processing line: {line}")
        pass

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
        filter = None
        for f in self.filterLog.displayedFilters:
            if f["id"] == id:
                filter = f
                break
        
        if filter is None:
            return
        
        processName = filter["processName"]
        color       = filter["color"]
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
        processName = None
        for f in self.filterLog.displayedFilters:
            if f["id"] == id:
                processName = f["processName"]
                break
        
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
                }
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
        self.logviewModel.updateData([])
        self._logDict = {}
        self.toast.show(TOAST.INFO, "Log cleared")
        pass
    
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
