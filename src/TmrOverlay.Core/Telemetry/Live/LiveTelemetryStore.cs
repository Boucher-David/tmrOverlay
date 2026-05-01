using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal sealed class LiveTelemetryStore : ILiveTelemetrySource, ILiveTelemetrySink
{
    private const double CloseRadarRangeSeconds = 7d;
    private const double MulticlassWarningRangeSeconds = 25d;
    private const double CloseRadarRangeLaps = 0.02d;
    private const double MulticlassWarningRangeLaps = 0.14d;
    private const double MinimumClosingRateSecondsPerSecond = 0.15d;

    private readonly object _sync = new();
    private readonly Dictionary<int, ProximityHistory> _proximityHistory = [];
    private readonly Dictionary<int, CarStintHistory> _carStintHistory = [];
    private HistoricalSessionContext _context = HistoricalSessionContext.Empty;
    private LiveTelemetrySnapshot _snapshot = LiveTelemetrySnapshot.Empty;
    private long _sequence;

    public LiveTelemetrySnapshot Snapshot()
    {
        lock (_sync)
        {
            return _snapshot;
        }
    }

    public void MarkConnected()
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                IsConnected = true,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                Sequence = ++_sequence
            };
        }
    }

    public void MarkCollectionStarted(string sourceId, DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _proximityHistory.Clear();
            _carStintHistory.Clear();
            _snapshot = _snapshot with
            {
                IsConnected = true,
                IsCollecting = true,
                SourceId = sourceId,
                StartedAtUtc = startedAtUtc,
                LastUpdatedAtUtc = startedAtUtc,
                Sequence = ++_sequence
            };
        }
    }

    public void MarkDisconnected()
    {
        lock (_sync)
        {
            _context = HistoricalSessionContext.Empty;
            _proximityHistory.Clear();
            _carStintHistory.Clear();
            _snapshot = LiveTelemetrySnapshot.Empty with
            {
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                Sequence = ++_sequence
            };
        }
    }

    public void ApplySessionInfo(string sessionInfoYaml)
    {
        var context = SessionInfoSummaryParser.Parse(sessionInfoYaml);
        lock (_sync)
        {
            _context = context;
            _snapshot = _snapshot with
            {
                Context = context,
                Combo = HistoricalComboIdentity.From(context),
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                Sequence = ++_sequence
            };
        }
    }

    public void RecordFrame(HistoricalTelemetrySample sample)
    {
        lock (_sync)
        {
            var fuel = LiveFuelSnapshot.From(_context, sample);
            var proximity = LiveProximitySnapshot.From(_context, sample, fuel.LapTimeSeconds);
            var multiclassApproaches = BuildMulticlassApproaches(sample, proximity);
            proximity = proximity with
            {
                MulticlassApproaches = multiclassApproaches,
                StrongestMulticlassApproach = multiclassApproaches.MaxBy(approach => approach.Urgency)
            };
            UpdateProximityHistory(sample.CapturedAtUtc, proximity);
            UpdateCarStintHistory(sample);
            var teamCar = BuildTeamCarContext(sample);
            var focusCar = BuildFocusCarContext(sample, teamCar.CarIdx);

            _snapshot = new LiveTelemetrySnapshot(
                IsConnected: true,
                IsCollecting: true,
                SourceId: _snapshot.SourceId,
                StartedAtUtc: _snapshot.StartedAtUtc ?? sample.CapturedAtUtc,
                LastUpdatedAtUtc: sample.CapturedAtUtc,
                Sequence: ++_sequence,
                Context: _context,
                Combo: HistoricalComboIdentity.From(_context),
                LatestSample: sample,
                Fuel: fuel,
                Proximity: proximity,
                LeaderGap: LiveLeaderGapSnapshot.From(_context, sample))
            {
                TeamCar = teamCar,
                FocusCar = focusCar
            };
        }
    }

    private IReadOnlyList<LiveMulticlassApproach> BuildMulticlassApproaches(
        HistoricalTelemetrySample sample,
        LiveProximitySnapshot proximity)
    {
        var hasNonTeamFocus = sample.FocusCarIdx is { } focusCarIdx
            && sample.PlayerCarIdx is { } playerCarIdx
            && focusCarIdx != playerCarIdx;
        var referenceClass = hasNonTeamFocus ? sample.FocusCarClass : sample.FocusCarClass ?? sample.TeamCarClass;
        if (referenceClass is null || proximity.NearbyCars.Count == 0)
        {
            return [];
        }

        var approaches = new List<LiveMulticlassApproach>();
        foreach (var car in proximity.NearbyCars)
        {
            if (car.CarClass is null || car.CarClass == referenceClass)
            {
                continue;
            }

            if (!IsBehind(car) || !IsInMulticlassWarningRange(car))
            {
                continue;
            }

            var closingRate = CalculateClosingRate(sample.CapturedAtUtc, car);
            var closeEnoughForEarlyWarning = IsCloseEnoughForEarlyWarning(car);
            if (closingRate is null && !closeEnoughForEarlyWarning)
            {
                continue;
            }

            if (closingRate is not null
                && closingRate.Value < MinimumClosingRateSecondsPerSecond
                && !closeEnoughForEarlyWarning)
            {
                continue;
            }

            approaches.Add(new LiveMulticlassApproach(
                CarIdx: car.CarIdx,
                CarClass: car.CarClass,
                RelativeLaps: car.RelativeLaps,
                RelativeSeconds: car.RelativeSeconds,
                ClosingRateSecondsPerSecond: closingRate,
                Urgency: CalculateUrgency(car, closingRate)));
        }

        return approaches
            .OrderByDescending(approach => approach.Urgency)
            .ToArray();
    }

    private double? CalculateClosingRate(DateTimeOffset timestampUtc, LiveProximityCar car)
    {
        if (!_proximityHistory.TryGetValue(car.CarIdx, out var previous))
        {
            return null;
        }

        var elapsedSeconds = (timestampUtc - previous.TimestampUtc).TotalSeconds;
        if (elapsedSeconds is <= 0.1d or > 10d)
        {
            return null;
        }

        if (car.RelativeSeconds is { } currentSeconds && previous.RelativeSeconds is { } previousSeconds)
        {
            return (currentSeconds - previousSeconds) / elapsedSeconds;
        }

        return null;
    }

    private void UpdateProximityHistory(DateTimeOffset timestampUtc, LiveProximitySnapshot proximity)
    {
        foreach (var car in proximity.NearbyCars)
        {
            _proximityHistory[car.CarIdx] = new ProximityHistory(
                timestampUtc,
                car.RelativeSeconds,
                car.CarClass);
        }

        foreach (var carIdx in _proximityHistory.Keys.ToArray())
        {
            if ((timestampUtc - _proximityHistory[carIdx].TimestampUtc).TotalSeconds > 30d)
            {
                _proximityHistory.Remove(carIdx);
            }
        }
    }

    private void UpdateCarStintHistory(HistoricalTelemetrySample sample)
    {
        foreach (var observation in ObservedCars(sample))
        {
            if (observation.ProgressLaps is not { } progressLaps || !IsFinite(progressLaps) || progressLaps < 0d)
            {
                continue;
            }

            if (!_carStintHistory.TryGetValue(observation.CarIdx, out var history))
            {
                history = new CarStintHistory(sample.SessionTime, progressLaps, observation.OnPitRoad);
                _carStintHistory[observation.CarIdx] = history;
                continue;
            }

            history.Update(sample.SessionTime, progressLaps, observation.OnPitRoad);
        }
    }

    private static IEnumerable<CarObservation> ObservedCars(HistoricalTelemetrySample sample)
    {
        var observations = new Dictionary<int, CarObservation>();
        AddObservation(observations, TeamObservation(sample));
        AddObservation(observations, FocusObservation(sample));

        foreach (var car in sample.ClassCars ?? [])
        {
            AddObservation(observations, ObservationFromProximity(car));
        }

        foreach (var car in sample.NearbyCars ?? [])
        {
            AddObservation(observations, ObservationFromProximity(car));
        }

        return observations.Values;
    }

    private static void AddObservation(Dictionary<int, CarObservation> observations, CarObservation? observation)
    {
        if (observation is null)
        {
            return;
        }

        observations[observation.CarIdx] = observation;
    }

    private LiveCarContextSnapshot BuildTeamCarContext(HistoricalTelemetrySample sample)
    {
        var carIdx = sample.PlayerCarIdx;
        var progress = Progress(sample.TeamLapCompleted, sample.TeamLapDistPct) ?? Progress(sample.LapCompleted, sample.LapDistPct);
        return BuildCarContext(
            role: "team",
            isTeamCar: true,
            carIdx: carIdx,
            overallPosition: sample.TeamPosition,
            classPosition: sample.TeamClassPosition,
            carClass: sample.TeamCarClass,
            onPitRoad: sample.TeamOnPitRoad ?? sample.OnPitRoad,
            progress: progress);
    }

    private LiveCarContextSnapshot BuildFocusCarContext(HistoricalTelemetrySample sample, int? teamCarIdx)
    {
        var carIdx = sample.FocusCarIdx ?? teamCarIdx;
        var isTeamCar = carIdx is not null && teamCarIdx is not null && carIdx == teamCarIdx;
        var progress = Progress(sample.FocusLapCompleted, sample.FocusLapDistPct)
            ?? (isTeamCar ? Progress(sample.TeamLapCompleted, sample.TeamLapDistPct) : null)
            ?? (isTeamCar ? Progress(sample.LapCompleted, sample.LapDistPct) : null);
        return BuildCarContext(
            role: isTeamCar ? "team" : "focus",
            isTeamCar: isTeamCar,
            carIdx: carIdx,
            overallPosition: sample.FocusPosition ?? (isTeamCar ? sample.TeamPosition : null),
            classPosition: sample.FocusClassPosition ?? (isTeamCar ? sample.TeamClassPosition : null),
            carClass: sample.FocusCarClass ?? (isTeamCar ? sample.TeamCarClass : null),
            onPitRoad: sample.FocusOnPitRoad ?? (isTeamCar ? sample.TeamOnPitRoad ?? sample.OnPitRoad : null),
            progress: progress);
    }

    private LiveCarContextSnapshot BuildCarContext(
        string role,
        bool isTeamCar,
        int? carIdx,
        int? overallPosition,
        int? classPosition,
        int? carClass,
        bool? onPitRoad,
        double? progress)
    {
        if (carIdx is not { } idx)
        {
            return LiveCarContextSnapshot.Unavailable with { Role = role };
        }

        _carStintHistory.TryGetValue(idx, out var stintHistory);
        return new LiveCarContextSnapshot(
            HasData: true,
            CarIdx: idx,
            Role: role,
            IsTeamCar: isTeamCar,
            OverallPosition: overallPosition,
            ClassPosition: classPosition,
            CarClass: carClass,
            OnPitRoad: onPitRoad,
            ProgressLaps: progress,
            CurrentStintLaps: progress is { } progressLaps ? stintHistory?.CurrentStintLaps(progressLaps) : null,
            CurrentStintSeconds: stintHistory?.CurrentStintSeconds,
            ObservedPitStopCount: stintHistory?.ObservedPitStopCount ?? 0,
            StintSource: stintHistory?.StintSource ?? "unavailable");
    }

    private static CarObservation? TeamObservation(HistoricalTelemetrySample sample)
    {
        return sample.PlayerCarIdx is { } carIdx
            ? new CarObservation(
                carIdx,
                Progress(sample.TeamLapCompleted, sample.TeamLapDistPct) ?? Progress(sample.LapCompleted, sample.LapDistPct),
                sample.TeamOnPitRoad ?? sample.OnPitRoad)
            : null;
    }

    private static CarObservation? FocusObservation(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is { } carIdx
            ? new CarObservation(
                carIdx,
                Progress(sample.FocusLapCompleted, sample.FocusLapDistPct),
                sample.FocusOnPitRoad)
            : null;
    }

    private static CarObservation ObservationFromProximity(HistoricalCarProximity car)
    {
        return new CarObservation(
            car.CarIdx,
            Progress(car.LapCompleted, car.LapDistPct),
            car.OnPitRoad);
    }

    private static double? Progress(int? lapCompleted, double? lapDistPct)
    {
        return lapCompleted is { } completed
            && completed >= 0
            && lapDistPct is { } pct
            && IsFinite(pct)
            && pct >= 0d
            ? completed + Math.Clamp(pct, 0d, 1d)
            : null;
    }

    private static bool IsBehind(LiveProximityCar car)
    {
        return car.RelativeSeconds is { } seconds
            ? seconds < 0d
            : car.RelativeLaps < 0d;
    }

    private static bool IsInCloseRadarRange(LiveProximityCar car)
    {
        return car.RelativeSeconds is { } seconds
            ? Math.Abs(seconds) <= CloseRadarRangeSeconds
            : Math.Abs(car.RelativeLaps) <= CloseRadarRangeLaps;
    }

    private static bool IsInMulticlassWarningRange(LiveProximityCar car)
    {
        return car.RelativeSeconds is { } seconds
            ? Math.Abs(seconds) <= MulticlassWarningRangeSeconds
            : Math.Abs(car.RelativeLaps) <= MulticlassWarningRangeLaps;
    }

    private static bool IsCloseEnoughForEarlyWarning(LiveProximityCar car)
    {
        return car.RelativeSeconds is { } seconds
            ? seconds >= -MulticlassWarningRangeSeconds
            : car.RelativeLaps >= -0.07d;
    }

    private static double CalculateUrgency(LiveProximityCar car, double? closingRate)
    {
        var ratio = car.RelativeSeconds is { } seconds
            ? RangeUrgency(Math.Abs(seconds), CloseRadarRangeSeconds, MulticlassWarningRangeSeconds)
            : RangeUrgency(Math.Abs(car.RelativeLaps), CloseRadarRangeLaps, MulticlassWarningRangeLaps);

        var closingBoost = closingRate is { } rate
            ? Math.Clamp(rate / 1.5d, 0d, 0.25d)
            : 0d;
        return Math.Clamp(ratio + closingBoost, 0.05d, 1d);
    }

    private static double RangeUrgency(double distance, double closeRange, double warningRange)
    {
        if (warningRange <= closeRange)
        {
            return 1d - Math.Clamp(distance / Math.Max(0.001d, warningRange), 0d, 1d);
        }

        return 1d - Math.Clamp((distance - closeRange) / Math.Max(0.001d, warningRange - closeRange), 0d, 1d);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private sealed record ProximityHistory(
        DateTimeOffset TimestampUtc,
        double? RelativeSeconds,
        int? CarClass);

    private sealed record CarObservation(
        int CarIdx,
        double? ProgressLaps,
        bool? OnPitRoad);

    private sealed class CarStintHistory
    {
        private double _startSessionTime;
        private double _startProgressLaps;
        private double _lastSessionTime;
        private bool? _lastOnPitRoad;
        private bool _observedPitExit;

        public CarStintHistory(double sessionTime, double progressLaps, bool? onPitRoad)
        {
            _startSessionTime = sessionTime;
            _startProgressLaps = progressLaps;
            _lastSessionTime = sessionTime;
            _lastOnPitRoad = onPitRoad;
        }

        public int ObservedPitStopCount { get; private set; }

        public double CurrentStintSeconds => Math.Max(0d, _lastSessionTime - _startSessionTime);

        public string StintSource => _observedPitExit ? "observed pit exit" : "observed since first seen";

        public void Update(double sessionTime, double progressLaps, bool? onPitRoad)
        {
            if (progressLaps + 0.25d < _startProgressLaps || sessionTime + 1d < _lastSessionTime)
            {
                Reset(sessionTime, progressLaps, onPitRoad);
                return;
            }

            if (_lastOnPitRoad == true && onPitRoad != true)
            {
                _startSessionTime = sessionTime;
                _startProgressLaps = progressLaps;
                ObservedPitStopCount++;
                _observedPitExit = true;
            }

            _lastSessionTime = sessionTime;
            _lastOnPitRoad = onPitRoad;
        }

        public double? CurrentStintLaps(double currentProgressLaps)
        {
            var laps = currentProgressLaps - _startProgressLaps;
            return laps >= -0.05d ? Math.Max(0d, laps) : null;
        }

        private void Reset(double sessionTime, double progressLaps, bool? onPitRoad)
        {
            _startSessionTime = sessionTime;
            _startProgressLaps = progressLaps;
            _lastSessionTime = sessionTime;
            _lastOnPitRoad = onPitRoad;
            _observedPitExit = false;
            ObservedPitStopCount = 0;
        }
    }
}
