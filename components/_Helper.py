from PySide6.QtCore import QObject, Property, Signal

class Helper(QObject):
    autoScrollDownChanged = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)
        self._autoScrollDown = False
        
    @Property(bool, notify=autoScrollDownChanged)
    def autoScrollDown(self):
        return self._autoScrollDown
    
    @autoScrollDown.setter
    def autoScrollDown(self, value):
        self._autoScrollDown = value
        self.autoScrollDownChanged.emit()