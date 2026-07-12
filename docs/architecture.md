# Architecture: QtLogViewer

## Overview

QtLogViewer is a desktop log viewer application built with **Python + PySide6 (Qt for Python)** for the backend and **QML** for the UI. It follows an **MVVM-like pattern** where Python classes act as ViewModels/Services and QML handles the View layer, communicating via Qt's property/signal system.

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                        QML Layer (View)                  │
│  main.qml · LogViewTable · LeftToolPanel · Panels...    │
└────────────────────────┬────────────────────────────────┘
                         │  Bindings / Signals / Slots
                         │  (QML Context Properties)
┌────────────────────────▼────────────────────────────────┐
│                    Controller (Facade)                   │
│              components/_Controller.py                   │
└──┬──────────┬──────────┬──────────┬──────────┬──────────┘
   │          │          │          │          │
   ▼          ▼          ▼          ▼          ▼
LogModel  FilterLog  SearchLog  Remote     Bookmark /
+ Proxy             + Configs  DeviceManager  Toast / Helper
```

---

## Entry Point

**`main.py`**

- Creates the `QApplication` and `QQmlApplicationEngine`.
- Instantiates `Controller` and all sub-components.
- Exposes Python objects to QML as **context properties**:

| Context Property      | Python Object              |
|-----------------------|----------------------------|
| `controller`          | `Controller`               |
| `logModel`            | `LogModel` (via Controller)|
| `filterLog`           | `FilterLog`                |
| `searchLog`           | `SearchLog`                |
| `remoteDeviceManager` | `RemoteDeviceManager`      |
| `toastMgr`            | `Toast`                    |
| `helper`              | `Helper`                   |
| `bookmark`            | `Bookmark`                 |

- Registers `SortFilterProxyModel` as a QML type (`com.mycompany.qmlcomponents 1.0`).
- Loads `qmls/main.qml` as the root UI.

---

## Backend Components (`components/`)

### `_Controller.py` — Central Coordinator

The `Controller` is the main facade between QML and all backend services. It:

- Owns and wires together all sub-components.
- Manages `QThread` workers for async file loading and SSH streaming.
- Exposes Qt `Property` and `Slot` methods called directly from QML.
- Handles app lifecycle (config loading on start, cleanup on exit via `atexit`).

**Key signals:** `showLoadingScreenChanged`, `logViewReadyChanged`, `loadLogFileCompleted`, `detailsTextChanged`, `highlightLineNumChanged`, `showNotification`, `themeChanged`, `showLessColumnsChanged`.

---

### `_LogViewModel.py` — Data Model

`LogModel` extends `QAbstractTableModel` and is the data source for the log table.

- **Columns:** Date Time, Time Stamp, Level, Process Name, Message.
- Parses log lines using a compiled regex:
  ```
  YYYY-MM-DDTHH:MM:SS.sssZ [timestamp] user.LEVEL ProcessName [] Message
  ```
- Stores parsed entries as a list of dicts; also maintains a `line_number`-keyed dict for fast lookup.
- Assigns per-row colors based on process name (from `FilterLog`).

---

### `_SortFilterProxyModel.py` — Proxy Model

`SortFilterProxyModel` extends `QSortFilterProxyModel`.

- Sits between `LogModel` and the QML `TableView`.
- Provides `rowLineNum(line)` slot for scrolling to a specific log line by its original line number.
- Filtering logic (regex from `FilterLog`) is applied here via Qt's built-in proxy mechanism.

---

### `_FilterLog.py` — Filter Management

`FilterLog` manages named process-name filters, each with a color and enabled state.

- Filters are persisted to a JSON file (path from `Configurations`).
- Builds a combined `QRegularExpression` from all enabled filters.
- Exposes `displayedFilters` (list), `filteredRegex`, and CRUD slots (`addFilter`, `updateFilter`, `removeFilter`, `enableFilter`, `updateColorFilter`).

---

### `_SearchLog.py` — Search & Highlight

`SearchLog` manages keyword search state.

- Holds a `QRegularExpression` built from `|`-separated search terms.
- Maintains `searchWords` list for per-keyword highlight color assignment.
- Provides a 10-color palette (`rgba`) cycled by keyword index.
- `showSearchResults` flag toggles a separate search-results table view in QML.

---

### `_RemoteDeviceManager.py` — SSH Device Manager

Manages remote device connections for live log streaming.

- Holds a list of configured devices, the currently connected device, and connection status.
- State machine: `IDLE → INPROGRESS → SUCCESS / FAILED`.
- `streaming` flag controls whether a live log stream is active.
- SSH connectivity is handled via `asyncssh` (async) driven from `Controller` in a dedicated `QThread`.

---

### `_Worker.py` — Thread Worker

Generic async task runner used with `QThread`.

- Accepts any callable + args/kwargs.
- Emits `taskCompleted(result)` when done.
- Used by `Controller` to offload file I/O and SSH operations off the main thread.

---

### `_Configurations.py` — Persistence

Loads and saves application configuration from `C:/QtLogViewer/savedConfig.json`.

- On startup, restores: last filter path, remote device list, theme, column visibility.
- `saveConfig(key, value)` persists individual keys to disk.

---

### `_Bookmark.py` — Bookmarks

Tracks bookmarked log lines (by line number).

- `displayList`: list of bookmark dicts `{line, text, ...}`.
- `highlightLines`: flat list of line numbers for QML highlight rendering.
- CRUD: `addBookmark(dict)`, `removeBookmark(line)`, `isBookmarked(line)`.

---

### `_Toast.py` — Notifications

Singleton `Toast` (enforced by a `@singleton` decorator + `@QmlElement`).

- `show(type, message)` emits `showMsg(int, str)` to QML.
- Types: `INFO (0)`, `WARNING (1)`, `ERROR (2)`.

---

### `_Helper.py` — UI Helpers

`Helper` exposes simple UI state properties to QML.

- `autoScrollDown` (bool): controls whether the log table auto-scrolls to the latest entry during streaming.

---

### `_Defines.py` — Path Utility

`Defines` provides a `path(arg)` slot that resolves asset paths relative to the project root — used during development to reference local files.

---

## Frontend Components (`qmls/`)

| File                        | Role                                                       |
|-----------------------------|------------------------------------------------------------|
| `main.qml`                  | Root `ApplicationWindow`; menu bar, layout composition     |
| `LogViewTable.qml`          | Main log `TableView` with filter proxy and row highlights  |
| `LeftToolPanel.qml`         | Side panel: filter list, remote device controls            |
| `FilterDetailPanel.qml`     | Add/edit filter form                                       |
| `BookmarkPanel.qml`         | Bookmarks list panel                                       |
| `RemoteDeviceDetailPanel.qml`| SSH device config and connection UI                       |
| `LoadingScreen.qml`         | Full-screen loading overlay                                |
| `Toast.qml`                 | Toast notification overlay                                 |
| `Notification.qml`          | In-app notification banner                                 |
| `CustomCheckBox.qml`        | Reusable styled checkbox                                   |
| `HighlightAnimation.qml`    | Row flash animation on scroll-to                           |

---

## Styling (`styles/`)

`Styler.qml` is a **pragma Singleton** QML object providing global theme state.

- `themeMode`: `DARK | LIGHT` — derived from `controller.theme`.
- `showLessColumns`: mirrors `controller.showLessColumns`.
- All QML components import `Styles` and read from `Styler` for colors.

---

## Data Flow: Loading a Log File

```
QML (user picks file)
  → controller.loadLogFile(path)          [Slot]
      → Worker(logviewModel.loadLogFile)  [QThread]
          → LogModel parses file
          → taskCompleted signal
      → controller sets logViewReady = true
          → SortFilterProxyModel.setSourceModel(logModel)
          → QML TableView refreshes
```

## Data Flow: Live Log Streaming (Remote Device)

```
QML → controller.requestConnectToDevice(device)
  → asyncssh connects in _connRDeviceThread
  → on success: remoteDeviceManager.connectedDevice = device
  → controller.requestStartStream()
      → SSH exec stream script in _streamLogFileThread
      → Worker reads chunks → appends to LogModel
      → helper.autoScrollDown = true → QML scrolls to bottom
```

## Data Flow: Filtering

```
FilterLog.filteredRegex changes (Signal)
  → SortFilterProxyModel receives new regex
  → filterAcceptsRow() re-evaluated
  → QML TableView updates automatically
```

---

## Configuration Files

| File                              | Purpose                                    |
|-----------------------------------|--------------------------------------------|
| `C:/QtLogViewer/savedConfig.json` | Runtime config: theme, devices, filter path|
| `configurations/filters.json`     | Default/bundled filter definitions         |
| `configurations/homeFilter.json`  | Home filter preset                         |
| `configurations/savedConfig.json` | Dev-time config fallback                   |

---

## Build

The application is packaged with **PyInstaller** using `main.spec`. The `build/main/` directory contains analysis artifacts. The distributable executable bundles Python, PySide6, and all assets.

---

## Technology Stack

| Layer      | Technology                          |
|------------|-------------------------------------|
| Language   | Python 3.x                          |
| UI Toolkit | PySide6 (Qt 6) + QML                |
| Qt Style   | Fusion / Universal (theme-aware)    |
| SSH        | asyncssh                            |
| Clipboard  | pyperclip + Qt QClipboard           |
| Packaging  | PyInstaller                         |
| Fonts      | Mukta Vaani, Concert One, Moirai One|
