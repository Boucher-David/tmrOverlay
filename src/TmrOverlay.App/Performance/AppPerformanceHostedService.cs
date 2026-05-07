using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TmrOverlay.App.Performance;

internal sealed class AppPerformanceHostedService : IHostedService, IDisposable
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(30);

    private readonly AppPerformanceState _performanceState;
    private readonly AppPerformanceSnapshotRecorder _recorder;
    private readonly ILogger<AppPerformanceHostedService> _logger;
    private System.Threading.Timer? _timer;

    public AppPerformanceHostedService(
        AppPerformanceState performanceState,
        AppPerformanceSnapshotRecorder recorder,
        ILogger<AppPerformanceHostedService> logger)
    {
        _performanceState = performanceState;
        _recorder = recorder;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new System.Threading.Timer(_ => RecordSnapshot(), null, InitialDelay, SnapshotInterval);
        _logger.LogInformation(
            "Performance diagnostics started. First snapshot delay: {InitialDelaySeconds}s. Interval: {SnapshotIntervalSeconds}s. Logs: {PerformanceLogsRoot}.",
            InitialDelay.TotalSeconds,
            SnapshotInterval.TotalSeconds,
            _recorder.PerformanceLogsRoot);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        RecordSnapshot();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void RecordSnapshot()
    {
        _recorder.Record(_performanceState.Snapshot());
    }
}
