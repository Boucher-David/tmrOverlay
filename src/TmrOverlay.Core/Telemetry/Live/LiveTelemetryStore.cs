using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal sealed class LiveTelemetryStore : ILiveTelemetrySource, ILiveTelemetrySink
{
    private const double CloseRadarRangeSeconds = 2d;
    private const double MulticlassWarningRangeSeconds = 5d;
    private const double MinimumClosingRateSecondsPerSecond = 0.15d;

    private readonly object _sync = new();
    private readonly Dictionary<int, ProximityHistory> _proximityHistory = [];
    private HistoricalSessionContext _context = HistoricalSessionContext.Empty;
    private LiveTelemetrySnapshot _snapshot = LiveTelemetrySnapshot.Empty;
    private int? _lastProximityReferenceCarIdx;
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
            _lastProximityReferenceCarIdx = null;
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
            var proximity = LiveProximitySnapshot.From(_context, sample);
            ResetProximityHistoryIfReferenceChanged(sample);
            var multiclassApproaches = BuildMulticlassApproaches(sample, proximity);
            proximity = proximity with
            {
                MulticlassApproaches = multiclassApproaches,
                StrongestMulticlassApproach = multiclassApproaches.MaxBy(approach => approach.Urgency)
            };
            UpdateProximityHistory(sample.CapturedAtUtc, proximity);

            var leaderGap = LiveLeaderGapSnapshot.From(sample);
            var models = LiveRaceModelBuilder.From(_context, sample, fuel, proximity, leaderGap);

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
                LeaderGap: leaderGap)
            {
                Models = models
            };
        }
    }

    private void ResetProximityHistoryIfReferenceChanged(HistoricalTelemetrySample sample)
    {
        var referenceCarIdx = sample.FocusCarIdx ?? sample.PlayerCarIdx;
        if (_lastProximityReferenceCarIdx is not null && _lastProximityReferenceCarIdx != referenceCarIdx)
        {
            _proximityHistory.Clear();
        }

        _lastProximityReferenceCarIdx = referenceCarIdx;
    }

    private IReadOnlyList<LiveMulticlassApproach> BuildMulticlassApproaches(
        HistoricalTelemetrySample sample,
        LiveProximitySnapshot proximity)
    {
        var focusCarClass = FocusCarClass(sample);
        if (focusCarClass is null || proximity.NearbyCars.Count == 0)
        {
            return [];
        }

        var approaches = new List<LiveMulticlassApproach>();
        foreach (var car in proximity.NearbyCars)
        {
            if (car.CarClass is null || car.CarClass == focusCarClass)
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

        if (car.HasReliableRelativeSeconds
            && previous.RelativeSeconds is { } previousSeconds
            && !double.IsNaN(previousSeconds)
            && !double.IsInfinity(previousSeconds))
        {
            var currentSeconds = car.RelativeSeconds!.Value;
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

    private static bool IsBehind(LiveProximityCar car)
    {
        return car.HasReliableRelativeSeconds && car.RelativeSeconds!.Value < 0d;
    }

    private static bool IsInMulticlassWarningRange(LiveProximityCar car)
    {
        return car.HasReliableRelativeSeconds
            && car.RelativeSeconds!.Value < -CloseRadarRangeSeconds
            && car.RelativeSeconds!.Value >= -MulticlassWarningRangeSeconds;
    }

    private static bool IsCloseEnoughForEarlyWarning(LiveProximityCar car)
    {
        return IsInMulticlassWarningRange(car);
    }

    private static double CalculateUrgency(LiveProximityCar car, double? closingRate)
    {
        var seconds = car.HasReliableRelativeSeconds ? car.RelativeSeconds!.Value : MulticlassWarningRangeSeconds;
        var ratio = RangeUrgency(Math.Abs(seconds), CloseRadarRangeSeconds, MulticlassWarningRangeSeconds);

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

    private static int? FocusCarClass(HistoricalTelemetrySample sample)
    {
        return HasExplicitNonPlayerFocus(sample)
            ? sample.FocusCarClass
            : sample.FocusCarClass ?? sample.TeamCarClass;
    }

    private static bool HasExplicitNonPlayerFocus(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is not null
            && sample.PlayerCarIdx is not null
            && sample.FocusCarIdx != sample.PlayerCarIdx;
    }

    private sealed record ProximityHistory(
        DateTimeOffset TimestampUtc,
        double? RelativeSeconds,
        int? CarClass);
}
