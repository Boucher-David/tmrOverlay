using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Analysis;
using Xunit;

namespace TmrOverlay.App.Tests.Analysis;

public sealed class PostRaceAnalysisOptionsTests
{
    [Fact]
    public void FromConfiguration_DefaultsToEnabled()
    {
        var options = PostRaceAnalysisOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>()));

        Assert.True(options.Enabled);
    }

    [Fact]
    public void FromConfiguration_ParsesEnabled()
    {
        var options = PostRaceAnalysisOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>
        {
            ["PostRaceAnalysis:Enabled"] = "true"
        }));

        Assert.True(options.Enabled);
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
