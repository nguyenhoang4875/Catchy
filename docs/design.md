# Design: Large File Performance for Catchy

## 1. Problem Statement

Catchy originally had critical performance limitations when handling large log files (100MB–500MB+). Most have been resolved:

| Issue | Original Behavior | Current Status |
|-------|-----------------|--------|
| Blocking file load | Entire file read into memory at once | **DONE** — Worker thread + 64KB binary chunks, UI responsive |
| Regex parsing overhead | 3 regex patterns tried per line | **DONE** — Format pre-detection, 1 parser/line |
| Duplicate data storage | `_log_data` + `_logDict` store same entries | **DONE** — `_logDict` removed, single store only |
| Save is blocking | Sequential row iteration | **DONE** — Chunked save (10K rows) on worker thread with progress |
| No progress feedback | Loading/saving shows no progress | **DONE** — `loadProgress` / `saveProgress` properties + `isLoading` / `isSaving` states |
| Full recolor on filter change | Iterate ALL rows per change | **DONE** — Lazy color computation in `data()` roles |

## 2. Reference: EasyLog Architecture (C# — handles 500MB+ well)

EasyLog solves the same problem using these key strategies:

### 2.1 Streaming + Batched Loading
- **FileStream** with `SequentialScan` + `Asynchronous` flags, 64KB read buffer
- **50,000-record batches**: parse lines into batches, append to storage per batch
- **BulkAppend()**: in-memory-only append during file load (no disk I/O)
- **Event-driven progress**: fires `LogRecordsBatchLoaded` every 50K records → UI updates

### 2.2 Zero-Regex Span Parsing
- Manual `Span<char>` character parsing — no regex allocations
- Inline digit parsing (`(c-'0')*10 + (c-'0')`) instead of `int.Parse()`
- **~10× faster** than regex-based parsing

### 2.3 String Interning
- **Tag interning** via `ConcurrentDictionary` pool
- Millions of duplicate tag strings → 1 shared reference per unique tag
- **~60% heap reduction** for string data

### 2.4 Storage Architecture
- `RawSpoolStore`: single `List<LogRecord>` as primary store (no duplicates)
- `RecentRingBuffer`: fixed-size FIFO cache (2048 items) for live sessions
- `SnapshotView()`: O(1) read-only reference (no copy) after load completes

### 2.5 Threading Model
- `Task.Run()` offloads file I/O to thread pool
- UI thread only receives batched events
- `SemaphoreSlim` serializes concurrent appends
- `Interlocked.Increment()` for lock-free RowId assignment

### 2.6 Export with Chunking
- Splits output into **50MB chunks**
- Writes temp files → renames → optional compression
- Prevents memory spike from building one giant string

## 3. Implementation Status

### 3.1 Chunked / Batched File Loading — IMPLEMENTED

**Current architecture:**

```
                 ┌─────────────────────┐
                 │   User opens file    │
                 └──────────┬──────────┘
                            │
                 ┌──────────▼──────────┐
                 │  _loadFile(path):    │
                 │  isLoading=True      │
                 │  showLoadingScreen   │
                 │  updateData([])      │
                 └──────────┬──────────┘
                            │
                 ┌──────────▼──────────┐
                 │  Worker thread starts│
                 │  _loadFileBatched()  │
                 │  Opens file 'rb'     │
                 │  64KB buffer chunks  │
                 └──────────┬──────────┘
                            │
              ┌─────────────▼─────────────┐
              │  Read 64KB chunk           │
              │  Split by b'\n'            │
              │  Parse via detected format │
              │  Append to all_entries     │
              └─────────────┬─────────────┘
                            │
              ┌─────────────▼─────────────┐
              │  Every 5K lines:           │
              │  Emit progress float       │──► Main thread: update loadProgress
              │  (no data transfer)        │
              └─────────────┬─────────────┘
                            │
                    ┌───────▼───────┐
                    │  More chunks? │──Yes──► Loop back to read
                    └───────┬───────┘
                            │ No
              ┌─────────────▼─────────────┐
              │  Return all_entries        │
              │  via taskCompleted signal  │──► Main thread: single updateData()
              └───────────────────────────┘
```

**Implementation details:**
- `IO_BUFFER_SIZE = 65536` (64KB binary read buffer)
- `BATCH_SIZE = 50_000` constant defined but not used for batched emission (entire file returned at once)
- Progress emitted via `worker.batchLoaded.emit(None, pct)` — only float, no data payload
- `_onBatchLoaded` just updates `self.loadProgress = progress`
- `_onFileLoadComplete` does single `updateData(all_entries)` → `beginResetModel/endResetModel`
- Cancellation via `_loadCancelled` flag checked each chunk

### 3.2 Format Pre-Detection — IMPLEMENTED

```
┌──────────────────────────────────┐
│  detect_format(file_path):       │
│  Read first 20 lines             │
│  Try each fast parser first:     │
│    _FMT_ISO → _parse_iso_fast    │
│    _FMT_LOGCAT → _parse_logcat_regex │
│    _FMT_COMPACT → _parse_compact_fast │
│  Pick first with ≥1 hit          │
│  If none: try regex patterns     │
│  Return (fmt_enum, fallback_pat) │
└──────────────┬───────────────────┘
               │
     ┌─────────▼──────────┐
     │ Build parse closure │
     │ with inlined tag    │
     │ interning           │
     │ Use only chosen     │
     │ parser + rare       │
     │ fallback path       │
     └────────────────────┘
```

**Fast parsers available:**
- `_parse_compact_fast`: Pure `str.find()` bracket parsing — no regex (C-layer speed)
- `_parse_iso_fast`: Uses `log_pattern` regex (ISO format)
- `_parse_logcat_regex`: Uses `logcat_pattern` regex (Android threadtime)

**Impact:** Reduces from 3 patterns tried per line to 1 chosen parser + rare fallback.

### 3.3 Eliminate Duplicate Data Storage — IMPLEMENTED

| Before | After (Current) |
|--------|----------|
| `_log_data`: list of dicts | `_log_data`: list of dicts (single store) |
| `_logDict`: dict mapping `line_number → entry` | **Removed** |
| 2× memory | 1× memory |

- Detail panel lookups use direct index: `_log_data[line - 1 - _trimmedOffset]`
- `_trimmedOffset` tracks rows trimmed from front (for future row cap support)

### 3.4 String Interning for Tags — IMPLEMENTED

```python
class TagInternPool:
    __slots__ = ('_pool',)
    def __init__(self):
        self._pool = {}
    def intern(self, tag):
        existing = self._pool.get(tag)
        if existing is not None:
            return existing
        self._pool[tag] = tag
        return tag
    def clear(self):
        self._pool.clear()

_tag_pool = TagInternPool()  # module-level singleton
```

Used in two places:
1. Fast parser closure in `loadLogFile()`: inlines `_tag_pool.intern()` for tag dedup
2. `_normalize_entry()`: interns tag for streaming/fallback paths

### 3.5 Batched / Chunked File Saving — IMPLEMENTED

```
┌────────────────────────────────┐
│  Worker thread starts           │
│  _writeLogToFile(file_path)     │
│  Open file with 64KB buffer     │
│  WRITE_CHUNK_SIZE = 10,000 rows │
└────────────┬───────────────────┘
             │
  ┌──────────▼──────────┐
  │  Format chunk of     │
  │  rows via            │
  │  format_log_line()   │
  │  Join with '\n'      │
  └──────────┬──────────┘
             │
  ┌──────────▼──────────┐
  │  Write buffer to file│
  │  Update _saveProgressVal │
  │  Emit saveProgressChanged │
  └──────────┬──────────┘
             │
     ┌───────▼───────┐
     │  More rows?   │──Yes──► Loop
     └───────┬───────┘
             │ No
  ┌──────────▼──────────┐
  │  Write final '\n'    │
  │  Close file          │
  │  Return True/False   │
  └─────────────────────┘
```

**Implementation:**
- Uses `IO_BUFFER_SIZE` (64KB) for output file buffering
- Emits `saveProgressChanged` signal per chunk → `isSaving` and `saveProgress` properties on Controller
- `onLogFileSaved(success)` shows toast and resets `isSaving`
- Default filename uses `YYYYMMDD_HHMMSS.log` format

### 3.6 Progress Feedback UI — IMPLEMENTED

Controller exposes these properties to QML:

```python
# Loading
isLoading: bool           # True during file load
loadProgress: float       # 0.0–1.0
loadingFileName: str      # basename of file being loaded
showLoadingScreen: bool   # controls LoadingScreen.qml visibility

# Saving
isSaving: bool            # True during file save
saveProgress: float       # 0.0–1.0

# Cancellation
cancelLoad() slot         # sets _loadCancelled flag
```

QML components: `LoadingScreen.qml` displays during load.

### 3.7 Lazy Color Computation — IMPLEMENTED

| Before | After (Current) |
|--------|----------|
| Compute/store color per entry during load | No color stored per entry |
| `reapplyProcessColors()` iterates ALL rows | `setFilterColors()` emits `dataChanged` for color roles |
| O(N) recompute per filter change | O(visible rows) via Qt's lazy `data()` calls |

**Current implementation in `LogModel.data()`:**
```python
if role == ROLE_FILTER_COLOR:
    return self._filter_color_for_entry(entry, self._colors)
if role == ROLE_LEVEL_COLOR:
    return self._level_color_for_entry(entry)
```

- `setFilterColors()` pre-compiles regex patterns into `_compiled_colors` list
- `_filter_color_for_entry()` iterates compiled patterns against `PROCESS_NAME`
- `_level_color_for_entry()` is a simple dict lookup (`LOG_LEVEL_COLORS`)
- On filter change: bumps `_filter_version`, emits `dataChanged` for all rows with color roles
- Qt only calls `data()` for visible rows → effective O(visible) not O(N)

### 3.8 Architecture Overview (Current)

```
┌─────────────────────────────────────────────────────────┐
│                     QML UI Layer                         │
│  ┌───────────┐  ┌──────────┐  ┌────────────────────┐   │
│  │LogViewTable│  │LoadScreen│  │ Progress/Toast      │   │
│  └─────┬─────┘  └────┬─────┘  └─────────┬──────────┘   │
│        │              │                  │               │
│  ┌─────▼──────────────▼──────────────────▼──────────┐   │
│  │     SortFilterProxyModel (Python, index-based)    │   │
│  │     _indices: proxy_row → source_row              │   │
│  │     _rebuild(): O(N) filter + beginResetModel     │   │
│  │     _on_rows_inserted(): streaming append         │   │
│  └──────────────────────┬───────────────────────────┘   │
│                         │                                │
│  ┌──────────────────────▼───────────────────────────┐   │
│  │     LogModel (QAbstractTableModel)                │   │
│  │     _log_data: List[dict]  (single store)         │   │
│  │     Lazy color computation in data() roles        │   │
│  │     addRows() batch insert with begin/end signals │   │
│  │     updateData() single reset for file loads      │   │
│  └──────────────────────┬───────────────────────────┘   │
└─────────────────────────┼───────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────┐
│                   Python Backend                         │
│                                                          │
│  ┌──────────────────┐  ┌───────────────────────────┐    │
│  │  Worker Threads   │  │  Controller               │    │
│  │  (QThread)        │  │  - openFileDialog()       │    │
│  │                   │  │  - saveLogFile()           │    │
│  │  File load:       │  │  - startLogcat()          │    │
│  │  64KB binary read │  │  - loadProgress property  │    │
│  │  Format pre-det   │  │  - saveProgress property  │    │
│  │  Tag interning    │  │  - cancelLoad() slot      │    │
│  │  Single return    │  │  - isLoading/isSaving     │    │
│  │                   │  │                           │    │
│  │  Logcat writer:   │  │  Logcat streaming:        │    │
│  │  adb → file       │  │  - _logcatBuffer (deque)  │    │
│  │  50MB rotation    │  │  - 100ms flush timer      │    │
│  │                   │  │  - _flushLogcatBuffer()   │    │
│  │  Logcat reader:   │  │  - _batchInsert()         │    │
│  │  file → buffer    │  │  - auto-scroll pause      │    │
│  └──────────────────┘  └───────────────────────────┘    │
│                                                          │
│  ┌──────────────────────────────────────────────────┐    │
│  │  TagInternPool (_tag_pool singleton)              │    │
│  │  - intern(tag) → pooled string reference          │    │
│  │  - Used in fast parser closures + _normalize_entry│    │
│  └──────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
```

## 4. Performance Targets

| Metric | Original (before) | Target | Current Status |
|--------|---------------|--------|----------------|
| Load 500MB file | Freezes / crashes | < 30s, UI responsive | Worker thread, UI responsive, single model reset |
| Load 100MB file | ~15s, UI frozen | < 5s, UI responsive | Worker thread, progress reporting |
| Save 500MB file | Very slow, no progress | < 20s with progress | Chunked save with progress |
| Memory for 5M records | ~1GB+ (2× duplicate) | ~500MB (single store + interning) | Single store + tag interning |
| Filter change (5M records) | ~5s full recolor | < 0.5s (lazy, visible rows only) | Lazy data() + dataChanged signal |
| Parse speed | ~3 regex/line | 1 regex/line (pre-detected format) | Implemented |

## 5. Risk & Mitigation

| Risk | Mitigation |
|------|-----------|
| Qt model signals from worker thread crash | All model mutations on main thread: `updateData()` in `_onFileLoadComplete`, `addRows()` in `_flushLogcatBuffer` (timer callback) |
| Very large files exceed available RAM | All entries loaded into memory; future: configurable row limit or virtual/paged model |
| Format detection picks wrong parser | Fallback: if chosen parser returns None, `_parse_line()` tries all patterns |
| Breaking existing logcat streaming | Logcat uses separate `processLineDataLogcat()` path, file rotation handles disk |
| Regression in filter/search behavior | Custom `SortFilterProxyModel` keeps same interface, binary search for rowLineNum |

## 6. Future Improvements (Not Yet Implemented)

| Improvement | Description |
|-------------|-------------|
| True batched loading | Currently loads all entries then single reset. Could batch-emit to model for earlier display |
| Row cap for streaming | `trimRows()` exists but unused; could cap in-memory rows for very long logcat sessions |
| Zero-regex manual parsing for logcat | `_parse_logcat_regex` still uses regex; a `str.find()` parser would be faster |
| Virtual/paged model | For files exceeding available RAM, load only visible window + disk-backed store |
| Parallel parsing | Split file into segments, parse in multiple threads, merge results |
