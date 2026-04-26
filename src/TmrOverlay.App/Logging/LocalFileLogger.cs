using Microsoft.Extensions.Logging;

namespace TmrOverlay.App.Logging;

internal sealed class LocalFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LocalFileLoggerOptions _options;
    private readonly LocalFileLogWriter _writer;

    public LocalFileLogger(
        string categoryName,
        LocalFileLoggerOptions options,
        LocalFileLogWriter writer)
    {
        _categoryName = categoryName;
        _options = options;
        _writer = writer;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _options.Enabled
            && logLevel != LogLevel.None
            && logLevel >= _options.MinimumLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        _writer.Write(new LocalLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Level: logLevel,
            Category: _categoryName,
            EventId: eventId,
            Message: message,
            Exception: exception));
    }
}
