using System.Text.Json;
using TmrOverlay.App.History;
using TmrOverlay.Core.History;
using Xunit;

namespace TmrOverlay.App.Tests.History;

public sealed class SessionHistoryQueryServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Lookup_IgnoresBaselineAggregateByDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-history-query-test", Guid.NewGuid().ToString("N"));
        try
        {
            var userRoot = Path.Combine(root, "user");
            var baselineRoot = Path.Combine(root, "baseline");
            var combo = new HistoricalComboIdentity
            {
                CarKey = "car-test",
                TrackKey = "track-test",
                SessionKey = "race"
            };
            var aggregate = new HistoricalSessionAggregate
            {
                Combo = combo,
                SessionCount = 1,
                BaselineSessionCount = 1
            };
            aggregate.FuelPerLapLiters.Add(12.5d);
            WriteAggregate(baselineRoot, combo, aggregate);

            var service = new SessionHistoryQueryService(new SessionHistoryOptions
            {
                Enabled = true,
                ResolvedUserHistoryRoot = userRoot,
                ResolvedBaselineHistoryRoot = baselineRoot
            });

            var result = service.Lookup(combo);

            Assert.Null(result.UserAggregate);
            Assert.Null(result.BaselineAggregate);
            Assert.Null(result.PreferredAggregate);
            Assert.False(result.HasAnyData);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Lookup_ReturnsBaselineAggregateWhenBaselineHistoryIsEnabled()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-history-query-test", Guid.NewGuid().ToString("N"));
        try
        {
            var userRoot = Path.Combine(root, "user");
            var baselineRoot = Path.Combine(root, "baseline");
            var combo = new HistoricalComboIdentity
            {
                CarKey = "car-test",
                TrackKey = "track-test",
                SessionKey = "race"
            };
            var aggregate = new HistoricalSessionAggregate
            {
                Combo = combo,
                SessionCount = 1,
                BaselineSessionCount = 1
            };
            aggregate.FuelPerLapLiters.Add(12.5d);
            WriteAggregate(baselineRoot, combo, aggregate);

            var service = new SessionHistoryQueryService(new SessionHistoryOptions
            {
                Enabled = true,
                UseBaselineHistory = true,
                ResolvedUserHistoryRoot = userRoot,
                ResolvedBaselineHistoryRoot = baselineRoot
            });

            var result = service.Lookup(combo);

            Assert.Null(result.UserAggregate);
            Assert.NotNull(result.BaselineAggregate);
            Assert.Same(result.BaselineAggregate, result.PreferredAggregate);
            Assert.True(result.HasAnyData);
            Assert.Equal(12.5d, result.PreferredAggregate!.FuelPerLapLiters.Mean);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Lookup_IgnoresIncompatibleAggregateVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-history-query-test", Guid.NewGuid().ToString("N"));
        try
        {
            var userRoot = Path.Combine(root, "user");
            var baselineRoot = Path.Combine(root, "baseline");
            var combo = new HistoricalComboIdentity
            {
                CarKey = "car-test",
                TrackKey = "track-test",
                SessionKey = "race"
            };
            var aggregate = new HistoricalSessionAggregate
            {
                AggregateVersion = HistoricalDataVersions.AggregateVersion + 1,
                Combo = combo,
                SessionCount = 1,
                BaselineSessionCount = 1
            };
            aggregate.FuelPerLapLiters.Add(12.5d);
            WriteAggregate(userRoot, combo, aggregate);

            var service = new SessionHistoryQueryService(new SessionHistoryOptions
            {
                Enabled = true,
                ResolvedUserHistoryRoot = userRoot,
                ResolvedBaselineHistoryRoot = baselineRoot
            });

            var result = service.Lookup(combo);

            Assert.Null(result.UserAggregate);
            Assert.Null(result.PreferredAggregate);
            Assert.False(result.HasAnyData);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void WriteAggregate(
        string root,
        HistoricalComboIdentity combo,
        HistoricalSessionAggregate aggregate)
    {
        var path = Path.Combine(
            root,
            "cars",
            combo.CarKey,
            "tracks",
            combo.TrackKey,
            "sessions",
            combo.SessionKey,
            "aggregate.json");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(aggregate, JsonOptions));
    }
}
