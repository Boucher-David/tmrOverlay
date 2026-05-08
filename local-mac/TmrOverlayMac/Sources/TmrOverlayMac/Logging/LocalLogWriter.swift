import Foundation

final class LocalLogWriter {
    private let logsRoot: URL
    private let lock = NSLock()

    init(logsRoot: URL) {
        self.logsRoot = logsRoot
        try? FileManager.default.createDirectory(at: logsRoot, withIntermediateDirectories: true)
    }

    func info(_ message: String) {
        write(level: "Information", message)
    }

    func warning(_ message: String) {
        write(level: "Warning", message)
    }

    func error(_ message: String) {
        write(level: "Error", message)
    }

    private func write(level: String, _ message: String) {
        lock.withLock {
            do {
                try FileManager.default.createDirectory(at: logsRoot, withIntermediateDirectories: true)
                let path = logsRoot.appendingPathComponent("tmroverlay-\(Self.dateStamp()).log")
                let line = "\(ISO8601DateFormatter().string(from: Date())) [\(level)] \(message)\n"
                let data = Data(line.utf8)

                if FileManager.default.fileExists(atPath: path.path) {
                    let handle = try FileHandle(forWritingTo: path)
                    try handle.seekToEnd()
                    try handle.write(contentsOf: data)
                    try handle.close()
                } else {
                    try data.write(to: path)
                }
            } catch {
                NSLog("Failed to write local log: \(error)")
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
