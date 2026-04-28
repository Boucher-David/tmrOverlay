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

    private static HistoricalTelemetrySample CreateSample(
        DateTimeOffset? capturedAtUtc = null,
        double fuelLevelLiters = 42d,
        double fuelLevelPercent = 0.4d,
        double fuelUsePerHourKg = 60d,
        int? playerCarIdx = null,
        double? teamLapDistPct = null,
        double? teamEstimatedTimeSeconds = null,
        int? teamCarClass = null,
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
            TeamLapCompleted: teamLapDistPct is null ? null : 2,
            TeamLapDistPct: teamLapDistPct,
            TeamEstimatedTimeSeconds: teamEstimatedTimeSeconds,
            TeamCarClass: teamCarClass,
            NearbyCars: nearbyCars);
    }
}
