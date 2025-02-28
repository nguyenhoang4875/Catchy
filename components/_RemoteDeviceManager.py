from PySide6.QtCore import QObject, Signal, QRegularExpression, Property, Slot

IDLE        = 0
INPROGRESS  = 1
SUCCESS     = 2
FAILED      = 3

class RemoteDeviceManager(QObject):
    deviceListChanged           = Signal()
    connectedDeviceChanged      = Signal()
    connectingDeviceChanged     = Signal()
    connectProcessStatusChanged = Signal()
    streamingChanged            = Signal()
    hasConnectionChanged        = Signal()
    interuptedChanged           = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)
        self._deviceList                    = []
        self._connectedDevice               = None
        self._connectingDevice              = None
        self._connectProcessStatus          = IDLE
        self._streaming                     = False
        self._hasConnection                 = False
        self._interupted                    = False
        
    @Property(list, notify=deviceListChanged)
    def deviceList(self):
        return self._deviceList
    
    @deviceList.setter
    def deviceList(self, value):
        self._deviceList = value
        self.deviceListChanged.emit()
        
    @Property(dict, notify=connectedDeviceChanged)
    def connectedDevice(self):
        return self._connectedDevice
    
    @connectedDevice.setter
    def connectedDevice(self, value):
        self._connectedDevice = value
        self.connectedDeviceChanged.emit()
        
        if value is not None:
            self.hasConnection = True
        else:
            self.hasConnection = False
        
    @Property(dict, notify=connectingDeviceChanged)
    def connectingDevice(self):
        return self._connectingDevice
    
    @connectingDevice.setter
    def connectingDevice(self, value):
        self._connectingDevice = value
        self.connectingDeviceChanged.emit()
        
    @Property(int, notify=connectProcessStatusChanged)
    def connectProcessStatus(self):
        return self._connectProcessStatus
    
    @connectProcessStatus.setter
    def connectProcessStatus(self, value):
        self._connectProcessStatus = value
        self.connectProcessStatusChanged.emit()
        
    @Property(bool, notify=streamingChanged)
    def streaming(self):
        return self._streaming
    
    @streaming.setter
    def streaming(self, value):
        self._streaming = value
        self.streamingChanged.emit()
        
    @Property(bool, notify=hasConnectionChanged)
    def hasConnection(self):
        return self._hasConnection
    
    @hasConnection.setter
    def hasConnection(self, value):
        self._hasConnection = value
        self.hasConnectionChanged.emit()
        
    @Property(bool, notify=interuptedChanged)
    def interupted(self):
        return self._interupted
    
    @interupted.setter
    def interupted(self, value):
        self._interupted = value
        self.hasConnectionChanged.emit()
        
    @Slot(int)
    def setConnectedDevice(self, id):
        for device in self._deviceList:
            if device.id == id:
                self.connectedDevice = device
                break
    
    def loadDeviceList(self, deviceList):
        self.deviceList = deviceList