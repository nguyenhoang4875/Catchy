
import QtQuick
import QtQuick.Controls 2.15
import Qt.labs.qmlmodels 1.0
import QtQuick.Controls.Universal 2.12
import QtQuick.Layouts 1.13
import "."
ApplicationWindow {
    id: window
    visible: true
    width: 1280
    height: 720
    title: "Log Viewer"
    Universal.theme: Universal.Dark

    Item {
        id: root
        anchors.fill: parent

        FontLoader { id: concertOne; source: "./../assets/fonts/ConcertOne-Regular.ttf" }
        FontLoader { id: muktaVaani; source: "./../assets/fonts/MuktaVaani-SemiBold.ttf" }
        FontLoader { id: moiraiOne; source: "./../assets/fonts/MoiraiOne-Regular.ttf" }

        Rectangle {
            id: mainBg
            color: "#454545"
            opacity: 0.5
            z: -100
            anchors.fill: root
        }

        Item {
            id: menuBar
            height: 30
            width: parent.width
            anchors.top: parent.top
            anchors.left: parent.left

            Button {
                id: fileBtn
                anchors.left: parent.left
                width: 40
                height: 30
                hoverEnabled: true
                font.family: muktaVaani.font.family
                background: Rectangle {
                    color: "#303030"
                }

                icon.source: "./../assets/images/settings_icon.png"

                onClicked: fileMenu.open()

                Menu {
                    id: fileMenu
                    y: fileBtn.height
                    width: 160

                    background: Rectangle {
                        color: "#303030"
                        radius: 4
                    }
                    
                    Button {
                        width: parent.width
                        height: 26

                        Text {
                            width: contentWidth
                            height: parent.height
                            text: "Open file"
                            anchors.left: parent.left
                            anchors.leftMargin: parent.width / 3
                            color: "#ECEDF5"
                            verticalAlignment: Text.AlignVCenter
                            font.family: muktaVaani.font.family
                        }

                        onClicked: {
                            controller.openFileDialog()
                            fileMenu.close()
                        }
                    }
                    Button {
                        width: parent.width
                        height: 26

                        Text {
                            width: contentWidth
                            height: parent.height
                            text: "Load filter"
                            anchors.left: parent.left
                            anchors.leftMargin: parent.width / 3
                            color: "#ECEDF5"
                            verticalAlignment: Text.AlignVCenter
                            font.family: muktaVaani.font.family
                        }

                        onClicked: {
                            controller.openFilterDialog()
                            fileMenu.close()
                        }
                    }

                    Button {
                        width: parent.width
                        height: 26

                        Text {
                            width: contentWidth
                            height: parent.height
                            text: "Settings"
                            anchors.left: parent.left
                            anchors.leftMargin: parent.width / 3
                            color: "#ECEDF5"
                            verticalAlignment: Text.AlignVCenter
                            font.family: muktaVaani.font.family
                        }
                    }
                }
            }

            Button {
                id: streamingControlBtn
                anchors.left: fileBtn.right
                width: 40
                height: 30
                hoverEnabled: true
                font.family: muktaVaani.font.family
                background: Rectangle {
                    color: "#303030"
                }
                enabled: remoteDeviceManager.hasConnection
                padding: 0
                icon.source: remoteDeviceManager.streaming ? "./../assets/images/pause_streaming.svg" : "./../assets/images/start_streaming.svg"
                icon.color: !enabled ? "#8c888888" : remoteDeviceManager.streaming ? "#00ff55" : "#ffffff"
                onClicked: {
                    if (remoteDeviceManager.streaming) {
                        controller.stopStreaming()
                    } else {
                        controller.startStreaming()
                    }
                }
            }

            Button {
                id: clearLogBtn
                anchors.left: streamingControlBtn.right
                width: 40
                height: 30
                // hoverEnabled: true
                // padding: 0
                background: Rectangle {
                    color: "transparent"
                    z: -1
                }
                icon.source: "./../assets/images/clear_icon.svg"
                icon.color: "#ffffff"
                onClicked: controller.clearLog()
            }

            Button {
                id: autoScrollDownBtn
                anchors.left: clearLogBtn.right
                width: 40
                height: 30
                hoverEnabled: true
                padding: 0
                icon.source: "./../assets/images/auto_scroll_down.svg"
                background: Rectangle {
                    color: helper.autoScrollDown ? "#f86f46cf" : "transparent"
                    radius: 4
                    width: autoScrollDownBtn.width - 10
                    height: autoScrollDownBtn.height - 6
                    anchors.centerIn: autoScrollDownBtn
                }

                onClicked: {
                    helper.autoScrollDown = !helper.autoScrollDown
                }
            }

            TextInput {
                id: searchInput
                width: menuBar.width / 3
                height: 25
                anchors.verticalCenter: menuBar.verticalCenter
                anchors.horizontalCenter: menuBar.horizontalCenter
                font.pixelSize: 14
                font.family: muktaVaani.font.family
                verticalAlignment: Text.AlignVCenter
                color: "#ffffff"
                leftPadding: 10
                enabled: controller.logViewReady
                clip: true

                Keys.onPressed: (event) => {
                    if (event.key === Qt.Key_Return) {
                        console.log("Enter pressed: " + searchInput.text)
                        controller.setSearchRegex("")
                        controller.setSearchRegex(searchInput.text)
                        if (searchInput.text !== "") {
                            controller.setShowSearchResults(true)
                        } else {
                            controller.setShowSearchResults(false)
                        }
                    }
                }


                Text {
                    id: searchHint
                    anchors.fill: searchInput
                    verticalAlignment: Text.AlignVCenter
                    horizontalAlignment: Text.AlignHCenter
                    text: "Search"
                    color: "#6E6E6E"
                    font.pixelSize: 14
                    font.family: concertOne.font.family
                    z: 0
                    visible: searchInput.text.length === 0 && !searchInput.focus
                }

                Rectangle {
                    id: borderSearchInput
                    anchors.fill: parent
                    border.width: 1
                    border.color: "#595959"
                    radius: 2
                    color: "#3B3B3B"
                    z: -1
                }
            }

            Text {
                id: author
                anchors.right: parent.right
                verticalAlignment: Text.AlignBottom
                height: 30
                width: 120
                text: "by @phi.nguyen"
                font.pixelSize: 14
                font.family: moiraiOne.font.family
                color: "#ffffff"
                antialiasing: true
                font.bold: true
            }

            Rectangle {
                id: menuBarBg
                anchors.fill: parent
                color: "#303030"
                z: -1
            }
        }

        SplitView {
            id: verSplit
            orientation: Qt.Vertical
            anchors.left: root.left
            anchors.right: root.right
            anchors.bottom: root.bottom
            anchors.top: menuBar.bottom

            Item {
                id: topView
                SplitView.preferredHeight: verSplit.height * 0.65
                SplitView.fillWidth: true
                SplitView {
                    id: horSplit
                    orientation : Qt.Horizontal
                    height      : topView.height
                    width       : topView.width
                    LeftToolPanel {
                        id: leftView
                        SplitView.preferredWidth: horSplit.width * 0.15
                        SplitView.fillHeight: true
                    }

                    Item {
                        id: centerView
                        SplitView.fillWidth: true
                        SplitView.fillHeight: true

                        LogViewTable {
                            id: logviewTable
                            anchors.fill: parent
                            tableType: LogViewTable.TableType.ViewTable
                            filterProxyModel.sourceModel: logModel
                            filterProxyModel.filterKeyColumn: 3
                            filterProxyModel.filterRegularExpression: filterLog.filteredRegex
                        }

                        SelectionRectangle {
                            id: selectionRectangle
                        }

                        Shortcut {
                            sequences: [ StandardKey.Copy ]
                            onActivated: {
                                let copyString = ""
                                let indexes
                                if (logviewTable.logview.selectionModel.hasSelection) {
                                    indexes = logviewTable.logview.selectionModel.selectedIndexes
                                } else {
                                    indexes = searchResultTable.logview.selectionModel.selectedIndexes
                                }

                                let line = []
                                let lines = []
                                for (var i of indexes) {
                                    line.push(i.data())
                                    if (i.column === logviewTable.lastColSelected) {
                                        lines.push(line.join(" "))
                                        line = []
                                    }
                                }
                                copyString = lines.join("\n")
                                controller.copyToClipboard(copyString)
                            }
                        }

                    }

                    
                }
            }

            Item {
                id: bottomView
                SplitView.fillHeight: true
                SplitView.fillWidth: true

                LogViewTable {
                    id: searchResultTable
                    anchors.fill: parent
                    tableType: LogViewTable.TableType.SearchResultsTable
                    showTable: searchLog.showSearchResults
                    highlight: true
                    filterProxyModel.sourceModel: logviewTable.filterProxyModel
                    filterProxyModel.filterKeyColumn: 4
                    filterProxyModel.filterRegularExpression: searchLog.searchRegex
                }

                SelectionRectangle {
                    target: searchResultTable.logview
                }
            }

            Item {
                id: detailView
                SplitView.fillWidth: true
                SplitView.preferredHeight: verSplit.height * 0.05
                SplitView.minimumHeight: 50

                Rectangle {
                    id: topLine
                    width: parent.width
                    anchors.top: parent.top
                    height: 1
                    color: Qt.rgba(232, 188, 245, 0.43)
                    z: 1
                }

                Rectangle {
                    id: detailViewBg
                    anchors.fill: parent
                    color: "#303030"
                    z: -1
                }


                Image {
                    id: detailIcon
                    width: 25
                    height: 25
                    anchors.left: parent.left
                    anchors.leftMargin: 5
                    anchors.right: detailTextArea.left
                    anchors.rightMargin: 5
                    source: "./../assets/images/detail_icon.png"
                    anchors.verticalCenter: parent.verticalCenter
                }

                TextArea {
                    id: detailTextArea
                    anchors.fill: parent
                    anchors.left: detailIcon.right
                    anchors.leftMargin: 35
                    text: controller.hightlightSearchResults(controller.detailsText)
                    wrapMode: Text.WordWrap
                    readOnly: true
                    font.family: concertOne.font.family
                    verticalAlignment: Text.AlignVCenter
                    font.pixelSize: 14
                    color: "#ffffff"
                    textFormat: Text.RichText
                    background: Rectangle {
                        color: "#303030"
                        z: -2
                    }

                    Rectangle {
                        id: fadeRect
                        anchors.fill: parent
                        color: Qt.rgba(240, 241, 168, 0.43)
                        opacity: 0.0
                        z: -1
                    }

                    SequentialAnimation {
                        id: fadeInOutAnimation

                        // Fade in animation
                        NumberAnimation {
                            target: fadeRect
                            property: "opacity"
                            from: 0.0
                            to: 0.5
                            duration: 500  // 1 second fade-in
                            easing.type: Easing.InOutQuad
                        }

                        PauseAnimation {
                            duration: 200  // Hold at full opacity for 1 second
                        }

                        // Fade out animation
                        NumberAnimation {
                            target: fadeRect
                            property: "opacity"
                            from: 0.5
                            to: 0.0
                            duration: 200  // 1 second fade-out
                            easing.type: Easing.InOutQuad
                        }

                        PauseAnimation {
                            duration: 300  // Hold at no opacity for 1 second
                        }
                    }

                    onTextChanged: {
                        fadeInOutAnimation.start()
                    }
                }
            }
        }

        Connections {
            target: controller
            onLoadLogFileCompleted: {
                delayTimer.restart()
            }
        }

        Timer {
            id: delayTimer
            interval: 500
            onTriggered: {
                selectionRectangle.target = logviewTable.logview
            }
        }

        LoadingScreen {
            id: loadingScreen
            visible: controller.showLoadingScreen
            anchors.fill: parent
        }

        FilterDetailPanel {
            id: filterDetailPanel
            anchors.centerIn: parent
            width: parent.width * 0.3
            height: parent.height * 0.3

            function openPanel(_type, index) {
                console.log("openPanel: " + _type + " " + index)
                type = _type
                if (_type === FilterDetailPanel.Type.Edit) filterProfile = filterLog.displayedFilters[index]
                filterDetailPanel.open()
            }
        }

        RemoteDeviceDetailPanel {
            id: remoteDeviceDetailPanel
            anchors.centerIn: parent
            width: 400
            height: 580

            function openPanel(_type, index) {
                console.log("openPanel: " + _type + " " + index)
                remoteDeviceDetailPanel.selectedDeviceIndex = index
                type = _type
                if (_type === RemoteDeviceDetailPanel.Type.Edit) {
                    remoteDeviceDetailPanel.remoteDeviceId      = remoteDeviceManager.deviceList[index].id
                    remoteDeviceDetailPanel.isUseSSHGateway     = remoteDeviceManager.deviceList[index].isUseSSHGateway
                    remoteDeviceDetailPanel.remoteDeviceName    = remoteDeviceManager.deviceList[index].name
                    remoteDeviceDetailPanel.remoteDeviceHost    = remoteDeviceManager.deviceList[index].host
                    remoteDeviceDetailPanel.remoteDevicePort    = remoteDeviceManager.deviceList[index].port
                    remoteDeviceDetailPanel.remoteDeviceUser    = remoteDeviceManager.deviceList[index].username
                    remoteDeviceDetailPanel.remoteLogPath       = remoteDeviceManager.deviceList[index].remoteLogPath
                    remoteDeviceDetailPanel.sshGatewayHost      = remoteDeviceManager.deviceList[index].SSHGateway_IP
                    remoteDeviceDetailPanel.sshGatewayPort      = remoteDeviceManager.deviceList[index].SSHGateway_Port
                    remoteDeviceDetailPanel.sshGatewayUser      = remoteDeviceManager.deviceList[index].SSHGateway_User
                } else {

                }
                remoteDeviceDetailPanel.open()
            }
        }

        Toast {
            id: toast
            width: parent.width / 3
            height: parent.height / 6 - 20
            anchors.bottom: parent.bottom
            anchors.right: parent.right
            z: 1000
        }

    }
}
