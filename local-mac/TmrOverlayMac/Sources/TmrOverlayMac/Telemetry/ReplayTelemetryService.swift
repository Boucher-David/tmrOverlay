import Foundation

final class ReplayTelemetryService {
    private let state: TelemetryCaptureState
    private let captureDirectory: URL
    private let events: AppEventRecorder

    init(state: TelemetryCaptureState, captureDirectory: URL, events: AppEventRecorder) {
        self.state = state
        self.captureDirectory = captureDirectory
        self.events = events
    }

    func start() {
        events.record("replay_started", properties: ["captureDirectory": captureDirectory.path])
        state.setRawCaptureEnabled(true)
        state.markConnected()
        state.markCaptureStarted(captureDirectory, startedAtUtc: Date())
    }

    func stop() {
        state.markCaptureStopped()
        state.markDisconnected()
        events.record("replay_stopped")
    }
}
