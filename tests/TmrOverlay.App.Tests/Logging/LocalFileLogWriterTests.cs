using Microsoft.Extensions.Logging;
using TmrOverlay.App.Logging;
using Xunit;

namespace TmrOverlay.App.Tests.Logging;

public sealed class LocalFileLogWriterTests
{
    [Fact]
    public void Write_CreatesLogFile()
    {
        var logRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-logs-test", Guid.NewGuid().ToString("N"));
        try
        {
            using var writer = new LocalFileLogWriter(new LocalFileLoggerOptions
            {
                ResolvedLogRoot = logRoot
            });

            writer.Write(new LocalLogEntry(
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: LogLevel.Information,
                Category: "Test",
                EventId: new EventId(),
                Message: "hello logs",
                Exception: null));

            var logFile = Assert.Single(Directory.EnumerateFiles(logRoot, "tmroverlay-*.log"));
            Assert.Contains("hello logs", File.ReadAllText(logFile));
        }
        finally
        {
            if (Directory.Exists(logRoot))
            {
                Directory.Delete(logRoot, recursive: true);
            }
        }
    }
}
