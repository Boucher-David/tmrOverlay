import AppKit

enum DesignV2ComponentOverlayDefinition {
    static func definition(theme: DesignV2Theme) -> OverlayDefinition {
        OverlayDefinition(
            id: "design-v2-components-\(theme.id)",
            displayName: "Design V2 Components",
            defaultSize: theme.layout.componentGallerySize,
            showSessionFilters: false,
            showScaleControl: false,
            showOpacityControl: false
        )
    }
}
