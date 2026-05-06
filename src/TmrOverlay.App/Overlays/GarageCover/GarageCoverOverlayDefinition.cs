using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.GarageCover;

internal static class GarageCoverOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "garage-cover",
        DisplayName: "Garage Cover",
        DefaultWidth: 1280,
        DefaultHeight: 720,
        ShowScaleControl: false,
        ShowOpacityControl: false);
}
