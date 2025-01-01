# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Signal, QRegularExpression, Property, Slot
import json
import copy
class FilterLog(QObject):
    displayedFilterChanged  = Signal()
    filteredRegexChanged    = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)
        self._filterPath                = ""
        self._loadedFilters             = {}
        self._filteredRegex             = QRegularExpression(R"", QRegularExpression.CaseInsensitiveOption | QRegularExpression.DotMatchesEverythingOption)
        self._colors                    = {}
        self._filteredStringList        = []
        self._maxID                     = 0
        pass

    def create(self, jsonPath):
        self.loadFilterFromJson(jsonPath)
        self.refreshRegex()
        
    @Property(list, notify=displayedFilterChanged)
    def displayedFilters(self):
        return list(self._loadedFilters.values())
    
    @displayedFilters.setter
    def displayedFilters(self, val):
        self.displayedFilterChanged.emit()

    @Property(QRegularExpression, notify=filteredRegexChanged)
    def filteredRegex(self):
        return self._filteredRegex

    @filteredRegex.setter
    def filteredRegex(self, pattern):
        if (self._filteredRegex.pattern() == pattern):
            return
        self._filteredRegex.setPattern(pattern)
        self.filteredRegexChanged.emit()

    @Slot(int,str)
    def updateColorFilter(self, id, color):
        print("updateColorFilter id: ", id, " color: ", color)
        self._loadedFilters[id]["color"] = color
        self.saveFilterToJson()
        self.refreshFilterProps()

    @Slot(int,bool)
    def enableFilter(self, id, enabled):
        print("enableFilter id: ", id, " enabled: ", enabled)
        self._loadedFilters[id]["enabled"] = enabled
        self.saveFilterToJson()
        self.refreshFilterProps()


    @Slot(str, str, str)
    def addFilter(self, name, processName, color):
        id = self._maxID + 1
        filterConfig = { "id": id, "name": name, "processName": processName, "enabled": True, "color": color }
        self._loadedFilters[id] = filterConfig
        self.saveFilterToJson()
        self.refreshFilterProps()
        self.refreshRegex()
        self._maxID = id

    @Slot(int, str, str, bool, str)
    def updateFilter(self, id, name, processName, enabled, color):
        filterConfig = { "id": id, "name": name, "processName": processName, "enabled": enabled, "color": color }
        self._loadedFilters[id] = filterConfig
        self.saveFilterToJson()
        self.refreshFilterProps()

    @Slot(int)
    def removeFilter(self, id):
        del self._loadedFilters[id]
        self.saveFilterToJson()
        self.refreshFilterProps()
        self.refreshRegex()

    def saveFilterToJson(self):
        filters = []
        for key in self._loadedFilters:
            filters.append(self._loadedFilters[key])
        with open(self._filterPath, 'w', encoding='utf-8') as file:
            json.dump(filters, file, indent=4)

    def loadFilterFromJson(self, jsonPath):
        self._loadedFilters = {}
        self._filterPath    = jsonPath
        datas               = None
        try:
            with open(jsonPath, 'r', encoding='utf-8') as file:
                datas = json.load(file)
            for data in datas:
                    filterConfig = { "id": data["id"], "name": data["name"], "processName": data["processName"], "enabled": data["enabled"] , "color": data["color"] }
                    self._loadedFilters[data["id"]] = filterConfig
                    self._maxID = max(self._maxID, data["id"])
        
        except Exception as e:
            print(f"Error loading filter from JSON: {e}")
        
        self.refreshFilterProps()

    def refreshFilterProps(self):
        self.displayedFilterChanged.emit()
        self._filteredStringList = []
        for filterItem in self._loadedFilters.values():
            if (filterItem.get("enabled")):
                processName = filterItem.get("processName")
                color       = filterItem.get("color")

                self._filteredStringList.append(processName)
                self._colors[processName] = color

    def refreshRegex(self):
        if (len(self._filteredStringList) == 0):
            self.filteredRegex = ""
            return
        self.filteredRegex = "|".join(self._filteredStringList)

    def loadedFilters(self):
        return self._loadedFilters
    
    def originalFilters(self):
        return  copy.deepcopy(list(self._loadedFilters.values()))

    def colors(self):
        return self._colors
