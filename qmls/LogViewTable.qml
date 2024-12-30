import QtQuick
import QtQuick.Controls 2.15
import Qt.labs.qmlmodels 1.0
import QtQuick.Controls.Universal 2.12
import QtQuick.Layouts 1.13
import com.mycompany.qmlcomponents 1.0

Item {
    id: root
    property alias filterProxyModel: filterProxyModel
    property bool showTable: true
    property bool highlight: false
    property var highlightProvider: function(text) {
        return controller.hightlightSearchResults(text)
    }
    property alias logview: logView
    property int lastColSelected: -1
    property int firstColSelected: logHeaderModel.count

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

            onSelectedChanged: {
                if (selected) {
                    root.lastColSelected = Math.max(root.lastColSelected, column)
                    root.firstColSelected = Math.min(root.firstColSelected, column)
                }
            }

            TapHandler {
                onTapped: controller.showLogDetails(lineNumber)
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
                z: 2
                // readOnly: true
            }

            Rectangle {
                border.width: 1
                anchors.fill: parent
                z: -1
                color: "#272727"
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
        }
    }
    
}
