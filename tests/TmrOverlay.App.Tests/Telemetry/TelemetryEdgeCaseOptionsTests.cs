using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Telemetry;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class TelemetryEdgeCaseOptionsTests
{
    [Fact]
    public void FromConfiguration_DefaultsToDisabled()
    {
        var options = TelemetryEdgeCaseOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.False(options.Enabled);
    }

    [Fact]
    public void FromConfiguration_ParsesTelemetryEdgeCaseSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TelemetryEdgeCases:Enabled"] = "false",
                ["TelemetryEdgeCases:PreTriggerSeconds"] = "12.5",
                ["TelemetryEdgeCases:PostTriggerSeconds"] = "6",
                ["TelemetryEdgeCases:MaxClipsPerSession"] = "7",
                ["TelemetryEdgeCases:MaxFramesPerClip"] = "80",
                ["TelemetryEdgeCases:MinimumFrameSpacingSeconds"] = "0.25"
            })
            .Build();

        var options = TelemetryEdgeCaseOptions.FromConfiguration(configuration);

        Assert.False(options.Enabled);
        Assert.Equal(12.5d, options.PreTriggerSeconds);
        Assert.Equal(6d, options.PostTriggerSeconds);
        Assert.Equal(7, options.MaxClipsPerSession);
        Assert.Equal(80, options.MaxFramesPerClip);
        Assert.Equal(0.25d, options.MinimumFrameSpacingSeconds);
    }

    [Fact]
    public void FromConfiguration_ClampsLowerBounds()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TelemetryEdgeCases:PreTriggerSeconds"] = "0",
                ["TelemetryEdgeCases:PostTriggerSeconds"] = "0",
                ["TelemetryEdgeCases:MaxClipsPerSession"] = "0",
                ["TelemetryEdgeCases:MaxFramesPerClip"] = "1",
                ["TelemetryEdgeCases:MinimumFrameSpacingSeconds"] = "0"
            })
            .Build();

        var options = TelemetryEdgeCaseOptions.FromConfiguration(configuration);

        Assert.Equal(1d, options.PreTriggerSeconds);
        Assert.Equal(1d, options.PostTriggerSeconds);
        Assert.Equal(1, options.MaxClipsPerSession);
        Assert.Equal(20, options.MaxFramesPerClip);
        Assert.Equal(0.02d, options.MinimumFrameSpacingSeconds);
    }
}
