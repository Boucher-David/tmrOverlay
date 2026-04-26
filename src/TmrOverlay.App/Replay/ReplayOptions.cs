using Microsoft.Extensions.Configuration;

namespace TmrOverlay.App.Replay;

internal sealed class ReplayOptions
{
    public bool Enabled { get; init; }

    public string? CaptureDirectory { get; init; }

    public double SpeedMultiplier { get; init; } = 1d;

    public static ReplayOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Replay");

        return new ReplayOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            CaptureDirectory = string.IsNullOrWhiteSpace(section["CaptureDirectory"])
                ? null
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(section["CaptureDirectory"]!)),
            SpeedMultiplier = ParseDouble(section["SpeedMultiplier"], defaultValue: 1d, minimumValue: 0.1d)
        };
    }

    private static double ParseDouble(string? configuredValue, double defaultValue, double minimumValue)
    {
        if (!double.TryParse(configuredValue, out var parsedValue))
        {
            return defaultValue;
        }

        return Math.Max(parsedValue, minimumValue);
    }
}
