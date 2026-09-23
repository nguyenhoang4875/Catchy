
import QtQuick
import QtQuick.Controls 2.15
import Qt.labs.qmlmodels 1.0
import QtQuick.Controls.Universal 2.12
import QtQuick.Layouts 1.13
import "."
import Styles
ApplicationWindow {
    id: window
    visible: true
    width: 1280
    height: 720
    title: controller.openedFileName ? (controller.openedFileName + " - Log Viewer") : "Log Viewer"
    Universal.theme: Styler.themeMode === Styler.ThemeMode.DARK ? Universal.Dark : Universal.Light

    Item {
        id: root
        anchors.fill: parent

        FontLoader { id: concertOne; source: "./../assets/fonts/ConcertOne-Regular.ttf" }
        FontLoader { id: muktaVaani; source: "./../assets/fonts/MuktaVaani-SemiBold.ttf" }
        FontLoader { id: moiraiOne; source: "./../assets/fonts/MoiraiOne-Regular.ttf" }

        Rectangle {
            id: mainBg
            color: ({
                [Styler.ThemeMode.DARK]: "#454545",
                [Styler.ThemeMode.LIGHT]: "#EEF2F7"
            })[Styler.themeMode]
            opacity: 1.0
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
                    color: "transparent"
                }

                icon.source: "./../assets/images/settings_icon.png"
                icon.color: ({
                    [Styler.ThemeMode.DARK]: "#ffffff",
                    [Styler.ThemeMode.LIGHT]: "#1F0954"
                })[Styler.themeMode]

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
                            text: controller.showLogColors ? "Disable Color" : "Enable Color"
                            anchors.left: parent.left
                            anchors.leftMargin: parent.width / 3
                            color: "#ECEDF5"
                            verticalAlignment: Text.AlignVCenter
                            font.family: muktaVaani.font.family
                        }

                        onClicked: {
                            controller.showLogColors = !controller.showLogColors
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
                    color: "transparent"
                }
                enabled: controller.hasAdbDevices && controller.logSource !== "logcat"
                padding: 0
                icon.source: "./../assets/images/start_streaming.svg"
                icon.color: !enabled ? "#8c888888" : ({
                    [Styler.ThemeMode.DARK]: "#ffffff",
                    [Styler.ThemeMode.LIGHT]: "#1F0954"
                })[Styler.themeMode]
                onClicked: controller.setLogSource("logcat")
            }

            Button {
                id: stopStreamingBtn
                anchors.left: streamingControlBtn.right
                width: 40
                height: 30
                hoverEnabled: true
                font.family: muktaVaani.font.family
                background: Rectangle {
                    color: "transparent"
                }
                enabled: controller.hasAdbDevices && controller.logSource === "logcat"
                padding: 0
                icon.source: "./../assets/images/stop_stream.svg"
                icon.color: !enabled ? "#8c888888" : "#d94c4c"
                onClicked: {
                    controller.stopLogcat()
                    controller.setLogSource("file")
                }
            }

            Button {
                id: saveLogBtn
                anchors.left: stopStreamingBtn.right
                width: 40
                height: 30
                hoverEnabled: true
                font.family: muktaVaani.font.family
                background: Rectangle {
                    color: "transparent"
                }
                enabled: controller.logViewReady
                padding: 0
                icon.source: "./../assets/images/save_file.svg"
                icon.width: 16
                icon.height: 16
                icon.color: ({
                    [Styler.ThemeMode.DARK]: "#ffffff",
                    [Styler.ThemeMode.LIGHT]: "#1F0954"
                })[Styler.themeMode]
                onClicked: {
                    if (controller.logSource === "logcat") {
                        controller.stopLogcat()
                        controller.setLogSource("file")
                    }
                    controller.saveLogFile()
                }
            }

            Button {
                id: clearLogBtn
                anchors.left: saveLogBtn.right
                width: 40
                height: 30
                // hoverEnabled: true
                // padding: 0
                background: Rectangle {
                    color: "transparent"
                    z: -1
                }

                icon.source: "./../assets/images/clear_icon.svg"
                icon.color: ({
                    [Styler.ThemeMode.DARK]: "#ffffff",
                    [Styler.ThemeMode.LIGHT]: "#1F0954"
                })[Styler.themeMode]
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
                icon.color: ({
                    [Styler.ThemeMode.DARK]: "#ffffff",
                    [Styler.ThemeMode.LIGHT]: helper.autoScrollDown ? "#4fc4cf" : "#1F0954"
                })[Styler.themeMode]
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
                width: menuBar.width / 2
                height: 25
                property string historyHint: ""
                property string coloredDisplayText: ""
                property bool showColoredText: false
                property var filteredHistoryModel: []
                anchors.verticalCenter: menuBar.verticalCenter
                anchors.horizontalCenter: menuBar.horizontalCenter
                font.pixelSize: 14
                font.family: muktaVaani.font.family
                verticalAlignment: Text.AlignVCenter
                color: showColoredText ? "transparent" : ({
                    [Styler.ThemeMode.DARK]: "#ffffff",
                    [Styler.ThemeMode.LIGHT]: "#3A1D5E"
                })[Styler.themeMode]
                leftPadding: 10
                clip: true

                function escapeHtml(text) {
                    return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
                }

                // Highlights each "|"-separated word with a background color using
                // controller.getSearchWordColor, matching the same word's highlight in the
                // search results table. Empty/whitespace-only segments don't consume a color
                // index, mirroring SearchLog's filtering of searchWords on the Python side.
                function colorizeQuery(text) {
                    if (!text) return ""
                    var words = text.split("|")
                    var colored = []
                    var colorIndex = 0
                    for (var i = 0; i < words.length; i++) {
                        var word = words[i]
                        if (word.trim().length === 0) {
                            colored.push(escapeHtml(word))
                        } else {
                            var color = controller.getSearchWordColor(colorIndex)
                            colored.push("<span style='background-color: " + color + "'>" + escapeHtml(word) + "</span>")
                            colorIndex++
                        }
                    }
                    return colored.join("|")
                }

                // Keeps only history entries that contain the current query anywhere, so the
                // popup narrows down as the user types (case-insensitive substring match).
                function updateFilteredHistory() {
                    var all = searchLog.searchHistory
                    var text = searchInput.text
                    if (!text) {
                        searchInput.filteredHistoryModel = all
                        return
                    }
                    var lower = text.toLowerCase()
                    searchInput.filteredHistoryModel = all.filter(function(item) {
                        return item.toLowerCase().includes(lower)
                    })
                }

                onFilteredHistoryModelChanged: {
                    historyListView.currentIndex = filteredHistoryModel.length > 0 ? 0 : -1
                }

                Component.onCompleted: {
                    searchInput.text = controller.getCurrentSearchQuery()
                    searchInput.historyHint = controller.getSearchHistoryHint(searchInput.text)
                    searchInput.updateFilteredHistory()
                    if (searchInput.text.length > 0) {
                        searchInput.coloredDisplayText = searchInput.colorizeQuery(searchInput.text)
                        searchInput.showColoredText = true
                    }
                }

                onTextEdited: {
                    searchInput.historyHint = controller.getSearchHistoryHint(searchInput.text)
                    searchInput.showColoredText = false
                    searchInput.updateFilteredHistory()
                    if (searchHistoryPopup.visible && searchInput.filteredHistoryModel.length === 0) {
                        searchHistoryPopup.close()
                    }
                }

                Keys.onPressed: (event) => {
                    if (event.key === Qt.Key_Space && (event.modifiers & Qt.ControlModifier)) {
                        searchInput.updateFilteredHistory()
                        if (searchInput.filteredHistoryModel.length > 0) {
                            searchHistoryPopup.open()
                        }
                        event.accepted = true
                        return
                    }

                    if (searchHistoryPopup.visible && (event.key === Qt.Key_Down || event.key === Qt.Key_Up)) {
                        if (historyListView.count > 0) {
                            var step = event.key === Qt.Key_Down ? 1 : -1
                            historyListView.currentIndex = (historyListView.currentIndex + step + historyListView.count) % historyListView.count
                            historyListView.positionViewAtIndex(historyListView.currentIndex, ListView.Contain)
                        }
                        event.accepted = true
                        return
                    }

                    if (event.key === Qt.Key_Escape && searchHistoryPopup.visible) {
                        searchHistoryPopup.close()
                        event.accepted = true
                        return
                    }

                    if (event.key === Qt.Key_Tab) {
                        if (searchInput.historyHint !== "" && searchInput.historyHint !== searchInput.text) {
                            searchInput.text = searchInput.historyHint
                            searchInput.cursorPosition = searchInput.text.length
                            event.accepted = true
                        }
                        return
                    }

                    if (event.key === Qt.Key_Return) {
                        if (searchHistoryPopup.visible && historyListView.currentIndex >= 0) {
                            var picked = searchInput.filteredHistoryModel[historyListView.currentIndex]
                            searchInput.text = picked
                            searchInput.cursorPosition = searchInput.text.length
                            searchInput.historyHint = controller.getSearchHistoryHint(searchInput.text)
                            searchInput.showColoredText = false
                            searchHistoryPopup.close()
                            event.accepted = true
                            return
                        }

                        console.log("Enter pressed: " + searchInput.text)
                        controller.executeSearch(searchInput.text)
                        searchInput.historyHint = controller.getSearchHistoryHint(searchInput.text)
                        searchInput.coloredDisplayText = searchInput.colorizeQuery(searchInput.text)
                        searchInput.showColoredText = searchInput.text.length > 0
                        event.accepted = true
                    }
                }

                Connections {
                    target: searchLog
                    function onSearchHistoryChanged() {
                        searchInput.historyHint = controller.getSearchHistoryHint(searchInput.text)
                        searchInput.updateFilteredHistory()
                    }
                }

                // Shown on Ctrl+Space to pick a previous search query from searchLog.searchHistory.
                Popup {
                    id: searchHistoryPopup
                    y: searchInput.height + 4
                    width: searchInput.width
                    height: Math.min(200, historyListView.contentHeight + topPadding + bottomPadding)
                    padding: 4
                    closePolicy: Popup.CloseOnPressOutside | Popup.CloseOnEscape

                    background: Rectangle {
                        color: ({
                            [Styler.ThemeMode.DARK]: "#3a3a3a",
                            [Styler.ThemeMode.LIGHT]: "#ffffff"
                        })[Styler.themeMode]
                        border.width: 1
                        border.color: ({
                            [Styler.ThemeMode.DARK]: "#595959",
                            [Styler.ThemeMode.LIGHT]: "#6a6087"
                        })[Styler.themeMode]
                        radius: 4
                    }

                    contentItem: ListView {
                        id: historyListView
                        implicitHeight: contentHeight
                        clip: true
                        model: searchInput.filteredHistoryModel
                        delegate: ItemDelegate {
                            width: historyListView.width
                            text: modelData
                            highlighted: ListView.isCurrentItem
                            onClicked: {
                                historyListView.currentIndex = index
                                searchInput.text = modelData
                                searchInput.cursorPosition = searchInput.text.length
                                searchInput.historyHint = controller.getSearchHistoryHint(searchInput.text)
                                searchInput.showColoredText = false
                                searchHistoryPopup.close()
                                searchInput.forceActiveFocus()
                            }
                        }
                    }
                }

                Text {
                    id: coloredQueryText
                    anchors.fill: searchInput
                    leftPadding: searchInput.leftPadding
                    verticalAlignment: Text.AlignVCenter
                    textFormat: Text.RichText
                    text: searchInput.coloredDisplayText
                    color: ({
                        [Styler.ThemeMode.DARK]: "#ffffff",
                        [Styler.ThemeMode.LIGHT]: "#3A1D5E"
                    })[Styler.themeMode]
                    font.pixelSize: searchInput.font.pixelSize
                    font.family: searchInput.font.family
                    clip: true
                    z: 0.4
                    visible: searchInput.showColoredText && searchInput.text.length > 0
                }

                Text {
                    id: searchHint
                    anchors.fill: searchInput
                    verticalAlignment: Text.AlignVCenter
                    horizontalAlignment: Text.AlignHCenter
                    text: "Search"
                    color: ({
                        [Styler.ThemeMode.DARK]: "#ffffff",
                        [Styler.ThemeMode.LIGHT]: "#1F0954"
                    })[Styler.themeMode]
                    font.pixelSize: 14
                    font.family: concertOne.font.family
                    z: 0
                    visible: searchInput.text.length === 0 && !searchInput.focus
                }

                Text {
                    id: searchHistoryHint
                    anchors.fill: searchInput
                    leftPadding: searchInput.leftPadding
                    verticalAlignment: Text.AlignVCenter
                    text: searchInput.historyHint
                    color: ({
                        [Styler.ThemeMode.DARK]: "#8f8f8f",
                        [Styler.ThemeMode.LIGHT]: "#9a96a8"
                    })[Styler.themeMode]
                    font.pixelSize: 14
                    font.family: muktaVaani.font.family
                    z: -0.5
                    visible: searchInput.focus && searchInput.text.length > 0 && searchInput.historyHint !== "" && searchInput.historyHint !== searchInput.text
                }

                Rectangle {
                    id: borderSearchInput
                    anchors.fill: parent
                    border.width: 1
                    border.color: ({
                        [Styler.ThemeMode.DARK]: "#595959",
                        [Styler.ThemeMode.LIGHT]: "#6a6087"
                    })[Styler.themeMode]
                    radius: 2
                    color: ({
                        [Styler.ThemeMode.DARK]: "#595959",
                        [Styler.ThemeMode.LIGHT]: "#eceaef"
                    })[Styler.themeMode]
                    z: -1
                }
            }

            Button {
                id: themeBtn
                anchors.right: parent.right
                anchors.rightMargin: 10
                width: 40
                height: 30
                hoverEnabled: true
                icon.source: ({
                    [Styler.ThemeMode.DARK]: "./../assets/images/light_theme.svg",
                    [Styler.ThemeMode.LIGHT]: "./../assets/images/dark_theme.svg"
                })[Styler.themeMode]

                icon.color: ({
                    [Styler.ThemeMode.DARK]: hovered ? "#FF9408" : "#ffffff",
                    [Styler.ThemeMode.LIGHT]: hovered ? "#D46A7E" : "#1F0954"
                })[Styler.themeMode]

                background: Rectangle {
                    color: "transparent"
                }

                onClicked: {
                    Styler.themeMode = Styler.themeMode === Styler.ThemeMode.LIGHT ? Styler.ThemeMode.DARK : Styler.ThemeMode.LIGHT
                }
            }

            Button {
                id: showLessColumnBtn
                anchors.right: themeBtn.left
                anchors.rightMargin: 10
                width: 40
                height: 30
                hoverEnabled: true
                icon.source: "./../assets/images/columns.svg"
                property bool isOn: Styler.showLessColumns

                icon.color: ({
                    [Styler.ThemeMode.DARK]: hovered ? "#FF9408" : isOn ? "#3ddbd9" : "#ffffff",
                    [Styler.ThemeMode.LIGHT]: hovered ? "#D46A7E" : isOn ? "#D00F32" : "#1F0954"
                })[Styler.themeMode]

                background: Rectangle {
                    color: "transparent"
                }

                onClicked: {
                    Styler.showLessColumns = !Styler.showLessColumns
                }
            }

            Button {
                id: scrcpyBtn
                anchors.right: showLessColumnBtn.left
                anchors.rightMargin: 10
                width: 40
                height: 30
                hoverEnabled: true
                enabled: controller.hasAdbDevices
                property bool isOn: controller.scrcpyRunning
                icon.source: "./../assets/images/android_scrcpy.svg"

                icon.color: !enabled ? "#8c888888" : ({
                    [Styler.ThemeMode.DARK]: hovered ? "#FF9408" : isOn ? "#3ddbd9" : "#ffffff",
                    [Styler.ThemeMode.LIGHT]: hovered ? "#D46A7E" : isOn ? "#D00F32" : "#1F0954"
                })[Styler.themeMode]

                background: Rectangle {
                    color: "transparent"
                }

                onClicked: {
                    controller.openScrcpy()
                }
            }

            Rectangle {
                id: menuBarBg
                anchors.fill: parent
                color: ({
                    [Styler.ThemeMode.DARK]: "#303030",
                    [Styler.ThemeMode.LIGHT]: "#AFC0D8"
                })[Styler.themeMode]
                z: -1
            }
        }

        Shortcut {
            sequence: StandardKey.Find
            onActivated: {
                searchInput.forceActiveFocus()
                searchInput.selectAll()
            }
        }

        Shortcut {
            sequence: "Ctrl+B"
            onActivated: {
                Styler.showLessColumns = !Styler.showLessColumns
            }
        }
        
        Shortcut {
            sequence: "Ctrl+T"
            onActivated: {
                let filters = filterLog.displayedFilters
                if (filters.length > 0) {
                    filterDetailPanel.openPanel(FilterDetailPanel.Type.Edit, filters.length - 1)
                } else {
                    filterDetailPanel.openPanel(FilterDetailPanel.Type.New)

                }
            }
        }

        Shortcut {
            sequence: "Ctrl+Shift+T"
            onActivated: {
                filterDetailPanel.openPanel(FilterDetailPanel.Type.New)
            }
        }

        Shortcut {
            sequence: "Ctrl+M"
            onActivated: {
                if (controller.showLessColumns) {
                    controller.showLessColumns = false
                }
                leftView.toggleBookmarkTab()
            }
        }

        Shortcut {
            sequence: "Ctrl+P"
            onActivated: {
                helper.autoScrollDown = !helper.autoScrollDown
            }
        }

        SplitView {
            id: verSplit
            orientation: Qt.Vertical
            anchors.left: root.left
            anchors.right: root.right
            anchors.bottom: root.bottom
            anchors.top: menuBar.bottom

            handle: Rectangle {
                implicitHeight: 5
                color: SplitHandle.pressed  ? "#0c051e"
                                            : (SplitHandle.hovered ? Qt.lighter("#bab5c7", 1.1) : Qt.darker("#bab5c7", 1.1))
            }

            Item {
                id: topView
                SplitView.preferredHeight: verSplit.height * 0.65
                SplitView.fillWidth: true
                SplitView {
                    id: horSplit
                    orientation : Qt.Horizontal
                    height      : topView.height
                    width       : topView.width

                    handle: Rectangle {
                        implicitWidth: 5
                        color: SplitHandle.pressed ? "#0c051e"
                                                    : (SplitHandle.hovered ? Qt.lighter("#bab5c7", 1.1) : Qt.darker("#bab5c7", 1.1))
                    }

                    LeftToolPanel {
                        id: leftView
                        SplitView.preferredWidth: controller.showLessColumns ? 0 : horSplit.width * 0.15
                        SplitView.minimumWidth: 0
                        SplitView.fillHeight: true
                        visible: !controller.showLessColumns
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
                            filterProxyModel.filterCriteria: filterLog.filterCriteria
                        }

                        SelectionRectangle {
                            id: selectionRectangle
                        }

                        Shortcut {
                            sequences: [ StandardKey.Copy ]
                            onActivated: {
                                if (detailTextArea.activeFocus) {
                                    detailTextArea.copy()
                                    return
                                }

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
                    applyFilterColors: false
                    highlight: true
                    filterProxyModel.sourceModel: logviewTable.filterProxyModel
                    filterProxyModel.filterKeyColumn: -1
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
                    color: ({
                        [Styler.ThemeMode.DARK]: "#303030",
                        [Styler.ThemeMode.LIGHT]: "#31668A"
                    })[Styler.themeMode]
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
                        color: ({
                            [Styler.ThemeMode.DARK]: "#303030",
                            [Styler.ThemeMode.LIGHT]: "#31668A"
                        })[Styler.themeMode]
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
            visible: controller.showLoadingScreen || controller.isSaving
            anchors.fill: parent
        }

        FilterDetailPanel {
            id: filterDetailPanel
            anchors.centerIn: parent
            width: parent.width * 0.3
            height: parent.height * 0.3
            dim: true
            closePolicy: Popup.CloseOnPressOutside | Popup.CloseOnEscape

            function openPanel(_type, index) {
                console.log("openPanel: " + _type + " " + index)
                type = _type
                if (_type === FilterDetailPanel.Type.Edit) filterProfile = filterLog.displayedFilters[index]
                filterDetailPanel.open()
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

        DropArea {
            id: fileDropArea
            anchors.fill: parent
            keys: ["text/uri-list"]

            onDropped: (drop) => {
                if (drop.hasUrls && drop.urls.length > 0) {
                    controller.openFileByPath(drop.urls[0].toString())
                }
            }

            Rectangle {
                id: dropOverlay
                anchors.fill: parent
                color: "#80000000"
                visible: fileDropArea.containsDrag
                z: 999

                Text {
                    anchors.centerIn: parent
                    text: "Drop file to open"
                    color: "#ffffff"
                    font.pixelSize: 24
                    font.family: muktaVaani.font.family
                }
            }
        }

    }
}
