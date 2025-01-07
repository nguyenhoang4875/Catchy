import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Universal 2.12
import QtQuick.Dialogs

Popup {
    id: root
    property string message: ""
    background: Rectangle {
        id: dialogBg
        anchors.fill: parent
        color: "#5b5f5f"
        radius: 4
        border.width: 1
        border.color: "#aeb2b0"
    }


    Text {
        id: title
        width: parent.width
        height: 30
        text: "Notification"
        anchors.top: parent.top
        anchors.topMargin: 5
        anchors.left: icon.right
        anchors.leftMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 16
    }

    Text {
        id: message
        width: parent.width
        height: parent.height - title.height
        text: root.message
        anchors.top: title.bottom
        anchors.topMargin: 5
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignTop
        font.family: muktaVaani.font.family
    }
}