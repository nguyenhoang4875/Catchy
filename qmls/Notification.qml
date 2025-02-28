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
        id: message
        anchors.fill: parent
        text: root.message
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        horizontalAlignment: Text.AlignHCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 16
    }
}