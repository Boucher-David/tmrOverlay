import Foundation

enum AppPaths {
    static func appDataRoot() -> URL {
        if let override = environmentPath("TMR_MAC_APP_DATA_ROOT") {
            return override
        }

        if environmentFlag("TMR_MAC_USE_REPOSITORY_LOCAL_STORAGE") {
            return repositoryLocalRoot()
        }

        return applicationSupportRoot()
    }

    static func captureRoot() -> URL {
        if let override = environmentPath("TMR_MAC_CAPTURE_ROOT") {
            return override
        }

        return appDataRoot().appendingPathComponent("captures", isDirectory: true)
    }

    static func historyRoot() -> URL {
        if let override = environmentPath("TMR_MAC_HISTORY_ROOT") {
            return override
        }

        return appDataRoot()
            .appendingPathComponent("history", isDirectory: true)
            .appendingPathComponent("user", isDirectory: true)
    }

    static func logsRoot() -> URL {
        appDataRoot().appendingPathComponent("logs", isDirectory: true)
    }

    static func settingsRoot() -> URL {
        appDataRoot().appendingPathComponent("settings", isDirectory: true)
    }

    static func diagnosticsRoot() -> URL {
        appDataRoot().appendingPathComponent("diagnostics", isDirectory: true)
    }

    static func eventsRoot() -> URL {
        logsRoot().appendingPathComponent("events", isDirectory: true)
    }

    static func runtimeStateURL() -> URL {
        appDataRoot().appendingPathComponent("runtime-state.json")
    }

    private static func applicationSupportRoot() -> URL {
        let supportRoot = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSHomeDirectory())
                .appendingPathComponent("Library", isDirectory: true)
                .appendingPathComponent("Application Support", isDirectory: true)

        return supportRoot
            .appendingPathComponent("TmrOverlayMac", isDirectory: true)
            .standardizedFileURL
    }

    private static func repositoryLocalRoot() -> URL {
        let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let repositoryRoot = findRepositoryRoot(startingAt: currentDirectory)
        return (repositoryRoot ?? currentDirectory)
            .appendingPathComponent("local-mac", isDirectory: true)
            .appendingPathComponent("TmrOverlayMac", isDirectory: true)
            .standardizedFileURL
    }

    private static func environmentPath(_ name: String) -> URL? {
        guard let value = ProcessInfo.processInfo.environment[name],
              !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }

        return URL(fileURLWithPath: value).standardizedFileURL
    }

    private static func environmentFlag(_ name: String) -> Bool {
        guard let value = ProcessInfo.processInfo.environment[name] else {
            return false
        }

        return ["1", "true", "yes", "on"].contains(value.lowercased())
    }

    private static func findRepositoryRoot(startingAt url: URL) -> URL? {
        var directory = url.standardizedFileURL

        while true {
            let solutionURL = directory.appendingPathComponent("tmrOverlay.sln")
            if FileManager.default.fileExists(atPath: solutionURL.path) {
                return directory
            }

            let parent = directory.deletingLastPathComponent()
            if parent.path == directory.path {
                return nil
            }

            directory = parent
        }
    }
}
