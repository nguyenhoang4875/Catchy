# Catchy - Presentation Script for Mentor

---

## 1. What is this tool?

Catchy is a **desktop log viewer** application built with Python and PySide6 (Qt/QML).

- It provides a modern UI for viewing, filtering, searching, and bookmarking log files.
- It also supports **live logcat streaming** from Android devices via ADB.
- Tech stack: Python backend (MVVM pattern) + QML frontend, packaged as a standalone `.exe` via PyInstaller.

---

## 2. What problem does it solve?

When debugging embedded/Android systems, developers face these pain points:

| Problem | How Catchy solves it |
|---------|---------------------|
| Log files are huge and hard to navigate | Fast table view with virtual scrolling |
| Finding relevant logs is tedious | Multi-keyword search with color-coded highlights |
| Filtering by process/tag requires CLI tools | Visual filter management (add/remove/toggle with color) |
| Switching between live stream and file analysis | Supports both logcat streaming and static file loading |
| Losing track of important lines | Bookmark system to save and jump to key lines |
| No quick way to share log snippets | Copy-to-clipboard support |
| Dark/light environment preference | Theme switching (Dark / Light) |

---

## 3. Key Features

1. **Log file loading** — Open and parse large log files with progress indicator
2. **Live logcat streaming** — Real-time ADB logcat with auto-scroll
3. **Process filters** — Named color-coded filters, persisted to JSON
4. **Multi-keyword search** — Highlight multiple terms with different colors (10-color palette)
5. **Bookmarks** — Save/jump to important log lines
6. **Scrcpy integration** — Mirror Android screen alongside log viewing
7. **Column visibility** — Show/hide columns for focused reading
8. **Theme support** — Dark and Light modes
9. **Standalone executable** — Distributable without Python installation

---

## 4. Architecture Overview

```
┌───────────────────────────────────────────────┐
│           QML Layer (View)                     │
│  main.qml · LogViewTable · Panels · Styles    │
└───────────────────────┬───────────────────────┘
                        │ Signals / Properties
┌───────────────────────▼───────────────────────┐
│           Controller (Facade)                  │
│         Central coordinator                    │
└──┬────────┬────────┬────────┬────────┬────────┘
   │        │        │        │        │
   ▼        ▼        ▼        ▼        ▼
LogModel  Filter  Search  Bookmark  Helper
+ Proxy    Log     Log              + Toast
```

- **Backend (Python):** Controller orchestrates all services; Worker threads handle file I/O and streaming.
- **Frontend (QML):** Declarative UI with data binding to Python properties and signals.
- **Pattern:** MVVM-like — Python = ViewModel/Service, QML = View.

---

## 5. Benefits

- **Productivity** — No more manual `grep` or `ctrl+F` in text editors for log analysis.
- **Visual clarity** — Color-coded processes and search terms make patterns easy to spot.
- **All-in-one** — Combines file viewer + live stream + device mirror in one tool.
- **Cross-workflow** — Works for both post-mortem log analysis and real-time debugging.
- **Lightweight** — Single executable, no heavy IDE required.
- **Customizable** — Filters and configs are saved per user, ready for next session.

---

## 6. Benchmark / Performance

| Metric | Current Status | Planned Improvement |
|--------|---------------|---------------------|
| File loading | Blocking, full read into memory | Batched streaming (50K lines/batch) with progress |
| Memory | Duplicate storage (list + dict) | Single store, string interning |
| Parsing | 3 regex patterns per line | Format pre-detection, single parser |
| Filter change | O(N) full recolor | Optimized proxy-based filtering |
| Target | Handle 100MB–500MB+ files smoothly ||

---

## 7. Demo Flow (suggested)

1. **Open a log file** → Show loading progress and table rendering
2. **Add a filter** → Show color-coded process highlighting
3. **Search keywords** → Show multi-color highlights
4. **Bookmark a line** → Show bookmark panel and jump-to
5. **Switch to logcat** → Show live streaming with auto-scroll
6. **Toggle theme** → Show Dark/Light switch
7. (Optional) **Scrcpy** → Show device screen mirroring

---

## 8. Future Roadmap

- Chunked/batched file loading for large files (500MB+)
- Zero-regex span parsing for speed
- String interning to reduce memory
- Export with chunking (50MB splits)
- Remote log streaming via SSH

---

## 9. Summary / Closing

> "Catchy turns raw log data into an organized, searchable, visual workspace — helping developers debug faster without leaving one tool."

Questions?
