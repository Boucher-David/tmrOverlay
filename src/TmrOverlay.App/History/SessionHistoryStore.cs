using System.Text.Json;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.History;

internal sealed class SessionHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly SessionHistoryOptions _options;

    public SessionHistoryStore(SessionHistoryOptions options)
    {
        _options = options;
    }

    public async Task<HistoricalSessionGroup?> SaveAsync(
        HistoricalSessionSummary summary,
        CancellationToken cancellationToken)
    {
        return await SaveAsync(
                summary,
                HistoricalSessionSegmentContext.Normal("unknown"),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<HistoricalSessionGroup?> SaveAsync(
        HistoricalSessionSummary summary,
        HistoricalSessionSegmentContext segmentContext,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var sessionDirectory = GetSessionDirectory(summary);
        var summariesDirectory = Path.Combine(sessionDirectory, "summaries");

        Directory.CreateDirectory(summariesDirectory);

        var summaryPath = Path.Combine(
            summariesDirectory,
            $"{SessionHistoryPath.Slug(summary.SourceCaptureId)}.json");

        await File.WriteAllTextAsync(
                summaryPath,
                JsonSerializer.Serialize(summary, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        await UpdateAggregateAsync(sessionDirectory, summary, cancellationToken).ConfigureAwait(false);
        return await UpdateSessionGroupAsync(sessionDirectory, summary, segmentContext, cancellationToken).ConfigureAwait(false);
    }

    private string GetSessionDirectory(HistoricalSessionSummary summary)
    {
        return Path.Combine(
            _options.ResolvedHistoryRoot,
            "cars",
            summary.Combo.CarKey,
            "tracks",
            summary.Combo.TrackKey,
            "sessions",
            summary.Combo.SessionKey);
    }

    private static async Task UpdateAggregateAsync(
        string sessionDirectory,
        HistoricalSessionSummary summary,
        CancellationToken cancellationToken)
    {
        var aggregatePath = Path.Combine(sessionDirectory, "aggregate.json");
        var aggregate = await ReadAggregateAsync(aggregatePath, cancellationToken).ConfigureAwait(false)
            ?? new HistoricalSessionAggregate();

        aggregate.AggregateVersion = 1;
        aggregate.Combo = summary.Combo;
        aggregate.Car = summary.Car;
        aggregate.Track = summary.Track;
        aggregate.Session = summary.Session;
        aggregate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        aggregate.SessionCount++;

        if (summary.Quality.ContributesToBaseline)
        {
            aggregate.BaselineSessionCount++;
            aggregate.FuelPerLapLiters.Add(summary.Metrics.FuelPerLapLiters);
            aggregate.FuelPerHourLiters.Add(summary.Metrics.FuelPerHourLiters);
            aggregate.AverageLapSeconds.Add(summary.Metrics.AverageLapSeconds);
            aggregate.MedianLapSeconds.Add(summary.Metrics.MedianLapSeconds);
            aggregate.PitRoadEntryCount.Add(summary.Metrics.PitRoadEntryCount);
            aggregate.PitServiceCount.Add(summary.Metrics.PitServiceCount);
            aggregate.AverageStintLaps.Add(summary.Metrics.AverageStintLaps);
            aggregate.AverageStintSeconds.Add(summary.Metrics.AverageStintSeconds);
            aggregate.AverageStintFuelPerLapLiters.Add(summary.Metrics.AverageStintFuelPerLapLiters);
            foreach (var stint in summary.Stints)
            {
                if (stint.DistanceLaps <= 0d)
                {
                    continue;
                }

                if (IsLocalDriverStint(stint.DriverRole))
                {
                    aggregate.LocalDriverStintLaps.Add(stint.DistanceLaps);
                }
                else if (IsTeammateDriverStint(stint.DriverRole))
                {
                    aggregate.TeammateDriverStintLaps.Add(stint.DistanceLaps);
                }
            }

            aggregate.AveragePitLaneSeconds.Add(summary.Metrics.AveragePitLaneSeconds);
            aggregate.AveragePitStallSeconds.Add(summary.Metrics.AveragePitStallSeconds);
            aggregate.AveragePitServiceSeconds.Add(summary.Metrics.AveragePitServiceSeconds);
            aggregate.ObservedFuelFillRateLitersPerSecond.Add(summary.Metrics.ObservedFuelFillRateLitersPerSecond);
            aggregate.AverageTireChangePitServiceSeconds.Add(summary.Metrics.AverageTireChangePitServiceSeconds);
            aggregate.AverageNoTirePitServiceSeconds.Add(summary.Metrics.AverageNoTirePitServiceSeconds);
        }

        await File.WriteAllTextAsync(
                aggregatePath,
                JsonSerializer.Serialize(aggregate, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<HistoricalSessionGroup> UpdateSessionGroupAsync(
        string sessionDirectory,
        HistoricalSessionSummary summary,
        HistoricalSessionSegmentContext segmentContext,
        CancellationToken cancellationToken)
    {
        var groupsDirectory = Path.Combine(sessionDirectory, "session-groups");
        Directory.CreateDirectory(groupsDirectory);

        var groupId = BuildGroupId(summary);
        var groupPath = Path.Combine(groupsDirectory, $"{groupId}.json");
        var existing = await ReadSessionGroupAsync(groupPath, cancellationToken).ConfigureAwait(false);
        var segments = existing?.Segments.ToList() ?? [];
        var segment = BuildSegment(summary, segmentContext);
        var existingIndex = segments.FindIndex(
            item => string.Equals(item.SourceCaptureId, segment.SourceCaptureId, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            segments[existingIndex] = segment;
        }
        else
        {
            segments.Add(segment);
        }

        segments = CalculateSegmentGaps(segments);
        var group = new HistoricalSessionGroup
        {
            GroupId = groupId,
            CreatedAtUtc = existing?.CreatedAtUtc ?? DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Combo = summary.Combo,
            Car = summary.Car,
            Track = summary.Track,
            Session = summary.Session,
            Segments = segments
        };

        await File.WriteAllTextAsync(
                groupPath,
                JsonSerializer.Serialize(group, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
        return group;
    }

    private static HistoricalSessionSegment BuildSegment(
        HistoricalSessionSummary summary,
        HistoricalSessionSegmentContext context)
    {
        return new HistoricalSessionSegment
        {
            SourceCaptureId = summary.SourceCaptureId,
            AppRunId = context.AppRunId ?? summary.AppRunId,
            CollectionId = context.CollectionId ?? summary.CollectionId,
            StartedAtUtc = summary.StartedAtUtc,
            FinishedAtUtc = summary.FinishedAtUtc,
            CaptureDurationSeconds = summary.Metrics.CaptureDurationSeconds,
            SampleFrameCount = summary.Metrics.SampleFrameCount,
            DroppedFrameCount = summary.Metrics.DroppedFrameCount,
            QualityConfidence = summary.Quality.Confidence,
            ContributesToBaseline = summary.Quality.ContributesToBaseline,
            EndedReason = string.IsNullOrWhiteSpace(context.EndedReason) ? "unknown" : context.EndedReason,
            PreviousAppRunUnclean = context.PreviousAppRunUnclean,
            PreviousAppStartedAtUtc = context.PreviousAppStartedAtUtc,
            PreviousAppLastHeartbeatAtUtc = context.PreviousAppLastHeartbeatAtUtc,
            PreviousAppStoppedAtUtc = context.PreviousAppStoppedAtUtc
        };
    }

    private static List<HistoricalSessionSegment> CalculateSegmentGaps(
        IReadOnlyList<HistoricalSessionSegment> segments)
    {
        var ordered = segments
            .OrderBy(segment => segment.StartedAtUtc)
            .ThenBy(segment => segment.SourceCaptureId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var result = new List<HistoricalSessionSegment>(ordered.Length);
        HistoricalSessionSegment? previous = null;
        foreach (var segment in ordered)
        {
            result.Add(new HistoricalSessionSegment
            {
                SourceCaptureId = segment.SourceCaptureId,
                AppRunId = segment.AppRunId,
                CollectionId = segment.CollectionId,
                StartedAtUtc = segment.StartedAtUtc,
                FinishedAtUtc = segment.FinishedAtUtc,
                CaptureDurationSeconds = segment.CaptureDurationSeconds,
                SampleFrameCount = segment.SampleFrameCount,
                DroppedFrameCount = segment.DroppedFrameCount,
                QualityConfidence = segment.QualityConfidence,
                ContributesToBaseline = segment.ContributesToBaseline,
                EndedReason = segment.EndedReason,
                PreviousAppRunUnclean = segment.PreviousAppRunUnclean,
                PreviousAppStartedAtUtc = segment.PreviousAppStartedAtUtc,
                PreviousAppLastHeartbeatAtUtc = segment.PreviousAppLastHeartbeatAtUtc,
                PreviousAppStoppedAtUtc = segment.PreviousAppStoppedAtUtc,
                GapFromPreviousSegmentSeconds = previous is null
                    ? null
                    : Math.Max(0d, (segment.StartedAtUtc - previous.FinishedAtUtc).TotalSeconds)
            });
            previous = segment;
        }

        return result;
    }

    private static string BuildGroupId(HistoricalSessionSummary summary)
    {
        var sessionIdentity = summary.Session.SubSessionId is { } subSessionId
            ? $"subsession-{subSessionId}"
            : summary.Session.SessionId is { } sessionId
                ? $"session-{sessionId}"
                : $"local-{summary.StartedAtUtc:yyyyMMdd-HHmmss}-{summary.SourceCaptureId}";
        var sessionNum = summary.Session.SessionNum is { } value
            ? $"-s{value}"
            : string.Empty;
        return SessionHistoryPath.Slug(
            $"{sessionIdentity}{sessionNum}-{summary.Combo.CarKey}-{summary.Combo.TrackKey}-{summary.Combo.SessionKey}");
    }

    private static async Task<HistoricalSessionGroup?> ReadSessionGroupAsync(
        string groupPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(groupPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(groupPath);
        return await JsonSerializer.DeserializeAsync<HistoricalSessionGroup>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<HistoricalSessionAggregate?> ReadAggregateAsync(
        string aggregatePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(aggregatePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(aggregatePath);
        return await JsonSerializer.DeserializeAsync<HistoricalSessionAggregate>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLocalDriverStint(string driverRole)
    {
        return driverRole.Contains("local", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTeammateDriverStint(string driverRole)
    {
        return driverRole.Contains("team", StringComparison.OrdinalIgnoreCase)
            || driverRole.Contains("teammate", StringComparison.OrdinalIgnoreCase);
    }
}
