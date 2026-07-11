pragma Singleton
import QtQuick 2.15

Item {
    id: root
    enum ThemeMode {
        DARK,
        LIGHT
    }
    property int themeMode: controller.theme == "dark" ? Styler.ThemeMode.DARK : Styler.ThemeMode.LIGHT

    onThemeModeChanged: {
        controller.theme = (themeMode === Styler.ThemeMode.DARK) ? "dark" : "light";
    }

    property bool showLessColumns: controller.showLessColumns

    onShowLessColumnsChanged: {
        controller.showLessColumns = showLessColumns;
    }

}