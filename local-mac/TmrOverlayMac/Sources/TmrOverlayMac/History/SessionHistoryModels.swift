import Foundation

struct HistoricalSessionSummary: Codable {
    let summaryVersion: Int
    let sourceCaptureId: String
    let startedAtUtc: Date
    let finishedAtUtc: Date
    let combo: HistoricalComboIdentity
    let car: HistoricalCarIdentity
    let track: HistoricalTrackIdentity
    let session: HistoricalSessionIdentity
    let conditions: HistoricalConditions
    let metrics: HistoricalSessionMetrics
    var stints: [HistoricalStintSummary] = []
    var pitStops: [HistoricalPitStopSummary] = []
    let quality: HistoricalDataQuality

    static func mock(
        sourceCaptureId: String,
        startedAtUtc: Date,
        finishedAtUtc: Date,
        frameCount: Int,
        captureDurationSeconds: Double,
        validGreenTimeSeconds: Double,
        validDistanceLaps: Double,
        fuelUsedLiters: Double,
        fuelPerHourLiters: Double?,
        fuelPerLapLiters: Double?,
        startingFuelLiters: Double?,
        endingFuelLiters: Double?,
        minimumFuelLiters: Double?,
        maximumFuelLiters: Double?,
        contributesToBaseline: Bool
    ) -> HistoricalSessionSummary {
        let stints = validDistanceLaps > 0
            ? [
                HistoricalStintSummary(
                    stintNumber: 1,
                    startRaceTimeSeconds: 0,
                    endRaceTimeSeconds: validGreenTimeSeconds,
                    durationSeconds: validGreenTimeSeconds,
                    startLapCompleted: 0,
                    endLapCompleted: Int(validDistanceLaps.rounded(.down)),
                    distanceLaps: validDistanceLaps,
                    fuelStartLiters: startingFuelLiters,
                    fuelEndLiters: endingFuelLiters,
                    fuelUsedLiters: fuelUsedLiters > 0 ? fuelUsedLiters : nil,
                    fuelPerLapLiters: fuelPerLapLiters,
                    driverRole: "local-driver",
                    confidenceFlags: ["mac_mock_preview"]
                )
            ]
            : []

        return HistoricalSessionSummary(
            summaryVersion: 1,
            sourceCaptureId: sourceCaptureId,
            startedAtUtc: startedAtUtc,
            finishedAtUtc: finishedAtUtc,
            combo: HistoricalComboIdentity(
                carKey: "car-156-mercedesamgevogt3",
                trackKey: "track-262-nurburgring-combinedshortb",
                sessionKey: "race"
            ),
            car: HistoricalCarIdentity(
                carId: 156,
                carPath: "mercedesamgevogt3",
                carScreenName: "Mercedes-AMG GT3 2020",
                carScreenNameShort: "Mercedes GT3 2020",
                carClassId: 4098,
                driverCarFuelMaxLiters: 106,
                driverCarFuelKgPerLiter: 0.75,
                driverCarEstLapTimeSeconds: 465.166
            ),
            track: HistoricalTrackIdentity(
                trackId: 262,
                trackName: "nurburgring combinedshortb",
                trackDisplayName: "Gesamtstrecke VLN",
                trackConfigName: nil,
                trackLengthKm: 24.1544
            ),
            session: HistoricalSessionIdentity(
                sessionType: "Race",
                sessionName: "RACE",
                eventType: "Race",
                category: "SportsCar"
            ),
            conditions: HistoricalConditions(
                airTempC: 20,
                trackTempCrewC: 22,
                trackWetness: 0,
                weatherDeclaredWet: false,
                playerTireCompound: 0,
                trackWeatherType: "Realistic",
                trackSkies: "Dynamic",
                trackPrecipitationPercent: 0,
                sessionTrackRubberState: "carry over"
            ),
            metrics: HistoricalSessionMetrics(
                sampleFrameCount: frameCount,
                droppedFrameCount: 0,
                sessionInfoSnapshotCount: 1,
                captureDurationSeconds: captureDurationSeconds,
                onTrackTimeSeconds: validGreenTimeSeconds,
                pitRoadTimeSeconds: 0,
                movingTimeSeconds: validGreenTimeSeconds,
                validGreenTimeSeconds: validGreenTimeSeconds,
                validDistanceLaps: validDistanceLaps,
                completedValidLaps: Int(validDistanceLaps.rounded(.down)),
                fuelUsedLiters: fuelUsedLiters,
                fuelAddedLiters: 0,
                fuelPerHourLiters: fuelPerHourLiters,
                fuelPerLapLiters: fuelPerLapLiters,
                averageLapSeconds: validDistanceLaps >= 1 ? 482.194874 : nil,
                medianLapSeconds: validDistanceLaps >= 1 ? 482.092804 : nil,
                bestLapSeconds: validDistanceLaps >= 1 ? 474.440308 : nil,
                startingFuelLiters: startingFuelLiters,
                endingFuelLiters: endingFuelLiters,
                minimumFuelLiters: minimumFuelLiters,
                maximumFuelLiters: maximumFuelLiters,
                pitRoadEntryCount: 0,
                pitServiceCount: 0,
                stintCount: validGreenTimeSeconds > 0 ? 1 : 0,
                averageStintLaps: validDistanceLaps > 0 ? validDistanceLaps : nil,
                averageStintSeconds: validGreenTimeSeconds > 0 ? validGreenTimeSeconds : nil,
                averageStintFuelPerLapLiters: fuelPerLapLiters,
                averagePitLaneSeconds: nil,
                averagePitStallSeconds: nil,
                averagePitServiceSeconds: nil,
                observedFuelFillRateLitersPerSecond: nil,
                averageTireChangePitServiceSeconds: nil,
                averageNoTirePitServiceSeconds: nil
            ),
            stints: stints,
            pitStops: [],
            quality: HistoricalDataQuality(
                confidence: contributesToBaseline ? "medium" : "low",
                contributesToBaseline: contributesToBaseline,
                reasons: contributesToBaseline ? ["mock_local_harness"] : ["mock_short_sample"]
            )
        )
    }
}

struct HistoricalComboIdentity: Codable {
    let carKey: String
    let trackKey: String
    let sessionKey: String

    static let mockNurburgringMercedesRace = HistoricalComboIdentity(
        carKey: "car-156-mercedesamgevogt3",
        trackKey: "track-262-nurburgring-combinedshortb",
        sessionKey: "race"
    )
}

struct HistoricalCarIdentity: Codable {
    let carId: Int?
    let carPath: String?
    let carScreenName: String?
    let carScreenNameShort: String?
    let carClassId: Int?
    let driverCarFuelMaxLiters: Double?
    let driverCarFuelKgPerLiter: Double?
    let driverCarEstLapTimeSeconds: Double?
}

struct HistoricalTrackIdentity: Codable {
    let trackId: Int?
    let trackName: String?
    let trackDisplayName: String?
    let trackConfigName: String?
    let trackLengthKm: Double?
}

struct HistoricalSessionIdentity: Codable {
    let sessionType: String?
    let sessionName: String?
    let eventType: String?
    let category: String?
}

struct HistoricalConditions: Codable {
    let airTempC: Double?
    let trackTempCrewC: Double?
    let trackWetness: Int?
    let weatherDeclaredWet: Bool?
    let playerTireCompound: Int?
    let trackWeatherType: String?
    let trackSkies: String?
    let trackPrecipitationPercent: Double?
    let sessionTrackRubberState: String?
}

struct HistoricalSessionMetrics: Codable {
    let sampleFrameCount: Int
    let droppedFrameCount: Int
    let sessionInfoSnapshotCount: Int
    let captureDurationSeconds: Double
    let onTrackTimeSeconds: Double
    let pitRoadTimeSeconds: Double
    let movingTimeSeconds: Double
    let validGreenTimeSeconds: Double
    let validDistanceLaps: Double
    let completedValidLaps: Int
    let fuelUsedLiters: Double
    let fuelAddedLiters: Double
    let fuelPerHourLiters: Double?
    let fuelPerLapLiters: Double?
    let averageLapSeconds: Double?
    let medianLapSeconds: Double?
    let bestLapSeconds: Double?
    let startingFuelLiters: Double?
    let endingFuelLiters: Double?
    let minimumFuelLiters: Double?
    let maximumFuelLiters: Double?
    let pitRoadEntryCount: Int
    let pitServiceCount: Int
    let stintCount: Int
    let averageStintLaps: Double?
    let averageStintSeconds: Double?
    let averageStintFuelPerLapLiters: Double?
    let averagePitLaneSeconds: Double?
    let averagePitStallSeconds: Double?
    let averagePitServiceSeconds: Double?
    let observedFuelFillRateLitersPerSecond: Double?
    let averageTireChangePitServiceSeconds: Double?
    let averageNoTirePitServiceSeconds: Double?
}

struct HistoricalStintSummary: Codable {
    let stintNumber: Int
    let startRaceTimeSeconds: Double
    let endRaceTimeSeconds: Double
    let durationSeconds: Double
    let startLapCompleted: Int?
    let endLapCompleted: Int?
    let distanceLaps: Double
    let fuelStartLiters: Double?
    let fuelEndLiters: Double?
    let fuelUsedLiters: Double?
    let fuelPerLapLiters: Double?
    let driverRole: String
    let confidenceFlags: [String]
}

struct HistoricalPitStopSummary: Codable {
    let stopNumber: Int
    let entryRaceTimeSeconds: Double
    let exitRaceTimeSeconds: Double
    let pitLaneSeconds: Double
    let entryLapCompleted: Int?
    let exitLapCompleted: Int?
    let pitStallSeconds: Double?
    let serviceActiveSeconds: Double?
    let fuelBeforeLiters: Double?
    let fuelAfterLiters: Double?
    let fuelAddedLiters: Double?
    let fuelFillRateLitersPerSecond: Double?
    let tireSetChanged: Bool
    let fastRepairUsed: Bool
    let pitServiceFlags: Int?
    let confidenceFlags: [String]
}

struct HistoricalDataQuality: Codable {
    let confidence: String
    let contributesToBaseline: Bool
    let reasons: [String]
}

struct HistoricalSessionAggregate: Codable {
    var aggregateVersion = 3
    var combo: HistoricalComboIdentity?
    var car: HistoricalCarIdentity?
    var track: HistoricalTrackIdentity?
    var session: HistoricalSessionIdentity?
    var updatedAtUtc = Date()
    var sessionCount = 0
    var baselineSessionCount = 0
    var fuelPerLapLiters = RunningHistoricalMetric()
    var fuelPerHourLiters = RunningHistoricalMetric()
    var averageLapSeconds = RunningHistoricalMetric()
    var medianLapSeconds = RunningHistoricalMetric()
    var averageStintLaps = RunningHistoricalMetric()
    var averageStintSeconds = RunningHistoricalMetric()
    var averageStintFuelPerLapLiters = RunningHistoricalMetric()
    var localDriverStintLaps = RunningHistoricalMetric()
    var teammateDriverStintLaps = RunningHistoricalMetric()
    var averagePitLaneSeconds = RunningHistoricalMetric()
    var averagePitStallSeconds = RunningHistoricalMetric()
    var averagePitServiceSeconds = RunningHistoricalMetric()
    var observedFuelFillRateLitersPerSecond = RunningHistoricalMetric()
    var averageTireChangePitServiceSeconds = RunningHistoricalMetric()
    var averageNoTirePitServiceSeconds = RunningHistoricalMetric()
}

struct RunningHistoricalMetric: Codable {
    var sampleCount = 0
    var mean: Double?
    var minimum: Double?
    var maximum: Double?

    mutating func add(_ value: Double?) {
        guard let value, value.isFinite else {
            return
        }

        if sampleCount == 0 {
            sampleCount = 1
            mean = value
            minimum = value
            maximum = value
            return
        }

        mean = ((mean ?? 0) * Double(sampleCount) + value) / Double(sampleCount + 1)
        minimum = min(minimum ?? value, value)
        maximum = max(maximum ?? value, value)
        sampleCount += 1
    }
}
