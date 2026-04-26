using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TmrOverlay.App.Logging;

internal sealed class LocalFileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, LocalFileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly LocalFileLogWriter _writer;

    public LocalFileLoggerProvider(LocalFileLoggerOptions options)
    {
        Options = options;
        _writer = new LocalFileLogWriter(options);
    }

    internal LocalFileLoggerOptions Options { get; }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, category => new LocalFileLogger(category, Options, _writer));
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
