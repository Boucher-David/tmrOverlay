import AppKit

final class TrackMapView: NSView {
    private static let trackInteriorColor = NSColor(red: 0.035, green: 0.055, blue: 0.071, alpha: 1.0)
    private static let trackHaloColor = NSColor.white.withAlphaComponent(0.32)
    private static let trackLineColor = NSColor(red: 0.87, green: 0.93, blue: 0.96, alpha: 1.0)
    private static let sectorBoundaryColor = NSColor(red: 0.38, green: 0.78, blue: 1.0, alpha: 0.92)
    private static let personalBestSectorColor = NSColor(red: 0.18, green: 0.94, blue: 0.43, alpha: 1.0)
    private static let bestLapSectorColor = NSColor(red: 0.71, green: 0.36, blue: 1.0, alpha: 1.0)
    private static let focusMarkerColor = NSColor(red: 0.38, green: 0.78, blue: 1.0, alpha: 1.0)
    private static let defaultMarkerColor = NSColor(red: 0.93, green: 0.96, blue: 0.98, alpha: 0.96)
    private static let markerBorderColor = NSColor(red: 0.03, green: 0.055, blue: 0.07, alpha: 0.90)
    private static let trackInteriorMaximumAlpha: CGFloat = 0.59
    private static let sectorBoundaryTickLength: CGFloat = 17
    private static let markerSmoothingSeconds = 0.14

    private var snapshot: LiveTelemetrySnapshot = .empty
    private var smoothedMarkerProgress: [Int: Double] = [:]
    private var lastMarkerSmoothingAt: Date?

    var fontFamily = OverlayTheme.defaultFontFamily
    var internalOpacity: Double = 0.88 {
        didSet {
            needsDisplay = true
        }
    }
    var showSectorBoundaries = true {
        didSet {
            needsDisplay = true
        }
    }

    override init(frame frameRect: NSRect = NSRect(origin: .zero, size: TrackMapOverlayDefinition.definition.defaultSize)) {
        super.init(frame: frameRect)
        wantsLayer = true
        layer?.backgroundColor = NSColor.clear.cgColor
    }

    required init?(coder: NSCoder) {
        nil
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        guard bounds.width > 32, bounds.height > 32 else {
            return
        }

        let size = max(20, min(bounds.width, bounds.height) - 40)
        let trackRect = NSRect(
            x: (bounds.width - size) / 2,
            y: (bounds.height - size) / 2,
            width: size,
            height: size
        )

        drawTrack(in: trackRect)
        drawMarkers(in: trackRect)
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        self.snapshot = snapshot
        layer?.backgroundColor = NSColor.clear.cgColor
        needsDisplay = true
    }

    func showOverlayError(_ message: String) {
        needsDisplay = true
    }

    private func drawTrack(in rect: NSRect) {
        let interior = Self.trackInteriorColor.withAlphaComponent(Self.trackInteriorMaximumAlpha * min(max(CGFloat(internalOpacity), 0.2), 1.0))
        interior.setFill()
        NSBezierPath(ovalIn: rect).fill()

        let halo = NSBezierPath(ovalIn: rect)
        halo.lineWidth = 11
        Self.trackHaloColor.setStroke()
        halo.stroke()

        let line = NSBezierPath(ovalIn: rect)
        line.lineWidth = 4.4
        Self.trackLineColor.setStroke()
        line.stroke()

        drawSectorHighlights(in: rect)
        if showSectorBoundaries {
            drawSectorBoundaries(in: rect)
        }
    }

    private func drawSectorHighlights(in rect: NSRect) {
        for sector in snapshot.models.trackMap.sectors where hasHighlight(sector.highlight) {
            sectorHighlightColor(sector.highlight).setStroke()
            for range in segmentRanges(startPct: sector.startPct, endPct: sector.endPct) {
                let startAngle = range.start * 360 - 90
                let endAngle = range.end * 360 - 90
                guard endAngle > startAngle else {
                    continue
                }

                let path = NSBezierPath()
                path.lineWidth = 5.8
                path.lineCapStyle = .round
                path.appendArc(
                    withCenter: NSPoint(x: rect.midX, y: rect.midY),
                    radius: min(rect.width, rect.height) / 2,
                    startAngle: startAngle,
                    endAngle: endAngle,
                    clockwise: false
                )
                path.stroke()
            }
        }
    }

    private func segmentRanges(startPct: Double, endPct: Double) -> [(start: Double, end: Double)] {
        let start = normalize(startPct)
        let end = endPct >= 1 ? 1 : normalize(endPct)
        if end <= start && endPct < 1 {
            return [(start, 1), (0, end)]
        }

        return [(start, min(max(end, 0), 1))]
    }

    private func hasHighlight(_ highlight: String) -> Bool {
        highlight == LiveTrackSectorHighlights.personalBest
            || highlight == LiveTrackSectorHighlights.bestLap
    }

    private func sectorHighlightColor(_ highlight: String) -> NSColor {
        highlight == LiveTrackSectorHighlights.bestLap
            ? Self.bestLapSectorColor
            : Self.personalBestSectorColor
    }

    private func drawSectorBoundaries(in rect: NSRect) {
        guard snapshot.models.trackMap.sectors.count >= 2 else {
            return
        }

        Self.sectorBoundaryColor.setStroke()
        for progress in sectorBoundaryProgresses() {
            let point = point(on: rect, progress: progress)
            let center = NSPoint(x: rect.midX, y: rect.midY)
            let dx = point.x - center.x
            let dy = point.y - center.y
            let length = max(0.001, sqrt(dx * dx + dy * dy))
            let unitX = dx / length
            let unitY = dy / length
            let half = Self.sectorBoundaryTickLength / 2
            let path = NSBezierPath()
            path.lineWidth = 2.2
            path.lineCapStyle = .round
            path.move(to: NSPoint(x: point.x - unitX * half, y: point.y - unitY * half))
            path.line(to: NSPoint(x: point.x + unitX * half, y: point.y + unitY * half))
            path.stroke()
        }
    }

    private func sectorBoundaryProgresses() -> [Double] {
        var seen: Set<Int> = []
        var progresses: [Double] = []
        for sector in snapshot.models.trackMap.sectors {
            let progress = normalize(sector.startPct)
            let key = Int((progress * 100_000).rounded())
            guard !seen.contains(key) else {
                continue
            }

            seen.insert(key)
            progresses.append(progress)
        }
        return progresses
    }

    private func drawMarkers(in rect: NSRect) {
        let markers = smoothedTrackMarkers()
        for marker in markers.sorted(by: { lhs, rhs in
            if lhs.isFocus != rhs.isFocus {
                return !lhs.isFocus && rhs.isFocus
            }

            return lhs.carIdx < rhs.carIdx
        }) {
            let point = point(on: rect, progress: marker.lapDistPct)
            let positionAttributes = marker.isFocus && marker.positionLabel != nil ? focusPositionAttributes() : nil
            let positionSize = marker.positionLabel.map { ($0 as NSString).size(withAttributes: positionAttributes) } ?? .zero
            let radius = markerRadius(for: marker, positionSize: positionSize)
            let markerRect = NSRect(x: point.x - radius, y: point.y - radius, width: radius * 2, height: radius * 2)
            let path = NSBezierPath(ovalIn: markerRect)
            marker.color.setFill()
            path.fill()
            path.lineWidth = marker.isFocus ? 2 : 1.4
            Self.markerBorderColor.setStroke()
            path.stroke()
            if let positionLabel = marker.positionLabel, let positionAttributes {
                drawFocusPositionText(positionLabel, in: markerRect, attributes: positionAttributes)
            }
        }
    }

    private func markerRadius(for marker: TrackMarker, positionSize: NSSize) -> CGFloat {
        guard marker.isFocus else {
            return 3.6
        }

        guard marker.positionLabel != nil else {
            return 5.7
        }

        return max(5.7, max(positionSize.width, positionSize.height) / 2 + 3.5)
    }

    private func focusPositionAttributes() -> [NSAttributedString.Key: Any] {
        [
            .font: OverlayTheme.font(family: fontFamily, size: 8.2, weight: .bold),
            .foregroundColor: NSColor(red: 0.02, green: 0.05, blue: 0.065, alpha: 1.0)
        ]
    }

    private func drawFocusPositionText(_ text: String, in rect: NSRect, attributes: [NSAttributedString.Key: Any]) {
        let textSize = (text as NSString).size(withAttributes: attributes)
        (text as NSString).draw(
            at: NSPoint(x: rect.midX - textSize.width / 2, y: rect.midY - textSize.height / 2),
            withAttributes: attributes
        )
    }

    private func trackMarkers() -> [TrackMarker] {
        guard let frame = snapshot.latestFrame,
              frame.teamLapDistPct.isFinite,
              frame.teamLapDistPct >= 0 else {
            return []
        }

        let localProgress = normalize(frame.teamLapDistPct)
        var markers: [TrackMarker] = [
            TrackMarker(
                carIdx: frame.focusCarIdx ?? frame.playerCarIdx ?? -1,
                lapDistPct: localProgress,
                isFocus: true,
                color: Self.focusMarkerColor,
                positionLabel: focusPositionLabel(frame)
            )
        ]

        for car in snapshot.proximity.nearbyCars {
            markers.append(TrackMarker(
                carIdx: car.carIdx,
                lapDistPct: normalize(localProgress + car.relativeLaps),
                isFocus: false,
                color: OverlayClassColor.color(car.carClassColorHex, alpha: 0.96) ?? Self.defaultMarkerColor,
                positionLabel: nil
            ))
        }

        if frame.leaderLapDistPct.isFinite, frame.leaderLapDistPct >= 0 {
            markers.append(TrackMarker(
                carIdx: 1,
                lapDistPct: normalize(frame.leaderLapDistPct),
                isFocus: false,
                color: Self.defaultMarkerColor,
                positionLabel: nil
            ))
        }

        return markers
    }

    private func smoothedTrackMarkers() -> [TrackMarker] {
        let markers = trackMarkers()
        guard !markers.isEmpty else {
            smoothedMarkerProgress.removeAll()
            lastMarkerSmoothingAt = nil
            return markers
        }

        let now = Date()
        let elapsed = lastMarkerSmoothingAt.map { min(max(now.timeIntervalSince($0), 0), 0.25) } ?? 0.05
        lastMarkerSmoothingAt = now
        let alpha = 1 - exp(-elapsed / Self.markerSmoothingSeconds)
        let active = Set(markers.map(\.carIdx))
        for carIdx in Array(smoothedMarkerProgress.keys) where !active.contains(carIdx) {
            smoothedMarkerProgress.removeValue(forKey: carIdx)
        }

        return markers.map { marker in
            guard let current = smoothedMarkerProgress[marker.carIdx] else {
                smoothedMarkerProgress[marker.carIdx] = marker.lapDistPct
                return marker
            }

            var updated = marker
            updated.lapDistPct = smoothProgress(current: current, target: marker.lapDistPct, alpha: alpha)
            smoothedMarkerProgress[marker.carIdx] = updated.lapDistPct
            return updated
        }
    }

    private func smoothProgress(current: Double, target: Double, alpha: Double) -> Double {
        var delta = target - current
        if delta > 0.5 {
            delta -= 1
        } else if delta < -0.5 {
            delta += 1
        }

        return normalize(current + delta * min(max(alpha, 0), 1))
    }

    private func focusPositionLabel(_ frame: MockLiveTelemetryFrame) -> String? {
        guard let position = frame.teamClassPosition ?? frame.teamPosition, position > 0 else {
            return nil
        }

        return "\(position)"
    }

    private func point(on rect: NSRect, progress: Double) -> NSPoint {
        let angle = normalize(progress) * Double.pi * 2 - Double.pi / 2
        return NSPoint(
            x: rect.midX + CGFloat(cos(angle)) * rect.width / 2,
            y: rect.midY + CGFloat(sin(angle)) * rect.height / 2
        )
    }

    private func normalize(_ value: Double) -> Double {
        guard value.isFinite else {
            return 0
        }

        let normalized = value.truncatingRemainder(dividingBy: 1)
        return normalized < 0 ? normalized + 1 : normalized
    }

    private struct TrackMarker {
        var carIdx: Int
        var lapDistPct: Double
        var isFocus: Bool
        var color: NSColor
        var positionLabel: String?
    }
}
