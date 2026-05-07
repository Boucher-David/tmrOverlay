using TmrOverlay.App.Localhost;
using Xunit;

namespace TmrOverlay.App.Tests.Localhost;

public sealed class LocalhostOverlayStateTests
{
    [Fact]
    public void Snapshot_RecordsLifecycleAndRequestCounters()
    {
        var state = new LocalhostOverlayState(new LocalhostOverlayOptions
        {
            Enabled = true,
            Port = 9123
        });

        state.RecordStartAttempted();
        state.RecordStarted();
        state.RecordRequest("health", "GET", "/health", 200, TimeSpan.FromMilliseconds(2));
        state.RecordRequest("not_found", "GET", "/missing", 404, TimeSpan.FromMilliseconds(3));

        var snapshot = state.Snapshot();

        Assert.True(snapshot.Enabled);
        Assert.Equal(9123, snapshot.Port);
        Assert.Equal("http://localhost:9123/", snapshot.Prefix);
        Assert.Equal("listening", snapshot.Status);
        Assert.Equal(2L, snapshot.TotalRequests);
        Assert.Equal(1L, snapshot.SuccessfulRequests);
        Assert.Equal(1L, snapshot.FailedRequests);
        Assert.Equal(1L, snapshot.RouteCounts["health"]);
        Assert.Equal(1L, snapshot.RouteCounts["not_found"]);
        Assert.Equal(1L, snapshot.StatusCodeCounts["200"]);
        Assert.Equal(1L, snapshot.StatusCodeCounts["404"]);
        Assert.Equal("/missing", snapshot.LastRequestPath);
        Assert.Equal(404, snapshot.LastRequestStatusCode);
        Assert.True(snapshot.HasRecentRequests);
        Assert.NotNull(snapshot.LastRequestAgeSeconds);
    }

    [Fact]
    public void Snapshot_DefaultsToDisabledWhenLocalhostIsDisabled()
    {
        var state = new LocalhostOverlayState(new LocalhostOverlayOptions());

        var snapshot = state.Snapshot();

        Assert.False(snapshot.Enabled);
        Assert.Equal("disabled", snapshot.Status);
        Assert.Equal(0L, snapshot.TotalRequests);
        Assert.False(snapshot.HasRecentRequests);
        Assert.Null(snapshot.LastRequestAgeSeconds);
    }
}
