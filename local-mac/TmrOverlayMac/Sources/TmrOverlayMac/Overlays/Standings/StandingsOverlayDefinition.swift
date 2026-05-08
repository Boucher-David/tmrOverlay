import AppKit

enum StandingsOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "standings",
        displayName: "Standings",
        defaultSize: NSSize(width: 620, height: 340),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
