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
    public void RecordFrame_CarriesSessionInfoClassColorToProximityCars()
    {
        var store = new LiveTelemetryStore();
        store.ApplySessionInfo("""
DriverInfo:
 DriverCarIdx: 10
 Drivers:
 - CarIdx: 10
   CarClassColor: 0xffda59
 - CarIdx: 51
   CarClassColor: 0x33ceff
""");

        store.RecordFrame(CreateSample(
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            teamEstimatedTimeSeconds: 50d,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 51,
                    LapCompleted: 2,
                    LapDistPct: 0.51d,
                    F2TimeSeconds: null,
                    EstimatedTimeSeconds: 51d,
                    Position: 3,
                    ClassPosition: 1,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));

        var car = Assert.Single(store.Snapshot().Proximity.NearbyCars);
        Assert.Equal("#33CEFF", car.CarClassColorHex);
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
                    LapDistPct: 0.455d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: 45.5d,
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
        Assert.Equal(-4.5d, approach.RelativeSeconds!.Value, precision: 6);
        Assert.Equal(approach, snapshot.Proximity.StrongestMulticlassApproach);
    }

    [Fact]
    public void RecordFrame_DoesNotSurfaceMulticlassApproachInsideCloseRadarRange()
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
                    LapDistPct: 0.485d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: 48.5d,
                    Position: 3,
                    ClassPosition: 1,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));

        var snapshot = store.Snapshot();

        Assert.Empty(snapshot.Proximity.MulticlassApproaches);
        Assert.Null(snapshot.Proximity.StrongestMulticlassApproach);
    }

    [Fact]
    public void RecordFrame_ClearsMulticlassClosingHistoryWhenCameraFocusChanges()
    {
        var store = new LiveTelemetryStore();
        var startedAtUtc = DateTimeOffset.UtcNow;

        store.RecordFrame(CreateSample(
            capturedAtUtc: startedAtUtc,
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            teamEstimatedTimeSeconds: 50d,
            teamCarClass: 4098,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 51,
                    LapCompleted: 2,
                    LapDistPct: 0.455d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: 45.5d,
                    Position: 3,
                    ClassPosition: 1,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));
        store.RecordFrame(CreateSample(
            capturedAtUtc: startedAtUtc.AddSeconds(1),
            playerCarIdx: 10,
            teamCarClass: 4098,
            focusCarIdx: 30,
            focusLapDistPct: 0.50d,
            focusEstimatedTimeSeconds: 50d,
            focusCarClass: 4098,
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 51,
                    LapCompleted: 2,
                    LapDistPct: 0.465d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: 46.5d,
                    Position: 3,
                    ClassPosition: 1,
                    CarClass: 4099,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));

        var snapshot = store.Snapshot();

        var approach = Assert.Single(snapshot.Proximity.MulticlassApproaches);
        Assert.Equal(51, approach.CarIdx);
        Assert.Null(approach.ClosingRateSecondsPerSecond);
    }

    [Fact]
    public void RecordFrame_BuildsSharedRaceModelsFromCurrentSnapshot()
    {
        var store = new LiveTelemetryStore();
        store.ApplySessionInfo("""
WeekendInfo:
 TrackDisplayName: Test Circuit
 TrackLength: 5.100
 EventType: Race
 TeamRacing: 1
 TrackWeatherType: Static
 TrackSkies: Partly Cloudy
 TrackPrecipitation: 12
SessionInfo:
 CurrentSessionNum: 0
 Sessions:
 - SessionNum: 0
   SessionType: Race
   SessionName: Race
   SessionTrackRubberState: Moderate Usage
DriverInfo:
 DriverCarIdx: 10
 DriverCarFuelKgPerLtr: 0.75
 Drivers:
 - CarIdx: 10
   UserName: Driver One
   TeamName: Team One
   CarNumber: 71
   CarClassID: 4098
   CarClassShortName: GT3
   CarClassColor: 0xffda59
 - CarIdx: 11
   UserName: Leader One
   TeamName: Leader Team
   CarNumber: 1
   CarClassID: 4098
   CarClassShortName: GT3
   CarClassColor: 0xffda59
 - CarIdx: 12
   UserName: Chaser One
   TeamName: Chaser Team
   CarNumber: 12
   CarClassID: 4098
   CarClassShortName: GT3
   CarClassColor: 0xffda59
""");

        store.RecordFrame(CreateSample(
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            teamEstimatedTimeSeconds: 50d,
            teamCarClass: 4098,
            teamPosition: 7,
            teamClassPosition: 3,
            classLeaderCarIdx: 11,
            classLeaderLapDistPct: 0.53d,
            classLeaderF2TimeSeconds: 0d,
            focusClassCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 11,
                    LapCompleted: 2,
                    LapDistPct: 0.53d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: 53d,
                    Position: 1,
                    ClassPosition: 1,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false),
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 2,
                    LapDistPct: 0.49d,
                    F2TimeSeconds: 4d,
                    EstimatedTimeSeconds: 49d,
                    Position: 8,
                    ClassPosition: 4,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ],
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 2,
                    LapDistPct: 0.49d,
                    F2TimeSeconds: 4d,
                    EstimatedTimeSeconds: 49d,
                    Position: 8,
                    ClassPosition: 4,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));

        var models = store.Snapshot().Models;

        Assert.True(models.Session.HasData);
        Assert.Equal("Race", models.Session.SessionType);
        Assert.Equal(5100d, models.Spatial.TrackLengthMeters!.Value, precision: 6);
        Assert.Equal(10, models.DriverDirectory.PlayerCarIdx);
        Assert.Equal("Driver One", models.DriverDirectory.PlayerDriver?.DriverName);
        Assert.Equal(10, models.Timing.FocusRow?.CarIdx);
        Assert.Contains(models.Timing.ClassRows, row => row.CarIdx == 12 && row.DriverName == "Chaser One");
        Assert.Contains(models.Relative.Rows, row => row.CarIdx == 12 && row.IsBehind);
        Assert.Equal("Partly Cloudy", models.Weather.SkiesLabel);
        Assert.True(models.FuelPit.Fuel.HasValidFuel);
    }

    [Fact]
    public void LiveModelParityAnalyzer_HasNoMismatchesForCurrentOverlayInputs()
    {
        var store = new LiveTelemetryStore();

        store.RecordFrame(CreateSample(
            playerCarIdx: 10,
            teamLapDistPct: 0.50d,
            teamEstimatedTimeSeconds: 50d,
            teamCarClass: 4098,
            teamPosition: 7,
            teamClassPosition: 3,
            classLeaderCarIdx: 11,
            classLeaderLapDistPct: 0.53d,
            classLeaderF2TimeSeconds: 0d,
            focusClassCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 11,
                    LapCompleted: 2,
                    LapDistPct: 0.53d,
                    F2TimeSeconds: 0d,
                    EstimatedTimeSeconds: 53d,
                    Position: 1,
                    ClassPosition: 1,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false),
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 2,
                    LapDistPct: 0.49d,
                    F2TimeSeconds: 4d,
                    EstimatedTimeSeconds: 49d,
                    Position: 8,
                    ClassPosition: 4,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ],
            nearbyCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 2,
                    LapDistPct: 0.49d,
                    F2TimeSeconds: 4d,
                    EstimatedTimeSeconds: 49d,
                    Position: 8,
                    ClassPosition: 4,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ]));

        var parity = LiveModelParityAnalyzer.Analyze(store.Snapshot());

        Assert.False(parity.HasMismatch);
        Assert.Empty(parity.Observations);
        Assert.True(parity.Coverage.HasLegacyFuel);
        Assert.True(parity.Coverage.HasModelTiming);
    }

    [Fact]
    public void LiveModelParityAnalyzer_ReportsModelMismatch()
    {
        var store = new LiveTelemetryStore();
        store.RecordFrame(CreateSample());
        var snapshot = store.Snapshot();
        var mismatched = snapshot with
        {
            Models = snapshot.Models with
            {
                Weather = snapshot.Models.Weather with
                {
                    TrackWetness = 6
                }
            }
        };

        var parity = LiveModelParityAnalyzer.Analyze(mismatched);

        Assert.True(parity.HasMismatch);
        Assert.Contains(parity.Observations, observation =>
            observation.Family == "weather"
            && observation.Key == "track-wetness"
            && observation.LegacyValue == "1"
            && observation.ModelValue == "6");
    }

    [Fact]
    public void RecordFrame_KeepsModelsBackwardCompatibleWhenTimingIsUnavailable()
    {
        var store = new LiveTelemetryStore();

        store.RecordFrame(CreateSample(
            fuelLevelLiters: 0d,
            playerCarIdx: null,
            teamLapDistPct: null,
            teamEstimatedTimeSeconds: null,
            nearbyCars: null));

        var models = store.Snapshot().Models;

        Assert.False(models.Timing.HasData);
        Assert.Equal(LiveModelQuality.Unavailable, models.Timing.Quality);
        Assert.False(models.Relative.HasData);
        Assert.Equal(LiveModelQuality.Unavailable, models.FuelPit.Quality);
        Assert.True(models.Session.HasData);
    }

    [Fact]
    public void TimingColumnRegistry_ProvidesReusableDefaultColumns()
    {
        var keys = TimingColumnRegistry.All.Select(column => column.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(TimingColumnRegistry.Driver, keys);
        Assert.Contains(TimingColumnRegistry.Gap, keys);
        Assert.Contains(TimingColumnRegistry.Interval, keys);
        Assert.Contains(TimingColumnRegistry.Pit, keys);

        var gapColumn = Assert.Single(TimingColumnRegistry.All, column => column.Key == TimingColumnRegistry.Gap);
        var row = new LiveTimingRow(
            CarIdx: 10,
            Quality: LiveModelQuality.Reliable,
            Source: "test",
            IsPlayer: true,
            IsFocus: true,
            IsOverallLeader: false,
            IsClassLeader: false,
            DriverName: "Driver One",
            TeamName: "Team One",
            CarNumber: "71",
            CarClassName: "GT3",
            CarClassColorHex: "#FFDA59",
            OverallPosition: 7,
            ClassPosition: 3,
            CarClass: 4098,
            LapCompleted: 2,
            LapDistPct: 0.5d,
            ProgressLaps: 2.5d,
            F2TimeSeconds: 5d,
            EstimatedTimeSeconds: 50d,
            LastLapTimeSeconds: 90d,
            BestLapTimeSeconds: 89d,
            GapSecondsToClassLeader: 5.1234d,
            GapLapsToClassLeader: null,
            DeltaSecondsToFocus: 0d,
            TrackSurface: 3,
            OnPitRoad: false);

        Assert.Equal("5.123", gapColumn.FormatValue(row));
    }

    private static HistoricalTelemetrySample CreateSample(
        DateTimeOffset? capturedAtUtc = null,
        double fuelLevelLiters = 42d,
        double fuelLevelPercent = 0.4d,
        double fuelUsePerHourKg = 60d,
        int? playerCarIdx = null,
        double? teamLapDistPct = null,
        double? teamEstimatedTimeSeconds = null,
        int? teamCarClass = null,
        int? teamPosition = null,
        int? teamClassPosition = null,
        int? focusCarIdx = null,
        double? focusLapDistPct = null,
        double? focusEstimatedTimeSeconds = null,
        int? focusCarClass = null,
        int? classLeaderCarIdx = null,
        double? classLeaderLapDistPct = null,
        double? classLeaderF2TimeSeconds = null,
        IReadOnlyList<HistoricalCarProximity>? focusClassCars = null,
        IReadOnlyList<HistoricalCarProximity>? nearbyCars = null)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: capturedAtUtc ?? DateTimeOffset.UtcNow,
            SessionTime: 123d,
            SessionTick: 100,
            SessionInfoUpdate: 1,
            IsOnTrack: true,
            IsInGarage: false,
            OnPitRoad: false,
            PitstopActive: false,
            PlayerCarInPitStall: false,
            FuelLevelLiters: fuelLevelLiters,
            FuelLevelPercent: fuelLevelPercent,
            FuelUsePerHourKg: fuelUsePerHourKg,
            SpeedMetersPerSecond: 50d,
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
            FocusCarIdx: focusCarIdx,
            FocusLapCompleted: focusLapDistPct is null ? null : 2,
            FocusLapDistPct: focusLapDistPct,
            FocusEstimatedTimeSeconds: focusEstimatedTimeSeconds,
            FocusCarClass: focusCarClass,
            TeamLapCompleted: teamLapDistPct is null ? null : 2,
            TeamLapDistPct: teamLapDistPct,
            TeamEstimatedTimeSeconds: teamEstimatedTimeSeconds,
            TeamF2TimeSeconds: teamEstimatedTimeSeconds,
            TeamPosition: teamPosition,
            TeamClassPosition: teamClassPosition,
            TeamCarClass: teamCarClass,
            FocusClassLeaderCarIdx: classLeaderCarIdx,
            FocusClassLeaderLapCompleted: classLeaderLapDistPct is null ? null : 2,
            FocusClassLeaderLapDistPct: classLeaderLapDistPct,
            FocusClassLeaderF2TimeSeconds: classLeaderF2TimeSeconds,
            FocusClassCars: focusClassCars,
            NearbyCars: nearbyCars);
    }
}
