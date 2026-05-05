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
    private const double DefaultScreenFraction = 0.5d;

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

    public static Rectangle DefaultFrameForScreen(Rectangle screenBounds)
    {
        var screenWidth = screenBounds.Width > 0 ? screenBounds.Width : Definition.DefaultWidth;
        var screenHeight = screenBounds.Height > 0 ? screenBounds.Height : Definition.DefaultHeight;
        var targetWidth = Math.Min(Definition.DefaultWidth, Math.Max(MinimumWidth, (int)Math.Round(screenWidth * DefaultScreenFraction)));
        var targetHeight = (int)Math.Round(targetWidth * 9d / 16d);
        if (targetHeight > screenHeight * DefaultScreenFraction)
        {
            targetHeight = Math.Min(Definition.DefaultHeight, Math.Max(MinimumHeight, (int)Math.Round(screenHeight * DefaultScreenFraction)));
            targetWidth = (int)Math.Round(targetHeight * 16d / 9d);
        }

        targetWidth = Math.Clamp(targetWidth, MinimumWidth, MaximumWidth);
        targetHeight = Math.Clamp(targetHeight, MinimumHeight, MaximumHeight);
        return new Rectangle(
            screenBounds.Left + Math.Max(0, (screenWidth - targetWidth) / 2),
            screenBounds.Top + Math.Max(0, (screenHeight - targetHeight) / 2),
            targetWidth,
            targetHeight);
    }
}
