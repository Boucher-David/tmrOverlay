import AppKit

enum RelativeOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "relative",
        displayName: "Relative",
        defaultSize: NSSize(width: 520, height: 360),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
