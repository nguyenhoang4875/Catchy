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

## Optional tools for remote streaming feature

Some remote log streaming flows call shell scripts in the scripts folder and expect:

- Git Bash on Windows (bash.exe)
- OpenSSH client (ssh command)

Without these tools, local log viewing still works, but remote streaming/connect features may fail.
