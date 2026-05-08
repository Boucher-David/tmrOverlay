import AppKit

private struct RadarCaptureDemoColorVariant {
    let title: String
    let colorMode: CarRadarColorMode

    static let classColors = RadarCaptureDemoColorVariant(
        title: "Class colors",
        colorMode: .classColorOnly
    )

    static let neutralProximity = RadarCaptureDemoColorVariant(
        title: "Neutral fade",
        colorMode: .neutralProximity
    )

    static let classColorToAlertRed = RadarCaptureDemoColorVariant(
        title: "Class to red",
        colorMode: .classColorToAlertRed
    )
}

struct GapToLeaderDemoPitStop {
    let carIdx: Int
    let entrySessionTime: TimeInterval
    let exitSessionTime: TimeInterval
    let lossSeconds: TimeInterval

    init(
        carIdx: Int,
        entrySessionTime: TimeInterval,
        laneTimeSeconds: TimeInterval,
        lossSeconds: TimeInterval
    ) {
        self.carIdx = carIdx
        self.entrySessionTime = entrySessionTime
        self.exitSessionTime = entrySessionTime + laneTimeSeconds
        self.lossSeconds = lossSeconds
    }

    func loss(at sessionTime: TimeInterval) -> TimeInterval {
        guard sessionTime >= entrySessionTime else {
            return 0
        }

        guard sessionTime < exitSessionTime else {
            return lossSeconds
        }

        let duration = max(1, exitSessionTime - entrySessionTime)
        let progress = min(max((sessionTime - entrySessionTime) / duration, 0), 1)
        return progress * lossSeconds
    }
}

struct GapToLeaderDemoScenario {
    static let defaultLoopDurationSeconds: TimeInterval = 10 * 60
    static let focusedRollingLoopDurationSeconds: TimeInterval = 90 * 60
    static let focusedRollingWindowSeconds: TimeInterval = 10 * 60
    static let focusedGraphRefreshIntervalSeconds: TimeInterval = 0.75
    static let playbackSpeed = 20.0

    let title: String
    let startSessionTime: TimeInterval
    let focusCarIdx: Int?
    let lapTimeSeconds: TimeInterval?
    let loopDurationSeconds: TimeInterval
    let visibleTrendWindowSeconds: TimeInterval?
    let graphRefreshIntervalSeconds: TimeInterval
    let threatCarIdx: Int?
    let threatStartDeltaSeconds: TimeInterval
    let threatMinimumDeltaSeconds: TimeInterval
    let threatGainPerMinuteSeconds: TimeInterval
    let pitStops: [GapToLeaderDemoPitStop]

    init(
        title: String,
        startSessionTime: TimeInterval,
        focusCarIdx: Int? = nil,
        lapTimeSeconds: TimeInterval? = nil,
        loopDurationSeconds: TimeInterval = GapToLeaderDemoScenario.defaultLoopDurationSeconds,
        visibleTrendWindowSeconds: TimeInterval? = nil,
        graphRefreshIntervalSeconds: TimeInterval = 0,
        threatCarIdx: Int? = nil,
        threatStartDeltaSeconds: TimeInterval = 0,
        threatMinimumDeltaSeconds: TimeInterval = 0,
        threatGainPerMinuteSeconds: TimeInterval = 0,
        pitStops: [GapToLeaderDemoPitStop] = []
    ) {
        self.title = title
        self.startSessionTime = startSessionTime
        self.focusCarIdx = focusCarIdx
        self.lapTimeSeconds = lapTimeSeconds
        self.loopDurationSeconds = loopDurationSeconds
        self.visibleTrendWindowSeconds = visibleTrendWindowSeconds
        self.graphRefreshIntervalSeconds = graphRefreshIntervalSeconds
        self.threatCarIdx = threatCarIdx
        self.threatStartDeltaSeconds = threatStartDeltaSeconds
        self.threatMinimumDeltaSeconds = threatMinimumDeltaSeconds
        self.threatGainPerMinuteSeconds = threatGainPerMinuteSeconds
        self.pitStops = pitStops
    }

    static func focusedRolling(
        title: String,
        startSessionTime: TimeInterval,
        focusCarIdx: Int? = nil,
        lapTimeSeconds: TimeInterval? = nil,
        graphRefreshIntervalSeconds: TimeInterval = focusedGraphRefreshIntervalSeconds,
        threatCarIdx: Int? = nil,
        threatStartDeltaSeconds: TimeInterval = 0,
        threatMinimumDeltaSeconds: TimeInterval = 0,
        threatGainPerMinuteSeconds: TimeInterval = 0,
        pitStops: [GapToLeaderDemoPitStop] = []
    ) -> GapToLeaderDemoScenario {
        GapToLeaderDemoScenario(
            title: title,
            startSessionTime: startSessionTime,
            focusCarIdx: focusCarIdx,
            lapTimeSeconds: lapTimeSeconds,
            loopDurationSeconds: focusedRollingLoopDurationSeconds,
            visibleTrendWindowSeconds: focusedRollingWindowSeconds,
            graphRefreshIntervalSeconds: graphRefreshIntervalSeconds,
            threatCarIdx: threatCarIdx,
            threatStartDeltaSeconds: threatStartDeltaSeconds,
            threatMinimumDeltaSeconds: threatMinimumDeltaSeconds,
            threatGainPerMinuteSeconds: threatGainPerMinuteSeconds,
            pitStops: pitStops
        )
    }

    static let graph2PitWaveStops: [GapToLeaderDemoPitStop] = [
        GapToLeaderDemoPitStop(carIdx: FourHourRacePreview.classLeaderCarIdx, entrySessionTime: 3_560, laneTimeSeconds: 65, lossSeconds: 63.0),
        GapToLeaderDemoPitStop(carIdx: 102, entrySessionTime: 3_590, laneTimeSeconds: 64, lossSeconds: 63.8),
        GapToLeaderDemoPitStop(carIdx: 103, entrySessionTime: 3_620, laneTimeSeconds: 64, lossSeconds: 62.9),
        GapToLeaderDemoPitStop(carIdx: 104, entrySessionTime: 3_655, laneTimeSeconds: 66, lossSeconds: 64.4),
        GapToLeaderDemoPitStop(carIdx: 105, entrySessionTime: 3_690, laneTimeSeconds: 63, lossSeconds: 63.5),
        GapToLeaderDemoPitStop(carIdx: 107, entrySessionTime: 3_740, laneTimeSeconds: 64, lossSeconds: 62.6),
        GapToLeaderDemoPitStop(carIdx: 109, entrySessionTime: 3_805, laneTimeSeconds: 65, lossSeconds: 64.1),
        GapToLeaderDemoPitStop(carIdx: 110, entrySessionTime: 3_875, laneTimeSeconds: 65, lossSeconds: 63.4),
        GapToLeaderDemoPitStop(carIdx: 111, entrySessionTime: 3_955, laneTimeSeconds: 66, lossSeconds: 64.0)
    ]

    static let previewExamples: [GapToLeaderDemoScenario] = [
        focusedRolling(
            title: "Rolling 10m (0.5s)",
            startSessionTime: FourHourRacePreview.mockStartRaceSeconds + 20,
            graphRefreshIntervalSeconds: 0.5,
            threatCarIdx: 107,
            threatStartDeltaSeconds: 72,
            threatMinimumDeltaSeconds: 10,
            threatGainPerMinuteSeconds: 1.4
        ),
        focusedRolling(
            title: "Pit Wave, Weather, Handoff (1s)",
            startSessionTime: 3_460,
            graphRefreshIntervalSeconds: 1.0,
            threatCarIdx: 108,
            threatStartDeltaSeconds: 56,
            threatMinimumDeltaSeconds: 8,
            threatGainPerMinuteSeconds: 1.8,
            pitStops: graph2PitWaveStops
        ),
        GapToLeaderDemoScenario(
            title: "Mid-Race Spread",
            startSessionTime: 7_200
        ),
        focusedRolling(
            title: "Final-Hour Focus (2s)",
            startSessionTime: 10_800,
            focusCarIdx: 109,
            graphRefreshIntervalSeconds: 2.0,
            threatCarIdx: 110,
            threatStartDeltaSeconds: 62,
            threatMinimumDeltaSeconds: 12,
            threatGainPerMinuteSeconds: 1.2
        ),
        focusedRolling(
            title: "90s Lap (1s)",
            startSessionTime: 1_200,
            lapTimeSeconds: 90,
            graphRefreshIntervalSeconds: 1.0,
            threatCarIdx: 107,
            threatStartDeltaSeconds: 26,
            threatMinimumDeltaSeconds: 3,
            threatGainPerMinuteSeconds: 1.7
        ),
        focusedRolling(
            title: "30s Lap (0.5s)",
            startSessionTime: 1_200,
            lapTimeSeconds: 30,
            graphRefreshIntervalSeconds: 0.5,
            threatCarIdx: 108,
            threatStartDeltaSeconds: 11,
            threatMinimumDeltaSeconds: 1.5,
            threatGainPerMinuteSeconds: 0.9
        )
    ]
}

private final class GapToLeaderDemoPlayback {
    let scenario: GapToLeaderDemoScenario
    let view: GapToLeaderView
    let startedAtUtc: Date
    let onSnapshot: ((LiveTelemetrySnapshot) -> Void)?
    var store = LiveTelemetryStore()
    var lastLoopIndex = -1
    var sequenceBase: Int

    init(
        scenario: GapToLeaderDemoScenario,
        view: GapToLeaderView,
        startedAtUtc: Date,
        sequenceBase: Int,
        onSnapshot: ((LiveTelemetrySnapshot) -> Void)? = nil
    ) {
        self.scenario = scenario
        self.view = view
        self.startedAtUtc = startedAtUtc
        self.sequenceBase = sequenceBase
        self.onSnapshot = onSnapshot
        resetStore(startedAtUtc: startedAtUtc)
    }

    func update(capturedAtUtc: Date) {
        let elapsed = max(0, capturedAtUtc.timeIntervalSince(startedAtUtc))
        let playbackSeconds = elapsed * GapToLeaderDemoScenario.playbackSpeed
        let loopDurationSeconds = max(1, scenario.loopDurationSeconds)
        let loopIndex = Int(playbackSeconds / loopDurationSeconds)
        if loopIndex != lastLoopIndex {
            lastLoopIndex = loopIndex
            sequenceBase += 1_000
            view.resetTrend()
            resetStore(startedAtUtc: capturedAtUtc)
        }

        let loopOffset = playbackSeconds.truncatingRemainder(dividingBy: loopDurationSeconds)
        let positiveOffset = loopOffset >= 0 ? loopOffset : loopOffset + loopDurationSeconds
        let sessionTime = min(
            FourHourRacePreview.sessionLengthSeconds,
            scenario.startSessionTime + positiveOffset
        )
        var frame = MockLiveTelemetryFrame.mock(
            capturedAtUtc: capturedAtUtc,
            sessionTime: sessionTime,
            fuelLevelLiters: FourHourRacePreview.fuelLevelLiters(sessionTime: sessionTime),
            fuelUsePerHourLiters: FourHourRacePreview.fuelUsePerHourLiters
        )
        applyLapTimeOverride(to: &frame)
        store.recordFrame(frame)
        var snapshot = lapAdjustedSnapshot(store.snapshot())
        snapshot = pitWaveAdjustedSnapshot(snapshot, sessionTime: sessionTime)
        snapshot = focusedSnapshot(snapshot)
        snapshot = threatAdjustedSnapshot(snapshot, loopOffset: positiveOffset)
        view.update(with: snapshot)
        onSnapshot?(snapshot)
    }

    private func applyLapTimeOverride(to frame: inout MockLiveTelemetryFrame) {
        guard let lapTimeSeconds = scenario.lapTimeSeconds,
              lapTimeSeconds.isFinite,
              lapTimeSeconds > 0 else {
            return
        }

        let teamProgress = max(0, frame.sessionTime / lapTimeSeconds)
        frame.estimatedLapSeconds = lapTimeSeconds
        frame.teamLapCompleted = Int(teamProgress.rounded(.down))
        frame.teamLapDistPct = teamProgress.truncatingRemainder(dividingBy: 1)
        frame.leaderLapCompleted = frame.teamLapCompleted
        frame.leaderLapDistPct = frame.teamLapDistPct
    }

    private func lapAdjustedSnapshot(_ snapshot: LiveTelemetrySnapshot) -> LiveTelemetrySnapshot {
        guard let lapTimeSeconds = scenario.lapTimeSeconds,
              lapTimeSeconds.isFinite,
              lapTimeSeconds > 0 else {
            return snapshot
        }

        var output = snapshot
        var leaderGap = snapshot.leaderGap
        let scale = lapTimeSeconds / FourHourRacePreview.medianLapSeconds
        leaderGap.overallLeaderGap = scaledGap(leaderGap.overallLeaderGap, scale: scale)
        leaderGap.classLeaderGap = scaledGap(leaderGap.classLeaderGap, scale: scale)
        leaderGap.classCars = leaderGap.classCars.map { car in
            var outputCar = car
            outputCar.gapSecondsToClassLeader = car.gapSecondsToClassLeader.map { $0 * scale }
            outputCar.deltaSecondsToReference = car.deltaSecondsToReference.map { $0 * scale }
            return outputCar
        }
        output.leaderGap = leaderGap
        return output
    }

    private func scaledGap(_ gap: LiveGapValue, scale: Double) -> LiveGapValue {
        var output = gap
        output.seconds = gap.seconds.map { $0 * scale }
        return output
    }

    private func pitWaveAdjustedSnapshot(_ snapshot: LiveTelemetrySnapshot, sessionTime: TimeInterval) -> LiveTelemetrySnapshot {
        guard !scenario.pitStops.isEmpty else {
            return snapshot
        }

        let lossesByCarIdx = Dictionary(grouping: scenario.pitStops, by: { $0.carIdx })
            .mapValues { stops in
                stops.reduce(0) { total, stop in
                    total + stop.loss(at: sessionTime)
                }
            }
        guard lossesByCarIdx.values.contains(where: { $0 > 0.05 }) else {
            return snapshot
        }

        let cars = snapshot.leaderGap.classCars.map { car in
            var adjusted = car
            let loss = lossesByCarIdx[car.carIdx] ?? 0
            guard loss > 0,
                  let gapSeconds = demoAbsoluteGapSeconds(car) else {
                return adjusted
            }

            adjusted.gapSecondsToClassLeader = gapSeconds + loss
            adjusted.gapLapsToClassLeader = nil
            return adjusted
        }

        return normalizedClassSnapshot(snapshot, cars: cars, source: "demo pit wave")
    }

    private func normalizedClassSnapshot(
        _ snapshot: LiveTelemetrySnapshot,
        cars draftCars: [LiveClassGapCar],
        source: String
    ) -> LiveTelemetrySnapshot {
        guard let minimumGap = draftCars.compactMap(demoAbsoluteGapSeconds).min() else {
            return snapshot
        }

        var cars = draftCars.map { car in
            var adjusted = car
            if let gap = demoAbsoluteGapSeconds(car) {
                adjusted.gapSecondsToClassLeader = max(0, gap - minimumGap)
                adjusted.gapLapsToClassLeader = nil
            }
            return adjusted
        }

        cars.sort {
            let left = $0.gapSecondsToClassLeader ?? Double.greatestFiniteMagnitude
            let right = $1.gapSecondsToClassLeader ?? Double.greatestFiniteMagnitude
            if left == right {
                return $0.carIdx < $1.carIdx
            }
            return left < right
        }

        let classLeaderCarIdx = cars.first?.carIdx
        for index in cars.indices {
            cars[index].classPosition = index + 1
            cars[index].isClassLeader = cars[index].carIdx == classLeaderCarIdx
            cars[index].gapLapsToClassLeader = cars[index].isClassLeader ? 0 : nil
        }

        let referenceGap = cars.first(where: { $0.isReferenceCar })?.gapSecondsToClassLeader ?? 0
        for index in cars.indices {
            cars[index].deltaSecondsToReference = cars[index].gapSecondsToClassLeader.map { $0 - referenceGap }
        }

        var output = snapshot
        var leaderGap = snapshot.leaderGap
        let reference = cars.first(where: { $0.isReferenceCar })
        leaderGap.hasData = true
        leaderGap.classLeaderCarIdx = classLeaderCarIdx
        leaderGap.referenceClassPosition = reference?.classPosition
        leaderGap.classLeaderGap = LiveGapValue(
            hasData: reference != nil,
            isLeader: reference?.isClassLeader ?? false,
            seconds: reference?.gapSecondsToClassLeader,
            laps: reference?.isClassLeader == true ? 0 : nil,
            source: source
        )
        leaderGap.classCars = cars
        output.leaderGap = leaderGap
        return output
    }

    private func demoAbsoluteGapSeconds(_ car: LiveClassGapCar) -> Double? {
        car.gapSecondsToClassLeader
            ?? car.gapLapsToClassLeader.map { $0 * (scenario.lapTimeSeconds ?? FourHourRacePreview.medianLapSeconds) }
    }

    private func focusedSnapshot(_ snapshot: LiveTelemetrySnapshot) -> LiveTelemetrySnapshot {
        guard let focusCarIdx = scenario.focusCarIdx,
              let target = snapshot.leaderGap.classCars.first(where: { $0.carIdx == focusCarIdx }),
              let targetGap = target.gapSecondsToClassLeader else {
            return snapshot
        }

        var output = snapshot
        var leaderGap = snapshot.leaderGap
        leaderGap.referenceOverallPosition = target.classPosition
        leaderGap.referenceClassPosition = target.classPosition
        leaderGap.classLeaderGap = LiveGapValue(
            hasData: true,
            isLeader: target.isClassLeader,
            seconds: target.isClassLeader ? 0 : targetGap,
            laps: target.isClassLeader ? 0 : nil,
            source: "demo focus"
        )
        leaderGap.classCars = snapshot.leaderGap.classCars.map { car in
            var focusedCar = car
            focusedCar.isReferenceCar = car.carIdx == focusCarIdx
            focusedCar.deltaSecondsToReference = car.gapSecondsToClassLeader.map { $0 - targetGap }
            return focusedCar
        }
        output.leaderGap = leaderGap
        return output
    }

    private func threatAdjustedSnapshot(_ snapshot: LiveTelemetrySnapshot, loopOffset: TimeInterval) -> LiveTelemetrySnapshot {
        guard let threatCarIdx = scenario.threatCarIdx,
              scenario.threatStartDeltaSeconds > 0,
              scenario.threatMinimumDeltaSeconds > 0,
              scenario.threatGainPerMinuteSeconds > 0,
              let reference = snapshot.leaderGap.classCars.first(where: { $0.isReferenceCar }),
              let referenceGap = reference.gapSecondsToClassLeader,
              snapshot.leaderGap.classCars.contains(where: { $0.carIdx == threatCarIdx }) else {
            return snapshot
        }

        var output = snapshot
        var leaderGap = snapshot.leaderGap
        let gainedSeconds = loopOffset / 60 * scenario.threatGainPerMinuteSeconds
        let deltaToReference = max(
            scenario.threatMinimumDeltaSeconds,
            scenario.threatStartDeltaSeconds - gainedSeconds
        )
        let threatGap = referenceGap + deltaToReference

        var cars = leaderGap.classCars.map { car in
            var adjusted = car
            if car.carIdx == threatCarIdx {
                adjusted.gapSecondsToClassLeader = threatGap
                adjusted.gapLapsToClassLeader = nil
            }
            return adjusted
        }

        cars.sort {
            let left = $0.gapSecondsToClassLeader ?? Double.greatestFiniteMagnitude
            let right = $1.gapSecondsToClassLeader ?? Double.greatestFiniteMagnitude
            if left == right {
                return $0.carIdx < $1.carIdx
            }
            return left < right
        }

        let classLeaderCarIdx = cars.first?.carIdx
        let resolvedReferenceGap = cars.first(where: { $0.isReferenceCar })?.gapSecondsToClassLeader ?? referenceGap
        for index in cars.indices {
            cars[index].classPosition = index + 1
            cars[index].isClassLeader = cars[index].carIdx == classLeaderCarIdx
            cars[index].deltaSecondsToReference = cars[index].gapSecondsToClassLeader.map { $0 - resolvedReferenceGap }
        }

        leaderGap.classLeaderCarIdx = classLeaderCarIdx
        leaderGap.referenceClassPosition = cars.first(where: { $0.isReferenceCar })?.classPosition
        leaderGap.classLeaderGap = LiveGapValue(
            hasData: true,
            isLeader: reference.carIdx == classLeaderCarIdx,
            seconds: resolvedReferenceGap,
            laps: reference.carIdx == classLeaderCarIdx ? 0 : nil,
            source: "demo threat"
        )
        leaderGap.classCars = cars
        output.leaderGap = leaderGap
        return output
    }

    private func resetStore(startedAtUtc: Date) {
        store = LiveTelemetryStore()
        store.markConnected()
        store.markCollectionStarted(sourceId: "gap-demo-\(scenario.title)", startedAtUtc: startedAtUtc)
    }
}

private final class RawPracticeGapDemoPlayback {
    private static let playbackSpeed = 5.0
    private static let initialHoldSeconds = 6.0

    let capture: RawPracticeGapCapture
    let state: RawPracticeGapState
    let view: GapToLeaderView
    let startedAtUtc: Date
    let onSnapshot: ((LiveTelemetrySnapshot) -> Void)?
    private let frames: [RawPracticeGapFrame]
    private var lastLoopIndex = -1
    private var lastFrameOrdinal = -1

    init(
        capture: RawPracticeGapCapture,
        state: RawPracticeGapState,
        view: GapToLeaderView,
        startedAtUtc: Date,
        onSnapshot: ((LiveTelemetrySnapshot) -> Void)? = nil
    ) {
        self.capture = capture
        self.state = state
        self.view = view
        self.startedAtUtc = startedAtUtc
        self.onSnapshot = onSnapshot
        let sortedFrames = capture.frames.sorted { $0.frameIndex < $1.frameIndex }
        let startFrameIndex = state.startFrameIndex ?? Int.min
        let targetFrames = sortedFrames.filter { $0.frameIndex >= startFrameIndex && $0.frameIndex <= state.frameIndex }
        self.frames = targetFrames.isEmpty
            ? Array(sortedFrames.prefix(1))
            : targetFrames
        primeAtTarget()
    }

    func update(capturedAtUtc: Date) {
        guard frames.count > 1 else {
            return
        }

        let elapsed = max(0, capturedAtUtc.timeIntervalSince(startedAtUtc))
        guard elapsed >= Self.initialHoldSeconds else {
            return
        }

        let playbackElapsed = elapsed - Self.initialHoldSeconds
        let duration = loopDurationSeconds()
        let loopIndex = Int(playbackElapsed / duration)
        let loopOffset = playbackElapsed.truncatingRemainder(dividingBy: duration)
        let progress = min(max(loopOffset / duration, 0), 1)
        let frameOrdinal = min(frames.count - 1, Int(Double(frames.count - 1) * progress))

        if loopIndex != lastLoopIndex || frameOrdinal < lastFrameOrdinal {
            lastLoopIndex = loopIndex
            lastFrameOrdinal = -1
            view.resetTrend()
        }

        guard frameOrdinal > lastFrameOrdinal else {
            return
        }

        for ordinal in (lastFrameOrdinal + 1)...frameOrdinal {
            let snapshot = frames[ordinal].snapshot(lapReferenceSeconds: capture.lapReferenceSeconds)
            view.update(with: snapshot)
            onSnapshot?(snapshot)
        }
        lastFrameOrdinal = frameOrdinal
    }

    private func primeAtTarget() {
        view.resetTrend()
        for frame in frames {
            let snapshot = frame.snapshot(lapReferenceSeconds: capture.lapReferenceSeconds)
            view.update(with: snapshot)
            onSnapshot?(snapshot)
        }
        lastFrameOrdinal = frames.count - 1
    }

    private func loopDurationSeconds() -> TimeInterval {
        guard let first = frames.first,
              let last = frames.last else {
            return 8
        }

        let rawDuration = max(1, last.sessionTime - first.sessionTime)
        return max(8, min(28, rawDuration / Self.playbackSpeed))
    }
}

private enum TrackMapSectorDemoSnapshotFactory {
    private static let lapOneSeconds = 14.0
    private static let lapTwoSeconds = 12.0
    private static let lapThreeSeconds = 14.0
    private static let sectors: [LiveTrackSectorSegment] = [
        LiveTrackSectorSegment(sectorNum: 0, startPct: 0.0, endPct: 0.18, highlight: LiveTrackSectorHighlights.none),
        LiveTrackSectorSegment(sectorNum: 1, startPct: 0.18, endPct: 0.36, highlight: LiveTrackSectorHighlights.none),
        LiveTrackSectorSegment(sectorNum: 2, startPct: 0.36, endPct: 0.54, highlight: LiveTrackSectorHighlights.none),
        LiveTrackSectorSegment(sectorNum: 3, startPct: 0.54, endPct: 0.72, highlight: LiveTrackSectorHighlights.none),
        LiveTrackSectorSegment(sectorNum: 4, startPct: 0.72, endPct: 0.88, highlight: LiveTrackSectorHighlights.none),
        LiveTrackSectorSegment(sectorNum: 5, startPct: 0.88, endPct: 1.0, highlight: LiveTrackSectorHighlights.none)
    ]

    static func snapshot(
        sourceId: String,
        sequence: Int,
        playbackSeconds: TimeInterval,
        capturedAtUtc: Date,
        highlights: [Int: String] = [:],
        fullLapHighlight: String? = nil
    ) -> LiveTelemetrySnapshot {
        let frame = frame(capturedAtUtc: capturedAtUtc, playbackSeconds: playbackSeconds)
        var snapshot = LiveTelemetrySnapshot.empty
        snapshot.isConnected = true
        snapshot.isCollecting = true
        snapshot.sourceId = sourceId
        snapshot.startedAtUtc = capturedAtUtc.addingTimeInterval(-playbackSeconds)
        snapshot.lastUpdatedAtUtc = capturedAtUtc
        snapshot.sequence = sequence
        snapshot.combo = .mockNurburgringMercedesRace
        snapshot.latestFrame = frame
        snapshot.fuel = LiveFuelSnapshot.from(frame)
        snapshot.proximity = LiveProximitySnapshot.from(frame)
        snapshot.leaderGap = LiveLeaderGapSnapshot.from(frame)
        snapshot.models = LiveRaceModels(trackMap: model(highlights: highlights, fullLapHighlight: fullLapHighlight))
        return snapshot
    }

    static func frame(capturedAtUtc: Date, playbackSeconds: TimeInterval) -> MockLiveTelemetryFrame {
        let progress = lapProgress(playbackSeconds)
        let teamLapCompleted = Int(progress.rounded(.down))
        let teamLapDistPct = progress.truncatingRemainder(dividingBy: 1)
        let justCompletedLap = teamLapCompleted > 0 && teamLapDistPct < 0.045
        var frame = MockLiveTelemetryFrame.mock(
            capturedAtUtc: capturedAtUtc,
            sessionTime: playbackSeconds,
            fuelLevelLiters: FourHourRacePreview.fuelLevelLiters(sessionTime: playbackSeconds),
            fuelUsePerHourLiters: FourHourRacePreview.fuelUsePerHourLiters
        )
        frame.estimatedLapSeconds = currentLapSeconds(progress)
        frame.teamLapCompleted = teamLapCompleted
        frame.teamLapDistPct = teamLapDistPct
        frame.leaderLapCompleted = teamLapCompleted
        frame.leaderLapDistPct = teamLapDistPct
        frame.lastLapTimeSeconds = justCompletedLap ? completedLapSeconds(teamLapCompleted) : nil
        frame.bestLapTimeSeconds = justCompletedLap ? min(lapOneSeconds, lapTwoSeconds) : nil
        frame.lapDeltaToSessionBestLapSeconds = justCompletedLap ? (teamLapCompleted == 1 ? 0.0 : 1.2) : nil
        frame.lapDeltaToSessionBestLapOk = justCompletedLap ? true : nil
        return frame
    }

    private static func model(highlights: [Int: String], fullLapHighlight: String?) -> LiveTrackMapModel {
        LiveTrackMapModel(
            hasSectors: true,
            hasLiveTiming: true,
            quality: "reliable",
            sectors: sectors.map { sector in
                var output = sector
                output.highlight = fullLapHighlight ?? highlights[sector.sectorNum] ?? LiveTrackSectorHighlights.none
                return output
            }
        )
    }

    private static func lapProgress(_ playbackSeconds: TimeInterval) -> Double {
        if playbackSeconds < lapOneSeconds {
            return playbackSeconds / lapOneSeconds
        }

        if playbackSeconds < lapOneSeconds + lapTwoSeconds {
            return 1.0 + (playbackSeconds - lapOneSeconds) / lapTwoSeconds
        }

        return 2.0 + (playbackSeconds - lapOneSeconds - lapTwoSeconds) / lapThreeSeconds
    }

    private static func currentLapSeconds(_ progress: Double) -> Double {
        if progress < 1 {
            return lapOneSeconds
        }

        if progress < 2 {
            return lapTwoSeconds
        }

        return lapThreeSeconds
    }

    private static func completedLapSeconds(_ lapCompleted: Int) -> Double {
        switch lapCompleted {
        case 1:
            return lapOneSeconds
        case 2:
            return lapTwoSeconds
        default:
            return lapThreeSeconds
        }
    }
}

private final class TrackMapSectorDemoPlayback {
    private static let loopDurationSeconds = 34.0

    let view: TrackMapView
    let startedAtUtc: Date
    let onSnapshot: ((LiveTelemetrySnapshot) -> Void)?
    private var store = LiveTelemetryStore()
    private var lastLoopIndex = -1

    init(
        view: TrackMapView,
        startedAtUtc: Date,
        onSnapshot: ((LiveTelemetrySnapshot) -> Void)? = nil
    ) {
        self.view = view
        self.startedAtUtc = startedAtUtc
        self.onSnapshot = onSnapshot
        resetStore(startedAtUtc: startedAtUtc, loopIndex: 0)
    }

    func update(capturedAtUtc: Date) {
        let elapsed = max(0, capturedAtUtc.timeIntervalSince(startedAtUtc))
        let loopIndex = Int(elapsed / Self.loopDurationSeconds)
        if loopIndex != lastLoopIndex {
            lastLoopIndex = loopIndex
            resetStore(startedAtUtc: capturedAtUtc, loopIndex: loopIndex)
        }

        let loopOffset = elapsed.truncatingRemainder(dividingBy: Self.loopDurationSeconds)
        let playbackSeconds = loopOffset >= 0 ? loopOffset : loopOffset + Self.loopDurationSeconds
        store.recordFrame(TrackMapSectorDemoSnapshotFactory.frame(
            capturedAtUtc: capturedAtUtc,
            playbackSeconds: playbackSeconds
        ))
        let snapshot = store.snapshot()
        view.update(with: snapshot)
        onSnapshot?(snapshot)
    }

    private func resetStore(startedAtUtc: Date, loopIndex: Int) {
        store = LiveTelemetryStore()
        store.markConnected()
        store.markCollectionStarted(sourceId: "track-map-sector-demo-\(loopIndex)", startedAtUtc: startedAtUtc)
    }
}

final class OverlayManager {
    private static let liveTelemetryFreshnessSeconds = 1.5
    private static let telemetryFadeInSeconds = 0.22
    private static let telemetryFadeOutSeconds = 0.65

    private let state: TelemetryCaptureState
    private let liveTelemetryStore: LiveTelemetryStore
    private let historyQueryService: SessionHistoryQueryService
    private let settingsStore: AppSettingsStore
    private let events: AppEventRecorder
    private let logger: LocalLogWriter
    private let liveOverlayDiagnosticsRecorder: LiveOverlayDiagnosticsRecorder?
    private var settings = ApplicationSettings()
    private var overlayWindows: [String: OverlayWindow] = [:]
    private var appliedScales: [String: Double] = [:]
    private var baseOverlayOpacities: [String: Double] = [:]
    private var liveTelemetryFadeAlphas: [String: Double] = [:]
    private var liveTelemetryFadeTargets: [String: Double] = [:]
    private var lastFadeStepAtUtc = Date()
    private var fuelCalculatorView: FuelCalculatorView?
    private var relativeOverlayView: RelativeOverlayView?
    private var standingsOverlayView: StandingsOverlayView?
    private var trackMapView: TrackMapView?
    private var flagsOverlayView: SimpleTelemetryOverlayView?
    private var sessionWeatherOverlayView: SimpleTelemetryOverlayView?
    private var pitServiceOverlayView: SimpleTelemetryOverlayView?
    private var inputStateOverlayView: SimpleTelemetryOverlayView?
    private var carRadarView: CarRadarView?
    private var gapToLeaderView: GapToLeaderView?
    private var relativeDesignV2View: RelativeDesignV2OverlayView?
    private var settingsOverlayView: SettingsOverlayView?
    private var radarSettingsPreviewVisible = false
    private var radarCaptureDemoScenarios: [RadarCaptureScenario] = []
    private var radarCaptureDemoViews: [String: RadarCaptureDemoView] = [:]
    private var radarCaptureDemoStartedAtUtc: Date?
    private var radarCaptureDemoSequence = 0
    private var gapToLeaderDemoActive = false
    private var gapToLeaderDemoViews: [String: GapToLeaderView] = [:]
    private var gapToLeaderDemoPlaybacks: [String: GapToLeaderDemoPlayback] = [:]
    private var rawPracticeGapDemoPlaybacks: [String: RawPracticeGapDemoPlayback] = [:]
    private var gapToLeaderDemoStartedAtUtc: Date?
    private var trackMapSectorDemoActive = false
    private var trackMapSectorDemoViews: [String: TrackMapView] = [:]
    private var trackMapSectorDemoPlaybacks: [String: TrackMapSectorDemoPlayback] = [:]
    private var designV2ComponentDemoActive = false
    private var designV2ComponentDemoOverlayIds = Set<String>()
    private var designV2OverlaySuiteActive = false
    private var designV2OverlaySuiteViews: [DesignV2OverlayMockKind: DesignV2OverlaySuiteView] = [:]
    private var designV2OverlaySuiteOverlayIds = Set<String>()
    private let relativeDesignV2DemoDefinition = OverlayDefinition(
        id: "relative-design-v2",
        displayName: "Relative V2",
        defaultSize: RelativeOverlayDefinition.definition.defaultSize,
        showSessionFilters: false,
        showScaleControl: false,
        showOpacityControl: false,
        fadeWhenLiveTelemetryUnavailable: true
    )
    private var lastOverlayErrors: [String: String] = [:]
    private var lastOverlayErrorDates: [String: Date] = [:]

    init(
        state: TelemetryCaptureState,
        liveTelemetryStore: LiveTelemetryStore,
        historyQueryService: SessionHistoryQueryService,
        settingsStore: AppSettingsStore,
        events: AppEventRecorder,
        logger: LocalLogWriter,
        liveOverlayDiagnosticsRecorder: LiveOverlayDiagnosticsRecorder? = nil
    ) {
        self.state = state
        self.liveTelemetryStore = liveTelemetryStore
        self.historyQueryService = historyQueryService
        self.settingsStore = settingsStore
        self.events = events
        self.logger = logger
        self.liveOverlayDiagnosticsRecorder = liveOverlayDiagnosticsRecorder
    }

    func showStartupOverlays() {
        settings = settingsStore.load()
        ensureManagedOverlaySettings()
        openSettingsOverlay()
        refreshOverlayVisibility()
        applyDisplaySettingsToOpenOverlays()
        settingsStore.save(settings)
    }

    func openSettingsOverlay() {
        let defaultOrigin = centeredDefaultOrigin(definition: SettingsOverlayDefinition.definition)
        var overlaySettings = settings.overlay(
            id: SettingsOverlayDefinition.definition.id,
            defaultSize: SettingsOverlayDefinition.definition.defaultSize,
            defaultOrigin: defaultOrigin,
            defaultEnabled: true
        )
        overlaySettings.width = SettingsOverlayDefinition.definition.defaultSize.width
        overlaySettings.height = SettingsOverlayDefinition.definition.defaultSize.height
        overlaySettings.x = defaultOrigin.x
        overlaySettings.y = defaultOrigin.y
        overlaySettings.opacity = 1.0
        overlaySettings.alwaysOnTop = false
        overlaySettings.enabled = true
        settings.updateOverlay(overlaySettings)

        if settingsOverlayView == nil {
            settingsOverlayView = showOverlay(
                definition: SettingsOverlayDefinition.definition,
                defaultOrigin: defaultOrigin
            ) { [weak self] in
                SettingsOverlayView(
                    settings: self?.settings ?? ApplicationSettings(),
                    captureSnapshot: self?.state.snapshot() ?? .idle,
                    overlayDefinitions: self?.managedOverlayDefinitions ?? [],
                    onSettingsChanged: { [weak self] updatedSettings in
                        self?.settings = updatedSettings
                        self?.settingsStore.save(updatedSettings)
                        self?.refreshOverlayVisibility()
                        self?.applyDisplaySettingsToOpenOverlays()
                    },
                    rawCaptureChanged: { [weak self] enabled in
                        guard let self else {
                            return false
                        }

                        let accepted = self.state.setRawCaptureEnabled(enabled)
                        self.events.record("raw_capture_runtime_toggle", properties: [
                            "requested": String(enabled),
                            "accepted": String(accepted),
                            "source": "settings_overlay"
                        ])

                        if accepted {
                            self.logger.info("Runtime raw capture request changed to \(enabled).")
                        } else {
                            self.logger.warning("Runtime raw capture request to disable was rejected because capture is active.")
                        }

                        self.settingsOverlayView?.updateCaptureStatus(self.state.snapshot())
                        return accepted
                    },
                    selectedOverlayChanged: { [weak self] overlayId in
                        self?.selectSettingsOverlayTab(overlayId)
                    }
                )
            } as? SettingsOverlayView
        }

        if let selectedSettingsTab = Self.environmentString("TMR_MAC_SELECTED_SETTINGS_TAB") {
            settingsOverlayView?.selectTab(identifier: selectedSettingsTab)
        }

        if let window = overlayWindows[SettingsOverlayDefinition.definition.id] {
            var frame = window.frame
            frame.origin = overlayOrigin(settings: overlaySettings, size: frame.size)
            window.setFrame(frame, display: true)
        }

        applyDisplaySettingsToOpenOverlays()
        if let window = overlayWindows[SettingsOverlayDefinition.definition.id] {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
        }
        refreshOverlayVisibility()
    }

    func updateStatus(with snapshot: TelemetryCaptureStatusSnapshot? = nil) {
        let captureSnapshot = snapshot ?? state.snapshot()
        let liveSnapshot = liveTelemetryStore.snapshot()
        refreshOverlayVisibility(liveSnapshot: liveSnapshot)
        applyDisplaySettingsToOpenOverlays(captureSnapshot: captureSnapshot)
        fuelCalculatorView?.update(with: liveSnapshot)
        updateOverlay("relative", showError: { [weak self] message in
            self?.relativeOverlayView?.showOverlayError(message)
        }) { [weak self] in
            self?.relativeOverlayView?.update(with: liveSnapshot)
        }
        updateOverlay("relative v2", showError: { [weak self] message in
            self?.relativeDesignV2View?.showOverlayError(message)
        }) { [weak self] in
            self?.relativeDesignV2View?.update(with: liveSnapshot)
        }
        for (kind, view) in designV2OverlaySuiteViews {
            updateOverlay("\(kind.title) v2", showError: { message in
                view.showOverlayError(message)
            }) {
                view.update(with: liveSnapshot)
            }
        }
        updateOverlay("standings", showError: { [weak self] message in
            self?.standingsOverlayView?.showOverlayError(message)
        }) { [weak self] in
            self?.standingsOverlayView?.update(with: liveSnapshot)
        }
        updateOverlay("track map", showError: { [weak self] message in
            self?.trackMapView?.showOverlayError(message)
        }) { [weak self] in
            self?.trackMapView?.update(with: liveSnapshot)
        }
        updateOverlay("flags", showError: { [weak self] message in
            self?.flagsOverlayView?.showOverlayError(message)
        }) { [weak self] in
            self?.flagsOverlayView?.update(with: liveSnapshot)
        }
        updateOverlay("session/weather", showError: { [weak self] message in
            self?.sessionWeatherOverlayView?.showOverlayError(message)
        }) { [weak self] in
            self?.sessionWeatherOverlayView?.update(with: liveSnapshot)
        }
        updateOverlay("pit service", showError: { [weak self] message in
            self?.pitServiceOverlayView?.showOverlayError(message)
        }) { [weak self] in
            self?.pitServiceOverlayView?.update(with: liveSnapshot)
        }
        updateOverlay("input state", showError: { [weak self] message in
            self?.inputStateOverlayView?.showOverlayError(message)
        }) { [weak self] in
            self?.inputStateOverlayView?.update(with: liveSnapshot)
        }
        updateOverlay("car radar", showError: { [weak self] message in
            self?.carRadarView?.showOverlayError(message)
        }) { [weak self] in
            self?.carRadarView?.update(with: liveSnapshot)
        }
        updateOverlay("gap-to-leader", showError: { [weak self] message in
            self?.gapToLeaderView?.showOverlayError(message)
        }) { [weak self] in
            self?.gapToLeaderView?.update(with: liveSnapshot)
        }
        updateRadarCaptureDemoWindows(capturedAtUtc: Date())
        updateGapToLeaderDemoWindows(capturedAtUtc: Date())
        updateTrackMapSectorDemoWindows(capturedAtUtc: Date())
    }

    func setRadarPreviewVisible(_ visible: Bool) {
        radarSettingsPreviewVisible = visible
        refreshOverlayVisibility()
        applyDisplaySettingsToOpenOverlays()
    }

    func showRadarCaptureDemo(scenarios: [RadarCaptureScenario], startedAtUtc: Date) {
        radarCaptureDemoScenarios = scenarios
        radarCaptureDemoStartedAtUtc = startedAtUtc
        radarCaptureDemoSequence = 0
        radarSettingsPreviewVisible = false
        carRadarView?.settingsPreviewVisible = false
        overlayWindows[CarRadarOverlayDefinition.definition.id]?.orderOut(nil)
        closeRadarCaptureDemoWindows()

        let colorVariants: [RadarCaptureDemoColorVariant] = [.neutralProximity, .classColors, .classColorToAlertRed]
        for (index, scenario) in scenarios.enumerated() {
            let overlayId = radarCaptureDemoOverlayId(index: index)
            let size = radarCaptureDemoWindowSize(variantCount: colorVariants.count)
            let definition = OverlayDefinition(
                id: overlayId,
                displayName: "Radar \(index + 1)",
                defaultSize: size
            )
            let contentView = RadarCaptureDemoView(
                title: "\(index + 1). \(scenario.title)",
                radarSize: CarRadarOverlayDefinition.definition.defaultSize,
                colorVariants: colorVariants
            )
            for radarView in contentView.radarViews {
                radarView.showGapLabels = true
                radarView.debugOpaqueRendering = true
            }
            let overlaySettings = OverlaySettings(
                id: overlayId,
                x: radarCaptureDemoOrigin(index: index, variantCount: colorVariants.count).x,
                y: radarCaptureDemoOrigin(index: index, variantCount: colorVariants.count).y,
                width: size.width,
                height: size.height,
                opacity: 1.0
            )
            let window = makeOverlayWindow(
                contentView: contentView,
                definition: definition,
                settings: overlaySettings
            )
            window.title = scenario.title
            overlayWindows[overlayId] = window
            radarCaptureDemoViews[overlayId] = contentView
            appliedScales[overlayId] = overlaySettings.scale
            window.orderFrontRegardless()
        }

        applyDisplaySettingsToOpenOverlays()
        updateRadarCaptureDemoWindows(capturedAtUtc: Date())
    }

    func showGapToLeaderDemo(scenarios: [GapToLeaderDemoScenario]) {
        gapToLeaderDemoActive = true
        gapToLeaderDemoStartedAtUtc = Date()
        closeGapToLeaderDemoWindows()
        hideManagedOverlayWindowsForDemo()

        for (index, scenario) in scenarios.enumerated() {
            let overlayId = gapToLeaderDemoOverlayId(index: index)
            let definition = OverlayDefinition(
                id: overlayId,
                displayName: "Gap Demo \(index + 1)",
                defaultSize: GapToLeaderOverlayDefinition.definition.defaultSize
            )
            let view = GapToLeaderView(frame: NSRect(origin: .zero, size: definition.defaultSize))
            view.displayMode = index == 2 ? .leaderGap : .filteredLeaderGap
            view.visibleTrendWindowSeconds = scenario.visibleTrendWindowSeconds
            view.graphRefreshIntervalSeconds = scenario.graphRefreshIntervalSeconds
            configureGapToLeaderDemoView(view)
            let playback = GapToLeaderDemoPlayback(
                scenario: scenario,
                view: view,
                startedAtUtc: gapToLeaderDemoStartedAtUtc ?? Date(),
                sequenceBase: index * 10_000,
                onSnapshot: { [weak self] snapshot in
                    self?.liveOverlayDiagnosticsRecorder?.record(snapshot)
                }
            )

            let overlaySettings = OverlaySettings(
                id: overlayId,
                x: gapToLeaderDemoOrigin(index: index, count: scenarios.count).x,
                y: gapToLeaderDemoOrigin(index: index, count: scenarios.count).y,
                width: definition.defaultSize.width,
                height: definition.defaultSize.height,
                opacity: 1.0
            )
            let window = makeOverlayWindow(
                contentView: view,
                definition: definition,
                settings: overlaySettings
            )
            window.title = scenario.title
            overlayWindows[overlayId] = window
            gapToLeaderDemoViews[overlayId] = view
            gapToLeaderDemoPlaybacks[overlayId] = playback
            appliedScales[overlayId] = overlaySettings.scale
            window.orderFrontRegardless()
        }

        applyDisplaySettingsToOpenOverlays()
        updateGapToLeaderDemoWindows(capturedAtUtc: Date())
    }

    func showRawPracticeGapDemo(capture: RawPracticeGapCapture, startedAtUtc: Date) {
        gapToLeaderDemoActive = true
        gapToLeaderDemoStartedAtUtc = startedAtUtc
        closeGapToLeaderDemoWindows()
        hideManagedOverlayWindowsForDemo()

        for (index, state) in capture.states.enumerated() {
            let overlayId = gapToLeaderDemoOverlayId(index: index)
            let definition = OverlayDefinition(
                id: overlayId,
                displayName: "Raw Gap \(index + 1)",
                defaultSize: GapToLeaderOverlayDefinition.definition.defaultSize
            )
            let view = GapToLeaderView(frame: NSRect(origin: .zero, size: definition.defaultSize))
            view.displayMode = .filteredLeaderGap
            view.isPaceTimingMode = true
            view.visibleTrendWindowSeconds = capture.visibleTrendWindowSeconds
            view.graphRefreshIntervalSeconds = 0
            configureGapToLeaderDemoView(view)
            let playback = RawPracticeGapDemoPlayback(
                capture: capture,
                state: state,
                view: view,
                startedAtUtc: startedAtUtc,
                onSnapshot: { [weak self] snapshot in
                    self?.liveOverlayDiagnosticsRecorder?.record(snapshot)
                }
            )

            let overlaySettings = OverlaySettings(
                id: overlayId,
                x: gapToLeaderDemoOrigin(index: index, count: capture.states.count).x,
                y: gapToLeaderDemoOrigin(index: index, count: capture.states.count).y,
                width: definition.defaultSize.width,
                height: definition.defaultSize.height,
                opacity: 1.0
            )
            let window = makeOverlayWindow(
                contentView: view,
                definition: definition,
                settings: overlaySettings
            )
            window.title = state.title
            overlayWindows[overlayId] = window
            gapToLeaderDemoViews[overlayId] = view
            rawPracticeGapDemoPlaybacks[overlayId] = playback
            appliedScales[overlayId] = overlaySettings.scale
            window.orderFrontRegardless()
        }

        applyDisplaySettingsToOpenOverlays()
        updateGapToLeaderDemoWindows(capturedAtUtc: Date())
    }

    func showTrackMapSectorDemo(startedAtUtc: Date) {
        trackMapSectorDemoActive = true
        closeTrackMapSectorDemoWindows()
        hideManagedOverlayWindowsForDemo()

        let examples: [(String, NSPoint, LiveTelemetrySnapshot, Bool)] = [
            (
                "track-map-sector-demo-live",
                NSPoint(x: 650, y: 260),
                TrackMapSectorDemoSnapshotFactory.snapshot(
                    sourceId: "track-map-sector-demo-live",
                    sequence: 1,
                    playbackSeconds: 0,
                    capturedAtUtc: startedAtUtc
                ),
                true
            ),
            (
                "track-map-sector-demo-normal",
                NSPoint(x: 24, y: 24),
                TrackMapSectorDemoSnapshotFactory.snapshot(
                    sourceId: "track-map-sector-demo-normal",
                    sequence: 2,
                    playbackSeconds: 3,
                    capturedAtUtc: startedAtUtc
                ),
                false
            ),
            (
                "track-map-sector-demo-sector-best",
                NSPoint(x: 400, y: 24),
                TrackMapSectorDemoSnapshotFactory.snapshot(
                    sourceId: "track-map-sector-demo-sector-best",
                    sequence: 3,
                    playbackSeconds: 8,
                    capturedAtUtc: startedAtUtc,
                    highlights: [0: LiveTrackSectorHighlights.personalBest, 1: LiveTrackSectorHighlights.personalBest]
                ),
                false
            ),
            (
                "track-map-sector-demo-best-lap",
                NSPoint(x: 24, y: 400),
                TrackMapSectorDemoSnapshotFactory.snapshot(
                    sourceId: "track-map-sector-demo-best-lap",
                    sequence: 4,
                    playbackSeconds: 14.1,
                    capturedAtUtc: startedAtUtc,
                    fullLapHighlight: LiveTrackSectorHighlights.bestLap
                ),
                false
            ),
            (
                "track-map-sector-demo-following-s1",
                NSPoint(x: 400, y: 400),
                TrackMapSectorDemoSnapshotFactory.snapshot(
                    sourceId: "track-map-sector-demo-following-s1",
                    sequence: 5,
                    playbackSeconds: 16.3,
                    capturedAtUtc: startedAtUtc,
                    highlights: [0: LiveTrackSectorHighlights.personalBest]
                ),
                false
            )
        ]

        for (index, example) in examples.enumerated() {
            let definition = OverlayDefinition(
                id: example.0,
                displayName: "Track Map Demo \(index + 1)",
                defaultSize: TrackMapOverlayDefinition.definition.defaultSize
            )
            let view = TrackMapView(frame: NSRect(origin: .zero, size: definition.defaultSize))
            view.update(with: example.2)
            let overlaySettings = OverlaySettings(
                id: definition.id,
                x: example.1.x,
                y: example.1.y,
                width: definition.defaultSize.width,
                height: definition.defaultSize.height,
                opacity: 1.0
            )
            let window = makeOverlayWindow(
                contentView: view,
                definition: definition,
                settings: overlaySettings
            )
            overlayWindows[definition.id] = window
            trackMapSectorDemoViews[definition.id] = view
            appliedScales[definition.id] = overlaySettings.scale
            if example.3 {
                trackMapSectorDemoPlaybacks[definition.id] = TrackMapSectorDemoPlayback(
                    view: view,
                    startedAtUtc: startedAtUtc,
                    onSnapshot: { [weak self] snapshot in
                        self?.liveOverlayDiagnosticsRecorder?.record(snapshot)
                    }
                )
            }
            window.orderFrontRegardless()
        }

        updateTrackMapSectorDemoWindows(capturedAtUtc: Date())
    }

    func showDesignV2ComponentDemo(theme: DesignV2Theme) {
        designV2ComponentDemoActive = true
        closeDesignV2ComponentDemoWindows()
        hideManagedOverlayWindowsForDemo()

        let definition = DesignV2ComponentOverlayDefinition.definition(theme: theme)
        let view = DesignV2ComponentGalleryView(theme: theme)
        let overlaySettings = OverlaySettings(
            id: definition.id,
            x: 24,
            y: 24,
            width: definition.defaultSize.width,
            height: definition.defaultSize.height,
            opacity: 1.0
        )
        let window = makeOverlayWindow(
            contentView: view,
            definition: definition,
            settings: overlaySettings
        )
        window.title = "\(definition.displayName) - \(theme.displayName)"
        overlayWindows[definition.id] = window
        designV2ComponentDemoOverlayIds.insert(definition.id)
        appliedScales[definition.id] = overlaySettings.scale
        window.orderFrontRegardless()
    }

    func showDesignV2OverlaySuiteDemo(theme: DesignV2Theme) {
        designV2OverlaySuiteActive = true
        closeDesignV2OverlaySuiteWindows()
        hideManagedOverlayWindowsForDemo()

        for (index, kind) in designV2OverlaySuiteKinds().enumerated() {
            showDesignV2OverlaySuiteWindow(kind: kind, index: index, theme: theme)
        }
        applyDisplaySettingsToOpenOverlays()
    }

    private func designV2OverlaySuiteKinds() -> [DesignV2OverlayMockKind] {
        guard let rawValue = Self.environmentString("TMR_MAC_DESIGN_V2_OVERLAYS") else {
            return DesignV2OverlayMockKind.allCases
        }

        let requested = rawValue
            .split { $0 == "," || $0 == " " || $0 == ";" || $0 == ":" }
            .compactMap { DesignV2OverlayMockKind(reviewAlias: String($0)) }
        return requested.isEmpty ? DesignV2OverlayMockKind.allCases : requested
    }

    func showRelativeDesignV2ShellDemo(theme: DesignV2Theme) {
        var relativeSettings = settings.overlay(
            id: RelativeOverlayDefinition.definition.id,
            defaultSize: RelativeOverlayDefinition.definition.defaultSize,
            defaultOrigin: NSPoint(x: 24, y: 530),
            defaultEnabled: true
        )
        relativeSettings.enabled = true
        relativeSettings.alwaysOnTop = true
        relativeSettings.showInTest = true
        relativeSettings.showInPractice = true
        relativeSettings.showInQualifying = true
        relativeSettings.showInRace = true
        relativeSettings.scale = min(max(relativeSettings.scale, 0.6), 2.0)
        relativeSettings.width = RelativeOverlayDefinition.definition.defaultSize.width * relativeSettings.scale
        relativeSettings.height = RelativeOverlayDefinition.definition.defaultSize.height * relativeSettings.scale
        settings.updateOverlay(relativeSettings)
        settingsStore.save(settings)
        refreshOverlayVisibility()
        applyDisplaySettingsToOpenOverlays()
        showRelativeDesignV2DemoWindow(settings: relativeSettings, theme: theme)
    }

    private func showRelativeDesignV2DemoWindow(settings relativeSettings: OverlaySettings, theme: DesignV2Theme) {
        if let window = overlayWindows[relativeDesignV2DemoDefinition.id] {
            relativeDesignV2View?.theme = theme
            resizeRelativeDesignV2Demo(settings: relativeSettings)
            window.orderFrontRegardless()
            return
        }

        let size = RelativeDesignV2OverlayView.demoSize(
            settings: relativeSettings,
            sessionKey: liveTelemetryStore.snapshot().combo.sessionKey
        )
        let overlaySettings = OverlaySettings(
            id: relativeDesignV2DemoDefinition.id,
            x: relativeSettings.x + relativeSettings.width + 24,
            y: relativeSettings.y,
            width: size.width,
            height: size.height,
            opacity: relativeSettings.opacity,
            alwaysOnTop: relativeSettings.alwaysOnTop
        )
        let view = RelativeDesignV2OverlayView(frame: NSRect(origin: .zero, size: size))
        view.theme = theme
        let carsEachSide = relativeCarsEachSide(relativeSettings)
        view.carsAhead = carsEachSide
        view.carsBehind = carsEachSide
        view.contentSettings = relativeSettings
        view.fontFamily = OverlayTheme.defaultFontFamily
        let window = makeOverlayWindow(
            contentView: view,
            definition: relativeDesignV2DemoDefinition,
            settings: overlaySettings
        )
        window.title = relativeDesignV2DemoDefinition.displayName
        overlayWindows[relativeDesignV2DemoDefinition.id] = window
        relativeDesignV2View = view
        appliedScales[relativeDesignV2DemoDefinition.id] = relativeSettings.scale
        applyRelativeDesignV2Opacity(settings: relativeSettings)
        window.orderFrontRegardless()
    }

    private func showDesignV2OverlaySuiteWindow(kind: DesignV2OverlayMockKind, index: Int, theme: DesignV2Theme) {
        let sourceDefinition = kind.sourceDefinition
        let sourceSettings = settings.overlay(
            id: sourceDefinition.id,
            defaultSize: sourceDefinition.defaultSize,
            defaultOrigin: defaultOrigin(definition: sourceDefinition),
            defaultEnabled: false
        )
        let size = designV2SuiteSize(kind: kind, sourceSettings: sourceSettings)
        let definition = designV2SuiteDefinition(kind: kind, size: size)
        let origin = designV2SuiteOrigin(index: index, size: size)
        let overlaySettings = OverlaySettings(
            id: definition.id,
            x: origin.x,
            y: origin.y,
            width: size.width,
            height: size.height,
            opacity: sourceDefinition.showOpacityControl ? sourceSettings.opacity : 1.0,
            alwaysOnTop: sourceSettings.alwaysOnTop
        )
        let view = DesignV2OverlaySuiteView(
            kind: kind,
            historyQueryService: historyQueryService
        )
        view.theme = theme
        view.sourceSettings = sourceSettings
        view.fontFamily = settings.general.fontFamily
        view.unitSystem = settings.general.unitSystem
        let window = makeOverlayWindow(
            contentView: view,
            definition: definition,
            settings: overlaySettings
        )
        window.title = definition.displayName
        overlayWindows[definition.id] = window
        designV2OverlaySuiteViews[kind] = view
        designV2OverlaySuiteOverlayIds.insert(definition.id)
        appliedScales[definition.id] = sourceSettings.scale
        applyDesignV2OverlaySuiteOpacity(kind: kind, settings: sourceSettings)
        window.orderFrontRegardless()
    }

    private func designV2SuiteDefinition(kind: DesignV2OverlayMockKind, size: NSSize) -> OverlayDefinition {
        OverlayDefinition(
            id: kind.demoId,
            displayName: "\(kind.title) V2",
            defaultSize: size,
            showSessionFilters: false,
            showScaleControl: false,
            showOpacityControl: false,
            fadeWhenLiveTelemetryUnavailable: kind.sourceDefinition.fadeWhenLiveTelemetryUnavailable
        )
    }

    private func designV2SuiteSize(kind: DesignV2OverlayMockKind, sourceSettings: OverlaySettings) -> NSSize {
        let scale = min(max(sourceSettings.scale, 0.6), 2.0)
        return NSSize(
            width: kind.defaultSize.width * scale,
            height: kind.defaultSize.height * scale
        )
    }

    private func designV2SuiteOrigin(index: Int, size: NSSize) -> NSPoint {
        let columns = 2
        let gap: CGFloat = 22
        let column = index % columns
        let row = index / columns
        let x = CGFloat(24) + CGFloat(column) * (660 + gap)
        let y = CGFloat(620) - CGFloat(row) * (max(size.height, 310) + gap)
        return NSPoint(x: x, y: max(24, y))
    }

    func closeDesignV2ComponentDemo() {
        designV2ComponentDemoActive = false
        closeDesignV2ComponentDemoWindows()
        refreshOverlayVisibility()
    }

    func closeDesignV2OverlaySuiteDemo() {
        designV2OverlaySuiteActive = false
        closeDesignV2OverlaySuiteWindows()
        refreshOverlayVisibility()
    }

    func closeAll() {
        for (overlayId, window) in overlayWindows {
            saveOverlayFrame(window.frame, overlayId: overlayId, window: window)
            window.close()
        }

        overlayWindows.removeAll()
        appliedScales.removeAll()
        baseOverlayOpacities.removeAll()
        liveTelemetryFadeAlphas.removeAll()
        liveTelemetryFadeTargets.removeAll()
        fuelCalculatorView = nil
        relativeOverlayView = nil
        relativeDesignV2View = nil
        standingsOverlayView = nil
        trackMapView = nil
        flagsOverlayView = nil
        sessionWeatherOverlayView = nil
        pitServiceOverlayView = nil
        inputStateOverlayView = nil
        carRadarView = nil
        gapToLeaderView = nil
        settingsOverlayView = nil
        radarCaptureDemoScenarios = []
        radarCaptureDemoViews.removeAll()
        radarCaptureDemoStartedAtUtc = nil
        gapToLeaderDemoViews.removeAll()
        gapToLeaderDemoPlaybacks.removeAll()
        rawPracticeGapDemoPlaybacks.removeAll()
        gapToLeaderDemoStartedAtUtc = nil
        gapToLeaderDemoActive = false
        trackMapSectorDemoViews.removeAll()
        trackMapSectorDemoPlaybacks.removeAll()
        trackMapSectorDemoActive = false
        designV2ComponentDemoOverlayIds.removeAll()
        designV2ComponentDemoActive = false
        designV2OverlaySuiteViews.removeAll()
        designV2OverlaySuiteOverlayIds.removeAll()
        designV2OverlaySuiteActive = false
    }

    private var managedOverlayDefinitions: [OverlayDefinition] {
        [
            FuelCalculatorOverlayDefinition.definition,
            RelativeOverlayDefinition.definition,
            StandingsOverlayDefinition.definition,
            TrackMapOverlayDefinition.definition,
            StreamChatOverlayDefinition.definition,
            GarageCoverOverlayDefinition.definition,
            FlagsOverlayDefinition.definition,
            SessionWeatherOverlayDefinition.definition,
            PitServiceOverlayDefinition.definition,
            InputStateOverlayDefinition.definition,
            CarRadarOverlayDefinition.definition,
            GapToLeaderOverlayDefinition.definition
        ]
    }

    private func ensureManagedOverlaySettings() {
        for definition in managedOverlayDefinitions {
            var overlaySettings = settings.overlay(
                id: definition.id,
                defaultSize: definition.defaultSize,
                defaultOrigin: defaultOrigin(definition: definition),
                defaultEnabled: false
            )
            overlaySettings.scale = min(max(overlaySettings.scale, 0.6), 2.0)
            if overlaySettings.width <= 0 || overlaySettings.height <= 0 {
                overlaySettings.width = definition.defaultSize.width * overlaySettings.scale
                overlaySettings.height = definition.defaultSize.height * overlaySettings.scale
            }
            applyGapRaceOnlyPolicy(definition: definition, settings: &overlaySettings)
            applyFlagsCompactPolicy(definition: definition, settings: &overlaySettings)
            settings.updateOverlay(overlaySettings)
        }
    }

    private func refreshOverlayVisibility(liveSnapshot: LiveTelemetrySnapshot? = nil) {
        guard !gapToLeaderDemoActive && !trackMapSectorDemoActive && !designV2ComponentDemoActive && !designV2OverlaySuiteActive else {
            hideManagedOverlayWindowsForDemo()
            return
        }

        let sessionKind = currentSessionKind(liveSnapshot: liveSnapshot ?? liveTelemetryStore.snapshot())
        refreshFuelOverlay(sessionKind: sessionKind)
        refreshRelativeOverlay(sessionKind: sessionKind)
        refreshStandingsOverlay(sessionKind: sessionKind)
        refreshTrackMapOverlay(sessionKind: sessionKind)
        refreshFlagsOverlay(sessionKind: sessionKind)
        refreshSessionWeatherOverlay(sessionKind: sessionKind)
        refreshPitServiceOverlay(sessionKind: sessionKind)
        refreshInputStateOverlay(sessionKind: sessionKind)
        refreshCarRadarOverlay(sessionKind: sessionKind)
        refreshGapOverlay(sessionKind: sessionKind)
        applyLiveTelemetryFade(liveSnapshot: liveSnapshot ?? liveTelemetryStore.snapshot())
    }

    private func applyDisplaySettingsToOpenOverlays(captureSnapshot: TelemetryCaptureStatusSnapshot? = nil) {
        let fontFamily = OverlayTheme.defaultFontFamily
        settingsOverlayView?.applySettings(settings)
        settingsOverlayView?.updateCaptureStatus(captureSnapshot ?? state.snapshot())

        if let fuelSettings = settings.overlays.first(where: { $0.id == FuelCalculatorOverlayDefinition.definition.id }) {
            fuelCalculatorView?.showAdvice = fuelSettings.showFuelAdvice
            fuelCalculatorView?.showSource = fuelSettings.showFuelSource
        }
        fuelCalculatorView?.fontFamily = fontFamily
        fuelCalculatorView?.unitSystem = settings.general.unitSystem

        if let relativeSettings = settings.overlays.first(where: { $0.id == RelativeOverlayDefinition.definition.id }) {
            let carsEachSide = relativeCarsEachSide(relativeSettings)
            relativeOverlayView?.carsAhead = carsEachSide
            relativeOverlayView?.carsBehind = carsEachSide
            relativeOverlayView?.contentSettings = relativeSettings
            relativeDesignV2View?.carsAhead = carsEachSide
            relativeDesignV2View?.carsBehind = carsEachSide
            relativeDesignV2View?.contentSettings = relativeSettings
            resizeRelativeDesignV2Demo(settings: relativeSettings)
        }
        relativeOverlayView?.fontFamily = fontFamily
        relativeDesignV2View?.fontFamily = fontFamily

        if let standingsSettings = settings.overlays.first(where: { $0.id == StandingsOverlayDefinition.definition.id }) {
            standingsOverlayView?.contentSettings = standingsSettings
        }
        standingsOverlayView?.fontFamily = fontFamily
        applyDesignV2OverlaySuiteSettings(fontFamily: fontFamily)
        trackMapView?.fontFamily = fontFamily
        if let trackMapSettings = settings.overlays.first(where: { $0.id == TrackMapOverlayDefinition.definition.id }) {
            trackMapView?.internalOpacity = min(max(trackMapSettings.opacity, 0.2), 1.0)
            trackMapView?.showSectorBoundaries = optionBool(
                trackMapSettings,
                key: "track-map.sector-boundaries.enabled",
                defaultValue: true
            )
            for view in trackMapSectorDemoViews.values {
                view.showSectorBoundaries = optionBool(
                    trackMapSettings,
                    key: "track-map.sector-boundaries.enabled",
                    defaultValue: true
                )
            }
        }

        configureSimpleTelemetryView(flagsOverlayView, id: FlagsOverlayDefinition.definition.id, fontFamily: fontFamily)
        configureSimpleTelemetryView(sessionWeatherOverlayView, id: SessionWeatherOverlayDefinition.definition.id, fontFamily: fontFamily)
        configureSimpleTelemetryView(pitServiceOverlayView, id: PitServiceOverlayDefinition.definition.id, fontFamily: fontFamily)
        configureSimpleTelemetryView(inputStateOverlayView, id: InputStateOverlayDefinition.definition.id, fontFamily: fontFamily)
        if let radarSettings = settings.overlays.first(where: { $0.id == CarRadarOverlayDefinition.definition.id }) {
            carRadarView?.showMulticlassWarning = radarSettings.showRadarMulticlassWarning
            for demoView in radarCaptureDemoViews.values {
                for radarView in demoView.radarViews {
                    radarView.showMulticlassWarning = radarSettings.showRadarMulticlassWarning
                }
            }
        }
        carRadarView?.fontFamily = fontFamily
        for demoView in radarCaptureDemoViews.values {
            for radarView in demoView.radarViews {
                radarView.fontFamily = fontFamily
            }
        }

        if let gapSettings = settings.overlays.first(where: { $0.id == GapToLeaderOverlayDefinition.definition.id }) {
            gapToLeaderView?.carsAhead = gapSettings.classGapCarsAhead
            gapToLeaderView?.carsBehind = gapSettings.classGapCarsBehind
            for view in gapToLeaderDemoViews.values {
                view.carsAhead = gapSettings.classGapCarsAhead
                view.carsBehind = gapSettings.classGapCarsBehind
            }
        }
        gapToLeaderView?.fontFamily = fontFamily
        for view in gapToLeaderDemoViews.values {
            view.fontFamily = fontFamily
        }
    }

    private func configureSimpleTelemetryView(_ view: SimpleTelemetryOverlayView?, id: String, fontFamily: String) {
        guard let view else {
            return
        }

        view.fontFamily = fontFamily
        view.unitSystem = settings.general.unitSystem
        if id == FlagsOverlayDefinition.definition.id,
           let flagsSettings = settings.overlays.first(where: { $0.id == id }) {
            view.flagDisplayOptions = FlagDisplayOptions(
                showGreen: flagsSettings.flagsShowGreen,
                showBlue: flagsSettings.flagsShowBlue,
                showYellow: flagsSettings.flagsShowYellow,
                showCritical: flagsSettings.flagsShowCritical,
                showFinish: flagsSettings.flagsShowFinish
            )
        }
        if id == InputStateOverlayDefinition.definition.id,
           let inputSettings = settings.overlays.first(where: { $0.id == id }) {
            view.inputDisplayOptions = InputDisplayOptions(
                showThrottle: optionBool(inputSettings, key: "input-state.current.throttle", defaultValue: true),
                showBrake: optionBool(inputSettings, key: "input-state.current.brake", defaultValue: true),
                showClutch: optionBool(inputSettings, key: "input-state.current.clutch", defaultValue: true),
                showSteering: optionBool(inputSettings, key: "input-state.current.steering", defaultValue: true),
                showGear: optionBool(inputSettings, key: "input-state.current.gear", defaultValue: true),
                showSpeed: optionBool(inputSettings, key: "input-state.current.speed", defaultValue: true)
            )
        }
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

    private func refreshFuelOverlay(sessionKind: OverlaySessionKind?) {
        let definition = FuelCalculatorOverlayDefinition.definition
        guard shouldShow(definition: definition, sessionKind: sessionKind) else {
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        if fuelCalculatorView == nil {
            fuelCalculatorView = showOverlay(
                definition: definition,
                defaultOrigin: NSPoint(x: 24, y: 190)
            ) { [weak self] in
                FuelCalculatorView(historyQueryService: self?.historyQueryService ?? SessionHistoryQueryService(userHistoryRoot: AppPaths.historyRoot()))
            } as? FuelCalculatorView
        }

        applyScaleIfNeeded(definition: definition)
        overlayWindows[definition.id]?.orderFrontRegardless()
    }

    private func refreshRelativeOverlay(sessionKind: OverlaySessionKind?) {
        let definition = RelativeOverlayDefinition.definition
        guard shouldShow(definition: definition, sessionKind: sessionKind) else {
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        if relativeOverlayView == nil {
            relativeOverlayView = showOverlay(
                definition: definition,
                defaultOrigin: NSPoint(x: 24, y: 530)
            ) {
                RelativeOverlayView()
            } as? RelativeOverlayView
        }

        applyScaleIfNeeded(definition: definition)
        overlayWindows[definition.id]?.orderFrontRegardless()
    }

    private func refreshStandingsOverlay(sessionKind: OverlaySessionKind?) {
        let definition = StandingsOverlayDefinition.definition
        guard shouldShow(definition: definition, sessionKind: sessionKind) else {
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        if standingsOverlayView == nil {
            standingsOverlayView = showOverlay(
                definition: definition,
                defaultOrigin: defaultOrigin(definition: definition)
            ) {
                StandingsOverlayView()
            } as? StandingsOverlayView
        }

        applyScaleIfNeeded(definition: definition)
        overlayWindows[definition.id]?.orderFrontRegardless()
    }

    private func refreshTrackMapOverlay(sessionKind: OverlaySessionKind?) {
        let definition = TrackMapOverlayDefinition.definition
        guard shouldShow(definition: definition, sessionKind: sessionKind) else {
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        if trackMapView == nil {
            trackMapView = showOverlay(
                definition: definition,
                defaultOrigin: defaultOrigin(definition: definition)
            ) {
                TrackMapView()
            } as? TrackMapView
        }

        applyScaleIfNeeded(definition: definition)
        overlayWindows[definition.id]?.orderFrontRegardless()
    }

    private func refreshFlagsOverlay(sessionKind: OverlaySessionKind?) {
        if isSettingsWindowActive {
            overlayWindows[FlagsOverlayDefinition.definition.id]?.orderOut(nil)
            return
        }

        refreshSimpleTelemetryOverlay(
            definition: FlagsOverlayDefinition.definition,
            sessionKind: sessionKind,
            view: &flagsOverlayView,
            kind: .flags
        )
    }

    private func refreshSessionWeatherOverlay(sessionKind: OverlaySessionKind?) {
        refreshSimpleTelemetryOverlay(
            definition: SessionWeatherOverlayDefinition.definition,
            sessionKind: sessionKind,
            view: &sessionWeatherOverlayView,
            kind: .sessionWeather
        )
    }

    private func refreshPitServiceOverlay(sessionKind: OverlaySessionKind?) {
        refreshSimpleTelemetryOverlay(
            definition: PitServiceOverlayDefinition.definition,
            sessionKind: sessionKind,
            view: &pitServiceOverlayView,
            kind: .pitService
        )
    }

    private func refreshInputStateOverlay(sessionKind: OverlaySessionKind?) {
        refreshSimpleTelemetryOverlay(
            definition: InputStateOverlayDefinition.definition,
            sessionKind: sessionKind,
            view: &inputStateOverlayView,
            kind: .inputState
        )
    }

    private func refreshSimpleTelemetryOverlay(
        definition: OverlayDefinition,
        sessionKind: OverlaySessionKind?,
        view: inout SimpleTelemetryOverlayView?,
        kind: SimpleTelemetryOverlayKind
    ) {
        guard shouldShow(definition: definition, sessionKind: sessionKind) else {
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        if view == nil {
            view = showOverlay(
                definition: definition,
                defaultOrigin: defaultOrigin(definition: definition)
            ) {
                SimpleTelemetryOverlayView(kind: kind)
            } as? SimpleTelemetryOverlayView
        }

        applyScaleIfNeeded(definition: definition)
        overlayWindows[definition.id]?.orderFrontRegardless()
    }

    private func refreshCarRadarOverlay(sessionKind: OverlaySessionKind?) {
        let definition = CarRadarOverlayDefinition.definition
        guard radarCaptureDemoScenarios.isEmpty else {
            carRadarView?.settingsPreviewVisible = false
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        let settingsPreview = radarSettingsPreviewVisible
        guard settingsPreview || shouldShow(definition: definition, sessionKind: sessionKind) else {
            carRadarView?.settingsPreviewVisible = false
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        if carRadarView == nil {
            carRadarView = showOverlay(
                definition: definition,
                defaultOrigin: NSPoint(x: 650, y: 24)
            ) {
                CarRadarView()
            } as? CarRadarView
        }

        applyScaleIfNeeded(definition: definition)
        carRadarView?.settingsPreviewVisible = settingsPreview
        overlayWindows[definition.id]?.orderFrontRegardless()
    }

    private func refreshGapOverlay(sessionKind: OverlaySessionKind?) {
        let definition = GapToLeaderOverlayDefinition.definition
        guard shouldShow(definition: definition, sessionKind: sessionKind) else {
            overlayWindows[definition.id]?.orderOut(nil)
            return
        }

        if gapToLeaderView == nil {
            gapToLeaderView = showOverlay(
                definition: definition,
                defaultOrigin: NSPoint(x: 650, y: 260)
            ) {
                GapToLeaderView()
            } as? GapToLeaderView
        }

        applyScaleIfNeeded(definition: definition)
        overlayWindows[definition.id]?.orderFrontRegardless()
    }

    private func showOverlay(
        definition: OverlayDefinition,
        defaultOrigin: NSPoint = NSPoint(x: 24, y: 24),
        makeContentView: () -> NSView
    ) -> NSView? {
        let overlaySettings = settings.overlay(
            id: definition.id,
            defaultSize: definition.defaultSize,
            defaultOrigin: defaultOrigin,
            defaultEnabled: definition.id == SettingsOverlayDefinition.definition.id
        )

        let contentView = makeContentView()
        let window = makeOverlayWindow(
            contentView: contentView,
            definition: definition,
            settings: overlaySettings
        )
        window.frameDidChange = { [weak self, weak window] frame in
            self?.saveOverlayFrame(frame, overlayId: definition.id, window: window)
        }

        overlayWindows[definition.id] = window
        appliedScales[definition.id] = overlaySettings.scale
        window.orderFrontRegardless()
        return contentView
    }

    private func updateOverlay(
        _ name: String,
        showError: (String) -> Void,
        update: () throws -> Void
    ) {
        do {
            try update()
        } catch {
            let message = "\(name): \(error)"
            logOverlayError(name: name, message: message)
            showError(message)
        }
    }

    private func logOverlayError(name: String, message: String) {
        let now = Date()
        let shouldLog = lastOverlayErrors[name] != message
            || lastOverlayErrorDates[name].map { now.timeIntervalSince($0) > 30 } != false
        guard shouldLog else {
            return
        }

        lastOverlayErrors[name] = message
        lastOverlayErrorDates[name] = now
        logger.error("Overlay update failed: \(message)")
    }

    private func makeOverlayWindow(
        contentView: NSView,
        definition: OverlayDefinition,
        settings: OverlaySettings
    ) -> OverlayWindow {
        let isSettingsWindow = definition.id == SettingsOverlayDefinition.definition.id
        let size = NSSize(
            width: settings.width > 0 ? settings.width : definition.defaultSize.width,
            height: settings.height > 0 ? settings.height : definition.defaultSize.height
        )
        let window = OverlayWindow(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: isSettingsWindow
                ? [.titled, .closable, .miniaturizable]
                : [.borderless],
            backing: .buffered,
            defer: false
        )
        contentView.frame = NSRect(origin: .zero, size: size)
        window.contentView = contentView
        window.backgroundColor = isSettingsWindow ? .windowBackgroundColor : .clear
        window.isOpaque = isSettingsWindow
        window.level = isSettingsWindow ? .normal : (settings.alwaysOnTop ? .floating : .normal)
        setBaseOverlayOpacity(
            definition: definition,
            window: window,
            opacity: isSettingsWindow ? 1.0 : baseOpacity(definition: definition, settings: settings)
        )
        window.collectionBehavior = isSettingsWindow ? [] : [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.hasShadow = true
        window.ignoresMouseEvents = definition.id == FlagsOverlayDefinition.definition.id
        window.setFrameOrigin(overlayOrigin(settings: settings, size: size))
        return window
    }

    private func overlayOrigin(settings: OverlaySettings, size: NSSize) -> NSPoint {
        guard let screen = NSScreen.main else {
            return NSPoint(x: settings.x, y: settings.y)
        }

        let visibleFrame = screen.visibleFrame
        return NSPoint(
            x: visibleFrame.minX + settings.x,
            y: visibleFrame.maxY - settings.y - size.height
        )
    }

    private func saveOverlayFrame(_ frame: NSRect, overlayId: String, window: OverlayWindow?) {
        guard var overlaySettings = settings.overlays.first(where: { $0.id == overlayId }) else {
            return
        }

        if overlayId == SettingsOverlayDefinition.definition.id {
            return
        }

        if let visibleFrame = window?.screen?.visibleFrame ?? NSScreen.main?.visibleFrame {
            overlaySettings.x = frame.minX - visibleFrame.minX
            overlaySettings.y = visibleFrame.maxY - frame.maxY
        } else {
            overlaySettings.x = frame.minX
            overlaySettings.y = frame.minY
        }

        overlaySettings.width = frame.width
        overlaySettings.height = frame.height
        if overlayId == FlagsOverlayDefinition.definition.id {
            overlaySettings.screenId = nil
        }
        if let window {
            if overlayId != TrackMapOverlayDefinition.definition.id {
                overlaySettings.opacity = baseOverlayOpacities[overlayId] ?? Double(window.alphaValue)
            }
            overlaySettings.alwaysOnTop = window.level == .floating
        }

        settings.updateOverlay(overlaySettings)
        settingsStore.save(settings)
    }

    private func selectSettingsOverlayTab(_ overlayId: String?) {
        let previewVisible = overlayId == CarRadarOverlayDefinition.definition.id
            && settings.overlays.first(where: { $0.id == CarRadarOverlayDefinition.definition.id })?.enabled == true
        guard radarSettingsPreviewVisible != previewVisible else {
            return
        }

        radarSettingsPreviewVisible = previewVisible
        refreshOverlayVisibility()
        applyDisplaySettingsToOpenOverlays()
    }

    private func shouldShow(definition: OverlayDefinition, sessionKind: OverlaySessionKind?) -> Bool {
        guard let overlaySettings = settings.overlays.first(where: { $0.id == definition.id }),
              overlaySettings.enabled else {
            return false
        }

        if definition.id == FlagsOverlayDefinition.definition.id && sessionKind == nil {
            return false
        }

        switch sessionKind {
        case .test:
            return overlaySettings.showInTest
        case .practice:
            return overlaySettings.showInPractice
        case .qualifying:
            return overlaySettings.showInQualifying
        case .race:
            return overlaySettings.showInRace
        case nil:
            return true
        }
    }

    private var isSettingsWindowActive: Bool {
        guard let window = overlayWindows[SettingsOverlayDefinition.definition.id] else {
            return false
        }

        return window.isKeyWindow || window.isMainWindow
    }

    private func applyScaleIfNeeded(definition: OverlayDefinition) {
        guard var overlaySettings = settings.overlays.first(where: { $0.id == definition.id }),
              let window = overlayWindows[definition.id] else {
            return
        }
        defer {
            applyOpacityIfNeeded(definition: definition, settings: overlaySettings, window: window)
        }

        if definition.id == FlagsOverlayDefinition.definition.id {
            applyFlagsCompactPolicy(definition: definition, settings: &overlaySettings)
            let size = FlagsOverlayDefinition.resolveSize(overlaySettings)
            overlaySettings.width = size.width
            overlaySettings.height = size.height
            let origin = overlayOrigin(settings: overlaySettings, size: size)
            guard abs(window.frame.width - size.width) > 0.5
                || abs(window.frame.height - size.height) > 0.5
                || abs(window.frame.minX - origin.x) > 0.5
                || abs(window.frame.minY - origin.y) > 0.5 else {
                return
            }

            let frame = NSRect(origin: origin, size: size)
            window.setFrame(frame, display: true)
            window.contentView?.frame = NSRect(origin: .zero, size: size)
            settings.updateOverlay(overlaySettings)
            appliedScales[definition.id] = overlaySettings.scale
            return
        }

        overlaySettings.scale = min(max(overlaySettings.scale, 0.6), 2.0)
        let size = scaledOverlaySize(definition: definition, settings: overlaySettings)
        guard abs((appliedScales[definition.id] ?? -1) - overlaySettings.scale) > 0.001
            || abs(window.frame.width - size.width) > 0.5
            || abs(window.frame.height - size.height) > 0.5 else {
            return
        }

        var frame = window.frame
        let maxY = frame.maxY
        frame.size = size
        frame.origin.y = maxY - size.height
        window.setFrame(frame, display: true)
        window.contentView?.frame = NSRect(origin: .zero, size: size)
        overlaySettings.width = size.width
        overlaySettings.height = size.height
        settings.updateOverlay(overlaySettings)
        appliedScales[definition.id] = overlaySettings.scale
    }

    private func scaledOverlaySize(definition: OverlayDefinition, settings overlaySettings: OverlaySettings) -> NSSize {
        var size = NSSize(
            width: definition.defaultSize.width * overlaySettings.scale,
            height: definition.defaultSize.height * overlaySettings.scale
        )
        if let content = OverlayContentColumns.definition(for: definition.id) {
            let contentWidth = OverlayContentColumns.visibleColumnStates(
                for: content,
                settings: overlaySettings
            ).reduce(0) { $0 + $1.width }
            size.width = max(size.width, CGFloat(contentWidth + 28))
            size.height = max(size.height, CGFloat(content.nativeMinimumTableHeight + 64))
        }

        return size
    }

    private func resizeRelativeDesignV2Demo(settings relativeSettings: OverlaySettings) {
        guard let window = overlayWindows[relativeDesignV2DemoDefinition.id],
              let view = relativeDesignV2View else {
            return
        }

        let size = RelativeDesignV2OverlayView.demoSize(
            settings: relativeSettings,
            sessionKey: liveTelemetryStore.snapshot().combo.sessionKey
        )
        applyRelativeDesignV2Opacity(settings: relativeSettings)
        guard abs(window.frame.width - size.width) > 0.5
            || abs(window.frame.height - size.height) > 0.5 else {
            return
        }

        var frame = window.frame
        let maxY = frame.maxY
        frame.size = size
        frame.origin.y = maxY - size.height
        window.setFrame(frame, display: true)
        view.frame = NSRect(origin: .zero, size: size)
        appliedScales[relativeDesignV2DemoDefinition.id] = relativeSettings.scale
    }

    private func relativeCarsEachSide(_ overlaySettings: OverlaySettings) -> Int {
        min(max(max(overlaySettings.relativeCarsAhead, overlaySettings.relativeCarsBehind), 0), 8)
    }

    private func applyRelativeDesignV2Opacity(settings relativeSettings: OverlaySettings) {
        guard let window = overlayWindows[relativeDesignV2DemoDefinition.id] else {
            return
        }

        setBaseOverlayOpacity(
            definition: relativeDesignV2DemoDefinition,
            window: window,
            opacity: min(max(relativeSettings.opacity, 0.2), 1.0)
        )
    }

    private func applyDesignV2OverlaySuiteSettings(fontFamily: String) {
        guard !designV2OverlaySuiteViews.isEmpty else {
            return
        }

        for (kind, view) in designV2OverlaySuiteViews {
            let sourceDefinition = kind.sourceDefinition
            let sourceSettings = settings.overlay(
                id: sourceDefinition.id,
                defaultSize: sourceDefinition.defaultSize,
                defaultOrigin: defaultOrigin(definition: sourceDefinition),
                defaultEnabled: false
            )
            view.sourceSettings = sourceSettings
            view.fontFamily = fontFamily
            view.unitSystem = settings.general.unitSystem
            resizeDesignV2OverlaySuiteWindow(kind: kind, sourceSettings: sourceSettings)
            applyDesignV2OverlaySuiteOpacity(kind: kind, settings: sourceSettings)
        }
    }

    private func resizeDesignV2OverlaySuiteWindow(kind: DesignV2OverlayMockKind, sourceSettings: OverlaySettings) {
        let definition = designV2SuiteDefinition(
            kind: kind,
            size: designV2SuiteSize(kind: kind, sourceSettings: sourceSettings)
        )
        guard let window = overlayWindows[definition.id],
              let view = designV2OverlaySuiteViews[kind] else {
            return
        }

        let size = designV2SuiteSize(kind: kind, sourceSettings: sourceSettings)
        guard abs(window.frame.width - size.width) > 0.5
            || abs(window.frame.height - size.height) > 0.5 else {
            return
        }

        var frame = window.frame
        let maxY = frame.maxY
        frame.size = size
        frame.origin.y = maxY - size.height
        window.setFrame(frame, display: true)
        view.frame = NSRect(origin: .zero, size: size)
        appliedScales[definition.id] = sourceSettings.scale
    }

    private func applyDesignV2OverlaySuiteOpacity(kind: DesignV2OverlayMockKind, settings sourceSettings: OverlaySettings) {
        let definition = designV2SuiteDefinition(
            kind: kind,
            size: designV2SuiteSize(kind: kind, sourceSettings: sourceSettings)
        )
        guard let window = overlayWindows[definition.id] else {
            return
        }

        let opacity = kind.sourceDefinition.showOpacityControl
            ? min(max(sourceSettings.opacity, 0.2), 1.0)
            : 1.0
        setBaseOverlayOpacity(definition: definition, window: window, opacity: opacity)
    }

    private func applyOpacityIfNeeded(definition: OverlayDefinition, settings overlaySettings: OverlaySettings, window: OverlayWindow) {
        guard definition.showOpacityControl else {
            setBaseOverlayOpacity(definition: definition, window: window, opacity: baseOpacity(definition: definition, settings: overlaySettings))
            return
        }

        let opacity = min(max(overlaySettings.opacity, 0.2), 1.0)
        if definition.id == TrackMapOverlayDefinition.definition.id {
            setBaseOverlayOpacity(definition: definition, window: window, opacity: 1.0)
            trackMapView?.internalOpacity = opacity
            return
        }

        setBaseOverlayOpacity(definition: definition, window: window, opacity: opacity)
    }

    private func applyLiveTelemetryFade(liveSnapshot: LiveTelemetrySnapshot) {
        let now = Date()
        let elapsed = max(0, now.timeIntervalSince(lastFadeStepAtUtc))
        lastFadeStepAtUtc = now
        let liveAvailable = isLiveTelemetryAvailable(liveSnapshot)

        for definition in managedOverlayDefinitions {
            guard let window = overlayWindows[definition.id] else {
                continue
            }

            let previewKeepsVisible = definition.id == CarRadarOverlayDefinition.definition.id && radarSettingsPreviewVisible
            let target = definition.fadeWhenLiveTelemetryUnavailable && !liveAvailable && !previewKeepsVisible ? 0.0 : 1.0
            if liveTelemetryFadeTargets[definition.id] == nil {
                liveTelemetryFadeTargets[definition.id] = target
                liveTelemetryFadeAlphas[definition.id] = target
                applyEffectiveWindowOpacity(overlayId: definition.id, window: window)
                continue
            }

            liveTelemetryFadeTargets[definition.id] = target
            let current = liveTelemetryFadeAlphas[definition.id] ?? target
            let duration = target > current ? Self.telemetryFadeInSeconds : Self.telemetryFadeOutSeconds
            let delta = duration <= 0 ? 1 : elapsed / duration
            let next = Self.moveToward(current, target: target, delta: delta)
            liveTelemetryFadeAlphas[definition.id] = next
            applyEffectiveWindowOpacity(overlayId: definition.id, window: window)
        }
    }

    private func isLiveTelemetryAvailable(_ snapshot: LiveTelemetrySnapshot) -> Bool {
        guard snapshot.isConnected,
              snapshot.isCollecting,
              let lastUpdatedAtUtc = snapshot.lastUpdatedAtUtc else {
            return false
        }

        return Date().timeIntervalSince(lastUpdatedAtUtc) <= Self.liveTelemetryFreshnessSeconds
    }

    private func setBaseOverlayOpacity(definition: OverlayDefinition, window: OverlayWindow, opacity: Double) {
        baseOverlayOpacities[definition.id] = min(max(opacity, 0), 1)
        applyEffectiveWindowOpacity(overlayId: definition.id, window: window)
    }

    private func baseOpacity(definition: OverlayDefinition, settings overlaySettings: OverlaySettings) -> Double {
        if definition.id == TrackMapOverlayDefinition.definition.id || !definition.showOpacityControl {
            return 1.0
        }

        return min(max(overlaySettings.opacity, 0.2), 1.0)
    }

    private func applyEffectiveWindowOpacity(overlayId: String, window: OverlayWindow) {
        let base = baseOverlayOpacities[overlayId] ?? 1.0
        let fade = liveTelemetryFadeAlphas[overlayId] ?? 1.0
        let effective = min(max(base * fade, 0), 1)
        if abs(Double(window.alphaValue) - effective) > 0.001 {
            window.alphaValue = effective
        }
    }

    private static func moveToward(_ current: Double, target: Double, delta: Double) -> Double {
        if current < target {
            return min(target, current + delta)
        }

        return max(target, current - delta)
    }

    private func applyGapRaceOnlyPolicy(definition: OverlayDefinition, settings overlaySettings: inout OverlaySettings) {
        guard definition.id == GapToLeaderOverlayDefinition.definition.id else {
            return
        }

        overlaySettings.showInTest = false
        overlaySettings.showInPractice = false
        overlaySettings.showInQualifying = false
        overlaySettings.showInRace = true
        overlaySettings.gapRaceOnlyDefaultApplied = true
    }

    private func applyFlagsCompactPolicy(definition: OverlayDefinition, settings overlaySettings: inout OverlaySettings) {
        guard definition.id == FlagsOverlayDefinition.definition.id else {
            return
        }

        let hadPrimaryScreenDefault = overlaySettings.screenId == FlagsOverlayDefinition.primaryScreenDefaultId
        let hadFullScreenSize = overlaySettings.width > FlagsOverlayDefinition.maximumWidth
            || overlaySettings.height > FlagsOverlayDefinition.maximumHeight
            || (overlaySettings.width >= 900 && overlaySettings.height >= 500)
        let size = hadPrimaryScreenDefault || hadFullScreenSize
            ? definition.defaultSize
            : FlagsOverlayDefinition.resolveSize(overlaySettings)

        overlaySettings.scale = 1
        overlaySettings.width = size.width
        overlaySettings.height = size.height
        if hadPrimaryScreenDefault || hadFullScreenSize {
            let origin = defaultOrigin(definition: definition)
            overlaySettings.x = origin.x
            overlaySettings.y = origin.y
        }

        overlaySettings.screenId = nil
    }

    private func defaultOrigin(definition: OverlayDefinition) -> NSPoint {
        switch definition.id {
        case FuelCalculatorOverlayDefinition.definition.id:
            return NSPoint(x: 24, y: 190)
        case RelativeOverlayDefinition.definition.id:
            return NSPoint(x: 24, y: 530)
        case StandingsOverlayDefinition.definition.id:
            return NSPoint(x: 650, y: 620)
        case TrackMapOverlayDefinition.definition.id:
            return NSPoint(x: 650, y: 400)
        case GarageCoverOverlayDefinition.definition.id:
            return NSPoint(x: 0, y: 0)
        case FlagsOverlayDefinition.definition.id:
            let visibleFrame = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1920, height: 1080)
            return NSPoint(
                x: max(0, (visibleFrame.width - definition.defaultSize.width) / 2),
                y: 96
            )
        case SessionWeatherOverlayDefinition.definition.id:
            return NSPoint(x: 1070, y: 280)
        case PitServiceOverlayDefinition.definition.id:
            return NSPoint(x: 1070, y: 570)
        case InputStateOverlayDefinition.definition.id:
            return NSPoint(x: 1070, y: 820)
        case CarRadarOverlayDefinition.definition.id:
            return NSPoint(x: 650, y: 24)
        case GapToLeaderOverlayDefinition.definition.id:
            return NSPoint(x: 650, y: 260)
        default:
            return NSPoint(x: 24, y: 24)
        }
    }

    private func centeredDefaultOrigin(definition: OverlayDefinition) -> NSPoint {
        guard let visibleFrame = NSScreen.main?.visibleFrame else {
            return NSPoint(x: 24, y: 24)
        }

        return NSPoint(
            x: max(0, (visibleFrame.width - definition.defaultSize.width) / 2),
            y: max(0, (visibleFrame.height - definition.defaultSize.height) / 2)
        )
    }

    private func updateRadarCaptureDemoWindows(capturedAtUtc: Date) {
        guard !radarCaptureDemoScenarios.isEmpty else {
            return
        }

        let playbackElapsedSeconds = radarCaptureDemoStartedAtUtc.map {
            capturedAtUtc.timeIntervalSince($0)
        } ?? 0
        radarCaptureDemoSequence += 1
        for (index, scenario) in radarCaptureDemoScenarios.enumerated() {
            let overlayId = radarCaptureDemoOverlayId(index: index)
            guard let contentView = radarCaptureDemoViews[overlayId] else {
                continue
            }

            contentView.updatePlayback(
                elapsedSeconds: loopedPlaybackElapsedSeconds(playbackElapsedSeconds),
                durationSeconds: RadarCaptureScenario.playbackDurationSeconds
            )
            let snapshot = scenario.makeSnapshot(
                capturedAtUtc: capturedAtUtc,
                startedAtUtc: radarCaptureDemoStartedAtUtc,
                sequence: radarCaptureDemoSequence * 100 + index,
                playbackElapsedSeconds: playbackElapsedSeconds
            )
            contentView.update(with: snapshot)
            liveOverlayDiagnosticsRecorder?.record(snapshot)
        }
    }

    private func updateGapToLeaderDemoWindows(capturedAtUtc: Date) {
        guard gapToLeaderDemoActive else {
            return
        }

        for playback in gapToLeaderDemoPlaybacks.values {
            playback.update(capturedAtUtc: capturedAtUtc)
        }
        for playback in rawPracticeGapDemoPlaybacks.values {
            playback.update(capturedAtUtc: capturedAtUtc)
        }
    }

    private func updateTrackMapSectorDemoWindows(capturedAtUtc: Date) {
        guard trackMapSectorDemoActive else {
            return
        }

        for playback in trackMapSectorDemoPlaybacks.values {
            playback.update(capturedAtUtc: capturedAtUtc)
        }
    }

    private func closeRadarCaptureDemoWindows() {
        for overlayId in Array(radarCaptureDemoViews.keys) {
            overlayWindows[overlayId]?.close()
            overlayWindows.removeValue(forKey: overlayId)
            appliedScales.removeValue(forKey: overlayId)
        }

        radarCaptureDemoViews.removeAll()
    }

    private func closeGapToLeaderDemoWindows() {
        for overlayId in Array(gapToLeaderDemoViews.keys) {
            overlayWindows[overlayId]?.close()
            overlayWindows.removeValue(forKey: overlayId)
            appliedScales.removeValue(forKey: overlayId)
        }

        gapToLeaderDemoViews.removeAll()
        gapToLeaderDemoPlaybacks.removeAll()
        rawPracticeGapDemoPlaybacks.removeAll()
    }

    private func closeTrackMapSectorDemoWindows() {
        for overlayId in Array(trackMapSectorDemoViews.keys) {
            overlayWindows[overlayId]?.close()
            overlayWindows.removeValue(forKey: overlayId)
            appliedScales.removeValue(forKey: overlayId)
            baseOverlayOpacities.removeValue(forKey: overlayId)
        }

        trackMapSectorDemoViews.removeAll()
        trackMapSectorDemoPlaybacks.removeAll()
    }

    private func closeDesignV2ComponentDemoWindows() {
        for overlayId in designV2ComponentDemoOverlayIds {
            overlayWindows[overlayId]?.close()
            overlayWindows.removeValue(forKey: overlayId)
            appliedScales.removeValue(forKey: overlayId)
            baseOverlayOpacities.removeValue(forKey: overlayId)
        }

        designV2ComponentDemoOverlayIds.removeAll()
    }

    private func closeDesignV2OverlaySuiteWindows() {
        for overlayId in designV2OverlaySuiteOverlayIds {
            overlayWindows[overlayId]?.close()
            overlayWindows.removeValue(forKey: overlayId)
            appliedScales.removeValue(forKey: overlayId)
            baseOverlayOpacities.removeValue(forKey: overlayId)
        }

        designV2OverlaySuiteViews.removeAll()
        designV2OverlaySuiteOverlayIds.removeAll()
    }

    private func hideManagedOverlayWindowsForDemo() {
        for definition in managedOverlayDefinitions {
            overlayWindows[definition.id]?.orderOut(nil)
        }
    }

    private func radarCaptureDemoOverlayId(index: Int) -> String {
        "car-radar-capture-demo-\(index)"
    }

    private func gapToLeaderDemoOverlayId(index: Int) -> String {
        "gap-to-leader-demo-\(index)"
    }

    private func loopedPlaybackElapsedSeconds(_ elapsedSeconds: TimeInterval) -> TimeInterval {
        let wrapped = elapsedSeconds.truncatingRemainder(dividingBy: RadarCaptureScenario.playbackDurationSeconds)
        return wrapped >= 0 ? wrapped : wrapped + RadarCaptureScenario.playbackDurationSeconds
    }

    private func radarCaptureDemoWindowSize(variantCount: Int) -> NSSize {
        let radarSize = CarRadarOverlayDefinition.definition.defaultSize
        let count = max(1, variantCount)
        let gap: CGFloat = count > 1 ? 18 : 0
        let variantLabelHeight: CGFloat = count > 1 ? 22 : 0
        return NSSize(
            width: radarSize.width * CGFloat(count) + gap * CGFloat(count - 1),
            height: radarSize.height + 28 + variantLabelHeight
        )
    }

    private func radarCaptureDemoOrigin(index: Int, variantCount: Int) -> NSPoint {
        let size = radarCaptureDemoWindowSize(variantCount: variantCount)
        let gap = 18.0
        let margin = 24.0
        let visibleWidth = NSScreen.main?.visibleFrame.width ?? 1440
        let availableColumns = max(1, Int((visibleWidth - margin * 2 + gap) / (size.width + gap)))
        let columns = max(1, min(2, availableColumns))
        let column = index % columns
        let row = index / columns

        return NSPoint(
            x: margin + Double(column) * (size.width + gap),
            y: margin + Double(row) * (size.height + gap)
        )
    }

    private func gapToLeaderDemoOrigin(index: Int, count: Int) -> NSPoint {
        let size = GapToLeaderOverlayDefinition.definition.defaultSize
        let gap = 18.0
        let margin = 24.0
        let offsetX = Self.environmentDouble("TMR_MAC_GAP_DEMO_OFFSET_X") ?? 0
        let offsetY = Self.environmentDouble("TMR_MAC_GAP_DEMO_OFFSET_Y") ?? 0
        let visibleWidth = NSScreen.main?.visibleFrame.width ?? 1440
        let availableColumns = max(1, Int((visibleWidth - margin * 2 + gap) / (size.width + gap)))
        let columns = max(1, min(count, min(2, availableColumns)))
        let column = index % columns
        let row = index / columns

        return NSPoint(
            x: margin + offsetX + Double(column) * (size.width + gap),
            y: margin + offsetY + Double(row) * (size.height + gap)
        )
    }

    private static func environmentDouble(_ key: String) -> Double? {
        guard let rawValue = ProcessInfo.processInfo.environment[key],
              let value = Double(rawValue),
              value.isFinite else {
            return nil
        }

        return value
    }

    private static func environmentString(_ key: String) -> String? {
        guard let value = ProcessInfo.processInfo.environment[key]?.trimmingCharacters(in: .whitespacesAndNewlines),
              !value.isEmpty else {
            return nil
        }

        return value
    }

    private func configureGapToLeaderDemoView(_ view: GapToLeaderView) {
        view.fontFamily = OverlayTheme.defaultFontFamily
        if let gapSettings = settings.overlays.first(where: { $0.id == GapToLeaderOverlayDefinition.definition.id }) {
            view.carsAhead = gapSettings.classGapCarsAhead
            view.carsBehind = gapSettings.classGapCarsBehind
        }
    }

    private func currentSessionKind(liveSnapshot: LiveTelemetrySnapshot) -> OverlaySessionKind? {
        classifySession(liveSnapshot.combo.sessionKey)
    }

    private func classifySession(_ value: String?) -> OverlaySessionKind? {
        guard let value else {
            return nil
        }

        let normalized = value.lowercased()
        if normalized.contains("test") {
            return .test
        }

        if normalized.contains("practice") {
            return .practice
        }

        if normalized.contains("qual") {
            return .qualifying
        }

        if normalized.contains("race") {
            return .race
        }

        return nil
    }

    private enum OverlaySessionKind {
        case test
        case practice
        case qualifying
        case race
    }
}

private final class RadarCaptureDemoView: NSView {
    let radarViews: [CarRadarView]
    private let titleLabel: NSTextField
    private let variantLabels: [NSTextField]
    private let title: String
    private let preferredRadarSize: NSSize

    init(title: String, radarSize: NSSize, colorVariants: [RadarCaptureDemoColorVariant]) {
        self.title = title
        preferredRadarSize = radarSize
        let resolvedVariants = colorVariants.isEmpty ? [.neutralProximity, .classColors, .classColorToAlertRed] : colorVariants
        radarViews = resolvedVariants.map { variant in
            let view = CarRadarView(frame: NSRect(origin: .zero, size: radarSize))
            view.colorMode = variant.colorMode
            return view
        }
        variantLabels = resolvedVariants.map { variant in
            let label = NSTextField(labelWithString: variant.title)
            label.font = NSFont.systemFont(ofSize: 11, weight: .medium)
            label.textColor = NSColor.black
            label.alignment = .center
            label.lineBreakMode = .byTruncatingTail
            return label
        }
        titleLabel = NSTextField(labelWithString: title)
        let variantGap: CGFloat = resolvedVariants.count > 1 ? 18 : 0
        let variantLabelHeight: CGFloat = resolvedVariants.count > 1 ? 22 : 0
        super.init(frame: NSRect(
            origin: .zero,
            size: NSSize(
                width: radarSize.width * CGFloat(resolvedVariants.count) + variantGap * CGFloat(resolvedVariants.count - 1),
                height: radarSize.height + 28 + variantLabelHeight
            )
        ))

        wantsLayer = true
        layer?.backgroundColor = NSColor.clear.cgColor
        titleLabel.font = NSFont.systemFont(ofSize: 12, weight: .semibold)
        titleLabel.textColor = NSColor.black
        titleLabel.alignment = .center
        titleLabel.lineBreakMode = .byTruncatingTail
        for radarView in radarViews {
            addSubview(radarView)
        }
        for variantLabel in variantLabels where radarViews.count > 1 {
            addSubview(variantLabel)
        }
        addSubview(titleLabel)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    func updatePlayback(elapsedSeconds: TimeInterval, durationSeconds: TimeInterval) {
        titleLabel.stringValue = String(format: "%@  t+%.1f/%.0fs", title, elapsedSeconds, durationSeconds)
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        for radarView in radarViews {
            radarView.update(with: snapshot)
        }
    }

    override func layout() {
        super.layout()
        let titleHeight: CGFloat = 24
        let variantLabelHeight: CGFloat = radarViews.count > 1 ? 22 : 0
        titleLabel.frame = NSRect(
            x: 6,
            y: bounds.height - titleHeight,
            width: max(0, bounds.width - 12),
            height: titleHeight
        )
        let radarHeight = min(max(0, bounds.height - titleHeight - variantLabelHeight), preferredRadarSize.height)
        let radarWidth = min(
            preferredRadarSize.width,
            max(0, (bounds.width - CGFloat(max(0, radarViews.count - 1)) * 18) / CGFloat(max(1, radarViews.count)))
        )
        let gap: CGFloat = radarViews.count > 1 ? 18 : 0
        let totalWidth = radarWidth * CGFloat(radarViews.count) + gap * CGFloat(max(0, radarViews.count - 1))
        var x = (bounds.width - totalWidth) / 2

        for index in radarViews.indices {
            radarViews[index].frame = NSRect(
                x: x,
                y: 0,
                width: radarWidth,
                height: radarHeight
            )

            if radarViews.count > 1 {
                variantLabels[index].frame = NSRect(
                    x: x,
                    y: radarHeight,
                    width: radarWidth,
                    height: variantLabelHeight
                )
            }

            x += radarWidth + gap
        }
    }
}
