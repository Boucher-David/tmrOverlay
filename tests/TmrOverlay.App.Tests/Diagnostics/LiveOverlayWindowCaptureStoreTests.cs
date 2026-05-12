using System.Text.Json;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Settings;
using Xunit;

namespace TmrOverlay.App.Tests.Diagnostics;

public sealed class LiveOverlayWindowCaptureStoreTests
{
    [Fact]
    public void Snapshot_RecordsNativeAndBrowserParityMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-live-window-capture-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var store = new LiveOverlayWindowCaptureStore(storage);
            var definition = StandingsOverlayDefinition.Definition;
            var settings = new OverlaySettings
            {
                Id = definition.Id,
                Enabled = true,
                X = 44,
                Y = 55,
                Width = 580,
                Height = 340,
                Scale = 1.25d,
                Opacity = 0.72d
            };

            store.RecordOverlayWindow(
                definition,
                settings,
                form: null,
                enabled: true,
                sessionAllowed: true,
                settingsPreview: false,
                desiredVisible: true,
                actualVisible: false,
                topMost: false,
                liveTelemetryAvailable: true,
                contextRequirement: definition.ContextRequirement.ToString(),
                contextAvailable: true,
                contextReason: "not_required",
                settingsOverlayActive: false,
                settingsWindowVisible: true,
                settingsWindowIntersects: false,
                settingsWindowInputProtected: false,
                inputTransparent: false,
                noActivate: false,
                implementation: "native-v2-not-created",
                nativeFormType: null,
                nativeRenderer: null,
                nativeBodyKind: null);

            var manifest = store.Snapshot();

            Assert.Equal("live-window-screen-crops", manifest.CaptureKind);
            Assert.Equal(10, manifest.CaptureCadenceSeconds);
            Assert.Contains("Browser-source fields", manifest.Description);
            Assert.Contains("screenshotAgeSeconds", manifest.ScreenshotFreshnessNote);
            var overlay = Assert.Single(manifest.Overlays);
            Assert.Equal(definition.Id, overlay.OverlayId);
            Assert.Equal("native-v2-not-created", overlay.Implementation);
            Assert.Null(overlay.NativeFormType);
            Assert.Null(overlay.NativeRenderer);
            Assert.Null(overlay.NativeBodyKind);
            Assert.True(overlay.BrowserSourceSupported);
            Assert.Equal("/overlays/standings", overlay.BrowserRoute);
            Assert.True(overlay.BrowserRequiresTelemetry);
            Assert.Equal(250, overlay.BrowserRefreshIntervalMilliseconds);
            Assert.True(overlay.BrowserRecommendedWidth > 0);
            Assert.True(overlay.BrowserRecommendedHeight > 0);
            Assert.Equal(definition.DefaultWidth, overlay.DefaultWidth);
            Assert.Equal(definition.DefaultHeight, overlay.DefaultHeight);
            Assert.False(overlay.ScreenshotRepresentsCurrentState);
            Assert.Null(overlay.ScreenshotAgeSeconds);
            Assert.Equal("AnyTelemetry", overlay.ContextRequirement);
            Assert.True(overlay.ContextAvailable);
            Assert.Equal("not_required", overlay.ContextReason);
            Assert.True(overlay.SettingsWindowVisible);
            Assert.False(overlay.SettingsWindowIntersects);
            Assert.False(overlay.SettingsWindowInputProtected);
            Assert.False(overlay.TopMost);
            Assert.False(overlay.InputInterceptRisk);

            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            Assert.Contains("\"browserRoute\":\"/overlays/standings\"", json);
            Assert.Contains("\"captureCadenceSeconds\":10", json);
            Assert.DoesNotContain("screenshotSignature", json, StringComparison.OrdinalIgnoreCase);
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
    public void Snapshot_DoesNotReportSettingsActiveWhenSettingsWindowIsHidden()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-live-window-capture-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var store = new LiveOverlayWindowCaptureStore(storage);
            var definition = StandingsOverlayDefinition.Definition;
            var settings = new OverlaySettings
            {
                Id = definition.Id,
                Enabled = true,
                X = 44,
                Y = 55,
                Width = 580,
                Height = 340,
                Scale = 1.25d,
                Opacity = 1d
            };

            store.RecordOverlayWindow(
                definition,
                settings,
                form: null,
                enabled: true,
                sessionAllowed: true,
                settingsPreview: false,
                desiredVisible: true,
                actualVisible: true,
                topMost: false,
                liveTelemetryAvailable: true,
                contextRequirement: definition.ContextRequirement.ToString(),
                contextAvailable: true,
                contextReason: "not_required",
                settingsOverlayActive: true,
                settingsWindowVisible: false,
                settingsWindowIntersects: false,
                settingsWindowInputProtected: false,
                inputTransparent: false,
                noActivate: false,
                implementation: "native-v2",
                nativeFormType: "StandingsForm",
                nativeRenderer: "winforms",
                nativeBodyKind: "table");

            var overlay = Assert.Single(store.Snapshot().Overlays);

            Assert.False(overlay.SettingsOverlayActive);
            Assert.False(overlay.SettingsWindowVisible);
            Assert.False(overlay.InputInterceptRisk);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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
