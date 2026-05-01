using TmrOverlay.App.Telemetry;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class TelemetryCaptureStateTests
{
    [Fact]
    public void SetRawCaptureEnabled_AllowsRuntimeEnableBeforeCaptureStarts()
    {
        var state = new TelemetryCaptureState();

        var accepted = state.SetRawCaptureEnabled(true);

        Assert.True(accepted);
        Assert.True(state.Snapshot().RawCaptureEnabled);
    }

    [Fact]
    public void SetRawCaptureEnabled_RequestsStopWhileRawCaptureIsActive()
    {
        var state = new TelemetryCaptureState();
        state.SetRawCaptureEnabled(true);
        state.MarkCollectionStarted("collection-test", "session-test", DateTimeOffset.UtcNow.AddSeconds(-1));
        state.MarkCaptureStarted("capture-active", DateTimeOffset.UtcNow, "capture-test", "collection-test", "session-test");

        var accepted = state.SetRawCaptureEnabled(false);
        var snapshot = state.Snapshot();

        Assert.True(accepted);
        Assert.False(snapshot.RawCaptureEnabled);
        Assert.True(snapshot.RawCaptureActive);
        Assert.True(snapshot.RawCaptureStopRequested);
        Assert.NotNull(snapshot.RawCaptureStopRequestedAtUtc);
        Assert.NotNull(snapshot.LastWarning);
        Assert.Contains("stop requested", snapshot.LastWarning!);

        state.MarkRawCaptureStopped(DateTimeOffset.UtcNow);
        var stopped = state.Snapshot();

        Assert.True(stopped.IsCapturing);
        Assert.False(stopped.RawCaptureActive);
        Assert.False(stopped.RawCaptureStopRequested);
        Assert.NotNull(stopped.LastRawCaptureStoppedAtUtc);
        Assert.Equal("collection-test", stopped.CurrentCollectionId);
    }

    [Fact]
    public void HistoryFinalizationState_SurfacesBackgroundSaveProgress()
    {
        var state = new TelemetryCaptureState();
        var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2);
        var savedAtUtc = DateTimeOffset.UtcNow;

        state.SetAppRunId("run-test");
        state.MarkCollectionStarted("collection-test", "session-test", startedAtUtc.AddSeconds(-1));
        state.MarkHistoryFinalizationStarted(startedAtUtc);
        var saving = state.Snapshot();

        Assert.Equal("run-test", saving.AppRunId);
        Assert.Equal("collection-test", saving.CurrentCollectionId);
        Assert.Equal("session-test", saving.CurrentSourceId);
        Assert.True(saving.IsHistoryFinalizing);
        Assert.Equal(startedAtUtc, saving.HistoryFinalizationStartedAtUtc);

        state.MarkHistorySummarySaved(
            "GT3 / Nurburgring / Race",
            savedAtUtc,
            historySaveElapsedMilliseconds: 12,
            analysisSaveElapsedMilliseconds: 34);
        state.MarkHistoryFinalizationStopped(elapsedMilliseconds: 56);
        var saved = state.Snapshot();

        Assert.False(saved.IsHistoryFinalizing);
        Assert.Null(saved.HistoryFinalizationStartedAtUtc);
        Assert.Equal(1, saved.HistorySummarySaveCount);
        Assert.Equal(savedAtUtc, saved.LastHistorySavedAtUtc);
        Assert.Equal("GT3 / Nurburgring / Race", saved.LastHistorySummaryLabel);
        Assert.Equal(56, saved.LastHistoryFinalizationElapsedMilliseconds);
        Assert.Equal(12, saved.LastHistorySaveElapsedMilliseconds);
        Assert.Equal(34, saved.LastAnalysisSaveElapsedMilliseconds);
    }

    [Fact]
    public void CaptureWriteState_SurfacesRawCapturePerformance()
    {
        var state = new TelemetryCaptureState();
        var timestampUtc = DateTimeOffset.UtcNow;

        state.RecordCaptureWrite(new TelemetryCaptureWriteStatus(
            TimestampUtc: timestampUtc,
            CaptureId: "capture-test",
            DirectoryPath: @"C:\captures\capture-test",
            FramesWritten: 42,
            SessionInfoSnapshotCount: 1,
            TelemetryFileBytes: 123_456,
            LastWriteBytes: 8_192,
            LastWriteElapsedMilliseconds: 3,
            LastWriteKind: "frame",
            AverageWriteElapsedMilliseconds: 1.7,
            MaxWriteElapsedMilliseconds: 8));

        var snapshot = state.Snapshot();

        Assert.Equal(42, snapshot.WrittenFrameCount);
        Assert.Equal(123_456, snapshot.TelemetryFileBytes);
        Assert.Equal(timestampUtc, snapshot.LastDiskWriteAtUtc);
        Assert.Equal(1, snapshot.CaptureWriteStatusCount);
        Assert.Equal(8_192, snapshot.LastCaptureWriteBytes);
        Assert.Equal(3, snapshot.LastCaptureWriteElapsedMilliseconds);
        Assert.Equal("frame", snapshot.LastCaptureWriteKind);
        Assert.Equal(1.7, snapshot.AverageCaptureWriteElapsedMilliseconds.GetValueOrDefault(), 3);
        Assert.Equal(8, snapshot.MaxCaptureWriteElapsedMilliseconds);
    }

    [Fact]
    public void CaptureSynthesisState_SurfacesBackgroundSynthesisProgress()
    {
        var state = new TelemetryCaptureState();
        var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2);
        var savedAtUtc = DateTimeOffset.UtcNow;

        state.MarkCaptureSynthesisPending(startedAtUtc.AddSeconds(-5), "iRacingSim64DX11 (1234)");
        var pending = state.Snapshot();

        Assert.True(pending.IsCaptureSynthesisPending);
        Assert.Equal(startedAtUtc.AddSeconds(-5), pending.CaptureSynthesisPendingSinceUtc);
        Assert.Equal("iRacingSim64DX11 (1234)", pending.CaptureSynthesisPendingReason);

        state.MarkCaptureSynthesisStarted(startedAtUtc);
        var running = state.Snapshot();

        Assert.False(running.IsCaptureSynthesisPending);
        Assert.Null(running.CaptureSynthesisPendingSinceUtc);
        Assert.Null(running.CaptureSynthesisPendingReason);
        Assert.True(running.IsCaptureSynthesisRunning);
        Assert.Equal(startedAtUtc, running.CaptureSynthesisStartedAtUtc);

        state.MarkCaptureSynthesisSaved(new CaptureSynthesisResult(
            Path: @"C:\captures\capture-test\capture-synthesis.json",
            StablePath: @"C:\captures\capture-test\capture-synthesis.json",
            Bytes: 1_234_567,
            TelemetryBytes: 98_765_432,
            ElapsedMilliseconds: 1_250,
            ProcessCpuMilliseconds: 1_080,
            ProcessCpuPercentOfOneCore: 86.4,
            StartedAtUtc: startedAtUtc,
            FinishedAtUtc: savedAtUtc,
            TotalFrameRecords: 12_228,
            SampledFrameCount: 12_228,
            SampleStride: 1,
            FieldCount: 334));
        state.MarkCaptureSynthesisStopped();
        var saved = state.Snapshot();

        Assert.False(saved.IsCaptureSynthesisRunning);
        Assert.Null(saved.CaptureSynthesisStartedAtUtc);
        Assert.Equal(1, saved.CaptureSynthesisSaveCount);
        Assert.Equal(savedAtUtc, saved.LastCaptureSynthesisSavedAtUtc);
        Assert.Equal(@"C:\captures\capture-test\capture-synthesis.json", saved.LastCaptureSynthesisPath);
        Assert.Equal(1_234_567, saved.LastCaptureSynthesisBytes);
        Assert.Equal(98_765_432, saved.LastCaptureSynthesisTelemetryBytes);
        Assert.Equal(1_250, saved.LastCaptureSynthesisElapsedMilliseconds);
        Assert.Equal(1_080, saved.LastCaptureSynthesisProcessCpuMilliseconds);
        Assert.Equal(86.4, saved.LastCaptureSynthesisProcessCpuPercentOfOneCore.GetValueOrDefault(), 1);
        Assert.Equal(12_228, saved.LastCaptureSynthesisTotalFrameRecords);
        Assert.Equal(12_228, saved.LastCaptureSynthesisSampledFrameCount);
        Assert.Equal(1, saved.LastCaptureSynthesisSampleStride);
        Assert.Equal(334, saved.LastCaptureSynthesisFieldCount);
    }
}
