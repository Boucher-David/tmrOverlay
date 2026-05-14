using System;
using System.Text.Json.Nodes;
using TmrOverlay.Core.History;
using Xunit;

namespace TmrOverlay.App.Tests.History;

public sealed class HistoricalSessionAccumulatorTests
{
    private const string FourHourRadarFixtureRelativePath = "fixtures/telemetry-analysis/radar-calibration-4h-side-windows.json";

    [Fact]
    public void BuildSummary_RecordsCleanRadarSideOverlapWindow()
    {
        var accumulator = new HistoricalSessionAccumulator();
        var capturedAtUtc = DateTimeOffset.Parse("2026-05-13T12:00:00Z");

        accumulator.RecordFrame(Sample(capturedAtUtc, sessionTime: 0.0d, carLeftRight: 1));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(100), sessionTime: 0.1d, carLeftRight: 2));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(200), sessionTime: 0.2d, carLeftRight: 2));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(300), sessionTime: 0.3d, carLeftRight: 2));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(400), sessionTime: 0.4d, carLeftRight: 1));

        var summary = BuildSummary(accumulator, capturedAtUtc);

        Assert.NotNull(summary.RadarCalibration);
        Assert.Equal(1, summary.RadarCalibration.SideOverlapWindowSeconds.SampleCount);
        Assert.Equal(0, summary.RadarCalibration.EstimatedBodyLengthMeters.SampleCount);
        Assert.Equal(0.2d, summary.RadarCalibration.SideOverlapWindowSeconds.Mean!.Value, precision: 3);
        Assert.Contains("carleft-right-clean-transition", summary.RadarCalibration.ConfidenceFlags);
        Assert.Contains("not-live-consumed", summary.RadarCalibration.ConfidenceFlags);
    }

    [Fact]
    public void BuildSummary_EstimatesRadarBodyLengthFromStableIdentityBackedSideWindow()
    {
        var accumulator = new HistoricalSessionAccumulator();
        accumulator.ApplySessionInfo("""
WeekendInfo:
 TrackLength: 1.000 km
SessionInfo:
 CurrentSessionNum: 0
 Sessions:
 - SessionNum: 0
   SessionType: Race
DriverInfo:
 DriverCarIdx: 10
 Drivers:
 - CarIdx: 10
   CarScreenName: Mercedes-AMG GT3 2020
 - CarIdx: 12
   CarScreenName: Mercedes-AMG GT3 2020
""");
        var capturedAtUtc = DateTimeOffset.Parse("2026-05-13T12:00:00Z");

        accumulator.RecordFrame(Sample(capturedAtUtc, sessionTime: 0.0d, carLeftRight: 1));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(100), sessionTime: 0.1d, carLeftRight: 3, nearbyMeters: 4.8d));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(200), sessionTime: 0.2d, carLeftRight: 3, nearbyMeters: 2.4d));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(300), sessionTime: 0.3d, carLeftRight: 3, nearbyMeters: 0.2d));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(400), sessionTime: 0.4d, carLeftRight: 3, nearbyMeters: -2.4d));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(500), sessionTime: 0.5d, carLeftRight: 1));

        var summary = BuildSummary(accumulator, capturedAtUtc);

        Assert.NotNull(summary.RadarCalibration);
        Assert.Equal(1, summary.RadarCalibration.SideOverlapWindowSeconds.SampleCount);
        Assert.Equal(1, summary.RadarCalibration.EstimatedBodyLengthMeters.SampleCount);
        Assert.Equal(4.8d, summary.RadarCalibration.EstimatedBodyLengthMeters.Mean!.Value, precision: 3);
        Assert.Contains("identity-backed-window", summary.RadarCalibration.ConfidenceFlags);
        Assert.Contains("identity-backed-body-length", summary.RadarCalibration.ConfidenceFlags);
        Assert.DoesNotContain("not-live-consumed", summary.RadarCalibration.ConfidenceFlags);
    }

    [Fact]
    public void BuildSummary_FinalizesActiveRadarSideWindowAtCaptureEnd()
    {
        var accumulator = new HistoricalSessionAccumulator();
        var capturedAtUtc = DateTimeOffset.Parse("2026-05-13T12:00:00Z");

        accumulator.RecordFrame(Sample(capturedAtUtc, sessionTime: 0.0d, carLeftRight: 1));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(100), sessionTime: 0.1d, carLeftRight: 3));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(200), sessionTime: 0.2d, carLeftRight: 3));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(300), sessionTime: 0.3d, carLeftRight: 3));

        var summary = BuildSummary(accumulator, capturedAtUtc);

        Assert.NotNull(summary.RadarCalibration);
        Assert.Equal(1, summary.RadarCalibration.SideOverlapWindowSeconds.SampleCount);
        Assert.Equal(0.2d, summary.RadarCalibration.SideOverlapWindowSeconds.Mean!.Value, precision: 3);
    }

    [Fact]
    public void BuildSummary_AcceptsPreGridSessionStateSideBySideRows()
    {
        var accumulator = new HistoricalSessionAccumulator();
        var capturedAtUtc = DateTimeOffset.Parse("2026-05-13T12:00:00Z");

        accumulator.RecordFrame(Sample(capturedAtUtc, sessionTime: 0.0d, carLeftRight: 1, sessionState: 3));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(100), sessionTime: 0.1d, carLeftRight: 4, sessionState: 3));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(200), sessionTime: 0.2d, carLeftRight: 4, sessionState: 3));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(300), sessionTime: 0.3d, carLeftRight: 4, sessionState: 3));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(400), sessionTime: 0.4d, carLeftRight: 1, sessionState: 3));

        var summary = BuildSummary(accumulator, capturedAtUtc);

        Assert.NotNull(summary.RadarCalibration);
        Assert.Equal(1, summary.RadarCalibration.SideOverlapWindowSeconds.SampleCount);
        Assert.Equal(0.2d, summary.RadarCalibration.SideOverlapWindowSeconds.Mean!.Value, precision: 3);
    }


    [Fact]
    public void BuildSummary_IgnoresRadarSideWindowsOutsideDrivingContext()
    {
        var accumulator = new HistoricalSessionAccumulator();
        var capturedAtUtc = DateTimeOffset.Parse("2026-05-13T12:00:00Z");

        accumulator.RecordFrame(Sample(capturedAtUtc, sessionTime: 0.0d, carLeftRight: 1));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(100), sessionTime: 0.1d, carLeftRight: 2, onPitRoad: true));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(200), sessionTime: 0.2d, carLeftRight: 2, onPitRoad: true));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(300), sessionTime: 0.3d, carLeftRight: 2, isInGarage: true));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(400), sessionTime: 0.4d, carLeftRight: 2, speedMetersPerSecond: 0d));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(500), sessionTime: 0.5d, carLeftRight: 1));

        var summary = BuildSummary(accumulator, capturedAtUtc);

        Assert.Null(summary.RadarCalibration);
    }

    [Fact]
    public void BuildSummary_DoesNotBridgeRadarSideWindowsAcrossTelemetryGaps()
    {
        var accumulator = new HistoricalSessionAccumulator();
        var capturedAtUtc = DateTimeOffset.Parse("2026-05-13T12:00:00Z");

        accumulator.RecordFrame(Sample(capturedAtUtc, sessionTime: 0.0d, carLeftRight: 1));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(100), sessionTime: 0.1d, carLeftRight: 2));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(200), sessionTime: 0.2d, carLeftRight: 2));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddSeconds(2), sessionTime: 2.0d, carLeftRight: 2));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(2100), sessionTime: 2.1d, carLeftRight: 2));
        accumulator.RecordFrame(Sample(capturedAtUtc.AddMilliseconds(2200), sessionTime: 2.2d, carLeftRight: 1));

        var summary = BuildSummary(accumulator, capturedAtUtc);

        Assert.Null(summary.RadarCalibration);
    }

    [Fact]
    public void BuildSummary_CalibratesFromFourHourRadarSideWindowFixture()
    {
        var fixture = ReadFixture(FourHourRadarFixtureRelativePath);
        var windows = RequiredArray(fixture["cleanSideWindows"]).Select(RequiredObject).ToArray();
        var tickRateHz = RequiredDouble(fixture, "tickRateHz");
        var capturedAtUtc = DateTimeOffset.Parse("2026-04-26T13:03:34.932Z");
        var accumulator = new HistoricalSessionAccumulator();
        var sessionTime = 0d;

        accumulator.RecordFrame(Sample(capturedAtUtc, sessionTime, carLeftRight: 1));
        foreach (var window in windows)
        {
            var carLeftRight = RequiredInt(window, "carLeftRight");
            var deltaCount = RequiredInt(window, "deltaCount");
            sessionTime += 1d;
            accumulator.RecordFrame(Sample(capturedAtUtc.AddSeconds(sessionTime), sessionTime, carLeftRight: 1, sessionState: 3));
            sessionTime += 1d / tickRateHz;
            accumulator.RecordFrame(Sample(capturedAtUtc.AddSeconds(sessionTime), sessionTime, carLeftRight, sessionState: 3));

            for (var delta = 0; delta < deltaCount; delta++)
            {
                sessionTime += 1d / tickRateHz;
                accumulator.RecordFrame(Sample(capturedAtUtc.AddSeconds(sessionTime), sessionTime, carLeftRight, sessionState: 3));
            }

            sessionTime += 1d / tickRateHz;
            accumulator.RecordFrame(Sample(capturedAtUtc.AddSeconds(sessionTime), sessionTime, carLeftRight: 1, sessionState: 3));
        }

        var summary = BuildSummary(accumulator, capturedAtUtc);

        Assert.NotNull(summary.RadarCalibration);
        Assert.Equal(RequiredInt(fixture, "cleanSideWindowCount"), summary.RadarCalibration.SideOverlapWindowSeconds.SampleCount);
        Assert.Equal(RequiredDouble(fixture, "expectedMeanSeconds"), summary.RadarCalibration.SideOverlapWindowSeconds.Mean!.Value, precision: 3);
        Assert.Equal(RequiredDouble(fixture, "expectedMinimumSeconds"), summary.RadarCalibration.SideOverlapWindowSeconds.Minimum!.Value, precision: 3);
        Assert.Equal(RequiredDouble(fixture, "expectedMaximumSeconds"), summary.RadarCalibration.SideOverlapWindowSeconds.Maximum!.Value, precision: 3);
        Assert.Contains("not-live-consumed", summary.RadarCalibration.ConfidenceFlags);
    }

    private static HistoricalSessionSummary BuildSummary(
        HistoricalSessionAccumulator accumulator,
        DateTimeOffset capturedAtUtc)
    {
        return accumulator.BuildSummary(
            sourceCaptureId: "capture-radar-calibration-test",
            startedAtUtc: capturedAtUtc,
            finishedAtUtc: capturedAtUtc.AddSeconds(1),
            droppedFrameCount: 0,
            sessionInfoSnapshotCount: 0);
    }

    private static HistoricalTelemetrySample Sample(
        DateTimeOffset capturedAtUtc,
        double sessionTime,
        int? carLeftRight,
        bool isOnTrack = true,
        bool isInGarage = false,
        bool onPitRoad = false,
        bool playerCarInPitStall = false,
        bool? teamOnPitRoad = null,
        double speedMetersPerSecond = 50d,
        int? sessionState = null,
        double? nearbyMeters = null)
    {
        var nearbyCars = nearbyMeters is { } meters
            ? new[]
            {
                new HistoricalCarProximity(
                    CarIdx: 12,
                    LapCompleted: 2,
                    LapDistPct: 0.5d + meters / 1000d,
                    F2TimeSeconds: null,
                    EstimatedTimeSeconds: null,
                    Position: 2,
                    ClassPosition: 2,
                    CarClass: 4098,
                    TrackSurface: 3,
                    OnPitRoad: false)
            }
            : null;
        return new HistoricalTelemetrySample(
            CapturedAtUtc: capturedAtUtc,
            SessionTime: sessionTime,
            SessionTick: (int)Math.Round(sessionTime * 60d),
            SessionInfoUpdate: 1,
            IsOnTrack: isOnTrack,
            IsInGarage: isInGarage,
            OnPitRoad: onPitRoad,
            PitstopActive: false,
            PlayerCarInPitStall: playerCarInPitStall,
            FuelLevelLiters: 50d,
            FuelLevelPercent: 0.5d,
            FuelUsePerHourKg: 60d,
            SpeedMetersPerSecond: speedMetersPerSecond,
            Lap: 3,
            LapCompleted: 2,
            LapDistPct: 0.5d,
            LapLastLapTimeSeconds: null,
            LapBestLapTimeSeconds: null,
            AirTempC: 20d,
            TrackTempCrewC: 25d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            SessionState: sessionState,
            PlayerCarIdx: 10,
            FocusCarIdx: 10,
            TeamLapCompleted: 2,
            TeamLapDistPct: 0.5d,
            CarLeftRight: carLeftRight,
            NearbyCars: nearbyCars,
            TeamOnPitRoad: teamOnPitRoad);
    }

    private static JsonObject ReadFixture(string relativePath)
    {
        var path = FindRepoRootFile(relativePath);
        return RequiredObject(JsonNode.Parse(File.ReadAllText(path)));
    }

    private static string FindRepoRootFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }

    private static JsonObject RequiredObject(JsonNode? node)
    {
        return Assert.IsType<JsonObject>(node);
    }

    private static JsonArray RequiredArray(JsonNode? node)
    {
        return Assert.IsType<JsonArray>(node);
    }

    private static int RequiredInt(JsonObject value, string propertyName)
    {
        return Assert.IsAssignableFrom<JsonValue>(value[propertyName]).GetValue<int>();
    }

    private static double RequiredDouble(JsonObject value, string propertyName)
    {
        return Assert.IsAssignableFrom<JsonValue>(value[propertyName]).GetValue<double>();
    }
}
