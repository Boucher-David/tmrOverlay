namespace TmrOverlay.Core.History;

internal sealed record TelemetryFocusSegmentSnapshot(
    int CarIdx,
    bool IsTeamCar,
    int FrameCount,
    double? StartSessionTimeSeconds,
    double? EndSessionTimeSeconds,
    int TimingFrameCount,
    int ProgressFrameCount,
    int ClassTimingFrameCount);

internal sealed class TelemetryAvailabilitySnapshot
{
    public static TelemetryAvailabilitySnapshot Empty { get; } = new();

    public int SampleFrameCount { get; init; }

    public int LocalDrivingFrameCount { get; init; }

    public int LocalMovingFrameCount { get; init; }

    public int LocalFuelScalarFrameCount { get; init; }

    public int LocalDrivingFuelScalarFrameCount { get; init; }

    public int LocalFuelScalarWithoutDrivingFrameCount { get; init; }

    public int LocalLapProgressFrameCount { get; init; }

    public int LocalScalarIdleFrameCount { get; init; }

    public int FocusCarFrameCount { get; init; }

    public int TeamFocusFrameCount { get; init; }

    public int NonTeamFocusFrameCount { get; init; }

    public int MissingFocusCarFrameCount { get; init; }

    public int FocusCarChangeCount { get; init; }

    public int UniqueFocusCarCount { get; init; }

    public int? CurrentFocusCarIdx { get; init; }

    public IReadOnlyList<TelemetryFocusSegmentSnapshot> FocusSegments { get; init; } = [];

    public int FocusProgressFrameCount { get; init; }

    public int FocusLapDistanceFrameCount { get; init; }

    public int FocusTimingFrameCount { get; init; }

    public int ClassTimingFrameCount { get; init; }

    public int ClassLapDistanceFrameCount { get; init; }

    public int NearbyTimingFrameCount { get; init; }

    public int NearbyLapDistanceFrameCount { get; init; }

    public int CarLeftRightAvailableFrameCount { get; init; }

    public int CarLeftRightActiveFrameCount { get; init; }

    public int CarLeftRightUnavailableFrameCount => Math.Max(0, SampleFrameCount - CarLeftRightAvailableFrameCount);

    public bool HasLocalDriving => LocalDrivingFrameCount > 0;

    public bool HasLocalFuelScalars => LocalFuelScalarFrameCount > 0;

    public bool HasLocalDrivingFuelScalars => LocalDrivingFuelScalarFrameCount > 0;

    public bool LocalFuelScalarsOnlyWhileIdle => LocalFuelScalarFrameCount > 0
        && LocalDrivingFuelScalarFrameCount == 0;

    public bool HasFocusTiming => FocusTimingFrameCount > 0 || ClassTimingFrameCount > 0;

    public bool HasFocusChanges => FocusCarChangeCount > 0;

    public bool HasCarLeftRight => CarLeftRightAvailableFrameCount > 0;

    public bool CarLeftRightUnavailable => SampleFrameCount > 0 && CarLeftRightAvailableFrameCount == 0;

    public bool CarLeftRightAlwaysInactive => CarLeftRightAvailableFrameCount > 0 && CarLeftRightActiveFrameCount == 0;

    public bool LocalScalarsIdle => SampleFrameCount > 0
        && LocalDrivingFrameCount == 0
        && LocalMovingFrameCount == 0
        && LocalLapProgressFrameCount == 0;

    public bool IsSpectatedTimingOnly => SampleFrameCount > 0
        && LocalDrivingFrameCount == 0
        && FocusCarFrameCount > 0
        && HasFocusTiming;
}

internal sealed class TelemetryAvailabilityTracker
{
    private readonly HashSet<int> _uniqueFocusCarIds = [];
    private readonly List<FocusSegmentBuilder> _focusSegments = [];
    private int? _lastFocusCarIdx;
    private FocusSegmentBuilder? _activeFocusSegment;
    private int _sampleFrameCount;
    private int _localDrivingFrameCount;
    private int _localMovingFrameCount;
    private int _localFuelScalarFrameCount;
    private int _localDrivingFuelScalarFrameCount;
    private int _localFuelScalarWithoutDrivingFrameCount;
    private int _localLapProgressFrameCount;
    private int _localScalarIdleFrameCount;
    private int _focusCarFrameCount;
    private int _teamFocusFrameCount;
    private int _nonTeamFocusFrameCount;
    private int _missingFocusCarFrameCount;
    private int _focusCarChangeCount;
    private int _focusProgressFrameCount;
    private int _focusLapDistanceFrameCount;
    private int _focusTimingFrameCount;
    private int _classTimingFrameCount;
    private int _classLapDistanceFrameCount;
    private int _nearbyTimingFrameCount;
    private int _nearbyLapDistanceFrameCount;
    private int _carLeftRightAvailableFrameCount;
    private int _carLeftRightActiveFrameCount;

    public void Reset()
    {
        _uniqueFocusCarIds.Clear();
        _focusSegments.Clear();
        _lastFocusCarIdx = null;
        _activeFocusSegment = null;
        _sampleFrameCount = 0;
        _localDrivingFrameCount = 0;
        _localMovingFrameCount = 0;
        _localFuelScalarFrameCount = 0;
        _localDrivingFuelScalarFrameCount = 0;
        _localFuelScalarWithoutDrivingFrameCount = 0;
        _localLapProgressFrameCount = 0;
        _localScalarIdleFrameCount = 0;
        _focusCarFrameCount = 0;
        _teamFocusFrameCount = 0;
        _nonTeamFocusFrameCount = 0;
        _missingFocusCarFrameCount = 0;
        _focusCarChangeCount = 0;
        _focusProgressFrameCount = 0;
        _focusLapDistanceFrameCount = 0;
        _focusTimingFrameCount = 0;
        _classTimingFrameCount = 0;
        _classLapDistanceFrameCount = 0;
        _nearbyTimingFrameCount = 0;
        _nearbyLapDistanceFrameCount = 0;
        _carLeftRightAvailableFrameCount = 0;
        _carLeftRightActiveFrameCount = 0;
    }

    public void RecordFrame(HistoricalTelemetrySample sample)
    {
        _sampleFrameCount++;

        var hasLocalDriving = sample.IsOnTrack && !sample.IsInGarage;
        var hasLocalMoving = sample.SpeedMetersPerSecond > 1d;
        var hasLocalFuel = IsValidFuel(sample.FuelLevelLiters);
        var hasFocusProgress = HasLapProgress(sample.FocusLapCompleted, sample.FocusLapDistPct);
        var hasFocusLapDistance = HasLapDistancePct(sample.FocusLapDistPct);
        var hasFocusTiming = HasFocusTiming(sample);
        var hasClassTiming = sample.ClassCars?.Any(HasTimingOrStanding) == true;
        var hasClassLapDistance = sample.ClassCars?.Any(car => HasLapDistancePct(car.LapDistPct)) == true;
        var hasNearbyTiming = sample.NearbyCars?.Any(HasTimingOrStanding) == true;
        var hasNearbyLapDistance = sample.NearbyCars?.Any(car => HasLapDistancePct(car.LapDistPct)) == true;

        if (hasLocalDriving)
        {
            _localDrivingFrameCount++;
        }

        if (hasLocalMoving)
        {
            _localMovingFrameCount++;
        }

        if (hasLocalFuel)
        {
            _localFuelScalarFrameCount++;
            if (hasLocalDriving)
            {
                _localDrivingFuelScalarFrameCount++;
            }
            else
            {
                _localFuelScalarWithoutDrivingFrameCount++;
            }
        }

        if (!hasLocalDriving && !hasLocalMoving && !hasLocalFuel)
        {
            _localScalarIdleFrameCount++;
        }

        if (HasLapProgress(sample.LapCompleted, sample.LapDistPct) && sample.IsOnTrack)
        {
            _localLapProgressFrameCount++;
        }

        if (sample.CarLeftRight is { } carLeftRight)
        {
            _carLeftRightAvailableFrameCount++;
            if (carLeftRight > 1)
            {
                _carLeftRightActiveFrameCount++;
            }
        }

        if (sample.FocusCarIdx is { } focusCarIdx && focusCarIdx >= 0)
        {
            var isTeamFocus = IsTeamFocus(sample, focusCarIdx);
            _focusCarFrameCount++;
            _uniqueFocusCarIds.Add(focusCarIdx);
            if (_lastFocusCarIdx is { } previousFocusCarIdx && previousFocusCarIdx != focusCarIdx)
            {
                _focusCarChangeCount++;
            }

            _lastFocusCarIdx = focusCarIdx;
            if (isTeamFocus)
            {
                _teamFocusFrameCount++;
            }
            else
            {
                _nonTeamFocusFrameCount++;
            }

            RecordFocusSegment(sample, focusCarIdx, isTeamFocus, hasFocusTiming, hasFocusProgress, hasClassTiming);
        }
        else
        {
            _missingFocusCarFrameCount++;
            _activeFocusSegment = null;
        }

        if (hasFocusProgress)
        {
            _focusProgressFrameCount++;
        }

        if (hasFocusLapDistance)
        {
            _focusLapDistanceFrameCount++;
        }

        if (hasFocusTiming)
        {
            _focusTimingFrameCount++;
        }

        if (hasClassTiming)
        {
            _classTimingFrameCount++;
        }

        if (hasClassLapDistance)
        {
            _classLapDistanceFrameCount++;
        }

        if (hasNearbyTiming)
        {
            _nearbyTimingFrameCount++;
        }

        if (hasNearbyLapDistance)
        {
            _nearbyLapDistanceFrameCount++;
        }
    }

    public TelemetryAvailabilitySnapshot Snapshot()
    {
        return new TelemetryAvailabilitySnapshot
        {
            SampleFrameCount = _sampleFrameCount,
            LocalDrivingFrameCount = _localDrivingFrameCount,
            LocalMovingFrameCount = _localMovingFrameCount,
            LocalFuelScalarFrameCount = _localFuelScalarFrameCount,
            LocalDrivingFuelScalarFrameCount = _localDrivingFuelScalarFrameCount,
            LocalFuelScalarWithoutDrivingFrameCount = _localFuelScalarWithoutDrivingFrameCount,
            LocalLapProgressFrameCount = _localLapProgressFrameCount,
            LocalScalarIdleFrameCount = _localScalarIdleFrameCount,
            FocusCarFrameCount = _focusCarFrameCount,
            TeamFocusFrameCount = _teamFocusFrameCount,
            NonTeamFocusFrameCount = _nonTeamFocusFrameCount,
            MissingFocusCarFrameCount = _missingFocusCarFrameCount,
            FocusCarChangeCount = _focusCarChangeCount,
            UniqueFocusCarCount = _uniqueFocusCarIds.Count,
            CurrentFocusCarIdx = _lastFocusCarIdx,
            FocusSegments = _focusSegments.Select(segment => segment.ToSnapshot()).ToArray(),
            FocusProgressFrameCount = _focusProgressFrameCount,
            FocusLapDistanceFrameCount = _focusLapDistanceFrameCount,
            FocusTimingFrameCount = _focusTimingFrameCount,
            ClassTimingFrameCount = _classTimingFrameCount,
            ClassLapDistanceFrameCount = _classLapDistanceFrameCount,
            NearbyTimingFrameCount = _nearbyTimingFrameCount,
            NearbyLapDistanceFrameCount = _nearbyLapDistanceFrameCount,
            CarLeftRightAvailableFrameCount = _carLeftRightAvailableFrameCount,
            CarLeftRightActiveFrameCount = _carLeftRightActiveFrameCount
        };
    }

    private void RecordFocusSegment(
        HistoricalTelemetrySample sample,
        int focusCarIdx,
        bool isTeamFocus,
        bool hasFocusTiming,
        bool hasFocusProgress,
        bool hasClassTiming)
    {
        if (_activeFocusSegment is null || !_activeFocusSegment.Matches(focusCarIdx, isTeamFocus))
        {
            _activeFocusSegment = new FocusSegmentBuilder(focusCarIdx, isTeamFocus);
            _focusSegments.Add(_activeFocusSegment);
        }

        _activeFocusSegment.Record(sample, hasFocusTiming, hasFocusProgress, hasClassTiming);
    }

    private static bool IsTeamFocus(HistoricalTelemetrySample sample, int focusCarIdx)
    {
        return sample.PlayerCarIdx is { } playerCarIdx && playerCarIdx == focusCarIdx;
    }

    private static bool HasFocusTiming(HistoricalTelemetrySample sample)
    {
        return IsTimingValue(sample.FocusF2TimeSeconds)
            || IsTimingValue(sample.FocusEstimatedTimeSeconds)
            || IsTimingValue(sample.FocusLastLapTimeSeconds)
            || IsTimingValue(sample.FocusBestLapTimeSeconds)
            || sample.FocusPosition is > 0
            || sample.FocusClassPosition is > 0;
    }

    private static bool HasTimingOrStanding(HistoricalCarProximity car)
    {
        return IsTimingValue(car.F2TimeSeconds)
            || IsTimingValue(car.EstimatedTimeSeconds)
            || car.Position is > 0
            || car.ClassPosition is > 0;
    }

    private static bool HasLapProgress(int? lapCompleted, double? lapDistPct)
    {
        return lapCompleted is >= 0
            && HasLapDistancePct(lapDistPct);
    }

    private static bool HasLapDistancePct(double? lapDistPct)
    {
        return lapDistPct is { } pct
            && IsFinite(pct)
            && pct >= 0d;
    }

    private static bool IsValidFuel(double fuelLevelLiters)
    {
        return IsFinite(fuelLevelLiters) && fuelLevelLiters > 0d;
    }

    private static bool IsTimingValue(double? value)
    {
        return value is { } timing
            && IsFinite(timing)
            && timing >= 0d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private sealed class FocusSegmentBuilder
    {
        private readonly int _carIdx;
        private readonly bool _isTeamCar;
        private int _frameCount;
        private double? _startSessionTimeSeconds;
        private double? _endSessionTimeSeconds;
        private int _timingFrameCount;
        private int _progressFrameCount;
        private int _classTimingFrameCount;

        public FocusSegmentBuilder(int carIdx, bool isTeamCar)
        {
            _carIdx = carIdx;
            _isTeamCar = isTeamCar;
        }

        public bool Matches(int carIdx, bool isTeamCar)
        {
            return _carIdx == carIdx && _isTeamCar == isTeamCar;
        }

        public void Record(
            HistoricalTelemetrySample sample,
            bool hasFocusTiming,
            bool hasFocusProgress,
            bool hasClassTiming)
        {
            _frameCount++;
            if (IsFinite(sample.SessionTime))
            {
                _startSessionTimeSeconds ??= sample.SessionTime;
                _endSessionTimeSeconds = sample.SessionTime;
            }

            if (hasFocusTiming)
            {
                _timingFrameCount++;
            }

            if (hasFocusProgress)
            {
                _progressFrameCount++;
            }

            if (hasClassTiming)
            {
                _classTimingFrameCount++;
            }
        }

        public TelemetryFocusSegmentSnapshot ToSnapshot()
        {
            return new TelemetryFocusSegmentSnapshot(
                CarIdx: _carIdx,
                IsTeamCar: _isTeamCar,
                FrameCount: _frameCount,
                StartSessionTimeSeconds: _startSessionTimeSeconds,
                EndSessionTimeSeconds: _endSessionTimeSeconds,
                TimingFrameCount: _timingFrameCount,
                ProgressFrameCount: _progressFrameCount,
                ClassTimingFrameCount: _classTimingFrameCount);
        }
    }
}
