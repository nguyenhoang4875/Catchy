# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Signal, Property, QRegularExpression


class SearchLog(QObject):
    searchRegexChanged = Signal()
    showSearchResultsChanged = Signal()
    searchWordsChanged = Signal()
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self._searchRegex = QRegularExpression(R"", QRegularExpression.CaseInsensitiveOption | QRegularExpression.DotMatchesEverythingOption)
        self._searchWords = []
        self._showSearchResults = False
        # Định nghĩa bảng màu cho highlight
        self._colorPalette = [
            'rgba(97, 165, 77, 0.6)',   # Green
            'rgba(255, 193, 7, 0.6)',   # Yellow  
            'rgba(220, 53, 69, 0.6)',   # Red
            'rgba(13, 110, 253, 0.6)',  # Blue
            'rgba(111, 66, 193, 0.6)',  # Purple
            'rgba(255, 99, 132, 0.6)',  # Pink
            'rgba(255, 159, 64, 0.6)',  # Orange
            'rgba(75, 192, 192, 0.6)',  # Teal
            'rgba(153, 102, 255, 0.6)', # Violet
            'rgba(255, 205, 86, 0.6)'   # Light Yellow
        ]

    @Property(QRegularExpression, notify=searchRegexChanged)
    def searchRegex(self):
        return self._searchRegex
    
    @searchRegex.setter
    def searchRegex(self, pattern):
        self._searchRegex.setPattern(pattern)
        # Tách các từ khóa bằng dấu |
        self._searchWords = [word.strip() for word in pattern.split('|') if word.strip()]
        self.searchRegexChanged.emit()
        self.searchWordsChanged.emit()

    @Property(bool, notify=showSearchResultsChanged)
    def showSearchResults(self):
        return self._showSearchResults  
    
    @showSearchResults.setter
    def showSearchResults(self, val):
        self._showSearchResults = val
        self.showSearchResultsChanged.emit()
    
    @Property(list, notify=searchWordsChanged)
    def searchWords(self):
        return self._searchWords
    
    def getColorForIndex(self, index):
        """Lấy màu theo index, lặp lại nếu vượt quá số màu có sẵn"""
        return self._colorPalette[index % len(self._colorPalette)]