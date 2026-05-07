using System.Drawing;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.Flags;

internal static class FlagsOverlayDefinition
{
    public const string PrimaryScreenDefaultId = "primary-screen-default";
    public const int MinimumWidth = 180;
    public const int MaximumWidth = 960;
    public const int MinimumHeight = 96;
    public const int MaximumHeight = 420;

    public static OverlayDefinition Definition { get; } = new(
        Id: "flags",
        DisplayName: "Flags",
        DefaultWidth: 360,
        DefaultHeight: 170,
        ShowSessionFilters: false,
        ShowScaleControl: true,
        ShowOpacityControl: false,
        FadeWhenLiveTelemetryUnavailable: true);

    public static Size ResolveSize(OverlaySettings settings)
    {
        return new Size(
            Math.Clamp(settings.Width > 0 ? settings.Width : Definition.DefaultWidth, MinimumWidth, MaximumWidth),
            Math.Clamp(settings.Height > 0 ? settings.Height : Definition.DefaultHeight, MinimumHeight, MaximumHeight));
    }
}
