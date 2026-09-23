import QtQuick
import QtQuick.Controls 2.15
import Qt.labs.qmlmodels 1.0
import QtQuick.Controls.Universal 2.12
import QtQuick.Layouts 1.13
import QtCore 6.6
import com.mycompany.qmlcomponents 1.0
import Styles
Item {
    id: root
    enum TableType {
        ViewTable,
        SearchResultsTable
    }
    property var tableType
    property color logTextColor: ({
        [Styler.ThemeMode.DARK]: "#ffffff",
        [Styler.ThemeMode.LIGHT]: "#111111"
    })[Styler.themeMode]
    property color logRowColor: ({
        [Styler.ThemeMode.DARK]: "#2f2f2f",
        [Styler.ThemeMode.LIGHT]: "#ffffff"
    })[Styler.themeMode]
    property color logBookmarkRowColor: ({
        [Styler.ThemeMode.DARK]: "#5a3a3a",
        [Styler.ThemeMode.LIGHT]: "#ffe5e5"
    })[Styler.themeMode]
    property color logSelectionColor: ({
        [Styler.ThemeMode.DARK]: "#5b84d6",
        [Styler.ThemeMode.LIGHT]: "#7ea7ff"
    })[Styler.themeMode]
    property alias filterProxyModel: filterProxyModel
    property bool showTable: true
    property bool applyFilterColors: true
    property bool highlight: false
    property var highlightProvider: function(text) {
        return controller.hightlightSearchResults(text)
    }
    property alias logview: logView
    property int lastColSelected: -1
    property int firstColSelected: logHeaderModel.count
    property int highlightLineNum: -1
    property var selectedRows: ({})
    property real maxMessageTextWidth: 0
    property real pendingMaxMessageTextWidth: 0
    property real headerContentWidth: {
        var fixedTotal = 0
        for (var i = 0; i < logHeaderModel.count; i++) {
            var entry = logHeaderModel.get(i)
            if (entry.size > 0 && entry.resizeable) {
                var item = header.itemAt(i)
                fixedTotal += item ? item.width : entry.size * root.width
            }
        }
        var msgWidth = Math.max(root.width - fixedTotal, maxMessageTextWidth + 16)
        return fixedTotal + msgWidth
    }

    function scrollToLine(lineNum) {
        scrollTimer.lineToScroll = lineNum
        scrollTimer.restart()
    }

    // Applies alpha to a filter color so row text stays legible over the tinted background.
    function filterBgColor(colorStr, alpha) {
        var c = Qt.color(colorStr)
        return Qt.rgba(c.r, c.g, c.b, alpha)
    }

    // Selects every column of a single row, replacing the current selection.
    function selectRow(row) {
        logView.selectionModel.select(filterProxyModel.index(row, 0), ItemSelectionModel.ClearAndSelect | ItemSelectionModel.Rows)
    }

    // Selects every column of all rows between startRow and endRow (inclusive), replacing the current selection.
    function selectRowRange(startRow, endRow) {
        var lo = Math.min(startRow, endRow)
        var hi = Math.max(startRow, endRow)
        logView.selectionModel.clearSelection()
        for (var r = lo; r <= hi; r++) {
            logView.selectionModel.select(filterProxyModel.index(r, 0), ItemSelectionModel.Select | ItemSelectionModel.Rows)
        }
    }

    function highlightLine(lineNum) {
        root.highlightLineNum = lineNum
        highlightTimer.lineToHighlight = lineNum
        highlightTimer.restart()
    }

    function scrollAndHighlight(lineNum) {
        root.highlightLineNum = lineNum
        scrollToLine(lineNum)
        highlightTimer.lineToHighlight = lineNum
        highlightTimer.restart()
    }

    function applyColumnLayout() {
        if (controller.showLessColumns) {
            // Compact mode: keep Line, Tag visible and let Message take the rest.
            logHeaderModel.setProperty(0, "size", 0.04)
            logHeaderModel.setProperty(0, "resizeable", true)
            logHeaderModel.setProperty(1, "size", 0)
            logHeaderModel.setProperty(1, "resizeable", false)
            logHeaderModel.setProperty(2, "size", 0)
            logHeaderModel.setProperty(2, "resizeable", false)
            logHeaderModel.setProperty(3, "size", 0)
            logHeaderModel.setProperty(3, "resizeable", false)
            logHeaderModel.setProperty(4, "size", 0)
            logHeaderModel.setProperty(4, "resizeable", false)
            logHeaderModel.setProperty(5, "size", 0.20)
            logHeaderModel.setProperty(5, "resizeable", true)
            logHeaderModel.setProperty(6, "size", 0)
            logHeaderModel.setProperty(6, "resizeable", true)
            return
        }

        // Full mode: Line, Date Time, PID, TID, Level, Tag, Message (remaining).
        logHeaderModel.setProperty(0, "size", 0.04)
        logHeaderModel.setProperty(0, "resizeable", true)
        logHeaderModel.setProperty(1, "size", 0.12)
        logHeaderModel.setProperty(1, "resizeable", true)
        logHeaderModel.setProperty(2, "size", 0.04)
        logHeaderModel.setProperty(2, "resizeable", true)
        logHeaderModel.setProperty(3, "size", 0.04)
        logHeaderModel.setProperty(3, "resizeable", true)
        logHeaderModel.setProperty(4, "size", 0.04)
        logHeaderModel.setProperty(4, "resizeable", true)
        logHeaderModel.setProperty(5, "size", 0.20)
        logHeaderModel.setProperty(5, "resizeable", true)
        logHeaderModel.setProperty(6, "size", 0)
        logHeaderModel.setProperty(6, "resizeable", true)
    }
    Connections {
        target: controller
        function onHighlightLineNumChanged() {
            if (root.tableType === LogViewTable.TableType.ViewTable) {
                scrollAndHighlight(controller.highlightLineNum)
            }
        }
    }

    Connections {
        target: helper

        function onAutoScrollDownChanged() {
            if (root.tableType === LogViewTable.TableType.ViewTable) {
                if (helper.autoScrollDown) {
                    logview.positionViewAtRow(logView.rows - 1, TableView.AlignBottom)
                }
            }
        }
    }

    Menu {
        id: optionMenu
        width: 120
        height: 60
        property int lineSelected: -1
        property string log: ""
        property bool isMarked: false
        
        padding: 0
        margins: 0
        topInset: 0
        bottomInset: 0
        leftInset: 0
        rightInset: 0
        
        function openMenu(mark, line, x, y) {
            optionMenu.lineSelected = line
            var logMessage = controller.getLogMessage(line)
            optionMenu.log = logMessage
            optionMenu.open()
            optionMenu.x = x - 0
            optionMenu.y = y + 30
            optionMenu.isMarked = mark
            if (mark) {
                makeBookmarkBtn.contentItem.text = "Remove"
            } else {
                makeBookmarkBtn.contentItem.text = "Bookmark"
            }
        }

        background: Rectangle {
            color: ({
                [Styler.ThemeMode.DARK]: "#303030",
                [Styler.ThemeMode.LIGHT]: "#8078DE"
            })[Styler.themeMode]
            radius: 2
            border.width: 0.5
            border.color: Qt.rgba(0.5, 0.5, 0.5, 0.5)
        }

        Button {
            id: makeBookmarkBtn
            width: parent.width
            height: 30
            anchors.top: parent.top
            font.family: concertOne.font.family
            hoverEnabled: true
            contentItem: Text {
                text: "Bookmark"
                color: ({
                    [Styler.ThemeMode.DARK]: makeBookmarkBtn.hovered ? "#acf39999" : "#ffffff",
                    [Styler.ThemeMode.LIGHT]: makeBookmarkBtn.hovered ? "#FFC513" : "#FDB147"
                })[Styler.themeMode]
                font.pixelSize: 12
                verticalAlignment: Text.AlignVCenter
                horizontalAlignment: Text.AlignHCenter
                font.bold: true
            }
            flat: true

            background: Rectangle {
                color: makeBookmarkBtn.down ? "#adc8c7ca" : "transparent"
                radius: 2
            }
            onClicked: {
                if (optionMenu.isMarked) {
                    bookmark.removeBookmark(optionMenu.lineSelected)
                } else {
                 bookmark.addBookmark({line: optionMenu.lineSelected, log: optionMenu.log})
                }
                optionMenu.close()
            }
        }

        Button {
            id: copyLineBtn
            width: parent.width
            height: 30
            anchors.top: makeBookmarkBtn.bottom
            font.family: concertOne.font.family
            hoverEnabled: true
            contentItem: Text {
                text: "Copy"
                color: ({
                    [Styler.ThemeMode.DARK]: copyLineBtn.hovered ? "#acf39999" : "#ffffff",
                    [Styler.ThemeMode.LIGHT]: copyLineBtn.hovered ? "#FFC513" : "#FDB147"
                })[Styler.themeMode]
                font.pixelSize: 12
                verticalAlignment: Text.AlignVCenter
                horizontalAlignment: Text.AlignHCenter
                font.bold: true
            }
            flat: true

            background: Rectangle {
                color: copyLineBtn.down ? "#adc8c7ca" : "transparent"
                radius: 2
            }
            onClicked: {
                logView.copySelectedCells()
                optionMenu.close()
            }
        }
    }

    Timer {
        id: scrollTimer
        interval: 300
        property int lineToScroll: -1
        onTriggered: {
            let rowIdx = filterProxyModel.rowLineNum(lineToScroll)
            if (root.tableType === LogViewTable.TableType.ViewTable && rowIdx >= 0) {
                logview.positionViewAtRow(rowIdx, TableView.AlignVCenter)
            }
        }
    }

    Timer {
        id: highlightTimer
        interval: 300
        property int lineToHighlight: -1
        onTriggered: {
            let rowIdx = filterProxyModel.rowLineNum(lineToHighlight)
            for (var i = 0; i < logHeaderModel.count; i++) {
                let item = logview.itemAtIndex(logview.index(rowIdx, i))
                if (item) {
                    item.highlight()
                }
            }
        }
    }

    ListModel {
        id: logHeaderModel
        ListElement { title: "Line";            size: 0.04 ;    resizeable: true }
        ListElement { title: "Date Time";       size: 0.12 ;    resizeable: true }
        ListElement { title: "PID";             size: 0.04 ;    resizeable: true }
        ListElement { title: "TID";             size: 0.04 ;    resizeable: true }
        ListElement { title: "Level";       size: 0.04 ;    resizeable: true }
        ListElement { title: "Tag";             size: 0.20 ;    resizeable: true }
        ListElement { title: "Message";         size: 0 ;       resizeable: true }
    }

    Connections {
        target: controller

        function onShowLessColumnsChanged() {
            root.applyColumnLayout()
        }
    }

    Component.onCompleted: root.applyColumnLayout()

    Item {
        id: headerContainer
        anchors.top: root.top
        anchors.left: root.left
        anchors.right: root.right
        height: 30
        clip: true

        SplitView {
            id: header
            x: -logView.contentX
            width: root.headerContentWidth
            height: parent.height
            orientation: Qt.Horizontal
            spacing: 0

        handle: Rectangle {
            implicitWidth: 2
            color: ({
                    [Styler.ThemeMode.DARK]: "#5F606A",
                    [Styler.ThemeMode.LIGHT]: "#d1d1e9"
            })[Styler.themeMode]
        }

        Repeater {
            model: logHeaderModel
            delegate: Rectangle {
                SplitView.preferredWidth: model.size !== 0 ? model.size * root.width : undefined
                SplitView.fillWidth: (model.size === 0)
                SplitView.fillHeight: true
                visible: model.resizeable
                color: ({
                    [Styler.ThemeMode.DARK]: "#222222",
                    [Styler.ThemeMode.LIGHT]: "#5684AE"
                })[Styler.themeMode]
                Text {
                    anchors.centerIn: parent
                    text: model.title
                    visible: model.resizeable
                    color: ({
                        [Styler.ThemeMode.DARK]: "#ffffff",
                        [Styler.ThemeMode.LIGHT]: "#fffffe"
                    })[Styler.themeMode]
                    font.family: concertOne.font.family
                    font.pixelSize: 14
                }
                onWidthChanged: {
                    logView.forceLayout()
                }
            }
        }
        }
    }

    SortFilterProxyModel {
        id: filterProxyModel
    }

    Connections {
        target: filterProxyModel
        function onModelReset() {
            root.maxMessageTextWidth = 0
            root.pendingMaxMessageTextWidth = 0
        }
    }

    // Coalesces per-row message-width growth so the header/TableView only
    // relayout once scrolling has a lull, instead of on nearly every frame.
    Timer {
        id: maxWidthCommitTimer
        interval: 200
        repeat: false
        onTriggered: {
            if (root.pendingMaxMessageTextWidth > root.maxMessageTextWidth) {
                root.maxMessageTextWidth = root.pendingMaxMessageTextWidth
            }
        }
    }

    TableView {
        id: logView
        visible: root.showTable
        anchors.top: headerContainer.bottom
        anchors.left: root.left
        anchors.right: root.right
        height: root.height - headerContainer.height
        width: root.width

        columnSpacing: 0.5 
        rowSpacing: 0
        boundsBehavior: Flickable.StopAtBounds
        clip: true
        focus: true

        onActiveFocusChanged: {
            if (!activeFocus) {
                interactive = true
            }
        }

        Rectangle {
            anchors.fill: parent
            color: root.logRowColor
            z: -2
        }

        onRowsChanged: {
            if (root.tableType === LogViewTable.TableType.ViewTable) {
                if (helper.autoScrollDown) {
                    logview.positionViewAtRow(logView.rows - 1, TableView.AlignBottom)
                }
            }
        }

        Keys.onPressed: (event) => {
            if (event.key === Qt.Key_Control) {
                interactive = false
            } else if ((event.modifiers & Qt.ControlModifier) && event.key === Qt.Key_C) {
                // Ctrl+C: Copy selected cells
                root.copySelectedCells()
                event.accepted = true
            } else if ((event.modifiers & Qt.ControlModifier) && event.key === Qt.Key_V) {
                // Ctrl+V: Paste (for future implementation)
                root.pasteSelectedCells()
                event.accepted = true
            } else if ((event.modifiers & Qt.ControlModifier) && event.key === Qt.Key_A) {
                // Ctrl+A: Select all
                logView.selectionModel.select(
                    logView.selectionModel.model.index(0, 0),
                    ItemSelectionModel.Select | ItemSelectionModel.Columns
                )
                event.accepted = true
            }
        }

        Keys.onReleased: (event) => {
            if (event.key === Qt.Key_Control) {
                interactive = true
            }
        }
        
        function copySelectedCells() {
            var selectedIndexes = logView.selectionModel.selectedIndexes
            if (selectedIndexes.length === 0) {
                return
            }
            
            // Build a text representation of selected cells
            var textToCopy = ""
            var lastRow = -1
            
            // Sort indices by row then column
            var sortedIndexes = selectedIndexes.slice().sort(function(a, b) {
                if (a.row !== b.row) return a.row - b.row
                return a.column - b.column
            })
            
            for (var i = 0; i < sortedIndexes.length; i++) {
                var index = sortedIndexes[i]
                
                if (index.row !== lastRow) {
                    if (lastRow !== -1) {
                        textToCopy += "\n"
                    }
                    lastRow = index.row
                } else {
                    textToCopy += "\t"
                }
                
                var data = filterProxyModel.data(index, Qt.DisplayRole)
                textToCopy += data ? data.toString() : ""
            }
            
            // Copy to clipboard using controller
            if (textToCopy) {
                controller.copyToClipboard(textToCopy)
            }
        }
        
        function pasteSelectedCells() {
            // Placeholder for paste functionality
            // This can be extended in the future if needed
            console.log("Paste functionality not yet implemented")
        }
    

        selectionModel: ItemSelectionModel {
            model: filterProxyModel
        }
        columnWidthProvider: function (column) {
            return header.itemAt(column).visible ? header.itemAt(column).width : 0
        }

        ScrollBar.vertical: ScrollBar {
            policy: ScrollBar.AsNeeded
            wheelEnabled: true
        }

        ScrollBar.horizontal: ScrollBar {
            policy: ScrollBar.AsNeeded
        }

        model: filterProxyModel

        delegate: Item {
            required property bool selected
            // required property bool current
            implicitHeight: 15
            implicitWidth: header.itemAt(column).visible ? header.itemAt(column).width : 0
            visible: implicitWidth > 0
            property bool isLastColumn: column === header.count - 1
            property int lineNum: lineNumber
            property bool bookmarked: bookmark.highlightLines.includes(lineNum)

            function highlight() {
                highlightAnimation.start()
            }

            onSelectedChanged: {
                if (selected) {
                    root.lastColSelected = Math.max(root.lastColSelected, column)
                    root.firstColSelected = Math.min(root.firstColSelected, column)
                }
            }

            TapHandler {
                acceptedButtons: Qt.LeftButton | Qt.RightButton
                onTapped: (eventPoint, button) => {
                    var cellIndex = filterProxyModel.index(row, column)
                    if (button === Qt.LeftButton) {
                        
                        if (point.modifiers & Qt.ControlModifier) {
                            // Ctrl+Click: Toggle cell selection for multi-column selection
                            if (logView.selectionModel.isSelected(cellIndex)) {
                                logView.selectionModel.select(cellIndex, ItemSelectionModel.Deselect)
                            } else {
                                logView.selectionModel.select(cellIndex, ItemSelectionModel.Select)
                            }
                        } else if (point.modifiers & Qt.ShiftModifier) {
                            // Shift+Click: end line - selects every line between the anchor (start) line and this one.
                            // Note: QML's QModelIndex value type has no "valid" property, so check row >= 0 instead.
                            var currentIndex = logView.selectionModel.currentIndex
                            var anchorRow = currentIndex && currentIndex.row >= 0 ? currentIndex.row : row
                            root.selectRowRange(anchorRow, row)
                            // Remember this click as the anchor so a further Shift+Click extends from here
                            logView.selectionModel.setCurrentIndex(cellIndex, ItemSelectionModel.Current)
                        } else {
                            // Regular click: start line - selects this whole line and becomes the range anchor
                            root.selectRow(row)
                            logView.selectionModel.setCurrentIndex(cellIndex, ItemSelectionModel.Current)
                        }
                        
                        // Show log details and highlight line
                        controller.showLogDetails(lineNumber)
                        if (root.tableType === LogViewTable.TableType.ViewTable) {
                            highlightLine(lineNumber)
                        } else {
                            root.highlightLineNum = lineNumber
                            controller.highlightLineNum = lineNumber
                        }
                    } else if (button === Qt.RightButton) {
                        if (root.tableType === LogViewTable.TableType.ViewTable) {
                            highlightLine(lineNumber)
                        } else {
                            root.highlightLineNum = lineNumber
                            controller.highlightLineNum = lineNumber
                        }
                        // Right-clicking outside the current selection selects just this line, so
                        // "Copy" always acts on the line(s) the user actually meant to copy.
                        if (!logView.selectionModel.isSelected(cellIndex)) {
                            root.selectRow(row)
                            logView.selectionModel.setCurrentIndex(cellIndex, ItemSelectionModel.Current)
                        }
                        let pos = mapToItem(logView, eventPoint.position.x, eventPoint.position.y)
                        optionMenu.openMenu(bookmarked, lineNumber, pos.x, pos.y)
                    }
                }
                onDoubleTapped: {
                    if (root.tableType === LogViewTable.TableType.SearchResultsTable) {
                        console.log("Double clicked on search result line number: " + lineNumber)
                        root.highlightLineNum = lineNumber
                        controller.highlightLineNum = lineNumber
                        helper.autoScrollDown = false
                    }
                }
            }

            Text {
                id: logText
                text: root.highlight && isLastColumn ? highlightProvider(display) : display
                anchors.fill: parent
                horizontalAlignment: isLastColumn ? Text.AlignLeft : Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
                color: (controller.showLogColors && levelColor) ? levelColor : root.logTextColor
                font.family: muktaVaani.font.family
                font.pointSize: 10
                font.bold: highlightAnimation.bolded
                clip: true
                anchors.leftMargin: 4
                anchors.rightMargin: 4
                textFormat: (root.highlight && isLastColumn) ? Text.RichText : Text.PlainText
                z: 10

                onImplicitWidthChanged: {
                    if (isLastColumn && implicitWidth > root.maxMessageTextWidth) {
                        root.pendingMaxMessageTextWidth = Math.max(root.pendingMaxMessageTextWidth, implicitWidth)
                        maxWidthCommitTimer.restart()
                    }
                }
                Component.onCompleted: {
                    if (isLastColumn && implicitWidth > root.maxMessageTextWidth) {
                        root.maxMessageTextWidth = implicitWidth
                        root.pendingMaxMessageTextWidth = Math.max(root.pendingMaxMessageTextWidth, implicitWidth)
                    }
                }
            }

            Rectangle {
                border.width: 0
                anchors.fill: parent
                z: -1
                color: bookmarked ? root.logBookmarkRowColor : ((root.applyFilterColors && filterColor) ? root.filterBgColor(filterColor, 0.3) : root.logRowColor)
            }

            Rectangle {
                visible: selected
                anchors.fill: parent
                anchors.topMargin: 0
                anchors.bottomMargin: 0
                z: 0
                opacity: 0.5
                color: root.logSelectionColor
            }

            Rectangle {
                visible: lineNum === root.highlightLineNum
                anchors.fill: parent
                anchors.topMargin: 0
                anchors.bottomMargin: 0
                z: 1
                opacity: 0.4
                color: root.logSelectionColor
            }

            HighlightAnimation {
                id: highlightAnimation
                anchors.fill: parent
                visible: root.tableType === LogViewTable.TableType.ViewTable
                z: 2
            }
        }
    }

}
