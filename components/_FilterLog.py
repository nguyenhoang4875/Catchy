# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Signal, Property, Slot
import json
import copy
class FilterLog(QObject):
    displayedFilterChanged  = Signal()
    filterCriteriaChanged   = Signal()
    def __init__(self, parent=None):
        super().__init__(parent)
        self._filterPath                = ""
        self._loadedFilters             = {}
        self._colors                    = {}
        self._filterCriteria            = []
        self._maxID                     = 0
        pass

    def create(self, jsonPath):
        self.loadFilterFromJson(jsonPath)
        
    @Property(list, notify=displayedFilterChanged)
    def displayedFilters(self):
        return list(self._loadedFilters.values())
    
    @displayedFilters.setter
    def displayedFilters(self, val):
        self.displayedFilterChanged.emit()

    @Property(list, notify=filterCriteriaChanged)
    def filterCriteria(self):
        return self._filterCriteria

    @filterCriteria.setter
    def filterCriteria(self, val):
        self._filterCriteria = val
        self.filterCriteriaChanged.emit()

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


    @Slot(str, str, str, str, str)
    def addFilter(self, name, tag, pid, tid, color):
        id = self._maxID + 1
        filterConfig = { "id": id, "name": name, "tag": tag, "pid": pid, "tid": tid, "enabled": True, "color": color }
        self._loadedFilters[id] = filterConfig
        self.saveFilterToJson()
        self.refreshFilterProps()
        self._maxID = id

    @Slot(int, str, str, str, str, bool, str)
    def updateFilter(self, id, name, tag, pid, tid, enabled, color):
        filterConfig = { "id": id, "name": name, "tag": tag, "pid": pid, "tid": tid, "enabled": enabled, "color": color }
        self._loadedFilters[id] = filterConfig
        self.saveFilterToJson()
        self.refreshFilterProps()

    @Slot(int)
    def removeFilter(self, id):
        del self._loadedFilters[id]
        self.saveFilterToJson()
        self.refreshFilterProps()

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
                    filterConfig = {
                        "id":      data["id"],
                        "name":    data["name"],
                        "tag":     data.get("tag", data.get("processName", "")),
                        "pid":     data.get("pid", ""),
                        "tid":     data.get("tid", ""),
                        "enabled": data["enabled"],
                        "color":   data["color"]
                    }
                    self._loadedFilters[data["id"]] = filterConfig
                    self._maxID = max(self._maxID, data["id"])
        
        except Exception as e:
            print(f"Error loading filter from JSON: {e}")
        
        self.refreshFilterProps()

    def refreshFilterProps(self):
        self.displayedFilterChanged.emit()
        self._colors = {}
        new_criteria = []
        for filterItem in self._loadedFilters.values():
            tag   = filterItem.get("tag", "")
            color = filterItem.get("color", "")
            if tag:
                self._colors[tag] = color
            if filterItem.get("enabled"):
                new_criteria.append({
                    "tag":   tag,
                    "pid":   filterItem.get("pid", ""),
                    "tid":   filterItem.get("tid", ""),
                    "color": color,
                })
        self.filterCriteria = new_criteria

    def loadedFilters(self):
        return self._loadedFilters
    
    def originalFilters(self):
        return  copy.deepcopy(list(self._loadedFilters.values()))

    def colors(self):
        return self._colors
