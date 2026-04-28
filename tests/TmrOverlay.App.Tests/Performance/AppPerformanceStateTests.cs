using TmrOverlay.App.Performance;
using TmrOverlay.App.Telemetry;
using Xunit;

namespace TmrOverlay.App.Tests.Performance;

public sealed class AppPerformanceStateTests
{
    [Fact]
    public void Snapshot_IncludesRollingOperationStats()
    {
        var state = new AppPerformanceState();

        state.RecordOperation("test.operation", TimeSpan.FromMilliseconds(1));
        state.RecordOperation("test.operation", TimeSpan.FromMilliseconds(2), succeeded: false);
        state.RecordOperation("test.operation", TimeSpan.FromMilliseconds(3));

        var snapshot = state.Snapshot();
        var metric = Assert.Single(snapshot.Metrics.Where(metric => metric.Id == "test.operation"));
        Assert.Equal(3, metric.Count);
        Assert.Equal(1, metric.ErrorCount);
        Assert.Equal(2d, metric.AverageMilliseconds);
        Assert.Equal(3d, metric.LastMilliseconds);
        Assert.Equal(3d, metric.MaxMilliseconds);
        Assert.Equal(3d, metric.P95Milliseconds);
    }

    [Fact]
    public void Snapshot_CalculatesTelemetryFrameRateFromFrameTimestamps()
    {
        var state = new AppPerformanceState();
        var start = DateTimeOffset.Parse("2026-04-28T12:00:00Z");

        state.RecordTelemetryFrame(start);
        state.RecordTelemetryFrame(start.AddSeconds(1));
        state.RecordTelemetryFrame(start.AddSeconds(2));

        var snapshot = state.Snapshot();

        Assert.Equal(3, snapshot.TelemetryFrameCount);
        Assert.Equal(1d, snapshot.TelemetryFramesPerSecond);
    }

    [Fact]
    public void RecordCaptureWrite_TracksLatestCaptureStatusAndWriterDuration()
    {
        var state = new AppPerformanceState();
        var timestamp = DateTimeOffset.Parse("2026-04-28T12:00:00Z");

        state.RecordCaptureWrite(new TelemetryCaptureWriteStatus(
            TimestampUtc: timestamp,
            CaptureId: "capture-1",
            DirectoryPath: @"C:\captures\capture-1",
            FramesWritten: 42,
            SessionInfoSnapshotCount: 2,
            PendingMessageCount: 3,
            TelemetryFileBytes: 4096,
            Exception: null,
            LastWriteDuration: TimeSpan.FromMilliseconds(4)));

        var snapshot = state.Snapshot();
        var writerMetric = Assert.Single(snapshot.Metrics.Where(metric => metric.Id == AppPerformanceMetricIds.CaptureWriterWrite));

        Assert.Equal(1, snapshot.Capture.WriteStatusCount);
        Assert.Equal("capture-1", snapshot.Capture.LastCaptureId);
        Assert.Equal(42, snapshot.Capture.LastFramesWritten);
        Assert.Equal(2, snapshot.Capture.LastSessionInfoSnapshotCount);
        Assert.Equal(3, snapshot.Capture.LastPendingMessageCount);
        Assert.Equal(4096, snapshot.Capture.LastTelemetryFileBytes);
        Assert.Equal(timestamp, snapshot.Capture.LastWriteAtUtc);
        Assert.Equal(1, writerMetric.Count);
        Assert.Equal(4d, writerMetric.AverageMilliseconds);
    }
}
