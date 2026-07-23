# Design: Large File Performance for Catchy

## 1. Problem Statement

Catchy currently has critical performance limitations when handling large log files (100MB–500MB+):

| Issue | Current Behavior | Impact |
|-------|-----------------|--------|
| Blocking file load | Entire file read into memory at once via `loadLogFile()` | UI freezes for 500MB files |
| 50K row hard cap | `MAX_LOG_ROWS = 50,000` discards older entries | Cannot view full large file |
| Regex parsing overhead | 3 regex patterns tried per line (`_parse_line()`) | Slow parsing for millions of lines |
| Duplicate data storage | `_log_data` (list) + `_logDict` (dict) store same entries | 2× memory usage |
| Save is blocking | `_writeLogToFile()` iterates all rows sequentially | Slow for large datasets |
| No progress feedback | Loading/saving shows no progress to user | Appears frozen on large files |
| Full recolor on filter change | `reapplyProcessColors()` iterates ALL rows | O(N) per filter change |

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

## 3. Proposed Design for Catchy

### 3.1 Chunked / Batched File Loading

**Replace the current all-at-once `loadLogFile()` with a batched streaming approach.**

```
                 ┌─────────────────────┐
                 │   User opens file    │
                 └──────────┬──────────┘
                            │
                 ┌──────────▼──────────┐
                 │  Worker thread starts│
                 │  StreamReader(file)  │
                 │  64KB buffer         │
                 └──────────┬──────────┘
                            │
              ┌─────────────▼─────────────┐
              │  Read lines into batch    │
              │  BATCH_SIZE = 50,000      │
              │  Parse each line          │
              └─────────────┬─────────────┘
                            │
              ┌─────────────▼─────────────┐
              │  Emit batchLoaded signal  │──► Main thread: addRows(batch)
              │  Include progress %       │──► Main thread: update progress bar
              └─────────────┬─────────────┘
                            │
                    ┌───────▼───────┐
                    │   More lines? │──Yes──► Loop back to read
                    └───────┬───────┘
                            │ No
              ┌─────────────▼─────────────┐
              │  Emit loadComplete signal │──► Main thread: finalize UI
              └───────────────────────────┘
```

**Key changes:**
- New constant: `BATCH_SIZE = 50_000`
- Remove `MAX_LOG_ROWS` cap for file loading (keep for live logcat only)
- Worker emits `batchLoaded(entries, progress_pct)` signal per batch
- Main thread calls `addRows()` per batch (already uses `beginInsertRows/endInsertRows`)
- File opened with buffered reading (`buffering=65536`)

### 3.2 Format Pre-Detection

**Detect the log format once using the first few lines, then use only that parser.**

```
┌──────────────────────────────────┐
│  Read first 20 lines of file     │
│  Try each regex pattern          │
│  Pick the pattern that matches   │
│  most lines (≥1 match = chosen)  │
└──────────────┬───────────────────┘
               │
     ┌─────────▼──────────┐
     │ Use ONLY the chosen │
     │ parser for rest of  │
     │ file (1 regex/line) │
     └────────────────────┘
```

**Impact:** Reduces regex operations from 3×N to 1×N (3× faster parsing).

### 3.3 Eliminate Duplicate Data Storage

**Remove `_logDict` and use `_log_data` list with index-based O(1) access.**

| Current | Proposed |
|---------|----------|
| `_log_data`: list of dicts | `_log_data`: list of dicts (primary store) |
| `_logDict`: dict mapping `line_number → entry` | **Removed** — use `_log_data[index]` directly |
| 2× memory | 1× memory |

- `_logDict` is currently used for detail panel lookups by `line_number`
- Replace with direct index access: `_log_data[row_index]`
- For logcat streaming where rows are trimmed, maintain an offset counter: `actual_index = line_number - _trimmed_count`

### 3.4 String Interning for Tags

**Pool frequently repeated tag strings to reduce memory.**

```python
class TagInternPool:
    def __init__(self):
        self._pool = {}

    def intern(self, tag: str) -> str:
        if tag not in self._pool:
            self._pool[tag] = tag
        return self._pool[tag]
```

**Savings estimate for 10M records with ~300 unique tags:**
- Without interning: 10M × ~20 bytes/tag = ~200MB
- With interning: 300 × 20 bytes + 10M × 8 bytes (references) = ~80MB
- **~120MB saved**

### 3.5 Batched / Chunked File Saving

**Replace the current sequential save with a chunked write approach.**

```
┌────────────────────────────┐
│  Worker thread starts      │
│  Open output file          │
│  WRITE_CHUNK = 10,000 rows │
└────────────┬───────────────┘
             │
  ┌──────────▼──────────┐
  │  Format chunk of     │
  │  rows into string    │
  │  buffer (join lines) │
  └──────────┬──────────┘
             │
  ┌──────────▼──────────┐
  │  Write buffer to file│
  │  Emit progress signal│
  └──────────┬──────────┘
             │
     ┌───────▼───────┐
     │  More rows?   │──Yes──► Loop
     └───────┬───────┘
             │ No
  ┌──────────▼──────────┐
  │  Close file          │
  │  Emit saveComplete   │
  └─────────────────────┘
```

**Key changes:**
- Build output in chunks using `'\n'.join()` for batch of rows
- Emit `saveProgress(pct)` signal → UI shows progress
- Use `buffering=65536` on output file for I/O efficiency
- Optional: write only filtered rows (currently saves all)

### 3.6 Progress Feedback UI

**Add a loading/saving progress indicator to the QML UI.**

- Reuse or extend existing `LoadingScreen.qml` component
- Show: percentage, record count, elapsed time
- Support cancellation via a cancel button

```
┌─────────────────────────────────────────┐
│  Loading: data_log.log                  │
│  ████████████░░░░░░░░  62%              │
│  3,100,000 / 5,000,000 records          │
│  Elapsed: 12s                           │
│                              [Cancel]   │
└─────────────────────────────────────────┘
```

### 3.7 Lazy Color Computation

**Defer color computation from load time to render time.**

| Current | Proposed |
|---------|----------|
| Compute color for every entry during `loadLogFile()` | Store entries without color fields |
| `reapplyProcessColors()` iterates ALL rows on filter change | Compute color in `data()` role handler on demand |
| O(N) per filter change | O(visible rows) per filter change |

- `data()` method in `LogModel` already resolves display roles per-cell
- Add color computation in the `filterColor` / `levelColor` role handlers
- Cache computed colors with a dirty flag — invalidate on filter change
- `dataChanged` signal scoped to visible row range only

### 3.8 Architecture Overview (After Changes)

```
┌─────────────────────────────────────────────────────────┐
│                     QML UI Layer                         │
│  ┌───────────┐  ┌──────────┐  ┌────────────────────┐   │
│  │LogViewTable│  │LoadScreen│  │ProgressIndicator   │   │
│  └─────┬─────┘  └────┬─────┘  └─────────┬──────────┘   │
│        │              │                  │               │
│  ┌─────▼──────────────▼──────────────────▼──────────┐   │
│  │          SortFilterProxyModel                     │   │
│  └──────────────────────┬───────────────────────────┘   │
│                         │                                │
│  ┌──────────────────────▼───────────────────────────┐   │
│  │     LogModel (QAbstractTableModel)                │   │
│  │     _log_data: List[dict]  (single store)         │   │
│  │     Lazy color computation in data() roles        │   │
│  │     addRows() / trimRows() with batch signals     │   │
│  └──────────────────────┬───────────────────────────┘   │
└─────────────────────────┼───────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────┐
│                   Python Backend                         │
│                                                          │
│  ┌──────────────────┐  ┌───────────────────────────┐    │
│  │  Worker Thread   │  │  Controller               │    │
│  │  (QThread)       │  │  - openFileDialog()       │    │
│  │                  │  │  - saveLogFile()           │    │
│  │  Batched load:   │  │  - batchLoaded signal     │    │
│  │  50K rows/batch  │  │  - saveProgress signal    │    │
│  │  Format pre-det  │  │  - loadComplete signal    │    │
│  │  Tag interning   │  │  - saveComplete signal    │    │
│  └──────────────────┘  └───────────────────────────┘    │
│                                                          │
│  ┌──────────────────────────────────────────────────┐    │
│  │  TagInternPool                                    │    │
│  │  - intern(tag) → pooled string reference          │    │
│  └──────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
```

## 4. Performance Targets

| Metric | Current (est.) | Target |
|--------|---------------|--------|
| Load 500MB file | Freezes / crashes | < 30s, UI responsive |
| Load 100MB file | ~15s, UI frozen | < 5s, UI responsive |
| Save 500MB file | Very slow, no progress | < 20s with progress |
| Memory for 5M records | ~1GB+ (2× duplicate) | ~500MB (single store + interning) |
| Filter change (5M records) | ~5s full recolor | < 0.5s (lazy, visible rows only) |
| Parse speed | ~3 regex/line | 1 regex/line (pre-detected format) |

## 5. Risk & Mitigation

| Risk | Mitigation |
|------|-----------|
| Qt model signals from worker thread crash | Ensure `addRows()` always called on main thread via signal-slot |
| Very large files exceed available RAM | Add configurable row limit with user warning; future: virtual/paged model |
| Format detection picks wrong parser | Fallback: if chosen parser fails on a line, try others (rare path) |
| Breaking existing logcat streaming | Keep `MAX_LOG_ROWS` cap for logcat only; file load uses separate path |
| Regression in filter/search behavior | Keep existing `SortFilterProxyModel` interface unchanged |
