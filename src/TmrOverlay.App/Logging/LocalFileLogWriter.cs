using System.Text;

namespace TmrOverlay.App.Logging;

internal sealed class LocalFileLogWriter : IDisposable
{
    private const string LogFilePrefix = "tmroverlay";
    private readonly LocalFileLoggerOptions _options;
    private readonly object _sync = new();
    private bool _disposed;

    public LocalFileLogWriter(LocalFileLoggerOptions options)
    {
        _options = options;
        Directory.CreateDirectory(_options.ResolvedLogRoot);
    }

    public void Write(LocalLogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                Directory.CreateDirectory(_options.ResolvedLogRoot);
                var logPath = GetCurrentLogPath(entry.TimestampUtc);
                RotateIfNeeded(logPath);
                File.AppendAllText(logPath, Format(entry), Encoding.UTF8);
                PruneOldLogs();
            }
        }
        catch
        {
            // Local diagnostics should never interrupt telemetry collection.
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
        }
    }

    private string GetCurrentLogPath(DateTimeOffset timestampUtc)
    {
        return Path.Combine(
            _options.ResolvedLogRoot,
            $"{LogFilePrefix}-{timestampUtc:yyyyMMdd}.log");
    }

    private void RotateIfNeeded(string logPath)
    {
        if (!File.Exists(logPath))
        {
            return;
        }

        var fileInfo = new FileInfo(logPath);
        if (fileInfo.Length < _options.MaxFileBytes)
        {
            return;
        }

        var rotatedPath = Path.Combine(
            _options.ResolvedLogRoot,
            $"{Path.GetFileNameWithoutExtension(logPath)}-{DateTimeOffset.UtcNow:HHmmssfff}.log");

        File.Move(logPath, rotatedPath, overwrite: false);
    }

    private void PruneOldLogs()
    {
        var logs = Directory
            .EnumerateFiles(_options.ResolvedLogRoot, $"{LogFilePrefix}-*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(_options.RetainedFileCount)
            .ToArray();

        foreach (var log in logs)
        {
            TryDelete(log.FullName);
        }
    }

    private static string Format(LocalLogEntry entry)
    {
        var builder = new StringBuilder();
        builder
            .Append(entry.TimestampUtc.ToString("O"))
            .Append(" [")
            .Append(entry.Level)
            .Append("] ")
            .Append(entry.Category);

        if (entry.EventId.Id != 0 || !string.IsNullOrWhiteSpace(entry.EventId.Name))
        {
            builder
                .Append(" (")
                .Append(entry.EventId.Id);

            if (!string.IsNullOrWhiteSpace(entry.EventId.Name))
            {
                builder.Append(": ").Append(entry.EventId.Name);
            }

            builder.Append(')');
        }

        builder
            .Append(": ")
            .AppendLine(entry.Message);

        if (entry.Exception is not null)
        {
            builder.AppendLine(entry.Exception.ToString());
        }

        return builder.ToString();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Logging cleanup must never affect the live collector.
        }
    }
}
