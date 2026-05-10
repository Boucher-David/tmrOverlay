using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.CarRadar;

internal static class CarRadarBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: CarRadarOverlayDefinition.Definition.Id,
        title: CarRadarOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/car-radar",
        fadeWhenTelemetryUnavailable: CarRadarOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        bodyClass: "car-radar-page",
        moduleAssetName: "car-radar");
}
