# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Signal, Property, QRegularExpression


class SearchLog(QObject):
    searchRegexChanged = Signal()
    showSearchResultsChanged = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)

        self._searchRegex       = QRegularExpression(R"", QRegularExpression.CaseInsensitiveOption | QRegularExpression.DotMatchesEverythingOption)
        self._searchWords       = []
        self._showSearchResults = False
        pass

    @Property(QRegularExpression, notify=searchRegexChanged)
    def searchRegex(self):
        return self._searchRegex
    
    @searchRegex.setter
    def searchRegex(self, pattern):
        self._searchRegex.setPattern(pattern)
        self.searchRegexChanged.emit()

    @Property(bool, notify=showSearchResultsChanged)
    def showSearchResults(self):
        return self._showSearchResults  
    
    @showSearchResults.setter
    def showSearchResults(self, val):
        self._showSearchResults = val
        self.showSearchResultsChanged.emit()