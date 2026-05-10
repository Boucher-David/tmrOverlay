using System.Text.Json;
using TmrOverlay.Core.Overlays;

namespace TmrOverlay.Core.Settings;

internal static class AppSettingsMigrator
{
    public const int CurrentVersion = 8;
    private const string FlagsOverlayId = "flags";
    private const string FlagsPrimaryScreenDefaultId = "primary-screen-default";
    private const int FlagsDefaultWidth = 360;
    private const int FlagsDefaultHeight = 170;
    private const int FlagsMaximumWidth = 960;
    private const int FlagsMaximumHeight = 420;

    private static readonly string[] ObsoleteOptionKeys =
    [
        "flags.green-seconds",
        "flags.blue-seconds",
        "flags.yellow-seconds",
        "flags.critical-seconds",
        "flags.finish-seconds"
    ];

    private static readonly string[] KnownScopedOptionKeys =
    [
        OverlayOptionKeys.StatusCaptureDetails,
        OverlayOptionKeys.StatusHealthDetails,
        OverlayOptionKeys.FuelAdvice,
        OverlayOptionKeys.FuelSource,
        OverlayOptionKeys.ChromeHeaderStatusTest,
        OverlayOptionKeys.ChromeHeaderStatusPractice,
        OverlayOptionKeys.ChromeHeaderStatusQualifying,
        OverlayOptionKeys.ChromeHeaderStatusRace,
        OverlayOptionKeys.ChromeFooterSourceTest,
        OverlayOptionKeys.ChromeFooterSourcePractice,
        OverlayOptionKeys.ChromeFooterSourceQualifying,
        OverlayOptionKeys.ChromeFooterSourceRace,
        OverlayOptionKeys.RadarMulticlassWarning,
        OverlayOptionKeys.StandingsClassSeparatorsEnabled,
        OverlayOptionKeys.StandingsOtherClassRows,
        OverlayOptionKeys.StandingsColumnClassWidth,
        OverlayOptionKeys.StandingsColumnCarWidth,
        OverlayOptionKeys.StandingsColumnDriverWidth,
        OverlayOptionKeys.StandingsColumnGapWidth,
        OverlayOptionKeys.StandingsColumnIntervalWidth,
        OverlayOptionKeys.StandingsColumnPitWidth,
        OverlayOptionKeys.RelativeCarsEachSide,
        OverlayOptionKeys.RelativeCarsAhead,
        OverlayOptionKeys.RelativeCarsBehind,
        OverlayOptionKeys.InputShowThrottle,
        OverlayOptionKeys.InputShowBrake,
        OverlayOptionKeys.InputShowClutch,
        OverlayOptionKeys.InputShowSteering,
        OverlayOptionKeys.InputShowGear,
        OverlayOptionKeys.InputShowSpeed,
        OverlayOptionKeys.GapCarsAhead,
        OverlayOptionKeys.GapCarsBehind,
        OverlayOptionKeys.GapRaceOnlyDefaultApplied,
        OverlayOptionKeys.TrackMapBuildFromTelemetry,
        OverlayOptionKeys.TrackMapSectorBoundariesEnabled,
        OverlayOptionKeys.StreamChatProvider,
        OverlayOptionKeys.StreamChatStreamlabsUrl,
        OverlayOptionKeys.StreamChatTwitchChannel,
        OverlayOptionKeys.GarageCoverImagePath,
        OverlayOptionKeys.GarageCoverPreviewUntilUtc,
        OverlayOptionKeys.FlagsShowGreen,
        OverlayOptionKeys.FlagsShowBlue,
        OverlayOptionKeys.FlagsShowYellow,
        OverlayOptionKeys.FlagsShowCritical,
        OverlayOptionKeys.FlagsShowFinish
    ];

    public static ApplicationSettings Migrate(ApplicationSettings? settings)
    {
        settings ??= new ApplicationSettings();
        settings.General ??= new ApplicationGeneralSettings();
        settings.Overlays ??= [];

        NormalizeGeneral(settings.General);
        NormalizeOverlays(settings.Overlays);
        settings.SettingsVersion = CurrentVersion;
        return settings;
    }

    private static void NormalizeGeneral(ApplicationGeneralSettings general)
    {
        if (string.IsNullOrWhiteSpace(general.FontFamily))
        {
            general.FontFamily = "Segoe UI";
        }
        else
        {
            general.FontFamily = general.FontFamily.Trim();
        }

        general.UnitSystem = string.Equals(general.UnitSystem, "Imperial", StringComparison.OrdinalIgnoreCase)
            ? "Imperial"
            : "Metric";
    }

    private static void NormalizeOverlays(List<OverlaySettings> overlays)
    {
        overlays.RemoveAll(overlay => string.IsNullOrWhiteSpace(overlay.Id));

        foreach (var overlay in overlays)
        {
            overlay.Options = new Dictionary<string, string>(
                overlay.Options ?? [],
                StringComparer.OrdinalIgnoreCase);
            RemoveObsoleteOverlayOptions(overlay);
            MigrateLegacyOverlayOptions(overlay);
            RemoveIrrelevantOverlayOptions(overlay);
            EnsureScopedOverlayOptions(overlay);

            overlay.Scale = ClampFinite(overlay.Scale, 0.6d, 2d, 1d);
            overlay.Width = Math.Max(0, overlay.Width);
            overlay.Height = Math.Max(0, overlay.Height);
            overlay.Opacity = ClampFinite(overlay.Opacity, 0.2d, 1d, 0.88d);
            NormalizeFlagsOverlay(overlay);
            overlay.LegacyProperties = null;
        }
    }

    private static void RemoveIrrelevantOverlayOptions(OverlaySettings overlay)
    {
        foreach (var key in KnownScopedOptionKeys)
        {
            if (!OverlayOwnsOption(overlay.Id, key))
            {
                overlay.Options.Remove(key);
            }
        }
    }

    private static void EnsureScopedOverlayOptions(OverlaySettings overlay)
    {
        if (SupportsSharedChromeSettings(overlay.Id))
        {
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusTest, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusPractice, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusQualifying, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusRace, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourceTest, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourcePractice, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourceQualifying, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourceRace, defaultValue: true);
        }

        switch (overlay.Id.Trim().ToLowerInvariant())
        {
            case "fuel-calculator":
                EnsureOption(overlay, OverlayOptionKeys.FuelAdvice, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.FuelSource, defaultValue: true);
                break;
            case "car-radar":
                EnsureOption(overlay, OverlayOptionKeys.RadarMulticlassWarning, defaultValue: true);
                break;
            case "standings":
                EnsureOption(overlay, OverlayOptionKeys.StandingsClassSeparatorsEnabled, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.StandingsOtherClassRows, defaultValue: 2, minimum: 0, maximum: 6);
                EnsureOption(overlay, OverlayOptionKeys.StandingsColumnClassWidth, defaultValue: 54, minimum: 42, maximum: 110);
                EnsureOption(overlay, OverlayOptionKeys.StandingsColumnCarWidth, defaultValue: 66, minimum: 48, maximum: 130);
                EnsureOption(overlay, OverlayOptionKeys.StandingsColumnDriverWidth, defaultValue: 300, minimum: 180, maximum: 520);
                EnsureOption(overlay, OverlayOptionKeys.StandingsColumnGapWidth, defaultValue: 92, minimum: 64, maximum: 160);
                EnsureOption(overlay, OverlayOptionKeys.StandingsColumnIntervalWidth, defaultValue: 92, minimum: 64, maximum: 160);
                EnsureOption(overlay, OverlayOptionKeys.StandingsColumnPitWidth, defaultValue: 46, minimum: 34, maximum: 90);
                break;
            case "relative":
                NormalizeRelativeCarsEachSide(overlay);
                break;
            case "input-state":
                EnsureOption(overlay, OverlayOptionKeys.InputShowThrottle, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.InputShowBrake, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.InputShowClutch, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.InputShowSteering, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.InputShowGear, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.InputShowSpeed, defaultValue: true);
                break;
            case "gap-to-leader":
                EnsureOption(overlay, OverlayOptionKeys.GapCarsAhead, defaultValue: 5, minimum: 0, maximum: 12);
                EnsureOption(overlay, OverlayOptionKeys.GapCarsBehind, defaultValue: 5, minimum: 0, maximum: 12);
                break;
            case "track-map":
                EnsureOption(overlay, OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.TrackMapSectorBoundariesEnabled, defaultValue: true);
                break;
            case "flags":
                EnsureOption(overlay, OverlayOptionKeys.FlagsShowGreen, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.FlagsShowBlue, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.FlagsShowYellow, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.FlagsShowCritical, defaultValue: true);
                EnsureOption(overlay, OverlayOptionKeys.FlagsShowFinish, defaultValue: true);
                break;
        }
    }

    private static bool OverlayOwnsOption(string overlayId, string key)
    {
        if (SupportsSharedChromeSettings(overlayId) && IsSharedChromeOption(key))
        {
            return true;
        }

        return overlayId.Trim().ToLowerInvariant() switch
        {
            "fuel-calculator" => key is OverlayOptionKeys.FuelAdvice or OverlayOptionKeys.FuelSource,
            "car-radar" => key is OverlayOptionKeys.RadarMulticlassWarning,
            "standings" => key is
                OverlayOptionKeys.StandingsClassSeparatorsEnabled
                or OverlayOptionKeys.StandingsOtherClassRows
                or OverlayOptionKeys.StandingsColumnClassWidth
                or OverlayOptionKeys.StandingsColumnCarWidth
                or OverlayOptionKeys.StandingsColumnDriverWidth
                or OverlayOptionKeys.StandingsColumnGapWidth
                or OverlayOptionKeys.StandingsColumnIntervalWidth
                or OverlayOptionKeys.StandingsColumnPitWidth,
            "relative" => key is
                OverlayOptionKeys.RelativeCarsEachSide
                or OverlayOptionKeys.RelativeCarsAhead
                or OverlayOptionKeys.RelativeCarsBehind,
            "input-state" => key is
                OverlayOptionKeys.InputShowThrottle
                or OverlayOptionKeys.InputShowBrake
                or OverlayOptionKeys.InputShowClutch
                or OverlayOptionKeys.InputShowSteering
                or OverlayOptionKeys.InputShowGear
                or OverlayOptionKeys.InputShowSpeed,
            "gap-to-leader" => key is
                OverlayOptionKeys.GapCarsAhead
                or OverlayOptionKeys.GapCarsBehind
                or OverlayOptionKeys.GapRaceOnlyDefaultApplied,
            "track-map" => key is
                OverlayOptionKeys.TrackMapBuildFromTelemetry
                or OverlayOptionKeys.TrackMapSectorBoundariesEnabled,
            "stream-chat" => key is
                OverlayOptionKeys.StreamChatProvider
                or OverlayOptionKeys.StreamChatStreamlabsUrl
                or OverlayOptionKeys.StreamChatTwitchChannel,
            "garage-cover" => key is
                OverlayOptionKeys.GarageCoverImagePath
                or OverlayOptionKeys.GarageCoverPreviewUntilUtc,
            "flags" => key is
                OverlayOptionKeys.FlagsShowGreen
                or OverlayOptionKeys.FlagsShowBlue
                or OverlayOptionKeys.FlagsShowYellow
                or OverlayOptionKeys.FlagsShowCritical
                or OverlayOptionKeys.FlagsShowFinish,
            _ => false
        };
    }

    private static bool SupportsSharedChromeSettings(string overlayId)
    {
        return overlayId.Trim().ToLowerInvariant() is "standings" or "relative" or "fuel-calculator" or "gap-to-leader";
    }

    private static bool IsSharedChromeOption(string key)
    {
        return key is
            OverlayOptionKeys.ChromeHeaderStatusTest
            or OverlayOptionKeys.ChromeHeaderStatusPractice
            or OverlayOptionKeys.ChromeHeaderStatusQualifying
            or OverlayOptionKeys.ChromeHeaderStatusRace
            or OverlayOptionKeys.ChromeFooterSourceTest
            or OverlayOptionKeys.ChromeFooterSourcePractice
            or OverlayOptionKeys.ChromeFooterSourceQualifying
            or OverlayOptionKeys.ChromeFooterSourceRace;
    }

    private static void NormalizeFlagsOverlay(OverlaySettings overlay)
    {
        if (!string.Equals(overlay.Id, FlagsOverlayId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hadPrimaryScreenDefault = string.Equals(
            overlay.ScreenId,
            FlagsPrimaryScreenDefaultId,
            StringComparison.Ordinal);
        var hadFullScreenSize = overlay.Width > FlagsMaximumWidth
            || overlay.Height > FlagsMaximumHeight
            || (overlay.Width >= 900 && overlay.Height >= 500);
        if (!hadPrimaryScreenDefault && !hadFullScreenSize)
        {
            return;
        }

        overlay.Scale = 1d;
        overlay.Width = FlagsDefaultWidth;
        overlay.Height = FlagsDefaultHeight;
        overlay.ScreenId ??= FlagsPrimaryScreenDefaultId;
    }

    private static void NormalizeRelativeCarsEachSide(OverlaySettings overlay)
    {
        var carsEachSide = overlay.Options.ContainsKey(OverlayOptionKeys.RelativeCarsEachSide)
            ? overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsEachSide, defaultValue: 5, minimum: 0, maximum: 8)
            : Math.Max(
                overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsAhead, defaultValue: 5, minimum: 0, maximum: 8),
                overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsBehind, defaultValue: 5, minimum: 0, maximum: 8));
        overlay.SetIntegerOption(OverlayOptionKeys.RelativeCarsEachSide, carsEachSide, 0, 8);
        // Keep the old split keys normalized for pre-v0.17.0 browser/native readers.
        overlay.SetIntegerOption(OverlayOptionKeys.RelativeCarsAhead, carsEachSide, 0, 8);
        overlay.SetIntegerOption(OverlayOptionKeys.RelativeCarsBehind, carsEachSide, 0, 8);
    }

    private static void MigrateLegacyOverlayOptions(OverlaySettings overlay)
    {
        if (overlay.LegacyProperties is null || overlay.LegacyProperties.Count == 0)
        {
            return;
        }

        MigrateLegacyBoolean(overlay, "showStatusCaptureDetails", OverlayOptionKeys.StatusCaptureDetails);
        MigrateLegacyBoolean(overlay, "showStatusHealthDetails", OverlayOptionKeys.StatusHealthDetails);
        MigrateLegacyBoolean(overlay, "showFuelAdvice", OverlayOptionKeys.FuelAdvice);
        MigrateLegacyBoolean(overlay, "showFuelSource", OverlayOptionKeys.FuelSource);
        MigrateLegacyBoolean(overlay, "showRadarMulticlassWarning", OverlayOptionKeys.RadarMulticlassWarning);
        MigrateLegacyInteger(overlay, "classGapCarsAhead", OverlayOptionKeys.GapCarsAhead, 0, 12);
        MigrateLegacyInteger(overlay, "classGapCarsBehind", OverlayOptionKeys.GapCarsBehind, 0, 12);
    }

    private static void RemoveObsoleteOverlayOptions(OverlaySettings overlay)
    {
        foreach (var key in ObsoleteOptionKeys)
        {
            overlay.Options.Remove(key);
        }
    }

    private static void MigrateLegacyBoolean(OverlaySettings overlay, string legacyName, string optionKey)
    {
        if (overlay.Options.ContainsKey(optionKey)
            || overlay.LegacyProperties is null
            || !overlay.LegacyProperties.TryGetValue(legacyName, out var value)
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return;
        }

        overlay.SetBooleanOption(optionKey, value.GetBoolean());
    }

    private static void MigrateLegacyInteger(
        OverlaySettings overlay,
        string legacyName,
        string optionKey,
        int minimum,
        int maximum)
    {
        if (overlay.Options.ContainsKey(optionKey)
            || overlay.LegacyProperties is null
            || !overlay.LegacyProperties.TryGetValue(legacyName, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var parsed))
        {
            return;
        }

        overlay.SetIntegerOption(optionKey, parsed, minimum, maximum);
    }

    private static void EnsureOption(OverlaySettings overlay, string key, bool defaultValue)
    {
        overlay.SetBooleanOption(key, overlay.GetBooleanOption(key, defaultValue));
    }

    private static void EnsureOption(OverlaySettings overlay, string key, int defaultValue, int minimum, int maximum)
    {
        overlay.SetIntegerOption(key, overlay.GetIntegerOption(key, defaultValue, minimum, maximum), minimum, maximum);
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback)
    {
        return double.IsNaN(value) || double.IsInfinity(value)
            ? fallback
            : Math.Clamp(value, minimum, maximum);
    }
}
