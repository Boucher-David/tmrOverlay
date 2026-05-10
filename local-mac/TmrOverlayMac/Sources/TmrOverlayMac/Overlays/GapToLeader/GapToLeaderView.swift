import AppKit

final class GapToLeaderView: NSView {
    private enum Layout {
        static let padding: CGFloat = 14
        static let trendWindow: TimeInterval = 4 * 60 * 60
        static let axisLabelWidth: CGFloat = 64
        static let xAxisLabelLaneHeight: CGFloat = 17
        static let xAxisLabelYOffset: CGFloat = 13
        static let maxTrendPointsPerCar = 36_000
        static let maxWeatherPoints = 36_000
        static let maxDriverChangeMarkers = 64
        static let stickyVisibilityMinimumSeconds: Double = 120
        static let stickyVisibilityLaps: Double = 1.5
        static let entryTailSeconds: Double = 300
        static let entryFadeSeconds: Double = 45
        static let missingSegmentGapSeconds: Double = 10
        static let missingTelemetryGraceSeconds: Double = 5
        static let minimumTrendDomainSeconds: Double = 120
        static let minimumTrendDomainLaps: Double = 1.5
        static let trendRightPaddingSeconds: Double = 20
        static let trendRightPaddingLaps: Double = 0.15
        static let focusScaleMinimumReferenceGapSeconds: Double = 90
        static let focusScaleMinimumReferenceGapLaps: Double = 0.5
        static let focusScaleMinimumRangeSeconds: Double = 20
        static let focusScaleMinimumRangeLaps: Double = 0.1
        static let focusScalePaddingMultiplier: Double = 1.18
        static let focusScaleTriggerRatio: Double = 3
        static let sameLapReferenceBoundaryLaps: Double = 0.95
        static let focusScaleReferenceRatio: CGFloat = 0.56
        static let focusScaleTopPadding: CGFloat = 18
        static let focusScaleBottomPadding: CGFloat = 8
        static let filteredRangeMinimumSeconds: Double = 15
        static let filteredRangeMaximumSeconds: Double = 90
        static let filteredRangeLaps: Double = 0.5
        static let endpointLabelLaneWidth: CGFloat = 38
        static let endpointLabelPinThreshold: CGFloat = 4
        static let endpointLabelHeight: CGFloat = 13
        static let endpointLabelGap: CGFloat = 1
        static let threatBadgeHeight: CGFloat = 16
        static let metricDeadbandMinimumSeconds: Double = 0.25
        static let metricDeadbandLapFraction: Double = 0.0025
        static let threatMinimumGainSeconds: Double = 0.5
        static let threatGainLapFraction: Double = 0.005
        static let metricsFastCadenceSeconds: Double = 2
        static let pitCycleSettleSeconds: Double = 60
        static let denseLeaderChangeCount = 4
        static let metricsTableWidth: CGFloat = 184
        static let metricsTableGap: CGFloat = 10
        static let metricsMinimumPlotWidth: CGFloat = 300
        static let defaultFocusedTrendWindowSeconds: Double = 10 * 60
        static let fuelStintResetMinimumLiters: Double = 5
        static let tacticalWindowSeconds: Double = 10 * 60
        static let tacticalContextHeight: CGFloat = 18
        static let tacticalContextGap: CGFloat = 8
    }

    enum DisplayMode {
        case leaderGap
        case filteredLeaderGap
        case tacticalRelative
    }

    private let titleLabel = NSTextField(labelWithString: "Class Gap Trend")
    private let statusLabel = NSTextField(labelWithString: "waiting")
    private let sourceLabel = NSTextField(labelWithString: "source: waiting")
    private var graphRect = NSRect.zero
    private var gap = LiveLeaderGapSnapshot.unavailable
    // Overlay-local render buffer only. The gap overlay never persists this trace.
    private var series: [Int: [GapTrendPoint]] = [:]
    private var weather: [WeatherTrendPoint] = []
    private var driverChangeMarkers: [DriverChangeMarker] = []
    private var leaderChangeMarkers: [LeaderChangeMarker] = []
    private var carRenderStates: [Int: CarRenderState] = [:]
    private var latestPointAt: Date?
    private var latestAxisSeconds: Double?
    private var trendStartAxisSeconds: Double?
    private var lapReferenceSeconds: Double?
    private var lastGraphRefreshAtUtc: Date?
    private var cachedFocusedTrendMetrics: [FocusedTrendMetric] = []
    private var cachedMetricsCadenceKey: String?
    private var cachedMetricsReferenceCarIdx: Int?
    private var cachedMetricsStintStartAxisSeconds: Double?
    private var lastExplicitTeamDriverKey: String?
    private var currentFuelStintStartAxisSeconds: Double?
    private var lastFuelLevelLiters: Double?
    private var lastClassLeaderCarIdx: Int?
    private var lastSequence = 0
    private var overlayError: String?
    var displayMode: DisplayMode = .filteredLeaderGap {
        didSet {
            titleLabel.stringValue = titleText()
            needsDisplay = true
        }
    }
    var isPaceTimingMode = false {
        didSet { needsDisplay = true }
    }
    var carsAhead = 5 {
        didSet { refreshDesiredCarSelection() }
    }
    var carsBehind = 5 {
        didSet { refreshDesiredCarSelection() }
    }
    var visibleTrendWindowSeconds: TimeInterval? = Layout.defaultFocusedTrendWindowSeconds {
        didSet { needsDisplay = true }
    }
    var graphRefreshIntervalSeconds: TimeInterval = 0 {
        didSet {
            lastGraphRefreshAtUtc = nil
            needsDisplay = true
        }
    }
    var fontFamily = "SF Pro" {
        didSet { applyFonts() }
    }

    override init(frame frameRect: NSRect = NSRect(origin: .zero, size: GapToLeaderOverlayDefinition.definition.defaultSize)) {
        super.init(frame: frameRect)
        setup()
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func layout() {
        super.layout()
        let width = bounds.width
        let height = bounds.height
        titleLabel.frame = NSRect(x: Layout.padding, y: height - 34, width: 220, height: 22)
        statusLabel.frame = NSRect(x: 234, y: height - 34, width: width - 248, height: 22)
        sourceLabel.frame = NSRect(x: Layout.padding, y: 10, width: width - Layout.padding * 2, height: 18)
        graphRect = NSRect(
            x: Layout.padding,
            y: Layout.padding + 22,
            width: width - Layout.padding * 2,
            height: height - Layout.padding * 2 - 46
        )
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)

        NSColor(red255: 24, green: 30, blue: 34).setFill()
        graphRect.fill()
        NSColor(calibratedWhite: 1, alpha: 0.26).setStroke()
        graphRect.frame()

        if let overlayError {
            drawError(overlayError)
            return
        }

        drawGraph()
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        gap = snapshot.leaderGap
        lapReferenceSeconds = selectLapReferenceSeconds(snapshot)
        if snapshot.sequence != lastSequence {
            lastSequence = snapshot.sequence
            record(snapshot)
        }

        let selectedSeries = selectChartSeries()
        let trendDomain = selectTimeDomain(selectedSeries)
        let trendScale = displayMode == .tacticalRelative
            ? nil
            : selectGapScale(selectedSeries, start: trendDomain.start, end: trendDomain.end)
        if displayMode == .tacticalRelative {
            statusLabel.stringValue = gap.hasData ? tacticalStatusText() : "waiting"
            sourceLabel.stringValue = gap.hasData
                ? "\(formatTrendWindow(Layout.tacticalWindowSeconds)) \(tacticalFocusText()) | lap \(formatLapReference()) | cars \(selectedSeries.count)"
                : "source: waiting"
        } else if displayMode == .filteredLeaderGap {
            statusLabel.stringValue = gap.hasData
                ? filteredStatusText()
                : "waiting"
            let scaleText = trendScale?.isFocusRelative == true ? "local scale" : focusedTrendDescriptor()
            sourceLabel.stringValue = gap.hasData
                ? "\(formatTrendWindow(trendDomain.end - trendDomain.start)) \(scaleText) | lap \(formatLapReference()) | range +/-\(formatPlainSeconds(filteredGapRangeSeconds())) | cars \(selectedSeries.count)"
                : "source: waiting"
        } else {
            statusLabel.stringValue = gap.hasData
                ? "\(position(gap.referenceClassPosition)) \(gapText(gap.classLeaderGap))"
                : "waiting"
            let scaleText = trendScale?.isFocusRelative == true ? "local scale" : "class trend"
            sourceLabel.stringValue = gap.hasData
                ? "\(formatTrendWindow(trendDomain.end - trendDomain.start)) \(scaleText) | cars \(selectedSeries.count)"
                : "source: waiting"
        }
        overlayError = nil
        applyStatusColor()
        needsLayout = true
        if shouldRefreshGraph(at: snapshot.latestFrame?.capturedAtUtc ?? snapshot.lastUpdatedAtUtc ?? Date()) {
            needsDisplay = true
        }
    }

    private func shouldRefreshGraph(at timestamp: Date) -> Bool {
        guard graphRefreshIntervalSeconds.isFinite, graphRefreshIntervalSeconds > 0 else {
            lastGraphRefreshAtUtc = timestamp
            return true
        }

        guard let lastGraphRefreshAtUtc else {
            self.lastGraphRefreshAtUtc = timestamp
            return true
        }

        if timestamp.timeIntervalSince(lastGraphRefreshAtUtc) >= graphRefreshIntervalSeconds {
            self.lastGraphRefreshAtUtc = timestamp
            return true
        }

        return false
    }

    private func titleText() -> String {
        switch displayMode {
        case .leaderGap:
            return "Class Gap Trend"
        case .filteredLeaderGap:
            return "Focused Gap Trend"
        case .tacticalRelative:
            return "Near Me Gap"
        }
    }

    func showOverlayError(_ message: String) {
        overlayError = message
        statusLabel.stringValue = "graph error"
        sourceLabel.stringValue = trimError(message)
        layer?.backgroundColor = NSColor(red255: 42, green: 18, blue: 22, alpha: 0.88).cgColor
        statusLabel.textColor = NSColor(red255: 236, green: 112, blue: 99)
        needsDisplay = true
    }

    func resetTrend() {
        gap = .unavailable
        series.removeAll()
        weather.removeAll()
        driverChangeMarkers.removeAll()
        leaderChangeMarkers.removeAll()
        carRenderStates.removeAll()
        latestPointAt = nil
        latestAxisSeconds = nil
        trendStartAxisSeconds = nil
        lapReferenceSeconds = nil
        lastGraphRefreshAtUtc = nil
        cachedFocusedTrendMetrics.removeAll()
        cachedMetricsCadenceKey = nil
        cachedMetricsReferenceCarIdx = nil
        cachedMetricsStintStartAxisSeconds = nil
        lastExplicitTeamDriverKey = nil
        currentFuelStintStartAxisSeconds = nil
        lastFuelLevelLiters = nil
        lastClassLeaderCarIdx = nil
        lastSequence = 0
        overlayError = nil
        statusLabel.stringValue = "waiting"
        sourceLabel.stringValue = "source: waiting"
        applyStatusColor()
        needsDisplay = true
    }

    private func setup() {
        wantsLayer = true
        layer?.borderWidth = 1
        layer?.borderColor = NSColor(calibratedWhite: 1, alpha: 0.28).cgColor
        layer?.backgroundColor = NSColor(red255: 18, green: 30, blue: 42, alpha: 0.88).cgColor
        titleLabel.stringValue = titleText()

        configure(titleLabel, font: overlayFont(ofSize: 15, weight: .semibold), color: .white)
        configure(statusLabel, font: overlayFont(ofSize: 12, weight: .regular), color: NSColor(red255: 140, green: 190, blue: 245), alignment: .right)
        configure(sourceLabel, font: overlayFont(ofSize: 11, weight: .regular), color: NSColor(red255: 128, green: 145, blue: 153))
        [titleLabel, statusLabel, sourceLabel].forEach(addSubview)
    }

    private func drawError(_ message: String) {
        NSColor(red255: 42, green: 18, blue: 22, alpha: 0.58).setFill()
        graphRect.fill()
        NSColor(red255: 236, green: 112, blue: 99, alpha: 0.72).setStroke()
        graphRect.frame()

        let titleAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 13, weight: .semibold),
            .foregroundColor: NSColor(red255: 255, green: 225, blue: 220, alpha: 0.94)
        ]
        let detailAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 10, weight: .regular),
            .foregroundColor: NSColor(red255: 255, green: 225, blue: 220, alpha: 0.78)
        ]
        drawCentered("gap graph error", rect: graphRect.offsetBy(dx: 0, dy: 6), attributes: titleAttrs)
        drawCentered(trimError(message), rect: graphRect.offsetBy(dx: 0, dy: -18), attributes: detailAttrs)
    }

    private func drawCentered(_ text: String, rect: NSRect, attributes: [NSAttributedString.Key: Any]) {
        let string = NSString(string: text)
        let size = string.size(withAttributes: attributes)
        string.draw(
            at: NSPoint(x: rect.midX - size.width / 2, y: rect.midY - size.height / 2),
            withAttributes: attributes
        )
    }

    private func configure(
        _ label: NSTextField,
        font: NSFont,
        color: NSColor,
        alignment: NSTextAlignment = .left
    ) {
        label.font = font
        label.textColor = color
        label.alignment = alignment
        label.backgroundColor = .clear
        label.isBordered = false
        label.isEditable = false
    }

    private func applyFonts() {
        titleLabel.font = overlayFont(ofSize: 15, weight: .semibold)
        statusLabel.font = overlayFont(ofSize: 12, weight: .regular)
        sourceLabel.font = overlayFont(ofSize: 11, weight: .regular)
        needsLayout = true
        needsDisplay = true
    }

    private func overlayFont(ofSize size: CGFloat, weight: NSFont.Weight) -> NSFont {
        NSFont(name: fontFamily, size: size) ?? NSFont.systemFont(ofSize: size, weight: weight)
    }

    private func record(_ snapshot: LiveTelemetrySnapshot) {
        let timestamp = snapshot.latestFrame?.capturedAtUtc ?? snapshot.lastUpdatedAtUtc ?? Date()
        let axisSeconds = selectAxisSeconds(timestamp: timestamp, sessionTime: snapshot.latestFrame?.sessionTime)
        latestPointAt = timestamp
        latestAxisSeconds = axisSeconds
        if trendStartAxisSeconds == nil || axisSeconds < (trendStartAxisSeconds ?? axisSeconds) {
            trendStartAxisSeconds = axisSeconds
        }

        recordWeather(snapshot, axisSeconds: axisSeconds)
        recordDriverChange(snapshot, timestamp: timestamp, axisSeconds: axisSeconds)
        recordFuelStint(snapshot, axisSeconds: axisSeconds)
        recordLeaderChange(snapshot, timestamp: timestamp, axisSeconds: axisSeconds)

        for car in snapshot.leaderGap.classCars {
            guard let gapSeconds = chartGapSeconds(car) else {
                continue
            }

            var points = series[car.carIdx] ?? []
            let startsSegment = points.isEmpty || axisSeconds - (points.last?.axisSeconds ?? axisSeconds) > Layout.missingSegmentGapSeconds
            points.append(GapTrendPoint(
                timestamp: timestamp,
                axisSeconds: axisSeconds,
                gapSeconds: gapSeconds,
                isReferenceCar: car.isReferenceCar,
                isClassLeader: car.isClassLeader,
                classPosition: car.classPosition,
                startsSegment: startsSegment
            ))
            if points.count > Layout.maxTrendPointsPerCar {
                points.removeFirst(points.count - Layout.maxTrendPointsPerCar)
            }
            series[car.carIdx] = points
        }

        updateCarRenderStates(snapshot, axisSeconds: axisSeconds)
        pruneOldPoints(latestAxisSeconds: axisSeconds)
    }

    private func recordWeather(_ snapshot: LiveTelemetrySnapshot, axisSeconds: Double) {
        let condition = weatherCondition(snapshot)
        if let last = weather.last, abs(last.axisSeconds - axisSeconds) < 0.001 {
            weather[weather.count - 1] = WeatherTrendPoint(axisSeconds: axisSeconds, condition: condition)
        } else {
            weather.append(WeatherTrendPoint(axisSeconds: axisSeconds, condition: condition))
        }

        if weather.count > Layout.maxWeatherPoints {
            weather.removeFirst(weather.count - Layout.maxWeatherPoints)
        }
    }

    private func recordDriverChange(_ snapshot: LiveTelemetrySnapshot, timestamp: Date, axisSeconds: Double) {
        guard let frame = snapshot.latestFrame else {
            return
        }

        if let previous = lastExplicitTeamDriverKey,
           previous != frame.teamDriverKey,
           let reference = snapshot.leaderGap.classCars.first(where: { $0.isReferenceCar }),
           let gapSeconds = chartGapSeconds(reference) {
            driverChangeMarkers.append(DriverChangeMarker(
                timestamp: timestamp,
                axisSeconds: axisSeconds,
                carIdx: reference.carIdx,
                gapSeconds: gapSeconds,
                isReferenceCar: true,
                label: frame.teamDriverInitials
            ))
            if driverChangeMarkers.count > Layout.maxDriverChangeMarkers {
                driverChangeMarkers.removeFirst(driverChangeMarkers.count - Layout.maxDriverChangeMarkers)
            }
        }

        lastExplicitTeamDriverKey = frame.teamDriverKey
    }

    private func recordFuelStint(_ snapshot: LiveTelemetrySnapshot, axisSeconds: Double) {
        guard let fuelLevelLiters = snapshot.latestFrame?.fuelLevelLiters,
              fuelLevelLiters.isFinite else {
            return
        }

        if currentFuelStintStartAxisSeconds == nil {
            currentFuelStintStartAxisSeconds = axisSeconds
        } else if let lastFuelLevelLiters,
                  fuelLevelLiters - lastFuelLevelLiters >= Layout.fuelStintResetMinimumLiters {
            currentFuelStintStartAxisSeconds = axisSeconds
        }

        lastFuelLevelLiters = fuelLevelLiters
    }

    private func recordLeaderChange(_ snapshot: LiveTelemetrySnapshot, timestamp: Date, axisSeconds: Double) {
        guard let leaderCarIdx = snapshot.leaderGap.classLeaderCarIdx else {
            return
        }

        if let previous = lastClassLeaderCarIdx, previous != leaderCarIdx {
            leaderChangeMarkers.append(LeaderChangeMarker(
                timestamp: timestamp,
                axisSeconds: axisSeconds,
                previousLeaderCarIdx: previous,
                newLeaderCarIdx: leaderCarIdx
            ))
        }

        lastClassLeaderCarIdx = leaderCarIdx
    }

    private func updateCarRenderStates(_ snapshot: LiveTelemetrySnapshot, axisSeconds: Double) {
        let desiredCarIds = selectDesiredCarIds(snapshot.leaderGap.classCars)
        for car in snapshot.leaderGap.classCars {
            guard let gapSeconds = chartGapSeconds(car) else {
                continue
            }

            let state = carRenderStates[car.carIdx] ?? CarRenderState(carIdx: car.carIdx)
            let wasVisible = shouldKeepVisible(state, axisSeconds: axisSeconds)
            state.lastSeenAxisSeconds = axisSeconds
            state.lastGapSeconds = gapSeconds
            state.isReferenceCar = car.isReferenceCar
            state.isClassLeader = car.isClassLeader
            state.classPosition = car.classPosition
            state.deltaSecondsToReference = car.deltaSecondsToReference
            state.classColorHex = car.carClassColorHex
            state.isCurrentlyDesired = desiredCarIds.contains(car.carIdx)
            if state.isCurrentlyDesired {
                if !wasVisible {
                    state.visibleSinceAxisSeconds = axisSeconds
                }
                state.lastDesiredAxisSeconds = axisSeconds
            }
            carRenderStates[car.carIdx] = state
        }

        for state in carRenderStates.values where !desiredCarIds.contains(state.carIdx) {
            state.isCurrentlyDesired = false
        }
    }

    private func refreshDesiredCarSelection() {
        guard gap.hasData else {
            needsDisplay = true
            return
        }

        let axisSeconds = latestAxisSeconds ?? selectAxisSeconds(timestamp: latestPointAt ?? Date(), sessionTime: nil)
        let desiredCarIds = selectDesiredCarIds(gap.classCars)
        for car in gap.classCars {
            guard let state = carRenderStates[car.carIdx] else {
                continue
            }

            let wasVisible = shouldKeepVisible(state, axisSeconds: axisSeconds)
            state.isCurrentlyDesired = desiredCarIds.contains(car.carIdx)
            if state.isCurrentlyDesired {
                if !wasVisible {
                    state.visibleSinceAxisSeconds = axisSeconds
                }
                state.lastDesiredAxisSeconds = axisSeconds
            }
        }

        for state in carRenderStates.values where !desiredCarIds.contains(state.carIdx) {
            state.isCurrentlyDesired = false
        }

        cachedFocusedTrendMetrics.removeAll()
        cachedMetricsCadenceKey = nil
        lastGraphRefreshAtUtc = nil
        needsDisplay = true
    }

    private func drawGraph() {
        if displayMode == .tacticalRelative {
            drawTacticalGraph()
            return
        }

        let inset = graphRect.insetBy(dx: 12, dy: 14)
        let inner = NSRect(
            x: inset.minX,
            y: inset.minY + Layout.xAxisLabelLaneHeight,
            width: inset.width,
            height: max(40, inset.height - Layout.xAxisLabelLaneHeight - 4)
        )
        let metricsTableWidth = focusedMetricsTableWidth(inner: inner)
        let metricsTableRect = metricsTableWidth > 0
            ? NSRect(
                x: inner.maxX - metricsTableWidth,
                y: inner.minY,
                width: metricsTableWidth,
                height: inner.height
            )
            : .zero
        let chartRight = metricsTableWidth > 0
            ? metricsTableRect.minX - Layout.metricsTableGap
            : inner.maxX
        let labelLane = NSRect(
            x: chartRight - Layout.endpointLabelLaneWidth,
            y: inner.minY,
            width: Layout.endpointLabelLaneWidth,
            height: inner.height
        )
        let plot = NSRect(
            x: inner.minX + Layout.axisLabelWidth,
            y: inner.minY,
            width: max(40, labelLane.minX - (inner.minX + Layout.axisLabelWidth)),
            height: inner.height
        )
        let selectedSeries = selectChartSeries()
        let domain = selectTimeDomain(selectedSeries)
        let gapScale = selectGapScale(selectedSeries, start: domain.start, end: domain.end)
        let threatMetric = displayMode == .filteredLeaderGap ? activeThreatMetric() : nil
        let threatCarIdx = threatMetric?.chaser?.carIdx
        let denseLeaderCycle = isDenseLeaderCycle(domain: domain)
        let endpointLabelCarIds = endpointLabelCarIds(
            selectedSeries,
            threatCarIdx: threatCarIdx,
            denseLeaderCycle: denseLeaderCycle
        )

        drawWeatherBands(plot: plot, domain: domain)
        drawGridLines(plot: plot, domain: domain, gapScale: gapScale)
        drawLeaderChangeMarkers(plot: plot, domain: domain, denseLeaderCycle: denseLeaderCycle)

        NSColor(calibratedWhite: 1, alpha: 0.92).setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: plot.maxY), to: NSPoint(x: plot.maxX, y: plot.maxY))

        var endpointLabels: [EndpointLabel] = []
        for selection in selectedSeries.sorted(by: { drawPriority($0.state, threatCarIdx: threatCarIdx) < drawPriority($1.state, threatCarIdx: threatCarIdx) }) {
            let state = selection.state
            if gapScale.isFocusRelative, state.isClassLeader {
                continue
            }

            guard let points = series[state.carIdx] else {
                continue
            }

            let visiblePoints = points
                .filter { $0.axisSeconds >= selection.drawStartSeconds && $0.axisSeconds >= domain.start && $0.axisSeconds <= domain.end }
            guard !visiblePoints.isEmpty else {
                continue
            }

            let color = seriesColor(state, threatCarIdx: threatCarIdx).withAlphaComponent(
                selection.alpha * seriesAlphaMultiplier(
                    state,
                    threatCarIdx: threatCarIdx,
                    denseLeaderCycle: denseLeaderCycle
                )
            )
            color.setStroke()
            color.setFill()
            drawSeriesSegments(visiblePoints, color: color, isReferenceCar: state.isReferenceCar, dashed: selection.isStale || selection.isStickyExit, domain: domain, gapScale: gapScale, plot: plot)
            if endpointLabelCarIds.contains(state.carIdx) {
                appendPositionLabel(&endpointLabels, state: state, point: visiblePoints[visiblePoints.count - 1], color: color, domain: domain, gapScale: gapScale, plot: plot)
            }
            if selection.isStale {
                drawTerminalMarker(visiblePoints[visiblePoints.count - 1], color: color, domain: domain, gapScale: gapScale, plot: plot)
            }
        }

        if let threatMetric {
            drawThreatAnnotation(threatMetric, plot: plot)
        }
        drawPositionLabels(endpointLabels, plot: plot, labelRect: labelLane)
        drawDriverChangeMarkers(plot: plot, domain: domain, gapScale: gapScale)
        drawScaleLabels(plot: plot, gapScale: gapScale)
        if metricsTableWidth > 0 {
            drawFocusedMetricsTable(metricsTableRect)
        }
    }

    private func focusedMetricsTableWidth(inner: NSRect) -> CGFloat {
        guard displayMode == .filteredLeaderGap else {
            return 0
        }

        let availableAfterTable = inner.width
            - Layout.axisLabelWidth
            - Layout.endpointLabelLaneWidth
            - Layout.metricsTableGap
            - Layout.metricsTableWidth
        return availableAfterTable >= Layout.metricsMinimumPlotWidth ? Layout.metricsTableWidth : 0
    }

    private func isDenseLeaderCycle(domain: (start: Double, end: Double)) -> Bool {
        leaderChangeMarkers.filter { $0.axisSeconds >= domain.start && $0.axisSeconds <= domain.end }.count >= Layout.denseLeaderChangeCount
    }

    private func endpointLabelCarIds(
        _ selectedSeries: [ChartSeriesSelection],
        threatCarIdx: Int?,
        denseLeaderCycle: Bool
    ) -> Set<Int> {
        guard denseLeaderCycle else {
            return Set(selectedSeries.map { $0.state.carIdx })
        }

        var selected = Set<Int>()
        for selection in selectedSeries {
            let state = selection.state
            if state.isReferenceCar || state.isClassLeader || state.carIdx == threatCarIdx {
                selected.insert(state.carIdx)
            }
        }

        if let closestAhead = selectedSeries
            .map(\.state)
            .filter({ !$0.isReferenceCar && !$0.isClassLeader && $0.carIdx != threatCarIdx && ($0.deltaSecondsToReference ?? 0) < 0 })
            .max(by: { ($0.deltaSecondsToReference ?? -Double.greatestFiniteMagnitude) < ($1.deltaSecondsToReference ?? -Double.greatestFiniteMagnitude) }) {
            selected.insert(closestAhead.carIdx)
        }

        if let closestBehind = selectedSeries
            .map(\.state)
            .filter({ !$0.isReferenceCar && !$0.isClassLeader && $0.carIdx != threatCarIdx && ($0.deltaSecondsToReference ?? 0) > 0 })
            .min(by: { ($0.deltaSecondsToReference ?? Double.greatestFiniteMagnitude) < ($1.deltaSecondsToReference ?? Double.greatestFiniteMagnitude) }) {
            selected.insert(closestBehind.carIdx)
        }

        return selected
    }

    private func drawTacticalGraph() {
        let inner = graphRect.insetBy(dx: 12, dy: 14).offsetBy(dx: 0, dy: -4)
        let plot = NSRect(
            x: inner.minX + Layout.axisLabelWidth,
            y: inner.minY + Layout.tacticalContextHeight + Layout.tacticalContextGap,
            width: max(40, inner.width - Layout.axisLabelWidth),
            height: max(40, inner.height - Layout.tacticalContextHeight - Layout.tacticalContextGap)
        )
        let context = NSRect(
            x: plot.minX,
            y: inner.minY,
            width: plot.width,
            height: Layout.tacticalContextHeight
        )
        let selectedSeries = selectChartSeries()
        let latest = latestAxisSeconds ?? selectAxisSeconds(timestamp: latestPointAt ?? Date(), sessionTime: nil)
        let domain = (
            start: latest - Layout.tacticalWindowSeconds,
            end: latest
        )
        guard let referenceState = carRenderStates.values.first(where: { $0.isReferenceCar }),
              let referencePoints = series[referenceState.carIdx],
              !referencePoints.isEmpty else {
            drawTacticalWaiting(plot: plot)
            return
        }

        drawWeatherBands(plot: plot, domain: domain)
        let tacticalSeries = selectedSeries
            .compactMap { tacticalSelection($0, domain: domain, referencePoints: referencePoints) }
        let maxDelta = tacticalMaxDelta(tacticalSeries)
        drawTacticalGrid(plot: plot, maxDelta: maxDelta)

        var endpointLabels: [EndpointLabel] = []
        for selection in tacticalSeries.sorted(by: { tacticalDrawPriority($0.state) < tacticalDrawPriority($1.state) }) {
            let color = seriesColor(selection.state, threatCarIdx: nil).withAlphaComponent(selection.alpha * seriesAlphaMultiplier(selection.state))
            color.setStroke()
            color.setFill()
            let graphPoints = selection.points.map { tacticalGraphPoint($0, domain: domain, maxDelta: maxDelta, plot: plot) }
            drawSegment(graphPoints, color: color, isReferenceCar: selection.state.isReferenceCar, dashed: selection.isStale || selection.isStickyExit)
            if let last = selection.points.last {
                appendTacticalPositionLabel(
                    &endpointLabels,
                    selection: selection,
                    point: last,
                    color: color,
                    domain: domain,
                    maxDelta: maxDelta,
                    plot: plot
                )
            }
        }

        drawPositionLabels(endpointLabels, plot: plot)
        drawTacticalContextStrip(context, domain: domain)
    }

    private func drawTacticalWaiting(plot: NSRect) {
        NSColor(calibratedWhite: 1, alpha: 0.16).setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: plot.midY), to: NSPoint(x: plot.maxX, y: plot.midY))
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 10, weight: .regular),
            .foregroundColor: NSColor(red255: 138, green: 152, blue: 160)
        ]
        drawAxisLabel("team", y: plot.midY, plot: plot, attrs: attrs)
    }

    private func drawTacticalGrid(plot: NSRect, maxDelta: Double) {
        NSColor(calibratedWhite: 1, alpha: 0.24).setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: plot.midY), to: NSPoint(x: plot.maxX, y: plot.midY))

        NSColor(calibratedWhite: 1, alpha: 0.12).setStroke()
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 138, green: 152, blue: 160, alpha: 0.78)
        ]
        let step = niceGridStep(maxDelta / 2)
        var value = step
        while value < maxDelta {
            let aheadY = tacticalY(-value, maxDelta: maxDelta, plot: plot)
            let behindY = tacticalY(value, maxDelta: maxDelta, plot: plot)
            NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: aheadY), to: NSPoint(x: plot.maxX, y: aheadY))
            NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: behindY), to: NSPoint(x: plot.maxX, y: behindY))
            drawAxisLabel("-\(formatPlainSeconds(value))", y: aheadY, plot: plot, attrs: attrs)
            drawAxisLabel("+\(formatPlainSeconds(value))", y: behindY, plot: plot, attrs: attrs)
            value += step
        }

        drawAxisLabel("team", y: plot.midY, plot: plot, attrs: attrs)
    }

    private func drawTacticalContextStrip(_ rect: NSRect, domain: (start: Double, end: Double)) {
        NSColor(red255: 24, green: 30, blue: 34, alpha: 0.88).setFill()
        rect.fill()
        NSColor(calibratedWhite: 1, alpha: 0.16).setStroke()
        rect.frame()

        let total = FourHourRacePreview.sessionLengthSeconds
        for start in stride(from: 0.0, through: total, by: 120) {
            let end = min(total, start + 120)
            let condition = FourHourRacePreview.weatherDeclaredWet(sessionTime: start)
                ? WeatherCondition.declaredWet
                : weatherCondition(wetness: FourHourRacePreview.trackWetness(sessionTime: start))
            guard let color = weatherBandColor(condition) else {
                continue
            }
            let x = contextX(start, rect: rect)
            let right = contextX(end, rect: rect)
            color.withAlphaComponent(color.alphaComponent * 2).setFill()
            NSRect(x: x, y: rect.minY, width: max(1, right - x), height: rect.height).fill()
        }

        let highlightStart = max(0, min(total, domain.start))
        let highlightEnd = max(0, min(total, domain.end))
        let left = contextX(highlightStart, rect: rect)
        let right = contextX(highlightEnd, rect: rect)
        NSColor(calibratedWhite: 1, alpha: 0.18).setFill()
        NSRect(x: left, y: rect.minY, width: max(2, right - left), height: rect.height).fill()
        NSColor(calibratedWhite: 1, alpha: 0.65).setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: right, y: rect.minY), to: NSPoint(x: right, y: rect.maxY))
    }

    private func drawGridLines(plot: NSRect, domain: (start: Double, end: Double), gapScale: GapScale) {
        drawLapIntervalLines(plot: plot, domain: domain)

        if gapScale.isFocusRelative {
            drawFocusRelativeGridLines(plot: plot, gapScale: gapScale)
            return
        }

        NSColor(calibratedWhite: 1, alpha: 0.13).setStroke()
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 138, green: 152, blue: 160, alpha: 0.72)
        ]
        let step = niceGridStep(gapScale.maxGapSeconds / 4)
        var value = step
        while value < gapScale.maxGapSeconds {
            let y = gapY(value, maxGap: gapScale.maxGapSeconds, plot: plot)
            NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: y), to: NSPoint(x: plot.maxX, y: y))
            drawAxisLabel(formatAxisSeconds(value), y: y, plot: plot, attrs: attrs)
            value += step
        }

        guard let lapSeconds = lapReferenceSeconds,
              lapSeconds >= 20,
              gapScale.maxGapSeconds >= lapSeconds * 0.85 else {
            return
        }

        NSColor(calibratedWhite: 1, alpha: 0.58).setStroke()
        let lapAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(calibratedWhite: 1, alpha: 0.82)
        ]
        var lap = 1
        while Double(lap) * lapSeconds < gapScale.maxGapSeconds {
            let y = gapY(Double(lap) * lapSeconds, maxGap: gapScale.maxGapSeconds, plot: plot)
            NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: y), to: NSPoint(x: plot.maxX, y: y))
            drawAxisLabel("+\(lap) lap", y: y, plot: plot, attrs: lapAttrs)
            lap += 1
        }
    }

    private func drawFocusRelativeGridLines(plot: NSRect, gapScale: GapScale) {
        NSColor(calibratedWhite: 1, alpha: 0.13).setStroke()
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 138, green: 152, blue: 160, alpha: 0.72)
        ]
        let focusAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 112, green: 224, blue: 146, alpha: 0.86)
        ]
        let focusY = focusReferenceY(plot: plot)
        NSColor(red255: 112, green: 224, blue: 146, alpha: 0.36).setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: focusY), to: NSPoint(x: plot.maxX, y: focusY))
        drawAxisLabel("focus", y: focusY, plot: plot, attrs: focusAttrs)

        NSColor(calibratedWhite: 1, alpha: 0.13).setStroke()
        let aheadStep = niceGridStep(gapScale.aheadSeconds / 2)
        var ahead = aheadStep
        while ahead < gapScale.aheadSeconds {
            let y = gapDeltaY(-ahead, gapScale: gapScale, plot: plot)
            NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: y), to: NSPoint(x: plot.maxX, y: y))
            drawAxisLabel(formatDeltaSeconds(-ahead), y: y, plot: plot, attrs: attrs)
            ahead += aheadStep
        }

        let behindStep = niceGridStep(gapScale.behindSeconds / 2)
        var behind = behindStep
        while behind < gapScale.behindSeconds {
            let y = gapDeltaY(behind, gapScale: gapScale, plot: plot)
            NSBezierPath.strokeLine(from: NSPoint(x: plot.minX, y: y), to: NSPoint(x: plot.maxX, y: y))
            drawAxisLabel(formatDeltaSeconds(behind), y: y, plot: plot, attrs: attrs)
            behind += behindStep
        }
    }

    private func drawLapIntervalLines(plot: NSRect, domain: (start: Double, end: Double)) {
        guard let lapSeconds = lapReferenceSeconds, lapSeconds >= 20 else {
            return
        }

        let intervalSeconds = lapSeconds * 5
        let duration = domain.end - domain.start
        guard duration >= intervalSeconds * 0.75 else {
            return
        }

        NSColor(calibratedWhite: 1, alpha: 0.13).setStroke()
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 138, green: 152, blue: 160, alpha: 0.72)
        ]

        var elapsed = intervalSeconds
        while elapsed < duration {
            let x = plot.minX + CGFloat(elapsed / duration) * plot.width
            NSBezierPath.strokeLine(from: NSPoint(x: x, y: plot.minY), to: NSPoint(x: x, y: plot.maxY))
            NSString(string: String(format: "%.0fL", elapsed / lapSeconds)).draw(
                at: NSPoint(x: x - 10, y: plot.minY - Layout.xAxisLabelYOffset),
                withAttributes: attrs
            )
            elapsed += intervalSeconds
        }
    }

    private func drawWeatherBands(plot: NSRect, domain: (start: Double, end: Double)) {
        let points = weather
            .filter { $0.axisSeconds <= domain.end }
            .sorted { $0.axisSeconds < $1.axisSeconds }
        guard !points.isEmpty else {
            return
        }

        let startIndex = points.lastIndex { $0.axisSeconds <= domain.start }
            ?? points.firstIndex { $0.axisSeconds >= domain.start }
        guard let startIndex else {
            return
        }

        for index in startIndex..<points.count {
            let point = points[index]
            let segmentStart = max(domain.start, point.axisSeconds)
            let segmentEnd = index + 1 < points.count
                ? min(domain.end, points[index + 1].axisSeconds)
                : domain.end
            guard segmentEnd > domain.start, segmentEnd > segmentStart,
                  let color = weatherBandColor(point.condition) else {
                continue
            }

            let left = axisX(segmentStart, domain: domain, plot: plot)
            let right = axisX(segmentEnd, domain: domain, plot: plot)
            color.setFill()
            NSRect(x: left, y: plot.minY, width: max(1, right - left), height: plot.height).fill()

            if point.condition == .declaredWet {
                NSColor(red255: 94, green: 190, blue: 255, alpha: 0.15).setFill()
                NSRect(x: left, y: plot.maxY - 4, width: max(1, right - left), height: 4).fill()
            }
        }
    }

    private func drawDriverChangeMarkers(plot: NSRect, domain: (start: Double, end: Double), gapScale: GapScale) {
        let markers = driverChangeMarkers.filter { $0.axisSeconds >= domain.start && $0.axisSeconds <= domain.end }
        guard !markers.isEmpty else {
            return
        }

        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 205, green: 218, blue: 228, alpha: 0.86)
        ]

        for marker in markers {
            let point = graphPoint(marker, domain: domain, gapScale: gapScale, plot: plot)
            NSColor(calibratedWhite: 1, alpha: 0.8).setStroke()
            NSBezierPath.strokeLine(from: NSPoint(x: point.x, y: point.y - 9), to: NSPoint(x: point.x, y: point.y + 9))
            NSColor(red255: 18, green: 30, blue: 42, alpha: 0.92).setFill()
            NSBezierPath(ovalIn: NSRect(x: point.x - 4.5, y: point.y - 4.5, width: 9, height: 9)).fill()
            NSColor(red255: 112, green: 224, blue: 146).setStroke()
            NSBezierPath(ovalIn: NSRect(x: point.x - 4.5, y: point.y - 4.5, width: 9, height: 9)).stroke()
            NSString(string: marker.label).draw(at: NSPoint(x: point.x + 6, y: point.y + 6), withAttributes: attrs)
        }
    }

    private func drawLeaderChangeMarkers(plot: NSRect, domain: (start: Double, end: Double), denseLeaderCycle: Bool) {
        let markers = leaderChangeMarkers.filter { $0.axisSeconds >= domain.start && $0.axisSeconds <= domain.end }
        guard !markers.isEmpty else {
            return
        }

        if denseLeaderCycle {
            drawDenseLeaderCycleMarkers(markers, plot: plot, domain: domain)
            return
        }

        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 218, green: 226, blue: 230, alpha: 0.58)
        ]

        for marker in markers {
            let x = axisX(marker.axisSeconds, domain: domain, plot: plot)
            let path = NSBezierPath()
            path.lineWidth = 1
            path.setLineDash([2, 3], count: 2, phase: 0)
            path.move(to: NSPoint(x: x, y: plot.minY))
            path.line(to: NSPoint(x: x, y: plot.maxY))
            NSColor(calibratedWhite: 1, alpha: 0.45).setStroke()
            path.stroke()
            NSString(string: "leader").draw(at: NSPoint(x: x + 4, y: plot.maxY - 14), withAttributes: attrs)
        }
    }

    private func drawDenseLeaderCycleMarkers(
        _ markers: [LeaderChangeMarker],
        plot: NSRect,
        domain: (start: Double, end: Double)
    ) {
        let stroke = NSColor(calibratedWhite: 1, alpha: 0.34)
        for marker in markers {
            let x = axisX(marker.axisSeconds, domain: domain, plot: plot)
            stroke.setStroke()
            NSBezierPath.strokeLine(
                from: NSPoint(x: x, y: plot.maxY - 11),
                to: NSPoint(x: x, y: plot.maxY)
            )
        }

        let text = "leader cycle x\(markers.count)"
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 8.5, weight: .regular),
            .foregroundColor: NSColor(red255: 218, green: 226, blue: 230, alpha: 0.74)
        ]
        let string = NSString(string: text)
        let size = string.size(withAttributes: attrs)
        let badge = NSRect(
            x: plot.minX + 5,
            y: plot.maxY - size.height - 5,
            width: size.width + 8,
            height: size.height + 3
        )
        NSColor(red255: 18, green: 24, blue: 28, alpha: 0.72).setFill()
        badge.fill()
        string.draw(at: NSPoint(x: badge.minX + 4, y: badge.minY + 1), withAttributes: attrs)
    }

    private func drawSeriesSegments(
        _ points: [GapTrendPoint],
        color: NSColor,
        isReferenceCar: Bool,
        dashed: Bool,
        domain: (start: Double, end: Double),
        gapScale: GapScale,
        plot: NSRect
    ) {
        var segment: [NSPoint] = []
        for point in points {
            if point.startsSegment, !segment.isEmpty {
                drawSegment(segment, color: color, isReferenceCar: isReferenceCar, dashed: dashed)
                segment.removeAll()
            }
            segment.append(graphPoint(point, domain: domain, gapScale: gapScale, plot: plot))
        }

        drawSegment(segment, color: color, isReferenceCar: isReferenceCar, dashed: dashed)
    }

    private func drawSegment(_ points: [NSPoint], color: NSColor, isReferenceCar: Bool, dashed: Bool) {
        guard let first = points.first else {
            return
        }

        color.setStroke()
        color.setFill()
        if points.count == 1 {
            NSBezierPath(ovalIn: NSRect(x: first.x - 3, y: first.y - 3, width: 6, height: 6)).fill()
            return
        }

        let path = NSBezierPath()
        path.lineWidth = isReferenceCar ? 2.8 : 1.5
        if dashed {
            path.setLineDash([5, 4], count: 2, phase: 0)
        }
        path.move(to: first)
        points.dropFirst().forEach { path.line(to: $0) }
        path.stroke()

        if let last = points.last {
            let radius: CGFloat = isReferenceCar ? 4.5 : 3
            NSBezierPath(ovalIn: NSRect(x: last.x - radius, y: last.y - radius, width: radius * 2, height: radius * 2)).fill()
        }
    }

    private func appendPositionLabel(
        _ labels: inout [EndpointLabel],
        state: CarRenderState,
        point: GapTrendPoint,
        color: NSColor,
        domain: (start: Double, end: Double),
        gapScale: GapScale,
        plot: NSRect
    ) {
        guard let position = state.classPosition, position > 0 else {
            return
        }

        let graphPoint = graphPoint(point, domain: domain, gapScale: gapScale, plot: plot)
        labels.append(EndpointLabel(
            text: "P\(position)",
            point: graphPoint,
            color: color,
            isReferenceCar: state.isReferenceCar,
            isClassLeader: state.isClassLeader
        ))
    }

    private func drawPositionLabels(_ labels: [EndpointLabel], plot: NSRect, labelRect: NSRect? = nil) {
        guard !labels.isEmpty else {
            return
        }

        if let labelRect {
            let pinnedLabels = labels.filter { shouldPinPositionLabel($0, plot: plot) }
            let floatingLabels = labels.filter { !shouldPinPositionLabel($0, plot: plot) }
            for label in floatingLabels.sorted(by: positionLabelDrawOrder) {
                let y = clampedPositionLabelY(label.point.y - Layout.endpointLabelHeight / 2, bounds: plot)
                drawPositionLabel(label, y: y, plot: plot, labelRect: nil, pinnedToLane: false)
            }
            drawPinnedPositionLabels(pinnedLabels, plot: plot, labelRect: labelRect)
            return
        }

        let labelHeight = Layout.endpointLabelHeight
        let labelGap = Layout.endpointLabelGap
        let labelBounds = labelRect ?? plot
        let minY = labelBounds.minY + 1
        let maxY = labelBounds.maxY - labelHeight - 1
        var positioned = labels
            .sorted {
                if $0.point.y == $1.point.y {
                    if $0.isReferenceCar != $1.isReferenceCar {
                        return $0.isReferenceCar
                    }
                    return $0.isClassLeader && !$1.isClassLeader
                }

                return $0.point.y < $1.point.y
            }
            .map { PositionedEndpointLabel(label: $0, y: $0.point.y - labelHeight / 2) }

        for index in positioned.indices {
            var y = min(max(positioned[index].y, minY), maxY)
            if index > positioned.startIndex {
                y = max(y, positioned[index - 1].y + labelHeight + labelGap)
            }
            positioned[index].y = y
        }

        if let last = positioned.last, last.y > maxY {
            let shift = last.y - maxY
            for index in positioned.indices {
                positioned[index].y = max(minY, positioned[index].y - shift)
            }
        }

        for item in positioned {
            drawPositionLabel(item.label, y: item.y, plot: plot, labelRect: nil, pinnedToLane: false)
        }
    }

    private func drawPinnedPositionLabels(_ labels: [EndpointLabel], plot: NSRect, labelRect: NSRect) {
        guard !labels.isEmpty else {
            return
        }

        let referenceLabels = labels.filter { $0.isReferenceCar }
        let otherLabels = labels.filter { !$0.isReferenceCar }
        let referenceYValues = referenceLabels.map {
            clampedPositionLabelY($0.point.y - Layout.endpointLabelHeight / 2, bounds: labelRect)
        }
        let referencePositioned = referenceLabels.map { label in
            PositionedEndpointLabel(label: label, y: clampedPositionLabelY(label.point.y - Layout.endpointLabelHeight / 2, bounds: labelRect))
        }

        var otherPositioned: [PositionedEndpointLabel] = []
        for label in otherLabels.sorted(by: { $0.point.y < $1.point.y }) {
            var y = clampedPositionLabelY(label.point.y - Layout.endpointLabelHeight / 2, bounds: labelRect)
            for referenceY in referenceYValues where labelRangesOverlap(y, referenceY) {
                y = label.point.y < referenceY
                    ? referenceY - Layout.endpointLabelHeight - Layout.endpointLabelGap
                    : referenceY + Layout.endpointLabelHeight + Layout.endpointLabelGap
            }
            if let previous = otherPositioned.last {
                y = max(y, previous.y + Layout.endpointLabelHeight + Layout.endpointLabelGap)
            }
            otherPositioned.append(PositionedEndpointLabel(label: label, y: y))
        }

        let minY = labelRect.minY + 1
        let maxY = labelRect.maxY - Layout.endpointLabelHeight - 1
        if let last = otherPositioned.last, last.y > maxY {
            let shift = last.y - maxY
            for index in otherPositioned.indices {
                otherPositioned[index].y = max(minY, otherPositioned[index].y - shift)
            }
        }

        let positioned = otherPositioned + referencePositioned
        for item in positioned.sorted(by: positionLabelLayerOrder) {
            drawPositionLabel(item.label, y: item.y, plot: plot, labelRect: labelRect, pinnedToLane: true)
        }
    }

    private func shouldPinPositionLabel(_ label: EndpointLabel, plot: NSRect) -> Bool {
        label.point.x >= plot.maxX - Layout.endpointLabelPinThreshold
    }

    private func clampedPositionLabelY(_ y: CGFloat, bounds: NSRect) -> CGFloat {
        min(max(y, bounds.minY + 1), bounds.maxY - Layout.endpointLabelHeight - 1)
    }

    private func labelRangesOverlap(_ leftY: CGFloat, _ rightY: CGFloat) -> Bool {
        abs(leftY - rightY) < Layout.endpointLabelHeight + Layout.endpointLabelGap
    }

    private func positionLabelDrawOrder(_ left: EndpointLabel, _ right: EndpointLabel) -> Bool {
        if left.isReferenceCar != right.isReferenceCar {
            return !left.isReferenceCar
        }
        if left.isClassLeader != right.isClassLeader {
            return !left.isClassLeader
        }
        return left.point.y < right.point.y
    }

    private func positionLabelLayerOrder(_ left: PositionedEndpointLabel, _ right: PositionedEndpointLabel) -> Bool {
        positionLabelDrawOrder(left.label, right.label)
    }

    private func drawPositionLabel(_ label: EndpointLabel, y: CGFloat, plot: NSRect, labelRect: NSRect?, pinnedToLane: Bool) {
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: label.isReferenceCar ? 10 : 9, weight: .regular),
            .foregroundColor: label.color.withAlphaComponent(label.isReferenceCar ? label.color.alphaComponent : label.color.alphaComponent * 0.78)
        ]
        let text = NSString(string: label.text)
        let size = text.size(withAttributes: attrs)
        let labelBounds = pinnedToLane ? (labelRect ?? plot) : plot
        let x = pinnedToLane
            ? min(labelBounds.maxX - size.width - 1, max(labelBounds.minX + 4, label.point.x + 8))
            : min(labelBounds.maxX - size.width - 2, label.point.x + 6)
        let backgroundRect = NSRect(x: x - 2, y: y, width: size.width + 4, height: 13)

        if pinnedToLane || abs(y + 6.5 - label.point.y) > 3 {
            let path = NSBezierPath()
            path.lineWidth = 1
            path.move(to: NSPoint(x: label.point.x + 3, y: label.point.y))
            path.line(to: NSPoint(x: backgroundRect.minX, y: y + 6.5))
            label.color.withAlphaComponent(label.color.alphaComponent * 0.32).setStroke()
            path.stroke()
        }

        NSColor(red255: 18, green: 30, blue: 42, alpha: label.isReferenceCar ? 0.74 : 0.59).setFill()
        backgroundRect.fill()
        text.draw(at: NSPoint(x: x, y: y - 1), withAttributes: attrs)
    }

    private func drawTerminalMarker(
        _ point: GapTrendPoint,
        color: NSColor,
        domain: (start: Double, end: Double),
        gapScale: GapScale,
        plot: NSRect
    ) {
        let graphPoint = graphPoint(point, domain: domain, gapScale: gapScale, plot: plot)
        color.setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: graphPoint.x - 4, y: graphPoint.y - 4), to: NSPoint(x: graphPoint.x + 4, y: graphPoint.y + 4))
        NSBezierPath.strokeLine(from: NSPoint(x: graphPoint.x - 4, y: graphPoint.y + 4), to: NSPoint(x: graphPoint.x + 4, y: graphPoint.y - 4))
    }

    private func drawThreatAnnotation(_ metric: FocusedTrendMetric, plot: NSRect) {
        guard let chaser = metric.chaser else {
            return
        }

        let danger = dangerColor(alpha: 0.96)
        let text = "THREAT \(chaser.label) \(formatChangeSeconds(chaser.gainSeconds)) \(metric.label)"
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 8.5, weight: .semibold),
            .foregroundColor: danger
        ]
        let string = NSString(string: text)
        let size = string.size(withAttributes: attrs)
        let x = min(max(plot.minX + 2, plot.midX - size.width / 2), plot.maxX - size.width - 8)
        let y = plot.minY + 6
        let badge = NSRect(x: x - 4, y: y - 2, width: size.width + 8, height: Layout.threatBadgeHeight)

        NSColor(red255: 18, green: 24, blue: 28, alpha: 0.84).setFill()
        badge.fill()
        danger.withAlphaComponent(0.38).setStroke()
        badge.frame()
        string.draw(at: NSPoint(x: x, y: y), withAttributes: attrs)
    }

    private func drawScaleLabels(plot: NSRect, gapScale: GapScale) {
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 10, weight: .regular),
            .foregroundColor: NSColor(red255: 138, green: 152, blue: 160)
        ]
        drawAxisLabel(scaleTopReferenceLabel(), y: plot.maxY, plot: plot, attrs: attrs)
        if gapScale.isFocusRelative {
            drawAxisLabel(formatDeltaSeconds(-gapScale.aheadSeconds), y: plot.maxY - Layout.focusScaleTopPadding, plot: plot, attrs: attrs)
            drawAxisLabel(formatDeltaSeconds(gapScale.behindSeconds), y: plot.minY + Layout.focusScaleBottomPadding, plot: plot, attrs: attrs)
            return
        }

        drawAxisLabel(formatAxisSeconds(gapScale.maxGapSeconds), y: plot.minY, plot: plot, attrs: attrs)
    }

    private func drawFocusedMetricsTable(_ rect: NSRect) {
        NSColor(red255: 18, green: 24, blue: 28, alpha: 0.74).setFill()
        rect.fill()
        NSColor(calibratedWhite: 1, alpha: 0.15).setStroke()
        rect.frame()

        let titleAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 10, weight: .semibold),
            .foregroundColor: NSColor(red255: 220, green: 230, blue: 236, alpha: 0.92)
        ]
        let headerAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 8, weight: .regular),
            .foregroundColor: NSColor(red255: 126, green: 144, blue: 154, alpha: 0.9)
        ]
        let rowAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 205, green: 218, blue: 228, alpha: 0.9)
        ]

        NSString(string: "TREND").draw(at: NSPoint(x: rect.minX + 8, y: rect.maxY - 16), withAttributes: titleAttrs)
        NSString(string: "win").draw(at: NSPoint(x: rect.minX + 8, y: rect.maxY - 31), withAttributes: headerAttrs)
        NSString(string: "leader d").draw(at: NSPoint(x: rect.minX + 43, y: rect.maxY - 31), withAttributes: headerAttrs)
        NSString(string: "threat").draw(at: NSPoint(x: rect.minX + 104, y: rect.maxY - 31), withAttributes: headerAttrs)

        for (index, metric) in focusedTrendMetrics().enumerated() {
            let y = rect.maxY - 48 - CGFloat(index) * 22
            NSString(string: metric.label).draw(at: NSPoint(x: rect.minX + 8, y: y), withAttributes: rowAttrs)
            drawMetricValue(metric, at: NSPoint(x: rect.minX + 43, y: y), positiveIsGood: false)
            drawChaserValue(metric, at: NSPoint(x: rect.minX + 104, y: y))
        }
    }

    private func drawMetricValue(_ metric: FocusedTrendMetric, at point: NSPoint, positiveIsGood: Bool) {
        let value = metric.focusGapChange
        let color: NSColor
        let text: String
        switch metric.state {
        case .ready:
            if let value {
                color = metricChangeColor(value, positiveIsGood: positiveIsGood)
                text = formatChangeSeconds(value)
            } else {
                color = NSColor(red255: 105, green: 120, blue: 130, alpha: 0.72)
                text = "--"
            }
        case .warming(let label):
            color = NSColor(red255: 126, green: 144, blue: 154, alpha: 0.9)
            text = label
        case .leaderChanged:
            color = NSColor(red255: 126, green: 144, blue: 154, alpha: 0.9)
            text = "leader"
        case .unavailable:
            color = NSColor(red255: 105, green: 120, blue: 130, alpha: 0.72)
            text = "--"
        }

        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: color
        ]
        NSString(string: text).draw(at: point, withAttributes: attrs)
    }

    private func drawChaserValue(_ metric: FocusedTrendMetric, at point: NSPoint) {
        let color: NSColor
        let text: String
        switch metric.state {
        case .ready:
            if let chaser = metric.chaser {
                color = dangerColor(alpha: 0.96)
                text = "\(chaser.label) \(formatChangeSeconds(chaser.gainSeconds))"
            } else {
                color = NSColor(red255: 105, green: 120, blue: 130, alpha: 0.72)
                text = "--"
            }
        case .leaderChanged:
            color = NSColor(red255: 126, green: 144, blue: 154, alpha: 0.9)
            text = "reset"
        case .warming, .unavailable:
            color = NSColor(red255: 105, green: 120, blue: 130, alpha: 0.72)
            text = "--"
        }

        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: color
        ]
        NSString(string: text).draw(at: point, withAttributes: attrs)
    }

    private func activeThreatMetric() -> FocusedTrendMetric? {
        focusedTrendMetrics()
            .compactMap { metric -> FocusedTrendMetric? in
                guard case .ready = metric.state,
                      let chaser = metric.chaser,
                      chaser.gainSeconds >= threatGainThresholdSeconds() else {
                    return nil
                }
                return metric
            }
            .max { ($0.chaser?.gainSeconds ?? 0) < ($1.chaser?.gainSeconds ?? 0) }
    }

    private func focusedTrendMetrics() -> [FocusedTrendMetric] {
        guard let lapReferenceSeconds,
              isValidLapReference(lapReferenceSeconds),
              let referenceState = carRenderStates.values.first(where: { $0.isReferenceCar }) else {
            return [
                FocusedTrendMetric(label: "5L", focusGapChange: nil, chaser: nil, state: .unavailable),
                FocusedTrendMetric(label: "10L", focusGapChange: nil, chaser: nil, state: .unavailable),
                FocusedTrendMetric(label: "stint", focusGapChange: nil, chaser: nil, state: .unavailable)
            ]
        }

        let latest = latestAxisSeconds ?? selectAxisSeconds(timestamp: latestPointAt ?? Date(), sessionTime: nil)
        let cadenceKey = metricsCadenceKey(latest: latest, lapReferenceSeconds: lapReferenceSeconds)
        if cachedMetricsCadenceKey == cadenceKey,
           cachedMetricsReferenceCarIdx == referenceState.carIdx,
           cachedMetricsStintStartAxisSeconds == currentFuelStintStartAxisSeconds,
           !cachedFocusedTrendMetrics.isEmpty {
            return cachedFocusedTrendMetrics
        }

        let metrics = [
            focusedTrendMetric(label: "5L", lookbackSeconds: lapReferenceSeconds * 5, targetLaps: 5, latest: latest),
            focusedTrendMetric(label: "10L", lookbackSeconds: lapReferenceSeconds * 10, targetLaps: 10, latest: latest),
            focusedStintTrendMetric(latest: latest)
        ]
        cachedFocusedTrendMetrics = metrics
        cachedMetricsCadenceKey = cadenceKey
        cachedMetricsReferenceCarIdx = referenceState.carIdx
        cachedMetricsStintStartAxisSeconds = currentFuelStintStartAxisSeconds
        return metrics
    }

    private func metricsCadenceKey(latest: Double, lapReferenceSeconds: Double) -> String {
        if isPitCycleActive(latest: latest) {
            let bucket = Int((latest / Layout.metricsFastCadenceSeconds).rounded(.down))
            return "pit-\(bucket)"
        }

        let lapKey = Int((latest / lapReferenceSeconds).rounded(.down))
        return "lap-\(lapKey)"
    }

    private func isPitCycleActive(latest: Double) -> Bool {
        if leaderChangeMarkers.contains(where: { latest - $0.axisSeconds <= Layout.pitCycleSettleSeconds && latest >= $0.axisSeconds }) {
            return true
        }

        if let currentFuelStintStartAxisSeconds,
           latest - currentFuelStintStartAxisSeconds <= Layout.pitCycleSettleSeconds,
           latest >= currentFuelStintStartAxisSeconds {
            return true
        }

        return false
    }

    private func focusedStintTrendMetric(latest: Double) -> FocusedTrendMetric {
        guard let currentFuelStintStartAxisSeconds else {
            return FocusedTrendMetric(label: "stint", focusGapChange: nil, chaser: nil, state: .unavailable)
        }

        let lookbackSeconds = latest - currentFuelStintStartAxisSeconds
        guard lookbackSeconds >= 5 else {
            return FocusedTrendMetric(label: "stint", focusGapChange: nil, chaser: nil, state: .warming("out lap"))
        }

        return focusedTrendMetric(label: "stint", lookbackSeconds: lookbackSeconds, targetLaps: nil, latest: latest)
    }

    private func focusedTrendMetric(label: String, lookbackSeconds: Double, targetLaps: Double?, latest: Double) -> FocusedTrendMetric {
        guard lookbackSeconds.isFinite, lookbackSeconds > 0,
              let referenceState = carRenderStates.values.first(where: { $0.isReferenceCar }),
              let referenceCurrent = latestTrendPoint(carIdx: referenceState.carIdx) else {
            return FocusedTrendMetric(label: label, focusGapChange: nil, chaser: nil, state: .unavailable)
        }

        let targetAxisSeconds = latest - lookbackSeconds
        if leaderChangedBetween(start: targetAxisSeconds, end: latest) {
            return FocusedTrendMetric(label: label, focusGapChange: nil, chaser: nil, state: .leaderChanged)
        }

        guard let referencePast = trendPoint(carIdx: referenceState.carIdx, near: targetAxisSeconds) else {
            return FocusedTrendMetric(
                label: label,
                focusGapChange: nil,
                chaser: nil,
                state: warmupState(referenceCarIdx: referenceState.carIdx, latest: latest, targetLaps: targetLaps)
            )
        }

        let focusGapChange = referenceCurrent.gapSeconds - referencePast.gapSeconds
        let chaser = strongestBehindGain(
            referenceState: referenceState,
            referenceCurrent: referenceCurrent,
            referencePast: referencePast,
            targetAxisSeconds: targetAxisSeconds
        )
        return FocusedTrendMetric(label: label, focusGapChange: focusGapChange, chaser: chaser, state: .ready)
    }

    private func warmupState(referenceCarIdx: Int, latest: Double, targetLaps: Double?) -> TrendMetricState {
        guard let targetLaps,
              targetLaps > 0,
              let lapReferenceSeconds,
              isValidLapReference(lapReferenceSeconds),
              let first = firstTrendPoint(carIdx: referenceCarIdx) else {
            return .unavailable
        }

        let availableLaps = max(0, (latest - first.axisSeconds) / lapReferenceSeconds)
        return .warming(String(format: "%.1fL", min(availableLaps, targetLaps)))
    }

    private func leaderChangedBetween(start: Double, end: Double) -> Bool {
        leaderChangeMarkers.contains { marker in
            marker.axisSeconds > start && marker.axisSeconds <= end
        }
    }

    private func strongestBehindGain(
        referenceState: CarRenderState,
        referenceCurrent: GapTrendPoint,
        referencePast: GapTrendPoint,
        targetAxisSeconds: Double
    ) -> BehindGainMetric? {
        carRenderStates.values
            .compactMap { state -> BehindGainMetric? in
                guard state.carIdx != referenceState.carIdx,
                      !state.isReferenceCar,
                      let current = latestTrendPoint(carIdx: state.carIdx),
                      current.gapSeconds > referenceCurrent.gapSeconds,
                      let past = trendPoint(carIdx: state.carIdx, near: targetAxisSeconds) else {
                    return nil
                }

                let currentDelta = current.gapSeconds - referenceCurrent.gapSeconds
                let pastDelta = past.gapSeconds - referencePast.gapSeconds
                let gain = pastDelta - currentDelta
                guard gain >= threatGainThresholdSeconds() else {
                    return nil
                }

                return BehindGainMetric(carIdx: state.carIdx, label: carShortLabel(state), gainSeconds: gain)
            }
            .max { $0.gainSeconds < $1.gainSeconds }
    }

    private func latestTrendPoint(carIdx: Int) -> GapTrendPoint? {
        series[carIdx]?.last
    }

    private func firstTrendPoint(carIdx: Int) -> GapTrendPoint? {
        series[carIdx]?.first
    }

    private func trendPoint(carIdx: Int, near axisSeconds: Double) -> GapTrendPoint? {
        guard let points = series[carIdx], !points.isEmpty else {
            return nil
        }

        let tolerance = trendLookupToleranceSeconds()
        guard let point = points.min(by: { abs($0.axisSeconds - axisSeconds) < abs($1.axisSeconds - axisSeconds) }),
              abs(point.axisSeconds - axisSeconds) <= tolerance else {
            return nil
        }

        return point
    }

    private func trendLookupToleranceSeconds() -> Double {
        min(60, max(8, (lapReferenceSeconds ?? 60) * 0.08))
    }

    private func carShortLabel(_ state: CarRenderState) -> String {
        return "#\(state.carIdx)"
    }

    private func drawAxisLabel(
        _ text: String,
        y: CGFloat,
        plot: NSRect,
        attrs: [NSAttributedString.Key: Any]
    ) {
        let string = NSString(string: text)
        let size = string.size(withAttributes: attrs)
        let x = plot.minX - 8 - size.width
        string.draw(at: NSPoint(x: x, y: y - size.height / 2), withAttributes: attrs)
    }

    private func tacticalSelection(
        _ selection: ChartSeriesSelection,
        domain: (start: Double, end: Double),
        referencePoints: [GapTrendPoint]
    ) -> TacticalSeriesSelection? {
        guard let points = series[selection.state.carIdx] else {
            return nil
        }

        let tacticalPoints = points
            .filter { $0.axisSeconds >= selection.drawStartSeconds && $0.axisSeconds >= domain.start && $0.axisSeconds <= domain.end }
            .compactMap { point -> TacticalTrendPoint? in
                let referenceGap = selection.state.isReferenceCar
                    ? point.gapSeconds
                    : referenceGapSeconds(at: point.axisSeconds, referencePoints: referencePoints)
                guard let referenceGap else {
                    return nil
                }

                return TacticalTrendPoint(
                    axisSeconds: point.axisSeconds,
                    deltaSecondsToReference: point.gapSeconds - referenceGap,
                    classPosition: point.classPosition,
                    startsSegment: point.startsSegment
                )
            }
        guard !tacticalPoints.isEmpty else {
            return nil
        }

        return TacticalSeriesSelection(
            state: selection.state,
            points: tacticalPoints,
            alpha: selection.alpha,
            isStickyExit: selection.isStickyExit,
            isStale: selection.isStale
        )
    }

    private func referenceGapSeconds(at axisSeconds: Double, referencePoints: [GapTrendPoint]) -> Double? {
        referencePoints
            .min { abs($0.axisSeconds - axisSeconds) < abs($1.axisSeconds - axisSeconds) }
            .flatMap { abs($0.axisSeconds - axisSeconds) <= 2 ? $0.gapSeconds : nil }
    }

    private func referenceGap(at axisSeconds: Double, referencePoints: [GapTrendPoint]) -> Double {
        guard !referencePoints.isEmpty else {
            return 0
        }

        if axisSeconds <= referencePoints[0].axisSeconds {
            return referencePoints[0].gapSeconds
        }

        let last = referencePoints[referencePoints.count - 1]
        if axisSeconds >= last.axisSeconds {
            return last.gapSeconds
        }

        var low = 0
        var high = referencePoints.count - 1
        while low <= high {
            let mid = low + (high - low) / 2
            let midSeconds = referencePoints[mid].axisSeconds
            if abs(midSeconds - axisSeconds) < 0.001 {
                return referencePoints[mid].gapSeconds
            }

            if midSeconds < axisSeconds {
                low = mid + 1
            } else {
                high = mid - 1
            }
        }

        let afterIndex = min(max(low, 0), referencePoints.count - 1)
        let beforeIndex = min(max(low - 1, 0), referencePoints.count - 1)
        let after = referencePoints[afterIndex]
        let before = referencePoints[beforeIndex]
        let span = after.axisSeconds - before.axisSeconds
        guard span > 0.001 else {
            return before.gapSeconds
        }

        let ratio = min(max((axisSeconds - before.axisSeconds) / span, 0), 1)
        return before.gapSeconds + (after.gapSeconds - before.gapSeconds) * ratio
    }

    private func tacticalMaxDelta(_ selections: [TacticalSeriesSelection]) -> Double {
        let maxDelta = selections
            .flatMap(\.points)
            .map { abs($0.deltaSecondsToReference) }
            .max() ?? 5
        return niceCeiling(max(5, maxDelta * 1.15))
    }

    private func tacticalGraphPoint(
        _ point: TacticalTrendPoint,
        domain: (start: Double, end: Double),
        maxDelta: Double,
        plot: NSRect
    ) -> NSPoint {
        let total = max(1, domain.end - domain.start)
        let xRatio = min(max((point.axisSeconds - domain.start) / total, 0), 1)
        return NSPoint(
            x: plot.minX + CGFloat(xRatio) * plot.width,
            y: tacticalY(point.deltaSecondsToReference, maxDelta: maxDelta, plot: plot)
        )
    }

    private func tacticalY(_ deltaSeconds: Double, maxDelta: Double, plot: NSRect) -> CGFloat {
        let ratio = min(max(deltaSeconds / maxDelta, -1), 1)
        return plot.midY - CGFloat(ratio) * plot.height / 2
    }

    private func appendTacticalPositionLabel(
        _ labels: inout [EndpointLabel],
        selection: TacticalSeriesSelection,
        point: TacticalTrendPoint,
        color: NSColor,
        domain: (start: Double, end: Double),
        maxDelta: Double,
        plot: NSRect
    ) {
        guard let position = selection.state.classPosition ?? point.classPosition, position > 0 else {
            return
        }

        labels.append(EndpointLabel(
            text: selection.state.isReferenceCar ? "P\(position) team" : "P\(position)",
            point: tacticalGraphPoint(point, domain: domain, maxDelta: maxDelta, plot: plot),
            color: color,
            isReferenceCar: selection.state.isReferenceCar,
            isClassLeader: selection.state.isClassLeader
        ))
    }

    private func tacticalStatusText() -> String {
        let cars = gap.classCars
        let ahead = cars
            .compactMap { car -> Double? in
                guard !car.isReferenceCar,
                      let delta = car.deltaSecondsToReference,
                      delta < 0 else {
                    return nil
                }
                return abs(delta)
            }
            .min()
        let behind = cars
            .compactMap { car -> Double? in
                guard !car.isReferenceCar,
                      let delta = car.deltaSecondsToReference,
                      delta > 0 else {
                    return nil
                }
                return delta
            }
            .min()
        return "P\(position(gap.referenceClassPosition)) A \(formatPlainSeconds(ahead)) | B \(formatPlainSeconds(behind))"
    }

    private func scaleTopReferenceLabel() -> String {
        if focusIsSameLapAsClassLeader() {
            return "leader"
        }

        if let position = highestEligibleReferencePosition() {
            return "P\(position)"
        }

        return closestIneligibleHigherReferenceLabel() ?? "best"
    }

    private func focusIsSameLapAsClassLeader() -> Bool {
        guard let reference = gap.classCars.first(where: { $0.isReferenceCar }) else {
            return gap.classLeaderGap.isLeader
        }

        if reference.isClassLeader {
            return true
        }

        guard let leader = gap.classCars.first(where: { $0.isClassLeader }) else {
            return false
        }

        return isSameLapReferenceCandidate(leader, comparedTo: reference)
    }

    private func highestEligibleReferencePosition() -> Int? {
        guard let reference = gap.classCars.first(where: { $0.isReferenceCar }) else {
            return nil
        }

        let referencePosition = reference.classPosition ?? gap.referenceClassPosition
        return gap.classCars
            .filter { !$0.isReferenceCar && !$0.isClassLeader }
            .filter { car in
                guard let referencePosition, let classPosition = car.classPosition else {
                    return false
                }

                return classPosition < referencePosition
                    && isSameLapReferenceCandidate(car, comparedTo: reference)
            }
            .compactMap(\.classPosition)
            .min()
    }

    private func closestIneligibleHigherReferenceLabel() -> String? {
        guard let reference = gap.classCars.first(where: { $0.isReferenceCar }),
              let referencePosition = reference.classPosition ?? gap.referenceClassPosition,
              let referenceGapLaps = normalizedClassLeaderGapLaps(reference) else {
            return nil
        }

        let closest = gap.classCars
            .filter { !$0.isReferenceCar }
            .compactMap { car -> (position: Int, lapDelta: Double)? in
                guard let classPosition = car.classPosition,
                      classPosition < referencePosition,
                      let candidateGapLaps = normalizedClassLeaderGapLaps(car) else {
                    return nil
                }

                let lapDelta = referenceGapLaps - candidateGapLaps
                guard lapDelta >= Layout.sameLapReferenceBoundaryLaps else {
                    return nil
                }

                return (classPosition, lapDelta)
            }
            .min { $0.lapDelta < $1.lapDelta }

        guard let closest else {
            return nil
        }

        return "P\(closest.position) \(formatLapDelta(closest.lapDelta))"
    }

    private func isSameLapReferenceCandidate(
        _ candidate: LiveClassGapCar,
        comparedTo reference: LiveClassGapCar
    ) -> Bool {
        guard let candidateGapLaps = normalizedClassLeaderGapLaps(candidate),
              let referenceGapLaps = normalizedClassLeaderGapLaps(reference) else {
            return false
        }

        return abs(candidateGapLaps - referenceGapLaps) < Layout.sameLapReferenceBoundaryLaps
    }

    private func normalizedClassLeaderGapLaps(_ car: LiveClassGapCar) -> Double? {
        if let laps = car.gapLapsToClassLeader, laps.isFinite {
            return laps
        }

        guard let seconds = chartGapSeconds(car),
              let lapReferenceSeconds,
              isValidLapReference(lapReferenceSeconds) else {
            return nil
        }

        return seconds / lapReferenceSeconds
    }

    private func formatLapDelta(_ lapDelta: Double) -> String {
        let rounded = lapDelta.rounded()
        let value: String
        if abs(lapDelta - rounded) < 0.08 {
            value = String(format: "%.0f", rounded)
        } else {
            value = String(format: "%.1f", lapDelta)
        }

        let plural = abs((Double(value) ?? lapDelta) - 1) < 0.01 ? "lap" : "laps"
        return "+\(value) \(plural)"
    }

    private func tacticalFocusText() -> String {
        guard let reference = gap.classCars.first(where: { $0.isReferenceCar }) else {
            return "focus"
        }

        return "\(position(reference.classPosition)) focus"
    }

    private func formatLapReference() -> String {
        guard let lapReferenceSeconds, lapReferenceSeconds.isFinite else {
            return "--"
        }

        return formatPlainSeconds(lapReferenceSeconds)
    }

    private func filteredStatusText() -> String {
        if isPaceTimingMode {
            return ""
        }

        let gapDisplay = isPaceTimingMode && gap.classLeaderGap.isLeader
            ? "best"
            : gapText(gap.classLeaderGap)
        return "\(isPaceTimingMode ? "pace" : "") \(position(gap.referenceClassPosition)) \(gapDisplay)".trimmingCharacters(in: .whitespaces)
    }

    private func focusedTrendDescriptor() -> String {
        if isPaceTimingMode {
            return "practice timing"
        }

        if let visibleTrendWindowSeconds,
           visibleTrendWindowSeconds.isFinite,
           visibleTrendWindowSeconds > 0 {
            return "rolling focused"
        }

        return "focused trend"
    }

    private func contextX(_ sessionTime: Double, rect: NSRect) -> CGFloat {
        let ratio = min(max(sessionTime / FourHourRacePreview.sessionLengthSeconds, 0), 1)
        return rect.minX + CGFloat(ratio) * rect.width
    }

    private func weatherCondition(wetness: Int) -> WeatherCondition {
        switch wetness {
        case 4...:
            return .wet
        case 2...:
            return .damp
        case 0...:
            return .dry
        default:
            return .unknown
        }
    }

    private func selectDesiredCarIds(_ cars: [LiveClassGapCar]) -> Set<Int> {
        if displayMode == .filteredLeaderGap {
            return selectRangeFilteredCarIds(cars)
        }

        return selectCountFilteredCarIds(cars)
    }

    private func selectCountFilteredCarIds(_ cars: [LiveClassGapCar]) -> Set<Int> {
        var selected = Set<Int>()
        let reference = cars.first(where: { $0.isReferenceCar })
        for car in cars where car.isClassLeader || car.isReferenceCar {
            selected.insert(car.carIdx)
        }

        for car in cars
            .filter({ candidate in
                guard let reference else {
                    return false
                }

                return !candidate.isReferenceCar
                    && !candidate.isClassLeader
                    && isSameLapReferenceCandidate(candidate, comparedTo: reference)
                    && (candidate.deltaSecondsToReference ?? 0) < 0
            })
            .sorted(by: { ($0.deltaSecondsToReference ?? -Double.greatestFiniteMagnitude) > ($1.deltaSecondsToReference ?? -Double.greatestFiniteMagnitude) })
            .prefix(max(0, min(carsAhead, 12))) {
            selected.insert(car.carIdx)
        }

        for car in cars
            .filter({ candidate in
                guard let reference else {
                    return false
                }

                return !candidate.isReferenceCar
                    && !candidate.isClassLeader
                    && isSameLapReferenceCandidate(candidate, comparedTo: reference)
                    && (candidate.deltaSecondsToReference ?? 0) > 0
            })
            .sorted(by: { ($0.deltaSecondsToReference ?? .greatestFiniteMagnitude) < ($1.deltaSecondsToReference ?? .greatestFiniteMagnitude) })
            .prefix(max(0, min(carsBehind, 12))) {
            selected.insert(car.carIdx)
        }

        return selected
    }

    private func selectRangeFilteredCarIds(_ cars: [LiveClassGapCar]) -> Set<Int> {
        var selected = Set<Int>()
        let reference = cars.first(where: { $0.isReferenceCar })
        for car in cars where car.isClassLeader || car.isReferenceCar {
            selected.insert(car.carIdx)
        }

        let rangeSeconds = filteredGapRangeSeconds()
        for car in cars
            .filter({ candidate in
                guard let reference else {
                    return false
                }

                return !candidate.isReferenceCar
                    && !candidate.isClassLeader
                    && isSameLapReferenceCandidate(candidate, comparedTo: reference)
                    && (candidate.deltaSecondsToReference ?? 0) < 0
                    && abs(candidate.deltaSecondsToReference ?? 0) <= rangeSeconds
            })
            .sorted(by: { abs($0.deltaSecondsToReference ?? -Double.greatestFiniteMagnitude) < abs($1.deltaSecondsToReference ?? -Double.greatestFiniteMagnitude) })
            .prefix(max(0, min(carsAhead, 12))) {
            selected.insert(car.carIdx)
        }

        for car in cars
            .filter({ candidate in
                guard let reference else {
                    return false
                }

                return !candidate.isReferenceCar
                    && !candidate.isClassLeader
                    && isSameLapReferenceCandidate(candidate, comparedTo: reference)
                    && (candidate.deltaSecondsToReference ?? 0) > 0
                    && (candidate.deltaSecondsToReference ?? 0) <= rangeSeconds
            })
            .sorted(by: { ($0.deltaSecondsToReference ?? .greatestFiniteMagnitude) < ($1.deltaSecondsToReference ?? .greatestFiniteMagnitude) })
            .prefix(max(0, min(carsBehind, 12))) {
            selected.insert(car.carIdx)
        }

        return selected
    }

    private func filteredGapRangeSeconds() -> Double {
        let lapScaledRange = lapReferenceSeconds.map { isValidLapReference($0) ? $0 * Layout.filteredRangeLaps : 0 } ?? 0
        return min(
            Layout.filteredRangeMaximumSeconds,
            max(Layout.filteredRangeMinimumSeconds, lapScaledRange)
        )
    }

    private func shouldKeepVisible(_ state: CarRenderState, axisSeconds: Double) -> Bool {
        guard let lastDesired = state.lastDesiredAxisSeconds else {
            return false
        }

        return axisSeconds - lastDesired <= stickyVisibilitySeconds()
    }

    private func stickyVisibilitySeconds() -> Double {
        if displayMode == .filteredLeaderGap {
            return filteredGapRangeSeconds()
        }

        return max(
            Layout.stickyVisibilityMinimumSeconds,
            (lapReferenceSeconds ?? 0) * Layout.stickyVisibilityLaps
        )
    }

    private func selectChartSeries() -> [ChartSeriesSelection] {
        let now = latestAxisSeconds ?? selectAxisSeconds(timestamp: latestPointAt ?? Date(), sessionTime: nil)
        return carRenderStates.values
            .filter { shouldKeepVisible($0, axisSeconds: now) }
            .map { selection($0, now: now) }
            .sorted { $0.state.lastGapSeconds < $1.state.lastGapSeconds }
    }

    private func selection(_ state: CarRenderState, now: Double) -> ChartSeriesSelection {
        let lastDesired = state.lastDesiredAxisSeconds ?? now
        let visibleSince = state.visibleSinceAxisSeconds ?? lastDesired
        let isStickyExit = !state.isCurrentlyDesired
        let isStale = now - state.lastSeenAxisSeconds > Layout.missingTelemetryGraceSeconds
        let stickySeconds = stickyVisibilitySeconds()
        let exitAlpha = isStickyExit
            ? 1 - min(max((now - lastDesired) / max(1, stickySeconds), 0), 1)
            : 1
        let entryAlpha = min(max((now - visibleSince) / Layout.entryFadeSeconds, 0), 1)
        let alpha = min(max(min(exitAlpha, 0.35 + entryAlpha * 0.65), 0.18), 1)
        let drawStartSeconds = now - visibleSince <= Layout.entryFadeSeconds
            ? max(0, visibleSince - Layout.entryTailSeconds)
            : -Double.greatestFiniteMagnitude
        return ChartSeriesSelection(
            state: state,
            alpha: alpha,
            isStickyExit: isStickyExit,
            isStale: isStale,
            drawStartSeconds: drawStartSeconds
        )
    }

    private func selectTimeDomain(_ selectedSeries: [ChartSeriesSelection]) -> (start: Double, end: Double) {
        let latest = latestAxisSeconds ?? selectAxisSeconds(timestamp: latestPointAt ?? Date(), sessionTime: nil)
        let anchor = trendStartAxisSeconds ?? firstVisibleAxisSeconds(selectedSeries) ?? latest
        let elapsed = max(0, latest - anchor)
        if let visibleTrendWindowSeconds,
           visibleTrendWindowSeconds.isFinite,
           visibleTrendWindowSeconds > 0 {
            let duration = max(minimumTrendDomain(), visibleTrendWindowSeconds)
            if elapsed >= duration {
                return (latest - duration, latest)
            }

            return (anchor, anchor + duration)
        }

        if elapsed >= Layout.trendWindow {
            return (latest - Layout.trendWindow, latest)
        }

        let duration = min(
            Layout.trendWindow,
            max(minimumTrendDomain(), elapsed + trendRightPadding())
        )
        return (anchor, anchor + duration)
    }

    private func minimumTrendDomain() -> Double {
        max(
            Layout.minimumTrendDomainSeconds,
            lapReferenceSeconds.map { isValidLapReference($0) ? $0 * Layout.minimumTrendDomainLaps : 0 } ?? 0
        )
    }

    private func trendRightPadding() -> Double {
        max(
            Layout.trendRightPaddingSeconds,
            lapReferenceSeconds.map { isValidLapReference($0) ? $0 * Layout.trendRightPaddingLaps : 0 } ?? 0
        )
    }

    private func firstVisibleAxisSeconds(_ selectedSeries: [ChartSeriesSelection]) -> Double? {
        let firstVisible = selectedSeries
            .compactMap { selection in series[selection.state.carIdx]?.filter { $0.axisSeconds >= selection.drawStartSeconds } }
            .flatMap { $0 }
            .map(\.axisSeconds)
            .min()
        return firstVisible
    }

    private func maxGapSeconds(_ selectedSeries: [ChartSeriesSelection], start: Double, end: Double) -> Double {
        let historyMax = selectedSeries
            .compactMap { selection in series[selection.state.carIdx]?.filter { $0.axisSeconds >= selection.drawStartSeconds } }
            .flatMap { $0 }
            .filter { $0.axisSeconds >= start && $0.axisSeconds <= end }
            .map(\.gapSeconds)
            .max() ?? 1
        return niceCeiling(max(1, historyMax))
    }

    private func selectGapScale(_ selectedSeries: [ChartSeriesSelection], start: Double, end: Double) -> GapScale {
        let leaderScaleMax = maxGapSeconds(selectedSeries, start: start, end: end)
        guard let referenceSelection = selectedSeries.first(where: { $0.state.isReferenceCar }),
              let rawReferencePoints = series[referenceSelection.state.carIdx] else {
            return .leader(maxGapSeconds: leaderScaleMax)
        }

        let referencePoints = rawReferencePoints
            .filter { $0.axisSeconds >= start && $0.axisSeconds <= end }
            .sorted { $0.axisSeconds < $1.axisSeconds }
        guard !referencePoints.isEmpty else {
            return .leader(maxGapSeconds: leaderScaleMax)
        }

        let latestReferenceGap = referenceGap(at: end, referencePoints: referencePoints)
        let triggerGap = focusScaleMinimumReferenceGap()
        guard latestReferenceGap >= triggerGap else {
            return .leader(maxGapSeconds: leaderScaleMax)
        }

        var maxAheadSeconds = 0.0
        var maxBehindSeconds = 0.0
        var hasLocalComparison = false
        for selection in selectedSeries where !selection.state.isClassLeader {
            guard let points = series[selection.state.carIdx] else {
                continue
            }

            for point in points where point.axisSeconds >= selection.drawStartSeconds
                && point.axisSeconds >= start
                && point.axisSeconds <= end {
                let delta = point.gapSeconds - referenceGap(at: point.axisSeconds, referencePoints: referencePoints)
                hasLocalComparison = hasLocalComparison
                    || (!selection.state.isReferenceCar && abs(delta) > 0.001)
                if delta < 0 {
                    maxAheadSeconds = max(maxAheadSeconds, abs(delta))
                } else {
                    maxBehindSeconds = max(maxBehindSeconds, delta)
                }
            }
        }

        let minimumRange = focusScaleMinimumRange()
        let aheadRange = niceCeiling(max(minimumRange, maxAheadSeconds * Layout.focusScalePaddingMultiplier))
        let behindRange = niceCeiling(max(minimumRange, maxBehindSeconds * Layout.focusScalePaddingMultiplier))
        let localRange = max(aheadRange, behindRange)
        guard hasLocalComparison,
              leaderScaleMax >= max(triggerGap, localRange * Layout.focusScaleTriggerRatio) else {
            return .leader(maxGapSeconds: leaderScaleMax)
        }

        return .focusRelative(
            maxGapSeconds: leaderScaleMax,
            aheadSeconds: aheadRange,
            behindSeconds: behindRange,
            referencePoints: referencePoints,
            latestReferenceGapSeconds: latestReferenceGap
        )
    }

    private func focusScaleMinimumReferenceGap() -> Double {
        max(
            Layout.focusScaleMinimumReferenceGapSeconds,
            lapReferenceSeconds.map { isValidLapReference($0) ? $0 * Layout.focusScaleMinimumReferenceGapLaps : 0 } ?? 0
        )
    }

    private func focusScaleMinimumRange() -> Double {
        max(
            Layout.focusScaleMinimumRangeSeconds,
            lapReferenceSeconds.map { isValidLapReference($0) ? $0 * Layout.focusScaleMinimumRangeLaps : 0 } ?? 0
        )
    }

    private func graphPoint(
        _ point: GapTrendPoint,
        domain: (start: Double, end: Double),
        gapScale: GapScale,
        plot: NSRect
    ) -> NSPoint {
        graphPoint(axisSeconds: point.axisSeconds, gapSeconds: point.gapSeconds, domain: domain, gapScale: gapScale, plot: plot)
    }

    private func graphPoint(
        _ marker: DriverChangeMarker,
        domain: (start: Double, end: Double),
        gapScale: GapScale,
        plot: NSRect
    ) -> NSPoint {
        graphPoint(axisSeconds: marker.axisSeconds, gapSeconds: marker.gapSeconds, domain: domain, gapScale: gapScale, plot: plot)
    }

    private func graphPoint(
        axisSeconds: Double,
        gapSeconds: Double,
        domain: (start: Double, end: Double),
        gapScale: GapScale,
        plot: NSRect
    ) -> NSPoint {
        return NSPoint(
            x: axisX(axisSeconds, domain: domain, plot: plot),
            y: gapY(axisSeconds: axisSeconds, gapSeconds: gapSeconds, gapScale: gapScale, plot: plot)
        )
    }

    private func axisX(_ axisSeconds: Double, domain: (start: Double, end: Double), plot: NSRect) -> CGFloat {
        let total = max(1, domain.end - domain.start)
        let ratio = min(max((axisSeconds - domain.start) / total, 0), 1)
        return plot.minX + CGFloat(ratio) * plot.width
    }

    private func pruneOldPoints(latestAxisSeconds: Double) {
        let cutoff = latestAxisSeconds - Layout.trendWindow
        for carIdx in Array(series.keys) {
            series[carIdx]?.removeAll { $0.axisSeconds < cutoff }
            if series[carIdx]?.isEmpty ?? true {
                series.removeValue(forKey: carIdx)
            }
        }

        weather.removeAll { $0.axisSeconds < cutoff }
        driverChangeMarkers.removeAll { $0.axisSeconds < cutoff }
        leaderChangeMarkers.removeAll { $0.axisSeconds < cutoff }
        for (carIdx, state) in carRenderStates where series[carIdx] == nil {
            if let lastDesired = state.lastDesiredAxisSeconds,
               latestAxisSeconds - lastDesired > stickyVisibilitySeconds() {
                carRenderStates.removeValue(forKey: carIdx)
            }
        }
    }

    private func applyStatusColor() {
        if !gap.hasData {
            layer?.backgroundColor = NSColor(red255: 14, green: 18, blue: 21, alpha: 0.88).cgColor
            statusLabel.textColor = NSColor(calibratedWhite: 0.65, alpha: 1)
            return
        }

        layer?.backgroundColor = NSColor(red255: 18, green: 30, blue: 42, alpha: 0.88).cgColor
        statusLabel.textColor = NSColor(red255: 140, green: 190, blue: 245)
    }

    private func drawPriority(_ car: CarRenderState, threatCarIdx: Int?) -> Int {
        if car.isReferenceCar {
            return 3
        }

        if car.isClassLeader {
            return 2
        }

        if car.carIdx == threatCarIdx {
            return 1
        }

        return 0
    }

    private func tacticalDrawPriority(_ car: CarRenderState) -> Int {
        car.isReferenceCar ? 2 : 0
    }

    private func seriesColor(_ car: CarRenderState, threatCarIdx: Int?) -> NSColor {
        if car.carIdx == threatCarIdx {
            return dangerColor(alpha: 0.96)
        }

        if car.isReferenceCar {
            return OverlayClassColor.color(car.classColorHex) ?? NSColor(red255: 112, green: 224, blue: 146)
        }

        if car.isClassLeader {
            return NSColor(calibratedWhite: 1, alpha: 0.92)
        }

        return (car.deltaSecondsToReference ?? 0) < 0
            ? NSColor(red255: 140, green: 190, blue: 245)
            : NSColor(red255: 246, green: 184, blue: 88)
    }

    private func seriesAlphaMultiplier(
        _ car: CarRenderState,
        threatCarIdx: Int? = nil,
        denseLeaderCycle: Bool = false
    ) -> Double {
        if car.isClassLeader || car.isReferenceCar || car.carIdx == threatCarIdx {
            return 1
        }

        return denseLeaderCycle ? 0.24 : 0.48
    }

    private func chartGapSeconds(_ car: LiveClassGapCar) -> Double? {
        car.gapSecondsToClassLeader ?? car.gapLapsToClassLeader.map { $0 * (lapReferenceSeconds ?? 60) }
    }

    private func metricChangeColor(_ value: Double, positiveIsGood: Bool) -> NSColor {
        guard abs(value) >= metricDeadbandSeconds() else {
            return NSColor(red255: 190, green: 202, blue: 210, alpha: 0.88)
        }

        return (value > 0) == positiveIsGood
            ? NSColor(red255: 112, green: 224, blue: 146, alpha: 0.94)
            : dangerColor(alpha: 0.96)
    }

    private func metricDeadbandSeconds() -> Double {
        max(
            Layout.metricDeadbandMinimumSeconds,
            (lapReferenceSeconds ?? 0) * Layout.metricDeadbandLapFraction
        )
    }

    private func threatGainThresholdSeconds() -> Double {
        max(
            Layout.threatMinimumGainSeconds,
            (lapReferenceSeconds ?? 0) * Layout.threatGainLapFraction
        )
    }

    private func dangerColor(alpha: CGFloat = 1) -> NSColor {
        NSColor(red255: 236, green: 112, blue: 99, alpha: alpha)
    }

    private func weatherCondition(_ snapshot: LiveTelemetrySnapshot) -> WeatherCondition {
        guard let frame = snapshot.latestFrame else {
            return .unknown
        }

        if frame.weatherDeclaredWet {
            return .declaredWet
        }

        switch frame.trackWetness {
        case 4...:
            return .wet
        case 2...:
            return .damp
        case 0...:
            return .dry
        default:
            return .unknown
        }
    }

    private func weatherBandColor(_ condition: WeatherCondition) -> NSColor? {
        switch condition {
        case .damp:
            return NSColor(red255: 75, green: 170, blue: 205, alpha: 0.04)
        case .wet:
            return NSColor(red255: 70, green: 135, blue: 230, alpha: 0.07)
        case .declaredWet:
            return NSColor(red255: 78, green: 142, blue: 238, alpha: 0.1)
        default:
            return nil
        }
    }

    private func selectLapReferenceSeconds(_ snapshot: LiveTelemetrySnapshot) -> Double? {
        if let lapTime = snapshot.fuel.lapTimeSeconds, isValidLapReference(lapTime) {
            return lapTime
        }

        if let lapTime = snapshot.latestFrame?.estimatedLapSeconds, isValidLapReference(lapTime) {
            return lapTime
        }

        return nil
    }

    private func selectAxisSeconds(timestamp: Date, sessionTime: Double?) -> Double {
        if let sessionTime, sessionTime.isFinite, sessionTime >= 0 {
            return sessionTime
        }

        return timestamp.timeIntervalSince1970
    }

    private func isValidLapReference(_ seconds: Double) -> Bool {
        seconds.isFinite && seconds > 20 && seconds < 1800
    }

    private func gapY(axisSeconds: Double, gapSeconds: Double, gapScale: GapScale, plot: NSRect) -> CGFloat {
        guard gapScale.isFocusRelative else {
            return gapY(gapSeconds, maxGap: gapScale.maxGapSeconds, plot: plot)
        }

        let referenceGap = referenceGap(at: axisSeconds, referencePoints: gapScale.referencePoints)
        return gapDeltaY(gapSeconds - referenceGap, gapScale: gapScale, plot: plot)
    }

    private func gapDeltaY(_ deltaSeconds: Double, gapScale: GapScale, plot: NSRect) -> CGFloat {
        let referenceY = focusReferenceY(plot: plot)
        if deltaSeconds < 0 {
            let ratio = min(max(abs(deltaSeconds) / max(1, gapScale.aheadSeconds), 0), 1)
            return referenceY + CGFloat(ratio) * max(1, plot.maxY - Layout.focusScaleTopPadding - referenceY)
        }

        let behindRatio = min(max(deltaSeconds / max(1, gapScale.behindSeconds), 0), 1)
        return referenceY - CGFloat(behindRatio) * max(1, referenceY - (plot.minY + Layout.focusScaleBottomPadding))
    }

    private func focusReferenceY(plot: NSRect) -> CGFloat {
        plot.maxY - Layout.focusScaleReferenceRatio * plot.height
    }

    private func gapY(_ gapSeconds: Double, maxGap: Double, plot: NSRect) -> CGFloat {
        let ratio = min(max(gapSeconds / maxGap, 0), 1)
        return plot.maxY - CGFloat(ratio) * plot.height
    }

    private func niceCeiling(_ value: Double) -> Double {
        if value <= 1 {
            return 1
        }

        let magnitude = pow(10, floor(log10(value)))
        let normalized = value / magnitude
        for step in [1.0, 1.5, 2.0, 3.0, 5.0, 7.5, 10.0] where normalized <= step {
            return step * magnitude
        }

        return 10 * magnitude
    }

    private func niceGridStep(_ value: Double) -> Double {
        if value <= 0.25 {
            return 0.25
        }

        let magnitude = pow(10, floor(log10(value)))
        let normalized = value / magnitude
        for step in [1.0, 2.0, 2.5, 5.0, 10.0] where normalized <= step {
            return step * magnitude
        }

        return 10 * magnitude
    }

    private func formatAxisSeconds(_ seconds: Double) -> String {
        if seconds < 10 {
            return String(format: "+%.1fs", seconds)
        }

        return formatGapSeconds(seconds)
    }

    private func formatTrendWindow(_ seconds: TimeInterval) -> String {
        if seconds >= 3600 {
            let hours = seconds / 3600
            return abs(hours.rounded() - hours) < 0.05
                ? String(format: "%.0fh", hours)
                : String(format: "%.1fh", hours)
        }

        return String(format: "%.0fm", seconds / 60)
    }

    private func position(_ value: Int?) -> String {
        guard let value, value > 0 else {
            return "--"
        }

        return "\(value)"
    }

    private func gapText(_ gap: LiveGapValue) -> String {
        if !gap.hasData {
            return "--"
        }

        if gap.isLeader {
            return "leader"
        }

        if let seconds = gap.seconds {
            return formatGapSeconds(seconds)
        }

        if let laps = gap.laps {
            return String(format: "+%.2f lap", laps)
        }

        return "--"
    }

    private func formatGapSeconds(_ seconds: Double) -> String {
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

    private func formatChangeSeconds(_ seconds: Double) -> String {
        guard seconds.isFinite else {
            return "--"
        }

        if abs(seconds) < 0.05 {
            return "0.0"
        }

        return String(format: "%@%.1f", seconds > 0 ? "+" : "", seconds)
    }

    private func formatDeltaSeconds(_ seconds: Double) -> String {
        guard seconds.isFinite else {
            return "--"
        }

        if abs(seconds) < 0.05 {
            return "0.0"
        }

        let absolute = abs(seconds)
        if absolute >= 60 {
            return String(format: "%@%.0f:%04.1f", seconds > 0 ? "+" : "-", floor(absolute / 60), absolute.truncatingRemainder(dividingBy: 60))
        }

        return String(format: "%@%.1fs", seconds > 0 ? "+" : "-", absolute)
    }

    private func trimError(_ message: String) -> String {
        message.count <= 72 ? message : "\(message.prefix(69))..."
    }

    private struct GapTrendPoint {
        var timestamp: Date
        var axisSeconds: Double
        var gapSeconds: Double
        var isReferenceCar: Bool
        var isClassLeader: Bool
        var classPosition: Int?
        var startsSegment: Bool
    }

    private struct GapScale {
        var maxGapSeconds: Double
        var isFocusRelative: Bool
        var aheadSeconds: Double
        var behindSeconds: Double
        var referencePoints: [GapTrendPoint]
        var latestReferenceGapSeconds: Double

        static func leader(maxGapSeconds: Double) -> GapScale {
            GapScale(
                maxGapSeconds: maxGapSeconds,
                isFocusRelative: false,
                aheadSeconds: 0,
                behindSeconds: 0,
                referencePoints: [],
                latestReferenceGapSeconds: 0
            )
        }

        static func focusRelative(
            maxGapSeconds: Double,
            aheadSeconds: Double,
            behindSeconds: Double,
            referencePoints: [GapTrendPoint],
            latestReferenceGapSeconds: Double
        ) -> GapScale {
            GapScale(
                maxGapSeconds: maxGapSeconds,
                isFocusRelative: true,
                aheadSeconds: aheadSeconds,
                behindSeconds: behindSeconds,
                referencePoints: referencePoints,
                latestReferenceGapSeconds: latestReferenceGapSeconds
            )
        }
    }

    private struct WeatherTrendPoint {
        var axisSeconds: Double
        var condition: WeatherCondition
    }

    private struct DriverChangeMarker {
        var timestamp: Date
        var axisSeconds: Double
        var carIdx: Int
        var gapSeconds: Double
        var isReferenceCar: Bool
        var label: String
    }

    private struct LeaderChangeMarker {
        var timestamp: Date
        var axisSeconds: Double
        var previousLeaderCarIdx: Int
        var newLeaderCarIdx: Int
    }

    private struct ChartSeriesSelection {
        var state: CarRenderState
        var alpha: Double
        var isStickyExit: Bool
        var isStale: Bool
        var drawStartSeconds: Double
    }

    private struct FocusedTrendMetric {
        var label: String
        var focusGapChange: Double?
        var chaser: BehindGainMetric?
        var state: TrendMetricState
    }

    private struct BehindGainMetric {
        var carIdx: Int
        var label: String
        var gainSeconds: Double
    }

    private enum TrendMetricState {
        case ready
        case warming(String)
        case leaderChanged
        case unavailable
    }

    private struct TacticalSeriesSelection {
        var state: CarRenderState
        var points: [TacticalTrendPoint]
        var alpha: Double
        var isStickyExit: Bool
        var isStale: Bool
    }

    private struct TacticalTrendPoint {
        var axisSeconds: Double
        var deltaSecondsToReference: Double
        var classPosition: Int?
        var startsSegment: Bool
    }

    private struct EndpointLabel {
        var text: String
        var point: NSPoint
        var color: NSColor
        var isReferenceCar: Bool
        var isClassLeader: Bool
    }

    private struct PositionedEndpointLabel {
        var label: EndpointLabel
        var y: CGFloat
    }

    private final class CarRenderState {
        let carIdx: Int
        var lastSeenAxisSeconds: Double = 0
        var lastGapSeconds: Double = 0
        var lastDesiredAxisSeconds: Double?
        var visibleSinceAxisSeconds: Double?
        var isCurrentlyDesired = false
        var isReferenceCar = false
        var isClassLeader = false
        var classPosition: Int?
        var deltaSecondsToReference: Double?
        var classColorHex: String?

        init(carIdx: Int) {
            self.carIdx = carIdx
        }
    }

    private enum WeatherCondition {
        case unknown
        case dry
        case damp
        case wet
        case declaredWet
    }
}
