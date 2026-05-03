namespace TmrOverlay.Core.Overlays;

internal sealed record OverlayDefinition(
    string Id,
    string DisplayName,
    int DefaultWidth,
    int DefaultHeight,
    IReadOnlyList<OverlaySettingsOptionDescriptor>? Options = null,
    bool ShowSessionFilters = true,
    bool ShowScaleControl = true,
    bool ShowOpacityControl = true)
{
    public IReadOnlyList<OverlaySettingsOptionDescriptor> SettingsOptions { get; } = Options ?? [];
}
