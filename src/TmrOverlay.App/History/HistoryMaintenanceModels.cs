using TmrOverlay.Core.AppInfo;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.History;

internal sealed class HistoryMaintenanceManifest
{
    public int ManifestVersion { get; init; } = 1;

    public DateTimeOffset LastRunAtUtc { get; init; }

    public AppVersionInfo? AppVersion { get; init; }

    public int CurrentSummaryVersion { get; init; } = HistoricalDataVersions.SummaryVersion;

    public int CurrentCollectionModelVersion { get; init; } = HistoricalDataVersions.CollectionModelVersion;

    public int CurrentAggregateVersion { get; init; } = HistoricalDataVersions.AggregateVersion;

    public int SummaryFilesScanned { get; init; }

    public int SummaryFilesCompatible { get; init; }

    public int SummaryFilesMigrated { get; init; }

    public int SummaryFilesSkipped { get; init; }

    public int SummaryFilesBackedUp { get; init; }

    public int AggregateFilesRebuilt { get; init; }

    public IReadOnlyList<HistoryMaintenanceFileResult> Files { get; init; } = [];
}

internal sealed record HistoryMaintenanceFileResult(
    string RelativePath,
    string Action,
    string? Reason,
    int? SummaryVersion,
    int? CollectionModelVersion);

internal static class HistoryMaintenanceActions
{
    public const string Compatible = "compatible";
    public const string Migrated = "migrated";
    public const string Skipped = "skipped";
    public const string AggregateRebuilt = "aggregate_rebuilt";
}
