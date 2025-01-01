# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QSortFilterProxyModel, Slot, QModelIndex, QMimeData, Qt

class SortFilterProxyModel(QSortFilterProxyModel):
    def __init__(self):
        super().__init__()


    @Slot(int, result=int)
    def rowLineNum(self, line):
        for i in range(self.rowCount()):
            if self.data(self.index(i, 0), Qt.UserRole) == line:
                return i
            
        return -1
    

