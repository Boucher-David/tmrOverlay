using Microsoft.Extensions.Configuration;

namespace TmrOverlay.App.Telemetry;

internal sealed class LiveModelParityOptions
{
    private static readonly char[] WindowsInvalidPathSegmentChars = ['<', '>', ':', '"', '|', '?', '*'];

    public bool Enabled { get; init; }

    public double MinimumFrameSpacingSeconds { get; init; } = 1d;

    public int MaxFramesPerSession { get; init; } = 600;

    public int MaxObservationsPerFrame { get; init; } = 20;

    public int MaxObservationSummaries { get; init; } = 200;

    public int PromotionCandidateMinimumFrames { get; init; } = 10_000;

    public double PromotionCandidateMaxMismatchFrameRate { get; init; } = 0.001d;

    public double PromotionCandidateMinimumCoverageRatio { get; init; } = 0.98d;

    public string OutputFileName { get; init; } = "live-model-parity.json";

    public string LogDirectoryName { get; init; } = "model-parity";

    public static LiveModelParityOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("LiveModelParity");
        return new LiveModelParityOptions
        {
            Enabled = ParseBoolean(section["Enabled"], defaultValue: true),
            MinimumFrameSpacingSeconds = ParseDouble(section["MinimumFrameSpacingSeconds"], defaultValue: 1d, minimumValue: 0.1d),
            MaxFramesPerSession = ParseInt32(section["MaxFramesPerSession"], defaultValue: 600, minimumValue: 10),
            MaxObservationsPerFrame = ParseInt32(section["MaxObservationsPerFrame"], defaultValue: 20, minimumValue: 1),
            MaxObservationSummaries = ParseInt32(section["MaxObservationSummaries"], defaultValue: 200, minimumValue: 10),
            PromotionCandidateMinimumFrames = ParseInt32(section["PromotionCandidateMinimumFrames"], defaultValue: 10_000, minimumValue: 1),
            PromotionCandidateMaxMismatchFrameRate = ParseRatio(section["PromotionCandidateMaxMismatchFrameRate"], defaultValue: 0.001d),
            PromotionCandidateMinimumCoverageRatio = ParseRatio(section["PromotionCandidateMinimumCoverageRatio"], defaultValue: 0.98d),
            OutputFileName = ParsePathSegment(section["OutputFileName"], defaultValue: "live-model-parity.json"),
            LogDirectoryName = ParsePathSegment(section["LogDirectoryName"], defaultValue: "model-parity")
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
        if (!double.TryParse(
                configuredValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedValue))
        {
            return defaultValue;
        }

        return Math.Max(parsedValue, minimumValue);
    }

    private static double ParseRatio(string? configuredValue, double defaultValue)
    {
        if (!double.TryParse(
                configuredValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedValue))
        {
            return defaultValue;
        }

        return Math.Clamp(parsedValue, 0d, 1d);
    }

    private static string ParsePathSegment(string? configuredValue, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return defaultValue;
        }

        var trimmed = configuredValue.Trim();
        if (trimmed is "." or ".."
            || trimmed.Contains('/')
            || trimmed.Contains('\\')
            || trimmed.IndexOfAny(WindowsInvalidPathSegmentChars) >= 0
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return defaultValue;
        }

        return trimmed;
    }
}
