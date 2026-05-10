using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class RelativeOverlayViewModelTests
{
    [Fact]
    public void From_WhenTelemetryIsUnavailable_ShowsWaitingState()
    {
        var viewModel = RelativeOverlayViewModel.From(
            LiveTelemetrySnapshot.Empty,
            DateTimeOffset.UtcNow,
            carsAhead: 5,
            carsBehind: 5);

        Assert.Equal("waiting for iRacing", viewModel.Status);
        Assert.Equal("source: waiting", viewModel.Source);
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public void From_AddsReferenceRowAndCapsCarsAheadAndBehind()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(
            now,
            RelativeRow(carIdx: 11, isAhead: true, seconds: 0.4d, classPosition: 5),
            RelativeRow(carIdx: 12, isAhead: true, seconds: 1.2d, classPosition: 4),
            RelativeRow(carIdx: 13, isAhead: false, seconds: -0.5d, classPosition: 7),
            RelativeRow(carIdx: 14, isAhead: false, seconds: -1.5d, classPosition: 8));

        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            carsAhead: 1,
            carsBehind: 1);

        Assert.Equal("6 - 2/4 cars", viewModel.Status);
        Assert.Equal("source: live proximity telemetry", viewModel.Source);
        Assert.Collection(
            viewModel.Rows,
            row =>
            {
                Assert.True(row.IsAhead);
                Assert.Equal("5", row.Position);
                Assert.Equal("-0.400", row.Gap);
            },
            row =>
            {
                Assert.True(row.IsReference);
                Assert.Equal("6", row.Position);
                Assert.Equal("0.000", row.Gap);
            },
            row =>
            {
                Assert.True(row.IsBehind);
                Assert.Equal("7", row.Position);
                Assert.Equal("+0.500", row.Gap);
            });
    }

    [Fact]
    public void From_FormatsTimingFallbackRowsByDirection()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(
            now,
            RelativeRow(
                carIdx: 11,
                isAhead: true,
                seconds: -2.5d,
                classPosition: 5,
                source: "class-gap",
                quality: LiveModelQuality.Inferred,
                placementEvidence: LiveSignalEvidence.Unavailable("class-gap", "no_lap_distance_placement")),
            RelativeRow(
                carIdx: 12,
                isAhead: false,
                seconds: 3.75d,
                classPosition: 7,
                source: "class-gap",
                quality: LiveModelQuality.Inferred,
                placementEvidence: LiveSignalEvidence.Unavailable("class-gap", "no_lap_distance_placement")));

        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            carsAhead: 5,
            carsBehind: 5);

        Assert.Equal("source: model-v2 timing fallback", viewModel.Source);
        Assert.Equal("-2.500", viewModel.Rows[0].Gap);
        Assert.Equal("0.000", viewModel.Rows[1].Gap);
        Assert.Equal("+3.750", viewModel.Rows[2].Gap);
    }

    [Fact]
    public void From_LabelsFullyDegradedRowsAsPartial()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(
            now,
            RelativeRow(
                carIdx: 11,
                isAhead: true,
                seconds: null,
                classPosition: 5,
                quality: LiveModelQuality.Partial,
                timingEvidence: LiveSignalEvidence.Partial("proximity-relative-seconds", "relative_seconds_missing"),
                placementEvidence: LiveSignalEvidence.Unavailable("CarIdxLapDistPct", "missing_lap_distance")));

        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            carsAhead: 5,
            carsBehind: 5);

        Assert.Equal("source: partial timing", viewModel.Source);
        Assert.True(viewModel.Rows[0].IsPartial);
        Assert.Equal("--", viewModel.Rows[0].Gap);
    }

    [Fact]
    public void From_UsesScoringOnlyAsRelativeRowEnrichment()
    {
        var now = DateTimeOffset.UtcNow;
        var nearbyRow = RelativeRow(
            carIdx: 11,
            isAhead: true,
            seconds: 1.1d,
            classPosition: 0,
            driverName: null);
        var snapshot = Snapshot(
            now,
            nearbyRow) with
        {
            Models = Snapshot(now, nearbyRow).Models with
            {
                Scoring = new LiveScoringModel(
                    HasData: true,
                    Quality: LiveModelQuality.Reliable,
                    ReferenceCarIdx: 10,
                    ReferenceCarClass: 4098,
                    ClassGroups: [],
                    Rows:
                    [
                        ScoringRow(10, overallPosition: 6, classPosition: 6, carNumber: "10", driverName: "Reference Driver"),
                        ScoringRow(11, overallPosition: 5, classPosition: 5, carNumber: "88", driverName: "Scored Nearby"),
                        ScoringRow(99, overallPosition: 4, classPosition: 4, carNumber: "99", driverName: "Scoring Only")
                    ])
            }
        };

        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            carsAhead: 5,
            carsBehind: 5);

        Assert.DoesNotContain(viewModel.Rows, row => row.Driver.Contains("Scoring Only", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row =>
            row.IsAhead
            && row.Position == "5"
            && row.Driver == "#88 Scored Nearby");
    }

    [Fact]
    public void From_KeepsPitRoadRelativeRowsVisible()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(
            now,
            RelativeRow(
                carIdx: 11,
                isAhead: false,
                seconds: -0.7d,
                classPosition: 7,
                onPitRoad: true));

        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            carsAhead: 5,
            carsBehind: 5);

        var pitRow = Assert.Single(viewModel.Rows.Where(row => row.IsPit));
        Assert.True(pitRow.IsBehind);
        Assert.Contains("PIT", pitRow.Detail, StringComparison.Ordinal);
    }

    private static LiveTelemetrySnapshot Snapshot(DateTimeOffset now, params LiveRelativeRow[] rows)
    {
        var reference = TimingRow(10, "Reference Driver", classPosition: 6, isPlayer: true, isFocus: true);
        var timingRows = rows
            .Select(row => TimingRow(
                row.CarIdx,
                $"Driver {row.CarIdx}",
                row.ClassPosition ?? 0,
                overallPosition: row.OverallPosition ?? 0))
            .Prepend(reference)
            .ToArray();

        return LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = now,
            Sequence = 1,
            Context = HistoricalSessionContext.Empty,
            Combo = HistoricalComboIdentity.From(HistoricalSessionContext.Empty),
            Models = LiveRaceModels.Empty with
            {
                DriverDirectory = new LiveDriverDirectoryModel(
                    HasData: true,
                    Quality: LiveModelQuality.Reliable,
                    PlayerCarIdx: 10,
                    FocusCarIdx: 10,
                    ReferenceCarClass: 4098,
                    PlayerDriver: Driver(10, "Reference Driver", carNumber: "10"),
                    FocusDriver: Driver(10, "Reference Driver", carNumber: "10"),
                    Drivers: timingRows.Select(row => Driver(
                        row.CarIdx,
                        row.DriverName,
                        carNumber: row.CarNumber ?? row.CarIdx.ToString())).ToArray()),
                Timing = LiveTimingModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Reliable,
                    PlayerCarIdx = 10,
                    FocusCarIdx = 10,
                    PlayerRow = reference,
                    FocusRow = reference,
                    OverallRows = timingRows,
                    ClassRows = timingRows
                },
                Scoring = new LiveScoringModel(
                    HasData: true,
                    Quality: LiveModelQuality.Reliable,
                    ReferenceCarIdx: 10,
                    ReferenceCarClass: 4098,
                    ClassGroups: [],
                    Rows:
                    [
                        ScoringRow(10, overallPosition: 8, classPosition: 6, carNumber: "10", driverName: "Reference Driver")
                    ]),
                Relative = new LiveRelativeModel(
                    HasData: rows.Length > 0,
                    Quality: rows.Length == 0 ? LiveModelQuality.Unavailable : rows.Max(row => row.Quality),
                    ReferenceCarIdx: 10,
                    Rows: rows)
            }
        };
    }

    private static LiveRelativeRow RelativeRow(
        int carIdx,
        bool isAhead,
        double? seconds,
        int classPosition,
        string source = "proximity",
        LiveModelQuality quality = LiveModelQuality.Reliable,
        LiveSignalEvidence? timingEvidence = null,
        LiveSignalEvidence? placementEvidence = null,
        string? driverName = "default",
        bool onPitRoad = false)
    {
        var resolvedDriverName = string.Equals(driverName, "default", StringComparison.Ordinal)
            ? $"Driver {carIdx}"
            : driverName;
        return new LiveRelativeRow(
            CarIdx: carIdx,
            Quality: quality,
            Source: source,
            IsAhead: isAhead,
            IsBehind: !isAhead,
            IsSameClass: true,
            TimingEvidence: timingEvidence ?? LiveSignalEvidence.Reliable("proximity-relative-seconds"),
            PlacementEvidence: placementEvidence ?? LiveSignalEvidence.Reliable("CarIdxLapDistPct+track-length"),
            DriverName: resolvedDriverName,
            OverallPosition: classPosition + 2,
            ClassPosition: classPosition > 0 ? classPosition : null,
            CarClass: 4098,
            RelativeSeconds: seconds,
            RelativeLaps: seconds is { } value ? value / 120d : null,
            RelativeMeters: seconds is { } meters ? meters * 45d : null,
            OnPitRoad: onPitRoad);
    }

    private static LiveTimingRow TimingRow(
        int carIdx,
        string driverName,
        int classPosition,
        int overallPosition = 10,
        bool isPlayer = false,
        bool isFocus = false)
    {
        return new LiveTimingRow(
            CarIdx: carIdx,
            Quality: LiveModelQuality.Reliable,
            Source: "test",
            IsPlayer: isPlayer,
            IsFocus: isFocus,
            IsOverallLeader: false,
            IsClassLeader: false,
            HasTiming: true,
            HasSpatialProgress: true,
            CanUseForRadarPlacement: true,
            TimingEvidence: LiveSignalEvidence.Reliable("test"),
            SpatialEvidence: LiveSignalEvidence.Reliable("test"),
            RadarPlacementEvidence: LiveSignalEvidence.Reliable("test"),
            GapEvidence: LiveSignalEvidence.Reliable("test"),
            DriverName: driverName,
            TeamName: null,
            CarNumber: carIdx.ToString(),
            CarClassName: "GT3",
            CarClassColorHex: "#FFDA59",
            OverallPosition: overallPosition,
            ClassPosition: classPosition,
            CarClass: 4098,
            LapCompleted: null,
            LapDistPct: null,
            ProgressLaps: null,
            F2TimeSeconds: null,
            EstimatedTimeSeconds: null,
            LastLapTimeSeconds: null,
            BestLapTimeSeconds: null,
            GapSecondsToClassLeader: null,
            GapLapsToClassLeader: null,
            DeltaSecondsToFocus: null,
            TrackSurface: null,
            OnPitRoad: false);
    }

    private static LiveDriverIdentity Driver(int carIdx, string? name, string carNumber)
    {
        return new LiveDriverIdentity(
            CarIdx: carIdx,
            DriverName: name,
            AbbrevName: null,
            Initials: null,
            UserId: null,
            TeamId: null,
            TeamName: null,
            CarNumber: carNumber,
            CarClassId: 4098,
            CarClassName: "GT3",
            CarClassColorHex: "#FFDA59",
            IsSpectator: false);
    }

    private static LiveScoringRow ScoringRow(
        int carIdx,
        int overallPosition,
        int classPosition,
        string carNumber,
        string driverName)
    {
        return new LiveScoringRow(
            CarIdx: carIdx,
            OverallPositionRaw: overallPosition,
            ClassPositionRaw: classPosition - 1,
            OverallPosition: overallPosition,
            ClassPosition: classPosition,
            CarClass: 4098,
            DriverName: driverName,
            TeamName: null,
            CarNumber: carNumber,
            CarClassName: "GT3",
            CarClassColorHex: "#FFDA59",
            IsPlayer: carIdx == 10,
            IsFocus: carIdx == 10,
            IsReferenceClass: true,
            Lap: null,
            LapsComplete: null,
            LastLapTimeSeconds: null,
            BestLapTimeSeconds: null,
            ReasonOut: null);
    }
}
