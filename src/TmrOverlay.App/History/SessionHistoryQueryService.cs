using System.Text.Json;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.History;

internal sealed class SessionHistoryQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SessionHistoryOptions _options;

    public SessionHistoryQueryService(SessionHistoryOptions options)
    {
        _options = options;
    }

    public SessionHistoryLookupResult Lookup(HistoricalComboIdentity combo)
    {
        if (!_options.Enabled)
        {
            return SessionHistoryLookupResult.Empty(combo);
        }

        var userAggregate = ReadAggregate(_options.ResolvedUserHistoryRoot, combo);
        var baselineAggregate = _options.UseBaselineHistory
            ? ReadAggregate(_options.ResolvedBaselineHistoryRoot, combo)
            : null;
        return new SessionHistoryLookupResult(combo, userAggregate, baselineAggregate);
    }

    private static HistoricalSessionAggregate? ReadAggregate(string root, HistoricalComboIdentity combo)
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

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var aggregate = JsonSerializer.Deserialize<HistoricalSessionAggregate>(stream, JsonOptions);
            return aggregate?.AggregateVersion == HistoricalDataVersions.AggregateVersion
                ? aggregate
                : null;
        }
        catch
        {
            return null;
        }
    }
}
