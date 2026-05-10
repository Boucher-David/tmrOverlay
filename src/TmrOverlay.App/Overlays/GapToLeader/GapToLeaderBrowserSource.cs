using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.GapToLeader;

internal static class GapToLeaderBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: GapToLeaderOverlayDefinition.Definition.Id,
        title: GapToLeaderOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/gap-to-leader",
        fadeWhenTelemetryUnavailable: GapToLeaderOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        moduleAssetName: "gap-to-leader");
}
