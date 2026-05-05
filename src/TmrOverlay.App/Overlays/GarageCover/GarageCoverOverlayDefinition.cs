using System.Drawing;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.GarageCover;

internal static class GarageCoverOverlayDefinition
{
    public const int MinimumWidth = 320;
    public const int MinimumHeight = 180;
    public const int MaximumWidth = 7680;
    public const int MaximumHeight = 4320;

    public static OverlayDefinition Definition { get; } = new(
        Id: "garage-cover",
        DisplayName: "Garage Cover",
        DefaultWidth: 1280,
        DefaultHeight: 720,
        ShowScaleControl: false,
        ShowOpacityControl: false);

    public static Size ResolveSize(OverlaySettings settings)
    {
        return new Size(
            Math.Clamp(settings.Width, MinimumWidth, MaximumWidth),
            Math.Clamp(settings.Height, MinimumHeight, MaximumHeight));
    }
}
