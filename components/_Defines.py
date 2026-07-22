from PySide6.QtCore import QObject, Slot
from pathlib import Path

# Log source constants
SOURCE_FILE     = "file"
SOURCE_LOGCAT   = "logcat"
SOURCE_SSH      = "ssh"

# Performance constants
BATCH_SIZE          = 50_000      # Records per batch during file loading
WRITE_CHUNK_SIZE    = 10_000      # Records per chunk during file saving
IO_BUFFER_SIZE      = 65536       # 64KB I/O buffer for file read/write
MAX_LOG_ROWS        = 50_000      # Row cap for live logcat streaming only


class Defines(QObject):
    def __init__(self, root, parent=None):
        super().__init__(parent)
        self._root = root
    
    @Slot(str, result=str)
    def path(self, arg):
        path = "{}/{}".format(Path(self._root).resolve().parent.as_posix(), arg)
        print(path)
        return path

