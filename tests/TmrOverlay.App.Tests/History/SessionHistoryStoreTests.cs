using TmrOverlay.App.History;
using TmrOverlay.Core.Analysis;
using TmrOverlay.Core.History;
using Xunit;

namespace TmrOverlay.App.Tests.History;

public sealed class SessionHistoryStoreTests
{
    [Fact]
    public async Task SaveAsync_GroupsSegmentsFromSameIRacingSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-history-store-test", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionHistoryStore(CreateOptions(root));
            var firstStart = new DateTimeOffset(2026, 5, 1, 18, 0, 0, TimeSpan.Zero);
            var first = CreateSummary(
                "capture-first",
                firstStart,
                firstStart.AddMinutes(10));
            var second = CreateSummary(
                "capture-second",
                firstStart.AddMinutes(10).AddSeconds(42),
                firstStart.AddMinutes(25));

            var firstGroup = await store.SaveAsync(
                first,
                HistoricalSessionSegmentContext.Normal("iracing_disconnected"),
                CancellationToken.None);
            var secondGroup = await store.SaveAsync(
                second,
                new HistoricalSessionSegmentContext
                {
                    EndedReason = "app_stopped",
                    AppRunId = "run-test",
                    CollectionId = "collection-second",
                    PreviousAppRunUnclean = true,
                    PreviousAppStartedAtUtc = firstStart.AddHours(-2),
                    PreviousAppLastHeartbeatAtUtc = firstStart.AddMinutes(9)
                },
                CancellationToken.None);

            Assert.NotNull(firstGroup);
            Assert.NotNull(secondGroup);
            Assert.Equal(firstGroup!.GroupId, secondGroup!.GroupId);
            Assert.Equal(2, secondGroup.Segments.Count);
            Assert.Contains(
                Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories),
                path => path.Contains($"{Path.DirectorySeparatorChar}session-groups{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
            Assert.Collection(
                secondGroup.Segments,
                segment =>
                {
                    Assert.Equal("capture-first", segment.SourceCaptureId);
                    Assert.Equal("iracing_disconnected", segment.EndedReason);
                    Assert.Null(segment.GapFromPreviousSegmentSeconds);
                    Assert.False(segment.PreviousAppRunUnclean);
                },
                segment =>
                {
                    Assert.Equal("capture-second", segment.SourceCaptureId);
                    Assert.Equal("run-test", segment.AppRunId);
                    Assert.Equal("collection-second", segment.CollectionId);
                    Assert.Equal("app_stopped", segment.EndedReason);
                    Assert.Equal(42d, segment.GapFromPreviousSegmentSeconds);
                    Assert.True(segment.PreviousAppRunUnclean);
                    Assert.Equal(firstStart.AddMinutes(9), segment.PreviousAppLastHeartbeatAtUtc);
                });
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
    public async Task PostRaceAnalysis_UsesStableSessionGroupId()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-history-analysis-test", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionHistoryStore(CreateOptions(root));
            var startedAtUtc = new DateTimeOffset(2026, 5, 1, 19, 0, 0, TimeSpan.Zero);
            var summary = CreateSummary("capture-analysis", startedAtUtc, startedAtUtc.AddMinutes(5));
            var group = await store.SaveAsync(
                summary,
                new HistoricalSessionSegmentContext
                {
                    EndedReason = "iracing_disconnected",
                    PreviousAppRunUnclean = true,
                    PreviousAppLastHeartbeatAtUtc = startedAtUtc.AddMinutes(-1)
                },
                CancellationToken.None);

            var analysis = PostRaceAnalysisBuilder.Build(summary, group);

            Assert.NotNull(group);
            Assert.Equal(group!.GroupId, analysis.Id);
            Assert.Equal(group.GroupId, analysis.SourceId);
            Assert.Contains(analysis.Lines, line => line.Contains("Session stitching: 1 capture segment", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(analysis.Lines, line => line.Contains("Crash/reload context", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static SessionHistoryOptions CreateOptions(string root)
    {
        return new SessionHistoryOptions
        {
            Enabled = true,
            ResolvedUserHistoryRoot = root,
            ResolvedBaselineHistoryRoot = Path.Combine(root, "baseline")
        };
    }

    private static HistoricalSessionSummary CreateSummary(
        string sourceCaptureId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc)
    {
        return new HistoricalSessionSummary
        {
            SourceCaptureId = sourceCaptureId,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = finishedAtUtc,
            Combo = new HistoricalComboIdentity
            {
                CarKey = "car-156-mercedes-amg-gt3-2020",
                TrackKey = "track-262-gesamtstrecke-vln",
                SessionKey = "race"
            },
            Car = new HistoricalCarIdentity
            {
                CarId = 156,
                CarScreenName = "Mercedes-AMG GT3 2020",
                DriverCarFuelMaxLiters = 106d
            },
            Track = new HistoricalTrackIdentity
            {
                TrackId = 262,
                TrackDisplayName = "Gesamtstrecke VLN"
            },
            Session = new HistoricalSessionIdentity
            {
                SessionType = "Race",
                SessionNum = 0,
                SessionId = 123456,
                SubSessionId = 654321,
                TeamRacing = true
            },
            Conditions = new HistoricalConditions(),
            Metrics = new HistoricalSessionMetrics
            {
                SampleFrameCount = 600,
                CaptureDurationSeconds = (finishedAtUtc - startedAtUtc).TotalSeconds,
                CompletedValidLaps = 4,
                ValidDistanceLaps = 4d,
                FuelPerLapLiters = 13.2d,
                FuelPerHourLiters = 95d,
                AverageLapSeconds = 500d,
                MedianLapSeconds = 498d,
                AverageStintLaps = 7d,
                AverageStintSeconds = 3500d,
                AverageStintFuelPerLapLiters = 13.2d,
                TelemetryAvailability = new TelemetryAvailabilitySnapshot
                {
                    SampleFrameCount = 600,
                    LocalDrivingFrameCount = 600,
                    LocalFuelScalarFrameCount = 600,
                    LocalDrivingFuelScalarFrameCount = 600
                }
            },
            Quality = new HistoricalDataQuality
            {
                Confidence = "high",
                ContributesToBaseline = true,
                Reasons = []
            }
        };
    }
}
