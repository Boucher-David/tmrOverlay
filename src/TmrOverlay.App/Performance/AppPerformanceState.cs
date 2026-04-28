using System.Diagnostics;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Performance;

internal sealed class AppPerformanceState
{
    private const int RecentSampleCapacity = 512;

    private readonly object _sync = new();
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly Dictionary<string, RollingPerformanceMetric> _metrics = new(StringComparer.OrdinalIgnoreCase);
    private long _telemetryFrameCount;
    private DateTimeOffset? _firstTelemetryFrameAtUtc;
    private DateTimeOffset? _lastTelemetryFrameAtUtc;
    private long _captureWriteStatusCount;
    private string? _lastCaptureId;
    private string? _lastCaptureDirectory;
    private int _lastCaptureFramesWritten;
    private int _lastCaptureSessionInfoSnapshotCount;
    private int _lastCapturePendingMessageCount;
    private long? _lastTelemetryFileBytes;
    private DateTimeOffset? _lastCaptureWriteAtUtc;
    private string? _lastCaptureWriteError;

    public void RecordTelemetryFrame(DateTimeOffset capturedAtUtc)
    {
        lock (_sync)
        {
            _telemetryFrameCount++;
            _firstTelemetryFrameAtUtc ??= capturedAtUtc;
            _lastTelemetryFrameAtUtc = capturedAtUtc;
        }
    }

    public void RecordOperation(string metricId, TimeSpan elapsed, bool succeeded = true)
    {
        if (string.IsNullOrWhiteSpace(metricId))
        {
            return;
        }

        lock (_sync)
        {
            if (!_metrics.TryGetValue(metricId, out var metric))
            {
                metric = new RollingPerformanceMetric(metricId, RecentSampleCapacity);
                _metrics[metricId] = metric;
            }

            metric.Record(elapsed, succeeded, DateTimeOffset.UtcNow);
        }
    }

    public void RecordCaptureWrite(TelemetryCaptureWriteStatus writeStatus)
    {
        lock (_sync)
        {
            _captureWriteStatusCount++;
            _lastCaptureId = writeStatus.CaptureId;
            _lastCaptureDirectory = writeStatus.DirectoryPath;
            _lastCaptureFramesWritten = writeStatus.FramesWritten;
            _lastCaptureSessionInfoSnapshotCount = writeStatus.SessionInfoSnapshotCount;
            _lastCapturePendingMessageCount = writeStatus.PendingMessageCount;
            _lastTelemetryFileBytes = writeStatus.TelemetryFileBytes ?? _lastTelemetryFileBytes;
            _lastCaptureWriteAtUtc = writeStatus.TimestampUtc;
            _lastCaptureWriteError = writeStatus.Exception?.Message;

            if (writeStatus.LastWriteDuration is { } duration)
            {
                if (!_metrics.TryGetValue(AppPerformanceMetricIds.CaptureWriterWrite, out var metric))
                {
                    metric = new RollingPerformanceMetric(AppPerformanceMetricIds.CaptureWriterWrite, RecentSampleCapacity);
                    _metrics[AppPerformanceMetricIds.CaptureWriterWrite] = metric;
                }

                metric.Record(duration, writeStatus.Exception is null, writeStatus.TimestampUtc);
            }
        }
    }

    public AppPerformanceSnapshot Snapshot()
    {
        lock (_sync)
        {
            var metrics = _metrics
                .Values
                .Select(metric => metric.Snapshot())
                .OrderBy(metric => metric.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new AppPerformanceSnapshot(
                TimestampUtc: DateTimeOffset.UtcNow,
                StartedAtUtc: _startedAtUtc,
                TelemetryFrameCount: _telemetryFrameCount,
                TelemetryFramesPerSecond: CalculateTelemetryFramesPerSecond(),
                Metrics: metrics,
                Capture: new CapturePerformanceSnapshot(
                    WriteStatusCount: _captureWriteStatusCount,
                    LastCaptureId: _lastCaptureId,
                    LastCaptureDirectory: _lastCaptureDirectory,
                    LastFramesWritten: _lastCaptureFramesWritten,
                    LastSessionInfoSnapshotCount: _lastCaptureSessionInfoSnapshotCount,
                    LastPendingMessageCount: _lastCapturePendingMessageCount,
                    LastTelemetryFileBytes: _lastTelemetryFileBytes,
                    LastWriteAtUtc: _lastCaptureWriteAtUtc,
                    LastWriteError: _lastCaptureWriteError),
                Process: ProcessPerformanceSnapshot.Capture());
        }
    }

    private double CalculateTelemetryFramesPerSecond()
    {
        if (_telemetryFrameCount < 2 || _firstTelemetryFrameAtUtc is null || _lastTelemetryFrameAtUtc is null)
        {
            return 0d;
        }

        var elapsedSeconds = (_lastTelemetryFrameAtUtc.Value - _firstTelemetryFrameAtUtc.Value).TotalSeconds;
        return elapsedSeconds <= 0d
            ? 0d
            : Math.Round((_telemetryFrameCount - 1) / elapsedSeconds, 2);
    }

    private sealed class RollingPerformanceMetric
    {
        private readonly string _id;
        private readonly double[] _recentMilliseconds;
        private int _nextSampleIndex;
        private int _recentSampleCount;
        private long _count;
        private long _errorCount;
        private double _totalMilliseconds;
        private double _lastMilliseconds;
        private double _maxMilliseconds;
        private DateTimeOffset? _lastRecordedAtUtc;

        public RollingPerformanceMetric(string id, int sampleCapacity)
        {
            _id = id;
            _recentMilliseconds = new double[sampleCapacity];
        }

        public void Record(TimeSpan elapsed, bool succeeded, DateTimeOffset timestampUtc)
        {
            var milliseconds = Math.Max(0d, elapsed.TotalMilliseconds);
            _count++;
            if (!succeeded)
            {
                _errorCount++;
            }

            _totalMilliseconds += milliseconds;
            _lastMilliseconds = milliseconds;
            _maxMilliseconds = Math.Max(_maxMilliseconds, milliseconds);
            _lastRecordedAtUtc = timestampUtc;

            _recentMilliseconds[_nextSampleIndex] = milliseconds;
            _nextSampleIndex = (_nextSampleIndex + 1) % _recentMilliseconds.Length;
            _recentSampleCount = Math.Min(_recentSampleCount + 1, _recentMilliseconds.Length);
        }

        public PerformanceMetricSnapshot Snapshot()
        {
            var recent = new double[_recentSampleCount];
            Array.Copy(_recentMilliseconds, recent, _recentSampleCount);
            Array.Sort(recent);
            var p95 = recent.Length == 0
                ? 0d
                : recent[Math.Clamp((int)Math.Ceiling(recent.Length * 0.95d) - 1, 0, recent.Length - 1)];

            return new PerformanceMetricSnapshot(
                Id: _id,
                Count: _count,
                ErrorCount: _errorCount,
                AverageMilliseconds: _count == 0 ? 0d : Math.Round(_totalMilliseconds / _count, 3),
                LastMilliseconds: Math.Round(_lastMilliseconds, 3),
                MaxMilliseconds: Math.Round(_maxMilliseconds, 3),
                P95Milliseconds: Math.Round(p95, 3),
                LastRecordedAtUtc: _lastRecordedAtUtc);
        }
    }
}

internal static class AppPerformanceMetricIds
{
    public const string TelemetryDataChanged = "telemetry.data_changed";
    public const string LiveTelemetrySink = "telemetry.live_sink";
    public const string HistoryRecordFrame = "telemetry.history_record_frame";
    public const string CaptureWriterWrite = "capture.writer_write";
    public const string CaptureWriteStatusCallback = "capture.write_status_callback";
    public const string OverlayStatusRefresh = "overlay.status.refresh";
    public const string OverlayFuelRefresh = "overlay.fuel.refresh";
    public const string OverlayRadarRefresh = "overlay.radar.refresh";
    public const string OverlayGapRefresh = "overlay.gap.refresh";
}

internal sealed record AppPerformanceSnapshot(
    DateTimeOffset TimestampUtc,
    DateTimeOffset StartedAtUtc,
    long TelemetryFrameCount,
    double TelemetryFramesPerSecond,
    IReadOnlyList<PerformanceMetricSnapshot> Metrics,
    CapturePerformanceSnapshot Capture,
    ProcessPerformanceSnapshot Process);

internal sealed record PerformanceMetricSnapshot(
    string Id,
    long Count,
    long ErrorCount,
    double AverageMilliseconds,
    double LastMilliseconds,
    double MaxMilliseconds,
    double P95Milliseconds,
    DateTimeOffset? LastRecordedAtUtc);

internal sealed record CapturePerformanceSnapshot(
    long WriteStatusCount,
    string? LastCaptureId,
    string? LastCaptureDirectory,
    int LastFramesWritten,
    int LastSessionInfoSnapshotCount,
    int LastPendingMessageCount,
    long? LastTelemetryFileBytes,
    DateTimeOffset? LastWriteAtUtc,
    string? LastWriteError);

internal sealed record ProcessPerformanceSnapshot(
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long ManagedHeapBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    public static ProcessPerformanceSnapshot Capture()
    {
        using var process = Process.GetCurrentProcess();
        return new ProcessPerformanceSnapshot(
            WorkingSetBytes: process.WorkingSet64,
            PrivateMemoryBytes: process.PrivateMemorySize64,
            ManagedHeapBytes: GC.GetTotalMemory(forceFullCollection: false),
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2));
    }
}
