import AppKit

enum PitServiceOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "pit-service",
        displayName: "Pit Service",
        defaultSize: NSSize(width: 420, height: 430),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
