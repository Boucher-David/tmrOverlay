using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.EdgeCases;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class TelemetryEdgeCaseDetectorTests
{
    [Fact]
    public void Analyze_CapturesSideOccupancyWithoutAdjacentTimedCar()
    {
        var detector = new TelemetryEdgeCaseDetector();

        var observations = detector.Analyze(
            CreateSample(carLeftRight: 2, nearbyCars: []),
            RawTelemetryWatchSnapshot.Empty);

        Assert.Contains(observations, observation =>
            observation.Key == "side-occupancy.no-adjacent-car"
            && observation.Severity == TelemetryEdgeCaseSeverity.Warning);
    }

    [Fact]
    public void Analyze_CapturesPreviouslyUnseenRawEngineeringChannels()
    {
        var detector = new TelemetryEdgeCaseDetector();

        var observations = detector.Analyze(
            CreateSample(),
            new RawTelemetryWatchSnapshot(new Dictionary<string, double>
            {
                ["LFwearL"] = 94.5d,
                ["RFwearL"] = 93.8d
            }));

        Assert.Contains(observations, observation =>
            observation.Key == "raw.tires.wear.active"
            && observation.Fields.ContainsKey("LFwearL"));
    }

    [Fact]
    public void Analyze_CapturesIncidentCounterIncrease()
    {
        var detector = new TelemetryEdgeCaseDetector();
        detector.Analyze(
            CreateSample(sessionTime: 1d, sessionTick: 1),
            new RawTelemetryWatchSnapshot(new Dictionary<string, double>
            {
                ["PlayerCarTeamIncidentCount"] = 0d
            }));

        var observations = detector.Analyze(
            CreateSample(sessionTime: 2d, sessionTick: 2),
            new RawTelemetryWatchSnapshot(new Dictionary<string, double>
            {
                ["PlayerCarTeamIncidentCount"] = 2d
            }));

        Assert.Contains(observations, observation =>
            observation.Key.StartsWith("raw.incident-count-increased.PlayerCarTeamIncidentCount.", StringComparison.Ordinal));
    }

    private static HistoricalTelemetrySample CreateSample(
        double sessionTime = 123d,
        int sessionTick = 100,
        int? carLeftRight = null,
        IReadOnlyList<HistoricalCarProximity>? nearbyCars = null)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: DateTimeOffset.Parse("2026-04-29T12:00:00Z").AddSeconds(sessionTime),
            SessionTime: sessionTime,
            SessionTick: sessionTick,
            SessionInfoUpdate: 1,
            IsOnTrack: true,
            IsInGarage: false,
            OnPitRoad: false,
            PitstopActive: false,
            PlayerCarInPitStall: false,
            FuelLevelLiters: 42d,
            FuelLevelPercent: 0.4d,
            FuelUsePerHourKg: 60d,
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
            PlayerCarIdx: 10,
            FocusCarIdx: 10,
            FocusLapCompleted: 2,
            FocusLapDistPct: 0.5d,
            TeamLapCompleted: 2,
            TeamLapDistPct: 0.5d,
            TeamOnPitRoad: false,
            CarLeftRight: carLeftRight,
            NearbyCars: nearbyCars ?? []);
    }
}
