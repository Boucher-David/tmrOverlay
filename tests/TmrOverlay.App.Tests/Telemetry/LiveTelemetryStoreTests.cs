using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class LiveTelemetryStoreTests
{
    [Fact]
    public void LiveFuelSnapshot_FromConvertsFuelUseToLitersAndProjection()
    {
        var context = new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity
            {
                DriverCarFuelKgPerLiter = 0.75d,
                DriverCarEstLapTimeSeconds = 90d
            },
            Track = new HistoricalTrackIdentity(),
            Session = new HistoricalSessionIdentity(),
            Conditions = new HistoricalSessionInfoConditions()
        };
        var sample = CreateSample(
            fuelLevelLiters: 50d,
            fuelLevelPercent: 0.5d,
            fuelUsePerHourKg: 75d);

        var fuel = LiveFuelSnapshot.From(context, sample);

        Assert.True(fuel.HasValidFuel);
        Assert.Equal("local-driver-scalar", fuel.Source);
        Assert.Equal(100d, fuel.FuelUsePerHourLiters);
        Assert.Equal(2.5d, fuel.FuelPerLapLiters);
        Assert.Equal(90d, fuel.LapTimeSeconds);
        Assert.Equal("player-last-lap", fuel.LapTimeSource);
        Assert.Equal(30d, fuel.EstimatedMinutesRemaining);
        Assert.Equal(20d, fuel.EstimatedLapsRemaining);
        Assert.Equal("live", fuel.Confidence);
    }

    [Fact]
    public void RecordFrame_PublishesLatestSampleAndFuel()
    {
        var store = new LiveTelemetryStore();
        var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5);
        var capturedAtUtc = DateTimeOffset.UtcNow;

        store.MarkConnected();
        store.MarkCollectionStarted("session-test", startedAtUtc);
        store.RecordFrame(CreateSample(capturedAtUtc: capturedAtUtc));

        var snapshot = store.Snapshot();

        Assert.True(snapshot.IsConnected);
        Assert.True(snapshot.IsCollecting);
        Assert.Equal("session-test", snapshot.SourceId);
        Assert.Equal(startedAtUtc, snapshot.StartedAtUtc);
        Assert.Equal(capturedAtUtc, snapshot.LastUpdatedAtUtc);
        Assert.NotNull(snapshot.LatestSample);
        Assert.True(snapshot.Fuel.HasValidFuel);
    }

    [Fact]
    public void RecordFrame_SurfacesMulticlassApproachOutsideCloseRadarRange()
    {
        var store = new LiveTelemetryStore();

        store.RecordFrame(CreateSample(
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            teamEstimatedTimeSeconds: 50d,
            teamCarClass: 4098,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 51,
                    LapCompleted: 2,
                    LapDistPct: 0.32d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: 32d,
                    Position: 3,
                    ClassPosition: 1,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));

        var snapshot = store.Snapshot();

        var approach = Assert.Single(snapshot.Proximity.MulticlassApproaches);
        Assert.Equal(51, approach.CarIdx);
        Assert.Equal(4099, approach.CarClass);
        Assert.Equal(-18d, approach.RelativeSeconds!.Value, precision: 6);
        Assert.Equal(approach, snapshot.Proximity.StrongestMulticlassApproach);
    }

    [Fact]
    public void RecordFrame_UsesFocusedCarForRadarAndGapWithoutReplacingTeamFuelContext()
    {
        var store = new LiveTelemetryStore();

        store.RecordFrame(CreateSample(
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            teamEstimatedTimeSeconds: 50d,
            teamCarClass: 4098,
            focusCarIdx: 20,
            focusLapDistPct: 0.20d,
            focusEstimatedTimeSeconds: 20d,
            focusF2TimeSeconds: 55d,
            focusPosition: 4,
            focusClassPosition: 2,
            focusCarClass: 4099,
            focusClassLeaderCarIdx: 51,
            focusClassLeaderF2TimeSeconds: 12d,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 21,
                    LapCompleted: 2,
                    LapDistPct: 0.21d,
                    F2TimeSeconds: 57d,
                    EstimatedTimeSeconds: 21d,
                    Position: 5,
                    ClassPosition: 3,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ],
            classCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 21,
                    LapCompleted: 2,
                    LapDistPct: 0.21d,
                    F2TimeSeconds: 57d,
                    EstimatedTimeSeconds: 21d,
                    Position: 5,
                    ClassPosition: 3,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));

        var snapshot = store.Snapshot();

        Assert.Equal(10, snapshot.TeamCar.CarIdx);
        Assert.Equal(20, snapshot.FocusCar.CarIdx);
        Assert.False(snapshot.FocusCar.IsTeamCar);
        Assert.Equal(21, snapshot.Proximity.NearestAhead?.CarIdx);
        Assert.Equal(1d, snapshot.Proximity.NearestAhead!.RelativeSeconds!.Value, precision: 6);
        Assert.Equal(20, snapshot.LeaderGap.ClassCars.Single(car => car.IsTeamCar).CarIdx);
        Assert.Equal(43d, snapshot.LeaderGap.ClassLeaderGap.Seconds!.Value);
    }

    [Fact]
    public void RecordFrame_TracksFocusedRivalStintFromObservedPitExit()
    {
        var store = new LiveTelemetryStore();

        store.RecordFrame(CreateSample(
            capturedAtUtc: DateTimeOffset.UtcNow,
            sessionTime: 100d,
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            focusCarIdx: 20,
            focusLapCompleted: 5,
            focusLapDistPct: 0.00d,
            focusOnPitRoad: true));
        store.RecordFrame(CreateSample(
            capturedAtUtc: DateTimeOffset.UtcNow.AddSeconds(10),
            sessionTime: 110d,
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            focusCarIdx: 20,
            focusLapCompleted: 5,
            focusLapDistPct: 0.05d,
            focusOnPitRoad: false));
        store.RecordFrame(CreateSample(
            capturedAtUtc: DateTimeOffset.UtcNow.AddSeconds(20),
            sessionTime: 200d,
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            focusCarIdx: 20,
            focusLapCompleted: 5,
            focusLapDistPct: 0.55d,
            focusOnPitRoad: false));

        var focus = store.Snapshot().FocusCar;

        Assert.Equal(20, focus.CarIdx);
        Assert.Equal(0.5d, focus.CurrentStintLaps!.Value, precision: 6);
        Assert.Equal(90d, focus.CurrentStintSeconds!.Value, precision: 6);
        Assert.Equal(1, focus.ObservedPitStopCount);
        Assert.Equal("observed pit exit", focus.StintSource);
    }

    [Fact]
    public void RecordFrame_TracksTelemetryAvailabilityForSpectatedTiming()
    {
        var store = new LiveTelemetryStore();

        store.MarkCollectionStarted("spectated-practice", DateTimeOffset.UtcNow);
        store.RecordFrame(CreateSample(
            isOnTrack: false,
            speedMetersPerSecond: 0d,
            fuelLevelLiters: 0d,
            playerCarIdx: 10,
            focusCarIdx: 61,
            focusF2TimeSeconds: 23.6d,
            focusPosition: 26,
            focusClassPosition: 25,
            classCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 2,
                    LapCompleted: -1,
                    LapDistPct: -1d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: null,
                    Position: 1,
                    ClassPosition: 1,
                    CarClass: 4098,
                    TrackSurface: null,
                    OnPitRoad: null)
            ]));
        store.RecordFrame(CreateSample(
            isOnTrack: false,
            speedMetersPerSecond: 0d,
            fuelLevelLiters: 0d,
            playerCarIdx: 10,
            focusCarIdx: 31,
            focusF2TimeSeconds: 24.1d,
            focusPosition: 27,
            focusClassPosition: 26));

        var availability = store.Snapshot().TelemetryAvailability;

        Assert.Equal(2, availability.SampleFrameCount);
        Assert.Equal(0, availability.LocalDrivingFrameCount);
        Assert.Equal(0, availability.LocalFuelScalarFrameCount);
        Assert.Equal(2, availability.LocalScalarIdleFrameCount);
        Assert.Equal(2, availability.NonTeamFocusFrameCount);
        Assert.Equal(1, availability.FocusCarChangeCount);
        Assert.Equal(2, availability.UniqueFocusCarCount);
        Assert.Equal(31, availability.CurrentFocusCarIdx);
        Assert.Collection(
            availability.FocusSegments,
            first =>
            {
                Assert.Equal(61, first.CarIdx);
                Assert.False(first.IsTeamCar);
                Assert.Equal(1, first.FrameCount);
                Assert.Equal(1, first.TimingFrameCount);
            },
            second =>
            {
                Assert.Equal(31, second.CarIdx);
                Assert.False(second.IsTeamCar);
                Assert.Equal(1, second.FrameCount);
                Assert.Equal(1, second.TimingFrameCount);
            });
        Assert.Equal(2, availability.FocusTimingFrameCount);
        Assert.Equal(1, availability.ClassTimingFrameCount);
        Assert.True(availability.IsSpectatedTimingOnly);
    }

    private static HistoricalTelemetrySample CreateSample(
        DateTimeOffset? capturedAtUtc = null,
        double sessionTime = 123d,
        bool isOnTrack = true,
        bool isInGarage = false,
        double speedMetersPerSecond = 50d,
        double fuelLevelLiters = 42d,
        double fuelLevelPercent = 0.4d,
        double fuelUsePerHourKg = 60d,
        int? playerCarIdx = null,
        double? teamLapDistPct = null,
        double? teamEstimatedTimeSeconds = null,
        int? teamCarClass = null,
        IReadOnlyList<HistoricalCarProximity>? nearbyCars = null,
        IReadOnlyList<HistoricalCarProximity>? classCars = null,
        int? focusCarIdx = null,
        int? focusLapCompleted = null,
        double? focusLapDistPct = null,
        double? focusEstimatedTimeSeconds = null,
        double? focusF2TimeSeconds = null,
        int? focusPosition = null,
        int? focusClassPosition = null,
        int? focusCarClass = null,
        bool? focusOnPitRoad = null,
        int? focusClassLeaderCarIdx = null,
        double? focusClassLeaderF2TimeSeconds = null)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: capturedAtUtc ?? DateTimeOffset.UtcNow,
            SessionTime: sessionTime,
            SessionTick: 100,
            SessionInfoUpdate: 1,
            IsOnTrack: isOnTrack,
            IsInGarage: isInGarage,
            OnPitRoad: false,
            PitstopActive: false,
            PlayerCarInPitStall: false,
            FuelLevelLiters: fuelLevelLiters,
            FuelLevelPercent: fuelLevelPercent,
            FuelUsePerHourKg: fuelUsePerHourKg,
            SpeedMetersPerSecond: speedMetersPerSecond,
            Lap: 3,
            LapCompleted: 2,
            LapDistPct: 0.5d,
            LapLastLapTimeSeconds: 90d,
            LapBestLapTimeSeconds: 89d,
            AirTempC: 20d,
            TrackTempCrewC: 24d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            PlayerCarIdx: playerCarIdx,
            TeamLapCompleted: teamLapDistPct is null ? null : 2,
            TeamLapDistPct: teamLapDistPct,
            TeamEstimatedTimeSeconds: teamEstimatedTimeSeconds,
            TeamCarClass: teamCarClass,
            NearbyCars: nearbyCars,
            ClassCars: classCars,
            FocusCarIdx: focusCarIdx,
            FocusLapCompleted: focusLapCompleted,
            FocusLapDistPct: focusLapDistPct,
            FocusEstimatedTimeSeconds: focusEstimatedTimeSeconds,
            FocusF2TimeSeconds: focusF2TimeSeconds,
            FocusPosition: focusPosition,
            FocusClassPosition: focusClassPosition,
            FocusCarClass: focusCarClass,
            FocusOnPitRoad: focusOnPitRoad,
            FocusClassLeaderCarIdx: focusClassLeaderCarIdx,
            FocusClassLeaderF2TimeSeconds: focusClassLeaderF2TimeSeconds);
    }
}
