import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Dialogs
Item {
    id: root
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
            color: "#303030"
            radius: 2
            border.width: 0.5
            border.color: Qt.rgba(0.5, 0.5, 0.5, 0.5)
        }

        Button {
            id: removeFilterBtn
            width: parent.width
            height: 20
            anchors.centerIn: parent
            font.family: muktaVaani.font.family
            hoverEnabled: true
            contentItem: Text {
                text: "Remove"
                color: removeFilterBtn.hovered ? "#A65DEE" : "#ffffff"
                font.pixelSize: 14
                verticalAlignment: Text.AlignVCenter
                horizontalAlignment: Text.AlignHCenter
            }
            flat: true

            background: Rectangle {
                color: removeFilterBtn.down ? "#A65DEE" : "transparent"
                radius: 2
            }
            onClicked: {
                controller.removeFilter(rightPressMenu_filterTab.idSelected)
                rightPressMenu_filterTab.close()
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
            icon.color: bar.currentIndex === 0 ? "transparent" : "#ffffff"
            background: Rectangle {
                color: bar.currentIndex === 0 ?  "transparent" : "#444444"
                radius: 2
            }
        }
        TabButton {
            display: AbstractButton.IconOnly
            icon.source: "./../assets/images/bookmark_icon.png"
            icon.width: 15
            icon.height: 15
            icon.color: bar.currentIndex === 1 ? "transparent" : "#ffffff"
            background: Rectangle {
                color: bar.currentIndex === 1 ? "transparent" : "#444444"
                radius: 2
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
                        color: "transparent"
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
                        width: 20
                        height: 20
                        display: AbstractButton.IconOnly

                        onCheckStateChanged: {
                            filterTab.hasChanges = true
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
                        color: "#CAAFE4"
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
                            onClicked: {
                                colorDialog.selectedFilterID = modelData.id
                                colorDialog.open()
                            }
                        }
                    }
                }
            }

            Item {
                id: applyChangesBtn
                width: parent.width
                height: 30
                anchors.bottom: parent.bottom
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.rightMargin: 10
                anchors.leftMargin: 10
                anchors.bottomMargin: 10
                enabled: filterTab.hasChanges

                Rectangle {
                    id: applyChangesBg
                    anchors.fill: parent
                    color: parent.enabled ? (applyChangesArea.containsMouse ? "#5D62EE" : "#444444") : Qt.rgba(207, 202, 202, 0.18)
                    radius: 2
                    border.width: 1
                    border.color: "#aeb2b0"
                }

                Text {
                    id: applyChangesText
                    anchors.centerIn: parent
                    text: "Apply"
                    color: parent.enabled ? "#ECEDF5" : Qt.rgba(202, 200, 200, 0.15)
                    font.family: muktaVaani.font.family
                    font.pixelSize: 14
                }
                
                MouseArea {
                    id: applyChangesArea
                    anchors.fill: parent
                    hoverEnabled: true
                    enabled: filterTab.hasChanges
                    onClicked: {
                        controller.applyFilterChanges()
                        filterTab.hasChanges = false
                    }
                }
            }

            Rectangle {
                id: filterTabBg
                anchors.fill: parent
                color: "transparent"
                border.width: 1
                border.color: "#6d6d6d"
                opacity: 0.4
                z: -1
            }
        }
        Item {
            id: discoverTab
            Rectangle {
                id: discoverTabBg
                anchors.fill: parent
                color: "transparent"
                border.width: 1
                border.color: "#6d6d6d"
                opacity: 0.4
                z: -1
            }
        }

    }
}
