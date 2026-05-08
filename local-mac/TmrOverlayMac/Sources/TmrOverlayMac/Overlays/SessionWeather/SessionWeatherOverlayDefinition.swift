import AppKit

enum SessionWeatherOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "session-weather",
        displayName: "Session / Weather",
        defaultSize: NSSize(width: 420, height: 260),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
