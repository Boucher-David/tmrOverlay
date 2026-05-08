import Foundation

struct TelemetryCaptureStatusSnapshot {
    let isConnected: Bool
    let isCapturing: Bool
    let rawCaptureEnabled: Bool
    let rawCaptureActive: Bool
    let captureRoot: URL?
    let currentCaptureDirectory: URL?
    let lastCaptureDirectory: URL?
    let frameCount: Int
    let writtenFrameCount: Int
    let droppedFrameCount: Int
    let telemetryFileBytes: Int64?
    let captureStartedAtUtc: Date?
    let lastFrameCapturedAtUtc: Date?
    let lastDiskWriteAtUtc: Date?
    let appWarning: String?
    let lastWarning: String?
    let lastError: String?
    let lastIssueAtUtc: Date?

    init(
        isConnected: Bool,
        isCapturing: Bool,
        rawCaptureEnabled: Bool = false,
        rawCaptureActive: Bool = false,
        captureRoot: URL?,
        currentCaptureDirectory: URL?,
        lastCaptureDirectory: URL?,
        frameCount: Int,
        writtenFrameCount: Int,
        droppedFrameCount: Int,
        telemetryFileBytes: Int64?,
        captureStartedAtUtc: Date?,
        lastFrameCapturedAtUtc: Date?,
        lastDiskWriteAtUtc: Date?,
        appWarning: String?,
        lastWarning: String?,
        lastError: String?,
        lastIssueAtUtc: Date?
    ) {
        self.isConnected = isConnected
        self.isCapturing = isCapturing
        self.rawCaptureEnabled = rawCaptureEnabled
        self.rawCaptureActive = rawCaptureActive
        self.captureRoot = captureRoot
        self.currentCaptureDirectory = currentCaptureDirectory
        self.lastCaptureDirectory = lastCaptureDirectory
        self.frameCount = frameCount
        self.writtenFrameCount = writtenFrameCount
        self.droppedFrameCount = droppedFrameCount
        self.telemetryFileBytes = telemetryFileBytes
        self.captureStartedAtUtc = captureStartedAtUtc
        self.lastFrameCapturedAtUtc = lastFrameCapturedAtUtc
        self.lastDiskWriteAtUtc = lastDiskWriteAtUtc
        self.appWarning = appWarning
        self.lastWarning = lastWarning
        self.lastError = lastError
        self.lastIssueAtUtc = lastIssueAtUtc
    }

    static let idle = TelemetryCaptureStatusSnapshot(
        isConnected: false,
        isCapturing: false,
        captureRoot: nil,
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

    static func idleWithCaptureRoot(_ captureRoot: URL) -> TelemetryCaptureStatusSnapshot {
        TelemetryCaptureStatusSnapshot(
            isConnected: false,
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
    }
}

final class TelemetryCaptureState {
    private let lock = NSLock()
    private var isConnected = false
    private var isCollecting = false
    private var rawCaptureEnabled = false
    private var captureRoot: URL?
    private var currentCaptureDirectory: URL?
    private var lastCaptureDirectory: URL?
    private var frameCount = 0
    private var writtenFrameCount = 0
    private var droppedFrameCount = 0
    private var telemetryFileBytes: Int64?
    private var captureStartedAtUtc: Date?
    private var lastFrameCapturedAtUtc: Date?
    private var lastDiskWriteAtUtc: Date?
    private var appWarning: String?
    private var lastWarning: String?
    private var lastError: String?
    private var lastIssueAtUtc: Date?

    func setCaptureRoot(_ root: URL) {
        lock.withLock {
            captureRoot = root
        }
    }

    @discardableResult
    func setRawCaptureEnabled(_ enabled: Bool) -> Bool {
        lock.withLock {
            if !enabled, currentCaptureDirectory != nil {
                lastWarning = "Raw capture is already active; it will stop when the current collection ends."
                lastIssueAtUtc = Date()
                return false
            }

            rawCaptureEnabled = enabled
            return true
        }
    }

    func isRawCaptureEnabled() -> Bool {
        lock.withLock {
            rawCaptureEnabled
        }
    }

    func markConnected() {
        lock.withLock {
            isConnected = true
            lastWarning = nil
        }
    }

    func markDisconnected() {
        lock.withLock {
            isConnected = false
            isCollecting = false
            currentCaptureDirectory = nil
            captureStartedAtUtc = nil
            frameCount = 0
            writtenFrameCount = 0
            droppedFrameCount = 0
            telemetryFileBytes = nil
            lastFrameCapturedAtUtc = nil
            lastDiskWriteAtUtc = nil
        }
    }

    func markCaptureStarted(_ directory: URL, startedAtUtc: Date) {
        lock.withLock {
            isCollecting = true
            currentCaptureDirectory = directory
            lastCaptureDirectory = directory
            captureStartedAtUtc = startedAtUtc
            frameCount = 0
            writtenFrameCount = 0
            droppedFrameCount = 0
            telemetryFileBytes = nil
            lastFrameCapturedAtUtc = nil
            lastDiskWriteAtUtc = nil
            lastWarning = nil
            lastError = nil
            lastIssueAtUtc = nil
        }
    }

    func markCollectionStarted(startedAtUtc: Date) {
        lock.withLock {
            isCollecting = true
            captureStartedAtUtc = startedAtUtc
            frameCount = 0
            writtenFrameCount = 0
            droppedFrameCount = 0
            telemetryFileBytes = nil
            lastFrameCapturedAtUtc = nil
            lastDiskWriteAtUtc = nil
            lastWarning = nil
            lastError = nil
            lastIssueAtUtc = nil
        }
    }

    func markCaptureStopped() {
        lock.withLock {
            isCollecting = false
            currentCaptureDirectory = nil
            captureStartedAtUtc = nil
            frameCount = 0
            writtenFrameCount = 0
            droppedFrameCount = 0
            lastFrameCapturedAtUtc = nil
            lastDiskWriteAtUtc = nil
        }
    }

    func recordFrame(capturedAtUtc: Date) {
        lock.withLock {
            frameCount += 1
            lastFrameCapturedAtUtc = capturedAtUtc
        }
    }

    func recordCaptureWrite(framesWritten: Int, telemetryFileBytes: Int64?, writtenAtUtc: Date) {
        lock.withLock {
            writtenFrameCount = framesWritten
            self.telemetryFileBytes = telemetryFileBytes ?? self.telemetryFileBytes
            lastDiskWriteAtUtc = writtenAtUtc
        }
    }

    func recordDroppedFrame() {
        lock.withLock {
            droppedFrameCount += 1
            lastWarning = "Dropped telemetry frame because the capture queue is full."
            lastIssueAtUtc = Date()
        }
    }

    func recordWarning(_ message: String) {
        lock.withLock {
            lastWarning = message
            lastIssueAtUtc = Date()
        }
    }

    func recordAppWarning(_ message: String) {
        lock.withLock {
            appWarning = message
        }
    }

    func recordError(_ message: String) {
        lock.withLock {
            lastError = message
            lastIssueAtUtc = Date()
        }
    }

    func snapshot() -> TelemetryCaptureStatusSnapshot {
        lock.withLock {
            TelemetryCaptureStatusSnapshot(
                isConnected: isConnected,
                isCapturing: isCollecting,
                rawCaptureEnabled: rawCaptureEnabled,
                rawCaptureActive: currentCaptureDirectory != nil,
                captureRoot: captureRoot,
                currentCaptureDirectory: currentCaptureDirectory,
                lastCaptureDirectory: lastCaptureDirectory,
                frameCount: frameCount,
                writtenFrameCount: writtenFrameCount,
                droppedFrameCount: droppedFrameCount,
                telemetryFileBytes: telemetryFileBytes,
                captureStartedAtUtc: captureStartedAtUtc,
                lastFrameCapturedAtUtc: lastFrameCapturedAtUtc,
                lastDiskWriteAtUtc: lastDiskWriteAtUtc,
                appWarning: appWarning,
                lastWarning: lastWarning,
                lastError: lastError,
                lastIssueAtUtc: lastIssueAtUtc
            )
        }
    }
}
