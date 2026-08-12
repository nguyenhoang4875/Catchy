# Catchy — Deep Analysis

## 1. How to Load the Large File

### Current Implementation

Catchy loads files via `_loadFile()` → `_loadFileBatched()` worker → `LogModel.loadLogFile()`.

**Architecture:**
```
User clicks Open → _loadFile(path)
    → Sets _loadCancelled = False
    → Sets loadingFileName, loadProgress=0, isLoading=True, showLoadingScreen=True
    → Clears model via updateData([]), resets _nextLineNum=1, _trimmedOffset=0
    → Clears bookmarks
    → Spawns Worker(_loadFileBatched, file_path) on _loadLogFileThread
    → Worker.batchLoaded connects to _onBatchLoaded (progress only)
    → Worker.taskCompleted connects to _onFileLoadComplete
    → Worker calls _loadFileBatched(path)
        → Sets filter colors on model
        → Calls LogModel.loadLogFile(path, colors, progress_callback, cancel_flag)
        → progress_callback emits worker.batchLoaded(None, pct) — no data, just float
        → Returns all entries at once
    → Main thread receives all_entries via taskCompleted signal
    → _onFileLoadComplete: single updateData(all_entries) = beginResetModel + assign + endResetModel
    → Sets _nextLineNum, isLoading=False, showLoadingScreen=False, logViewReady=True
    → Emits loadLogFileCompleted, shows toast with record count
```

**Key mechanisms in `loadLogFile()`:**

| Mechanism | Detail |
|-----------|--------|
| Binary read | Opens file in `'rb'` mode with 64KB buffer (`IO_BUFFER_SIZE = 65536`) |
| Chunk-based I/O | Reads 64KB chunks, splits by `b'\n'`, handles partial lines via `remainder` |
| Format pre-detection | `detect_format()` reads first 20 lines, tries fast parsers first, then regex fallback |
| Fast parsers | `_parse_compact_fast` uses `str.find()` (C-layer); `_parse_iso_fast` uses regex; `_parse_logcat_regex` uses regex |
| Tag interning | `TagInternPool` deduplicates repeated tag strings via `_pool` dict |
| Progress reporting | Every 5,000 lines, emits progress as `bytes_read / file_size` (capped at 0.99 until done) |
| Cancellation | Checks `cancel_flag()` lambda each chunk iteration |
| Single model update | After all entries parsed, ONE `beginResetModel/endResetModel` call (no N×addRows) |

**Performance characteristics:**
- File I/O is fully offloaded to a worker thread — UI stays responsive
- The 64KB binary read + manual split is faster than Python's `readline()`
- Format detection picks ONE parser for the entire file — 1 regex/line instead of 3
- Fast path inlines tag interning directly in the parse closure (avoids `_normalize_entry`)
- No duplicate data storage — only `_log_data` list (no `_logDict`)
- Progress callback is lightweight (just a float signal via `batchLoaded(None, pct)`)
- `_onBatchLoaded` on main thread only updates `loadProgress` property — no model changes until complete

**Constants (from `_Defines.py`):**
```python
BATCH_SIZE           = 50_000      # Records per batch during file loading (available, not currently batched)
IO_BUFFER_SIZE       = 65536       # 64KB I/O buffer for file read/write
WRITE_CHUNK_SIZE     = 10_000      # Records per chunk during file saving
MAX_LOGCAT_FILE_SIZE = 50 * 1024 * 1024  # 50MB file rotation threshold
```

**Parser selection logic in `loadLogFile()`:**
```python
fmt, fallback_pattern = detect_format(file_path)
fast_parser = _FAST_PARSERS.get(fmt)  # {ISO: _parse_iso_fast, LOGCAT: _parse_logcat_regex, COMPACT: _parse_compact_fast}
if fast_parser:
    # Closure with inlined tag interning — skips _normalize_entry
    def parse_fn(line): ...
elif fallback_pattern:
    # Uses detected regex + _normalize_entry
    def parse_fn(line): ...
else:
    parse_fn = self._parse_line  # tries all 3 regex patterns
```

**Cancellation support:**
- `Controller.cancelLoad()` sets `_loadCancelled = True`
- Worker checks `cancel_flag=lambda: self._loadCancelled` each 64KB chunk
- Partial results are still returned and loaded into model

## 2. How to Split the File

### Logcat File Rotation (Splitting on Write)

When streaming from `adb logcat`, Catchy splits output files at a size threshold:

```
MAX_LOGCAT_FILE_SIZE = 50 * 1024 * 1024  # 50MB per file
```

**Rotation mechanism in `_runLogcat()`:**
```
adb logcat -v threadtime → stdout line
    → Write line to current file (line-buffered: buffering=1)
    → Accumulate current_size += len(line.encode('utf-8'))
    → If current_size >= 50MB:
        → Close current file
        → Generate new filename: logcat_YYYYMMDD_HHMMSS_mmm.log
        → Append new path to _logcatFileQueue (deque)
        → Open new file, reset current_size = 0
    → Loop ends when process.poll() is not None
```

**Reader follows the rotation via `_streamLogcatFile()`:**
```
Open file[0] → seek to end → read new lines
    → Parse each line with processLineDataLogcat()
    → Append parsed entry to _logcatBuffer (deque)
    → When readline() returns empty:
        → Check if _logcatFileQueue has more files (writer rotated)
        → If yes: close current file, clear _logcatBuffer, open next file from start
        → If no: sleep 100ms and retry
    → Loop continues while _logcatStreaming is True
```

### Chunked File Saving (Splitting on Export)

When saving logs to disk via `_writeLogToFile()`:

```
WRITE_CHUNK_SIZE = 10_000 rows per chunk
```

**Mechanism:**
```python
with open(file_path, 'w', encoding='utf-8', buffering=IO_BUFFER_SIZE) as file:
    for start in range(0, total, WRITE_CHUNK_SIZE):
        end = min(start + WRITE_CHUNK_SIZE, total)
        chunk_lines = [format_log_line(log_data[i]) for i in range(start, end)]
        file.write('\n'.join(chunk_lines))
        if end < total:
            file.write('\n')
        self._saveProgressVal = end / total
        self.saveProgressChanged.emit()
    file.write('\n')  # final newline
```

This prevents building a single massive string in memory — keeps peak memory bounded to ~10K formatted lines at a time. Progress is emitted via `saveProgressChanged` signal after each chunk.

---

## 3. How to Handle When Stream Too Much Log

### Problem: Unbounded logcat can produce millions of lines

Catchy uses multiple strategies to handle high-throughput streaming:

### 3.1 Buffered Batch Insert (Rate Limiting UI Updates)

```
_logcatBuffer = deque()           # Thread-safe accumulator
_logcatFlushTimer interval = 100ms  # Flush at most 10× per second
```

**Flow:**
```
Reader thread (_streamLogcatFile) → parses line via processLineDataLogcat → appends to _logcatBuffer
    ↓ (every 100ms)
Main thread timer fires → _flushLogcatBuffer():
    → Drain all entries from deque via popleft()
    → _assignLineNums(entries): assign sequential _nextLineNum per entry
    → _batchInsert(entries): calls logviewModel.addRows(entries)
    → addRows: single beginInsertRows/endInsertRows for entire batch
```

This decouples the parsing rate from the UI refresh rate. Even at 100K lines/sec input, the UI only updates 10 times/second with batched inserts.

### 3.2 File Rotation (Prevent Single File Growing Unbounded)

- Writer rotates at 50MB threshold (`MAX_LOGCAT_FILE_SIZE`)
- Reader clears `_logcatBuffer` on rotation and opens new file from the beginning
- Old files remain on disk for post-mortem analysis
- File queue is managed via `_logcatFileQueue` (deque)

### 3.3 Auto-Scroll Pause = Timer Stop

```python
def _onAutoScrollDownChanged(self):
    if self.helper.autoScrollDown:
        if self._logcatStreaming:
            self._flushLogcatBuffer()     # drain pending
            self._logcatFlushTimer.start() # resume flushing
    else:
        self._logcatFlushTimer.stop()   # stop flushing to UI
```

When user scrolls up (pauses auto-scroll):
- Timer stops → no UI updates → no model changes → smooth scrolling
- Buffer keeps accumulating in background (reader thread still runs)
- When user re-enables auto-scroll, buffer is flushed immediately then timer restarts

### 3.4 trimRows() — Row Cap for Memory (Available, Not Active)

```python
def trimRows(self, count):
    """Remove count rows from the front to enforce a max row cap."""
    if count <= 0:
        return
    count = min(count, len(self._log_data))
    self.beginRemoveRows(QModelIndex(), 0, count - 1)
    del self._log_data[:count]
    self.endRemoveRows()
```

This method exists as a utility but is **not currently called** anywhere. The old `MAX_LOG_ROWS` cap has been removed. Instead, logcat streaming relies on file rotation (50MB per file) to bound disk usage, and `_trimmedOffset` tracking in Controller would support row trimming if re-enabled in the future.

### 3.5 Lightweight Parsing Path

For logcat specifically, uses `processLineDataLogcat()` which only tries the `logcat_pattern` regex — no multi-format detection overhead per line.

```python
def processLineDataLogcat(self, lineData, colors):
    match = logcat_pattern.match(lineData)
    if match:
        log_entry = self._normalize_entry(match.groupdict())
        return (True, log_entry)
    else:
        return (False, None)
```

---

## 4. Filter / Color System

### Lazy Color Computation

Colors are NOT stored per-row. They are computed on-demand in `LogModel.data()`:

```python
def data(self, index, role=Qt.DisplayRole):
    entry = self._log_data[row]
    if role == ROLE_FILTER_COLOR:
        return self._filter_color_for_entry(entry, self._colors)
    if role == ROLE_LEVEL_COLOR:
        return self._level_color_for_entry(entry)
    if role == Qt.DecorationRole:
        return self._color_for_entry(entry, self._colors)
```

- `_filter_color_for_entry`: iterates pre-compiled `_compiled_colors` list `(pattern_str, compiled_re, color)`, matches against `PROCESS_NAME`
- `_level_color_for_entry`: direct `LOG_LEVEL_COLORS` dict lookup
- `setFilterColors()` bumps `_filter_version`, emits `dataChanged` for all rows with color roles

### SortFilterProxyModel

Custom Python-based proxy model (NOT Qt's QSortFilterProxyModel):
- Maintains `_indices` list mapping proxy_row → source_row
- `_rebuild()`: one O(N) loop + single `beginResetModel/endResetModel`
- Optimized paths: `_fast_tag_filter` for tag-only criteria, `_general_filter` for multi-field
- Streaming support: `_on_rows_inserted` checks new rows and appends matching indices
- `_on_rows_removed`: adjusts indices using `bisect_left/bisect_right`
- `rowLineNum()`: binary search for line number in LogModel data

---

## 5. Threading Model

| Thread | Purpose | Lifecycle |
|--------|---------|-----------|
| `_loadLogFileThread` | File loading + file saving | Started per operation, quit + wait on completion |
| `_logcatThread` | Runs `_runLogcat()` (adb → file writer) | Started by `startLogcat()`, stopped by `_stopLogcatProcess()` |
| `_logcatStreamThread` | Runs `_streamLogcatFile()` (file → buffer reader) | Started by `startLogcat()`, stopped by `_stopLogcatProcess()` |
| `_filterThread` | `addFilter()` / `updateFilter()` operations | Started per operation |
| `_searchThread` | `_doSearch()` / `applySearchQuery()` | Started per search execution |

All workers use the `Worker(QObject)` class with `taskCompleted` signal. Worker is moved to thread, `thread.started` connects to `worker.run()`.

---

## Summary Table

| Concern | Strategy | Key Code |
|---------|----------|----------|
| Large file load | Worker thread + 64KB binary chunks + format pre-detect + single model reset | `loadLogFile()`, `_loadFileBatched()`, `_onFileLoadComplete()` |
| File splitting (write) | 50MB rotation threshold, deque-based file queue | `_runLogcat()`, `MAX_LOGCAT_FILE_SIZE` |
| File splitting (save) | 10K row chunks with progress | `_writeLogToFile()`, `WRITE_CHUNK_SIZE` |
| Stream flood control | 100ms flush timer + deque buffer + batch insert | `_flushLogcatBuffer()`, `_logcatFlushTimer` |
| Memory control | Tag interning + single data store + trimRows() | `TagInternPool`, `trimRows()` |
| UI responsiveness | Auto-scroll pause stops timer; progress-only signals during load | `_onAutoScrollDownChanged()` |
| Lazy colors | Computed in `data()` role handler, not stored per-row | `_filter_color_for_entry()`, `_level_color_for_entry()` |
| Fast filtering | Python proxy with precomputed index list, no C++→Python per-row calls | `SortFilterProxyModel._rebuild()` |
