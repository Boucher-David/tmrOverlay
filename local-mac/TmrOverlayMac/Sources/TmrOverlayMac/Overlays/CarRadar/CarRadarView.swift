import AppKit

enum CarRadarColorMode {
    case neutralProximity
    case classColorOnly
    case classColorToAlertRed
}

final class CarRadarView: NSView {
    private enum Layout {
        static let radarRangeSeconds = 2.0
        static let multiclassWarningRangeSeconds = 25.0
        static let snapshotStaleSeconds = 1.5
        static let fadeInSeconds = 0.25
        static let fadeOutSeconds = 0.85
        static let minimumVisibleAlpha = 0.02
        static let maxWideRowRadarCars = 18
        static let focusedCarLengthMeters = 4.746
        static let radarRangeMeters = focusedCarLengthMeters * 6
        static let contactWindowMeters = focusedCarLengthMeters
        static let sideAttachmentWindowMeters = focusedCarLengthMeters * 2
        static let minimumSideAttachmentWindowSeconds = 0.8
        static let multiclassWarningArcStartDegrees: CGFloat = 242.5
        static let multiclassWarningArcEndDegrees: CGFloat = 297.5
        static let gapLabelHeight: CGFloat = 14
        static let suspiciousZeroTimingSeconds = 0.05
        static let suspiciousZeroTimingLaps = 0.001
        static let focusedCarWidth: CGFloat = 24
        static let focusedCarHeight: CGFloat = 48
        static let radarCarWidth: CGFloat = 20
        static let radarCarHeight: CGFloat = 36
        static let carCornerRadius: CGFloat = 4
        static let separatedCarPaddingPixels: CGFloat = 2
        static let wideRowBucketPixels: CGFloat = 30
        static let wideRowSlotPitchPixels: CGFloat = 36
        static let proximityWarningGapMeters = 2.0
        static let contactRedStart = 0.74
    }

    private var proximity = LiveProximitySnapshot.unavailable
    private var carVisuals: [Int: RadarCarVisual] = [:]
    private var lastRefreshAtUtc: Date?
    private var radarAlpha = 0.0
    private var leftSideAlpha = 0.0
    private var rightSideAlpha = 0.0
    private var overlayError: String?
    var showMulticlassWarning = true {
        didSet { needsDisplay = true }
    }
    var showGapLabels = false {
        didSet { needsDisplay = true }
    }
    var debugOpaqueRendering = false {
        didSet { needsDisplay = true }
    }
    var colorMode: CarRadarColorMode = .neutralProximity {
        didSet { needsDisplay = true }
    }
    var settingsPreviewVisible = false {
        didSet {
            if settingsPreviewVisible {
                radarAlpha = 1
            }

            needsDisplay = true
        }
    }
    var fontFamily = "SF Pro" {
        didSet { needsDisplay = true }
    }

    override init(frame frameRect: NSRect = NSRect(origin: .zero, size: CarRadarOverlayDefinition.definition.defaultSize)) {
        super.init(frame: frameRect)
        wantsLayer = true
        layer?.backgroundColor = NSColor.clear.cgColor
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        guard shouldPaintRadar else {
            return
        }

        if let overlayError {
            drawError(overlayError)
            return
        }

        let diameter = min(bounds.width, bounds.height) - 8
        let rect = NSRect(
            x: (bounds.width - diameter) / 2,
            y: (bounds.height - diameter) / 2,
            width: diameter,
            height: diameter
        )
        NSColor(red255: 12, green: 18, blue: 22, alpha: scaledAlpha(0.32)).setFill()
        NSBezierPath(ovalIn: rect).fill()
        NSColor(calibratedWhite: 1, alpha: scaledAlpha(0.47)).setStroke()
        NSBezierPath(ovalIn: rect).stroke()

        drawMulticlassApproachWarning(in: rect)
        drawDistanceRings(in: rect)
        let sideAttachments = currentSideWarningAttachments()
        drawNearbyCars(in: rect, sideAttachments: sideAttachments)
        drawSideWarningCars(in: rect, sideAttachments: sideAttachments)
        drawPlayerCar(in: rect)
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        let now = Date()
        let elapsedSeconds = lastRefreshAtUtc.map { min(max(now.timeIntervalSince($0), 0), 0.5) } ?? Layout.fadeInSeconds
        lastRefreshAtUtc = now
        proximity = isFresh(snapshot, now: now) ? snapshot.proximity : .unavailable
        overlayError = nil
        updateFadeState(now: now, elapsedSeconds: elapsedSeconds)
        needsDisplay = true
    }

    func showOverlayError(_ message: String) {
        overlayError = message
        needsDisplay = true
    }

    private var shouldPaintRadar: Bool {
        overlayError != nil
            || settingsPreviewVisible
            || radarAlpha > Layout.minimumVisibleAlpha
            || leftSideAlpha > Layout.minimumVisibleAlpha
            || rightSideAlpha > Layout.minimumVisibleAlpha
            || carVisuals.values.contains { $0.alpha > Layout.minimumVisibleAlpha }
    }

    private var hasCurrentRadarSignal: Bool {
        overlayError != nil
            || settingsPreviewVisible
            || proximity.hasCarLeft
            || proximity.hasCarRight
            || !currentRadarCars().isEmpty
            || currentMulticlassApproach() != nil
    }

    private func isFresh(_ snapshot: LiveTelemetrySnapshot, now: Date) -> Bool {
        guard snapshot.isConnected,
              snapshot.isCollecting,
              let lastUpdatedAtUtc = snapshot.lastUpdatedAtUtc else {
            return false
        }

        let ageSeconds = now.timeIntervalSince(lastUpdatedAtUtc)
        return ageSeconds >= 0 && ageSeconds <= Layout.snapshotStaleSeconds
    }

    private func updateFadeState(now: Date, elapsedSeconds: TimeInterval) {
        updateRadarAlpha(hasCurrentSignal: hasCurrentRadarSignal, elapsedSeconds: elapsedSeconds)
        updateSideWarningAlphas(elapsedSeconds: elapsedSeconds)
        updateCarVisuals(now: now, elapsedSeconds: elapsedSeconds)
    }

    private func updateRadarAlpha(hasCurrentSignal: Bool, elapsedSeconds: TimeInterval) {
        let target = hasCurrentSignal ? 1.0 : 0.0
        let duration = target > radarAlpha ? Layout.fadeInSeconds : Layout.fadeOutSeconds
        radarAlpha = moveToward(radarAlpha, target: target, delta: elapsedSeconds / duration)
    }

    private func updateSideWarningAlphas(elapsedSeconds: TimeInterval) {
        leftSideAlpha = moveTowardSideAlpha(leftSideAlpha, visible: proximity.hasCarLeft, elapsedSeconds: elapsedSeconds)
        rightSideAlpha = moveTowardSideAlpha(rightSideAlpha, visible: proximity.hasCarRight, elapsedSeconds: elapsedSeconds)
    }

    private func moveTowardSideAlpha(_ current: Double, visible: Bool, elapsedSeconds: TimeInterval) -> Double {
        let target = visible ? 1.0 : 0.0
        let duration = target > current ? Layout.fadeInSeconds : Layout.fadeOutSeconds
        return moveToward(current, target: target, delta: elapsedSeconds / duration)
    }

    private func updateCarVisuals(now: Date, elapsedSeconds: TimeInterval) {
        var currentCars: [Int: LiveProximityCar] = [:]
        for car in currentRadarCars() {
            if let existing = currentCars[car.carIdx],
               abs(rangeRatio(existing)) <= abs(rangeRatio(car)) {
                continue
            }

            currentCars[car.carIdx] = car
        }

        for car in currentCars.values {
            let visual = carVisuals[car.carIdx] ?? RadarCarVisual(car: car)
            visual.car = car
            visual.lastSeenAtUtc = now
            visual.alpha = moveToward(visual.alpha, target: 1, delta: elapsedSeconds / Layout.fadeInSeconds)
            carVisuals[car.carIdx] = visual
        }

        for (carIdx, visual) in carVisuals where currentCars[carIdx] == nil {
            visual.alpha = moveToward(visual.alpha, target: 0, delta: elapsedSeconds / Layout.fadeOutSeconds)
        }

        let expiredCarIds = carVisuals
            .filter { _, visual in
                visual.alpha <= Layout.minimumVisibleAlpha
                    && now.timeIntervalSince(visual.lastSeenAtUtc) > Layout.fadeOutSeconds
            }
            .map(\.key)
        for carIdx in expiredCarIds {
            carVisuals.removeValue(forKey: carIdx)
        }
    }

    private func currentRadarCars() -> [LiveProximityCar] {
        proximity.nearbyCars.filter(isInRadarRange)
    }

    private func currentMulticlassApproach() -> LiveMulticlassApproach? {
        guard showMulticlassWarning else {
            return nil
        }

        return proximity.multiclassApproaches
            .filter(isInMulticlassWarningRange)
            .max { $0.urgency < $1.urgency }
    }

    private func moveToward(_ current: Double, target: Double, delta: Double) -> Double {
        guard delta > 0 else {
            return current
        }

        return current < target
            ? min(target, current + delta)
            : max(target, current - delta)
    }

    private func drawError(_ message: String) {
        let diameter = min(bounds.width, bounds.height) - 8
        let rect = NSRect(
            x: (bounds.width - diameter) / 2,
            y: (bounds.height - diameter) / 2,
            width: diameter,
            height: diameter
        )
        NSColor(red255: 32, green: 14, blue: 18, alpha: 0.58).setFill()
        NSBezierPath(ovalIn: rect).fill()
        NSColor(red255: 236, green: 112, blue: 99, alpha: 0.82).setStroke()
        NSBezierPath(ovalIn: rect).stroke()

        let titleAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 13, weight: .semibold),
            .foregroundColor: NSColor(red255: 255, green: 225, blue: 220, alpha: 0.94)
        ]
        let detailAttrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 10, weight: .regular),
            .foregroundColor: NSColor(red255: 255, green: 225, blue: 220, alpha: 0.78)
        ]
        drawCentered("radar error", rect: rect.offsetBy(dx: 0, dy: 6), attributes: titleAttrs)
        drawCentered(trimError(message), rect: rect.offsetBy(dx: 0, dy: -18), attributes: detailAttrs)
    }

    private func drawCentered(_ text: String, rect: NSRect, attributes: [NSAttributedString.Key: Any]) {
        let string = NSString(string: text)
        let size = string.size(withAttributes: attributes)
        string.draw(
            at: NSPoint(x: rect.midX - size.width / 2, y: rect.midY - size.height / 2),
            withAttributes: attributes
        )
    }

    private func drawDistanceRings(in rect: NSRect) {
        NSColor(calibratedWhite: 1, alpha: scaledAlpha(0.16)).setStroke()
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 9, weight: .regular),
            .foregroundColor: NSColor(red255: 220, green: 230, blue: 236, alpha: scaledAlpha(0.46))
        ]
        for index in 1...2 {
            let inset = rect.width * CGFloat(index) / 6
            let radius = rect.width / 2 - inset
            NSBezierPath(ovalIn: rect.insetBy(dx: inset, dy: inset)).stroke()
            NSString(string: ringGapText(index)).draw(
                in: NSRect(x: rect.midX + radius * 0.35, y: rect.midY + radius - 8, width: 58, height: 16),
                withAttributes: attrs
            )
        }
    }

    private func drawPlayerCar(in rect: NSRect) {
        let carRect = NSRect(
            x: rect.midX - Layout.focusedCarWidth / 2,
            y: rect.midY - Layout.focusedCarHeight / 2,
            width: Layout.focusedCarWidth,
            height: Layout.focusedCarHeight
        )
        NSColor(calibratedWhite: 1, alpha: scaledAlpha(0.94)).setFill()
        NSBezierPath(roundedRect: carRect, xRadius: Layout.carCornerRadius, yRadius: Layout.carCornerRadius).fill()
        NSColor(red255: 20, green: 24, blue: 28, alpha: scaledAlpha(0.9)).setStroke()
        NSBezierPath(roundedRect: carRect, xRadius: Layout.carCornerRadius, yRadius: Layout.carCornerRadius).stroke()

        if showGapLabels {
            drawCenterGapLabel(in: carRect)
        }
    }

    private func drawMulticlassApproachWarning(in rect: NSRect) {
        guard let approach = currentMulticlassApproach() else {
            return
        }

        let urgency = min(max(approach.urgency, 0), 1)
        let alpha = scaledAlpha(0.47 + urgency * 0.43)
        let path = NSBezierPath()
        path.lineWidth = 5
        path.lineCapStyle = .round
        path.appendArc(
            withCenter: NSPoint(x: rect.midX, y: rect.midY),
            radius: rect.width / 2 - 4,
            startAngle: Layout.multiclassWarningArcStartDegrees,
            endAngle: Layout.multiclassWarningArcEndDegrees
        )
        NSColor(red255: 236, green: 112, blue: 99, alpha: alpha).setStroke()
        path.stroke()

        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 11, weight: .semibold),
            .foregroundColor: NSColor(red255: 255, green: 225, blue: 220, alpha: alpha)
        ]
        let text = NSString(string: multiclassWarningText(approach))
        let size = text.size(withAttributes: attrs)
        text.draw(
            at: NSPoint(x: rect.midX - size.width / 2, y: rect.minY + 30),
            withAttributes: attrs
        )
    }

    private func drawNearbyCars(in rect: NSRect, sideAttachments: SideWarningAttachments) {
        let placements = radarCarPlacements(in: rect, sideAttachments: sideAttachments)

        for placement in placements {
            let visual = placement.visual
            let car = visual.car
            let visualAlpha = visual.alpha * radarAlpha * radarEntryOpacity(for: car)
            carColor(for: car, alphaMultiplier: visualAlpha).setFill()
            NSBezierPath(roundedRect: placement.bounds, xRadius: Layout.carCornerRadius, yRadius: Layout.carCornerRadius).fill()
            NSColor(calibratedWhite: 1, alpha: CGFloat(min(1, 1.07 * visualAlpha))).setStroke()
            NSBezierPath(roundedRect: placement.bounds, xRadius: Layout.carCornerRadius, yRadius: Layout.carCornerRadius).stroke()
        }

        guard showGapLabels else {
            return
        }

        for placement in placements {
            drawGapLabel(
                for: placement.visual.car,
                in: placement.bounds,
                alphaMultiplier: placement.visual.alpha * radarAlpha * radarEntryOpacity(for: placement.visual.car)
            )
        }
    }

    private func radarCarPlacements(in rect: NSRect, sideAttachments: SideWarningAttachments) -> [RadarCarPlacement] {
        let usableRadius = rect.width / 2 - 34
        let visibleCars = Array(carVisuals.values
            .filter { $0.alpha > Layout.minimumVisibleAlpha }
            .filter { !sideAttachments.contains(carIdx: $0.car.carIdx) }
            .sorted { abs(rangeRatio($0.car)) < abs(rangeRatio($1.car)) }
            .prefix(Layout.maxWideRowRadarCars))
        let candidates = visibleCars.enumerated().map { index, visual in
            let offset = longitudinalOffset(for: visual.car, usableRadius: usableRadius)
            return WideRowCandidate(
                visual: visual,
                sourceIndex: index,
                idealOffset: offset,
                direction: placementDirection(for: visual.car, index: index, idealOffset: offset)
            )
        }
        var rows: [WideRadarRow] = []

        for candidate in candidates.sorted(by: { $0.idealOffset < $1.idealOffset }) {
            if let rowIndex = rows.firstIndex(where: {
                $0.direction == candidate.direction
                    && abs($0.anchorOffset - candidate.idealOffset) <= Layout.wideRowBucketPixels
            }) {
                rows[rowIndex].candidates.append(candidate)
            } else {
                rows.append(WideRadarRow(
                    anchorOffset: candidate.idealOffset,
                    direction: candidate.direction,
                    candidates: [candidate]
                ))
            }
        }

        return rows.flatMap { row in
            placements(for: row, centerX: rect.midX, centerY: rect.midY, usableRadius: usableRadius)
        }
    }

    private func placements(
        for row: WideRadarRow,
        centerX: CGFloat,
        centerY: CGFloat,
        usableRadius: CGFloat
    ) -> [RadarCarPlacement] {
        guard !row.candidates.isEmpty else {
            return []
        }

        let rowOffset = row.candidates.reduce(CGFloat(0)) { $0 + $1.idealOffset } / CGFloat(row.candidates.count)
        let clampedRowMagnitude = min(abs(rowOffset), usableRadius)
        let availableHalfWidth = sqrt(max(0, usableRadius * usableRadius - clampedRowMagnitude * clampedRowMagnitude))
        let maxCenterOffset = max(0, availableHalfWidth - Layout.radarCarWidth / 2 - 4)
        let maxSlots = max(1, Int((maxCenterOffset * 2 / Layout.wideRowSlotPitchPixels).rounded(.down)) + 1)
        let visibleCandidates = Array(row.candidates
            .sorted {
                if $0.sourceIndex != $1.sourceIndex {
                    return $0.sourceIndex < $1.sourceIndex
                }

                return $0.visual.car.carIdx < $1.visual.car.carIdx
            }
            .prefix(maxSlots))
        let lineWidth = Layout.wideRowSlotPitchPixels * CGFloat(max(0, visibleCandidates.count - 1))

        return visibleCandidates.enumerated().map { slotIndex, candidate in
            let xOffset = CGFloat(slotIndex) * Layout.wideRowSlotPitchPixels - lineWidth / 2
            return RadarCarPlacement(
                visual: candidate.visual,
                bounds: radarCarBounds(centerX: centerX + xOffset, centerY: centerY - rowOffset),
                offset: rowOffset
            )
        }
    }

    private func placementDirection(for car: LiveProximityCar, index: Int, idealOffset: CGFloat) -> CGFloat {
        if idealOffset < 0 {
            return -1
        }

        if idealOffset > 0 {
            return 1
        }

        if abs(car.relativeLaps) > 0.0001 {
            return car.relativeLaps < 0 ? -1 : 1
        }

        return index.isMultiple(of: 2) ? 1 : -1
    }

    private func radarCarBounds(centerX: CGFloat, centerY: CGFloat) -> NSRect {
        NSRect(
            x: centerX - Layout.radarCarWidth / 2,
            y: centerY - Layout.radarCarHeight / 2,
            width: Layout.radarCarWidth,
            height: Layout.radarCarHeight
        )
    }

    private func drawCenterGapLabel(in carRect: NSRect) {
        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 8, weight: .bold),
            .foregroundColor: NSColor(red255: 20, green: 24, blue: 28, alpha: scaledAlpha(0.92))
        ]
        let string = NSString(string: "0.0s")
        let textSize = string.size(withAttributes: attrs)
        string.draw(
            at: NSPoint(x: carRect.midX - textSize.width / 2, y: carRect.midY - textSize.height / 2),
            withAttributes: attrs
        )
    }

    private func drawGapLabel(for car: LiveProximityCar, in carRect: NSRect, alphaMultiplier: Double) {
        guard let text = gapLabelText(for: car) else {
            return
        }

        let attrs: [NSAttributedString.Key: Any] = [
            .font: overlayFont(ofSize: 8, weight: .bold),
            .foregroundColor: NSColor(calibratedWhite: 1, alpha: CGFloat(min(1, 0.96 * alphaMultiplier)))
        ]
        let string = NSString(string: text)
        let textSize = string.size(withAttributes: attrs)
        let labelWidth = max(32, textSize.width + 8)
        let labelRect = NSRect(
            x: carRect.midX - labelWidth / 2,
            y: carRect.midY - Layout.gapLabelHeight / 2,
            width: labelWidth,
            height: Layout.gapLabelHeight
        )
        NSColor(red255: 10, green: 14, blue: 17, alpha: CGFloat(min(1, 0.78 * alphaMultiplier))).setFill()
        NSBezierPath(roundedRect: labelRect, xRadius: 3, yRadius: 3).fill()
        NSColor(calibratedWhite: 1, alpha: CGFloat(min(1, 0.34 * alphaMultiplier))).setStroke()
        NSBezierPath(roundedRect: labelRect, xRadius: 3, yRadius: 3).stroke()
        string.draw(
            at: NSPoint(x: labelRect.midX - textSize.width / 2, y: labelRect.midY - textSize.height / 2),
            withAttributes: attrs
        )
    }

    private func currentSideWarningAttachments() -> SideWarningAttachments {
        var usedCarIdxs = Set<Int>()
        let left = leftSideAlpha > Layout.minimumVisibleAlpha
            ? selectSideAttachment(excluding: usedCarIdxs)
            : nil
        if let left {
            usedCarIdxs.insert(left.car.carIdx)
        }

        let right = rightSideAlpha > Layout.minimumVisibleAlpha
            ? selectSideAttachment(excluding: usedCarIdxs)
            : nil
        return SideWarningAttachments(left: left, right: right)
    }

    private func selectSideAttachment(excluding excludedCarIdxs: Set<Int>) -> RadarCarVisual? {
        carVisuals.values
            .filter { $0.alpha > Layout.minimumVisibleAlpha }
            .filter { !excludedCarIdxs.contains($0.car.carIdx) }
            .filter { isSideAttachmentCandidate($0.car) }
            .sorted {
                let leftRange = abs(rangeRatio($0.car))
                let rightRange = abs(rangeRatio($1.car))
                if leftRange != rightRange {
                    return leftRange < rightRange
                }

                if $0.alpha != $1.alpha {
                    return $0.alpha > $1.alpha
                }

                return $0.car.carIdx < $1.car.carIdx
            }
            .first
    }

    private func isSideAttachmentCandidate(_ car: LiveProximityCar) -> Bool {
        guard isInRadarRange(car) else {
            return false
        }

        if let meters = reliableRelativeMeters(for: car) {
            return abs(meters) <= Layout.sideAttachmentWindowMeters
        }

        guard let seconds = reliableRelativeSeconds(for: car) else {
            return false
        }

        let windowSeconds = max(
            Layout.minimumSideAttachmentWindowSeconds,
            proximity.sideOverlapWindowSeconds * 2
        )
        return abs(seconds) <= windowSeconds
    }

    private func drawSideWarningCars(in rect: NSRect, sideAttachments: SideWarningAttachments) {
        guard leftSideAlpha > Layout.minimumVisibleAlpha || rightSideAlpha > Layout.minimumVisibleAlpha else {
            return
        }

        let usableRadius = rect.width / 2 - 34
        if leftSideAlpha > Layout.minimumVisibleAlpha {
            drawWarningCar(
                x: rect.midX - 42,
                y: sideWarningCenterY(centerY: rect.midY, usableRadius: usableRadius, visual: sideAttachments.left),
                alphaMultiplier: leftSideAlpha * radarAlpha * sideAttachmentAlpha(sideAttachments.left),
                mappedToTimedCar: sideAttachments.left != nil
            )
        }

        if rightSideAlpha > Layout.minimumVisibleAlpha {
            drawWarningCar(
                x: rect.midX + 42,
                y: sideWarningCenterY(centerY: rect.midY, usableRadius: usableRadius, visual: sideAttachments.right),
                alphaMultiplier: rightSideAlpha * radarAlpha * sideAttachmentAlpha(sideAttachments.right),
                mappedToTimedCar: sideAttachments.right != nil
            )
        }
    }

    private func sideWarningCenterY(centerY: CGFloat, usableRadius: CGFloat, visual: RadarCarVisual?) -> CGFloat {
        guard let visual else {
            return centerY
        }

        let maximumBias = Layout.focusedCarHeight * 0.55
        let offset = min(max(longitudinalOffset(for: visual.car, usableRadius: usableRadius), -maximumBias), maximumBias)
        return centerY - offset
    }

    private func sideAttachmentAlpha(_ visual: RadarCarVisual?) -> Double {
        guard let visual else {
            return 1
        }

        return max(0.45, visual.alpha)
    }

    private func drawWarningCar(x: CGFloat, y: CGFloat, alphaMultiplier: Double, mappedToTimedCar: Bool) {
        let carRect = NSRect(
            x: x - Layout.radarCarWidth / 2,
            y: y - Layout.radarCarHeight / 2,
            width: Layout.radarCarWidth,
            height: Layout.radarCarHeight
        )
        let fillAlpha = mappedToTimedCar ? 0.96 : 0.93
        NSColor(red255: 236, green: 112, blue: 99, alpha: CGFloat(min(1, fillAlpha * alphaMultiplier))).setFill()
        NSBezierPath(roundedRect: carRect, xRadius: Layout.carCornerRadius, yRadius: Layout.carCornerRadius).fill()
        NSColor(calibratedWhite: 1, alpha: CGFloat(min(1, 0.96 * alphaMultiplier))).setStroke()
        NSBezierPath(roundedRect: carRect, xRadius: Layout.carCornerRadius, yRadius: Layout.carCornerRadius).stroke()
    }

    private func longitudinalOffset(for car: LiveProximityCar, usableRadius: CGFloat) -> CGFloat {
        if let meters = reliableRelativeMeters(for: car) {
            return longitudinalOffsetFromDistance(meters, usableRadius: usableRadius)
        }

        guard let seconds = reliableRelativeSeconds(for: car) else {
            return (car.relativeLaps < 0 ? -1 : 1) * usableRadius
        }

        let sign = seconds == 0 ? 0 : (seconds < 0 ? -1.0 : 1.0)
        guard sign != 0 else {
            return 0
        }

        let absSeconds = abs(seconds)
        let contactSeconds = min(max(proximity.sideOverlapWindowSeconds, 0.05), Layout.radarRangeSeconds * 0.5)
        let separatedCenterOffset = min(
            usableRadius,
            Layout.focusedCarHeight / 2 + Layout.radarCarHeight / 2 + Layout.separatedCarPaddingPixels
        )

        if absSeconds <= contactSeconds {
            return CGFloat(sign) * CGFloat(absSeconds / contactSeconds) * separatedCenterOffset
        }

        let remainingSeconds = max(0.001, Layout.radarRangeSeconds - contactSeconds)
        let remainingPixels = max(0, usableRadius - separatedCenterOffset)
        let outerRatio = min(max((absSeconds - contactSeconds) / remainingSeconds, 0), 1)
        return CGFloat(sign) * (separatedCenterOffset + CGFloat(outerRatio) * remainingPixels)
    }

    private func longitudinalOffsetFromDistance(_ meters: Double, usableRadius: CGFloat) -> CGFloat {
        let sign = meters == 0 ? 0 : (meters < 0 ? -1.0 : 1.0)
        guard sign != 0 else {
            return 0
        }

        let absMeters = abs(meters)
        let separatedCenterOffset = min(
            usableRadius,
            Layout.focusedCarHeight / 2 + Layout.radarCarHeight / 2 + Layout.separatedCarPaddingPixels
        )

        if absMeters <= Layout.contactWindowMeters {
            return CGFloat(sign) * CGFloat(absMeters / Layout.contactWindowMeters) * separatedCenterOffset
        }

        let remainingMeters = max(0.001, Layout.radarRangeMeters - Layout.contactWindowMeters)
        let remainingPixels = max(0, usableRadius - separatedCenterOffset)
        let outerRatio = min(max((absMeters - Layout.contactWindowMeters) / remainingMeters, 0), 1)
        return CGFloat(sign) * (separatedCenterOffset + CGFloat(outerRatio) * remainingPixels)
    }

    private func carColor(for car: LiveProximityCar, alphaMultiplier: Double) -> NSColor {
        let alpha = CGFloat(0.93 * alphaMultiplier)
        let classBaseColor = OverlayClassColor.color(car.carClassColorHex)
            ?? neutralCarBaseColor(alpha: alpha)
        if colorMode == .classColorOnly {
            return NSColor(
                red255: red255(classBaseColor),
                green: green255(classBaseColor),
                blue: blue255(classBaseColor),
                alpha: alpha
            )
        }

        let normalized = proximityTint(for: car)
        let alertRed = NSColor(red255: 255, green: 24, blue: 16, alpha: alpha)
        if colorMode == .classColorToAlertRed {
            let redMix = smoothStep(edge0: 0, edge1: 1, value: normalized)
            return NSColor(
                red255: lerp(red255(classBaseColor), red255(alertRed), redMix),
                green: lerp(green255(classBaseColor), green255(alertRed), redMix),
                blue: lerp(blue255(classBaseColor), blue255(alertRed), redMix),
                alpha: alpha
            )
        }

        let baseColor = neutralCarBaseColor(alpha: alpha)
        let yellow = NSColor(red255: 255, green: 220, blue: 66, alpha: alpha)

        if normalized <= 0 {
            return NSColor(
                red255: red255(baseColor),
                green: green255(baseColor),
                blue: blue255(baseColor),
                alpha: alpha
            )
        }

        if normalized < Layout.contactRedStart {
            let yellowMix = smoothStep(
                edge0: 0,
                edge1: Layout.contactRedStart,
                value: normalized
            )
            return NSColor(
                red255: lerp(red255(baseColor), red255(yellow), yellowMix),
                green: lerp(green255(baseColor), green255(yellow), yellowMix),
                blue: lerp(blue255(baseColor), blue255(yellow), yellowMix),
                alpha: alpha
            )
        }

        let redMix = smoothStep(edge0: Layout.contactRedStart, edge1: 1, value: normalized)
        return NSColor(
            red255: lerp(red255(yellow), red255(alertRed), redMix),
            green: lerp(green255(yellow), green255(alertRed), redMix),
            blue: lerp(blue255(yellow), blue255(alertRed), redMix),
            alpha: alpha
        )
    }

    private func proximityTint(for car: LiveProximityCar) -> Double {
        if let meters = reliableRelativeMeters(for: car) {
            return bumperGapProximity(centerDistanceMeters: abs(meters))
        }

        guard let seconds = reliableRelativeSeconds(for: car) else {
            return 0
        }

        let contactSeconds = min(max(proximity.sideOverlapWindowSeconds, 0.05), Layout.radarRangeSeconds * 0.5)
        let warningGapSeconds = contactSeconds * Layout.proximityWarningGapMeters / Layout.focusedCarLengthMeters
        let secondsGapPastContact = abs(seconds) - contactSeconds
        return 1 - min(max(secondsGapPastContact / max(0.001, warningGapSeconds), 0), 1)
    }

    private func radarEntryOpacity(for car: LiveProximityCar) -> Double {
        if let meters = reliableRelativeMeters(for: car) {
            let warningStartMeters = Layout.contactWindowMeters + Layout.proximityWarningGapMeters
            return opacityBetweenRangeEdgeAndWarningStart(
                absoluteValue: abs(meters),
                warningStart: warningStartMeters,
                radarRange: Layout.radarRangeMeters
            )
        }

        guard let seconds = reliableRelativeSeconds(for: car) else {
            return 0
        }

        let contactSeconds = min(max(proximity.sideOverlapWindowSeconds, 0.05), Layout.radarRangeSeconds * 0.5)
        let warningGapSeconds = contactSeconds * Layout.proximityWarningGapMeters / Layout.focusedCarLengthMeters
        return opacityBetweenRangeEdgeAndWarningStart(
            absoluteValue: abs(seconds),
            warningStart: contactSeconds + warningGapSeconds,
            radarRange: Layout.radarRangeSeconds
        )
    }

    private func opacityBetweenRangeEdgeAndWarningStart(
        absoluteValue: Double,
        warningStart: Double,
        radarRange: Double
    ) -> Double {
        if absoluteValue <= warningStart {
            return 1
        }

        guard radarRange > warningStart else {
            return absoluteValue <= radarRange ? 1 : 0
        }

        let normalized = 1 - min(max((absoluteValue - warningStart) / (radarRange - warningStart), 0), 1)
        return smoothStep(edge0: 0, edge1: 1, value: normalized)
    }

    private func bumperGapProximity(centerDistanceMeters: Double) -> Double {
        let bumperGapMeters = centerDistanceMeters - Layout.focusedCarLengthMeters
        return 1 - min(max(bumperGapMeters / Layout.proximityWarningGapMeters, 0), 1)
    }

    private func neutralCarBaseColor(alpha: CGFloat) -> NSColor {
        NSColor(red255: 255, green: 255, blue: 255, alpha: alpha)
    }

    private func red255(_ color: NSColor) -> CGFloat {
        rgba(color).red * 255
    }

    private func green255(_ color: NSColor) -> CGFloat {
        rgba(color).green * 255
    }

    private func blue255(_ color: NSColor) -> CGFloat {
        rgba(color).blue * 255
    }

    private func rgba(_ color: NSColor) -> (red: CGFloat, green: CGFloat, blue: CGFloat, alpha: CGFloat) {
        let converted = color.usingColorSpace(.deviceRGB) ?? color
        return (converted.redComponent, converted.greenComponent, converted.blueComponent, converted.alphaComponent)
    }

    private func scaledAlpha(_ alpha: CGFloat) -> CGFloat {
        let boostedAlpha = debugOpaqueRendering ? min(1, alpha * 1.85) : alpha
        return min(max(boostedAlpha * CGFloat(radarAlpha), 0), 1)
    }

    private func lerp(_ start: CGFloat, _ end: CGFloat, _ ratio: Double) -> CGFloat {
        start + (end - start) * CGFloat(ratio)
    }

    private func smoothStep(edge0: Double, edge1: Double, value: Double) -> Double {
        let ratio = min(max((value - edge0) / (edge1 - edge0), 0), 1)
        return ratio * ratio * (3 - 2 * ratio)
    }

    private func rangeRatio(_ car: LiveProximityCar) -> Double {
        if let meters = reliableRelativeMeters(for: car) {
            let raw = meters / Layout.radarRangeMeters
            return min(max(raw, -1), 1)
        }

        guard let seconds = reliableRelativeSeconds(for: car) else {
            return car.relativeLaps < 0 ? -1 : 1
        }

        let raw = seconds / Layout.radarRangeSeconds
        return min(max(raw, -1), 1)
    }

    private func isInRadarRange(_ car: LiveProximityCar) -> Bool {
        guard car.onPitRoad != true else {
            return false
        }

        if let meters = reliableRelativeMeters(for: car) {
            return abs(meters) <= Layout.radarRangeMeters
        }

        guard let seconds = reliableRelativeSeconds(for: car) else {
            return false
        }

        return abs(seconds) <= Layout.radarRangeSeconds
    }

    private func ringGapText(_ ringIndex: Int) -> String {
        let seconds = Layout.radarRangeSeconds * (1 - Double(ringIndex) / 3)
        return String(format: "%.1fs", seconds)
    }

    private func multiclassWarningText(_ approach: LiveMulticlassApproach) -> String {
        if let seconds = approach.relativeSeconds {
            return String(format: "multiclass %.1f seconds", abs(seconds))
        }

        return "multiclass approaching"
    }

    private func gapLabelText(for car: LiveProximityCar) -> String? {
        guard let seconds = reliableRelativeSeconds(for: car) else {
            return nil
        }

        return String(format: "%+.1fs", seconds)
    }

    private func isInMulticlassWarningRange(_ approach: LiveMulticlassApproach) -> Bool {
        guard let seconds = approach.relativeSeconds, seconds.isFinite else {
            return false
        }

        return seconds < -Layout.radarRangeSeconds && seconds >= -Layout.multiclassWarningRangeSeconds
    }

    private func reliableRelativeSeconds(for car: LiveProximityCar) -> Double? {
        guard let seconds = car.relativeSeconds, seconds.isFinite else {
            return nil
        }

        guard abs(seconds) <= Layout.suspiciousZeroTimingSeconds,
              abs(car.relativeLaps) >= Layout.suspiciousZeroTimingLaps else {
            return seconds
        }

        return nil
    }

    private func reliableRelativeMeters(for car: LiveProximityCar) -> Double? {
        guard let meters = car.relativeMeters, meters.isFinite else {
            return nil
        }

        return meters
    }

    private func trimError(_ message: String) -> String {
        message.count <= 46 ? message : "\(message.prefix(43))..."
    }

    private func overlayFont(ofSize size: CGFloat, weight: NSFont.Weight) -> NSFont {
        NSFont(name: fontFamily, size: size) ?? NSFont.systemFont(ofSize: size, weight: weight)
    }

    private final class RadarCarVisual {
        var car: LiveProximityCar
        var alpha = 0.0
        var lastSeenAtUtc = Date()

        init(car: LiveProximityCar) {
            self.car = car
        }
    }

    private struct RadarCarPlacement {
        let visual: RadarCarVisual
        let bounds: NSRect
        let offset: CGFloat
    }

    private struct SideWarningAttachments {
        let left: RadarCarVisual?
        let right: RadarCarVisual?

        func contains(carIdx: Int) -> Bool {
            left?.car.carIdx == carIdx || right?.car.carIdx == carIdx
        }
    }

    private struct WideRowCandidate {
        let visual: RadarCarVisual
        let sourceIndex: Int
        let idealOffset: CGFloat
        let direction: CGFloat
    }

    private struct WideRadarRow {
        let anchorOffset: CGFloat
        let direction: CGFloat
        var candidates: [WideRowCandidate]
    }
}
