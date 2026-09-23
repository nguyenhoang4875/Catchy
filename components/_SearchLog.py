# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Signal, Property, QRegularExpression
import re


class SearchLog(QObject):
    searchRegexChanged = Signal()
    showSearchResultsChanged = Signal()
    searchWordsChanged = Signal()
    previousSearchQueryChanged = Signal()
    searchHistoryChanged = Signal()
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self._searchRegex = QRegularExpression(R"", QRegularExpression.CaseInsensitiveOption | QRegularExpression.DotMatchesEverythingOption)
        self._searchWords = []
        self._compiledWords = []
        self._showSearchResults = False
        self._previousSearchQuery = ""
        self._searchHistory = []
        self._maxHistorySize = 30
        # Định nghĩa bảng màu cho highlight (đậm/bão hòa hơn để hiện rõ trên nền trắng)
        self._colorPaletteRgb = [
            (46, 125, 50),    # Green
            (249, 168, 37),   # Amber
            (198, 40, 40),    # Red
            (21, 101, 192),   # Blue
            (106, 27, 154),   # Purple
            (173, 20, 87),    # Pink
            (230, 81, 0),     # Orange
            (0, 105, 92),     # Teal
            (69, 39, 160),    # Indigo
            (158, 157, 36),   # Olive
        ]
        self._highlightAlpha = 0.6

    @Property(QRegularExpression, notify=searchRegexChanged)
    def searchRegex(self):
        return self._searchRegex
    
    @searchRegex.setter
    def searchRegex(self, pattern):
        self._searchRegex.setPattern(pattern)
        # Tách các từ khóa bằng dấu |
        self._searchWords = [word.strip() for word in pattern.split('|') if word.strip()]
        self._rebuildCompiledWords()
        self.searchRegexChanged.emit()
        self.searchWordsChanged.emit()

    def _rebuildCompiledWords(self):
        """Precompile (regex, color) pairs once per query so per-row highlighting
        doesn't recompile a regex for every visible cell on every scroll frame."""
        compiled = []
        for i, word in enumerate(self._searchWords):
            try:
                compiled.append((re.compile(re.escape(word), re.IGNORECASE), self.getColorForIndex(i)))
            except re.error:
                continue
        self._compiledWords = compiled

    def compiledSearchWords(self):
        return self._compiledWords

    @Property(str, notify=previousSearchQueryChanged)
    def previousSearchQuery(self):
        return self._previousSearchQuery

    @Property(list, notify=searchHistoryChanged)
    def searchHistory(self):
        return self._searchHistory

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

    def _setPreviousSearchQuery(self, query):
        if self._previousSearchQuery == query:
            return
        self._previousSearchQuery = query
        self.previousSearchQueryChanged.emit()

    def _addToHistory(self, query):
        normalized = (query or "").strip()
        if not normalized:
            return

        self._searchHistory = [item for item in self._searchHistory if item.lower() != normalized.lower()]
        self._searchHistory.insert(0, normalized)
        self._searchHistory = self._searchHistory[:self._maxHistorySize]
        self.searchHistoryChanged.emit()

    def restoreSearchState(self, current_query, previous_query, history):
        self._searchHistory = [str(item).strip() for item in (history or []) if str(item).strip()]
        self._searchHistory = self._searchHistory[:self._maxHistorySize]
        self._setPreviousSearchQuery((previous_query or "").strip())
        self.searchRegex = (current_query or "").strip()
        self.searchHistoryChanged.emit()

    def applySearchQuery(self, query):
        normalized = (query or "").strip()
        current = self._searchRegex.pattern().strip()

        if normalized and current and normalized.lower() != current.lower():
            self._setPreviousSearchQuery(current)

        self.searchRegex = normalized
        if normalized:
            self._addToHistory(normalized)

    def getSearchHint(self, prefix):
        normalized = (prefix or "").strip()
        if not normalized:
            return ""

        lower_prefix = normalized.lower()
        for item in self._searchHistory:
            if item.lower().startswith(lower_prefix) and item.lower() != lower_prefix:
                return item

        return ""
    
    def getColorForIndex(self, index):
        """Lấy màu (translucent, dùng cho background highlight) theo index, lặp lại nếu vượt quá số màu có sẵn"""
        r, g, b = self._colorPaletteRgb[index % len(self._colorPaletteRgb)]
        return f'rgba({r}, {g}, {b}, {self._highlightAlpha})'