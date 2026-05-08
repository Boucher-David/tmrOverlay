import AppKit

final class StatusOverlayView: NSView {
    private enum Layout {
        static let horizontalPadding: CGFloat = 16
    }

    private let indicatorView = NSView()
    private let titleLabel = NSTextField(labelWithString: "TmrOverlay")
    private let statusLabel = NSTextField(labelWithString: "Waiting for iRacing")
    private let detailLabel = NSTextField(labelWithString: "collector idle")
    private let captureLabel = NSTextField(labelWithString: "capture: not started")
    private let healthLabel = NSTextField(labelWithString: "health: waiting for telemetry")
    var showCaptureDetails = true {
        didSet { captureLabel.isHidden = !showCaptureDetails }
    }
    var showHealthDetails = true {
        didSet { healthLabel.isHidden = !showHealthDetails }
    }
    var fontFamily = "SF Pro" {
        didSet { applyFonts() }
    }

    init() {
        super.init(frame: NSRect(origin: .zero, size: StatusOverlayDefinition.defaultSize))

        wantsLayer = true
        layer?.cornerRadius = 0
        layer?.borderWidth = 1
        layer?.borderColor = OverlayTheme.Colors.windowBorder.cgColor

        indicatorView.frame = NSRect(x: 16, y: 122, width: 12, height: 12)
        indicatorView.wantsLayer = true
        indicatorView.layer?.cornerRadius = 6
        addSubview(indicatorView)

        titleLabel.frame = NSRect(x: 36, y: 116, width: 300, height: 22)
        titleLabel.font = OverlayTheme.font(family: fontFamily, size: 15, weight: .semibold)
        titleLabel.textColor = OverlayTheme.Colors.textPrimary
        titleLabel.backgroundColor = .clear
        addSubview(titleLabel)

        statusLabel.frame = NSRect(x: 16, y: 92, width: 390, height: 22)
        statusLabel.font = OverlayTheme.font(family: fontFamily, size: 13)
        statusLabel.textColor = OverlayTheme.Colors.textSecondary
        statusLabel.backgroundColor = .clear
        addSubview(statusLabel)

        detailLabel.frame = NSRect(x: 16, y: 70, width: 408, height: 18)
        detailLabel.font = NSFont.monospacedSystemFont(ofSize: 12, weight: .regular)
        detailLabel.textColor = OverlayTheme.Colors.textSubtle
        detailLabel.backgroundColor = .clear
        addSubview(detailLabel)

        captureLabel.frame = NSRect(x: 16, y: 48, width: 408, height: 18)
        captureLabel.font = NSFont.monospacedSystemFont(ofSize: 11, weight: .regular)
        captureLabel.textColor = OverlayTheme.Colors.textMuted
        captureLabel.backgroundColor = .clear
        addSubview(captureLabel)

        healthLabel.frame = NSRect(x: 16, y: 14, width: 408, height: 32)
        healthLabel.font = NSFont.monospacedSystemFont(ofSize: 11, weight: .regular)
        healthLabel.textColor = OverlayTheme.Colors.textSubtle
        healthLabel.backgroundColor = .clear
        addSubview(healthLabel)

        applyFonts()
        update(with: .idle)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func layout() {
        super.layout()

        let width = bounds.width
        let top = bounds.height
        let labelWidth = max(0, width - Layout.horizontalPadding * 2)
        let titleWidth = max(0, width - 52)

        indicatorView.frame = NSRect(x: 16, y: top - 28, width: 12, height: 12)
        titleLabel.frame = NSRect(x: 36, y: top - 34, width: titleWidth, height: 22)
        statusLabel.frame = NSRect(x: 16, y: top - 58, width: labelWidth, height: 22)
        detailLabel.frame = NSRect(x: 16, y: top - 80, width: labelWidth, height: 18)
        captureLabel.frame = NSRect(x: 16, y: top - 102, width: labelWidth, height: 18)
        healthLabel.frame = NSRect(x: 16, y: 14, width: labelWidth, height: 32)
    }

    func update(with snapshot: TelemetryCaptureStatusSnapshot) {
        let health = CaptureHealth(snapshot: snapshot)

        if health.level == .error {
            layer?.backgroundColor = OverlayTheme.Colors.errorBackground.cgColor
            indicatorView.layer?.backgroundColor = OverlayTheme.Colors.errorIndicator.cgColor
        } else if health.level == .warning {
            layer?.backgroundColor = OverlayTheme.Colors.warningBackground.cgColor
            indicatorView.layer?.backgroundColor = OverlayTheme.Colors.warningIndicator.cgColor
        } else if snapshot.isCapturing {
            layer?.backgroundColor = OverlayTheme.Colors.successBackground.cgColor
            indicatorView.layer?.backgroundColor = OverlayTheme.Colors.successIndicator.cgColor
        } else if snapshot.isConnected {
            layer?.backgroundColor = OverlayTheme.Colors.warningBackground.cgColor
            indicatorView.layer?.backgroundColor = OverlayTheme.Colors.warningIndicator.cgColor
        } else {
            layer?.backgroundColor = OverlayTheme.Colors.neutralBackground.cgColor
            indicatorView.layer?.backgroundColor = OverlayTheme.Colors.neutralIndicator.cgColor
        }

        statusLabel.stringValue = health.statusText
        detailLabel.stringValue = health.detailText
        captureLabel.stringValue = health.captureText
        healthLabel.stringValue = health.messageText
        captureLabel.isHidden = !showCaptureDetails
        healthLabel.isHidden = !showHealthDetails
    }

    private func applyFonts() {
        titleLabel.font = OverlayTheme.font(family: fontFamily, size: 15, weight: .semibold)
        statusLabel.font = OverlayTheme.font(family: fontFamily, size: 13)
        detailLabel.font = OverlayTheme.font(family: fontFamily, size: 12)
        captureLabel.font = OverlayTheme.font(family: fontFamily, size: 11)
        healthLabel.font = OverlayTheme.font(family: fontFamily, size: 11)
    }

    private enum CaptureHealthLevel {
        case ok
        case warning
        case error
    }

    private struct CaptureHealth {
        let level: CaptureHealthLevel
        let statusText: String
        let detailText: String
        let captureText: String
        let messageText: String

        init(snapshot: TelemetryCaptureStatusSnapshot) {
            let now = Date()
            let captureURL = snapshot.currentCaptureDirectory ?? snapshot.lastCaptureDirectory ?? snapshot.captureRoot
            let frameAge = Self.ageSeconds(snapshot.lastFrameCapturedAtUtc, now: now)
            let diskAge = Self.ageSeconds(snapshot.lastDiskWriteAtUtc, now: now)
            let detail = snapshot.rawCaptureEnabled
                ? String(
                    format: "queued %7d  written %7d  drops %4d  file %@",
                    snapshot.frameCount,
                    snapshot.writtenFrameCount,
                    snapshot.droppedFrameCount,
                    Self.formatBytes(snapshot.telemetryFileBytes)
                )
                : String(format: "frames %7d  history on  raw off", snapshot.frameCount)
            let capture = snapshot.rawCaptureEnabled
                ? "raw: \(Self.compactPath(captureURL))"
                : "raw: disabled; history ready"
            let appWarning = snapshot.appWarning.map { "warning: \(Self.trim($0))" }

            if let lastError = snapshot.lastError, !lastError.isEmpty {
                level = .error
                statusText = "Capture error"
                detailText = detail
                captureText = capture
                messageText = "error: \(Self.trim(lastError))"
                return
            }

            if !snapshot.isConnected {
                level = .warning
                statusText = "Waiting for iRacing"
                detailText = "collector idle"
                captureText = capture
                messageText = Self.combine(appWarning, "health: sim not connected; no live telemetry source")
                return
            }

            if !snapshot.isCapturing {
                level = .warning
                statusText = "Connected, waiting for telemetry"
                detailText = "waiting for first telemetry frame"
                captureText = capture
                messageText = Self.combine(appWarning, "health: SDK connected but no live telemetry frame has started collection")
                return
            }

            if snapshot.rawCaptureEnabled && snapshot.frameCount > 0 && snapshot.writtenFrameCount == 0 {
                level = .error
                statusText = "Frames queued, not written"
                detailText = detail
                captureText = capture
                messageText = "error: telemetry frames arrived but disk writer has not confirmed writes"
                return
            }

            if snapshot.droppedFrameCount > 0 {
                level = .warning
                statusText = "Collecting with dropped frames"
                detailText = detail
                captureText = capture
                messageText = "warning: capture queue overflowed; disk may be too slow"
                return
            }

            if let frameAge, frameAge > 5 {
                level = .error
                statusText = "Telemetry frames stalled"
                detailText = detail
                captureText = capture
                messageText = String(format: "error: no SDK frame for %.0fs", frameAge)
                return
            }

            if snapshot.rawCaptureEnabled, let diskAge, diskAge > 5 {
                level = .error
                statusText = "Disk writes stalled"
                detailText = detail
                captureText = capture
                messageText = String(format: "error: no telemetry.bin write confirmation for %.0fs", diskAge)
                return
            }

            if let lastWarning = snapshot.lastWarning, !lastWarning.isEmpty {
                level = .warning
                statusText = "Collecting with warning"
                detailText = detail
                captureText = capture
                messageText = "warning: \(Self.trim(lastWarning))"
                return
            }

            if let appWarning {
                level = .warning
                statusText = "Build may be stale"
                detailText = detail
                captureText = capture
                messageText = appWarning
                return
            }

            level = .ok
            statusText = snapshot.rawCaptureEnabled ? "Collecting raw telemetry" : "Analyzing live telemetry"
            detailText = detail
            captureText = capture
            messageText = snapshot.rawCaptureEnabled
                ? "health: live frames ok; last frame \(Self.formatAge(frameAge)), disk \(Self.formatAge(diskAge))"
                : "health: live analysis ok; last frame \(Self.formatAge(frameAge))"
        }

        private static func ageSeconds(_ timestamp: Date?, now: Date) -> Double? {
            guard let timestamp else {
                return nil
            }

            return max(0, now.timeIntervalSince(timestamp))
        }

        private static func formatAge(_ seconds: Double?) -> String {
            guard let seconds else {
                return "n/a"
            }

            return String(format: "%.1fs ago", seconds)
        }

        private static func formatBytes(_ bytes: Int64?) -> String {
            guard let bytes else {
                return "n/a"
            }

            if bytes < 1024 {
                return "\(bytes) B"
            }

            if bytes < 1024 * 1024 {
                return String(format: "%.1f KB", Double(bytes) / 1024)
            }

            return String(format: "%.1f MB", Double(bytes) / 1024 / 1024)
        }

        private static func compactPath(_ url: URL?) -> String {
            guard let url else {
                return "not resolved"
            }

            let parts = url.path.split(separator: "/")
            if parts.count <= 3 {
                return url.path
            }

            return ".../" + parts.suffix(3).joined(separator: "/")
        }

        private static func trim(_ value: String) -> String {
            let normalized = value.replacingOccurrences(of: "\n", with: " ")
            guard normalized.count > 96 else {
                return normalized
            }

            return String(normalized.prefix(95)) + "..."
        }

        private static func combine(_ first: String?, _ second: String) -> String {
            guard let first, !first.isEmpty else {
                return second
            }

            return "\(first) | \(second)"
        }
    }
}
