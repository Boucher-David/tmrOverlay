import AppKit

enum InputStateOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "input-state",
        displayName: "Inputs",
        defaultSize: NSSize(width: 520, height: 260),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
