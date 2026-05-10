using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using Xunit;

namespace TmrOverlay.App.Tests.Settings;

public sealed class SharedOverlayContractTests
{
    [Fact]
    public void SharedContractFile_MatchesSettingsVersionAndStreamChatOptionKeys()
    {
        var path = FindSharedContractPath();
        var contract = SharedOverlayContract.Parse(File.ReadAllText(path));

        Assert.Equal(AppSettingsMigrator.CurrentVersion, contract.SettingsVersion);
        Assert.Equal("Segoe UI", contract.DefaultFontFamily);
        Assert.Equal("Metric", contract.DefaultUnitSystem);
        Assert.Equal(SharedOverlayContract.StreamChatProviderTwitch, contract.StreamChatDefaultProvider);
        Assert.Equal("techmatesracing", contract.StreamChatDefaultTwitchChannel);
        Assert.Equal(
            SharedOverlayContract.StreamChatProviderTwitch,
            contract.OverlayOptionDefault("stream-chat", OverlayOptionKeys.StreamChatProvider, "missing"));
        Assert.Equal(
            "techmatesracing",
            contract.OverlayOptionDefault("stream-chat", OverlayOptionKeys.StreamChatTwitchChannel, "missing"));
    }

    [Fact]
    public void SharedContractFile_DefinesDesignV2TokensUsedByNativeAndBrowser()
    {
        var path = FindSharedContractPath();
        var contract = SharedOverlayContract.Parse(File.ReadAllText(path));

        Assert.Equal("#00E8FF", contract.DesignV2Color("cyan", "missing"));
        Assert.Equal("#FF2AA7", contract.DesignV2Color("magenta", "missing"));
        Assert.Equal("#FFD15B", contract.DesignV2Color("amber", "missing"));
        Assert.Equal("#090E20F2", contract.DesignV2Color("surface", "missing"));
    }

    private static string FindSharedContractPath()
    {
        var path = SharedOverlayContract.TryFindDefaultContractPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        return path!;
    }
}
