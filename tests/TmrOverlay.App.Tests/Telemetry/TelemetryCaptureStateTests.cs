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
}
