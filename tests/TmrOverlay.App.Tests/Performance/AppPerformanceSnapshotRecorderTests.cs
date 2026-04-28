using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using Xunit;

namespace TmrOverlay.App.Tests.Performance;

public sealed class AppPerformanceSnapshotRecorderTests
{
    [Fact]
    public void Record_WritesPerformanceSnapshotJsonlUnderLogsRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-performance-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var state = new AppPerformanceState();
            state.RecordTelemetryFrame(DateTimeOffset.Parse("2026-04-28T12:00:00Z"));
            state.RecordOperation("test.operation", TimeSpan.FromMilliseconds(2));
            var recorder = new AppPerformanceSnapshotRecorder(storage);

            recorder.Record(state.Snapshot());

            var files = Directory.GetFiles(recorder.PerformanceLogsRoot, "performance-*.jsonl");
            var file = Assert.Single(files);
            var content = File.ReadAllText(file);
            Assert.Contains("\"telemetryFrameCount\":1", content);
            Assert.Contains("\"id\":\"test.operation\"", content);
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
