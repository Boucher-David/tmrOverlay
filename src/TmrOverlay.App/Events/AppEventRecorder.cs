using System.Text.Json;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Events;

internal sealed class AppEventRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppStorageOptions _storageOptions;
    private readonly TelemetryDiagnosticContext _diagnosticContext;
    private readonly object _sync = new();

    public AppEventRecorder(
        AppStorageOptions storageOptions,
        TelemetryDiagnosticContext? diagnosticContext = null)
    {
        _storageOptions = storageOptions;
        _diagnosticContext = diagnosticContext ?? new TelemetryDiagnosticContext();
    }

    public void Record(
        string name,
        IReadOnlyDictionary<string, string?>? properties = null,
        string severity = "info")
    {
        var normalizedProperties = NormalizeProperties(properties);
        var appEvent = new AppEvent(
            EventVersion: 2,
            TimestampUtc: DateTimeOffset.UtcNow,
            Name: name,
            Severity: string.IsNullOrWhiteSpace(severity) ? "info" : severity,
            Properties: normalizedProperties);

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

    private IReadOnlyDictionary<string, string?> NormalizeProperties(
        IReadOnlyDictionary<string, string?>? properties)
    {
        var normalized = properties is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(properties, StringComparer.OrdinalIgnoreCase);
        normalized.TryAdd("appRunId", _diagnosticContext.AppRunId);
        return normalized;
    }
}
