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
            observation.Key == "raw.startup-engineering-baseline"
            && observation.Fields.ContainsKey("tires.wear.LFwearL"));
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

    [Fact]
    public void Analyze_DowngradesZeroF2TimingDuringGridContext()
    {
        var detector = new TelemetryEdgeCaseDetector();
        detector.Analyze(
            CreateSample(
                sessionTime: 1d,
                sessionTick: 1,
                speedMetersPerSecond: 0d,
                lapCompleted: 0,
                lapDistPct: 0d,
                focusLapCompleted: 0,
                focusLapDistPct: 0d,
                sessionState: 3),
            RawTelemetryWatchSnapshot.Empty);

        var observations = detector.Analyze(
            CreateSample(
                sessionTime: 2d,
                sessionTick: 2,
                speedMetersPerSecond: 0d,
                lapCompleted: 0,
                lapDistPct: 0d,
                focusLapCompleted: 0,
                focusLapDistPct: 0d,
                focusF2TimeSeconds: 0d,
                focusEstimatedTimeSeconds: 0d,
                sessionState: 3,
                nearbyCars:
                [
                    new HistoricalCarProximity(
                        CarIdx: 1,
                        LapCompleted: 0,
                        LapDistPct: 0.001d,
                        F2TimeSeconds: 0d,
                        EstimatedTimeSeconds: 0d,
                        Position: 1,
                        ClassPosition: 1,
                        CarClass: 4098,
                        TrackSurface: 3,
                        OnPitRoad: false)
                ]),
            RawTelemetryWatchSnapshot.Empty);

        var observation = Assert.Single(observations, observation =>
            observation.Key == "timing.uninitialized-start-context.CarIdxF2Time.car-1");
        Assert.Equal(TelemetryEdgeCaseSeverity.Info, observation.Severity);
        Assert.Equal("stationary-grid", observation.Fields["context"]);
        Assert.DoesNotContain(observations, observation =>
            observation.Key == "timing.zero.CarIdxF2Time.car-1");
    }

    [Fact]
    public void Analyze_WarnsForZeroF2TimingAfterStartContext()
    {
        var detector = new TelemetryEdgeCaseDetector();
        detector.Analyze(
            CreateSample(
                sessionTime: 1d,
                sessionTick: 1,
                focusF2TimeSeconds: 20d,
                focusEstimatedTimeSeconds: 100d,
                sessionState: 4),
            RawTelemetryWatchSnapshot.Empty);

        var observations = detector.Analyze(
            CreateSample(
                sessionTime: 2d,
                sessionTick: 2,
                focusF2TimeSeconds: 0d,
                focusEstimatedTimeSeconds: 100d,
                sessionState: 4,
                nearbyCars:
                [
                    new HistoricalCarProximity(
                        CarIdx: 1,
                        LapCompleted: 2,
                        LapDistPct: 0.501d,
                        F2TimeSeconds: 0d,
                        EstimatedTimeSeconds: 101d,
                        Position: 1,
                        ClassPosition: 1,
                        CarClass: 4098,
                        TrackSurface: 3,
                        OnPitRoad: false)
                ]),
            RawTelemetryWatchSnapshot.Empty);

        Assert.Contains(observations, observation =>
            observation.Key == "timing.zero.CarIdxF2Time.car-1"
            && observation.Severity == TelemetryEdgeCaseSeverity.Warning);
        Assert.DoesNotContain(observations, observation =>
            observation.Key == "timing.uninitialized-start-context.CarIdxF2Time.car-1");
    }

    [Fact]
    public void Analyze_GroupsActivePitCommands()
    {
        var detector = new TelemetryEdgeCaseDetector();

        var observations = detector.Analyze(
            CreateSample(isOnTrack: false, speedMetersPerSecond: 0d),
            new RawTelemetryWatchSnapshot(new Dictionary<string, double>
            {
                ["dpLFTireChange"] = 1d,
                ["dpRFTireChange"] = 1d,
                ["dpFuelFill"] = 1d
            }));

        var pitCommands = Assert.Single(observations, observation => observation.Key == "raw.pit-commands.active");
        Assert.Contains("dpLFTireChange", pitCommands.Fields["variables"] ?? string.Empty);
        Assert.Equal(1, observations.Count(observation =>
            observation.Key.StartsWith("raw.pit-commands.", StringComparison.Ordinal)));
    }

    [Fact]
    public void Analyze_DowngradesEngineWarningsWhenEngineAppearsOff()
    {
        var detector = new TelemetryEdgeCaseDetector();

        var observations = detector.Analyze(
            CreateSample(isOnTrack: false, speedMetersPerSecond: 0d),
            new RawTelemetryWatchSnapshot(new Dictionary<string, double>
            {
                ["EngineWarnings"] = 14d,
                ["RPM"] = 0d,
                ["OilPress"] = 0d
            }));

        Assert.Contains(observations, observation =>
            observation.Key == "raw.engine-warning.engine-off"
            && observation.Severity == TelemetryEdgeCaseSeverity.Info);
    }

    [Fact]
    public void Analyze_DowngradesTireSetInitializationDuringGridContext()
    {
        var detector = new TelemetryEdgeCaseDetector();
        detector.Analyze(
            CreateSample(sessionTime: 1d, sessionTick: 1, isOnTrack: false, tireSetsUsed: 0),
            RawTelemetryWatchSnapshot.Empty);

        var observations = detector.Analyze(
            CreateSample(sessionTime: 2d, sessionTick: 2, isOnTrack: true, speedMetersPerSecond: 0d, tireSetsUsed: 1),
            RawTelemetryWatchSnapshot.Empty);

        Assert.Contains(observations, observation =>
            observation.Key == "tires.set-count-initialized"
            && observation.Severity == TelemetryEdgeCaseSeverity.Info);
        Assert.DoesNotContain(observations, observation =>
            observation.Key == "tires.set-count-increased-outside-pit-context");
    }

    [Fact]
    public void Analyze_RecordsActiveResetContextForProgressJump()
    {
        var detector = new TelemetryEdgeCaseDetector();
        detector.Analyze(
            CreateSample(
                sessionTime: 1d,
                sessionTick: 1,
                lapCompleted: -1,
                lapDistPct: 0.50d,
                focusLapCompleted: -1,
                focusLapDistPct: 0.50d),
            RawTelemetryWatchSnapshot.Empty);

        var observations = detector.Analyze(
            CreateSample(
                sessionTime: 2d,
                sessionTick: 2,
                lapCompleted: -1,
                lapDistPct: 0.92d,
                focusLapCompleted: -1,
                focusLapDistPct: 0.92d),
            new RawTelemetryWatchSnapshot(new Dictionary<string, double>
            {
                ["EnterExitReset"] = 2d
            }));

        Assert.Contains(observations, observation =>
            observation.Key == "progress.discontinuity.active-reset"
            && observation.Severity == TelemetryEdgeCaseSeverity.Info
            && observation.Fields["context"] == "active-reset");
        Assert.Contains(observations, observation =>
            observation.Key == "raw.active-reset.reset-key-action"
            && observation.Severity == TelemetryEdgeCaseSeverity.Info);
    }

    [Fact]
    public void Analyze_DoesNotFlagNormalStartFinishWrapWhenLapCounterIsUnavailable()
    {
        var detector = new TelemetryEdgeCaseDetector();
        detector.Analyze(
            CreateSample(
                sessionTime: 1d,
                sessionTick: 1,
                lapCompleted: -1,
                lapDistPct: 0.99d,
                focusLapCompleted: -1,
                focusLapDistPct: 0.99d),
            RawTelemetryWatchSnapshot.Empty);

        var observations = detector.Analyze(
            CreateSample(
                sessionTime: 2d,
                sessionTick: 2,
                lapCompleted: -1,
                lapDistPct: 0.01d,
                focusLapCompleted: -1,
                focusLapDistPct: 0.01d),
            RawTelemetryWatchSnapshot.Empty);

        Assert.DoesNotContain(observations, observation =>
            observation.Key.StartsWith("progress.discontinuity.", StringComparison.Ordinal));
    }

    private static HistoricalTelemetrySample CreateSample(
        double sessionTime = 123d,
        int sessionTick = 100,
        bool isOnTrack = true,
        double speedMetersPerSecond = 50d,
        int lapCompleted = 2,
        double lapDistPct = 0.5d,
        int? focusLapCompleted = 2,
        double? focusLapDistPct = 0.5d,
        int? sessionState = null,
        int? carLeftRight = null,
        double? focusF2TimeSeconds = null,
        double? focusEstimatedTimeSeconds = null,
        int? tireSetsUsed = null,
        IReadOnlyList<HistoricalCarProximity>? nearbyCars = null)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: DateTimeOffset.Parse("2026-04-29T12:00:00Z").AddSeconds(sessionTime),
            SessionTime: sessionTime,
            SessionTick: sessionTick,
            SessionInfoUpdate: 1,
            IsOnTrack: isOnTrack,
            IsInGarage: false,
            OnPitRoad: false,
            PitstopActive: false,
            PlayerCarInPitStall: false,
            FuelLevelLiters: 42d,
            FuelLevelPercent: 0.4d,
            FuelUsePerHourKg: 60d,
            SpeedMetersPerSecond: speedMetersPerSecond,
            Lap: 3,
            LapCompleted: lapCompleted,
            LapDistPct: lapDistPct,
            LapLastLapTimeSeconds: 90d,
            LapBestLapTimeSeconds: 89d,
            AirTempC: 20d,
            TrackTempCrewC: 24d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            SessionState: sessionState,
            PlayerCarIdx: 10,
            FocusCarIdx: 10,
            FocusLapCompleted: focusLapCompleted,
            FocusLapDistPct: focusLapDistPct,
            FocusF2TimeSeconds: focusF2TimeSeconds,
            FocusEstimatedTimeSeconds: focusEstimatedTimeSeconds,
            TeamLapCompleted: lapCompleted,
            TeamLapDistPct: lapDistPct,
            TeamOnPitRoad: false,
            CarLeftRight: carLeftRight,
            NearbyCars: nearbyCars ?? [],
            TireSetsUsed: tireSetsUsed);
    }
}
