namespace TmrOverlay.App.Events;

internal sealed record AppEvent(
    DateTimeOffset TimestampUtc,
    string Name,
    IReadOnlyDictionary<string, string?> Properties);
