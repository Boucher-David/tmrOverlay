using System.Text.Json;
using System.Text.Json.Serialization;

namespace TmrOverlay.Core.Settings;

internal sealed class OverlaySettings
{
    public required string Id { get; init; }

    public bool Enabled { get; set; }

    public double Scale { get; set; } = 1d;

    public int X { get; set; } = 24;

    public int Y { get; set; } = 24;

    public int Width { get; set; }

    public int Height { get; set; }

    public double Opacity { get; set; } = 0.88d;

    public bool AlwaysOnTop { get; set; } = true;

    public bool ShowInTest { get; set; } = true;

    public bool ShowInPractice { get; set; } = true;

    public bool ShowInQualifying { get; set; } = true;

    public bool ShowInRace { get; set; } = true;

    public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? ScreenId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? LegacyProperties { get; set; }

    public bool GetBooleanOption(string key, bool defaultValue)
    {
        return Options.TryGetValue(key, out var configured)
            && bool.TryParse(configured, out var parsed)
            ? parsed
            : defaultValue;
    }

    public void SetBooleanOption(string key, bool value)
    {
        Options[key] = value.ToString();
    }

    public int GetIntegerOption(string key, int defaultValue, int minimum, int maximum)
    {
        var value = Options.TryGetValue(key, out var configured)
            && int.TryParse(configured, out var parsed)
            ? parsed
            : defaultValue;
        return Math.Clamp(value, minimum, maximum);
    }

    public void SetIntegerOption(string key, int value, int minimum, int maximum)
    {
        Options[key] = Math.Clamp(value, minimum, maximum).ToString();
    }

    public string GetStringOption(string key, string defaultValue = "")
    {
        return Options.TryGetValue(key, out var configured)
            ? configured
            : defaultValue;
    }

    public void SetStringOption(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Options.Remove(key);
            return;
        }

        Options[key] = value.Trim();
    }
}
