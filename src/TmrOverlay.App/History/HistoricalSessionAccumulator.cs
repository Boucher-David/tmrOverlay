using TmrOverlay.App.AppInfo;

namespace TmrOverlay.App.History;

internal sealed class HistoricalSessionAccumulator
{
    private const double MinimumGreenSecondsForFuelPerHour = 30d;
    private const double MinimumDistanceLapsForFuelPerLap = 0.25d;
    private const double MaximumFuelBurnLitersPerSecond = 0.10d;
    private readonly object _sync = new();
    private HistoricalSessionContext _context = HistoricalSessionContext.Empty;
    private HistoricalTelemetrySample? _previousSample;
    private readonly List<double> _completedLapTimesSeconds = [];
    private readonly List<HistoricalStintSummary> _stints = [];
    private readonly List<HistoricalPitStopSummary> _pitStops = [];
    private StintBuilder? _activeStint;
    private PitStopBuilder? _activePitStop;
    private DateTimeOffset? _firstFrameAtUtc;
    private DateTimeOffset? _lastFrameAtUtc;
    private double _onTrackTimeSeconds;
    private double _pitRoadTimeSeconds;
    private double _movingTimeSeconds;
    private double _validGreenTimeSeconds;
    private double _validDistanceLaps;
    private double _fuelUsedLiters;
    private double _fuelAddedLiters;
    private double? _startingFuelLiters;
    private double? _endingFuelLiters;
    private double? _minimumFuelLiters;
    private double? _maximumFuelLiters;
    private double? _lastLapLastLapTime;
    private int _frameCount;
    private int _pitRoadEntryCount;
    private int _pitServiceCount;

    public void ApplySessionInfo(string sessionInfoYaml)
    {
        lock (_sync)
        {
            _context = SessionInfoSummaryParser.Parse(sessionInfoYaml);
        }
    }

    public void RecordFrame(HistoricalTelemetrySample sample)
    {
        lock (_sync)
        {
            _frameCount++;
            _firstFrameAtUtc ??= sample.CapturedAtUtc;
            _lastFrameAtUtc = sample.CapturedAtUtc;
            TrackFuelExtremes(sample);
            TrackLapTime(sample);

            if (_previousSample is not null)
            {
                RecordDelta(_previousSample, sample);
            }

            _previousSample = sample;
        }
    }

    public HistoricalSessionSummary BuildSummary(
        string sourceCaptureId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        int droppedFrameCount,
        int sessionInfoSnapshotCount)
    {
        lock (_sync)
        {
            var captureDurationSeconds = _firstFrameAtUtc is not null && _lastFrameAtUtc is not null
                ? Math.Max(0d, (_lastFrameAtUtc.Value - _firstFrameAtUtc.Value).TotalSeconds)
                : 0d;

            double? fuelPerHourLiters = _validGreenTimeSeconds >= MinimumGreenSecondsForFuelPerHour && _fuelUsedLiters > 0d
                ? _fuelUsedLiters / _validGreenTimeSeconds * 3600d
                : null;

            double? fuelPerLapLiters = _validDistanceLaps >= MinimumDistanceLapsForFuelPerLap && _fuelUsedLiters > 0d
                ? _fuelUsedLiters / _validDistanceLaps
                : null;

            double? averageLapSeconds = _completedLapTimesSeconds.Count > 0
                ? _completedLapTimesSeconds.Average()
                : null;

            double? medianLapSeconds = _completedLapTimesSeconds.Count > 0
                ? Median(_completedLapTimesSeconds)
                : null;

            double? bestLapSeconds = _completedLapTimesSeconds.Count > 0
                ? _completedLapTimesSeconds.Min()
                : null;

            FinalizeActiveStint();
            FinalizeActivePitStop();

            double? averageStintLaps = _stints.Count > 0
                ? _stints.Average(stint => stint.DistanceLaps)
                : null;

            double? averageStintSeconds = _stints.Count > 0
                ? _stints.Average(stint => stint.DurationSeconds)
                : null;

            double? averageStintFuelPerLapLiters = Average(_stints.Select(stint => stint.FuelPerLapLiters));

            double? averagePitLaneSeconds = _pitStops.Count > 0
                ? _pitStops.Average(stop => stop.PitLaneSeconds)
                : null;

            double? averagePitStallSeconds = Average(_pitStops.Select(stop => stop.PitStallSeconds));
            double? averagePitServiceSeconds = Average(_pitStops.Select(stop => stop.ServiceActiveSeconds));
            double? observedFuelFillRateLitersPerSecond = Average(_pitStops.Select(stop => stop.FuelFillRateLitersPerSecond));
            double? averageTireChangePitServiceSeconds = Average(_pitStops
                .Where(stop => stop.TireSetChanged)
                .Select(stop => stop.ServiceActiveSeconds ?? stop.PitStallSeconds));
            double? averageNoTirePitServiceSeconds = Average(_pitStops
                .Where(stop => !stop.TireSetChanged)
                .Select(stop => stop.ServiceActiveSeconds ?? stop.PitStallSeconds));

            var metrics = new HistoricalSessionMetrics
            {
                SampleFrameCount = _frameCount,
                DroppedFrameCount = droppedFrameCount,
                SessionInfoSnapshotCount = sessionInfoSnapshotCount,
                CaptureDurationSeconds = captureDurationSeconds,
                OnTrackTimeSeconds = _onTrackTimeSeconds,
                PitRoadTimeSeconds = _pitRoadTimeSeconds,
                MovingTimeSeconds = _movingTimeSeconds,
                ValidGreenTimeSeconds = _validGreenTimeSeconds,
                ValidDistanceLaps = _validDistanceLaps,
                CompletedValidLaps = _completedLapTimesSeconds.Count,
                FuelUsedLiters = _fuelUsedLiters,
                FuelAddedLiters = _fuelAddedLiters,
                FuelPerHourLiters = fuelPerHourLiters,
                FuelPerLapLiters = fuelPerLapLiters,
                AverageLapSeconds = averageLapSeconds,
                MedianLapSeconds = medianLapSeconds,
                BestLapSeconds = bestLapSeconds,
                StartingFuelLiters = _startingFuelLiters,
                EndingFuelLiters = _endingFuelLiters,
                MinimumFuelLiters = _minimumFuelLiters,
                MaximumFuelLiters = _maximumFuelLiters,
                PitRoadEntryCount = _pitRoadEntryCount,
                PitServiceCount = _pitServiceCount,
                StintCount = _stints.Count,
                AverageStintLaps = averageStintLaps,
                AverageStintSeconds = averageStintSeconds,
                AverageStintFuelPerLapLiters = averageStintFuelPerLapLiters,
                AveragePitLaneSeconds = averagePitLaneSeconds,
                AveragePitStallSeconds = averagePitStallSeconds,
                AveragePitServiceSeconds = averagePitServiceSeconds,
                ObservedFuelFillRateLitersPerSecond = observedFuelFillRateLitersPerSecond,
                AverageTireChangePitServiceSeconds = averageTireChangePitServiceSeconds,
                AverageNoTirePitServiceSeconds = averageNoTirePitServiceSeconds
            };

            return new HistoricalSessionSummary
            {
                SourceCaptureId = sourceCaptureId,
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc,
                Combo = HistoricalComboIdentity.From(_context),
                Car = _context.Car,
                Track = _context.Track,
                Session = _context.Session,
                Conditions = BuildConditions(),
                Metrics = metrics,
                Stints = _stints.ToArray(),
                PitStops = _pitStops.ToArray(),
                Quality = HistoricalDataQuality.From(_context, metrics),
                AppVersion = AppVersionInfo.Current
            };
        }
    }

    private void RecordDelta(HistoricalTelemetrySample previous, HistoricalTelemetrySample current)
    {
        var deltaSeconds = current.SessionTime - previous.SessionTime;
        if (deltaSeconds <= 0d || deltaSeconds > 1d)
        {
            return;
        }

        if (previous.IsOnTrack)
        {
            _onTrackTimeSeconds += deltaSeconds;
        }

        if (previous.OnPitRoad)
        {
            _pitRoadTimeSeconds += deltaSeconds;
        }

        if (previous.SpeedMetersPerSecond > 1d)
        {
            _movingTimeSeconds += deltaSeconds;
        }

        var previousOnPitRoad = IsTeamCarOnPitRoad(previous);
        var currentOnPitRoad = IsTeamCarOnPitRoad(current);
        var previousInStint = IsTeamCarInGreenStint(previous);
        var currentInStint = IsTeamCarInGreenStint(current);

        if (_activeStint is null && currentInStint)
        {
            _activeStint = StintBuilder.Start(_stints.Count + 1, current);
        }

        if (!previousOnPitRoad && currentOnPitRoad)
        {
            FinalizeActiveStint(previous);
            _pitRoadEntryCount++;
            _activePitStop = PitStopBuilder.Start(_pitStops.Count + 1, current);
        }
        else if (previousInStint && !currentInStint)
        {
            FinalizeActiveStint(current);
        }

        if (!previous.PitstopActive && current.PitstopActive)
        {
            _pitServiceCount++;
        }

        if (_activePitStop is not null)
        {
            _activePitStop.RecordDelta(previous, current, deltaSeconds);
        }

        if (_activeStint is not null)
        {
            _activeStint.RecordDelta(previous, current, deltaSeconds);
        }

        if (previousOnPitRoad && !currentOnPitRoad)
        {
            FinalizeActivePitStop(current);
            if (currentInStint)
            {
                _activeStint = StintBuilder.Start(_stints.Count + 1, current);
            }
        }

        if (IsGreenFuelSample(previous) && IsValidFuel(current.FuelLevelLiters))
        {
            _validGreenTimeSeconds += deltaSeconds;
            _validDistanceLaps += CalculateDistanceDeltaLaps(previous, current);

            var fuelDeltaLiters = previous.FuelLevelLiters - current.FuelLevelLiters;
            var maximumExpectedBurn = Math.Max(0.05d, deltaSeconds * MaximumFuelBurnLitersPerSecond);
            if (fuelDeltaLiters > 0d && fuelDeltaLiters <= maximumExpectedBurn)
            {
                _fuelUsedLiters += fuelDeltaLiters;
            }
        }

        var addedFuelLiters = current.FuelLevelLiters - previous.FuelLevelLiters;
        if (addedFuelLiters > 0.25d && IsPitServiceContext(previous, current))
        {
            _fuelAddedLiters += addedFuelLiters;
        }
    }

    private void TrackFuelExtremes(HistoricalTelemetrySample sample)
    {
        if (!IsValidFuel(sample.FuelLevelLiters))
        {
            return;
        }

        _startingFuelLiters ??= sample.FuelLevelLiters;
        _endingFuelLiters = sample.FuelLevelLiters;
        _minimumFuelLiters = _minimumFuelLiters is null ? sample.FuelLevelLiters : Math.Min(_minimumFuelLiters.Value, sample.FuelLevelLiters);
        _maximumFuelLiters = _maximumFuelLiters is null ? sample.FuelLevelLiters : Math.Max(_maximumFuelLiters.Value, sample.FuelLevelLiters);
    }

    private void TrackLapTime(HistoricalTelemetrySample sample)
    {
        var lapTime = sample.LapLastLapTimeSeconds;
        if (lapTime is null || lapTime <= 20d || lapTime > 1800d)
        {
            return;
        }

        if (_lastLapLastLapTime is not null && Math.Abs(_lastLapLastLapTime.Value - lapTime.Value) < 0.001d)
        {
            return;
        }

        _lastLapLastLapTime = lapTime;
        _completedLapTimesSeconds.Add(lapTime.Value);
    }

    private HistoricalConditions BuildConditions()
    {
        var latestSample = _previousSample;

        return new HistoricalConditions
        {
            AirTempC = latestSample?.AirTempC,
            TrackTempCrewC = latestSample?.TrackTempCrewC,
            TrackWetness = latestSample?.TrackWetness,
            WeatherDeclaredWet = latestSample?.WeatherDeclaredWet,
            PlayerTireCompound = latestSample?.PlayerTireCompound,
            TrackWeatherType = _context.Conditions.TrackWeatherType,
            TrackSkies = _context.Conditions.TrackSkies,
            TrackPrecipitationPercent = _context.Conditions.TrackPrecipitationPercent,
            SessionTrackRubberState = _context.Conditions.SessionTrackRubberState
        };
    }

    private static bool IsGreenFuelSample(HistoricalTelemetrySample sample)
    {
        return sample.IsOnTrack
            && !sample.OnPitRoad
            && !sample.IsInGarage
            && sample.SpeedMetersPerSecond > 5d
            && IsValidFuel(sample.FuelLevelLiters);
    }

    private static bool IsPitServiceContext(HistoricalTelemetrySample previous, HistoricalTelemetrySample current)
    {
        return IsTeamCarOnPitRoad(previous)
            || IsTeamCarOnPitRoad(current)
            || previous.PitstopActive
            || current.PitstopActive
            || previous.PlayerCarInPitStall
            || current.PlayerCarInPitStall;
    }

    private static bool IsValidFuel(double fuelLevelLiters)
    {
        return !double.IsNaN(fuelLevelLiters) && !double.IsInfinity(fuelLevelLiters) && fuelLevelLiters > 0d;
    }

    private static bool IsTeamCarOnPitRoad(HistoricalTelemetrySample sample)
    {
        return sample.TeamOnPitRoad ?? sample.OnPitRoad;
    }

    private static bool IsTeamCarInGreenStint(HistoricalTelemetrySample sample)
    {
        return HasTeamProgress(sample)
            && !IsTeamCarOnPitRoad(sample)
            && !sample.IsInGarage
            && (sample.SessionState is null || sample.SessionState < 5);
    }

    private static bool HasTeamProgress(HistoricalTelemetrySample sample)
    {
        return sample.TeamLapCompleted is { } teamLapCompleted
            && teamLapCompleted >= 0
            && sample.TeamLapDistPct is { } teamLapDistPct
            && teamLapDistPct >= 0d;
    }

    private void FinalizeActiveStint(HistoricalTelemetrySample? endSample = null)
    {
        if (_activeStint is null)
        {
            return;
        }

        var summary = _activeStint.Build(endSample ?? _previousSample);
        if (summary.DurationSeconds >= 30d || summary.DistanceLaps >= 0.1d)
        {
            _stints.Add(summary);
        }

        _activeStint = null;
    }

    private void FinalizeActivePitStop(HistoricalTelemetrySample? exitSample = null)
    {
        if (_activePitStop is null)
        {
            return;
        }

        var summary = _activePitStop.Build(exitSample ?? _previousSample);
        if (summary.PitLaneSeconds >= 1d)
        {
            _pitStops.Add(summary);
        }

        _activePitStop = null;
    }

    private static double? Average(IEnumerable<double?> values)
    {
        var valid = values
            .Where(value => value is not null && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value))
            .Select(value => value!.Value)
            .ToArray();

        return valid.Length > 0 ? valid.Average() : null;
    }

    private static double CalculateDistanceDeltaLaps(HistoricalTelemetrySample previous, HistoricalTelemetrySample current)
    {
        var previousPosition = TeamLapPosition(previous);
        var currentPosition = TeamLapPosition(current);
        var delta = currentPosition - previousPosition;

        return delta is > 0d and < 0.5d ? delta : 0d;
    }

    private static double TeamLapPosition(HistoricalTelemetrySample sample)
    {
        if (sample.TeamLapCompleted is { } teamLapCompleted
            && teamLapCompleted >= 0
            && sample.TeamLapDistPct is { } teamLapDistPct
            && teamLapDistPct >= 0d)
        {
            return teamLapCompleted + Math.Clamp(teamLapDistPct, 0d, 1d);
        }

        return LapPosition(sample);
    }

    private static double LapPosition(HistoricalTelemetrySample sample)
    {
        var lap = sample.Lap >= 0 ? sample.Lap : 0;
        return lap + Math.Clamp(sample.LapDistPct, 0d, 1d);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;

        if (sorted.Length % 2 == 1)
        {
            return sorted[middle];
        }

        return (sorted[middle - 1] + sorted[middle]) / 2d;
    }

    private sealed class StintBuilder
    {
        private readonly int _stintNumber;
        private readonly HistoricalTelemetrySample _start;
        private readonly double _startPosition;
        private double _distanceLaps;
        private double? _fuelStartLiters;
        private double? _fuelEndLiters;
        private bool _sawLocalFuelScalar;
        private bool _sawTeamCarArray;

        private StintBuilder(int stintNumber, HistoricalTelemetrySample start)
        {
            _stintNumber = stintNumber;
            _start = start;
            _startPosition = TeamLapPosition(start);
            _sawTeamCarArray = start.TeamLapCompleted is not null && start.TeamLapDistPct is not null;
            TrackFuel(start);
        }

        public static StintBuilder Start(int stintNumber, HistoricalTelemetrySample start)
        {
            return new StintBuilder(stintNumber, start);
        }

        public void RecordDelta(HistoricalTelemetrySample previous, HistoricalTelemetrySample current, double deltaSeconds)
        {
            _sawTeamCarArray |= current.TeamLapCompleted is not null && current.TeamLapDistPct is not null;
            var distanceDelta = CalculateDistanceDeltaLaps(previous, current);
            if (distanceDelta > 0d)
            {
                _distanceLaps += distanceDelta;
            }

            TrackFuel(current);
        }

        public HistoricalStintSummary Build(HistoricalTelemetrySample? endSample)
        {
            var end = endSample ?? _start;
            var durationSeconds = Math.Max(0d, end.SessionTime - _start.SessionTime);
            var endPosition = TeamLapPosition(end);
            var distanceLaps = _distanceLaps > 0d
                ? _distanceLaps
                : Math.Max(0d, endPosition - _startPosition);
            double? fuelUsedLiters = _fuelStartLiters is not null && _fuelEndLiters is not null
                ? Math.Max(0d, _fuelStartLiters.Value - _fuelEndLiters.Value)
                : null;
            double? fuelPerLap = fuelUsedLiters is { } fuelUsed && fuelUsed > 0d && distanceLaps > 0.1d
                ? fuelUsed / distanceLaps
                : null;
            var confidence = new List<string>();

            if (_sawTeamCarArray)
            {
                confidence.Add("team_car_array_distance");
            }

            if (_sawLocalFuelScalar)
            {
                confidence.Add("local_fuel_scalar");
            }
            else
            {
                confidence.Add("fuel_inferred_from_history_only");
            }

            return new HistoricalStintSummary
            {
                StintNumber = _stintNumber,
                StartRaceTimeSeconds = _start.SessionTime,
                EndRaceTimeSeconds = end.SessionTime,
                DurationSeconds = durationSeconds,
                StartLapCompleted = _start.TeamLapCompleted ?? _start.LapCompleted,
                EndLapCompleted = end.TeamLapCompleted ?? end.LapCompleted,
                DistanceLaps = distanceLaps,
                FuelStartLiters = _fuelStartLiters,
                FuelEndLiters = _fuelEndLiters,
                FuelUsedLiters = fuelUsedLiters,
                FuelPerLapLiters = fuelPerLap,
                DriverRole = _sawLocalFuelScalar ? "local-driver-scalar" : "team-driver-inferred",
                ConfidenceFlags = confidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        private void TrackFuel(HistoricalTelemetrySample sample)
        {
            if (!IsValidFuel(sample.FuelLevelLiters))
            {
                return;
            }

            _sawLocalFuelScalar = true;
            _fuelStartLiters ??= sample.FuelLevelLiters;
            _fuelEndLiters = sample.FuelLevelLiters;
        }
    }

    private sealed class PitStopBuilder
    {
        private readonly int _stopNumber;
        private readonly HistoricalTelemetrySample _entry;
        private double _stallSeconds;
        private double _serviceActiveSeconds;
        private double? _fuelBeforeLiters;
        private double? _fuelAfterLiters;
        private int? _startingTireSetsUsed;
        private int? _endingTireSetsUsed;
        private int? _startingFastRepairUsed;
        private int? _endingFastRepairUsed;
        private int? _pitServiceFlags;
        private bool _sawLocalFuelScalar;
        private bool _sawTeamCarArray;
        private bool _sawServiceActive;
        private bool _sawPitStall;

        private PitStopBuilder(int stopNumber, HistoricalTelemetrySample entry)
        {
            _stopNumber = stopNumber;
            _entry = entry;
            _startingTireSetsUsed = entry.TireSetsUsed;
            _endingTireSetsUsed = entry.TireSetsUsed;
            _startingFastRepairUsed = entry.TeamFastRepairsUsed ?? entry.FastRepairUsed;
            _endingFastRepairUsed = _startingFastRepairUsed;
            _pitServiceFlags = entry.PitServiceFlags;
            TrackFuel(entry);
            _sawTeamCarArray = entry.TeamOnPitRoad is not null;
        }

        public static PitStopBuilder Start(int stopNumber, HistoricalTelemetrySample entry)
        {
            return new PitStopBuilder(stopNumber, entry);
        }

        public void RecordDelta(HistoricalTelemetrySample previous, HistoricalTelemetrySample current, double deltaSeconds)
        {
            _sawTeamCarArray |= previous.TeamOnPitRoad is not null || current.TeamOnPitRoad is not null;
            if (previous.PlayerCarInPitStall)
            {
                _stallSeconds += deltaSeconds;
                _sawPitStall = true;
            }

            if (previous.PitstopActive)
            {
                _serviceActiveSeconds += deltaSeconds;
                _sawServiceActive = true;
            }

            _endingTireSetsUsed = current.TireSetsUsed ?? _endingTireSetsUsed;
            _endingFastRepairUsed = current.TeamFastRepairsUsed ?? current.FastRepairUsed ?? _endingFastRepairUsed;
            _pitServiceFlags = current.PitServiceFlags ?? _pitServiceFlags;
            TrackFuel(current);
        }

        public HistoricalPitStopSummary Build(HistoricalTelemetrySample? exitSample)
        {
            var exit = exitSample ?? _entry;
            var pitLaneSeconds = Math.Max(0d, exit.SessionTime - _entry.SessionTime);
            double? fuelAddedLiters = _fuelBeforeLiters is not null && _fuelAfterLiters is not null
                ? Math.Max(0d, _fuelAfterLiters.Value - _fuelBeforeLiters.Value)
                : null;
            double? fuelFillRate = fuelAddedLiters is { } addedFuel && addedFuel > 0d && _serviceActiveSeconds > 0d
                ? addedFuel / _serviceActiveSeconds
                : null;
            var confidence = new List<string>();
            if (_sawTeamCarArray)
            {
                confidence.Add("team_car_array");
            }

            if (_sawLocalFuelScalar)
            {
                confidence.Add("local_fuel_scalar");
            }
            else
            {
                confidence.Add("fuel_unavailable_or_teammate_invalid");
            }

            if (_sawPitStall)
            {
                confidence.Add("pit_stall_signal");
            }

            if (_sawServiceActive)
            {
                confidence.Add("service_active_signal");
            }

            return new HistoricalPitStopSummary
            {
                StopNumber = _stopNumber,
                EntryRaceTimeSeconds = _entry.SessionTime,
                ExitRaceTimeSeconds = exit.SessionTime,
                PitLaneSeconds = pitLaneSeconds,
                EntryLapCompleted = _entry.TeamLapCompleted ?? _entry.LapCompleted,
                ExitLapCompleted = exit.TeamLapCompleted ?? exit.LapCompleted,
                PitStallSeconds = _stallSeconds > 0d ? _stallSeconds : null,
                ServiceActiveSeconds = _serviceActiveSeconds > 0d ? _serviceActiveSeconds : null,
                FuelBeforeLiters = _fuelBeforeLiters,
                FuelAfterLiters = _fuelAfterLiters,
                FuelAddedLiters = fuelAddedLiters,
                FuelFillRateLitersPerSecond = fuelFillRate,
                TireSetChanged = _startingTireSetsUsed is not null
                    && _endingTireSetsUsed is not null
                    && _endingTireSetsUsed > _startingTireSetsUsed,
                FastRepairUsed = _startingFastRepairUsed is not null
                    && _endingFastRepairUsed is not null
                    && _endingFastRepairUsed > _startingFastRepairUsed,
                PitServiceFlags = _pitServiceFlags,
                ConfidenceFlags = confidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        private void TrackFuel(HistoricalTelemetrySample sample)
        {
            if (!IsValidFuel(sample.FuelLevelLiters))
            {
                return;
            }

            _sawLocalFuelScalar = true;
            _fuelBeforeLiters ??= sample.FuelLevelLiters;
            _fuelAfterLiters = sample.FuelLevelLiters;
        }
    }
}
