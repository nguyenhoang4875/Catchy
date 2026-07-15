Task: Handle large log files (main and important feature)
Goal: open and use 200MB+ logs smoothly without UI freeze, and make search fast.

## 0) Guardrails (do before coding)
- Keep existing features working during migration: filter colors, bookmarks, details panel, save log, search result table, streaming/logcat.
- Avoid big-bang rewrite. Implement in phases with compatibility checkpoints.
- Define success metrics first:
	- Open 200MB file: UI remains responsive (no visible freeze > 200ms on main interactions).
	- Initial usable table after indexing stage, not after full parse.
	- Memory usage significantly reduced vs current full-object load.
	- Search starts in background and can be cancelled/restarted.

## 1) Baseline + diagnosis (current architecture)
- Add lightweight timing logs (open, parse, first render, search latency) for current flow.
- Test with representative files: 50MB, 200MB, malformed lines, mixed formats.
- Capture baseline:
	- total load time
	- peak RAM
	- UI responsiveness while loading/searching
- Record findings in docs (for before/after comparison).

## 2) New large-file architecture skeleton
- Create new backend components (no controller switch yet):
	- `components/_LogFileReader.py`
	- `components/_LogFileIndexer.py`
	- `components/_LogCache.py` (LRU)
	- `components/_VirtualLogModel.py`
	- `components/_SearchEngine.py`
- Keep old `LogModel` intact for fallback.
- Reuse existing parse regex logic from `LogModel` in a shared parser utility to avoid behavior mismatch.

## 3) Line indexing first (critical milestone)
- Implement byte-offset indexing in background thread:
	- Build offsets list by scanning file in chunks.
	- Store line start offsets (uint64-like integers).
- Expose progress signal (0-100 + message) for loading screen.
- Completion output:
	- total line count
	- offset index ready
- Milestone acceptance:
	- 200MB indexing runs without blocking UI.
	- user can cancel indexing safely.

## 4) Virtual model (lazy row parsing)
- Implement `VirtualLogModel(QAbstractTableModel)`:
	- rowCount = indexed line count
	- data(row, col): read line by offset, parse on demand
	- keep role compatibility (`lineNumber`, colors, display columns)
- Add LRU cache for parsed rows (start with 5k-10k rows, configurable).
- Optional prefetch around viewport rows (small window ahead/behind).
- Milestone acceptance:
	- scrolling is smooth on 200MB file.
	- memory stays bounded; no full-file dict/list in RAM.

## 5) Controller integration (safe migration)
- Add feature flag in controller/config:
	- `largeFileMode` ON for file sources, OFF fallback to old model if needed.
- Update open-file flow:
	- old path: parse-all model (fallback)
	- new path: index -> bind virtual model -> ready
- Keep signal contracts used by QML stable (`logViewReady`, `loadLogFileCompleted`, details updates).
- Update details retrieval to fetch from virtual model/index (not `_logDict` full mirror for big files).

## 6) Search engine redesign (fast + non-blocking)
- Implement background search over file bytes/chunks (or mmap where safe):
	- input: query/regex, case sensitivity options
	- output: matching line numbers (not full log objects)
- Add cancellation token/versioning so rapid re-search does not pile up stale jobs.
- Wire search results table to line-number mapping + proxy lookup.
- Milestone acceptance:
	- searching large file does not freeze UI.
	- first results appear incrementally.

## 7) Filtering strategy for large files
- Short term: keep current proxy filter for visible rows (minimal behavior change).
- Medium term: add background filter scan returning matching line numbers for true large-file scalability.
- Ensure color/filter logic matches current behavior for tag/pid/tid regex rules.

## 8) Feature compatibility pass (must not regress)
- Verify and fix integrations with virtual model:
	- bookmark add/remove/highlight
	- row jump by original line number
	- details pane + search term highlight
	- copy selected cells
	- save log behavior (decide: save visible rows vs full source + document it)
	- show less columns + theme behavior unchanged

## 9) Robustness + edge cases
- Encoding strategy: UTF-8 with fallback (`errors="replace"`) for invalid bytes.
- Handle very long lines safely.
- Handle files with no newline at EOF.
- Handle malformed/unmatched lines consistently (show raw message fallback if needed).
- Ensure thread cleanup/cancel on:
	- new file open
	- source switch (file/logcat/ssh)
	- app exit

## 10) Performance validation checklist
- Open + scroll tests:
	- 50MB, 200MB, and stress file >200MB if available.
- Measure and compare against baseline:
	- time to usable table
	- peak memory
	- search latency (first result + full completion)
- UI behavior checks:
	- loading indicator correctness
	- no deadlocks/crashes during rapid user actions

## 11) Rollout plan
- Phase A: ship index + virtual model for local file open (search unchanged).
- Phase B: ship background search engine + cancellation.
- Phase C: optional background filtering/index optimizations.
- Keep fallback switch to old model until Phase B passes validation.

## 12) Definition of done
- 200MB local log file opens and is usable without UI hang.
- Search is responsive and asynchronous.
- Memory remains bounded (no full parsed object graph for entire file).
- Existing key UX features still work.
- Baseline vs new metrics documented.
