# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QAbstractTableModel, QModelIndex, Qt, Signal, Property, Slot, QRegularExpression
from bisect import bisect_left, bisect_right
import re

# Column keys for direct _log_data access.
_SEARCH_KEYS = ('tag', 'message', 'datetime', 'pid', 'tid', 'log_level')


class SortFilterProxyModel(QAbstractTableModel):
    """Fast filtered table model using a precomputed index list.

    Replaces QSortFilterProxyModel to eliminate per-row C++→Python overhead.
    Filter/search changes run one O(N) Python loop then emit a single model
    reset — no filterAcceptsRow() calls from C++.
    """
    filterCriteriaChanged = Signal()

    def __init__(self, parent=None):
        super().__init__(parent)
        self._source = None
        self._filter_criteria = []
        self._compiled_criteria = []   # list of (tag_c, pid_c, tid_c) tuples
        self._regex_pattern = ''
        self._py_regex = None
        self._indices = []             # proxy_row → source_row
        self._source_rows = []         # exposed for search proxy fast access

    # ── Properties ────────────────────────────────────────────────────────

    @Property('QVariant')
    def sourceModel(self):
        return self._source

    @sourceModel.setter
    def sourceModel(self, model):
        if self._source is model:
            return
        old = self._source
        if old is not None:
            for sig, slot in self._signal_pairs(old):
                try:
                    sig.disconnect(slot)
                except RuntimeError:
                    pass
        self._source = model
        if model is not None:
            for sig, slot in self._signal_pairs(model):
                sig.connect(slot)
        self._rebuild()

    def _signal_pairs(self, model):
        return [
            (model.modelReset, self._rebuild),
            (model.rowsInserted, self._on_rows_inserted),
            (model.rowsRemoved, self._on_rows_removed),
            (model.dataChanged, self._on_data_changed),
        ]

    @Property(list, notify=filterCriteriaChanged)
    def filterCriteria(self):
        return self._filter_criteria

    @filterCriteria.setter
    def filterCriteria(self, criteria):
        self._filter_criteria = criteria if criteria else []
        self._compile_criteria()
        self.filterCriteriaChanged.emit()
        self._rebuild()

    @Property(QRegularExpression)
    def filterRegularExpression(self):
        return QRegularExpression(self._regex_pattern)

    @filterRegularExpression.setter
    def filterRegularExpression(self, regex):
        pattern = ''
        if isinstance(regex, QRegularExpression):
            pattern = regex.pattern() or ''
        elif isinstance(regex, str):
            pattern = regex
        if pattern == self._regex_pattern:
            return
        self._regex_pattern = pattern
        if pattern:
            try:
                self._py_regex = re.compile(pattern, re.IGNORECASE | re.DOTALL)
            except re.error:
                self._py_regex = None
        else:
            self._py_regex = None
        self._rebuild()

    @Property(int)
    def filterKeyColumn(self):
        return -1

    @filterKeyColumn.setter
    def filterKeyColumn(self, _col):
        pass  # unused — kept for QML compatibility

    # ── Criteria compilation ──────────────────────────────────────────────

    def _compile_criteria(self):
        self._compiled_criteria = []
        for criterion in self._filter_criteria:
            self._compiled_criteria.append((
                self._compile_one(criterion.get('tag')),
                self._compile_one(criterion.get('pid')),
                self._compile_one(criterion.get('tid')),
            ))

    @staticmethod
    def _compile_one(pattern):
        p = (pattern or '').strip()
        if not p:
            return None
        try:
            return re.compile(p, re.IGNORECASE)
        except re.error:
            return p.lower()  # fallback to substring

    # ── Rebuild engine ────────────────────────────────────────────────────

    def _rebuild(self):
        """Recompute filtered index list. One O(N) loop + one model reset."""
        self.beginResetModel()
        self._indices = []
        self._source_rows = []

        if self._source is not None:
            log_data = getattr(self._source, '_log_data', None)
            if log_data is not None:
                self._rebuild_from_data(log_data)
            else:
                self._rebuild_from_proxy()
            self._source_rows = self._indices

        self.endResetModel()

    def _rebuild_from_data(self, log_data):
        """Filter LogModel data directly — fastest path."""
        criteria = self._compiled_criteria
        py_regex = self._py_regex
        n = len(log_data)

        if not criteria and py_regex is None:
            self._indices = list(range(n))
            return

        indices = []
        _append = indices.append

        # Detect fast path: all criteria only check tag
        tag_only = criteria and all(p is None and t is None for _, p, t in criteria)

        if tag_only and py_regex is None:
            self._fast_tag_filter(log_data, n, criteria, indices, _append)
        else:
            self._general_filter(log_data, n, criteria, py_regex, indices, _append)

        self._indices = indices

    @staticmethod
    def _fast_tag_filter(log_data, n, criteria, indices, _append):
        """Optimized loop for tag-only filter criteria (most common case)."""
        if len(criteria) == 1:
            tag_c = criteria[0][0]
            if tag_c is None:
                indices.extend(range(n))
                return
            if isinstance(tag_c, str):
                for i in range(n):
                    if tag_c in log_data[i]['tag'].lower():
                        _append(i)
            else:
                _search = tag_c.search
                for i in range(n):
                    if _search(log_data[i]['tag']):
                        _append(i)
        else:
            # Multiple tag criteria
            regex_fns = []
            substr_pats = []
            for tag_c, _, _ in criteria:
                if tag_c is None:
                    indices.extend(range(n))
                    return
                elif isinstance(tag_c, str):
                    substr_pats.append(tag_c)
                else:
                    regex_fns.append(tag_c.search)
            for i in range(n):
                tag = log_data[i]['tag']
                matched = False
                for fn in regex_fns:
                    if fn(tag):
                        matched = True
                        break
                if not matched:
                    tag_low = tag.lower()
                    for sp in substr_pats:
                        if sp in tag_low:
                            matched = True
                            break
                if matched:
                    _append(i)

    @staticmethod
    def _general_filter(log_data, n, criteria, py_regex, indices, _append):
        """General filter loop: criteria + optional search regex."""
        has_criteria = bool(criteria)
        for i in range(n):
            entry = log_data[i]

            if has_criteria:
                tag = entry['tag']
                pid = entry['pid']
                tid = entry['tid']
                ok = False
                for tag_c, pid_c, tid_c in criteria:
                    # Inline matching — avoids per-call function overhead
                    if tag_c is not None:
                        if isinstance(tag_c, str):
                            if tag_c not in tag.lower():
                                continue
                        elif not tag_c.search(tag):
                            continue
                    if pid_c is not None:
                        if isinstance(pid_c, str):
                            if pid_c not in pid.lower():
                                continue
                        elif not pid_c.search(pid):
                            continue
                    if tid_c is not None:
                        if isinstance(tid_c, str):
                            if tid_c not in tid.lower():
                                continue
                        elif not tid_c.search(tid):
                            continue
                    ok = True
                    break
                if not ok:
                    continue

            if py_regex is not None:
                _s = py_regex.search
                found = False
                for key in _SEARCH_KEYS:
                    val = entry.get(key, '')
                    if val and _s(val):
                        found = True
                        break
                if not found:
                    continue

            _append(i)

    def _rebuild_from_proxy(self):
        """Filter when source is another SortFilterProxyModel (search proxy)."""
        py_regex = self._py_regex
        source = self._source

        # Access LogModel data through the proxy chain
        inner = getattr(source, '_source', None)
        log_data = getattr(inner, '_log_data', None) if inner else None
        parent_source_rows = getattr(source, '_source_rows', None)

        if py_regex is None:
            # No search regex — accept all parent rows
            n = source.rowCount() if source else 0
            self._indices = list(range(n))
            return

        if log_data is None:
            n = source.rowCount() if source else 0
            self._indices = list(range(n))
            return

        indices = []
        _search = py_regex.search
        _append = indices.append

        if parent_source_rows is not None:
            for i, src_row in enumerate(parent_source_rows):
                if 0 <= src_row < len(log_data):
                    entry = log_data[src_row]
                    for key in _SEARCH_KEYS:
                        val = entry.get(key, '')
                        if val and _search(val):
                            _append(i)
                            break
        else:
            for i in range(len(log_data)):
                entry = log_data[i]
                for key in _SEARCH_KEYS:
                    val = entry.get(key, '')
                    if val and _search(val):
                        _append(i)
                        break

        self._indices = indices

    # ── Streaming support ─────────────────────────────────────────────────

    def _accepts_row(self, source_row):
        """Check if a single source row passes all active filters."""
        criteria = self._compiled_criteria
        py_regex = self._py_regex
        if not criteria and py_regex is None:
            return True

        entry = self._get_source_entry(source_row)
        if entry is None:
            return True

        if criteria:
            tag = entry.get('tag', '')
            pid = entry.get('pid', '')
            tid = entry.get('tid', '')
            ok = False
            for tag_c, pid_c, tid_c in criteria:
                if tag_c is not None:
                    if isinstance(tag_c, str):
                        if tag_c not in tag.lower():
                            continue
                    elif not tag_c.search(tag):
                        continue
                if pid_c is not None:
                    if isinstance(pid_c, str):
                        if pid_c not in pid.lower():
                            continue
                    elif not pid_c.search(pid):
                        continue
                if tid_c is not None:
                    if isinstance(tid_c, str):
                        if tid_c not in tid.lower():
                            continue
                    elif not tid_c.search(tid):
                        continue
                ok = True
                break
            if not ok:
                return False

        if py_regex is not None:
            for key in _SEARCH_KEYS:
                val = entry.get(key, '')
                if val and py_regex.search(val):
                    return True
            return False

        return True

    def _get_source_entry(self, source_row):
        """Get LogModel entry dict for a given source row."""
        if self._source is None:
            return None
        log_data = getattr(self._source, '_log_data', None)
        if log_data is not None:
            return log_data[source_row] if 0 <= source_row < len(log_data) else None
        # Source is another proxy
        parent_indices = getattr(self._source, '_indices', None)
        inner = getattr(self._source, '_source', None)
        if parent_indices is not None and inner is not None:
            if 0 <= source_row < len(parent_indices):
                actual = parent_indices[source_row]
                inner_data = getattr(inner, '_log_data', None)
                if inner_data is not None and 0 <= actual < len(inner_data):
                    return inner_data[actual]
        return None

    def _on_rows_inserted(self, _parent, first, last):
        """Handle new rows from source (streaming / logcat)."""
        new_proxy = []
        for src in range(first, last + 1):
            if self._accepts_row(src):
                new_proxy.append(src)
        if new_proxy:
            pf = len(self._indices)
            pl = pf + len(new_proxy) - 1
            self.beginInsertRows(QModelIndex(), pf, pl)
            self._indices.extend(new_proxy)
            self._source_rows = self._indices
            self.endInsertRows()

    def _on_rows_removed(self, _parent, first, last):
        """Handle rows removed from source (logcat trim from front)."""
        removed_count = last - first + 1
        rm_start = bisect_left(self._indices, first)
        rm_end = bisect_right(self._indices, last)
        proxy_removed = rm_end - rm_start

        # Build new index list: skip removed, shift remaining
        new_indices = []
        for src in self._indices:
            if first <= src <= last:
                continue
            new_indices.append(src - removed_count if src > last else src)

        if proxy_removed > 0:
            self.beginRemoveRows(QModelIndex(), rm_start, rm_end - 1)
            self._indices = new_indices
            self._source_rows = self._indices
            self.endRemoveRows()
        else:
            self._indices = new_indices
            self._source_rows = self._indices

    def _on_data_changed(self, topLeft, bottomRight, roles):
        """Forward data changes from source to views."""
        if not self._indices:
            return
        src_first = topLeft.row()
        src_last = bottomRight.row()
        lo = bisect_left(self._indices, src_first)
        hi = bisect_right(self._indices, src_last)
        if lo < hi:
            self.dataChanged.emit(
                self.index(lo, topLeft.column()),
                self.index(hi - 1, bottomRight.column()),
                roles,
            )

    # ── QAbstractTableModel interface ─────────────────────────────────────

    def rowCount(self, parent=QModelIndex()):
        return len(self._indices)

    def columnCount(self, parent=QModelIndex()):
        return self._source.columnCount() if self._source else 0

    def data(self, index, role=Qt.DisplayRole):
        if not index.isValid() or self._source is None:
            return None
        r = index.row()
        if not 0 <= r < len(self._indices):
            return None
        src = self._indices[r]
        return self._source.data(self._source.index(src, index.column()), role)

    def headerData(self, section, orientation, role=Qt.DisplayRole):
        return self._source.headerData(section, orientation, role) if self._source else None

    def roleNames(self):
        return self._source.roleNames() if self._source else {}

    # ── Index mapping helpers ─────────────────────────────────────────────

    def mapToSource(self, proxy_index):
        if not proxy_index.isValid() or self._source is None:
            return QModelIndex()
        r = proxy_index.row()
        if 0 <= r < len(self._indices):
            return self._source.index(self._indices[r], proxy_index.column())
        return QModelIndex()

    def mapFromSource(self, source_index):
        if not source_index.isValid():
            return QModelIndex()
        src_row = source_index.row()
        i = bisect_left(self._indices, src_row)
        if i < len(self._indices) and self._indices[i] == src_row:
            return self.index(i, source_index.column())
        return QModelIndex()

    @Slot(int, result=int)
    def rowLineNum(self, line):
        """Find proxy row by line number using binary search."""
        if self._source is None:
            return -1

        # Resolve LogModel data (may be direct or through proxy chain)
        log_data = getattr(self._source, '_log_data', None)
        parent_indices = None
        if log_data is None:
            inner = getattr(self._source, '_source', None)
            log_data = getattr(inner, '_log_data', None) if inner else None
            parent_indices = getattr(self._source, '_indices', None)
        if log_data is None:
            return -1

        # Step 1: binary search for line_number in LogModel
        lo, hi = 0, len(log_data) - 1
        source_row = -1
        while lo <= hi:
            mid = (lo + hi) // 2
            mid_line = log_data[mid].get('line_number', -1)
            if mid_line == line:
                source_row = mid
                break
            elif mid_line < line:
                lo = mid + 1
            else:
                hi = mid - 1
        if source_row < 0:
            return -1

        if parent_indices is not None:
            # Search proxy: source_row → parent proxy row → our proxy row
            j = bisect_left(parent_indices, source_row)
            if j >= len(parent_indices) or parent_indices[j] != source_row:
                return -1
            k = bisect_left(self._indices, j)
            if k < len(self._indices) and self._indices[k] == j:
                return k
            return -1
        else:
            # Filter proxy: source_row → our proxy row
            k = bisect_left(self._indices, source_row)
            if k < len(self._indices) and self._indices[k] == source_row:
                return k
            return -1
