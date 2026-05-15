using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.InputState;

internal sealed record InputStateBrowserSettings(
    bool ShowThrottleTrace,
    bool ShowBrakeTrace,
    bool ShowClutchTrace,
    bool ShowThrottle,
    bool ShowBrake,
    bool ShowClutch,
    bool ShowSteering,
    bool ShowGear,
    bool ShowSpeed)
{
    public static InputStateBrowserSettings Default { get; } = new(
        ShowThrottleTrace: true,
        ShowBrakeTrace: true,
        ShowClutchTrace: true,
        ShowThrottle: true,
        ShowBrake: true,
        ShowClutch: true,
        ShowSteering: true,
        ShowGear: true,
        ShowSpeed: true);

    public static InputStateBrowserSettings From(ApplicationSettings settings)
    {
        var input = settings.Overlays.FirstOrDefault(
            overlay => string.Equals(overlay.Id, InputStateOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase));
        return new InputStateBrowserSettings(
            ShowThrottleTrace: input?.GetBooleanOption(OverlayOptionKeys.InputShowThrottleTrace, defaultValue: Default.ShowThrottleTrace) ?? Default.ShowThrottleTrace,
            ShowBrakeTrace: input?.GetBooleanOption(OverlayOptionKeys.InputShowBrakeTrace, defaultValue: Default.ShowBrakeTrace) ?? Default.ShowBrakeTrace,
            ShowClutchTrace: input?.GetBooleanOption(OverlayOptionKeys.InputShowClutchTrace, defaultValue: Default.ShowClutchTrace) ?? Default.ShowClutchTrace,
            ShowThrottle: input?.GetBooleanOption(OverlayOptionKeys.InputShowThrottle, defaultValue: Default.ShowThrottle) ?? Default.ShowThrottle,
            ShowBrake: input?.GetBooleanOption(OverlayOptionKeys.InputShowBrake, defaultValue: Default.ShowBrake) ?? Default.ShowBrake,
            ShowClutch: input?.GetBooleanOption(OverlayOptionKeys.InputShowClutch, defaultValue: Default.ShowClutch) ?? Default.ShowClutch,
            ShowSteering: input?.GetBooleanOption(OverlayOptionKeys.InputShowSteering, defaultValue: Default.ShowSteering) ?? Default.ShowSteering,
            ShowGear: input?.GetBooleanOption(OverlayOptionKeys.InputShowGear, defaultValue: Default.ShowGear) ?? Default.ShowGear,
            ShowSpeed: input?.GetBooleanOption(OverlayOptionKeys.InputShowSpeed, defaultValue: Default.ShowSpeed) ?? Default.ShowSpeed);
    }
}
