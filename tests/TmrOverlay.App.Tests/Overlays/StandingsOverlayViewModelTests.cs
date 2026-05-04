using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class StandingsOverlayViewModelTests
{
    [Fact]
    public void From_WhenTelemetryIsUnavailable_ShowsWaitingState()
    {
        var viewModel = StandingsOverlayViewModel.From(
            LiveTelemetrySnapshot.Empty,
            DateTimeOffset.UtcNow);

        Assert.Equal("waiting for iRacing", viewModel.Status);
        Assert.Equal("source: waiting", viewModel.Source);
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public void From_FormatsClassTimingRows()
    {
        var now = DateTimeOffset.UtcNow;
        var reference = TimingRow(
            carIdx: 10,
            driverName: "Reference Driver",
            carNumber: "44",
            classPosition: 3,
            gapSeconds: 12.4d,
            deltaSeconds: 0d,
            isFocus: true,
            onPitRoad: true);
        var leader = TimingRow(
            carIdx: 1,
            driverName: "Class Leader",
            carNumber: "1",
            classPosition: 1,
            gapSeconds: 0d,
            deltaSeconds: -12.4d,
            isLeader: true);
        var trailing = TimingRow(
            carIdx: 12,
            driverName: "Trailing Driver",
            carNumber: "12",
            classPosition: 4,
            gapSeconds: 18.2d,
            deltaSeconds: 5.8d);

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
                    FocusCarIdx = 10,
                    FocusRow = reference,
                    ClassRows = [trailing, reference, leader]
                }
            }
        };

        var viewModel = StandingsOverlayViewModel.From(snapshot, now);

        Assert.Equal("C3 - 3 rows", viewModel.Status);
        Assert.Equal("source: live timing telemetry", viewModel.Source);
        Assert.Collection(
            viewModel.Rows,
            row =>
            {
                Assert.True(row.IsLeader);
                Assert.Equal("C1", row.ClassPosition);
                Assert.Equal("Leader", row.Gap);
                Assert.Equal("-12.4", row.Interval);
            },
            row =>
            {
                Assert.True(row.IsReference);
                Assert.Equal("#44", row.CarNumber);
                Assert.Equal("0.0", row.Interval);
                Assert.Equal("IN", row.Pit);
            },
            row =>
            {
                Assert.Equal("Trailing Driver", row.Driver);
                Assert.Equal("+18.2", row.Gap);
                Assert.Equal("+5.8", row.Interval);
            });
    }

    private static LiveTimingRow TimingRow(
        int carIdx,
        string driverName,
        string carNumber,
        int classPosition,
        double? gapSeconds,
        double? deltaSeconds,
        bool isLeader = false,
        bool isFocus = false,
        bool onPitRoad = false)
    {
        return new LiveTimingRow(
            CarIdx: carIdx,
            Quality: LiveModelQuality.Reliable,
            Source: "test",
            IsPlayer: isFocus,
            IsFocus: isFocus,
            IsOverallLeader: isLeader,
            IsClassLeader: isLeader,
            HasTiming: true,
            HasSpatialProgress: true,
            CanUseForRadarPlacement: true,
            TimingEvidence: LiveSignalEvidence.Reliable("test"),
            SpatialEvidence: LiveSignalEvidence.Reliable("test"),
            RadarPlacementEvidence: LiveSignalEvidence.Reliable("test"),
            GapEvidence: LiveSignalEvidence.Reliable("test"),
            DriverName: driverName,
            TeamName: null,
            CarNumber: carNumber,
            CarClassName: "GT3",
            CarClassColorHex: "#FFDA59",
            OverallPosition: classPosition + 2,
            ClassPosition: classPosition,
            CarClass: 4098,
            LapCompleted: null,
            LapDistPct: null,
            ProgressLaps: null,
            F2TimeSeconds: null,
            EstimatedTimeSeconds: null,
            LastLapTimeSeconds: null,
            BestLapTimeSeconds: null,
            GapSecondsToClassLeader: gapSeconds,
            GapLapsToClassLeader: null,
            DeltaSecondsToFocus: deltaSeconds,
            TrackSurface: null,
            OnPitRoad: onPitRoad);
    }
}
