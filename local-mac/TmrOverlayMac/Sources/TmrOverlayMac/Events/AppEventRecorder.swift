import Foundation

struct AppEvent: Codable {
    let timestampUtc: Date
    let name: String
    let properties: [String: String]
}

final class AppEventRecorder {
    private let eventsRoot: URL
    private let encoder = JSONEncoder()
    private let lock = NSLock()

    init(eventsRoot: URL) {
        self.eventsRoot = eventsRoot
        encoder.dateEncodingStrategy = .iso8601
    }

    func record(_ name: String, properties: [String: String] = [:]) {
        lock.withLock {
            do {
                try FileManager.default.createDirectory(at: eventsRoot, withIntermediateDirectories: true)
                let event = AppEvent(timestampUtc: Date(), name: name, properties: properties)
                let data = try encoder.encode(event)
                let path = eventsRoot.appendingPathComponent("events-\(Self.dateStamp()).jsonl")
                let line = data + Data("\n".utf8)

                if FileManager.default.fileExists(atPath: path.path) {
                    let handle = try FileHandle(forWritingTo: path)
                    try handle.seekToEnd()
                    try handle.write(contentsOf: line)
                    try handle.close()
                } else {
                    try line.write(to: path)
                }
            } catch {
                NSLog("Failed to record app event: \(error)")
            }
        }
    }

    private static func dateStamp() -> String {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyyMMdd"
        return formatter.string(from: Date())
    }
}
