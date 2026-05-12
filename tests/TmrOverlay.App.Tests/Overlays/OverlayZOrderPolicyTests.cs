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
    public void SettingsWindowProtection_OnlyProtectsActiveIntersectingSettingsWindow()
    {
        Assert.False(OverlayZOrderPolicy.ShouldProtectSettingsWindowInput(
            settingsWindowActive: true,
            isSettingsWindow: true,
            intersectsSettingsWindow: true));
        Assert.True(OverlayZOrderPolicy.ShouldProtectSettingsWindowInput(
            settingsWindowActive: true,
            isSettingsWindow: false,
            intersectsSettingsWindow: true));
        Assert.False(OverlayZOrderPolicy.ShouldProtectSettingsWindowInput(
            settingsWindowActive: true,
            isSettingsWindow: false,
            intersectsSettingsWindow: false));
        Assert.False(OverlayZOrderPolicy.ShouldProtectSettingsWindowInput(
            settingsWindowActive: false,
            isSettingsWindow: false,
            intersectsSettingsWindow: true));
    }

    [Fact]
    public void InputTransparency_PreservesIntrinsicStreamChatClickThroughBehavior()
    {
        Assert.True(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: true,
            forceInputTransparent: false,
            settingsWindowActive: false,
            isSettingsWindow: false,
            intersectsSettingsWindow: false));
        Assert.True(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: false,
            forceInputTransparent: true,
            settingsWindowActive: false,
            isSettingsWindow: false,
            intersectsSettingsWindow: false));
        Assert.False(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: false,
            forceInputTransparent: false,
            settingsWindowActive: true,
            isSettingsWindow: true,
            intersectsSettingsWindow: true));
        Assert.False(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: false,
            forceInputTransparent: false,
            settingsWindowActive: true,
            isSettingsWindow: false,
            intersectsSettingsWindow: false));
        Assert.True(OverlayZOrderPolicy.ShouldOverlayBeInputTransparent(
            intrinsicallyTransparent: false,
            forceInputTransparent: false,
            settingsWindowActive: true,
            isSettingsWindow: false,
            intersectsSettingsWindow: true));
    }
}
