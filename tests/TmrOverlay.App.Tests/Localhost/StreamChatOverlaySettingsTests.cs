using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using Xunit;

namespace TmrOverlay.App.Tests.Localhost;

public sealed class StreamChatOverlaySettingsTests
{
    [Fact]
    public void From_UsesOnlySelectedStreamlabsProvider()
    {
        var settings = new ApplicationSettings();
        var overlay = settings.GetOrAddOverlay(
            StreamChatOverlayDefinition.Definition.Id,
            StreamChatOverlayDefinition.Definition.DefaultWidth,
            StreamChatOverlayDefinition.Definition.DefaultHeight);
        overlay.SetStringOption(OverlayOptionKeys.StreamChatProvider, StreamChatOverlaySettings.ProviderStreamlabs);
        overlay.SetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl, "https://streamlabs.com/widgets/chat-box/abc123");
        overlay.SetStringOption(OverlayOptionKeys.StreamChatTwitchChannel, "tmracing");

        var result = StreamChatOverlaySettings.From(settings);

        Assert.True(result.IsConfigured);
        Assert.Equal(StreamChatOverlaySettings.ProviderStreamlabs, result.Provider);
        Assert.Equal("https://streamlabs.com/widgets/chat-box/abc123", result.StreamlabsWidgetUrl);
        Assert.Null(result.TwitchChannel);
    }

    [Fact]
    public void From_UsesOnlySelectedTwitchProvider()
    {
        var settings = new ApplicationSettings();
        var overlay = settings.GetOrAddOverlay(
            StreamChatOverlayDefinition.Definition.Id,
            StreamChatOverlayDefinition.Definition.DefaultWidth,
            StreamChatOverlayDefinition.Definition.DefaultHeight);
        overlay.SetStringOption(OverlayOptionKeys.StreamChatProvider, StreamChatOverlaySettings.ProviderTwitch);
        overlay.SetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl, "https://streamlabs.com/widgets/chat-box/abc123");
        overlay.SetStringOption(OverlayOptionKeys.StreamChatTwitchChannel, "https://www.twitch.tv/TMRacing");

        var result = StreamChatOverlaySettings.From(settings);

        Assert.True(result.IsConfigured);
        Assert.Equal(StreamChatOverlaySettings.ProviderTwitch, result.Provider);
        Assert.Null(result.StreamlabsWidgetUrl);
        Assert.Equal("tmracing", result.TwitchChannel);
    }

    [Theory]
    [InlineData("http://streamlabs.com/widgets/chat-box/abc123")]
    [InlineData("https://example.com/widgets/chat-box/abc123")]
    [InlineData("https://streamlabs.com/widgets/alert-box/abc123")]
    public void NormalizeStreamlabsUrl_RejectsNonChatWidgetUrls(string url)
    {
        Assert.Null(StreamChatOverlaySettings.NormalizeStreamlabsUrl(url));
    }
}
