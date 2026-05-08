import AppKit

enum OverlayTheme {
    static let defaultFontFamily = "SF Pro"

    static let preferredFontFamilies = [
        "SF Pro",
        "Inter",
        "Helvetica Neue",
        "Arial",
        "Avenir",
        "Segoe UI",
        "Trebuchet MS",
        "Verdana",
        "Menlo",
        "Monaco",
        "Georgia",
        "Times New Roman"
    ]

    enum Typography {
        static let overlayTitleSize: CGFloat = 11
        static let overlayStatusSize: CGFloat = 9
        static let overlaySourceSize: CGFloat = 8.5
        static let tableTextSize: CGFloat = 8.8
        static let tableHeaderSize: CGFloat = 9.2
        static let miniLabelSize: CGFloat = 7
    }

    enum Colors {
        static let windowBorder = NSColor(calibratedWhite: 1, alpha: 0.28)
        static let windowBackground = NSColor(red255: 14, green: 18, blue: 21, alpha: 0.88)
        static let settingsBackground = NSColor(red255: 16, green: 20, blue: 23, alpha: 0.92)
        static let titleBarBackground = NSColor(red255: 24, green: 30, blue: 34, alpha: 0.96)
        static let panelBackground = NSColor(red255: 24, green: 30, blue: 34)
        static let pageBackground = NSColor(red255: 20, green: 25, blue: 29)
        static let buttonBackground = NSColor(red255: 40, green: 48, blue: 54)
        static let tabBackground = NSColor(red255: 24, green: 30, blue: 34)
        static let tabSelectedBackground = NSColor(red255: 38, green: 48, blue: 56)
        static let tabBorder = NSColor(red255: 64, green: 82, blue: 92)

        static let textPrimary = NSColor.white
        static let textSecondary = NSColor(red255: 218, green: 226, blue: 230)
        static let textMuted = NSColor(red255: 128, green: 145, blue: 153)
        static let textSubtle = NSColor(calibratedWhite: 0.70, alpha: 1)
        static let textControl = NSColor(red255: 220, green: 225, blue: 230)

        static let neutralBackground = NSColor(red255: 26, green: 26, blue: 26, alpha: 0.88)
        static let neutralIndicator = NSColor(red255: 140, green: 140, blue: 140)
        static let warningBackground = NSColor(red255: 64, green: 46, blue: 14, alpha: 0.88)
        static let warningStrongBackground = NSColor(red255: 54, green: 30, blue: 14, alpha: 0.88)
        static let warningText = NSColor(red255: 246, green: 184, blue: 88)
        static let warningIndicator = NSColor(red255: 244, green: 180, blue: 64)
        static let successBackground = NSColor(red255: 14, green: 38, blue: 28, alpha: 0.88)
        static let successStrongBackground = NSColor(red255: 18, green: 46, blue: 34, alpha: 0.88)
        static let successText = NSColor(red255: 112, green: 224, blue: 146)
        static let successIndicator = NSColor(red255: 80, green: 214, blue: 124)
        static let infoBackground = NSColor(red255: 18, green: 30, blue: 42, alpha: 0.88)
        static let infoText = NSColor(red255: 140, green: 190, blue: 245)
        static let errorBackground = NSColor(red255: 70, green: 18, blue: 24, alpha: 0.88)
        static let errorGraphBackground = NSColor(red255: 42, green: 18, blue: 22, alpha: 0.88)
        static let errorText = NSColor(red255: 236, green: 112, blue: 99)
        static let errorIndicator = NSColor(red255: 245, green: 88, blue: 88)
    }

    enum Layout {
        static let outerPadding: CGFloat = 14
        static let overlayHeaderHeight: CGFloat = 34
        static let overlayTableRowHeight: CGFloat = 28
        static let overlayCompactRowHeight: CGFloat = 24
        static let overlayBorderWidth: CGFloat = 1
        static let settingsTitleBarHeight: CGFloat = 42
        static let settingsTabTop: CGFloat = 54
        static let settingsTabInset: CGFloat = 12
        static let labelHeight: CGFloat = 24
        static let inputHeight: CGFloat = 28
    }

    static func font(
        family: String,
        size: CGFloat,
        weight: NSFont.Weight = .regular
    ) -> NSFont {
        let requested = family.trimmingCharacters(in: .whitespacesAndNewlines)
        if requested.isEmpty
            || requested.caseInsensitiveCompare(defaultFontFamily) == .orderedSame
            || requested.caseInsensitiveCompare("System") == .orderedSame {
            return NSFont.systemFont(ofSize: size, weight: weight)
        }

        return NSFont(name: requested, size: size)
            ?? NSFont.systemFont(ofSize: size, weight: weight)
    }

    static func monospacedFont(
        size: CGFloat,
        weight: NSFont.Weight = .regular
    ) -> NSFont {
        NSFont.monospacedSystemFont(ofSize: size, weight: weight)
    }
}
