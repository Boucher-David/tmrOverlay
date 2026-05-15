@testable import TmrOverlayMacCore
import XCTest

final class StandingsDisplayFormattingTests: XCTestCase {
    func testGapSuppressesLapDistanceFallback() {
        XCTAssertEqual(
            StandingsDisplayFormatting.gap(isClassLeader: false, seconds: nil, laps: 0.002),
            "--"
        )
    }

    func testGapUsesSecondsWhenAvailable() {
        XCTAssertEqual(
            StandingsDisplayFormatting.gap(isClassLeader: false, seconds: 4.25, laps: 0.002),
            "+4.2"
        )
    }

    func testGapUsesWholeLapContextWhenAvailable() {
        XCTAssertEqual(
            StandingsDisplayFormatting.gap(isClassLeader: false, seconds: nil, laps: 1),
            "+1L"
        )
    }

    func testLeaderGapUsesLapProgressWhenAvailable() {
        XCTAssertEqual(
            StandingsDisplayFormatting.gap(isClassLeader: true, seconds: 0, lapCompleted: 12, lapDistPct: 0.35),
            "Lap 13"
        )
        XCTAssertEqual(
            StandingsDisplayFormatting.gap(isClassLeader: true, seconds: 0),
            "Leader"
        )
        XCTAssertEqual(
            StandingsDisplayFormatting.gap(isClassLeader: true, seconds: 0, lapCompleted: 0, lapDistPct: 0),
            "Leader"
        )
    }

    func testRelativeGapSuppressesLapDistanceFallback() {
        XCTAssertEqual(
            RelativeDisplayFormatting.gap(seconds: nil, meters: nil, laps: 0.002, direction: .behind),
            "--"
        )
    }

    func testRelativeGapUsesSecondsWhenAvailable() {
        XCTAssertEqual(
            RelativeDisplayFormatting.gap(seconds: 1.234, meters: 42, laps: 0.002, direction: .ahead),
            "-1.234"
        )
    }

}
