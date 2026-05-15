import AppKit

final class RelativeOverlayView: NSView {
    private enum Layout {
        static let padding: CGFloat = 14
        static let titleHeight: CGFloat = 24
        static let footerHeight: CGFloat = 22
        static let maximumRows = 17
        static let staleSeconds: TimeInterval = 1.5
        static let compactRowHeight: CGFloat = 26
    }

    private let titleLabel = NSTextField(labelWithString: "Relative")
    private let statusLabel = NSTextField(labelWithString: "waiting")
    private let sourceLabel = NSTextField(labelWithString: "source: waiting")
    private var rowLabels: [[NSTextField]] = []
    private var tableRect = NSRect.zero
    private var renderedRows: [RelativeDisplayRow?] = [nil]
    private var renderedRowCount = 1
    private var overlayError: String?
    private var showHeaderStatus = true
    private var showFooterSource = true
    private var lastSessionKey: String?
    var contentSettings = OverlaySettings(
        id: RelativeOverlayDefinition.definition.id,
        width: RelativeOverlayDefinition.definition.defaultSize.width,
        height: RelativeOverlayDefinition.definition.defaultSize.height
    ) {
        didSet {
            applyChromeSettings(sessionKey: lastSessionKey)
            needsLayout = true
            needsDisplay = true
        }
    }

    var carsAhead = 5 {
        didSet {
            needsLayout = true
            needsDisplay = true
        }
    }
    var carsBehind = 5 {
        didSet {
            needsLayout = true
            needsDisplay = true
        }
    }
    var fontFamily = "SF Pro" {
        didSet { applyFonts() }
    }

    override init(frame frameRect: NSRect = NSRect(origin: .zero, size: RelativeOverlayDefinition.definition.defaultSize)) {
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
        titleLabel.frame = NSRect(x: Layout.padding, y: height - 34, width: 150, height: 22)
        statusLabel.frame = NSRect(x: 164, y: height - 34, width: width - 178, height: 22)
        let footerHeight = showFooterSource ? Layout.footerHeight : 0
        let availableTableHeight = height - Layout.padding * 2 - Layout.titleHeight - footerHeight
        let compactTableHeight = min(availableTableHeight, max(Layout.compactRowHeight, CGFloat(renderedRowCount) * Layout.compactRowHeight))
        tableRect = NSRect(
            x: Layout.padding,
            y: height - 42 - compactTableHeight,
            width: width - Layout.padding * 2,
            height: compactTableHeight
        )
        sourceLabel.frame = NSRect(x: Layout.padding, y: 10, width: width - Layout.padding * 2, height: 18)

        let visibleRows = CGFloat(renderedRowCount)
        let rowHeight = tableRect.height / visibleRows
        let columns = displayColumns
        let contentWidth = CGFloat(columns.reduce(0) { $0 + $1.width })
        tableRect.size.width = max(contentWidth, tableRect.width)

        for index in 0..<Layout.maximumRows {
            let visible = CGFloat(index) < visibleRows
            let rowY = tableRect.maxY - CGFloat(index + 1) * rowHeight
            var x = tableRect.minX
            for columnIndex in 0..<maximumColumns {
                let label = rowLabels[index][columnIndex]
                guard columnIndex < columns.count else {
                    label.isHidden = true
                    continue
                }

                let column = columns[columnIndex]
                label.isHidden = !visible
                label.alignment = textAlignment(column.definition.alignment)
                label.frame = NSRect(
                    x: x + 7,
                    y: rowY + 3,
                    width: CGFloat(column.width) - 14,
                    height: rowHeight - 6
                )
                x += CGFloat(column.width)
            }
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        OverlayTheme.Colors.windowBorder.setStroke()
        bounds.insetBy(dx: 0.5, dy: 0.5).frame()
        OverlayTheme.Colors.panelBackground.setFill()
        tableRect.fill()
        fillRowBackgrounds()
        OverlayTheme.Colors.windowBorder.setStroke()
        tableRect.frame()

        let visibleRows = renderedRowCount
        let rowHeight = tableRect.height / CGFloat(visibleRows)
        for row in 1..<visibleRows {
            let y = tableRect.maxY - CGFloat(row) * rowHeight
            NSBezierPath.strokeLine(from: NSPoint(x: tableRect.minX, y: y), to: NSPoint(x: tableRect.maxX, y: y))
        }

        var x = tableRect.minX
        for column in displayColumns.dropLast() {
            x += CGFloat(column.width)
            NSBezierPath.strokeLine(from: NSPoint(x: x, y: tableRect.minY), to: NSPoint(x: x, y: tableRect.maxY))
        }
    }

    func update(with snapshot: LiveTelemetrySnapshot, now: Date = Date()) {
        lastSessionKey = snapshot.combo.sessionKey
        applyChromeSettings(sessionKey: snapshot.combo.sessionKey)
        let rows = buildRows(snapshot: snapshot, now: now)
        if rows.isEmpty {
            clearRows(placeholder: statusLabel.stringValue)
        } else {
            applyRows(rows)
        }

        overlayError = nil
        applyStatusColor(hasRows: !rows.isEmpty)
        needsLayout = true
        needsDisplay = true
    }

    func showOverlayError(_ message: String) {
        lastSessionKey = nil
        applyChromeSettings(sessionKey: nil)
        overlayError = message
        statusLabel.stringValue = "relative error"
        sourceLabel.stringValue = trimError(message)
        clearRows(placeholder: "relative error")
        applyStatusColor(hasRows: false)
        needsDisplay = true
    }

    private func buildRows(snapshot: LiveTelemetrySnapshot, now: Date) -> [RelativeDisplayRow] {
        guard snapshot.isConnected, snapshot.isCollecting else {
            statusLabel.stringValue = "waiting for iRacing"
            sourceLabel.stringValue = "source: waiting"
            return []
        }

        guard let updatedAt = snapshot.lastUpdatedAtUtc,
              abs(now.timeIntervalSince(updatedAt)) <= Layout.staleSeconds else {
            statusLabel.stringValue = "waiting for fresh telemetry"
            sourceLabel.stringValue = "source: waiting"
            return []
        }

        let proximity = snapshot.proximity
        let referenceRow = RelativeDisplayRow(
            position: referencePosition(snapshot.latestFrame),
            driver: referenceDriver(snapshot.latestFrame),
            gap: "0.000",
            detail: referenceClass(snapshot.latestFrame),
            classColorHex: snapshot.latestFrame?.capturedReferenceCar?.carClassColorHex ?? "#FFDA59",
            isReference: true,
            isAhead: false,
            isBehind: false,
            isSameClass: true,
            isPit: snapshot.latestFrame?.onPitRoad == true,
            isPartial: false,
            lapDeltaToReference: 0
        )

        let ahead = proximity.nearbyCars
            .filter { $0.relativeLaps > 0 }
            .sorted { sortKey($0) < sortKey($1) }
            .prefix(clampedCarsAhead)
            .sorted { sortKey($0) > sortKey($1) }
            .map { displayRow(car: $0, direction: .ahead, referenceClass: proximity.referenceCarClass) }
        let behind = proximity.nearbyCars
            .filter { $0.relativeLaps < 0 }
            .sorted { sortKey($0) < sortKey($1) }
            .prefix(clampedCarsBehind)
            .map { displayRow(car: $0, direction: .behind, referenceClass: proximity.referenceCarClass) }
        let rows = Array(ahead) + [referenceRow] + Array(behind)
        let shownCars = rows.filter { !$0.isReference }.count
        statusLabel.stringValue = "\(referenceRow.position) - \(shownCars) cars"
        sourceLabel.stringValue = proximity.hasData
            ? "source: live proximity telemetry"
            : "source: waiting"
        return proximity.hasData ? rows : [referenceRow]
    }

    private func displayRow(
        car: LiveProximityCar,
        direction: RelativeDirection,
        referenceClass: Int?
    ) -> RelativeDisplayRow {
        let sameClass = referenceClass != nil && car.carClass == referenceClass
        return RelativeDisplayRow(
            position: car.classPosition.map { "\($0)" } ?? car.overallPosition.map { "\($0)" } ?? "--",
            driver: car.driverName ?? MockDriverNames.displayName(for: car.carIdx),
            gap: relativeGap(car: car, direction: direction),
            detail: classLabel(carClass: car.carClass, carClassName: car.carClassName, onPitRoad: car.onPitRoad),
            classColorHex: car.carClassColorHex,
            isReference: false,
            isAhead: direction == .ahead,
            isBehind: direction == .behind,
            isSameClass: sameClass,
            isPit: car.onPitRoad == true,
            isPartial: car.relativeSeconds == nil && car.relativeMeters == nil,
            lapDeltaToReference: car.lapDeltaToReference
        )
    }

    private func applyRows(_ rows: [RelativeDisplayRow]) {
        let rows = stableRows(from: rows)
        renderedRows = rows
        renderedRowCount = max(1, min(Layout.maximumRows, rows.count))
        for index in 0..<Layout.maximumRows {
            if index < rows.count, let row = rows[index] {
                applyRow(row, index: index)
            } else {
                applyBlank(index: index, placeholder: "")
            }
        }
    }

    private func clearRows(placeholder: String) {
        renderedRows = [nil]
        renderedRowCount = 1
        for index in 0..<Layout.maximumRows {
            applyBlank(index: index, placeholder: index == 0 ? placeholder : "")
        }
    }

    private func stableRows(from rows: [RelativeDisplayRow]) -> [RelativeDisplayRow?] {
        guard !rows.isEmpty else {
            return [nil]
        }

        let aheadCapacity = clampedCarsAhead
        let behindCapacity = clampedCarsBehind
        let reference = rows.first { $0.isReference }
        let hasReference = reference != nil
        let visibleRows = max(1, min(Layout.maximumRows, aheadCapacity + behindCapacity + (hasReference ? 1 : 0)))
        var stableRows = Array<RelativeDisplayRow?>(repeating: nil, count: visibleRows)

        let ahead = rows.filter(\.isAhead)
        let aheadStart = max(0, aheadCapacity - ahead.count)
        for (offset, row) in ahead.enumerated() where aheadStart + offset < stableRows.count {
            stableRows[aheadStart + offset] = row
        }

        let behindStart = hasReference ? aheadCapacity + 1 : aheadCapacity
        if let reference, aheadCapacity < stableRows.count {
            stableRows[aheadCapacity] = reference
        }

        let behind = rows.filter(\.isBehind)
        for (offset, row) in behind.enumerated() where behindStart + offset < stableRows.count {
            stableRows[behindStart + offset] = row
        }

        return !hasReference && ahead.isEmpty && behind.isEmpty ? [nil] : stableRows
    }

    private func fillRowBackgrounds() {
        let visibleRows = max(1, renderedRowCount)
        let rowHeight = tableRect.height / CGFloat(visibleRows)
        for index in 0..<min(visibleRows, Layout.maximumRows) {
            let rowY = tableRect.maxY - CGFloat(index + 1) * rowHeight
            let rect = NSRect(x: tableRect.minX, y: rowY, width: tableRect.width, height: rowHeight)
            let row = index < renderedRows.count ? renderedRows[index] : nil
            rowFill(row).setFill()
            rect.fill()
        }
    }

    private func rowFill(_ row: RelativeDisplayRow?) -> NSColor {
        guard let row else {
            return OverlayClassColor.blend(
                panel: OverlayTheme.Colors.panelBackground,
                accent: OverlayTheme.Colors.windowBorder,
                panelWeight: 10,
                accentWeight: 1
            )
        }

        let base: NSColor
        if row.isReference {
            base = OverlayTheme.Colors.infoBackground
        } else if row.isPit {
            base = OverlayTheme.Colors.warningStrongBackground
        } else {
            base = OverlayTheme.Colors.panelBackground
        }

        guard let classColor = OverlayClassColor.color(row.classColorHex) else {
            return base
        }

        return OverlayClassColor.blend(
            panel: base,
            accent: classColor,
            panelWeight: row.isReference ? 10 : 8,
            accentWeight: 1
        )
    }

    private func applyRow(_ row: RelativeDisplayRow, index: Int) {
        let columns = displayColumns

        let lappedColor = lappedTextColor(row.lapDeltaToReference)
        let textColor = row.isReference
            ? OverlayTheme.Colors.textPrimary
            : lappedColor ?? (row.isPartial ? OverlayTheme.Colors.textMuted : OverlayTheme.Colors.textSecondary)
        let gapColor = row.isReference
            ? OverlayTheme.Colors.textPrimary
            : lappedColor ?? (row.isPartial ? OverlayTheme.Colors.textMuted : (row.isAhead ? OverlayTheme.Colors.textSubtle : OverlayTheme.Colors.successText))
        for columnIndex in 0..<maximumColumns {
            let label = rowLabels[index][columnIndex]
            guard columnIndex < columns.count else {
                label.stringValue = ""
                label.textColor = OverlayTheme.Colors.textMuted
                continue
            }

            let column = columns[columnIndex]
            label.stringValue = value(for: row, column: column)
            label.textColor = color(forColumn: column, textColor: textColor, gapColor: gapColor, row: row)
        }
    }

    private func applyBlank(index: Int, placeholder: String) {
        let columns = displayColumns
        for columnIndex in 0..<maximumColumns {
            let label = rowLabels[index][columnIndex]
            label.stringValue = columnIndex == 0 ? placeholder : ""
            label.textColor = OverlayTheme.Colors.textMuted
            label.isHidden = columnIndex >= columns.count
        }
    }

    private func value(for row: RelativeDisplayRow, column: OverlayContentColumnState) -> String {
        switch column.definition.dataKey {
        case OverlayContentColumns.dataRelativePosition:
            return row.position
        case OverlayContentColumns.dataDriver:
            return row.driver
        case OverlayContentColumns.dataGap:
            return row.gap
        case OverlayContentColumns.dataPit:
            return row.isPit ? "IN" : ""
        default:
            return ""
        }
    }

    private func color(
        forColumn column: OverlayContentColumnState,
        textColor: NSColor,
        gapColor: NSColor,
        row: RelativeDisplayRow
    ) -> NSColor {
        if column.definition.dataKey == OverlayContentColumns.dataGap {
            return gapColor
        }

        if column.definition.dataKey == OverlayContentColumns.dataPit, row.isPit {
            return OverlayTheme.Colors.warningIndicator
        }

        return textColor
    }

    private func lappedTextColor(_ lapDeltaToReference: Int?) -> NSColor? {
        switch lapDeltaToReference {
        case let value? where value >= 2:
            return OverlayTheme.Colors.errorIndicator
        case 1:
            return NSColor(red255: 255, green: 155, blue: 164)
        case -1:
            return NSColor(red255: 150, green: 210, blue: 255)
        case let value? where value <= -2:
            return NSColor(red255: 82, green: 158, blue: 255)
        default:
            return nil
        }
    }

    private func setup() {
        wantsLayer = true
        layer?.borderWidth = 1
        layer?.borderColor = OverlayTheme.Colors.windowBorder.cgColor
        layer?.backgroundColor = OverlayTheme.Colors.windowBackground.cgColor

        configure(titleLabel, font: overlayFont(ofSize: 15, weight: .semibold), color: OverlayTheme.Colors.textPrimary)
        configure(statusLabel, font: overlayFont(ofSize: 12, weight: .regular), color: OverlayTheme.Colors.textSubtle, alignment: .right)
        configure(sourceLabel, font: overlayFont(ofSize: 11, weight: .regular), color: OverlayTheme.Colors.textMuted)

        for _ in 0..<Layout.maximumRows {
            var labels: [NSTextField] = []
            for _ in 0..<maximumColumns {
                let label = NSTextField(labelWithString: "")
                configure(label, font: overlayFont(ofSize: 11, weight: .regular), color: OverlayTheme.Colors.textSecondary)
                labels.append(label)
            }
            rowLabels.append(labels)
        }

        [titleLabel, statusLabel, sourceLabel].forEach(addSubview)
        rowLabels.flatMap { $0 }.forEach(addSubview)
        statusLabel.isHidden = !showHeaderStatus
        sourceLabel.isHidden = !showFooterSource
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
        label.lineBreakMode = .byTruncatingTail
        label.maximumNumberOfLines = 1
    }

    private func applyFonts() {
        titleLabel.font = overlayFont(ofSize: 15, weight: .semibold)
        statusLabel.font = overlayFont(ofSize: 12, weight: .regular)
        sourceLabel.font = overlayFont(ofSize: 11, weight: .regular)
        rowLabels.flatMap { $0 }.forEach { $0.font = overlayFont(ofSize: 11, weight: .regular) }
        needsLayout = true
        needsDisplay = true
    }

    private func applyStatusColor(hasRows: Bool) {
        if overlayError != nil {
            layer?.backgroundColor = OverlayTheme.Colors.errorBackground.cgColor
            statusLabel.textColor = OverlayTheme.Colors.errorIndicator
            return
        }

        layer?.backgroundColor = hasRows
            ? OverlayTheme.Colors.windowBackground.cgColor
            : OverlayTheme.Colors.neutralBackground.cgColor
        statusLabel.textColor = hasRows
            ? OverlayTheme.Colors.textSecondary
            : OverlayTheme.Colors.textSubtle
    }

    private func overlayFont(ofSize size: CGFloat, weight: NSFont.Weight) -> NSFont {
        NSFont(name: fontFamily, size: size) ?? NSFont.systemFont(ofSize: size, weight: weight)
    }

    private var clampedCarsAhead: Int {
        min(max(carsAhead, 0), 8)
    }

    private var clampedCarsBehind: Int {
        min(max(carsBehind, 0), 8)
    }

    private var displayColumns: [OverlayContentColumnState] {
        OverlayContentColumns.visibleColumnStates(
            for: OverlayContentColumns.relative,
            settings: contentSettings
        )
    }

    private var maximumColumns: Int {
        OverlayContentColumns.relative.columns.count
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

    private func relativeGap(car: LiveProximityCar, direction: RelativeDirection) -> String {
        RelativeDisplayFormatting.gap(
            seconds: car.relativeSeconds,
            meters: car.relativeMeters,
            laps: car.relativeLaps,
            direction: direction == .ahead ? .ahead : .behind)
    }

    private func sortKey(_ car: LiveProximityCar) -> Double {
        if let seconds = car.relativeSeconds, seconds.isFinite {
            return abs(seconds)
        }

        if let meters = car.relativeMeters, meters.isFinite {
            return abs(meters)
        }

        return abs(car.relativeLaps)
    }

    private func referencePosition(_ frame: MockLiveTelemetryFrame?) -> String {
        if referencePositionShouldBeHidden(frame) {
            return "--"
        }

        if let classPosition = frame?.teamClassPosition {
            return "\(classPosition)"
        }

        if let position = frame?.teamPosition {
            return "\(position)"
        }

        return "--"
    }

    private func referencePositionShouldBeHidden(_ frame: MockLiveTelemetryFrame?) -> Bool {
        guard let frame else {
            return false
        }

        if !frame.isOnTrack || frame.isInGarage {
            return true
        }

        return frame.onPitRoad
            && frame.teamLapCompleted <= 0
            && frame.teamLapDistPct <= 0.001
    }

    private func referenceDriver(_ frame: MockLiveTelemetryFrame?) -> String {
        guard let frame else {
            return "My Car"
        }

        return frame.teamDriverName.isEmpty ? "My Car" : frame.teamDriverName
    }

    private func referenceClass(_ frame: MockLiveTelemetryFrame?) -> String {
        if frame?.onPitRoad == true {
            return "GT3 PIT"
        }

        return "GT3"
    }

    private func classLabel(carClass: Int?, carClassName: String?, onPitRoad: Bool?) -> String {
        let label: String
        if let carClassName, !carClassName.isEmpty {
            label = carClassName
        } else {
            switch carClass {
            case 4098:
                label = "GT3"
            case 4099:
                label = "LMP"
            case let value?:
                label = "Class \(value)"
            case nil:
                label = "class"
            }
        }

        return onPitRoad == true ? "\(label) PIT" : label
    }

    private func applyChromeSettings(sessionKey: String?) {
        let nextShowHeaderStatus = chromeOption(
            sessionKey: sessionKey,
            testKey: "chrome.header.status.test",
            practiceKey: "chrome.header.status.practice",
            qualifyingKey: "chrome.header.status.qualifying",
            raceKey: "chrome.header.status.race"
        )
        let nextShowFooterSource = chromeOption(
            sessionKey: sessionKey,
            testKey: "chrome.footer.source.test",
            practiceKey: "chrome.footer.source.practice",
            qualifyingKey: "chrome.footer.source.qualifying",
            raceKey: "chrome.footer.source.race"
        )

        guard nextShowHeaderStatus != showHeaderStatus || nextShowFooterSource != showFooterSource else {
            return
        }

        showHeaderStatus = nextShowHeaderStatus
        showFooterSource = nextShowFooterSource
        statusLabel.isHidden = !showHeaderStatus
        sourceLabel.isHidden = !showFooterSource
        needsLayout = true
        needsDisplay = true
    }

    private func chromeOption(
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
            return boolOption(practiceKey, defaultValue: true)
        case "practice":
            return boolOption(practiceKey, defaultValue: true)
        case "qualifying":
            return boolOption(qualifyingKey, defaultValue: true)
        case "race":
            return boolOption(raceKey, defaultValue: true)
        default:
            return true
        }
    }

    private func sessionKind(_ sessionKey: String?) -> String? {
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

    private func boolOption(_ key: String, defaultValue: Bool) -> Bool {
        guard let value = contentSettings.options[key]?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() else {
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

    private func trimError(_ message: String) -> String {
        message.count <= 80 ? message : String(message.prefix(77)) + "..."
    }
}

private struct RelativeDisplayRow {
    var position: String
    var driver: String
    var gap: String
    var detail: String
    var classColorHex: String?
    var isReference: Bool
    var isAhead: Bool
    var isBehind: Bool
    var isSameClass: Bool
    var isPit: Bool
    var isPartial: Bool
    var lapDeltaToReference: Int?
}

private enum RelativeDirection {
    case ahead
    case behind
}
