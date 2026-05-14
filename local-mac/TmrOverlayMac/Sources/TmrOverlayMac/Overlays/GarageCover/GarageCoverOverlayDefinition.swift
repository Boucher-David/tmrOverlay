import AppKit

enum GarageCoverOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "garage-cover",
        displayName: "Garage Cover",
        defaultSize: NSSize(width: 1280, height: 720),
        showScaleControl: true,
        showOpacityControl: false
    )
}
