using TmrOverlay.App.History;
using TmrOverlay.App.Overlays.BrowserSources;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class BrowserOverlayModelFactoryTests
{
    [Fact]
    public void GapToLeaderGraph_SelectsClassCarsWhenReferenceTimingIsNotChartable()
    {
        var factory = new BrowserOverlayModelFactory(new SessionHistoryQueryService(new SessionHistoryOptions
        {
            Enabled = false,
            ResolvedUserHistoryRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-test-history"),
            ResolvedBaselineHistoryRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-test-baseline-history")
        }));
        var now = DateTimeOffset.Parse("2026-05-13T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var leader = TimingRow(
            carIdx: 11,
            isClassLeader: true,
            classPosition: 1,
            gapSeconds: 0d,
            gapEvidence: LiveSignalEvidence.Reliable("class-leader-row"));
        var focusWithoutTiming = TimingRow(
            carIdx: 12,
            isFocus: true,
            classPosition: 2,
            gapSeconds: null,
            deltaSeconds: null,
            gapEvidence: LiveSignalEvidence.Partial("CarIdxF2Time", "reference_f2_time_missing"));
        var timedClassCar = TimingRow(
            carIdx: 13,
            classPosition: 3,
            gapSeconds: 8.5d,
            deltaSeconds: null,
            gapEvidence: LiveSignalEvidence.Inferred("CarIdxEstTime+CarIdxLapDistPct"));
        var snapshot = LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = now,
            Sequence = 1,
            Models = LiveRaceModels.Empty with
            {
                Timing = LiveTimingModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Partial,
                    FocusCarIdx = focusWithoutTiming.CarIdx,
                    ClassLeaderCarIdx = leader.CarIdx,
                    FocusRow = focusWithoutTiming,
                    ClassRows = [leader, focusWithoutTiming, timedClassCar],
                    ClassLeaderGapEvidence = LiveSignalEvidence.Partial("CarIdxF2Time", "reference_f2_time_missing")
                },
                RaceProgress = LiveRaceProgressModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Partial,
                    ReferenceClassPosition = 2,
                    StrategyLapTimeSeconds = 91d
                }
            }
        };

        var built = factory.TryBuild("gap-to-leader", snapshot, new ApplicationSettings(), now, out var response);

        Assert.True(built);
        Assert.NotNull(response.Model.Graph);
        Assert.Contains(response.Model.Graph.Series, series => series.CarIdx == leader.CarIdx);
        Assert.Contains(response.Model.Graph.Series, series => series.CarIdx == timedClassCar.CarIdx);
        Assert.DoesNotContain(response.Model.Graph.Series, series => series.CarIdx == focusWithoutTiming.CarIdx);
        Assert.All(response.Model.Graph.TrendMetrics, metric => Assert.Equal("unavailable", metric.State));
    }

    [Fact]
    public void GapToLeaderGraph_AnchorsLeadLapCarsWhenReferenceIsLapped()
    {
        var factory = new BrowserOverlayModelFactory(new SessionHistoryQueryService(new SessionHistoryOptions
        {
            Enabled = false,
            ResolvedUserHistoryRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-test-history"),
            ResolvedBaselineHistoryRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-test-baseline-history")
        }));
        var now = DateTimeOffset.Parse("2026-05-13T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var leader = TimingRow(
            carIdx: 11,
            isClassLeader: true,
            classPosition: 1,
            gapSeconds: 0d,
            gapEvidence: LiveSignalEvidence.Reliable("class-leader-row"));
        var leadLapCar = TimingRow(
            carIdx: 12,
            classPosition: 2,
            gapSeconds: 4.2d,
            gapEvidence: LiveSignalEvidence.Reliable("CarIdxF2Time"));
        var lappedFocus = TimingRow(
            carIdx: 13,
            isFocus: true,
            classPosition: 8,
            gapLaps: 1d,
            gapEvidence: LiveSignalEvidence.Inferred("CarIdxLapCompleted+CarIdxLapDistPct"));
        var snapshot = LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = now,
            Sequence = 1,
            Models = LiveRaceModels.Empty with
            {
                Timing = LiveTimingModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Reliable,
                    FocusCarIdx = lappedFocus.CarIdx,
                    ClassLeaderCarIdx = leader.CarIdx,
                    FocusRow = lappedFocus,
                    ClassRows = [leader, leadLapCar, lappedFocus],
                    ClassLeaderGapEvidence = LiveSignalEvidence.Inferred("CarIdxLapCompleted+CarIdxLapDistPct")
                },
                RaceProgress = LiveRaceProgressModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Partial,
                    ReferenceClassPosition = 8,
                    StrategyLapTimeSeconds = 90d
                }
            }
        };

        var built = factory.TryBuild("gap-to-leader", snapshot, new ApplicationSettings(), now, out var response);

        Assert.True(built);
        Assert.NotNull(response.Model.Graph);
        Assert.Contains(response.Model.Graph.Series, series => series.CarIdx == leader.CarIdx);
        Assert.Contains(response.Model.Graph.Series, series => series.CarIdx == leadLapCar.CarIdx);
        Assert.DoesNotContain(response.Model.Graph.Series, series => series.CarIdx == lappedFocus.CarIdx);
    }

    private static LiveTimingRow TimingRow(
        int carIdx,
        bool isFocus = false,
        bool isClassLeader = false,
        int? classPosition = null,
        double? gapSeconds = null,
        double? gapLaps = null,
        double? deltaSeconds = null,
        LiveSignalEvidence? gapEvidence = null)
    {
        return new LiveTimingRow(
            CarIdx: carIdx,
            Quality: LiveModelQuality.Reliable,
            Source: "test",
            IsPlayer: isFocus,
            IsFocus: isFocus,
            IsOverallLeader: false,
            IsClassLeader: isClassLeader,
            HasTiming: true,
            HasSpatialProgress: true,
            CanUseForRadarPlacement: false,
            TimingEvidence: LiveSignalEvidence.Reliable("test"),
            SpatialEvidence: LiveSignalEvidence.Reliable("test"),
            RadarPlacementEvidence: LiveSignalEvidence.Unavailable("test", "not_applicable"),
            GapEvidence: gapEvidence ?? LiveSignalEvidence.Unavailable("class-gap", "gap_signals_missing"),
            DriverName: null,
            TeamName: null,
            CarNumber: carIdx.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CarClassName: "GT3",
            CarClassColorHex: null,
            OverallPosition: null,
            ClassPosition: classPosition,
            CarClass: 1,
            LapCompleted: null,
            LapDistPct: null,
            ProgressLaps: null,
            F2TimeSeconds: null,
            EstimatedTimeSeconds: null,
            LastLapTimeSeconds: null,
            BestLapTimeSeconds: null,
            GapSecondsToClassLeader: gapSeconds,
            GapLapsToClassLeader: gapLaps,
            IntervalSecondsToPreviousClassRow: null,
            IntervalLapsToPreviousClassRow: null,
            DeltaSecondsToFocus: deltaSeconds,
            TrackSurface: null,
            OnPitRoad: false);
    }
}
