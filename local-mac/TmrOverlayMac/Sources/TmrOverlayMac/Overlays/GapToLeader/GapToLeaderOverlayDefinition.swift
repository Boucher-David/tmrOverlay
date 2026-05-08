import AppKit

enum GapToLeaderOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "gap-to-leader",
        displayName: "Gap To Leader",
        defaultSize: NSSize(width: 560, height: 260),
        showSessionFilters: false,
        fadeWhenLiveTelemetryUnavailable: true
    )
}
