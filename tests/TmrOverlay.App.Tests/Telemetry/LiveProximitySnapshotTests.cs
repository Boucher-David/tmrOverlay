using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class LiveProximitySnapshotTests
{
    [Fact]
    public void From_MapsSideWarningAndWrapsPhysicalLapDistance()
    {
        var context = new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity(),
            Track = new HistoricalTrackIdentity { TrackLengthKm = 5d },
            Session = new HistoricalSessionIdentity(),
            Conditions = new HistoricalSessionInfoConditions()
        };
        var sample = CreateSample(
            teamLapDistPct: 0.98d,
            teamEstimatedTimeSeconds: 99d,
            carLeftRight: 4,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 5,
                    LapDistPct: 0.01d,
                    F2TimeSeconds: 12d,
                    EstimatedTimeSeconds: 2d,
                    Position: 3,
                    ClassPosition: 3,
                    CarClass: 1,
                    TrackSurface: 3,
                    OnPitRoad: false),
                new HistoricalCarProximity(
                    CarIdx: 13,
                    LapCompleted: 5,
                    LapDistPct: 0.95d,
                    F2TimeSeconds: 15d,
                    EstimatedTimeSeconds: 95d,
                    Position: 4,
                    ClassPosition: 4,
                    CarClass: 1,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]);

        var proximity = LiveProximitySnapshot.From(context, sample);

        Assert.True(proximity.HasData);
        Assert.True(proximity.HasCarLeft);
        Assert.True(proximity.HasCarRight);
        Assert.Equal("both sides", proximity.SideStatus);
        Assert.Equal(12, proximity.NearestAhead?.CarIdx);
        Assert.Equal(13, proximity.NearestBehind?.CarIdx);
        Assert.Equal(3d, proximity.NearestAhead!.RelativeSeconds!.Value, precision: 6);
        Assert.Equal(-150d, proximity.NearestBehind!.RelativeMeters!.Value, precision: 6);
    }

    [Fact]
    public void From_DoesNotFilterMulticlassTrafficFromRadar()
    {
        var context = new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity(),
            Track = new HistoricalTrackIdentity(),
            Session = new HistoricalSessionIdentity(),
            Conditions = new HistoricalSessionInfoConditions()
        };
        var sample = CreateSample(
            teamLapDistPct: 0.50d,
            teamCarClass: 4098,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 55,
                    LapCompleted: 5,
                    LapDistPct: 0.51d,
                    F2TimeSeconds: 12d,
                    EstimatedTimeSeconds: 51d,
                    Position: 1,
                    ClassPosition: 1,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]);

        var proximity = LiveProximitySnapshot.From(context, sample);

        var multiclassCar = Assert.Single(proximity.NearbyCars);
        Assert.Equal(55, multiclassCar.CarIdx);
        Assert.Equal(4099, multiclassCar.CarClass);
    }

    [Fact]
    public void From_UsesLiveF2TimingWhenEstimatedTimingIsUnavailable()
    {
        var context = new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity(),
            Track = new HistoricalTrackIdentity(),
            Session = new HistoricalSessionIdentity(),
            Conditions = new HistoricalSessionInfoConditions()
        };
        var sample = CreateSample(
            teamLapDistPct: 0.50d,
            teamF2TimeSeconds: 20d,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 5,
                    LapDistPct: 0.53d,
                    F2TimeSeconds: 16d,
                    EstimatedTimeSeconds: null,
                    Position: 3,
                    ClassPosition: 3,
                    CarClass: 1,
                    TrackSurface: 3,
                    OnPitRoad: false),
                new HistoricalCarProximity(
                    CarIdx: 13,
                    LapCompleted: 5,
                    LapDistPct: 0.47d,
                    F2TimeSeconds: 25d,
                    EstimatedTimeSeconds: null,
                    Position: 4,
                    ClassPosition: 4,
                    CarClass: 1,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]);

        var proximity = LiveProximitySnapshot.From(context, sample);

        Assert.Equal(4d, proximity.NearestAhead!.RelativeSeconds!.Value, precision: 6);
        Assert.Equal(-5d, proximity.NearestBehind!.RelativeSeconds!.Value, precision: 6);
    }

    [Fact]
    public void From_DoesNotSynthesizeTimingWhenLiveTimingIsMissing()
    {
        var context = new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity(),
            Track = new HistoricalTrackIdentity(),
            Session = new HistoricalSessionIdentity(),
            Conditions = new HistoricalSessionInfoConditions()
        };
        var sample = CreateSample(
            teamLapDistPct: 0.50d,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 5,
                    LapDistPct: 0.53d,
                    F2TimeSeconds: null,
                    EstimatedTimeSeconds: null,
                    Position: 3,
                    ClassPosition: 3,
                    CarClass: 1,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]);

        var proximity = LiveProximitySnapshot.From(context, sample);

        var car = Assert.Single(proximity.NearbyCars);
        Assert.Null(car.RelativeSeconds);
        Assert.Equal(0.03d, car.RelativeLaps, precision: 6);
    }

    [Fact]
    public void From_KeepsPitRoadCarsWhenPlayerIsAlsoInPits()
    {
        var context = new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity(),
            Track = new HistoricalTrackIdentity(),
            Session = new HistoricalSessionIdentity(),
            Conditions = new HistoricalSessionInfoConditions()
        };
        var sample = CreateSample(
            teamLapDistPct: 0.50d,
            onPitRoad: true,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 24,
                    LapCompleted: 5,
                    LapDistPct: 0.505d,
                    F2TimeSeconds: 20d,
                    EstimatedTimeSeconds: 50.5d,
                    Position: 8,
                    ClassPosition: 6,
                    CarClass: 1,
                    TrackSurface: 3,
                    OnPitRoad: true)
            ]);

        var proximity = LiveProximitySnapshot.From(context, sample);

        var car = Assert.Single(proximity.NearbyCars);
        Assert.Equal(24, car.CarIdx);
        Assert.True(car.OnPitRoad);
    }

    [Fact]
    public void LeaderGap_UsesF2TimeForOverallAndClassLeaderGaps()
    {
        var sample = CreateSample(
            teamPosition: 7,
            teamClassPosition: 6,
            teamF2TimeSeconds: 91.5d,
            leaderF2TimeSeconds: 0d,
            classLeaderF2TimeSeconds: 23.25d);

        var gap = LiveLeaderGapSnapshot.From(sample);

        Assert.True(gap.HasData);
        Assert.Equal(7, gap.TeamOverallPosition);
        Assert.Equal(6, gap.TeamClassPosition);
        Assert.Equal(91.5d, gap.OverallLeaderGap.Seconds!.Value);
        Assert.Equal(68.25d, gap.ClassLeaderGap.Seconds!.Value);
        Assert.Equal("CarIdxF2Time", gap.OverallLeaderGap.Source);
        Assert.Contains(gap.ClassCars, car => car.IsClassLeader && car.GapSecondsToClassLeader == 0d);
        Assert.Contains(gap.ClassCars, car => car.IsTeamCar && car.GapSecondsToClassLeader == 68.25d);
    }

    [Fact]
    public void LeaderGap_ReportsLeaderWhenPlayerIsPositionOne()
    {
        var sample = CreateSample(teamPosition: 1, teamClassPosition: 1);

        var gap = LiveLeaderGapSnapshot.From(sample);

        Assert.True(gap.OverallLeaderGap.IsLeader);
        Assert.True(gap.ClassLeaderGap.IsLeader);
    }

    [Fact]
    public void LeaderGap_UsesClassTimingRowsEvenWhenLapProgressIsUnavailable()
    {
        var sample = CreateSample(
            teamClassPosition: 7,
            teamF2TimeSeconds: 125d,
            classLeaderF2TimeSeconds: 0d,
            teamCarClass: 4098,
            classCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 23,
                    LapCompleted: -1,
                    LapDistPct: -1d,
                    F2TimeSeconds: 62.75d,
                    EstimatedTimeSeconds: null,
                    Position: 2,
                    ClassPosition: 2,
                    CarClass: 4098,
                    TrackSurface: null,
                    OnPitRoad: null)
            ]);

        var gap = LiveLeaderGapSnapshot.From(sample);

        var timingOnlyCar = Assert.Single(gap.ClassCars, car => car.CarIdx == 23);
        Assert.Equal(62.75d, timingOnlyCar.GapSecondsToClassLeader!.Value);
        Assert.Equal(2, timingOnlyCar.ClassPosition);
    }

    [Fact]
    public void LeaderGap_IgnoresOtherClassP1Rows()
    {
        var sample = CreateSample(
            teamClassPosition: 6,
            teamCarClass: 4098,
            teamF2TimeSeconds: 90d,
            classLeaderF2TimeSeconds: 0d,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 55,
                    LapCompleted: 5,
                    LapDistPct: 0.51d,
                    F2TimeSeconds: 5d,
                    EstimatedTimeSeconds: 51d,
                    Position: 1,
                    ClassPosition: 1,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]);

        var gap = LiveLeaderGapSnapshot.From(sample);

        Assert.DoesNotContain(gap.ClassCars, car => car.CarIdx == 55);
        Assert.Single(gap.ClassCars, car => car.IsClassLeader);
        Assert.Equal(2, gap.ClassCars.Single(car => car.IsClassLeader).CarIdx);
    }

    private static HistoricalTelemetrySample CreateSample(
        double teamLapDistPct = 0.42d,
        int? carLeftRight = null,
        IReadOnlyList<HistoricalCarProximity>? nearbyCars = null,
        IReadOnlyList<HistoricalCarProximity>? classCars = null,
        int? teamPosition = 2,
        int? teamClassPosition = 2,
        int? teamCarClass = null,
        double? teamF2TimeSeconds = null,
        double? teamEstimatedTimeSeconds = null,
        double? leaderF2TimeSeconds = null,
        double? classLeaderF2TimeSeconds = null,
        bool onPitRoad = false)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            SessionTime: 123d,
            SessionTick: 100,
            SessionInfoUpdate: 1,
            IsOnTrack: true,
            IsInGarage: false,
            OnPitRoad: onPitRoad,
            PitstopActive: false,
            PlayerCarInPitStall: onPitRoad,
            FuelLevelLiters: 42d,
            FuelLevelPercent: 0.4d,
            FuelUsePerHourKg: 60d,
            SpeedMetersPerSecond: 50d,
            Lap: 6,
            LapCompleted: 5,
            LapDistPct: teamLapDistPct,
            LapLastLapTimeSeconds: 100d,
            LapBestLapTimeSeconds: 99d,
            AirTempC: 20d,
            TrackTempCrewC: 24d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            PlayerCarIdx: 10,
            TeamLapCompleted: 5,
            TeamLapDistPct: teamLapDistPct,
            TeamF2TimeSeconds: teamF2TimeSeconds,
            TeamEstimatedTimeSeconds: teamEstimatedTimeSeconds,
            TeamPosition: teamPosition,
            TeamClassPosition: teamClassPosition,
            TeamCarClass: teamCarClass,
            LeaderCarIdx: teamPosition == 1 ? 10 : 1,
            LeaderLapCompleted: 5,
            LeaderLapDistPct: 0.75d,
            LeaderF2TimeSeconds: leaderF2TimeSeconds,
            ClassLeaderCarIdx: teamClassPosition == 1 ? 10 : 2,
            ClassLeaderLapCompleted: 5,
            ClassLeaderLapDistPct: 0.66d,
            ClassLeaderF2TimeSeconds: classLeaderF2TimeSeconds,
            CarLeftRight: carLeftRight,
            NearbyCars: nearbyCars,
            ClassCars: classCars);
    }
}
