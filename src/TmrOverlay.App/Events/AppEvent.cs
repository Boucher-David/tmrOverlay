namespace TmrOverlay.App.Events;

internal sealed record AppEvent(
    int EventVersion,
    DateTimeOffset TimestampUtc,
    string Name,
    string Severity,
    IReadOnlyDictionary<string, string?> Properties);
