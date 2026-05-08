import AppKit
import UniformTypeIdentifiers

enum GarageCoverImageStore {
    private static let allowedExtensions: Set<String> = ["png", "jpg", "jpeg", "bmp", "gif"]

    static func configureImportPanel(_ panel: NSOpenPanel) {
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = allowedExtensions.compactMap { UTType(filenameExtension: $0) }
        panel.title = "Import garage cover image"
    }

    static func copyImage(from sourceURL: URL) throws -> URL {
        let fileExtension = sourceURL.pathExtension.lowercased()
        guard allowedExtensions.contains(fileExtension) else {
            throw NSError(
                domain: "TmrOverlayMac",
                code: 1,
                userInfo: [NSLocalizedDescriptionKey: "Garage cover images must be PNG, JPG, BMP, or GIF files."]
            )
        }

        let directory = storageDirectory
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let destination = directory.appendingPathComponent("cover.\(fileExtension)")
        if sourceURL.standardizedFileURL.path == destination.standardizedFileURL.path {
            return destination
        }

        let temporaryDestination = directory.appendingPathComponent("cover-import.\(fileExtension)")
        try? FileManager.default.removeItem(at: temporaryDestination)
        try FileManager.default.copyItem(at: sourceURL, to: temporaryDestination)
        removeCoverImages(in: directory)
        try? FileManager.default.removeItem(at: destination)
        try FileManager.default.moveItem(at: temporaryDestination, to: destination)
        return destination
    }

    static func clearImportedImages() {
        removeCoverImages(in: storageDirectory)
    }

    private static var storageDirectory: URL {
        AppPaths.settingsRoot().appendingPathComponent("garage-cover", isDirectory: true)
    }

    private static func removeCoverImages(in directory: URL) {
        guard let existing = try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil) else {
            return
        }

        for url in existing where url.deletingPathExtension().lastPathComponent == "cover" {
            try? FileManager.default.removeItem(at: url)
        }
    }
}
