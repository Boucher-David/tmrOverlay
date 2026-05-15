import AppKit

enum FuelCalculatorOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "fuel-calculator",
        displayName: "Fuel Calculator",
        defaultSize: NSSize(width: 600, height: 340),
        fadeWhenLiveTelemetryUnavailable: true
    )
}
