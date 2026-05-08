import AppKit

private struct DemoOverlayState {
    let title: String
    let makeSnapshot: () -> TelemetryCaptureStatusSnapshot
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let state = TelemetryCaptureState()
    private let liveTelemetryStore = LiveTelemetryStore()
    private var captureService: MockTelemetryCaptureService?
    private var menuController: MenuController?
    private var overlayManager: OverlayManager?
    private var refreshTimer: Timer?
    private var logWriter: LocalLogWriter?
    private var eventRecorder: AppEventRecorder?
    private var runtimeStateStore: RuntimeStateStore?
    private var performanceLogWriter: PerformanceLogWriter?
    private var liveOverlayDiagnosticsRecorder: LiveOverlayDiagnosticsRecorder?
    private var performanceTimer: Timer?
    private var demoStates: [DemoOverlayState] = []
    private var demoSnapshotIndex: Int?
    private var demoTimer: Timer?
    private var radarCaptureDemoActive = false
    private var radarCaptureDemoStartedAtUtc: Date?
    private var mockTelemetryStarted = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)

        let appDataRoot = AppPaths.appDataRoot()
        let captureRoot = AppPaths.captureRoot()
        let historyRoot = AppPaths.historyRoot()
        let logsRoot = AppPaths.logsRoot()
        let settingsRoot = AppPaths.settingsRoot()
        let diagnosticsRoot = AppPaths.diagnosticsRoot()
        let eventsRoot = AppPaths.eventsRoot()
        let runtimeStateURL = AppPaths.runtimeStateURL()
        let arguments = Array(ProcessInfo.processInfo.arguments.dropFirst())
        let rawCaptureEnabled = ProcessInfo.processInfo.environment["TMR_MAC_RAW_CAPTURE_ENABLED"]?.lowercased() == "true"
        let radarCaptureDemoEnabled = ProcessInfo.processInfo.environment["TMR_MAC_RADAR_CAPTURE_DEMO"]?.lowercased() == "true"
            || arguments.contains("--radar-capture-demo")
        let gapToLeaderDemoEnabled = ProcessInfo.processInfo.environment["TMR_MAC_GAP_TO_LEADER_DEMO"]?.lowercased() == "true"
            || arguments.contains("--gap-to-leader-demo")
        let rawPracticeGapDemoEnabled = ProcessInfo.processInfo.environment["TMR_MAC_RAW_PRACTICE_GAP_DEMO"]?.lowercased() == "true"
            || arguments.contains("--raw-practice-gap-demo")
        let trackMapSectorDemoEnabled = ProcessInfo.processInfo.environment["TMR_MAC_TRACK_MAP_SECTOR_DEMO"]?.lowercased() == "true"
            || arguments.contains("--track-map-sector-demo")
        let designV2ComponentDemoTheme = designV2ComponentTheme(arguments: arguments)
        let relativeDesignV2ShellDemoEnabled = designV2RelativeShellEnabled(arguments: arguments)
        let designV2OverlaySuiteDemoEnabled = designV2OverlaySuiteEnabled(arguments: arguments)

        let logger = LocalLogWriter(logsRoot: logsRoot)
        let events = AppEventRecorder(eventsRoot: eventsRoot)
        let performance = PerformanceLogWriter(logsRoot: logsRoot)
        let runtime = RuntimeStateStore(stateURL: runtimeStateURL)
        let settingsStore = AppSettingsStore(settingsRoot: settingsRoot)
        let historyQueryService = SessionHistoryQueryService(userHistoryRoot: historyRoot)
        let overlayDiagnostics = LiveOverlayDiagnosticsRecorder(
            logsRoot: logsRoot,
            events: events,
            logger: logger
        )
        let diagnostics = DiagnosticsBundleWriter(
            appDataRoot: appDataRoot,
            captureRoot: captureRoot,
            historyRoot: historyRoot,
            diagnosticsRoot: diagnosticsRoot,
            logsRoot: logsRoot,
            settingsRoot: settingsRoot,
            eventsRoot: eventsRoot,
            runtimeStateURL: runtimeStateURL,
            makeTelemetrySnapshot: { [state] in state.snapshot() }
        )

        let previousState = runtime.start()
        if let previousState, !previousState.stoppedCleanly {
            logger.warning("Previous TmrOverlayMac run did not stop cleanly. Started at \(previousState.startedAtUtc).")
            events.record("previous_run_unclean", properties: [
                "startedAtUtc": ISO8601DateFormatter().string(from: previousState.startedAtUtc)
            ])
        }

        events.record("app_started")
        RetentionService().clean(captureRoot: captureRoot, diagnosticsRoot: diagnosticsRoot)
        recordBuildFreshness(logger: logger)
        logger.info("TmrOverlayMac \(AppVersionInfo.current.version) started. App data: \(appDataRoot.path). Raw capture enabled: \(rawCaptureEnabled). Captures: \(captureRoot.path).")

        let service = MockTelemetryCaptureService(
            state: state,
            liveTelemetryStore: liveTelemetryStore,
            liveOverlayDiagnosticsRecorder: overlayDiagnostics,
            diagnosticsBundleWriter: diagnostics,
            captureRoot: captureRoot,
            historyRoot: historyRoot,
            rawCaptureEnabled: rawCaptureEnabled,
            events: events,
            logger: logger
        )
        demoStates = makeDemoStates(captureRoot: captureRoot)
        let overlayManager = OverlayManager(
            state: state,
            liveTelemetryStore: liveTelemetryStore,
            historyQueryService: historyQueryService,
            settingsStore: settingsStore,
            events: events,
            logger: logger,
            liveOverlayDiagnosticsRecorder: overlayDiagnostics
        )
        let menu = MenuController(
            state: state,
            captureRoot: captureRoot,
            logsRoot: logsRoot,
            diagnosticsBundleWriter: diagnostics,
            events: events,
            logger: logger,
            openSettings: { [weak overlayManager] in
                overlayManager?.openSettingsOverlay()
            },
            demoActions: makeDemoActions(logger: logger, overlayManager: overlayManager)
        )

        self.captureService = service
        self.menuController = menu
        self.overlayManager = overlayManager
        self.logWriter = logger
        self.eventRecorder = events
        self.performanceLogWriter = performance
        self.liveOverlayDiagnosticsRecorder = overlayDiagnostics
        self.runtimeStateStore = runtime

        overlayManager.showStartupOverlays()
        if relativeDesignV2ShellDemoEnabled {
            overlayManager.showRelativeDesignV2ShellDemo(theme: .outrun)
            logger.info("Enabled Design V2 Relative shell demo.")
        }
        if designV2OverlaySuiteDemoEnabled {
            overlayManager.showDesignV2OverlaySuiteDemo(theme: .outrun)
            logger.info("Enabled Design V2 overlay suite demo.")
        }
        if let designV2ComponentDemoTheme {
            startDesignV2ComponentDemo(
                theme: designV2ComponentDemoTheme,
                logger: logger
            )
        } else if trackMapSectorDemoEnabled {
            startTrackMapSectorDemo(
                captureRoot: captureRoot,
                logger: logger
            )
        } else if rawPracticeGapDemoEnabled {
            startRawPracticeGapDemo(
                captureRoot: captureRoot,
                logger: logger,
                captureURL: rawPracticeGapCaptureURL(arguments: arguments)
            )
        } else if gapToLeaderDemoEnabled {
            startGapToLeaderDemo(
                captureRoot: captureRoot,
                logger: logger
            )
        } else if radarCaptureDemoEnabled {
            startRadarCaptureDemo(
                captureRoot: captureRoot,
                logger: logger
            )
        } else {
            service.start()
            mockTelemetryStarted = true
        }
        startDemoCycleIfRequested(logger: logger)
        startPerformanceLogging(performance)

        refreshTimer = Timer.scheduledTimer(withTimeInterval: 0.25, repeats: true) { [weak self] _ in
            self?.refresh()
        }
    }

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        refreshTimer?.invalidate()
        refreshTimer = nil
        demoTimer?.invalidate()
        demoTimer = nil
        performanceTimer?.invalidate()
        performanceTimer = nil
        performanceLogWriter?.record(demoSnapshotIndex.map { demoStates[$0].makeSnapshot() } ?? state.snapshot())
        overlayManager?.closeAll()
        if mockTelemetryStarted {
            captureService?.stop()
        } else {
            liveOverlayDiagnosticsRecorder?.completeCollection()
        }
        runtimeStateStore?.stopCleanly()
        eventRecorder?.record("app_stopped")
        logWriter?.info("TmrOverlayMac stopped.")
        return .terminateNow
    }

    private func refresh() {
        if radarCaptureDemoActive {
            publishRadarCaptureDemoFrame()
        }

        let snapshot = demoSnapshotIndex.map { demoStates[$0].makeSnapshot() } ?? state.snapshot()
        overlayManager?.updateStatus(with: snapshot)
        menuController?.refresh()
    }

    private func startPerformanceLogging(_ performance: PerformanceLogWriter) {
        performance.record(demoSnapshotIndex.map { demoStates[$0].makeSnapshot() } ?? state.snapshot())
        performanceTimer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self, weak performance] _ in
            guard let self, let performance else {
                return
            }

            performance.record(self.demoSnapshotIndex.map { self.demoStates[$0].makeSnapshot() } ?? self.state.snapshot())
        }
    }

    private func recordBuildFreshness(logger: LocalLogWriter) {
        let buildFreshness = BuildFreshnessChecker.check()
        guard buildFreshness.sourceNewerThanBuild,
              let message = buildFreshness.message else {
            return
        }

        state.recordAppWarning(message)
        logger.warning(message)
    }

    private func makeDemoActions(logger: LocalLogWriter, overlayManager: OverlayManager) -> [(String, () -> Void)] {
        var actions: [(String, () -> Void)] = demoStates.enumerated().map { index, demoState in
            ("Demo: \(demoState.title)", { [weak self] in
                self?.showDemoSnapshot(index: index, logger: logger)
            })
        }
        actions.append(("Demo: Clear", { [weak self] in
            self?.clearDemoSnapshot(logger: logger)
        }))
        actions.append(("Design V2 Components: Current", { [weak overlayManager] in
            overlayManager?.showDesignV2ComponentDemo(theme: .current)
        }))
        actions.append(("Design V2 Components: Outrun", { [weak overlayManager] in
            overlayManager?.showDesignV2ComponentDemo(theme: .outrun)
        }))
        actions.append(("Design V2 Components: Close", { [weak overlayManager] in
            overlayManager?.closeDesignV2ComponentDemo()
        }))
        return actions
    }

    private func showDemoSnapshot(index: Int, logger: LocalLogWriter) {
        guard index >= 0, index < demoStates.count else {
            return
        }

        demoSnapshotIndex = index
        logger.info("Showing demo overlay state \(index): \(demoStates[index].title).")
        refresh()
    }

    private func clearDemoSnapshot(logger: LocalLogWriter) {
        demoSnapshotIndex = nil
        demoTimer?.invalidate()
        demoTimer = nil
        logger.info("Cleared demo overlay state.")
        refresh()
    }

    private func startDemoCycleIfRequested(logger: LocalLogWriter) {
        guard ProcessInfo.processInfo.environment["TMR_MAC_DEMO_STATES"]?.lowercased() == "true",
              !demoStates.isEmpty else {
            return
        }

        showDemoSnapshot(index: 0, logger: logger)
        demoTimer = Timer.scheduledTimer(withTimeInterval: 4.0, repeats: true) { [weak self, weak logger] _ in
            guard let self else {
                return
            }

            let nextIndex = ((self.demoSnapshotIndex ?? -1) + 1) % self.demoStates.count
            self.showDemoSnapshot(index: nextIndex, logger: logger ?? LocalLogWriter(logsRoot: AppPaths.logsRoot()))
        }
    }

    private func startDesignV2ComponentDemo(theme: DesignV2Theme, logger: LocalLogWriter) {
        logger.info("Starting Design V2 component demo for \(theme.id) theme.")
        overlayManager?.showDesignV2ComponentDemo(theme: theme)
    }

    private func startRadarCaptureDemo(captureRoot: URL, logger: LocalLogWriter) {
        let scenarios = RadarCaptureScenario.captureExamples
        guard let firstScenario = scenarios.first else {
            logger.warning("Radar capture demo requested but no capture scenarios are available.")
            return
        }

        let startedAtUtc = Date()
        radarCaptureDemoActive = true
        radarCaptureDemoStartedAtUtc = startedAtUtc
        state.setCaptureRoot(captureRoot)
        state.setRawCaptureEnabled(false)
        state.markConnected()
        state.markCollectionStarted(startedAtUtc: startedAtUtc)
        liveTelemetryStore.markConnected()
        liveTelemetryStore.markCollectionStarted(
            sourceId: firstScenario.sourceCaptureId,
            startedAtUtc: startedAtUtc
        )
        liveOverlayDiagnosticsRecorder?.startCollection(
            sourceId: firstScenario.sourceCaptureId,
            startedAtUtc: startedAtUtc
        )
        eventRecorder?.record("radar_capture_demo_started", properties: [
            "source": firstScenario.sourceCaptureId,
            "scenarioCount": String(scenarios.count)
        ])
        logger.info("Started radar capture demo from \(firstScenario.sourceCaptureId) with \(scenarios.count) radar windows.")
        demoSnapshotIndex = nil
        overlayManager?.showRadarCaptureDemo(
            scenarios: scenarios,
            startedAtUtc: startedAtUtc
        )
        publishRadarCaptureDemoFrame()
        refresh()
    }

    private func startGapToLeaderDemo(captureRoot: URL, logger: LocalLogWriter) {
        let scenarios = GapToLeaderDemoScenario.previewExamples
        guard !scenarios.isEmpty else {
            logger.warning("Gap to leader demo requested but no scenarios are available.")
            return
        }

        let startedAtUtc = Date()
        state.setCaptureRoot(captureRoot)
        state.setRawCaptureEnabled(false)
        state.markConnected()
        state.markCollectionStarted(startedAtUtc: startedAtUtc)
        liveTelemetryStore.markConnected()
        liveTelemetryStore.markCollectionStarted(
            sourceId: "gap-to-leader-demo",
            startedAtUtc: startedAtUtc
        )
        liveOverlayDiagnosticsRecorder?.startCollection(
            sourceId: "gap-to-leader-demo",
            startedAtUtc: startedAtUtc
        )
        eventRecorder?.record("gap_to_leader_demo_started", properties: [
            "scenarioCount": String(scenarios.count)
        ])
        logger.info("Started gap to leader demo with \(scenarios.count) overlay windows.")
        demoSnapshotIndex = nil
        overlayManager?.showGapToLeaderDemo(scenarios: scenarios)
        refresh()
    }

    private func startRawPracticeGapDemo(captureRoot: URL, logger: LocalLogWriter, captureURL: URL) {
        let capture: RawPracticeGapCapture
        do {
            capture = try RawPracticeGapCapture.load(from: captureURL)
        } catch {
            logger.error("Raw practice gap demo requested but capture JSON could not be loaded from \(captureURL.path): \(error).")
            return
        }

        guard !capture.states.isEmpty else {
            logger.warning("Raw practice gap demo requested but \(capture.captureId) does not contain target states.")
            return
        }

        let startedAtUtc = Date()
        state.setCaptureRoot(captureRoot)
        state.setRawCaptureEnabled(false)
        state.markConnected()
        state.markCollectionStarted(startedAtUtc: startedAtUtc)
        liveTelemetryStore.markConnected()
        liveTelemetryStore.markCollectionStarted(
            sourceId: capture.captureId,
            startedAtUtc: startedAtUtc
        )
        liveOverlayDiagnosticsRecorder?.startCollection(
            sourceId: capture.captureId,
            startedAtUtc: startedAtUtc
        )
        eventRecorder?.record("raw_practice_gap_demo_started", properties: [
            "captureId": capture.captureId,
            "stateCount": String(capture.states.count),
            "captureJson": captureURL.path
        ])
        logger.info("Started raw practice gap demo for \(capture.captureId) with \(capture.states.count) overlay windows from \(captureURL.path).")
        demoSnapshotIndex = nil
        overlayManager?.showRawPracticeGapDemo(
            capture: capture,
            startedAtUtc: startedAtUtc
        )
        refresh()
    }

    private func startTrackMapSectorDemo(captureRoot: URL, logger: LocalLogWriter) {
        let startedAtUtc = Date()
        state.setCaptureRoot(captureRoot)
        state.setRawCaptureEnabled(false)
        state.markConnected()
        state.markCollectionStarted(startedAtUtc: startedAtUtc)
        liveTelemetryStore.markConnected()
        liveTelemetryStore.markCollectionStarted(
            sourceId: "track-map-sector-demo",
            startedAtUtc: startedAtUtc
        )
        liveOverlayDiagnosticsRecorder?.startCollection(
            sourceId: "track-map-sector-demo",
            startedAtUtc: startedAtUtc
        )
        eventRecorder?.record("track_map_sector_demo_started")
        logger.info("Started track map sector demo.")
        demoSnapshotIndex = nil
        overlayManager?.showTrackMapSectorDemo(startedAtUtc: startedAtUtc)
        refresh()
    }

    private func rawPracticeGapCaptureURL(arguments: [String]) -> URL {
        let explicitPath = argumentValue(named: "--gap-capture-json", arguments: arguments)
            ?? ProcessInfo.processInfo.environment["TMR_MAC_GAP_CAPTURE_JSON"]
        let path = explicitPath?.isEmpty == false
            ? explicitPath!
            : "/tmp/tmr-raw-practice-gap-frames.json"
        return URL(fileURLWithPath: path)
    }

    private func designV2ComponentTheme(arguments: [String]) -> DesignV2Theme? {
        let envValue = ProcessInfo.processInfo.environment["TMR_MAC_DESIGN_V2_COMPONENTS_DEMO"]
        let demoValue = argumentValue(named: "--design-v2-components-demo", arguments: arguments)
        let themeValue = argumentValue(named: "--design-v2-theme", arguments: arguments)
            ?? ProcessInfo.processInfo.environment["TMR_MAC_DESIGN_V2_THEME"]
        let flagEnabled = arguments.contains("--design-v2-components-demo")
            || demoValue != nil
            || envValue != nil
            || themeValue != nil

        guard flagEnabled else {
            return nil
        }

        let requested = (demoValue ?? themeValue ?? envValue ?? "outrun")
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
        if ["false", "0", "no", "off"].contains(requested) {
            return nil
        }

        switch requested {
        case "current", "classic", "default":
            return .current
        default:
            return .outrun
        }
    }

    private func designV2RelativeShellEnabled(arguments: [String]) -> Bool {
        if arguments.contains("--design-v2-relative-shell") {
            return true
        }

        return ProcessInfo.processInfo.environment["TMR_MAC_DESIGN_V2_RELATIVE_SHELL"]?.lowercased() == "true"
    }

    private func designV2OverlaySuiteEnabled(arguments: [String]) -> Bool {
        if arguments.contains("--design-v2-overlay-suite") {
            return true
        }

        return ProcessInfo.processInfo.environment["TMR_MAC_DESIGN_V2_OVERLAY_SUITE"]?.lowercased() == "true"
    }

    private func argumentValue(named name: String, arguments: [String]) -> String? {
        for (index, argument) in arguments.enumerated() {
            if argument == name, index + 1 < arguments.count {
                return arguments[index + 1]
            }

            let prefix = "\(name)="
            if argument.hasPrefix(prefix) {
                return String(argument.dropFirst(prefix.count))
            }
        }

        return nil
    }

    private func publishRadarCaptureDemoFrame() {
        guard let firstScenario = RadarCaptureScenario.captureExamples.first else {
            return
        }

        let now = Date()
        let playbackElapsedSeconds = radarCaptureDemoStartedAtUtc.map { now.timeIntervalSince($0) } ?? 0
        liveTelemetryStore.recordRadarScenario(
            firstScenario,
            capturedAtUtc: now,
            playbackElapsedSeconds: playbackElapsedSeconds
        )
        liveOverlayDiagnosticsRecorder?.record(liveTelemetryStore.snapshot())
        state.recordFrame(capturedAtUtc: now)
    }

    private func makeDemoStates(captureRoot: URL) -> [DemoOverlayState] {
        func capture(_ id: String) -> URL {
            captureRoot.appendingPathComponent("capture-demo-\(id)", isDirectory: true)
        }

        return [
            DemoOverlayState(title: "Waiting for iRacing") {
                TelemetryCaptureStatusSnapshot.idleWithCaptureRoot(captureRoot)
            },
            DemoOverlayState(title: "Connected, No Capture") {
                TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: false,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: nil,
                    lastCaptureDirectory: nil,
                    frameCount: 0,
                    writtenFrameCount: 0,
                    droppedFrameCount: 0,
                    telemetryFileBytes: nil,
                    captureStartedAtUtc: nil,
                    lastFrameCapturedAtUtc: nil,
                    lastDiskWriteAtUtc: nil,
                    appWarning: nil,
                    lastWarning: nil,
                    lastError: nil,
                    lastIssueAtUtc: nil
                )
            },
            DemoOverlayState(title: "Healthy Live Analysis") {
                let now = Date()
                return TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: true,
                    rawCaptureEnabled: false,
                    rawCaptureActive: false,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: nil,
                    lastCaptureDirectory: nil,
                    frameCount: 9_640,
                    writtenFrameCount: 0,
                    droppedFrameCount: 0,
                    telemetryFileBytes: nil,
                    captureStartedAtUtc: now.addingTimeInterval(-160),
                    lastFrameCapturedAtUtc: now.addingTimeInterval(-0.1),
                    lastDiskWriteAtUtc: nil,
                    appWarning: nil,
                    lastWarning: nil,
                    lastError: nil,
                    lastIssueAtUtc: nil
                )
            },
            DemoOverlayState(title: "Healthy Capture") {
                let now = Date()
                let directory = capture("healthy")
                return TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: true,
                    rawCaptureEnabled: true,
                    rawCaptureActive: true,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: directory,
                    lastCaptureDirectory: directory,
                    frameCount: 18_240,
                    writtenFrameCount: 18_238,
                    droppedFrameCount: 0,
                    telemetryFileBytes: 143_220_736,
                    captureStartedAtUtc: now.addingTimeInterval(-304),
                    lastFrameCapturedAtUtc: now.addingTimeInterval(-0.1),
                    lastDiskWriteAtUtc: now.addingTimeInterval(-0.2),
                    appWarning: nil,
                    lastWarning: nil,
                    lastError: nil,
                    lastIssueAtUtc: nil
                )
            },
            DemoOverlayState(title: "Stale Build") {
                let now = Date()
                let directory = capture("stale-build")
                return TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: true,
                    rawCaptureEnabled: true,
                    rawCaptureActive: true,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: directory,
                    lastCaptureDirectory: directory,
                    frameCount: 7_812,
                    writtenFrameCount: 7_811,
                    droppedFrameCount: 0,
                    telemetryFileBytes: 61_482_496,
                    captureStartedAtUtc: now.addingTimeInterval(-130),
                    lastFrameCapturedAtUtc: now.addingTimeInterval(-0.1),
                    lastDiskWriteAtUtc: now.addingTimeInterval(-0.2),
                    appWarning: "Local source is newer than this build; rebuild recommended. Newest: src/TmrOverlay.App/Telemetry/TelemetryCaptureState.cs",
                    lastWarning: nil,
                    lastError: nil,
                    lastIssueAtUtc: nil
                )
            },
            DemoOverlayState(title: "Dropped Frames") {
                let now = Date()
                let directory = capture("dropped-frames")
                return TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: true,
                    rawCaptureEnabled: true,
                    rawCaptureActive: true,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: directory,
                    lastCaptureDirectory: directory,
                    frameCount: 22_480,
                    writtenFrameCount: 22_451,
                    droppedFrameCount: 29,
                    telemetryFileBytes: 176_914_432,
                    captureStartedAtUtc: now.addingTimeInterval(-375),
                    lastFrameCapturedAtUtc: now.addingTimeInterval(-0.1),
                    lastDiskWriteAtUtc: now.addingTimeInterval(-0.3),
                    appWarning: nil,
                    lastWarning: "Dropped telemetry frame because the capture queue is full.",
                    lastError: nil,
                    lastIssueAtUtc: now.addingTimeInterval(-2)
                )
            },
            DemoOverlayState(title: "Frames Not Written") {
                let now = Date()
                let directory = capture("not-written")
                return TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: true,
                    rawCaptureEnabled: true,
                    rawCaptureActive: true,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: directory,
                    lastCaptureDirectory: directory,
                    frameCount: 844,
                    writtenFrameCount: 0,
                    droppedFrameCount: 0,
                    telemetryFileBytes: 32,
                    captureStartedAtUtc: now.addingTimeInterval(-14),
                    lastFrameCapturedAtUtc: now.addingTimeInterval(-0.1),
                    lastDiskWriteAtUtc: nil,
                    appWarning: nil,
                    lastWarning: nil,
                    lastError: nil,
                    lastIssueAtUtc: nil
                )
            },
            DemoOverlayState(title: "Disk Writes Stalled") {
                let now = Date()
                let directory = capture("disk-stalled")
                return TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: true,
                    rawCaptureEnabled: true,
                    rawCaptureActive: true,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: directory,
                    lastCaptureDirectory: directory,
                    frameCount: 12_006,
                    writtenFrameCount: 11_992,
                    droppedFrameCount: 0,
                    telemetryFileBytes: 94_208_000,
                    captureStartedAtUtc: now.addingTimeInterval(-203),
                    lastFrameCapturedAtUtc: now.addingTimeInterval(-0.1),
                    lastDiskWriteAtUtc: now.addingTimeInterval(-12),
                    appWarning: nil,
                    lastWarning: nil,
                    lastError: nil,
                    lastIssueAtUtc: nil
                )
            },
            DemoOverlayState(title: "Capture Error") {
                let now = Date()
                let directory = capture("error")
                return TelemetryCaptureStatusSnapshot(
                    isConnected: true,
                    isCapturing: true,
                    rawCaptureEnabled: true,
                    rawCaptureActive: true,
                    captureRoot: captureRoot,
                    currentCaptureDirectory: directory,
                    lastCaptureDirectory: directory,
                    frameCount: 1_122,
                    writtenFrameCount: 1_116,
                    droppedFrameCount: 3,
                    telemetryFileBytes: 8_790_016,
                    captureStartedAtUtc: now.addingTimeInterval(-22),
                    lastFrameCapturedAtUtc: now.addingTimeInterval(-0.2),
                    lastDiskWriteAtUtc: now.addingTimeInterval(-0.4),
                    appWarning: nil,
                    lastWarning: nil,
                    lastError: "Capture writer failed: Access to telemetry.bin was denied.",
                    lastIssueAtUtc: now.addingTimeInterval(-1)
                )
            }
        ]
    }

}
