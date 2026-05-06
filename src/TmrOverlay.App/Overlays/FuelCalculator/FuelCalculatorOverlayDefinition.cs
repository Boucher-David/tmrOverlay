using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.FuelCalculator;

internal static class FuelCalculatorOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "fuel-calculator",
        DisplayName: "Fuel Calculator",
        DefaultWidth: 600,
        DefaultHeight: 320,
        Options:
        [
            OverlaySettingsOptionDescriptor.Boolean(
                OverlayOptionKeys.FuelAdvice,
                "Show advice column",
                defaultValue: true),
            OverlaySettingsOptionDescriptor.Boolean(
                OverlayOptionKeys.FuelSource,
                "Show source row",
                defaultValue: true)
        ],
        FadeWhenLiveTelemetryUnavailable: true);
}
