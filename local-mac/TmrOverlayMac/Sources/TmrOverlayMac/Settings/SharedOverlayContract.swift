import AppKit
import Foundation

enum SharedOverlayContract {
    static let defaultContractRelativePath = "shared/tmr-overlay-contract.json"
    static let defaultSchemaRelativePath = "shared/tmr-overlay-contract.schema.json"
    static let streamChatProviderKey = "stream-chat.provider"
    static let streamChatStreamlabsUrlKey = "stream-chat.streamlabs-url"
    static let streamChatTwitchChannelKey = "stream-chat.twitch-channel"

    static let current = load()

    struct Snapshot {
        let contractVersion: Int
        let settingsVersion: Int
        let defaultFontFamily: String
        let defaultUnitSystem: String
        let overlayOptionDefaults: [String: [String: String]]
        let designV2Colors: [String: String]
        let loadedPath: String?

        var streamChatDefaultProvider: String {
            overlayOptionDefault(
                overlayId: "stream-chat",
                optionKey: streamChatProviderKey,
                fallback: "twitch"
            )
        }

        var streamChatDefaultTwitchChannel: String {
            overlayOptionDefault(
                overlayId: "stream-chat",
                optionKey: streamChatTwitchChannelKey,
                fallback: "techmatesracing"
            )
        }

        func overlayOptionDefault(overlayId: String, optionKey: String, fallback: String) -> String {
            let overlay = overlayOptionDefaults.first { $0.key.caseInsensitiveCompare(overlayId) == .orderedSame }?.value
            let value = overlay?.first { $0.key.caseInsensitiveCompare(optionKey) == .orderedSame }?.value
            let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            return trimmed.isEmpty ? fallback : trimmed
        }

        func designV2ColorHex(_ key: String, fallback: String) -> String {
            let value = designV2Colors.first { $0.key.caseInsensitiveCompare(key) == .orderedSame }?.value ?? fallback
            return value.trimmingCharacters(in: .whitespacesAndNewlines)
        }

        func designV2Color(_ key: String, fallback: NSColor) -> NSColor {
            NSColor(tmrHex: designV2ColorHex(key, fallback: "")) ?? fallback
        }
    }

    private struct ContractFile: Decodable {
        var contractVersion: Int?
        var settings: Settings?
        var design: Design?
    }

    private struct Settings: Decodable {
        var settingsVersion: Int?
        var general: General?
        var overlays: [String: OverlayDefaults]?
    }

    private struct General: Decodable {
        var fontFamily: String?
        var unitSystem: String?
    }

    private struct OverlayDefaults: Decodable {
        var options: [String: String]?
    }

    private struct Design: Decodable {
        var v2: DesignV2?
    }

    private struct DesignV2: Decodable {
        var colors: [String: String]?
    }

    private static func load() -> Snapshot {
        for url in candidateContractURLs() {
            guard FileManager.default.fileExists(atPath: url.path),
                  let data = try? Data(contentsOf: url),
                  let contract = try? JSONDecoder().decode(ContractFile.self, from: data) else {
                continue
            }

            return snapshot(from: contract, loadedPath: url.path)
        }

        return fallbackSnapshot(loadedPath: nil)
    }

    private static func snapshot(from contract: ContractFile, loadedPath: String?) -> Snapshot {
        let fallback = fallbackSnapshot(loadedPath: loadedPath)
        var overlayDefaults: [String: [String: String]] = [:]
        for (overlayId, defaults) in contract.settings?.overlays ?? [:] {
            let options = defaults.options?.filter { !$0.value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty } ?? [:]
            if !options.isEmpty {
                overlayDefaults[overlayId] = options
            }
        }

        let colors = (contract.design?.v2?.colors ?? [:])
            .filter { NSColor(tmrHex: $0.value) != nil }

        return Snapshot(
            contractVersion: contract.contractVersion ?? fallback.contractVersion,
            settingsVersion: contract.settings?.settingsVersion ?? fallback.settingsVersion,
            defaultFontFamily: contract.settings?.general?.fontFamily?.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty
                ?? fallback.defaultFontFamily,
            defaultUnitSystem: contract.settings?.general?.unitSystem?.caseInsensitiveCompare("Imperial") == .orderedSame ? "Imperial" : "Metric",
            overlayOptionDefaults: overlayDefaults.isEmpty ? fallback.overlayOptionDefaults : overlayDefaults,
            designV2Colors: colors.isEmpty ? fallback.designV2Colors : colors,
            loadedPath: loadedPath
        )
    }

    private static func fallbackSnapshot(loadedPath: String?) -> Snapshot {
        Snapshot(
            contractVersion: 1,
            settingsVersion: 9,
            defaultFontFamily: "Segoe UI",
            defaultUnitSystem: "Metric",
            overlayOptionDefaults: [
                "stream-chat": [
                    streamChatProviderKey: "twitch",
                    streamChatTwitchChannelKey: "techmatesracing"
                ]
            ],
            designV2Colors: [
                "backgroundTop": "#12051F",
                "backgroundMid": "#0C122A",
                "backgroundBottom": "#030B18",
                "surface": "#090E20F2",
                "surfaceInset": "#0D152CE6",
                "surfaceRaised": "#121F3CEB",
                "titleBar": "#080A1CF8",
                "border": "#28486CD2",
                "borderMuted": "#20365496",
                "gridLine": "#00E8FF3D",
                "textPrimary": "#FFF7FF",
                "textSecondary": "#D0E6FF",
                "textMuted": "#8CAED4",
                "textDim": "#527094",
                "cyan": "#00E8FF",
                "magenta": "#FF2AA7",
                "amber": "#FFD15B",
                "green": "#62FF9F",
                "orange": "#FF7D49",
                "purple": "#7E32FF",
                "error": "#FF6274",
                "trackInterior": "#090E1296",
                "trackHalo": "#FFFFFF52",
                "trackLine": "#DEEAF5",
                "trackMarkerBorder": "#080E12E6",
                "pitLine": "#62C7FFBE",
                "startFinishBoundary": "#FFD15B",
                "startFinishBoundaryShadow": "#05090ED2",
                "personalBestSector": "#50D67C",
                "bestLapSector": "#B65CFF",
                "flagPole": "#D6DCE2E1",
                "flagPoleShadow": "#00000078"
            ],
            loadedPath: loadedPath
        )
    }

    static func defaultContractURL() -> URL? {
        candidateContractURLs().first { FileManager.default.fileExists(atPath: $0.path) }
    }

    static func defaultSchemaURL() -> URL? {
        guard let contractURL = defaultContractURL() else {
            return nil
        }

        let schemaURL = contractURL
            .deletingLastPathComponent()
            .appendingPathComponent(URL(fileURLWithPath: defaultSchemaRelativePath).lastPathComponent)
        return FileManager.default.fileExists(atPath: schemaURL.path) ? schemaURL : nil
    }

    private static func candidateContractURLs() -> [URL] {
        var urls: [URL] = []
        if let configured = ProcessInfo.processInfo.environment["TMR_SHARED_CONTRACT_PATH"],
           !configured.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            urls.append(URL(fileURLWithPath: configured))
        }

        if let resourceURL = Bundle.main.resourceURL {
            urls.append(resourceURL.appendingPathComponent(defaultContractRelativePath))
        }

        urls.append(contentsOf: parentCandidates(from: URL(fileURLWithPath: FileManager.default.currentDirectoryPath)))
        urls.append(contentsOf: parentCandidates(from: URL(fileURLWithPath: #filePath).deletingLastPathComponent()))
        return urls
    }

    private static func parentCandidates(from start: URL) -> [URL] {
        var candidates: [URL] = []
        var directory = start.standardizedFileURL
        for _ in 0..<12 {
            candidates.append(directory.appendingPathComponent(defaultContractRelativePath))
            let parent = directory.deletingLastPathComponent()
            if parent.path == directory.path {
                break
            }
            directory = parent
        }
        return candidates
    }
}

private extension String {
    var nilIfEmpty: String? {
        isEmpty ? nil : self
    }
}

private extension NSColor {
    convenience init?(tmrHex value: String) {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.hasPrefix("#") else {
            return nil
        }

        let hex = String(trimmed.dropFirst())
        guard (hex.count == 6 || hex.count == 8),
              let raw = UInt64(hex, radix: 16) else {
            return nil
        }

        let red: CGFloat
        let green: CGFloat
        let blue: CGFloat
        let alpha: CGFloat
        if hex.count == 6 {
            red = CGFloat((raw >> 16) & 0xff)
            green = CGFloat((raw >> 8) & 0xff)
            blue = CGFloat(raw & 0xff)
            alpha = 1
        } else {
            red = CGFloat((raw >> 24) & 0xff)
            green = CGFloat((raw >> 16) & 0xff)
            blue = CGFloat((raw >> 8) & 0xff)
            alpha = CGFloat(raw & 0xff) / 255.0
        }

        self.init(red255: red, green: green, blue: blue, alpha: alpha)
    }
}
