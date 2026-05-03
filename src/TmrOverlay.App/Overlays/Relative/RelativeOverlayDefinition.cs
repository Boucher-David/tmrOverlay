using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.Relative;

internal static class RelativeOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "relative",
        DisplayName: "Relative",
        DefaultWidth: 520,
        DefaultHeight: 360,
        Options:
        [
            OverlaySettingsOptionDescriptor.Integer(
                OverlayOptionKeys.RelativeCarsAhead,
                "Cars ahead",
                0,
                8,
                defaultValue: 5),
            OverlaySettingsOptionDescriptor.Integer(
                OverlayOptionKeys.RelativeCarsBehind,
                "Cars behind",
                0,
                8,
                defaultValue: 5)
        ]);
}
