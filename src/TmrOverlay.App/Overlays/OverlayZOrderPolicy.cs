using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays;

internal static class OverlayZOrderPolicy
{
    public static bool ShouldSettingsWindowBeTopMost(bool settingsWindowFocused)
    {
        return settingsWindowFocused;
    }

    public static bool ShouldManagedOverlayBeTopMost(OverlaySettings settings)
    {
        return settings.AlwaysOnTop;
    }

    public static bool ShouldProtectSettingsWindowInput(bool settingsWindowVisible, bool isSettingsWindow)
    {
        return settingsWindowVisible && !isSettingsWindow;
    }

    public static bool ShouldOverlayBeInputTransparent(
        bool intrinsicallyTransparent,
        bool forceInputTransparent,
        bool settingsWindowVisible,
        bool isSettingsWindow)
    {
        return intrinsicallyTransparent
            || forceInputTransparent
            || ShouldProtectSettingsWindowInput(settingsWindowVisible, isSettingsWindow);
    }
}
