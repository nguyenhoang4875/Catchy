import QtQuick
import QtQuick.Controls
// import QtQuick.Controls.Basic
import Styles
Button {
    id: root
    signal checkStateChanged(bool checked)
    icon.source: checked ? "./../assets/images/check_icon.png" : "./../assets/images/uncheck_icon.png"
    icon.color: ({
        [Styler.ThemeMode.DARK]: "transparent",
        [Styler.ThemeMode.LIGHT]: "#ffffff"
    })[Styler.themeMode]
    icon.width: 20
    icon.height: 20
    padding: 0
    opacity: enabled ? 1.0 : 0.45
    background: Rectangle {
        color: "transparent"
    }
    onClicked: {
        checked = !checked
        checkStateChanged(checked)
    }
}