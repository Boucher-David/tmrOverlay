import Foundation

final class DiagnosticsBundleWriter {
    private let appDataRoot: URL
    private let captureRoot: URL
    private let historyRoot: URL
    private let diagnosticsRoot: URL
    private let logsRoot: URL
    private let settingsRoot: URL
    private let eventsRoot: URL
    private let runtimeStateURL: URL
    private let makeTelemetrySnapshot: () -> TelemetryCaptureStatusSnapshot

    init(
        appDataRoot: URL,
        captureRoot: URL,
        historyRoot: URL,
        diagnosticsRoot: URL,
        logsRoot: URL,
        settingsRoot: URL,
        eventsRoot: URL,
        runtimeStateURL: URL,
        makeTelemetrySnapshot: @escaping () -> TelemetryCaptureStatusSnapshot
    ) {
        self.appDataRoot = appDataRoot
        self.captureRoot = captureRoot
        self.historyRoot = historyRoot
        self.diagnosticsRoot = diagnosticsRoot
        self.logsRoot = logsRoot
        self.settingsRoot = settingsRoot
        self.eventsRoot = eventsRoot
        self.runtimeStateURL = runtimeStateURL
        self.makeTelemetrySnapshot = makeTelemetrySnapshot
    }

    func createBundle(source: String = "manual") throws -> URL {
        try FileManager.default.createDirectory(at: diagnosticsRoot, withIntermediateDirectories: true)
        let bundleURL = diagnosticsRoot.appendingPathComponent("tmroverlay-diagnostics-\(Self.timestamp()).diagnostics", isDirectory: true)
        try FileManager.default.createDirectory(at: bundleURL, withIntermediateDirectories: true)

        let metadataURL = bundleURL.appendingPathComponent("metadata", isDirectory: true)
        try FileManager.default.createDirectory(at: metadataURL, withIntermediateDirectories: true)
        let telemetrySnapshot = makeTelemetrySnapshot()
        try writeJson(AppVersionInfo.current, to: metadataURL.appendingPathComponent("app-version.json"))
        try writeJson([
            "createdAtUtc": ISO8601DateFormatter().string(from: Date()),
            "source": source
        ], to: metadataURL.appendingPathComponent("diagnostics-bundle.json"))
        try writeJson(storageMetadata(), to: metadataURL.appendingPathComponent("storage.json"))
        try writeJson(telemetryStateMetadata(telemetrySnapshot), to: metadataURL.appendingPathComponent("telemetry-state.json"))
        try writeJson(performanceMetadata(telemetrySnapshot), to: metadataURL.appendingPathComponent("performance.json"))
        try writeJson(uiFreezeWatchMetadata(telemetrySnapshot), to: metadataURL.appendingPathComponent("ui-freeze-watch.json"))
        copyIfExists(runtimeStateURL, to: bundleURL.appendingPathComponent("runtime", isDirectory: true).appendingPathComponent("runtime-state.json"))
        copySanitizedSettingsIfExists(settingsRoot.appendingPathComponent("settings.json"), to: bundleURL.appendingPathComponent("settings", isDirectory: true).appendingPathComponent("settings.json"))
        copyRecentFiles(from: logsRoot, to: bundleURL.appendingPathComponent("logs", isDirectory: true), suffix: ".log", limit: 10)
        copyRecentFiles(from: logsRoot.appendingPathComponent("performance", isDirectory: true), to: bundleURL.appendingPathComponent("performance", isDirectory: true), suffix: ".jsonl", limit: 10)
        copyRecentFiles(from: logsRoot.appendingPathComponent("overlay-diagnostics", isDirectory: true), to: bundleURL.appendingPathComponent("overlay-diagnostics", isDirectory: true), suffix: ".json", limit: 10)
        copyRecentFiles(from: eventsRoot, to: bundleURL.appendingPathComponent("events", isDirectory: true), suffix: ".jsonl", limit: 10)
        copyLatestCaptureMetadata(to: bundleURL.appendingPathComponent("latest-capture", isDirectory: true))
        return bundleURL
    }

    private func copyLatestCaptureMetadata(to destination: URL) {
        guard let latestCapture = latestDirectory(in: captureRoot, prefix: "capture-") else {
            return
        }

        try? FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
        for fileName in ["capture-manifest.json", "telemetry-schema.json", "latest-session.yaml", "live-overlay-diagnostics.json"] {
            copyIfExists(latestCapture.appendingPathComponent(fileName), to: destination.appendingPathComponent(fileName))
        }
    }

    private func copyRecentFiles(from source: URL, to destination: URL, suffix: String, limit: Int) {
        guard let urls = try? FileManager.default.contentsOfDirectory(
            at: source,
            includingPropertiesForKeys: [.contentModificationDateKey],
            options: [.skipsHiddenFiles]
        ) else {
            return
        }

        try? FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
        for url in urls.filter({ $0.lastPathComponent.hasSuffix(suffix) }).sorted(by: { modificationDate($0) > modificationDate($1) }).prefix(limit) {
            copyIfExists(url, to: destination.appendingPathComponent(url.lastPathComponent))
        }
    }

    private func latestDirectory(in root: URL, prefix: String) -> URL? {
        guard let urls = try? FileManager.default.contentsOfDirectory(
            at: root,
            includingPropertiesForKeys: [.contentModificationDateKey],
            options: [.skipsHiddenFiles]
        ) else {
            return nil
        }

        return urls.filter { $0.lastPathComponent.hasPrefix(prefix) }.max { modificationDate($0) < modificationDate($1) }
    }

    private func copyIfExists(_ source: URL, to destination: URL) {
        guard FileManager.default.fileExists(atPath: source.path) else {
            return
        }

        try? FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        try? FileManager.default.removeItem(at: destination)
        try? FileManager.default.copyItem(at: source, to: destination)
    }

    private func copySanitizedSettingsIfExists(_ source: URL, to destination: URL) {
        guard FileManager.default.fileExists(atPath: source.path) else {
            return
        }

        do {
            let data = try Data(contentsOf: source)
            guard var root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                throw NSError(domain: "TmrOverlayMac", code: 1)
            }

            if var overlays = root["overlays"] as? [[String: Any]] {
                for index in overlays.indices {
                    guard (overlays[index]["id"] as? String)?.caseInsensitiveCompare("stream-chat") == .orderedSame else {
                        continue
                    }

                    if overlays[index]["streamChatStreamlabsUrl"] != nil {
                        overlays[index]["streamChatStreamlabsUrl"] = "<redacted>"
                    }

                    if var options = overlays[index]["options"] as? [String: Any],
                       options["stream-chat.streamlabs-url"] != nil {
                        options["stream-chat.streamlabs-url"] = "<redacted>"
                        overlays[index]["options"] = options
                    }
                }
                root["overlays"] = overlays
            }

            try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            try? FileManager.default.removeItem(at: destination)
            let sanitized = try JSONSerialization.data(withJSONObject: root, options: [.prettyPrinted, .sortedKeys])
            try sanitized.write(to: destination)
        } catch {
            let redactedNote = destination.deletingLastPathComponent().appendingPathComponent("settings-redacted.txt")
            try? FileManager.default.createDirectory(at: redactedNote.deletingLastPathComponent(), withIntermediateDirectories: true)
            try? "Settings could not be parsed; omitted to avoid copying private stream chat widget URLs."
                .write(to: redactedNote, atomically: true, encoding: .utf8)
        }
    }

    private func storageMetadata() -> [String: String] {
        [
            "appDataRoot": appDataRoot.path,
            "captureRoot": captureRoot.path,
            "historyRoot": historyRoot.path,
            "logsRoot": logsRoot.path,
            "settingsRoot": settingsRoot.path,
            "diagnosticsRoot": diagnosticsRoot.path,
            "eventsRoot": eventsRoot.path,
            "runtimeStatePath": runtimeStateURL.path
        ]
    }

    private func telemetryStateMetadata(_ snapshot: TelemetryCaptureStatusSnapshot) -> [String: String] {
        [
            "isConnected": String(snapshot.isConnected),
            "isCapturing": String(snapshot.isCapturing),
            "rawCaptureEnabled": String(snapshot.rawCaptureEnabled),
            "rawCaptureActive": String(snapshot.rawCaptureActive),
            "captureRoot": snapshot.captureRoot?.path ?? "",
            "currentCaptureDirectory": snapshot.currentCaptureDirectory?.path ?? "",
            "lastCaptureDirectory": snapshot.lastCaptureDirectory?.path ?? "",
            "frameCount": String(snapshot.frameCount),
            "writtenFrameCount": String(snapshot.writtenFrameCount),
            "droppedFrameCount": String(snapshot.droppedFrameCount),
            "telemetryFileBytes": snapshot.telemetryFileBytes.map(String.init) ?? "",
            "appWarning": snapshot.appWarning ?? "",
            "lastWarning": snapshot.lastWarning ?? "",
            "lastError": snapshot.lastError ?? ""
        ]
    }

    private func performanceMetadata(_ snapshot: TelemetryCaptureStatusSnapshot) -> [String: String] {
        [
            "source": "mac-harness-mock",
            "telemetryFrameCount": String(snapshot.frameCount),
            "telemetryFramesPerSecond": snapshot.isCapturing ? "60" : "0",
            "dataChangedAverageMilliseconds": snapshot.isCapturing ? "0.42" : "0",
            "dataChangedP95Milliseconds": snapshot.isCapturing ? "1.35" : "0",
            "iracingChanQualityLast": snapshot.isCapturing ? "0.94" : "",
            "iracingChanLatencyLast": snapshot.isCapturing ? "0.066667" : "",
            "iracingFrameRateLast": snapshot.isCapturing ? "60" : "",
            "overlayRefreshAverageMilliseconds": snapshot.isCapturing ? "1.8" : "0",
            "overlayRefreshP95Milliseconds": snapshot.isCapturing ? "4.7" : "0",
            "overlayInputChangedPercent": snapshot.isCapturing ? "status 25, fuel 100, radar 100, gap 100" : "",
            "writtenFrameCount": String(snapshot.writtenFrameCount),
            "droppedFrameCount": String(snapshot.droppedFrameCount),
            "telemetryFileBytes": snapshot.telemetryFileBytes.map(String.init) ?? "",
            "processMemory": "not collected in mac harness"
        ]
    }

    private func uiFreezeWatchMetadata(_ snapshot: TelemetryCaptureStatusSnapshot) -> [String: String] {
        [
            "source": "mac-harness-mock",
            "telemetryFrameCount": String(snapshot.frameCount),
            "settingsApplyMode": "immediate-main-thread-mock",
            "flagsInputPolicy": "ignoresMouseEvents-and-hidden-while-settings-active",
            "windowsValidationNote": "Production freeze triage lives in Windows metadata/ui-freeze-watch.json and performance overlay.window/timer metrics."
        ]
    }

    private func writeJson<T: Encodable>(_ value: T, to url: URL) throws {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted]
        try encoder.encode(value).write(to: url)
    }

    private func modificationDate(_ url: URL) -> Date {
        ((try? url.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate) ?? .distantPast
    }

    private static func timestamp() -> String {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyyMMdd-HHmmss-SSS"
        return formatter.string(from: Date())
    }
}
