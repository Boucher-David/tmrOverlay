using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays;

internal static class OverlayZOrderPolicy
{
    public static bool ShouldSettingsWindowBeTopMost(bool settingsWindowFocused)
    {
        return settingsWindowFocused;
    }

    public static bool ShouldAutoHideSettingsWindow(
        bool settingsWindowVisible,
        bool settingsWindowActive,
        bool settingsWindowEnabled)
    {
        return settingsWindowVisible && !settingsWindowActive && settingsWindowEnabled;
    }

    public static bool ShouldManagedOverlayBeTopMost(OverlaySettings settings)
    {
        return settings.AlwaysOnTop;
    }

    public static bool ShouldProtectSettingsWindowInput(
        bool settingsWindowActive,
        bool isSettingsWindow,
        bool intersectsSettingsWindow)
    {
        return settingsWindowActive && !isSettingsWindow && intersectsSettingsWindow;
    }

    public static bool ShouldOverlayBeInputTransparent(
        bool intrinsicallyTransparent,
        bool forceInputTransparent,
        bool settingsWindowActive,
        bool isSettingsWindow,
        bool intersectsSettingsWindow)
    {
        return intrinsicallyTransparent
            || forceInputTransparent
            || ShouldProtectSettingsWindowInput(settingsWindowActive, isSettingsWindow, intersectsSettingsWindow);
    }
}
