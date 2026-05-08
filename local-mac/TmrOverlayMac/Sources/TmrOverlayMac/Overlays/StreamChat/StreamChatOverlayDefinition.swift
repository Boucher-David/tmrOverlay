import AppKit

enum StreamChatOverlayDefinition {
    static let definition = OverlayDefinition(
        id: "stream-chat",
        displayName: "Stream Chat",
        defaultSize: NSSize(width: 380, height: 520),
        showSessionFilters: false,
        showScaleControl: false,
        showOpacityControl: false
    )
}
