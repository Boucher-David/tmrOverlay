using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Events;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.EdgeCases;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class TelemetryEdgeCaseRecorderTests
{
    [Fact]
    public void CompleteCollection_WritesBoundedClipWithPreAndPostFrames()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-edge-case-recorder-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var recorder = new TelemetryEdgeCaseRecorder(
                new TelemetryEdgeCaseOptions
                {
                    Enabled = true,
                    PreTriggerSeconds = 5d,
                    PostTriggerSeconds = 1d,
                    MaxClipsPerSession = 3,
                    MaxFramesPerClip = 6,
                    MinimumFrameSpacingSeconds = 0.5d
                },
                storage,
                new AppEventRecorder(storage),
                NullLogger<TelemetryEdgeCaseRecorder>.Instance);
            var startedAt = DateTimeOffset.Parse("2026-04-29T12:00:00Z");
            recorder.StartCollection(
                "session-edge-case-test",
                startedAt,
                new RawTelemetrySchemaSnapshot(
                    [
                        new RawTelemetryWatchedVariable(
                            "FuelLevel",
                            "fuel",
                            "Float",
                            1,
                            "l",
                            "Fuel level")
                    ],
                    []));

            recorder.RecordFrame(
                CreateSample(startedAt, sessionTime: 0d, sessionTick: 1),
                new RawTelemetryWatchSnapshot(new Dictionary<string, double>
                {
                    ["FuelLevel"] = 42d
                }));
            recorder.RecordFrame(
                CreateSample(startedAt.AddSeconds(1), sessionTime: 1d, sessionTick: 2, carLeftRight: 2),
                new RawTelemetryWatchSnapshot(new Dictionary<string, double>
                {
                    ["FuelLevel"] = 41.9d
                }));
            recorder.RecordFrame(
                CreateSample(startedAt.AddSeconds(2), sessionTime: 2d, sessionTick: 3),
                new RawTelemetryWatchSnapshot(new Dictionary<string, double>
                {
                    ["FuelLevel"] = 41.8d
                }));

            var path = recorder.CompleteCollection(startedAt.AddSeconds(3));

            Assert.NotNull(path);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;
            Assert.Equal(1, rootElement.GetProperty("clipCount").GetInt32());
            Assert.Equal(1, rootElement.GetProperty("observationCount").GetInt32());

            var clip = rootElement.GetProperty("clips")[0];
            Assert.Equal("side-occupancy.no-adjacent-car", clip.GetProperty("trigger").GetProperty("key").GetString());
            var frames = clip.GetProperty("frames");
            Assert.Equal(3, frames.GetArrayLength());
            Assert.True(frames[0].GetProperty("rawWatch").TryGetProperty("FuelLevel", out _));
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
    public void RecordFrame_RespectsMaxClipsPerSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-edge-case-recorder-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var recorder = new TelemetryEdgeCaseRecorder(
                new TelemetryEdgeCaseOptions
                {
                    Enabled = true,
                    PreTriggerSeconds = 1d,
                    PostTriggerSeconds = 1d,
                    MaxClipsPerSession = 1,
                    MaxFramesPerClip = 5,
                    MinimumFrameSpacingSeconds = 0.1d
                },
                storage,
                new AppEventRecorder(storage),
                NullLogger<TelemetryEdgeCaseRecorder>.Instance);
            var startedAt = DateTimeOffset.Parse("2026-04-29T12:00:00Z");
            recorder.StartCollection("session-edge-case-limit-test", startedAt, RawTelemetrySchemaSnapshot.Empty);

            recorder.RecordFrame(
                CreateSample(startedAt, sessionTime: 0d, sessionTick: 1, carLeftRight: 2),
                RawTelemetryWatchSnapshot.Empty);
            recorder.RecordFrame(
                CreateSample(startedAt.AddSeconds(1), sessionTime: 1d, sessionTick: 2),
                new RawTelemetryWatchSnapshot(new Dictionary<string, double>
                {
                    ["EngineWarnings"] = 1d
                }));

            var path = recorder.CompleteCollection(startedAt.AddSeconds(2));

            Assert.NotNull(path);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(1, document.RootElement.GetProperty("clipCount").GetInt32());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static HistoricalTelemetrySample CreateSample(
        DateTimeOffset capturedAtUtc,
        double sessionTime,
        int sessionTick,
        int? carLeftRight = null)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: capturedAtUtc,
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
            NearbyCars: []);
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
