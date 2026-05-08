@testable import TmrOverlayMacCore
import XCTest

final class SessionHistoryWriterTests: XCTestCase {
    func testSaveWritesSummaryAndAggregate() throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("tmr-overlay-history-tests", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer {
            try? FileManager.default.removeItem(at: root)
        }

        let summary = HistoricalSessionSummary.mock(
            sourceCaptureId: "capture-test",
            startedAtUtc: Date(timeIntervalSince1970: 1_700_000_000),
            finishedAtUtc: Date(timeIntervalSince1970: 1_700_000_120),
            frameCount: 7200,
            captureDurationSeconds: 120,
            validGreenTimeSeconds: 120,
            validDistanceLaps: 1.25,
            fuelUsedLiters: 3.5,
            fuelPerHourLiters: 105,
            fuelPerLapLiters: 2.8,
            startingFuelLiters: 106,
            endingFuelLiters: 102.5,
            minimumFuelLiters: 102.5,
            maximumFuelLiters: 106,
            contributesToBaseline: true
        )

        try SessionHistoryWriter(historyRoot: root).save(summary: summary)

        let sessionDirectory = root
            .appendingPathComponent("cars", isDirectory: true)
            .appendingPathComponent(summary.combo.carKey, isDirectory: true)
            .appendingPathComponent("tracks", isDirectory: true)
            .appendingPathComponent(summary.combo.trackKey, isDirectory: true)
            .appendingPathComponent("sessions", isDirectory: true)
            .appendingPathComponent(summary.combo.sessionKey, isDirectory: true)
        let summaryURL = sessionDirectory
            .appendingPathComponent("summaries", isDirectory: true)
            .appendingPathComponent("capture-test.json")
        let aggregateURL = sessionDirectory.appendingPathComponent("aggregate.json")

        XCTAssertTrue(FileManager.default.fileExists(atPath: summaryURL.path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: aggregateURL.path))
    }
}
