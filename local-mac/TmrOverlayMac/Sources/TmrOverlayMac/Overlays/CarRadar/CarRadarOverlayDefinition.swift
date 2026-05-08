import AppKit

enum CarRadarOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "car-radar",
        displayName: "Car Radar",
        defaultSize: NSSize(width: 300, height: 300),
        showOpacityControl: false,
        fadeWhenLiveTelemetryUnavailable: true
    )
}
