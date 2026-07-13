import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Universal 2.12
import QtQuick.Dialogs
import QtQuick.Effects
import Styles
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
        color: ({
            [Styler.ThemeMode.DARK]: "#2b2b2b",
            [Styler.ThemeMode.LIGHT]: "#eff0f3"
        })[Styler.themeMode]
        opacity: 1
        radius: 4
        border.width: 1
        border.color: ({
            [Styler.ThemeMode.DARK]: "#5f6160",
            [Styler.ThemeMode.LIGHT]: "#5D21D0"
        })[Styler.themeMode]
    }

    MultiEffect {
        source: dialogBg
        anchors.fill: dialogBg
        blurEnabled: true
        blur: ({
            [Styler.ThemeMode.DARK]: 0.2,
            [Styler.ThemeMode.LIGHT]: 0.9
        })[Styler.themeMode]
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
        color: ({
            [Styler.ThemeMode.DARK]: "#ECEDF5",
            [Styler.ThemeMode.LIGHT]: "#3F3075"
        })[Styler.themeMode]
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
        color: ({
            [Styler.ThemeMode.DARK]: "#ECEDF5",
            [Styler.ThemeMode.LIGHT]: "#3F3075"
        })[Styler.themeMode]
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
            color: ({
                [Styler.ThemeMode.DARK]: "#434342",
                [Styler.ThemeMode.LIGHT]: "#e2dae1"
            })[Styler.themeMode]
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
    }

    TextField {
        id: tagString
        width: parent.width - 10
        height: 32
        anchors.top: filterName.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: ({
            [Styler.ThemeMode.DARK]: "#ECEDF5",
            [Styler.ThemeMode.LIGHT]: "#3F3075"
        })[Styler.themeMode]
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return filterProfile.tag
            }
        }

        placeholderText: "Tag regex (e.g. tag1|tag2)"

        background: Rectangle {
            color: ({
                [Styler.ThemeMode.DARK]: "#434342",
                [Styler.ThemeMode.LIGHT]: "#e2dae1"
            })[Styler.themeMode]
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
    }

    TextField {
        id: pidString
        width: parent.width - 10
        height: 32
        anchors.top: tagString.bottom
        anchors.topMargin: 10
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: ({
            [Styler.ThemeMode.DARK]: "#ECEDF5",
            [Styler.ThemeMode.LIGHT]: "#3F3075"
        })[Styler.themeMode]
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return filterProfile.pid
            }
        }

        placeholderText: "PID regex (e.g. 781|795)"

        background: Rectangle {
            color: ({
                [Styler.ThemeMode.DARK]: "#434342",
                [Styler.ThemeMode.LIGHT]: "#e2dae1"
            })[Styler.themeMode]
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
    }

    TextField {
        id: tidString
        width: parent.width - 10
        height: 32
        anchors.top: pidString.bottom
        anchors.topMargin: 10
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: ({
            [Styler.ThemeMode.DARK]: "#ECEDF5",
            [Styler.ThemeMode.LIGHT]: "#3F3075"
        })[Styler.themeMode]
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return filterProfile.tid
            }
        }

        placeholderText: "TID regex (e.g. 781|795)"

        background: Rectangle {
            color: ({
                [Styler.ThemeMode.DARK]: "#434342",
                [Styler.ThemeMode.LIGHT]: "#e2dae1"
            })[Styler.themeMode]
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
    }

    RoundButton {
        id: filterColor
        width: 24
        height: 24
        anchors.top: tidString.bottom
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
                return ({
                    [Styler.ThemeMode.DARK]: "#ECEDF5",
                    [Styler.ThemeMode.LIGHT]: "#3F3075"
                })[Styler.themeMode]
            }
        }
        background: Rectangle {
            color: ({
                [Styler.ThemeMode.DARK]: "#434342",
                [Styler.ThemeMode.LIGHT]: "transparent"
            })[Styler.themeMode]
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
        id: checkAndroidBtn
        width: 130
        height: 30
        anchors.left: parent.left
        anchors.leftMargin: 44
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 5
        font.family: muktaVaani.font.family
        contentItem: Text {
            text: "Check Android"
            color: "#ECEDF5"
            verticalAlignment: Text.AlignVCenter
            horizontalAlignment: Text.AlignHCenter
            font.family: muktaVaani.font.family
            font.bold: true
            font.pixelSize: 12
        }

        background: Rectangle {
            color: Styler.ThemeMode.DARK === Styler.themeMode ? "#3f6254" : "#2e9c75"
            radius: 4
            border.width: 1
            border.color: "#7f8180"
        }

        onClicked: {
            controller.checkCurrentAndroidFilter(filterName.text, tagString.text)
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
        font.family: muktaVaani.font.family
        contentItem: Text {
            text: "OK"
            color: ({
                [Styler.ThemeMode.DARK]: "#ECEDF5",
                [Styler.ThemeMode.LIGHT]: "#ECEDF5"
            })[Styler.themeMode]
            verticalAlignment: Text.AlignVCenter
            horizontalAlignment: Text.AlignHCenter
            font.family: muktaVaani.font.family
            font.bold: true
            font.pixelSize: 13
        }

        background: Rectangle {
            color: Styler.ThemeMode.DARK === Styler.themeMode ? "#444645" : "#7d85ff"
            radius: 4
            border.width: 1
            border.color: "#7f8180"
        }

        onClicked: {
            if (root.type === FilterDetailPanel.Type.New) {
                controller.addFilter(filterName.text, tagString.text, pidString.text, tidString.text, filterColor.icon.color)
            } else {
                controller.updateFilter(root.filterProfile.id, filterName.text, tagString.text, pidString.text, tidString.text, root.filterProfile.enabled, filterColor.icon.color)
            }
            root.close()
        }
    }
}