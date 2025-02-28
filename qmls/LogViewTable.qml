import QtQuick
import QtQuick.Controls 2.15
import Qt.labs.qmlmodels 1.0
import QtQuick.Controls.Universal 2.12
import QtQuick.Layouts 1.13
import com.mycompany.qmlcomponents 1.0

Item {
    id: root
    enum TableType {
        ViewTable,
        SearchResultsTable
    }
    property var tableType
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
            color: "#303030"
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
                color: makeBookmarkBtn.hovered ? "#acf39999" : "#ffffff"
                font.pixelSize: 12
                verticalAlignment: Text.AlignVCenter
                horizontalAlignment: Text.AlignHCenter
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
        ListElement { title: "Date Time";       size: 0.17 }
        ListElement { title: "Time Stamp";      size: 0.1 }
        ListElement { title: "Log level";       size: 0.07 }
        ListElement { title: "Process Name";    size: 0.1 }
        ListElement { title: "Message";         size: 0 }
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
            color: SplitHandle.pressed ? "#81e889"
                                       : (SplitHandle.hovered ? Qt.lighter("#c2f4c6", 1.1) : "#5F606A")
        }

        Repeater {
            model: logHeaderModel
            delegate: Rectangle {
                SplitView.preferredWidth: model.size !== 0 ? model.size * root.width : undefined
                SplitView.fillWidth: (model.size === 0)
                SplitView.fillHeight: true
                color: "#222222"
                Text {
                    anchors.centerIn: parent
                    text: model.title
                    color: "#ffffff"
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

        columnSpacing: 2
        rowSpacing: 1
        boundsBehavior: Flickable.StopAtBounds
        clip: true
        focus: true

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
            return header.itemAt(column).width
        }

        ScrollBar.vertical: ScrollBar {
            policy: ScrollBar.AsNeeded
            wheelEnabled: true
        }

        model: filterProxyModel

        delegate: Item {
            required property bool selected
            // required property bool current
            implicitHeight: 30
            implicitWidth: header.itemAt(column).width
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
                color: decoration
                font.family: muktaVaani.font.family
                font.pointSize: 10
                // wrapMode: Text.WordWrap
                // font.bold: true
                clip: true
                anchors.leftMargin: 5
                anchors.rightMargin: 5
                textFormat: TextEdit.RichText
                z: 10
                // readOnly: true
            }

            Rectangle {
                border.width: 1
                anchors.fill: parent
                z: -1
                color: bookmarked ? "#47fd5e5e" : "#272727"
                border.color: "#272727"
            }

            Rectangle {
                visible: selected
                anchors.fill: parent
                anchors.topMargin: 3
                anchors.bottomMargin: 3
                z: 0
                opacity: 0.9
                color: "#323553"
            }

            HighlightAnimation {
                id: highlightAnimation
                anchors.fill: parent
                visible: root.tableType === LogViewTable.TableType.ViewTable
                z: 1
            }
        }
    }
    
}
