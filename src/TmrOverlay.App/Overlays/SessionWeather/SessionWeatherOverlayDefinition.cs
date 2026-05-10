using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.SessionWeather;

internal static class SessionWeatherOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "session-weather",
        DisplayName: "Session / Weather",
        DefaultWidth: 420,
        DefaultHeight: 330,
        FadeWhenLiveTelemetryUnavailable: true);
}
