import QtQuick

Item {
    id: root
    signal start()
    signal stop()

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
        id: loadingText
        source: "./../assets/images/loading_text_ani.gif"
        asynchronous: true
        anchors.top: loadingImage.bottom
        anchors.horizontalCenter: loadingImage.horizontalCenter
        anchors.topMargin: -100
        width: 160
        height: 160
        z: 1001
    }

    AnimatedImage {
        id: loadingImage
        source: "./../assets/images/loading_icon.gif"
        asynchronous: true
        anchors.centerIn: root
        width: 120
        height: 120
        z: 1001
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
