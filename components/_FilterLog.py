# This Python file uses the following encoding: utf-8
from PySide6.QtCore import QObject, Signal, Property, Slot
from PySide6.QtGui import QColor
import json
import copy
import re
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

    def _normalizeColor(self, color):
        if isinstance(color, QColor):
            return color.name(QColor.NameFormat.HexArgb)

        colorStr = str(color).strip() if color is not None else ""
        if not colorStr:
            return ""

        parsed = QColor(colorStr)
        if parsed.isValid():
            return parsed.name(QColor.NameFormat.HexArgb)

        # Backward compatibility: convert Python QColor repr strings written by old code.
        rgbf_match = re.match(
            r"^PySide6\.QtGui\.QColor\.fromRgbF\(([^,]+),\s*([^,]+),\s*([^,]+),\s*([^\)]+)\)$",
            colorStr,
        )
        if rgbf_match:
            try:
                r = max(0.0, min(1.0, float(rgbf_match.group(1))))
                g = max(0.0, min(1.0, float(rgbf_match.group(2))))
                b = max(0.0, min(1.0, float(rgbf_match.group(3))))
                a = max(0.0, min(1.0, float(rgbf_match.group(4))))
                return QColor.fromRgbF(r, g, b, a).name(QColor.NameFormat.HexArgb)
            except ValueError:
                pass

        # Keep original text if Qt cannot parse it.
        return colorStr
        
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

    @Slot(int, str)
    @Slot(int, QColor)
    def updateColorFilter(self, id, color):
        print("updateColorFilter id: ", id, " color: ", color)
        self._loadedFilters[id]["color"] = self._normalizeColor(color)
        self.saveFilterToJson()
        self.refreshFilterProps()

    @Slot(int,bool)
    def enableFilter(self, id, enabled):
        print("enableFilter id: ", id, " enabled: ", enabled)
        self._loadedFilters[id]["enabled"] = enabled
        self.saveFilterToJson()
        self.refreshFilterProps()


    @Slot(str, str, str)
    @Slot(str, str, QColor)
    def addFilter(self, tag, tid, color):
        id = self._maxID + 1
        colorStr = self._normalizeColor(color)
        filterConfig = { "id": id, "name": tag, "tag": tag, "pid": "", "tid": tid, "enabled": True, "color": colorStr }
        self._loadedFilters[id] = filterConfig
        self.saveFilterToJson()
        self.refreshFilterProps()
        self._maxID = id

    @Slot(int, str, str, bool, str)
    @Slot(int, str, str, bool, QColor)
    def updateFilter(self, id, tag, tid, enabled, color):
        colorStr = self._normalizeColor(color)
        filterConfig = { "id": id, "name": tag, "tag": tag, "pid": "", "tid": tid, "enabled": enabled, "color": colorStr }
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
                        "color":   self._normalizeColor(data.get("color", ""))
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
            if tag and filterItem.get("enabled"):
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
