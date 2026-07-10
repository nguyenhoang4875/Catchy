from PySide6.QtCore import QObject, Slot
from pathlib import Path

# Log source constants
SOURCE_FILE     = "file"
SOURCE_LOGCAT   = "logcat"
SOURCE_SSH      = "ssh"


class Defines(QObject):
    def __init__(self, root, parent=None):
        super().__init__(parent)
        self._root = root
    
    @Slot(str, result=str)
    def path(self, arg):
        path = "{}/{}".format(Path(self._root).resolve().parent.as_posix(), arg)
        print(path)
        return path

