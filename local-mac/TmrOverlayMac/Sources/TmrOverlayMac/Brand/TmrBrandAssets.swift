import AppKit

enum TmrBrandAssets {
    private static let logoRelativePath = "assets/brand/TMRLogo.png"

    static func loadLogoImage() -> NSImage? {
        for url in logoCandidates() where FileManager.default.fileExists(atPath: url.path) {
            if let image = NSImage(contentsOf: url) {
                return image
            }
        }

        return nil
    }

    private static func logoCandidates() -> [URL] {
        var candidates: [URL] = []
        if let resourceURL = Bundle.main.resourceURL {
            candidates.append(resourceURL.appendingPathComponent("TMRLogo.png"))
            candidates.append(resourceURL.appendingPathComponent(logoRelativePath))
        }

        let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).standardizedFileURL
        candidates.append(currentDirectory.appendingPathComponent(logoRelativePath))
        if let repositoryRoot = findRepositoryRoot(startingAt: currentDirectory) {
            candidates.append(repositoryRoot.appendingPathComponent(logoRelativePath))
        }

        return candidates
    }

    private static func findRepositoryRoot(startingAt url: URL) -> URL? {
        var directory = url

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
