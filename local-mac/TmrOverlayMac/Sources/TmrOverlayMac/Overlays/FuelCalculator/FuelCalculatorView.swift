import AppKit

final class FuelCalculatorView: NSView {
    private enum Layout {
        static let padding: CGFloat = 14
        static let titleHeight: CGFloat = 24
        static let footerHeight: CGFloat = 22
        static let maxStintRows = 6
        static let historyLookupCacheSeconds: TimeInterval = 30
    }

    private let titleLabel: NSTextField
    private let historyQueryService: SessionHistoryQueryService
    private let statusLabel = NSTextField(labelWithString: "waiting")
    private let overviewTitleLabel = NSTextField(labelWithString: "Overview")
    private let overviewValueLabel = NSTextField(labelWithString: "waiting for live fuel")
    private let tiresHeaderLabel = NSTextField(labelWithString: "Advice")
    private let sourceLabel = NSTextField(labelWithString: "source: waiting")
    private var stintNumberLabels: [NSTextField] = []
    private var stintLengthLabels: [NSTextField] = []
    private var stintTireLabels: [NSTextField] = []
    private var tableRect = NSRect.zero
    private var visibleStintRows = Layout.maxStintRows
    private var cachedHistoryCombo: HistoricalComboIdentity?
    private var cachedHistory: SessionHistoryLookupResult?
    private var cachedHistoryAt: Date?
    var showAdvice = true {
        didSet {
            tiresHeaderLabel.isHidden = !showAdvice
            stintTireLabels.forEach { $0.isHidden = !showAdvice }
            needsLayout = true
            needsDisplay = true
        }
    }
    var showSource = true {
        didSet {
            sourceLabel.isHidden = !showSource
            needsLayout = true
        }
    }
    var unitSystem = "Metric" {
        didSet { needsDisplay = true }
    }
    var fontFamily = "SF Pro" {
        didSet { applyFonts() }
    }

    init(
        frame frameRect: NSRect,
        title: String = "Fuel Calculator",
        historyQueryService: SessionHistoryQueryService
    ) {
        titleLabel = NSTextField(labelWithString: title)
        self.historyQueryService = historyQueryService
        super.init(frame: frameRect)
        setup()
    }

    convenience init(
        title: String = "Fuel Calculator",
        historyQueryService: SessionHistoryQueryService
    ) {
        self.init(
            frame: NSRect(origin: .zero, size: FuelCalculatorOverlayDefinition.definition.defaultSize),
            title: title,
            historyQueryService: historyQueryService
        )
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func layout() {
        super.layout()

        let width = bounds.width
        let height = bounds.height
        titleLabel.frame = NSRect(x: Layout.padding, y: height - 34, width: 260, height: 22)
        statusLabel.frame = NSRect(x: 274, y: height - 34, width: width - 288, height: 22)
        tableRect = NSRect(
            x: Layout.padding,
            y: Layout.padding + Layout.footerHeight,
            width: width - Layout.padding * 2,
            height: height - Layout.padding * 2 - Layout.titleHeight - Layout.footerHeight
        )
        sourceLabel.frame = NSRect(x: Layout.padding, y: 10, width: width - Layout.padding * 2, height: 18)

        let rowHeight = tableRect.height / CGFloat(rowCount)
        let firstWidth = tableRect.width * (showAdvice ? 0.24 : 0.28)
        let secondWidth = tableRect.width * (showAdvice ? 0.48 : 0.72)
        overviewTitleLabel.frame = cellFrame(row: 0, column: 0, rowHeight: rowHeight, firstWidth: firstWidth, secondWidth: secondWidth)
        overviewValueLabel.frame = cellFrame(row: 0, column: 1, rowHeight: rowHeight, firstWidth: firstWidth, secondWidth: secondWidth)
        tiresHeaderLabel.frame = cellFrame(row: 0, column: 2, rowHeight: rowHeight, firstWidth: firstWidth, secondWidth: secondWidth)

        for index in 0..<stintNumberLabels.count {
            let row = index + 1
            stintNumberLabels[index].frame = cellFrame(row: row, column: 0, rowHeight: rowHeight, firstWidth: firstWidth, secondWidth: secondWidth)
            stintLengthLabels[index].frame = cellFrame(row: row, column: 1, rowHeight: rowHeight, firstWidth: firstWidth, secondWidth: secondWidth)
            stintTireLabels[index].frame = cellFrame(row: row, column: 2, rowHeight: rowHeight, firstWidth: firstWidth, secondWidth: secondWidth)
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)

        NSColor(calibratedWhite: 1, alpha: 0.26).setStroke()
        tableRect.frame()

        let rowHeight = tableRect.height / CGFloat(rowCount)
        let firstWidth = tableRect.width * (showAdvice ? 0.24 : 0.28)
        let secondWidth = tableRect.width * (showAdvice ? 0.48 : 0.72)
        let firstSeparatorX = tableRect.minX + firstWidth
        let secondSeparatorX = firstSeparatorX + secondWidth
        NSBezierPath.strokeLine(
            from: NSPoint(x: firstSeparatorX, y: tableRect.minY),
            to: NSPoint(x: firstSeparatorX, y: tableRect.maxY)
        )
        if showAdvice {
            NSBezierPath.strokeLine(
                from: NSPoint(x: secondSeparatorX, y: tableRect.minY),
                to: NSPoint(x: secondSeparatorX, y: tableRect.maxY)
            )
        }

        for row in 1..<rowCount {
            let y = tableRect.minY + CGFloat(row) * rowHeight
            NSBezierPath.strokeLine(from: NSPoint(x: tableRect.minX, y: y), to: NSPoint(x: tableRect.maxX, y: y))
        }
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        let history = lookupHistory(snapshot.combo)
        let strategy = FuelStrategyCalculator.make(from: snapshot, history: history)
        statusLabel.stringValue = strategy.status
        overviewValueLabel.stringValue = overview(strategy)
        sourceLabel.stringValue = sourceText(strategy, history: history)
        let rows = displayRows(strategy)
        visibleStintRows = Layout.maxStintRows

        for index in 0..<stintNumberLabels.count {
            if index < rows.count {
                let row = rows[index]
                stintNumberLabels[index].stringValue = row.label
                stintLengthLabels[index].stringValue = row.value
                stintTireLabels[index].stringValue = row.advice
                setRowVisible(true, index: index)
            } else {
                stintNumberLabels[index].stringValue = "Stint \(index + 1)"
                stintLengthLabels[index].stringValue = ""
                stintTireLabels[index].stringValue = ""
                setRowVisible(true, index: index)
            }
        }

        applyStatusColor(strategy)
        needsLayout = true
        needsDisplay = true
    }

    private func setup() {
        wantsLayer = true
        layer?.borderWidth = 1
        layer?.borderColor = NSColor(calibratedWhite: 1, alpha: 0.28).cgColor
        layer?.backgroundColor = NSColor(red255: 14, green: 18, blue: 21, alpha: 0.88).cgColor

        configure(titleLabel, font: NSFont.systemFont(ofSize: 15, weight: .semibold), color: .white)
        configure(statusLabel, font: NSFont.monospacedSystemFont(ofSize: 12, weight: .regular), color: NSColor(red255: 145, green: 224, blue: 170), alignment: .right)
        configure(overviewTitleLabel, font: NSFont.monospacedSystemFont(ofSize: 12, weight: .bold), color: .white)
        configure(overviewValueLabel, font: NSFont.monospacedSystemFont(ofSize: 12, weight: .bold), color: .white, alignment: .right)
        configure(tiresHeaderLabel, font: NSFont.monospacedSystemFont(ofSize: 12, weight: .bold), color: .white, alignment: .right)
        configure(sourceLabel, font: NSFont.monospacedSystemFont(ofSize: 11, weight: .regular), color: NSColor(red255: 128, green: 145, blue: 153))

        for index in 0..<Layout.maxStintRows {
            let stintLabel = NSTextField(labelWithString: "Stint \(index + 1)")
            let lengthLabel = NSTextField(labelWithString: "--")
            let tireLabel = NSTextField(labelWithString: "--")
            configure(stintLabel, font: NSFont.monospacedSystemFont(ofSize: 12, weight: .regular), color: NSColor(red255: 218, green: 226, blue: 230))
            configure(lengthLabel, font: NSFont.monospacedSystemFont(ofSize: 12, weight: .regular), color: NSColor(red255: 218, green: 226, blue: 230), alignment: .right)
            configure(tireLabel, font: NSFont.monospacedSystemFont(ofSize: 12, weight: .regular), color: NSColor(red255: 218, green: 226, blue: 230), alignment: .right)
            stintNumberLabels.append(stintLabel)
            stintLengthLabels.append(lengthLabel)
            stintTireLabels.append(tireLabel)
        }

        [titleLabel, statusLabel, overviewTitleLabel, overviewValueLabel, tiresHeaderLabel, sourceLabel].forEach(addSubview)
        stintNumberLabels.forEach(addSubview)
        stintLengthLabels.forEach(addSubview)
        stintTireLabels.forEach(addSubview)
        applyFonts()
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
        overviewTitleLabel.font = overlayFont(ofSize: 12, weight: .bold)
        overviewValueLabel.font = overlayFont(ofSize: 12, weight: .bold)
        tiresHeaderLabel.font = overlayFont(ofSize: 12, weight: .bold)
        sourceLabel.font = overlayFont(ofSize: 11, weight: .regular)
        stintNumberLabels.forEach { $0.font = overlayFont(ofSize: 12, weight: .regular) }
        stintLengthLabels.forEach { $0.font = overlayFont(ofSize: 12, weight: .regular) }
        stintTireLabels.forEach { $0.font = overlayFont(ofSize: 12, weight: .regular) }
        needsLayout = true
        needsDisplay = true
    }

    private func overlayFont(ofSize size: CGFloat, weight: NSFont.Weight) -> NSFont {
        NSFont(name: fontFamily, size: size) ?? NSFont.systemFont(ofSize: size, weight: weight)
    }

    private func overview(_ strategy: FuelStrategySnapshot) -> String {
        if let plannedLaps = strategy.plannedRaceLaps,
           let stintCount = strategy.plannedStintCount,
           let finalStintLaps = strategy.finalStintTargetLaps {
            if stintCount <= 1 {
                return "\(plannedLaps) laps | no stop"
            }

            return "\(plannedLaps) laps | \(stintCount) stints | final \(finalStintLaps)"
        }

        let fuel = fuelVolume(strategy.currentFuelLiters)
        let laps = FuelStrategyCalculator.format(strategy.raceLapsRemaining, suffix: " laps")
        let needed = (strategy.additionalFuelNeededLiters ?? 0) > 0.1
            ? "+\(fuelVolume(strategy.additionalFuelNeededLiters))"
            : "covered"
        return "\(fuel) | \(laps) | \(needed)"
    }

    private func stintText(_ stint: FuelStintEstimate) -> String {
        if stint.source == "finish" {
            return "no fuel stop needed"
        }

        if let targetLaps = stint.targetLaps {
            let suffix = stint.source == "final" ? " final" : ""
            return "\(targetLaps) laps\(suffix) | target \(fuelPerLap(stint.targetFuelPerLapLiters))"
        }

        return String(format: "%.1f laps", stint.lengthLaps)
    }

    private func displayRows(_ strategy: FuelStrategySnapshot) -> [FuelDisplayRow] {
        var rows: [FuelDisplayRow] = []
        if let comparison = strategy.rhythmComparison, comparison.additionalStopCount > 0 {
            rows.append(FuelDisplayRow(
                label: "Strategy",
                value: rhythmText(comparison),
                advice: rhythmAdvice(comparison)
            ))
        }

        for stint in strategy.stints.prefix(Layout.maxStintRows - rows.count) {
            rows.append(FuelDisplayRow(
                label: "Stint \(stint.number)",
                value: stintText(stint),
                advice: showAdvice ? (stint.tireAdvice?.text ?? "--").replacingOccurrences(of: " L", with: " \(fuelVolumeSuffix)") : ""
            ))
        }

        return rows
    }

    private func rhythmText(_ comparison: FuelRhythmComparison) -> String {
        "\(comparison.longTargetLaps)-lap rhythm avoids +\(comparison.additionalStopCount) \(comparison.additionalStopCount == 1 ? "stop" : "stops")"
    }

    private func rhythmAdvice(_ comparison: FuelRhythmComparison) -> String {
        let time = comparison.estimatedTimeLossSeconds.map { String(format: "~%.0fs", $0) } ?? "--"
        if comparison.requiredSavingLitersPerLap > 0.01 {
            return "\(time) | save \(fuelPerLap(comparison.requiredSavingLitersPerLap))"
        }

        return time
    }

    private func sourceText(_ strategy: FuelStrategySnapshot, history: SessionHistoryLookupResult) -> String {
        let fuelPerLap = fuelPerLap(strategy.fuelPerLapLiters)
        let fullTank = FuelStrategyCalculator.format(strategy.fullTankStintLaps, suffix: " laps/tank")
        let historySource = history.userAggregate != nil
            ? "user"
            : history.baselineAggregate != nil
                ? "baseline"
                : "none"
        let range: String
        if strategy.fuelPerLapMinimumLiters != nil || strategy.fuelPerLapMaximumLiters != nil {
            range = " | min/avg/max \(fuelNumber(strategy.fuelPerLapMinimumLiters))/\(fuelNumber(strategy.fuelPerLapLiters))/\(fuelNumber(strategy.fuelPerLapMaximumLiters)) \(fuelPerLapSuffix)"
        } else {
            range = ""
        }
        let tireModel = strategy.tireChangeServiceSeconds != nil || strategy.fuelFillRateLitersPerSecond != nil
            ? " | tires \(strategy.tireModelSource)"
            : ""
        return "burn \(fuelPerLap) (\(strategy.fuelPerLapSource)) | \(fullTank) | history \(historySource)\(range)\(tireModel)"
    }

    private func plain(_ value: Double?) -> String {
        guard let value, value.isFinite else {
            return "--"
        }

        return String(format: "%.1f", value)
    }

    private func fuelVolume(_ liters: Double?) -> String {
        "\(fuelNumber(liters)) \(fuelVolumeSuffix)"
    }

    private func fuelPerLap(_ liters: Double?) -> String {
        "\(fuelNumber(liters)) \(fuelPerLapSuffix)"
    }

    private func fuelNumber(_ liters: Double?) -> String {
        guard let liters, liters.isFinite else {
            return "--"
        }

        let value = unitSystem == "Imperial" ? liters * 0.264172052 : liters
        return String(format: "%.1f", value)
    }

    private var fuelVolumeSuffix: String {
        unitSystem == "Imperial" ? "gal" : "L"
    }

    private var fuelPerLapSuffix: String {
        unitSystem == "Imperial" ? "gal/lap" : "L/lap"
    }

    private func applyStatusColor(_ strategy: FuelStrategySnapshot) {
        if !strategy.hasData || strategy.fuelPerLapLiters == nil {
            layer?.backgroundColor = NSColor(red255: 14, green: 18, blue: 21, alpha: 0.88).cgColor
            statusLabel.textColor = NSColor(calibratedWhite: 0.65, alpha: 1)
        } else if ((strategy.rhythmComparison?.isRealistic ?? false) && (strategy.rhythmComparison?.additionalStopCount ?? 0) > 0)
            || ((strategy.requiredFuelSavingPercent ?? 0) > 0 && (strategy.requiredFuelSavingPercent ?? 1) <= 0.05)
            || ((strategy.stopOptimization?.isRealistic ?? false) && (strategy.stopOptimization?.requiredSavingLitersPerLap ?? 0) > 0) {
            layer?.backgroundColor = NSColor(red255: 54, green: 30, blue: 14, alpha: 0.88).cgColor
            statusLabel.textColor = NSColor(red255: 246, green: 184, blue: 88)
        } else {
            layer?.backgroundColor = NSColor(red255: 14, green: 38, blue: 28, alpha: 0.88).cgColor
            statusLabel.textColor = NSColor(red255: 112, green: 224, blue: 146)
        }
    }

    private var rowCount: Int {
        max(1, visibleStintRows + 1)
    }

    private func lookupHistory(_ combo: HistoricalComboIdentity) -> SessionHistoryLookupResult {
        let now = Date()
        if let cachedHistory,
           let cachedHistoryCombo,
           sameCombo(cachedHistoryCombo, combo),
           let cachedHistoryAt,
           now.timeIntervalSince(cachedHistoryAt) <= Layout.historyLookupCacheSeconds {
            return cachedHistory
        }

        let history = historyQueryService.lookup(combo)
        cachedHistory = history
        cachedHistoryCombo = combo
        cachedHistoryAt = now
        return history
    }

    private func sameCombo(_ lhs: HistoricalComboIdentity, _ rhs: HistoricalComboIdentity) -> Bool {
        lhs.carKey == rhs.carKey
            && lhs.trackKey == rhs.trackKey
            && lhs.sessionKey == rhs.sessionKey
    }

    private func setRowVisible(_ visible: Bool, index: Int) {
        stintNumberLabels[index].isHidden = !visible
        stintLengthLabels[index].isHidden = !visible
        stintTireLabels[index].isHidden = !visible || !showAdvice
    }

    private func cellFrame(row: Int, column: Int, rowHeight: CGFloat, firstWidth: CGFloat, secondWidth: CGFloat) -> NSRect {
        let y = tableRect.maxY - CGFloat(row + 1) * rowHeight + 6
        if column == 0 {
            return NSRect(x: tableRect.minX + 8, y: y, width: firstWidth - 16, height: rowHeight - 10)
        }

        if column == 1 {
            return NSRect(x: tableRect.minX + firstWidth + 8, y: y, width: secondWidth - 16, height: rowHeight - 10)
        }

        return NSRect(
            x: tableRect.minX + firstWidth + secondWidth + 8,
            y: y,
            width: tableRect.width - firstWidth - secondWidth - 16,
            height: rowHeight - 10
        )
    }
}

private struct FuelDisplayRow {
    let label: String
    let value: String
    let advice: String
}
