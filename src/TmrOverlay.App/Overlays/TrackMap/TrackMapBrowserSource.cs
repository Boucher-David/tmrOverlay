using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.TrackMap;

internal static class TrackMapBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: TrackMapOverlayDefinition.Definition.Id,
        title: TrackMapOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/track-map",
        bodyClass: "track-map-page",
        renderWhenTelemetryUnavailable: true,
        fadeWhenTelemetryUnavailable: TrackMapOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        refreshIntervalMilliseconds: 100,
        moduleAssetName: "track-map");
}
