using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal static class SettingsOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "settings",
        DisplayName: "Settings",
        DefaultWidth: 600,
        DefaultHeight: 600);
}
