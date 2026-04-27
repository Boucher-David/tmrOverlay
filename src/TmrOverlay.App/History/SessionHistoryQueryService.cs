using System.Text.Json;

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
            return JsonSerializer.Deserialize<HistoricalSessionAggregate>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record SessionHistoryLookupResult(
    HistoricalComboIdentity Combo,
    HistoricalSessionAggregate? UserAggregate,
    HistoricalSessionAggregate? BaselineAggregate)
{
    public HistoricalSessionAggregate? PreferredAggregate => UserAggregate ?? BaselineAggregate;

    public string? PreferredAggregateSource => UserAggregate is not null
        ? "user"
        : BaselineAggregate is not null
            ? "baseline"
            : null;

    public bool HasAnyData => PreferredAggregate is not null;

    public static SessionHistoryLookupResult Empty(HistoricalComboIdentity combo)
    {
        return new SessionHistoryLookupResult(combo, UserAggregate: null, BaselineAggregate: null);
    }
}
