using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.History;

internal sealed class SessionHistoryOptions
{
    public bool Enabled { get; init; } = true;

    public bool UseBaselineHistory { get; init; }

    public required string ResolvedUserHistoryRoot { get; init; }

    public required string ResolvedBaselineHistoryRoot { get; init; }

    public string ResolvedHistoryRoot => ResolvedUserHistoryRoot;

    public static SessionHistoryOptions FromConfiguration(
        IConfiguration configuration,
        AppStorageOptions storageOptions)
    {
        var section = configuration.GetSection("SessionHistory");

        return new SessionHistoryOptions
        {
            Enabled = ParseBoolean(section["Enabled"], defaultValue: true),
            UseBaselineHistory = ParseBoolean(section["UseBaselineHistory"], defaultValue: false),
            ResolvedUserHistoryRoot = storageOptions.UserHistoryRoot,
            ResolvedBaselineHistoryRoot = storageOptions.BaselineHistoryRoot
        };
    }

    private static bool ParseBoolean(string? configuredValue, bool defaultValue)
    {
        return bool.TryParse(configuredValue, out var parsedValue) ? parsedValue : defaultValue;
    }
}
