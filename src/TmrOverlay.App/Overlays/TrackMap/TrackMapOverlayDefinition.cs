using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.TrackMap;

internal static class TrackMapOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "track-map",
        DisplayName: "Track Map",
        DefaultWidth: 360,
        DefaultHeight: 360,
        FadeWhenLiveTelemetryUnavailable: true);
}
