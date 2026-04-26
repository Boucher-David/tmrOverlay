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
