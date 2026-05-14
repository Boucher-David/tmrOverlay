@testable import TmrOverlayMacCore
import AppKit
import XCTest

@MainActor
final class OverlayCatalogueBehaviourTests: XCTestCase {
    func testBrowserOverlayRoutesCoverProductCatalogue() {
        let definitions = browserBackedDefinitions()
        let ids = definitions.map(\.id).sorted()

        XCTAssertEqual(ids, [
            "car-radar",
            "fuel-calculator",
            "gap-to-leader",
            "garage-cover",
            "input-state",
            "pit-service",
            "relative",
            "session-weather",
            "standings",
            "stream-chat",
            "track-map"
        ])

        for definition in definitions {
            XCTAssertEqual(BrowserOverlayCatalog.route(for: definition.id), "/overlays/\(definition.id)")
            XCTAssertGreaterThan(definition.defaultSize.width, 0)
            XCTAssertGreaterThan(definition.defaultSize.height, 0)
        }
    }

    func testSimpleTelemetryOverlaysRenderCatalogueBehaviour() {
        let snapshot = liveSnapshot(sessionTime: 1_200)

        let sessionWeather = SimpleTelemetryOverlayView(kind: .sessionWeather)
        sessionWeather.update(with: snapshot)
        XCTAssertTrue(labelText(in: sessionWeather).contains("Session / Weather"))
        XCTAssertTrue(labelText(in: sessionWeather).contains("Surface"))

        let pitService = SimpleTelemetryOverlayView(kind: .pitService)
        pitService.update(with: snapshot)
        XCTAssertTrue(labelText(in: pitService).contains("Pit Service"))
        XCTAssertTrue(labelText(in: pitService).contains("Fuel request"))

        let inputState = SimpleTelemetryOverlayView(kind: .inputState)
        inputState.update(with: snapshot)
        XCTAssertTrue(labelText(in: inputState).contains("Input / Car State"))
        XCTAssertTrue(labelText(in: inputState).contains("Pedals"))
    }

    func testFuelAndRelativeOverlaysRenderCatalogueBehaviour() {
        let snapshot = liveSnapshot(sessionTime: 1_200)
        let historyRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("tmr-mac-overlay-catalogue-tests", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer {
            try? FileManager.default.removeItem(at: historyRoot)
        }

        let fuel = FuelCalculatorView(historyQueryService: SessionHistoryQueryService(userHistoryRoot: historyRoot))
        fuel.update(with: snapshot)
        XCTAssertTrue(labelText(in: fuel).contains("Fuel Calculator"))
        XCTAssertTrue(labelText(in: fuel).contains("Overview"))

        let relative = RelativeOverlayView()
        relative.update(with: snapshot, now: snapshot.lastUpdatedAtUtc ?? Date())
        XCTAssertTrue(labelText(in: relative).contains("Relative"))
        XCTAssertTrue(labelText(in: relative).contains("0.000"))
    }

    func testGarageCoverVisibilityFollowsGarageSignal() {
        let cover = GarageCoverView()

        cover.update(with: .empty)
        XCTAssertFalse(cover.isHidden)

        cover.update(with: liveSnapshot(sessionTime: 1_200, isGarageVisible: false))
        XCTAssertTrue(cover.isHidden)

        cover.update(with: liveSnapshot(sessionTime: 1_200, isGarageVisible: true))
        XCTAssertFalse(cover.isHidden)
    }

    private func browserBackedDefinitions() -> [OverlayDefinition] {
        [
            StandingsOverlayDefinition.definition,
            RelativeOverlayDefinition.definition,
            FuelCalculatorOverlayDefinition.definition,
            SessionWeatherOverlayDefinition.definition,
            PitServiceOverlayDefinition.definition,
            InputStateOverlayDefinition.definition,
            CarRadarOverlayDefinition.definition,
            GapToLeaderOverlayDefinition.definition,
            TrackMapOverlayDefinition.definition,
            StreamChatOverlayDefinition.definition,
            GarageCoverOverlayDefinition.definition
        ]
    }

    private func liveSnapshot(
        sessionTime: TimeInterval,
        isGarageVisible: Bool = false
    ) -> LiveTelemetrySnapshot {
        var frame = MockLiveTelemetryFrame.mock(
            capturedAtUtc: Date(),
            sessionTime: sessionTime,
            fuelLevelLiters: 72,
            fuelUsePerHourLiters: 82
        )
        frame.isGarageVisible = isGarageVisible

        let store = LiveTelemetryStore()
        store.recordFrame(frame)
        return store.snapshot()
    }

    private func labelText(in view: NSView) -> [String] {
        view.subviews.flatMap { subview -> [String] in
            var values: [String] = []
            if let label = subview as? NSTextField, !label.stringValue.isEmpty {
                values.append(label.stringValue)
            }

            values.append(contentsOf: labelText(in: subview))
            return values
        }
    }
}
