using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Telemetry;
using Xunit;

namespace TmrOverlay.App.Tests.Diagnostics;

public sealed class AppDiagnosticsStatusModelTests
{
    [Fact]
    public void From_TreatsWaitingForIRacingAsNeutralIdleState()
    {
        var model = AppDiagnosticsStatusModel.From(Snapshot());

        Assert.Equal(AppDiagnosticsSeverity.Neutral, model.Severity);
        Assert.Equal("Waiting for iRacing", model.StatusText);
        Assert.Equal("Waiting for iRacing", model.SupportStatusText);
        Assert.Equal("Not connected; start iRacing when ready", model.SessionStateText);
        Assert.Equal(
            "No active issue. Waiting is expected before iRacing is running.",
            model.CurrentIssueText);
    }

    [Fact]
    public void From_ReportsHealthyLiveAnalysisAsSuccess()
    {
        var now = DateTimeOffset.UtcNow;
        var model = AppDiagnosticsStatusModel.From(
            Snapshot(
                isConnected: true,
                isCapturing: true,
                frameCount: 1234,
                lastFrameCapturedAtUtc: now.AddMilliseconds(-500)),
            now);

        Assert.Equal(AppDiagnosticsSeverity.Success, model.Severity);
        Assert.Equal("Analyzing live telemetry", model.StatusText);
        Assert.Equal("Live telemetry", model.SupportStatusText);
        Assert.Equal("Receiving live telemetry (1,234 frames)", model.SessionStateText);
    }

    [Fact]
    public void From_ReportsRawCaptureWriterStallAsError()
    {
        var now = DateTimeOffset.UtcNow;
        var model = AppDiagnosticsStatusModel.From(
            Snapshot(
                isConnected: true,
                isCapturing: true,
                rawCaptureEnabled: true,
                frameCount: 20,
                writtenFrameCount: 0,
                lastFrameCapturedAtUtc: now),
            now);

        Assert.Equal(AppDiagnosticsSeverity.Error, model.Severity);
        Assert.Equal("Frames queued, not written", model.StatusText);
        Assert.Equal("Error", model.SupportStatusText);
        Assert.True(model.HasActiveIssue);
    }

    private static TelemetryCaptureStatusSnapshot Snapshot(
        bool isConnected = false,
        bool isCapturing = false,
        bool rawCaptureEnabled = false,
        bool rawCaptureActive = false,
        int frameCount = 0,
        int writtenFrameCount = 0,
        DateTimeOffset? lastFrameCapturedAtUtc = null)
    {
        return new TelemetryCaptureStatusSnapshot(
            IsConnected: isConnected,
            IsCapturing: isCapturing,
            RawCaptureEnabled: rawCaptureEnabled,
            RawCaptureActive: rawCaptureActive,
            CaptureRoot: null,
            CurrentCaptureDirectory: null,
            LastCaptureDirectory: null,
            FrameCount: frameCount,
            WrittenFrameCount: writtenFrameCount,
            DroppedFrameCount: 0,
            TelemetryFileBytes: null,
            CaptureStartedAtUtc: null,
            LastFrameCapturedAtUtc: lastFrameCapturedAtUtc,
            LastDiskWriteAtUtc: null,
            AppWarning: null,
            LastWarning: null,
            LastError: null,
            LastIssueAtUtc: null);
    }
}
