import AppKit

final class RelativeDesignV2OverlayView: NSView {
    private enum Layout {
        static let padding: CGFloat = 16
        static let headerHeight: CGFloat = 38
        static let footerHeight: CGFloat = 38
        static let hiddenFooterBottomPadding: CGFloat = 16
        static let rowHeight: CGFloat = 31
        static let rowGap: CGFloat = 6
        static let columnInset: CGFloat = 12
        static let columnGap: CGFloat = 10
        static let minimumColumnWidth: CGFloat = 32
        static let maximumRows = 17
        static let staleSeconds: TimeInterval = 1.5
    }

    var theme = DesignV2Theme.outrun {
        didSet { needsDisplay = true }
    }
    var contentSettings = OverlaySettings(
        id: RelativeOverlayDefinition.definition.id,
        width: RelativeOverlayDefinition.definition.defaultSize.width,
        height: RelativeOverlayDefinition.definition.defaultSize.height
    ) {
        didSet {
            applyChromeSettings(sessionKey: lastSessionKey)
            needsDisplay = true
        }
    }
    var carsAhead = 5 {
        didSet { needsDisplay = true }
    }
    var carsBehind = 5 {
        didSet { needsDisplay = true }
    }
    var fontFamily = "SF Pro" {
        didSet { needsDisplay = true }
    }

    private var latestRows: [RelativeDesignV2Row] = []
    private var latestPlaceholder = "waiting"
    private var statusText = "waiting"
    private var sourceText = "source: waiting"
    private var showHeaderStatus = true
    private var showFooterSource = true
    private var lastSessionKey: String?

    override init(frame frameRect: NSRect = NSRect(origin: .zero, size: RelativeOverlayDefinition.definition.defaultSize)) {
        super.init(frame: frameRect)
        wantsLayer = true
        layer?.backgroundColor = NSColor.clear.cgColor
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override var isFlipped: Bool {
        true
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        let scale = designScale
        let outer = bounds.insetBy(dx: 0.5, dy: 0.5)
        rounded(outer, radius: theme.layout.cornerRadius * scale, fill: theme.colors.surface, stroke: theme.colors.border, lineWidth: 1)

        let header = NSRect(x: outer.minX + 1, y: outer.minY + 1, width: outer.width - 2, height: scaled(Layout.headerHeight))
        rounded(header, radius: max(2, (theme.layout.cornerRadius - 1) * scale), fill: theme.colors.titleBar, stroke: nil, lineWidth: 0)
        rounded(
            NSRect(x: outer.minX, y: outer.minY + scaled(7), width: max(1, scaled(2)), height: outer.height - scaled(14)),
            radius: 2,
            fill: theme.colors.accentPrimary,
            stroke: nil,
            lineWidth: 0
        )
        rounded(
            NSRect(x: outer.minX, y: header.maxY - 1, width: outer.width, height: max(1, scaled(2))),
            radius: 1,
            fill: theme.colors.accentSecondary,
            stroke: nil,
            lineWidth: 0
        )

        drawText(
            "Relative V2",
            in: NSRect(x: outer.minX + scaled(14), y: header.midY - scaled(9), width: scaled(150), height: scaled(18)),
            font: overlayFont(ofSize: 14 * scale, weight: .bold),
            color: theme.colors.textPrimary
        )
        if showHeaderStatus {
            drawText(
                statusText,
                in: NSRect(x: outer.minX + scaled(174), y: header.midY - scaled(8), width: outer.width - scaled(190), height: scaled(16)),
                font: overlayFont(ofSize: 11 * scale, weight: .semibold),
                color: theme.colors.textSecondary,
                alignment: .right
            )
        }

        let rows = designRows()
        let rowArea = tableRect(rowCount: rows.count)
        if rows.allSatisfy({ $0 == nil }) {
            drawText(
                latestPlaceholder,
                in: rowArea.insetBy(dx: scaled(12), dy: scaled(8)),
                font: overlayFont(ofSize: 12 * scale, weight: .semibold),
                color: theme.colors.textMuted
            )
        } else {
            drawRows(rows, in: rowArea)
        }

        if showFooterSource {
            drawText(
                sourceText,
                in: NSRect(x: outer.minX + scaled(14), y: outer.maxY - scaled(26), width: outer.width - scaled(28), height: scaled(16)),
                font: overlayFont(ofSize: 10 * scale, weight: .regular),
                color: theme.colors.textMuted
            )
        }
    }

    func update(with snapshot: LiveTelemetrySnapshot, now: Date = Date()) {
        lastSessionKey = snapshot.combo.sessionKey
        applyChromeSettings(sessionKey: snapshot.combo.sessionKey)
        let rows = buildRows(snapshot: snapshot, now: now)
        if rows.isEmpty {
            latestRows = []
            latestPlaceholder = statusText
        } else {
            latestRows = rows
            latestPlaceholder = ""
        }

        needsDisplay = true
    }

    func showOverlayError(_ message: String) {
        lastSessionKey = nil
        applyChromeSettings(sessionKey: nil)
        statusText = "relative error"
        sourceText = trimError(message)
        latestRows = []
        latestPlaceholder = "relative error"
        needsDisplay = true
    }

    static func demoSize(settings: OverlaySettings, sessionKey: String? = nil) -> NSSize {
        let scale = clampedScale(settings.scale)
        let columns = OverlayContentColumns.visibleColumnStates(
            for: OverlayContentColumns.relative,
            settings: settings
        )
        let contentWidth = columns.reduce(CGFloat(0)) { total, column in
            total + max(Layout.minimumColumnWidth, CGFloat(column.width))
        }
        let rowCount = max(
            1,
            min(
                Layout.maximumRows,
                relativeCarsEachSide(settings)
                    + relativeCarsEachSide(settings)
                    + 1
            )
        )
        let bottomReserve = chromeOption(
            settings: settings,
            sessionKey: sessionKey,
            testKey: "chrome.footer.source.test",
            practiceKey: "chrome.footer.source.practice",
            qualifyingKey: "chrome.footer.source.qualifying",
            raceKey: "chrome.footer.source.race"
        ) ? Layout.footerHeight : Layout.hiddenFooterBottomPadding
        let columnGaps = CGFloat(max(0, columns.count - 1)) * Layout.columnGap
        let rowGaps = CGFloat(max(0, rowCount - 1)) * Layout.rowGap
        let width = max(
            260,
            contentWidth + columnGaps + Layout.padding * 2 + Layout.columnInset * 2
        )
        let height = Layout.headerHeight
            + Layout.padding
            + CGFloat(rowCount) * Layout.rowHeight
            + rowGaps
            + bottomReserve
        return NSSize(width: width * scale, height: height * scale)
    }

    private func buildRows(snapshot: LiveTelemetrySnapshot, now: Date) -> [RelativeDesignV2Row] {
        guard snapshot.isConnected, snapshot.isCollecting else {
            statusText = "waiting for iRacing"
            sourceText = "source: waiting"
            return []
        }

        guard let updatedAt = snapshot.lastUpdatedAtUtc,
              abs(now.timeIntervalSince(updatedAt)) <= Layout.staleSeconds else {
            statusText = "waiting for fresh telemetry"
            sourceText = "source: waiting"
            return []
        }

        let proximity = snapshot.proximity
        let referenceRow = RelativeDesignV2Row(
            position: referencePosition(snapshot.latestFrame),
            driver: referenceDriver(snapshot.latestFrame),
            gap: "0.000",
            classColorHex: snapshot.latestFrame?.capturedReferenceCar?.carClassColorHex ?? "#FFDA59",
            isReference: true,
            isAhead: false,
            isBehind: false,
            isPit: snapshot.latestFrame?.onPitRoad == true,
            isPartial: false,
            lapDeltaToReference: 0
        )

        let ahead = proximity.nearbyCars
            .filter { $0.relativeLaps > 0 }
            .sorted { sortKey($0) < sortKey($1) }
            .prefix(clampedCarsAhead)
            .sorted { sortKey($0) > sortKey($1) }
            .map { displayRow(car: $0, direction: .ahead) }
        let behind = proximity.nearbyCars
            .filter { $0.relativeLaps < 0 }
            .sorted { sortKey($0) < sortKey($1) }
            .prefix(clampedCarsBehind)
            .map { displayRow(car: $0, direction: .behind) }
        let rows = Array(ahead) + [referenceRow] + Array(behind)
        let shownCars = rows.filter { !$0.isReference }.count
        statusText = "\(referenceRow.position) - \(shownCars) cars"
        sourceText = proximity.hasData
            ? "source: live proximity telemetry"
            : "source: waiting"
        return proximity.hasData ? rows : [referenceRow]
    }

    private func displayRow(car: LiveProximityCar, direction: RelativeDesignV2Direction) -> RelativeDesignV2Row {
        RelativeDesignV2Row(
            position: car.classPosition.map { "\($0)" } ?? car.overallPosition.map { "\($0)" } ?? "--",
            driver: car.driverName ?? MockDriverNames.displayName(for: car.carIdx),
            gap: relativeGap(car: car, direction: direction),
            classColorHex: car.carClassColorHex,
            isReference: false,
            isAhead: direction == .ahead,
            isBehind: direction == .behind,
            isPit: car.onPitRoad == true,
            isPartial: car.relativeSeconds == nil && car.relativeMeters == nil,
            lapDeltaToReference: car.lapDeltaToReference
        )
    }

    private func drawRows(_ rows: [RelativeDesignV2Row?], in rect: NSRect) {
        let scale = designScale
        let rowHeight = scaled(Layout.rowHeight)
        let rowGap = rows.count > 1 ? scaled(Layout.rowGap) : 0
        for index in 0..<min(rows.count, Layout.maximumRows) {
            let rowY = rect.minY + CGFloat(index) * (rowHeight + rowGap)
            let rowRect = NSRect(x: rect.minX, y: rowY, width: rect.width, height: rowHeight)
            guard let row = rows[index] else {
                rounded(
                    rowRect,
                    radius: 5 * scale,
                    fill: theme.colors.surfaceInset,
                    stroke: theme.colors.borderMuted.withAlphaComponent(0.28),
                    lineWidth: 1
                )
                continue
            }

            rounded(
                rowRect,
                radius: 5 * scale,
                fill: rowFill(row),
                stroke: rowStroke(row),
                lineWidth: 1
            )
            drawColumns(row, in: rowRect)
        }
    }

    private func drawColumns(_ row: RelativeDesignV2Row, in rowRect: NSRect) {
        let columns = displayColumns
        let scale = designScale
        let availableWidth = rowRect.width
            - scaled(Layout.columnInset) * 2
            - CGFloat(max(0, columns.count - 1)) * scaled(Layout.columnGap)
        let configuredWidth = max(
            1,
            columns.reduce(CGFloat(0)) { total, column in
                total + max(Layout.minimumColumnWidth, CGFloat(column.width)) * scale
            }
        )
        let fitScale = min(1, availableWidth / configuredWidth)
        var x = rowRect.minX + scaled(Layout.columnInset)

        for column in columns {
            let columnWidth = max(Layout.minimumColumnWidth, CGFloat(column.width)) * scale * fitScale
            let cell = NSRect(x: x, y: rowRect.midY - scaled(9), width: columnWidth, height: scaled(18))
            drawColumnValue(row, column: column, in: cell)
            x += columnWidth + scaled(Layout.columnGap)
        }
    }

    private func drawColumnValue(_ row: RelativeDesignV2Row, column: OverlayContentColumnState, in rect: NSRect) {
        let scale = designScale
        switch column.definition.dataKey {
        case OverlayContentColumns.dataRelativePosition:
            drawText(
                row.position,
                in: rect,
                font: overlayFont(ofSize: 10 * scale, weight: .semibold),
                color: row.isReference ? theme.colors.accentPrimary : textColor(row),
                alignment: textAlignment(column.definition.alignment)
            )
        case OverlayContentColumns.dataDriver:
            drawText(
                row.driver,
                in: rect,
                font: overlayFont(ofSize: (designRows().count > 9 ? 11.5 : 13) * scale, weight: .bold),
                color: textColor(row),
                alignment: textAlignment(column.definition.alignment)
            )
        case OverlayContentColumns.dataGap:
            drawText(
                row.gap,
                in: rect,
                font: OverlayTheme.monospacedFont(size: (designRows().count > 9 ? 11.5 : 13) * scale, weight: .bold),
                color: gapColor(row),
                alignment: textAlignment(column.definition.alignment)
            )
        case OverlayContentColumns.dataPit:
            if row.isPit {
                drawText(
                    "PIT",
                    in: rect,
                    font: overlayFont(ofSize: 10 * scale, weight: .bold),
                    color: theme.colors.partial,
                    alignment: textAlignment(column.definition.alignment)
                )
            }
        default:
            break
        }
    }

    private func designRows() -> [RelativeDesignV2Row?] {
        guard !latestRows.isEmpty else {
            return [nil]
        }

        let aheadCapacity = clampedCarsAhead
        let behindCapacity = clampedCarsBehind
        let reference = latestRows.first { $0.isReference }
        let hasReference = reference != nil
        let visibleRows = max(1, min(Layout.maximumRows, aheadCapacity + behindCapacity + (hasReference ? 1 : 0)))
        var rows = Array<RelativeDesignV2Row?>(repeating: nil, count: visibleRows)

        let ahead = latestRows.filter(\.isAhead)
        let aheadStart = max(0, aheadCapacity - ahead.count)
        for (offset, row) in ahead.enumerated() where aheadStart + offset < rows.count {
            rows[aheadStart + offset] = row
        }

        let behindStart = hasReference ? aheadCapacity + 1 : aheadCapacity
        if let reference, aheadCapacity < rows.count {
            rows[aheadCapacity] = reference
        }

        let behind = latestRows.filter(\.isBehind)
        for (offset, row) in behind.enumerated() where behindStart + offset < rows.count {
            rows[behindStart + offset] = row
        }

        return rows
    }

    private func tableRect(rowCount: Int) -> NSRect {
        let count = max(1, rowCount)
        let rowAreaHeight = CGFloat(count) * scaled(Layout.rowHeight)
            + CGFloat(max(0, count - 1)) * scaled(Layout.rowGap)
        return NSRect(
            x: scaled(Layout.padding),
            y: scaled(Layout.headerHeight + Layout.padding),
            width: bounds.width - scaled(Layout.padding) * 2,
            height: rowAreaHeight
        )
    }

    private func rowFill(_ row: RelativeDesignV2Row) -> NSColor {
        if row.isReference {
            let accent = OverlayClassColor.color(row.classColorHex) ?? theme.colors.accentPrimary
            return OverlayClassColor.blend(panel: theme.colors.surfaceInset, accent: accent, panelWeight: 10, accentWeight: 1)
        }

        if row.isPit {
            let accent = OverlayClassColor.color(row.classColorHex) ?? theme.colors.partial
            return OverlayClassColor.blend(panel: theme.colors.surfaceRaised, accent: accent, panelWeight: 13, accentWeight: 1)
        }

        if let classColor = OverlayClassColor.color(row.classColorHex) {
            return OverlayClassColor.blend(panel: theme.colors.surfaceRaised, accent: classColor, panelWeight: 12, accentWeight: 1)
        }

        return row.isPartial ? theme.colors.surfaceInset : theme.colors.surfaceRaised
    }

    private func rowStroke(_ row: RelativeDesignV2Row) -> NSColor {
        if row.isReference {
            return theme.colors.accentPrimary.withAlphaComponent(0.42)
        }

        if row.isPit {
            return theme.colors.partial.withAlphaComponent(0.34)
        }

        return theme.colors.borderMuted.withAlphaComponent(row.isPartial ? 0.22 : 0.34)
    }

    private func gapColor(_ row: RelativeDesignV2Row) -> NSColor {
        if row.isReference {
            return theme.colors.textPrimary
        }

        if let color = lappedTextColor(row.lapDeltaToReference) {
            return color
        }

        if row.isPartial || row.isPit {
            return theme.colors.textMuted
        }

        return timingColor(row)
    }

    private func textColor(_ row: RelativeDesignV2Row) -> NSColor {
        if row.isReference {
            return theme.colors.textPrimary
        }

        if let color = lappedTextColor(row.lapDeltaToReference) {
            return color
        }

        return row.isPartial || row.isPit ? theme.colors.textMuted : theme.colors.textPrimary
    }

    private func lappedTextColor(_ lapDeltaToReference: Int?) -> NSColor? {
        switch lapDeltaToReference {
        case let value? where value >= 2:
            return theme.colors.error
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

    private func timingColor(_ row: RelativeDesignV2Row) -> NSColor {
        if row.isReference {
            return theme.colors.textPrimary
        }

        if row.isPartial || row.isPit {
            return theme.colors.textMuted
        }

        return row.isAhead ? theme.colors.measured : theme.colors.live
    }

    private var displayColumns: [OverlayContentColumnState] {
        OverlayContentColumns.visibleColumnStates(
            for: OverlayContentColumns.relative,
            settings: contentSettings
        )
    }

    private var clampedCarsAhead: Int {
        min(max(carsAhead, 0), 8)
    }

    private var clampedCarsBehind: Int {
        min(max(carsBehind, 0), 8)
    }

    private var designScale: CGFloat {
        Self.clampedScale(contentSettings.scale)
    }

    private func scaled(_ value: CGFloat) -> CGFloat {
        value * designScale
    }

    private static func clampedScale(_ scale: Double) -> CGFloat {
        CGFloat(min(max(scale, 0.6), 2.0))
    }

    private func overlayFont(ofSize size: CGFloat, weight: NSFont.Weight) -> NSFont {
        DesignV2Drawing.font(family: fontFamily, size: size, weight: weight)
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

    private func relativeGap(car: LiveProximityCar, direction: RelativeDesignV2Direction) -> String {
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

    private func trimError(_ message: String) -> String {
        message.count <= 80 ? message : String(message.prefix(77)) + "..."
    }

    private func applyChromeSettings(sessionKey: String?) {
        let nextShowHeaderStatus = Self.chromeOption(
            settings: contentSettings,
            sessionKey: sessionKey,
            testKey: "chrome.header.status.test",
            practiceKey: "chrome.header.status.practice",
            qualifyingKey: "chrome.header.status.qualifying",
            raceKey: "chrome.header.status.race"
        )
        let nextShowFooterSource = Self.chromeOption(
            settings: contentSettings,
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
        needsDisplay = true
    }

    private static func relativeCarsEachSide(_ settings: OverlaySettings) -> Int {
        min(max(max(settings.relativeCarsAhead, settings.relativeCarsBehind), 0), 8)
    }

    private static func chromeOption(
        settings: OverlaySettings,
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
            return boolOption(settings: settings, key: practiceKey, defaultValue: true)
        case "practice":
            return boolOption(settings: settings, key: practiceKey, defaultValue: true)
        case "qualifying":
            return boolOption(settings: settings, key: qualifyingKey, defaultValue: true)
        case "race":
            return boolOption(settings: settings, key: raceKey, defaultValue: true)
        default:
            return true
        }
    }

    private static func sessionKind(_ sessionKey: String?) -> String? {
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

    private static func boolOption(settings: OverlaySettings, key: String, defaultValue: Bool) -> Bool {
        guard let value = settings.options[key]?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() else {
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

    private func rounded(
        _ rect: NSRect,
        radius: CGFloat,
        fill: NSColor?,
        stroke: NSColor?,
        lineWidth: CGFloat
    ) {
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
}

private struct RelativeDesignV2Row {
    var position: String
    var driver: String
    var gap: String
    var classColorHex: String?
    var isReference: Bool
    var isAhead: Bool
    var isBehind: Bool
    var isPit: Bool
    var isPartial: Bool
    var lapDeltaToReference: Int?
}

private enum RelativeDesignV2Direction {
    case ahead
    case behind
}
