using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
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
            var overlay = settings.GetOrAddOverlay("status", 304, 92);
            overlay.X = 128;
            overlay.Y = 256;
            overlay.Opacity = 0.75;

            store.Save(settings);

            var reloaded = new AppSettingsStore(storage).Load();
            var persisted = Assert.Single(reloaded.Overlays);
            Assert.Equal("status", persisted.Id);
            Assert.Equal(128, persisted.X);
            Assert.Equal(256, persisted.Y);
            Assert.Equal(0.75, persisted.Opacity);
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
