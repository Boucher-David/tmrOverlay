using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Events;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class LiveOverlayDiagnosticsRecorderTests
{
    [Fact]
    public void CompleteCollection_WritesGapRadarFuelAndPositionDiagnostics()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-live-overlay-diagnostics-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var captureDirectory = Path.Combine(storage.CaptureRoot, "capture-diagnostics");
            Directory.CreateDirectory(captureDirectory);
            var recorder = new LiveOverlayDiagnosticsRecorder(
                new LiveOverlayDiagnosticsOptions
                {
                    MinimumFrameSpacingSeconds = 0.1d,
                    MaxSampleFramesPerSession = 10,
                    MaxEventExamplesPerSession = 20,
                    LargeGapSeconds = 600d,
                    GapJumpSeconds = 300d
                },
                storage,
                new AppEventRecorder(storage),
                NullLogger<LiveOverlayDiagnosticsRecorder>.Instance);
            var context = CreateContext();
            var startedAtUtc = DateTimeOffset.Parse("2026-05-02T12:00:00Z");
            recorder.StartCollection("capture-diagnostics", startedAtUtc);

            recorder.RecordFrame(CreateSnapshot(
                context,
                CreateSample(
                    startedAtUtc,
                    sessionTime: 0d,
                    focusCarIdx: 12,
                    carLeftRight: 2,
                    focusF2TimeSeconds: 700d,
                    classPosition: 2,
                    observedPosition: 25,
                    observedClassPosition: 10,
                    observedLapDistPct: 0.9748d),
                sequence: 1));
            recorder.RecordFrame(CreateSnapshot(
                context,
                CreateSample(
                    startedAtUtc.AddSeconds(1),
                    sessionTime: 1d,
                    focusCarIdx: 10,
                    carLeftRight: 2,
                    focusF2TimeSeconds: 1100d,
                    classPosition: 3,
                    observedPosition: 26,
                    observedClassPosition: 11,
                    observedLapDistPct: 0.9752d),
                sequence: 2));

            var path = recorder.CompleteCollection(startedAtUtc.AddSeconds(2), captureDirectory);

            Assert.Equal(Path.Combine(captureDirectory, "live-overlay-diagnostics.json"), path);
            using var document = JsonDocument.Parse(File.ReadAllText(path!));
            var rootElement = document.RootElement;
            Assert.Equal(2, rootElement.GetProperty("totals").GetProperty("frameCount").GetInt32());
            Assert.True(rootElement.GetProperty("gap").GetProperty("nonRaceFramesWithData").GetInt32() >= 1);
            Assert.True(rootElement.GetProperty("gap").GetProperty("classLargeGapFrames").GetInt32() >= 1);
            Assert.Equal(1, rootElement.GetProperty("gap").GetProperty("classJumpFrames").GetInt32());
            Assert.Equal(1, rootElement.GetProperty("radar").GetProperty("nonPlayerFocusFrames").GetInt32());
            Assert.Equal(1, rootElement.GetProperty("radar").GetProperty("rawSideSuppressedForFocusFrames").GetInt32());
            Assert.Equal(1, rootElement.GetProperty("radar").GetProperty("sideSignalWithoutPlacementFrames").GetInt32());
            Assert.Equal(2, rootElement.GetProperty("fuel").GetProperty("framesWithInstantaneousBurn").GetInt32());
            Assert.Equal(2, rootElement.GetProperty("fuel").GetProperty("instantaneousBurnWithoutFuelLevelFrames").GetInt32());
            Assert.Equal(1, rootElement.GetProperty("positionCadence").GetProperty("intraLapOverallPositionChanges").GetInt32());
            Assert.Equal(1, rootElement.GetProperty("positionCadence").GetProperty("intraLapClassPositionChanges").GetInt32());

            var eventKinds = rootElement
                .GetProperty("eventSamples")
                .EnumerateArray()
                .Select(item => item.GetProperty("kind").GetString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("gap.non-race-data", eventKinds);
            Assert.Contains("gap.large-jump", eventKinds);
            Assert.Contains("radar.side-suppressed-focus", eventKinds);
            Assert.Contains("fuel.instantaneous-without-level", eventKinds);
            Assert.Contains("position.overall-intra-lap", eventKinds);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static LiveTelemetrySnapshot CreateSnapshot(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        long sequence)
    {
        var fuel = LiveFuelSnapshot.From(context, sample);
        var proximity = LiveProximitySnapshot.From(context, sample);
        var leaderGap = LiveLeaderGapSnapshot.From(sample);
        return new LiveTelemetrySnapshot(
            IsConnected: true,
            IsCollecting: true,
            SourceId: "capture-diagnostics",
            StartedAtUtc: DateTimeOffset.Parse("2026-05-02T12:00:00Z"),
            LastUpdatedAtUtc: sample.CapturedAtUtc,
            Sequence: sequence,
            Context: context,
            Combo: HistoricalComboIdentity.From(context),
            LatestSample: sample,
            Fuel: fuel,
            Proximity: proximity,
            LeaderGap: leaderGap)
        {
            Models = LiveRaceModelBuilder.From(context, sample, fuel, proximity, leaderGap)
        };
    }

    private static HistoricalTelemetrySample CreateSample(
        DateTimeOffset capturedAtUtc,
        double sessionTime,
        int focusCarIdx,
        int carLeftRight,
        double focusF2TimeSeconds,
        int classPosition,
        int observedPosition,
        int observedClassPosition,
        double observedLapDistPct)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: capturedAtUtc,
            SessionTime: sessionTime,
            SessionTick: (int)sessionTime + 1,
            SessionInfoUpdate: 1,
            IsOnTrack: true,
            IsInGarage: false,
            OnPitRoad: false,
            PitstopActive: false,
            PlayerCarInPitStall: false,
            FuelLevelLiters: 0d,
            FuelLevelPercent: 0d,
            FuelUsePerHourKg: 60d,
            SpeedMetersPerSecond: 50d,
            Lap: 109,
            LapCompleted: 108,
            LapDistPct: 0.5d,
            LapLastLapTimeSeconds: 500d,
            LapBestLapTimeSeconds: 490d,
            AirTempC: 20d,
            TrackTempCrewC: 24d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            PlayerCarIdx: 10,
            FocusCarIdx: focusCarIdx,
            FocusLapCompleted: 108,
            FocusLapDistPct: 0.5d,
            FocusF2TimeSeconds: focusF2TimeSeconds,
            FocusEstimatedTimeSeconds: focusF2TimeSeconds,
            FocusLastLapTimeSeconds: 500d,
            FocusBestLapTimeSeconds: 490d,
            FocusPosition: observedPosition,
            FocusClassPosition: classPosition,
            FocusCarClass: 4098,
            FocusOnPitRoad: false,
            TeamLapCompleted: 108,
            TeamLapDistPct: 0.5d,
            TeamF2TimeSeconds: focusF2TimeSeconds,
            TeamEstimatedTimeSeconds: focusF2TimeSeconds,
            TeamPosition: observedPosition,
            TeamClassPosition: classPosition,
            TeamCarClass: 4098,
            LeaderCarIdx: 11,
            LeaderLapCompleted: 109,
            LeaderLapDistPct: 0.1d,
            LeaderF2TimeSeconds: 0d,
            ClassLeaderCarIdx: 11,
            ClassLeaderLapCompleted: 109,
            ClassLeaderLapDistPct: 0.1d,
            ClassLeaderF2TimeSeconds: 0d,
            FocusClassLeaderCarIdx: 11,
            FocusClassLeaderLapCompleted: 109,
            FocusClassLeaderLapDistPct: 0.1d,
            FocusClassLeaderF2TimeSeconds: 0d,
            CarLeftRight: carLeftRight,
            ClassCars:
            [
                new HistoricalCarProximity(
                    CarIdx: 43,
                    LapCompleted: 108,
                    LapDistPct: observedLapDistPct,
                    F2TimeSeconds: 900d,
                    EstimatedTimeSeconds: 900d,
                    Position: observedPosition,
                    ClassPosition: observedClassPosition,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false)
            ],
            TeamOnPitRoad: false,
            DriversSoFar: 1);
    }

    private static HistoricalSessionContext CreateContext()
    {
        return new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity
            {
                CarId = 156,
                CarScreenName = "Mercedes-AMG GT3 2020",
                CarClassId = 4098,
                DriverCarFuelKgPerLiter = 0.75d,
                DriverCarEstLapTimeSeconds = 500d
            },
            Track = new HistoricalTrackIdentity
            {
                TrackId = 262,
                TrackDisplayName = "Nurburgring Combined",
                TrackLengthKm = 24.3d
            },
            Session = new HistoricalSessionIdentity
            {
                SessionType = "Offline Testing",
                EventType = "Test"
            },
            Conditions = new HistoricalSessionInfoConditions(),
            Drivers:
            [
                new HistoricalSessionDriver
                {
                    CarIdx = 10,
                    UserName = "Player",
                    CarClassId = 4098
                },
                new HistoricalSessionDriver
                {
                    CarIdx = 12,
                    UserName = "Focused Driver",
                    CarClassId = 4098
                }
            ]
        };
    }

    private static AppStorageOptions CreateStorage(string root)
    {
        return new AppStorageOptions
        {
            AppDataRoot = root,
            CaptureRoot = Path.Combine(root, "captures"),
            UserHistoryRoot = Path.Combine(root, "history", "user"),
            BaselineHistoryRoot = Path.Combine(root, "history", "baseline"),
            LogsRoot = Path.Combine(root, "logs"),
            SettingsRoot = Path.Combine(root, "settings"),
            DiagnosticsRoot = Path.Combine(root, "diagnostics"),
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }
}
