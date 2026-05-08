import Foundation

struct RuntimeState: Codable {
    let runtimeStateVersion: Int
    let startedAtUtc: Date
    var lastHeartbeatAtUtc: Date?
    var stoppedAtUtc: Date?
    var stoppedCleanly: Bool
    let appVersion: AppVersionInfo
}

final class RuntimeStateStore {
    private let stateURL: URL
    private let lock = NSLock()
    private var state: RuntimeState?
    private var heartbeatTimer: DispatchSourceTimer?

    init(stateURL: URL) {
        self.stateURL = stateURL
    }

    func start() -> RuntimeState? {
        let previous = readPrevious()
        lock.withLock {
            state = RuntimeState(
                runtimeStateVersion: 1,
                startedAtUtc: Date(),
                lastHeartbeatAtUtc: Date(),
                stoppedAtUtc: nil,
                stoppedCleanly: false,
                appVersion: .current
            )
            writeLocked()
        }
        startHeartbeat()
        return previous
    }

    func stopCleanly() {
        heartbeatTimer?.cancel()
        heartbeatTimer = nil

        lock.withLock {
            state?.lastHeartbeatAtUtc = Date()
            state?.stoppedAtUtc = Date()
            state?.stoppedCleanly = true
            writeLocked()
        }
    }

    private func readPrevious() -> RuntimeState? {
        guard let data = try? Data(contentsOf: stateURL) else {
            return nil
        }

        return try? JSONDecoder().decode(RuntimeState.self, from: data)
    }

    private func startHeartbeat() {
        let timer = DispatchSource.makeTimerSource(queue: DispatchQueue.global(qos: .utility))
        timer.schedule(deadline: .now() + 30, repeating: 30)
        timer.setEventHandler { [weak self] in
            self?.heartbeat()
        }
        heartbeatTimer = timer
        timer.resume()
    }

    private func heartbeat() {
        lock.withLock {
            state?.lastHeartbeatAtUtc = Date()
            writeLocked()
        }
    }

    private func writeLocked() {
        guard let state else {
            return
        }

        do {
            try FileManager.default.createDirectory(at: stateURL.deletingLastPathComponent(), withIntermediateDirectories: true)
            let encoder = JSONEncoder()
            encoder.dateEncodingStrategy = .iso8601
            encoder.outputFormatting = [.prettyPrinted]
            try encoder.encode(state).write(to: stateURL)
        } catch {
            NSLog("Failed to write runtime state: \(error)")
        }
    }
}
