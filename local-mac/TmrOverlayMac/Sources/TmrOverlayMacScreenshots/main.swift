import Foundation
import TmrOverlayMacCore

let outputRoot = CommandLine.arguments.dropFirst().first.map(URL.init(fileURLWithPath:))
    ?? URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .appendingPathComponent("mocks", isDirectory: true)

do {
    try OverlayScreenshotGenerator.renderScreenshots(to: outputRoot)
    print("Wrote overlay screenshots to \(outputRoot.path)")
} catch {
    fputs("Failed to render overlay screenshots: \(error)\n", stderr)
    exit(1)
}
