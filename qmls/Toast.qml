import QtQuick 2.15
import QtQuick.Effects
Item {
    id: root
    width: 120
    height: 120
    visible: toastView.model.count > 0

    property var _I: 0
    property var _W: 1
    property var _E: 2
    
    property var _INFO      : "#56df3a"
    property var _WARNING   : "#f89616"
    property var _ERROR     : "#ee322c"

    
    property var messageQueue: []
    property var triggerLoop: messageQueue

    onTriggerLoopChanged: {
        if (triggerLoop.length > 0 && !looper.running) {
            looper.start()
        } else if (triggerLoop.length == 0){
            looper.stop()
        }
    }
    
 
    Connections {
        target: toastMgr
        function onShowMsg(type, msg) {
            var delay = root.messageQueue.length * 500
            var msgCxt = {
                ctx: msg,
                type: type,
                delay: delay
            }
            root.messageQueue.push(msgCxt)
            triggerLoopChanged()
        }
    }

    ListModel {
        id: toastModel
    }


    ListView {
        id: toastView
        width: root.width - root.width / 4
        height: root.height
        model: toastModel
        verticalLayoutDirection: ListView.BottomToTop
        layoutDirection: Qt.RightToLeft
        anchors.right: parent.right
        spacing: 5
        clip: true

        add: Transition {
            NumberAnimation {
                properties: "x"
                duration: 800
                from: root.width + toastView.width
                to: 0
                easing.type: Easing.InOutQuad
            }
        }

        remove: Transition {
            NumberAnimation {
                properties: "opacity"
                duration: 400
                from: 0.7
                to: 0
            }
        }
        delegate: Item {
            id: notication
            height: 30
            width: Math.max(msg.paintedWidth, toastView.width)
            Rectangle {
                id: itemBg
                width: parent.width
                height: parent.height
                color: type == _I ? _INFO : (type == _W ? _WARNING : _ERROR)
                radius: 6
                opacity: 0.5
            }

            Text {
                id: msg
                width: notication.width
                height: 30
                horizontalAlignment : Text.AlignHCenter
                verticalAlignment   : Text.AlignVCenter
                text: what
                color: "#ECEDF5"
                font.family: muktaVaani.font.family
                font.pixelSize: 12
            }
        }
    }

    Component {
        id: delayAdd
        Timer {
            id: timerAdd
            interval: delay
            property int delay: 0
            property var msg: null
            property var callback: null
            
            onTriggered: {
                callback(msg)
                timerAdd.destroy()
            }
        }
    }

    Component {
        id: dynamicTimer
        Timer {
            id: timer
            property int index: 0
            interval: 3000
            property var callback: null
            
            onTriggered: {
                callback(index)
                timer.destroy()
            }
        }
    }

    Timer {
        id: looper
        interval: 50
        repeat: true
        property var delayAdd: delayAdd
        property var dynamicTimer: dynamicTimer

        onTriggered: {
            if(root.messageQueue.length > 0) {
                var msg = root.messageQueue[0]
                root.messageQueue.shift()
                triggerLoopChanged()

                var msgContext = {
                    what: msg.ctx,
                    type: msg.type
                }

                var delayTimer      = delayAdd.createObject(root)
                delayTimer.msg      = msgContext
                delayTimer.delay    = msg.delay

                delayTimer.callback = function cb(msg) {
                    toastModel.append(msg)
                    var timer = dynamicTimer.createObject(root)
                    timer.index = 0
                    timer.callback = function cb(index) {
                        toastModel.remove(index)
                    }
                    timer.start()
                }
                delayTimer.start()
            }
        }
    }

    Component.onCompleted: {
        // test.start()
    }
}