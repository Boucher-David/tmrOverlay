using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.GarageCover;

internal static class GarageCoverBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: GarageCoverOverlayDefinition.Definition.Id,
        title: GarageCoverOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/garage-cover",
        bodyClass: "garage-cover-page",
        renderWhenTelemetryUnavailable: true,
        fadeWhenTelemetryUnavailable: false,
        refreshIntervalMilliseconds: 250,
        moduleAssetName: "garage-cover");
}
