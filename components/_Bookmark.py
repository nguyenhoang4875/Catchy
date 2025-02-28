from PySide6.QtCore import QObject, Property, Signal, Slot

class Bookmark(QObject):
    displayListChanged      = Signal()
    highlightLinesChanged   = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)
        self._displayList       = []
        self._highlightLines    = []
        
    @Property(list, notify=displayListChanged)
    def displayList(self):
        return self._displayList
    
    @displayList.setter
    def displayList(self, value):
        self._displayList = value
        self.displayListChanged.emit()
        
        highlightLines = []
        for bookmark in self._displayList:
            line = bookmark["line"]
            highlightLines.append(line)
            
        self.highlightLines = highlightLines
    
    @Slot(dict)    
    def addBookmark(self, bookmark):
        print("addBookmark: ", bookmark)
        self.displayList.append(bookmark)
        self.displayListChanged.emit()
        
        self.addHighlightLine(bookmark["line"])
        
    @Slot(int)
    def removeBookmark(self, line):
        for bookmark in self.displayList:
            if bookmark["line"] == line:
                self._displayList.remove(bookmark)
                self.displayListChanged.emit()
                break
                
        self._highlightLines.remove(line)
        self.highlightLinesChanged.emit()
        
    @Property(list, notify=highlightLinesChanged)
    def highlightLines(self):
        return self._highlightLines
    
    @highlightLines.setter
    def highlightLines(self, value):
        self._highlightLines = value
        self.highlightLinesChanged.emit()
        
    def addHighlightLine(self, line):
        self._highlightLines.append(line)
        self.highlightLinesChanged.emit()
    
    @Slot(int, result=bool)
    def isBookmarked(self, line):
        return line in self._highlightLines
    
    @Slot()
    def clearAll(self):
        self._displayList.clear()
        self.displayListChanged.emit()
        
        self._highlightLines.clear()
        self.highlightLinesChanged.emit()