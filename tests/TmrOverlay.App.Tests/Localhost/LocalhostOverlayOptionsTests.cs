using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Localhost;
using Xunit;

namespace TmrOverlay.App.Tests.Localhost;

public sealed class LocalhostOverlayOptionsTests
{
    [Fact]
    public void FromConfiguration_DefaultsToDisabledLocalhostOverlays()
    {
        var options = LocalhostOverlayOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>()));

        Assert.False(options.Enabled);
        Assert.Equal(8765, options.Port);
        Assert.Equal("http://localhost:8765/", options.Prefix);
    }

    [Fact]
    public void FromConfiguration_HonorsEnabledAndPort()
    {
        var options = LocalhostOverlayOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>
        {
            ["LocalhostOverlays:Enabled"] = "true",
            ["LocalhostOverlays:Port"] = "9123"
        }));

        Assert.True(options.Enabled);
        Assert.Equal(9123, options.Port);
        Assert.Equal("http://localhost:9123/", options.Prefix);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1023")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void FromConfiguration_InvalidPortFallsBackToDefault(string configuredPort)
    {
        var options = LocalhostOverlayOptions.FromConfiguration(BuildConfiguration(new Dictionary<string, string?>
        {
            ["LocalhostOverlays:Enabled"] = "true",
            ["LocalhostOverlays:Port"] = configuredPort
        }));

        Assert.True(options.Enabled);
        Assert.Equal(8765, options.Port);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
