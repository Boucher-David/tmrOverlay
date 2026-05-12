using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.Core.Overlays;

internal static class OverlayChromeSettings
{
    private static readonly string[] ChromeOptionKeys =
    [
        OverlayOptionKeys.ChromeHeaderStatusTest,
        OverlayOptionKeys.ChromeHeaderStatusPractice,
        OverlayOptionKeys.ChromeHeaderStatusQualifying,
        OverlayOptionKeys.ChromeHeaderStatusRace,
        OverlayOptionKeys.ChromeHeaderTimeRemainingTest,
        OverlayOptionKeys.ChromeHeaderTimeRemainingPractice,
        OverlayOptionKeys.ChromeHeaderTimeRemainingQualifying,
        OverlayOptionKeys.ChromeHeaderTimeRemainingRace,
        OverlayOptionKeys.ChromeFooterSourceTest,
        OverlayOptionKeys.ChromeFooterSourcePractice,
        OverlayOptionKeys.ChromeFooterSourceQualifying,
        OverlayOptionKeys.ChromeFooterSourceRace
    ];

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

    public static bool ShowHeaderTimeRemaining(OverlaySettings settings, LiveTelemetrySnapshot snapshot)
    {
        return IsEnabledForSession(
            settings,
            OverlayAvailabilityEvaluator.CurrentSessionKind(snapshot),
            OverlayOptionKeys.ChromeHeaderTimeRemainingTest,
            OverlayOptionKeys.ChromeHeaderTimeRemainingPractice,
            OverlayOptionKeys.ChromeHeaderTimeRemainingQualifying,
            OverlayOptionKeys.ChromeHeaderTimeRemainingRace);
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

    public static string SettingsSignature(OverlaySettings settings)
    {
        return string.Join(
            "|",
            ChromeOptionKeys.Select(key => $"{key}:{settings.GetBooleanOption(key, defaultValue: true)}"));
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
