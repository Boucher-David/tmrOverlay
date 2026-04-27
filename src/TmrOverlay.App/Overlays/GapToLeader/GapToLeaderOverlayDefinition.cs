using TmrOverlay.App.Overlays.Abstractions;

namespace TmrOverlay.App.Overlays.GapToLeader;

internal static class GapToLeaderOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "gap-to-leader",
        DisplayName: "Gap To Leader",
        DefaultWidth: 560,
        DefaultHeight: 260);
}
