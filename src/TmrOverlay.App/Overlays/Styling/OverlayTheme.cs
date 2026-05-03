using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TmrOverlay.App.Overlays.Styling;

/// <summary>
/// Human-editable visual tokens for the Windows overlays.
/// Keep common window chrome, text, and state colors here; graph/car-specific
/// palette decisions can stay next to the drawing code that explains them.
/// </summary>
internal static class OverlayTheme
{
    public static string DefaultFontFamily { get; private set; } = "Segoe UI";

    public static class Typography
    {
        public static readonly string[] PreferredFontFamilies =
        [
            "Segoe UI",
            "Inter",
            "Arial",
            "Calibri",
            "Helvetica Neue",
            "Tahoma",
            "Trebuchet MS",
            "Verdana",
            "Consolas",
            "Courier New",
            "Georgia",
            "Times New Roman"
        ];

        public const float OverlayTitleSize = 11f;
        public const float OverlayStatusSize = 9f;
        public const float OverlaySourceSize = 8.5f;
        public const float TableTextSize = 8.8f;
        public const float TableHeaderSize = 9.2f;
        public const float MiniLabelSize = 7f;
    }

    public static class Colors
    {
        public static Color WindowBackground { get; set; } = Color.FromArgb(14, 18, 21);
        public static Color SettingsBackground { get; set; } = Color.FromArgb(16, 20, 23);
        public static Color TitleBarBackground { get; set; } = Color.FromArgb(24, 30, 34);
        public static Color PanelBackground { get; set; } = Color.FromArgb(24, 30, 34);
        public static Color PageBackground { get; set; } = Color.FromArgb(20, 25, 29);
        public static Color ButtonBackground { get; set; } = Color.FromArgb(40, 48, 54);
        public static Color TabBackground { get; set; } = Color.FromArgb(24, 30, 34);
        public static Color TabSelectedBackground { get; set; } = Color.FromArgb(38, 48, 56);
        public static Color TabBorder { get; set; } = Color.FromArgb(64, 82, 92);
        public static Color WindowBorder { get; set; } = Color.FromArgb(72, 255, 255, 255);

        public static Color TextPrimary { get; set; } = Color.White;
        public static Color TextSecondary { get; set; } = Color.FromArgb(218, 226, 230);
        public static Color TextMuted { get; set; } = Color.FromArgb(128, 145, 153);
        public static Color TextSubtle { get; set; } = Color.FromArgb(160, 160, 160);
        public static Color TextControl { get; set; } = Color.FromArgb(220, 225, 230);

        public static Color NeutralBackground { get; set; } = Color.FromArgb(26, 26, 26);
        public static Color NeutralIndicator { get; set; } = Color.FromArgb(140, 140, 140);
        public static Color WarningBackground { get; set; } = Color.FromArgb(64, 46, 14);
        public static Color WarningStrongBackground { get; set; } = Color.FromArgb(54, 30, 14);
        public static Color WarningText { get; set; } = Color.FromArgb(246, 184, 88);
        public static Color WarningIndicator { get; set; } = Color.FromArgb(244, 180, 64);
        public static Color SuccessBackground { get; set; } = Color.FromArgb(14, 38, 28);
        public static Color SuccessStrongBackground { get; set; } = Color.FromArgb(18, 46, 34);
        public static Color SuccessText { get; set; } = Color.FromArgb(112, 224, 146);
        public static Color SuccessIndicator { get; set; } = Color.FromArgb(80, 214, 124);
        public static Color InfoBackground { get; set; } = Color.FromArgb(18, 30, 42);
        public static Color InfoText { get; set; } = Color.FromArgb(140, 190, 245);
        public static Color ErrorBackground { get; set; } = Color.FromArgb(70, 18, 24);
        public static Color ErrorGraphBackground { get; set; } = Color.FromArgb(42, 18, 22);
        public static Color ErrorText { get; set; } = Color.FromArgb(236, 112, 99);
        public static Color ErrorIndicator { get; set; } = Color.FromArgb(245, 88, 88);
    }

    public static class Layout
    {
        public const int OuterPadding = 14;
        public const int OverlayHeaderHeight = 34;
        public const int OverlayTableRowHeight = 28;
        public const int OverlayCompactRowHeight = 24;
        public const int OverlayBorderWidth = 1;
        public const int SettingsTitleBarHeight = 42;
        public const int SettingsTabTop = 54;
        public const int SettingsTabInset = 12;
        public const int LabelHeight = 24;
        public const int InputHeight = 28;
    }

    public static Font Font(string family, float size, FontStyle style = FontStyle.Regular)
    {
        return new Font(
            string.IsNullOrWhiteSpace(family) ? DefaultFontFamily : family,
            size,
            style,
            GraphicsUnit.Point);
    }

    public static Font MonospaceFont(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font(FontFamily.GenericMonospace, size, style, GraphicsUnit.Point);
    }

    public static void LoadOverrides(string themePath, ILogger logger)
    {
        if (!File.Exists(themePath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(themePath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.TryGetProperty("defaultFontFamily", out var fontElement)
                && fontElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(fontElement.GetString()))
            {
                DefaultFontFamily = fontElement.GetString()!.Trim();
            }

            if (root.TryGetProperty("colors", out var colorsElement)
                && colorsElement.ValueKind == JsonValueKind.Object)
            {
                ApplyColorOverrides(colorsElement, logger);
            }

            logger.LogInformation("Loaded overlay theme overrides from {ThemePath}.", themePath);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to load overlay theme overrides from {ThemePath}.", themePath);
        }
    }

    private static void ApplyColorOverrides(JsonElement colorsElement, ILogger logger)
    {
        var properties = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(Color))
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var configuredColor in colorsElement.EnumerateObject())
        {
            var propertyName = NormalizeThemeKey(configuredColor.Name);
            if (!properties.TryGetValue(propertyName, out var property)
                || configuredColor.Value.ValueKind != JsonValueKind.String
                || !TryParseColor(configuredColor.Value.GetString(), out var color))
            {
                logger.LogWarning("Ignoring invalid overlay theme color {ColorKey}.", configuredColor.Name);
                continue;
            }

            property.SetValue(null, color);
        }
    }

    private static string NormalizeThemeKey(string key)
    {
        return string.Concat(key
            .Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static bool TryParseColor(string? configured, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        var value = configured.Trim().TrimStart('#');
        if (value.Length == 6
            && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            color = Color.FromArgb(
                (rgb >> 16) & 0xff,
                (rgb >> 8) & 0xff,
                rgb & 0xff);
            return true;
        }

        if (value.Length == 8
            && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            color = Color.FromArgb(
                (argb >> 24) & 0xff,
                (argb >> 16) & 0xff,
                (argb >> 8) & 0xff,
                argb & 0xff);
            return true;
        }

        return false;
    }
}
