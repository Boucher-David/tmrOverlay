using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Events;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class LiveModelParityRecorderTests
{
    [Fact]
    public void CompleteCollection_WritesCaptureSidecarWithPostSessionSignalAvailability()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-model-parity-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var captureDirectory = Path.Combine(storage.CaptureRoot, "capture-test");
            Directory.CreateDirectory(captureDirectory);
            File.WriteAllText(
                Path.Combine(captureDirectory, "telemetry-schema.json"),
                """
                [
                  { "name": "SessionTime" },
                  { "name": "CarIdxF2Time" },
                  { "name": "TrackWetness" }
                ]
                """);
            File.WriteAllText(
                Path.Combine(captureDirectory, "capture-synthesis.json"),
                """
                {
                  "sourceFiles": { "telemetryBytes": 1234 },
                  "frameScan": { "totalFrameRecords": 120, "sampledFrameCount": 12, "sampleStride": 10 },
                  "schemaSummary": { "fieldCount": 3 },
                  "fields": [
                    { "name": "SessionTime" },
                    { "name": "CarIdxF2Time" },
                    { "name": "TrackWetness" }
                  ]
                }
                """);
            Directory.CreateDirectory(Path.Combine(captureDirectory, "ibt-analysis"));
            File.WriteAllText(
                Path.Combine(captureDirectory, "ibt-analysis", "status.json"),
                """
                {
                  "status": "succeeded",
                  "fieldCount": 4,
                  "totalRecordCount": 100,
                  "sampledRecordCount": 10,
                  "commonFieldCount": 2,
                  "ibtOnlyFieldCount": 1,
                  "liveOnlyFieldCount": 1,
                  "source": { "bytes": 4096 }
                }
                """);
            File.WriteAllText(
                Path.Combine(captureDirectory, "ibt-analysis", "ibt-vs-live-schema.json"),
                """
                {
                  "commonFieldNames": [ "SessionTime", "TrackWetness" ],
                  "onlyInIbtFieldNames": [ "Lat" ],
                  "onlyInLiveFieldNames": [ "CarIdxF2Time" ]
                }
                """);

            var recorder = new LiveModelParityRecorder(
                new LiveModelParityOptions
                {
                    Enabled = true,
                    MinimumFrameSpacingSeconds = 0.1d,
                    MaxFramesPerSession = 10,
                    MaxObservationsPerFrame = 10,
                    MaxObservationSummaries = 10
                },
                storage,
                new AppEventRecorder(storage),
                NullLogger<LiveModelParityRecorder>.Instance);
            var store = new LiveTelemetryStore();
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            recorder.StartCollection("capture-test", startedAtUtc);
            store.MarkCollectionStarted("capture-test", startedAtUtc);
            store.RecordFrame(CreateSample());
            recorder.RecordFrame(store.Snapshot());

            var path = recorder.CompleteCollection(DateTimeOffset.UtcNow, captureDirectory);

            Assert.Equal(Path.Combine(captureDirectory, "live-model-parity.json"), path);
            using var document = JsonDocument.Parse(File.ReadAllText(path!));
            var rootElement = document.RootElement;
            Assert.Equal(1, rootElement.GetProperty("totals").GetProperty("frameCount").GetInt32());
            Assert.Equal(0, rootElement.GetProperty("totals").GetProperty("mismatchFrameCount").GetInt32());
            var readiness = rootElement.GetProperty("promotionReadiness");
            Assert.False(readiness.GetProperty("isCandidate").GetBoolean());
            Assert.Equal("insufficient-data", readiness.GetProperty("status").GetString());
            var postSession = rootElement.GetProperty("postSessionEvaluation");
            Assert.True(postSession.GetProperty("captureSynthesis").GetProperty("exists").GetBoolean());
            Assert.Equal("succeeded", postSession.GetProperty("ibtAnalysis").GetProperty("status").GetString());
            var signals = postSession
                .GetProperty("signalAvailability")
                .EnumerateArray()
                .ToDictionary(
                    item => item.GetProperty("signal").GetString()!,
                    item => item.GetProperty("ibtAvailability").GetString()!,
                    StringComparer.OrdinalIgnoreCase);
            Assert.Equal("common", signals["SessionTime"]);
            Assert.Equal("live-only", signals["CarIdxF2Time"]);
            Assert.Equal("ibt-only", signals["Lat"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void CompleteCollection_WritesPromotionCandidateSignalWhenThresholdsPass()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-model-parity-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var captureDirectory = Path.Combine(storage.CaptureRoot, "capture-candidate");
            Directory.CreateDirectory(captureDirectory);
            var recorder = new LiveModelParityRecorder(
                new LiveModelParityOptions
                {
                    Enabled = true,
                    PromotionCandidateMinimumFrames = 1,
                    PromotionCandidateMaxMismatchFrameRate = 0d,
                    PromotionCandidateMinimumCoverageRatio = 1d
                },
                storage,
                new AppEventRecorder(storage),
                NullLogger<LiveModelParityRecorder>.Instance);
            var store = new LiveTelemetryStore();
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            recorder.StartCollection("capture-candidate", startedAtUtc);
            store.MarkCollectionStarted("capture-candidate", startedAtUtc);
            store.RecordFrame(CreateSample());
            recorder.RecordFrame(store.Snapshot());

            var path = recorder.CompleteCollection(DateTimeOffset.UtcNow, captureDirectory);

            using var document = JsonDocument.Parse(File.ReadAllText(path!));
            var readiness = document.RootElement.GetProperty("promotionReadiness");
            Assert.True(readiness.GetProperty("isCandidate").GetBoolean());
            Assert.Equal("candidate", readiness.GetProperty("status").GetString());
            var eventLog = string.Join(
                Environment.NewLine,
                Directory.EnumerateFiles(storage.EventsRoot, "events-*.jsonl")
                    .Select(File.ReadAllText));
            Assert.Contains("live_model_v2_promotion_candidate", eventLog);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static HistoricalTelemetrySample CreateSample()
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            SessionTime: 123d,
            SessionTick: 100,
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
            TeamLapCompleted: 2,
            TeamLapDistPct: 0.5d,
            TeamEstimatedTimeSeconds: 50d,
            TeamF2TimeSeconds: 50d,
            TeamPosition: 7,
            TeamClassPosition: 3,
            TeamCarClass: 4098);
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
            TrackMapRoot = Path.Combine(root, "track-maps", "user"),
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }
}
