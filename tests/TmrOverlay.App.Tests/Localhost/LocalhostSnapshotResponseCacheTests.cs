using System.Text;
using System.Text.Json;
using TmrOverlay.App.Localhost;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Localhost;

public sealed class LocalhostSnapshotResponseCacheTests
{
    [Fact]
    public void GetOrCreate_ReusesSerializedSnapshotForSameLiveSequence()
    {
        var cache = new LocalhostSnapshotResponseCache(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var firstSnapshot = LiveTelemetrySnapshot.Empty with { Sequence = 11 };

        var first = cache.GetOrCreate(firstSnapshot, DateTimeOffset.Parse("2026-05-07T12:00:00Z"));
        var second = cache.GetOrCreate(firstSnapshot, DateTimeOffset.Parse("2026-05-07T12:00:01Z"));
        var next = cache.GetOrCreate(firstSnapshot with { Sequence = 12 }, DateTimeOffset.Parse("2026-05-07T12:00:02Z"));

        Assert.Same(first, second);
        Assert.NotSame(first, next);
        Assert.Contains("\"sequence\":11", Encoding.UTF8.GetString(first));
        Assert.Contains("\"sequence\":12", Encoding.UTF8.GetString(next));
    }
}
