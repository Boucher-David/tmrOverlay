using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.Core.Overlays;

internal static class OverlayChromeSettings
{
    public static bool ShowHeaderStatus(OverlaySettings settings, LiveTelemetrySnapshot snapshot)
    {
        return IsEnabledForSession(
            settings,
            OverlayAvailabilityEvaluator.CurrentSessionKind(snapshot),
            OverlayOptionKeys.ChromeHeaderStatusTest,
            OverlayOptionKeys.ChromeHeaderStatusPractice,
            OverlayOptionKeys.ChromeHeaderStatusQualifying,
            OverlayOptionKeys.ChromeHeaderStatusRace);
    }

    public static bool ShowFooterSource(OverlaySettings settings, LiveTelemetrySnapshot snapshot)
    {
        return IsEnabledForSession(
            settings,
            OverlayAvailabilityEvaluator.CurrentSessionKind(snapshot),
            OverlayOptionKeys.ChromeFooterSourceTest,
            OverlayOptionKeys.ChromeFooterSourcePractice,
            OverlayOptionKeys.ChromeFooterSourceQualifying,
            OverlayOptionKeys.ChromeFooterSourceRace);
    }

    private static bool IsEnabledForSession(
        OverlaySettings settings,
        OverlaySessionKind? sessionKind,
        string testKey,
        string practiceKey,
        string qualifyingKey,
        string raceKey)
    {
        return sessionKind switch
        {
            OverlaySessionKind.Test => settings.GetBooleanOption(testKey, defaultValue: true),
            OverlaySessionKind.Practice => settings.GetBooleanOption(practiceKey, defaultValue: true),
            OverlaySessionKind.Qualifying => settings.GetBooleanOption(qualifyingKey, defaultValue: true),
            OverlaySessionKind.Race => settings.GetBooleanOption(raceKey, defaultValue: true),
            _ => true
        };
    }
}
