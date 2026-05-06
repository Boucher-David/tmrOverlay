using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.Standings;

internal static class StandingsOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "standings",
        DisplayName: "Standings",
        DefaultWidth: 620,
        DefaultHeight: 340,
        FadeWhenLiveTelemetryUnavailable: true);
}
