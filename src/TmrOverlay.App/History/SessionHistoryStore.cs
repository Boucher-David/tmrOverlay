using System.Text.Json;

namespace TmrOverlay.App.History;

internal sealed class SessionHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly SessionHistoryOptions _options;

    public SessionHistoryStore(SessionHistoryOptions options)
    {
        _options = options;
    }

    public async Task SaveAsync(HistoricalSessionSummary summary, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var sessionDirectory = GetSessionDirectory(summary);
        var summariesDirectory = Path.Combine(sessionDirectory, "summaries");

        Directory.CreateDirectory(summariesDirectory);

        var summaryPath = Path.Combine(
            summariesDirectory,
            $"{SessionHistoryPath.Slug(summary.SourceCaptureId)}.json");

        await File.WriteAllTextAsync(
                summaryPath,
                JsonSerializer.Serialize(summary, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        await UpdateAggregateAsync(sessionDirectory, summary, cancellationToken).ConfigureAwait(false);
    }

    private string GetSessionDirectory(HistoricalSessionSummary summary)
    {
        return Path.Combine(
            _options.ResolvedHistoryRoot,
            "cars",
            summary.Combo.CarKey,
            "tracks",
            summary.Combo.TrackKey,
            "sessions",
            summary.Combo.SessionKey);
    }

    private static async Task UpdateAggregateAsync(
        string sessionDirectory,
        HistoricalSessionSummary summary,
        CancellationToken cancellationToken)
    {
        var aggregatePath = Path.Combine(sessionDirectory, "aggregate.json");
        var aggregate = await ReadAggregateAsync(aggregatePath, cancellationToken).ConfigureAwait(false)
            ?? new HistoricalSessionAggregate();

        aggregate.AggregateVersion = 1;
        aggregate.Combo = summary.Combo;
        aggregate.Car = summary.Car;
        aggregate.Track = summary.Track;
        aggregate.Session = summary.Session;
        aggregate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        aggregate.SessionCount++;

        if (summary.Quality.ContributesToBaseline)
        {
            aggregate.BaselineSessionCount++;
            aggregate.FuelPerLapLiters.Add(summary.Metrics.FuelPerLapLiters);
            aggregate.FuelPerHourLiters.Add(summary.Metrics.FuelPerHourLiters);
            aggregate.AverageLapSeconds.Add(summary.Metrics.AverageLapSeconds);
            aggregate.MedianLapSeconds.Add(summary.Metrics.MedianLapSeconds);
            aggregate.PitRoadEntryCount.Add(summary.Metrics.PitRoadEntryCount);
            aggregate.PitServiceCount.Add(summary.Metrics.PitServiceCount);
            aggregate.AverageStintLaps.Add(summary.Metrics.AverageStintLaps);
            aggregate.AverageStintSeconds.Add(summary.Metrics.AverageStintSeconds);
            aggregate.AverageStintFuelPerLapLiters.Add(summary.Metrics.AverageStintFuelPerLapLiters);
            foreach (var stint in summary.Stints)
            {
                if (stint.DistanceLaps <= 0d)
                {
                    continue;
                }

                if (IsLocalDriverStint(stint.DriverRole))
                {
                    aggregate.LocalDriverStintLaps.Add(stint.DistanceLaps);
                }
                else if (IsTeammateDriverStint(stint.DriverRole))
                {
                    aggregate.TeammateDriverStintLaps.Add(stint.DistanceLaps);
                }
            }

            aggregate.AveragePitLaneSeconds.Add(summary.Metrics.AveragePitLaneSeconds);
            aggregate.AveragePitStallSeconds.Add(summary.Metrics.AveragePitStallSeconds);
            aggregate.AveragePitServiceSeconds.Add(summary.Metrics.AveragePitServiceSeconds);
            aggregate.ObservedFuelFillRateLitersPerSecond.Add(summary.Metrics.ObservedFuelFillRateLitersPerSecond);
            aggregate.AverageTireChangePitServiceSeconds.Add(summary.Metrics.AverageTireChangePitServiceSeconds);
            aggregate.AverageNoTirePitServiceSeconds.Add(summary.Metrics.AverageNoTirePitServiceSeconds);
        }

        await File.WriteAllTextAsync(
                aggregatePath,
                JsonSerializer.Serialize(aggregate, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<HistoricalSessionAggregate?> ReadAggregateAsync(
        string aggregatePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(aggregatePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(aggregatePath);
        return await JsonSerializer.DeserializeAsync<HistoricalSessionAggregate>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLocalDriverStint(string driverRole)
    {
        return driverRole.Contains("local", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTeammateDriverStint(string driverRole)
    {
        return driverRole.Contains("team", StringComparison.OrdinalIgnoreCase)
            || driverRole.Contains("teammate", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class HistoricalSessionAggregate
{
    public int AggregateVersion { get; set; } = 1;

    public HistoricalComboIdentity? Combo { get; set; }

    public HistoricalCarIdentity? Car { get; set; }

    public HistoricalTrackIdentity? Track { get; set; }

    public HistoricalSessionIdentity? Session { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public int SessionCount { get; set; }

    public int BaselineSessionCount { get; set; }

    public RunningHistoricalMetric FuelPerLapLiters { get; set; } = new();

    public RunningHistoricalMetric FuelPerHourLiters { get; set; } = new();

    public RunningHistoricalMetric AverageLapSeconds { get; set; } = new();

    public RunningHistoricalMetric MedianLapSeconds { get; set; } = new();

    public RunningHistoricalMetric PitRoadEntryCount { get; set; } = new();

    public RunningHistoricalMetric PitServiceCount { get; set; } = new();

    public RunningHistoricalMetric AverageStintLaps { get; set; } = new();

    public RunningHistoricalMetric AverageStintSeconds { get; set; } = new();

    public RunningHistoricalMetric AverageStintFuelPerLapLiters { get; set; } = new();

    public RunningHistoricalMetric LocalDriverStintLaps { get; set; } = new();

    public RunningHistoricalMetric TeammateDriverStintLaps { get; set; } = new();

    public RunningHistoricalMetric AveragePitLaneSeconds { get; set; } = new();

    public RunningHistoricalMetric AveragePitStallSeconds { get; set; } = new();

    public RunningHistoricalMetric AveragePitServiceSeconds { get; set; } = new();

    public RunningHistoricalMetric ObservedFuelFillRateLitersPerSecond { get; set; } = new();

    public RunningHistoricalMetric AverageTireChangePitServiceSeconds { get; set; } = new();

    public RunningHistoricalMetric AverageNoTirePitServiceSeconds { get; set; } = new();
}

internal sealed class RunningHistoricalMetric
{
    public int SampleCount { get; set; }

    public double? Mean { get; set; }

    public double? Minimum { get; set; }

    public double? Maximum { get; set; }

    public void Add(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return;
        }

        if (SampleCount == 0)
        {
            SampleCount = 1;
            Mean = value;
            Minimum = value;
            Maximum = value;
            return;
        }

        Mean = ((Mean ?? 0d) * SampleCount + value.Value) / (SampleCount + 1);
        Minimum = Math.Min(Minimum ?? value.Value, value.Value);
        Maximum = Math.Max(Maximum ?? value.Value, value.Value);
        SampleCount++;
    }
}
