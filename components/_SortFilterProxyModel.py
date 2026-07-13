# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QSortFilterProxyModel, Slot, QModelIndex, QMimeData, Qt, Signal, Property
import re

class SortFilterProxyModel(QSortFilterProxyModel):
    filterCriteriaChanged = Signal()

    def __init__(self):
        super().__init__()
        self._filter_criteria = []

    @Property(list, notify=filterCriteriaChanged)
    def filterCriteria(self):
        return self._filter_criteria

    @filterCriteria.setter
    def filterCriteria(self, criteria):
        self._filter_criteria = criteria if criteria else []
        self.filterCriteriaChanged.emit()
        self.invalidateFilter()

    @staticmethod
    def _regex_match(pattern, value):
        """Return True if pattern (regex) matches anywhere in value. Empty pattern matches all."""
        if not pattern:
            return True
        try:
            return bool(re.search(pattern, value, re.IGNORECASE))
        except re.error:
            # Fall back to substring match if pattern is invalid regex
            return pattern.lower() in value.lower()

    def filterAcceptsRow(self, source_row, source_parent):
        if not self._filter_criteria:
            return super().filterAcceptsRow(source_row, source_parent)

        model = self.sourceModel()
        for criterion in self._filter_criteria:
            tag_pattern = (criterion.get("tag") or "").strip()
            pid_pattern = (criterion.get("pid") or "").strip()
            tid_pattern = (criterion.get("tid") or "").strip()

            tag_val = str(model.data(model.index(source_row, 4, source_parent), Qt.DisplayRole) or "")
            pid_val = str(model.data(model.index(source_row, 1, source_parent), Qt.DisplayRole) or "")
            tid_val = str(model.data(model.index(source_row, 2, source_parent), Qt.DisplayRole) or "")

            if (self._regex_match(tag_pattern, tag_val) and
                self._regex_match(pid_pattern, pid_val) and
                self._regex_match(tid_pattern, tid_val)):
                return True

        return False

    @Slot(int, result=int)
    def rowLineNum(self, line):
        for i in range(self.rowCount()):
            if self.data(self.index(i, 0), Qt.UserRole) == line:
                return i
            
        return -1


