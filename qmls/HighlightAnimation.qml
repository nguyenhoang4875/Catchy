import QtQuick
import Styles
Item {
    id: root
    function start() {
        console.log("HighlightAnimation.qml: start()")
        fadeInOutAnimation.start()
    }

    Rectangle {
        id: fadeRect
        anchors.fill: parent
        color: "#80ffd5"
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
}