import AppKit

enum SimpleTelemetryOverlayKind {
    case flags
    case sessionWeather
    case pitService
    case inputState
}

struct FlagDisplayOptions {
    var showGreen = true
    var showBlue = true
    var showYellow = true
    var showCritical = true
    var showFinish = true
}

struct InputDisplayOptions {
    var showThrottleTrace = true
    var showBrakeTrace = true
    var showClutchTrace = true
    var showThrottle = true
    var showBrake = true
    var showClutch = true
    var showSteering = true
    var showGear = true
    var showSpeed = true
}

final class SimpleTelemetryOverlayView: NSView {
    private enum Layout {
        static let padding: CGFloat = 14
        static let maximumRows = 8
        static let sourceHeight: CGFloat = 20
        static let compactInputWidth: CGFloat = 320
        static let compactInputHeight: CGFloat = 180
        static let fullInputHeight: CGFloat = 270
    }

    private enum Tone {
        case normal
        case info
        case success
        case warning
        case error
    }

    private enum FlagCategory {
        case green
        case blue
        case yellow
        case critical
        case finish
    }

    private enum FlagKind {
        case green
        case blue
        case yellow
        case caution
        case red
        case black
        case meatball
        case white
        case checkered
    }

    private struct FlagDisplayItem {
        let kind: FlagKind
        let category: FlagCategory
        let label: String
        let detail: String?
    }

    private struct Row {
        let label: String
        let value: String
        let tone: Tone

        init(_ label: String, _ value: String, tone: Tone = .normal) {
            self.label = label
            self.value = value
            self.tone = tone
        }
    }

    private let kind: SimpleTelemetryOverlayKind
    private let titleLabel = NSTextField(labelWithString: "")
    private let statusLabel = NSTextField(labelWithString: "waiting")
    private let sourceLabel = NSTextField(labelWithString: "source: waiting")
    private var labelCells: [NSTextField] = []
    private var valueCells: [NSTextField] = []
    private var tableRect = NSRect.zero
    private var latestFrame: MockLiveTelemetryFrame?
    private var inputTrace: [InputTracePoint] = []
    private var flagDisplayItems: [FlagDisplayItem] = []
    private var overlayError: String?
    private var pitServiceLastValues: [String: String] = [:]
    private var pitServiceHighlightUntil: [String: Date] = [:]

    var flagDisplayOptions = FlagDisplayOptions() {
        didSet {
            needsDisplay = true
        }
    }

    var inputDisplayOptions = InputDisplayOptions() {
        didSet {
            needsDisplay = true
        }
    }

    var showSourceFooter = false {
        didSet {
            updateSourceVisibility()
            needsLayout = true
            needsDisplay = true
        }
    }

    var fontFamily = OverlayTheme.defaultFontFamily {
        didSet { applyFonts() }
    }

    var unitSystem = "Metric" {
        didSet { needsDisplay = true }
    }

    init(kind: SimpleTelemetryOverlayKind) {
        self.kind = kind
        super.init(frame: NSRect(origin: .zero, size: Self.defaultSize(for: kind)))
        setup()
    }

    required init?(coder: NSCoder) {
        nil
    }

    override func layout() {
        super.layout()

        if kind == .flags {
            titleLabel.isHidden = true
            statusLabel.isHidden = true
            sourceLabel.isHidden = true
            labelCells.forEach { $0.isHidden = true }
            valueCells.forEach { $0.isHidden = true }
            tableRect = bounds
            return
        }

        let customInput = kind == .inputState
        titleLabel.isHidden = customInput
        statusLabel.isHidden = customInput
        labelCells.forEach { $0.isHidden = customInput }
        valueCells.forEach { $0.isHidden = customInput }

        let width = bounds.width
        let tableBottom = Layout.padding + (sourceLabel.isHidden ? 0 : Layout.sourceHeight)
        titleLabel.frame = NSRect(x: Layout.padding, y: bounds.height - 34, width: 185, height: 22)
        statusLabel.frame = NSRect(x: 204, y: bounds.height - 34, width: max(110, width - 218), height: 22)
        tableRect = customInput
            ? bounds.insetBy(dx: Layout.padding, dy: Layout.padding)
            : NSRect(
                x: Layout.padding,
                y: tableBottom,
                width: max(240, width - Layout.padding * 2),
                height: max(128, bounds.height - tableBottom - 44)
            )
        sourceLabel.frame = NSRect(x: Layout.padding, y: 9, width: max(240, width - Layout.padding * 2), height: 18)

        let rowHeight = tableRect.height / CGFloat(Layout.maximumRows)
        let labelWidth = tableRect.width * 0.38
        let valueWidth = tableRect.width - labelWidth
        for index in 0..<Layout.maximumRows {
            let y = tableRect.maxY - CGFloat(index + 1) * rowHeight
            labelCells[index].frame = NSRect(x: tableRect.minX + 8, y: y + 3, width: labelWidth - 14, height: rowHeight - 6)
            valueCells[index].frame = NSRect(x: tableRect.minX + labelWidth + 8, y: y + 3, width: valueWidth - 14, height: rowHeight - 6)
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)

        if kind == .flags {
            drawFlags()
            return
        }

        OverlayTheme.Colors.windowBorder.setStroke()
        bounds.insetBy(dx: 0.5, dy: 0.5).frame()
        if kind == .inputState {
            drawInputGraph()
            return
        }

        OverlayTheme.Colors.panelBackground.setFill()
        tableRect.fill()
        OverlayTheme.Colors.windowBorder.setStroke()
        tableRect.frame()

        let rowHeight = tableRect.height / CGFloat(Layout.maximumRows)
        for row in 1..<Layout.maximumRows {
            let y = tableRect.maxY - CGFloat(row) * rowHeight
            NSBezierPath.strokeLine(from: NSPoint(x: tableRect.minX, y: y), to: NSPoint(x: tableRect.maxX, y: y))
        }

        let labelWidth = tableRect.width * 0.38
        NSBezierPath.strokeLine(
            from: NSPoint(x: tableRect.minX + labelWidth, y: tableRect.minY),
            to: NSPoint(x: tableRect.minX + labelWidth, y: tableRect.maxY)
        )
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        latestFrame = snapshot.latestFrame
        let model = buildModel(snapshot)
        overlayError = nil
        titleLabel.stringValue = model.title
        statusLabel.stringValue = model.status
        sourceLabel.stringValue = model.source
        if kind == .flags, let frame = snapshot.latestFrame {
            flagDisplayItems = displayFlags(
                flags: syntheticFlags(frame),
                sessionState: frame.sessionState
            ).filter { flagCategoryEnabled($0.category) }
        } else if kind == .flags {
            flagDisplayItems = []
        }
        if kind == .inputState, let frame = snapshot.latestFrame {
            appendInputTrace(frame)
        }
        applyTone(model.tone)
        applyRows(model.rows)
        updateSourceVisibility()
        needsLayout = true
        needsDisplay = true
    }

    func showOverlayError(_ message: String) {
        overlayError = message
        titleLabel.stringValue = defaultTitle
        statusLabel.stringValue = "overlay error"
        sourceLabel.stringValue = message
        if kind == .flags {
            flagDisplayItems = [
                FlagDisplayItem(
                    kind: .red,
                    category: .critical,
                    label: "Flags",
                    detail: "error"
                )
            ]
        }
        applyTone(.error)
        applyRows([Row("Error", message, tone: .error)])
        updateSourceVisibility()
        needsLayout = true
        needsDisplay = true
    }

    private func setup() {
        wantsLayer = true
        layer?.backgroundColor = OverlayTheme.Colors.windowBackground.cgColor

        titleLabel.stringValue = defaultTitle
        titleLabel.textColor = OverlayTheme.Colors.textPrimary
        titleLabel.backgroundColor = .clear
        statusLabel.alignment = .right
        statusLabel.textColor = OverlayTheme.Colors.textSubtle
        statusLabel.backgroundColor = .clear
        sourceLabel.textColor = OverlayTheme.Colors.textMuted
        sourceLabel.backgroundColor = .clear
        addSubview(titleLabel)
        addSubview(statusLabel)
        addSubview(sourceLabel)

        for _ in 0..<Layout.maximumRows {
            let label = NSTextField(labelWithString: "")
            label.textColor = OverlayTheme.Colors.textSecondary
            label.backgroundColor = .clear
            let value = NSTextField(labelWithString: "")
            value.alignment = .right
            value.textColor = OverlayTheme.Colors.textPrimary
            value.backgroundColor = .clear
            labelCells.append(label)
            valueCells.append(value)
            addSubview(label)
            addSubview(value)
        }

        applyFonts()
        updateSourceVisibility()
        if kind == .flags {
            layer?.backgroundColor = NSColor.clear.cgColor
        }
    }

    private func buildModel(_ snapshot: LiveTelemetrySnapshot) -> (title: String, status: String, source: String, tone: Tone, rows: [Row]) {
        guard snapshot.isConnected, snapshot.isCollecting, let frame = snapshot.latestFrame else {
            return (defaultTitle, "waiting for telemetry", "source: waiting", .normal, [])
        }

        switch kind {
        case .flags:
            return buildFlags(frame)
        case .sessionWeather:
            return buildSessionWeather(frame)
        case .pitService:
            return buildPitService(frame)
        case .inputState:
            return buildInputState(frame)
        }
    }

    private func buildFlags(_ frame: MockLiveTelemetryFrame) -> (String, String, String, Tone, [Row]) {
        let flags = syntheticFlags(frame)
        let status = primaryFlag(flags: flags, sessionState: frame.sessionState)
        let tone = flagTone(flags: flags, sessionState: frame.sessionState)
        return (
            "Flags",
            status,
            "source: session flags telemetry",
            tone,
            [
                Row("State", sessionState(frame.sessionState), tone: tone),
                Row("Flags", flagList(flags), tone: tone),
                Row("Raw", String(format: "0x%08X", flags)),
                Row("Time left", formatDuration(frame.sessionTimeRemain)),
                Row("Laps", "\(max(0, 30 - frame.teamLapCompleted)) left | 30 total")
            ]
        )
    }

    private func buildSessionWeather(_ frame: MockLiveTelemetryFrame) -> (String, String, String, Tone, [Row]) {
        let surface = trackWetnessLabel(frame.trackWetness)
        let changed = Int(frame.sessionTime / 45).isMultiple(of: 5)
        let tone: Tone = frame.weatherDeclaredWet || frame.trackWetness > 1 ? .info : .normal
        let status = frame.weatherDeclaredWet ? "declared wet" : "Race"
        let sky = frame.weatherDeclaredWet ? "overcast | realistic | rain 65%" : "partly cloudy | realistic | rain 12%"
        let windSpeed = 3.6 + sin(frame.sessionTime / 420) * 1.2
        let wind = "NW \(Int((windSpeed * 3.6).rounded())) km/h | hum \(frame.weatherDeclaredWet ? 82 : 67)% | fog \(frame.weatherDeclaredWet ? 9 : 0)%"
        let surfaceText = frame.weatherDeclaredWet
            ? "\(surface) | declared wet | rubber moderate"
            : "\(surface) | rubber \(frame.sessionTime > 3_600 ? "moderate" : "clean")"
        return (
            "Session / Weather",
            status,
            "source: session + weather telemetry",
            changed ? .info : tone,
            [
                Row("Session", "Race | team"),
                Row("Clock", "\(formatDuration(frame.sessionTime)) elapsed | \(formatDuration(frame.sessionTimeRemain)) left"),
                Row("Laps", "\(max(0, 30 - frame.teamLapCompleted)) left | 30 total"),
                Row("Track", "Nurburgring Combined | 24.36 km"),
                Row("Temps", "air \(formatTemperature(21.5 + sin(frame.sessionTime / 300) * 2)) | track \(formatTemperature(28 + sin(frame.sessionTime / 240) * 4))", tone: changed ? .info : .normal),
                Row("Surface", surfaceText, tone: tone),
                Row("Sky", sky, tone: changed ? .info : .normal),
                Row("Wind", wind, tone: changed ? .info : .normal)
            ]
        )
    }

    private func buildPitService(_ frame: MockLiveTelemetryFrame) -> (String, String, String, Tone, [Row]) {
        let inPitWindow = frame.onPitRoad || pitWindow(frame.sessionTime)
        let serviceFlags = inPitWindow ? 0x1f : 0x10
        let fuelRequest = max(0, min(frame.fuelMaxLiters, frame.fuelMaxLiters - frame.fuelLevelLiters))
        let tone: Tone = inPitWindow ? .error : .info
        let release = inPitWindow ? "RED - service active" : "armed"
        let service = inPitWindow ? "active | tires, fuel" : "requested | fuel"
        let fuelRequestText = formatFuel(fuelRequest)
        let repair = "--"
        let tires = inPitWindow ? "four tires | 2 sets used" : "none"
        let fastRepair = (serviceFlags & 0x40) != 0 ? "selected | local 0 | team 0" : "local 0 | team 0"
        return (
            "Pit Service",
            inPitWindow ? "hold" : "service requested",
            "source: player/team pit service telemetry",
            tone,
            [
                Row("Release", release, tone: tone),
                Row("Location", inPitWindow ? "team on pit road" : "off pit road", tone: tone),
                Row("Service", service, tone: pitServiceTone(key: "service", value: service, baseTone: tone)),
                Row("Pit status", inPitWindow ? "in progress" : "none", tone: tone),
                Row("Fuel request", fuelRequestText, tone: pitServiceTone(key: "fuel-request", value: fuelRequestText, baseTone: .normal)),
                Row("Repair", repair, tone: pitServiceTone(key: "repair", value: repair, baseTone: .normal)),
                Row("Tires", tires, tone: pitServiceTone(key: "tires", value: tires, baseTone: .normal)),
                Row("Fast repair", fastRepair, tone: pitServiceTone(key: "fast-repair", value: fastRepair, baseTone: .normal))
            ]
        )
    }

    private func buildInputState(_ frame: MockLiveTelemetryFrame) -> (String, String, String, Tone, [Row]) {
        let speedMetersPerSecond = 52 + sin(frame.sessionTime * 0.9) * 18
        let gear = max(1, min(6, Int(speedMetersPerSecond / 13)))
        let rpm = 4_800 + speedMetersPerSecond * 72
        let throttle = max(0, min(1, 0.72 + sin(frame.sessionTime * 1.4) * 0.28))
        let brake = max(0, min(1, sin(frame.sessionTime * 0.72) - 0.75))
        let steeringDegrees = sin(frame.sessionTime * 1.1) * 9
        let brakeText = frame.brakeAbsActive ? "B \(formatPercent(brake)) ABS" : "B \(formatPercent(brake))"
        let status = frame.brakeAbsActive
            ? "\(gear) | \(Int(rpm)) rpm | ABS"
            : "\(gear) | \(Int(rpm)) rpm"
        return (
            "Input / Car State",
            status,
            "source: local car telemetry",
            .normal,
            [
                Row("Speed", formatSpeed(speedMetersPerSecond)),
                Row("Gear / RPM", "\(gear) | \(Int(rpm)) rpm"),
                Row("Pedals", "T \(formatPercent(throttle)) | \(brakeText) | C 0%"),
                Row("Steering", String(format: "%+.0f deg", steeringDegrees)),
                Row("Warnings", "none"),
                Row("Electrical", String(format: "%.1f V", 13.8 + sin(frame.sessionTime / 30) * 0.1)),
                Row("Cooling", formatTemperature(88 + sin(frame.sessionTime / 90) * 3)),
                Row("Oil / Fuel", "oil \(formatPressure(4.1)) | fuel \(formatPressure(3.8))")
            ]
        )
    }

    private func drawFlags() {
        guard !flagDisplayItems.isEmpty else {
            return
        }

        let padding: CGFloat = 8
        let gap: CGFloat = 8
        let drawBounds = bounds.insetBy(dx: padding, dy: padding)
        let grid = flagGrid(count: flagDisplayItems.count)
        let cellWidth = (drawBounds.width - CGFloat(grid.columns - 1) * gap) / CGFloat(grid.columns)
        let cellHeight = (drawBounds.height - CGFloat(grid.rows - 1) * gap) / CGFloat(grid.rows)
        for index in flagDisplayItems.indices {
            let row = index / grid.columns
            let column = index % grid.columns
            let cell = NSRect(
                x: drawBounds.minX + CGFloat(column) * (cellWidth + gap),
                y: drawBounds.maxY - CGFloat(row + 1) * cellHeight - CGFloat(row) * gap,
                width: cellWidth,
                height: cellHeight
            )
            drawFlagCell(flagDisplayItems[index], in: cell, index: index)
        }
    }

    private func drawFlagCell(_ flag: FlagDisplayItem, in cell: NSRect, index: Int) {
        let compact = cell.height < 92 || cell.width < 132
        let flagArea = NSRect(
            x: cell.minX,
            y: cell.minY,
            width: cell.width,
            height: max(32, cell.height)
        )
        let poleX = flagArea.minX + max(12, flagArea.width * 0.16)
        NSColor(red255: 0, green: 0, blue: 0, alpha: 0.45).setStroke()
        var polePath = NSBezierPath()
        polePath.lineWidth = compact ? 2 : 3
        polePath.lineCapStyle = .round
        polePath.move(to: NSPoint(x: poleX + 1, y: flagArea.minY + 3))
        polePath.line(to: NSPoint(x: poleX + 1, y: flagArea.maxY - 3))
        polePath.stroke()

        NSColor(red255: 214, green: 220, blue: 226, alpha: 0.88).setStroke()
        polePath = NSBezierPath()
        polePath.lineWidth = compact ? 2 : 3
        polePath.lineCapStyle = .round
        polePath.move(to: NSPoint(x: poleX, y: flagArea.minY + 4))
        polePath.line(to: NSPoint(x: poleX, y: flagArea.maxY - 4))
        polePath.stroke()

        let clothLeft = poleX + 1
        let clothWidth = max(48, flagArea.maxX - clothLeft - 8)
        let clothHeight = max(24, min(flagArea.height * 0.7, clothWidth * 0.58))
        let clothBounds = NSRect(
            x: clothLeft,
            y: flagArea.minY + max(4, (flagArea.height - clothHeight) * 0.38),
            width: clothWidth,
            height: clothHeight
        )
        let path = flagPath(in: clothBounds, wave: compact ? 3.5 : 5.5, phase: index.isMultiple(of: 2) ? 1 : -1)
        drawFlagCloth(flag, path: path, bounds: clothBounds)
    }

    private func drawFlagCloth(_ flag: FlagDisplayItem, path: NSBezierPath, bounds: NSRect) {
        if flag.kind == .checkered {
            drawCheckeredFlag(path: path, bounds: bounds)
            return
        }

        flagFillColor(flag.kind).setFill()
        path.fill()
        if flag.kind == .meatball {
            NSColor(red255: 245, green: 124, blue: 38).setFill()
            let diameter = min(bounds.width, bounds.height) * 0.44
            NSBezierPath(ovalIn: NSRect(
                x: bounds.midX - diameter / 2,
                y: bounds.midY - diameter / 2,
                width: diameter,
                height: diameter
            )).fill()
        } else if flag.kind == .caution {
            NSGraphicsContext.saveGraphicsState()
            path.addClip()
            NSColor(red255: 0, green: 0, blue: 0, alpha: 0.28).setFill()
            let stripeWidth = max(8, bounds.width * 0.12)
            var x = bounds.minX - bounds.height
            while x < bounds.maxX {
                let stripe = NSBezierPath()
                stripe.move(to: NSPoint(x: x, y: bounds.minY))
                stripe.line(to: NSPoint(x: x + stripeWidth, y: bounds.minY))
                stripe.line(to: NSPoint(x: x + stripeWidth + bounds.height, y: bounds.maxY))
                stripe.line(to: NSPoint(x: x + bounds.height, y: bounds.maxY))
                stripe.close()
                stripe.fill()
                x += stripeWidth * 2.5
            }
            NSGraphicsContext.restoreGraphicsState()
        }

        drawFlagOutline(path: path, kind: flag.kind)
    }

    private func drawCheckeredFlag(path: NSBezierPath, bounds: NSRect) {
        NSGraphicsContext.saveGraphicsState()
        path.addClip()
        NSColor(red255: 245, green: 247, blue: 250).setFill()
        bounds.fill()
        NSColor(red255: 8, green: 10, blue: 12).setFill()
        let columns = 6
        let rows = 4
        let squareWidth = bounds.width / CGFloat(columns)
        let squareHeight = bounds.height / CGFloat(rows)
        for row in 0..<rows {
            for column in 0..<columns where (row + column) % 2 != 0 {
                NSRect(
                    x: bounds.minX + CGFloat(column) * squareWidth,
                    y: bounds.minY + CGFloat(row) * squareHeight,
                    width: squareWidth + 1,
                    height: squareHeight + 1
                ).fill()
            }
        }
        NSGraphicsContext.restoreGraphicsState()
        drawFlagOutline(path: path, kind: .checkered)
    }

    private func drawFlagOutline(path: NSBezierPath, kind: FlagKind) {
        let outline = kind == .white || kind == .checkered
            ? NSColor(red255: 26, green: 30, blue: 34, alpha: 0.86)
            : NSColor(red255: 255, green: 255, blue: 255, alpha: 0.68)
        outline.setStroke()
        path.lineWidth = 1.4
        path.lineJoinStyle = .round
        path.stroke()
    }

    private func flagPath(in rect: NSRect, wave: CGFloat, phase: CGFloat) -> NSBezierPath {
        let path = NSBezierPath()
        path.move(to: NSPoint(x: rect.minX, y: rect.maxY))
        path.curve(
            to: NSPoint(x: rect.maxX, y: rect.maxY + wave * phase),
            controlPoint1: NSPoint(x: rect.minX + rect.width * 0.28, y: rect.maxY - wave * phase),
            controlPoint2: NSPoint(x: rect.minX + rect.width * 0.62, y: rect.maxY + wave * phase)
        )
        path.line(to: NSPoint(x: rect.maxX, y: rect.minY + wave * 0.4 * phase))
        path.curve(
            to: NSPoint(x: rect.minX, y: rect.minY),
            controlPoint1: NSPoint(x: rect.minX + rect.width * 0.62, y: rect.minY - wave * phase),
            controlPoint2: NSPoint(x: rect.minX + rect.width * 0.28, y: rect.minY + wave * phase)
        )
        path.close()
        return path
    }

    private func flagFillColor(_ kind: FlagKind) -> NSColor {
        switch kind {
        case .green:
            return NSColor(red255: 48, green: 214, blue: 109)
        case .blue:
            return NSColor(red255: 55, green: 162, blue: 255)
        case .yellow, .caution:
            return NSColor(red255: 255, green: 207, blue: 74)
        case .red:
            return NSColor(red255: 236, green: 76, blue: 86)
        case .black, .meatball:
            return NSColor(red255: 8, green: 10, blue: 12)
        case .white:
            return NSColor(red255: 246, green: 248, blue: 250)
        case .checkered:
            return .white
        }
    }

    private func flagGrid(count: Int) -> (columns: Int, rows: Int) {
        switch count {
        case ...1:
            return (1, 1)
        case 2:
            return (2, 1)
        case ...4:
            return (2, 2)
        case ...6:
            return (3, 2)
        default:
            return (4, 2)
        }
    }

    private func drawInputGraph() {
        guard let frame = latestFrame else {
            drawInputWaiting()
            return
        }

        let content = bounds.insetBy(dx: 14, dy: 14)
        let railEnabled = inputRailEnabled && content.width >= 360
        let railWidth = railEnabled ? min(max(content.width * 0.30, 126), 162) : 0
        let graphWidth = railEnabled ? content.width - railWidth - 10 : content.width
        let graph = NSRect(x: content.minX, y: content.minY, width: max(160, graphWidth), height: content.height)
        OverlayTheme.Colors.panelBackground.setFill()
        graph.fill()
        OverlayTheme.Colors.windowBorder.setStroke()
        graph.frame()

        NSColor(calibratedWhite: 1, alpha: 0.12).setStroke()
        for step in 1..<4 {
            let y = graph.minY + graph.height * CGFloat(step) / 4
            NSBezierPath.strokeLine(from: NSPoint(x: graph.minX, y: y), to: NSPoint(x: graph.maxX, y: y))
        }

        if inputDisplayOptions.showThrottleTrace {
            drawTrace(in: graph, color: OverlayTheme.Colors.successText) { $0.throttle }
        }
        if inputDisplayOptions.showBrakeTrace {
            drawTrace(in: graph, color: OverlayTheme.Colors.errorIndicator) { $0.brake }
            drawActiveTraceSegments(in: graph, color: NSColor(red255: 255, green: 209, blue: 102), select: { $0.brake }, isActive: { $0.brakeAbsActive })
        }
        if inputDisplayOptions.showClutchTrace {
            drawTrace(in: graph, color: NSColor(red255: 104, green: 193, blue: 255)) { $0.clutch }
        }
        drawInputLegend(in: graph)
        if railEnabled {
            drawInputRail(frame, rect: NSRect(x: graph.maxX + 10, y: content.minY, width: railWidth, height: content.height))
        }
    }

    private func drawWideInputGraph(_ frame: MockLiveTelemetryFrame) {
        let graphHeight = max(CGFloat(80), bounds.height - 118)
        let graph = NSRect(x: 14, y: bounds.height - 44 - graphHeight, width: max(260, bounds.width - 28), height: graphHeight)
        OverlayTheme.Colors.panelBackground.setFill()
        graph.fill()
        OverlayTheme.Colors.windowBorder.setStroke()
        graph.frame()

        NSColor(calibratedWhite: 1, alpha: 0.12).setStroke()
        for step in 1..<4 {
            let y = graph.minY + graph.height * CGFloat(step) / 4
            NSBezierPath.strokeLine(from: NSPoint(x: graph.minX, y: y), to: NSPoint(x: graph.maxX, y: y))
        }

        if inputDisplayOptions.showThrottleTrace {
            drawTrace(in: graph, color: OverlayTheme.Colors.successText) { $0.throttle }
        }
        if inputDisplayOptions.showBrakeTrace {
            drawTrace(in: graph, color: OverlayTheme.Colors.errorIndicator) { $0.brake }
            drawActiveTraceSegments(in: graph, color: NSColor(red255: 255, green: 209, blue: 102), select: { $0.brake }, isActive: { $0.brakeAbsActive })
        }
        if inputDisplayOptions.showClutchTrace {
            drawTrace(in: graph, color: NSColor(red255: 104, green: 193, blue: 255)) { $0.clutch }
        }
        drawInputLegend(in: graph)

        let speedMetersPerSecond = 52 + sin(frame.sessionTime * 0.9) * 18
        let gear = max(1, min(6, Int(speedMetersPerSecond / 13)))
        let rpm = 4_800 + speedMetersPerSecond * 72
        let steeringDegrees = sin(frame.sessionTime * 1.1) * 9
        let rows = [
            ("Speed", formatSpeed(speedMetersPerSecond)),
            ("Gear", "\(gear)"),
            ("RPM", "\(Int(rpm))"),
            ("Steer", String(format: "%+.0f deg", steeringDegrees)),
            ("Water", formatTemperature(88 + sin(frame.sessionTime / 90) * 3)),
            ("Oil", formatPressure(4.1))
        ]
        let readout = NSRect(x: 14, y: 12, width: max(260, bounds.width - 28), height: max(36, graph.minY - 20))
        drawCompactInputReadouts(rows, in: readout)
    }

    private func drawCompactInputState(_ frame: MockLiveTelemetryFrame) {
        let panel = NSRect(x: 14, y: 14, width: max(160, bounds.width - 28), height: max(120, bounds.height - 56))
        OverlayTheme.Colors.panelBackground.setFill()
        panel.fill()
        OverlayTheme.Colors.windowBorder.setStroke()
        panel.frame()

        let speedMetersPerSecond = 52 + sin(frame.sessionTime * 0.9) * 18
        let gear = max(1, min(6, Int(speedMetersPerSecond / 13)))
        let rpm = 4_800 + speedMetersPerSecond * 72
        let throttle = max(0, min(1, 0.72 + sin(frame.sessionTime * 1.4) * 0.28))
        let brake = max(0, min(1, sin(frame.sessionTime * 0.72) - 0.75))
        let clutch = max(0, min(1, 0.08 + sin(frame.sessionTime / 2.7) * 0.08))
        let steeringDegrees = sin(frame.sessionTime * 1.1) * 9

        let pedalHeight = min(CGFloat(76), max(CGFloat(58), panel.height / 2 - 8))
        let pedalRect = NSRect(x: panel.minX + 10, y: panel.maxY - pedalHeight - 8, width: panel.width - 20, height: pedalHeight)
        drawCompactInputBar(label: "T", value: throttle, color: OverlayTheme.Colors.successText, rect: pedalRect, row: 0)
        drawCompactInputBar(
            label: frame.brakeAbsActive ? "B ABS" : "B",
            value: brake,
            color: frame.brakeAbsActive ? NSColor(red255: 255, green: 209, blue: 102) : OverlayTheme.Colors.errorIndicator,
            rect: pedalRect,
            row: 1)
        drawCompactInputBar(label: "C", value: clutch, color: NSColor(red255: 104, green: 193, blue: 255), rect: pedalRect, row: 2)

        let readoutRect = NSRect(
            x: panel.minX + 10,
            y: panel.minY + 8,
            width: panel.width - 20,
            height: max(44, pedalRect.minY - panel.minY - 16)
        )
        let rows = [
            ("Speed", formatSpeed(speedMetersPerSecond)),
            ("Gear", "\(gear)"),
            ("RPM", "\(Int(rpm))"),
            ("Steer", String(format: "%+.0f deg", steeringDegrees)),
            ("Water", formatTemperature(88 + sin(frame.sessionTime / 90) * 3)),
            ("Oil", formatPressure(4.1))
        ]
        drawCompactInputReadouts(rows, in: readoutRect)
    }

    private func drawCompactInputBar(label: String, value: Double, color: NSColor, rect: NSRect, row: Int) {
        let rowHeight = max(CGFloat(16), rect.height / 3)
        let y = rect.maxY - CGFloat(row + 1) * rowHeight
        let labelAttrs: [NSAttributedString.Key: Any] = [
            .font: OverlayTheme.font(family: fontFamily, size: 10, weight: .semibold),
            .foregroundColor: OverlayTheme.Colors.textSubtle
        ]
        let labelText = NSString(string: label)
        let labelWidth = max(CGFloat(22), labelText.size(withAttributes: labelAttrs).width + 8)
        labelText.draw(at: NSPoint(x: rect.minX, y: y + max(0, (rowHeight - 12) / 2)), withAttributes: labelAttrs)

        let barRect = NSRect(
            x: rect.minX + labelWidth,
            y: y + max(4, rowHeight / 2 - 4),
            width: max(20, rect.width - labelWidth - 52),
            height: 8
        )
        NSColor(calibratedWhite: 1, alpha: 0.16).setFill()
        barRect.fill()
        color.setFill()
        NSRect(x: barRect.minX, y: barRect.minY, width: barRect.width * CGFloat(min(max(value, 0), 1)), height: barRect.height).fill()

        let valueAttrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.monospacedSystemFont(ofSize: 10, weight: .regular),
            .foregroundColor: OverlayTheme.Colors.textPrimary
        ]
        let valueText = NSString(string: formatPercent(value))
        let valueSize = valueText.size(withAttributes: valueAttrs)
        valueText.draw(at: NSPoint(x: rect.maxX - valueSize.width, y: y + max(0, (rowHeight - valueSize.height) / 2)), withAttributes: valueAttrs)
    }

    private func drawCompactInputReadouts(_ rows: [(String, String)], in rect: NSRect) {
        let columns = rect.width >= 300 ? 3 : 2
        let rowCount = Int(ceil(Double(rows.count) / Double(columns)))
        let cellWidth = max(CGFloat(70), rect.width / CGFloat(columns))
        let cellHeight = max(CGFloat(18), rect.height / CGFloat(max(1, rowCount)))
        let labelAttrs: [NSAttributedString.Key: Any] = [
            .font: OverlayTheme.font(family: fontFamily, size: 9),
            .foregroundColor: OverlayTheme.Colors.textSubtle
        ]
        let valueAttrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.monospacedSystemFont(ofSize: 9, weight: .regular),
            .foregroundColor: OverlayTheme.Colors.textPrimary
        ]
        for (index, row) in rows.enumerated() {
            let column = index % columns
            let line = index / columns
            let x = rect.minX + CGFloat(column) * cellWidth
            let y = rect.maxY - CGFloat(line + 1) * cellHeight + max(0, (cellHeight - 12) / 2)
            NSString(string: row.0).draw(at: NSPoint(x: x, y: y), withAttributes: labelAttrs)
            let valueText = NSString(string: row.1)
            let valueSize = valueText.size(withAttributes: valueAttrs)
            valueText.draw(at: NSPoint(x: x + cellWidth - valueSize.width - 4, y: y), withAttributes: valueAttrs)
        }
    }

    private func drawInputWaiting() {
        OverlayTheme.Colors.panelBackground.setFill()
        tableRect.fill()
    }

    private func drawTrace(in graph: NSRect, color: NSColor, select: (InputTracePoint) -> Double) {
        guard inputTrace.count > 1 else {
            return
        }

        let maximumPoints = max(1, inputTrace.count - 1)
        let points = inputTrace.enumerated().map { index, point in
            NSPoint(
                x: graph.minX + CGFloat(index) / CGFloat(maximumPoints) * graph.width,
                y: graph.minY + CGFloat(select(point)) * graph.height
            )
        }

        let path = smoothTracePath(points)
        NSGraphicsContext.saveGraphicsState()
        NSBezierPath(rect: graph).addClip()
        color.setStroke()
        path.lineWidth = 2
        path.stroke()
        NSGraphicsContext.restoreGraphicsState()
    }

    private func drawActiveTraceSegments(
        in graph: NSRect,
        color: NSColor,
        select: (InputTracePoint) -> Double,
        isActive: (InputTracePoint) -> Bool
    ) {
        guard inputTrace.count > 1 else {
            return
        }

        let maximumPoints = max(1, inputTrace.count - 1)
        NSGraphicsContext.saveGraphicsState()
        NSBezierPath(rect: graph).addClip()
        color.setStroke()
        for index in 1..<inputTrace.count where isActive(inputTrace[index]) {
            let previous = inputTrace[index - 1]
            let current = inputTrace[index]
            let path = NSBezierPath()
            path.lineWidth = 3
            path.lineCapStyle = .round
            path.move(to: NSPoint(
                x: graph.minX + CGFloat(index - 1) / CGFloat(maximumPoints) * graph.width,
                y: graph.minY + CGFloat(select(previous)) * graph.height
            ))
            path.line(to: NSPoint(
                x: graph.minX + CGFloat(index) / CGFloat(maximumPoints) * graph.width,
                y: graph.minY + CGFloat(select(current)) * graph.height
            ))
            path.stroke()
        }
        NSGraphicsContext.restoreGraphicsState()
    }

    private func smoothTracePath(_ points: [NSPoint]) -> NSBezierPath {
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

    private func drawInputLegend(in graph: NSRect) {
        let brakeAbsActive = latestFrame?.brakeAbsActive == true
        var items: [(String, NSColor)] = []
        if inputDisplayOptions.showThrottleTrace {
            items.append(("Throttle", OverlayTheme.Colors.successText))
        }
        if inputDisplayOptions.showBrakeTrace {
            items.append((brakeAbsActive ? "Brake ABS" : "Brake", brakeAbsActive ? NSColor(red255: 255, green: 209, blue: 102) : OverlayTheme.Colors.errorIndicator))
        }
        if inputDisplayOptions.showClutchTrace {
            items.append(("Clutch", NSColor(red255: 104, green: 193, blue: 255)))
        }
        var x = graph.minX + 8
        let y = graph.maxY - 22
        for item in items {
            item.1.setFill()
            NSRect(x: x, y: y + 8, width: 14, height: 3).fill()
            x += 18
            let text = NSString(string: item.0)
            let attrs: [NSAttributedString.Key: Any] = [
                .font: OverlayTheme.font(family: fontFamily, size: 11, weight: .semibold),
                .foregroundColor: item.1
            ]
            text.draw(at: NSPoint(x: x, y: y), withAttributes: attrs)
            x += text.size(withAttributes: attrs).width + 14
        }
    }

    private var inputRailEnabled: Bool {
        inputDisplayOptions.showThrottle
            || inputDisplayOptions.showBrake
            || inputDisplayOptions.showClutch
            || inputDisplayOptions.showSteering
            || inputDisplayOptions.showGear
            || inputDisplayOptions.showSpeed
    }

    private func drawInputRail(_ frame: MockLiveTelemetryFrame, rect: NSRect) {
        OverlayTheme.Colors.panelBackground.setFill()
        rect.fill()
        OverlayTheme.Colors.windowBorder.setStroke()
        rect.frame()

        let inner = rect.insetBy(dx: 8, dy: 8)
        var bottom = inner.minY
        let numericItems = inputNumericItems(frame)
        if !numericItems.isEmpty {
            let numericHeight = min(max(inner.height / 4, 32), 42)
            drawInputNumericItems(numericItems, in: NSRect(x: inner.minX, y: bottom, width: inner.width, height: numericHeight))
            bottom += numericHeight + 8
        }

        if inputDisplayOptions.showSteering, inner.maxY - bottom >= 54 {
            let wheelHeight = min(max((inner.maxY - bottom) / 3, 48), 62)
            drawInputWheel(frame, in: NSRect(x: inner.minX, y: bottom, width: inner.width, height: wheelHeight))
            bottom += wheelHeight + 8
        }

        let pedalItems = inputPedalItems(frame)
        guard !pedalItems.isEmpty, inner.maxY - bottom > 34 else {
            return
        }

        drawInputPedals(pedalItems, in: NSRect(x: inner.minX, y: bottom, width: inner.width, height: inner.maxY - bottom))
    }

    private func inputPedalItems(_ frame: MockLiveTelemetryFrame) -> [(String, Double, NSColor)] {
        let last = inputTrace.last
        var items: [(String, Double, NSColor)] = []
        if inputDisplayOptions.showThrottle {
            items.append(("T", last?.throttle ?? 0, OverlayTheme.Colors.successText))
        }
        if inputDisplayOptions.showBrake {
            items.append((frame.brakeAbsActive ? "ABS" : "B", last?.brake ?? 0, frame.brakeAbsActive ? NSColor(red255: 255, green: 209, blue: 102) : OverlayTheme.Colors.errorIndicator))
        }
        if inputDisplayOptions.showClutch {
            items.append(("C", last?.clutch ?? 0, NSColor(red255: 104, green: 193, blue: 255)))
        }
        return items
    }

    private func inputNumericItems(_ frame: MockLiveTelemetryFrame) -> [(String, String)] {
        let speedMetersPerSecond = 52 + sin(frame.sessionTime * 0.9) * 18
        let gear = max(1, min(6, Int(speedMetersPerSecond / 13)))
        var items: [(String, String)] = []
        if inputDisplayOptions.showGear {
            items.append(("Gear", "\(gear)"))
        }
        if inputDisplayOptions.showSpeed {
            items.append(("Speed", formatSpeed(speedMetersPerSecond)))
        }
        return items
    }

    private func drawInputPedals(_ items: [(String, Double, NSColor)], in rect: NSRect) {
        let columnWidth = rect.width / CGFloat(max(1, items.count))
        for (index, item) in items.enumerated() {
            let column = NSRect(x: rect.minX + CGFloat(index) * columnWidth, y: rect.minY, width: columnWidth, height: rect.height)
            drawInputPedal(item, in: column)
        }
    }

    private func drawInputPedal(_ item: (String, Double, NSColor), in rect: NSRect) {
        let labelAttrs: [NSAttributedString.Key: Any] = [
            .font: OverlayTheme.font(family: fontFamily, size: 10, weight: .semibold),
            .foregroundColor: OverlayTheme.Colors.textSubtle
        ]
        let valueAttrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.monospacedSystemFont(ofSize: 10, weight: .regular),
            .foregroundColor: OverlayTheme.Colors.textPrimary
        ]
        NSString(string: item.0).draw(in: NSRect(x: rect.minX, y: rect.maxY - 15, width: rect.width, height: 14), withAttributes: labelAttrs)
        let track = NSRect(x: rect.midX - 7, y: rect.minY + 18, width: 14, height: max(18, rect.height - 38))
        NSColor(calibratedWhite: 1, alpha: 0.16).setFill()
        track.fill()
        item.2.setFill()
        let value = min(max(item.1, 0), 1)
        NSRect(x: track.minX, y: track.minY, width: track.width, height: track.height * value).fill()
        NSString(string: formatPercent(value)).draw(in: NSRect(x: rect.minX, y: rect.minY, width: rect.width, height: 14), withAttributes: valueAttrs)
    }

    private func drawInputWheel(_ frame: MockLiveTelemetryFrame, in rect: NSRect) {
        let labelAttrs: [NSAttributedString.Key: Any] = [
            .font: OverlayTheme.font(family: fontFamily, size: 10, weight: .semibold),
            .foregroundColor: OverlayTheme.Colors.textSubtle
        ]
        let valueAttrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.monospacedSystemFont(ofSize: 10, weight: .regular),
            .foregroundColor: OverlayTheme.Colors.textPrimary
        ]
        NSString(string: "Wheel").draw(at: NSPoint(x: rect.minX, y: rect.maxY - 14), withAttributes: labelAttrs)
        let steering = CGFloat(sin(frame.sessionTime * 1.1) * 0.55)
        NSString(string: String(format: "%+.0f deg", Double(steering * 180 / CGFloat.pi))).draw(at: NSPoint(x: rect.maxX - 56, y: rect.maxY - 14), withAttributes: valueAttrs)

        let size = min(rect.width, rect.height - 18)
        guard size >= 24 else {
            return
        }
        let wheel = NSRect(x: rect.midX - size / 2, y: rect.minY, width: size, height: size)
        OverlayTheme.Colors.textSecondary.setStroke()
        let rim = NSBezierPath(ovalIn: wheel)
        rim.lineWidth = 3
        rim.stroke()
        let center = NSPoint(x: wheel.midX, y: wheel.midY)
        NSColor(red255: 104, green: 193, blue: 255).setStroke()
        for spoke in 0..<3 {
            let theta = steering + CGFloat(spoke) * CGFloat.pi * 2 / 3 - CGFloat.pi / 2
            let end = NSPoint(x: center.x + cos(theta) * size * 0.39, y: center.y + sin(theta) * size * 0.39)
            NSBezierPath.strokeLine(from: center, to: end)
        }
    }

    private func drawInputNumericItems(_ items: [(String, String)], in rect: NSRect) {
        let cellWidth = rect.width / CGFloat(max(1, items.count))
        let labelAttrs: [NSAttributedString.Key: Any] = [
            .font: OverlayTheme.font(family: fontFamily, size: 9, weight: .semibold),
            .foregroundColor: OverlayTheme.Colors.textSubtle
        ]
        let valueAttrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.monospacedSystemFont(ofSize: items.count == 1 ? 13 : 11, weight: .semibold),
            .foregroundColor: OverlayTheme.Colors.textPrimary
        ]
        for (index, item) in items.enumerated() {
            let cell = NSRect(x: rect.minX + CGFloat(index) * cellWidth, y: rect.minY, width: cellWidth, height: rect.height)
            NSString(string: item.0).draw(in: NSRect(x: cell.minX, y: cell.maxY - 13, width: cell.width, height: 12), withAttributes: labelAttrs)
            NSString(string: item.1).draw(in: NSRect(x: cell.minX, y: cell.minY, width: cell.width, height: cell.height - 12), withAttributes: valueAttrs)
        }
    }

    private func drawWheel(_ frame: MockLiveTelemetryFrame) {
        let wheel = NSRect(x: bounds.width - 148, y: bounds.height - 162, width: 108, height: 108)
        OverlayTheme.Colors.textSecondary.setStroke()
        let rim = NSBezierPath(ovalIn: wheel)
        rim.lineWidth = 4
        rim.stroke()

        let steering = CGFloat(sin(frame.sessionTime * 1.1) * 0.55)
        let center = NSPoint(x: wheel.midX, y: wheel.midY)
        NSColor(red255: 104, green: 193, blue: 255).setStroke()
        for spoke in 0..<3 {
            let theta = steering + CGFloat(spoke) * CGFloat.pi * 2 / 3 - CGFloat.pi / 2
            let end = NSPoint(x: center.x + cos(theta) * 42, y: center.y + sin(theta) * 42)
            NSBezierPath.strokeLine(from: center, to: end)
        }
    }

    private func drawInputReadouts(_ frame: MockLiveTelemetryFrame) {
        let speedMetersPerSecond = 52 + sin(frame.sessionTime * 0.9) * 18
        let gear = max(1, min(6, Int(speedMetersPerSecond / 13)))
        let rpm = 4_800 + speedMetersPerSecond * 72
        let rows = [
            ("Speed", formatSpeed(speedMetersPerSecond)),
            ("Gear", "\(gear)"),
            ("RPM", "\(Int(rpm))"),
            ("Cooling", formatTemperature(88 + sin(frame.sessionTime / 90) * 3)),
            ("Oil", formatPressure(4.1))
        ]
        let labelAttrs: [NSAttributedString.Key: Any] = [
            .font: OverlayTheme.font(family: fontFamily, size: 11),
            .foregroundColor: OverlayTheme.Colors.textSubtle
        ]
        let valueAttrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.monospacedSystemFont(ofSize: 11, weight: .regular),
            .foregroundColor: OverlayTheme.Colors.textPrimary
        ]
        var y = bounds.height - 190
        for row in rows {
            NSString(string: row.0).draw(at: NSPoint(x: bounds.width - 152, y: y), withAttributes: labelAttrs)
            NSString(string: row.1).draw(at: NSPoint(x: bounds.width - 82, y: y), withAttributes: valueAttrs)
            y -= 19
        }
    }

    private func appendInputTrace(_ frame: MockLiveTelemetryFrame) {
        let throttle = max(0, min(1, 0.72 + sin(frame.sessionTime * 1.4) * 0.28))
        let brake = max(0, min(1, sin(frame.sessionTime * 0.72) - 0.75))
        let clutch = max(0, min(1, 0.08 + sin(frame.sessionTime / 2.7) * 0.08))
        inputTrace.append(InputTracePoint(throttle: throttle, brake: brake, clutch: clutch, brakeAbsActive: frame.brakeAbsActive))
        if inputTrace.count > 180 {
            inputTrace.removeFirst(inputTrace.count - 180)
        }
    }

    private func applyRows(_ rows: [Row]) {
        for index in 0..<Layout.maximumRows {
            guard index < rows.count else {
                labelCells[index].stringValue = ""
                valueCells[index].stringValue = ""
                labelCells[index].textColor = OverlayTheme.Colors.textSecondary
                valueCells[index].textColor = OverlayTheme.Colors.textPrimary
                continue
            }

            let row = rows[index]
            labelCells[index].stringValue = row.label
            valueCells[index].stringValue = row.value
            labelCells[index].textColor = OverlayTheme.Colors.textSecondary
            valueCells[index].textColor = color(for: row.tone)
        }
    }

    private func applyTone(_ tone: Tone) {
        if kind == .flags {
            layer?.backgroundColor = NSColor.clear.cgColor
            return
        }

        statusLabel.textColor = color(for: tone)
        switch tone {
        case .success:
            layer?.backgroundColor = OverlayTheme.Colors.successBackground.cgColor
        case .warning:
            layer?.backgroundColor = OverlayTheme.Colors.warningBackground.cgColor
        case .error:
            layer?.backgroundColor = OverlayTheme.Colors.errorBackground.cgColor
        default:
            layer?.backgroundColor = OverlayTheme.Colors.windowBackground.cgColor
        }
    }

    private func color(for tone: Tone) -> NSColor {
        switch tone {
        case .success:
            return OverlayTheme.Colors.successText
        case .warning:
            return OverlayTheme.Colors.warningIndicator
        case .error:
            return OverlayTheme.Colors.errorIndicator
        case .info:
            return NSColor(red255: 104, green: 193, blue: 255)
        case .normal:
            return OverlayTheme.Colors.textPrimary
        }
    }

    private func updateSourceVisibility() {
        sourceLabel.isHidden = kind == .flags || kind == .inputState || (!showSourceFooter && overlayError == nil)
    }

    private func applyFonts() {
        titleLabel.font = OverlayTheme.font(family: fontFamily, size: 15, weight: .semibold)
        statusLabel.font = OverlayTheme.font(family: fontFamily, size: 12)
        sourceLabel.font = OverlayTheme.font(family: fontFamily, size: 11)
        for label in labelCells {
            label.font = OverlayTheme.font(family: fontFamily, size: 12, weight: .semibold)
        }
        for value in valueCells {
            value.font = NSFont.monospacedSystemFont(ofSize: 12, weight: .regular)
        }
    }

    private var defaultTitle: String {
        switch kind {
        case .flags:
            return "Flags"
        case .sessionWeather:
            return "Session / Weather"
        case .pitService:
            return "Pit Service"
        case .inputState:
            return "Input / Car State"
        }
    }

    private static func defaultSize(for kind: SimpleTelemetryOverlayKind) -> NSSize {
        switch kind {
        case .flags:
            return FlagsOverlayDefinition.definition.defaultSize
        case .sessionWeather:
            return SessionWeatherOverlayDefinition.definition.defaultSize
        case .pitService:
            return PitServiceOverlayDefinition.definition.defaultSize
        case .inputState:
            return InputStateOverlayDefinition.definition.defaultSize
        }
    }

    private func syntheticFlags(_ frame: MockLiveTelemetryFrame) -> Int {
        if frame.sessionState == 5 {
            return 0x00000001
        }

        let flagCycle = frame.sessionTime.truncatingRemainder(dividingBy: 60)
        if flagCycle > 26 && flagCycle < 35 {
            return 0x00000020
        }

        if frame.weatherDeclaredWet && Int(frame.sessionTime / 45).isMultiple(of: 3) {
            return 0x00000008
        }

        return 0x00000004
    }

    private func displayFlags(flags: Int, sessionState: Int) -> [FlagDisplayItem] {
        var items: [(order: Int, item: FlagDisplayItem)] = []
        if (flags & 0x00000010) != 0 {
            items.append((10, FlagDisplayItem(kind: .red, category: .critical, label: "Red", detail: nil)))
        }
        if (flags & 0x00100000) != 0 {
            items.append((20, FlagDisplayItem(kind: .meatball, category: .critical, label: "Repair", detail: nil)))
        }

        var blackLabels: [String] = []
        appendBlackLabel(flags: flags, bit: 0x00010000, label: "Black", labels: &blackLabels)
        appendBlackLabel(flags: flags, bit: 0x00020000, label: "DQ", labels: &blackLabels)
        appendBlackLabel(flags: flags, bit: 0x00200000, label: "Scoring", labels: &blackLabels)
        appendBlackLabel(flags: flags, bit: 0x00400000, label: "Driver", labels: &blackLabels)
        appendBlackLabel(flags: flags, bit: 0x00080000, label: "Furled", labels: &blackLabels)
        if let first = blackLabels.first {
            items.append((
                30,
                FlagDisplayItem(
                    kind: .black,
                    category: .critical,
                    label: first,
                    detail: blackLabels.count > 1 ? blackLabels.dropFirst().joined(separator: " / ") : nil
                )
            ))
        }

        if (flags & 0x00008000) != 0 || (flags & 0x00004000) != 0 {
            items.append((
                40,
                FlagDisplayItem(
                    kind: .caution,
                    category: .yellow,
                    label: "Caution",
                    detail: (flags & 0x00008000) != 0 ? "waving" : nil
                )
            ))
        } else if (flags & 0x00000008) != 0
            || (flags & 0x00000100) != 0
            || (flags & 0x00000200) != 0
            || (flags & 0x00000040) != 0
            || (flags & 0x00002000) != 0 {
            let label: String
            let detail: String?
            if (flags & 0x00000200) != 0 {
                label = "One to green"
                detail = nil
            } else if (flags & 0x00000040) != 0 {
                label = "Debris"
                detail = nil
            } else {
                label = "Yellow"
                detail = (flags & 0x00000100) != 0 || (flags & 0x00002000) != 0 ? "waving" : nil
            }
            items.append((42, FlagDisplayItem(kind: .yellow, category: .yellow, label: label, detail: detail)))
        }

        if (flags & 0x00000020) != 0 {
            items.append((50, FlagDisplayItem(kind: .blue, category: .blue, label: "Blue", detail: nil)))
        }
        if (flags & 0x00000001) != 0 || sessionState == 5 {
            items.append((60, FlagDisplayItem(kind: .checkered, category: .finish, label: "Checkered", detail: sessionState == 5 ? "session complete" : nil)))
        }
        if (flags & 0x00000002) != 0
            || (flags & 0x00000800) != 0
            || (flags & 0x00001000) != 0
            || (flags & 0x00000080) != 0 {
            let label = (flags & 0x00000002) != 0
                ? "White"
                : (flags & 0x00001000) != 0
                    ? "Five to go"
                    : (flags & 0x00000800) != 0
                        ? "Ten to go"
                        : "Crossed"
            items.append((70, FlagDisplayItem(kind: .white, category: .finish, label: label, detail: nil)))
        }
        if (flags & 0x00000400) != 0
            || (flags & 0x20000000) != 0
            || (flags & 0x40000000) != 0
            || (flags & 0x80000000) != 0 {
            let label = (flags & 0x80000000) != 0
                ? "Start"
                : (flags & 0x40000000) != 0
                    ? "Set"
                    : (flags & 0x20000000) != 0
                        ? "Ready"
                        : "Green"
            let detail = (flags & 0x00000400) != 0 ? "held" : nil
            items.append((80, FlagDisplayItem(kind: .green, category: .green, label: label, detail: detail)))
        }

        return items.sorted { $0.order < $1.order }.map(\.item)
    }

    private func appendBlackLabel(flags: Int, bit: Int, label: String, labels: inout [String]) {
        if (flags & bit) != 0 {
            labels.append(label)
        }
    }

    private func primaryFlag(flags: Int, sessionState: Int) -> String {
        if (flags & 0x00100000) != 0 {
            return "repair flag"
        }

        if (flags & 0x00010000) != 0 {
            return "black flag"
        }

        if (flags & 0x00020000) != 0 {
            return "disqualified"
        }

        if (flags & 0x00200000) != 0 || (flags & 0x00400000) != 0 {
            return "driver flag"
        }

        if (flags & 0x00000010) != 0 {
            return "red flag"
        }

        if (flags & 0x00080000) != 0 {
            return "black flag warning"
        }

        if (flags & 0x00000008) != 0 || (flags & 0x00000040) != 0 {
            return "yellow"
        }

        if (flags & 0x00000020) != 0 {
            return "blue flag"
        }

        if (flags & 0x00000001) != 0 {
            return "checkered"
        }

        if (flags & 0x00000002) != 0 {
            return "white flag"
        }

        if (flags & 0x00001000) != 0 || (flags & 0x00000800) != 0 || (flags & 0x00000080) != 0 {
            return "race countdown"
        }

        return (flags & 0x00000400) != 0 || (flags & 0x80000000) != 0 ? "green" : self.sessionState(sessionState)
    }

    private func flagTone(flags: Int, sessionState: Int) -> Tone {
        if (flags & 0x00010000) != 0
            || (flags & 0x00100000) != 0
            || (flags & 0x00400000) != 0
            || (flags & 0x00200000) != 0
            || (flags & 0x00080000) != 0
            || (flags & 0x00020000) != 0
            || (flags & 0x00000010) != 0 {
            return .error
        }

        if (flags & 0x00000008) != 0 || (flags & 0x00000040) != 0 {
            return .warning
        }

        if (flags & 0x00000020) != 0
            || (flags & 0x00001000) != 0
            || (flags & 0x00000800) != 0
            || (flags & 0x00000080) != 0
            || (flags & 0x00000001) != 0
            || (flags & 0x00000002) != 0 {
            return .info
        }

        if (flags & 0x00000400) != 0 || (flags & 0x00000004) != 0 || sessionState == 4 {
            return .success
        }

        return .info
    }

    private func flagList(_ flags: Int) -> String {
        var labels: [String] = []
        if (flags & 0x00000001) != 0 {
            labels.append("checkered")
        }
        if (flags & 0x00000002) != 0 {
            labels.append("white")
        }
        if (flags & 0x00000004) != 0 {
            labels.append("green")
        }
        if (flags & 0x00000008) != 0 {
            labels.append("yellow")
        }
        if (flags & 0x00000020) != 0 {
            labels.append("blue")
        }
        if (flags & 0x00000040) != 0 {
            labels.append("debris")
        }
        return labels.isEmpty ? "none" : labels.joined(separator: ", ")
    }

    private func selectFlagBorderColor(status: String) -> NSColor? {
        let normalized = status.lowercased()
        guard let category = flagCategory(normalized),
              flagCategoryEnabled(category) else {
            return nil
        }

        if category == .blue {
            return NSColor(red255: 55, green: 162, blue: 255)
        }

        switch category {
        case .green:
            return OverlayTheme.Colors.successText
        case .yellow:
            return OverlayTheme.Colors.warningIndicator
        case .critical:
            return OverlayTheme.Colors.errorIndicator
        case .finish:
            return .white
        case .blue:
            return NSColor(red255: 55, green: 162, blue: 255)
        }
    }

    private func flagCategory(_ normalized: String) -> FlagCategory? {
        if normalized.contains("red")
            || normalized.contains("black")
            || normalized.contains("service")
            || normalized.contains("repair")
            || normalized.contains("disqual")
            || normalized.contains("driver flag")
            || normalized.contains("scoring invalid")
            || normalized.contains("unknown driver")
            || normalized.contains("furled") {
            return .critical
        }

        if normalized.contains("yellow")
            || normalized.contains("caution")
            || normalized.contains("debris")
            || normalized.contains("one lap to green")
            || normalized.contains("random") {
            return .yellow
        }

        if normalized.contains("blue") {
            return .blue
        }

        if normalized.contains("checkered")
            || normalized.contains("white")
            || normalized.contains("countdown")
            || normalized.contains("crossed")
            || normalized.contains("ten to go")
            || normalized.contains("five to go") {
            return .finish
        }

        if normalized.contains("green") || normalized.contains("start go") {
            return .green
        }

        return nil
    }

    private func flagCategoryEnabled(_ category: FlagCategory) -> Bool {
        switch category {
        case .green:
            return flagDisplayOptions.showGreen
        case .blue:
            return flagDisplayOptions.showBlue
        case .yellow:
            return flagDisplayOptions.showYellow
        case .critical:
            return flagDisplayOptions.showCritical
        case .finish:
            return flagDisplayOptions.showFinish
        }
    }

    private func sessionState(_ state: Int) -> String {
        switch state {
        case 4:
            return "racing (4)"
        case 5:
            return "checkered (5)"
        default:
            return "state \(state)"
        }
    }

    private func trackWetnessLabel(_ wetness: Int) -> String {
        switch wetness {
        case 0:
            return "dry"
        case 1:
            return "mostly dry"
        case 2:
            return "damp"
        case 3:
            return "wet"
        default:
            return "wetness \(wetness)"
        }
    }

    private func pitWindow(_ sessionTime: TimeInterval) -> Bool {
        let phase = sessionTime.truncatingRemainder(dividingBy: 2_400)
        return phase > 2_155 && phase < 2_245
    }

    private func pitServiceTone(key: String, value: String, baseTone: Tone) -> Tone {
        let now = Date()
        let normalized = value.trimmingCharacters(in: .whitespacesAndNewlines)
        if let previous = pitServiceLastValues[key], previous != normalized {
            pitServiceHighlightUntil[key] = now.addingTimeInterval(30)
        }

        pitServiceLastValues[key] = normalized
        let isHighlighted = pitServiceHighlightUntil[key].map { $0 >= now } ?? false
        return strongestTone(baseTone, isHighlighted ? .info : .normal)
    }

    private func strongestTone(_ left: Tone, _ right: Tone) -> Tone {
        toneWeight(left) >= toneWeight(right) ? left : right
    }

    private func toneWeight(_ tone: Tone) -> Int {
        switch tone {
        case .error:
            return 50
        case .warning:
            return 40
        case .info:
            return 30
        case .success:
            return 20
        case .normal:
            return 0
        }
    }

    private func formatDuration(_ seconds: Double) -> String {
        let clamped = max(0, seconds)
        let hours = Int(clamped / 3_600)
        let minutes = Int(clamped / 60) % 60
        let secs = Int(clamped) % 60
        return hours > 0
            ? String(format: "%d:%02d:%02d", hours, minutes, secs)
            : String(format: "%d:%02d", minutes, secs)
    }

    private func formatFuel(_ liters: Double) -> String {
        if unitSystem.caseInsensitiveCompare("Imperial") == .orderedSame {
            return String(format: "%.1f gal", liters * 0.264172)
        }

        return String(format: "%.1f L", liters)
    }

    private func formatSpeed(_ metersPerSecond: Double) -> String {
        if unitSystem.caseInsensitiveCompare("Imperial") == .orderedSame {
            return String(format: "%.0f mph", metersPerSecond * 2.23694)
        }

        return String(format: "%.0f km/h", metersPerSecond * 3.6)
    }

    private func formatTemperature(_ celsius: Double) -> String {
        if unitSystem.caseInsensitiveCompare("Imperial") == .orderedSame {
            return String(format: "%.0f F", celsius * 9 / 5 + 32)
        }

        return String(format: "%.0f C", celsius)
    }

    private func formatPressure(_ bar: Double) -> String {
        if unitSystem.caseInsensitiveCompare("Imperial") == .orderedSame {
            return String(format: "%.0f psi", bar * 14.5038)
        }

        return String(format: "%.1f bar", bar)
    }

    private func formatPercent(_ value: Double) -> String {
        String(format: "%.0f%%", min(max(value, 0), 1) * 100)
    }
}

private struct InputTracePoint {
    var throttle: Double
    var brake: Double
    var clutch: Double
    var brakeAbsActive: Bool
}
