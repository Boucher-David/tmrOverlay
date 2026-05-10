using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Events;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Updates;
using Xunit;

namespace TmrOverlay.App.Tests.Updates;

public sealed class ReleaseUpdateServiceTests
{
    [Fact]
    public async Task StopAndDispose_CanRunRepeatedlyAfterStartupCheckIsCanceled()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-update-test", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ReleaseUpdateService(
                new ReleaseUpdateOptions
                {
                    Enabled = true,
                    CheckOnStartup = true,
                    StartupDelaySeconds = 300
                },
                new AppEventRecorder(CreateStorage(root)),
                NullLogger<ReleaseUpdateService>.Instance);

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);

            service.Dispose();
            service.Dispose();
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
