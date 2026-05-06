using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.History;
using Xunit;

namespace TmrOverlay.App.Tests.History;

public sealed class HistoryMaintenanceServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public async Task RunAsync_NormalizesLegacySummaryAndRebuildsAggregate()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-history-maintenance-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var options = CreateOptions(storage);
            var combo = new HistoricalComboIdentity
            {
                CarKey = "car-test",
                TrackKey = "track-test",
                SessionKey = "race"
            };
            var summaryPath = WriteLegacySummary(options.ResolvedUserHistoryRoot, combo);
            WriteAggregate(options.ResolvedUserHistoryRoot, combo, sessionCount: 999);
            var service = CreateService(options, storage);

            var manifest = await service.RunAsync(CancellationToken.None);

            Assert.NotNull(manifest);
            Assert.Equal(1, manifest.SummaryFilesScanned);
            Assert.Equal(1, manifest.SummaryFilesCompatible);
            Assert.Equal(1, manifest.SummaryFilesMigrated);
            Assert.Equal(0, manifest.SummaryFilesSkipped);
            Assert.Equal(1, manifest.SummaryFilesBackedUp);
            Assert.Equal(1, manifest.AggregateFilesRebuilt);

            var migratedSummary = JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
            Assert.Equal(HistoricalDataVersions.SummaryVersion, migratedSummary["summaryVersion"]!.GetValue<int>());
            Assert.Equal(HistoricalDataVersions.CollectionModelVersion, migratedSummary["collectionModelVersion"]!.GetValue<int>());

            var aggregate = ReadAggregate(options.ResolvedUserHistoryRoot, combo);
            Assert.NotNull(aggregate);
            Assert.Equal(HistoricalDataVersions.AggregateVersion, aggregate.AggregateVersion);
            Assert.Equal(1, aggregate.SessionCount);
            Assert.Equal(1, aggregate.BaselineSessionCount);
            Assert.Equal(12.5d, aggregate.FuelPerLapLiters.Mean);
            Assert.Single(Directory.EnumerateFiles(Path.Combine(options.ResolvedUserHistoryRoot, ".backups"), "*.json", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_SkipsCorruptSummaryAndWritesManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-history-maintenance-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var options = CreateOptions(storage);
            var corruptPath = Path.Combine(
                options.ResolvedUserHistoryRoot,
                "cars",
                "car-test",
                "tracks",
                "track-test",
                "sessions",
                "race",
                "summaries",
                "corrupt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)!);
            File.WriteAllText(corruptPath, "{");
            var service = CreateService(options, storage);

            var manifest = await service.RunAsync(CancellationToken.None);

            Assert.NotNull(manifest);
            Assert.Equal(1, manifest.SummaryFilesScanned);
            Assert.Equal(0, manifest.SummaryFilesCompatible);
            Assert.Equal(1, manifest.SummaryFilesSkipped);
            Assert.Equal(0, manifest.AggregateFilesRebuilt);
            Assert.Contains(manifest.Files, file =>
                file.Action == HistoryMaintenanceActions.Skipped
                && file.Reason == "corrupt_json");
            Assert.True(File.Exists(service.ManifestPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static HistoryMaintenanceService CreateService(
        SessionHistoryOptions options,
        AppStorageOptions storage)
    {
        return new HistoryMaintenanceService(
            options,
            new AppEventRecorder(storage),
            NullLogger<HistoryMaintenanceService>.Instance);
    }

    private static string WriteLegacySummary(string userRoot, HistoricalComboIdentity combo)
    {
        var sessionDirectory = SessionDirectory(userRoot, combo);
        var path = Path.Combine(sessionDirectory, "summaries", "capture-legacy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var summary = new HistoricalSessionSummary
        {
            SourceCaptureId = "capture-legacy",
            StartedAtUtc = DateTimeOffset.Parse("2026-04-30T12:00:00Z"),
            FinishedAtUtc = DateTimeOffset.Parse("2026-04-30T13:00:00Z"),
            Combo = combo,
            Car = new HistoricalCarIdentity(),
            Track = new HistoricalTrackIdentity(),
            Session = new HistoricalSessionIdentity { SessionType = "Race" },
            Conditions = new HistoricalConditions(),
            Metrics = new HistoricalSessionMetrics
            {
                SampleFrameCount = 100,
                CaptureDurationSeconds = 3600d,
                ValidGreenTimeSeconds = 3000d,
                ValidDistanceLaps = 4d,
                CompletedValidLaps = 4,
                FuelPerLapLiters = 12.5d,
                FuelPerHourLiters = 50d,
                AverageLapSeconds = 120d,
                MedianLapSeconds = 119d
            },
            Quality = new HistoricalDataQuality
            {
                Confidence = "high",
                ContributesToBaseline = true,
                Reasons = []
            }
        };
        var node = JsonNode.Parse(JsonSerializer.Serialize(summary, JsonOptions))!.AsObject();
        node.Remove("summaryVersion");
        node.Remove("collectionModelVersion");
        File.WriteAllText(path, node.ToJsonString(JsonOptions));
        return path;
    }

    private static void WriteAggregate(
        string userRoot,
        HistoricalComboIdentity combo,
        int sessionCount)
    {
        var path = Path.Combine(SessionDirectory(userRoot, combo), "aggregate.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var aggregate = new HistoricalSessionAggregate
        {
            Combo = combo,
            SessionCount = sessionCount
        };
        File.WriteAllText(path, JsonSerializer.Serialize(aggregate, JsonOptions));
    }

    private static HistoricalSessionAggregate? ReadAggregate(
        string userRoot,
        HistoricalComboIdentity combo)
    {
        var path = Path.Combine(SessionDirectory(userRoot, combo), "aggregate.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<HistoricalSessionAggregate>(stream, JsonOptions);
    }

    private static string SessionDirectory(
        string userRoot,
        HistoricalComboIdentity combo)
    {
        return Path.Combine(
            userRoot,
            "cars",
            combo.CarKey,
            "tracks",
            combo.TrackKey,
            "sessions",
            combo.SessionKey);
    }

    private static SessionHistoryOptions CreateOptions(AppStorageOptions storage)
    {
        return new SessionHistoryOptions
        {
            Enabled = true,
            ResolvedUserHistoryRoot = storage.UserHistoryRoot,
            ResolvedBaselineHistoryRoot = storage.BaselineHistoryRoot
        };
    }

    private static AppStorageOptions CreateStorage(string root)
    {
        return new AppStorageOptions
        {
            AppDataRoot = root,
            CaptureRoot = Path.Combine(root, "captures"),
            UserHistoryRoot = Path.Combine(root, "history", "user"),
            BaselineHistoryRoot = Path.Combine(root, "history", "baseline"),
            LogsRoot = Path.Combine(root, "logs"),
            SettingsRoot = Path.Combine(root, "settings"),
            DiagnosticsRoot = Path.Combine(root, "diagnostics"),
            TrackMapRoot = Path.Combine(root, "track-maps", "user"),
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }
}
