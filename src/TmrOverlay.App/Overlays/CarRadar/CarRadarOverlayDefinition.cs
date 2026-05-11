using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.CarRadar;

internal static class CarRadarOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "car-radar",
        DisplayName: "Car Radar",
        DefaultWidth: 300,
        DefaultHeight: 300,
        Options:
        [
            OverlaySettingsOptionDescriptor.Boolean(
                OverlayOptionKeys.RadarMulticlassWarning,
                "Show multiclass warning",
                defaultValue: true)
        ],
        ShowOpacityControl: false,
        FadeWhenLiveTelemetryUnavailable: true,
        ContextRequirement: OverlayContextRequirement.LocalPlayerInCar);
}
