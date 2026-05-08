import Foundation

final class PerformanceLogWriter {
    private let performanceRoot: URL
    private let encoder = JSONEncoder()

    init(logsRoot: URL) {
        performanceRoot = logsRoot.appendingPathComponent("performance", isDirectory: true)
        encoder.outputFormatting = [.sortedKeys]
    }

    func record(_ snapshot: TelemetryCaptureStatusSnapshot) {
        let entry: [String: String] = [
            "timestampUtc": ISO8601DateFormatter().string(from: Date()),
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
            "telemetryFileBytes": snapshot.telemetryFileBytes.map(String.init) ?? ""
        ]

        do {
            try FileManager.default.createDirectory(at: performanceRoot, withIntermediateDirectories: true)
            let path = performanceRoot.appendingPathComponent("performance-\(Self.dayStamp()).jsonl")
            let data = try encoder.encode(entry)
            if FileManager.default.fileExists(atPath: path.path),
               let handle = try? FileHandle(forWritingTo: path) {
                defer { try? handle.close() }
                try handle.seekToEnd()
                try handle.write(contentsOf: data)
                try handle.write(contentsOf: Data("\n".utf8))
            } else {
                try (String(data: data, encoding: .utf8) ?? "{}").appending("\n").write(to: path, atomically: true, encoding: .utf8)
            }
        } catch {
            // Performance diagnostics must never interfere with mock telemetry.
        }
    }

    private static func dayStamp() -> String {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyyMMdd"
        return formatter.string(from: Date())
    }
}
