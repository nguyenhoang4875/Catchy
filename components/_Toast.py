from PySide6.QtCore import QObject, Signal, Slot
from PySide6.QtQml import QmlElement
QML_IMPORT_NAME = "io.qt.toast"
QML_IMPORT_MAJOR_VERSION = 1
class TOAST:
    ERROR     = 2
    WARNING   = 1
    INFO      = 0

def singleton(cls):
    instances = {}
    
    def get_instance(*args, **kwargs):
        if cls not in instances:
            instances[cls] = cls(*args, **kwargs)
        return instances[cls]
    return get_instance

@singleton
@QmlElement
class Toast(QObject):
    showMsg = Signal(int, str)
    def __init__(self, parent=None):
        super().__init__(parent)
        # self.ERROR     = 2
        # self.WARNING   = 1
        # self.INFO      = 0
    
    def show(self, type, message):
        self.showMsg.emit(type, message)

    @Slot()
    def close(self):
        print(":close")
    