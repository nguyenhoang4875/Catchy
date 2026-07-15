import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Dialogs
import Styles
Item {
    id: root
    
    property int _IDLE          : 0
    property int _IN_PROGRESS   : 1
    property int _SUCCESS       : 2
    property int _FAILED        : 3
    

    Connections {
        target: remoteDeviceManager
        onConnectingDeviceChanged: {
            console.log("Connecting device changed: " + remoteDeviceManager.connectingDevice.id)
            // console.log("Connected device changed: " + controller.connectedDevice.id)
        }
    }

    ColorDialog {
        id: colorDialog
        title: "Please choose a color"
        property int selectedFilterID: -1
        onAccepted: {
            console.log("You chose: " + colorDialog.selectedColor)
            controller.updateColorFilter(selectedFilterID, colorDialog.selectedColor)
            selectedFilterID = -1
            filterTab.hasChanges = true
        }
        onRejected: {
            console.log("Canceled")
            selectedFilterID = -1
        }
    }

    Menu {
        id: rightPressMenu_filterTab
        width: 120
        height: 30
        property int idSelected
        padding: 0
        margins: 0
        topInset: 0
        bottomInset: 0
        leftInset: 0
        rightInset: 0
        
        function openMenu(id, x, y) {
            rightPressMenu_filterTab.idSelected = id
            rightPressMenu_filterTab.open()
            rightPressMenu_filterTab.x = x
            rightPressMenu_filterTab.y = y - 20
        }

        background: Rectangle {
            color: ({
                [Styler.ThemeMode.LIGHT]:   "#5684AE",
                [Styler.ThemeMode.DARK]:    "#303030"
            })[Styler.themeMode]
            radius: 2
            border.width: 0.5
            border.color: Qt.rgba(0.5, 0.5, 0.5, 0.5)
        }

        Button {
            id: removeFilterBtn
            width: parent.width
            height: 30
            anchors.centerIn: parent
            font.family: muktaVaani.font.family
            hoverEnabled: true
            contentItem: Text {
                text: "Remove"
                color: removeFilterBtn.hovered ? "#0ac79e" : "#ffffff"
                font.pixelSize: 14
                verticalAlignment: Text.AlignVCenter
                horizontalAlignment: Text.AlignHCenter
                font.bold: true
            }
            flat: true

            background: Rectangle {
                color: removeFilterBtn.down ? "#adc8c7ca" : "transparent"
                radius: 2
            }
            onClicked: {
                controller.removeFilter(rightPressMenu_filterTab.idSelected)
                rightPressMenu_filterTab.close()
            }
        }
    }


    Menu {
        id: rightPressMenu_remoteTab
        width: 120
        height: 30
        property int idSelected
        padding: 0
        margins: 0
        topInset: 0
        bottomInset: 0
        leftInset: 0
        rightInset: 0
        
        function openMenu(id, x, y) {
            rightPressMenu_remoteTab.idSelected = id
            rightPressMenu_remoteTab.open()
            rightPressMenu_remoteTab.x = x
            rightPressMenu_remoteTab.y = y - 20
        }

        background: Rectangle {
            color: ({
                [Styler.ThemeMode.LIGHT]:   "#5684AE",
                [Styler.ThemeMode.DARK]:    "#303030"
            })[Styler.themeMode]
            radius: 2
            border.width: 0.5
            border.color: Qt.rgba(0.5, 0.5, 0.5, 0.5)
        }

        Button {
            id: removeRemoteDeviceBtn
            width: parent.width
            height: 30
            anchors.centerIn: parent
            font.family: muktaVaani.font.family
            hoverEnabled: true
            contentItem: Text {
                text: "Remove"
                color: removeRemoteDeviceBtn.hovered ? "#0ac79e" : "#ffffff"
                font.pixelSize: 14
                verticalAlignment: Text.AlignVCenter
                horizontalAlignment: Text.AlignHCenter
                font.bold: true
            }
            flat: true

            background: Rectangle {
                color: removeRemoteDeviceBtn.down ? "#adc8c7ca" : "transparent"
                radius: 2
            }
            onClicked: {
                controller.removeRemoteDevice(rightPressMenu_remoteTab.idSelected)
                rightPressMenu_remoteTab.close()
            }
        }
    }

    TabBar {
        id: bar
        width: parent.width
        height: 30
        anchors.top: root.top
        anchors.topMargin: 3
        
        TabButton {
            display: AbstractButton.IconOnly
            icon.source: "./../assets/images/filter_icon.png"
            icon.height: 15
            icon.width: 15
            icon.color: ({
                [Styler.ThemeMode.LIGHT]:   bar.currentIndex === 0 ? "#f6622be2" : "#ffffff",
                [Styler.ThemeMode.DARK]:    bar.currentIndex === 0 ? "transparent" : "#ffffff"
            })[Styler.themeMode]
            background: Rectangle {
                color: ({
                    [Styler.ThemeMode.LIGHT]:   bar.currentIndex === 0 ? "transparent" : "#5684AE",
                    [Styler.ThemeMode.DARK]:    bar.currentIndex === 0 ? "transparent" : "#444444"
                })[Styler.themeMode]
                radius: 2
            }
        }
        TabButton {
            display: AbstractButton.IconOnly
            icon.source: "./../assets/images/bookmark_icon.png"
            icon.height: 15
            icon.width: 15
            icon.color: ({
                [Styler.ThemeMode.LIGHT]:   bar.currentIndex === 1 ? "#f6622be2" : "#ffffff",
                [Styler.ThemeMode.DARK]:    bar.currentIndex === 1 ? "transparent" : "#ffffff"
            })[Styler.themeMode]
            background: Rectangle {
                color: ({
                    [Styler.ThemeMode.LIGHT]:   bar.currentIndex === 1 ? "transparent" : "#5684AE",
                    [Styler.ThemeMode.DARK]:    bar.currentIndex === 1 ? "transparent" : "#444444"
                })[Styler.themeMode]
                radius: 2
            }
        }
        TabButton {
            display: AbstractButton.IconOnly
            icon.source: "./../assets/images/remote.svg"
            icon.height: 15
            icon.width: 15
            icon.color: ({
                [Styler.ThemeMode.LIGHT]:   bar.currentIndex === 2 ? "#f6622be2" : "#ffffff",
                [Styler.ThemeMode.DARK]:    bar.currentIndex === 2 ? "transparent" : "#ffffff"
            })[Styler.themeMode]
            background: Rectangle {
                color: ({
                    [Styler.ThemeMode.LIGHT]:   bar.currentIndex === 2 ? "transparent" : "#5684AE",
                    [Styler.ThemeMode.DARK]:    bar.currentIndex === 2 ? "transparent" : "#444444"
                })[Styler.themeMode]
                radius: 2
            }
        }
    }

    Connections {
        target: controller

        function onShowLessColumnsChanged() {
            if (controller.showLessColumns && bar.currentIndex === 1) {
                bar.currentIndex = 0
            }
        }
    }

    StackLayout {
        width: parent.width
        currentIndex: bar.currentIndex
        anchors.top: bar.bottom
        anchors.bottom: root.bottom
        // FILTER TAB ********************************************
        Item {
            id: filterTab
            property bool hasChanges: false
            RoundButton {
                id: addFilterBtn
                width: 30
                height: 30
                display: AbstractButton.IconOnly
                icon.source: "./../assets/images/add_icon.png"
                anchors.top: parent.top
                anchors.topMargin: 5
                anchors.right: parent.right
                anchors.rightMargin: 5
                radius: 16

                background: Rectangle {
                    implicitWidth: 30
                    implicitHeight: 30
                    opacity: enabled ? 1 : 0.3
                    color: addFilterBtn.down ? "#5d5d5d" : "transparent"
                    radius: 16
                }

                onClicked: {
                    filterDetailPanel.openPanel(FilterDetailPanel.Type.New)
                }
            }

            ListView {
                id: filterLv
                width: parent.width
                height: parent.height - addFilterBtn.height - 10
                anchors.left: parent.left
                anchors.bottom: parent.bottom
                spacing: 3
                model: filterLog.displayedFilters

                delegate: Item {
                    id: filterItem
                    width: parent.width
                    height: 30

                    MouseArea {
                        anchors.fill: parent
                        acceptedButtons: Qt.LeftButton | Qt.RightButton
                        onDoubleClicked: {
                            filterDetailPanel.openPanel(FilterDetailPanel.Type.Edit, index)
                        }
                        onPressed: {
                            if (mouse.button === Qt.RightButton) {
                                let pos = mapToItem(window.contentItem, mouse.x, mouse.y)
                                rightPressMenu_filterTab.openMenu(modelData.id, pos.x , pos.y)
                            }
                        }
                    }

                    Rectangle {
                        anchors.fill: parent
                        color: ({
                            [Styler.ThemeMode.LIGHT]:   "#5684AE",
                            [Styler.ThemeMode.DARK]:    "transparent"
                        })[Styler.themeMode]
                        border.color: "#888888"
                        radius: 2
                        opacity: 0.6
                    }

                    CustomCheckBox {
                        id: filterItemCB
                        anchors.left: parent.left
                        anchors.leftMargin: 5
                        anchors.verticalCenter: parent.verticalCenter
                        checked: modelData.enabled
                        enabled: !controller.filteringInProgress
                        width: 20
                        height: 20
                        display: AbstractButton.IconOnly

                        onCheckStateChanged: {
                            if (controller.filteringInProgress) {
                                return
                            }
                            controller.enableFilter(modelData.id, checked)
                        }
                    }

                    Text {
                        id: filterItemName
                        height: parent.height
                        width: parent.width * 0.7
                        anchors.left: parent.left
                        anchors.leftMargin: 35
                        verticalAlignment: Text.AlignVCenter
                        horizontalAlignment: Text.AlignLeft
                        color: ({
                            [Styler.ThemeMode.LIGHT]:   "#ffffff",
                            [Styler.ThemeMode.DARK]:    "#CAAFE4"
                        })[Styler.themeMode]
                        font.family: concertOne.font.family
                        font.pixelSize: 14
                        text: modelData.name
                    }

                    Rectangle {
                        id: filterColor
                        width: 18
                        height: 18
                        anchors.verticalCenter: parent.verticalCenter
                        anchors.right: parent.right
                        anchors.rightMargin: 5
                        color: modelData.color
                        border.width: 1
                        border.color: modelData.color
                        radius: 2
                        z: 0

                        MouseArea {
                            id: colorPickerArea
                            anchors.fill: parent
                            enabled: !controller.filteringInProgress
                            onClicked: {
                                colorDialog.selectedFilterID = modelData.id
                                colorDialog.open()
                            }
                        }
                    }
                }
            }

            Rectangle {
                id: filterTabBg
                anchors.fill: parent
                color: "transparent"
                border.width: 1.2
                border.color: ({
                    [Styler.ThemeMode.LIGHT]:   "#7C92A8",
                    [Styler.ThemeMode.DARK]:    "#6d6d6d"
                })[Styler.themeMode]
                opacity: 0.7
                z: 1
            }
        }
        Item {
            id: bookmarkTab
            Rectangle {
                id: discoverTabBg
                anchors.fill: parent
                color: "transparent"
                border.width: 1.2
                border.color: ({
                    [Styler.ThemeMode.LIGHT]:   "#7C92A8",
                    [Styler.ThemeMode.DARK]:    "#6d6d6d"
                })[Styler.themeMode]
                opacity: 0.7
                z: -1
            }

            BookmarkPanel {
                id: bookmarkPanel
                width: parent.width
                height: parent.height
                anchors.left: parent.left
                anchors.top: parent.top
                anchors.right: parent.right
                anchors.bottom: parent.bottom
            }
        }
        Item {
            id: remoteTab
            
            property string connStatusIcon_n: "./../assets/images/remote_device_n.svg"
            property string connStatusIcon_c: "./../assets/images/remote_device_c.svg"
            

            Rectangle {
                id: remoteTabBg
                anchors.fill: parent
                color: "transparent"
                border.width: 1.2
                border.color: ({
                    [Styler.ThemeMode.LIGHT]:   "#7C92A8",
                    [Styler.ThemeMode.DARK]:    "#6d6d6d"
                })[Styler.themeMode]
                opacity: 0.7
                z: -1
            }

            RoundButton {
                id: addRemoteDeviceBtn
                width: 30
                height: 30
                display: AbstractButton.IconOnly
                icon.source: "./../assets/images/add_icon.png"
                anchors.top: parent.top
                anchors.topMargin: 5
                anchors.right: parent.right
                anchors.rightMargin: 5
                radius: 16

                background: Rectangle {
                    implicitWidth: 30
                    implicitHeight: 30
                    opacity: enabled ? 1 : 0.3
                    color: addFilterBtn.down ? "#5d5d5d" : "transparent"
                    radius: 16
                }

                onClicked: {
                    remoteDeviceDetailPanel.openPanel(RemoteDeviceDetailPanel.Type.New, 0)
                }
            }

            ListView {
                id: remoteDeviceLv
                width: parent.width
                height: parent.height - addRemoteDeviceBtn.height - 10
                anchors.left: parent.left
                anchors.bottom: parent.bottom
                spacing: 3
                model: remoteDeviceManager.deviceList

                delegate: Item {
                    id: remoteDeviceItem
                    width: parent.width
                    height: 30
                    property int deviceId   : modelData.id
                    property string name    : modelData.name
                    property string host    : modelData.host
                    property bool connected : remoteDeviceManager.connectedDevice.id === deviceId
                    property bool connecting: (remoteDeviceManager.connectingDevice.id === deviceId) && (remoteDeviceManager.connectProcessStatus === root._IN_PROGRESS)
                    onConnectingChanged: {
                        console.log("Connecting changed: " + connecting)
                    }
                    property bool disabledDuringHasConnection: remoteDeviceManager.hasConnection && !connected
                    
                    enabled: !disabledDuringHasConnection

                    MouseArea {
                        anchors.fill: parent
                        acceptedButtons: Qt.LeftButton | Qt.RightButton
                        enabled: !remoteDeviceItem.connected
                        onDoubleClicked: {
                            remoteDeviceDetailPanel.openPanel(FilterDetailPanel.Type.Edit, index)
                        }
                        onPressed: {
                            if (mouse.button === Qt.RightButton) {
                                let pos = mapToItem(window.contentItem, mouse.x, mouse.y)
                                rightPressMenu_remoteTab.openMenu(remoteDeviceItem.deviceId, pos.x , pos.y)
                            }
                        }
                    }
                    

                    Rectangle {
                        anchors.fill: parent
                        color: ({
                            [Styler.ThemeMode.LIGHT]:   remoteDeviceItem.enabled ? "#5684AE"    : "#5f5c5c",
                            [Styler.ThemeMode.DARK]:    remoteDeviceItem.enabled ? "transparent" : "#5f5c5c"
                        })[Styler.themeMode]
                        border.color: remoteDeviceItem.enabled ? "#888888" : "#8c888888"
                        radius: 2
                        opacity: 0.6
                    }


                    Button {
                        id: connStatusIcon
                        anchors.left: parent.left
                        anchors.leftMargin: 5
                        anchors.verticalCenter: parent.verticalCenter
                        width: 20
                        height: 20
                        icon.source: remoteDeviceItem.connected ? remoteTab.connStatusIcon_c : remoteTab.connStatusIcon_n
                        icon.color: !remoteDeviceItem.enabled ? "#af7e7e7e" : remoteDeviceItem.connected ? "#0fe62b" : "#fffdfd"
                        display: AbstractButton.IconOnly
                        icon.width: 20
                        icon.height: 20
                        padding: 0
                        background: Rectangle {
                            color: "transparent"
                        }
                    }

                    Text {
                        id: remoteDeviceName
                        anchors.left: connStatusIcon.right
                        width: parent.width * 0.7
                        anchors.leftMargin: 5
                        anchors.verticalCenter: parent.verticalCenter
                        color: remoteDeviceItem.enabled ? "#ffffff" : "#af7e7e7e"
                        font.pixelSize: 14
                        text: remoteDeviceItem.name
                        font.family: concertOne.font.family
                    }

                    AnimatedImage {
                        id: loadingIcon
                        width: 20
                        height: 20
                        anchors.right: parent.right
                        anchors.rightMargin: 5
                        anchors.verticalCenter: parent.verticalCenter
                        source: "./../assets/images/loading-circle.gif"
                        visible: remoteDeviceItem.connecting
                        smooth: true
                        
                    }

                    Button {
                        id: connButton
                        
                        property string iconConnected: "./../assets/images/plug-connected_green.svg"
                        property string iconDisconnected: "./../assets/images/plug-disconnected.svg"
                        property string iconNormal: "./../assets/images/plug-connected.svg"
                        
                        anchors.right: parent.right
                        anchors.rightMargin: 5
                        anchors.verticalCenter: parent.verticalCenter
                        width: 20
                        height: 20
                        icon.source: remoteDeviceItem.connecting ? "" : 
                                                                (remoteDeviceItem.connected ? connButton.iconDisconnected : connButton.iconNormal)
                        hoverEnabled: true
                        icon.color: enabled ? (hovered ? "#6163d6" : "#ffffff") : "#8c888888"
                        display: AbstractButton.IconOnly
                        icon.width: 20
                        icon.height: 20
                        padding: 0
                        enabled: remoteDeviceItem.enabled
                        visible: !remoteDeviceItem.connecting
                        background: Rectangle {
                            color: "transparent"
                        }

                        onClicked: {
                            if (remoteDeviceItem.connected) {
                                controller.requestDisconnectFromDevice()
                            } else {
                                controller.requestConnectToDevice(remoteDeviceItem.deviceId)
                            }
                        }
                    }

                }
            }
        }

    }
}
