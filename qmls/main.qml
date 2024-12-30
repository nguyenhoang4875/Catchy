
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

                Keys.onPressed: (event) => {
                    if (event.key === Qt.Key_Return) {
                        console.log("Enter pressed: " + searchInput.text)
                        controller.setSearchRegex(searchInput.text)
                        controller.setShowSearchResults(true)
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
                SplitView.preferredHeight: verSplit.height * 0.28
                SplitView.fillWidth: true

                LogViewTable {
                    id: searchResultTable
                    anchors.fill: parent
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
                SplitView.fillHeight: true
                SplitView.fillWidth: true

                TextArea {
                    id: detailTextArea
                    anchors.fill: parent
                    text: controller.hightlightSearchResults(controller.detailsText)
                    wrapMode: Text.WordWrap
                    readOnly: true
                    font.family: concertOne.font.family
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
    }
}
