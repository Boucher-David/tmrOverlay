import AppKit

struct OverlayDefinition {
    let id: String
    let displayName: String
    let defaultSize: NSSize
    let showSessionFilters: Bool
    let showScaleControl: Bool
    let showOpacityControl: Bool
    let fadeWhenLiveTelemetryUnavailable: Bool

    init(
        id: String,
        displayName: String,
        defaultSize: NSSize,
        showSessionFilters: Bool = true,
        showScaleControl: Bool = true,
        showOpacityControl: Bool = true,
        fadeWhenLiveTelemetryUnavailable: Bool = false
    ) {
        self.id = id
        self.displayName = displayName
        self.defaultSize = defaultSize
        self.showSessionFilters = showSessionFilters
        self.showScaleControl = showScaleControl
        self.showOpacityControl = showOpacityControl
        self.fadeWhenLiveTelemetryUnavailable = fadeWhenLiveTelemetryUnavailable
    }
}
