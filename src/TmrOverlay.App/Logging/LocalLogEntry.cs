using Microsoft.Extensions.Logging;

namespace TmrOverlay.App.Logging;

internal sealed record LocalLogEntry(
    DateTimeOffset TimestampUtc,
    LogLevel Level,
    string Category,
    EventId EventId,
    string Message,
    Exception? Exception);
