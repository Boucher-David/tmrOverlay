using TmrOverlay.Core.Analysis;
using TmrOverlay.Core.History;
using Xunit;

namespace TmrOverlay.App.Tests.History;

public sealed class HistoricalSessionAccumulatorTests
{
    [Fact]
    public void BuildSummary_LabelsSpectatedPracticeTimingSeparatelyFromLocalFuel()
    {
        var accumulator = new HistoricalSessionAccumulator();
        var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);

        accumulator.RecordFrame(CreateSpectatedTimingSample(
            capturedAtUtc: startedAtUtc,
            sessionTime: 3017.7d,
            focusCarIdx: 61,
            focusF2TimeSeconds: 23.6d));
        accumulator.RecordFrame(CreateSpectatedTimingSample(
            capturedAtUtc: startedAtUtc.AddSeconds(1),
            sessionTime: 3018.7d,
            focusCarIdx: 31,
            focusF2TimeSeconds: 24.1d));

        var summary = accumulator.BuildSummary(
            "spectated-practice",
            startedAtUtc,
            startedAtUtc.AddSeconds(2),
            droppedFrameCount: 0,
            sessionInfoSnapshotCount: 1);

        var availability = summary.Metrics.TelemetryAvailability;
        Assert.Equal(2, availability.SampleFrameCount);
        Assert.Equal(0, availability.LocalDrivingFrameCount);
        Assert.Equal(0, availability.LocalFuelScalarFrameCount);
        Assert.Equal(2, availability.LocalScalarIdleFrameCount);
        Assert.Equal(2, availability.FocusCarFrameCount);
        Assert.Equal(2, availability.NonTeamFocusFrameCount);
        Assert.Equal(1, availability.FocusCarChangeCount);
        Assert.Equal(31, availability.CurrentFocusCarIdx);
        Assert.Collection(
            availability.FocusSegments,
            first =>
            {
                Assert.Equal(61, first.CarIdx);
                Assert.False(first.IsTeamCar);
                Assert.Equal(1, first.FrameCount);
            },
            second =>
            {
                Assert.Equal(31, second.CarIdx);
                Assert.False(second.IsTeamCar);
                Assert.Equal(1, second.FrameCount);
            });
        Assert.Equal(2, availability.FocusTimingFrameCount);
        Assert.Equal(2, availability.ClassTimingFrameCount);
        Assert.Equal(0, availability.CarLeftRightAvailableFrameCount);
        Assert.Equal(2, availability.CarLeftRightUnavailableFrameCount);
        Assert.True(availability.IsSpectatedTimingOnly);

        Assert.Contains("spectated_session", summary.Quality.Reasons);
        Assert.Contains("local_scalars_idle", summary.Quality.Reasons);
        Assert.Contains("focus_timing_available", summary.Quality.Reasons);
        Assert.Contains("focus_car_changed", summary.Quality.Reasons);
        Assert.Contains("car_left_right_unavailable", summary.Quality.Reasons);
        Assert.False(summary.Quality.ContributesToBaseline);

        var analysis = PostRaceAnalysisBuilder.Build(summary);
        Assert.Contains(analysis.Lines, line => line.Contains("spectated focus timing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(analysis.Lines, line => line.Contains("focus segments", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(analysis.Lines, line => line.Contains("gap/radar validation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HistoricalDataQuality_LabelsNonRaceSessions()
    {
        var quality = HistoricalDataQuality.From(
            new HistoricalSessionContext
            {
                Car = new HistoricalCarIdentity(),
                Track = new HistoricalTrackIdentity(),
                Session = new HistoricalSessionIdentity
                {
                    SessionType = "Practice"
                },
                Conditions = new HistoricalSessionInfoConditions()
            },
            new HistoricalSessionMetrics
            {
                SampleFrameCount = 1,
                TelemetryAvailability = new TelemetryAvailabilitySnapshot
                {
                    SampleFrameCount = 1,
                    FocusTimingFrameCount = 1
                }
            });

        Assert.Contains("non_race_session", quality.Reasons);
        Assert.Contains("focus_timing_available", quality.Reasons);
    }

    private static HistoricalTelemetrySample CreateSpectatedTimingSample(
        DateTimeOffset capturedAtUtc,
        double sessionTime,
        int focusCarIdx,
        double focusF2TimeSeconds)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: capturedAtUtc,
            SessionTime: sessionTime,
            SessionTick: 100,
            SessionInfoUpdate: 1,
            IsOnTrack: false,
            IsInGarage: false,
            OnPitRoad: false,
            PitstopActive: false,
            PlayerCarInPitStall: false,
            FuelLevelLiters: 0d,
            FuelLevelPercent: 0d,
            FuelUsePerHourKg: 0d,
            SpeedMetersPerSecond: 0d,
            Lap: 0,
            LapCompleted: 0,
            LapDistPct: 0d,
            LapLastLapTimeSeconds: null,
            LapBestLapTimeSeconds: null,
            AirTempC: 20d,
            TrackTempCrewC: 24d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            PlayerCarIdx: 10,
            FocusCarIdx: focusCarIdx,
            FocusF2TimeSeconds: focusF2TimeSeconds,
            FocusPosition: 26,
            FocusClassPosition: 25,
            ClassCars:
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
                    OnPitRoad: null),
                new HistoricalCarProximity(
                    CarIdx: focusCarIdx,
                    LapCompleted: -1,
                    LapDistPct: -1d,
                    F2TimeSeconds: focusF2TimeSeconds,
                    EstimatedTimeSeconds: null,
                    Position: 26,
                    ClassPosition: 25,
                    CarClass: 4098,
                    TrackSurface: null,
                    OnPitRoad: null)
            ]);
    }
}
