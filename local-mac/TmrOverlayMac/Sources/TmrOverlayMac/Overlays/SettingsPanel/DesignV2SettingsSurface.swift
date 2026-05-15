import AppKit

enum DesignV2SettingsPalette {
    static let bgTop = NSColor(red255: 18, green: 5, blue: 31)
    static let bgMid = NSColor(red255: 12, green: 18, blue: 42)
    static let bgBottom = NSColor(red255: 3, green: 11, blue: 24)
    static let panelRaised = NSColor(red255: 17, green: 28, blue: 55, alpha: 0.98)
    static let titleBar = NSColor(red255: 11, green: 14, blue: 33, alpha: 0.98)
    static let border = NSColor(red255: 40, green: 72, blue: 108, alpha: 0.92)
    static let borderDim = NSColor(red255: 30, green: 52, blue: 82, alpha: 0.78)
    static let text = NSColor(red255: 255, green: 247, blue: 255)
    static let secondary = NSColor(red255: 208, green: 230, blue: 255)
    static let muted = NSColor(red255: 140, green: 174, blue: 212)
    static let dim = NSColor(red255: 82, green: 112, blue: 148)
    static let cyan = NSColor(red255: 0, green: 232, blue: 255)
    static let magenta = NSColor(red255: 255, green: 42, blue: 167)
    static let amber = NSColor(red255: 255, green: 209, blue: 91)
    static let green = NSColor(red255: 98, green: 255, blue: 159)
    static let orange = NSColor(red255: 255, green: 125, blue: 73)
    static let purple = NSColor(red255: 126, green: 50, blue: 255)
}

struct DesignV2SettingsSidebarTab {
    var id: String
    var label: String
}

struct DesignV2SettingsSegment {
    var id: String
    var label: String
    var width: CGFloat
}

enum DesignV2SettingsRegion: String, CaseIterable {
    case general
    case content
    case header
    case footer
    case preview
    case twitch
    case streamlabs

    var title: String {
        switch self {
        case .general:
            return "General"
        case .content:
            return "Content"
        case .header:
            return "Header"
        case .footer:
            return "Footer"
        case .preview:
            return "Preview"
        case .twitch:
            return "Twitch"
        case .streamlabs:
            return "Streamlabs"
        }
    }

    var segmentWidth: CGFloat {
        switch self {
        case .general:
            return 86
        case .preview:
            return 82
        case .streamlabs:
            return 104
        default:
            return 76
        }
    }

    static var standardSegments: [DesignV2SettingsSegment] {
        allCases.map { region in
            DesignV2SettingsSegment(id: region.rawValue, label: region.title, width: region.segmentWidth)
        }
    }
}

enum DesignV2SettingsReferenceImages {
    static func load(relativePath: String) -> NSImage? {
        for url in candidates(relativePath: relativePath) where FileManager.default.fileExists(atPath: url.path) {
            if let image = NSImage(contentsOf: url) {
                return image
            }
        }
        return nil
    }

    private static func candidates(relativePath: String) -> [URL] {
        let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).standardizedFileURL
        var candidates = [currentDirectory.appendingPathComponent(relativePath)]
        var directory = currentDirectory
        while true {
            if FileManager.default.fileExists(atPath: directory.appendingPathComponent("tmrOverlay.sln").path) {
                candidates.append(directory.appendingPathComponent(relativePath))
                break
            }

            let parent = directory.deletingLastPathComponent()
            if parent.path == directory.path {
                break
            }
            directory = parent
        }
        return candidates
    }
}

enum DesignV2SettingsChrome {
    static let matchedWindowBoundsOrigin = NSPoint(x: 44, y: 36)

    static let sidebarTabs: [DesignV2SettingsSidebarTab] = [
        DesignV2SettingsSidebarTab(id: "general", label: "General"),
        DesignV2SettingsSidebarTab(id: "standings", label: "Standings"),
        DesignV2SettingsSidebarTab(id: "relative", label: "Relative"),
        DesignV2SettingsSidebarTab(id: "gap-to-leader", label: "Gap To Leader"),
        DesignV2SettingsSidebarTab(id: "track-map", label: "Track Map"),
        DesignV2SettingsSidebarTab(id: "stream-chat", label: "Stream Chat"),
        DesignV2SettingsSidebarTab(id: "garage-cover", label: "Garage Cover"),
        DesignV2SettingsSidebarTab(id: "fuel-calculator", label: "Fuel Calculator"),
        DesignV2SettingsSidebarTab(id: "input-state", label: "Inputs"),
        DesignV2SettingsSidebarTab(id: "car-radar", label: "Car Radar"),
        DesignV2SettingsSidebarTab(id: "flags", label: "Flags"),
        DesignV2SettingsSidebarTab(id: "session-weather", label: "Session / Weather"),
        DesignV2SettingsSidebarTab(id: "pit-service", label: "Pit Service"),
        DesignV2SettingsSidebarTab(id: "error-logging", label: "Diagnostics")
    ]

    static var overlayTabOrder: [String] {
        sidebarTabs
            .map(\.id)
            .filter { $0 != "general" && $0 != "error-logging" }
    }

    static func sidebarButtonFrame(index: Int) -> NSRect {
        NSRect(x: 78, y: 164 + CGFloat(index) * 32, width: 162, height: 27)
    }

    static func segmentFrames(
        for segments: [DesignV2SettingsSegment],
        origin: NSPoint = NSPoint(x: 312, y: 208),
        gap: CGFloat = 12
    ) -> [(DesignV2SettingsSegment, NSRect)] {
        var x = origin.x
        return segments.map { segment in
            let rect = NSRect(x: x, y: origin.y, width: segment.width, height: 30)
            x += segment.width + gap
            return (segment, rect)
        }
    }

    static func segmentShellWidth(for segments: [DesignV2SettingsSegment], gap: CGFloat = 12) -> CGFloat {
        guard !segments.isEmpty else {
            return 0
        }

        return segments.reduce(CGFloat(0)) { $0 + $1.width } + gap * CGFloat(segments.count - 1) + 12
    }
}

enum DesignV2SettingsOverlaySpecs {
    static let fullSurfaceOverlayIds: Set<String> = [
        "standings",
        "relative",
        "gap-to-leader",
        "fuel-calculator",
        "session-weather",
        "pit-service",
        "track-map",
        "stream-chat",
        "garage-cover",
        "input-state",
        "car-radar",
        "flags"
    ]

    static func usesFullSurface(_ overlayId: String) -> Bool {
        fullSurfaceOverlayIds.contains(overlayId)
    }

    static func supportsSharedChromeSettings(_ overlayId: String) -> Bool {
        ["standings", "relative", "fuel-calculator", "gap-to-leader", "session-weather", "pit-service"].contains(overlayId)
    }

    static func regions(for overlayId: String) -> [DesignV2SettingsRegion] {
        if overlayId == "garage-cover" {
            return [.general, .preview]
        }

        if overlayId == "stream-chat" {
            return [.general, .content, .twitch, .streamlabs]
        }

        return supportsSharedChromeSettings(overlayId)
            ? [.general, .content, .header, .footer]
            : [.general, .content]
    }

    static func segments(for overlayId: String) -> [DesignV2SettingsSegment] {
        regions(for: overlayId).map { region in
            DesignV2SettingsSegment(id: region.rawValue, label: region.title, width: region.segmentWidth)
        }
    }

    static func subtitle(for overlayId: String) -> String {
        switch overlayId {
        case "standings":
            return "Class and overall running order for the current session."
        case "relative":
            return "Nearby-car timing around the local in-car reference."
        case "gap-to-leader":
            return "Focused class gap trend and nearby leader context."
        case "fuel-calculator":
            return "Fuel strategy, stint targets, and source confidence."
        case "session-weather":
            return "Session timing, track state, and weather telemetry."
        case "pit-service":
            return "Pit request state, service plan, and release context."
        case "track-map":
            return "Live car location and sector context."
        case "stream-chat":
            return "Local browser-source chat setup for Streamlabs or Twitch."
        case "garage-cover":
            return "Local browser-source privacy cover for garage and setup scenes."
        case "input-state":
            return "Input rail visibility for pedal, steering, gear, and speed telemetry."
        case "car-radar":
            return "Local proximity radar and faster-class warning controls."
        case "flags":
            return "Compact session flag strip display and size controls."
        default:
            return "Overlay settings and browser-source controls."
        }
    }
}

struct DesignV2SettingsRenderer {
    var fontFamily: String

    func drawBackdrop(in bounds: NSRect) {
        drawGradient(
            bounds,
            colors: [
                DesignV2SettingsPalette.bgTop,
                DesignV2SettingsPalette.bgMid,
                DesignV2SettingsPalette.bgBottom
            ],
            angle: -55
        )

        clipRounded(NSRect(x: bounds.width - 288, y: 48, width: 176, height: 176), radius: 88) {
            drawGradient(
                NSRect(x: bounds.width - 288, y: 48, width: 176, height: 176),
                colors: [
                    DesignV2SettingsPalette.amber,
                    DesignV2SettingsPalette.orange,
                    DesignV2SettingsPalette.magenta,
                    DesignV2SettingsPalette.purple
                ],
                angle: 90
            )
            var offset: CGFloat = 44
            while offset <= 142 {
                fillRounded(
                    NSRect(x: bounds.width - 300, y: 48 + offset, width: 200, height: offset > 100 ? 12 : 8),
                    radius: 0,
                    color: DesignV2SettingsPalette.bgTop.withAlphaComponent(0.92)
                )
                offset += 22
            }
        }

        let gridTop = bounds.height * 0.58
        drawGradient(
            NSRect(x: 0, y: gridTop, width: bounds.width, height: bounds.height - gridTop),
            colors: [
                DesignV2SettingsPalette.cyan.withAlphaComponent(0.02),
                DesignV2SettingsPalette.cyan.withAlphaComponent(0.12),
                DesignV2SettingsPalette.magenta.withAlphaComponent(0.40)
            ],
            angle: 90
        )
        var y = gridTop + 16
        while y <= bounds.height - 14 {
            let alpha = min(0.5, 0.14 + (y - gridTop) / 460)
            DesignV2Drawing.line(
                from: NSPoint(x: 0, y: y),
                to: NSPoint(x: bounds.maxX, y: y),
                color: DesignV2SettingsPalette.cyan.withAlphaComponent(alpha),
                width: 1
            )
            y += 24
        }
        var x: CGFloat = -180
        while x <= bounds.width + 180 {
            DesignV2Drawing.line(
                from: NSPoint(x: bounds.midX, y: gridTop),
                to: NSPoint(x: x, y: bounds.maxY),
                color: DesignV2SettingsPalette.magenta.withAlphaComponent(0.42),
                width: 1
            )
            x += 150
        }
    }

    func drawWindowShell() {
        let outer = NSRect(x: 44, y: 36, width: 1152, height: 608)
        fillRounded(outer, radius: 18, color: NSColor.black.withAlphaComponent(0.28))
        drawGradient(
            outer,
            colors: [
                NSColor(red255: 8, green: 10, blue: 23),
                NSColor(red255: 15, green: 9, blue: 32),
                NSColor(red255: 5, green: 20, blue: 37)
            ],
            angle: -25,
            radius: 18
        )
        strokeRounded(outer, radius: 18, color: DesignV2SettingsPalette.cyan.withAlphaComponent(0.78), lineWidth: 1.4)
        fillRounded(NSRect(x: 44, y: 36, width: 1152, height: 58), radius: 18, color: DesignV2SettingsPalette.titleBar)
        fillRounded(NSRect(x: 44, y: 92, width: 1152, height: 2), radius: 0, color: DesignV2SettingsPalette.magenta)
        fillRounded(NSRect(x: 44, y: 94, width: 1152, height: 1), radius: 0, color: DesignV2SettingsPalette.cyan)
    }

    func drawTitleBar() {
        let logoRect = NSRect(x: 66, y: 50, width: 50, height: 30)
        if let logo = TmrBrandAssets.loadLogoImage() {
            drawAspectFit(logo, in: logoRect)
        } else {
            fillRounded(NSRect(x: 66, y: 54, width: 46, height: 24), radius: 5, color: DesignV2SettingsPalette.panelRaised)
            strokeRounded(NSRect(x: 66, y: 54, width: 46, height: 24), radius: 5, color: DesignV2SettingsPalette.magenta, lineWidth: 1.2)
            drawCentered("TMR", in: NSRect(x: 66, y: 53, width: 46, height: 24), size: 14, weight: .black, color: DesignV2SettingsPalette.text)
        }
        drawText("Tech Mates Racing Overlay", in: NSRect(x: 128, y: 52, width: 480, height: 28), size: 24, weight: .heavy, color: DesignV2SettingsPalette.text)
        drawCentered("X", in: NSRect(x: 1132, y: 54, width: 30, height: 24), size: 13, weight: .black, color: NSColor(red255: 255, green: 200, blue: 239))
    }

    func drawSidebar(activeTabId: String) {
        let sidebar = NSRect(x: 64, y: 116, width: 190, height: 506)
        fillRounded(sidebar, radius: 14, color: NSColor(red255: 6, green: 13, blue: 26, alpha: 0.92))
        strokeRounded(sidebar, radius: 14, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
        drawText("SETTINGS", in: NSRect(x: 84, y: 136, width: 110, height: 18), size: 12, weight: .heavy, color: DesignV2SettingsPalette.cyan)

        for (index, tab) in DesignV2SettingsChrome.sidebarTabs.enumerated() {
            let rect = DesignV2SettingsChrome.sidebarButtonFrame(index: index)
            let active = tab.id == activeTabId
            fillRounded(rect, radius: 8, color: active ? NSColor(red255: 48, green: 16, blue: 68) : NSColor(red255: 17, green: 26, blue: 50))
            if active {
                strokeRounded(rect, radius: 8, color: DesignV2SettingsPalette.magenta, lineWidth: 1.3)
                fillRounded(NSRect(x: rect.minX, y: rect.minY, width: 5, height: rect.height), radius: 3, color: DesignV2SettingsPalette.cyan)
            }
            drawText(
                tab.label,
                in: NSRect(x: rect.minX + 14, y: rect.minY + 7, width: rect.width - 30, height: 16),
                size: 11.5,
                weight: active ? .heavy : .semibold,
                color: active ? DesignV2SettingsPalette.text : NSColor(red255: 185, green: 217, blue: 255)
            )
        }
    }

    func drawContentContainer() {
        let rect = NSRect(x: 278, y: 116, width: 890, height: 506)
        fillRounded(rect, radius: 16, color: NSColor(red255: 8, green: 17, blue: 33, alpha: 0.94))
        strokeRounded(rect, radius: 16, color: DesignV2SettingsPalette.border, lineWidth: 1.2)
    }

    func drawContentHeader(title: String, subtitle: String, status: String? = nil) {
        fillRounded(NSRect(x: 278, y: 116, width: 890, height: 70), radius: 16, color: NSColor(red255: 16, green: 22, blue: 50, alpha: 0.9))
        fillRounded(NSRect(x: 278, y: 184, width: 890, height: 2), radius: 0, color: DesignV2SettingsPalette.magenta)
        fillRounded(NSRect(x: 278, y: 186, width: 890, height: 1), radius: 0, color: DesignV2SettingsPalette.cyan)
        drawText(title, in: NSRect(x: 306, y: 134, width: 520, height: 32), size: 26, weight: .black, color: DesignV2SettingsPalette.text)
        drawText(subtitle, in: NSRect(x: 306, y: 164, width: 570, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
        if let status {
            drawPill(status, in: NSRect(x: 1012, y: 134, width: 112, height: 30), fill: NSColor(red255: 10, green: 47, blue: 63), textColor: DesignV2SettingsPalette.cyan)
        }
    }

    func drawSegments(_ segments: [DesignV2SettingsSegment], selectedId: String) {
        let shell = NSRect(
            x: 306,
            y: 202,
            width: DesignV2SettingsChrome.segmentShellWidth(for: segments),
            height: 42
        )
        fillRounded(shell, radius: 21, color: NSColor(red255: 8, green: 15, blue: 31))
        strokeRounded(shell, radius: 21, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)

        for (segment, rect) in DesignV2SettingsChrome.segmentFrames(for: segments) {
            let selected = segment.id == selectedId
            if selected {
                fillRounded(rect, radius: 15, color: DesignV2SettingsPalette.magenta)
            }
            drawCentered(
                segment.label,
                in: NSRect(x: rect.minX, y: rect.minY - 1, width: rect.width, height: rect.height),
                size: 12,
                weight: .heavy,
                color: selected ? DesignV2SettingsPalette.text : DesignV2SettingsPalette.cyan
            )
        }
    }

    func drawPanel(_ rect: NSRect, title: String) {
        fillRounded(rect, radius: 12, color: NSColor(red255: 9, green: 18, blue: 34, alpha: 0.96))
        strokeRounded(rect, radius: 12, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
        drawText(title, in: NSRect(x: rect.minX + 22, y: rect.minY + 18, width: rect.width - 44, height: 20), size: 15, weight: .heavy, color: DesignV2SettingsPalette.text)
        DesignV2Drawing.line(from: NSPoint(x: rect.minX + 22, y: rect.minY + 48), to: NSPoint(x: rect.maxX - 22, y: rect.minY + 48), color: DesignV2SettingsPalette.borderDim, width: 1)
    }

    func drawPill(_ text: String, in rect: NSRect, fill: NSColor, textColor: NSColor) {
        fillRounded(rect, radius: rect.height / 2, color: fill)
        strokeRounded(rect, radius: rect.height / 2, color: NSColor.white.withAlphaComponent(0.16), lineWidth: 1)
        drawCentered(text, in: rect.insetBy(dx: 8, dy: 0), size: min(12, rect.height * 0.45), weight: .bold, color: textColor)
    }

    func drawText(
        _ text: String,
        in rect: NSRect,
        size: CGFloat,
        weight: NSFont.Weight = .regular,
        color: NSColor,
        alignment: NSTextAlignment = .left,
        monospaced: Bool = false
    ) {
        DesignV2Drawing.text(
            text,
            in: rect,
            font: monospaced ? OverlayTheme.monospacedFont(size: size, weight: weight) : font(size: size, weight: weight),
            color: color,
            alignment: alignment
        )
    }

    func drawCentered(_ text: String, in rect: NSRect, size: CGFloat, weight: NSFont.Weight = .semibold, color: NSColor, monospaced: Bool = false) {
        let textHeight = size * 1.45
        drawText(
            text,
            in: NSRect(x: rect.minX, y: rect.minY + max(0, (rect.height - textHeight) / 2), width: rect.width, height: textHeight),
            size: size,
            weight: weight,
            color: color,
            alignment: .center,
            monospaced: monospaced
        )
    }

    func fillRounded(_ rect: NSRect, radius: CGFloat, color: NSColor) {
        DesignV2Drawing.rounded(rect, radius: radius, fill: color, stroke: nil, lineWidth: 0)
    }

    func strokeRounded(_ rect: NSRect, radius: CGFloat, color: NSColor, lineWidth: CGFloat) {
        DesignV2Drawing.rounded(rect.insetBy(dx: lineWidth / 2, dy: lineWidth / 2), radius: radius, fill: nil, stroke: color, lineWidth: lineWidth)
    }

    func drawGradient(_ rect: NSRect, colors: [NSColor], angle: CGFloat, radius: CGFloat = 0) {
        guard let gradient = NSGradient(colors: colors) else {
            return
        }

        if radius > 0 {
            clipRounded(rect, radius: radius) {
                gradient.draw(in: rect, angle: angle)
            }
        } else {
            gradient.draw(in: rect, angle: angle)
        }
    }

    func clipRounded(_ rect: NSRect, radius: CGFloat, _ body: () -> Void) {
        NSGraphicsContext.saveGraphicsState()
        NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius).addClip()
        body()
        NSGraphicsContext.restoreGraphicsState()
    }

    func drawAspectFit(_ image: NSImage, in rect: NSRect) {
        guard image.size.width > 0, image.size.height > 0 else {
            return
        }

        let scale = min(rect.width / image.size.width, rect.height / image.size.height)
        let size = NSSize(width: image.size.width * scale, height: image.size.height * scale)
        let target = NSRect(
            x: rect.minX + (rect.width - size.width) / 2,
            y: rect.minY + (rect.height - size.height) / 2,
            width: size.width,
            height: size.height
        )
        image.draw(
            in: target,
            from: NSRect(origin: .zero, size: image.size),
            operation: .sourceOver,
            fraction: 1,
            respectFlipped: true,
            hints: nil
        )
    }

    private func font(size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        DesignV2Drawing.font(family: fontFamily, size: size, weight: weight)
    }
}

final class DesignV2SettingsStepperControl: NSControl {
    private var value: Int
    private let minimum: Int
    private let maximum: Int
    private let valueLabel: (Int) -> String
    private let controlFont: NSFont
    private let onChange: (Int) -> Void

    override var isFlipped: Bool { true }

    init(
        frame: NSRect,
        value: Int,
        minimum: Int,
        maximum: Int,
        valueLabel: @escaping (Int) -> String = { String($0) },
        font: NSFont,
        onChange: @escaping (Int) -> Void
    ) {
        self.value = min(max(value, minimum), maximum)
        self.minimum = minimum
        self.maximum = maximum
        self.valueLabel = valueLabel
        self.controlFont = font
        self.onChange = onChange
        super.init(frame: frame)
        wantsLayer = false
    }

    required init?(coder: NSCoder) { nil }

    override func draw(_ dirtyRect: NSRect) {
        let rect = bounds.insetBy(dx: 0.5, dy: 0.5)
        DesignV2Drawing.rounded(
            rect,
            radius: 10,
            fill: NSColor(red255: 17, green: 30, blue: 60),
            stroke: DesignV2SettingsPalette.borderDim,
            lineWidth: 1
        )

        let buttonWidth: CGFloat = 34
        let left = NSRect(x: rect.minX + 4, y: rect.minY + 4, width: buttonWidth, height: rect.height - 8)
        let right = NSRect(x: rect.maxX - buttonWidth - 4, y: rect.minY + 4, width: buttonWidth, height: rect.height - 8)
        drawButton(left, label: "-", enabled: value > minimum)
        drawButton(right, label: "+", enabled: value < maximum)
        DesignV2Drawing.text(
            valueLabel(value),
            in: NSRect(x: left.maxX + 8, y: rect.minY + 8, width: right.minX - left.maxX - 16, height: rect.height - 16),
            font: controlFont,
            color: DesignV2SettingsPalette.text,
            alignment: .center
        )
    }

    override func mouseDown(with event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil)
        let nextValue: Int
        if point.x < bounds.midX {
            nextValue = max(minimum, value - 1)
        } else {
            nextValue = min(maximum, value + 1)
        }
        guard nextValue != value else {
            return
        }

        value = nextValue
        onChange(value)
        needsDisplay = true
    }

    private func drawButton(_ rect: NSRect, label: String, enabled: Bool) {
        DesignV2Drawing.rounded(
            rect,
            radius: 8,
            fill: enabled ? NSColor(red255: 6, green: 46, blue: 55) : DesignV2SettingsPalette.panelRaised,
            stroke: enabled ? DesignV2SettingsPalette.cyan : DesignV2SettingsPalette.border,
            lineWidth: 1
        )
        DesignV2Drawing.text(
            label,
            in: NSRect(x: rect.minX, y: rect.minY + 4, width: rect.width, height: rect.height - 8),
            font: controlFont,
            color: enabled ? DesignV2SettingsPalette.green : DesignV2SettingsPalette.dim,
            alignment: .center
        )
    }
}

final class DesignV2SettingsActionButtonControl: NSControl {
    private let title: String
    private let buttonFont: NSFont
    private let onClick: () -> Void

    override var isFlipped: Bool { true }

    init(frame: NSRect, title: String, font: NSFont, onClick: @escaping () -> Void) {
        self.title = title
        self.buttonFont = font
        self.onClick = onClick
        super.init(frame: frame)
        wantsLayer = false
    }

    required init?(coder: NSCoder) { nil }

    override func draw(_ dirtyRect: NSRect) {
        DesignV2Drawing.rounded(bounds, radius: 8, fill: NSColor(red255: 36, green: 17, blue: 56), stroke: nil, lineWidth: 0)
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = .center
        NSString(string: title).draw(
            in: NSRect(x: 0, y: 7, width: bounds.width, height: 16),
            withAttributes: [
                .font: buttonFont,
                .foregroundColor: DesignV2SettingsPalette.text,
                .paragraphStyle: paragraph
            ]
        )
    }

    override func mouseDown(with event: NSEvent) {
        onClick()
    }
}

final class DesignV2SettingsChoiceControl: NSControl {
    private let options: [String]
    private var selectedIndex: Int
    private let controlFont: NSFont
    private let onChange: (String) -> Void

    override var isFlipped: Bool { true }

    init(
        frame: NSRect,
        options: [String],
        selected: String,
        font: NSFont,
        onChange: @escaping (String) -> Void
    ) {
        self.options = options
        self.selectedIndex = options.firstIndex(where: { $0.caseInsensitiveCompare(selected) == .orderedSame }) ?? 0
        self.controlFont = font
        self.onChange = onChange
        super.init(frame: frame)
        wantsLayer = false
    }

    required init?(coder: NSCoder) { nil }

    func applySelected(_ selected: String) {
        let nextIndex = options.firstIndex(where: { $0.caseInsensitiveCompare(selected) == .orderedSame }) ?? 0
        guard nextIndex != selectedIndex else {
            return
        }

        selectedIndex = nextIndex
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        let rect = bounds.insetBy(dx: 0.5, dy: 0.5)
        DesignV2Drawing.rounded(
            rect,
            radius: rect.height / 2,
            fill: NSColor(red255: 17, green: 30, blue: 60),
            stroke: DesignV2SettingsPalette.borderDim,
            lineWidth: 1
        )

        guard !options.isEmpty else {
            return
        }

        let segmentWidth = rect.width / CGFloat(options.count)
        for (index, option) in options.enumerated() {
            let segment = NSRect(
                x: rect.minX + CGFloat(index) * segmentWidth,
                y: rect.minY,
                width: segmentWidth,
                height: rect.height
            )
            let selected = index == selectedIndex
            if selected {
                DesignV2Drawing.rounded(
                    segment.insetBy(dx: 3, dy: 3),
                    radius: (rect.height - 6) / 2,
                    fill: DesignV2SettingsPalette.magenta,
                    stroke: nil,
                    lineWidth: 0
                )
            }
            DesignV2Drawing.text(
                option,
                in: NSRect(x: segment.minX, y: segment.minY + 7, width: segment.width, height: 16),
                font: controlFont,
                color: selected ? DesignV2SettingsPalette.text : DesignV2SettingsPalette.muted,
                alignment: .center
            )
        }
    }

    override func mouseDown(with event: NSEvent) {
        guard !options.isEmpty else {
            return
        }

        let point = convert(event.locationInWindow, from: nil)
        let rawIndex = Int(floor(point.x / max(1, bounds.width / CGFloat(options.count))))
        let index = min(max(rawIndex, 0), options.count - 1)
        guard index != selectedIndex else {
            return
        }

        selectedIndex = index
        onChange(options[index])
        needsDisplay = true
    }
}

final class DesignV2SettingsToggleControl: NSControl {
    private var isOn: Bool
    private let onChange: (Bool) -> Void

    override var isFlipped: Bool { true }

    init(frame: NSRect, isOn: Bool, theme: DesignV2Theme, onChange: @escaping (Bool) -> Void) {
        self.isOn = isOn
        self.onChange = onChange
        super.init(frame: frame)
        wantsLayer = false
    }

    required init?(coder: NSCoder) { nil }

    override func draw(_ dirtyRect: NSRect) {
        let rect = bounds.insetBy(dx: 0.5, dy: 0.5)
        DesignV2Drawing.rounded(
            rect,
            radius: rect.height / 2,
            fill: isOn ? NSColor(red255: 5, green: 60, blue: 69) : DesignV2SettingsPalette.panelRaised,
            stroke: isOn ? DesignV2SettingsPalette.cyan : DesignV2SettingsPalette.border,
            lineWidth: 1
        )
        let knob = NSRect(x: isOn ? 30 : 6, y: 4, width: 20, height: 20)
        DesignV2Drawing.rounded(knob, radius: 10, fill: isOn ? DesignV2SettingsPalette.green : DesignV2SettingsPalette.dim, stroke: nil, lineWidth: 0)
    }

    override func mouseDown(with event: NSEvent) {
        isOn.toggle()
        onChange(isOn)
        needsDisplay = true
    }
}

final class DesignV2SettingsPercentSliderControl: NSControl {
    private var value: Int
    private let allowedValues: [Int]
    private let activeColor: NSColor
    private let onChange: (Int) -> Void

    override var isFlipped: Bool { true }

    init(frame: NSRect, value: Int, allowedValues: [Int], activeColor: NSColor, theme: DesignV2Theme, onChange: @escaping (Int) -> Void) {
        self.value = value
        self.allowedValues = allowedValues
        self.activeColor = activeColor
        self.onChange = onChange
        super.init(frame: frame)
        wantsLayer = false
    }

    required init?(coder: NSCoder) { nil }

    override func draw(_ dirtyRect: NSRect) {
        let minValue = CGFloat(allowedValues.first ?? 0)
        let maxValue = CGFloat(allowedValues.last ?? 100)
        let pct = maxValue > minValue ? (CGFloat(value) - minValue) / (maxValue - minValue) : 0
        let track = NSRect(x: bounds.minX, y: bounds.minY + 11, width: bounds.width, height: 6)
        DesignV2Drawing.rounded(track, radius: 3, fill: DesignV2SettingsPalette.panelRaised, stroke: nil, lineWidth: 0)
        DesignV2Drawing.rounded(NSRect(x: track.minX, y: track.minY, width: track.width * pct, height: track.height), radius: 3, fill: activeColor, stroke: nil, lineWidth: 0)
        DesignV2Drawing.rounded(NSRect(x: track.minX + track.width * pct - 8, y: bounds.minY + 6, width: 16, height: 16), radius: 8, fill: DesignV2SettingsPalette.amber, stroke: nil, lineWidth: 0)
    }

    override func mouseDown(with event: NSEvent) {
        update(from: event)
    }

    override func mouseDragged(with event: NSEvent) {
        update(from: event)
    }

    private func update(from event: NSEvent) {
        let x = min(max(convert(event.locationInWindow, from: nil).x, bounds.minX), bounds.maxX)
        let fraction = bounds.width > 0 ? (x - bounds.minX) / bounds.width : 0
        let minValue = CGFloat(allowedValues.first ?? 0)
        let maxValue = CGFloat(allowedValues.last ?? 100)
        let raw = Int((minValue + fraction * (maxValue - minValue)).rounded())
        value = allowedValues.min(by: { abs($0 - raw) < abs($1 - raw) }) ?? raw
        onChange(value)
        needsDisplay = true
    }
}

final class DesignV2SettingsCheckControl: NSControl {
    private let title: String
    private var isOn: Bool
    private let controlFont: NSFont
    private let onChange: (Bool) -> Void

    override var isFlipped: Bool { true }

    init(frame: NSRect, title: String, isOn: Bool, theme: DesignV2Theme, font: NSFont, onChange: @escaping (Bool) -> Void) {
        self.title = title
        self.isOn = isOn
        self.controlFont = font
        self.onChange = onChange
        super.init(frame: frame)
        wantsLayer = false
    }

    required init?(coder: NSCoder) { nil }

    override func draw(_ dirtyRect: NSRect) {
        let box = NSRect(x: 0.5, y: 0.5, width: 19, height: 19)
        DesignV2Drawing.rounded(
            box,
            radius: 5,
            fill: isOn ? NSColor(red255: 6, green: 46, blue: 55) : DesignV2SettingsPalette.panelRaised,
            stroke: isOn ? DesignV2SettingsPalette.cyan : DesignV2SettingsPalette.border,
            lineWidth: 1
        )
        if isOn {
            DesignV2Drawing.line(from: NSPoint(x: 5, y: 10), to: NSPoint(x: 9, y: 15), color: DesignV2SettingsPalette.green, width: 2)
            DesignV2Drawing.line(from: NSPoint(x: 9, y: 15), to: NSPoint(x: 16, y: 6), color: DesignV2SettingsPalette.green, width: 2)
        }
        if !title.isEmpty {
            DesignV2Drawing.text(title, in: NSRect(x: 28, y: 2, width: bounds.width - 28, height: 18), font: controlFont, color: isOn ? DesignV2SettingsPalette.secondary : DesignV2SettingsPalette.muted)
        }
    }

    override func mouseDown(with event: NSEvent) {
        isOn.toggle()
        onChange(isOn)
        needsDisplay = true
    }
}
