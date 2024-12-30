
# This Python file uses the following encoding: utf-8
import sys
from pathlib import Path

from PySide6.QtGui import QGuiApplication, QClipboard
from PySide6.QtQml import QQmlApplicationEngine, qmlRegisterType
from PySide6.QtWidgets import QApplication

from components import Defines
from components._LogViewModel import LogModel
from components._SortFilterProxyModel import SortFilterProxyModel
from components._FilterLog import FilterLog
from components._Controller import Controller

if __name__ == "__main__":
    app = QApplication(sys.argv)
    engine = QQmlApplicationEngine()
    engine.addImportPath(Path(__file__).parent)
    qml_file = Path(__file__).resolve().parent / "qmls/main.qml"

    qmlRegisterType(SortFilterProxyModel, "com.mycompany.qmlcomponents", 1, 0, "SortFilterProxyModel")

    controller = Controller()
    controller.clipboard = app.clipboard()
    engine.rootContext().setContextProperty("controller", controller)

    engine.rootContext().setContextProperty("filterLog", controller.getFilterLog())

    engine.rootContext().setContextProperty("logModel", controller.getLogViewModel())

    engine.rootContext().setContextProperty("searchLog", controller.getSearchLog())

    engine.load(qml_file)
    
    if not engine.rootObjects():
        sys.exit(-1)
    sys.exit(app.exec())
