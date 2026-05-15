using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.PitService;

internal static class PitServiceOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "pit-service",
        DisplayName: "Pit Service",
        DefaultWidth: 420,
        DefaultHeight: 560,
        FadeWhenLiveTelemetryUnavailable: true,
        ContextRequirement: OverlayContextRequirement.LocalPlayerInCarOrPit);
}
