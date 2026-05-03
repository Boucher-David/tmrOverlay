using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.Flags;

internal static class FlagsOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "flags",
        DisplayName: "Flags",
        DefaultWidth: 380,
        DefaultHeight: 210);
}
