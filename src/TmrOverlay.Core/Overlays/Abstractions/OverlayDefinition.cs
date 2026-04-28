namespace TmrOverlay.Core.Overlays;

internal sealed record OverlayDefinition(
    string Id,
    string DisplayName,
    int DefaultWidth,
    int DefaultHeight,
    IReadOnlyList<OverlaySettingsOptionDescriptor>? Options = null)
{
    public IReadOnlyList<OverlaySettingsOptionDescriptor> SettingsOptions { get; } = Options ?? [];
}
