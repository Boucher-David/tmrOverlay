import AppKit

final class StandingsOverlayView: NSView {
    private enum Layout {
        static let padding: CGFloat = 14
        static let columnGap: CGFloat = 8
        static let maximumRows = 8
    }

    private struct Row {
        let classPosition: String
        let car: String
        let driver: String
        let gap: String
        let interval: String
        let pit: String
        let isReference: Bool
        let isLeader: Bool
    }

    private let titleLabel = NSTextField(labelWithString: "Standings")
    private let statusLabel = NSTextField(labelWithString: "waiting")
    private var headerLabels: [NSTextField] = []
    private var rowLabels: [[NSTextField]] = []
    private var tableRect = NSRect.zero
    private var overlayError: String?
    var contentSettings = OverlaySettings(
        id: StandingsOverlayDefinition.definition.id,
        width: StandingsOverlayDefinition.definition.defaultSize.width,
        height: StandingsOverlayDefinition.definition.defaultSize.height
    ) {
        didSet {
            needsLayout = true
            needsDisplay = true
        }
    }

    var fontFamily = OverlayTheme.defaultFontFamily {
        didSet { applyFonts() }
    }

    override init(frame frameRect: NSRect = NSRect(origin: .zero, size: StandingsOverlayDefinition.definition.defaultSize)) {
        super.init(frame: frameRect)
        setup()
    }

    required init?(coder: NSCoder) {
        nil
    }

    override func layout() {
        super.layout()
        titleLabel.frame = NSRect(x: Layout.padding, y: bounds.height - 34, width: 185, height: 22)
        statusLabel.frame = NSRect(x: 204, y: bounds.height - 34, width: max(110, bounds.width - 218), height: 22)
        tableRect = NSRect(
            x: Layout.padding,
            y: Layout.padding,
            width: max(1, bounds.width - Layout.padding * 2),
            height: max(220, bounds.height - 54)
        )

        let columns = displayColumns
        let contentWidth = CGFloat(columns.reduce(0) { $0 + $1.width })
            + CGFloat(max(0, columns.count - 1)) * Layout.columnGap
        tableRect.size.width = max(contentWidth, tableRect.width)
        let rowHeight = tableRect.height / CGFloat(Layout.maximumRows + 1)
        var x = tableRect.minX
        for column in 0..<maximumColumns {
            let label = headerLabels[column]
            guard column < columns.count else {
                label.isHidden = true
                for row in 0..<Layout.maximumRows {
                    rowLabels[row][column].isHidden = true
                }
                continue
            }

            let columnState = columns[column]
            let width = CGFloat(columnState.width)
            label.isHidden = false
            label.stringValue = columnState.definition.label
            label.alignment = textAlignment(columnState.definition.alignment)
            label.frame = NSRect(x: x + 6, y: tableRect.maxY - rowHeight + 3, width: width - 12, height: rowHeight - 6)
            for row in 0..<Layout.maximumRows {
                let y = tableRect.maxY - CGFloat(row + 2) * rowHeight
                rowLabels[row][column].isHidden = false
                rowLabels[row][column].alignment = textAlignment(columnState.definition.alignment)
                rowLabels[row][column].frame = NSRect(x: x + 6, y: y + 3, width: width - 12, height: rowHeight - 6)
            }
            x += width + Layout.columnGap
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        OverlayTheme.Colors.windowBorder.setStroke()
        bounds.insetBy(dx: 0.5, dy: 0.5).frame()
        OverlayTheme.Colors.panelBackground.setFill()
        tableRect.fill()
        OverlayTheme.Colors.windowBorder.setStroke()
        tableRect.frame()

        let rowHeight = tableRect.height / CGFloat(Layout.maximumRows + 1)
        for row in 1...(Layout.maximumRows) {
            let y = tableRect.maxY - CGFloat(row) * rowHeight
            NSBezierPath.strokeLine(from: NSPoint(x: tableRect.minX, y: y), to: NSPoint(x: tableRect.maxX, y: y))
        }

        var x = tableRect.minX
        for column in displayColumns.dropLast() {
            x += CGFloat(column.width)
            NSBezierPath.strokeLine(from: NSPoint(x: x + Layout.columnGap / 2, y: tableRect.minY), to: NSPoint(x: x + Layout.columnGap / 2, y: tableRect.maxY))
            x += Layout.columnGap
        }
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        overlayError = nil
        let rows = buildRows(snapshot)
        if rows.isEmpty {
            statusLabel.stringValue = "waiting for timing"
            clearRows()
        } else {
            let reference = rows.first(where: { $0.isReference })
            statusLabel.stringValue = reference.map { "class \($0.classPosition) - \(rows.count) rows" } ?? "\(rows.count) rows"
            applyRows(rows)
        }

        layer?.backgroundColor = OverlayTheme.Colors.windowBackground.cgColor
        needsLayout = true
        needsDisplay = true
    }

    func showOverlayError(_ message: String) {
        overlayError = message
        statusLabel.stringValue = "overlay error"
        clearRows()
        rowLabels[0][0].stringValue = "ERR"
        rowLabels[0][2].stringValue = message
        rowLabels[0][2].textColor = OverlayTheme.Colors.errorIndicator
        layer?.backgroundColor = OverlayTheme.Colors.errorBackground.cgColor
        needsLayout = true
        needsDisplay = true
    }

    private func setup() {
        wantsLayer = true
        layer?.backgroundColor = OverlayTheme.Colors.windowBackground.cgColor

        titleLabel.textColor = OverlayTheme.Colors.textPrimary
        titleLabel.backgroundColor = .clear
        statusLabel.alignment = .right
        statusLabel.textColor = OverlayTheme.Colors.textSubtle
        statusLabel.backgroundColor = .clear
        addSubview(titleLabel)
        addSubview(statusLabel)

        for _ in 0..<maximumColumns {
            let label = NSTextField(labelWithString: "")
            label.textColor = OverlayTheme.Colors.textMuted
            label.backgroundColor = .clear
            headerLabels.append(label)
            addSubview(label)
        }

        for _ in 0..<Layout.maximumRows {
            var row: [NSTextField] = []
            for _ in 0..<maximumColumns {
                let label = NSTextField(labelWithString: "")
                label.backgroundColor = .clear
                label.textColor = OverlayTheme.Colors.textPrimary
                row.append(label)
                addSubview(label)
            }
            rowLabels.append(row)
        }

        applyFonts()
    }

    private func buildRows(_ snapshot: LiveTelemetrySnapshot) -> [Row] {
        guard snapshot.isConnected, snapshot.isCollecting, !snapshot.leaderGap.classCars.isEmpty else {
            return []
        }

        let referenceGap = snapshot.leaderGap.classCars.first(where: { $0.isReferenceCar })?.gapSecondsToClassLeader
            ?? snapshot.leaderGap.classLeaderGap.seconds
            ?? 0
        return snapshot.leaderGap.classCars
            .sorted {
                ($0.classPosition ?? Int.max, $0.carIdx) < ($1.classPosition ?? Int.max, $1.carIdx)
            }
            .prefix(Layout.maximumRows)
            .map { car in
                Row(
                    classPosition: car.classPosition.map { "\($0)" } ?? "--",
                    car: car.carNumber.map { $0.hasPrefix("#") ? $0 : "#\($0)" }
                        ?? (car.carIdx == FourHourRacePreview.teamCarIdx ? "#44" : "#\(car.carIdx)"),
                    driver: driverName(car: car, snapshot: snapshot),
                    gap: formatGap(car),
                    interval: formatInterval(car.deltaSecondsToReference, referenceGap: referenceGap, isReference: car.isReferenceCar),
                    pit: pitStatus(car: car, snapshot: snapshot),
                    isReference: car.isReferenceCar,
                    isLeader: car.isClassLeader
                )
            }
    }

    private func applyRows(_ rows: [Row]) {
        clearRows()
        for (rowIndex, row) in rows.enumerated() where rowIndex < Layout.maximumRows {
            let columns = displayColumns
            for columnIndex in 0..<maximumColumns {
                guard columnIndex < columns.count else {
                    rowLabels[rowIndex][columnIndex].stringValue = ""
                    continue
                }

                let column = columns[columnIndex]
                rowLabels[rowIndex][columnIndex].stringValue = value(for: row, column: column)
                rowLabels[rowIndex][columnIndex].textColor = textColor(row: row, column: column)
            }
        }
    }

    private func clearRows() {
        for row in rowLabels {
            for label in row {
                label.stringValue = ""
                label.textColor = OverlayTheme.Colors.textPrimary
            }
        }
    }

    private func value(for row: Row, column: OverlayContentColumnState) -> String {
        switch column.definition.dataKey {
        case OverlayContentColumns.dataClassPosition:
            return row.classPosition
        case OverlayContentColumns.dataCarNumber:
            return row.car
        case OverlayContentColumns.dataDriver:
            return row.driver
        case OverlayContentColumns.dataGap:
            return row.gap
        case OverlayContentColumns.dataInterval:
            return row.interval
        case OverlayContentColumns.dataPit:
            return row.pit
        default:
            return ""
        }
    }

    private func textColor(row: Row, column: OverlayContentColumnState) -> NSColor {
        let dataKey = column.definition.dataKey
        if dataKey == OverlayContentColumns.dataPit && !row.pit.isEmpty {
            return OverlayTheme.Colors.warningIndicator
        }

        if row.isReference {
            return NSColor(red255: 255, green: 218, blue: 89)
        }

        if row.isLeader && dataKey == OverlayContentColumns.dataGap {
            return OverlayTheme.Colors.successText
        }

        return dataKey == OverlayContentColumns.dataDriver ? OverlayTheme.Colors.textSecondary : OverlayTheme.Colors.textPrimary
    }

    private func driverName(car: LiveClassGapCar, snapshot: LiveTelemetrySnapshot) -> String {
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

    private func formatGap(_ car: LiveClassGapCar) -> String {
        StandingsDisplayFormatting.gap(
            isClassLeader: car.isClassLeader,
            seconds: car.gapSecondsToClassLeader,
            laps: car.gapLapsToClassLeader,
            lapCompleted: car.lapCompleted,
            lapDistPct: car.lapDistPct)
    }

    private func formatInterval(_ delta: Double?, referenceGap: Double, isReference: Bool) -> String {
        StandingsDisplayFormatting.interval(delta, referenceGap: referenceGap, isReference: isReference)
    }

    private func pitStatus(car: LiveClassGapCar, snapshot: LiveTelemetrySnapshot) -> String {
        guard let sessionTime = snapshot.latestFrame?.sessionTime else {
            return ""
        }

        if car.onPitRoad == true {
            return "IN"
        }

        if car.isReferenceCar && snapshot.latestFrame?.onPitRoad == true {
            return "IN"
        }

        if car.classPosition == 2 {
            return "IN"
        }

        let window = pitWindow(car: car, sessionTime: sessionTime)
        if sessionTime >= window.entry && sessionTime <= window.exit {
            return "IN"
        }

        if sessionTime > window.exit && sessionTime <= window.exit + 20 {
            return "OUT"
        }

        return ""
    }

    private func pitWindow(car: LiveClassGapCar, sessionTime: TimeInterval) -> (entry: TimeInterval, exit: TimeInterval) {
        let position = car.classPosition ?? abs(car.carIdx % 18) + 1
        let fourHourPitDuration = FourHourRacePreview.firstPitExitSeconds - FourHourRacePreview.firstPitEntrySeconds
        let duration = fourHourPitDuration + TimeInterval(abs(car.carIdx % 5) * 4)

        if sessionTime < FourHourRacePreview.firstPitEntrySeconds - 600 {
            let cycleLength: TimeInterval = 240
            let cycleStart = sessionTime - sessionTime.truncatingRemainder(dividingBy: cycleLength)
            let reviewEntry = cycleStart + 44 + TimeInterval((position % 5) * 20)
            return (reviewEntry, reviewEntry + duration)
        }

        if car.isReferenceCar || car.carIdx == FourHourRacePreview.teamCarIdx {
            return (FourHourRacePreview.firstPitEntrySeconds, FourHourRacePreview.firstPitExitSeconds)
        }

        let waveOffset = TimeInterval((position % 9) * 34) - 140
        let entry = FourHourRacePreview.firstPitEntrySeconds + waveOffset
        return (entry, entry + duration)
    }

    private var displayColumns: [OverlayContentColumnState] {
        OverlayContentColumns.visibleColumnStates(
            for: OverlayContentColumns.standings,
            settings: contentSettings
        )
    }

    private var maximumColumns: Int {
        OverlayContentColumns.standings.columns.count
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

    private func applyFonts() {
        titleLabel.font = OverlayTheme.font(family: fontFamily, size: 15, weight: .semibold)
        statusLabel.font = OverlayTheme.font(family: fontFamily, size: 12)
        for label in headerLabels {
            label.font = OverlayTheme.font(family: fontFamily, size: 11, weight: .semibold)
        }
        for row in rowLabels {
            for label in row {
                label.font = NSFont.monospacedSystemFont(ofSize: 12, weight: .regular)
            }
        }
    }
}
