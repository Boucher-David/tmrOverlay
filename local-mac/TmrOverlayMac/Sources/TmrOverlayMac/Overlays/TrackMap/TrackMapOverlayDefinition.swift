import AppKit

enum TrackMapOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "track-map",
        displayName: "Track Map",
        defaultSize: NSSize(width: 360, height: 360),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
