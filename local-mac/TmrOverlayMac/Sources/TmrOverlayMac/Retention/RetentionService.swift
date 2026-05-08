import Foundation

final class RetentionService {
    func clean(
        captureRoot: URL,
        diagnosticsRoot: URL,
        captureRetentionDays: Int = 30,
        maxCaptureDirectories: Int = 50,
        diagnosticsRetentionDays: Int = 14,
        maxDiagnosticsBundles: Int = 20
    ) {
        cleanDirectories(
            root: captureRoot,
            matches: { $0.lastPathComponent.hasPrefix("capture-") },
            maxAgeDays: captureRetentionDays,
            maxCount: maxCaptureDirectories
        )
        cleanDirectories(
            root: diagnosticsRoot,
            matches: { $0.lastPathComponent.hasSuffix(".diagnostics") },
            maxAgeDays: diagnosticsRetentionDays,
            maxCount: maxDiagnosticsBundles
        )
    }

    private func cleanDirectories(root: URL, matches: (URL) -> Bool, maxAgeDays: Int, maxCount: Int) {
        guard let urls = try? FileManager.default.contentsOfDirectory(
            at: root,
            includingPropertiesForKeys: [.contentModificationDateKey],
            options: [.skipsHiddenFiles]
        ) else {
            return
        }

        let cutoff = Date().addingTimeInterval(-Double(max(1, maxAgeDays)) * 24 * 60 * 60)
        let sorted = urls
            .filter(matches)
            .sorted { lhs, rhs in modificationDate(lhs) > modificationDate(rhs) }

        var deletedPaths = Set<String>()

        for (index, url) in sorted.enumerated() {
            if index >= max(1, maxCount) || modificationDate(url) < cutoff {
                if deletedPaths.insert(url.path).inserted {
                    try? FileManager.default.removeItem(at: url)
                }
            }
        }
    }

    private func modificationDate(_ url: URL) -> Date {
        ((try? url.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate) ?? .distantPast
    }
}
