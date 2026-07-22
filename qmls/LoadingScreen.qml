import QtQuick
import QtQuick.Controls 2.15

Item {
    id: root
    signal start()
    signal stop()

    // Bind to controller properties
    property real progress: typeof controller !== "undefined" ? controller.loadProgress : 0.0
    property string fileName: typeof controller !== "undefined" ? controller.loadingFileName : ""
    property bool isLoading: typeof controller !== "undefined" ? controller.isLoading : false
    property bool isSaving: typeof controller !== "undefined" ? controller.isSaving : false
    property real saveProgressVal: typeof controller !== "undefined" ? controller.saveProgress : 0.0

    onStart: {
        root.visible = true
    }

    onStop: {
        delayTimer.restart()
    }
    Rectangle {
        id: loadingBg
        anchors.fill: parent
        color: "#46484F"
        opacity: 0.8
        z: 1000
    }

    AnimatedImage {
        id: loadingImage
        source: "./../assets/images/loading_icon.gif"
        asynchronous: true
        anchors.centerIn: root
        anchors.verticalCenterOffset: -60
        width: 120
        height: 120
        z: 1001
        visible: !root.isLoading && !root.isSaving
    }

    AnimatedImage {
        id: loadingText
        source: "./../assets/images/loading_text_ani.gif"
        asynchronous: true
        anchors.top: loadingImage.bottom
        anchors.horizontalCenter: loadingImage.horizontalCenter
        anchors.topMargin: -100
        width: 160
        height: 160
        z: 1001
        visible: !root.isLoading && !root.isSaving
    }

    // Progress panel shown during file loading or saving
    Column {
        id: progressPanel
        anchors.centerIn: parent
        spacing: 12
        z: 1001
        visible: root.isLoading || root.isSaving

        Text {
            id: progressTitle
            anchors.horizontalCenter: parent.horizontalCenter
            text: root.isSaving ? "Saving..." : ("Loading: " + root.fileName)
            color: "#ECEDF5"
            font.pixelSize: 16
            font.bold: true
        }

        ProgressBar {
            id: progressBar
            width: 320
            height: 18
            from: 0.0
            to: 1.0
            value: root.isSaving ? root.saveProgressVal : root.progress
            anchors.horizontalCenter: parent.horizontalCenter

            background: Rectangle {
                implicitWidth: progressBar.width
                implicitHeight: progressBar.height
                color: "#2a2a2a"
                radius: 4
            }

            contentItem: Item {
                implicitWidth: progressBar.width
                implicitHeight: progressBar.height
                Rectangle {
                    width: progressBar.visualPosition * parent.width
                    height: parent.height
                    radius: 4
                    color: "#4CAF50"
                }
            }
        }

        Text {
            id: progressPercent
            anchors.horizontalCenter: parent.horizontalCenter
            text: Math.round((root.isSaving ? root.saveProgressVal : root.progress) * 100) + "%"
            color: "#CCCCCC"
            font.pixelSize: 14
        }

        Button {
            id: cancelBtn
            anchors.horizontalCenter: parent.horizontalCenter
            width: 100
            height: 30
            visible: root.isLoading
            text: "Cancel"
            onClicked: {
                if (typeof controller !== "undefined") {
                    controller.cancelLoad()
                }
            }

            background: Rectangle {
                color: cancelBtn.hovered ? "#D32F2F" : "#555555"
                radius: 4
            }

            contentItem: Text {
                text: cancelBtn.text
                color: "#ECEDF5"
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
                font.pixelSize: 12
            }
        }
    }

    Timer {
        id: delayTimer
        interval: 2400
        repeat: false
        onTriggered: {
            root.visible = false
        }
    }

}
