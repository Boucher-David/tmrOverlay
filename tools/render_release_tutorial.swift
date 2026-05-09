import AppKit
import Foundation

private enum Theme {
    static let background = NSColor(red255: 12, green: 15, blue: 18)
    static let panel = NSColor(red255: 19, green: 24, blue: 28, alpha: 1)
    static let panelAlt = NSColor(red255: 24, green: 30, blue: 34, alpha: 1)
    static let panelRaised = NSColor(red255: 30, green: 38, blue: 44, alpha: 0.98)
    static let border = NSColor(white: 1.0, alpha: 0.22)
    static let borderStrong = NSColor(white: 1.0, alpha: 0.34)
    static let text = NSColor.white
    static let secondary = NSColor(red255: 218, green: 226, blue: 230)
    static let muted = NSColor(red255: 128, green: 145, blue: 153)
    static let subtle = NSColor(red255: 92, green: 110, blue: 120)
    static let accent = NSColor(red255: 69, green: 203, blue: 250)
    static let success = NSColor(red255: 112, green: 224, blue: 146)
    static let successBackground = NSColor(red255: 18, green: 46, blue: 34, alpha: 0.96)
    static let info = NSColor(red255: 140, green: 190, blue: 245)
    static let infoBackground = NSColor(red255: 18, green: 30, blue: 42, alpha: 0.96)
    static let warning = NSColor(red255: 246, green: 184, blue: 88)
    static let warningBackground = NSColor(red255: 64, green: 46, blue: 14, alpha: 0.96)
    static let error = NSColor(red255: 236, green: 112, blue: 99)
    static let errorBackground = NSColor(red255: 70, green: 18, blue: 24, alpha: 0.96)
    static let purple = NSColor(red255: 192, green: 132, blue: 252)

    static func font(_ size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        NSFont(name: "SF Pro", size: size) ?? NSFont.systemFont(ofSize: size, weight: weight)
    }
}

private extension NSColor {
    convenience init(red255 red: CGFloat, green: CGFloat, blue: CGFloat, alpha: CGFloat = 1.0) {
        self.init(calibratedRed: red / 255.0, green: green / 255.0, blue: blue / 255.0, alpha: alpha)
    }
}

private final class Canvas {
    let size: CGSize
    let context: CGContext

    init(size: CGSize, context: CGContext) {
        self.size = size
        self.context = context
    }

    func topRect(x: CGFloat, y: CGFloat, width: CGFloat, height: CGFloat) -> CGRect {
        CGRect(x: x, y: size.height - y - height, width: width, height: height)
    }

    func fill(_ rect: CGRect, _ color: NSColor, radius: CGFloat = 0) {
        color.setFill()
        path(rect, radius).fill()
    }

    func stroke(_ rect: CGRect, _ color: NSColor, width: CGFloat = 1, radius: CGFloat = 0) {
        color.setStroke()
        let p = path(rect.insetBy(dx: width / 2, dy: width / 2), radius)
        p.lineWidth = width
        p.stroke()
    }

    func line(from: CGPoint, to: CGPoint, color: NSColor, width: CGFloat = 1) {
        color.setStroke()
        let p = NSBezierPath()
        p.move(to: from)
        p.line(to: to)
        p.lineWidth = width
        p.stroke()
    }

    func text(
        _ value: String,
        _ rect: CGRect,
        size: CGFloat,
        weight: NSFont.Weight = .regular,
        color: NSColor = Theme.text,
        align: NSTextAlignment = .left
    ) {
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = align
        paragraph.lineBreakMode = .byTruncatingTail
        let attributes: [NSAttributedString.Key: Any] = [
            .font: Theme.font(size, weight: weight),
            .foregroundColor: color,
            .paragraphStyle: paragraph
        ]
        NSString(string: value).draw(in: rect, withAttributes: attributes)
    }

    func multiline(
        _ value: String,
        _ rect: CGRect,
        size: CGFloat,
        weight: NSFont.Weight = .regular,
        color: NSColor = Theme.secondary,
        align: NSTextAlignment = .left
    ) {
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = align
        paragraph.lineSpacing = 3
        paragraph.lineBreakMode = .byWordWrapping
        let attributes: [NSAttributedString.Key: Any] = [
            .font: Theme.font(size, weight: weight),
            .foregroundColor: color,
            .paragraphStyle: paragraph
        ]
        NSString(string: value).draw(
            with: rect,
            options: [.usesLineFragmentOrigin, .usesFontLeading],
            attributes: attributes
        )
    }

    func centered(
        _ value: String,
        _ rect: CGRect,
        size: CGFloat,
        weight: NSFont.Weight = .semibold,
        color: NSColor = Theme.text
    ) {
        let y = rect.minY + max(0, (rect.height - size * 1.35) / 2)
        text(
            value,
            CGRect(x: rect.minX, y: y, width: rect.width, height: size * 1.45),
            size: size,
            weight: weight,
            color: color,
            align: .center
        )
    }

    func pill(_ value: String, _ rect: CGRect, color: NSColor, textColor: NSColor) {
        fill(rect, color, radius: 6)
        centered(value, rect.insetBy(dx: 8, dy: 0), size: 12, weight: .bold, color: textColor)
    }

    func badge(_ value: String, _ rect: CGRect, color: NSColor) {
        fill(rect, color, radius: rect.height / 2)
        centered(value, rect, size: 16, weight: .bold, color: Theme.background)
    }

    func button(_ value: String, _ rect: CGRect, color: NSColor = Theme.panelRaised, textColor: NSColor = Theme.secondary) {
        fill(rect, color, radius: 5)
        stroke(rect, Theme.border, width: 1, radius: 5)
        centered(value, rect, size: 12, weight: .semibold, color: textColor)
    }

    func drawImage(_ image: NSImage, in rect: CGRect) {
        image.draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1.0)
    }

    private func path(_ rect: CGRect, _ radius: CGFloat) -> NSBezierPath {
        radius <= 0 ? NSBezierPath(rect: rect) : NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
    }
}

private struct StepCard {
    let number: String
    let title: String
    let subtitle: String
    let bullets: [String]
    let accent: NSColor
    let renderPreview: (Canvas, CGRect) -> Void
}

private let canvasSize = CGSize(width: 1600, height: 1000)

private func renderTutorial(_ c: Canvas) {
    c.fill(CGRect(origin: .zero, size: c.size), Theme.background)
    drawHeader(c)

    let cards = [
        StepCard(
            number: "1",
            title: "Download",
            subtitle: "Use the latest GitHub Release.",
            bullets: [
                "Open the latest vX.Y.Z release.",
                "Download the MSI installer.",
                "Use the zip only as a fallback."
            ],
            accent: Theme.accent,
            renderPreview: drawReleasePreview
        ),
        StepCard(
            number: "2",
            title: "Install",
            subtitle: "MSI is the normal path.",
            bullets: [
                "Run the MSI and use a shortcut.",
                "SmartScreen: More info > Run anyway.",
                "Portable fallback runs TMROverlay.exe."
            ],
            accent: Theme.success,
            renderPreview: drawInstallPreview
        ),
        StepCard(
            number: "3",
            title: "Upgrade",
            subtitle: "Install from the app.",
            bullets: [
                "Check from tray or Support tab.",
                "Install when an update is available.",
                "Restart to apply when prompted."
            ],
            accent: Theme.warning,
            renderPreview: drawUpgradePreview
        )
    ]

    let cardWidth: CGFloat = 486
    let topY: CGFloat = 142
    for (index, card) in cards.enumerated() {
        let rect = c.topRect(x: 42 + CGFloat(index) * (cardWidth + 28), y: topY, width: cardWidth, height: 238)
        drawStepCard(c, rect: rect, card: card)
    }

    drawAppPreview(c, rect: c.topRect(x: 42, y: 414, width: 726, height: 422))
    drawSupportPreview(c, rect: c.topRect(x: 804, y: 414, width: 754, height: 422))
    drawFooter(c)
}

private func drawHeader(_ c: Canvas) {
    let header = c.topRect(x: 0, y: 0, width: c.size.width, height: 116)
    c.fill(header, NSColor(red255: 14, green: 18, blue: 21, alpha: 0.98))
    c.line(
        from: CGPoint(x: 0, y: header.minY),
        to: CGPoint(x: c.size.width, y: header.minY),
        color: Theme.borderStrong,
        width: 1
    )

    if let logo = loadLogoImage() {
        c.drawImage(logo, in: c.topRect(x: 42, y: 24, width: 124, height: 70))
    } else {
        c.fill(c.topRect(x: 42, y: 24, width: 124, height: 70), Theme.panelRaised, radius: 8)
        c.centered("TMR", c.topRect(x: 42, y: 24, width: 124, height: 70), size: 28, weight: .bold)
    }

    c.text(
        "Tech Mates Racing Overlay",
        c.topRect(x: 190, y: 28, width: 760, height: 34),
        size: 29,
        weight: .bold
    )
    c.text(
        "Windows tester install and feedback quick start",
        c.topRect(x: 192, y: 68, width: 760, height: 22),
        size: 16,
        color: Theme.muted
    )
    c.pill(
        "Private tester handoff",
        c.topRect(x: 1254, y: 36, width: 250, height: 34),
        color: Theme.infoBackground,
        textColor: Theme.info
    )
}

private func drawStepCard(_ c: Canvas, rect: CGRect, card: StepCard) {
    c.fill(rect, Theme.panel, radius: 8)
    c.stroke(rect, Theme.border, width: 1, radius: 8)
    c.fill(CGRect(x: rect.minX, y: rect.maxY - 5, width: rect.width, height: 5), card.accent, radius: 8)
    c.badge(card.number, CGRect(x: rect.minX + 22, y: rect.maxY - 62, width: 42, height: 42), color: card.accent)
    c.text(card.title, CGRect(x: rect.minX + 82, y: rect.maxY - 51, width: rect.width - 108, height: 24), size: 18, weight: .bold)
    c.text(card.subtitle, CGRect(x: rect.minX + 82, y: rect.maxY - 78, width: rect.width - 108, height: 20), size: 12.5, color: Theme.muted)

    var bulletY = rect.maxY - 116
    for bullet in card.bullets {
        c.fill(CGRect(x: rect.minX + 28, y: bulletY + 6, width: 7, height: 7), card.accent, radius: 3.5)
        c.text(bullet, CGRect(x: rect.minX + 48, y: bulletY - 2, width: rect.width - 76, height: 22), size: 12.5, color: Theme.secondary)
        bulletY -= 38
    }
}

private func drawReleasePreview(_ c: Canvas, _ rect: CGRect) {
    c.fill(rect, Theme.panelAlt, radius: 7)
    c.stroke(rect, Theme.border, width: 1, radius: 7)
    c.fill(CGRect(x: rect.minX, y: rect.maxY - 28, width: rect.width, height: 28), Theme.panelRaised, radius: 7)
    c.text("GitHub Release", CGRect(x: rect.minX + 12, y: rect.maxY - 21, width: rect.width - 24, height: 16), size: 11, weight: .bold)
    let rows = [
        ("TMROverlay-vX.Y.Z-win-x64.msi", Theme.accent),
        ("TmrOverlay-vX.Y.Z-win-x64.zip", Theme.muted),
        ("release notes", Theme.muted)
    ]
    for (index, row) in rows.enumerated() {
        let y = rect.maxY - 58 - CGFloat(index) * 28
        c.fill(CGRect(x: rect.minX + 12, y: y, width: rect.width - 24, height: 20), NSColor(white: 1, alpha: 0.045), radius: 4)
        c.text(row.0, CGRect(x: rect.minX + 20, y: y + 4, width: rect.width - 36, height: 14), size: 9.5, color: row.1)
    }
}

private func drawInstallPreview(_ c: Canvas, _ rect: CGRect) {
    c.fill(rect, Theme.panelAlt, radius: 7)
    c.stroke(rect, Theme.border, width: 1, radius: 7)
    c.text("Installed app", CGRect(x: rect.minX + 12, y: rect.maxY - 25, width: rect.width - 24, height: 18), size: 11, weight: .bold)
    let lines = [
        "Desktop shortcut",
        "  Tech Mates Racing Overlay",
        "Start Menu shortcut",
        "  Tech Mates Racing Overlay",
        "MSI installer",
        "  TMROverlay-vX.Y.Z-win-x64.msi"
    ]
    for (index, line) in lines.enumerated() {
        let color = line.contains(".exe") || line.contains(".msi") || line.contains("shortcut") ? Theme.success : Theme.secondary
        c.text(line, CGRect(x: rect.minX + 16, y: rect.maxY - 52 - CGFloat(index) * 17, width: rect.width - 32, height: 14), size: 9.5, color: color)
    }
}

private func drawUpgradePreview(_ c: Canvas, _ rect: CGRect) {
    c.fill(rect, Theme.warningBackground, radius: 7)
    c.stroke(rect, Theme.warning, width: 1, radius: 7)
    c.text("Update available", CGRect(x: rect.minX + 12, y: rect.maxY - 30, width: rect.width - 24, height: 18), size: 11, weight: .bold, color: Theme.warning)
    c.multiline(
        "Installed builds can download updates and restart when you choose.",
        CGRect(x: rect.minX + 12, y: rect.maxY - 72, width: rect.width - 24, height: 34),
        size: 10.5,
        color: Theme.secondary
    )
    c.button("Install Update", CGRect(x: rect.minX + 12, y: rect.minY + 18, width: 102, height: 24), color: Theme.panelAlt)
    c.button("Restart", CGRect(x: rect.minX + 124, y: rect.minY + 18, width: 82, height: 24), color: Theme.panelAlt, textColor: Theme.warning)
}

private func drawAppPreview(_ c: Canvas, rect: CGRect) {
    c.fill(rect, Theme.panel, radius: 8)
    c.stroke(rect, Theme.border, width: 1, radius: 8)
    c.text("What the app now contains", CGRect(x: rect.minX + 24, y: rect.maxY - 42, width: 430, height: 24), size: 19, weight: .bold)
    c.text("Settings is the control surface. Driving overlays start hidden until enabled.", CGRect(x: rect.minX + 24, y: rect.maxY - 68, width: 640, height: 20), size: 12.5, color: Theme.muted)

    let window = CGRect(x: rect.minX + 24, y: rect.minY + 34, width: 425, height: 260)
    c.fill(window, NSColor(red255: 16, green: 20, blue: 23, alpha: 0.98), radius: 7)
    c.stroke(window, Theme.borderStrong, width: 1, radius: 7)
    c.fill(CGRect(x: window.minX, y: window.maxY - 38, width: window.width, height: 38), Theme.panelAlt, radius: 7)
    if let logo = loadLogoImage() {
        c.drawImage(logo, in: CGRect(x: window.minX + 12, y: window.maxY - 31, width: 48, height: 27))
    }
    c.text("Tech Mates Racing Overlay", CGRect(x: window.minX + 68, y: window.maxY - 24, width: 240, height: 16), size: 11.5, weight: .bold)
    c.text("TMR Overlay", CGRect(x: window.minX + 68, y: window.maxY - 38, width: 180, height: 14), size: 8.5, color: Theme.muted)

    let tabs = ["General", "Standings", "Relative", "Track Map", "Stream Chat", "Support"]
    for (index, tab) in tabs.enumerated() {
        let y = window.maxY - 72 - CGFloat(index) * 30
        let selected = index == 0
        c.fill(CGRect(x: window.minX + 12, y: y, width: 100, height: 24), selected ? Theme.panelRaised : Theme.panelAlt, radius: 4)
        c.text(tab, CGRect(x: window.minX + 22, y: y + 6, width: 82, height: 13), size: 8.5, weight: .semibold, color: selected ? Theme.text : Theme.muted)
    }

    let pane = CGRect(x: window.minX + 130, y: window.minY + 18, width: window.width - 148, height: window.height - 70)
    c.fill(pane, NSColor(red255: 20, green: 25, blue: 29, alpha: 1), radius: 5)
    c.text("Track Map", CGRect(x: pane.minX + 16, y: pane.maxY - 32, width: pane.width - 32, height: 18), size: 13, weight: .bold)
    c.text("Native overlay", CGRect(x: pane.minX + 18, y: pane.maxY - 70, width: 100, height: 16), size: 10, color: Theme.secondary)
    c.fill(CGRect(x: pane.minX + 120, y: pane.maxY - 75, width: 94, height: 24), Theme.panelRaised, radius: 4)
    c.text("Hidden", CGRect(x: pane.minX + 132, y: pane.maxY - 69, width: 70, height: 13), size: 9.5)
    c.text("OBS route", CGRect(x: pane.minX + 18, y: pane.maxY - 110, width: 100, height: 16), size: 10, color: Theme.secondary)
    c.fill(CGRect(x: pane.minX + 120, y: pane.maxY - 115, width: 126, height: 24), Theme.infoBackground, radius: 4)
    c.text("localhost", CGRect(x: pane.minX + 132, y: pane.maxY - 109, width: 92, height: 13), size: 9.5, color: Theme.info)
    c.stroke(CGRect(x: pane.minX + 18, y: pane.minY + 28, width: pane.width - 36, height: 64), Theme.border, width: 1, radius: 4)
    c.multiline("Flat tabs cover standings, relative, track map, stream chat, fuel, inputs, radar, flags, weather, pit service, and support.", CGRect(x: pane.minX + 30, y: pane.minY + 36, width: pane.width - 60, height: 52), size: 10.2, color: Theme.muted)

    let notesX = rect.minX + 480
    drawExpectationRow(c, x: notesX, y: rect.maxY - 126, title: "Native overlays", detail: "Windows overlay windows.", color: Theme.success)
    drawExpectationRow(c, x: notesX, y: rect.maxY - 196, title: "Localhost routes", detail: "OBS/browser-source pages.", color: Theme.info)
    drawExpectationRow(c, x: notesX, y: rect.maxY - 266, title: "Support tab", detail: "Version and diagnostics.", color: Theme.purple)
}

private func drawExpectationRow(_ c: Canvas, x: CGFloat, y: CGFloat, title: String, detail: String, color: NSColor) {
    c.fill(CGRect(x: x, y: y + 8, width: 10, height: 10), color, radius: 5)
    c.text(title, CGRect(x: x + 22, y: y + 6, width: 220, height: 17), size: 12.5, weight: .bold)
    c.text(detail, CGRect(x: x + 22, y: y - 18, width: 206, height: 28), size: 10.5, color: Theme.muted)
}

private func drawSupportPreview(_ c: Canvas, rect: CGRect) {
    c.fill(rect, Theme.panel, radius: 8)
    c.stroke(rect, Theme.border, width: 1, radius: 8)
    c.text("What we need testers to exercise", CGRect(x: rect.minX + 24, y: rect.maxY - 42, width: 430, height: 24), size: 19, weight: .bold)
    c.text("Ask teammates to try the normal install path, waiting states, live overlays, browser sources, and diagnostics.", CGRect(x: rect.minX + 24, y: rect.maxY - 68, width: 686, height: 20), size: 12.5, color: Theme.muted)

    let support = CGRect(x: rect.minX + 24, y: rect.minY + 34, width: 410, height: 260)
    c.fill(support, NSColor(red255: 20, green: 25, blue: 29, alpha: 1), radius: 7)
    c.stroke(support, Theme.borderStrong, width: 1, radius: 7)
    c.text("Support", CGRect(x: support.minX + 18, y: support.maxY - 34, width: 160, height: 20), size: 14, weight: .bold)
    c.text("Share version details and diagnostics.", CGRect(x: support.minX + 18, y: support.maxY - 60, width: support.width - 36, height: 16), size: 9.5, color: Theme.muted)
    drawSupportMetric(c, rect: CGRect(x: support.minX + 18, y: support.maxY - 94, width: support.width - 36, height: 22), label: "App version", value: "vX.Y.Z")
    c.text("Diagnostic telemetry", CGRect(x: support.minX + 18, y: support.maxY - 128, width: support.width - 36, height: 15), size: 9.6, weight: .bold)
    c.text("Enable before a requested repro.", CGRect(x: support.minX + 18, y: support.maxY - 148, width: support.width - 36, height: 15), size: 9.2, color: Theme.muted)
    c.fill(CGRect(x: support.minX + 18, y: support.maxY - 172, width: 13, height: 13), Theme.panelRaised, radius: 3)
    c.text("Capture diagnostic telemetry", CGRect(x: support.minX + 38, y: support.maxY - 175, width: support.width - 56, height: 16), size: 9.2, color: Theme.secondary)
    c.button("Create Bundle", CGRect(x: support.minX + 18, y: support.minY + 58, width: 116, height: 28), color: Theme.successBackground, textColor: Theme.success)
    c.button("Copy Latest Path", CGRect(x: support.minX + 144, y: support.minY + 58, width: 120, height: 28))
    c.button("Open Diagnostics", CGRect(x: support.minX + 274, y: support.minY + 58, width: 118, height: 28), color: Theme.infoBackground, textColor: Theme.info)
    c.text("Current state: Live telemetry | No active issue", CGRect(x: support.minX + 18, y: support.minY + 28, width: support.width - 36, height: 15), size: 9.2, color: Theme.muted)

    let checklistX = rect.minX + 470
    c.text("Please test:", CGRect(x: checklistX, y: rect.maxY - 118, width: 210, height: 20), size: 14, weight: .bold)
    let feedbackItems = [
        "Fresh launch with no iRacing running",
        "Enable diagnostic capture before a repro",
        "Enable, move, and scale a few overlays",
        "Open localhost routes in browser or OBS",
        "Join a live/replay session and drive laps",
        "Create diagnostics and copy the zip path"
    ]
    var y = rect.maxY - 150
    for item in feedbackItems {
        c.fill(CGRect(x: checklistX, y: y + 5, width: 8, height: 8), Theme.success, radius: 4)
        c.multiline(item, CGRect(x: checklistX + 20, y: y - 2, width: 250, height: 28), size: 11.2, color: Theme.secondary)
        y -= 32
    }

    c.text("Send back:", CGRect(x: checklistX, y: rect.minY + 76, width: 210, height: 18), size: 12.5, weight: .bold)
    c.multiline("Diagnostics zip path, app version, overlay names, what was happening, and a screenshot or short clip when visible.", CGRect(x: checklistX, y: rect.minY + 22, width: 252, height: 52), size: 10.2, color: Theme.muted)
}

private func drawSupportMetric(_ c: Canvas, rect: CGRect, label: String, value: String) {
    c.text(label, CGRect(x: rect.minX, y: rect.minY + 4, width: 108, height: 14), size: 9.5, color: Theme.muted)
    c.fill(CGRect(x: rect.minX + 118, y: rect.minY, width: rect.width - 118, height: rect.height), Theme.panelRaised, radius: 4)
    c.text(value, CGRect(x: rect.minX + 128, y: rect.minY + 5, width: rect.width - 136, height: 14), size: 9.5, color: Theme.secondary)
}

private func drawFooter(_ c: Canvas) {
    let footer = c.topRect(x: 42, y: 868, width: 1516, height: 92)
    c.fill(footer, NSColor(red255: 14, green: 18, blue: 21, alpha: 0.96), radius: 8)
    c.stroke(footer, Theme.border, width: 1, radius: 8)
    c.text("Support-safe reminders", CGRect(x: footer.minX + 24, y: footer.maxY - 34, width: 260, height: 20), size: 15, weight: .bold)
    c.pill("User data: %LOCALAPPDATA%\\TmrOverlay", CGRect(x: footer.minX + 310, y: footer.maxY - 45, width: 340, height: 30), color: Theme.infoBackground, textColor: Theme.info)
    c.pill("Upgrade: install update, restart to apply", CGRect(x: footer.minX + 674, y: footer.maxY - 45, width: 356, height: 30), color: Theme.successBackground, textColor: Theme.success)
    c.pill("Uninstall removes user data", CGRect(x: footer.minX + 1054, y: footer.maxY - 45, width: 276, height: 30), color: Theme.warningBackground, textColor: Theme.warning)
    c.text("Attach the diagnostics bundle when reporting telemetry, overlay, or startup problems.", CGRect(x: footer.minX + 24, y: footer.minY + 16, width: footer.width - 48, height: 18), size: 12, color: Theme.muted)
}

private func drawGrid(_ c: Canvas) {
    for x in stride(from: CGFloat(0), through: c.size.width, by: 80) {
        c.line(from: CGPoint(x: x, y: 0), to: CGPoint(x: x, y: c.size.height), color: NSColor(white: 1, alpha: 0.025), width: 1)
    }
    for y in stride(from: CGFloat(0), through: c.size.height, by: 80) {
        c.line(from: CGPoint(x: 0, y: y), to: CGPoint(x: c.size.width, y: y), color: NSColor(white: 1, alpha: 0.025), width: 1)
    }
}

private func loadLogoImage() -> NSImage? {
    guard let repositoryRoot = findRepositoryRoot(startingAt: URL(fileURLWithPath: FileManager.default.currentDirectoryPath)) else {
        return nil
    }

    let logoURL = repositoryRoot.appendingPathComponent("assets/brand/TMRLogo.png")
    return NSImage(contentsOf: logoURL)
}

private func findRepositoryRoot(startingAt url: URL) -> URL? {
    var directory = url.standardizedFileURL
    while true {
        if FileManager.default.fileExists(atPath: directory.appendingPathComponent("tmrOverlay.sln").path) {
            return directory
        }

        let parent = directory.deletingLastPathComponent()
        if parent.path == directory.path {
            return nil
        }

        directory = parent
    }
}

private func renderPNG(size: CGSize, render: (Canvas) -> Void, to url: URL) throws {
    let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: Int(size.width),
        pixelsHigh: Int(size.height),
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
    )!

    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    let context = NSGraphicsContext.current!.cgContext
    context.clear(CGRect(origin: .zero, size: size))
    render(Canvas(size: size, context: context))
    NSGraphicsContext.restoreGraphicsState()

    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(
            domain: "render_release_tutorial",
            code: 1,
            userInfo: [NSLocalizedDescriptionKey: "Could not encode PNG"]
        )
    }

    try data.write(to: url)
}

let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let defaultOutputRoot = findRepositoryRoot(startingAt: currentDirectory) ?? currentDirectory
let output = CommandLine.arguments.dropFirst().first.map(URL.init(fileURLWithPath:))
    ?? defaultOutputRoot.appendingPathComponent("docs/assets/windows-release-teammate-tutorial.png")

do {
    try FileManager.default.createDirectory(at: output.deletingLastPathComponent(), withIntermediateDirectories: true)
    try renderPNG(size: canvasSize, render: renderTutorial, to: output)
    print("wrote \(output.path)")
} catch {
    fputs("Failed to render release tutorial: \(error)\n", stderr)
    exit(1)
}
