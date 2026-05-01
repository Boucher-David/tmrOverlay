namespace TmrOverlay.App.Diagnostics;

internal sealed class TelemetryDiagnosticContext
{
    public string AppRunId { get; } = BuildId("run", DateTimeOffset.UtcNow);

    public string NewCollectionId(DateTimeOffset startedAtUtc)
    {
        return BuildId("collection", startedAtUtc);
    }

    private static string BuildId(string prefix, DateTimeOffset timestampUtc)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}-{timestampUtc:yyyyMMdd-HHmmss-fff}-{unique}";
    }
}
