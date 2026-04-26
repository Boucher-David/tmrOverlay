using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Logging;

internal sealed class LocalFileLoggerOptions
{
    public bool Enabled { get; init; } = true;

    public required string ResolvedLogRoot { get; init; }

    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    public long MaxFileBytes { get; init; } = 2 * 1024 * 1024;

    public int RetainedFileCount { get; init; } = 10;

    public static LocalFileLoggerOptions FromConfiguration(
        IConfiguration configuration,
        AppStorageOptions storageOptions)
    {
        var section = configuration.GetSection("Logging:File");

        return new LocalFileLoggerOptions
        {
            Enabled = ParseBoolean(section["Enabled"], defaultValue: true),
            ResolvedLogRoot = storageOptions.LogsRoot,
            MinimumLevel = ParseLogLevel(section["MinimumLevel"], LogLevel.Information),
            MaxFileBytes = ParseInt64(section["MaxFileBytes"], defaultValue: 2 * 1024 * 1024, minimumValue: 64 * 1024),
            RetainedFileCount = ParseInt32(section["RetainedFileCount"], defaultValue: 10, minimumValue: 1)
        };
    }

    private static bool ParseBoolean(string? configuredValue, bool defaultValue)
    {
        return bool.TryParse(configuredValue, out var parsedValue) ? parsedValue : defaultValue;
    }

    private static LogLevel ParseLogLevel(string? configuredValue, LogLevel defaultValue)
    {
        return Enum.TryParse<LogLevel>(configuredValue, ignoreCase: true, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static int ParseInt32(string? configuredValue, int defaultValue, int minimumValue)
    {
        if (!int.TryParse(configuredValue, out var parsedValue))
        {
            return defaultValue;
        }

        return Math.Max(parsedValue, minimumValue);
    }

    private static long ParseInt64(string? configuredValue, long defaultValue, long minimumValue)
    {
        if (!long.TryParse(configuredValue, out var parsedValue))
        {
            return defaultValue;
        }

        return Math.Max(parsedValue, minimumValue);
    }
}
