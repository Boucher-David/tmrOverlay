import AppKit

enum TmrBrandAssets {
    private static let logoRelativePath = "assets/brand/TMRLogo.png"
    private static let garageCoverRelativePath = "assets/brand/Team_Logo_4k_TMRBRANDING.png"

    static func loadLogoImage() -> NSImage? {
        loadImage(candidates: assetCandidates(outputFileName: "TMRLogo.png", sourceRelativePath: logoRelativePath))
    }

    static func loadGarageCoverDefaultImage() -> NSImage? {
        loadImage(candidates: assetCandidates(outputFileName: "GarageCoverDefault.png", sourceRelativePath: garageCoverRelativePath))
    }

    private static func loadImage(candidates: [URL]) -> NSImage? {
        for url in candidates where FileManager.default.fileExists(atPath: url.path) {
            if let image = NSImage(contentsOf: url) {
                return image
            }
        }

        return nil
    }

    private static func assetCandidates(outputFileName: String, sourceRelativePath: String) -> [URL] {
        var candidates: [URL] = []
        if let resourceURL = Bundle.main.resourceURL {
            candidates.append(resourceURL.appendingPathComponent(outputFileName))
            candidates.append(resourceURL.appendingPathComponent(sourceRelativePath))
        }

        let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).standardizedFileURL
        candidates.append(currentDirectory.appendingPathComponent(sourceRelativePath))
        if let repositoryRoot = findRepositoryRoot(startingAt: currentDirectory) {
            candidates.append(repositoryRoot.appendingPathComponent(sourceRelativePath))
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
