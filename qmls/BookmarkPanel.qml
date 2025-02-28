import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Effects
Item {
    id: root

    Menu {
        id: detail
        width: bookmarkDetail.contentWidth
        height: 26
        property string log: ""
        
        padding: 0
        margins: 0
        topInset: 0
        bottomInset: 0
        leftInset: 0
        rightInset: 0

        enter: Transition {
            ParallelAnimation {
                NumberAnimation { property: "height"; from: 0; to: 26; duration: 200 }
                NumberAnimation { property: "width"; from: 0; to: bookmarkDetail.contentWidth; duration: 200 }
            }
        }
        
        function openMenu(line, x, y) {
            var logMessage = controller.getLogMessage(line)
            detail.log = logMessage
            detail.open()
            detail.x = x + 10
            detail.y = y + 30
        }

        background: Rectangle {
            color: "#d04b4949"
            radius: 4
            border.width: 0.5
            border.color: Qt.rgba(0.5, 0.5, 0.5, 0.5)
        }

        Text {
            id: bookmarkDetail
            text: detail.log
            width: contentWidth
            lineHeightMode: Text.FixedHeight
            height: 26
            color: "#ffffff"
            font.pixelSize: 12
            anchors.fill: parent
            font.family: muktaVaani.font.family
            anchors.left: parent.left
            anchors.leftMargin: 5
            anchors.right: parent.right
            anchors.rightMargin: 5
            anchors.verticalCenter: parent.verticalCenter
            clip: true
        }
    }

    RoundButton {
        id: removeAllBtn
        width: 30
        height: 30
        display: AbstractButton.IconOnly
        icon.source: "./../assets/images/clear_bookmark.svg"
        icon.color: !removeAllBtn.enabled ? "#484625bd" : "#5823d3"
        anchors.top: root.top
        anchors.topMargin: 5
        anchors.right: parent.right
        anchors.rightMargin: 5
        radius: 16
        enabled: bookmark.displayList.length > 0

        background: Rectangle {
            implicitWidth: 30
            implicitHeight: 30
            color: addFilterBtn.down ? "#5d5d5d" : "transparent"
            radius: 16
        }

        onClicked: {
            bookmark.clearAll()
        }
    }

    ListView {
        id: bookmarkList
        width: parent.width
        height: parent.height - removeAllBtn.height
        anchors.top: removeAllBtn.bottom
        // anchors.topMargin: 5
        clip: true
        model: bookmark.displayList
        spacing: 5

        delegate: Item {
            id: bookmarkItem
            width: parent.width
            height: 24
            Rectangle {
                id: itemBg
                width: parent.width
                height: parent.height
                color: "#b0b696ff"
                radius: 2
                border.width: 1
                border.color: "#9fffffff"
                opacity: 0.5
            }

            MouseArea {
                id: bookmarkMouseArea
                anchors.fill: parent
                hoverEnabled: true
                onClicked: {
                    controller.highlightLineNum = modelData.line
                }

                onEntered: {
                    itemBg.color = "#e0b596ff"
                    itemBg.opacity = 0.8
                    let pos = mapToItem(bookmarkList, mouseX , mouseY)
                    detail.openMenu(modelData.line, pos.x, pos.y)
                }

                onExited: {
                    itemBg.color = "#b0b696ff"
                    itemBg.opacity = 0.5
                    detail.close()
                }
            }


            Text {
                id: bookmarkLine
                width: bookmarkList.width * 0.15
                height: 24
                text: modelData.line
                color: "#d4b2f5"
                font.pixelSize: 10
                font.family: concertOne.font.family
                font.bold: true
                anchors.left: parent.left
                anchors.leftMargin: 5
                verticalAlignment: Text.AlignVCenter
                clip: true
            }

            Text {
                id: bookmarkContent
                width: bookmarkList.width * 0.75
                height: 24
                text: modelData.log
                clip: true
                color: "#ffffff"
                font.pixelSize: 12
                anchors.left: bookmarkLine.right
                font.family: concertOne.font.family
                anchors.leftMargin: 8
                verticalAlignment: Text.AlignVCenter
            }
        }
    }
}