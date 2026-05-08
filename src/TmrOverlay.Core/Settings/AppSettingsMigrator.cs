using System.Text.Json;
using TmrOverlay.Core.Overlays;

namespace TmrOverlay.Core.Settings;

internal static class AppSettingsMigrator
{
    public const int CurrentVersion = 7;
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
            EnsureOption(overlay, OverlayOptionKeys.StatusCaptureDetails, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.StatusHealthDetails, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.FuelAdvice, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.FuelSource, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusTest, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusPractice, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusQualifying, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeHeaderStatusRace, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourceTest, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourcePractice, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourceQualifying, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.ChromeFooterSourceRace, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.RadarMulticlassWarning, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.StandingsClassSeparatorsEnabled, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.StandingsOtherClassRows, defaultValue: 2, minimum: 0, maximum: 6);
            EnsureOption(overlay, OverlayOptionKeys.StandingsColumnClassWidth, defaultValue: 54, minimum: 42, maximum: 110);
            EnsureOption(overlay, OverlayOptionKeys.StandingsColumnCarWidth, defaultValue: 66, minimum: 48, maximum: 130);
            EnsureOption(overlay, OverlayOptionKeys.StandingsColumnDriverWidth, defaultValue: 300, minimum: 180, maximum: 520);
            EnsureOption(overlay, OverlayOptionKeys.StandingsColumnGapWidth, defaultValue: 92, minimum: 64, maximum: 160);
            EnsureOption(overlay, OverlayOptionKeys.StandingsColumnIntervalWidth, defaultValue: 92, minimum: 64, maximum: 160);
            EnsureOption(overlay, OverlayOptionKeys.StandingsColumnPitWidth, defaultValue: 46, minimum: 34, maximum: 90);
            EnsureOption(overlay, OverlayOptionKeys.RelativeCarsAhead, defaultValue: 5, minimum: 0, maximum: 8);
            EnsureOption(overlay, OverlayOptionKeys.RelativeCarsBehind, defaultValue: 5, minimum: 0, maximum: 8);
            EnsureOption(overlay, OverlayOptionKeys.InputShowThrottle, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.InputShowBrake, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.InputShowClutch, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.InputShowSteering, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.InputShowGear, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.InputShowSpeed, defaultValue: true);
            EnsureOption(overlay, OverlayOptionKeys.GapCarsAhead, defaultValue: 5, minimum: 0, maximum: 12);
            EnsureOption(overlay, OverlayOptionKeys.GapCarsBehind, defaultValue: 5, minimum: 0, maximum: 12);
            EnsureOption(overlay, OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: true);

            overlay.Scale = ClampFinite(overlay.Scale, 0.6d, 2d, 1d);
            overlay.Width = Math.Max(0, overlay.Width);
            overlay.Height = Math.Max(0, overlay.Height);
            overlay.Opacity = ClampFinite(overlay.Opacity, 0.2d, 1d, 0.88d);
            NormalizeFlagsOverlay(overlay);
            overlay.LegacyProperties = null;
        }
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
