using TmrOverlay.App.Overlays.Abstractions;

namespace TmrOverlay.App.Overlays.FuelCalculator;

internal static class FuelCalculatorOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "fuel-calculator",
        DisplayName: "Fuel Calculator",
        DefaultWidth: 600,
        DefaultHeight: 320);
}
