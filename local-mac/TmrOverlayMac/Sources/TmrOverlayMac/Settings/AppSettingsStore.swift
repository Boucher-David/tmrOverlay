import Foundation

struct OverlaySettings: Codable {
    var id: String
    var enabled = false
    var scale = 1.0
    var x = 24.0
    var y = 24.0
    var width = 304.0
    var height = 92.0
    var opacity = 1.0
    var alwaysOnTop = true
    var showInTest = true
    var showInPractice = true
    var showInQualifying = true
    var showInRace = true
    var showStatusCaptureDetails = true
    var showStatusHealthDetails = true
    var showFuelAdvice = true
    var showRadarMulticlassWarning = true
    var relativeCarsAhead = 5
    var relativeCarsBehind = 5
    var classGapCarsAhead = 5
    var classGapCarsBehind = 5
    var gapRaceOnlyDefaultApplied = false
    var simpleTelemetrySourceFooter = false
    var flagsShowGreen = true
    var flagsShowBlue = true
    var flagsShowYellow = true
    var flagsShowCritical = true
    var flagsShowFinish = true
    var trackMapBuildFromTelemetry = true
    var streamChatProvider = StreamChatProviderOptions.defaultProvider
    var streamChatStreamlabsUrl = ""
    var streamChatTwitchChannel = StreamChatProviderOptions.defaultTwitchChannel
    var garageCoverImagePath = ""
    var garageCoverDefaultFrameApplied = false
    var options: [String: String] = [:]
    var screenId: String?

    enum CodingKeys: String, CodingKey {
        case id
        case enabled
        case scale
        case x
        case y
        case width
        case height
        case opacity
        case alwaysOnTop
        case showInTest
        case showInPractice
        case showInQualifying
        case showInRace
        case showStatusCaptureDetails
        case showStatusHealthDetails
        case showFuelAdvice
        case showRadarMulticlassWarning
        case relativeCarsAhead
        case relativeCarsBehind
        case classGapCarsAhead
        case classGapCarsBehind
        case gapRaceOnlyDefaultApplied
        case simpleTelemetrySourceFooter
        case flagsShowGreen
        case flagsShowBlue
        case flagsShowYellow
        case flagsShowCritical
        case flagsShowFinish
        case trackMapBuildFromTelemetry
        case streamChatProvider
        case streamChatStreamlabsUrl
        case streamChatTwitchChannel
        case garageCoverImagePath
        case garageCoverDefaultFrameApplied
        case options
        case screenId
    }

    init(
        id: String,
        enabled: Bool = false,
        scale: Double = 1.0,
        x: Double = 24.0,
        y: Double = 24.0,
        width: Double = 304.0,
        height: Double = 92.0,
        opacity: Double = 1.0,
        alwaysOnTop: Bool = true,
        showInTest: Bool = true,
        showInPractice: Bool = true,
        showInQualifying: Bool = true,
        showInRace: Bool = true,
        showStatusCaptureDetails: Bool = true,
        showStatusHealthDetails: Bool = true,
        showFuelAdvice: Bool = true,
        showRadarMulticlassWarning: Bool = true,
        relativeCarsAhead: Int = 5,
        relativeCarsBehind: Int = 5,
        classGapCarsAhead: Int = 5,
        classGapCarsBehind: Int = 5,
        gapRaceOnlyDefaultApplied: Bool = false,
        simpleTelemetrySourceFooter: Bool = false,
        flagsShowGreen: Bool = true,
        flagsShowBlue: Bool = true,
        flagsShowYellow: Bool = true,
        flagsShowCritical: Bool = true,
        flagsShowFinish: Bool = true,
        trackMapBuildFromTelemetry: Bool = true,
        streamChatProvider: String = StreamChatProviderOptions.defaultProvider,
        streamChatStreamlabsUrl: String = "",
        streamChatTwitchChannel: String = StreamChatProviderOptions.defaultTwitchChannel,
        garageCoverImagePath: String = "",
        garageCoverDefaultFrameApplied: Bool = false,
        options: [String: String] = [:],
        screenId: String? = nil
    ) {
        self.id = id
        self.enabled = enabled
        self.scale = scale
        self.x = x
        self.y = y
        self.width = width
        self.height = height
        self.opacity = opacity
        self.alwaysOnTop = alwaysOnTop
        self.showInTest = showInTest
        self.showInPractice = showInPractice
        self.showInQualifying = showInQualifying
        self.showInRace = showInRace
        self.showStatusCaptureDetails = showStatusCaptureDetails
        self.showStatusHealthDetails = showStatusHealthDetails
        self.showFuelAdvice = showFuelAdvice
        self.showRadarMulticlassWarning = showRadarMulticlassWarning
        self.relativeCarsAhead = relativeCarsAhead
        self.relativeCarsBehind = relativeCarsBehind
        self.classGapCarsAhead = classGapCarsAhead
        self.classGapCarsBehind = classGapCarsBehind
        self.gapRaceOnlyDefaultApplied = gapRaceOnlyDefaultApplied
        self.simpleTelemetrySourceFooter = simpleTelemetrySourceFooter
        self.flagsShowGreen = flagsShowGreen
        self.flagsShowBlue = flagsShowBlue
        self.flagsShowYellow = flagsShowYellow
        self.flagsShowCritical = flagsShowCritical
        self.flagsShowFinish = flagsShowFinish
        self.trackMapBuildFromTelemetry = trackMapBuildFromTelemetry
        self.streamChatProvider = streamChatProvider
        self.streamChatStreamlabsUrl = streamChatStreamlabsUrl
        self.streamChatTwitchChannel = streamChatTwitchChannel
        self.garageCoverImagePath = garageCoverImagePath
        self.garageCoverDefaultFrameApplied = garageCoverDefaultFrameApplied
        self.options = options
        self.screenId = screenId
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        enabled = try container.decodeIfPresent(Bool.self, forKey: .enabled) ?? false
        scale = try container.decodeIfPresent(Double.self, forKey: .scale) ?? 1.0
        x = try container.decodeIfPresent(Double.self, forKey: .x) ?? 24.0
        y = try container.decodeIfPresent(Double.self, forKey: .y) ?? 24.0
        width = try container.decodeIfPresent(Double.self, forKey: .width) ?? 304.0
        height = try container.decodeIfPresent(Double.self, forKey: .height) ?? 92.0
        opacity = try container.decodeIfPresent(Double.self, forKey: .opacity) ?? 1.0
        alwaysOnTop = try container.decodeIfPresent(Bool.self, forKey: .alwaysOnTop) ?? true
        showInTest = try container.decodeIfPresent(Bool.self, forKey: .showInTest) ?? true
        showInPractice = try container.decodeIfPresent(Bool.self, forKey: .showInPractice) ?? true
        showInQualifying = try container.decodeIfPresent(Bool.self, forKey: .showInQualifying) ?? true
        showInRace = try container.decodeIfPresent(Bool.self, forKey: .showInRace) ?? true
        showStatusCaptureDetails = try container.decodeIfPresent(Bool.self, forKey: .showStatusCaptureDetails) ?? true
        showStatusHealthDetails = try container.decodeIfPresent(Bool.self, forKey: .showStatusHealthDetails) ?? true
        showFuelAdvice = try container.decodeIfPresent(Bool.self, forKey: .showFuelAdvice) ?? true
        showRadarMulticlassWarning = try container.decodeIfPresent(Bool.self, forKey: .showRadarMulticlassWarning) ?? true
        relativeCarsAhead = try container.decodeIfPresent(Int.self, forKey: .relativeCarsAhead) ?? 5
        relativeCarsBehind = try container.decodeIfPresent(Int.self, forKey: .relativeCarsBehind) ?? 5
        classGapCarsAhead = try container.decodeIfPresent(Int.self, forKey: .classGapCarsAhead) ?? 5
        classGapCarsBehind = try container.decodeIfPresent(Int.self, forKey: .classGapCarsBehind) ?? 5
        gapRaceOnlyDefaultApplied = try container.decodeIfPresent(Bool.self, forKey: .gapRaceOnlyDefaultApplied) ?? false
        simpleTelemetrySourceFooter = try container.decodeIfPresent(Bool.self, forKey: .simpleTelemetrySourceFooter) ?? false
        flagsShowGreen = try container.decodeIfPresent(Bool.self, forKey: .flagsShowGreen) ?? true
        flagsShowBlue = try container.decodeIfPresent(Bool.self, forKey: .flagsShowBlue) ?? true
        flagsShowYellow = try container.decodeIfPresent(Bool.self, forKey: .flagsShowYellow) ?? true
        flagsShowCritical = try container.decodeIfPresent(Bool.self, forKey: .flagsShowCritical) ?? true
        flagsShowFinish = try container.decodeIfPresent(Bool.self, forKey: .flagsShowFinish) ?? true
        trackMapBuildFromTelemetry = try container.decodeIfPresent(Bool.self, forKey: .trackMapBuildFromTelemetry) ?? true
        streamChatProvider = try container.decodeIfPresent(String.self, forKey: .streamChatProvider) ?? StreamChatProviderOptions.defaultProvider
        streamChatStreamlabsUrl = try container.decodeIfPresent(String.self, forKey: .streamChatStreamlabsUrl) ?? ""
        streamChatTwitchChannel = try container.decodeIfPresent(String.self, forKey: .streamChatTwitchChannel) ?? StreamChatProviderOptions.defaultTwitchChannel
        garageCoverImagePath = try container.decodeIfPresent(String.self, forKey: .garageCoverImagePath) ?? ""
        garageCoverDefaultFrameApplied = try container.decodeIfPresent(Bool.self, forKey: .garageCoverDefaultFrameApplied) ?? false
        options = try container.decodeIfPresent([String: String].self, forKey: .options) ?? [:]
        screenId = try container.decodeIfPresent(String.self, forKey: .screenId)
    }
}

struct ApplicationGeneralSettings: Codable {
    var fontFamily = SharedOverlayContract.current.defaultFontFamily
    var unitSystem = SharedOverlayContract.current.defaultUnitSystem

    enum CodingKeys: String, CodingKey {
        case fontFamily
        case unitSystem
    }

    init(
        fontFamily: String = SharedOverlayContract.current.defaultFontFamily,
        unitSystem: String = SharedOverlayContract.current.defaultUnitSystem
    ) {
        self.fontFamily = fontFamily
        self.unitSystem = unitSystem
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        fontFamily = try container.decodeIfPresent(String.self, forKey: .fontFamily)
            ?? SharedOverlayContract.current.defaultFontFamily
        unitSystem = try container.decodeIfPresent(String.self, forKey: .unitSystem)
            ?? SharedOverlayContract.current.defaultUnitSystem
    }
}

struct ApplicationSettings: Codable {
    var settingsVersion = AppSettingsMigrator.currentVersion
    var general = ApplicationGeneralSettings()
    var overlays: [OverlaySettings] = []

    enum CodingKeys: String, CodingKey {
        case settingsVersion
        case general
        case overlays
    }

    init(
        settingsVersion: Int = AppSettingsMigrator.currentVersion,
        general: ApplicationGeneralSettings = ApplicationGeneralSettings(),
        overlays: [OverlaySettings] = []
    ) {
        self.settingsVersion = settingsVersion
        self.general = general
        self.overlays = overlays
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        settingsVersion = try container.decodeIfPresent(Int.self, forKey: .settingsVersion) ?? 1
        general = try container.decodeIfPresent(ApplicationGeneralSettings.self, forKey: .general) ?? ApplicationGeneralSettings()
        overlays = try container.decodeIfPresent([OverlaySettings].self, forKey: .overlays) ?? []
    }

    mutating func overlay(
        id: String,
        defaultSize: CGSize,
        defaultOrigin: CGPoint = CGPoint(x: 24, y: 24),
        defaultEnabled: Bool = false
    ) -> OverlaySettings {
        if let existing = overlays.first(where: { $0.id == id }) {
            return existing
        }

        let created = OverlaySettings(
            id: id,
            enabled: defaultEnabled,
            x: defaultOrigin.x,
            y: defaultOrigin.y,
            width: defaultSize.width,
            height: defaultSize.height
        )
        overlays.append(created)
        return created
    }

    mutating func updateOverlay(_ settings: OverlaySettings) {
        if let index = overlays.firstIndex(where: { $0.id == settings.id }) {
            overlays[index] = settings
        } else {
            overlays.append(settings)
        }
    }
}

enum AppSettingsMigrator {
    static let currentVersion = SharedOverlayContract.current.settingsVersion
    private static let legacyDefaultOpacity = 0.88
    private static let flagsOverlayId = "flags"
    private static let flagsPrimaryScreenDefaultId = "primary-screen-default"
    private static let flagsDefaultWidth = 360.0
    private static let flagsDefaultHeight = 170.0
    private static let flagsMaximumWidth = 960.0
    private static let flagsMaximumHeight = 420.0

    static func migrate(_ settings: ApplicationSettings) -> ApplicationSettings {
        var migrated = settings
        let sourceVersion = migrated.settingsVersion
        migrated.settingsVersion = currentVersion
        migrated.general = normalizedGeneral(migrated.general)
        migrated.overlays = migrated.overlays
            .filter { !$0.id.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .map { normalizedOverlay($0, sourceVersion: sourceVersion) }
        return migrated
    }

    private static func normalizedGeneral(_ general: ApplicationGeneralSettings) -> ApplicationGeneralSettings {
        let trimmedFont = general.fontFamily.trimmingCharacters(in: .whitespacesAndNewlines)
        return ApplicationGeneralSettings(
            fontFamily: trimmedFont.isEmpty ? SharedOverlayContract.current.defaultFontFamily : trimmedFont,
            unitSystem: general.unitSystem.caseInsensitiveCompare("Imperial") == .orderedSame ? "Imperial" : "Metric"
        )
    }

    private static func normalizedOverlay(_ overlay: OverlaySettings, sourceVersion: Int) -> OverlaySettings {
        var normalized = overlay
        normalized.scale = clampFinite(normalized.scale, minimum: 0.6, maximum: 2.0, fallback: 1.0)
        normalized.width = max(0, normalized.width)
        normalized.height = max(0, normalized.height)
        if sourceVersion < currentVersion && normalized.opacity.isFinite && abs(normalized.opacity - legacyDefaultOpacity) <= 0.0001 {
            normalized.opacity = 1.0
        }
        normalized.opacity = clampFinite(normalized.opacity, minimum: 0.2, maximum: 1.0, fallback: 1.0)
        let relativeCarsEachSide = min(max(max(normalized.relativeCarsAhead, normalized.relativeCarsBehind), 0), 8)
        normalized.relativeCarsAhead = relativeCarsEachSide
        normalized.relativeCarsBehind = relativeCarsEachSide
        normalized.classGapCarsAhead = min(max(normalized.classGapCarsAhead, 0), 12)
        normalized.classGapCarsBehind = min(max(normalized.classGapCarsBehind, 0), 12)
        normalizeStreamChatOptions(&normalized)
        normalizeFlagsOverlay(&normalized)
        return normalized
    }

    private static func normalizeStreamChatOptions(_ overlay: inout OverlaySettings) {
        let providerFromOptions = overlay.options[SharedOverlayContract.streamChatProviderKey]
        let providerSource = providerFromOptions ?? overlay.streamChatProvider
        let provider = providerSource.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? StreamChatProviderOptions.defaultProvider
            : StreamChatProviderOptions.normalize(providerSource)
        overlay.streamChatProvider = provider
        overlay.options[SharedOverlayContract.streamChatProviderKey] = provider

        if let streamlabsUrl = overlay.options[SharedOverlayContract.streamChatStreamlabsUrlKey] {
            overlay.streamChatStreamlabsUrl = streamlabsUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        } else {
            overlay.streamChatStreamlabsUrl = overlay.streamChatStreamlabsUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        }
        if !overlay.streamChatStreamlabsUrl.isEmpty {
            overlay.options[SharedOverlayContract.streamChatStreamlabsUrlKey] = overlay.streamChatStreamlabsUrl
        } else {
            overlay.options.removeValue(forKey: SharedOverlayContract.streamChatStreamlabsUrlKey)
        }

        let channelFromOptions = overlay.options[SharedOverlayContract.streamChatTwitchChannelKey]
        let channelSource = channelFromOptions ?? overlay.streamChatTwitchChannel
        let channel = normalizeTwitchChannel(channelSource) ?? StreamChatProviderOptions.defaultTwitchChannel
        overlay.streamChatTwitchChannel = channel
        overlay.options[SharedOverlayContract.streamChatTwitchChannelKey] = channel

        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowAuthorColorKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowBadgesKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowBitsKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowFirstMessageKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowRepliesKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowTimestampsKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowEmotesKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowAlertsKey, defaultValue: true)
        ensureBoolOption(&overlay, key: SharedOverlayContract.streamChatShowMessageIdsKey, defaultValue: false)
    }

    private static func ensureBoolOption(_ overlay: inout OverlaySettings, key: String, defaultValue: Bool) {
        let value = overlay.options[key]?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        overlay.options[key] = (value == "true" || value == "false")
            ? value
            : (defaultValue ? "true" : "false")
    }

    private static func normalizeTwitchChannel(_ value: String) -> String? {
        var normalized = value.trimmingCharacters(in: .whitespacesAndNewlines)
        if let url = URL(string: normalized),
           let host = url.host?.lowercased(),
           host == "twitch.tv" || host == "www.twitch.tv" {
            normalized = url.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        }

        normalized = normalized
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .trimmingCharacters(in: CharacterSet(charactersIn: "@"))
            .split(separator: "/")
            .first
            .map(String.init) ?? ""
        normalized = normalized.lowercased()
        guard (3...25).contains(normalized.count),
              normalized.allSatisfy({ $0.isLetter || $0.isNumber || $0 == "_" }) else {
            return nil
        }

        return normalized
    }

    private static func normalizeFlagsOverlay(_ overlay: inout OverlaySettings) {
        guard overlay.id.caseInsensitiveCompare(flagsOverlayId) == .orderedSame else {
            return
        }

        let hadPrimaryScreenDefault = overlay.screenId == flagsPrimaryScreenDefaultId
        let hadFullScreenSize = overlay.width > flagsMaximumWidth
            || overlay.height > flagsMaximumHeight
            || (overlay.width >= 900 && overlay.height >= 500)
        guard hadPrimaryScreenDefault || hadFullScreenSize else {
            return
        }

        overlay.scale = 1
        overlay.width = flagsDefaultWidth
        overlay.height = flagsDefaultHeight
        if overlay.screenId == nil {
            overlay.screenId = flagsPrimaryScreenDefaultId
        }
    }

    private static func clampFinite(_ value: Double, minimum: Double, maximum: Double, fallback: Double) -> Double {
        guard value.isFinite else {
            return fallback
        }

        return min(max(value, minimum), maximum)
    }
}

final class AppSettingsStore {
    private let settingsURL: URL

    init(settingsRoot: URL) {
        settingsURL = settingsRoot.appendingPathComponent("settings.json")
    }

    func load() -> ApplicationSettings {
        let loaded: ApplicationSettings
        if let data = try? Data(contentsOf: settingsURL),
           let settings = try? JSONDecoder().decode(ApplicationSettings.self, from: data) {
            loaded = settings
        } else {
            loaded = ApplicationSettings()
        }

        let migrated = AppSettingsMigrator.migrate(loaded)
        save(migrated)
        return migrated
    }

    func save(_ settings: ApplicationSettings) {
        do {
            try FileManager.default.createDirectory(at: settingsURL.deletingLastPathComponent(), withIntermediateDirectories: true)
            let encoder = JSONEncoder()
            encoder.outputFormatting = [.prettyPrinted]
            try encoder.encode(AppSettingsMigrator.migrate(settings)).write(to: settingsURL)
        } catch {
            NSLog("Failed to save settings: \(error)")
        }
    }
}
