using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
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
            var edgeCaseDirectory = Path.Combine(storage.LogsRoot, "edge-cases");
            var modelParityDirectory = Path.Combine(storage.LogsRoot, "model-parity");
            Directory.CreateDirectory(edgeCaseDirectory);
            Directory.CreateDirectory(modelParityDirectory);
            File.WriteAllText(Path.Combine(storage.LogsRoot, "tmroverlay-20260426.log"), "log line");
            File.WriteAllText(Path.Combine(edgeCaseDirectory, "session-20260426-edge-cases.json"), """{"clipCount":1}""");
            File.WriteAllText(Path.Combine(modelParityDirectory, "session-20260426-live-model-parity.json"), """{"frameCount":1}""");
            File.WriteAllText(Path.Combine(storage.EventsRoot, "events-20260426.jsonl"), "{}");
            File.WriteAllText(Path.Combine(storage.SettingsRoot, "settings.json"), "{}");
            File.WriteAllText(storage.RuntimeStatePath, "{}");

            var analysisDirectory = Path.Combine(storage.UserHistoryRoot, "analysis");
            var historySessionDirectory = Path.Combine(
                storage.UserHistoryRoot,
                "cars",
                "car-156-mercedesamgevogt3",
                "tracks",
                "track-262-nurburgring-combinedshortb",
                "sessions",
                "race");
            var summariesDirectory = Path.Combine(historySessionDirectory, "summaries");
            Directory.CreateDirectory(analysisDirectory);
            Directory.CreateDirectory(summariesDirectory);
            Directory.CreateDirectory(Path.Combine(storage.UserHistoryRoot, ".maintenance"));
            File.WriteAllText(Path.Combine(analysisDirectory, "20260426-race.json"), """{"title":"race analysis"}""");
            File.WriteAllText(Path.Combine(storage.UserHistoryRoot, ".maintenance", "manifest.json"), """{"summaryFilesScanned":1}""");
            File.WriteAllText(Path.Combine(historySessionDirectory, "aggregate.json"), """{"sessionCount":1}""");
            File.WriteAllText(Path.Combine(summariesDirectory, "capture-20260426-120000-000.json"), """{"sourceCaptureId":"capture-20260426-120000-000"}""");

            var captureDirectory = Path.Combine(storage.CaptureRoot, "capture-20260426-120000-000");
            Directory.CreateDirectory(captureDirectory);
            File.WriteAllText(Path.Combine(captureDirectory, "capture-manifest.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "telemetry-schema.json"), "[]");
            File.WriteAllText(Path.Combine(captureDirectory, "latest-session.yaml"), "WeekendInfo: {}");
            File.WriteAllText(Path.Combine(captureDirectory, "capture-synthesis.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "live-model-parity.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "telemetry.bin"), "raw");
            var ibtAnalysisDirectory = Path.Combine(captureDirectory, "ibt-analysis");
            Directory.CreateDirectory(ibtAnalysisDirectory);
            File.WriteAllText(Path.Combine(ibtAnalysisDirectory, "status.json"), """{"status":"skipped"}""");
            File.WriteAllText(Path.Combine(ibtAnalysisDirectory, "ibt-schema-summary.json"), "{}");
            File.WriteAllText(Path.Combine(ibtAnalysisDirectory, "source.ibt"), "raw ibt");

            var state = new TelemetryCaptureState();
            state.MarkCaptureStarted(captureDirectory, DateTimeOffset.UtcNow);
            var performance = new AppPerformanceState();
            performance.RecordOperation("test.operation", TimeSpan.FromMilliseconds(3));
            var performanceRecorder = new AppPerformanceSnapshotRecorder(storage);
            performanceRecorder.Record(performance.Snapshot());
            var service = new DiagnosticsBundleService(
                storage,
                new LiveModelParityOptions(),
                state,
                performance,
                performanceRecorder,
                NullLogger<DiagnosticsBundleService>.Instance);

            var bundlePath = service.CreateBundle();

            using var archive = ZipFile.OpenRead(bundlePath);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("metadata/app-version.json", entryNames);
            Assert.Contains("metadata/storage.json", entryNames);
            Assert.Contains("metadata/telemetry-state.json", entryNames);
            Assert.Contains("metadata/performance.json", entryNames);
            Assert.Contains("runtime/runtime-state.json", entryNames);
            Assert.Contains("settings/settings.json", entryNames);
            Assert.Contains("logs/tmroverlay-20260426.log", entryNames);
            Assert.Contains("edge-cases/session-20260426-edge-cases.json", entryNames);
            Assert.Contains("model-parity/session-20260426-live-model-parity.json", entryNames);
            Assert.Contains(entryNames, entryName => entryName.StartsWith("performance/performance-", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("events/events-20260426.jsonl", entryNames);
            Assert.Contains("latest-capture/capture-manifest.json", entryNames);
            Assert.Contains("latest-capture/telemetry-schema.json", entryNames);
            Assert.Contains("latest-capture/latest-session.yaml", entryNames);
            Assert.Contains("latest-capture/capture-synthesis.json", entryNames);
            Assert.Contains("latest-capture/live-model-parity.json", entryNames);
            Assert.Contains("latest-capture/ibt-analysis/status.json", entryNames);
            Assert.Contains("latest-capture/ibt-analysis/ibt-schema-summary.json", entryNames);
            Assert.Contains("analysis/20260426-race.json", entryNames);
            Assert.DoesNotContain("history/user/analysis/20260426-race.json", entryNames);
            Assert.Contains("history/user/.maintenance/manifest.json", entryNames);
            Assert.Contains("history/user/cars/car-156-mercedesamgevogt3/tracks/track-262-nurburgring-combinedshortb/sessions/race/aggregate.json", entryNames);
            Assert.Contains("history/user/cars/car-156-mercedesamgevogt3/tracks/track-262-nurburgring-combinedshortb/sessions/race/summaries/capture-20260426-120000-000.json", entryNames);
            Assert.DoesNotContain("latest-capture/telemetry.bin", entryNames);
            Assert.DoesNotContain("latest-capture/ibt-analysis/source.ibt", entryNames);
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
