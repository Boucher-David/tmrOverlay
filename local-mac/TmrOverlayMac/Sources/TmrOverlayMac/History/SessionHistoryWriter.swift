import Foundation

final class SessionHistoryWriter {
    private let historyRoot: URL

    init(historyRoot: URL) {
        self.historyRoot = historyRoot
    }

    func save(summary: HistoricalSessionSummary) throws {
        let sessionDirectory = historyRoot
            .appendingPathComponent("cars", isDirectory: true)
            .appendingPathComponent(summary.combo.carKey, isDirectory: true)
            .appendingPathComponent("tracks", isDirectory: true)
            .appendingPathComponent(summary.combo.trackKey, isDirectory: true)
            .appendingPathComponent("sessions", isDirectory: true)
            .appendingPathComponent(summary.combo.sessionKey, isDirectory: true)

        let summariesDirectory = sessionDirectory.appendingPathComponent("summaries", isDirectory: true)
        try FileManager.default.createDirectory(at: summariesDirectory, withIntermediateDirectories: true)

        let summaryURL = summariesDirectory.appendingPathComponent("\(summary.sourceCaptureId.slug()).json")
        try Self.writeJson(summary, to: summaryURL)
        try updateAggregate(at: sessionDirectory.appendingPathComponent("aggregate.json"), with: summary)
    }

    private func updateAggregate(at url: URL, with summary: HistoricalSessionSummary) throws {
        var aggregate = try Self.readJson(HistoricalSessionAggregate.self, from: url) ?? HistoricalSessionAggregate()
        aggregate.combo = summary.combo
        aggregate.car = summary.car
        aggregate.track = summary.track
        aggregate.session = summary.session
        aggregate.updatedAtUtc = Date()
        aggregate.sessionCount += 1

        if summary.quality.contributesToBaseline {
            aggregate.baselineSessionCount += 1
            aggregate.fuelPerLapLiters.add(summary.metrics.fuelPerLapLiters)
            aggregate.fuelPerHourLiters.add(summary.metrics.fuelPerHourLiters)
            aggregate.averageLapSeconds.add(summary.metrics.averageLapSeconds)
            aggregate.medianLapSeconds.add(summary.metrics.medianLapSeconds)
            aggregate.averageStintLaps.add(summary.metrics.averageStintLaps)
            aggregate.averageStintSeconds.add(summary.metrics.averageStintSeconds)
            aggregate.averageStintFuelPerLapLiters.add(summary.metrics.averageStintFuelPerLapLiters)
            for stint in summary.stints where stint.distanceLaps > 0 {
                if stint.driverRole == "local-driver" {
                    aggregate.localDriverStintLaps.add(stint.distanceLaps)
                } else if stint.driverRole == "teammate-driver" {
                    aggregate.teammateDriverStintLaps.add(stint.distanceLaps)
                }
            }
            aggregate.averagePitLaneSeconds.add(summary.metrics.averagePitLaneSeconds)
            aggregate.averagePitStallSeconds.add(summary.metrics.averagePitStallSeconds)
            aggregate.averagePitServiceSeconds.add(summary.metrics.averagePitServiceSeconds)
            aggregate.observedFuelFillRateLitersPerSecond.add(summary.metrics.observedFuelFillRateLitersPerSecond)
            aggregate.averageTireChangePitServiceSeconds.add(summary.metrics.averageTireChangePitServiceSeconds)
            aggregate.averageNoTirePitServiceSeconds.add(summary.metrics.averageNoTirePitServiceSeconds)
        }

        try Self.writeJson(aggregate, to: url)
    }

    private static func writeJson<T: Encodable>(_ value: T, to url: URL) throws {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted]
        let data = try encoder.encode(value)
        try data.write(to: url, options: .atomic)
    }

    private static func readJson<T: Decodable>(_ type: T.Type, from url: URL) throws -> T? {
        guard FileManager.default.fileExists(atPath: url.path) else {
            return nil
        }

        let data = try Data(contentsOf: url)
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(type, from: data)
    }
}
