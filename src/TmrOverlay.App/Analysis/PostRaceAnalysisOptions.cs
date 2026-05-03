using Microsoft.Extensions.Configuration;

namespace TmrOverlay.App.Analysis;

internal sealed class PostRaceAnalysisOptions
{
    public bool Enabled { get; init; }

    public static PostRaceAnalysisOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("PostRaceAnalysis");
        return new PostRaceAnalysisOptions
        {
            Enabled = ParseBoolean(section["Enabled"], defaultValue: false)
        };
    }

    private static bool ParseBoolean(string? configuredValue, bool defaultValue)
    {
        return bool.TryParse(configuredValue, out var parsedValue) ? parsedValue : defaultValue;
    }
}
