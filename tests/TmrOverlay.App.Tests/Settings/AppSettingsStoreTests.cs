using TmrOverlay.Core.Settings;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Overlays;
using Xunit;

namespace TmrOverlay.App.Tests.Settings;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void Save_PersistsOverlaySettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-settings-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var store = new AppSettingsStore(storage);
            var settings = store.Load();
            settings.General.FontFamily = "Verdana";
            settings.General.UnitSystem = "Imperial";
            var overlay = settings.GetOrAddOverlay("standings", 620, 340);
            overlay.X = 128;
            overlay.Y = 256;
            overlay.Opacity = 0.75;
            overlay.Scale = 1.25;
            overlay.ShowInQualifying = false;

            store.Save(settings);

            var reloaded = new AppSettingsStore(storage).Load();
            var persisted = Assert.Single(reloaded.Overlays);
            Assert.Equal(AppSettingsMigrator.CurrentVersion, reloaded.SettingsVersion);
            Assert.Equal("Verdana", reloaded.General.FontFamily);
            Assert.Equal("Imperial", reloaded.General.UnitSystem);
            Assert.Equal("standings", persisted.Id);
            Assert.Equal(128, persisted.X);
            Assert.Equal(256, persisted.Y);
            Assert.Equal(0.75, persisted.Opacity);
            Assert.Equal(1.25, persisted.Scale);
            Assert.False(persisted.ShowInQualifying);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_MigratesAndNormalizesLegacySettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-settings-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            Directory.CreateDirectory(storage.SettingsRoot);
            var settingsPath = Path.Combine(storage.SettingsRoot, "settings.json");
            File.WriteAllText(
                settingsPath,
                """
                {
                  "settingsVersion": 1,
                  "general": {
                    "fontFamily": "  ",
                    "unitSystem": "imperial"
                  },
                  "overlays": [
                    {
                      "id": "gap-to-leader",
                      "scale": 4.5,
                      "width": -10,
                      "height": -20,
                      "opacity": 1.5,
                      "classGapCarsAhead": -4,
                      "classGapCarsBehind": 99,
                      "options": {
                        "flags.green-seconds": "0",
                        "flags.blue-seconds": "12"
                      }
                    }
                  ]
                }
                """);

            var settings = new AppSettingsStore(storage).Load();
            var overlay = Assert.Single(settings.Overlays);
            Assert.Equal(AppSettingsMigrator.CurrentVersion, settings.SettingsVersion);
            Assert.Equal("Segoe UI", settings.General.FontFamily);
            Assert.Equal("Imperial", settings.General.UnitSystem);
            Assert.Equal("gap-to-leader", overlay.Id);
            Assert.Equal(2d, overlay.Scale);
            Assert.Equal(0, overlay.Width);
            Assert.Equal(0, overlay.Height);
            Assert.Equal(1d, overlay.Opacity);
            Assert.Equal(0, overlay.GetIntegerOption(OverlayOptionKeys.GapCarsAhead, 5, 0, 12));
            Assert.Equal(12, overlay.GetIntegerOption(OverlayOptionKeys.GapCarsBehind, 5, 0, 12));
            Assert.Equal(5, overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsEachSide, 5, 0, 8));
            Assert.Equal(5, overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsAhead, 5, 0, 8));
            Assert.Equal(5, overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsBehind, 5, 0, 8));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.StandingsClassSeparatorsEnabled, defaultValue: false));
            Assert.Equal(2, overlay.GetIntegerOption(OverlayOptionKeys.StandingsOtherClassRows, 2, 0, 6));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.FuelAdvice, defaultValue: true));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.ChromeHeaderStatusPractice, defaultValue: false));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.ChromeFooterSourcePractice, defaultValue: false));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, defaultValue: true));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: true));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.TrackMapSectorBoundariesEnabled, defaultValue: true));
            Assert.False(overlay.Options.ContainsKey("flags.green-seconds"));
            Assert.False(overlay.Options.ContainsKey("flags.blue-seconds"));

            var saved = File.ReadAllText(settingsPath);
            Assert.Contains($"\"settingsVersion\": {AppSettingsMigrator.CurrentVersion}", saved);
            Assert.DoesNotContain("flags.green-seconds", saved);
            Assert.DoesNotContain("flags.blue-seconds", saved);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_CompactsLegacyFullScreenFlagsOverlay()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-settings-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            Directory.CreateDirectory(storage.SettingsRoot);
            var settingsPath = Path.Combine(storage.SettingsRoot, "settings.json");
            File.WriteAllText(
                settingsPath,
                """
                {
                  "settingsVersion": 6,
                  "overlays": [
                    {
                      "id": "flags",
                      "enabled": true,
                      "scale": 1.7,
                      "x": 0,
                      "y": 0,
                      "width": 1920,
                      "height": 1440,
                      "screenId": "primary-screen-default"
                    }
                  ]
                }
                """);

            var settings = new AppSettingsStore(storage).Load();
            var overlay = Assert.Single(settings.Overlays);
            Assert.Equal("flags", overlay.Id);
            Assert.True(overlay.Enabled);
            Assert.Equal(1d, overlay.Scale);
            Assert.Equal(360, overlay.Width);
            Assert.Equal(170, overlay.Height);
            Assert.Equal("primary-screen-default", overlay.ScreenId);

            var saved = File.ReadAllText(settingsPath);
            Assert.Contains("\"width\": 360", saved);
            Assert.Contains("\"height\": 170", saved);
            Assert.DoesNotContain("\"width\": 1920", saved);
            Assert.DoesNotContain("\"height\": 1440", saved);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetOrAddOverlay_KeepsOverlaySettingsIndependentById()
    {
        var settings = new ApplicationSettings();

        var standings = settings.GetOrAddOverlay("standings", 620, 340);
        var fuel = settings.GetOrAddOverlay("fuel-calculator", 360, 180);
        standings.X = 64;
        fuel.X = 512;

        var reloadedStandings = settings.GetOrAddOverlay("standings", 100, 100);

        Assert.Equal(2, settings.Overlays.Count);
        Assert.Same(standings, reloadedStandings);
        Assert.Equal(64, standings.X);
        Assert.Equal(512, fuel.X);
        Assert.Equal(620, standings.Width);
        Assert.Equal(360, fuel.Width);
    }

    private static AppStorageOptions CreateStorage(string root)
    {
        return new AppStorageOptions
        {
            AppDataRoot = root,
            CaptureRoot = Path.Combine(root, "captures"),
            UserHistoryRoot = Path.Combine(root, "history", "user"),
            BaselineHistoryRoot = Path.Combine(root, "history", "baseline"),
            LogsRoot = Path.Combine(root, "logs"),
            SettingsRoot = Path.Combine(root, "settings"),
            DiagnosticsRoot = Path.Combine(root, "diagnostics"),
            TrackMapRoot = Path.Combine(root, "track-maps", "user"),
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }
}
