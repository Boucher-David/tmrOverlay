using TmrOverlay.App.Overlays.Content;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal readonly record struct OverlaySettingsSessionColumn(
    OverlaySessionKind Kind,
    string Label,
    string ShortLabel);

internal static class OverlaySettingsSessionColumns
{
    public static readonly OverlaySettingsSessionColumn[] Display =
    [
        new(OverlaySessionKind.Practice, "Practice", "P"),
        new(OverlaySessionKind.Qualifying, "Qualifying", "Q"),
        new(OverlaySessionKind.Race, "Race", "R")
    ];

    public static readonly OverlaySettingsSessionColumn[] RaceOnly =
    [
        new(OverlaySessionKind.Race, "Race", "R")
    ];

    public static IReadOnlyList<OverlaySettingsSessionColumn> ChromeColumnsFor(string overlayId)
    {
        return string.Equals(overlayId, "gap-to-leader", StringComparison.OrdinalIgnoreCase)
            ? RaceOnly
            : Display;
    }

    public static bool ContentEnabledFor(
        OverlaySettings settings,
        string enabledOptionKey,
        bool defaultEnabled,
        OverlaySessionKind sessionKind)
    {
        return OverlayContentColumnSettings.ContentEnabledForSession(
            settings,
            enabledOptionKey,
            defaultEnabled,
            sessionKind);
    }

    public static void SetContentEnabledFor(
        OverlaySettings settings,
        string enabledOptionKey,
        OverlaySessionKind sessionKind,
        bool enabled)
    {
        settings.SetBooleanOption(OverlayContentColumnSettings.SessionEnabledOptionKey(enabledOptionKey, sessionKind), enabled);
        if (OverlayAvailabilityEvaluator.NormalizeSessionKind(sessionKind) == OverlaySessionKind.Practice)
        {
            settings.SetBooleanOption(OverlayContentColumnSettings.SessionEnabledOptionKey(enabledOptionKey, OverlaySessionKind.Test), enabled);
        }
    }

    public static bool ChromeEnabledFor(
        OverlaySettings settings,
        SettingsOverlayTabSections.OverlayChromeSettingsRow row,
        OverlaySessionKind sessionKind)
    {
        return settings.GetBooleanOption(ChromeKeyFor(row, sessionKind), defaultValue: true);
    }

    public static void SetChromeEnabledFor(
        OverlaySettings settings,
        SettingsOverlayTabSections.OverlayChromeSettingsRow row,
        OverlaySessionKind sessionKind,
        bool enabled)
    {
        settings.SetBooleanOption(ChromeKeyFor(row, sessionKind), enabled);
        if (OverlayAvailabilityEvaluator.NormalizeSessionKind(sessionKind) == OverlaySessionKind.Practice)
        {
            settings.SetBooleanOption(row.TestKey, enabled);
        }
    }

    private static string ChromeKeyFor(
        SettingsOverlayTabSections.OverlayChromeSettingsRow row,
        OverlaySessionKind sessionKind)
    {
        return OverlayAvailabilityEvaluator.NormalizeSessionKind(sessionKind) switch
        {
            OverlaySessionKind.Practice => row.PracticeKey,
            OverlaySessionKind.Qualifying => row.QualifyingKey,
            OverlaySessionKind.Race => row.RaceKey,
            _ => row.PracticeKey
        };
    }
}
