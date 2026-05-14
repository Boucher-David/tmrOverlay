import AppKit

final class DesignV2OverlaySuiteView: NSView {
    private enum Layout {
        static let padding: CGFloat = 16
        static let headerHeight: CGFloat = 38
        static let footerHeight: CGFloat = 32
        static let bodyGap: CGFloat = 12
        static let rowHeight: CGFloat = 30
        static let rowGap: CGFloat = 5
        static let columnGap: CGFloat = 8
        static let metricLabelWidth: CGFloat = 124
        static let minimumColumnWidth: CGFloat = 24
    }

    private let kind: DesignV2OverlayMockKind
    private let historyQueryService: SessionHistoryQueryService
    private var latestSnapshot = LiveTelemetrySnapshot.empty
    private var latestModel: DesignV2OverlayModel
    private var overlayError: String?
    private var cachedHistoryCombo: HistoricalComboIdentity?
    private var cachedHistory: SessionHistoryLookupResult?
    private var cachedHistoryAt: Date?
    private var gapPoints: [Double] = []
    private var inputTrace: [DesignV2InputPoint] = []
    private lazy var garageCoverLogoImage = TmrBrandAssets.loadLogoImage()
    var theme = DesignV2Theme.outrun {
        didSet { needsDisplay = true }
    }
    var sourceSettings = OverlaySettings(id: "", width: 0, height: 0) {
        didSet { needsDisplay = true }
    }
    var fontFamily = OverlayTheme.defaultFontFamily {
        didSet { needsDisplay = true }
    }
    var unitSystem = "Metric" {
        didSet { needsDisplay = true }
    }

    init(kind: DesignV2OverlayMockKind, historyQueryService: SessionHistoryQueryService) {
        self.kind = kind
        self.historyQueryService = historyQueryService
        latestModel = DesignV2OverlayModel(
            title: kind.title,
            status: "waiting",
            footer: "source: waiting",
            evidence: .unavailable,
            body: .metricRows([])
        )
        sourceSettings = OverlaySettings(
            id: kind.sourceDefinition.id,
            width: kind.sourceDefinition.defaultSize.width,
            height: kind.sourceDefinition.defaultSize.height
        )
        super.init(frame: NSRect(origin: .zero, size: kind.defaultSize))
        wantsLayer = true
        layer?.backgroundColor = NSColor.clear.cgColor
    }

    required init?(coder: NSCoder) {
        nil
    }

    override var isFlipped: Bool {
        true
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        latestSnapshot = snapshot
        overlayError = nil
        if kind == .inputState, let frame = snapshot.latestFrame {
            appendInputTrace(frame)
        }
        latestModel = buildModel(snapshot)
        needsDisplay = true
    }

    func showOverlayError(_ message: String) {
        overlayError = message
        latestModel = DesignV2OverlayModel(
            title: kind.title,
            status: "overlay error",
            footer: trim(message),
            evidence: .error,
            body: .metricRows([DesignV2OverlayMetricRow(label: "Error", value: trim(message), evidence: .error)])
        )
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        let model = latestModel
        if drawCustomOverlay(model.body) {
            return
        }

        let outer = bounds.insetBy(dx: 0.5, dy: 0.5)
        rounded(outer, radius: theme.layout.cornerRadius, fill: theme.colors.surface, stroke: theme.colors.border)

        let header = NSRect(x: outer.minX + 1, y: outer.minY + 1, width: outer.width - 2, height: Layout.headerHeight)
        rounded(header, radius: max(2, theme.layout.cornerRadius - 1), fill: theme.colors.titleBar, stroke: nil)
        rounded(
            NSRect(x: outer.minX, y: outer.minY + 7, width: 2, height: max(1, outer.height - 14)),
            radius: 2,
            fill: theme.color(for: model.evidence),
            stroke: nil
        )
        rounded(
            NSRect(x: outer.minX, y: header.maxY - 1, width: outer.width, height: 2),
            radius: 1,
            fill: theme.colors.accentSecondary,
            stroke: nil
        )

        drawText(
            model.title,
            in: NSRect(x: outer.minX + 14, y: header.midY - 9, width: 210, height: 18),
            font: overlayFont(size: 14, weight: .bold),
            color: theme.colors.textPrimary
        )
        drawText(
            model.status,
            in: NSRect(x: outer.minX + 232, y: header.midY - 8, width: outer.width - 248, height: 16),
            font: overlayFont(size: 11, weight: .semibold),
            color: theme.color(for: model.evidence),
            alignment: .right
        )

        let bodyRect = NSRect(
            x: outer.minX + Layout.padding,
            y: header.maxY + Layout.bodyGap,
            width: outer.width - Layout.padding * 2,
            height: max(1, outer.height - Layout.headerHeight - Layout.footerHeight - Layout.bodyGap - 1)
        )
        drawBody(model.body, in: bodyRect)

        drawText(
            model.footer,
            in: NSRect(x: outer.minX + 14, y: outer.maxY - 24, width: outer.width - 28, height: 14),
            font: overlayFont(size: 10, weight: .regular),
            color: theme.colors.textMuted
        )
    }

    private func drawBody(_ body: DesignV2OverlayBody, in rect: NSRect) {
        switch body {
        case let .table(columns, rows):
            drawTable(columns: columns, rows: rows, in: rect)
        case let .metricRows(rows):
            drawMetricRows(rows, in: rect)
        case let .graph(points):
            drawGraph(points: points, in: rect)
        case let .chat(rows):
            drawChat(rows, in: rect)
        case let .garageCover(model):
            drawGarageCover(model, in: rect)
        case let .radar(model):
            drawRadar(model, in: rect)
        case let .inputs(model):
            drawInputs(model, in: rect)
        case let .trackMap(model):
            drawTrackMap(model, in: rect)
        }
    }

    private func drawCustomOverlay(_ body: DesignV2OverlayBody) -> Bool {
        switch body {
        case let .garageCover(model):
            drawGarageCover(model, in: bounds.insetBy(dx: 0.5, dy: 0.5))
            return true
        case let .radar(model):
            drawRadar(model, in: bounds.insetBy(dx: 0.5, dy: 0.5))
            return true
        case let .inputs(model):
            drawInputs(model, in: bounds.insetBy(dx: 0.5, dy: 0.5))
            return true
        case let .trackMap(model):
            drawTrackMap(model, in: bounds.insetBy(dx: 0.5, dy: 0.5))
            return true
        default:
            return false
        }
    }

    private func drawTable(columns: [DesignV2OverlayColumn], rows: [DesignV2OverlayRow], in rect: NSRect) {
        rounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted)
        guard !columns.isEmpty else {
            return
        }

        let visibleRows = rows.prefix(max(1, Int((rect.height - Layout.rowHeight) / (Layout.rowHeight + Layout.rowGap))))
        let totalColumnGap = CGFloat(max(0, columns.count - 1)) * Layout.columnGap
        let configuredWidth = columns.reduce(CGFloat(0)) { $0 + max(Layout.minimumColumnWidth, $1.width) }
        let availableWidth = max(1, rect.width - 20 - totalColumnGap)
        let fit = min(1, availableWidth / max(1, configuredWidth))
        var x = rect.minX + 10
        for column in columns {
            let width = max(Layout.minimumColumnWidth, column.width) * fit
            drawText(
                column.label,
                in: NSRect(x: x, y: rect.minY + 8, width: width, height: 14),
                font: overlayFont(size: 10, weight: .semibold),
                color: theme.colors.textMuted,
                alignment: column.alignment
            )
            x += width + Layout.columnGap
        }

        for (rowIndex, row) in visibleRows.enumerated() {
            let topPadding = row.isClassHeader && rowIndex > 0 ? CGFloat(7) : 0
            let rowY = rect.minY + 30 + CGFloat(rowIndex) * (Layout.rowHeight + Layout.rowGap) + topPadding
            let rowHeight = row.isClassHeader ? CGFloat(24) : Layout.rowHeight
            let rowRect = NSRect(x: rect.minX + 8, y: rowY, width: rect.width - 16, height: rowHeight)
            let classColor = OverlayClassColor.color(row.classColorHex, alpha: 0.95)
            let fill: NSColor
            let stroke: NSColor
            if row.isClassHeader, let classColor {
                fill = OverlayClassColor.blend(panel: theme.colors.surfaceRaised, accent: classColor, panelWeight: 4, accentWeight: 2)
                stroke = classColor.withAlphaComponent(0.52)
            } else if row.isReference {
                fill = OverlayClassColor.blend(panel: theme.colors.surfaceRaised, accent: theme.colors.accentPrimary, panelWeight: 10, accentWeight: 1)
                stroke = theme.colors.borderMuted.withAlphaComponent(0.34)
            } else {
                fill = theme.colors.surfaceRaised
                stroke = theme.colors.borderMuted.withAlphaComponent(0.34)
            }
            rounded(rowRect, radius: 5, fill: fill, stroke: stroke)
            if row.isClassHeader, let classColor {
                rounded(
                    NSRect(x: rowRect.minX, y: rowRect.minY, width: 3, height: rowRect.height),
                    radius: 2,
                    fill: classColor,
                    stroke: nil
                )
            }
            if row.isClassHeader {
                drawText(
                    row.classHeaderTitle.isEmpty ? "Class" : row.classHeaderTitle,
                    in: NSRect(x: rowRect.minX + 10, y: rowRect.midY - 7, width: rowRect.width * 0.58, height: 14),
                    font: overlayFont(size: 10, weight: .semibold),
                    color: theme.colors.textPrimary
                )
                drawText(
                    row.classHeaderDetail,
                    in: NSRect(x: rowRect.minX + rowRect.width * 0.58, y: rowRect.midY - 7, width: rowRect.width * 0.42 - 10, height: 14),
                    font: overlayFont(size: 9.5, weight: .semibold),
                    color: theme.colors.textSecondary,
                    alignment: .right
                )
                continue
            }
            x = rowRect.minX + 8
            for (columnIndex, column) in columns.enumerated() {
                let width = max(Layout.minimumColumnWidth, column.width) * fit
                let value = columnIndex < row.values.count ? row.values[columnIndex] : ""
                drawText(
                    value,
                    in: NSRect(x: x, y: rowRect.midY - 8, width: width, height: 16),
                    font: overlayFont(size: 11.5, weight: columnIndex == 0 ? .semibold : .regular),
                    color: tableTextColor(row: row),
                    alignment: column.alignment
                )
                x += width + Layout.columnGap
            }
        }
    }

    private func tableTextColor(row: DesignV2OverlayRow) -> NSColor {
        if row.isClassHeader {
            return theme.colors.textPrimary
        }
        if row.isReference {
            return theme.colors.textPrimary
        }
        if row.evidence == .error {
            return theme.colors.error
        }
        return theme.colors.textSecondary
    }

    private func drawMetricRows(_ rows: [DesignV2OverlayMetricRow], in rect: NSRect) {
        let visibleRows = rows.prefix(max(1, Int(rect.height / (Layout.rowHeight + Layout.rowGap))))
        if visibleRows.isEmpty {
            rounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted)
            drawText("waiting", in: rect.insetBy(dx: 12, dy: 10), font: overlayFont(size: 12, weight: .semibold), color: theme.colors.textMuted)
            return
        }

        for (index, row) in visibleRows.enumerated() {
            let rowRect = NSRect(
                x: rect.minX,
                y: rect.minY + CGFloat(index) * (Layout.rowHeight + Layout.rowGap),
                width: rect.width,
                height: Layout.rowHeight
            )
            rounded(rowRect, radius: 5, fill: theme.colors.surfaceRaised, stroke: theme.colors.borderMuted.withAlphaComponent(0.34))
            drawText(
                row.label,
                in: NSRect(x: rowRect.minX + 10, y: rowRect.midY - 8, width: Layout.metricLabelWidth, height: 16),
                font: overlayFont(size: 10.5, weight: .semibold),
                color: theme.colors.textMuted
            )
            drawText(
                row.value,
                in: NSRect(x: rowRect.minX + Layout.metricLabelWidth + 12, y: rowRect.midY - 8, width: rowRect.width - Layout.metricLabelWidth - 22, height: 16),
                font: overlayFont(size: 11.5, weight: .semibold),
                color: theme.color(for: row.evidence),
                alignment: .right
            )
        }
    }

    private func drawGraph(points: [Double], in rect: NSRect) {
        rounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted)
        guard points.count > 1 else {
            drawText("waiting for trend", in: rect.insetBy(dx: 12, dy: 10), font: overlayFont(size: 12, weight: .semibold), color: theme.colors.textMuted)
            return
        }

        let minValue = points.min() ?? 0
        let maxValue = points.max() ?? 1
        let span = max(1, maxValue - minValue)
        let frame = rect.insetBy(dx: 12, dy: 14)
        let axisWidth: CGFloat = 58
        let xAxisHeight: CGFloat = 17
        let plot = NSRect(
            x: frame.minX + axisWidth,
            y: frame.minY,
            width: max(40, frame.width - axisWidth - 4),
            height: max(40, frame.height - xAxisHeight)
        )
        theme.colors.gridLine.setStroke()
        for index in 1..<4 {
            let y = plot.minY + CGFloat(index) * plot.height / 4
            NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: y), to: NSPoint(x: plot.maxX, y: y))
        }
        theme.colors.textMuted.withAlphaComponent(0.42).setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: plot.minY), to: NSPoint(x: plot.maxX, y: plot.minY))
        drawText("leader", in: NSRect(x: frame.minX, y: plot.minY - 6, width: axisWidth - 8, height: 14), font: overlayFont(size: 10, weight: .regular), color: theme.colors.textMuted, alignment: .right)
        drawText(formatAxisSeconds(maxValue), in: NSRect(x: frame.minX, y: plot.maxY - 8, width: axisWidth - 8, height: 14), font: overlayFont(size: 10, weight: .regular), color: theme.colors.textMuted, alignment: .right)
        drawText("10m", in: NSRect(x: plot.minX, y: plot.maxY + 4, width: 44, height: 14), font: overlayFont(size: 10, weight: .regular), color: theme.colors.textMuted)
        drawText("now", in: NSRect(x: plot.maxX - 44, y: plot.maxY + 4, width: 44, height: 14), font: overlayFont(size: 10, weight: .regular), color: theme.colors.textMuted, alignment: .right)

        let path = NSBezierPath()
        for (index, point) in points.enumerated() {
            let progress = CGFloat(index) / CGFloat(max(1, points.count - 1))
            let normalized = CGFloat((point - minValue) / span)
            let p = NSPoint(x: plot.minX + progress * plot.width, y: plot.maxY - normalized * plot.height)
            index == 0 ? path.move(to: p) : path.line(to: p)
        }
        theme.colors.accentPrimary.setStroke()
        path.lineWidth = 2
        path.stroke()
    }

    private func drawChat(_ rows: [DesignV2ChatRow], in rect: NSRect) {
        rounded(rect, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted)
        let visibleRows = rows.prefix(max(1, Int(rect.height / 48)))
        for (index, row) in visibleRows.enumerated() {
            let rowRect = NSRect(x: rect.minX + 8, y: rect.minY + 8 + CGFloat(index) * 48, width: rect.width - 16, height: 40)
            rounded(rowRect, radius: 5, fill: theme.colors.surfaceRaised, stroke: theme.colors.borderMuted.withAlphaComponent(0.30))
            drawText(row.author, in: NSRect(x: rowRect.minX + 10, y: rowRect.minY + 6, width: rowRect.width - 20, height: 13), font: overlayFont(size: 10, weight: .bold), color: theme.color(for: row.evidence))
            drawText(row.message, in: NSRect(x: rowRect.minX + 10, y: rowRect.minY + 21, width: rowRect.width - 20, height: 14), font: overlayFont(size: 11, weight: .regular), color: theme.colors.textSecondary)
        }
    }

    private func drawGarageCover(_ model: DesignV2GarageCoverModel, in rect: NSRect) {
        let background = NSBezierPath(rect: rect)
        theme.colors.surfaceInset.setFill()
        background.fill()

        let horizonY = rect.minY + rect.height * 0.57
        NSColor(red255: 73, green: 19, blue: 83, alpha: 0.68).setFill()
        NSRect(x: rect.minX, y: horizonY, width: rect.width, height: rect.height * 0.13).fill()
        NSColor(red255: 5, green: 19, blue: 32, alpha: 0.96).setFill()
        NSRect(x: rect.minX, y: horizonY + rect.height * 0.13, width: rect.width, height: rect.maxY - horizonY).fill()

        drawGarageGrid(in: rect, horizonY: horizonY)
        drawOutrunSun(in: NSRect(x: rect.maxX - 206, y: rect.minY + 24, width: 136, height: 136))

        let borderRect = rect.insetBy(dx: 18, dy: 18)
        rounded(borderRect, radius: 12, fill: nil, stroke: theme.colors.accentPrimary.withAlphaComponent(0.92))
        let logoRect = NSRect(x: borderRect.minX + 50, y: borderRect.minY + 54, width: 116, height: 64)
        rounded(logoRect, radius: 7, fill: theme.colors.surfaceRaised, stroke: theme.colors.accentSecondary)
        if let garageCoverLogoImage {
            drawImage(garageCoverLogoImage, containedIn: logoRect.insetBy(dx: 10, dy: 8))
        } else {
            drawText("TMR", in: logoRect.insetBy(dx: 14, dy: 8), font: overlayFont(size: 26, weight: .bold), color: theme.colors.textPrimary)
        }

        drawText(
            "Tech Mates Racing",
            in: NSRect(x: logoRect.maxX + 28, y: logoRect.minY + 1, width: min(500, borderRect.maxX - logoRect.maxX - 260), height: 24),
            font: overlayFont(size: 18, weight: .semibold),
            color: theme.colors.accentPrimary
        )
        drawText(
            "Garage Cover",
            in: NSRect(x: logoRect.maxX + 26, y: logoRect.minY + 24, width: min(500, borderRect.maxX - logoRect.maxX - 260), height: 42),
            font: overlayFont(size: max(30, rect.width / 29), weight: .bold),
            color: theme.colors.textPrimary
        )
        drawText(
            "Setup screen privacy surface",
            in: NSRect(x: logoRect.maxX + 30, y: logoRect.maxY + 4, width: 380, height: 18),
            font: overlayFont(size: 15, weight: .semibold),
            color: theme.colors.textSecondary
        )

        let alertY = rect.minY + rect.height * 0.70
        NSColor(red255: 255, green: 42, blue: 167, alpha: model.isGarageVisible || model.shouldFailClosed ? 0.78 : 0.42).setFill()
        NSRect(x: rect.minX, y: alertY, width: rect.width, height: 64).fill()
        NSColor(red255: 255, green: 209, blue: 91, alpha: 0.95).setFill()
        NSRect(x: rect.minX, y: alertY + 64, width: rect.width, height: 10).fill()
        drawText(
            model.state,
            in: NSRect(x: borderRect.minX + 50, y: alertY + 17, width: borderRect.width - 100, height: 22),
            font: overlayFont(size: 19, weight: .bold),
            color: theme.colors.textPrimary
        )
        drawText(
            model.detail,
            in: NSRect(x: borderRect.minX + 50, y: alertY + 88, width: borderRect.width - 100, height: 18),
            font: overlayFont(size: 14, weight: .semibold),
            color: theme.colors.textSecondary
        )
    }

    private func drawGarageGrid(in rect: NSRect, horizonY: CGFloat) {
        let centerX = rect.midX
        let bottomY = rect.maxY + 80
        let gridColor = theme.colors.accentSecondary.withAlphaComponent(0.28)
        gridColor.setStroke()
        for index in -8...8 {
            let endX = centerX + CGFloat(index) * rect.width / 8
            NSBezierPath.strokeLine(from: NSPoint(x: centerX, y: horizonY), to: NSPoint(x: endX, y: bottomY))
        }

        for step in 0..<8 {
            let progress = CGFloat(step) / 7
            let y = horizonY + pow(progress, 1.7) * (rect.maxY - horizonY)
            NSBezierPath.strokeLine(from: NSPoint(x: rect.minX, y: y), to: NSPoint(x: rect.maxX, y: y))
        }
    }

    private func imageContainRect(imageSize: NSSize, bounds: NSRect) -> NSRect {
        guard imageSize.width > 0,
              imageSize.height > 0,
              bounds.width > 0,
              bounds.height > 0 else {
            return bounds
        }

        let scale = min(bounds.width / imageSize.width, bounds.height / imageSize.height)
        let width = imageSize.width * scale
        let height = imageSize.height * scale
        return NSRect(
            x: bounds.midX - width / 2,
            y: bounds.midY - height / 2,
            width: width,
            height: height
        )
    }

    private func drawImage(_ image: NSImage, containedIn bounds: NSRect) {
        let target = imageContainRect(imageSize: image.size, bounds: bounds)
        NSGraphicsContext.saveGraphicsState()
        if isFlipped {
            let transform = NSAffineTransform()
            transform.translateX(by: 0, yBy: target.maxY + target.minY)
            transform.scaleX(by: 1, yBy: -1)
            transform.concat()
        }
        image.draw(
            in: target,
            from: .zero,
            operation: .sourceOver,
            fraction: 1
        )
        NSGraphicsContext.restoreGraphicsState()
    }

    private func drawOutrunSun(in rect: NSRect) {
        let path = NSBezierPath(ovalIn: rect)
        NSGraphicsContext.saveGraphicsState()
        path.addClip()
        let gradient = NSGradient(colors: [
            theme.colors.accentTertiary,
            NSColor(red255: 255, green: 42, blue: 167, alpha: 0.92),
            theme.colors.accentTertiary
        ])
        gradient?.draw(in: rect, angle: 90)
        theme.colors.surfaceInset.setFill()
        for index in 0..<5 {
            let y = rect.minY + rect.height * (0.36 + CGFloat(index) * 0.11)
            NSRect(x: rect.minX, y: y, width: rect.width, height: 8).fill()
        }
        NSGraphicsContext.restoreGraphicsState()
    }

    private func drawRadar(_ model: DesignV2RadarModel, in rect: NSRect) {
        theme.colors.surface.withAlphaComponent(0.92).setFill()
        NSBezierPath(ovalIn: rect.insetBy(dx: 4, dy: 4)).fill()
        let radarRect = rect.insetBy(dx: 4, dy: 4)
        theme.colors.accentPrimary.setStroke()
        let outer = NSBezierPath(ovalIn: radarRect)
        outer.lineWidth = 2
        outer.stroke()

        drawText("CAR RADAR", in: NSRect(x: rect.minX + 24, y: rect.minY + 22, width: 110, height: 16), font: overlayFont(size: 12, weight: .bold), color: theme.colors.textPrimary)
        let statusText = model.isAvailable ? radarStatusText(model.proximity) : "WAITING"
        drawText(statusText, in: NSRect(x: rect.maxX - 98, y: rect.minY + 20, width: 78, height: 16), font: overlayFont(size: 10, weight: .bold), color: model.isAvailable ? theme.colors.error : theme.colors.textMuted, alignment: .right)

        let center = NSPoint(x: radarRect.midX, y: radarRect.midY)
        theme.colors.gridLine.setStroke()
        for fraction in [CGFloat(1.0 / 3.0), CGFloat(2.0 / 3.0)] {
            let inset = radarRect.width * fraction / 2
            NSBezierPath(ovalIn: radarRect.insetBy(dx: inset, dy: inset)).stroke()
        }
        NSBezierPath.strokeLine(from: NSPoint(x: radarRect.minX, y: center.y), to: NSPoint(x: radarRect.maxX, y: center.y))
        NSBezierPath.strokeLine(from: NSPoint(x: center.x, y: radarRect.minY), to: NSPoint(x: center.x, y: radarRect.maxY))

        let cars = radarCars(model.proximity, isAvailable: model.isAvailable)
        drawRadarCars(cars, in: radarRect)
        if model.proximity.hasCarLeft || !model.isAvailable {
            drawRadarCar(NSRect(x: center.x - 94, y: center.y - 28, width: 28, height: 58), color: theme.colors.error)
        }
        if model.proximity.hasCarRight || !model.isAvailable {
            drawRadarCar(NSRect(x: center.x + 66, y: center.y - 64, width: 28, height: 58), color: theme.colors.accentPrimary)
        }
        drawRadarCar(NSRect(x: center.x - 12, y: center.y - 24, width: 24, height: 48), color: theme.colors.textPrimary)
    }

    private func drawRadarCars(_ cars: [LiveProximityCar], in rect: NSRect) {
        let center = NSPoint(x: rect.midX, y: rect.midY)
        let usableRadius = rect.width / 2 - 48
        for (index, car) in cars.prefix(8).enumerated() {
            let seconds = car.relativeSeconds ?? car.relativeLaps * 120
            let normalized = min(max(seconds / 2.0, -1), 1)
            let lane = CGFloat((index % 3) - 1) * 36
            let x = center.x + lane
            let y = center.y - CGFloat(normalized) * usableRadius
            let base = OverlayClassColor.color(car.carClassColorHex) ?? (abs(normalized) < 0.35 ? theme.colors.error : theme.colors.history)
            drawRadarCar(NSRect(x: x - 12, y: y - 25, width: 24, height: 50), color: base)
        }
    }

    private func drawRadarCar(_ rect: NSRect, color: NSColor) {
        rounded(rect, radius: 6, fill: color.withAlphaComponent(0.96), stroke: theme.colors.textPrimary.withAlphaComponent(0.28))
    }

    private func radarCars(_ proximity: LiveProximitySnapshot, isAvailable: Bool) -> [LiveProximityCar] {
        guard isAvailable, !proximity.nearbyCars.isEmpty else {
            return [
                LiveProximityCar(carIdx: 12, relativeLaps: 0.014, relativeSeconds: 1.2, relativeMeters: nil, overallPosition: 6, classPosition: 5, carClass: 4098, carClassColorHex: "#FFDA59", onPitRoad: false),
                LiveProximityCar(carIdx: 51, relativeLaps: -0.065, relativeSeconds: -3.4, relativeMeters: nil, overallPosition: 3, classPosition: 1, carClass: 4099, carClassColorHex: "#33CEFF", onPitRoad: false)
            ]
        }

        return proximity.nearbyCars
    }

    private func radarStatusText(_ proximity: LiveProximitySnapshot) -> String {
        if let approach = proximity.strongestMulticlassApproach,
           let seconds = approach.relativeSeconds {
            return String(format: "%.1fs GTP", abs(seconds))
        }
        if proximity.hasCarLeft || proximity.hasCarRight {
            return "SIDE"
        }
        return "CLEAR"
    }

    private func drawInputs(_ model: DesignV2InputModel, in rect: NSRect) {
        rounded(rect, radius: 8, fill: theme.colors.surface, stroke: theme.colors.accentPrimary.withAlphaComponent(0.92))
        let headerHeight: CGFloat = 38
        theme.colors.titleBar.setFill()
        NSRect(x: rect.minX, y: rect.minY, width: rect.width, height: headerHeight).fill()
        NSColor(red255: 255, green: 42, blue: 167, alpha: 0.95).setFill()
        NSRect(x: rect.minX, y: rect.minY + headerHeight - 2, width: rect.width, height: 2).fill()
        drawText("Inputs", in: NSRect(x: rect.minX + 14, y: rect.minY + 10, width: 100, height: 16), font: overlayFont(size: 13, weight: .bold), color: theme.colors.textPrimary)
        let livePill = NSRect(x: rect.maxX - 92, y: rect.minY + 8, width: 76, height: 20)
        rounded(livePill, radius: 10, fill: theme.colors.accentPrimary.withAlphaComponent(0.24), stroke: theme.colors.accentPrimary.withAlphaComponent(0.45))
        drawText(model.isAvailable ? "LIVE" : "WAIT", in: livePill.insetBy(dx: 10, dy: 4), font: overlayFont(size: 9, weight: .bold), color: theme.colors.accentPrimary, alignment: .center)

        let content = NSRect(x: rect.minX + 18, y: rect.minY + headerHeight + 18, width: rect.width - 36, height: rect.height - headerHeight - 34)
        let railWidth: CGFloat = inputRailVisible ? 204 : 0
        let graph = NSRect(x: content.minX, y: content.minY + 6, width: max(160, content.width - railWidth - 18), height: content.height - 12)
        rounded(graph, radius: 5, fill: theme.colors.surfaceInset, stroke: theme.colors.borderMuted)
        drawInputGrid(in: graph)
        if inputBlockEnabled(OverlayContentColumns.inputThrottleTraceBlockId) {
            drawInputTrace(model.trace, in: graph, color: theme.colors.live) { $0.throttle }
        }
        if inputBlockEnabled(OverlayContentColumns.inputBrakeTraceBlockId) {
            drawInputTrace(model.trace, in: graph, color: theme.colors.error) { $0.brake }
            drawInputAbsTrace(model.trace, in: graph)
        }
        if inputBlockEnabled(OverlayContentColumns.inputClutchTraceBlockId) {
            drawInputTrace(model.trace, in: graph, color: theme.colors.accentPrimary) { $0.clutch }
        }

        if inputRailVisible {
            let rail = NSRect(x: graph.maxX + 18, y: content.minY, width: railWidth, height: content.height)
            drawInputRail(model, in: rail)
        }
    }

    private func drawInputGrid(in rect: NSRect) {
        theme.colors.gridLine.setStroke()
        for step in 1..<4 {
            let y = rect.minY + rect.height * CGFloat(step) / 4
            NSBezierPath.strokeLine(from: NSPoint(x: rect.minX, y: y), to: NSPoint(x: rect.maxX, y: y))
        }
    }

    private func drawInputTrace(_ trace: [DesignV2InputPoint], in rect: NSRect, color: NSColor, select: (DesignV2InputPoint) -> Double) {
        guard trace.count > 1 else {
            drawInputWaitingTrace(in: rect, color: color)
            return
        }

        let maximumIndex = max(1, trace.count - 1)
        let points = trace.enumerated().map { index, point in
            let value = min(max(select(point), 0), 1)
            return NSPoint(
                x: rect.minX + CGFloat(index) / CGFloat(maximumIndex) * rect.width,
                y: rect.maxY - CGFloat(value) * rect.height
            )
        }
        let path = smoothInputTracePath(points)
        color.setStroke()
        path.lineWidth = 2
        path.lineJoinStyle = .round
        path.lineCapStyle = .round
        path.stroke()
    }

    private func smoothInputTracePath(_ points: [NSPoint]) -> NSBezierPath {
        let path = NSBezierPath()
        guard let first = points.first else {
            return path
        }

        path.move(to: first)
        guard points.count > 2 else {
            for point in points.dropFirst() {
                path.line(to: point)
            }
            return path
        }

        for index in 0..<(points.count - 1) {
            let p0 = index == 0 ? points[index] : points[index - 1]
            let p1 = points[index]
            let p2 = points[index + 1]
            let p3 = index + 2 < points.count ? points[index + 2] : p2
            let c1 = NSPoint(
                x: p1.x + (p2.x - p0.x) / 6,
                y: p1.y + (p2.y - p0.y) / 6
            )
            let c2 = NSPoint(
                x: p2.x - (p3.x - p1.x) / 6,
                y: p2.y - (p3.y - p1.y) / 6
            )
            path.curve(to: p2, controlPoint1: c1, controlPoint2: c2)
        }

        return path
    }

    private func drawInputWaitingTrace(in rect: NSRect, color: NSColor) {
        color.withAlphaComponent(0.24).setStroke()
        let y = rect.midY
        NSBezierPath.strokeLine(from: NSPoint(x: rect.minX + 8, y: y), to: NSPoint(x: rect.maxX - 8, y: y))
    }

    private func drawInputAbsTrace(_ trace: [DesignV2InputPoint], in rect: NSRect) {
        guard trace.count > 1 else {
            return
        }

        let maximumIndex = max(1, trace.count - 1)
        theme.colors.history.setStroke()
        for index in 1..<trace.count where trace[index].brakeAbsActive {
            let previous = trace[index - 1]
            let current = trace[index]
            let path = NSBezierPath()
            path.lineWidth = 3
            path.lineCapStyle = .round
            path.move(to: NSPoint(
                x: rect.minX + CGFloat(index - 1) / CGFloat(maximumIndex) * rect.width,
                y: rect.maxY - CGFloat(min(max(previous.brake, 0), 1)) * rect.height
            ))
            path.line(to: NSPoint(
                x: rect.minX + CGFloat(index) / CGFloat(maximumIndex) * rect.width,
                y: rect.maxY - CGFloat(min(max(current.brake, 0), 1)) * rect.height
            ))
            path.stroke()
        }
    }

    private func drawInputRail(_ model: DesignV2InputModel, in rect: NSRect) {
        var y = rect.minY
        if inputBlockEnabled(OverlayContentColumns.inputThrottleBlockId) {
            drawInputBar(label: "THR", value: model.throttle, color: theme.colors.live, in: NSRect(x: rect.minX, y: y, width: rect.width, height: 27))
            y += 42
        }
        if inputBlockEnabled(OverlayContentColumns.inputBrakeBlockId) {
            drawInputBar(label: "BRK", value: model.brake, color: model.brakeAbsActive ? theme.colors.history : theme.colors.error, in: NSRect(x: rect.minX, y: y, width: rect.width, height: 27))
            y += 42
        }
        if inputBlockEnabled(OverlayContentColumns.inputClutchBlockId) {
            drawInputBar(label: "CLT", value: model.clutch, color: theme.colors.accentPrimary, in: NSRect(x: rect.minX, y: y, width: rect.width, height: 27))
            y += 42
        }

        let readouts = [
            inputBlockEnabled(OverlayContentColumns.inputGearBlockId) ? ("GEAR", model.gear > 0 ? "\(model.gear)" : "--") : nil,
            inputBlockEnabled(OverlayContentColumns.inputSpeedBlockId) ? ("SPD", formatSpeed(model.speedMetersPerSecond)) : nil,
            inputBlockEnabled(OverlayContentColumns.inputSteeringBlockId) ? ("STR", String(format: "%+.0f deg", model.steeringDegrees)) : nil
        ].compactMap { $0 }
        let readoutTop = max(y + 4, rect.maxY - CGFloat(max(1, readouts.count)) * 28)
        for (index, row) in readouts.enumerated() {
            drawInputReadout(row.0, value: row.1, in: NSRect(x: rect.minX, y: readoutTop + CGFloat(index) * 28, width: rect.width, height: 20))
        }
    }

    private func drawInputBar(label: String, value: Double, color: NSColor, in rect: NSRect) {
        drawText(label, in: NSRect(x: rect.minX, y: rect.minY + 3, width: 42, height: 14), font: overlayFont(size: 10, weight: .bold), color: theme.colors.textMuted)
        let bar = NSRect(x: rect.minX + 48, y: rect.minY + 4, width: rect.width - 48, height: 12)
        rounded(bar, radius: 6, fill: theme.colors.surfaceRaised, stroke: nil)
        rounded(NSRect(x: bar.minX, y: bar.minY, width: bar.width * CGFloat(min(max(value, 0), 1)), height: bar.height), radius: 6, fill: color, stroke: nil)
    }

    private func drawInputReadout(_ label: String, value: String, in rect: NSRect) {
        drawText(label, in: NSRect(x: rect.minX, y: rect.minY + 3, width: 42, height: 14), font: overlayFont(size: 9, weight: .bold), color: theme.colors.textMuted)
        drawText(value, in: NSRect(x: rect.minX + 50, y: rect.minY, width: rect.width - 50, height: 18), font: overlayFont(size: 13, weight: .bold), color: theme.colors.textPrimary, alignment: .right)
    }

    private func drawTrackMap(_ model: DesignV2TrackMapModel, in rect: NSRect) {
        let size = max(20, min(rect.width, rect.height) - 40)
        let trackRect = NSRect(
            x: rect.midX - size / 2,
            y: rect.midY - size / 2,
            width: size,
            height: size
        )
        let fillOpacity = min(max(CGFloat(sourceSettings.opacity), 0.2), 1.0)
        NSColor(red: 0.035, green: 0.055, blue: 0.071, alpha: 0.59 * fillOpacity).setFill()
        NSBezierPath(ovalIn: trackRect).fill()

        let halo = NSBezierPath(ovalIn: trackRect)
        halo.lineWidth = 11
        NSColor.white.withAlphaComponent(0.32).setStroke()
        halo.stroke()

        let line = NSBezierPath(ovalIn: trackRect)
        line.lineWidth = 4.4
        NSColor(red: 0.87, green: 0.93, blue: 0.96, alpha: 1.0).setStroke()
        line.stroke()

        drawTrackMapSectorHighlights(model.snapshot.models.trackMap.sectors, in: trackRect)
        if trackMapSectorBoundariesVisible {
            drawTrackMapSectorBoundaries(model.snapshot.models.trackMap.sectors, in: trackRect)
        }
        drawTrackMapMarkers(trackMapMarkers(model.snapshot), in: trackRect)
    }

    private func drawTrackMapSectorHighlights(_ sectors: [LiveTrackSectorSegment], in rect: NSRect) {
        for sector in sectors where hasTrackMapHighlight(sector.highlight) {
            trackMapSectorHighlightColor(sector.highlight).setStroke()
            for range in trackMapSegmentRanges(startPct: sector.startPct, endPct: sector.endPct) {
                let path = NSBezierPath()
                path.lineWidth = 5.8
                path.lineCapStyle = .round
                path.appendArc(
                    withCenter: NSPoint(x: rect.midX, y: rect.midY),
                    radius: min(rect.width, rect.height) / 2,
                    startAngle: range.start * 360 - 90,
                    endAngle: range.end * 360 - 90,
                    clockwise: false
                )
                path.stroke()
            }
        }
    }

    private func drawTrackMapSectorBoundaries(_ sectors: [LiveTrackSectorSegment], in rect: NSRect) {
        guard sectors.count >= 2 else {
            return
        }

        for boundary in trackMapBoundaryMarkers(sectors) {
            let progress = boundary.progress
            trackMapBoundaryColor(boundary.highlight).setStroke()
            let point = trackMapPoint(on: rect, progress: progress)
            let dx = point.x - rect.midX
            let dy = point.y - rect.midY
            let length = max(0.001, sqrt(dx * dx + dy * dy))
            let unitX = dx / length
            let unitY = dy / length
            let half: CGFloat = 8.5
            let path = NSBezierPath()
            path.lineWidth = 2.2
            path.lineCapStyle = .round
            path.move(to: NSPoint(x: point.x - unitX * half, y: point.y - unitY * half))
            path.line(to: NSPoint(x: point.x + unitX * half, y: point.y + unitY * half))
            path.stroke()
        }
    }

    private func drawTrackMapMarkers(_ markers: [DesignV2TrackMarker], in rect: NSRect) {
        for marker in markers.sorted(by: { lhs, rhs in
            if lhs.isFocus != rhs.isFocus {
                return !lhs.isFocus && rhs.isFocus
            }
            return lhs.carIdx < rhs.carIdx
        }) {
            let point = trackMapPoint(on: rect, progress: marker.lapDistPct)
            let radius = marker.isFocus ? max(5.7, CGFloat(marker.positionLabel?.count ?? 0) * 2.9 + 5.1) : 3.6
            let markerRect = NSRect(x: point.x - radius, y: point.y - radius, width: radius * 2, height: radius * 2)
            let path = NSBezierPath(ovalIn: markerRect)
            marker.color.setFill()
            path.fill()
            path.lineWidth = marker.isFocus ? 2 : 1.4
            NSColor(red: 0.03, green: 0.055, blue: 0.07, alpha: 0.90).setStroke()
            path.stroke()
            if marker.isFocus, let label = marker.positionLabel {
                drawText(
                    label,
                    in: markerRect.insetBy(dx: 1, dy: markerRect.height / 2 - 5),
                    font: overlayFont(size: 7.6, weight: .bold),
                    color: NSColor(red: 0.02, green: 0.05, blue: 0.065, alpha: 1.0),
                    alignment: .center
                )
            }
        }
    }

    private func buildModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        if let overlayError {
            return DesignV2OverlayModel(title: kind.title, status: "overlay error", footer: trim(overlayError), evidence: .error, body: .metricRows([]))
        }

        switch kind {
        case .standings:
            return standingsModel(snapshot)
        case .gapToLeader:
            return gapModel(snapshot)
        case .fuelCalculator:
            return fuelModel(snapshot)
        case .pitService:
            return pitServiceModel(snapshot)
        case .sessionWeather:
            return sessionWeatherModel(snapshot)
        case .streamChat:
            return streamChatModel()
        case .garageCover:
            return garageCoverModel(snapshot)
        case .carRadar:
            return radarModel(snapshot)
        case .inputState:
            return inputModel(snapshot)
        case .trackMap:
            return trackMapModel(snapshot)
        }
    }

    private func standingsModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        guard snapshot.isConnected, snapshot.isCollecting, !snapshot.leaderGap.classCars.isEmpty else {
            return DesignV2OverlayModel(title: "Standings", status: "waiting for timing", footer: "source: waiting", evidence: .unavailable, body: .table(columns: standingsColumns(), rows: []))
        }

        let referenceGap = snapshot.leaderGap.classCars.first(where: { $0.isReferenceCar })?.gapSecondsToClassLeader
            ?? snapshot.leaderGap.classLeaderGap.seconds
            ?? 0
        let rows = standingsRows(snapshot: snapshot, referenceGap: referenceGap)
        let reference = snapshot.leaderGap.classCars.first(where: { $0.isReferenceCar })
        let status = reference?.classPosition.map { "class \($0) - \(rows.count) rows" } ?? "\(rows.count) rows"
        return DesignV2OverlayModel(title: "Standings", status: status, footer: "source: live class timing", evidence: .live, body: .table(columns: standingsColumns(), rows: rows))
    }

    private func standingsRows(snapshot: LiveTelemetrySnapshot, referenceGap: Double) -> [DesignV2OverlayRow] {
        let sortedCars = snapshot.leaderGap.classCars
            .sorted { ($0.classPosition ?? Int.max, $0.carIdx) < ($1.classPosition ?? Int.max, $1.carIdx) }
        guard standingsClassSeparatorsEnabled else {
            return sortedCars.prefix(8).map { standingsRow(car: $0, snapshot: snapshot, referenceGap: referenceGap) }
        }

        let referenceClass = referenceCarClass(snapshot)
        let otherClassRows = otherClassStandingsRows(snapshot: snapshot, referenceClass: referenceClass)
            .prefix(max(0, standingsOtherClassRows))
        let primaryLimit = otherClassRows.isEmpty ? 7 : 4
        var rows: [DesignV2OverlayRow] = [
            standingsClassHeader(
                className: referenceClassName(snapshot),
                rowCount: sortedCars.count,
                estimatedLaps: estimatedLaps(snapshot.latestFrame?.sessionTimeRemain, lapSeconds: snapshot.latestFrame?.estimatedLapSeconds),
                colorHex: sortedCars.first?.carClassColorHex ?? FourHourRacePreview.teamClassColorHex
            )
        ]
        rows.append(contentsOf: primaryStandingsCars(sortedCars, limit: primaryLimit).map {
            standingsRow(car: $0, snapshot: snapshot, referenceGap: referenceGap)
        })

        if !otherClassRows.isEmpty {
            let otherClassName = snapshot.proximity.nearbyCars
                .first { isOtherClass($0.carClass, referenceClass: referenceClass) }?
                .carClassName
                ?? "Other"
            rows.append(
                standingsClassHeader(
                    className: otherClassName,
                    rowCount: max(otherClassRows.count, snapshot.proximity.nearbyCars.filter { isOtherClass($0.carClass, referenceClass: referenceClass) }.count),
                    estimatedLaps: estimatedLaps(snapshot.latestFrame?.sessionTimeRemain, lapSeconds: (snapshot.latestFrame?.estimatedLapSeconds).map { $0 * 0.76 }),
                    colorHex: otherClassRows.first?.classColorHex ?? "#33CEFF"
                )
            )
            rows.append(contentsOf: otherClassRows)
        }

        return Array(rows.prefix(8))
    }

    private func primaryStandingsCars(_ cars: [LiveClassGapCar], limit: Int) -> [LiveClassGapCar] {
        let prefixCars = Array(cars.prefix(max(0, limit)))
        guard let reference = cars.first(where: { $0.isReferenceCar }),
              !prefixCars.contains(where: { $0.carIdx == reference.carIdx }),
              limit > 0 else {
            return prefixCars
        }

        var visibleCars = Array(prefixCars.dropLast())
        visibleCars.append(reference)
        return visibleCars.sorted {
            ($0.classPosition ?? Int.max, $0.carIdx) < ($1.classPosition ?? Int.max, $1.carIdx)
        }
    }

    private func standingsRow(car: LiveClassGapCar, snapshot: LiveTelemetrySnapshot, referenceGap: Double) -> DesignV2OverlayRow {
        DesignV2OverlayRow(
            values: standingsValues(car: car, snapshot: snapshot, referenceGap: referenceGap),
            evidence: car.isClassLeader ? .live : .measured,
            isReference: car.isReferenceCar,
            classColorHex: car.carClassColorHex
        )
    }

    private func standingsClassHeader(
        className: String,
        rowCount: Int,
        estimatedLaps: String,
        colorHex: String?
    ) -> DesignV2OverlayRow {
        let valuesByKey: [String: String] = [
            OverlayContentColumns.dataClassPosition: "",
            OverlayContentColumns.dataCarNumber: "",
            OverlayContentColumns.dataDriver: className,
            OverlayContentColumns.dataGap: "\(rowCount) cars",
            OverlayContentColumns.dataInterval: estimatedLaps,
            OverlayContentColumns.dataPit: ""
        ]
        return DesignV2OverlayRow(
            values: standingsValues(valuesByKey: valuesByKey),
            evidence: .live,
            isClassHeader: true,
            classColorHex: colorHex,
            classHeaderTitle: className,
            classHeaderDetail: "\(rowCount) cars | \(estimatedLaps)"
        )
    }

    private func otherClassStandingsRows(snapshot: LiveTelemetrySnapshot, referenceClass: Int?) -> [DesignV2OverlayRow] {
        let cars = snapshot.proximity.nearbyCars
            .filter { isOtherClass($0.carClass, referenceClass: referenceClass) }
            .sorted { abs($0.relativeLaps) < abs($1.relativeLaps) }

        let sourceCars = cars.isEmpty
            ? [
                LiveProximityCar(
                    carIdx: 51,
                    relativeLaps: -0.08,
                    relativeSeconds: -18,
                    relativeMeters: nil,
                    overallPosition: 3,
                    classPosition: 1,
                    carClass: 4099,
                    carClassColorHex: "#33CEFF",
                    onPitRoad: false
                )
            ]
            : cars

        return sourceCars.map { car in
            let valuesByKey: [String: String] = [
                OverlayContentColumns.dataClassPosition: car.classPosition.map { "\($0)" } ?? "--",
                OverlayContentColumns.dataCarNumber: car.carNumber.map { $0.hasPrefix("#") ? $0 : "#\($0)" } ?? "#\(car.carIdx)",
                OverlayContentColumns.dataDriver: car.driverName ?? MockDriverNames.displayName(for: car.carIdx),
                OverlayContentColumns.dataGap: car.classPosition == 1 ? "Leader" : "--",
                OverlayContentColumns.dataInterval: car.relativeSeconds.map { String(format: "%+.1f", $0) } ?? "--",
                OverlayContentColumns.dataPit: car.onPitRoad == true ? "IN" : ""
            ]
            return DesignV2OverlayRow(
                values: standingsValues(valuesByKey: valuesByKey),
                evidence: .measured,
                classColorHex: car.carClassColorHex
            )
        }
    }

    private func standingsColumns() -> [DesignV2OverlayColumn] {
        OverlayContentColumns.visibleColumnStates(for: OverlayContentColumns.standings, settings: sourceSettings).map {
            DesignV2OverlayColumn(label: $0.definition.label, width: CGFloat($0.width), alignment: textAlignment($0.definition.alignment))
        }
    }

    private func standingsValues(car: LiveClassGapCar, snapshot: LiveTelemetrySnapshot, referenceGap: Double) -> [String] {
        let valuesByKey: [String: String] = [
            OverlayContentColumns.dataClassPosition: car.classPosition.map { "\($0)" } ?? "--",
            OverlayContentColumns.dataCarNumber: car.carNumber.map { $0.hasPrefix("#") ? $0 : "#\($0)" }
                ?? (car.carIdx == FourHourRacePreview.teamCarIdx ? "#44" : "#\(car.carIdx)"),
            OverlayContentColumns.dataDriver: standingsDriverName(car: car, snapshot: snapshot),
            OverlayContentColumns.dataGap: formatLeaderGap(car),
            OverlayContentColumns.dataInterval: intervalText(car.deltaSecondsToReference, referenceGap: referenceGap, isReference: car.isReferenceCar),
            OverlayContentColumns.dataPit: pitText(car: car, snapshot: snapshot)
        ]
        return standingsValues(valuesByKey: valuesByKey)
    }

    private func standingsDriverName(car: LiveClassGapCar, snapshot: LiveTelemetrySnapshot) -> String {
        if let driverName = car.driverName, !driverName.isEmpty {
            return driverName
        }
        if let teamName = car.teamName, !teamName.isEmpty {
            return teamName
        }
        if car.isReferenceCar {
            return snapshot.latestFrame?.teamDriverName ?? "TMR"
        }
        return MockDriverNames.displayName(for: car.carIdx)
    }

    private func referenceCarClass(_ snapshot: LiveTelemetrySnapshot) -> Int? {
        snapshot.latestFrame?.capturedReferenceCar?.carClass
            ?? snapshot.leaderGap.classCars.first(where: { $0.isReferenceCar })?.carClass
            ?? 4098
    }

    private func referenceClassName(_ snapshot: LiveTelemetrySnapshot) -> String {
        snapshot.latestFrame?.capturedReferenceCar?.carClassName
            ?? snapshot.leaderGap.classCars.first(where: { $0.isReferenceCar })?.carClassName
            ?? "GT3"
    }

    private func isOtherClass(_ carClass: Int?, referenceClass: Int?) -> Bool {
        guard let carClass else {
            return false
        }

        return referenceClass.map { carClass != $0 } ?? false
    }

    private func standingsValues(valuesByKey: [String: String]) -> [String] {
        return OverlayContentColumns.visibleColumnStates(for: OverlayContentColumns.standings, settings: sourceSettings).map {
            valuesByKey[$0.definition.dataKey] ?? ""
        }
    }

    private func gapModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        let gap = snapshot.leaderGap
        if gap.hasData, let value = gap.classLeaderGap.seconds, value.isFinite {
            gapPoints.append(value)
            if gapPoints.count > 120 {
                gapPoints.removeFirst(gapPoints.count - 120)
            }
        }

        guard gap.hasData else {
            return DesignV2OverlayModel(title: "Focused Gap Trend", status: "waiting", footer: "source: waiting", evidence: .unavailable, body: .graph(points: gapPoints))
        }

        let status = "\(positionText(gap.referenceClassPosition)) \(gapText(gap.classLeaderGap))"
        let lapSeconds = snapshot.latestFrame?.estimatedLapSeconds
        let footer = "10m rolling focused | lap \(formatPlainSeconds(lapSeconds)) | range +/-\(formatPlainSeconds(filteredGapRangeSeconds(lapSeconds: lapSeconds))) | cars \(gap.classCars.count)"
        return DesignV2OverlayModel(title: "Focused Gap Trend", status: status, footer: footer, evidence: .live, body: .graph(points: gapPoints))
    }

    private func fuelModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        let history = lookupHistory(snapshot.combo)
        let strategy = FuelStrategyCalculator.make(from: snapshot, history: history)
        let rows = fuelRows(strategy).map {
            DesignV2OverlayMetricRow(label: $0.label, value: sourceSettings.showFuelAdvice && !$0.advice.isEmpty ? "\($0.value) | \($0.advice)" : $0.value, evidence: strategy.hasData ? .modeled : .unavailable)
        }
        let footer = fuelSourceText(strategy, history: history)
        return DesignV2OverlayModel(title: "Fuel Calculator", status: strategy.status, footer: footer, evidence: strategy.hasData ? .modeled : .unavailable, body: .metricRows(rows))
    }

    private func pitServiceModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        guard snapshot.isConnected, snapshot.isCollecting, let frame = snapshot.latestFrame else {
            return DesignV2OverlayModel(title: "Pit Service", status: "waiting for telemetry", footer: "source: waiting", evidence: .unavailable, body: .metricRows([]))
        }

        let inPitWindow = frame.onPitRoad || pitWindow(frame.sessionTime)
        let fuelRequest = max(0, min(frame.fuelMaxLiters, frame.fuelMaxLiters - frame.fuelLevelLiters))
        let rows = [
            DesignV2OverlayMetricRow(label: "Release", value: inPitWindow ? "RED - service active" : "armed", evidence: inPitWindow ? .error : .live),
            DesignV2OverlayMetricRow(label: "Location", value: inPitWindow ? "team on pit road" : "off pit road", evidence: inPitWindow ? .error : .measured),
            DesignV2OverlayMetricRow(label: "Service", value: inPitWindow ? "active | tires, fuel" : "requested | fuel", evidence: .live),
            DesignV2OverlayMetricRow(label: "Fuel request", value: fuelVolume(fuelRequest), evidence: .measured),
            DesignV2OverlayMetricRow(label: "Repair", value: "--", evidence: .unavailable),
            DesignV2OverlayMetricRow(label: "Tires", value: inPitWindow ? "four tires | 2 sets used" : "none", evidence: inPitWindow ? .partial : .measured),
            DesignV2OverlayMetricRow(label: "Fast repair", value: "local 0 | team 0", evidence: .measured)
        ]
        return DesignV2OverlayModel(title: "Pit Service", status: inPitWindow ? "hold" : "service requested", footer: "source: player/team pit service telemetry", evidence: inPitWindow ? .error : .live, body: .metricRows(rows))
    }

    private func sessionWeatherModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        guard snapshot.isConnected, snapshot.isCollecting, let frame = snapshot.latestFrame else {
            return DesignV2OverlayModel(title: "Session / Weather", status: "waiting for telemetry", footer: "source: waiting", evidence: .unavailable, body: .metricRows([]))
        }

        let wet = frame.weatherDeclaredWet || frame.trackWetness > 1
        let rows = [
            DesignV2OverlayMetricRow(label: "Session", value: "Race | team", evidence: .live),
            DesignV2OverlayMetricRow(label: "Clock", value: "\(formatDuration(frame.sessionTime)) elapsed | \(formatDuration(frame.sessionTimeRemain)) left", evidence: .live),
            DesignV2OverlayMetricRow(label: "Laps", value: "\(max(0, 30 - frame.teamLapCompleted)) left | 30 total", evidence: .modeled),
            DesignV2OverlayMetricRow(label: "Track", value: "Nurburgring Combined | 24.36 km", evidence: .measured),
            DesignV2OverlayMetricRow(label: "Temps", value: "air \(temperature(21.5 + sin(frame.sessionTime / 300) * 2)) | track \(temperature(28 + sin(frame.sessionTime / 240) * 4))", evidence: .live),
            DesignV2OverlayMetricRow(label: "Surface", value: wet ? "wet | declared wet | rubber moderate" : "dry | rubber moderate", evidence: wet ? .partial : .measured),
            DesignV2OverlayMetricRow(label: "Sky", value: wet ? "overcast | rain 65%" : "partly cloudy | rain 12%", evidence: .live),
            DesignV2OverlayMetricRow(label: "Wind", value: "NW \(Int((3.6 + sin(frame.sessionTime / 420) * 1.2) * 3.6)) km/h", evidence: .live)
        ]
        return DesignV2OverlayModel(title: "Session / Weather", status: wet ? "declared wet" : "Race", footer: "source: session + weather telemetry", evidence: wet ? .partial : .live, body: .metricRows(rows))
    }

    private func streamChatModel() -> DesignV2OverlayModel {
        let provider = sourceSettings.streamChatProvider.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        let status: String
        let rows: [DesignV2ChatRow]
        switch provider {
        case "twitch":
            let channel = sourceSettings.streamChatTwitchChannel.trimmingCharacters(in: .whitespacesAndNewlines)
            status = channel.isEmpty ? "Twitch not configured" : "Twitch #\(channel)"
            rows = [
                DesignV2ChatRow(author: "TMR", message: channel.isEmpty ? "Add a Twitch channel in settings." : "Connecting to #\(channel)...", evidence: channel.isEmpty ? .unavailable : .live),
                DesignV2ChatRow(author: "spotterbot", message: "Fuel window opens in 3 laps.", evidence: .modeled),
                DesignV2ChatRow(author: "crew", message: "Traffic after the stop should clear.", evidence: .history)
            ]
        case "streamlabs":
            status = sourceSettings.streamChatStreamlabsUrl.isEmpty ? "Streamlabs not configured" : "Streamlabs"
            rows = [
                DesignV2ChatRow(author: "TMR", message: sourceSettings.streamChatStreamlabsUrl.isEmpty ? "Add a Streamlabs URL in settings." : "Streamlabs browser source configured.", evidence: sourceSettings.streamChatStreamlabsUrl.isEmpty ? .unavailable : .live),
                DesignV2ChatRow(author: "viewer", message: "Great recovery stint.", evidence: .live),
                DesignV2ChatRow(author: "mod", message: "Replay queued after race.", evidence: .measured)
            ]
        default:
            status = "not configured"
            rows = [
                DesignV2ChatRow(author: "TMR", message: "Choose Twitch or Streamlabs in settings.", evidence: .unavailable),
                DesignV2ChatRow(author: "system", message: "Chat overlay remains quiet until configured.", evidence: .measured)
            ]
        }

        return DesignV2OverlayModel(title: "Stream Chat", status: status, footer: "source: stream chat settings", evidence: provider == "none" || provider.isEmpty ? .unavailable : .live, body: .chat(rows))
    }

    private func garageCoverModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        let isGarageVisible = snapshot.latestFrame?.isGarageVisible == true
        let shouldFailClosed = !snapshot.isConnected || !snapshot.isCollecting || snapshot.latestFrame == nil
        let state: String
        let detail: String
        let evidence: DesignV2EvidenceKind
        if isGarageVisible {
            state = "LIVE GARAGE DETECTED"
            detail = "TMR Overlay fails closed until iRacing returns to the car."
            evidence = .live
        } else if shouldFailClosed {
            state = "PRIVACY COVER ACTIVE"
            detail = "Telemetry is unavailable, so the cover remains visible."
            evidence = .partial
        } else {
            state = "READY"
            detail = "Hidden during live driving; visible for garage/setup screens."
            evidence = .measured
        }

        return DesignV2OverlayModel(
            title: "Garage Cover",
            status: isGarageVisible || shouldFailClosed ? "cover visible" : "hidden while driving",
            footer: detail,
            evidence: evidence,
            body: .garageCover(DesignV2GarageCoverModel(
                state: state,
                detail: detail,
                isGarageVisible: isGarageVisible,
                shouldFailClosed: shouldFailClosed
            ))
        )
    }

    private func radarModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        let available = snapshot.isConnected && snapshot.isCollecting && snapshot.proximity.hasData
        return DesignV2OverlayModel(
            title: "Car Radar",
            status: available ? snapshot.proximity.sideStatus : "waiting",
            footer: available ? "source: local proximity telemetry" : "source: waiting",
            evidence: available ? .live : .unavailable,
            body: .radar(DesignV2RadarModel(proximity: snapshot.proximity, isAvailable: available))
        )
    }

    private func inputModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        guard snapshot.isConnected, snapshot.isCollecting, let frame = snapshot.latestFrame else {
            return DesignV2OverlayModel(
                title: "Inputs",
                status: "waiting",
                footer: "source: waiting",
                evidence: .unavailable,
                body: .inputs(DesignV2InputModel(
                    throttle: 0,
                    brake: 0,
                    clutch: 0,
                    steeringDegrees: 0,
                    speedMetersPerSecond: 0,
                    gear: 0,
                    brakeAbsActive: false,
                    trace: inputTrace,
                    isAvailable: false
                ))
            )
        }

        let sample = inputSample(frame)
        let model = DesignV2InputModel(
            throttle: sample.throttle,
            brake: sample.brake,
            clutch: sample.clutch,
            steeringDegrees: sample.steeringDegrees,
            speedMetersPerSecond: sample.speedMetersPerSecond,
            gear: sample.gear,
            brakeAbsActive: frame.brakeAbsActive,
            trace: inputTrace,
            isAvailable: true
        )
        return DesignV2OverlayModel(
            title: "Inputs",
            status: frame.brakeAbsActive ? "ABS active" : "live",
            footer: "source: local input telemetry",
            evidence: .live,
            body: .inputs(model)
        )
    }

    private func trackMapModel(_ snapshot: LiveTelemetrySnapshot) -> DesignV2OverlayModel {
        let available = snapshot.isConnected && snapshot.isCollecting && snapshot.latestFrame != nil
        return DesignV2OverlayModel(
            title: "Track Map",
            status: available ? "live" : "waiting",
            footer: available ? "source: live position telemetry" : "source: waiting",
            evidence: available ? .live : .unavailable,
            body: .trackMap(DesignV2TrackMapModel(snapshot: snapshot, isAvailable: available))
        )
    }

    private func appendInputTrace(_ frame: MockLiveTelemetryFrame) {
        let sample = inputSample(frame)
        inputTrace.append(DesignV2InputPoint(
            throttle: sample.throttle,
            brake: sample.brake,
            clutch: sample.clutch,
            brakeAbsActive: frame.brakeAbsActive
        ))
        if inputTrace.count > 180 {
            inputTrace.removeFirst(inputTrace.count - 180)
        }
    }

    private func inputSample(_ frame: MockLiveTelemetryFrame) -> (
        throttle: Double,
        brake: Double,
        clutch: Double,
        steeringDegrees: Double,
        speedMetersPerSecond: Double,
        gear: Int
    ) {
        let speedMetersPerSecond = 52 + sin(frame.sessionTime * 0.9) * 18
        return (
            throttle: max(0, min(1, 0.72 + sin(frame.sessionTime * 1.4) * 0.28)),
            brake: max(0, min(1, sin(frame.sessionTime * 0.72) - 0.75)),
            clutch: max(0, min(1, 0.08 + sin(frame.sessionTime / 2.7) * 0.08)),
            steeringDegrees: sin(frame.sessionTime * 1.1) * 9,
            speedMetersPerSecond: speedMetersPerSecond,
            gear: max(1, min(6, Int(speedMetersPerSecond / 13)))
        )
    }

    private func trackMapMarkers(_ snapshot: LiveTelemetrySnapshot) -> [DesignV2TrackMarker] {
        guard let frame = snapshot.latestFrame,
              frame.teamLapDistPct.isFinite,
              frame.teamLapDistPct >= 0 else {
            return []
        }

        let localProgress = normalizeTrackMapProgress(frame.teamLapDistPct)
        var markers: [DesignV2TrackMarker] = [
            DesignV2TrackMarker(
                carIdx: frame.focusCarIdx ?? frame.playerCarIdx ?? -1,
                lapDistPct: localProgress,
                isFocus: true,
                color: NSColor(red: 0.38, green: 0.78, blue: 1.0, alpha: 1.0),
                positionLabel: trackMapFocusPositionLabel(frame)
            )
        ]

        for car in snapshot.proximity.nearbyCars {
            markers.append(DesignV2TrackMarker(
                carIdx: car.carIdx,
                lapDistPct: normalizeTrackMapProgress(localProgress + car.relativeLaps),
                isFocus: false,
                color: OverlayClassColor.color(car.carClassColorHex, alpha: 0.96)
                    ?? NSColor(red: 0.93, green: 0.96, blue: 0.98, alpha: 0.96),
                positionLabel: nil
            ))
        }

        if frame.leaderLapDistPct.isFinite, frame.leaderLapDistPct >= 0 {
            markers.append(DesignV2TrackMarker(
                carIdx: 1,
                lapDistPct: normalizeTrackMapProgress(frame.leaderLapDistPct),
                isFocus: false,
                color: NSColor(red: 0.93, green: 0.96, blue: 0.98, alpha: 0.96),
                positionLabel: nil
            ))
        }

        return markers
    }

    private func trackMapFocusPositionLabel(_ frame: MockLiveTelemetryFrame) -> String? {
        guard let position = frame.teamClassPosition ?? frame.teamPosition, position > 0 else {
            return nil
        }

        return "\(position)"
    }

    private func trackMapPoint(on rect: NSRect, progress: Double) -> NSPoint {
        let angle = normalizeTrackMapProgress(progress) * Double.pi * 2 - Double.pi / 2
        return NSPoint(
            x: rect.midX + CGFloat(cos(angle)) * rect.width / 2,
            y: rect.midY + CGFloat(sin(angle)) * rect.height / 2
        )
    }

    private func trackMapSegmentRanges(startPct: Double, endPct: Double) -> [(start: Double, end: Double)] {
        let start = normalizeTrackMapProgress(startPct)
        let end = endPct >= 1 ? 1 : normalizeTrackMapProgress(endPct)
        if end <= start && endPct < 1 {
            return [(start, 1), (0, end)]
        }

        return [(start, min(max(end, 0), 1))]
    }

    private func trackMapBoundaryMarkers(_ sectors: [LiveTrackSectorSegment]) -> [(progress: Double, highlight: String)] {
        var seen: Set<Int> = []
        var markers: [(progress: Double, highlight: String)] = []
        for sector in sectors {
            let progress = sector.endPct >= 1 ? 1.0 : normalizeTrackMapProgress(sector.endPct)
            let key = Int((normalizeTrackMapProgress(progress) * 100_000).rounded())
            guard !seen.contains(key) else {
                continue
            }

            seen.insert(key)
            markers.append((progress, sector.boundaryHighlight))
        }
        return markers
    }

    private func hasTrackMapHighlight(_ highlight: String) -> Bool {
        highlight == LiveTrackSectorHighlights.personalBest
            || highlight == LiveTrackSectorHighlights.bestLap
    }

    private func trackMapSectorHighlightColor(_ highlight: String) -> NSColor {
        highlight == LiveTrackSectorHighlights.bestLap
            ? NSColor(red: 0.71, green: 0.36, blue: 1.0, alpha: 1.0)
            : NSColor(red: 0.18, green: 0.94, blue: 0.43, alpha: 1.0)
    }

    private func trackMapBoundaryColor(_ highlight: String) -> NSColor {
        hasTrackMapHighlight(highlight)
            ? trackMapSectorHighlightColor(highlight)
            : theme.colors.accentPrimary.withAlphaComponent(0.92)
    }

    private var trackMapSectorBoundariesVisible: Bool {
        optionBool(sourceSettings, key: "track-map.sector-boundaries.enabled", defaultValue: true)
    }

    private func normalizeTrackMapProgress(_ value: Double) -> Double {
        guard value.isFinite else {
            return 0
        }

        let normalized = value.truncatingRemainder(dividingBy: 1)
        return normalized < 0 ? normalized + 1 : normalized
    }

    private func fuelRows(_ strategy: FuelStrategySnapshot) -> [DesignV2FuelRow] {
        var rows: [DesignV2FuelRow] = [
            DesignV2FuelRow(label: "Overview", value: fuelOverview(strategy), advice: "")
        ]
        if let comparison = strategy.rhythmComparison, comparison.additionalStopCount > 0 {
            rows.append(DesignV2FuelRow(label: "Strategy", value: "\(comparison.longTargetLaps)-lap rhythm avoids +\(comparison.additionalStopCount) \(comparison.additionalStopCount == 1 ? "stop" : "stops")", advice: comparison.estimatedTimeLossSeconds.map { String(format: "~%.0fs", $0) } ?? "--"))
        }
        for stint in strategy.stints.prefix(6 - rows.count) {
            rows.append(DesignV2FuelRow(label: "Stint \(stint.number)", value: stintText(stint), advice: stint.tireAdvice?.text ?? ""))
        }
        return rows
    }

    private func fuelOverview(_ strategy: FuelStrategySnapshot) -> String {
        if let plannedLaps = strategy.plannedRaceLaps,
           let stintCount = strategy.plannedStintCount,
           let finalStintLaps = strategy.finalStintTargetLaps {
            return stintCount <= 1
                ? "\(plannedLaps) laps | no stop"
                : "\(plannedLaps) laps | \(stintCount) stints | final \(finalStintLaps)"
        }

        let needed = (strategy.additionalFuelNeededLiters ?? 0) > 0.1
            ? "+\(fuelVolume(strategy.additionalFuelNeededLiters))"
            : "covered"
        return "\(fuelVolume(strategy.currentFuelLiters)) | \(FuelStrategyCalculator.format(strategy.raceLapsRemaining, suffix: " laps")) | \(needed)"
    }

    private func fuelSourceText(_ strategy: FuelStrategySnapshot, history: SessionHistoryLookupResult) -> String {
        let historySource = history.userAggregate != nil ? "user" : history.baselineAggregate != nil ? "baseline" : "none"
        return "burn \(fuelPerLap(strategy.fuelPerLapLiters)) (\(strategy.fuelPerLapSource)) | \(FuelStrategyCalculator.format(strategy.fullTankStintLaps, suffix: " laps/tank")) | history \(historySource)"
    }

    private func stintText(_ stint: FuelStintEstimate) -> String {
        if stint.source == "finish" {
            return "no fuel stop needed"
        }
        if let targetLaps = stint.targetLaps {
            return "\(targetLaps) laps\(stint.source == "final" ? " final" : "") | target \(fuelPerLap(stint.targetFuelPerLapLiters))"
        }
        return String(format: "%.1f laps", stint.lengthLaps)
    }

    private func lookupHistory(_ combo: HistoricalComboIdentity) -> SessionHistoryLookupResult {
        let now = Date()
        if let cachedHistory,
           let cachedHistoryCombo,
           cachedHistoryCombo.carKey == combo.carKey,
           cachedHistoryCombo.trackKey == combo.trackKey,
           cachedHistoryCombo.sessionKey == combo.sessionKey,
           let cachedHistoryAt,
           now.timeIntervalSince(cachedHistoryAt) <= 30 {
            return cachedHistory
        }

        let history = historyQueryService.lookup(combo)
        cachedHistory = history
        cachedHistoryCombo = combo
        cachedHistoryAt = now
        return history
    }

    private func formatLeaderGap(_ car: LiveClassGapCar) -> String {
        StandingsDisplayFormatting.gap(
            isClassLeader: car.isClassLeader,
            seconds: car.gapSecondsToClassLeader,
            laps: car.gapLapsToClassLeader,
            lapCompleted: car.lapCompleted,
            lapDistPct: car.lapDistPct)
    }

    private func intervalText(_ delta: Double?, referenceGap: Double, isReference: Bool) -> String {
        StandingsDisplayFormatting.interval(delta, referenceGap: referenceGap, isReference: isReference)
    }

    private func pitText(car: LiveClassGapCar, snapshot: LiveTelemetrySnapshot) -> String {
        guard let sessionTime = snapshot.latestFrame?.sessionTime else {
            return ""
        }
        if car.isReferenceCar && snapshot.latestFrame?.onPitRoad == true {
            return "IN"
        }
        if car.onPitRoad == true {
            return "IN"
        }
        if car.classPosition == 2 {
            return "IN"
        }
        let position = car.classPosition ?? abs(car.carIdx % 18) + 1
        let entry = FourHourRacePreview.firstPitEntrySeconds + TimeInterval((position % 9) * 34) - 140
        let exit = entry + FourHourRacePreview.firstPitExitSeconds - FourHourRacePreview.firstPitEntrySeconds
        if sessionTime >= entry && sessionTime <= exit {
            return "IN"
        }
        if sessionTime > exit && sessionTime <= exit + 20 {
            return "OUT"
        }
        return ""
    }

    private func pitWindow(_ sessionTime: TimeInterval) -> Bool {
        sessionTime >= FourHourRacePreview.firstPitEntrySeconds
            && sessionTime <= FourHourRacePreview.firstPitExitSeconds
    }

    private func gapText(_ gap: LiveGapValue) -> String {
        if gap.isLeader {
            return "Leader"
        }
        if let seconds = gap.seconds, seconds.isFinite {
            return formatAxisSeconds(seconds)
        }
        if let laps = gap.laps, laps.isFinite {
            return String(format: "+%.1fL", laps)
        }
        return "--"
    }

    private var standingsClassSeparatorsEnabled: Bool {
        guard let block = standingsClassSeparatorBlock else {
            return true
        }

        return OverlayContentColumns.blockEnabled(block, settings: sourceSettings)
    }

    private var standingsOtherClassRows: Int {
        guard standingsClassSeparatorsEnabled,
              let block = standingsClassSeparatorBlock else {
            return 0
        }

        return OverlayContentColumns.blockCount(block, settings: sourceSettings)
    }

    private var standingsClassSeparatorBlock: OverlayContentBlockDefinition? {
        OverlayContentColumns.standings.blocks.first {
            $0.id == OverlayContentColumns.standingsClassSeparatorBlockId
        }
    }

    private func estimatedLaps(_ remaining: TimeInterval?, lapSeconds: TimeInterval?) -> String {
        guard let remaining,
              let lapSeconds,
              remaining.isFinite,
              lapSeconds.isFinite,
              remaining > 0,
              lapSeconds > 20 else {
            return ""
        }

        return String(format: "~%.0f laps", ceil(remaining / lapSeconds + 1))
    }

    private func filteredGapRangeSeconds(lapSeconds: TimeInterval?) -> Double {
        guard let lapSeconds, lapSeconds.isFinite, lapSeconds > 20 else {
            return 45
        }

        return min(max(lapSeconds * 0.5, 15), 90)
    }

    private func positionText(_ value: Int?) -> String {
        guard let value, value > 0 else {
            return "--"
        }

        return "\(value)"
    }

    private func formatAxisSeconds(_ seconds: Double) -> String {
        guard seconds.isFinite else {
            return "--"
        }
        if seconds >= 60 {
            return String(format: "+%.0f:%04.1f", floor(seconds / 60), seconds.truncatingRemainder(dividingBy: 60))
        }

        return String(format: "+%.1fs", seconds)
    }

    private func formatPlainSeconds(_ seconds: Double?) -> String {
        guard let seconds, seconds.isFinite else {
            return "--"
        }
        if seconds >= 60 {
            return String(format: "%.0f:%04.1f", floor(seconds / 60), seconds.truncatingRemainder(dividingBy: 60))
        }

        return String(format: "%.1fs", seconds)
    }

    private var inputRailVisible: Bool {
        inputBlockEnabled(OverlayContentColumns.inputThrottleBlockId)
            || inputBlockEnabled(OverlayContentColumns.inputBrakeBlockId)
            || inputBlockEnabled(OverlayContentColumns.inputClutchBlockId)
            || inputBlockEnabled(OverlayContentColumns.inputSteeringBlockId)
            || inputBlockEnabled(OverlayContentColumns.inputGearBlockId)
            || inputBlockEnabled(OverlayContentColumns.inputSpeedBlockId)
    }

    private func inputBlockEnabled(_ id: String) -> Bool {
        guard let block = OverlayContentColumns.inputState.blocks.first(where: { $0.id == id }) else {
            return true
        }

        return OverlayContentColumns.blockEnabled(block, settings: sourceSettings)
    }

    private func optionBool(_ overlay: OverlaySettings, key: String, defaultValue: Bool) -> Bool {
        guard let configured = overlay.options[key]?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() else {
            return defaultValue
        }

        if ["true", "1", "yes"].contains(configured) {
            return true
        }

        if ["false", "0", "no"].contains(configured) {
            return false
        }

        return defaultValue
    }

    private func formatSpeed(_ metersPerSecond: Double) -> String {
        guard metersPerSecond.isFinite else {
            return "--"
        }

        if unitSystem == "Imperial" {
            return "\(Int((metersPerSecond * 2.2369362921).rounded())) mph"
        }

        return "\(Int((metersPerSecond * 3.6).rounded())) km/h"
    }

    private func fuelVolume(_ liters: Double?) -> String {
        guard let liters, liters.isFinite else {
            return "-- \(fuelVolumeSuffix)"
        }
        let value = unitSystem == "Imperial" ? liters * 0.264172052 : liters
        return String(format: "%.1f %@", value, fuelVolumeSuffix)
    }

    private func fuelPerLap(_ liters: Double?) -> String {
        guard let liters, liters.isFinite else {
            return "-- \(fuelPerLapSuffix)"
        }
        let value = unitSystem == "Imperial" ? liters * 0.264172052 : liters
        return String(format: "%.1f %@", value, fuelPerLapSuffix)
    }

    private var fuelVolumeSuffix: String {
        unitSystem == "Imperial" ? "gal" : "L"
    }

    private var fuelPerLapSuffix: String {
        unitSystem == "Imperial" ? "gal/lap" : "L/lap"
    }

    private func temperature(_ celsius: Double) -> String {
        if unitSystem == "Imperial" {
            return "\(Int((celsius * 9 / 5 + 32).rounded()))F"
        }
        return "\(Int(celsius.rounded()))C"
    }

    private func formatDuration(_ seconds: TimeInterval) -> String {
        guard seconds.isFinite else {
            return "--"
        }
        let total = max(0, Int(seconds.rounded()))
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        let secs = total % 60
        return hours > 0
            ? String(format: "%d:%02d:%02d", hours, minutes, secs)
            : String(format: "%d:%02d", minutes, secs)
    }

    private func textAlignment(_ alignment: OverlayContentColumnAlignment) -> NSTextAlignment {
        switch alignment {
        case .left:
            return .left
        case .center:
            return .center
        case .right:
            return .right
        }
    }

    private func overlayFont(size: CGFloat, weight: NSFont.Weight) -> NSFont {
        DesignV2Drawing.font(family: fontFamily, size: size, weight: weight)
    }

    private func rounded(_ rect: NSRect, radius: CGFloat, fill: NSColor?, stroke: NSColor?) {
        DesignV2Drawing.rounded(rect, radius: radius, fill: fill, stroke: stroke)
    }

    private func drawText(_ text: String, in rect: NSRect, font: NSFont, color: NSColor, alignment: NSTextAlignment = .left) {
        DesignV2Drawing.text(text, in: rect, font: font, color: color, alignment: alignment)
    }

    private func trim(_ value: String) -> String {
        value.count <= 90 ? value : String(value.prefix(87)) + "..."
    }
}
