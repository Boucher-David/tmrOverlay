import AppKit

enum DesignV2ComponentKind: CaseIterable {
    case sidebarTab
    case button
    case formControls
    case statusPills
    case sectionPanel
    case tableRows
    case graphChrome
    case overlayShell
    case localhostBlock
    case settingsContentBlock

    var title: String {
        switch self {
        case .sidebarTab:
            return "Sidebar Tab"
        case .button:
            return "Buttons"
        case .formControls:
            return "Controls"
        case .statusPills:
            return "Status Pills"
        case .sectionPanel:
            return "Section Panel"
        case .tableRows:
            return "Table Rows"
        case .graphChrome:
            return "Graph Chrome"
        case .overlayShell:
            return "Overlay Shell"
        case .localhostBlock:
            return "Localhost Block"
        case .settingsContentBlock:
            return "Content Block"
        }
    }

    var note: String {
        switch self {
        case .sidebarTab:
            return "Left navigation states for settings and overlay managers."
        case .button:
            return "Primary, secondary, icon, and destructive commands."
        case .formControls:
            return "Checkbox, toggle, slider, and pixel input treatments."
        case .statusPills:
            return "Live, measured, stale, and error evidence markers."
        case .sectionPanel:
            return "Settings/content grouping without nested card noise."
        case .tableRows:
            return "Header, rows, focus state, and class separator."
        case .graphChrome:
            return "Axes, grid, labels, reference, and main trend line."
        case .overlayShell:
            return "Compact overlay title bar and body frame."
        case .localhostBlock:
            return "Browser-source URL with recommended OBS dimensions."
        case .settingsContentBlock:
            return "Column/content manager row states."
        }
    }

    var fileName: String {
        switch self {
        case .sidebarTab:
            return "sidebar-tab.png"
        case .button:
            return "buttons.png"
        case .formControls:
            return "controls.png"
        case .statusPills:
            return "status-pills.png"
        case .sectionPanel:
            return "section-panel.png"
        case .tableRows:
            return "table-rows.png"
        case .graphChrome:
            return "graph-chrome.png"
        case .overlayShell:
            return "overlay-shell.png"
        case .localhostBlock:
            return "localhost-block.png"
        case .settingsContentBlock:
            return "settings-content-block.png"
        }
    }
}

final class DesignV2ComponentGalleryView: NSView {
    static let componentPreviewSize = NSSize(width: 430, height: 250)

    private let theme: DesignV2Theme
    private let component: DesignV2ComponentKind?
    private let fontFamily: String

    override var isFlipped: Bool {
        true
    }

    init(
        theme: DesignV2Theme = .current,
        component: DesignV2ComponentKind? = nil,
        frame: NSRect? = nil,
        fontFamily: String = OverlayTheme.defaultFontFamily
    ) {
        self.theme = theme
        self.component = component
        self.fontFamily = fontFamily
        let size = frame?.size ?? (component == nil ? theme.layout.componentGallerySize : Self.componentPreviewSize)
        super.init(frame: frame ?? NSRect(origin: .zero, size: size))
        wantsLayer = false
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        drawBackdrop(in: bounds)

        if let component {
            drawComponent(component, in: bounds.insetBy(dx: 14, dy: 14))
            return
        }

        drawText(
            "Design V2 Components - \(theme.displayName)",
            in: NSRect(x: 22, y: 16, width: bounds.width - 44, height: 24),
            font: font(size: 17, weight: .semibold),
            color: theme.colors.textPrimary
        )
        drawText(
            "Live mac-harness overlay previews for reusable settings and overlay primitives.",
            in: NSRect(x: 22, y: 42, width: bounds.width - 44, height: 18),
            font: font(size: 10.5),
            color: theme.colors.textMuted
        )

        let columns = 2
        let gap: CGFloat = 12
        let left: CGFloat = 18
        let top: CGFloat = 78
        let cardWidth = (bounds.width - left * 2 - gap) / CGFloat(columns)
        let cardHeight: CGFloat = 96
        for (index, kind) in DesignV2ComponentKind.allCases.enumerated() {
            let column = index % columns
            let row = index / columns
            let rect = NSRect(
                x: left + CGFloat(column) * (cardWidth + gap),
                y: top + CGFloat(row) * (cardHeight + gap),
                width: cardWidth,
                height: cardHeight
            )
            drawComponent(kind, in: rect)
        }
    }

    private func drawComponent(_ kind: DesignV2ComponentKind, in rect: NSRect) {
        drawRounded(
            rect,
            radius: theme.layout.panelRadius,
            fill: theme.colors.surfaceRaised,
            stroke: theme.colors.border,
            lineWidth: 1
        )
        drawRounded(
            NSRect(x: rect.minX, y: rect.minY + 8, width: 3, height: rect.height - 16),
            radius: 1.5,
            fill: theme.colors.accentPrimary,
            stroke: nil,
            lineWidth: 0
        )
        drawText(
            kind.title.uppercased(),
            in: NSRect(x: rect.minX + 14, y: rect.minY + 10, width: rect.width - 28, height: 15),
            font: font(size: 9, weight: .semibold),
            color: theme.colors.textMuted
        )

        let content = NSRect(x: rect.minX + 14, y: rect.minY + 34, width: rect.width - 28, height: rect.height - 48)
        switch kind {
        case .sidebarTab:
            drawSidebarTabs(in: content)
        case .button:
            drawButtons(in: content)
        case .formControls:
            drawFormControls(in: content)
        case .statusPills:
            drawStatusPills(in: content)
        case .sectionPanel:
            drawSectionPanel(in: content)
        case .tableRows:
            drawTableRows(in: content)
        case .graphChrome:
            drawGraphChrome(in: content)
        case .overlayShell:
            drawOverlayShell(in: content)
        case .localhostBlock:
            drawLocalhostBlock(in: content)
        case .settingsContentBlock:
            drawSettingsContentBlock(in: content)
        }
    }

    private func drawSidebarTabs(in rect: NSRect) {
        let tabs = [("General", false), ("Standings", true), ("Relative", false)]
        var y = rect.minY
        for tab in tabs {
            let tabRect = NSRect(x: rect.minX, y: y, width: min(150, rect.width), height: 25)
            let color = tab.1 ? theme.colors.accentPrimary : theme.colors.borderMuted
            drawRounded(
                tabRect,
                radius: 5,
                fill: tab.1 ? color.withAlphaComponent(0.16) : theme.colors.surfaceInset,
                stroke: tab.1 ? color.withAlphaComponent(0.75) : theme.colors.borderMuted,
                lineWidth: 1
            )
            drawText(
                tab.0,
                in: tabRect.insetBy(dx: 10, dy: 6),
                font: font(size: 10, weight: tab.1 ? .semibold : .regular),
                color: tab.1 ? theme.colors.textPrimary : theme.colors.textSecondary
            )
            y += 31
        }
    }

    private func drawButtons(in rect: NSRect) {
        drawButton("Save", in: NSRect(x: rect.minX, y: rect.minY, width: 86, height: 30), tone: theme.colors.accentPrimary, filled: true)
        drawButton("Reset", in: NSRect(x: rect.minX + 96, y: rect.minY, width: 86, height: 30), tone: theme.colors.textMuted, filled: false)
        drawButton("Copy", in: NSRect(x: rect.minX, y: rect.minY + 40, width: 86, height: 30), tone: theme.colors.accentSecondary, filled: false)
        drawButton("Disable", in: NSRect(x: rect.minX + 96, y: rect.minY + 40, width: 96, height: 30), tone: theme.colors.error, filled: false)
    }

    private func drawButton(_ title: String, in rect: NSRect, tone: NSColor, filled: Bool) {
        drawRounded(
            rect,
            radius: 5,
            fill: filled ? tone.withAlphaComponent(0.22) : theme.colors.surfaceInset,
            stroke: tone.withAlphaComponent(filled ? 0.88 : 0.64),
            lineWidth: 1
        )
        drawText(
            title,
            in: rect.insetBy(dx: 10, dy: 8),
            font: font(size: 10, weight: .semibold),
            color: filled ? theme.colors.textPrimary : tone,
            alignment: .center
        )
    }

    private func drawFormControls(in rect: NSRect) {
        drawCheckbox(title: "Show gap", checked: true, in: NSRect(x: rect.minX, y: rect.minY, width: 120, height: 20))
        drawCheckbox(title: "Footer", checked: false, in: NSRect(x: rect.minX, y: rect.minY + 28, width: 120, height: 20))
        let inputRect = NSRect(x: rect.minX + 138, y: rect.minY, width: 74, height: 28)
        drawRounded(inputRect, radius: 4, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted, lineWidth: 1)
        drawText("96 px", in: inputRect.insetBy(dx: 8, dy: 7), font: font(size: 10), color: theme.colors.textPrimary, alignment: .right)
        let sliderRect = NSRect(x: rect.minX + 138, y: rect.minY + 42, width: min(150, rect.width - 138), height: 20)
        drawRounded(NSRect(x: sliderRect.minX, y: sliderRect.midY - 2, width: sliderRect.width, height: 4), radius: 2, fill: theme.colors.borderMuted, stroke: nil, lineWidth: 0)
        drawRounded(NSRect(x: sliderRect.minX, y: sliderRect.midY - 2, width: sliderRect.width * 0.62, height: 4), radius: 2, fill: theme.colors.accentPrimary, stroke: nil, lineWidth: 0)
        drawRounded(NSRect(x: sliderRect.minX + sliderRect.width * 0.62 - 6, y: sliderRect.midY - 7, width: 14, height: 14), radius: 7, fill: theme.colors.textPrimary, stroke: theme.colors.accentPrimary, lineWidth: 1)
    }

    private func drawCheckbox(title: String, checked: Bool, in rect: NSRect) {
        let box = NSRect(x: rect.minX, y: rect.minY + 2, width: 16, height: 16)
        drawRounded(
            box,
            radius: 3,
            fill: checked ? theme.colors.accentPrimary.withAlphaComponent(0.22) : theme.colors.surfaceInset,
            stroke: checked ? theme.colors.accentPrimary : theme.colors.borderMuted,
            lineWidth: 1
        )
        if checked {
            drawText("x", in: box.insetBy(dx: 3, dy: 0), font: font(size: 11, weight: .bold), color: theme.colors.textPrimary, alignment: .center)
        }
        drawText(title, in: NSRect(x: rect.minX + 24, y: rect.minY + 2, width: rect.width - 24, height: 16), font: font(size: 10), color: theme.colors.textSecondary)
    }

    private func drawStatusPills(in rect: NSRect) {
        let badges: [(String, DesignV2EvidenceKind)] = [
            ("live", .live),
            ("measured", .measured),
            ("stale", .stale),
            ("error", .error)
        ]
        var x = rect.minX
        var y = rect.minY
        for (index, badge) in badges.enumerated() {
            let width: CGFloat = index == 1 ? 96 : 74
            drawBadge(title: badge.0, evidence: badge.1, in: NSRect(x: x, y: y, width: width, height: 24))
            x += width + 8
            if x + width > rect.maxX {
                x = rect.minX
                y += 32
            }
        }
    }

    private func drawSectionPanel(in rect: NSRect) {
        drawRounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted, lineWidth: 1)
        drawText("Display in sessions", in: NSRect(x: rect.minX + 10, y: rect.minY + 8, width: rect.width - 20, height: 16), font: font(size: 11, weight: .semibold), color: theme.colors.textPrimary)
        drawText("Practice  Qualifying  Race", in: NSRect(x: rect.minX + 10, y: rect.minY + 34, width: rect.width - 20, height: 16), font: font(size: 10), color: theme.colors.textSecondary)
        drawRounded(NSRect(x: rect.minX + 10, y: rect.maxY - 24, width: rect.width - 20, height: 1), radius: 0.5, fill: theme.colors.gridLine, stroke: nil, lineWidth: 0)
    }

    private func drawTableRows(in rect: NSRect) {
        drawRounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted, lineWidth: 1)
        let header = NSRect(x: rect.minX + 8, y: rect.minY + 8, width: rect.width - 16, height: 16)
        drawText("POS  CAR                 GAP", in: header, font: font(size: 9, weight: .semibold), color: theme.colors.textMuted)
        drawRounded(NSRect(x: rect.minX + 1, y: rect.minY + 30, width: rect.width - 2, height: 1), radius: 0.5, fill: theme.colors.gridLine, stroke: nil, lineWidth: 0)
        let classRect = NSRect(x: rect.minX + 1, y: rect.minY + 32, width: rect.width - 2, height: 18)
        drawRounded(classRect, radius: 0, fill: theme.colors.history.withAlphaComponent(0.16), stroke: nil, lineWidth: 0)
        drawText("GT3 - est. 112 laps", in: classRect.insetBy(dx: 8, dy: 3), font: font(size: 9, weight: .semibold), color: theme.colors.history)
        drawText("P4   33  TMR Blue        +12.4", in: NSRect(x: rect.minX + 8, y: rect.minY + 56, width: rect.width - 16, height: 16), font: font(size: 10, weight: .semibold), color: theme.colors.textPrimary)
        drawText("P5   71  Apex            +18.9", in: NSRect(x: rect.minX + 8, y: rect.minY + 78, width: rect.width - 16, height: 16), font: font(size: 10), color: theme.colors.textSecondary)
    }

    private func drawGraphChrome(in rect: NSRect) {
        drawRounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted, lineWidth: 1)
        let plot = rect.insetBy(dx: 12, dy: 14)
        for index in 1...3 {
            let y = plot.minY + CGFloat(index) * plot.height / 4
            drawLine(from: NSPoint(x: plot.minX, y: y), to: NSPoint(x: plot.maxX, y: y), color: theme.colors.gridLine, width: 1)
        }
        drawLine(from: NSPoint(x: plot.minX, y: plot.midY), to: NSPoint(x: plot.maxX, y: plot.midY), color: theme.colors.history.withAlphaComponent(0.58), width: 1)
        let points = [
            NSPoint(x: plot.minX, y: plot.midY + 12),
            NSPoint(x: plot.minX + plot.width * 0.22, y: plot.midY + 2),
            NSPoint(x: plot.minX + plot.width * 0.48, y: plot.midY - 18),
            NSPoint(x: plot.minX + plot.width * 0.74, y: plot.midY - 8),
            NSPoint(x: plot.maxX, y: plot.midY - 24)
        ]
        drawPolyline(points: points, color: theme.colors.accentPrimary, width: 2.2)
        drawText("+/- same lap", in: NSRect(x: plot.minX, y: rect.minY + 2, width: 120, height: 14), font: font(size: 9), color: theme.colors.textMuted)
    }

    private func drawOverlayShell(in rect: NSRect) {
        drawRounded(rect, radius: 6, fill: theme.colors.surfaceInset, stroke: theme.colors.border, lineWidth: 1)
        let title = NSRect(x: rect.minX + 1, y: rect.minY + 1, width: rect.width - 2, height: 28)
        drawRounded(title, radius: 5, fill: theme.colors.titleBar, stroke: nil, lineWidth: 0)
        drawText("Gap To Leader", in: title.insetBy(dx: 10, dy: 7), font: font(size: 10, weight: .semibold), color: theme.colors.textPrimary)
        drawBadge(title: "live", evidence: .live, in: NSRect(x: title.maxX - 70, y: title.minY + 5, width: 58, height: 18))
        drawText("P3 +12.4s", in: NSRect(x: rect.minX + 12, y: rect.minY + 48, width: 120, height: 22), font: font(size: 17, weight: .bold), color: theme.colors.textPrimary)
        drawText("Reference: P2", in: NSRect(x: rect.minX + 12, y: rect.minY + 76, width: 160, height: 16), font: font(size: 10), color: theme.colors.textMuted)
    }

    private func drawLocalhostBlock(in rect: NSRect) {
        drawRounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted, lineWidth: 1)
        drawText("Browser source", in: NSRect(x: rect.minX + 10, y: rect.minY + 8, width: rect.width - 20, height: 16), font: font(size: 10, weight: .semibold), color: theme.colors.textPrimary)
        drawText("localhost:5116/overlays/standings", in: NSRect(x: rect.minX + 10, y: rect.minY + 34, width: rect.width - 20, height: 16), font: OverlayTheme.monospacedFont(size: 9.5), color: theme.colors.accentPrimary)
        drawText("OBS size: 912 x 520", in: NSRect(x: rect.minX + 10, y: rect.minY + 60, width: rect.width - 20, height: 16), font: font(size: 10), color: theme.colors.textSecondary)
    }

    private func drawSettingsContentBlock(in rect: NSRect) {
        drawRounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted, lineWidth: 1)
        let rows = [("==", "Position", "54 px", true), ("==", "Car", "180 px", true), ("==", "Last lap", "72 px", false)]
        var y = rect.minY + 8
        for row in rows {
            let color = row.3 ? theme.colors.textSecondary : theme.colors.textDisabled
            drawText(row.0, in: NSRect(x: rect.minX + 10, y: y, width: 20, height: 16), font: font(size: 10, weight: .bold), color: theme.colors.textMuted, alignment: .center)
            drawText(row.1, in: NSRect(x: rect.minX + 38, y: y, width: 130, height: 16), font: font(size: 10, weight: row.3 ? .semibold : .regular), color: color)
            drawText(row.2, in: NSRect(x: rect.maxX - 70, y: y, width: 58, height: 16), font: font(size: 10), color: color, alignment: .right)
            y += 24
        }
    }

    private func drawBadge(title: String, evidence: DesignV2EvidenceKind, in rect: NSRect) {
        let color = theme.color(for: evidence)
        drawRounded(rect, radius: rect.height / 2, fill: color.withAlphaComponent(0.14), stroke: color.withAlphaComponent(0.66), lineWidth: 1)
        drawText(title.uppercased(), in: rect.insetBy(dx: 8, dy: max(3, (rect.height - 12) / 2)), font: font(size: 8.5, weight: .semibold), color: theme.colors.textPrimary, alignment: .center)
    }

    private func drawBackdrop(in rect: NSRect) {
        drawRounded(rect.insetBy(dx: 1, dy: 1), radius: theme.layout.cornerRadius, fill: theme.colors.surface, stroke: theme.colors.border, lineWidth: 1)
    }

    private func drawRounded(_ rect: NSRect, radius: CGFloat, fill: NSColor?, stroke: NSColor?, lineWidth: CGFloat) {
        DesignV2Drawing.rounded(rect, radius: radius, fill: fill, stroke: stroke, lineWidth: lineWidth)
    }

    private func drawText(
        _ text: String,
        in rect: NSRect,
        font: NSFont,
        color: NSColor,
        alignment: NSTextAlignment = .left
    ) {
        DesignV2Drawing.text(text, in: rect, font: font, color: color, alignment: alignment)
    }

    private func drawLine(from start: NSPoint, to end: NSPoint, color: NSColor, width: CGFloat) {
        DesignV2Drawing.line(from: start, to: end, color: color, width: width)
    }

    private func drawPolyline(points: [NSPoint], color: NSColor, width: CGFloat) {
        DesignV2Drawing.polyline(points: points, color: color, width: width)
    }

    private func font(size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        DesignV2Drawing.font(family: fontFamily, size: size, weight: weight)
    }
}
