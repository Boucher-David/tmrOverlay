using System.Text.Json;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Events;

internal sealed class AppEventRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppStorageOptions _storageOptions;
    private readonly object _sync = new();

    public AppEventRecorder(AppStorageOptions storageOptions)
    {
        _storageOptions = storageOptions;
    }

    public void Record(string name, IReadOnlyDictionary<string, string?>? properties = null)
    {
        var appEvent = new AppEvent(
            TimestampUtc: DateTimeOffset.UtcNow,
            Name: name,
            Properties: properties ?? new Dictionary<string, string?>());

        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_storageOptions.EventsRoot);
                var eventPath = Path.Combine(_storageOptions.EventsRoot, $"events-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");
                File.AppendAllText(eventPath, JsonSerializer.Serialize(appEvent, JsonOptions) + Environment.NewLine);
            }
        }
        catch
        {
            // Event breadcrumbs are useful for triage, but must not affect the live app.
        }
    }
}
