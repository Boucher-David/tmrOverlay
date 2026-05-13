import Foundation

struct LiveTelemetrySnapshot {
    var isConnected: Bool
    var isCollecting: Bool
    var sourceId: String?
    var startedAtUtc: Date?
    var lastUpdatedAtUtc: Date?
    var sequence: Int
    var combo: HistoricalComboIdentity
    var latestFrame: MockLiveTelemetryFrame?
    var fuel: LiveFuelSnapshot
    var proximity: LiveProximitySnapshot
    var leaderGap: LiveLeaderGapSnapshot
    var completedStintCount: Int
    var models: LiveRaceModels = .empty

    static let empty = LiveTelemetrySnapshot(
        isConnected: false,
        isCollecting: false,
        sourceId: nil,
        startedAtUtc: nil,
        lastUpdatedAtUtc: nil,
        sequence: 0,
        combo: .mockNurburgringMercedesRace,
        latestFrame: nil,
        fuel: .unavailable,
        proximity: .unavailable,
        leaderGap: .unavailable,
        completedStintCount: 0
    )
}

struct LiveFuelSnapshot {
    var hasValidFuel: Bool
    var source: String
    var fuelLevelLiters: Double?
    var fuelLevelPercent: Double?
    var fuelUsePerHourLiters: Double?
    var fuelPerLapLiters: Double?
    var lapTimeSeconds: Double?
    var lapTimeSource: String
    var estimatedMinutesRemaining: Double?
    var estimatedLapsRemaining: Double?
    var confidence: String

    static let unavailable = LiveFuelSnapshot(
        hasValidFuel: false,
        source: "unavailable",
        fuelLevelLiters: nil,
        fuelLevelPercent: nil,
        fuelUsePerHourLiters: nil,
        fuelPerLapLiters: nil,
        lapTimeSeconds: nil,
        lapTimeSource: "unavailable",
        estimatedMinutesRemaining: nil,
        estimatedLapsRemaining: nil,
        confidence: "none"
    )

    static func from(_ frame: MockLiveTelemetryFrame) -> LiveFuelSnapshot {
        guard frame.fuelLevelLiters.isFinite, frame.fuelLevelLiters > 0 else {
            return .unavailable
        }

        let minutesRemaining = frame.fuelUsePerHourLiters > 0
            ? frame.fuelLevelLiters / frame.fuelUsePerHourLiters * 60
            : nil
        let fuelPerLap = frame.fuelUsePerHourLiters * frame.estimatedLapSeconds / 3600
        let lapsRemaining = fuelPerLap > 0 ? frame.fuelLevelLiters / fuelPerLap : nil

        return LiveFuelSnapshot(
            hasValidFuel: true,
            source: "mac-mock-scalar",
            fuelLevelLiters: frame.fuelLevelLiters,
            fuelLevelPercent: frame.fuelLevelPercent,
            fuelUsePerHourLiters: frame.fuelUsePerHourLiters,
            fuelPerLapLiters: fuelPerLap > 0 ? fuelPerLap : nil,
            lapTimeSeconds: frame.estimatedLapSeconds,
            lapTimeSource: "mock estimate",
            estimatedMinutesRemaining: minutesRemaining,
            estimatedLapsRemaining: lapsRemaining,
            confidence: frame.fuelUsePerHourLiters > 0 ? "live" : "level-only"
        )
    }
}

struct LiveProximitySnapshot {
    private static let closeRadarRangeSeconds = 2.0
    private static let multiclassWarningRangeSeconds = 25.0

    var hasData: Bool
    var carLeftRight: Int?
    var referenceCarClass: Int?
    var sideStatus: String
    var hasCarLeft: Bool
    var hasCarRight: Bool
    var nearbyCars: [LiveProximityCar]
    var nearestAhead: LiveProximityCar?
    var nearestBehind: LiveProximityCar?
    var multiclassApproaches: [LiveMulticlassApproach]
    var strongestMulticlassApproach: LiveMulticlassApproach?
    var sideOverlapWindowSeconds: Double

    static let unavailable = LiveProximitySnapshot(
        hasData: false,
        carLeftRight: nil,
        referenceCarClass: nil,
        sideStatus: "waiting",
        hasCarLeft: false,
        hasCarRight: false,
        nearbyCars: [],
        nearestAhead: nil,
        nearestBehind: nil,
        multiclassApproaches: [],
        strongestMulticlassApproach: nil,
        sideOverlapWindowSeconds: 0.22
    )

    static func from(_ frame: MockLiveTelemetryFrame) -> LiveProximitySnapshot {
        if !frame.capturedCars.isEmpty, let replaySnapshot = fromCapturedReplay(frame) {
            return replaySnapshot
        }

        guard frame.isOnTrack,
              !frame.isInGarage,
              !frame.onPitRoad,
              frame.focusCarIdx == nil || frame.focusCarIdx == frame.playerCarIdx
        else {
            return .unavailable
        }

        let phase = frame.sessionTime.truncatingRemainder(dividingBy: 18) / 18
        let aheadSeconds = 2.4 + sin(phase * Double.pi * 2) * 1.1
        let behindSeconds = -3.2 + cos(phase * Double.pi * 2) * 1.4
        let multiclassSeconds = -18.0 + sin(frame.sessionTime / 11) * 3.0
        let cars = [
            LiveProximityCar(
                carIdx: 12,
                relativeLaps: aheadSeconds / frame.estimatedLapSeconds,
                relativeSeconds: aheadSeconds,
                relativeMeters: nil,
                overallPosition: 6,
                classPosition: 5,
                carClass: 4098,
                carClassColorHex: "#FFDA59",
                onPitRoad: false
            ),
            LiveProximityCar(
                carIdx: 14,
                relativeLaps: behindSeconds / frame.estimatedLapSeconds,
                relativeSeconds: behindSeconds,
                relativeMeters: nil,
                overallPosition: 8,
                classPosition: 7,
                carClass: 4098,
                carClassColorHex: "#FFDA59",
                onPitRoad: false
            ),
            LiveProximityCar(
                carIdx: 51,
                relativeLaps: multiclassSeconds / frame.estimatedLapSeconds,
                relativeSeconds: multiclassSeconds,
                relativeMeters: nil,
                overallPosition: 3,
                classPosition: 1,
                carClass: 4099,
                carClassColorHex: "#33CEFF",
                onPitRoad: false,
                lapDeltaToReference: -1
            )
        ].sorted { abs($0.relativeLaps) < abs($1.relativeLaps) }
        let approaches = cars
            .filter {
                guard let seconds = $0.relativeSeconds, seconds.isFinite else {
                    return false
                }

                return $0.carClass != nil
                    && $0.carClass != 4098
                    && seconds < -closeRadarRangeSeconds
                    && seconds >= -multiclassWarningRangeSeconds
            }
            .map {
                let seconds = abs($0.relativeSeconds ?? multiclassWarningRangeSeconds)
                let range = multiclassWarningRangeSeconds - closeRadarRangeSeconds
                let urgency = 1 - min(max((seconds - closeRadarRangeSeconds) / range, 0), 1)
                return LiveMulticlassApproach(
                    carIdx: $0.carIdx,
                    carClass: $0.carClass,
                    relativeLaps: $0.relativeLaps,
                    relativeSeconds: $0.relativeSeconds,
                    closingRateSecondsPerSecond: 0.7,
                    urgency: urgency
                )
            }

        return LiveProximitySnapshot(
            hasData: true,
            carLeftRight: frame.carLeftRight,
            referenceCarClass: 4098,
            sideStatus: sideStatus(frame.carLeftRight),
            hasCarLeft: hasCarLeft(frame.carLeftRight),
            hasCarRight: hasCarRight(frame.carLeftRight),
            nearbyCars: cars,
            nearestAhead: cars.filter { $0.relativeLaps > 0 }.min { $0.relativeLaps < $1.relativeLaps },
            nearestBehind: cars.filter { $0.relativeLaps < 0 }.max { $0.relativeLaps < $1.relativeLaps },
            multiclassApproaches: approaches,
            strongestMulticlassApproach: approaches.max { $0.urgency < $1.urgency },
            sideOverlapWindowSeconds: 0.22
        )
    }

    private static func fromCapturedReplay(_ frame: MockLiveTelemetryFrame) -> LiveProximitySnapshot? {
        guard frame.isOnTrack,
              !frame.isInGarage,
              let reference = frame.capturedReferenceCar,
              let referenceProgress = reference.trackProgress,
              frame.focusCarIdx == nil || frame.focusCarIdx == frame.playerCarIdx else {
            return nil
        }

        let lapSeconds = frame.estimatedLapSeconds.isFinite && frame.estimatedLapSeconds > 0
            ? frame.estimatedLapSeconds
            : FourHourRacePreview.medianLapSeconds
        let cars = frame.capturedCars
            .filter { $0.carIdx != reference.carIdx && $0.trackProgress != nil }
            .map { car -> LiveProximityCar in
                let relativeLaps = normalizedRelativeLaps((car.trackProgress ?? referenceProgress) - referenceProgress)
                return LiveProximityCar(
                    carIdx: car.carIdx,
                    relativeLaps: relativeLaps,
                    relativeSeconds: relativeLaps * lapSeconds,
                    relativeMeters: nil,
                    overallPosition: car.overallPosition,
                    classPosition: car.classPosition,
                    carClass: car.carClass,
                    carClassColorHex: car.carClassColorHex,
                    onPitRoad: car.onPitRoad,
                    driverName: car.driverName,
                    carNumber: car.carNumber,
                    carClassName: car.carClassName,
                    lapDeltaToReference: lapDelta(car, reference)
                )
            }
            .filter {
                guard let seconds = $0.relativeSeconds, seconds.isFinite else {
                    return false
                }

                return abs(seconds) <= max(45, lapSeconds * 0.18)
            }
            .sorted { abs($0.relativeLaps) < abs($1.relativeLaps) }

        let approaches = cars
            .filter {
                guard let seconds = $0.relativeSeconds, seconds.isFinite else {
                    return false
                }

                return reference.carClass != nil
                    && $0.carClass != nil
                    && $0.carClass != reference.carClass
                    && seconds < -closeRadarRangeSeconds
                    && seconds >= -multiclassWarningRangeSeconds
            }
            .map {
                let seconds = abs($0.relativeSeconds ?? multiclassWarningRangeSeconds)
                let range = multiclassWarningRangeSeconds - closeRadarRangeSeconds
                let urgency = 1 - min(max((seconds - closeRadarRangeSeconds) / range, 0), 1)
                return LiveMulticlassApproach(
                    carIdx: $0.carIdx,
                    carClass: $0.carClass,
                    relativeLaps: $0.relativeLaps,
                    relativeSeconds: $0.relativeSeconds,
                    closingRateSecondsPerSecond: nil,
                    urgency: urgency
                )
            }

        return LiveProximitySnapshot(
            hasData: true,
            carLeftRight: frame.carLeftRight,
            referenceCarClass: reference.carClass,
            sideStatus: sideStatus(frame.carLeftRight),
            hasCarLeft: hasCarLeft(frame.carLeftRight),
            hasCarRight: hasCarRight(frame.carLeftRight),
            nearbyCars: cars,
            nearestAhead: cars.filter { $0.relativeLaps > 0 }.min { $0.relativeLaps < $1.relativeLaps },
            nearestBehind: cars.filter { $0.relativeLaps < 0 }.max { $0.relativeLaps < $1.relativeLaps },
            multiclassApproaches: approaches,
            strongestMulticlassApproach: approaches.max { $0.urgency < $1.urgency },
            sideOverlapWindowSeconds: 0.22
        )
    }

    private static func normalizedRelativeLaps(_ raw: Double) -> Double {
        guard raw.isFinite else {
            return 0
        }

        var value = raw
        while value > 0.5 {
            value -= 1
        }
        while value < -0.5 {
            value += 1
        }
        return value
    }

    private static func lapDelta(_ car: CapturedReplayCar, _ reference: CapturedReplayCar) -> Int? {
        guard let carLap = completedLap(car),
              let referenceLap = completedLap(reference) else {
            return nil
        }

        return carLap - referenceLap
    }

    private static func completedLap(_ car: CapturedReplayCar) -> Int? {
        if let lapCompleted = car.lapCompleted, lapCompleted >= 0 {
            return lapCompleted
        }

        guard let progress = car.trackProgress,
              progress.isFinite,
              progress >= 0 else {
            return nil
        }

        return Int(progress.rounded(.down))
    }

    private static func sideStatus(_ carLeftRight: Int?) -> String {
        switch carLeftRight {
        case 2:
            return "left"
        case 3:
            return "right"
        case 4:
            return "both sides"
        case 5:
            return "two left"
        case 6:
            return "two right"
        case 1:
            return "clear"
        case 0:
            return "off"
        default:
            return "waiting"
        }
    }

    private static func hasCarLeft(_ carLeftRight: Int?) -> Bool {
        carLeftRight == 2 || carLeftRight == 4 || carLeftRight == 5
    }

    private static func hasCarRight(_ carLeftRight: Int?) -> Bool {
        carLeftRight == 3 || carLeftRight == 4 || carLeftRight == 6
    }
}

struct LiveProximityCar {
    var carIdx: Int
    var relativeLaps: Double
    var relativeSeconds: Double?
    var relativeMeters: Double?
    var overallPosition: Int?
    var classPosition: Int?
    var carClass: Int?
    var carClassColorHex: String?
    var onPitRoad: Bool?
    var driverName: String?
    var carNumber: String?
    var carClassName: String?
    var lapDeltaToReference: Int?

    init(
        carIdx: Int,
        relativeLaps: Double,
        relativeSeconds: Double?,
        relativeMeters: Double?,
        overallPosition: Int?,
        classPosition: Int?,
        carClass: Int?,
        carClassColorHex: String? = nil,
        onPitRoad: Bool?,
        driverName: String? = nil,
        carNumber: String? = nil,
        carClassName: String? = nil,
        lapDeltaToReference: Int? = nil
    ) {
        self.carIdx = carIdx
        self.relativeLaps = relativeLaps
        self.relativeSeconds = relativeSeconds
        self.relativeMeters = relativeMeters
        self.overallPosition = overallPosition
        self.classPosition = classPosition
        self.carClass = carClass
        self.carClassColorHex = carClassColorHex
        self.onPitRoad = onPitRoad
        self.driverName = driverName
        self.carNumber = carNumber
        self.carClassName = carClassName
        self.lapDeltaToReference = lapDeltaToReference
    }
}

struct LiveMulticlassApproach {
    var carIdx: Int
    var carClass: Int?
    var relativeLaps: Double
    var relativeSeconds: Double?
    var closingRateSecondsPerSecond: Double?
    var urgency: Double
}

struct LiveRaceModels {
    var trackMap: LiveTrackMapModel
    var raceProjection: LiveRaceProjectionModel = .empty

    static let empty = LiveRaceModels(trackMap: .empty)
}

struct LiveRaceProjectionModel {
    var hasData: Bool
    var overallLeaderPaceSeconds: Double?
    var overallLeaderPaceSource: String
    var overallLeaderPaceConfidence: Double
    var referenceClassPaceSeconds: Double?
    var referenceClassPaceSource: String
    var referenceClassPaceConfidence: Double
    var teamPaceSeconds: Double?
    var teamPaceSource: String
    var teamPaceConfidence: Double
    var estimatedFinishLap: Double?
    var estimatedTeamLapsRemaining: Double?
    var estimatedTeamLapsRemainingSource: String
    var classProjections: [LiveClassRaceProjection]

    static let empty = LiveRaceProjectionModel(
        hasData: false,
        overallLeaderPaceSeconds: nil,
        overallLeaderPaceSource: "unavailable",
        overallLeaderPaceConfidence: 0,
        referenceClassPaceSeconds: nil,
        referenceClassPaceSource: "unavailable",
        referenceClassPaceConfidence: 0,
        teamPaceSeconds: nil,
        teamPaceSource: "unavailable",
        teamPaceConfidence: 0,
        estimatedFinishLap: nil,
        estimatedTeamLapsRemaining: nil,
        estimatedTeamLapsRemainingSource: "unavailable",
        classProjections: []
    )
}

struct LiveClassRaceProjection {
    var carClass: Int?
    var className: String
    var paceSeconds: Double?
    var paceSource: String
    var paceConfidence: Double
    var estimatedLapsRemaining: Double?
    var estimatedLapsRemainingSource: String
}

enum LiveTrackSectorHighlights {
    static let none = "none"
    static let personalBest = "personal-best"
    static let bestLap = "best-lap"
}

struct LiveTrackMapModel {
    var hasSectors: Bool
    var hasLiveTiming: Bool
    var quality: String
    var sectors: [LiveTrackSectorSegment]

    static let empty = LiveTrackMapModel(
        hasSectors: false,
        hasLiveTiming: false,
        quality: "unavailable",
        sectors: []
    )
}

struct LiveTrackSectorSegment {
    var sectorNum: Int
    var startPct: Double
    var endPct: Double
    var highlight: String
}

struct LiveLeaderGapSnapshot {
    var hasData: Bool
    var referenceOverallPosition: Int?
    var referenceClassPosition: Int?
    var overallLeaderCarIdx: Int?
    var classLeaderCarIdx: Int?
    var overallLeaderGap: LiveGapValue
    var classLeaderGap: LiveGapValue
    var classCars: [LiveClassGapCar]

    static let unavailable = LiveLeaderGapSnapshot(
        hasData: false,
        referenceOverallPosition: nil,
        referenceClassPosition: nil,
        overallLeaderCarIdx: nil,
        classLeaderCarIdx: nil,
        overallLeaderGap: .unavailable,
        classLeaderGap: .unavailable,
        classCars: []
    )

    static func from(_ frame: MockLiveTelemetryFrame) -> LiveLeaderGapSnapshot {
        if !frame.capturedCars.isEmpty, let replaySnapshot = fromCapturedReplay(frame) {
            return replaySnapshot
        }

        let overall = makeGap(
            position: frame.teamPosition,
            f2Position: frame.teamPosition,
            leaderCarIdx: 1,
            referenceCarIdx: FourHourRacePreview.teamCarIdx,
            referenceF2TimeSeconds: frame.teamF2TimeSeconds,
            leaderF2TimeSeconds: frame.leaderF2TimeSeconds
        )
        let classGap = makeGap(
            position: frame.teamClassPosition,
            f2Position: frame.teamPosition,
            leaderCarIdx: FourHourRacePreview.classLeaderCarIdx,
            referenceCarIdx: FourHourRacePreview.teamCarIdx,
            referenceF2TimeSeconds: frame.teamF2TimeSeconds,
            leaderF2TimeSeconds: frame.classLeaderF2TimeSeconds
        )
        let classCars = makeClassCars(frame: frame, referenceClassGap: classGap)

        return LiveLeaderGapSnapshot(
            hasData: overall.hasData || classGap.hasData,
            referenceOverallPosition: frame.teamPosition,
            referenceClassPosition: frame.teamClassPosition,
            overallLeaderCarIdx: 1,
            classLeaderCarIdx: classCars.first(where: { $0.isClassLeader })?.carIdx,
            overallLeaderGap: overall,
            classLeaderGap: classGap,
            classCars: classCars
        )
    }

    private static func makeGap(
        position: Int?,
        f2Position: Int?,
        leaderCarIdx: Int?,
        referenceCarIdx: Int,
        referenceF2TimeSeconds: Double?,
        leaderF2TimeSeconds: Double?
    ) -> LiveGapValue {
        if position == 1 || leaderCarIdx == referenceCarIdx {
            return LiveGapValue(hasData: true, isLeader: true, seconds: 0, laps: 0, source: "position")
        }

        if let referenceF2TimeSeconds,
           referenceF2TimeSeconds.isFinite,
           referenceF2TimeSeconds >= 0,
           !isRaceF2Placeholder(referenceF2TimeSeconds, position: f2Position) {
            let leaderF2 = leaderF2TimeSeconds ?? 0
            return LiveGapValue(
                hasData: true,
                isLeader: false,
                seconds: max(0, referenceF2TimeSeconds - leaderF2),
                laps: nil,
                source: "CarIdxF2Time"
            )
        }

        return .unavailable
    }

    private static func isRaceF2Placeholder(_ f2TimeSeconds: Double, position: Int?) -> Bool {
        guard let position, position > 1, f2TimeSeconds.isFinite, f2TimeSeconds >= 0 else {
            return false
        }

        return abs(f2TimeSeconds - (Double(position - 1) / 1000.0)) <= 0.00002
    }

    private static func makeClassCars(frame: MockLiveTelemetryFrame, referenceClassGap: LiveGapValue) -> [LiveClassGapCar] {
        let referenceGap = referenceClassGap.seconds ?? FourHourRacePreview.teamClassGapSeconds(sessionTime: frame.sessionTime)
        var cars: [ClassCarDraft] = [
            ClassCarDraft(
                carIdx: FourHourRacePreview.teamCarIdx,
                isReferenceCar: true,
                gapSecondsToClassLeader: referenceGap
            )
        ]

        for position in 1...18 where position != 6 {
            let gap = FourHourRacePreview.classGapSeconds(
                classPosition: position,
                sessionTime: frame.sessionTime
            )
            cars.append(ClassCarDraft(
                carIdx: position == 1 ? FourHourRacePreview.classLeaderCarIdx : 100 + position,
                isReferenceCar: false,
                gapSecondsToClassLeader: gap
            ))
        }

        let sortedCars = cars.sorted {
            if $0.gapSecondsToClassLeader == $1.gapSecondsToClassLeader {
                return $0.carIdx < $1.carIdx
            }

            return $0.gapSecondsToClassLeader < $1.gapSecondsToClassLeader
        }
        let leaderCarIdx = sortedCars.first?.carIdx
        return sortedCars.enumerated().map { index, car in
            LiveClassGapCar(
                carIdx: car.carIdx,
                isReferenceCar: car.isReferenceCar,
                isClassLeader: car.carIdx == leaderCarIdx,
                classPosition: index + 1,
                gapSecondsToClassLeader: car.gapSecondsToClassLeader,
                gapLapsToClassLeader: car.carIdx == leaderCarIdx ? 0 : nil,
                deltaSecondsToReference: car.gapSecondsToClassLeader - referenceGap,
                carClassColorHex: FourHourRacePreview.teamClassColorHex
            )
        }
    }

    private static func fromCapturedReplay(_ frame: MockLiveTelemetryFrame) -> LiveLeaderGapSnapshot? {
        guard let reference = frame.capturedReferenceCar else {
            return nil
        }

        let rankedCars = frame.capturedCars.filter {
            $0.overallPosition != nil || $0.classPosition != nil || $0.trackProgress != nil
        }
        guard !rankedCars.isEmpty else {
            return nil
        }

        let sameClassCars = rankedCars
            .filter { reference.carClass == nil || $0.carClass == reference.carClass }
            .sorted(by: classSort)
        guard !sameClassCars.isEmpty else {
            return nil
        }

        let classLeader = sameClassCars.first
        let overallLeader = rankedCars.sorted(by: overallSort).first
        let referenceClassGap = gapSeconds(from: classLeader, to: reference, lapSeconds: frame.estimatedLapSeconds)
        let referenceOverallGap = gapSeconds(from: overallLeader, to: reference, lapSeconds: frame.estimatedLapSeconds)
        let classCars = sameClassCars.map { car -> LiveClassGapCar in
            let gap = gapSeconds(from: classLeader, to: car, lapSeconds: frame.estimatedLapSeconds)
            return LiveClassGapCar(
                carIdx: car.carIdx,
                isReferenceCar: car.carIdx == reference.carIdx,
                isClassLeader: classLeader?.carIdx == car.carIdx,
                classPosition: car.classPosition,
                gapSecondsToClassLeader: gap,
                gapLapsToClassLeader: classLeader?.carIdx == car.carIdx ? 0 : nil,
                deltaSecondsToReference: gap.map { $0 - (referenceClassGap ?? 0) },
                carClassColorHex: car.carClassColorHex,
                driverName: car.driverName,
                teamName: car.teamName,
                carNumber: car.carNumber,
                carClass: car.carClass,
                carClassName: car.carClassName,
                lapCompleted: car.lapCompleted,
                lapDistPct: car.lapDistPct,
                onPitRoad: car.onPitRoad
            )
        }

        return LiveLeaderGapSnapshot(
            hasData: true,
            referenceOverallPosition: reference.overallPosition,
            referenceClassPosition: reference.classPosition,
            overallLeaderCarIdx: overallLeader?.carIdx,
            classLeaderCarIdx: classLeader?.carIdx,
            overallLeaderGap: LiveGapValue(
                hasData: referenceOverallGap != nil || reference.overallPosition == 1,
                isLeader: overallLeader?.carIdx == reference.carIdx || reference.overallPosition == 1,
                seconds: referenceOverallGap,
                laps: nil,
                source: "capture-replay"
            ),
            classLeaderGap: LiveGapValue(
                hasData: referenceClassGap != nil || classLeader?.carIdx == reference.carIdx || reference.classPosition == 1,
                isLeader: classLeader?.carIdx == reference.carIdx || reference.classPosition == 1,
                seconds: referenceClassGap,
                laps: nil,
                source: "capture-replay"
            ),
            classCars: classCars
        )
    }

    private static func classSort(_ left: CapturedReplayCar, _ right: CapturedReplayCar) -> Bool {
        let leftPosition = left.classPosition ?? Int.max
        let rightPosition = right.classPosition ?? Int.max
        if leftPosition != rightPosition {
            return leftPosition < rightPosition
        }

        return overallSort(left, right)
    }

    private static func overallSort(_ left: CapturedReplayCar, _ right: CapturedReplayCar) -> Bool {
        let leftPosition = left.overallPosition ?? Int.max
        let rightPosition = right.overallPosition ?? Int.max
        if leftPosition != rightPosition {
            return leftPosition < rightPosition
        }

        let leftProgress = left.trackProgress ?? -Double.greatestFiniteMagnitude
        let rightProgress = right.trackProgress ?? -Double.greatestFiniteMagnitude
        if leftProgress != rightProgress {
            return leftProgress > rightProgress
        }

        return left.carIdx < right.carIdx
    }

    private static func gapSeconds(
        from leader: CapturedReplayCar?,
        to car: CapturedReplayCar,
        lapSeconds: Double
    ) -> Double? {
        guard let leader,
              leader.carIdx != car.carIdx,
              let leaderProgress = leader.trackProgress,
              let carProgress = car.trackProgress else {
            return leader?.carIdx == car.carIdx ? 0 : nil
        }

        let effectiveLapSeconds = lapSeconds.isFinite && lapSeconds > 0
            ? lapSeconds
            : FourHourRacePreview.medianLapSeconds
        return max(0, (leaderProgress - carProgress) * effectiveLapSeconds)
    }
}

private struct ClassCarDraft {
    var carIdx: Int
    var isReferenceCar: Bool
    var gapSecondsToClassLeader: Double
}

struct LiveGapValue {
    var hasData: Bool
    var isLeader: Bool
    var seconds: Double?
    var laps: Double?
    var source: String

    static let unavailable = LiveGapValue(
        hasData: false,
        isLeader: false,
        seconds: nil,
        laps: nil,
        source: "unavailable"
    )
}

struct LiveClassGapCar {
    var carIdx: Int
    var isReferenceCar: Bool
    var isClassLeader: Bool
    var classPosition: Int?
    var gapSecondsToClassLeader: Double?
    var gapLapsToClassLeader: Double?
    var deltaSecondsToReference: Double?
    var carClassColorHex: String?
    var driverName: String? = nil
    var teamName: String? = nil
    var carNumber: String? = nil
    var carClass: Int? = nil
    var carClassName: String? = nil
    var lapCompleted: Int? = nil
    var lapDistPct: Double? = nil
    var onPitRoad: Bool? = nil
}

struct CapturedReplayCar {
    var carIdx: Int
    var carNumber: String?
    var driverName: String?
    var teamName: String?
    var carClass: Int?
    var carClassName: String?
    var carClassColorHex: String?
    var overallPosition: Int?
    var classPosition: Int?
    var lapCompleted: Int?
    var lapDistPct: Double?
    var f2TimeSeconds: Double?
    var estTimeSeconds: Double?
    var onPitRoad: Bool
    var trackSurface: Int?

    var trackProgress: Double? {
        guard let lapDistPct,
              lapDistPct.isFinite,
              lapDistPct >= 0 else {
            return nil
        }

        return Double(max(0, lapCompleted ?? 0)) + min(max(lapDistPct, 0), 1)
    }

    var displayDriverName: String {
        if let driverName, !driverName.isEmpty {
            return driverName
        }
        if let teamName, !teamName.isEmpty {
            return teamName
        }
        return MockDriverNames.displayName(for: carIdx)
    }

    var displayCarNumber: String {
        guard let carNumber, !carNumber.isEmpty else {
            return "#\(carIdx)"
        }

        return carNumber.hasPrefix("#") ? carNumber : "#\(carNumber)"
    }
}

struct MockLiveTelemetryFrame {
    var capturedAtUtc: Date
    var sessionTime: TimeInterval
    var sessionTimeRemain: TimeInterval
    var sessionTimeTotal: TimeInterval
    var sessionState: Int
    var fuelLevelLiters: Double
    var fuelMaxLiters: Double
    var fuelLevelPercent: Double
    var fuelUsePerHourLiters: Double
    var estimatedLapSeconds: Double
    var teamLapCompleted: Int
    var teamLapDistPct: Double
    var leaderLapCompleted: Int
    var leaderLapDistPct: Double
    var teamPosition: Int?
    var teamClassPosition: Int?
    var teamF2TimeSeconds: Double?
    var leaderF2TimeSeconds: Double?
    var classLeaderF2TimeSeconds: Double?
    var carLeftRight: Int?
    var playerCarIdx: Int?
    var focusCarIdx: Int?
    var lastLapTimeSeconds: Double?
    var bestLapTimeSeconds: Double?
    var lapDeltaToSessionBestLapSeconds: Double?
    var lapDeltaToSessionBestLapOk: Bool?
    var isOnTrack: Bool
    var isInGarage: Bool
    var isGarageVisible: Bool
    var onPitRoad: Bool
    var brakeAbsActive: Bool
    var trackWetness: Int
    var weatherDeclaredWet: Bool
    var teamDriverKey: String
    var teamDriverName: String
    var teamDriverInitials: String
    var driversSoFar: Int
    var capturedCars: [CapturedReplayCar] = []

    var capturedReferenceCar: CapturedReplayCar? {
        let preferredCarIdx = focusCarIdx ?? playerCarIdx
        if let preferredCarIdx,
           let car = capturedCars.first(where: { $0.carIdx == preferredCarIdx }) {
            return car
        }

        if let playerCarIdx,
           let car = capturedCars.first(where: { $0.carIdx == playerCarIdx }) {
            return car
        }

        return capturedCars.first
    }

    static func mock(
        capturedAtUtc: Date,
        sessionTime: TimeInterval,
        fuelLevelLiters: Double,
        fuelUsePerHourLiters: Double
    ) -> MockLiveTelemetryFrame {
        let fuelMax = FourHourRacePreview.fuelMaxLiters
        let estimatedLapSeconds = FourHourRacePreview.medianLapSeconds
        let sessionTimeTotal = FourHourRacePreview.sessionLengthSeconds
        let sessionTimeRemain = max(0, sessionTimeTotal - sessionTime)
        let teamProgress = max(0, sessionTime / FourHourRacePreview.averageLapSeconds)
        let leaderProgress = max(0, sessionTime / FourHourRacePreview.leaderPreviewLapSeconds)
        let teamLapCompleted = Int(teamProgress.rounded(.down))
        let teamLapDistPct = teamProgress.truncatingRemainder(dividingBy: 1)
        let teamClassPosition = FourHourRacePreview.teamClassPosition(sessionTime: sessionTime)
        let classLeaderGapSeconds = FourHourRacePreview.teamClassGapSeconds(sessionTime: sessionTime)
        let sideCycle = [1, 1, 2, 1, 3, 4]
        let sideIndex = Int((sessionTime / 6).rounded(.down)) % sideCycle.count
        let driver = FourHourRacePreview.teamDriver(sessionTime: sessionTime)
        let garageVisible = sessionTime.truncatingRemainder(dividingBy: 180) < 8
        let justCompletedLap = teamLapCompleted > 0 && teamLapDistPct < 0.04
        let sessionBestLap = justCompletedLap && teamLapCompleted % 3 == 1
        let syntheticBrake = max(0, min(1, sin(sessionTime * 0.72) - 0.75))

        return MockLiveTelemetryFrame(
            capturedAtUtc: capturedAtUtc,
            sessionTime: sessionTime,
            sessionTimeRemain: sessionTimeRemain,
            sessionTimeTotal: sessionTimeTotal,
            sessionState: sessionTimeRemain > 0 ? 4 : 5,
            fuelLevelLiters: fuelLevelLiters,
            fuelMaxLiters: fuelMax,
            fuelLevelPercent: fuelLevelLiters / fuelMax,
            fuelUsePerHourLiters: fuelUsePerHourLiters,
            estimatedLapSeconds: estimatedLapSeconds,
            teamLapCompleted: teamLapCompleted,
            teamLapDistPct: teamLapDistPct,
            leaderLapCompleted: Int(leaderProgress.rounded(.down)),
            leaderLapDistPct: leaderProgress.truncatingRemainder(dividingBy: 1),
            teamPosition: 7,
            teamClassPosition: teamClassPosition,
            teamF2TimeSeconds: classLeaderGapSeconds,
            leaderF2TimeSeconds: 0,
            classLeaderF2TimeSeconds: 0,
            carLeftRight: sideCycle[sideIndex],
            playerCarIdx: 10,
            focusCarIdx: 10,
            lastLapTimeSeconds: justCompletedLap ? estimatedLapSeconds : nil,
            bestLapTimeSeconds: justCompletedLap ? estimatedLapSeconds - (sessionBestLap ? 0.6 : 0.2) : nil,
            lapDeltaToSessionBestLapSeconds: justCompletedLap ? (sessionBestLap ? 0 : 1.8) : nil,
            lapDeltaToSessionBestLapOk: justCompletedLap ? true : nil,
            isOnTrack: true,
            isInGarage: false,
            isGarageVisible: garageVisible,
            onPitRoad: false,
            brakeAbsActive: syntheticBrake > 0.16 && Int(sessionTime * 8).isMultiple(of: 2),
            trackWetness: FourHourRacePreview.trackWetness(sessionTime: sessionTime),
            weatherDeclaredWet: FourHourRacePreview.weatherDeclaredWet(sessionTime: sessionTime),
            teamDriverKey: driver.key,
            teamDriverName: driver.name,
            teamDriverInitials: driver.initials,
            driversSoFar: driver.driversSoFar
        )
    }

}

private final class TrackMapSectorTracker {
    private static let improvementEpsilonSeconds = 0.001
    private static let maximumSectorSeconds = 900.0
    private static let maximumLapSeconds = 3600.0
    private static let lapStartSeedThreshold = 0.02
    private static let sectors: [SectorBoundary] = [
        SectorBoundary(sectorNum: 0, sectorIndex: 0, startPct: 0.0, endPct: 0.18),
        SectorBoundary(sectorNum: 1, sectorIndex: 1, startPct: 0.18, endPct: 0.36),
        SectorBoundary(sectorNum: 2, sectorIndex: 2, startPct: 0.36, endPct: 0.54),
        SectorBoundary(sectorNum: 3, sectorIndex: 3, startPct: 0.54, endPct: 0.72),
        SectorBoundary(sectorNum: 4, sectorIndex: 4, startPct: 0.72, endPct: 0.88),
        SectorBoundary(sectorNum: 5, sectorIndex: 5, startPct: 0.88, endPct: 1.0)
    ]

    private var bestSectorSeconds: [Int: Double] = [:]
    private var activeSectorHighlights: [Int: String] = [:]
    private var state: TimingState?
    private var fullLapHighlight: String?
    private var fullLapCompleted: Int?
    private var bestLapSeconds: Double?

    func reset() {
        bestSectorSeconds = [:]
        activeSectorHighlights = [:]
        state = nil
        fullLapHighlight = nil
        fullLapCompleted = nil
        bestLapSeconds = nil
    }

    func update(_ frame: MockLiveTelemetryFrame) -> LiveTrackMapModel {
        guard let observation = observation(from: frame) else {
            resetLiveProgress()
            return buildModel(hasLiveTiming: false)
        }

        process(observation, frame: frame)
        return buildModel(hasLiveTiming: true)
    }

    func model(
        highlights: [Int: String] = [:],
        fullLapHighlight: String? = nil,
        hasLiveTiming: Bool = true
    ) -> LiveTrackMapModel {
        let sectors = Self.sectors.map { sector in
            LiveTrackSectorSegment(
                sectorNum: sector.sectorNum,
                startPct: sector.startPct,
                endPct: sector.endPct,
                highlight: fullLapHighlight ?? highlights[sector.sectorNum] ?? LiveTrackSectorHighlights.none
            )
        }
        return LiveTrackMapModel(
            hasSectors: true,
            hasLiveTiming: hasLiveTiming,
            quality: hasLiveTiming ? "reliable" : "partial",
            sectors: sectors
        )
    }

    private func process(_ observation: Observation, frame: MockLiveTelemetryFrame) {
        guard var currentState = state else {
            state = seedState(observation)
            return
        }

        let previousProgress = Double(currentState.lapCompleted) + currentState.lapDistPct
        let currentProgress = Double(observation.lapCompleted) + observation.lapDistPct
        let progressDelta = currentProgress - previousProgress
        guard progressDelta > 0,
              progressDelta <= 1.25,
              progressDelta.isFinite,
              observation.sessionTimeSeconds > currentState.sessionTimeSeconds else {
            state = seedState(observation)
            activeSectorHighlights.removeAll()
            fullLapHighlight = nil
            fullLapCompleted = nil
            return
        }

        for crossing in sectorCrossings(previous: currentState, current: observation) {
            if crossing.sectorIndex == 1,
               let completed = fullLapCompleted,
               crossing.boundaryLapCompleted > completed {
                fullLapHighlight = nil
                fullLapCompleted = nil
                activeSectorHighlights.removeAll()
            }

            if let previousSectorIndex = currentState.lastBoundarySectorIndex,
               let previousBoundaryTime = currentState.lastBoundarySessionTimeSeconds {
                let elapsed = crossing.sessionTimeSeconds - previousBoundaryTime
                if elapsed > 0, elapsed < Self.maximumSectorSeconds {
                    let completedSector = Self.sectors[previousSectorIndex]
                    let highlight = classifySector(completedSector, elapsedSeconds: elapsed)
                    if highlight == LiveTrackSectorHighlights.none {
                        activeSectorHighlights.removeValue(forKey: completedSector.sectorNum)
                    } else {
                        activeSectorHighlights[completedSector.sectorNum] = highlight
                    }
                }
            }

            if crossing.sectorIndex == 0 {
                let lapSeconds = currentState.lastLapStartSessionTimeSeconds.map {
                    crossing.sessionTimeSeconds - $0
                } ?? frame.lastLapTimeSeconds
                let lapHighlight = classifyLap(lapSeconds, frame: frame)
                if lapHighlight == LiveTrackSectorHighlights.none {
                    fullLapHighlight = nil
                    fullLapCompleted = nil
                } else {
                    fullLapHighlight = lapHighlight
                    fullLapCompleted = crossing.boundaryLapCompleted - 1
                }

                currentState.lastLapStartSessionTimeSeconds = crossing.sessionTimeSeconds
            }

            currentState.lastBoundarySectorIndex = crossing.sectorIndex
            currentState.lastBoundarySessionTimeSeconds = crossing.sessionTimeSeconds
        }

        currentState.lapCompleted = observation.lapCompleted
        currentState.lapDistPct = observation.lapDistPct
        currentState.sessionTimeSeconds = observation.sessionTimeSeconds
        state = currentState
    }

    private func seedState(_ observation: Observation) -> TimingState {
        let nearLapStart = observation.lapDistPct <= Self.lapStartSeedThreshold
        return TimingState(
            lapCompleted: observation.lapCompleted,
            lapDistPct: observation.lapDistPct,
            sessionTimeSeconds: observation.sessionTimeSeconds,
            lastBoundarySectorIndex: nearLapStart ? 0 : nil,
            lastBoundarySessionTimeSeconds: nearLapStart ? observation.sessionTimeSeconds : nil,
            lastLapStartSessionTimeSeconds: nearLapStart ? observation.sessionTimeSeconds : nil
        )
    }

    private func sectorCrossings(previous: TimingState, current: Observation) -> [SectorCrossing] {
        let previousProgress = Double(previous.lapCompleted) + previous.lapDistPct
        let currentProgress = Double(current.lapCompleted) + current.lapDistPct
        let progressDelta = currentProgress - previousProgress
        let timeDelta = current.sessionTimeSeconds - previous.sessionTimeSeconds
        guard progressDelta > 0, timeDelta > 0 else {
            return []
        }

        var crossings: [SectorCrossing] = []
        for lap in previous.lapCompleted...current.lapCompleted {
            for sector in Self.sectors {
                let boundaryProgress = Double(lap) + sector.startPct
                guard boundaryProgress > previousProgress,
                      boundaryProgress <= currentProgress else {
                    continue
                }

                let interpolation = (boundaryProgress - previousProgress) / progressDelta
                guard interpolation.isFinite,
                      interpolation >= 0,
                      interpolation <= 1 else {
                    continue
                }

                crossings.append(SectorCrossing(
                    sectorIndex: sector.sectorIndex,
                    boundaryLapCompleted: lap,
                    sessionTimeSeconds: previous.sessionTimeSeconds + timeDelta * interpolation
                ))
            }
        }
        return crossings
    }

    private func classifySector(_ sector: SectorBoundary, elapsedSeconds: Double) -> String {
        if bestSectorSeconds[sector.sectorNum].map({ elapsedSeconds < $0 - Self.improvementEpsilonSeconds }) != false {
            bestSectorSeconds[sector.sectorNum] = elapsedSeconds
            return LiveTrackSectorHighlights.personalBest
        }

        return LiveTrackSectorHighlights.none
    }

    private func classifyLap(_ lapSeconds: Double?, frame: MockLiveTelemetryFrame) -> String {
        guard let elapsed = lapSeconds,
              elapsed.isFinite,
              elapsed > 0,
              elapsed < Self.maximumLapSeconds else {
            return LiveTrackSectorHighlights.none
        }

        let improved = bestLapSeconds.map { elapsed < $0 - Self.improvementEpsilonSeconds } ?? true
        if improved {
            bestLapSeconds = elapsed
        }

        if isSessionBestLapSignal(frame) || (improved && frame.lapDeltaToSessionBestLapOk != true) {
            return LiveTrackSectorHighlights.bestLap
        }

        return improved ? LiveTrackSectorHighlights.personalBest : LiveTrackSectorHighlights.none
    }

    private func isSessionBestLapSignal(_ frame: MockLiveTelemetryFrame) -> Bool {
        frame.lapDeltaToSessionBestLapOk == true
            && (frame.lapDeltaToSessionBestLapSeconds ?? Double.greatestFiniteMagnitude).isFinite
            && (frame.lapDeltaToSessionBestLapSeconds ?? Double.greatestFiniteMagnitude) <= Self.improvementEpsilonSeconds
    }

    private func buildModel(hasLiveTiming: Bool) -> LiveTrackMapModel {
        model(
            highlights: activeSectorHighlights,
            fullLapHighlight: fullLapHighlight,
            hasLiveTiming: hasLiveTiming
        )
    }

    private func resetLiveProgress() {
        state = nil
        fullLapHighlight = nil
        fullLapCompleted = nil
        activeSectorHighlights.removeAll()
    }

    private func observation(from frame: MockLiveTelemetryFrame) -> Observation? {
        guard frame.isOnTrack,
              !frame.isInGarage,
              !frame.onPitRoad,
              frame.teamLapCompleted >= 0,
              frame.teamLapDistPct.isFinite,
              frame.teamLapDistPct >= 0,
              frame.teamLapDistPct <= 1.000001,
              frame.sessionTime.isFinite else {
            return nil
        }

        return Observation(
            lapCompleted: frame.teamLapCompleted,
            lapDistPct: min(max(frame.teamLapDistPct, 0), 1),
            sessionTimeSeconds: frame.sessionTime
        )
    }

    private struct Observation {
        var lapCompleted: Int
        var lapDistPct: Double
        var sessionTimeSeconds: Double
    }

    private struct TimingState {
        var lapCompleted: Int
        var lapDistPct: Double
        var sessionTimeSeconds: Double
        var lastBoundarySectorIndex: Int?
        var lastBoundarySessionTimeSeconds: Double?
        var lastLapStartSessionTimeSeconds: Double?
    }

    private struct SectorBoundary {
        var sectorNum: Int
        var sectorIndex: Int
        var startPct: Double
        var endPct: Double
    }

    private struct SectorCrossing {
        var sectorIndex: Int
        var boundaryLapCompleted: Int
        var sessionTimeSeconds: Double
    }
}

final class LiveTelemetryStore {
    private let lock = NSLock()
    private let trackMapSectorTracker = TrackMapSectorTracker()
    private var current = LiveTelemetrySnapshot.empty
    private var sequence = 0

    func snapshot() -> LiveTelemetrySnapshot {
        lock.withLock {
            current
        }
    }

    func markConnected() {
        lock.withLock {
            sequence += 1
            current.isConnected = true
            current.lastUpdatedAtUtc = Date()
            current.sequence = sequence
        }
    }

    func markCollectionStarted(sourceId: String, startedAtUtc: Date) {
        lock.withLock {
            if current.sourceId != sourceId {
                trackMapSectorTracker.reset()
            }
            sequence += 1
            current.isConnected = true
            current.isCollecting = true
            current.sourceId = sourceId
            current.startedAtUtc = startedAtUtc
            current.lastUpdatedAtUtc = startedAtUtc
            current.sequence = sequence
        }
    }

    func markDisconnected() {
        lock.withLock {
            trackMapSectorTracker.reset()
            sequence += 1
            current = .empty
            current.lastUpdatedAtUtc = Date()
            current.sequence = sequence
        }
    }

    func recordFrame(_ frame: MockLiveTelemetryFrame) {
        lock.withLock {
            sequence += 1
            current.isConnected = true
            current.isCollecting = true
            current.startedAtUtc = current.startedAtUtc ?? frame.capturedAtUtc
            current.lastUpdatedAtUtc = frame.capturedAtUtc
            current.sequence = sequence
            current.combo = .mockNurburgringMercedesRace
            current.latestFrame = frame
            current.fuel = LiveFuelSnapshot.from(frame)
            current.proximity = LiveProximitySnapshot.from(frame)
            current.leaderGap = LiveLeaderGapSnapshot.from(frame)
            current.models = LiveRaceModels(trackMap: trackMapSectorTracker.update(frame))
        }
    }

    func recordRadarScenario(
        _ scenario: RadarCaptureScenario,
        capturedAtUtc: Date,
        playbackElapsedSeconds: TimeInterval = 0
    ) {
        lock.withLock {
            sequence += 1
            var snapshot = scenario.makeSnapshot(
                capturedAtUtc: capturedAtUtc,
                startedAtUtc: current.startedAtUtc,
                sequence: sequence,
                playbackElapsedSeconds: playbackElapsedSeconds
            )
            snapshot.completedStintCount = current.completedStintCount
            current = snapshot
        }
    }
}
