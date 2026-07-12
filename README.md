# Catchy (Qt Log Viewer)

Catchy is a desktop log viewer built with Python and Qt/QML using PySide6.

## Required Python modules

From code import analysis, this project needs these third-party Python packages:

- PySide6
- pyperclip
- asyncssh

Standard library modules (json, os, pathlib, asyncio, subprocess, etc.) are already included with Python.

## Python version

- Python 3.10 or newer is recommended.

## Install dependencies

Run these commands in the project folder:

```bash
python -m pip install --upgrade pip
python -m pip install PySide6 pyperclip asyncssh
```

## Run the app

```bash
python -u main.py
```

## Fix for your current error

If you see:

```text
ModuleNotFoundError: No module named 'PySide6'
```

install PySide6 in the same Python environment used to run main.py:

```bash
python -m pip install PySide6
```

## Deploy

deploy/build notes:
- Build command: `python -m PyInstaller --noconfirm --clean Catchy.spec`
- Output executable: `dist/Catchy/Catchy.exe`
- Required runtime folders are bundled into build: `qmls`, `styles`, `assets`, `scripts`

### Troubleshooting build errors on Windows

If you see WinError 5 or WinError 32 while PyInstaller is removing `dist/Catchy`, a process is locking files in the output folder.

1. Close the app if running (`Catchy.exe`).
2. Close any File Explorer window opened at `dist/Catchy`.
3. Stop Python/PyInstaller processes and remove old outputs:
	- `Get-Process Catchy,python,pyinstaller -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue`
	- `Remove-Item dist/Catchy -Recurse -Force -ErrorAction SilentlyContinue`
	- `Remove-Item build/Catchy -Recurse -Force -ErrorAction SilentlyContinue`
4. Build again.

If `python` is not recognized in your shell, run PyInstaller with the full interpreter path, for example:

- `& "D:/Program_code/Python314/python.exe" -m PyInstaller --noconfirm --clean --distpath "d:/Expert_Task/Catchy/dist" --workpath "d:/Expert_Task/Catchy/build" "d:/Expert_Task/Catchy/Catchy.spec"`

## Optional tools for remote streaming feature

Some remote log streaming flows call shell scripts in the scripts folder and expect:

- Git Bash on Windows (bash.exe)
- OpenSSH client (ssh command)

Without these tools, local log viewing still works, but remote streaming/connect features may fail.
