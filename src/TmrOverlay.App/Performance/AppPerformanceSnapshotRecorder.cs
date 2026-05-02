using System.Text.Json;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Performance;

internal sealed class AppPerformanceSnapshotRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppStorageOptions _storageOptions;
    private readonly object _sync = new();

    public AppPerformanceSnapshotRecorder(AppStorageOptions storageOptions)
    {
        _storageOptions = storageOptions;
    }

    public string PerformanceLogsRoot => Path.Combine(_storageOptions.LogsRoot, "performance");

    public void Record(AppPerformanceSnapshot snapshot)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(PerformanceLogsRoot);
                var path = Path.Combine(PerformanceLogsRoot, $"performance-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");
                File.AppendAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine);
            }
        }
        catch
        {
            // Performance diagnostics must never interfere with the live collector.
        }
    }
}
