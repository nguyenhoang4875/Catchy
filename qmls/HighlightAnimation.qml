import QtQuick
import Styles
Item {
    id: root
    property bool bolded: false

    function start() {
        console.log("HighlightAnimation.qml: start()")
        bolded = true
        boldTimer.restart()
    }

    Timer {
        id: boldTimer
        interval: 1500
        onTriggered: root.bolded = false
    }
}