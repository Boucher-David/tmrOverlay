using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;

namespace TmrOverlay.App.Overlays.Styling;

internal static class OverlayClassColor
{
    private static readonly ConcurrentDictionary<string, Color> ParsedColors = new(StringComparer.OrdinalIgnoreCase);

    public static Color? TryParse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var key = NormalizeKey(hex);
        if (key is null)
        {
            return null;
        }

        return ParsedColors.GetOrAdd(key, static value =>
        {
            var rgb = int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromArgb((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        });
    }

    public static Color? TryParseWithAlpha(string? hex, int alpha)
    {
        return TryParse(hex) is { } color
            ? Color.FromArgb(Math.Clamp(alpha, 0, 255), color.R, color.G, color.B)
            : null;
    }

    public static Color Blend(Color panel, Color accent, int panelWeight, int accentWeight)
    {
        var total = Math.Max(1, panelWeight + accentWeight);
        return Color.FromArgb(
            (panel.R * panelWeight + accent.R * accentWeight) / total,
            (panel.G * panelWeight + accent.G * accentWeight) / total,
            (panel.B * panelWeight + accent.B * accentWeight) / total);
    }

    private static string? NormalizeKey(string value)
    {
        var key = value.Trim().TrimStart('#');
        if (key.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            key = key[2..];
        }

        return key.Length == 6 && key.All(Uri.IsHexDigit)
            ? key.ToUpperInvariant()
            : null;
    }
}
