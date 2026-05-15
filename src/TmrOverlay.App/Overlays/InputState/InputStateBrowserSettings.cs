using TmrOverlay.App.Overlays.Content;
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

    public static InputStateBrowserSettings From(
        ApplicationSettings settings,
        OverlaySessionKind? sessionKind = null)
    {
        var input = settings.Overlays.FirstOrDefault(
            overlay => string.Equals(overlay.Id, InputStateOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase));
        return new InputStateBrowserSettings(
            ShowThrottleTrace: ContentEnabled(input, OverlayOptionKeys.InputShowThrottleTrace, Default.ShowThrottleTrace, sessionKind),
            ShowBrakeTrace: ContentEnabled(input, OverlayOptionKeys.InputShowBrakeTrace, Default.ShowBrakeTrace, sessionKind),
            ShowClutchTrace: ContentEnabled(input, OverlayOptionKeys.InputShowClutchTrace, Default.ShowClutchTrace, sessionKind),
            ShowThrottle: ContentEnabled(input, OverlayOptionKeys.InputShowThrottle, Default.ShowThrottle, sessionKind),
            ShowBrake: ContentEnabled(input, OverlayOptionKeys.InputShowBrake, Default.ShowBrake, sessionKind),
            ShowClutch: ContentEnabled(input, OverlayOptionKeys.InputShowClutch, Default.ShowClutch, sessionKind),
            ShowSteering: ContentEnabled(input, OverlayOptionKeys.InputShowSteering, Default.ShowSteering, sessionKind),
            ShowGear: ContentEnabled(input, OverlayOptionKeys.InputShowGear, Default.ShowGear, sessionKind),
            ShowSpeed: ContentEnabled(input, OverlayOptionKeys.InputShowSpeed, Default.ShowSpeed, sessionKind));
    }

    private static bool ContentEnabled(
        OverlaySettings? input,
        string key,
        bool defaultEnabled,
        OverlaySessionKind? sessionKind)
    {
        return input is null
            ? defaultEnabled
            : OverlayContentColumnSettings.ContentEnabledForSession(input, key, defaultEnabled, sessionKind);
    }
}
