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
