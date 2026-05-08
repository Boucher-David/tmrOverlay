import AppKit

enum StatusOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "status",
        displayName: "Collector Status",
        defaultSize: NSSize(width: 520, height: 150)
    )

    static var defaultSize: NSSize {
        definition.defaultSize
    }
}
