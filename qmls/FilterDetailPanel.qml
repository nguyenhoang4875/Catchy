import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Universal 2.12
import QtQuick.Dialogs
import QtQuick.Effects
Popup {
    id: root
    parent: Overlay.overlay

    enum Type {
        New,
        Edit
    }

    property int type: FilterDetailPanel.Type.New
    property var filterProfile

    background: Rectangle {
        id: dialogBg
        anchors.fill: parent
        color: "#363636"
        opacity: 1
        radius: 4
        border.width: 1
        border.color: "#5f6160"
    }

    MultiEffect {
        source: dialogBg
        anchors.fill: dialogBg
        blurEnabled: true
        blur: 0.2
    }

    Image {
        id: icon
        width: 22
        height: 22
        anchors.top: parent.top
        anchors.topMargin: 10
        anchors.left: parent.left
        anchors.leftMargin: 10
        sourceSize.width: 22
        sourceSize.height: 22
        source: "./../assets/images/filter_panel_icon.svg"
        
    }

    Text {
        id: dialogTitle
        width: parent.width
        height: 30
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "New"
            } else {
                return "Edit"
            }
        }
        anchors.top: parent.top
        anchors.topMargin: 5
        anchors.left: icon.right
        anchors.leftMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 16
    }

    TextField {
        id: filterName
        width: parent.width - 10
        height: 32
        anchors.top: dialogTitle.bottom
        anchors.topMargin: 10
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return filterProfile.name
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter filter name"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: "#434342"
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
    }

    TextField {
        id: processNameString
        width: parent.width - 10
        height: 32
        anchors.top: filterName.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return filterProfile.processName
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter process name"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: "#434342"
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
            
    }

    RoundButton {
        id: filterColor
        width: 24
        height: 24
        anchors.top: processNameString.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        icon.source: "./../assets/images/palette.svg"
        icon.width: 24
        icon.height: 24
        icon.color: {
            if (root.type === FilterDetailPanel.Type.Edit) {
                return filterProfile.color
            } else {
                return "#ECEDF5 "
            }
        }
        background: Rectangle {
            color: "#434342"
            radius: 16
            border.width: 1
            border.color: "#7e7e7e"
        }
        flat: true
        display: AbstractButton.IconOnly
        padding: 0
        onClicked: {
            colorPicker.open()
        }
    }

    ColorDialog {
        id: colorPicker
        title: "Please choose a color"
        property int currentSelectedFilter: -1
        onAccepted: {
            if (root.type === FilterDetailPanel.Type.New) {
                filterColor.icon.color = colorPicker.selectedColor
            } else {
                root.filterProfile.color = colorPicker.selectedColor
            }
        }
        onRejected: {

        }
    }

    Button {
        id: okBtn
        width: 60
        height: 30
        anchors.right: parent.right
        anchors.rightMargin: 10
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 5
        text: "OK"
        font.family: muktaVaani.font.family

        background: Rectangle {
            color: "#444645"
            radius: 4
            border.width: 1
            border.color: "#7f8180"
        }

        onClicked: {
            if (root.type === FilterDetailPanel.Type.New) {
                controller.addFilter(filterName.text, processNameString.text, filterColor.icon.color)
            } else {
                controller.updateFilter(root.filterProfile.id, filterName.text, processNameString.text, root.filterProfile.enabled, filterColor.icon.color)
            }
            root.close()
        }
    }
}