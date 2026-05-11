namespace TmrOverlay.Core.Overlays;

internal enum OverlayContextRequirement
{
    AnyTelemetry,
    LocalPlayerInCar,
    LocalPlayerInCarOrPit
}

internal sealed record OverlayDefinition(
    string Id,
    string DisplayName,
    int DefaultWidth,
    int DefaultHeight,
    IReadOnlyList<OverlaySettingsOptionDescriptor>? Options = null,
    bool ShowSessionFilters = true,
    bool ShowScaleControl = true,
    bool ShowOpacityControl = true,
    bool FadeWhenLiveTelemetryUnavailable = false,
    OverlayContextRequirement ContextRequirement = OverlayContextRequirement.AnyTelemetry)
{
    public IReadOnlyList<OverlaySettingsOptionDescriptor> SettingsOptions { get; } = Options ?? [];
}
