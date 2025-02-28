import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Universal 2.12
import QtQuick.Dialogs
import QtQuick.Effects
Popup {
    id: root
    parent: Overlay.overlay
    
    property int selectedDeviceIndex: -1
    property int remoteDeviceId: -1
    property string remoteDeviceName: ""
    property string remoteDeviceHost: ""
    property string remoteDeviceUser: ""
    property int remoteDevicePort: 22
    property string remoteLogPath: ""

    
    property bool isUseSSHGateway : false
    property string sshGatewayHost: ""
    property int    sshGatewayPort: 22
    property string sshGatewayUser: ""
    
    property bool remoteDeviceConnected: false
    

    enum Type {
        New,
        Edit
    }

    property int type: FilterDetailPanel.Type.New
    property var filterProfile

    background: Rectangle {
        id: dialogBg
        anchors.fill: parent
        color: "#363636"
        opacity: 1
        radius: 4
        border.width: 1
        border.color: "#5f6160"
    }

    MultiEffect {
        source: dialogBg
        anchors.fill: dialogBg
        blurEnabled: true
        blur: 0.2
    }

    Image {
        id: icon
        width: 22
        height: 22
        anchors.top: parent.top
        anchors.topMargin: 10
        anchors.left: parent.left
        anchors.leftMargin: 10
        sourceSize.width: 22
        sourceSize.height: 22
        source: "./../assets/images/filter_panel_icon.svg"
        
    }

    Text {
        id: dialogTitle
        width: parent.width
        height: 30
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "New"
            } else {
                return "Edit"
            }
        }
        anchors.top: parent.top
        anchors.topMargin: 5
        anchors.left: icon.right
        anchors.leftMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 16
    }

    TextField {
        id: remoteDeviceNameField
        width: parent.width - 10
        height: 32
        anchors.top: dialogTitle.bottom
        anchors.topMargin: 10
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return root.remoteDeviceName
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter device name"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: "#434342"
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
    }

    TextField {
        id: remoteDeviceHostField
        width: parent.width - 10
        height: 32
        anchors.top: remoteDeviceNameField.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        validator: RegularExpressionValidator { regularExpression: /^(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])(\.(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])){3}$/ }
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return root.remoteDeviceHost
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter device host IP"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: "#434342"
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        }
            
    }

    TextField {
        id: remoteDevicePortField
        width: parent.width - 10
        height: 32
        anchors.top: remoteDeviceHostField.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        validator: IntValidator {bottom: 1; top: 100000}
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return root.remoteDevicePort
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter port number"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: "#434342"
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        } 
    }

    TextField {
        id: remoteDeviceUserField
        width: parent.width - 10
        height: 32
        anchors.top: remoteDevicePortField.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return root.remoteDeviceUser
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter user name"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: "#434342"
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        } 
    }

    TextField {
        id: remoteLogPathField
        width: parent.width - 10
        height: 32
        anchors.top: remoteDeviceUserField.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: "#ECEDF5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "/var/log/messages"
            } else {
                return root.remoteLogPath
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "/var/log/messages"
            } else {
                return "/var/log/messages"
            }
        }

        background: Rectangle {
            color: "#434342"
            radius: 4
            border.width: 1
            border.color: "#7e7e7e"
        } 
    }


     CheckBox {
        id: useSSHGatewayCB
        checked: root.isUseSSHGateway
        text: qsTr("Use SSH Gateway")
        anchors.top: remoteLogPathField.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
    }

    TextField {
        id: sshGatewayHostField
        width: parent.width - 10
        height: 32
        anchors.top: useSSHGatewayCB.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: enabled ? "#ECEDF5" : "#8eecedf5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        enabled: useSSHGatewayCB.checked
        // validator: RegularExpressionValidator { regularExpression: /^(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])(\.(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])){3}$/ }
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return root.sshGatewayHost
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter SSH Gateway host IP"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: enabled ? "#434342" : "#7a616060"
            radius: 4
            border.width: 1
            border.color: enabled ? "#7e7e7e" : "#424242"
        }
    }

    TextField {
        id: sshGatewayUserField
        width: parent.width - 10
        height: 32
        anchors.top: sshGatewayHostField.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: enabled ? "#ECEDF5" : "#8eecedf5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        enabled: useSSHGatewayCB.checked
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return root.sshGatewayUser
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter SSH Gateway user name"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: enabled ? "#434342" : "#7a616060"
            radius: 4
            border.width: 1
            border.color: enabled ? "#7e7e7e" : "#424242"
        }
    }

    TextField {
        id: sshGatewayPortField
        width: parent.width - 10
        height: 32
        anchors.top: sshGatewayUserField.bottom
        anchors.topMargin: 20
        anchors.left: parent.left
        anchors.leftMargin: 10
        anchors.right: parent.right
        anchors.rightMargin: 10
        color: enabled ? "#ECEDF5" : "#75ecedf5"
        verticalAlignment: Text.AlignVCenter
        font.family: muktaVaani.font.family
        font.pixelSize: 13
        validator: IntValidator {bottom: 1; top: 100000}
        enabled: useSSHGatewayCB.checked
        text: {
            if (root.type === FilterDetailPanel.Type.New) {
                return ""
            } else {
                return root.sshGatewayPort
            }
        }

        placeholderText: {
            if (root.type === FilterDetailPanel.Type.New) {
                return "Enter SSH Gateway port"
            } else {
                return ""
            }
        }

        background: Rectangle {
            color: enabled ? "#434342" : "#7a616060"
            radius: 4
            border.width: 1
            border.color: enabled ? "#7e7e7e" : "#424242"
        }
    }

    Button {
        id: okBtn
        width: 60
        height: 30
        anchors.right: parent.right
        anchors.rightMargin: 10
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 5
        text: "OK"
        font.family: muktaVaani.font.family

        background: Rectangle {
            color: "#444645"
            radius: 4
            border.width: 1
            border.color: "#7f8180"
        }

        onClicked: {
            var deviceID = root.remoteDeviceId
            if (root.type === RemoteDeviceDetailPanel.Type.New) {
                deviceID = Math.floor(Math.random() * 1000000)
            }

            var remoteDevice = {
                id: deviceID,
                name: remoteDeviceNameField.text,
                host: remoteDeviceHostField.text,
                port: parseInt(remoteDevicePortField.text),
                username: remoteDeviceUserField.text,
                remoteLogPath: remoteLogPathField.text,
                isUseSSHGateway: useSSHGatewayCB.checked,
                SSHGateway_IP: sshGatewayHostField.text,
                SSHGateway_Port: parseInt(sshGatewayPortField.text),
                SSHGateway_User: sshGatewayUserField.text
            }

            if (root.type === FilterDetailPanel.Type.New) {
                controller.addRemoteDevice(remoteDevice)
            } else {
                controller.changeRemoteDeviceInfo(remoteDevice, selectedDeviceIndex)
            }
            root.close()
        }
    }
}