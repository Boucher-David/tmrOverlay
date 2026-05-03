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
            var overlay = settings.GetOrAddOverlay("status", 304, 92);
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
            Assert.Equal("status", persisted.Id);
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
                      "classGapCarsBehind": 99
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
            Assert.Equal(5, overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsAhead, 5, 0, 8));
            Assert.Equal(5, overlay.GetIntegerOption(OverlayOptionKeys.RelativeCarsBehind, 5, 0, 8));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.FuelAdvice, defaultValue: true));
            Assert.True(overlay.GetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, defaultValue: true));

            var saved = File.ReadAllText(settingsPath);
            Assert.Contains($"\"settingsVersion\": {AppSettingsMigrator.CurrentVersion}", saved);
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

        var status = settings.GetOrAddOverlay("status", 520, 150);
        var fuel = settings.GetOrAddOverlay("fuel-calculator", 360, 180);
        status.X = 64;
        fuel.X = 512;

        var reloadedStatus = settings.GetOrAddOverlay("status", 100, 100);

        Assert.Equal(2, settings.Overlays.Count);
        Assert.Same(status, reloadedStatus);
        Assert.Equal(64, status.X);
        Assert.Equal(512, fuel.X);
        Assert.Equal(520, status.Width);
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
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }
}
