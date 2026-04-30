using System.Text.Json;
using TmrOverlay.Core.History;

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

        SessionHistoryAggregateBuilder.AddSummary(aggregate, summary, DateTimeOffset.UtcNow);

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
        var aggregate = await JsonSerializer.DeserializeAsync<HistoricalSessionAggregate>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return aggregate?.AggregateVersion == HistoricalDataVersions.AggregateVersion
            ? aggregate
            : null;
    }
}
