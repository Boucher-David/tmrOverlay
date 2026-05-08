import AppKit

final class DesignV2PreviewView: NSView {
    private let scenario: DesignV2PreviewScenario
    private let fontFamily: String

    override var isFlipped: Bool {
        true
    }

    init(
        scenario: DesignV2PreviewScenario,
        frame: NSRect = NSRect(origin: .zero, size: DesignV2Theme.Layout.previewSize),
        fontFamily: String = OverlayTheme.defaultFontFamily
    ) {
        self.scenario = scenario
        self.fontFamily = fontFamily
        super.init(frame: frame)
        wantsLayer = false
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)

        let outer = bounds.insetBy(dx: 1, dy: 1)
        drawRounded(
            outer,
            radius: DesignV2Theme.Layout.cornerRadius,
            fill: DesignV2Theme.Colors.surface,
            stroke: DesignV2Theme.Colors.border,
            lineWidth: 1
        )
        drawAccent(in: outer)
        drawHeader(in: outer)
        drawMetrics(in: NSRect(x: outer.minX + 18, y: outer.minY + 82, width: 238, height: 220))

        let bodyRect = NSRect(x: outer.minX + 278, y: outer.minY + 82, width: outer.width - 296, height: 252)
        switch scenario.mode {
        case .standingsTable:
            drawTelemetryTable(in: bodyRect, title: "Timing Board")
        case .relativeTable:
            drawTelemetryTable(in: bodyRect, title: "Focus Relative")
        case .sectorComparison:
            drawTelemetryTable(in: bodyRect, title: "Sector Splits", tag: "model target")
        case .blindspotSignal:
            drawBlindspotSignal(in: bodyRect)
        case .lapDelta:
            drawLapDelta(in: bodyRect)
        case .stintLapGraph:
            drawStintLapGraph(in: bodyRect)
        case .flagStrip:
            drawFlagStrip(in: bodyRect)
        case .sourceTable:
            drawSourceTable(in: bodyRect, title: "Model Evidence")
        case .fuelMatrix:
            drawFuelMatrix(in: bodyRect)
        case .gapGraph:
            drawGapGraph(in: bodyRect)
        case .unavailable:
            drawUnavailable(in: bodyRect)
        }

        drawFooter(in: NSRect(x: outer.minX + 18, y: outer.maxY - 56, width: outer.width - 36, height: 38))
    }

    private func drawAccent(in rect: NSRect) {
        guard let primary = scenario.badges.first?.evidence.color else {
            return
        }

        let accentRect = NSRect(x: rect.minX, y: rect.minY + 8, width: 4, height: rect.height - 16)
        drawRounded(accentRect, radius: 2, fill: primary, stroke: nil, lineWidth: 0)
    }

    private func drawHeader(in rect: NSRect) {
        drawText(
            scenario.title,
            in: NSRect(x: rect.minX + 18, y: rect.minY + 16, width: 280, height: 24),
            font: font(size: 18, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )
        drawText(
            scenario.subtitle,
            in: NSRect(x: rect.minX + 18, y: rect.minY + 42, width: 410, height: 20),
            font: font(size: 11),
            color: DesignV2Theme.Colors.textMuted
        )

        var nextX = rect.maxX - 18
        for badge in scenario.badges.reversed() {
            let width = badgeWidth(badge)
            nextX -= width
            drawBadge(badge, in: NSRect(x: nextX, y: rect.minY + 18, width: width, height: 24))
            nextX -= 8
        }
    }

    private func drawMetrics(in rect: NSRect) {
        drawText(
            "Primary Readout",
            in: NSRect(x: rect.minX, y: rect.minY, width: rect.width, height: 18),
            font: font(size: 10, weight: .semibold),
            color: DesignV2Theme.Colors.textMuted
        )

        var rowY = rect.minY + 26
        for metric in scenario.metrics {
            drawMetric(metric, in: NSRect(x: rect.minX, y: rowY, width: rect.width, height: 54))
            rowY += 64
        }
    }

    private func drawMetric(_ metric: DesignV2Metric, in rect: NSRect) {
        drawRounded(
            rect,
            radius: DesignV2Theme.Layout.panelRadius,
            fill: DesignV2Theme.Colors.surfaceRaised,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )

        let color = metric.evidence.color
        drawRounded(
            NSRect(x: rect.minX, y: rect.minY + 8, width: 3, height: rect.height - 16),
            radius: 1.5,
            fill: color,
            stroke: nil,
            lineWidth: 0
        )

        drawText(
            metric.title.uppercased(),
            in: NSRect(x: rect.minX + 14, y: rect.minY + 9, width: 96, height: 16),
            font: font(size: 9, weight: .semibold),
            color: DesignV2Theme.Colors.textMuted
        )
        drawText(
            metric.value,
            in: NSRect(x: rect.maxX - 116, y: rect.minY + 8, width: 102, height: 20),
            font: font(size: 16, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary,
            alignment: .right
        )
        drawText(
            metric.detail,
            in: NSRect(x: rect.minX + 14, y: rect.minY + 30, width: rect.width - 28, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textSecondary
        )
    }

    private func drawTelemetryTable(in rect: NSRect, title: String, tag: String = "direct telemetry") {
        guard let table = scenario.table else {
            drawUnavailable(in: rect)
            return
        }

        drawPanel(rect)
        drawText(
            title,
            in: NSRect(x: rect.minX + 14, y: rect.minY + 12, width: 170, height: 18),
            font: font(size: 12, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )
        drawText(
            tag,
            in: NSRect(x: rect.maxX - 138, y: rect.minY + 13, width: 124, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textMuted,
            alignment: .right
        )

        let tableRect = NSRect(x: rect.minX + 12, y: rect.minY + 46, width: rect.width - 24, height: rect.height - 62)
        drawRounded(
            tableRect,
            radius: 5,
            fill: DesignV2Theme.Colors.surfaceInset,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )

        let columnCount = max(1, table.columns.count)
        let rowHeight: CGFloat = 30
        let headerHeight: CGFloat = 26
        let columnWidth = tableRect.width / CGFloat(columnCount)

        for (index, column) in table.columns.enumerated() {
            let cellRect = NSRect(
                x: tableRect.minX + CGFloat(index) * columnWidth + 8,
                y: tableRect.minY + 7,
                width: columnWidth - 12,
                height: 14
            )
            drawText(
                column,
                in: cellRect,
                font: font(size: 8.5, weight: .semibold),
                color: DesignV2Theme.Colors.textMuted,
                alignment: index >= max(0, columnCount - 3) ? .right : .left
            )
        }

        DesignV2Theme.Colors.gridLine.setStroke()
        let headerLine = NSBezierPath()
        headerLine.move(to: NSPoint(x: tableRect.minX + 1, y: tableRect.minY + headerHeight))
        headerLine.line(to: NSPoint(x: tableRect.maxX - 1, y: tableRect.minY + headerHeight))
        headerLine.lineWidth = 1
        headerLine.stroke()

        for (rowIndex, row) in table.rows.enumerated() {
            let rowY = tableRect.minY + headerHeight + CGFloat(rowIndex) * rowHeight
            let rowRect = NSRect(x: tableRect.minX + 1, y: rowY, width: tableRect.width - 2, height: rowHeight)
            if rowIndex == table.highlightedRowIndex {
                DesignV2Theme.Colors.measured.withAlphaComponent(0.13).setFill()
                rowRect.fill()
                drawRounded(
                    NSRect(x: rowRect.minX + 1, y: rowRect.minY + 6, width: 3, height: rowRect.height - 12),
                    radius: 1.5,
                    fill: DesignV2Theme.Colors.measured,
                    stroke: nil,
                    lineWidth: 0
                )
            }

            for index in 0..<columnCount {
                let value = index < row.count ? row[index] : ""
                let cellRect = NSRect(
                    x: tableRect.minX + CGFloat(index) * columnWidth + 8,
                    y: rowY + 8,
                    width: columnWidth - 12,
                    height: 15
                )
                drawText(
                    value,
                    in: cellRect,
                    font: font(size: index == 3 ? 10 : 9.5, weight: rowIndex == table.highlightedRowIndex ? .semibold : .regular),
                    color: rowIndex == table.highlightedRowIndex ? DesignV2Theme.Colors.textPrimary : DesignV2Theme.Colors.textSecondary,
                    alignment: index >= max(0, columnCount - 3) ? .right : .left
                )
            }
        }
    }

    private func drawBlindspotSignal(in rect: NSRect) {
        drawPanel(rect)
        drawText(
            "Side Occupancy",
            in: NSRect(x: rect.minX + 14, y: rect.minY + 12, width: 160, height: 18),
            font: font(size: 12, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )
        drawText(
            "CarLeftRight",
            in: NSRect(x: rect.maxX - 138, y: rect.minY + 13, width: 124, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textMuted,
            alignment: .right
        )

        let laneRect = NSRect(x: rect.minX + 30, y: rect.minY + 54, width: rect.width - 60, height: 118)
        drawRounded(
            laneRect,
            radius: 7,
            fill: DesignV2Theme.Colors.surfaceInset,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )

        let centerCar = NSRect(x: laneRect.midX - 25, y: laneRect.midY - 40, width: 50, height: 80)
        drawRounded(
            centerCar,
            radius: 6,
            fill: DesignV2Theme.Colors.measured.withAlphaComponent(0.24),
            stroke: DesignV2Theme.Colors.measured,
            lineWidth: 1
        )
        drawText(
            "YOU",
            in: NSRect(x: centerCar.minX, y: centerCar.midY - 8, width: centerCar.width, height: 16),
            font: font(size: 10, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary,
            alignment: .center
        )

        drawBlindspotSide(
            title: "LEFT",
            value: "CLEAR",
            active: false,
            color: DesignV2Theme.Colors.live,
            in: NSRect(x: laneRect.minX + 16, y: laneRect.minY + 24, width: 120, height: 70)
        )
        drawBlindspotSide(
            title: "RIGHT",
            value: "OVERLAP",
            active: true,
            color: DesignV2Theme.Colors.partial,
            in: NSRect(x: laneRect.maxX - 136, y: laneRect.minY + 24, width: 120, height: 70)
        )

        let summaryRect = NSRect(x: rect.minX + 18, y: laneRect.maxY + 18, width: rect.width - 36, height: 44)
        drawRounded(
            summaryRect,
            radius: 5,
            fill: DesignV2Theme.Colors.surfaceInset,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )
        drawText(
            "Direct local signal. Hide when the user is spectating, replaying, in garage, or not the focused car.",
            in: summaryRect.insetBy(dx: 12, dy: 13),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textSecondary
        )
    }

    private func drawBlindspotSide(title: String, value: String, active: Bool, color: NSColor, in rect: NSRect) {
        drawRounded(
            rect,
            radius: 6,
            fill: color.withAlphaComponent(active ? 0.22 : 0.10),
            stroke: color.withAlphaComponent(active ? 0.86 : 0.48),
            lineWidth: 1
        )
        drawText(
            title,
            in: NSRect(x: rect.minX + 10, y: rect.minY + 10, width: rect.width - 20, height: 14),
            font: font(size: 9, weight: .semibold),
            color: DesignV2Theme.Colors.textMuted,
            alignment: .center
        )
        drawText(
            value,
            in: NSRect(x: rect.minX + 8, y: rect.minY + 30, width: rect.width - 16, height: 24),
            font: font(size: active ? 16 : 14, weight: .bold),
            color: active ? color : DesignV2Theme.Colors.textSecondary,
            alignment: .center
        )
    }

    private func drawLapDelta(in rect: NSRect) {
        drawPanel(rect)
        drawText(
            "Current Lap Delta",
            in: NSRect(x: rect.minX + 14, y: rect.minY + 12, width: 170, height: 18),
            font: font(size: 12, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )
        drawText(
            scenario.graph?.unitLabel ?? "s vs target",
            in: NSRect(x: rect.maxX - 138, y: rect.minY + 13, width: 124, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textMuted,
            alignment: .right
        )

        drawText(
            scenario.graph?.valueLabel ?? scenario.metrics.first?.value ?? "--",
            in: NSRect(x: rect.minX + 22, y: rect.minY + 52, width: 180, height: 58),
            font: font(size: 44, weight: .bold),
            color: DesignV2Theme.Colors.partial
        )
        drawText(
            "target 8:19.50",
            in: NSRect(x: rect.minX + 26, y: rect.minY + 108, width: 150, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textMuted
        )

        let plot = NSRect(x: rect.minX + 24, y: rect.minY + 146, width: rect.width - 48, height: 72)
        drawLineGraph(
            scenario.graph,
            in: plot,
            lineColor: DesignV2Theme.Colors.partial,
            referenceColor: DesignV2Theme.Colors.measured
        )

        drawText(
            "simple once the model exposes a current-lap delta or stable local baseline",
            in: NSRect(x: rect.minX + 24, y: plot.maxY + 14, width: rect.width - 48, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textSecondary
        )
    }

    private func drawStintLapGraph(in rect: NSRect) {
        drawPanel(rect)
        drawText(
            scenario.graph?.title ?? "Stint Laps",
            in: NSRect(x: rect.minX + 14, y: rect.minY + 12, width: 180, height: 18),
            font: font(size: 12, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )
        drawText(
            scenario.graph?.valueLabel ?? "--",
            in: NSRect(x: rect.maxX - 138, y: rect.minY + 10, width: 124, height: 22),
            font: font(size: 16, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary,
            alignment: .right
        )

        let plot = NSRect(x: rect.minX + 18, y: rect.minY + 50, width: rect.width - 36, height: 150)
        drawLineGraph(
            scenario.graph,
            in: plot,
            lineColor: DesignV2Theme.Colors.measured,
            referenceColor: DesignV2Theme.Colors.history
        )

        let summaryRect = NSRect(x: rect.minX + 14, y: plot.maxY + 16, width: rect.width - 28, height: 38)
        drawRounded(
            summaryRect,
            radius: 5,
            fill: DesignV2Theme.Colors.surfaceRaised,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )
        drawText(
            "Measured completed laps only. Reset at stint boundaries; no fuel or strategy advice here.",
            in: summaryRect.insetBy(dx: 12, dy: 11),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textSecondary
        )
    }

    private func drawLineGraph(_ graph: DesignV2LineGraph?, in rect: NSRect, lineColor: NSColor, referenceColor: NSColor) {
        guard let graph, !graph.points.isEmpty else {
            drawUnavailable(in: rect)
            return
        }

        drawRounded(
            rect,
            radius: 5,
            fill: DesignV2Theme.Colors.surfaceInset,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )

        for index in 1...3 {
            let y = rect.minY + CGFloat(index) * rect.height / 4
            DesignV2Theme.Colors.gridLine.setStroke()
            let path = NSBezierPath()
            path.move(to: NSPoint(x: rect.minX + 1, y: y))
            path.line(to: NSPoint(x: rect.maxX - 1, y: y))
            path.lineWidth = 1
            path.stroke()
        }

        let minValue = graph.minValue ?? graph.points.min() ?? 0
        let maxValue = graph.maxValue ?? graph.points.max() ?? 1
        let range = max(0.001, maxValue - minValue)
        if let reference = graph.referenceValue {
            let normalized = CGFloat((reference - minValue) / range)
            let y = rect.minY + min(max(normalized, 0), 1) * rect.height
            referenceColor.withAlphaComponent(0.52).setStroke()
            let referencePath = NSBezierPath()
            referencePath.move(to: NSPoint(x: rect.minX + 1, y: y))
            referencePath.line(to: NSPoint(x: rect.maxX - 1, y: y))
            referencePath.lineWidth = 1
            referencePath.stroke()
        }

        let count = max(1, graph.points.count - 1)
        let points = graph.points.enumerated().map { index, value in
            NSPoint(
                x: CGFloat(index) / CGFloat(count),
                y: CGFloat((value - minValue) / range)
            )
        }
        drawLine(points: points, in: rect.insetBy(dx: 8, dy: 8), color: lineColor, width: 2.4)
    }

    private func drawFlagStrip(in rect: NSRect) {
        drawPanel(rect)

        let flagRect = NSRect(x: rect.minX + 18, y: rect.minY + 18, width: rect.width - 36, height: 74)
        drawRounded(
            flagRect,
            radius: 6,
            fill: DesignV2Theme.Colors.live.withAlphaComponent(0.15),
            stroke: DesignV2Theme.Colors.live.withAlphaComponent(0.74),
            lineWidth: 1
        )
        drawRounded(
            NSRect(x: flagRect.minX, y: flagRect.minY, width: 5, height: flagRect.height),
            radius: 2.5,
            fill: DesignV2Theme.Colors.live,
            stroke: nil,
            lineWidth: 0
        )
        drawText(
            "GREEN FLAG",
            in: NSRect(x: flagRect.minX + 18, y: flagRect.minY + 14, width: flagRect.width - 36, height: 28),
            font: font(size: 24, weight: .bold),
            color: DesignV2Theme.Colors.textPrimary
        )
        drawText(
            "Race running. No active race-control message.",
            in: NSRect(x: flagRect.minX + 20, y: flagRect.minY + 46, width: flagRect.width - 40, height: 16),
            font: font(size: 11),
            color: DesignV2Theme.Colors.textSecondary
        )

        var rowY = flagRect.maxY + 18
        for row in scenario.rows {
            drawSourceRow(row, in: NSRect(x: rect.minX + 18, y: rowY, width: rect.width - 36, height: 40))
            rowY += 48
        }
    }

    private func drawSourceTable(in rect: NSRect, title: String) {
        drawPanel(rect)
        drawText(
            title,
            in: NSRect(x: rect.minX + 14, y: rect.minY + 12, width: 160, height: 18),
            font: font(size: 12, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )
        drawText(
            "source / quality / usability",
            in: NSRect(x: rect.maxX - 188, y: rect.minY + 13, width: 174, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textMuted,
            alignment: .right
        )

        var rowY = rect.minY + 46
        for row in scenario.rows {
            drawSourceRow(row, in: NSRect(x: rect.minX + 12, y: rowY, width: rect.width - 24, height: 48))
            rowY += 58
        }
    }

    private func drawFuelMatrix(in rect: NSRect) {
        drawSourceTable(in: NSRect(x: rect.minX, y: rect.minY, width: rect.width, height: 214), title: "Fuel Evidence")

        let stripRect = NSRect(x: rect.minX, y: rect.maxY - 30, width: rect.width, height: 30)
        let labels = [
            DesignV2Badge(title: "measured", evidence: .measured),
            DesignV2Badge(title: "modeled", evidence: .modeled),
            DesignV2Badge(title: "history", evidence: .history)
        ]
        var x = stripRect.minX + 8
        for badge in labels {
            let width = badgeWidth(badge)
            drawBadge(badge, in: NSRect(x: x, y: stripRect.minY + 3, width: width, height: 22))
            x += width + 8
        }
    }

    private func drawGapGraph(in rect: NSRect) {
        drawPanel(rect)
        drawText(
            "Leader Context vs Local Battle",
            in: NSRect(x: rect.minX + 14, y: rect.minY + 12, width: 230, height: 18),
            font: font(size: 12, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )

        let plot = NSRect(x: rect.minX + 18, y: rect.minY + 48, width: rect.width - 36, height: 138)
        drawRounded(
            plot,
            radius: 5,
            fill: DesignV2Theme.Colors.surfaceInset,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )

        for index in 1...3 {
            let y = plot.minY + CGFloat(index) * plot.height / 4
            DesignV2Theme.Colors.gridLine.setStroke()
            let path = NSBezierPath()
            path.move(to: NSPoint(x: plot.minX + 1, y: y))
            path.line(to: NSPoint(x: plot.maxX - 1, y: y))
            path.lineWidth = 1
            path.stroke()
        }

        let wetBand = NSRect(x: plot.minX + plot.width * 0.54, y: plot.minY + 1, width: plot.width * 0.22, height: plot.height - 2)
        DesignV2Theme.Colors.measured.withAlphaComponent(0.11).setFill()
        wetBand.fill()

        drawLine(
            points: [
                NSPoint(x: 0.00, y: 0.48),
                NSPoint(x: 0.14, y: 0.44),
                NSPoint(x: 0.28, y: 0.53),
                NSPoint(x: 0.42, y: 0.35),
                NSPoint(x: 0.56, y: 0.42),
                NSPoint(x: 0.72, y: 0.60),
                NSPoint(x: 0.88, y: 0.50),
                NSPoint(x: 1.00, y: 0.58)
            ],
            in: plot,
            color: DesignV2Theme.Colors.measured,
            width: 2.4
        )
        drawLine(
            points: [
                NSPoint(x: 0.00, y: 0.72),
                NSPoint(x: 0.20, y: 0.67),
                NSPoint(x: 0.40, y: 0.78),
                NSPoint(x: 0.62, y: 0.69),
                NSPoint(x: 0.82, y: 0.74),
                NSPoint(x: 1.00, y: 0.68)
            ],
            in: plot,
            color: DesignV2Theme.Colors.history,
            width: 2
        )

        drawText(
            "+4L leader context",
            in: NSRect(x: plot.minX + 10, y: plot.minY + 9, width: 150, height: 16),
            font: font(size: 10, weight: .semibold),
            color: DesignV2Theme.Colors.history
        )
        drawText(
            "local class fight stays readable",
            in: NSRect(x: plot.maxX - 182, y: plot.maxY - 24, width: 170, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textSecondary,
            alignment: .right
        )

        let summaryRect = NSRect(x: rect.minX + 14, y: plot.maxY + 16, width: rect.width - 28, height: 38)
        drawRounded(
            summaryRect,
            radius: 5,
            fill: DesignV2Theme.Colors.surfaceRaised,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )
        drawText(
            "Scale policy: leader gap is context; local deltas own the Y-axis.",
            in: summaryRect.insetBy(dx: 12, dy: 11),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textSecondary
        )
    }

    private func drawUnavailable(in rect: NSRect) {
        drawPanel(rect)
        let iconRect = NSRect(x: rect.midX - 24, y: rect.minY + 48, width: 48, height: 48)
        drawRounded(
            iconRect,
            radius: 24,
            fill: DesignV2Theme.Colors.unavailable.withAlphaComponent(0.16),
            stroke: DesignV2Theme.Colors.unavailable.withAlphaComponent(0.68),
            lineWidth: 1
        )
        drawText(
            "!",
            in: NSRect(x: iconRect.minX, y: iconRect.minY + 10, width: iconRect.width, height: 28),
            font: font(size: 24, weight: .semibold),
            color: DesignV2Theme.Colors.unavailable,
            alignment: .center
        )
        drawText(
            "No Focus-Safe Placement",
            in: NSRect(x: rect.minX + 24, y: rect.minY + 112, width: rect.width - 48, height: 22),
            font: font(size: 15, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary,
            alignment: .center
        )
        drawText(
            "Timing rows can still feed relative or standings views. Radar side occupancy remains hidden until the focused car is the local player.",
            in: NSRect(x: rect.minX + 36, y: rect.minY + 142, width: rect.width - 72, height: 44),
            font: font(size: 11),
            color: DesignV2Theme.Colors.textSecondary,
            alignment: .center,
            lineBreakMode: .byWordWrapping
        )

        var x = rect.minX + 40
        for badge in [
            DesignV2Badge(title: "spatial missing", evidence: .unavailable),
            DesignV2Badge(title: "timing usable", evidence: .measured),
            DesignV2Badge(title: "warning gated", evidence: .partial)
        ] {
            let width = badgeWidth(badge)
            drawBadge(badge, in: NSRect(x: x, y: rect.maxY - 42, width: width, height: 22))
            x += width + 8
        }
    }

    private func drawSourceRow(_ row: DesignV2SourceRow, in rect: NSRect) {
        drawRounded(
            rect,
            radius: 5,
            fill: DesignV2Theme.Colors.surfaceInset,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )
        drawRounded(
            NSRect(x: rect.minX, y: rect.minY + 7, width: 3, height: rect.height - 14),
            radius: 1.5,
            fill: row.evidence.color,
            stroke: nil,
            lineWidth: 0
        )

        drawText(
            row.label,
            in: NSRect(x: rect.minX + 13, y: rect.minY + 8, width: 106, height: 16),
            font: font(size: 10, weight: .semibold),
            color: DesignV2Theme.Colors.textSecondary
        )
        drawText(
            row.value,
            in: NSRect(x: rect.minX + 124, y: rect.minY + 8, width: 120, height: 16),
            font: font(size: 11, weight: .semibold),
            color: row.evidence.color
        )
        drawText(
            row.detail,
            in: NSRect(x: rect.minX + 13, y: rect.minY + 27, width: rect.width - 26, height: 15),
            font: font(size: 9.5),
            color: DesignV2Theme.Colors.textMuted
        )
    }

    private func drawFooter(in rect: NSRect) {
        drawRounded(
            rect,
            radius: 6,
            fill: DesignV2Theme.Colors.surfaceInset,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )

        let badge = DesignV2Badge(title: footerBadgeTitle, evidence: scenario.badges.first?.evidence ?? .measured)
        let width = badgeWidth(badge)
        drawBadge(badge, in: NSRect(x: rect.minX + 10, y: rect.minY + 8, width: width, height: 22))
        drawText(
            scenario.footer,
            in: NSRect(x: rect.minX + width + 20, y: rect.minY + 11, width: rect.width - width - 30, height: 16),
            font: font(size: 10),
            color: DesignV2Theme.Colors.textSecondary
        )
    }

    private var footerBadgeTitle: String {
        switch scenario.mode {
        case .standingsTable, .relativeTable, .blindspotSignal, .flagStrip:
            return "telemetry"
        case .sectorComparison, .lapDelta, .stintLapGraph, .sourceTable, .fuelMatrix, .gapGraph, .unavailable:
            return "source"
        }
    }

    private func drawPanel(_ rect: NSRect) {
        drawRounded(
            rect,
            radius: DesignV2Theme.Layout.panelRadius,
            fill: DesignV2Theme.Colors.surfaceRaised,
            stroke: DesignV2Theme.Colors.borderMuted,
            lineWidth: 1
        )
    }

    private func drawBadge(_ badge: DesignV2Badge, in rect: NSRect) {
        let color = badge.evidence.color
        drawRounded(
            rect,
            radius: rect.height / 2,
            fill: color.withAlphaComponent(0.14),
            stroke: color.withAlphaComponent(0.66),
            lineWidth: 1
        )
        let dotRect = NSRect(x: rect.minX + 8, y: rect.midY - 3, width: 6, height: 6)
        drawRounded(dotRect, radius: 3, fill: color, stroke: nil, lineWidth: 0)
        drawText(
            badge.title.uppercased(),
            in: NSRect(x: rect.minX + 18, y: rect.minY + 5, width: rect.width - 24, height: rect.height - 8),
            font: font(size: 9, weight: .semibold),
            color: DesignV2Theme.Colors.textPrimary
        )
    }

    private func badgeWidth(_ badge: DesignV2Badge) -> CGFloat {
        let text = badge.title.uppercased() as NSString
        let size = text.size(withAttributes: [.font: font(size: 9, weight: .semibold)])
        return max(58, ceil(size.width) + 30)
    }

    private func drawLine(points: [NSPoint], in rect: NSRect, color: NSColor, width: CGFloat) {
        guard let first = points.first else {
            return
        }

        let path = NSBezierPath()
        path.move(to: mapped(first, in: rect))
        for point in points.dropFirst() {
            path.line(to: mapped(point, in: rect))
        }
        color.setStroke()
        path.lineWidth = width
        path.lineCapStyle = .round
        path.lineJoinStyle = .round
        path.stroke()
    }

    private func mapped(_ point: NSPoint, in rect: NSRect) -> NSPoint {
        NSPoint(
            x: rect.minX + point.x * rect.width,
            y: rect.minY + point.y * rect.height
        )
    }

    private func drawRounded(
        _ rect: NSRect,
        radius: CGFloat,
        fill: NSColor?,
        stroke: NSColor?,
        lineWidth: CGFloat
    ) {
        let path = NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
        if let fill {
            fill.setFill()
            path.fill()
        }
        if let stroke, lineWidth > 0 {
            stroke.setStroke()
            path.lineWidth = lineWidth
            path.stroke()
        }
    }

    private func drawText(
        _ text: String,
        in rect: NSRect,
        font: NSFont,
        color: NSColor,
        alignment: NSTextAlignment = .left,
        lineBreakMode: NSLineBreakMode = .byTruncatingTail
    ) {
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = alignment
        paragraph.lineBreakMode = lineBreakMode

        NSString(string: text).draw(
            in: rect,
            withAttributes: [
                .font: font,
                .foregroundColor: color,
                .paragraphStyle: paragraph
            ]
        )
    }

    private func font(size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        OverlayTheme.font(family: fontFamily, size: size, weight: weight)
    }
}
