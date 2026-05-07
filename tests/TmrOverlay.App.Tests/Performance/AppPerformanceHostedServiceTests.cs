using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Localhost;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using Xunit;

namespace TmrOverlay.App.Tests.Performance;

public sealed class AppPerformanceHostedServiceTests
{
    [Fact]
    public async Task StartAsync_DelaysFirstPerformanceLogWrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-performance-service-test", Guid.NewGuid().ToString("N"));
        try
        {
            var recorder = new AppPerformanceSnapshotRecorder(CreateStorage(root));
            using var service = new AppPerformanceHostedService(
                new AppPerformanceState(),
                recorder,
                new LocalhostOverlayState(new LocalhostOverlayOptions()),
                NullLogger<AppPerformanceHostedService>.Instance);

            await service.StartAsync(CancellationToken.None);

            Assert.False(Directory.Exists(recorder.PerformanceLogsRoot));

            await service.StopAsync(CancellationToken.None);

            Assert.Single(Directory.GetFiles(recorder.PerformanceLogsRoot, "performance-*.jsonl"));
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
