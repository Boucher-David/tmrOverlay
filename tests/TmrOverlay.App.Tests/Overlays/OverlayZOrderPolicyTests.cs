using TmrOverlay.App.Overlays;
using TmrOverlay.Core.Settings;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class OverlayZOrderPolicyTests
{
    [Fact]
    public void SettingsWindow_IsTopMostOnlyWhileFocused()
    {
        Assert.True(OverlayZOrderPolicy.ShouldSettingsWindowBeTopMost(settingsWindowFocused: true));
        Assert.False(OverlayZOrderPolicy.ShouldSettingsWindowBeTopMost(settingsWindowFocused: false));
    }

    [Fact]
    public void ManagedOverlays_KeepTheirAlwaysOnTopLayerIndependentOfSettingsFocus()
    {
        Assert.True(OverlayZOrderPolicy.ShouldManagedOverlayBeTopMost(new OverlaySettings
        {
            Id = "standings",
            AlwaysOnTop = true
        }));
        Assert.False(OverlayZOrderPolicy.ShouldManagedOverlayBeTopMost(new OverlaySettings
        {
            Id = "standings",
            AlwaysOnTop = false
        }));
    }

    [Fact]
    public void SettingsWindowProtection_DoesNotMakeSettingsProtectItself()
    {
        Assert.False(OverlayZOrderPolicy.ShouldProtectSettingsWindowInput(
            settingsWindowVisible: true,
            isSettingsWindow: true));
        Assert.True(OverlayZOrderPolicy.ShouldProtectSettingsWindowInput(
            settingsWindowVisible: true,
            isSettingsWindow: false));
        Assert.False(OverlayZOrderPolicy.ShouldProtectSettingsWindowInput(
            settingsWindowVisible: false,
            isSettingsWindow: false));
    }

    [Fact]
    public void InputTransparency_PreservesIntrinsicStreamChatClickThroughBehavior()
    {
        Assert.True(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: true,
            forceInputTransparent: false,
            settingsWindowVisible: false,
            isSettingsWindow: false));
        Assert.True(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: false,
            forceInputTransparent: false,
            settingsWindowVisible: true,
            isSettingsWindow: false));
        Assert.False(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: false,
            forceInputTransparent: false,
            settingsWindowVisible: true,
            isSettingsWindow: true));
    }
}
