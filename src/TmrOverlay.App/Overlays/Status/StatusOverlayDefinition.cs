using TmrOverlay.App.Overlays.Abstractions;

namespace TmrOverlay.App.Overlays.Status;

internal static class StatusOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "status",
        DisplayName: "Collector Status",
        DefaultWidth: 520,
        DefaultHeight: 150);
}
