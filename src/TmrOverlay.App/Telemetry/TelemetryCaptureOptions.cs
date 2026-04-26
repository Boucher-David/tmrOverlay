using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureOptions
{
    public required string ResolvedCaptureRoot { get; init; }

    public bool RawCaptureEnabled { get; init; }

    public bool StoreSessionInfoSnapshots { get; init; } = true;

    public int QueueCapacity { get; init; } = 2048;

    public static TelemetryCaptureOptions FromConfiguration(
        IConfiguration configuration,
        AppStorageOptions storageOptions)
    {
        var section = configuration.GetSection("TelemetryCapture");

        return new TelemetryCaptureOptions
        {
            ResolvedCaptureRoot = storageOptions.CaptureRoot,
            RawCaptureEnabled = ParseBoolean(section["RawCaptureEnabled"], defaultValue: false),
            StoreSessionInfoSnapshots = ParseBoolean(section["StoreSessionInfoSnapshots"], defaultValue: true),
            QueueCapacity = ParseInt32(section["QueueCapacity"], defaultValue: 2048, minimumValue: 128)
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
}
