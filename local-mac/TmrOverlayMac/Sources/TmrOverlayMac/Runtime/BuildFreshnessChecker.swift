import Foundation

struct BuildFreshnessResult {
    let sourceNewerThanBuild: Bool
    let message: String?

    static let current = BuildFreshnessResult(sourceNewerThanBuild: false, message: nil)
}

enum BuildFreshnessChecker {
    static func check() -> BuildFreshnessResult {
        guard let repositoryRoot = findRepositoryRoot(),
              let executableURL = Bundle.main.executableURL ?? executableURLFromArguments() else {
            return .current
        }

        let executableDate = modificationDate(executableURL)
        guard let newestSource = newestSourceFile(in: repositoryRoot),
              modificationDate(newestSource) > executableDate.addingTimeInterval(2) else {
            return .current
        }

        let relativePath = newestSource.path.replacingOccurrences(of: repositoryRoot.path + "/", with: "")
        return BuildFreshnessResult(
            sourceNewerThanBuild: true,
            message: "Local source is newer than this build; rebuild recommended. Newest: \(relativePath)"
        )
    }

    private static func executableURLFromArguments() -> URL? {
        guard let executablePath = CommandLine.arguments.first else {
            return nil
        }

        return URL(fileURLWithPath: executablePath).standardizedFileURL
    }

    private static func newestSourceFile(in repositoryRoot: URL) -> URL? {
        let roots = [
            repositoryRoot.appendingPathComponent("local-mac/TmrOverlayMac/Package.swift"),
            repositoryRoot.appendingPathComponent("local-mac/TmrOverlayMac/Sources", isDirectory: true),
            repositoryRoot.appendingPathComponent("local-mac/TmrOverlayMac/Tests", isDirectory: true),
            repositoryRoot.appendingPathComponent("src", isDirectory: true),
            repositoryRoot.appendingPathComponent("README.md")
        ]

        return roots
            .flatMap(sourceFiles)
            .max { modificationDate($0) < modificationDate($1) }
    }

    private static func sourceFiles(in url: URL) -> [URL] {
        if FileManager.default.fileExists(atPath: url.path), !isDirectory(url) {
            return [url]
        }

        guard isDirectory(url),
              let enumerator = FileManager.default.enumerator(
                at: url,
                includingPropertiesForKeys: [.isRegularFileKey, .contentModificationDateKey],
                options: [.skipsHiddenFiles]
              ) else {
            return []
        }

        return enumerator
            .compactMap { $0 as? URL }
            .filter { isSourceFile($0) && !isBuildOutput($0) }
    }

    private static func isSourceFile(_ url: URL) -> Bool {
        ["swift", "cs", "csproj", "json", "md"].contains(url.pathExtension.lowercased())
    }

    private static func isBuildOutput(_ url: URL) -> Bool {
        let path = url.path
        return path.contains("/.build/")
            || path.contains("/bin/")
            || path.contains("/obj/")
    }

    private static func isDirectory(_ url: URL) -> Bool {
        ((try? url.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory) == true
    }

    private static func modificationDate(_ url: URL) -> Date {
        ((try? url.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate) ?? .distantPast
    }

    private static func findRepositoryRoot() -> URL? {
        var directory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).standardizedFileURL

        while true {
            if FileManager.default.fileExists(atPath: directory.appendingPathComponent("tmrOverlay.sln").path) {
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
