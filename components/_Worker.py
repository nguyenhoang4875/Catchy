# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Signal, Slot
class Worker(QObject):
    taskCompleted = Signal(object)

    def __init__(self, task, *args, **kwargs):
        super().__init__()
        self.task = task
        self.args = args
        self.kwargs = kwargs

    @Slot()
    def run(self):
        print("run task")
        result = self.task(*self.args, **self.kwargs)
        self.taskCompleted.emit(result)
