using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.History;

internal sealed class HistoryMaintenanceService : IHostedService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly IReadOnlyList<IHistorySummaryMigration> SummaryMigrations = [];

    private readonly SessionHistoryOptions _options;
    private readonly AppEventRecorder _events;
    private readonly ILogger<HistoryMaintenanceService> _logger;
    private readonly CancellationTokenSource _maintenanceCancellation = new();
    private Task _maintenanceTask = Task.CompletedTask;

    public HistoryMaintenanceService(
        SessionHistoryOptions options,
        AppEventRecorder events,
        ILogger<HistoryMaintenanceService> logger)
    {
        _options = options;
        _events = events;
        _logger = logger;
    }

    public string ManifestPath => Path.Combine(_options.ResolvedUserHistoryRoot, ".maintenance", "manifest.json");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        _maintenanceTask = Task.Run(
            () => RunStartupMaintenanceAsync(_maintenanceCancellation.Token),
            CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _maintenanceCancellation.Cancel();
        try
        {
            await _maintenanceTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _maintenanceCancellation.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        _maintenanceCancellation.Cancel();
        _maintenanceCancellation.Dispose();
    }

    private async Task RunStartupMaintenanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "History maintenance failed.");
            _events.Record("history_maintenance_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.Message
            });
        }
    }

    public async Task<HistoryMaintenanceManifest?> RunAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var runAtUtc = DateTimeOffset.UtcNow;
        var files = new List<HistoryMaintenanceFileResult>();
        var compatibleSummariesBySessionDirectory = new Dictionary<string, List<HistoricalSessionSummary>>(StringComparer.OrdinalIgnoreCase);
        var summaryFilesScanned = 0;
        var summaryFilesCompatible = 0;
        var summaryFilesMigrated = 0;
        var summaryFilesSkipped = 0;
        var summaryFilesBackedUp = 0;
        var aggregateFilesRebuilt = 0;

        Directory.CreateDirectory(_options.ResolvedUserHistoryRoot);

        foreach (var summaryPath in EnumerateSummaryFiles(_options.ResolvedUserHistoryRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            summaryFilesScanned++;
            var relativePath = RelativePath(summaryPath);
            var result = await ReadAndMigrateSummaryAsync(summaryPath, cancellationToken).ConfigureAwait(false);
            if (result.Summary is null)
            {
                summaryFilesSkipped++;
                files.Add(new HistoryMaintenanceFileResult(
                    relativePath,
                    HistoryMaintenanceActions.Skipped,
                    result.Reason,
                    result.SummaryVersion,
                    result.CollectionModelVersion));
                continue;
            }

            summaryFilesCompatible++;
            if (result.UpdatedJson is not null)
            {
                BackupFile(summaryPath, runAtUtc);
                summaryFilesBackedUp++;
                await WriteTextAtomicAsync(summaryPath, result.UpdatedJson, cancellationToken).ConfigureAwait(false);
                summaryFilesMigrated++;
                files.Add(new HistoryMaintenanceFileResult(
                    relativePath,
                    HistoryMaintenanceActions.Migrated,
                    result.Reason,
                    HistoricalDataVersions.SummaryVersion,
                    HistoricalDataVersions.CollectionModelVersion));
            }
            else
            {
                files.Add(new HistoryMaintenanceFileResult(
                    relativePath,
                    HistoryMaintenanceActions.Compatible,
                    null,
                    result.SummaryVersion,
                    result.CollectionModelVersion));
            }

            var sessionDirectory = Directory.GetParent(Path.GetDirectoryName(summaryPath)!)!.FullName;
            if (!compatibleSummariesBySessionDirectory.TryGetValue(sessionDirectory, out var summaries))
            {
                summaries = [];
                compatibleSummariesBySessionDirectory[sessionDirectory] = summaries;
            }

            summaries.Add(result.Summary);
        }

        foreach (var session in compatibleSummariesBySessionDirectory.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var aggregate = SessionHistoryAggregateBuilder.Rebuild(session.Value, runAtUtc);
            var aggregatePath = Path.Combine(session.Key, "aggregate.json");
            await WriteTextAtomicAsync(
                    aggregatePath,
                    JsonSerializer.Serialize(aggregate, JsonOptions),
                    cancellationToken)
                .ConfigureAwait(false);
            aggregateFilesRebuilt++;
            files.Add(new HistoryMaintenanceFileResult(
                RelativePath(aggregatePath),
                HistoryMaintenanceActions.AggregateRebuilt,
                null,
                null,
                null));
        }

        var manifest = new HistoryMaintenanceManifest
        {
            LastRunAtUtc = runAtUtc,
            AppVersion = AppVersionInfo.Current,
            SummaryFilesScanned = summaryFilesScanned,
            SummaryFilesCompatible = summaryFilesCompatible,
            SummaryFilesMigrated = summaryFilesMigrated,
            SummaryFilesSkipped = summaryFilesSkipped,
            SummaryFilesBackedUp = summaryFilesBackedUp,
            AggregateFilesRebuilt = aggregateFilesRebuilt,
            Files = files
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.Action, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        await WriteTextAtomicAsync(
                ManifestPath,
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        _events.Record("history_maintenance_completed", new Dictionary<string, string?>
        {
            ["summaryFilesScanned"] = summaryFilesScanned.ToString(),
            ["summaryFilesMigrated"] = summaryFilesMigrated.ToString(),
            ["summaryFilesSkipped"] = summaryFilesSkipped.ToString(),
            ["aggregateFilesRebuilt"] = aggregateFilesRebuilt.ToString()
        });
        _logger.LogInformation(
            "History maintenance scanned {SummaryFilesScanned} summaries, migrated {SummaryFilesMigrated}, skipped {SummaryFilesSkipped}, and rebuilt {AggregateFilesRebuilt} aggregates.",
            summaryFilesScanned,
            summaryFilesMigrated,
            summaryFilesSkipped,
            aggregateFilesRebuilt);

        return manifest;
    }

    private async Task<SummaryMaintenanceResult> ReadAndMigrateSummaryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        JsonObject node;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            node = JsonNode.Parse(json) as JsonObject
                ?? throw new JsonException("Summary root was not a JSON object.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return SummaryMaintenanceResult.Skip("corrupt_json", null, null);
        }

        var summaryVersion = ReadInt32(node, "summaryVersion");
        var collectionModelVersion = ReadInt32(node, "collectionModelVersion");
        var changed = false;
        var reason = (string?)null;

        if (summaryVersion is null)
        {
            node["summaryVersion"] = HistoricalDataVersions.SummaryVersion;
            summaryVersion = HistoricalDataVersions.SummaryVersion;
            changed = true;
            reason = AppendReason(reason, "added_summary_version");
        }

        if (collectionModelVersion is null)
        {
            node["collectionModelVersion"] = HistoricalDataVersions.CollectionModelVersion;
            collectionModelVersion = HistoricalDataVersions.CollectionModelVersion;
            changed = true;
            reason = AppendReason(reason, "added_collection_model_version");
        }

        if (summaryVersion > HistoricalDataVersions.SummaryVersion)
        {
            return SummaryMaintenanceResult.Skip("future_summary_version", summaryVersion, collectionModelVersion);
        }

        if (collectionModelVersion > HistoricalDataVersions.CollectionModelVersion)
        {
            return SummaryMaintenanceResult.Skip("future_collection_model_version", summaryVersion, collectionModelVersion);
        }

        while (summaryVersion < HistoricalDataVersions.SummaryVersion)
        {
            var migration = SummaryMigrations.FirstOrDefault(migration => migration.FromSummaryVersion == summaryVersion);
            if (migration is null)
            {
                return SummaryMaintenanceResult.Skip("unsupported_summary_version", summaryVersion, collectionModelVersion);
            }

            node = migration.Migrate(node);
            summaryVersion = migration.ToSummaryVersion;
            node["summaryVersion"] = summaryVersion;
            changed = true;
            reason = AppendReason(reason, $"summary_v{migration.FromSummaryVersion}_to_v{migration.ToSummaryVersion}");
        }

        if (collectionModelVersion < HistoricalDataVersions.CollectionModelVersion)
        {
            return SummaryMaintenanceResult.Skip("unsupported_collection_model_version", summaryVersion, collectionModelVersion);
        }

        HistoricalSessionSummary? summary;
        try
        {
            summary = node.Deserialize<HistoricalSessionSummary>(JsonOptions);
        }
        catch (JsonException)
        {
            return SummaryMaintenanceResult.Skip("invalid_summary_shape", summaryVersion, collectionModelVersion);
        }

        if (summary is null)
        {
            return SummaryMaintenanceResult.Skip("invalid_summary_shape", summaryVersion, collectionModelVersion);
        }

        var updatedJson = changed
            ? node.ToJsonString(JsonOptions)
            : null;
        return SummaryMaintenanceResult.Compatible(
            summary,
            updatedJson,
            reason,
            summaryVersion,
            collectionModelVersion);
    }

    private IEnumerable<string> EnumerateSummaryFiles(string historyRoot)
    {
        var carsRoot = Path.Combine(historyRoot, "cars");
        if (!Directory.Exists(carsRoot))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(carsRoot, "*.json", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "summaries", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private void BackupFile(string path, DateTimeOffset runAtUtc)
    {
        var relativePath = RelativePath(path);
        var backupPath = Path.Combine(
            _options.ResolvedUserHistoryRoot,
            ".backups",
            runAtUtc.ToString("yyyyMMdd-HHmmss"),
            relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        if (!File.Exists(backupPath))
        {
            File.Copy(path, backupPath, overwrite: false);
        }
    }

    private async Task WriteTextAtomicAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    private string RelativePath(string path)
    {
        return Path.GetRelativePath(_options.ResolvedUserHistoryRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static int? ReadInt32(JsonObject node, string propertyName)
    {
        return node.TryGetPropertyValue(propertyName, out var value)
            && value is JsonValue jsonValue
            && jsonValue.TryGetValue<int>(out var number)
                ? number
                : null;
    }

    private static string AppendReason(string? existing, string reason)
    {
        return string.IsNullOrWhiteSpace(existing)
            ? reason
            : $"{existing},{reason}";
    }

    private interface IHistorySummaryMigration
    {
        int FromSummaryVersion { get; }

        int ToSummaryVersion { get; }

        JsonObject Migrate(JsonObject summary);
    }

    private sealed record SummaryMaintenanceResult(
        HistoricalSessionSummary? Summary,
        string? UpdatedJson,
        string? Reason,
        int? SummaryVersion,
        int? CollectionModelVersion)
    {
        public static SummaryMaintenanceResult Compatible(
            HistoricalSessionSummary summary,
            string? updatedJson,
            string? reason,
            int? summaryVersion,
            int? collectionModelVersion)
        {
            return new SummaryMaintenanceResult(summary, updatedJson, reason, summaryVersion, collectionModelVersion);
        }

        public static SummaryMaintenanceResult Skip(
            string reason,
            int? summaryVersion,
            int? collectionModelVersion)
        {
            return new SummaryMaintenanceResult(null, null, reason, summaryVersion, collectionModelVersion);
        }
    }
}
