# LogPilot - AAOS Log Viewer v1.0.0 Release Notes

> Release Date: 2026-04-23

## Overview

LogPilot v1.0.0 is the first stable release of the AAOS Log Viewer for Windows.  
It provides fast log file loading, real-time ADB log collection, advanced filtering, and search capabilities for Android / AAOS logs.

---

## Key Features

### Log Collection
- **ADB Live Capture**: Real-time log streaming via `adb logcat` with 30-second backfill
- **File Loading**: `.log`, `.logcat`, `.txt`, `.zip`, `.7z` — single or multi-file, drag & drop supported
- **Multi-file Timestamp Sorting**: When loading multiple files, all records are sorted by timestamp and re-indexed sequentially

### Search
- **Unified Search Bar** with `|` OR and `&` AND operators (`&` binds tighter)
- **Search Highlight**: Matching terms highlighted in Tag, PID, Message columns (yellow background)
- **Search History Dropdown**: Up to 10 recent searches, keyboard navigation (↑/↓), per-item delete (✕), persisted across app restarts
- **Search Scope**: Toggle between all logs or filtered logs only

### Filter
- **Filter Rules**: Include/Exclude by Text, Tag, PID with per-rule color highlighting
- **Filter Sets**: Save/Load/New filter configurations as JSON files
- **Level Filter**: V/D/I/W/E/F level selection
- **Drag & Drop Reorder**: Priority-based filter rule ordering
- **Batch Select**: Select All checkbox + Enable/Disable Selected

### UI / UX
- **Preview Panel**: Selected log message displayed in left panel with copyable header fields
- **Column Persistence**: Column widths and order saved across app restarts
- **Font Customization**: 7 font families + adjustable log grid font size (A-/A+)
- **Keyboard Navigation**: Home/End, PageUp/PageDown (page-level), ↑/↓, ESC focus release, Ctrl+F search focus, Ctrl+C smart copy
- **About Dialog**: Version info and contact (sungyeon22.kim@lge.com)

### Export
- **7z Archive Export**: Auto-split at 50MB with timestamp-based filenames
- **Re-importable**: Exported files can be reopened with Open Log

### Performance
- **Zero-regex Parser**: Manual `ReadOnlySpan<char>` parser for zero-allocation log parsing
- **Parallel File Loading**: Multi-file parallel parse + timestamp-sorted merge
- **Background Rendering**: LogRowModel creation on background threads (parallelized for 50K+)
- **Memory Optimized**: Tag string interning, LogRecord-reference models, snapshot view (O(1) no-copy)

---

## What's New in v1.0.0 (since v0.0.5 beta)

### New Features
- **About Dialog**: Version info + bug report contact (replaces version badge)
- **File Load Progress + Cancel**: Real-time progress display during file loading with Cancel button at every stage (parsing, sorting, rendering, filter application)
- **Timestamp-sorted Multi-file Load**: Multiple log files are merged by timestamp order with sequential RowId reassignment
- **Spool File Auto-cleanup**: Old spool files (>24h) in `%TEMP%\EasyLog\spool\` automatically cleaned on startup

### Improvements
- **Load Option Dialog Simplified**: Bilingual (Korean/English) concise load mode selection
- **Fixed-size Loading Overlay**: Loading popup width fixed at 360px — Cancel button position stable
- **Cancel Works at All Stages**: CancellationToken passed through rendering and filter application stages
- **Git Hash Removed from Version**: About dialog shows clean version string (e.g., `v1.0.0`)

---

## System Requirements

- **OS**: Windows 10 / 11 (x64)
- **Runtime**: Self-contained (.NET 8 included)
- **Optional**: ADB (for live log capture), 7-Zip (for export)

---

## How to Run

1. Extract the `.7z` package
2. Run `LogPilot.exe`
3. Open log files or start live ADB capture

---

## Package Contents

- `LogPilot.exe` + runtime DLLs (self-contained)
- `LogFilter/` — filter set storage folder
- `README.md` — usage guide
- `sample-logs/aaos-sample.log` — sample log file
- `tools/README.txt` — external tools info

---

## Known Limitations

- No auto-update mechanism
- ADB live capture requires ADB installed or bundled in `tools/`
- 7z export requires 7-Zip installed or bundled in `tools/`
- Settings folder changed from `ALV` (v0.0.4) to `LogPilot` — no auto-migration

---

## Contact

Bug reports & feature requests: **sungyeon22.kim@lge.com**

© 2025-2026 LG Electronics Inc. All rights reserved.

