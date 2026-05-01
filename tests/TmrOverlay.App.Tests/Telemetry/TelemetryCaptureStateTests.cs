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
    public void SetRawCaptureEnabled_RejectsDisableWhileRawCaptureIsActive()
    {
        var state = new TelemetryCaptureState();
        state.SetRawCaptureEnabled(true);
        state.MarkCaptureStarted("capture-active", DateTimeOffset.UtcNow);

        var accepted = state.SetRawCaptureEnabled(false);
        var snapshot = state.Snapshot();

        Assert.False(accepted);
        Assert.True(snapshot.RawCaptureEnabled);
        Assert.True(snapshot.RawCaptureActive);
        Assert.NotNull(snapshot.LastWarning);
        Assert.Contains("already active", snapshot.LastWarning!);
    }

    [Fact]
    public void HistoryFinalizationState_SurfacesBackgroundSaveProgress()
    {
        var state = new TelemetryCaptureState();
        var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2);
        var savedAtUtc = DateTimeOffset.UtcNow;

        state.MarkHistoryFinalizationStarted(startedAtUtc);
        var saving = state.Snapshot();

        Assert.True(saving.IsHistoryFinalizing);
        Assert.Equal(startedAtUtc, saving.HistoryFinalizationStartedAtUtc);

        state.MarkHistorySummarySaved("GT3 / Nurburgring / Race", savedAtUtc);
        state.MarkHistoryFinalizationStopped();
        var saved = state.Snapshot();

        Assert.False(saved.IsHistoryFinalizing);
        Assert.Null(saved.HistoryFinalizationStartedAtUtc);
        Assert.Equal(1, saved.HistorySummarySaveCount);
        Assert.Equal(savedAtUtc, saved.LastHistorySavedAtUtc);
        Assert.Equal("GT3 / Nurburgring / Race", saved.LastHistorySummaryLabel);
    }
}
