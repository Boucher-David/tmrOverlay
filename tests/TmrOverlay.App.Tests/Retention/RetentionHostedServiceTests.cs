using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Retention;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using Xunit;

namespace TmrOverlay.App.Tests.Retention;

public sealed class RetentionHostedServiceTests
{
    [Fact]
    public async Task StartAsync_RemovesOldCapturesAndDiagnostics()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-retention-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            Directory.CreateDirectory(storage.CaptureRoot);
            Directory.CreateDirectory(storage.DiagnosticsRoot);
            var performanceRoot = Path.Combine(storage.LogsRoot, "performance");
            var edgeCaseRoot = Path.Combine(storage.LogsRoot, "edge-cases");
            var modelParityRoot = Path.Combine(storage.LogsRoot, "model-parity");
            var overlayDiagnosticsRoot = Path.Combine(storage.LogsRoot, "overlay-diagnostics");
            Directory.CreateDirectory(performanceRoot);
            Directory.CreateDirectory(edgeCaseRoot);
            Directory.CreateDirectory(modelParityRoot);
            Directory.CreateDirectory(overlayDiagnosticsRoot);

            var keepCapture = Directory.CreateDirectory(Path.Combine(storage.CaptureRoot, "capture-keep"));
            var deleteCapture = Directory.CreateDirectory(Path.Combine(storage.CaptureRoot, "capture-delete"));
            var keepBundle = Path.Combine(storage.DiagnosticsRoot, "keep.zip");
            var deleteBundle = Path.Combine(storage.DiagnosticsRoot, "delete.zip");
            var keepPerformance = Path.Combine(performanceRoot, "performance-keep.jsonl");
            var deletePerformance = Path.Combine(performanceRoot, "performance-delete.jsonl");
            var keepEdgeCase = Path.Combine(edgeCaseRoot, "session-keep-edge-cases.json");
            var deleteEdgeCase = Path.Combine(edgeCaseRoot, "session-delete-edge-cases.json");
            var keepModelParity = Path.Combine(modelParityRoot, "session-keep-live-model-parity.json");
            var deleteModelParity = Path.Combine(modelParityRoot, "session-delete-live-model-parity.json");
            var keepOverlayDiagnostics = Path.Combine(overlayDiagnosticsRoot, "session-keep-live-overlay-diagnostics.json");
            var deleteOverlayDiagnostics = Path.Combine(overlayDiagnosticsRoot, "session-delete-live-overlay-diagnostics.json");
            File.WriteAllText(keepBundle, "keep");
            File.WriteAllText(deleteBundle, "delete");
            File.WriteAllText(keepPerformance, "keep");
            File.WriteAllText(deletePerformance, "delete");
            File.WriteAllText(keepEdgeCase, "keep");
            File.WriteAllText(deleteEdgeCase, "delete");
            File.WriteAllText(keepModelParity, "keep");
            File.WriteAllText(deleteModelParity, "delete");
            File.WriteAllText(keepOverlayDiagnostics, "keep");
            File.WriteAllText(deleteOverlayDiagnostics, "delete");

            Directory.SetLastWriteTimeUtc(keepCapture.FullName, DateTime.UtcNow);
            Directory.SetLastWriteTimeUtc(deleteCapture.FullName, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(keepBundle, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(deleteBundle, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(keepPerformance, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(deletePerformance, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(keepEdgeCase, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(deleteEdgeCase, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(keepModelParity, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(deleteModelParity, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(keepOverlayDiagnostics, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(deleteOverlayDiagnostics, DateTime.UtcNow.AddDays(-10));

            var service = new RetentionHostedService(
                storage,
                new RetentionOptions
                {
                    Enabled = true,
                    CaptureRetentionDays = 1,
                    MaxCaptureDirectories = 10,
                    DiagnosticsRetentionDays = 1,
                    MaxDiagnosticsBundles = 10,
                    PerformanceLogRetentionDays = 1,
                    MaxPerformanceLogFiles = 10,
                    EdgeCaseRetentionDays = 1,
                    MaxEdgeCaseFiles = 10
                },
                new LiveModelParityOptions(),
                new LiveOverlayDiagnosticsOptions(),
                NullLogger<RetentionHostedService>.Instance);

            await service.StartAsync(CancellationToken.None);

            Assert.True(Directory.Exists(keepCapture.FullName));
            Assert.False(Directory.Exists(deleteCapture.FullName));
            Assert.True(File.Exists(keepBundle));
            Assert.False(File.Exists(deleteBundle));
            Assert.True(File.Exists(keepPerformance));
            Assert.False(File.Exists(deletePerformance));
            Assert.True(File.Exists(keepEdgeCase));
            Assert.False(File.Exists(deleteEdgeCase));
            Assert.True(File.Exists(keepModelParity));
            Assert.False(File.Exists(deleteModelParity));
            Assert.True(File.Exists(keepOverlayDiagnostics));
            Assert.False(File.Exists(deleteOverlayDiagnostics));
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
