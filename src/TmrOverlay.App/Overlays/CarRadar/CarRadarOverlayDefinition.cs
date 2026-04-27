using TmrOverlay.App.Overlays.Abstractions;

namespace TmrOverlay.App.Overlays.CarRadar;

internal static class CarRadarOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "car-radar",
        DisplayName: "Car Radar",
        DefaultWidth: 300,
        DefaultHeight: 300);
}
