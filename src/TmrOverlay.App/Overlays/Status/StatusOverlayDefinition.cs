using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.Status;

internal static class StatusOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "status",
        DisplayName: "Collector Status",
        DefaultWidth: 520,
        DefaultHeight: 176,
        Options:
        [
            OverlaySettingsOptionDescriptor.Boolean(
                OverlayOptionKeys.StatusCaptureDetails,
                "Show capture path",
                defaultValue: true),
            OverlaySettingsOptionDescriptor.Boolean(
                OverlayOptionKeys.StatusHealthDetails,
                "Show health details",
                defaultValue: true)
        ]);
}
