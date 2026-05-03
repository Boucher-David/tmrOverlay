using System.Drawing;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.Flags;

internal static class FlagsOverlayDefinition
{
    public const string PrimaryScreenDefaultId = "primary-screen-default";
    public const int MinimumWidth = 640;
    public const int MaximumWidth = 7680;
    public const int MinimumHeight = 360;
    public const int MaximumHeight = 4320;

    private const double UltrawideAspectRatioThreshold = 2.0d;
    private const double UltrawideWidthToHeightRatio = 4d / 3d;

    public static OverlayDefinition Definition { get; } = new(
        Id: "flags",
        DisplayName: "Flags",
        DefaultWidth: 1920,
        DefaultHeight: 1080,
        ShowSessionFilters: false,
        ShowScaleControl: false,
        ShowOpacityControl: false);

    public static Size ResolveSize(OverlaySettings settings)
    {
        return new Size(
            Math.Clamp(settings.Width > 0 ? settings.Width : Definition.DefaultWidth, MinimumWidth, MaximumWidth),
            Math.Clamp(settings.Height > 0 ? settings.Height : Definition.DefaultHeight, MinimumHeight, MaximumHeight));
    }

    public static Size FitToScreen(Rectangle screenBounds)
    {
        var screenWidth = screenBounds.Width > 0 ? screenBounds.Width : Definition.DefaultWidth;
        var screenHeight = screenBounds.Height > 0 ? screenBounds.Height : Definition.DefaultHeight;
        var aspectRatio = screenWidth / (double)Math.Max(1, screenHeight);
        var targetWidth = screenWidth;
        var targetHeight = screenHeight;
        if (aspectRatio > UltrawideAspectRatioThreshold)
        {
            targetHeight = screenHeight;
            targetWidth = (int)Math.Round(targetHeight * UltrawideWidthToHeightRatio);
        }

        return new Size(
            Math.Clamp(targetWidth, MinimumWidth, MaximumWidth),
            Math.Clamp(targetHeight, MinimumHeight, MaximumHeight));
    }

    public static Rectangle DefaultFrameForScreen(Rectangle screenBounds)
    {
        var size = FitToScreen(screenBounds);
        var x = screenBounds.Left + Math.Max(0, (screenBounds.Width - size.Width) / 2);
        var y = screenBounds.Top + Math.Max(0, (screenBounds.Height - size.Height) / 2);
        return new Rectangle(x, y, size.Width, size.Height);
    }
}
