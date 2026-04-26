using Microsoft.Extensions.Configuration;

namespace TmrOverlay.App.Retention;

internal sealed class RetentionOptions
{
    public bool Enabled { get; init; } = true;

    public int CaptureRetentionDays { get; init; } = 30;

    public int MaxCaptureDirectories { get; init; } = 50;

    public int DiagnosticsRetentionDays { get; init; } = 14;

    public int MaxDiagnosticsBundles { get; init; } = 20;

    public static RetentionOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Retention");

        return new RetentionOptions
        {
            Enabled = ParseBoolean(section["Enabled"], defaultValue: true),
            CaptureRetentionDays = ParseInt32(section["CaptureRetentionDays"], defaultValue: 30, minimumValue: 1),
            MaxCaptureDirectories = ParseInt32(section["MaxCaptureDirectories"], defaultValue: 50, minimumValue: 1),
            DiagnosticsRetentionDays = ParseInt32(section["DiagnosticsRetentionDays"], defaultValue: 14, minimumValue: 1),
            MaxDiagnosticsBundles = ParseInt32(section["MaxDiagnosticsBundles"], defaultValue: 20, minimumValue: 1)
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
