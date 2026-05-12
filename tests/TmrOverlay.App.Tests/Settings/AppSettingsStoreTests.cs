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
            Assert.True(overlay.Options.ContainsKey(OverlayOptionKeys.GapCarsAhead));
            Assert.True(overlay.Options.ContainsKey(OverlayOptionKeys.GapCarsBehind));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.ChromeHeaderStatusPractice, defaultValue: false));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.ChromeFooterSourcePractice, defaultValue: false));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.RelativeCarsEachSide));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.RelativeCarsAhead));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.RelativeCarsBehind));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.StandingsClassSeparatorsEnabled));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.StandingsOtherClassRows));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.FuelAdvice));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.RadarMulticlassWarning));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.TrackMapBuildFromTelemetry));
            Assert.False(overlay.Options.ContainsKey(OverlayOptionKeys.TrackMapSectorBoundariesEnabled));
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
    public void Load_RemovesKnownOptionsThatBelongToOtherOverlays()
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
                  "settingsVersion": 8,
                  "overlays": [
                    {
                      "id": "relative",
                      "options": {
                        "relative.cars-each-side": "3",
                        "fuel.advice": "false",
                        "standings.other-class-rows": "6"
                      }
                    },
                    {
                      "id": "fuel-calculator",
                      "options": {
                        "fuel.advice": "false",
                        "relative.cars-each-side": "7",
                        "track-map.build-from-telemetry": "false"
                      }
                    }
                  ]
                }
                """);

            var settings = new AppSettingsStore(storage).Load();
            var relative = settings.Overlays.Single(overlay => overlay.Id == "relative");
            var fuel = settings.Overlays.Single(overlay => overlay.Id == "fuel-calculator");

            Assert.Equal(3, relative.GetIntegerOption(OverlayOptionKeys.RelativeCarsEachSide, 5, 0, 8));
            Assert.False(relative.Options.ContainsKey(OverlayOptionKeys.FuelAdvice));
            Assert.False(relative.Options.ContainsKey(OverlayOptionKeys.StandingsOtherClassRows));
            Assert.False(fuel.GetBooleanOption(OverlayOptionKeys.FuelAdvice, defaultValue: true));
            Assert.False(fuel.Options.ContainsKey(OverlayOptionKeys.RelativeCarsEachSide));
            Assert.False(fuel.Options.ContainsKey(OverlayOptionKeys.TrackMapBuildFromTelemetry));
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
    public void Load_CanonicalizesStreamChatSharedDefaultsIntoOptions()
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
                  "settingsVersion": 8,
                  "overlays": [
                    {
                      "id": "stream-chat",
                      "options": {}
                    }
                  ]
                }
                """);

            var settings = new AppSettingsStore(storage).Load();
            var overlay = Assert.Single(settings.Overlays);

            Assert.Equal("stream-chat", overlay.Id);
            Assert.Equal("twitch", overlay.GetStringOption(OverlayOptionKeys.StreamChatProvider));
            Assert.Equal("techmatesracing", overlay.GetStringOption(OverlayOptionKeys.StreamChatTwitchChannel));
            Assert.True(overlay.Options.ContainsKey(OverlayOptionKeys.StreamChatProvider));
            Assert.True(overlay.Options.ContainsKey(OverlayOptionKeys.StreamChatTwitchChannel));

            var saved = File.ReadAllText(settingsPath);
            Assert.Contains("\"stream-chat.provider\": \"twitch\"", saved);
            Assert.Contains("\"stream-chat.twitch-channel\": \"techmatesracing\"", saved);
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
    public void Load_PreservesExistingUserStateDuringMigration()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-settings-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            Directory.CreateDirectory(storage.SettingsRoot);
            var settingsPath = Path.Combine(storage.SettingsRoot, "settings.json");
            var garageCoverPath = Path.Combine(storage.AppDataRoot, "garage-cover", "cover.png");
            File.WriteAllText(
                settingsPath,
                $$"""
                {
                  "settingsVersion": 8,
                  "general": {
                    "fontFamily": "Verdana",
                    "unitSystem": "Imperial"
                  },
                  "overlays": [
                    {
                      "id": "standings",
                      "enabled": false,
                      "x": 144,
                      "y": 288,
                      "width": 780,
                      "height": 520,
                      "opacity": 0.72,
                      "alwaysOnTop": false,
                      "showInTest": false,
                      "showInPractice": false,
                      "showInQualifying": false,
                      "showInRace": true,
                      "screenId": "screen-2",
                      "options": {
                        "chrome.header.status.race": "false",
                        "chrome.footer.source.race": "false",
                        "standings.other-class-rows": "0"
                      }
                    },
                    {
                      "id": "track-map",
                      "enabled": true,
                      "options": {
                        "track-map.build-from-telemetry": "true",
                        "track-map.sector-boundaries.enabled": "false"
                      }
                    },
                    {
                      "id": "garage-cover",
                      "enabled": true,
                      "options": {
                        "garage-cover.image-path": "{{garageCoverPath.Replace("\\", "\\\\")}}"
                      }
                    }
                  ]
                }
                """);

            var settings = new AppSettingsStore(storage).Load();

            Assert.Equal(AppSettingsMigrator.CurrentVersion, settings.SettingsVersion);
            Assert.Equal("Verdana", settings.General.FontFamily);
            Assert.Equal("Imperial", settings.General.UnitSystem);

            var standings = settings.Overlays.Single(overlay => overlay.Id == "standings");
            Assert.False(standings.Enabled);
            Assert.Equal(144, standings.X);
            Assert.Equal(288, standings.Y);
            Assert.Equal(780, standings.Width);
            Assert.Equal(520, standings.Height);
            Assert.Equal(0.72, standings.Opacity);
            Assert.False(standings.AlwaysOnTop);
            Assert.False(standings.ShowInTest);
            Assert.False(standings.ShowInPractice);
            Assert.False(standings.ShowInQualifying);
            Assert.True(standings.ShowInRace);
            Assert.Equal("screen-2", standings.ScreenId);
            Assert.False(standings.GetBooleanOption(OverlayOptionKeys.ChromeHeaderStatusRace, defaultValue: true));
            Assert.False(standings.GetBooleanOption(OverlayOptionKeys.ChromeFooterSourceRace, defaultValue: true));
            Assert.True(standings.GetBooleanOption(OverlayOptionKeys.ChromeHeaderTimeRemainingRace, defaultValue: false));
            Assert.Equal(0, standings.GetIntegerOption(OverlayOptionKeys.StandingsOtherClassRows, 2, 0, 6));

            var trackMap = settings.Overlays.Single(overlay => overlay.Id == "track-map");
            Assert.True(trackMap.Enabled);
            Assert.True(trackMap.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: false));
            Assert.False(trackMap.GetBooleanOption(OverlayOptionKeys.TrackMapSectorBoundariesEnabled, defaultValue: true));

            var garageCover = settings.Overlays.Single(overlay => overlay.Id == "garage-cover");
            Assert.True(garageCover.Enabled);
            Assert.Equal(garageCoverPath, garageCover.GetStringOption(OverlayOptionKeys.GarageCoverImagePath));
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
    public void Load_DoesNotOverwriteUnreadableExistingSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-settings-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            Directory.CreateDirectory(storage.SettingsRoot);
            var settingsPath = Path.Combine(storage.SettingsRoot, "settings.json");
            const string unreadableSettings = "{ this is not json";
            File.WriteAllText(settingsPath, unreadableSettings);

            _ = new AppSettingsStore(storage).Load();

            Assert.Equal(unreadableSettings, File.ReadAllText(settingsPath));
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
