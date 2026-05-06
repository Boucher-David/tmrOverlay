using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Telemetry;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class LiveModelParityOptionsTests
{
    [Fact]
    public void FromConfiguration_DefaultsToEnabled()
    {
        var options = LiveModelParityOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>()));

        Assert.True(options.Enabled);
    }

    [Fact]
    public void FromConfiguration_UsesSafeCustomNames()
    {
        var options = LiveModelParityOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>
        {
            ["LiveModelParity:Enabled"] = "true",
            ["LiveModelParity:PromotionCandidateMinimumFrames"] = "2500",
            ["LiveModelParity:PromotionCandidateMaxMismatchFrameRate"] = "0.01",
            ["LiveModelParity:PromotionCandidateMinimumCoverageRatio"] = "0.95",
            ["LiveModelParity:OutputFileName"] = "custom-parity.json",
            ["LiveModelParity:LogDirectoryName"] = "custom-parity"
        }));

        Assert.True(options.Enabled);
        Assert.Equal(2500, options.PromotionCandidateMinimumFrames);
        Assert.Equal(0.01d, options.PromotionCandidateMaxMismatchFrameRate);
        Assert.Equal(0.95d, options.PromotionCandidateMinimumCoverageRatio);
        Assert.Equal("custom-parity.json", options.OutputFileName);
        Assert.Equal("custom-parity", options.LogDirectoryName);
    }

    [Fact]
    public void FromConfiguration_ReplacesPathLikeNamesWithDefaults()
    {
        var options = LiveModelParityOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>
        {
            ["LiveModelParity:OutputFileName"] = "bad:name.json",
            ["LiveModelParity:LogDirectoryName"] = "../outside"
        }));

        Assert.Equal("live-model-parity.json", options.OutputFileName);
        Assert.Equal("model-parity", options.LogDirectoryName);
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
