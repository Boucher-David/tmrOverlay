import Foundation

struct RawPracticeGapCapture: Decodable {
    var captureId: String
    var lapReferenceSeconds: Double
    var visibleTrendWindowSeconds: Double
    var frames: [RawPracticeGapFrame]
    var states: [RawPracticeGapState]

    static func load(from url: URL) throws -> RawPracticeGapCapture {
        let data = try Data(contentsOf: url)
        let decoder = JSONDecoder()
        return try decoder.decode(RawPracticeGapCapture.self, from: data)
    }
}

struct RawPracticeGapState: Decodable {
    var title: String
    var note: String
    var fileName: String
    var startFrameIndex: Int?
    var frameIndex: Int
}

struct RawPracticeGapFrame: Decodable {
    var frameIndex: Int
    var capturedUnixMs: Double
    var sessionTime: Double
    var trackWetness: Int
    var weatherDeclaredWet: Bool
    var focusCarIdx: Int
    var focusLabel: String
    var referenceOverallPosition: Int?
    var referenceClassPosition: Int?
    var overallLeaderCarIdx: Int?
    var classLeaderCarIdx: Int?
    var classLeaderGapSeconds: Double?
    var classLeaderGapLaps: Double?
    var classLeaderGapSource: String
    var classCars: [RawPracticeGapClassCar]

    func snapshot(lapReferenceSeconds: Double) -> LiveTelemetrySnapshot {
        let capturedAtUtc = Date(timeIntervalSince1970: capturedUnixMs / 1000)
        let frame = MockLiveTelemetryFrame(
            capturedAtUtc: capturedAtUtc,
            sessionTime: sessionTime,
            sessionTimeRemain: max(0, 7_200 - sessionTime),
            sessionTimeTotal: 7_200,
            sessionState: 4,
            fuelLevelLiters: 0,
            fuelMaxLiters: 106,
            fuelLevelPercent: 0,
            fuelUsePerHourLiters: 0,
            estimatedLapSeconds: lapReferenceSeconds,
            teamLapCompleted: 0,
            teamLapDistPct: 0,
            leaderLapCompleted: 0,
            leaderLapDistPct: 0,
            teamPosition: referenceOverallPosition,
            teamClassPosition: referenceClassPosition,
            teamF2TimeSeconds: classLeaderGapSeconds,
            leaderF2TimeSeconds: 0,
            classLeaderF2TimeSeconds: 0,
            carLeftRight: 0,
            playerCarIdx: focusCarIdx,
            focusCarIdx: focusCarIdx,
            lastLapTimeSeconds: nil,
            bestLapTimeSeconds: nil,
            lapDeltaToSessionBestLapSeconds: nil,
            lapDeltaToSessionBestLapOk: nil,
            isOnTrack: true,
            isInGarage: false,
            isGarageVisible: false,
            onPitRoad: false,
            brakeAbsActive: false,
            trackWetness: trackWetness,
            weatherDeclaredWet: weatherDeclaredWet,
            teamDriverKey: "focus-\(focusCarIdx)",
            teamDriverName: focusLabel,
            teamDriverInitials: focusInitials,
            driversSoFar: 1
        )
        let inferredGapLaps = classLeaderGapLaps
            ?? (classLeaderGapSeconds == nil
                ? classCars.first(where: { $0.isReferenceCar })?.gapSecondsToClassLeader.map { $0 / lapReferenceSeconds }
                : nil)
        let classGap = LiveGapValue(
            hasData: classLeaderGapSeconds != nil || inferredGapLaps != nil,
            isLeader: classLeaderGapSeconds == 0 || inferredGapLaps == 0,
            seconds: classLeaderGapSeconds,
            laps: inferredGapLaps,
            source: classLeaderGapSource
        )
        var snapshot = LiveTelemetrySnapshot.empty
        snapshot.isConnected = true
        snapshot.isCollecting = true
        snapshot.sourceId = "raw-practice-capture"
        snapshot.startedAtUtc = capturedAtUtc
        snapshot.lastUpdatedAtUtc = capturedAtUtc
        snapshot.sequence = frameIndex
        snapshot.latestFrame = frame
        snapshot.fuel = .unavailable
        snapshot.leaderGap = LiveLeaderGapSnapshot(
            hasData: classGap.hasData,
            referenceOverallPosition: referenceOverallPosition,
            referenceClassPosition: referenceClassPosition,
            overallLeaderCarIdx: overallLeaderCarIdx,
            classLeaderCarIdx: classLeaderCarIdx,
            overallLeaderGap: .unavailable,
            classLeaderGap: classGap,
            classCars: classCars.map(\.liveCar)
        )
        return snapshot
    }

    private var focusInitials: String {
        let words = focusLabel
            .split(separator: " ")
            .filter { !$0.isEmpty }
        let initials = words
            .prefix(2)
            .compactMap(\.first)
            .map(String.init)
            .joined()
        return initials.isEmpty ? "FC" : initials.uppercased()
    }
}

struct RawPracticeGapClassCar: Decodable {
    var carIdx: Int
    var isReferenceCar: Bool
    var isClassLeader: Bool
    var classPosition: Int?
    var gapSecondsToClassLeader: Double?
    var gapLapsToClassLeader: Double?
    var deltaSecondsToReference: Double?
    var carClassColorHex: String?

    var liveCar: LiveClassGapCar {
        LiveClassGapCar(
            carIdx: carIdx,
            isReferenceCar: isReferenceCar,
            isClassLeader: isClassLeader,
            classPosition: classPosition,
            gapSecondsToClassLeader: gapSecondsToClassLeader,
            gapLapsToClassLeader: gapLapsToClassLeader ?? (isClassLeader ? 0 : nil),
            deltaSecondsToReference: deltaSecondsToReference,
            carClassColorHex: carClassColorHex
        )
    }
}
