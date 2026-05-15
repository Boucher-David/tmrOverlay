import AppKit

struct DesignV2OverlayChromeSpec {
    var title: String
    var status: String
    var footer: String
    var evidenceColor: NSColor
    var showStatus: Bool = true
    var showFooter: Bool = true
    var headerHeight: CGFloat = 38
    var footerHeight: CGFloat = 32
    var bodyGap: CGFloat = 12
    var padding: CGFloat = 16
    var titleWidth: CGFloat = 210
    var statusLeading: CGFloat = 232
    var statusTrailing: CGFloat = 16
    var footerBottomInset: CGFloat = 24
}

struct DesignV2OverlayChromeLayout {
    var outer: NSRect
    var header: NSRect
    var body: NSRect
}

struct DesignV2OverlayRenderer {
    var theme: DesignV2Theme
    var fontFamily: String
    var scale: CGFloat = 1

    func drawChrome(in bounds: NSRect, spec: DesignV2OverlayChromeSpec) -> DesignV2OverlayChromeLayout {
        let outer = bounds.insetBy(dx: 0.5, dy: 0.5)
        rounded(outer, radius: theme.layout.cornerRadius * scale, fill: theme.colors.surface, stroke: theme.colors.border)

        let header = NSRect(
            x: outer.minX + 1,
            y: outer.minY + 1,
            width: outer.width - 2,
            height: scaled(spec.headerHeight)
        )
        rounded(header, radius: max(2, (theme.layout.cornerRadius - 1) * scale), fill: theme.colors.titleBar, stroke: nil)
        rounded(
            NSRect(x: outer.minX, y: outer.minY + scaled(7), width: max(1, scaled(2)), height: outer.height - scaled(14)),
            radius: 2,
            fill: spec.evidenceColor,
            stroke: nil
        )
        rounded(
            NSRect(x: outer.minX, y: header.maxY - 1, width: outer.width, height: max(1, scaled(2))),
            radius: 1,
            fill: theme.colors.accentSecondary,
            stroke: nil
        )

        text(
            spec.title,
            in: NSRect(
                x: outer.minX + scaled(14),
                y: header.midY - scaled(9),
                width: scaled(spec.titleWidth),
                height: scaled(18)
            ),
            font: font(size: 14 * scale, weight: .bold),
            color: theme.colors.textPrimary
        )
        if spec.showStatus {
            text(
                spec.status,
                in: NSRect(
                    x: outer.minX + scaled(spec.statusLeading),
                    y: header.midY - scaled(8),
                    width: outer.width - scaled(spec.statusLeading + spec.statusTrailing),
                    height: scaled(16)
                ),
                font: font(size: 11 * scale, weight: .semibold),
                color: spec.evidenceColor,
                alignment: .right
            )
        }

        if spec.showFooter {
            text(
                spec.footer,
                in: NSRect(
                    x: outer.minX + scaled(14),
                    y: outer.maxY - scaled(spec.footerBottomInset),
                    width: outer.width - scaled(28),
                    height: scaled(16)
                ),
                font: font(size: 10 * scale),
                color: theme.colors.textMuted
            )
        }

        let body = NSRect(
            x: outer.minX + scaled(spec.padding),
            y: header.maxY + scaled(spec.bodyGap),
            width: outer.width - scaled(spec.padding) * 2,
            height: max(1, outer.height - scaled(spec.headerHeight + spec.footerHeight + spec.bodyGap) - 1)
        )
        return DesignV2OverlayChromeLayout(outer: outer, header: header, body: body)
    }

    func font(size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        DesignV2Drawing.font(family: fontFamily, size: size, weight: weight)
    }

    func rounded(
        _ rect: NSRect,
        radius: CGFloat,
        fill: NSColor?,
        stroke: NSColor?,
        lineWidth: CGFloat = 1
    ) {
        DesignV2Drawing.rounded(rect, radius: radius, fill: fill, stroke: stroke, lineWidth: lineWidth)
    }

    func text(
        _ text: String,
        in rect: NSRect,
        font: NSFont,
        color: NSColor,
        alignment: NSTextAlignment = .left
    ) {
        DesignV2Drawing.text(text, in: rect, font: font, color: color, alignment: alignment)
    }

    private func scaled(_ value: CGFloat) -> CGFloat {
        value * scale
    }
}

enum DesignV2OverlayChromeVisibility {
    static func headerStatusEnabled(settings: OverlaySettings, sessionKey: String?) -> Bool {
        chromeOption(
            settings: settings,
            sessionKey: sessionKey,
            testKey: "chrome.header.status.test",
            practiceKey: "chrome.header.status.practice",
            qualifyingKey: "chrome.header.status.qualifying",
            raceKey: "chrome.header.status.race"
        )
    }

    static func headerTimeRemainingEnabled(settings: OverlaySettings, sessionKey: String?) -> Bool {
        chromeOption(
            settings: settings,
            sessionKey: sessionKey,
            testKey: "chrome.header.time-remaining.test",
            practiceKey: "chrome.header.time-remaining.practice",
            qualifyingKey: "chrome.header.time-remaining.qualifying",
            raceKey: "chrome.header.time-remaining.race"
        )
    }

    static func footerSourceEnabled(settings: OverlaySettings, sessionKey: String?) -> Bool {
        guard settings.id.lowercased() != "session-weather" else {
            return false
        }

        return chromeOption(
            settings: settings,
            sessionKey: sessionKey,
            testKey: "chrome.footer.source.test",
            practiceKey: "chrome.footer.source.practice",
            qualifyingKey: "chrome.footer.source.qualifying",
            raceKey: "chrome.footer.source.race"
        )
    }

    private static func chromeOption(
        settings: OverlaySettings,
        sessionKey: String?,
        testKey: String,
        practiceKey: String,
        qualifyingKey: String,
        raceKey: String
    ) -> Bool {
        guard let sessionKind = sessionKind(sessionKey) else {
            return true
        }

        switch sessionKind {
        case "test":
            return boolOption(settings.options[practiceKey], defaultValue: true)
        case "practice":
            return boolOption(settings.options[practiceKey], defaultValue: true)
        case "qualifying":
            return boolOption(settings.options[qualifyingKey], defaultValue: true)
        case "race":
            return boolOption(settings.options[raceKey], defaultValue: true)
        default:
            return true
        }
    }

    private static func sessionKind(_ sessionKey: String?) -> String? {
        guard let sessionKey else {
            return nil
        }

        let normalized = sessionKey.lowercased()
        if normalized.contains("test") || normalized.contains("practice") {
            return "practice"
        }
        if normalized.contains("qual") {
            return "qualifying"
        }
        if normalized.contains("race") {
            return "race"
        }
        return nil
    }

    private static func boolOption(_ value: String?, defaultValue: Bool) -> Bool {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() else {
            return defaultValue
        }

        switch value {
        case "true", "1", "yes", "on":
            return true
        case "false", "0", "no", "off":
            return false
        default:
            return defaultValue
        }
    }
}
