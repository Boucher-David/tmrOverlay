import AppKit

enum FlagsOverlayDefinition {
    static let primaryScreenDefaultId = "primary-screen-default"
    static let minimumWidth = 180.0
    static let maximumWidth = 960.0
    static let minimumHeight = 96.0
    static let maximumHeight = 420.0

    static let definition = OverlayDefinition(
        id: "flags",
        displayName: "Flags",
        defaultSize: NSSize(width: 360, height: 170),
        showSessionFilters: false,
        showScaleControl: false,
        showOpacityControl: false,
        fadeWhenLiveTelemetryUnavailable: true
    )

    static func resolveSize(_ settings: OverlaySettings) -> NSSize {
        NSSize(
            width: min(max(settings.width > 0 ? settings.width : definition.defaultSize.width, minimumWidth), maximumWidth),
            height: min(max(settings.height > 0 ? settings.height : definition.defaultSize.height, minimumHeight), maximumHeight)
        )
    }
}
