using System.Drawing;
using TmrOverlay.App.Overlays.Content;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.BrowserSources;

internal static class BrowserOverlayRecommendedSize
{
    public static Size For(OverlayDefinition definition, OverlaySettings settings)
    {
        var baseSize = new Size(
            settings.Width > 0 ? settings.Width : definition.DefaultWidth,
            settings.Height > 0 ? settings.Height : definition.DefaultHeight);

        if (OverlayContentColumnSettings.TryGetContentDefinition(definition.Id, out var contentDefinition))
        {
            var contentWidth = OverlayContentColumnSettings.TotalVisibleWidth(
                settings,
                contentDefinition);
            return new Size(
                Math.Max(baseSize.Width, contentWidth + contentDefinition.BrowserWidthPadding),
                Math.Max(baseSize.Height, contentDefinition.BrowserMinimumHeight));
        }

        return baseSize;
    }
}
