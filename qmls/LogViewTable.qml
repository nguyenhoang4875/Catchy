import QtQuick
import QtQuick.Controls 2.15
import Qt.labs.qmlmodels 1.0
import QtQuick.Controls.Universal 2.12
import QtQuick.Layouts 1.13
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
    property bool highlight: false
    property var highlightProvider: function(text) {
        return controller.hightlightSearchResults(text)
    }
    property alias logview: logView
    property int lastColSelected: -1
    property int firstColSelected: logHeaderModel.count
    property var highlightLineNum: controller.highlightLineNum
    onHighlightLineNumChanged: {
        if (root.tableType === LogViewTable.TableType.ViewTable) {
            let rowIdx = filterProxyModel.rowLineNum(highlightLineNum)
            logview.positionViewAtRow(rowIdx, TableView.AlignCenter)
            // let item = logview.itemAtIndex(logview.index(rowIdx, 0))
            // item.highlight()
            delayTimer.restart()
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
        height: 30
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
            anchors.centerIn: parent
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
    }

    Timer {
        id: delayTimer
        interval: 300
        onTriggered: {
            let rowIdx = filterProxyModel.rowLineNum(highlightLineNum)
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
        ListElement { title: "Date Time";       size: 0.17 ;    resizeable: true }
        ListElement { title: "Time Stamp";      size: 0.1 ;     resizeable: true }
        ListElement { title: "Log level";       size: 0.07 ;    resizeable: true }
        ListElement { title: "Process Name";    size: 0.1 ;     resizeable: true }
        ListElement { title: "Message";         size: 0 ;       resizeable: true }
    }

    Connections {
        target: controller

        function onShowLessColumnsChanged() {
            if (controller.showLessColumns) {
                logHeaderModel.setProperty(0, "size", 0)
                logHeaderModel.setProperty(0, "resizeable", false)
                logHeaderModel.setProperty(1, "size", 0)
                logHeaderModel.setProperty(1, "resizeable", false)
                logHeaderModel.setProperty(2, "size", 0)
                logHeaderModel.setProperty(2, "resizeable", false)
                logHeaderModel.setProperty(3, "size", 0.1)
                logHeaderModel.setProperty(4, "size", 0.9)
            } else {
                logHeaderModel.setProperty(0, "size", 0.17)
                logHeaderModel.setProperty(0, "resizeable", true)
                logHeaderModel.setProperty(1, "size", 0.1)
                logHeaderModel.setProperty(1, "resizeable", true)
                logHeaderModel.setProperty(2, "size", 0.07)
                logHeaderModel.setProperty(2, "resizeable", true)
                logHeaderModel.setProperty(3, "size", 0.1)
                logHeaderModel.setProperty(4, "size", 0)
            }
        }
    }

    SplitView {
        id: header
        anchors.top: root.top
        anchors.left: root.left
        anchors.right: root.right
        height: 30
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

    SortFilterProxyModel {
        id: filterProxyModel
    }

    TableView {
        id: logView
        visible: root.showTable
        anchors.top: header.bottom
        anchors.left: root.left
        anchors.right: root.right
        height: root.height - header.height
        width: root.width

        columnSpacing: 0.5 
        rowSpacing: 0
        boundsBehavior: Flickable.StopAtBounds
        clip: true
        focus: true

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
            }
        }

        Keys.onReleased: (event) => {
            if (event.key === Qt.Key_Control) {
                interactive = true
            }
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
                    if (button === Qt.LeftButton) {
                        controller.showLogDetails(lineNumber)
                    } else if (button === Qt.RightButton) {
                        let pos = mapToItem(logView, eventPoint.position.x, eventPoint.position.y)
                        optionMenu.openMenu(bookmarked, lineNumber, pos.x, pos.y)
                    }
                }
                onDoubleTapped: {
                    if (root.tableType === LogViewTable.TableType.SearchResultsTable) {
                        console.log("Double clicked on search result line number: " + lineNumber)
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
                color: controller.showLogColors && decoration ? decoration : root.logTextColor
                font.family: muktaVaani.font.family
                font.pointSize: 10
                // wrapMode: Text.WordWrap
                // font.bold: true
                clip: true
                anchors.leftMargin: 4
                anchors.rightMargin: 4
                textFormat: TextEdit.RichText
                z: 10
                // readOnly: true
            }

            Rectangle {
                border.width: 0
                anchors.fill: parent
                z: -1
                color: bookmarked ? root.logBookmarkRowColor : root.logRowColor
            }

            Rectangle {
                visible: selected
                anchors.fill: parent
                anchors.topMargin: 0
                anchors.bottomMargin: 0
                z: 0
                opacity: 0.25
                color: root.logSelectionColor
            }

            HighlightAnimation {
                id: highlightAnimation
                anchors.fill: parent
                visible: root.tableType === LogViewTable.TableType.ViewTable
                z: 1
            }
        }
    }

    Component.onCompleted: {
        if (controller.showLessColumns) {
            logHeaderModel.setProperty(0, "size", 0)
            logHeaderModel.setProperty(0, "resizeable", false)
            logHeaderModel.setProperty(1, "size", 0)
            logHeaderModel.setProperty(1, "resizeable", false)
            logHeaderModel.setProperty(2, "size", 0)
            logHeaderModel.setProperty(2, "resizeable", false)
            logHeaderModel.setProperty(3, "size", 0.1)
            logHeaderModel.setProperty(4, "size", 0.9)
        } else {
            logHeaderModel.setProperty(0, "size", 0.17)
            logHeaderModel.setProperty(0, "resizeable", true)
            logHeaderModel.setProperty(1, "size", 0.1)
            logHeaderModel.setProperty(1, "resizeable", true)
            logHeaderModel.setProperty(2, "size", 0.07)
            logHeaderModel.setProperty(2, "resizeable", true)
            logHeaderModel.setProperty(3, "size", 0.1)
            logHeaderModel.setProperty(4, "size", 0)
        }
    }
}
