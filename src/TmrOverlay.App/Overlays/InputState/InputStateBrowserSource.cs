using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.InputState;

internal static class InputStateBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: InputStateOverlayDefinition.Definition.Id,
        title: InputStateOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/input-state",
        aliases: ["/overlays/inputs"],
        fadeWhenTelemetryUnavailable: InputStateOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        bodyClass: "input-state-page",
        moduleAssetName: "input-state");
}
