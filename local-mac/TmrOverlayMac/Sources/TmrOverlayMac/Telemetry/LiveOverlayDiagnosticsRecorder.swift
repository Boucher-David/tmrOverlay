import Foundation

final class LiveOverlayDiagnosticsRecorder {
    private static let maxEventExamplesPerSession = 80
    private static let maxEventExamplesPerKind = 8

    private let logsRoot: URL
    private let events: AppEventRecorder
    private let logger: LocalLogWriter
    private let lock = NSLock()
    private var sourceId: String?
    private var startedAtUtc: Date?
    private var outputURL: URL?
    private var frameCount = 0
    private var sampledFrameCount = 0
    private var droppedFrameSampleCount = 0
    private var droppedEventSampleCount = 0
    private var sessionFrameCounts: [String: Int] = [:]
    private var classGapSourceCounts: [String: Int] = [:]
    private var focusFrameCounts: [String: Int] = [:]
    private var gapFramesWithData = 0
    private var nonRaceGapFrames = 0
    private var largeGapFrames = 0
    private var gapJumpFrames = 0
    private var maxClassRows = 0
    private var maxClassGapSeconds: Double?
    private var previousClassGapSeconds: Double?
    private var radarFramesWithData = 0
    private var nonPlayerFocusFrames = 0
    private var localSuppressedNonPlayerFocusFrames = 0
    private var localUnavailablePitOrGarageFrames = 0
    private var sideSignalFrames = 0
    private var sideSignalWithoutPlacementFrames = 0
    private var multiclassApproachFrames = 0
    private var maxNearbyCars = 0
    private var fuelFramesWithLevel = 0
    private var fuelFramesWithBurn = 0
    private var driverControlFrames = 0
    private var lastDriversSoFar: Int?
    private var positionObservedFrames = 0
    private var classPositionChanges = 0
    private var trackMapFramesWithSectors = 0
    private var trackMapLiveTimingFrames = 0
    private var trackMapPersonalBestSectorFrames = 0
    private var trackMapBestLapSectorFrames = 0
    private var trackMapFullLapHighlightFrames = 0
    private var trackMapSectorHighlightCounts: [String: Int] = [:]
    private var previousTeamClassPosition: Int?
    private var previousTeamLapCompleted: Int?
    private var previousTeamLapDistPct: Double?
    private var lastSampledFrameAtUtc: Date?
    private var lastWrittenAtUtc: Date?
    private var sampleFrames: [MacOverlayDiagnosticsFrameSample] = []
    private var eventSamples: [MacOverlayDiagnosticsEventSample] = []
    private var eventSampleCountsByKind: [String: Int] = [:]
    private var eventSampleKeys = Set<String>()

    init(logsRoot: URL, events: AppEventRecorder, logger: LocalLogWriter) {
        self.logsRoot = logsRoot
        self.events = events
        self.logger = logger
    }

    func startCollection(sourceId: String, startedAtUtc: Date, captureDirectory: URL? = nil) {
        lock.withLock {
            self.sourceId = sourceId
            self.startedAtUtc = startedAtUtc
            self.outputURL = Self.outputURL(logsRoot: logsRoot, sourceId: sourceId, captureDirectory: captureDirectory)
            frameCount = 0
            sampledFrameCount = 0
            droppedFrameSampleCount = 0
            droppedEventSampleCount = 0
            sessionFrameCounts = [:]
            classGapSourceCounts = [:]
            focusFrameCounts = [:]
            gapFramesWithData = 0
            nonRaceGapFrames = 0
            largeGapFrames = 0
            gapJumpFrames = 0
            maxClassRows = 0
            maxClassGapSeconds = nil
            previousClassGapSeconds = nil
            radarFramesWithData = 0
            nonPlayerFocusFrames = 0
            localSuppressedNonPlayerFocusFrames = 0
            localUnavailablePitOrGarageFrames = 0
            sideSignalFrames = 0
            sideSignalWithoutPlacementFrames = 0
            multiclassApproachFrames = 0
            maxNearbyCars = 0
            fuelFramesWithLevel = 0
            fuelFramesWithBurn = 0
            driverControlFrames = 0
            lastDriversSoFar = nil
            positionObservedFrames = 0
            classPositionChanges = 0
            trackMapFramesWithSectors = 0
            trackMapLiveTimingFrames = 0
            trackMapPersonalBestSectorFrames = 0
            trackMapBestLapSectorFrames = 0
            trackMapFullLapHighlightFrames = 0
            trackMapSectorHighlightCounts = [:]
            previousTeamClassPosition = nil
            previousTeamLapCompleted = nil
            previousTeamLapDistPct = nil
            lastSampledFrameAtUtc = nil
            lastWrittenAtUtc = nil
            sampleFrames = []
            eventSamples = []
            eventSampleCountsByKind = [:]
            eventSampleKeys = []
        }
    }

    func record(_ snapshot: LiveTelemetrySnapshot) {
        lock.withLock {
            guard sourceId != nil else {
                return
            }

            let capturedAtUtc = snapshot.lastUpdatedAtUtc ?? snapshot.latestFrame?.capturedAtUtc ?? Date()
            frameCount += 1
            recordSession(snapshot)
            recordGap(snapshot, capturedAtUtc: capturedAtUtc)
            recordRadar(snapshot, capturedAtUtc: capturedAtUtc)
            recordFuel(snapshot, capturedAtUtc: capturedAtUtc)
            recordPosition(snapshot, capturedAtUtc: capturedAtUtc)
            recordTrackMap(snapshot, capturedAtUtc: capturedAtUtc)
            recordSample(snapshot, capturedAtUtc: capturedAtUtc)
            writeLiveArtifactIfDue(capturedAtUtc: capturedAtUtc)
        }
    }

    func completeCollection(finishedAtUtc: Date = Date()) {
        lock.withLock {
            guard sourceId != nil else {
                return
            }

            writeArtifact(finishedAtUtc: finishedAtUtc)
            sourceId = nil
            startedAtUtc = nil
            outputURL = nil
        }
    }

    private func recordSession(_ snapshot: LiveTelemetrySnapshot) {
        increment(&sessionFrameCounts, key: sessionKind(snapshot))
    }

    private func recordGap(_ snapshot: LiveTelemetrySnapshot, capturedAtUtc: Date) {
        let classGap = snapshot.leaderGap.classLeaderGap
        increment(&classGapSourceCounts, key: classGap.source)
        if snapshot.leaderGap.hasData {
            gapFramesWithData += 1
            if sessionKind(snapshot) != "race" {
                nonRaceGapFrames += 1
                addEvent(kind: "gap.non-race-data", detail: "gap data present during mac demo session", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
            }
        }

        maxClassRows = max(maxClassRows, snapshot.leaderGap.classCars.count)
        if let seconds = classGap.seconds, seconds.isFinite {
            maxClassGapSeconds = max(maxClassGapSeconds ?? seconds, seconds)
            if seconds >= 600 {
                largeGapFrames += 1
                addEvent(kind: "gap.large-seconds", detail: "class gap \(format(seconds))s", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
            }

            if let previousClassGapSeconds, abs(seconds - previousClassGapSeconds) >= 300 {
                gapJumpFrames += 1
                addEvent(kind: "gap.large-jump", detail: "class gap changed from \(format(previousClassGapSeconds))s to \(format(seconds))s", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
            }

            previousClassGapSeconds = seconds
        }
    }

    private func recordRadar(_ snapshot: LiveTelemetrySnapshot, capturedAtUtc: Date) {
        let focusKind = focusKind(snapshot)
        increment(&focusFrameCounts, key: focusKind)
        if focusKind == "non-player" {
            nonPlayerFocusFrames += 1
            localSuppressedNonPlayerFocusFrames += 1
            addEvent(kind: "radar.local-suppressed-non-player-focus", detail: "local-only radar hidden while camera focus is another car", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
        }

        if let frame = snapshot.latestFrame,
           !frame.isOnTrack || frame.isInGarage || frame.onPitRoad {
            localUnavailablePitOrGarageFrames += 1
            addEvent(kind: "radar.local-unavailable-pit-or-garage", detail: "local-only radar unavailable while local car is off track, in garage, or in pit context", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
        }

        if snapshot.proximity.hasData {
            radarFramesWithData += 1
        }

        if snapshot.proximity.carLeftRight != nil {
            sideSignalFrames += 1
        }

        if !snapshot.proximity.multiclassApproaches.isEmpty {
            multiclassApproachFrames += 1
        }

        maxNearbyCars = max(maxNearbyCars, snapshot.proximity.nearbyCars.count)
        if (snapshot.proximity.hasCarLeft || snapshot.proximity.hasCarRight) && !hasSidePlacementCandidate(snapshot) {
            sideSignalWithoutPlacementFrames += 1
            addEvent(kind: "radar.side-without-placement", detail: "side occupancy has no close placement candidate", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
        }
    }

    private func recordFuel(_ snapshot: LiveTelemetrySnapshot, capturedAtUtc: Date) {
        if snapshot.fuel.hasValidFuel {
            fuelFramesWithLevel += 1
        }

        if let burn = snapshot.fuel.fuelUsePerHourLiters, burn.isFinite, burn > 0 {
            fuelFramesWithBurn += 1
        }

        if let driversSoFar = snapshot.latestFrame?.driversSoFar {
            driverControlFrames += 1
            if let lastDriversSoFar, lastDriversSoFar != driversSoFar {
                addEvent(kind: "fuel.driver-control-change", detail: "driversSoFar changed from \(lastDriversSoFar) to \(driversSoFar)", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
            }

            lastDriversSoFar = driversSoFar
        }
    }

    private func recordPosition(_ snapshot: LiveTelemetrySnapshot, capturedAtUtc: Date) {
        guard let frame = snapshot.latestFrame else {
            return
        }

        positionObservedFrames += 1
        if let previousTeamClassPosition,
           let current = frame.teamClassPosition,
           previousTeamClassPosition != current,
           previousTeamLapCompleted == frame.teamLapCompleted,
           let previousTeamLapDistPct,
           abs(frame.teamLapDistPct - previousTeamLapDistPct) > 0.00001,
           abs(frame.teamLapDistPct - previousTeamLapDistPct) < 0.5 {
            classPositionChanges += 1
            addEvent(kind: "position.class-intra-lap", detail: "team class P\(previousTeamClassPosition) -> P\(current)", snapshot: snapshot, capturedAtUtc: capturedAtUtc)
        }

        previousTeamClassPosition = frame.teamClassPosition
        previousTeamLapCompleted = frame.teamLapCompleted
        previousTeamLapDistPct = frame.teamLapDistPct
    }

    private func recordTrackMap(_ snapshot: LiveTelemetrySnapshot, capturedAtUtc: Date) {
        let trackMap = snapshot.models.trackMap
        guard trackMap.hasSectors else {
            return
        }

        trackMapFramesWithSectors += 1
        if trackMap.hasLiveTiming {
            trackMapLiveTimingFrames += 1
        }

        let highlighted = trackMap.sectors.filter { $0.highlight != LiveTrackSectorHighlights.none }
        guard !highlighted.isEmpty else {
            return
        }

        for sector in highlighted {
            increment(&trackMapSectorHighlightCounts, key: sector.highlight)
        }

        if highlighted.contains(where: { $0.highlight == LiveTrackSectorHighlights.personalBest }) {
            trackMapPersonalBestSectorFrames += 1
        }

        if highlighted.contains(where: { $0.highlight == LiveTrackSectorHighlights.bestLap }) {
            trackMapBestLapSectorFrames += 1
        }

        let highlightedKinds = Set(highlighted.map(\.highlight))
        if highlighted.count == trackMap.sectors.count,
           highlightedKinds.count == 1,
           let highlight = highlightedKinds.first {
            trackMapFullLapHighlightFrames += 1
            addEvent(
                kind: "track-map.full-lap-highlight",
                detail: "full lap \(highlight)",
                snapshot: snapshot,
                capturedAtUtc: capturedAtUtc
            )
        }
    }

    private func recordSample(_ snapshot: LiveTelemetrySnapshot, capturedAtUtc: Date) {
        if let lastSampledFrameAtUtc,
           capturedAtUtc.timeIntervalSince(lastSampledFrameAtUtc) < 1 {
            return
        }

        guard sampleFrames.count < 240 else {
            droppedFrameSampleCount += 1
            return
        }

        sampleFrames.append(MacOverlayDiagnosticsFrameSample(
            capturedAtUtc: capturedAtUtc,
            sequence: snapshot.sequence,
            sessionTimeSeconds: rounded(snapshot.latestFrame?.sessionTime),
            sessionKind: sessionKind(snapshot),
            focusKind: focusKind(snapshot),
            classGapSeconds: rounded(snapshot.leaderGap.classLeaderGap.seconds),
            classGapLaps: rounded(snapshot.leaderGap.classLeaderGap.laps),
            classGapSource: snapshot.leaderGap.classLeaderGap.source,
            classRowCount: snapshot.leaderGap.classCars.count,
            nearbyCarCount: snapshot.proximity.nearbyCars.count,
            hasSideSignal: snapshot.proximity.carLeftRight != nil,
            hasFuelLevel: snapshot.fuel.hasValidFuel,
            hasFuelBurn: (snapshot.fuel.fuelUsePerHourLiters ?? 0) > 0,
            trackMapHighlightedSectorCount: snapshot.models.trackMap.sectors.filter { $0.highlight != LiveTrackSectorHighlights.none }.count
        ))
        sampledFrameCount += 1
        lastSampledFrameAtUtc = capturedAtUtc
    }

    private func addEvent(kind: String, detail: String, snapshot: LiveTelemetrySnapshot, capturedAtUtc: Date) {
        let normalizedKind = kind.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? "unknown"
            : kind.trimmingCharacters(in: .whitespacesAndNewlines)
        let normalizedDetail = detail.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? normalizedKind
            : detail.trimmingCharacters(in: .whitespacesAndNewlines)
        let sessionKind = sessionKind(snapshot)
        let eventKey = [
            normalizedKind,
            normalizedDetail,
            sessionKind,
            snapshot.latestFrame?.teamDriverKey ?? ""
        ].joined(separator: "\u{001f}")

        guard !eventSampleKeys.contains(eventKey) else {
            droppedEventSampleCount += 1
            return
        }

        guard eventSamples.count < Self.maxEventExamplesPerSession else {
            droppedEventSampleCount += 1
            return
        }

        let kindCount = eventSampleCountsByKind[normalizedKind, default: 0]
        guard kindCount < Self.maxEventExamplesPerKind else {
            droppedEventSampleCount += 1
            return
        }

        eventSampleKeys.insert(eventKey)
        eventSampleCountsByKind[normalizedKind] = kindCount + 1
        eventSamples.append(MacOverlayDiagnosticsEventSample(
            kind: normalizedKind,
            detail: normalizedDetail,
            capturedAtUtc: capturedAtUtc,
            sequence: snapshot.sequence,
            sessionTimeSeconds: rounded(snapshot.latestFrame?.sessionTime),
            sessionKind: sessionKind,
            focusKind: focusKind(snapshot),
            rawCarLeftRight: snapshot.latestFrame?.carLeftRight,
            rawNearbyCarCount: snapshot.proximity.nearbyCars.count,
            hasRadarData: snapshot.proximity.hasData,
            hasSideSignal: snapshot.proximity.carLeftRight != nil,
            classGapSeconds: rounded(snapshot.leaderGap.classLeaderGap.seconds),
            nearbyCarCount: snapshot.proximity.nearbyCars.count
        ))
    }

    private func writeLiveArtifactIfDue(capturedAtUtc: Date) {
        if let lastWrittenAtUtc,
           capturedAtUtc.timeIntervalSince(lastWrittenAtUtc) < 5 {
            return
        }

        writeArtifact(finishedAtUtc: nil)
        lastWrittenAtUtc = capturedAtUtc
    }

    private func writeArtifact(finishedAtUtc: Date?) {
        guard let sourceId, let startedAtUtc, let outputURL else {
            return
        }

        let artifact = MacOverlayDiagnosticsArtifact(
            formatVersion: 1,
            sourceId: sourceId,
            startedAtUtc: startedAtUtc,
            finishedAtUtc: finishedAtUtc,
            totals: MacOverlayDiagnosticsTotals(
                frameCount: frameCount,
                sampledFrameCount: sampledFrameCount,
                droppedFrameSampleCount: droppedFrameSampleCount,
                droppedEventSampleCount: droppedEventSampleCount,
                sessionFrameCounts: sessionFrameCounts.sortedDictionary()
            ),
            gap: MacGapDiagnosticsSummary(
                framesWithData: gapFramesWithData,
                nonRaceFramesWithData: nonRaceGapFrames,
                classLargeGapFrames: largeGapFrames,
                classJumpFrames: gapJumpFrames,
                maxClassRows: maxClassRows,
                maxClassGapSeconds: rounded(maxClassGapSeconds),
                classGapSourceCounts: classGapSourceCounts.sortedDictionary()
            ),
            radar: MacRadarDiagnosticsSummary(
                framesWithData: radarFramesWithData,
                nonPlayerFocusFrames: nonPlayerFocusFrames,
                localSuppressedNonPlayerFocusFrames: localSuppressedNonPlayerFocusFrames,
                localUnavailablePitOrGarageFrames: localUnavailablePitOrGarageFrames,
                sideSignalFrames: sideSignalFrames,
                sideSignalWithoutPlacementFrames: sideSignalWithoutPlacementFrames,
                multiclassApproachFrames: multiclassApproachFrames,
                maxNearbyCars: maxNearbyCars,
                focusFrameCounts: focusFrameCounts.sortedDictionary()
            ),
            fuel: MacFuelDiagnosticsSummary(
                framesWithFuelLevel: fuelFramesWithLevel,
                framesWithBurn: fuelFramesWithBurn,
                driverControlFrames: driverControlFrames
            ),
            positionCadence: MacPositionCadenceDiagnosticsSummary(
                observedFrames: positionObservedFrames,
                intraLapClassPositionChanges: classPositionChanges
            ),
            trackMap: MacTrackMapDiagnosticsSummary(
                framesWithSectors: trackMapFramesWithSectors,
                liveTimingFrames: trackMapLiveTimingFrames,
                personalBestSectorFrames: trackMapPersonalBestSectorFrames,
                bestLapSectorFrames: trackMapBestLapSectorFrames,
                fullLapHighlightFrames: trackMapFullLapHighlightFrames,
                sectorHighlightCounts: trackMapSectorHighlightCounts.sortedDictionary()
            ),
            sampleFrames: sampleFrames,
            eventSamples: eventSamples
        )

        do {
            try FileManager.default.createDirectory(at: outputURL.deletingLastPathComponent(), withIntermediateDirectories: true)
            let encoder = JSONEncoder()
            encoder.dateEncodingStrategy = .iso8601
            encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            try encoder.encode(artifact).write(to: outputURL)
        } catch {
            logger.warning("Failed to write mac live overlay diagnostics: \(error).")
        }
    }

    private func hasSidePlacementCandidate(_ snapshot: LiveTelemetrySnapshot) -> Bool {
        let sideWindowSeconds = max(snapshot.proximity.sideOverlapWindowSeconds, 0.18)
        return snapshot.proximity.nearbyCars.contains { car in
            if let seconds = car.relativeSeconds {
                return abs(seconds) <= sideWindowSeconds
            }

            if let meters = car.relativeMeters {
                return abs(meters) <= 7.5
            }

            return false
        }
    }

    private func sessionKind(_ snapshot: LiveTelemetrySnapshot) -> String {
        snapshot.combo.sessionKey == "race" ? "race" : snapshot.combo.sessionKey
    }

    private func focusKind(_ snapshot: LiveTelemetrySnapshot) -> String {
        guard let frame = snapshot.latestFrame,
              let focusCarIdx = frame.focusCarIdx
        else {
            return "unknown"
        }

        return frame.playerCarIdx == focusCarIdx ? "player-or-team" : "non-player"
    }

    private func increment(_ values: inout [String: Int], key: String) {
        values[key, default: 0] += 1
    }

    private func rounded(_ value: Double?) -> Double? {
        guard let value, value.isFinite else {
            return nil
        }

        return (value * 1_000_000).rounded() / 1_000_000
    }

    private func format(_ value: Double) -> String {
        String(format: "%.3f", value)
    }

    private static func outputURL(logsRoot: URL, sourceId: String, captureDirectory: URL?) -> URL {
        if let captureDirectory {
            return captureDirectory.appendingPathComponent("live-overlay-diagnostics.json")
        }

        return logsRoot
            .appendingPathComponent("overlay-diagnostics", isDirectory: true)
            .appendingPathComponent("\(safeFileName(sourceId))-live-overlay-diagnostics.json")
    }

    private static func safeFileName(_ value: String) -> String {
        let invalid = CharacterSet(charactersIn: "/\\?%*|\"<>:")
        return value
            .unicodeScalars
            .map { invalid.contains($0) ? "-" : String($0) }
            .joined()
    }
}

private struct MacOverlayDiagnosticsArtifact: Encodable {
    var formatVersion: Int
    var sourceId: String
    var startedAtUtc: Date
    var finishedAtUtc: Date?
    var totals: MacOverlayDiagnosticsTotals
    var gap: MacGapDiagnosticsSummary
    var radar: MacRadarDiagnosticsSummary
    var fuel: MacFuelDiagnosticsSummary
    var positionCadence: MacPositionCadenceDiagnosticsSummary
    var trackMap: MacTrackMapDiagnosticsSummary
    var sampleFrames: [MacOverlayDiagnosticsFrameSample]
    var eventSamples: [MacOverlayDiagnosticsEventSample]
}

private struct MacOverlayDiagnosticsTotals: Encodable {
    var frameCount: Int
    var sampledFrameCount: Int
    var droppedFrameSampleCount: Int
    var droppedEventSampleCount: Int
    var sessionFrameCounts: [String: Int]
}

private struct MacGapDiagnosticsSummary: Encodable {
    var framesWithData: Int
    var nonRaceFramesWithData: Int
    var classLargeGapFrames: Int
    var classJumpFrames: Int
    var maxClassRows: Int
    var maxClassGapSeconds: Double?
    var classGapSourceCounts: [String: Int]
}

private struct MacRadarDiagnosticsSummary: Encodable {
    var framesWithData: Int
    var nonPlayerFocusFrames: Int
    var localSuppressedNonPlayerFocusFrames: Int
    var localUnavailablePitOrGarageFrames: Int
    var sideSignalFrames: Int
    var sideSignalWithoutPlacementFrames: Int
    var multiclassApproachFrames: Int
    var maxNearbyCars: Int
    var focusFrameCounts: [String: Int]
}

private struct MacFuelDiagnosticsSummary: Encodable {
    var framesWithFuelLevel: Int
    var framesWithBurn: Int
    var driverControlFrames: Int
}

private struct MacPositionCadenceDiagnosticsSummary: Encodable {
    var observedFrames: Int
    var intraLapClassPositionChanges: Int
}

private struct MacTrackMapDiagnosticsSummary: Encodable {
    var framesWithSectors: Int
    var liveTimingFrames: Int
    var personalBestSectorFrames: Int
    var bestLapSectorFrames: Int
    var fullLapHighlightFrames: Int
    var sectorHighlightCounts: [String: Int]
}

private struct MacOverlayDiagnosticsFrameSample: Encodable {
    var capturedAtUtc: Date
    var sequence: Int
    var sessionTimeSeconds: Double?
    var sessionKind: String
    var focusKind: String
    var classGapSeconds: Double?
    var classGapLaps: Double?
    var classGapSource: String
    var classRowCount: Int
    var nearbyCarCount: Int
    var hasSideSignal: Bool
    var hasFuelLevel: Bool
    var hasFuelBurn: Bool
    var trackMapHighlightedSectorCount: Int
}

private struct MacOverlayDiagnosticsEventSample: Encodable {
    var kind: String
    var detail: String
    var capturedAtUtc: Date
    var sequence: Int
    var sessionTimeSeconds: Double?
    var sessionKind: String
    var focusKind: String
    var rawCarLeftRight: Int?
    var rawNearbyCarCount: Int
    var hasRadarData: Bool
    var hasSideSignal: Bool
    var classGapSeconds: Double?
    var nearbyCarCount: Int
}

private extension Dictionary where Key == String, Value == Int {
    func sortedDictionary() -> [String: Int] {
        Dictionary(uniqueKeysWithValues: sorted { $0.key < $1.key })
    }
}
