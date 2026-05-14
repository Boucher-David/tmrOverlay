import AppKit
import Foundation

public enum OverlayScreenshotGenerator {
    public static func renderScreenshots(to outputRoot: URL) throws {
        if ProcessInfo.processInfo.environment["TMR_MAC_SCREENSHOT_ONLY_DESIGN_V2"] == "true" {
            let designV2Root = outputRoot.appendingPathComponent("design-v2", isDirectory: true)
            try FileManager.default.createDirectory(at: designV2Root, withIntermediateDirectories: true)
            try MainActor.assumeIsolated {
                try renderDesignV2States(
                    outputURL: designV2Root.appendingPathComponent("design-v2-states.png")
                )
            }
            return
        }

        if ProcessInfo.processInfo.environment["TMR_MAC_SCREENSHOT_ONLY_DESIGN_V2_COMPONENTS"] == "true" {
            let designV2Root = outputRoot.appendingPathComponent("design-v2", isDirectory: true)
            try FileManager.default.createDirectory(at: designV2Root, withIntermediateDirectories: true)
            try MainActor.assumeIsolated {
                try renderDesignV2ComponentGallery(
                    outputURL: designV2Root.appendingPathComponent("design-v2-components-outrun.png"),
                    theme: .outrun
                )
            }
            return
        }

        if ProcessInfo.processInfo.environment["TMR_MAC_SCREENSHOT_ONLY_SETTINGS_COMPONENTS"] == "true" {
            let settingsRoot = outputRoot.appendingPathComponent("settings-overlay", isDirectory: true)
            try FileManager.default.createDirectory(at: settingsRoot, withIntermediateDirectories: true)
            try MainActor.assumeIsolated {
                try renderSettingsComponentCrops(
                    outputURL: settingsRoot.appendingPathComponent("settings-components.png")
                )
            }
            return
        }

        if ProcessInfo.processInfo.environment["TMR_MAC_SCREENSHOT_ONLY_GAP"] == "true" {
            let gapToLeaderRoot = outputRoot.appendingPathComponent("gap-to-leader", isDirectory: true)
            try FileManager.default.createDirectory(at: gapToLeaderRoot, withIntermediateDirectories: true)
            try MainActor.assumeIsolated {
                try renderGapToLeaderStates(
                    outputURL: gapToLeaderRoot.appendingPathComponent("gap-to-leader-states.png")
                )
            }
            return
        }

        let fuelRoot = outputRoot.appendingPathComponent("fuel-calculator", isDirectory: true)
        let relativeRoot = outputRoot.appendingPathComponent("relative", isDirectory: true)
        let trackMapRoot = outputRoot.appendingPathComponent("track-map", isDirectory: true)
        let settingsRoot = outputRoot.appendingPathComponent("settings-overlay", isDirectory: true)
        let gapToLeaderRoot = outputRoot.appendingPathComponent("gap-to-leader", isDirectory: true)
        let carRadarRoot = outputRoot.appendingPathComponent("car-radar", isDirectory: true)
        let designV2Root = outputRoot.appendingPathComponent("design-v2", isDirectory: true)
        try FileManager.default.createDirectory(at: fuelRoot, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: relativeRoot, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: trackMapRoot, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: settingsRoot, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: gapToLeaderRoot, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: carRadarRoot, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: designV2Root, withIntermediateDirectories: true)

        try MainActor.assumeIsolated {
            try renderDesignV2States(
                outputURL: designV2Root.appendingPathComponent("design-v2-states.png")
            )
            try renderDesignV2ComponentGallery(
                outputURL: designV2Root.appendingPathComponent("design-v2-components-outrun.png"),
                theme: .outrun
            )
            try renderFuelCalculatorStates(
                outputURL: fuelRoot.appendingPathComponent("fuel-calculator-states.png")
            )
            try renderRelativeStates(
                outputURL: relativeRoot.appendingPathComponent("relative-states.png")
            )
            try renderTrackMapStates(
                outputURL: trackMapRoot.appendingPathComponent("track-map-sector-states.png")
            )
            try renderSettingsStates(
                outputURL: settingsRoot.appendingPathComponent("settings-overlay-states.png")
            )
            try renderGapToLeaderScreenshot(
                outputURL: gapToLeaderRoot.appendingPathComponent("gap-to-leader.png")
            )
            try renderRadarScreenshot(
                outputURL: carRadarRoot.appendingPathComponent("car-radar-multiclass.png")
            )
            try renderRadarStates(
                outputURL: carRadarRoot.appendingPathComponent("car-radar-states.png")
            )
            try renderGapToLeaderStates(
                outputURL: gapToLeaderRoot.appendingPathComponent("gap-to-leader-states.png")
            )
        }
    }

    @MainActor
    private static func renderDesignV2States(outputURL: URL) throws {
        let states = try DesignV2PreviewScenario.all.enumerated().map { index, scenario in
            let view = DesignV2PreviewView(scenario: scenario)
            let fileName: String
            switch scenario.mode {
            case .standingsTable:
                fileName = "standings-telemetry.png"
            case .relativeTable:
                fileName = "relative-telemetry.png"
            case .sectorComparison:
                fileName = "sector-comparison.png"
            case .blindspotSignal:
                fileName = "blindspot-signal.png"
            case .lapDelta:
                fileName = "laptime-delta.png"
            case .stintLapGraph:
                fileName = "stint-laptime-log.png"
            case .flagStrip:
                fileName = "flag-display.png"
            case .sourceTable:
                fileName = "analysis-exception.png"
            case .fuelMatrix:
                fileName = "fuel-truth-mix.png"
            case .gapGraph:
                fileName = "gap-context.png"
            case .unavailable:
                fileName = "focus-unavailable.png"
            }

            return ContactSheetState(
                title: "\(index + 1). \(scenario.title)",
                note: scenario.subtitle,
                fileName: fileName,
                image: try renderImage(view)
            )
        }

        let sheet = ContactSheetView(
            title: "Design V2 Proving Ground",
            subtitle: "Mac-only previews for telemetry-first overlays, with source/evidence UI reserved for derived analysis states.",
            states: states,
            imageMaxSize: NSSize(width: 740, height: 430)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 740, height: 430),
            outputRoot: outputURL.deletingLastPathComponent()
        )
    }

    @MainActor
    private static func renderDesignV2ComponentGallery(outputURL: URL, theme: DesignV2Theme) throws {
        let states = try DesignV2ComponentKind.allCases.enumerated().map { index, component in
            ContactSheetState(
                title: "\(index + 1). \(component.title)",
                note: component.note,
                fileName: component.fileName,
                image: try renderImage(DesignV2ComponentGalleryView(theme: theme, component: component))
            )
        }

        let sheet = ContactSheetView(
            title: "Design V2 Components - \(theme.displayName)",
            subtitle: "Real mac-harness overlay component previews rendered from the shared Design V2 token set.",
            states: states,
            imageMaxSize: NSSize(width: 450, height: 270)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 450, height: 270),
            outputRoot: outputURL.deletingLastPathComponent(),
            folderName: "components/\(theme.id)"
        )
    }

    @MainActor
    private static func renderFuelCalculatorStates(outputURL: URL) throws {
        let waitingView = FuelCalculatorView(
            frame: NSRect(origin: .zero, size: FuelCalculatorOverlayDefinition.definition.defaultSize),
            historyQueryService: SessionHistoryQueryService(userHistoryRoot: screenshotEmptyHistoryRoot())
        )
        waitingView.update(with: .empty)

        let earlyView = fuelView(sessionTime: FourHourRacePreview.mockStartRaceSeconds + 260)
        let middleView = fuelView(sessionTime: FourHourRacePreview.medianLapSeconds * 8.2)
        let stableFinishView = fuelView(sessionTime: FourHourRacePreview.medianLapSeconds * 29.2)
        stableFinishView.showAdvice = false

        let sheet = ContactSheetView(
            title: "Fuel Calculator States",
            subtitle: "Generated mac-harness previews for waiting, opening stint, mid-race planning, and stable finish states.",
            states: [
                ContactSheetState(title: "1. Waiting", note: "No fuel telemetry has arrived yet.", fileName: "waiting.png", image: try renderImage(waitingView)),
                ContactSheetState(title: "2. Opening stint", note: "Initial race-lap and stint target estimates.", fileName: "opening-stint.png", image: try renderImage(earlyView)),
                ContactSheetState(title: "3. Mid race", note: "Strategy rows and advice with live mock telemetry.", fileName: "mid-race.png", image: try renderImage(middleView)),
                ContactSheetState(title: "4. Stable finish", note: "Advice hidden while the table keeps the same row layout.", fileName: "stable-finish.png", image: try renderImage(stableFinishView))
            ],
            imageMaxSize: NSSize(width: 620, height: 310)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 620, height: 310),
            outputRoot: outputURL.deletingLastPathComponent()
        )
    }

    @MainActor
    private static func renderRelativeStates(outputURL: URL) throws {
        let waitingView = RelativeOverlayView(frame: NSRect(origin: .zero, size: RelativeOverlayDefinition.definition.defaultSize))
        waitingView.update(with: .empty)

        let liveView = relativeView(sessionTime: FourHourRacePreview.mockStartRaceSeconds + 360)
        let compactView = relativeView(sessionTime: FourHourRacePreview.mockStartRaceSeconds + 540)
        compactView.carsAhead = 1
        compactView.carsBehind = 1
        compactView.update(with: snapshot(from: mockFrame(sessionTime: FourHourRacePreview.mockStartRaceSeconds + 540)))

        var pitFrame = mockFrame(sessionTime: FourHourRacePreview.mockStartRaceSeconds + 780)
        pitFrame.onPitRoad = true
        let pitView = RelativeOverlayView(frame: NSRect(origin: .zero, size: RelativeOverlayDefinition.definition.defaultSize))
        pitView.update(with: snapshot(from: pitFrame))

        let sheet = ContactSheetView(
            title: "Relative States",
            subtitle: "Generated mac-harness previews for the first telemetry-first v2 overlay.",
            states: [
                ContactSheetState(title: "1. Waiting", note: "No fresh live telemetry yet.", fileName: "waiting.png", image: try renderImage(waitingView)),
                ContactSheetState(title: "2. Live relative", note: "Nearby cars around the local in-car reference.", fileName: "live-relative.png", image: try renderImage(liveView)),
                ContactSheetState(title: "3. Compact window", note: "User-limited ahead/behind row counts.", fileName: "compact-window.png", image: try renderImage(compactView)),
                ContactSheetState(title: "4. Pit context", note: "Local car pit state keeps the table stable.", fileName: "pit-context.png", image: try renderImage(pitView))
            ],
            imageMaxSize: NSSize(width: 620, height: 430)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 620, height: 430),
            outputRoot: outputURL.deletingLastPathComponent()
        )
    }

    @MainActor
    private static func renderTrackMapStates(outputURL: URL) throws {
        let normalView = trackMapView(
            sequence: 1,
            playbackSeconds: 3,
            highlights: [:],
            fullLapHighlight: nil
        )
        let sectorBestView = trackMapView(
            sequence: 2,
            playbackSeconds: 8,
            highlights: [
                0: LiveTrackSectorHighlights.personalBest,
                1: LiveTrackSectorHighlights.personalBest
            ],
            fullLapHighlight: nil
        )
        let bestLapView = trackMapView(
            sequence: 3,
            playbackSeconds: 14.1,
            highlights: [:],
            fullLapHighlight: LiveTrackSectorHighlights.bestLap
        )
        let followingSectorView = trackMapView(
            sequence: 4,
            playbackSeconds: 16.3,
            highlights: [0: LiveTrackSectorHighlights.personalBest],
            fullLapHighlight: nil
        )
        let purpleSectorView = trackMapView(
            sequence: 5,
            playbackSeconds: 23.4,
            highlights: [
                0: LiveTrackSectorHighlights.personalBest,
                1: LiveTrackSectorHighlights.bestLap
            ],
            fullLapHighlight: nil
        )

        let sheet = ContactSheetView(
            title: "Track Map Sector States",
            subtitle: "Generated mac-harness previews for normal, personal-best sector, full-lap best, and next-lap sector reset states.",
            states: [
                ContactSheetState(title: "1. Normal", note: "Base map remains continuous and white with live car markers.", fileName: "normal.png", image: try renderImage(normalView)),
                ContactSheetState(title: "2. Sector PB", note: "Completed personal-best sectors overlay green without breaking the base line.", fileName: "sector-personal-best.png", image: try renderImage(sectorBestView)),
                ContactSheetState(title: "3. Session Best Lap", note: "Finished session-best lap temporarily highlights the full track purple.", fileName: "session-best-lap.png", image: try renderImage(bestLapView)),
                ContactSheetState(title: "4. Following S1", note: "Next lap clears the full-lap color once sector 1 completes.", fileName: "following-sector-one.png", image: try renderImage(followingSectorView)),
                ContactSheetState(title: "5. Mixed Live Sectors", note: "Live sector state can show green and purple segments together.", fileName: "mixed-live-sectors.png", image: try renderImage(purpleSectorView))
            ],
            imageMaxSize: NSSize(width: 390, height: 390)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 390, height: 390),
            outputRoot: outputURL.deletingLastPathComponent()
        )
    }

    @MainActor
    private static func renderSettingsStates(outputURL: URL) throws {
        let fixture = settingsScreenshotFixture()
        let settings = fixture.settings
        let capture = fixture.capture

        let generalView = settingsView(settings: settings, capture: capture, selectedTab: "general")
        let supportView = settingsView(settings: settings, capture: capture, selectedTab: "error-logging")
        let standingsView = settingsView(settings: settings, capture: capture, selectedTab: StandingsOverlayDefinition.definition.id)
        let relativeView = settingsView(settings: settings, capture: capture, selectedTab: RelativeOverlayDefinition.definition.id)
        let gapView = settingsView(settings: settings, capture: capture, selectedTab: GapToLeaderOverlayDefinition.definition.id)
        let fuelView = settingsView(settings: settings, capture: capture, selectedTab: FuelCalculatorOverlayDefinition.definition.id)
        let sessionWeatherView = settingsView(settings: settings, capture: capture, selectedTab: SessionWeatherOverlayDefinition.definition.id)
        let pitServiceView = settingsView(settings: settings, capture: capture, selectedTab: PitServiceOverlayDefinition.definition.id)
        let trackMapView = settingsView(settings: settings, capture: capture, selectedTab: TrackMapOverlayDefinition.definition.id)
        let streamChatView = settingsView(settings: settings, capture: capture, selectedTab: StreamChatOverlayDefinition.definition.id)
        let inputStateView = settingsView(settings: settings, capture: capture, selectedTab: InputStateOverlayDefinition.definition.id)
        let carRadarView = settingsView(settings: settings, capture: capture, selectedTab: CarRadarOverlayDefinition.definition.id)
        let flagsView = settingsView(settings: settings, capture: capture, selectedTab: FlagsOverlayDefinition.definition.id)
        let garageCoverView = settingsView(settings: settings, capture: capture, selectedTab: GarageCoverOverlayDefinition.definition.id)

        let sheet = ContactSheetView(
            title: "Settings Window States",
            subtitle: "Generated mac-harness previews for the normal desktop settings window tabs.",
            states: [
                ContactSheetState(title: "1. General", note: "Shared units.", fileName: "general.png", image: try renderImage(generalView)),
                ContactSheetState(title: "2. Support", note: "App status, issue, bundle actions, diagnostic capture, storage, and app activity.", fileName: "support.png", image: try renderImage(supportView)),
                ContactSheetState(title: "3. Standings tab", note: "V2 content rows use session-state boxes without per-row sizing controls.", fileName: "standings-overlay.png", image: try renderImage(standingsView)),
                ContactSheetState(title: "4. Relative tab", note: "Relative uses the shared V2 content-row session matrix.", fileName: "overlay-tab.png", image: try renderImage(relativeView)),
                ContactSheetState(title: "5. Race-only tab", note: "Gap To Leader is fixed to race sessions, so redundant session filters are hidden.", fileName: "race-only-overlay.png", image: try renderImage(gapView)),
                ContactSheetState(title: "6. Fuel Calculator tab", note: "Shared V2 shell with fuel advice/source content switches.", fileName: "fuel-calculator-overlay.png", image: try renderImage(fuelView)),
                ContactSheetState(title: "7. Session / Weather tab", note: "Shared V2 shell with the current production content contract.", fileName: "session-weather-overlay.png", image: try renderImage(sessionWeatherView)),
                ContactSheetState(title: "8. Pit Service tab", note: "Shared V2 shell with the current production content contract.", fileName: "pit-service-overlay.png", image: try renderImage(pitServiceView)),
                ContactSheetState(title: "9. Track Map tab", note: "Bundled coverage, local browser route, map fill, and optional telemetry map generation.", fileName: "track-map-overlay.png", image: try renderImage(trackMapView)),
                ContactSheetState(title: "10. Stream Chat tab", note: "Native Twitch overlay plus browser-source Streamlabs/Twitch setup.", fileName: "stream-chat-overlay.png", image: try renderImage(streamChatView)),
                ContactSheetState(title: "11. Inputs tab", note: "Input rail content switches, without header/footer tabs.", fileName: "input-state-overlay.png", image: try renderImage(inputStateView)),
                ContactSheetState(title: "12. Car Radar tab", note: "Radar warning content, without header/footer tabs.", fileName: "car-radar-overlay.png", image: try renderImage(carRadarView)),
                ContactSheetState(title: "13. Flags tab", note: "Flag family switches and custom sizing.", fileName: "flags-overlay.png", image: try renderImage(flagsView)),
                ContactSheetState(title: "14. Garage Cover tab", note: "Localhost-only privacy cover image import.", fileName: "garage-cover-overlay.png", image: try renderImage(garageCoverView))
            ],
            imageMaxSize: NSSize(width: 650, height: 650)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 650, height: 650),
            outputRoot: outputURL.deletingLastPathComponent()
        )
        try renderSettingsComponentCrops(
            settings: settings,
            capture: capture,
            outputURL: outputURL.deletingLastPathComponent().appendingPathComponent("settings-components.png")
        )
    }

    private static func settingsScreenshotFixture() -> (settings: ApplicationSettings, capture: TelemetryCaptureStatusSnapshot) {
        var settings = ApplicationSettings()
        for definition in settingsOverlayDefinitions() {
            settings.updateOverlay(OverlaySettings(
                id: definition.id,
                width: definition.defaultSize.width,
                height: definition.defaultSize.height
            ))
        }

        let capture = TelemetryCaptureStatusSnapshot(
            isConnected: true,
            isCapturing: true,
            rawCaptureEnabled: false,
            rawCaptureActive: false,
            captureRoot: AppPaths.captureRoot(),
            currentCaptureDirectory: nil,
            lastCaptureDirectory: AppPaths.captureRoot().appendingPathComponent("capture-20260428-150000-000"),
            frameCount: 14_420,
            writtenFrameCount: 0,
            droppedFrameCount: 0,
            telemetryFileBytes: nil,
            captureStartedAtUtc: Date(timeIntervalSince1970: 1_800_000_000),
            lastFrameCapturedAtUtc: Date(),
            lastDiskWriteAtUtc: nil,
            appWarning: "Local build is older than source files.",
            lastWarning: nil,
            lastError: nil,
            lastIssueAtUtc: Date()
        )

        return (settings, capture)
    }

    @MainActor
    private static func renderSettingsComponentCrops(outputURL: URL) throws {
        let fixture = settingsScreenshotFixture()
        try renderSettingsComponentCrops(
            settings: fixture.settings,
            capture: fixture.capture,
            outputURL: outputURL
        )
    }

    @MainActor
    private static func renderSettingsComponentCrops(
        settings: ApplicationSettings,
        capture: TelemetryCaptureStatusSnapshot,
        outputURL: URL
    ) throws {
        let general = try renderImage(settingsView(settings: settings, capture: capture, selectedTab: "general"))
        let support = try renderImage(settingsView(settings: settings, capture: capture, selectedTab: "error-logging"))
        let relativeGeneral = try renderImage(settingsView(settings: settings, capture: capture, selectedTab: RelativeOverlayDefinition.definition.id))
        let relativeContent = try renderImage(settingsView(
            settings: settings,
            capture: capture,
            selectedTab: RelativeOverlayDefinition.definition.id,
            selectedRegion: DesignV2SettingsRegion.content.rawValue
        ))
        let streamChatContent = try renderImage(settingsView(
            settings: settings,
            capture: capture,
            selectedTab: StreamChatOverlayDefinition.definition.id,
            selectedRegion: DesignV2SettingsRegion.content.rawValue
        ))
        func matchedWindowCrop(_ rect: NSRect) -> NSRect {
            rect.offsetBy(
                dx: -DesignV2SettingsChrome.matchedWindowBoundsOrigin.x,
                dy: -DesignV2SettingsChrome.matchedWindowBoundsOrigin.y
            )
        }

        let components = [
            ContactSheetState(
                title: "1. Sidebar Tabs",
                note: "Actual V2 settings navigation tab states.",
                fileName: "sidebar-tabs.png",
                image: try cropImage(general, rect: matchedWindowCrop(NSRect(x: 64, y: 116, width: 190, height: 506)))
            ),
            ContactSheetState(
                title: "2. Region Tabs",
                note: "General, Content, Header, and Footer segmented tabs.",
                fileName: "region-tabs.png",
                image: try cropImage(relativeGeneral, rect: matchedWindowCrop(NSRect(x: 300, y: 198, width: 420, height: 52)))
            ),
            ContactSheetState(
                title: "3. Unit Choice",
                note: "Metric/Imperial segmented input inside a panel.",
                fileName: "unit-choice.png",
                image: try cropImage(general, rect: matchedWindowCrop(NSRect(x: 306, y: 214, width: 392, height: 132)))
            ),
            ContactSheetState(
                title: "4. Overlay Controls",
                note: "Toggle, sliders, session checks, and panel spacing.",
                fileName: "overlay-controls.png",
                image: try cropImage(relativeGeneral, rect: matchedWindowCrop(NSRect(x: 306, y: 272, width: 392, height: 226)))
            ),
            ContactSheetState(
                title: "5. Content Matrix",
                note: "Content rows with session-state checkbox columns.",
                fileName: "content-matrix.png",
                image: try cropImage(relativeContent, rect: matchedWindowCrop(NSRect(x: 306, y: 272, width: 690, height: 222)))
            ),
            ContactSheetState(
                title: "6. Chat Inputs",
                note: "Choice control, text fields, save button, and labels.",
                fileName: "chat-inputs.png",
                image: try cropImage(streamChatContent, rect: matchedWindowCrop(NSRect(x: 306, y: 272, width: 650, height: 204)))
            ),
            ContactSheetState(
                title: "7. Support Buttons",
                note: "Action button row density and update controls.",
                fileName: "support-buttons.png",
                image: try cropImage(support, rect: matchedWindowCrop(NSRect(x: 306, y: 410, width: 650, height: 174)))
            ),
            ContactSheetState(
                title: "8. Browser Source",
                note: "Localhost block and copy action alignment.",
                fileName: "browser-source.png",
                image: try cropImage(relativeGeneral, rect: matchedWindowCrop(NSRect(x: 306, y: 518, width: 650, height: 70)))
            )
        ]

        let sheet = ContactSheetView(
            title: "Settings V2 Components",
            subtitle: "Cropped from the actual mac settings V2 surface for focused parity review.",
            states: components,
            imageMaxSize: NSSize(width: 690, height: 520)
        )
        try render(sheet, to: outputURL)

        let componentRoot = outputURL.deletingLastPathComponent().appendingPathComponent("components", isDirectory: true)
        try FileManager.default.createDirectory(at: componentRoot, withIntermediateDirectories: true)
        for component in components {
            try writePNG(component.image, to: componentRoot.appendingPathComponent(component.fileName))
        }
    }

    @MainActor
    private static func renderGapToLeaderScreenshot(outputURL: URL) throws {
        let store = LiveTelemetryStore()
        let view = GapToLeaderView(frame: NSRect(origin: .zero, size: GapToLeaderOverlayDefinition.definition.defaultSize))
        store.markConnected()
        store.markCollectionStarted(sourceId: "screenshot", startedAtUtc: Date(timeIntervalSince1970: 1_800_000_000))

        for step in 0...140 {
            let sessionTime = FourHourRacePreview.mockStartRaceSeconds + Double(step) * 8
            let frame = MockLiveTelemetryFrame.mock(
                capturedAtUtc: Date(timeIntervalSince1970: 1_800_000_000 + Double(step)),
                sessionTime: sessionTime,
                fuelLevelLiters: FourHourRacePreview.fuelLevelLiters(sessionTime: sessionTime),
                fuelUsePerHourLiters: FourHourRacePreview.fuelUsePerHourLiters
            )
            store.recordFrame(frame)
            view.update(with: store.snapshot())
        }

        try render(view, to: outputURL)
    }

    @MainActor
    private static func renderRadarScreenshot(outputURL: URL) throws {
        let view = CarRadarView(frame: NSRect(origin: .zero, size: CarRadarOverlayDefinition.definition.defaultSize))
        let sessionTime = FourHourRacePreview.mockStartRaceSeconds + 120
        let frame = MockLiveTelemetryFrame.mock(
            capturedAtUtc: Date(timeIntervalSince1970: 1_800_000_200),
            sessionTime: sessionTime,
            fuelLevelLiters: FourHourRacePreview.fuelLevelLiters(sessionTime: sessionTime),
            fuelUsePerHourLiters: FourHourRacePreview.fuelUsePerHourLiters
        )
        var snapshot = LiveTelemetrySnapshot.empty
        snapshot.isConnected = true
        snapshot.isCollecting = true
        snapshot.lastUpdatedAtUtc = Date()
        snapshot.sequence = 1
        snapshot.latestFrame = frame
        snapshot.fuel = LiveFuelSnapshot.from(frame)
        snapshot.proximity = LiveProximitySnapshot.from(frame)
        snapshot.leaderGap = LiveLeaderGapSnapshot.from(frame)
        view.update(with: snapshot)

        try render(view, to: outputURL)
    }

    @MainActor
    private static func renderRadarStates(outputURL: URL) throws {
        let clearView = CarRadarView(frame: NSRect(origin: .zero, size: CarRadarOverlayDefinition.definition.defaultSize))
        clearView.update(with: .empty)

        let sidePressureView = CarRadarView(frame: NSRect(origin: .zero, size: CarRadarOverlayDefinition.definition.defaultSize))
        sidePressureView.update(with: radarSnapshot(proximity: LiveProximitySnapshot(
            hasData: true,
            carLeftRight: 4,
            referenceCarClass: 4098,
            referenceCarClassColorHex: "#FFDA59",
            sideStatus: "both sides",
            hasCarLeft: true,
            hasCarRight: true,
            nearbyCars: [
                LiveProximityCar(
                    carIdx: 12,
                    relativeLaps: 1.2 / FourHourRacePreview.medianLapSeconds,
                    relativeSeconds: 1.2,
                    relativeMeters: nil,
                    overallPosition: 8,
                    classPosition: 6,
                    carClass: 4098,
                    carClassColorHex: "#FFDA59",
                    onPitRoad: false
                ),
                LiveProximityCar(
                    carIdx: 14,
                    relativeLaps: -1.4 / FourHourRacePreview.medianLapSeconds,
                    relativeSeconds: -1.4,
                    relativeMeters: nil,
                    overallPosition: 10,
                    classPosition: 8,
                    carClass: 4098,
                    carClassColorHex: "#FFDA59",
                    onPitRoad: false
                )
            ],
            nearestAhead: nil,
            nearestBehind: nil,
            multiclassApproaches: [],
            strongestMulticlassApproach: nil,
            sideOverlapWindowSeconds: 0.22
        )))

        let multiclassView = CarRadarView(frame: NSRect(origin: .zero, size: CarRadarOverlayDefinition.definition.defaultSize))
        let multiclassFrame = mockFrame(sessionTime: FourHourRacePreview.mockStartRaceSeconds + 120)
        multiclassView.update(with: snapshot(from: multiclassFrame))

        let errorView = CarRadarView(frame: NSRect(origin: .zero, size: CarRadarOverlayDefinition.definition.defaultSize))
        errorView.showOverlayError("refresh: invalid proximity sample")

        let sheet = ContactSheetView(
            title: "Car Radar States",
            subtitle: "Generated mac-harness previews for hidden, close traffic, multiclass warning, and error states.",
            states: [
                ContactSheetState(
                    title: "1. Clear track",
                    note: "No nearby cars or multiclass warning; production overlay stays hidden.",
                    fileName: "clear-track.png",
                    image: try renderImage(clearView)
                ),
                ContactSheetState(
                    title: "2. Side pressure",
                    note: "Close class traffic with scalar left/right occupancy from telemetry.",
                    fileName: "side-pressure.png",
                    image: try renderImage(sidePressureView)
                ),
                ContactSheetState(
                    title: "3. Faster class approaching",
                    note: "Faster other-class traffic outside the close radar range.",
                    fileName: "multiclass-approaching.png",
                    image: try renderImage(multiclassView)
                ),
                ContactSheetState(
                    title: "4. Error reporting",
                    note: "Visible fallback when an unexpected refresh/render failure is logged.",
                    fileName: "error-reporting.png",
                    image: try renderImage(errorView)
                )
            ],
            imageMaxSize: NSSize(width: 330, height: 330)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 330, height: 330),
            outputRoot: outputURL.deletingLastPathComponent()
        )
    }

    @MainActor
    private static func renderGapToLeaderStates(outputURL: URL) throws {
        if let rawCapture = try loadRawGapCapture() {
            try renderRawPracticeGapStates(rawCapture, outputURL: outputURL)
            return
        }

        let waitingView = GapToLeaderView(frame: NSRect(origin: .zero, size: GapToLeaderOverlayDefinition.definition.defaultSize))
        waitingView.update(with: .empty)

        let tightFieldView = try gapToLeaderView(
            startSessionTime: FourHourRacePreview.mockStartRaceSeconds + 20,
            stepCount: 70,
            stepSeconds: 3
        )
        let handoffView = try gapToLeaderView(
            startSessionTime: FourHourRacePreview.mockStartRaceSeconds,
            stepCount: 140,
            stepSeconds: 8
        )
        let spreadView = try gapToLeaderView(
            startSessionTime: 8_200,
            stepCount: 180,
            stepSeconds: 8
        )

        let sheet = ContactSheetView(
            title: "Gap To Leader States",
            subtitle: "Generated mac-harness previews for waiting, tight field, pit/weather handoff, and long-run spread states.",
            states: [
                ContactSheetState(
                    title: "1. Waiting for timing",
                    note: "No class timing rows have arrived yet.",
                    fileName: "waiting-for-timing.png",
                    image: try renderImage(waitingView)
                ),
                ContactSheetState(
                    title: "2. Tight early field",
                    note: "Adaptive Y scale stays tight while the class pack is still compressed.",
                    fileName: "tight-early-field.png",
                    image: try renderImage(tightFieldView)
                ),
                ContactSheetState(
                    title: "3. Pit, weather, handoff",
                    note: "Shows pit loss, wet-condition banding, and the Dafydd-to-Simon marker.",
                    fileName: "pit-weather-handoff.png",
                    image: try renderImage(handoffView)
                ),
                ContactSheetState(
                    title: "4. Long-run spread",
                    note: "Context lines dim while current positions update as the field spreads.",
                    fileName: "long-run-spread.png",
                    image: try renderImage(spreadView)
                )
            ],
            imageMaxSize: NSSize(width: 720, height: 335)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 720, height: 335),
            outputRoot: outputURL.deletingLastPathComponent()
        )
    }

    @MainActor
    private static func renderRawPracticeGapStates(_ capture: RawPracticeGapCapture, outputURL: URL) throws {
        let states = try capture.states.map { state in
            let view = rawPracticeGapView(capture: capture, throughFrameIndex: state.frameIndex)
            return ContactSheetState(
                title: state.title,
                note: state.note,
                fileName: state.fileName,
                image: try renderImage(view)
            )
        }

        let sheet = ContactSheetView(
            title: "Gap To Leader - Raw Practice Capture",
            subtitle: "\(capture.captureId): spectated practice timing from decoded CarIdxF2Time/class rows.",
            states: states,
            imageMaxSize: NSSize(width: 720, height: 335)
        )
        try render(sheet, to: outputURL)
        try renderStateCards(
            states: sheet.states,
            imageMaxSize: NSSize(width: 720, height: 335),
            outputRoot: outputURL.deletingLastPathComponent()
        )
    }

    @MainActor
    private static func rawPracticeGapView(capture: RawPracticeGapCapture, throughFrameIndex frameIndex: Int) -> GapToLeaderView {
        let view = GapToLeaderView(frame: NSRect(origin: .zero, size: GapToLeaderOverlayDefinition.definition.defaultSize))
        view.isPaceTimingMode = true
        view.visibleTrendWindowSeconds = capture.visibleTrendWindowSeconds
        for frame in capture.frames where frame.frameIndex <= frameIndex {
            view.update(with: frame.snapshot(lapReferenceSeconds: capture.lapReferenceSeconds))
        }

        return view
    }

    private static func loadRawGapCapture() throws -> RawPracticeGapCapture? {
        guard let path = ProcessInfo.processInfo.environment["TMR_MAC_GAP_CAPTURE_JSON"],
              !path.isEmpty else {
            return nil
        }

        let url = URL(fileURLWithPath: path)
        return try RawPracticeGapCapture.load(from: url)
    }

    @MainActor
    private static func gapToLeaderView(
        startSessionTime: TimeInterval,
        stepCount: Int,
        stepSeconds: TimeInterval
    ) throws -> GapToLeaderView {
        let store = LiveTelemetryStore()
        let view = GapToLeaderView(frame: NSRect(origin: .zero, size: GapToLeaderOverlayDefinition.definition.defaultSize))
        store.markConnected()
        store.markCollectionStarted(sourceId: "screenshot", startedAtUtc: Date(timeIntervalSince1970: 1_800_100_000))

        for step in 0...stepCount {
            let sessionTime = startSessionTime + Double(step) * stepSeconds
            let frame = mockFrame(
                capturedAtUtc: Date(timeIntervalSince1970: 1_800_100_000 + Double(step)),
                sessionTime: sessionTime
            )
            store.recordFrame(frame)
            view.update(with: store.snapshot())
        }

        return view
    }

    @MainActor
    private static func render(_ view: NSView, to outputURL: URL) throws {
        let image = try renderImage(view)
        try writePNG(image, to: outputURL)
    }

    @MainActor
    private static func renderStateCards(
        states: [ContactSheetState],
        imageMaxSize: NSSize,
        outputRoot: URL,
        folderName: String = "states"
    ) throws {
        let stateRoot = outputRoot.appendingPathComponent(folderName, isDirectory: true)
        try FileManager.default.createDirectory(at: stateRoot, withIntermediateDirectories: true)
        for state in states {
            let view = StateCardView(state: state, imageMaxSize: imageMaxSize)
            try render(view, to: stateRoot.appendingPathComponent(state.fileName))
        }
    }

    private static func writePNG(_ image: NSImage, to outputURL: URL) throws {
        guard let data = image.tiffRepresentation,
              let bitmap = NSBitmapImageRep(data: data),
              let png = bitmap.representation(using: .png, properties: [:]) else {
            throw ScreenshotError.pngEncodingFailed(outputURL.lastPathComponent)
        }

        try png.write(to: outputURL)
    }

    @MainActor
    private static func renderImage(_ view: NSView) throws -> NSImage {
        view.layoutSubtreeIfNeeded()
        guard let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else {
            throw ScreenshotError.bitmapCreationFailed(String(describing: type(of: view)))
        }

        bitmap.size = view.bounds.size
        view.cacheDisplay(in: view.bounds, to: bitmap)
        let image = NSImage(size: view.bounds.size)
        image.addRepresentation(bitmap)
        return image
    }

    private static func cropImage(_ image: NSImage, rect: NSRect) throws -> NSImage {
        guard let cgImage = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
            throw ScreenshotError.cropFailed("missing CGImage")
        }

        let scaleX = CGFloat(cgImage.width) / max(CGFloat(1), image.size.width)
        let scaleY = CGFloat(cgImage.height) / max(CGFloat(1), image.size.height)
        let cropRect = CGRect(
            x: rect.minX * scaleX,
            y: rect.minY * scaleY,
            width: rect.width * scaleX,
            height: rect.height * scaleY
        ).integral
        guard let cropped = cgImage.cropping(to: cropRect) else {
            throw ScreenshotError.cropFailed("\(Int(rect.width))x\(Int(rect.height))")
        }

        return NSImage(cgImage: cropped, size: rect.size)
    }

    private static func mockFrame(
        capturedAtUtc: Date = Date(timeIntervalSince1970: 1_800_000_200),
        sessionTime: TimeInterval
    ) -> MockLiveTelemetryFrame {
        MockLiveTelemetryFrame.mock(
            capturedAtUtc: capturedAtUtc,
            sessionTime: sessionTime,
            fuelLevelLiters: FourHourRacePreview.fuelLevelLiters(sessionTime: sessionTime),
            fuelUsePerHourLiters: FourHourRacePreview.fuelUsePerHourLiters
        )
    }

    private static func snapshot(from frame: MockLiveTelemetryFrame) -> LiveTelemetrySnapshot {
        var snapshot = LiveTelemetrySnapshot.empty
        snapshot.isConnected = true
        snapshot.isCollecting = true
        snapshot.lastUpdatedAtUtc = Date()
        snapshot.sequence = 1
        snapshot.latestFrame = frame
        snapshot.fuel = LiveFuelSnapshot.from(frame)
        snapshot.proximity = LiveProximitySnapshot.from(frame)
        snapshot.leaderGap = LiveLeaderGapSnapshot.from(frame)
        return snapshot
    }

    private static func radarSnapshot(proximity: LiveProximitySnapshot) -> LiveTelemetrySnapshot {
        var snapshot = LiveTelemetrySnapshot.empty
        snapshot.isConnected = true
        snapshot.isCollecting = true
        snapshot.lastUpdatedAtUtc = Date()
        snapshot.sequence = 1
        snapshot.proximity = proximity
        return snapshot
    }

    private static func screenshotEmptyHistoryRoot() -> URL {
        FileManager.default.temporaryDirectory.appendingPathComponent(
            "tmroverlay-screenshot-empty-history-\(UUID().uuidString)",
            isDirectory: true
        )
    }

    @MainActor
    private static func fuelView(sessionTime: TimeInterval) -> FuelCalculatorView {
        let view = FuelCalculatorView(
            frame: NSRect(origin: .zero, size: FuelCalculatorOverlayDefinition.definition.defaultSize),
            historyQueryService: SessionHistoryQueryService(userHistoryRoot: AppPaths.historyRoot())
        )
        view.update(with: snapshot(from: mockFrame(sessionTime: sessionTime)))
        return view
    }

    @MainActor
    private static func relativeView(sessionTime: TimeInterval) -> RelativeOverlayView {
        let view = RelativeOverlayView(frame: NSRect(origin: .zero, size: RelativeOverlayDefinition.definition.defaultSize))
        view.update(with: snapshot(from: mockFrame(sessionTime: sessionTime)))
        return view
    }

    @MainActor
    private static func trackMapView(
        sequence: Int,
        playbackSeconds: TimeInterval,
        highlights: [Int: String],
        fullLapHighlight: String?
    ) -> TrackMapView {
        let view = TrackMapView(frame: NSRect(origin: .zero, size: TrackMapOverlayDefinition.definition.defaultSize))
        view.update(with: trackMapSnapshot(
            sequence: sequence,
            playbackSeconds: playbackSeconds,
            highlights: highlights,
            fullLapHighlight: fullLapHighlight
        ))
        return view
    }

    private static func trackMapSnapshot(
        sequence: Int,
        playbackSeconds: TimeInterval,
        highlights: [Int: String],
        fullLapHighlight: String?
    ) -> LiveTelemetrySnapshot {
        let capturedAtUtc = Date(timeIntervalSince1970: 1_800_200_000 + Double(sequence))
        let frame = trackMapFrame(capturedAtUtc: capturedAtUtc, playbackSeconds: playbackSeconds)
        var snapshot = LiveTelemetrySnapshot.empty
        snapshot.isConnected = true
        snapshot.isCollecting = true
        snapshot.sourceId = "track-map-screenshot"
        snapshot.startedAtUtc = capturedAtUtc.addingTimeInterval(-playbackSeconds)
        snapshot.lastUpdatedAtUtc = capturedAtUtc
        snapshot.sequence = sequence
        snapshot.combo = .mockNurburgringMercedesRace
        snapshot.latestFrame = frame
        snapshot.fuel = LiveFuelSnapshot.from(frame)
        snapshot.proximity = LiveProximitySnapshot.from(frame)
        snapshot.leaderGap = LiveLeaderGapSnapshot.from(frame)
        snapshot.models = LiveRaceModels(trackMap: trackMapModel(
            highlights: highlights,
            fullLapHighlight: fullLapHighlight
        ))
        return snapshot
    }

    private static func trackMapFrame(
        capturedAtUtc: Date,
        playbackSeconds: TimeInterval
    ) -> MockLiveTelemetryFrame {
        let lapOneSeconds = 14.0
        let lapTwoSeconds = 12.0
        let lapThreeSeconds = 14.0
        let lapProgress: Double
        if playbackSeconds < lapOneSeconds {
            lapProgress = playbackSeconds / lapOneSeconds
        } else if playbackSeconds < lapOneSeconds + lapTwoSeconds {
            lapProgress = 1.0 + (playbackSeconds - lapOneSeconds) / lapTwoSeconds
        } else {
            lapProgress = 2.0 + (playbackSeconds - lapOneSeconds - lapTwoSeconds) / lapThreeSeconds
        }

        let teamLapCompleted = Int(lapProgress.rounded(.down))
        let teamLapDistPct = lapProgress.truncatingRemainder(dividingBy: 1)
        let justCompletedLap = teamLapCompleted > 0 && teamLapDistPct < 0.045
        var frame = mockFrame(
            capturedAtUtc: capturedAtUtc,
            sessionTime: playbackSeconds
        )
        frame.estimatedLapSeconds = lapProgress < 1 ? lapOneSeconds : (lapProgress < 2 ? lapTwoSeconds : lapThreeSeconds)
        frame.teamLapCompleted = teamLapCompleted
        frame.teamLapDistPct = teamLapDistPct
        frame.leaderLapCompleted = teamLapCompleted
        frame.leaderLapDistPct = teamLapDistPct
        frame.lastLapTimeSeconds = justCompletedLap ? (teamLapCompleted == 1 ? lapOneSeconds : lapTwoSeconds) : nil
        frame.bestLapTimeSeconds = justCompletedLap ? min(lapOneSeconds, lapTwoSeconds) : nil
        frame.lapDeltaToSessionBestLapSeconds = justCompletedLap ? (teamLapCompleted == 1 ? 0.0 : 1.2) : nil
        frame.lapDeltaToSessionBestLapOk = justCompletedLap ? true : nil
        return frame
    }

    private static func trackMapModel(
        highlights: [Int: String],
        fullLapHighlight: String?
    ) -> LiveTrackMapModel {
        let sectors = [
            LiveTrackSectorSegment(sectorNum: 0, startPct: 0.0, endPct: 0.18, highlight: LiveTrackSectorHighlights.none),
            LiveTrackSectorSegment(sectorNum: 1, startPct: 0.18, endPct: 0.36, highlight: LiveTrackSectorHighlights.none),
            LiveTrackSectorSegment(sectorNum: 2, startPct: 0.36, endPct: 0.54, highlight: LiveTrackSectorHighlights.none),
            LiveTrackSectorSegment(sectorNum: 3, startPct: 0.54, endPct: 0.72, highlight: LiveTrackSectorHighlights.none),
            LiveTrackSectorSegment(sectorNum: 4, startPct: 0.72, endPct: 0.88, highlight: LiveTrackSectorHighlights.none),
            LiveTrackSectorSegment(sectorNum: 5, startPct: 0.88, endPct: 1.0, highlight: LiveTrackSectorHighlights.none)
        ]
        return LiveTrackMapModel(
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

    @MainActor
    private static func settingsView(
        settings: ApplicationSettings,
        capture: TelemetryCaptureStatusSnapshot,
        selectedTab: String,
        selectedRegion: String? = nil
    ) -> SettingsOverlayView {
        let view = SettingsOverlayView(
            settings: settings,
            captureSnapshot: capture,
            overlayDefinitions: settingsOverlayDefinitions(),
            onSettingsChanged: { _ in },
            rawCaptureChanged: { _ in true },
            selectedOverlayChanged: { _ in }
        )
        view.frame = NSRect(origin: .zero, size: SettingsOverlayDefinition.definition.defaultSize)
        view.selectTab(identifier: selectedTab)
        if let selectedRegion {
            view.selectRegion(identifier: selectedRegion)
        }
        view.updateCaptureStatus(capture)
        return view
    }

    private static func settingsOverlayDefinitions() -> [OverlayDefinition] {
        [
            StatusOverlayDefinition.definition,
            StandingsOverlayDefinition.definition,
            FuelCalculatorOverlayDefinition.definition,
            RelativeOverlayDefinition.definition,
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

    private enum ScreenshotError: Error {
        case bitmapCreationFailed(String)
        case pngEncodingFailed(String)
        case cropFailed(String)
    }
}

private struct RawGapCapture: Decodable {
    var captureId: String
    var lapReferenceSeconds: Double
    var visibleTrendWindowSeconds: Double
    var frames: [RawGapFrame]
    var states: [RawGapState]
}

private struct RawGapState: Decodable {
    var title: String
    var note: String
    var fileName: String
    var frameIndex: Int
}

private struct RawGapFrame: Decodable {
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
    var classCars: [RawGapClassCar]

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

private struct RawGapClassCar: Decodable {
    var carIdx: Int
    var isReferenceCar: Bool
    var isClassLeader: Bool
    var classPosition: Int?
    var gapSecondsToClassLeader: Double?
    var deltaSecondsToReference: Double?
    var carClassColorHex: String?

    var liveCar: LiveClassGapCar {
        LiveClassGapCar(
            carIdx: carIdx,
            isReferenceCar: isReferenceCar,
            isClassLeader: isClassLeader,
            classPosition: classPosition,
            gapSecondsToClassLeader: gapSecondsToClassLeader,
            gapLapsToClassLeader: gapSecondsToClassLeader == nil ? nil : 0,
            deltaSecondsToReference: deltaSecondsToReference,
            carClassColorHex: carClassColorHex
        )
    }
}

private struct ContactSheetState {
    var title: String
    var note: String
    var fileName: String
    var image: NSImage
}

private final class ContactSheetView: NSView {
    private let title: String
    private let subtitle: String
    let states: [ContactSheetState]
    private let imageMaxSize: NSSize

    override var isFlipped: Bool {
        true
    }

    init(
        title: String,
        subtitle: String,
        states: [ContactSheetState],
        imageMaxSize: NSSize
    ) {
        self.title = title
        self.subtitle = subtitle
        self.states = states
        self.imageMaxSize = imageMaxSize
        let columns = states.count > 4 ? 3 : 2
        let rows = max(1, Int(ceil(Double(states.count) / Double(columns))))
        let cardWidth: CGFloat = 825
        let cardHeight: CGFloat = 560
        let gap: CGFloat = 50
        let width = 100 + CGFloat(columns) * cardWidth + CGFloat(max(0, columns - 1)) * gap
        let height = 230 + CGFloat(rows) * cardHeight + CGFloat(max(0, rows - 1)) * gap
        super.init(frame: NSRect(x: 0, y: 0, width: width, height: height))
        wantsLayer = false
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        NSColor(red255: 9, green: 16, blue: 18).setFill()
        bounds.fill()

        drawText(
            title,
            at: NSPoint(x: 60, y: 34),
            font: NSFont.systemFont(ofSize: 30, weight: .bold),
            color: NSColor(red255: 244, green: 248, blue: 249)
        )
        drawText(
            subtitle,
            at: NSPoint(x: 60, y: 68),
            font: NSFont.systemFont(ofSize: 15, weight: .regular),
            color: NSColor(red255: 159, green: 176, blue: 186)
        )

        let cardWidth: CGFloat = 825
        let cardHeight: CGFloat = 560
        let columns = states.count > 4 ? 3 : 2
        let gap: CGFloat = 50
        for (index, state) in states.enumerated() {
            let column = index % columns
            let row = index / columns
            let origin = NSPoint(
                x: 50 + CGFloat(column) * (cardWidth + gap),
                y: 120 + CGFloat(row) * (cardHeight + gap)
            )
            drawCard(state, frame: NSRect(origin: origin, size: NSSize(width: cardWidth, height: cardHeight)))
        }
    }

    private func drawCard(_ state: ContactSheetState, frame: NSRect) {
        let shadow = NSShadow()
        shadow.shadowOffset = NSSize(width: 0, height: -12)
        shadow.shadowBlurRadius = 18
        shadow.shadowColor = NSColor(calibratedWhite: 0, alpha: 0.34)

        NSGraphicsContext.saveGraphicsState()
        shadow.set()
        NSColor(red255: 17, green: 25, blue: 29).setFill()
        NSBezierPath(roundedRect: frame, xRadius: 18, yRadius: 18).fill()
        NSGraphicsContext.restoreGraphicsState()

        NSColor(red255: 37, green: 51, blue: 58).setStroke()
        NSBezierPath(roundedRect: frame, xRadius: 18, yRadius: 18).stroke()

        drawText(
            state.title,
            at: NSPoint(x: frame.minX + 24, y: frame.minY + 28),
            font: NSFont.systemFont(ofSize: 18, weight: .bold),
            color: NSColor(red255: 231, green: 238, blue: 242)
        )
        drawText(
            state.note,
            at: NSPoint(x: frame.minX + 24, y: frame.minY + 54),
            font: NSFont.systemFont(ofSize: 12, weight: .regular),
            color: NSColor(red255: 143, green: 160, blue: 170)
        )

        let imageRect = fittedImageRect(
            imageSize: state.image.size,
            maxSize: imageMaxSize,
            centeredIn: NSRect(
                x: frame.minX + 36,
                y: frame.minY + 96,
                width: frame.width - 72,
                height: frame.height - 132
            )
        )
        state.image.draw(in: imageRect)
    }

    private func fittedImageRect(imageSize: NSSize, maxSize: NSSize, centeredIn rect: NSRect) -> NSRect {
        let ratio = min(maxSize.width / imageSize.width, maxSize.height / imageSize.height, rect.width / imageSize.width, rect.height / imageSize.height)
        let size = NSSize(width: imageSize.width * ratio, height: imageSize.height * ratio)
        return NSRect(
            x: rect.midX - size.width / 2,
            y: rect.midY - size.height / 2,
            width: size.width,
            height: size.height
        )
    }

    private func drawText(_ text: String, at point: NSPoint, font: NSFont, color: NSColor) {
        NSString(string: text).draw(
            at: point,
            withAttributes: [
                .font: font,
                .foregroundColor: color
            ]
        )
    }
}

private final class StateCardView: NSView {
    private let state: ContactSheetState
    private let imageMaxSize: NSSize

    override var isFlipped: Bool {
        true
    }

    init(state: ContactSheetState, imageMaxSize: NSSize) {
        self.state = state
        self.imageMaxSize = imageMaxSize
        super.init(frame: NSRect(x: 0, y: 0, width: 900, height: 760))
        wantsLayer = false
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        NSColor(red255: 9, green: 16, blue: 18).setFill()
        bounds.fill()

        let cardFrame = bounds.insetBy(dx: 40, dy: 40)
        NSColor(red255: 17, green: 25, blue: 29).setFill()
        NSBezierPath(roundedRect: cardFrame, xRadius: 18, yRadius: 18).fill()
        NSColor(red255: 37, green: 51, blue: 58).setStroke()
        NSBezierPath(roundedRect: cardFrame, xRadius: 18, yRadius: 18).stroke()

        drawText(
            state.title,
            at: NSPoint(x: cardFrame.minX + 28, y: cardFrame.minY + 28),
            font: NSFont.systemFont(ofSize: 22, weight: .bold),
            color: NSColor(red255: 231, green: 238, blue: 242)
        )
        drawText(
            state.note,
            at: NSPoint(x: cardFrame.minX + 28, y: cardFrame.minY + 62),
            font: NSFont.systemFont(ofSize: 14, weight: .regular),
            color: NSColor(red255: 143, green: 160, blue: 170)
        )

        let imageRect = fittedImageRect(
            imageSize: state.image.size,
            maxSize: imageMaxSize,
            centeredIn: NSRect(
                x: cardFrame.minX + 36,
                y: cardFrame.minY + 112,
                width: cardFrame.width - 72,
                height: cardFrame.height - 148
            )
        )
        state.image.draw(in: imageRect)
    }

    private func fittedImageRect(imageSize: NSSize, maxSize: NSSize, centeredIn rect: NSRect) -> NSRect {
        let ratio = min(maxSize.width / imageSize.width, maxSize.height / imageSize.height, rect.width / imageSize.width, rect.height / imageSize.height)
        let size = NSSize(width: imageSize.width * ratio, height: imageSize.height * ratio)
        return NSRect(
            x: rect.midX - size.width / 2,
            y: rect.midY - size.height / 2,
            width: size.width,
            height: size.height
        )
    }

    private func drawText(_ text: String, at point: NSPoint, font: NSFont, color: NSColor) {
        NSString(string: text).draw(
            at: point,
            withAttributes: [
                .font: font,
                .foregroundColor: color
            ]
        )
    }
}
