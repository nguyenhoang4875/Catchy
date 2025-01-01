from PySide6.QtCore import QObject, Slot
import json
from pathlib import Path
class Configurations(QObject):
    def __init__(self, parent=None):
        super().__init__(parent)
        self._configs = {}
        pass
    pass

    def loadLastSavedConfig(self):
        try:
            with open(Path(__file__).resolve().parent /'../configurations/savedConfig.json', 'r', encoding='utf-8') as file:
                self._configs = json.load(file)
        except Exception as e:
            print("Error loading saved configuration: ", e)
        pass

    def getConfigs(self):
        return self._configs
    
    def saveConfig(self, key, value):
        self._configs[key] = value
        with open(Path(__file__).resolve().parent /'../configurations/savedConfig.json', 'w', encoding='utf-8') as file:
            json.dump(self._configs, file, indent=4)
        pass
