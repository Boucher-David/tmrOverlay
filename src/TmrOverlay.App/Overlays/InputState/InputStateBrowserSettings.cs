using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.InputState;

internal sealed record InputStateBrowserSettings(
    bool ShowThrottle,
    bool ShowBrake,
    bool ShowClutch,
    bool ShowSteering,
    bool ShowGear,
    bool ShowSpeed)
{
    public static InputStateBrowserSettings Default { get; } = new(
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
            ShowThrottle: input?.GetBooleanOption(OverlayOptionKeys.InputShowThrottle, defaultValue: Default.ShowThrottle) ?? Default.ShowThrottle,
            ShowBrake: input?.GetBooleanOption(OverlayOptionKeys.InputShowBrake, defaultValue: Default.ShowBrake) ?? Default.ShowBrake,
            ShowClutch: input?.GetBooleanOption(OverlayOptionKeys.InputShowClutch, defaultValue: Default.ShowClutch) ?? Default.ShowClutch,
            ShowSteering: input?.GetBooleanOption(OverlayOptionKeys.InputShowSteering, defaultValue: Default.ShowSteering) ?? Default.ShowSteering,
            ShowGear: input?.GetBooleanOption(OverlayOptionKeys.InputShowGear, defaultValue: Default.ShowGear) ?? Default.ShowGear,
            ShowSpeed: input?.GetBooleanOption(OverlayOptionKeys.InputShowSpeed, defaultValue: Default.ShowSpeed) ?? Default.ShowSpeed);
    }
}
