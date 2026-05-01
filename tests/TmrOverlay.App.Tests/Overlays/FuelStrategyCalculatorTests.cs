using TmrOverlay.Core.History;
using TmrOverlay.Core.Fuel;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class FuelStrategyCalculatorTests
{
    [Fact]
    public void From_UsesLeaderProgressForTimedRaceLapsRemaining()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 100d,
            fuelUsePerHourKg: 75d,
            teamLapCompleted: 4,
            teamLapDistPct: 0.2d,
            leaderLapCompleted: 4,
            leaderLapDistPct: 0.5d,
            sessionTimeRemain: 250d,
            teamLastLapTimeSeconds: 100d);

        var strategy = FuelStrategyCalculator.From(live, EmptyHistory(live.Combo));

        Assert.NotNull(strategy.RaceLapsRemaining);
        Assert.Equal(2.8d, strategy.RaceLapsRemaining.Value, precision: 3);
        Assert.Equal(10d, strategy.FuelPerLapLiters);
        Assert.Equal("fuel covers finish", strategy.Status);
        Assert.Equal(2.8d, Assert.Single(strategy.Stints).LengthLaps, precision: 3);
        Assert.Equal("finish", strategy.Stints[0].Source);
        Assert.Equal("no tire stop", strategy.Stints[0].TireAdvice!.Text);
    }

    [Fact]
    public void From_UsesOverallLeaderPaceForTimedRaceProjection()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 100d,
            fuelUsePerHourKg: 75d,
            teamLapCompleted: 4,
            teamLapDistPct: 0.2d,
            leaderLapCompleted: 4,
            leaderLapDistPct: 0.5d,
            sessionTimeRemain: 250d,
            teamLastLapTimeSeconds: 100d,
            leaderLastLapTimeSeconds: 80d);

        var strategy = FuelStrategyCalculator.From(live, EmptyHistory(live.Combo));

        Assert.Equal(3.8d, strategy.RaceLapsRemaining!.Value, precision: 3);
        Assert.Equal("overall leader last lap", strategy.RacePaceSource);
        Assert.Equal("timed race by overall leader last lap", strategy.RaceLapEstimateSource);
        Assert.Equal(0.3d, strategy.OverallLeaderGapLaps!.Value, precision: 3);
    }

    [Fact]
    public void From_FallsBackToHistoricalFuelPerLapWhenLiveBurnUnavailable()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 50d,
            fuelUsePerHourKg: 0d,
            teamLastLapTimeSeconds: 100d,
            sessionTimeRemain: 1_000d);
        var aggregate = new HistoricalSessionAggregate
        {
            BaselineSessionCount = 1,
            FuelPerLapLiters = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 10d,
                Minimum = 10d,
                Maximum = 10d
            }
        };

        var strategy = FuelStrategyCalculator.From(live, new SessionHistoryLookupResult(live.Combo, null, aggregate));

        Assert.Equal(10d, strategy.FuelPerLapLiters);
        Assert.Equal("baseline history", strategy.FuelPerLapSource);
        Assert.Equal(50d, strategy.AdditionalFuelNeededLiters);
        Assert.Equal(2, strategy.PlannedStintCount);
        Assert.Equal(1, strategy.PlannedStopCount);
        Assert.Equal("2 stints / 1 stop", strategy.Status);
    }

    [Fact]
    public void From_TreatsFuelAsModeledWhenLocalDriverIsNotOnTrack()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 50d,
            fuelUsePerHourKg: 75d,
            teamLastLapTimeSeconds: 100d,
            sessionTimeRemain: 600d,
            isOnTrack: false);
        var aggregate = new HistoricalSessionAggregate
        {
            BaselineSessionCount = 1,
            FuelPerLapLiters = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 10d,
                Minimum = 10d,
                Maximum = 10d
            }
        };

        var strategy = FuelStrategyCalculator.From(live, new SessionHistoryLookupResult(live.Combo, null, aggregate));

        Assert.Null(strategy.CurrentFuelLiters);
        Assert.Null(strategy.FuelPercent);
        Assert.Equal(10d, strategy.FuelPerLapLiters);
        Assert.Equal("baseline history", strategy.FuelPerLapSource);
    }

    [Fact]
    public void From_UsesFourHourHistoryToPlanEightLapTargets()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 106d,
            fuelUsePerHourKg: 0d,
            sessionTimeRemain: 14_400d,
            teamLastLapTimeSeconds: 482.092804d,
            fuelMaxLiters: 106d,
            estimatedLapSeconds: 465.166d);
        var aggregate = new HistoricalSessionAggregate
        {
            BaselineSessionCount = 1,
            Car = live.Context.Car,
            FuelPerLapLiters = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 13.36344d,
                Minimum = 13.36344d,
                Maximum = 13.36344d
            },
            MedianLapSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 482.092804d,
                Minimum = 482.092804d,
                Maximum = 482.092804d
            },
            ObservedFuelFillRateLitersPerSecond = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 2.68d,
                Minimum = 2.68d,
                Maximum = 2.68d
            },
            AveragePitLaneSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 64.822333d,
                Minimum = 64.822333d,
                Maximum = 64.822333d
            },
            AverageTireChangePitServiceSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 39.2d,
                Minimum = 39.2d,
                Maximum = 39.2d
            }
        };

        var strategy = FuelStrategyCalculator.From(live, new SessionHistoryLookupResult(live.Combo, null, aggregate));

        Assert.Equal(30, strategy.PlannedRaceLaps);
        Assert.Equal(4, strategy.PlannedStintCount);
        Assert.Equal(3, strategy.PlannedStopCount);
        Assert.Equal(7, strategy.FinalStintTargetLaps);
        Assert.Equal("8-lap rhythm avoids +1 stop (~65s)", strategy.Status);
        Assert.Equal(new int?[] { 8, 8, 7, 7 }, strategy.Stints.Select(stint => stint.TargetLaps).ToArray());
        Assert.Equal(13.25d, strategy.Stints[0].TargetFuelPerLapLiters!.Value, precision: 3);
        Assert.Equal(13.36344d, strategy.Stints[0].CurrentFuelPerLapLiters!.Value, precision: 5);
        Assert.Equal("baseline history", strategy.Stints[0].CurrentFuelPerLapSource);
        Assert.Equal("tires free (106 L)", strategy.Stints[0].TireAdvice!.Text);
        Assert.Equal(1, strategy.RhythmComparison!.AdditionalStopCount);
        Assert.Equal(64.822333d, strategy.RhythmComparison.EstimatedTimeLossSeconds!.Value, precision: 3);
        Assert.Equal(0.113d, strategy.RequiredFuelSavingLitersPerLap!.Value, precision: 3);
        Assert.False(strategy.StopOptimization!.IsRealistic);
    }

    [Fact]
    public void From_UsesTeammateEightLapHistoryForAlternatingTeamPlan()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 106d,
            fuelUsePerHourKg: 0d,
            sessionTimeRemain: 14_400d,
            teamLastLapTimeSeconds: 482.092804d,
            fuelMaxLiters: 106d,
            estimatedLapSeconds: 465.166d);
        var aggregate = new HistoricalSessionAggregate
        {
            BaselineSessionCount = 1,
            Car = live.Context.Car,
            FuelPerLapLiters = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 13.36344d,
                Minimum = 13.36344d,
                Maximum = 13.36344d
            },
            MedianLapSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 482.092804d,
                Minimum = 482.092804d,
                Maximum = 482.092804d
            },
            TeammateDriverStintLaps = new RunningHistoricalMetric
            {
                SampleCount = 2,
                Mean = 8d,
                Minimum = 8d,
                Maximum = 8d
            },
            ObservedFuelFillRateLitersPerSecond = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 2.68d,
                Minimum = 2.68d,
                Maximum = 2.68d
            },
            AveragePitLaneSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 64.822333d,
                Minimum = 64.822333d,
                Maximum = 64.822333d
            },
            AverageTireChangePitServiceSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 39.2d,
                Minimum = 39.2d,
                Maximum = 39.2d
            }
        };

        var strategy = FuelStrategyCalculator.From(live, new SessionHistoryLookupResult(live.Combo, null, aggregate));

        Assert.Equal(new int?[] { 7, 8, 7, 8 }, strategy.Stints.Select(stint => stint.TargetLaps).ToArray());
        Assert.Equal(8, strategy.TeammateStintTargetLaps);
        Assert.Equal("baseline teammate stints", strategy.TeammateStintTargetSource);
        Assert.Equal(8, strategy.FinalStintTargetLaps);
        Assert.Equal("tires +4s", strategy.Stints[0].TireAdvice!.Text);
        Assert.Equal("tires free (106 L)", strategy.Stints[1].TireAdvice!.Text);
        Assert.Equal("8-lap rhythm avoids +1 stop (~65s)", strategy.Status);
        Assert.Equal(1, strategy.RhythmComparison!.AdditionalStopCount);
        Assert.Equal(0.113d, strategy.RequiredFuelSavingLitersPerLap!.Value, precision: 3);
    }

    [Fact]
    public void From_QuantifiesEnduranceRhythmStopCost()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 106d,
            fuelUsePerHourKg: 0d,
            sessionTimeRemain: 86_400d,
            teamLastLapTimeSeconds: 482.092804d,
            fuelMaxLiters: 106d,
            estimatedLapSeconds: 465.166d);
        var aggregate = new HistoricalSessionAggregate
        {
            BaselineSessionCount = 1,
            Car = live.Context.Car,
            FuelPerLapLiters = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 13.36344d,
                Minimum = 13.36344d,
                Maximum = 13.36344d
            },
            MedianLapSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 482.092804d,
                Minimum = 482.092804d,
                Maximum = 482.092804d
            },
            TeammateDriverStintLaps = new RunningHistoricalMetric
            {
                SampleCount = 2,
                Mean = 8d,
                Minimum = 8d,
                Maximum = 8d
            },
            AveragePitLaneSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 64.822333d,
                Minimum = 64.822333d,
                Maximum = 64.822333d
            }
        };

        var strategy = FuelStrategyCalculator.From(live, new SessionHistoryLookupResult(live.Combo, null, aggregate));

        Assert.Equal(180, strategy.PlannedRaceLaps);
        Assert.Equal(23, strategy.PlannedStintCount);
        Assert.Equal(22, strategy.PlannedStopCount);
        Assert.Equal("8-lap rhythm avoids +3 stops (~194s)", strategy.Status);
        Assert.Equal(7, strategy.RhythmComparison!.ShortTargetLaps);
        Assert.Equal(8, strategy.RhythmComparison.LongTargetLaps);
        Assert.Equal(25, strategy.RhythmComparison.ShortStopCount);
        Assert.Equal(22, strategy.RhythmComparison.LongStopCount);
        Assert.Equal(3, strategy.RhythmComparison.AdditionalStopCount);
        Assert.Equal(194.467d, strategy.RhythmComparison.EstimatedTimeLossSeconds!.Value, precision: 3);
        Assert.True(strategy.RhythmComparison.IsRealistic);
    }

    [Fact]
    public void From_RollsFutureStintRowsForwardAfterCompletedStints()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 106d,
            fuelUsePerHourKg: 0d,
            teamLapCompleted: 32,
            leaderLapCompleted: 32,
            sessionTimeRemain: 86_400d - 32d * 482.092804d,
            teamLastLapTimeSeconds: 482.092804d,
            fuelMaxLiters: 106d,
            estimatedLapSeconds: 465.166d) with
        {
            CompletedStintCount = 4
        };
        var aggregate = new HistoricalSessionAggregate
        {
            BaselineSessionCount = 1,
            Car = live.Context.Car,
            FuelPerLapLiters = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 13.36344d,
                Minimum = 13.36344d,
                Maximum = 13.36344d
            },
            MedianLapSeconds = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 482.092804d,
                Minimum = 482.092804d,
                Maximum = 482.092804d
            },
            TeammateDriverStintLaps = new RunningHistoricalMetric
            {
                SampleCount = 2,
                Mean = 8d,
                Minimum = 8d,
                Maximum = 8d
            }
        };

        var strategy = FuelStrategyCalculator.From(live, new SessionHistoryLookupResult(live.Combo, null, aggregate));

        Assert.Equal(5, strategy.Stints[0].Number);
        Assert.Equal(6, strategy.Stints[1].Number);
        Assert.Equal(7, strategy.Stints[2].Number);
    }

    [Fact]
    public void From_UsesObservedFocusStintsAsCurrentSessionTarget()
    {
        var live = CreateLiveSnapshot(
            fuelLevelLiters: 53d,
            fuelUsePerHourKg: 0d,
            teamLapCompleted: 8,
            leaderLapCompleted: 8,
            sessionTimeRemain: 3_600d,
            teamLastLapTimeSeconds: 480d,
            fuelMaxLiters: 106d,
            estimatedLapSeconds: 480d,
            isOnTrack: false) with
        {
            FocusCar = new LiveCarContextSnapshot(
                HasData: true,
                CarIdx: 42,
                Role: "focus",
                IsTeamCar: false,
                OverallPosition: 5,
                ClassPosition: 3,
                CarClass: 4099,
                OnPitRoad: false,
                ProgressLaps: 16d,
                CurrentStintLaps: 1d,
                CurrentStintSeconds: 480d,
                ObservedPitStopCount: 2,
                StintSource: "observed pit exit")
            {
                CompletedStints =
                [
                    new LiveObservedStint(1, 0d, 3_840d, 0d, 8d, 8d, 3_840d, "observed pit exit"),
                    new LiveObservedStint(2, 3_900d, 7_740d, 8d, 16d, 8d, 3_840d, "observed pit exit")
                ]
            }
        };
        var aggregate = new HistoricalSessionAggregate
        {
            BaselineSessionCount = 1,
            Car = live.Context.Car,
            FuelPerLapLiters = new RunningHistoricalMetric
            {
                SampleCount = 1,
                Mean = 13.0d,
                Minimum = 13.0d,
                Maximum = 13.0d
            }
        };

        var strategy = FuelStrategyCalculator.From(live, new SessionHistoryLookupResult(live.Combo, null, aggregate));

        Assert.Equal(8, strategy.TeammateStintTargetLaps);
        Assert.Equal("observed focus stints", strategy.TeammateStintTargetSource);
        Assert.Equal(3, strategy.Stints[0].Number);
    }

    private static LiveTelemetrySnapshot CreateLiveSnapshot(
        double fuelLevelLiters,
        double fuelUsePerHourKg,
        int teamLapCompleted = 0,
        double teamLapDistPct = 0d,
        int leaderLapCompleted = 0,
        double leaderLapDistPct = 0d,
        double sessionTimeRemain = 600d,
        double teamLastLapTimeSeconds = 100d,
        double? leaderLastLapTimeSeconds = null,
        double fuelMaxLiters = 100d,
        double estimatedLapSeconds = 100d,
        bool isOnTrack = true,
        bool isInGarage = false)
    {
        var context = new HistoricalSessionContext
        {
            Car = new HistoricalCarIdentity
            {
                DriverCarFuelMaxLiters = fuelMaxLiters,
                DriverCarFuelKgPerLiter = 0.75d,
                DriverCarEstLapTimeSeconds = estimatedLapSeconds
            },
            Track = new HistoricalTrackIdentity(),
            Session = new HistoricalSessionIdentity
            {
                SessionType = "Race",
                SessionTime = "3600 sec",
                SessionLaps = "unlimited"
            },
            Conditions = new HistoricalSessionInfoConditions()
        };
        var combo = HistoricalComboIdentity.From(context);
        var sample = new HistoricalTelemetrySample(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            SessionTime: 120d,
            SessionTick: 100,
            SessionInfoUpdate: 1,
            IsOnTrack: isOnTrack,
            IsInGarage: isInGarage,
            OnPitRoad: false,
            PitstopActive: false,
            PlayerCarInPitStall: false,
            FuelLevelLiters: fuelLevelLiters,
            FuelLevelPercent: fuelLevelLiters / 100d,
            FuelUsePerHourKg: fuelUsePerHourKg,
            SpeedMetersPerSecond: 50d,
            Lap: teamLapCompleted,
            LapCompleted: teamLapCompleted,
            LapDistPct: teamLapDistPct,
            LapLastLapTimeSeconds: teamLastLapTimeSeconds,
            LapBestLapTimeSeconds: teamLastLapTimeSeconds,
            AirTempC: 20d,
            TrackTempCrewC: 25d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            SessionTimeRemain: sessionTimeRemain,
            SessionTimeTotal: 3_600d,
            SessionLapsRemainEx: 32_767,
            SessionLapsTotal: 32_767,
            SessionState: 4,
            RaceLaps: teamLapCompleted,
            PlayerCarIdx: 15,
            TeamLapCompleted: teamLapCompleted,
            TeamLapDistPct: teamLapDistPct,
            TeamLastLapTimeSeconds: teamLastLapTimeSeconds,
            TeamBestLapTimeSeconds: teamLastLapTimeSeconds,
            TeamPosition: 2,
            TeamClassPosition: 2,
            LeaderCarIdx: 1,
            LeaderLapCompleted: leaderLapCompleted,
            LeaderLapDistPct: leaderLapDistPct,
            LeaderLastLapTimeSeconds: leaderLastLapTimeSeconds);

        return new LiveTelemetrySnapshot(
            IsConnected: true,
            IsCollecting: true,
            SourceId: "test",
            StartedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-2),
            LastUpdatedAtUtc: sample.CapturedAtUtc,
            Sequence: 1,
            Context: context,
            Combo: combo,
            LatestSample: sample,
            Fuel: LiveFuelSnapshot.From(context, sample),
            Proximity: LiveProximitySnapshot.From(context, sample, teamLastLapTimeSeconds),
            LeaderGap: LiveLeaderGapSnapshot.From(sample),
            Weather: LiveWeatherSnapshot.From(context, sample));
    }

    private static SessionHistoryLookupResult EmptyHistory(HistoricalComboIdentity combo)
    {
        return new SessionHistoryLookupResult(combo, UserAggregate: null, BaselineAggregate: null);
    }
}
