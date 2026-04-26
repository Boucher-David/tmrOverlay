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
                StintCount = _validGreenTimeSeconds > 0d ? Math.Max(1, _pitRoadEntryCount + 1) : 0
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

        if (!previous.OnPitRoad && current.OnPitRoad)
        {
            _pitRoadEntryCount++;
        }

        if (!previous.PitstopActive && current.PitstopActive)
        {
            _pitServiceCount++;
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
        return previous.OnPitRoad
            || current.OnPitRoad
            || previous.PitstopActive
            || current.PitstopActive
            || previous.PlayerCarInPitStall
            || current.PlayerCarInPitStall;
    }

    private static bool IsValidFuel(double fuelLevelLiters)
    {
        return !double.IsNaN(fuelLevelLiters) && !double.IsInfinity(fuelLevelLiters) && fuelLevelLiters > 0d;
    }

    private static double CalculateDistanceDeltaLaps(HistoricalTelemetrySample previous, HistoricalTelemetrySample current)
    {
        var previousPosition = LapPosition(previous);
        var currentPosition = LapPosition(current);
        var delta = currentPosition - previousPosition;

        return delta is > 0d and < 0.5d ? delta : 0d;
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
}
