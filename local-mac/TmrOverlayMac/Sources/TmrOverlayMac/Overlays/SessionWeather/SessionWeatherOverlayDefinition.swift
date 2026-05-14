import AppKit

enum SessionWeatherOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "session-weather",
        displayName: "Session / Weather",
        defaultSize: NSSize(width: 480, height: 520),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
