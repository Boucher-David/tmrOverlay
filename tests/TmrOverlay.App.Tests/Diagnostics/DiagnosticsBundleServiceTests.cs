using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Diagnostics;

public sealed class DiagnosticsBundleServiceTests
{
    [Fact]
    public void CreateBundle_IncludesTriageFilesAndExcludesRawTelemetry()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-diagnostics-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            Directory.CreateDirectory(storage.LogsRoot);
            Directory.CreateDirectory(storage.EventsRoot);
            Directory.CreateDirectory(storage.SettingsRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(storage.RuntimeStatePath)!);
            File.WriteAllText(Path.Combine(storage.LogsRoot, "tmroverlay-20260426.log"), "log line");
            File.WriteAllText(Path.Combine(storage.EventsRoot, "events-20260426.jsonl"), "{}");
            File.WriteAllText(Path.Combine(storage.SettingsRoot, "settings.json"), "{}");
            File.WriteAllText(storage.RuntimeStatePath, "{}");

            var captureDirectory = Path.Combine(storage.CaptureRoot, "capture-20260426-120000-000");
            Directory.CreateDirectory(captureDirectory);
            File.WriteAllText(Path.Combine(captureDirectory, "capture-manifest.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "telemetry-schema.json"), "[]");
            File.WriteAllText(Path.Combine(captureDirectory, "latest-session.yaml"), "WeekendInfo: {}");
            File.WriteAllText(Path.Combine(captureDirectory, "telemetry.bin"), "raw");

            var state = new TelemetryCaptureState();
            state.MarkCaptureStarted(captureDirectory, DateTimeOffset.UtcNow);
            var service = new DiagnosticsBundleService(
                storage,
                state,
                new LiveTelemetryStore(),
                NullLogger<DiagnosticsBundleService>.Instance);

            var bundlePath = service.CreateBundle();

            using var archive = ZipFile.OpenRead(bundlePath);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("metadata/app-version.json", entryNames);
            Assert.Contains("metadata/storage.json", entryNames);
            Assert.Contains("runtime/runtime-state.json", entryNames);
            Assert.Contains("runtime/telemetry-capture-state.json", entryNames);
            Assert.Contains("live/live-telemetry-snapshot.json", entryNames);
            Assert.Contains("live/overlay-state-summary.json", entryNames);
            Assert.Contains("live/telemetry-availability.json", entryNames);
            Assert.Contains("live/weather-snapshot.json", entryNames);
            Assert.Contains("settings/settings.json", entryNames);
            Assert.Contains("logs/tmroverlay-20260426.log", entryNames);
            Assert.Contains("events/events-20260426.jsonl", entryNames);
            Assert.Contains("latest-capture/capture-manifest.json", entryNames);
            Assert.Contains("latest-capture/telemetry-schema.json", entryNames);
            Assert.Contains("latest-capture/telemetry-schema-summary.json", entryNames);
            Assert.Contains("latest-capture/latest-session.yaml", entryNames);
            Assert.DoesNotContain("latest-capture/telemetry.bin", entryNames);
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
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }
}
