@testable import TmrOverlayMacCore
import XCTest

final class SlugTests: XCTestCase {
    func testSlugReturnsStablePathSegment() {
        XCTAssertEqual("Mercedes-AMG GT3 2020".slug(), "mercedes-amg-gt3-2020")
        XCTAssertEqual("Track 252 - Gesamtstrecke 24h".slug(), "track-252-gesamtstrecke-24h")
        XCTAssertEqual("  Race / Endurance  ".slug(), "race-endurance")
    }
}
