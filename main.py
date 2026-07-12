
# This Python file uses the following encoding: utf-8
import sys
from pathlib import Path

from PySide6.QtGui import QGuiApplication, QClipboard
from PySide6.QtGui import QIcon
from PySide6.QtQml import QQmlApplicationEngine, qmlRegisterType
from PySide6.QtWidgets import QApplication
from PySide6.QtQuickControls2 import QQuickStyle

from components import Defines
from components._LogViewModel import LogModel
from components._SortFilterProxyModel import SortFilterProxyModel
from components._FilterLog import FilterLog
from components._Controller import Controller

if __name__ == "__main__":
    if sys.platform.startswith("win"):
        # Give the process a stable AppUserModelID so taskbar uses this app icon.
        import ctypes

        ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("com.catchy.logviewer")

    app = QApplication(sys.argv)
    icon_path = Path(__file__).resolve().parent / "assets" / "images" / "android_catchy_icon.svg"
    app.setWindowIcon(QIcon(str(icon_path)))
    QQuickStyle.setStyle("Fusion")
    engine = QQmlApplicationEngine()
    engine.addImportPath(Path(__file__).parent)
    engine.addImportPath("qrc:/styles")
    qml_file = Path(__file__).resolve().parent / "qmls/main.qml"

    qmlRegisterType(SortFilterProxyModel, "com.mycompany.qmlcomponents", 1, 0, "SortFilterProxyModel")

    controller = Controller()
    controller.clipboard = app.clipboard()
    engine.rootContext().setContextProperty("controller", controller)

    engine.rootContext().setContextProperty("filterLog", controller.getFilterLog())

    engine.rootContext().setContextProperty("logModel", controller.getLogViewModel())

    engine.rootContext().setContextProperty("searchLog", controller.getSearchLog())
    
    engine.rootContext().setContextProperty("remoteDeviceManager", controller.getRemoteDeviceManager())
    
    engine.rootContext().setContextProperty("toastMgr", controller.getToast())
    
    engine.rootContext().setContextProperty("helper", controller.getHelper())
    
    engine.rootContext().setContextProperty("bookmark", controller.getBookmark())

    engine.load(qml_file)
    
    if not engine.rootObjects():
        sys.exit(-1)
    sys.exit(app.exec())
