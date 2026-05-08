import Foundation

final class MockTelemetryCaptureService {
    let captureRoot: URL
    let historyRoot: URL
    let rawCaptureEnabled: Bool

    private let state: TelemetryCaptureState
    private let liveTelemetryStore: LiveTelemetryStore
    private let liveOverlayDiagnosticsRecorder: LiveOverlayDiagnosticsRecorder
    private let diagnosticsBundleWriter: DiagnosticsBundleWriter
    private let events: AppEventRecorder
    private let logger: LocalLogWriter
    private let queue = DispatchQueue(label: "com.tmroverlay.mac.mock-capture")
    private var timer: DispatchSourceTimer?
    private var captureSession: MockCaptureSession?
    private var liveSession: MockLiveAnalysisSession?
    private var stopped = false

    init(
        state: TelemetryCaptureState,
        liveTelemetryStore: LiveTelemetryStore,
        liveOverlayDiagnosticsRecorder: LiveOverlayDiagnosticsRecorder,
        diagnosticsBundleWriter: DiagnosticsBundleWriter,
        captureRoot: URL,
        historyRoot: URL,
        rawCaptureEnabled: Bool,
        events: AppEventRecorder,
        logger: LocalLogWriter
    ) {
        self.state = state
        self.liveTelemetryStore = liveTelemetryStore
        self.liveOverlayDiagnosticsRecorder = liveOverlayDiagnosticsRecorder
        self.diagnosticsBundleWriter = diagnosticsBundleWriter
        self.captureRoot = captureRoot
        self.historyRoot = historyRoot
        self.rawCaptureEnabled = rawCaptureEnabled
        self.events = events
        self.logger = logger
    }

    func start() {
        state.setCaptureRoot(captureRoot)
        state.setRawCaptureEnabled(rawCaptureEnabled)
        if rawCaptureEnabled {
            try? FileManager.default.createDirectory(at: captureRoot, withIntermediateDirectories: true)
        }
        try? FileManager.default.createDirectory(at: historyRoot, withIntermediateDirectories: true)

        DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { [weak self] in
            guard let self, !self.stopped else {
                return
            }

            self.state.markConnected()
            self.liveTelemetryStore.markConnected()
            self.events.record("iracing_connected", properties: ["source": "mac_mock"])
            self.logger.info("Mock telemetry connected.")
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) { [weak self] in
            guard let self, !self.stopped else {
                return
            }

            self.queue.async {
                self.startCollection()
            }
        }
    }

    func stop() {
        queue.sync {
            stopped = true
            timer?.cancel()
            timer = nil
            finalizeActiveCollection()
            captureSession = nil
            liveSession = nil
            state.markCaptureStopped()
            state.markDisconnected()
            liveTelemetryStore.markDisconnected()
            events.record("iracing_disconnected", properties: ["source": "mac_mock"])
            logger.info("Mock telemetry disconnected.")
        }
    }

    private func startCollection() {
        guard !stopped, captureSession == nil, liveSession == nil else {
            return
        }

        if !rawCaptureEnabled {
            let session = MockLiveAnalysisSession(historyRoot: historyRoot)
            liveSession = session
            state.markCollectionStarted(startedAtUtc: session.startedAtUtc)
            liveTelemetryStore.markCollectionStarted(sourceId: session.sourceId, startedAtUtc: session.startedAtUtc)
            liveOverlayDiagnosticsRecorder.startCollection(sourceId: session.sourceId, startedAtUtc: session.startedAtUtc)
            events.record("telemetry_collection_started", properties: [
                "sourceId": session.sourceId,
                "rawCaptureEnabled": "false",
                "source": "mac_mock"
            ])
            logger.info("Started mock live telemetry analysis \(session.sourceId).")
            startTimer()
            return
        }

        do {
            let capture = try MockCaptureSession(rootDirectory: captureRoot, historyRoot: historyRoot)
            captureSession = capture
            state.markCaptureStarted(capture.directoryURL, startedAtUtc: capture.startedAtUtc)
            liveTelemetryStore.markCollectionStarted(sourceId: capture.captureId, startedAtUtc: capture.startedAtUtc)
            liveOverlayDiagnosticsRecorder.startCollection(
                sourceId: capture.captureId,
                startedAtUtc: capture.startedAtUtc,
                captureDirectory: capture.directoryURL
            )
            events.record("capture_started", properties: [
                "captureId": capture.captureId,
                "captureDirectory": capture.directoryURL.path,
                "source": "mac_mock"
            ])
            logger.info("Started mock capture \(capture.directoryURL.path).")
            startTimer()
        } catch {
            state.markDisconnected()
            state.recordError("Failed to start mock capture: \(error)")
            events.record("capture_start_failed", properties: [
                "error": "\(type(of: error))",
                "source": "mac_mock"
            ])
            logger.error("Failed to start mock capture: \(error)")
        }
    }

    private func startTimer() {
        let source = DispatchSource.makeTimerSource(queue: queue)
        source.schedule(deadline: .now(), repeating: .milliseconds(16), leeway: .milliseconds(4))
        source.setEventHandler { [weak self] in
            self?.writeNextFrame()
        }
        timer = source
        source.resume()
    }

    private func writeNextFrame() {
        startRuntimeRawCaptureIfRequested()

        if let liveSession {
            let frame = liveSession.recordNextFrame()
            writeRawCaptureFrameIfActive(capturedAtUtc: frame.capturedAtUtc)
            liveTelemetryStore.recordFrame(frame)
            liveOverlayDiagnosticsRecorder.record(liveTelemetryStore.snapshot())
            state.recordFrame(capturedAtUtc: frame.capturedAtUtc)
            return
        }

        guard let captureSession else {
            return
        }

        do {
            let capturedAtUtc = try captureSession.writeNextFrame()
            liveTelemetryStore.recordFrame(Self.makeLiveFrame(capturedAtUtc: capturedAtUtc, startedAtUtc: captureSession.startedAtUtc))
            liveOverlayDiagnosticsRecorder.record(liveTelemetryStore.snapshot())
            state.recordFrame(capturedAtUtc: capturedAtUtc)
            state.recordCaptureWrite(
                framesWritten: captureSession.frameCount,
                telemetryFileBytes: captureSession.telemetryFileBytes(),
                writtenAtUtc: Date()
            )
        } catch {
            state.recordError("Failed to write mock telemetry frame: \(error)")
            logger.error("Failed to write mock telemetry frame: \(error)")
        }
    }

    private func startRuntimeRawCaptureIfRequested() {
        guard state.isRawCaptureEnabled(), captureSession == nil else {
            return
        }

        do {
            let capture = try MockCaptureSession(rootDirectory: captureRoot, historyRoot: historyRoot)
            captureSession = capture
            state.markCaptureStarted(capture.directoryURL, startedAtUtc: capture.startedAtUtc)
            events.record("capture_started", properties: [
                "captureId": capture.captureId,
                "captureDirectory": capture.directoryURL.path,
                "source": "mac_mock_runtime"
            ])
            logger.info("Started runtime mock capture \(capture.directoryURL.path).")
        } catch {
            state.recordError("Failed to start runtime mock capture: \(error)")
            _ = state.setRawCaptureEnabled(false)
            events.record("capture_start_failed", properties: [
                "error": "\(type(of: error))",
                "source": "mac_mock_runtime"
            ])
            logger.error("Failed to start runtime mock capture: \(error)")
        }
    }

    private func writeRawCaptureFrameIfActive(capturedAtUtc: Date) {
        guard let captureSession else {
            return
        }

        do {
            _ = try captureSession.writeNextFrame()
            state.recordCaptureWrite(
                framesWritten: captureSession.frameCount,
                telemetryFileBytes: captureSession.telemetryFileBytes(),
                writtenAtUtc: capturedAtUtc
            )
        } catch {
            state.recordError("Failed to write runtime mock telemetry frame: \(error)")
            logger.error("Failed to write runtime mock telemetry frame: \(error)")
        }
    }

    private func finalizeActiveCollection() {
        var summaries: [HistoricalSessionSummary] = []
        var finalizedCollection = false
        if let liveSession {
            finalizedCollection = true
            if let summary = liveSession.finish() {
                summaries.append(summary)
            }
            liveOverlayDiagnosticsRecorder.completeCollection()
            logger.info("Finalized mock live telemetry analysis \(liveSession.sourceId).")
        }

        if let captureSession {
            finalizedCollection = true
            if let summary = captureSession.finish() {
                summaries.append(summary)
            }
            events.record("capture_finalized", properties: [
                "captureId": captureSession.captureId,
                "captureDirectory": captureSession.directoryURL.path,
                "frameCount": String(captureSession.frameCount),
                "source": "mac_mock"
            ])
            liveOverlayDiagnosticsRecorder.completeCollection()
            logger.info("Finalized mock capture \(captureSession.directoryURL.path).")
        }

        for summary in summaries {
            events.record("history_summary_saved", properties: [
                "sourceId": summary.sourceCaptureId,
                "carKey": summary.combo.carKey,
                "trackKey": summary.combo.trackKey,
                "sessionKey": summary.combo.sessionKey,
                "confidence": summary.quality.confidence,
                "source": "mac_mock"
            ])
        }

        if finalizedCollection {
            createEndOfSessionDiagnosticsBundle(sourceId: summaries.first?.sourceCaptureId)
        }
    }

    private func createEndOfSessionDiagnosticsBundle(sourceId: String?) {
        do {
            let bundleURL = try diagnosticsBundleWriter.createBundle(source: "session_finalization")
            events.record("diagnostics_bundle_created", properties: [
                "bundlePath": bundleURL.path,
                "source": "session_finalization",
                "sourceId": sourceId ?? ""
            ])
            logger.info("Created mock end-of-session diagnostics bundle \(bundleURL.path).")
        } catch {
            events.record("diagnostics_bundle_failed", properties: [
                "error": "\(type(of: error))",
                "source": "session_finalization",
                "sourceId": sourceId ?? ""
            ])
            logger.warning("Failed to create mock end-of-session diagnostics bundle: \(error).")
        }
    }

    private static func makeLiveFrame(capturedAtUtc: Date, startedAtUtc: Date) -> MockLiveTelemetryFrame {
        let sessionTime = FourHourRacePreview.mockStartRaceSeconds
            + max(0, capturedAtUtc.timeIntervalSince(startedAtUtc)) * FourHourRacePreview.mockSpeedMultiplier
        let fuelUsePerHourLiters = FourHourRacePreview.fuelUsePerHourLiters
        let fuelLevel = FourHourRacePreview.fuelLevelLiters(sessionTime: sessionTime)
        return MockLiveTelemetryFrame.mock(
            capturedAtUtc: capturedAtUtc,
            sessionTime: sessionTime,
            fuelLevelLiters: fuelLevel,
            fuelUsePerHourLiters: fuelUsePerHourLiters
        )
    }
}

private final class MockLiveAnalysisSession {
    let sourceId: String
    let startedAtUtc: Date

    private let historyRoot: URL
    private let sessionStartMonotonic = Date()
    private var frameIndex = 0
    private var previousSample: MockHistorySample?
    private var validGreenTimeSeconds = 0.0
    private var validDistanceLaps = 0.0
    private var fuelUsedLiters = 0.0
    private var startingFuelLiters: Double?
    private var endingFuelLiters: Double?
    private var minimumFuelLiters: Double?
    private var maximumFuelLiters: Double?
    private var finished = false

    init(historyRoot: URL) {
        self.historyRoot = historyRoot
        startedAtUtc = Date()
        sourceId = "session-\(Self.sourceIdFormatter.string(from: startedAtUtc))"
    }

    func recordNextFrame() -> MockLiveTelemetryFrame {
        frameIndex += 1
        let capturedAtUtc = Date()
        let sessionTime = FourHourRacePreview.mockStartRaceSeconds
            + capturedAtUtc.timeIntervalSince(sessionStartMonotonic) * FourHourRacePreview.mockSpeedMultiplier
        let fuelUsePerHourLiters = FourHourRacePreview.fuelUsePerHourLiters
        let lapTimeSeconds = FourHourRacePreview.medianLapSeconds
        let fuelLevel = FourHourRacePreview.fuelLevelLiters(sessionTime: sessionTime)
        let lapDistance = sessionTime.truncatingRemainder(dividingBy: lapTimeSeconds) / lapTimeSeconds
        recordHistorySample(MockHistorySample(
            sessionTime: sessionTime,
            fuelLevelLiters: fuelLevel,
            speedMetersPerSecond: 32.0 + sin(sessionTime * 1.2) * 18.0,
            lapDistancePct: lapDistance
        ))
        return MockLiveTelemetryFrame.mock(
            capturedAtUtc: capturedAtUtc,
            sessionTime: sessionTime,
            fuelLevelLiters: fuelLevel,
            fuelUsePerHourLiters: fuelUsePerHourLiters
        )
    }

    func finish() -> HistoricalSessionSummary? {
        guard !finished else {
            return nil
        }

        finished = true
        let finishedAtUtc = Date()
        let fuelPerHour = validGreenTimeSeconds >= 30 && fuelUsedLiters > 0
            ? fuelUsedLiters / validGreenTimeSeconds * 3600
            : nil
        let fuelPerLap = validDistanceLaps >= 0.25 && fuelUsedLiters > 0
            ? fuelUsedLiters / validDistanceLaps
            : nil
        let summary = HistoricalSessionSummary.mock(
            sourceCaptureId: sourceId,
            startedAtUtc: startedAtUtc,
            finishedAtUtc: finishedAtUtc,
            frameCount: frameIndex,
            captureDurationSeconds: max(0, finishedAtUtc.timeIntervalSince(startedAtUtc)),
            validGreenTimeSeconds: validGreenTimeSeconds,
            validDistanceLaps: validDistanceLaps,
            fuelUsedLiters: fuelUsedLiters,
            fuelPerHourLiters: fuelPerHour,
            fuelPerLapLiters: fuelPerLap,
            startingFuelLiters: startingFuelLiters,
            endingFuelLiters: endingFuelLiters,
            minimumFuelLiters: minimumFuelLiters,
            maximumFuelLiters: maximumFuelLiters,
            contributesToBaseline: fuelPerLap != nil
        )

        do {
            try SessionHistoryWriter(historyRoot: historyRoot).save(summary: summary)
            return summary
        } catch {
            NSLog("Failed to finalize mock live telemetry analysis: \(error)")
            return nil
        }
    }

    private func recordHistorySample(_ sample: MockHistorySample) {
        startingFuelLiters = startingFuelLiters ?? sample.fuelLevelLiters
        endingFuelLiters = sample.fuelLevelLiters
        minimumFuelLiters = minimumFuelLiters.map { min($0, sample.fuelLevelLiters) } ?? sample.fuelLevelLiters
        maximumFuelLiters = maximumFuelLiters.map { max($0, sample.fuelLevelLiters) } ?? sample.fuelLevelLiters

        defer {
            previousSample = sample
        }

        guard let previousSample else {
            return
        }

        let deltaSeconds = sample.sessionTime - previousSample.sessionTime
        guard deltaSeconds > 0, deltaSeconds < 1 else {
            return
        }

        validGreenTimeSeconds += deltaSeconds
        validDistanceLaps += Self.lapDistanceDelta(from: previousSample.lapDistancePct, to: sample.lapDistancePct)

        let fuelDelta = previousSample.fuelLevelLiters - sample.fuelLevelLiters
        if fuelDelta > 0, fuelDelta < 0.05 {
            fuelUsedLiters += fuelDelta
        }
    }

    private static func lapDistanceDelta(from previous: Double, to current: Double) -> Double {
        current >= previous ? current - previous : (1.0 - previous) + current
    }

    private static let sourceIdFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyyMMdd-HHmmss-SSS"
        return formatter
    }()
}
