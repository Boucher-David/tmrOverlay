using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryEdgeCaseOptions
{
    public bool Enabled { get; init; }

    public double PreTriggerSeconds { get; init; } = 10d;

    public double PostTriggerSeconds { get; init; } = 5d;

    public int MaxClipsPerSession { get; init; } = 20;

    public int MaxFramesPerClip { get; init; } = 240;

    public double MinimumFrameSpacingSeconds { get; init; } = 0.1d;

    public static TelemetryEdgeCaseOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("TelemetryEdgeCases");

        return new TelemetryEdgeCaseOptions
        {
            Enabled = ParseBoolean(section["Enabled"], defaultValue: true),
            PreTriggerSeconds = ParseDouble(section["PreTriggerSeconds"], defaultValue: 10d, minimumValue: 1d),
            PostTriggerSeconds = ParseDouble(section["PostTriggerSeconds"], defaultValue: 5d, minimumValue: 1d),
            MaxClipsPerSession = ParseInt32(section["MaxClipsPerSession"], defaultValue: 20, minimumValue: 1),
            MaxFramesPerClip = ParseInt32(section["MaxFramesPerClip"], defaultValue: 240, minimumValue: 20),
            MinimumFrameSpacingSeconds = ParseDouble(section["MinimumFrameSpacingSeconds"], defaultValue: 0.1d, minimumValue: 0.02d)
        };
    }

    private static bool ParseBoolean(string? configuredValue, bool defaultValue)
    {
        return bool.TryParse(configuredValue, out var parsedValue) ? parsedValue : defaultValue;
    }

    private static int ParseInt32(string? configuredValue, int defaultValue, int minimumValue)
    {
        if (!int.TryParse(configuredValue, out var parsedValue))
        {
            return defaultValue;
        }

        return Math.Max(parsedValue, minimumValue);
    }

    private static double ParseDouble(string? configuredValue, double defaultValue, double minimumValue)
    {
        if (!double.TryParse(configuredValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue)
            || double.IsNaN(parsedValue)
            || double.IsInfinity(parsedValue))
        {
            return defaultValue;
        }

        return Math.Max(parsedValue, minimumValue);
    }
}
