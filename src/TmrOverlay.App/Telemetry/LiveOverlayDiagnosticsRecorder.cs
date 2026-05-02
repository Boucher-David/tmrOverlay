using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Telemetry;

internal sealed class LiveOverlayDiagnosticsRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LiveOverlayDiagnosticsOptions _options;
    private readonly AppStorageOptions _storageOptions;
    private readonly AppEventRecorder _events;
    private readonly ILogger<LiveOverlayDiagnosticsRecorder> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<string, int> _sessionFrameCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _gapClassSourceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _gapClassEvidenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _gapOverallEvidenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _radarFocusFrameCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _radarPlacementEvidenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _fuelLevelEvidenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _fuelBurnEvidenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _fuelMeasuredEvidenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _fuelBaselineEvidenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _eventSampleCountsByKind = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _eventSampleKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, PositionState> _positionStates = [];
    private readonly List<LiveOverlayDiagnosticsFrameSample> _sampleFrames = [];
    private readonly List<LiveOverlayDiagnosticsEventSample> _eventSamples = [];
    private string? _sourceId;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _lastSampledFrameAtUtc;
    private string? _lastArtifactPath;
    private int _frameCount;
    private int _sampledFrameCount;
    private int _droppedFrameSampleCount;
    private int _droppedEventSampleCount;
    private int _gapFramesWithData;
    private int _gapNonRaceFramesWithData;
    private int _gapClassLargeFrames;
    private int _gapClassLapEquivalentFrames;
    private int _gapClassJumpFrames;
    private int _gapPitContextFrames;
    private int _gapClassRowFrames;
    private int _maxGapClassRows;
    private double? _maxClassGapSeconds;
    private double? _maxClassGapLaps;
    private double? _previousClassGapSeconds;
    private DateTimeOffset? _previousClassGapAtUtc;
    private int _radarFramesWithData;
    private int _radarNonPlayerFocusFrames;
    private int _radarSideSignalFrames;
    private int _radarRawSideSuppressedForFocusFrames;
    private int _radarSideSignalWithoutPlacementFrames;
    private int _radarTimingOnlyRowFrames;
    private int _radarSpatialRowFrames;
    private int _radarMulticlassApproachFrames;
    private int _maxRadarNearbyCars;
    private int _maxRadarTimingRows;
    private int _maxRadarSpatialCars;
    private int _fuelFramesWithLevel;
    private int _fuelFramesWithInstantaneousBurn;
    private int _fuelInstantaneousBurnWithoutLevelFrames;
    private int _fuelTeamContextWithoutLevelFrames;
    private int _fuelPitContextFrames;
    private int _fuelDriverControlFrames;
    private int? _lastDriversSoFar;
    private int _positionObservedFrames;
    private int _positionOverallChanges;
    private int _positionClassChanges;
    private int _positionIntraLapOverallChanges;
    private int _positionIntraLapClassChanges;

    public LiveOverlayDiagnosticsRecorder(
        LiveOverlayDiagnosticsOptions options,
        AppStorageOptions storageOptions,
        AppEventRecorder events,
        ILogger<LiveOverlayDiagnosticsRecorder> logger)
    {
        _options = options;
        _storageOptions = storageOptions;
        _events = events;
        _logger = logger;
    }

    public string DiagnosticsLogRoot => Path.Combine(_storageOptions.LogsRoot, _options.LogDirectoryName);

    public string? LastArtifactPath
    {
        get
        {
            lock (_sync)
            {
                return _lastArtifactPath;
            }
        }
    }

    public void StartCollection(string sourceId, DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _sourceId = sourceId;
            _startedAtUtc = startedAtUtc;
            _lastSampledFrameAtUtc = null;
            _lastArtifactPath = null;
            _frameCount = 0;
            _sampledFrameCount = 0;
            _droppedFrameSampleCount = 0;
            _droppedEventSampleCount = 0;
            _gapFramesWithData = 0;
            _gapNonRaceFramesWithData = 0;
            _gapClassLargeFrames = 0;
            _gapClassLapEquivalentFrames = 0;
            _gapClassJumpFrames = 0;
            _gapPitContextFrames = 0;
            _gapClassRowFrames = 0;
            _maxGapClassRows = 0;
            _maxClassGapSeconds = null;
            _maxClassGapLaps = null;
            _previousClassGapSeconds = null;
            _previousClassGapAtUtc = null;
            _radarFramesWithData = 0;
            _radarNonPlayerFocusFrames = 0;
            _radarSideSignalFrames = 0;
            _radarRawSideSuppressedForFocusFrames = 0;
            _radarSideSignalWithoutPlacementFrames = 0;
            _radarTimingOnlyRowFrames = 0;
            _radarSpatialRowFrames = 0;
            _radarMulticlassApproachFrames = 0;
            _maxRadarNearbyCars = 0;
            _maxRadarTimingRows = 0;
            _maxRadarSpatialCars = 0;
            _fuelFramesWithLevel = 0;
            _fuelFramesWithInstantaneousBurn = 0;
            _fuelInstantaneousBurnWithoutLevelFrames = 0;
            _fuelTeamContextWithoutLevelFrames = 0;
            _fuelPitContextFrames = 0;
            _fuelDriverControlFrames = 0;
            _lastDriversSoFar = null;
            _positionObservedFrames = 0;
            _positionOverallChanges = 0;
            _positionClassChanges = 0;
            _positionIntraLapOverallChanges = 0;
            _positionIntraLapClassChanges = 0;
            _sessionFrameCounts.Clear();
            _gapClassSourceCounts.Clear();
            _gapClassEvidenceCounts.Clear();
            _gapOverallEvidenceCounts.Clear();
            _radarFocusFrameCounts.Clear();
            _radarPlacementEvidenceCounts.Clear();
            _fuelLevelEvidenceCounts.Clear();
            _fuelBurnEvidenceCounts.Clear();
            _fuelMeasuredEvidenceCounts.Clear();
            _fuelBaselineEvidenceCounts.Clear();
            _eventSampleCountsByKind.Clear();
            _eventSampleKeys.Clear();
            _positionStates.Clear();
            _sampleFrames.Clear();
            _eventSamples.Clear();
        }
    }

    public void RecordFrame(LiveTelemetrySnapshot snapshot)
    {
        if (!_options.Enabled)
        {
            return;
        }

        lock (_sync)
        {
            if (_sourceId is null)
            {
                return;
            }

            var capturedAtUtc = snapshot.LastUpdatedAtUtc
                ?? snapshot.LatestSample?.CapturedAtUtc
                ?? DateTimeOffset.UtcNow;
            _frameCount++;
            RecordSession(snapshot);
            RecordGap(snapshot, capturedAtUtc);
            RecordRadar(snapshot, capturedAtUtc);
            RecordFuel(snapshot, capturedAtUtc);
            RecordPositionCadence(snapshot, capturedAtUtc);
            RecordSampleFrame(snapshot, capturedAtUtc);
        }
    }

    public string? CompleteCollection(DateTimeOffset finishedAtUtc, string? captureDirectory)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        lock (_sync)
        {
            if (_sourceId is null || _startedAtUtc is null)
            {
                return null;
            }

            try
            {
                var artifact = new LiveOverlayDiagnosticsArtifact(
                    FormatVersion: 1,
                    SourceId: _sourceId,
                    StartedAtUtc: _startedAtUtc.Value,
                    FinishedAtUtc: finishedAtUtc,
                    Options: new LiveOverlayDiagnosticsArtifactOptions(
                        MinimumFrameSpacingSeconds: _options.MinimumFrameSpacingSeconds,
                        MaxSampleFramesPerSession: _options.MaxSampleFramesPerSession,
                        MaxEventExamplesPerSession: _options.MaxEventExamplesPerSession,
                        MaxEventExamplesPerKind: _options.MaxEventExamplesPerKind,
                        LargeGapSeconds: _options.LargeGapSeconds,
                        LargeGapLapEquivalent: _options.LargeGapLapEquivalent,
                        GapJumpSeconds: _options.GapJumpSeconds),
                    Totals: new LiveOverlayDiagnosticsTotals(
                        FrameCount: _frameCount,
                        SampledFrameCount: _sampledFrameCount,
                        DroppedFrameSampleCount: _droppedFrameSampleCount,
                        DroppedEventSampleCount: _droppedEventSampleCount,
                        SessionFrameCounts: Sorted(_sessionFrameCounts)),
                    Gap: new GapOverlayDiagnosticsSummary(
                        FramesWithData: _gapFramesWithData,
                        NonRaceFramesWithData: _gapNonRaceFramesWithData,
                        ClassLargeGapFrames: _gapClassLargeFrames,
                        ClassLapEquivalentFrames: _gapClassLapEquivalentFrames,
                        ClassJumpFrames: _gapClassJumpFrames,
                        PitContextFrames: _gapPitContextFrames,
                        ClassRowFrames: _gapClassRowFrames,
                        MaxClassRows: _maxGapClassRows,
                        MaxClassGapSeconds: Round(_maxClassGapSeconds),
                        MaxClassGapLaps: Round(_maxClassGapLaps),
                        ClassGapSourceCounts: Sorted(_gapClassSourceCounts),
                        ClassGapEvidenceCounts: Sorted(_gapClassEvidenceCounts),
                        OverallGapEvidenceCounts: Sorted(_gapOverallEvidenceCounts)),
                    Radar: new RadarOverlayDiagnosticsSummary(
                        FramesWithData: _radarFramesWithData,
                        NonPlayerFocusFrames: _radarNonPlayerFocusFrames,
                        SideSignalFrames: _radarSideSignalFrames,
                        RawSideSuppressedForFocusFrames: _radarRawSideSuppressedForFocusFrames,
                        SideSignalWithoutPlacementFrames: _radarSideSignalWithoutPlacementFrames,
                        TimingOnlyRowFrames: _radarTimingOnlyRowFrames,
                        SpatialRowFrames: _radarSpatialRowFrames,
                        MulticlassApproachFrames: _radarMulticlassApproachFrames,
                        MaxNearbyCars: _maxRadarNearbyCars,
                        MaxTimingRows: _maxRadarTimingRows,
                        MaxSpatialCars: _maxRadarSpatialCars,
                        FocusFrameCounts: Sorted(_radarFocusFrameCounts),
                        PlacementEvidenceCounts: Sorted(_radarPlacementEvidenceCounts)),
                    Fuel: new FuelOverlayDiagnosticsSummary(
                        FramesWithFuelLevel: _fuelFramesWithLevel,
                        FramesWithInstantaneousBurn: _fuelFramesWithInstantaneousBurn,
                        InstantaneousBurnWithoutFuelLevelFrames: _fuelInstantaneousBurnWithoutLevelFrames,
                        TeamContextWithoutFuelLevelFrames: _fuelTeamContextWithoutLevelFrames,
                        PitContextFrames: _fuelPitContextFrames,
                        DriverControlFrames: _fuelDriverControlFrames,
                        FuelLevelEvidenceCounts: Sorted(_fuelLevelEvidenceCounts),
                        InstantaneousBurnEvidenceCounts: Sorted(_fuelBurnEvidenceCounts),
                        MeasuredBurnEvidenceCounts: Sorted(_fuelMeasuredEvidenceCounts),
                        BaselineEligibilityEvidenceCounts: Sorted(_fuelBaselineEvidenceCounts)),
                    PositionCadence: new PositionCadenceDiagnosticsSummary(
                        ObservedFrames: _positionObservedFrames,
                        OverallPositionChanges: _positionOverallChanges,
                        ClassPositionChanges: _positionClassChanges,
                        IntraLapOverallPositionChanges: _positionIntraLapOverallChanges,
                        IntraLapClassPositionChanges: _positionIntraLapClassChanges,
                        TrackedCarCount: _positionStates.Count),
                    SampleFrames: _sampleFrames.ToArray(),
                    EventSamples: _eventSamples.ToArray());

                var path = ResolveArtifactPath(captureDirectory, _sourceId);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(artifact, JsonOptions), Encoding.UTF8);
                _lastArtifactPath = path;
                _events.Record("live_overlay_diagnostics_saved", new Dictionary<string, string?>
                {
                    ["sourceId"] = _sourceId,
                    ["artifactPath"] = path,
                    ["frameCount"] = _frameCount.ToString(),
                    ["eventSampleCount"] = _eventSamples.Count.ToString()
                });
                _logger.LogInformation(
                    "Saved live overlay diagnostics artifact {ArtifactPath} with {FrameCount} frames and {EventSampleCount} event samples.",
                    path,
                    _frameCount,
                    _eventSamples.Count);
                return path;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to save live overlay diagnostics artifact for {SourceId}.", _sourceId);
                return null;
            }
        }
    }

    private void RecordSession(LiveTelemetrySnapshot snapshot)
    {
        Increment(_sessionFrameCounts, SessionKind(snapshot));
    }

    private void RecordGap(LiveTelemetrySnapshot snapshot, DateTimeOffset capturedAtUtc)
    {
        var sessionKind = SessionKind(snapshot);
        var classGap = snapshot.LeaderGap.ClassLeaderGap;
        var overallGap = snapshot.LeaderGap.OverallLeaderGap;
        Increment(_gapClassSourceCounts, classGap.Source);
        Increment(_gapClassEvidenceCounts, EvidenceKey(snapshot.Models.Timing.ClassLeaderGapEvidence));
        Increment(_gapOverallEvidenceCounts, EvidenceKey(snapshot.Models.Timing.OverallLeaderGapEvidence));

        if (snapshot.LeaderGap.HasData)
        {
            _gapFramesWithData++;
            if (!IsRaceSession(sessionKind))
            {
                _gapNonRaceFramesWithData++;
                AddEvent(
                    "gap.non-race-data",
                    $"gap data present during {sessionKind}",
                    snapshot,
                    capturedAtUtc);
            }
        }

        if (IsPitContext(snapshot))
        {
            _gapPitContextFrames++;
        }

        if (snapshot.LeaderGap.ClassCars.Count > 0)
        {
            _gapClassRowFrames++;
            _maxGapClassRows = Math.Max(_maxGapClassRows, snapshot.LeaderGap.ClassCars.Count);
        }

        if (classGap.Seconds is { } seconds && IsFinite(seconds))
        {
            _maxClassGapSeconds = Max(_maxClassGapSeconds, seconds);
            if (seconds >= _options.LargeGapSeconds)
            {
                _gapClassLargeFrames++;
                AddEvent(
                    "gap.large-seconds",
                    $"class gap {seconds:0.###}s",
                    snapshot,
                    capturedAtUtc);
            }

            var estimatedLapSeconds = EstimatedLapSeconds(snapshot);
            if (estimatedLapSeconds is { } lapSeconds
                && lapSeconds > 0d
                && seconds >= lapSeconds * _options.LargeGapLapEquivalent)
            {
                _gapClassLapEquivalentFrames++;
            }

            if (_previousClassGapSeconds is { } previousSeconds
                && Math.Abs(seconds - previousSeconds) >= _options.GapJumpSeconds)
            {
                _gapClassJumpFrames++;
                AddEvent(
                    "gap.large-jump",
                    $"class gap changed from {previousSeconds:0.###}s to {seconds:0.###}s",
                    snapshot,
                    capturedAtUtc);
            }

            _previousClassGapSeconds = seconds;
            _previousClassGapAtUtc = capturedAtUtc;
        }
        else if (classGap.Laps is { } laps && IsFinite(laps))
        {
            _maxClassGapLaps = Max(_maxClassGapLaps, laps);
            if (laps >= _options.LargeGapLapEquivalent)
            {
                _gapClassLapEquivalentFrames++;
                AddEvent(
                    "gap.large-laps",
                    $"class gap {laps:0.###} laps",
                    snapshot,
                    capturedAtUtc);
            }
        }

        if (!classGap.HasData && !snapshot.Models.Timing.ClassLeaderGapEvidence.IsUsable)
        {
            AddEvent(
                "gap.class-unavailable",
                snapshot.Models.Timing.ClassLeaderGapEvidence.MissingReason ?? "class gap unavailable",
                snapshot,
                capturedAtUtc);
        }

        if (!overallGap.HasData && !snapshot.Models.Timing.OverallLeaderGapEvidence.IsUsable)
        {
            AddEvent(
                "gap.overall-unavailable",
                snapshot.Models.Timing.OverallLeaderGapEvidence.MissingReason ?? "overall gap unavailable",
                snapshot,
                capturedAtUtc);
        }
    }

    private void RecordRadar(LiveTelemetrySnapshot snapshot, DateTimeOffset capturedAtUtc)
    {
        var playerCarIdx = snapshot.Models.DriverDirectory.PlayerCarIdx ?? snapshot.LatestSample?.PlayerCarIdx;
        var focusCarIdx = snapshot.Models.DriverDirectory.FocusCarIdx
            ?? snapshot.LatestSample?.FocusCarIdx
            ?? playerCarIdx;
        var focusKind = FocusKind(playerCarIdx, focusCarIdx);
        Increment(_radarFocusFrameCounts, focusKind);

        if (string.Equals(focusKind, "non-player", StringComparison.OrdinalIgnoreCase))
        {
            _radarNonPlayerFocusFrames++;
            if (snapshot.LatestSample?.CarLeftRight is not null)
            {
                _radarRawSideSuppressedForFocusFrames++;
                AddEvent(
                    "radar.side-suppressed-focus",
                    "CarLeftRight is player-scoped while focus is another car",
                    snapshot,
                    capturedAtUtc);
            }
        }

        if (snapshot.Proximity.HasData)
        {
            _radarFramesWithData++;
        }

        if (snapshot.Proximity.CarLeftRight is not null)
        {
            _radarSideSignalFrames++;
        }

        var timingRows = snapshot.Models.Timing.OverallRows;
        var timingOnlyRows = timingRows.Count(row => row.HasTiming && !row.CanUseForRadarPlacement);
        var spatialRows = timingRows.Count(row => row.CanUseForRadarPlacement);
        if (timingOnlyRows > 0)
        {
            _radarTimingOnlyRowFrames++;
        }

        if (spatialRows > 0 || snapshot.Models.Spatial.Cars.Count > 0)
        {
            _radarSpatialRowFrames++;
        }

        foreach (var row in timingRows)
        {
            Increment(_radarPlacementEvidenceCounts, EvidenceKey(row.RadarPlacementEvidence));
        }

        if (snapshot.Proximity.MulticlassApproaches.Count > 0)
        {
            _radarMulticlassApproachFrames++;
        }

        _maxRadarNearbyCars = Math.Max(_maxRadarNearbyCars, snapshot.Proximity.NearbyCars.Count);
        _maxRadarTimingRows = Math.Max(_maxRadarTimingRows, timingRows.Count);
        _maxRadarSpatialCars = Math.Max(_maxRadarSpatialCars, snapshot.Models.Spatial.Cars.Count);

        if ((snapshot.Proximity.HasCarLeft || snapshot.Proximity.HasCarRight) && !HasSidePlacementCandidate(snapshot))
        {
            _radarSideSignalWithoutPlacementFrames++;
            AddEvent(
                "radar.side-without-placement",
                "side occupancy has no nearby placement candidate inside the side-overlap window",
                snapshot,
                capturedAtUtc);
        }
    }

    private void RecordFuel(LiveTelemetrySnapshot snapshot, DateTimeOffset capturedAtUtc)
    {
        var fuelPit = snapshot.Models.FuelPit;
        Increment(_fuelLevelEvidenceCounts, EvidenceKey(fuelPit.FuelLevelEvidence));
        Increment(_fuelBurnEvidenceCounts, EvidenceKey(fuelPit.InstantaneousBurnEvidence));
        Increment(_fuelMeasuredEvidenceCounts, EvidenceKey(fuelPit.MeasuredBurnEvidence));
        Increment(_fuelBaselineEvidenceCounts, EvidenceKey(fuelPit.BaselineEligibilityEvidence));

        var hasFuelLevel = snapshot.Fuel.HasValidFuel || IsPositiveFinite(snapshot.LatestSample?.FuelLevelLiters);
        var hasInstantaneousBurn = IsPositiveFinite(snapshot.LatestSample?.FuelUsePerHourKg)
            || IsPositiveFinite(snapshot.Fuel.FuelUsePerHourKg)
            || IsPositiveFinite(snapshot.Fuel.FuelUsePerHourLiters);

        if (hasFuelLevel)
        {
            _fuelFramesWithLevel++;
        }

        if (hasInstantaneousBurn)
        {
            _fuelFramesWithInstantaneousBurn++;
        }

        if (hasInstantaneousBurn && !hasFuelLevel)
        {
            _fuelInstantaneousBurnWithoutLevelFrames++;
            AddEvent(
                "fuel.instantaneous-without-level",
                "FuelUsePerHour is present while FuelLevel is not usable",
                snapshot,
                capturedAtUtc);
        }

        if (!hasFuelLevel && HasTeamTimingContext(snapshot.LatestSample))
        {
            _fuelTeamContextWithoutLevelFrames++;
        }

        if (IsPitContext(snapshot))
        {
            _fuelPitContextFrames++;
        }

        if (snapshot.LatestSample?.DriversSoFar is { } driversSoFar)
        {
            _fuelDriverControlFrames++;
            if (_lastDriversSoFar is { } previousDriversSoFar && previousDriversSoFar != driversSoFar)
            {
                AddEvent(
                    "fuel.driver-control-change",
                    $"DCDriversSoFar changed from {previousDriversSoFar} to {driversSoFar}",
                    snapshot,
                    capturedAtUtc);
            }

            _lastDriversSoFar = driversSoFar;
        }
    }

    private void RecordPositionCadence(LiveTelemetrySnapshot snapshot, DateTimeOffset capturedAtUtc)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        var observations = PositionObservations(sample).ToArray();
        if (observations.Length == 0)
        {
            return;
        }

        _positionObservedFrames++;
        foreach (var observation in observations)
        {
            if (_positionStates.TryGetValue(observation.CarIdx, out var previous))
            {
                RecordPositionChange(snapshot, capturedAtUtc, previous, observation);
            }

            _positionStates[observation.CarIdx] = new PositionState(
                observation.CarIdx,
                observation.OverallPosition,
                observation.ClassPosition,
                observation.LapCompleted,
                observation.LapDistPct,
                capturedAtUtc,
                sample.SessionTime);
        }
    }

    private void RecordPositionChange(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        PositionState previous,
        PositionObservation current)
    {
        var sameLap = previous.LapCompleted == current.LapCompleted;
        var lapDelta = previous.LapDistPct is { } previousLapDistPct
            && current.LapDistPct is { } currentLapDistPct
            ? Math.Abs(currentLapDistPct - previousLapDistPct)
            : (double?)null;
        var progressedWithinLap = sameLap
            && lapDelta is > 0.00001d and < 0.5d;

        if (previous.OverallPosition is { } previousOverall
            && current.OverallPosition is { } currentOverall
            && previousOverall != currentOverall)
        {
            _positionOverallChanges++;
            if (progressedWithinLap)
            {
                _positionIntraLapOverallChanges++;
                AddEvent(
                    "position.overall-intra-lap",
                    $"car {current.CarIdx} overall P{previousOverall} -> P{currentOverall} on lap {current.LapCompleted}",
                    snapshot,
                    capturedAtUtc);
            }
        }

        if (previous.ClassPosition is { } previousClass
            && current.ClassPosition is { } currentClass
            && previousClass != currentClass)
        {
            _positionClassChanges++;
            if (progressedWithinLap)
            {
                _positionIntraLapClassChanges++;
                AddEvent(
                    "position.class-intra-lap",
                    $"car {current.CarIdx} class P{previousClass} -> P{currentClass} on lap {current.LapCompleted}",
                    snapshot,
                    capturedAtUtc);
            }
        }
    }

    private void RecordSampleFrame(LiveTelemetrySnapshot snapshot, DateTimeOffset capturedAtUtc)
    {
        if (_lastSampledFrameAtUtc is not null
            && (capturedAtUtc - _lastSampledFrameAtUtc.Value).TotalSeconds < _options.MinimumFrameSpacingSeconds)
        {
            return;
        }

        if (_sampleFrames.Count >= _options.MaxSampleFramesPerSession)
        {
            _droppedFrameSampleCount++;
            return;
        }

        var playerCarIdx = snapshot.Models.DriverDirectory.PlayerCarIdx ?? snapshot.LatestSample?.PlayerCarIdx;
        var focusCarIdx = snapshot.Models.DriverDirectory.FocusCarIdx
            ?? snapshot.LatestSample?.FocusCarIdx
            ?? playerCarIdx;
        _sampleFrames.Add(new LiveOverlayDiagnosticsFrameSample(
            CapturedAtUtc: capturedAtUtc,
            Sequence: snapshot.Sequence,
            SessionTimeSeconds: Round(snapshot.LatestSample?.SessionTime),
            SessionKind: SessionKind(snapshot),
            PlayerCarIdx: playerCarIdx,
            FocusCarIdx: focusCarIdx,
            FocusKind: FocusKind(playerCarIdx, focusCarIdx),
            ClassGapSeconds: Round(snapshot.LeaderGap.ClassLeaderGap.Seconds),
            ClassGapLaps: Round(snapshot.LeaderGap.ClassLeaderGap.Laps),
            ClassGapSource: snapshot.LeaderGap.ClassLeaderGap.Source,
            ClassRowCount: snapshot.LeaderGap.ClassCars.Count,
            NearbyCarCount: snapshot.Proximity.NearbyCars.Count,
            SpatialCarCount: snapshot.Models.Spatial.Cars.Count,
            TimingRowCount: snapshot.Models.Timing.OverallRows.Count,
            HasSideSignal: snapshot.Proximity.CarLeftRight is not null,
            HasFuelLevel: snapshot.Fuel.HasValidFuel,
            HasFuelBurn: IsPositiveFinite(snapshot.Fuel.FuelUsePerHourKg) || IsPositiveFinite(snapshot.Fuel.FuelUsePerHourLiters),
            FuelLevelEvidence: EvidenceKey(snapshot.Models.FuelPit.FuelLevelEvidence),
            FuelBurnEvidence: EvidenceKey(snapshot.Models.FuelPit.InstantaneousBurnEvidence)));
        _sampledFrameCount++;
        _lastSampledFrameAtUtc = capturedAtUtc;
    }

    private void AddEvent(
        string kind,
        string detail,
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset capturedAtUtc)
    {
        var normalizedKind = string.IsNullOrWhiteSpace(kind) ? "unknown" : kind.Trim();
        var normalizedDetail = string.IsNullOrWhiteSpace(detail) ? normalizedKind : detail.Trim();
        var playerCarIdx = snapshot.Models.DriverDirectory.PlayerCarIdx ?? snapshot.LatestSample?.PlayerCarIdx;
        var focusCarIdx = snapshot.Models.DriverDirectory.FocusCarIdx
            ?? snapshot.LatestSample?.FocusCarIdx
            ?? playerCarIdx;
        var sessionKind = SessionKind(snapshot);
        var eventKey = string.Join(
            "\u001f",
            normalizedKind,
            normalizedDetail,
            sessionKind,
            playerCarIdx?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            focusCarIdx?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
        if (_eventSampleKeys.Contains(eventKey))
        {
            _droppedEventSampleCount++;
            return;
        }

        if (_eventSamples.Count >= _options.MaxEventExamplesPerSession)
        {
            _droppedEventSampleCount++;
            return;
        }

        _eventSampleCountsByKind.TryGetValue(normalizedKind, out var kindCount);
        if (kindCount >= _options.MaxEventExamplesPerKind)
        {
            _droppedEventSampleCount++;
            return;
        }

        _eventSampleKeys.Add(eventKey);
        _eventSampleCountsByKind[normalizedKind] = kindCount + 1;
        _eventSamples.Add(new LiveOverlayDiagnosticsEventSample(
            Kind: normalizedKind,
            Detail: normalizedDetail,
            CapturedAtUtc: capturedAtUtc,
            Sequence: snapshot.Sequence,
            SessionTimeSeconds: Round(snapshot.LatestSample?.SessionTime),
            SessionKind: sessionKind,
            PlayerCarIdx: playerCarIdx,
            FocusCarIdx: focusCarIdx,
            ClassGapSeconds: Round(snapshot.LeaderGap.ClassLeaderGap.Seconds),
            ClassGapLaps: Round(snapshot.LeaderGap.ClassLeaderGap.Laps),
            NearbyCarCount: snapshot.Proximity.NearbyCars.Count,
            TimingRowCount: snapshot.Models.Timing.OverallRows.Count,
            SpatialCarCount: snapshot.Models.Spatial.Cars.Count));
    }

    private IEnumerable<PositionObservation> PositionObservations(HistoricalTelemetrySample sample)
    {
        var seen = new HashSet<int>();
        if (sample.FocusCarIdx is { } focusCarIdx)
        {
            yield return new PositionObservation(
                focusCarIdx,
                sample.FocusPosition,
                sample.FocusClassPosition,
                sample.FocusLapCompleted,
                sample.FocusLapDistPct);
            seen.Add(focusCarIdx);
        }

        if (sample.PlayerCarIdx is { } playerCarIdx && !seen.Contains(playerCarIdx))
        {
            yield return new PositionObservation(
                playerCarIdx,
                sample.TeamPosition,
                sample.TeamClassPosition,
                sample.TeamLapCompleted,
                sample.TeamLapDistPct);
            seen.Add(playerCarIdx);
        }

        if (sample.LeaderCarIdx is { } leaderCarIdx && !seen.Contains(leaderCarIdx))
        {
            yield return new PositionObservation(
                leaderCarIdx,
                1,
                null,
                sample.LeaderLapCompleted,
                sample.LeaderLapDistPct);
            seen.Add(leaderCarIdx);
        }

        if (sample.ClassLeaderCarIdx is { } classLeaderCarIdx && !seen.Contains(classLeaderCarIdx))
        {
            yield return new PositionObservation(
                classLeaderCarIdx,
                null,
                1,
                sample.ClassLeaderLapCompleted,
                sample.ClassLeaderLapDistPct);
            seen.Add(classLeaderCarIdx);
        }

        foreach (var car in sample.FocusClassCars ?? [])
        {
            if (seen.Add(car.CarIdx))
            {
                yield return FromCar(car);
            }
        }

        foreach (var car in sample.ClassCars ?? [])
        {
            if (seen.Add(car.CarIdx))
            {
                yield return FromCar(car);
            }
        }

        foreach (var car in sample.NearbyCars ?? [])
        {
            if (seen.Add(car.CarIdx))
            {
                yield return FromCar(car);
            }
        }
    }

    private bool HasSidePlacementCandidate(LiveTelemetrySnapshot snapshot)
    {
        var sideWindowSeconds = Math.Max(snapshot.Proximity.SideOverlapWindowSeconds, 0.18d);
        return snapshot.Proximity.NearbyCars.Any(car =>
            car.RelativeSeconds is { } seconds
                ? Math.Abs(seconds) <= sideWindowSeconds
                : car.RelativeMeters is { } meters && Math.Abs(meters) <= 7.5d);
    }

    private static PositionObservation FromCar(HistoricalCarProximity car)
    {
        return new PositionObservation(
            car.CarIdx,
            car.Position,
            car.ClassPosition,
            car.LapCompleted,
            car.LapDistPct);
    }

    private static bool IsPitContext(LiveTelemetrySnapshot snapshot)
    {
        return snapshot.Models.FuelPit.OnPitRoad
            || snapshot.Models.FuelPit.PitstopActive
            || snapshot.Models.FuelPit.PlayerCarInPitStall
            || snapshot.Models.FuelPit.TeamOnPitRoad == true
            || snapshot.LatestSample?.FocusOnPitRoad == true
            || snapshot.LatestSample?.TeamOnPitRoad == true;
    }

    private static bool HasTeamTimingContext(HistoricalTelemetrySample? sample)
    {
        return sample?.TeamLapCompleted is not null
            || sample?.TeamLapDistPct is not null
            || sample?.TeamF2TimeSeconds is not null
            || sample?.TeamEstimatedTimeSeconds is not null
            || sample?.TeamPosition is not null
            || sample?.TeamClassPosition is not null;
    }

    private static string SessionKind(LiveTelemetrySnapshot snapshot)
    {
        return FirstNonEmpty(
                snapshot.Models.Session.SessionType,
                snapshot.Context.Session.SessionType,
                snapshot.Models.Session.EventType,
                snapshot.Context.Session.EventType,
                snapshot.Models.Session.SessionName,
                snapshot.Context.Session.SessionName)
            ?? "unknown";
    }

    private static bool IsRaceSession(string sessionKind)
    {
        return string.Equals(sessionKind, "race", StringComparison.OrdinalIgnoreCase);
    }

    private static string FocusKind(int? playerCarIdx, int? focusCarIdx)
    {
        if (focusCarIdx is null)
        {
            return "unknown";
        }

        return playerCarIdx is not null && focusCarIdx == playerCarIdx
            ? "player-or-team"
            : "non-player";
    }

    private static string EvidenceKey(LiveSignalEvidence evidence)
    {
        return evidence.MissingReason is { Length: > 0 } missingReason
            ? $"{evidence.Source}:{evidence.Quality}:{missingReason}"
            : $"{evidence.Source}:{evidence.Quality}";
    }

    private static double? EstimatedLapSeconds(LiveTelemetrySnapshot snapshot)
    {
        return FirstPositiveFinite(
            snapshot.Fuel.LapTimeSeconds,
            snapshot.LatestSample?.FocusLastLapTimeSeconds,
            snapshot.LatestSample?.FocusBestLapTimeSeconds,
            snapshot.LatestSample?.TeamLastLapTimeSeconds,
            snapshot.LatestSample?.TeamBestLapTimeSeconds,
            snapshot.LatestSample?.LapLastLapTimeSeconds,
            snapshot.LatestSample?.LapBestLapTimeSeconds,
            snapshot.Context.Car.DriverCarEstLapTimeSeconds,
            snapshot.Context.Car.CarClassEstLapTimeSeconds);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static double? FirstPositiveFinite(params double?[] values)
    {
        foreach (var value in values)
        {
            if (IsPositiveFinite(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsPositiveFinite(double? value)
    {
        return value is { } finite && finite > 0d && IsFinite(finite);
    }

    private static bool IsFinite(double? value)
    {
        return value is { } finite && !double.IsNaN(finite) && !double.IsInfinity(finite);
    }

    private static double? Max(double? current, double candidate)
    {
        return current is null || candidate > current.Value ? candidate : current;
    }

    private static IReadOnlyDictionary<string, int> Sorted(Dictionary<string, int> values)
    {
        return values
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static void Increment(Dictionary<string, int> values, string? key)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? "unknown" : key.Trim();
        values.TryGetValue(normalizedKey, out var count);
        values[normalizedKey] = count + 1;
    }

    private static double? Round(double? value)
    {
        return value is { } finite && IsFinite(finite) ? Math.Round(finite, 6) : null;
    }

    private string ResolveArtifactPath(string? captureDirectory, string sourceId)
    {
        if (!string.IsNullOrWhiteSpace(captureDirectory))
        {
            return Path.Combine(captureDirectory, _options.OutputFileName);
        }

        return Path.Combine(
            DiagnosticsLogRoot,
            $"{SanitizeFileName(sourceId)}-{_options.OutputFileName}");
    }

    private static string SanitizeFileName(string sourceId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(sourceId.Select(character => invalid.Contains(character) ? '-' : character));
    }

    private sealed record PositionObservation(
        int CarIdx,
        int? OverallPosition,
        int? ClassPosition,
        int? LapCompleted,
        double? LapDistPct);

    private sealed record PositionState(
        int CarIdx,
        int? OverallPosition,
        int? ClassPosition,
        int? LapCompleted,
        double? LapDistPct,
        DateTimeOffset CapturedAtUtc,
        double SessionTimeSeconds);
}

internal sealed record LiveOverlayDiagnosticsArtifact(
    int FormatVersion,
    string SourceId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    LiveOverlayDiagnosticsArtifactOptions Options,
    LiveOverlayDiagnosticsTotals Totals,
    GapOverlayDiagnosticsSummary Gap,
    RadarOverlayDiagnosticsSummary Radar,
    FuelOverlayDiagnosticsSummary Fuel,
    PositionCadenceDiagnosticsSummary PositionCadence,
    IReadOnlyList<LiveOverlayDiagnosticsFrameSample> SampleFrames,
    IReadOnlyList<LiveOverlayDiagnosticsEventSample> EventSamples);

internal sealed record LiveOverlayDiagnosticsArtifactOptions(
    double MinimumFrameSpacingSeconds,
    int MaxSampleFramesPerSession,
    int MaxEventExamplesPerSession,
    int MaxEventExamplesPerKind,
    double LargeGapSeconds,
    double LargeGapLapEquivalent,
    double GapJumpSeconds);

internal sealed record LiveOverlayDiagnosticsTotals(
    int FrameCount,
    int SampledFrameCount,
    int DroppedFrameSampleCount,
    int DroppedEventSampleCount,
    IReadOnlyDictionary<string, int> SessionFrameCounts);

internal sealed record GapOverlayDiagnosticsSummary(
    int FramesWithData,
    int NonRaceFramesWithData,
    int ClassLargeGapFrames,
    int ClassLapEquivalentFrames,
    int ClassJumpFrames,
    int PitContextFrames,
    int ClassRowFrames,
    int MaxClassRows,
    double? MaxClassGapSeconds,
    double? MaxClassGapLaps,
    IReadOnlyDictionary<string, int> ClassGapSourceCounts,
    IReadOnlyDictionary<string, int> ClassGapEvidenceCounts,
    IReadOnlyDictionary<string, int> OverallGapEvidenceCounts);

internal sealed record RadarOverlayDiagnosticsSummary(
    int FramesWithData,
    int NonPlayerFocusFrames,
    int SideSignalFrames,
    int RawSideSuppressedForFocusFrames,
    int SideSignalWithoutPlacementFrames,
    int TimingOnlyRowFrames,
    int SpatialRowFrames,
    int MulticlassApproachFrames,
    int MaxNearbyCars,
    int MaxTimingRows,
    int MaxSpatialCars,
    IReadOnlyDictionary<string, int> FocusFrameCounts,
    IReadOnlyDictionary<string, int> PlacementEvidenceCounts);

internal sealed record FuelOverlayDiagnosticsSummary(
    int FramesWithFuelLevel,
    int FramesWithInstantaneousBurn,
    int InstantaneousBurnWithoutFuelLevelFrames,
    int TeamContextWithoutFuelLevelFrames,
    int PitContextFrames,
    int DriverControlFrames,
    IReadOnlyDictionary<string, int> FuelLevelEvidenceCounts,
    IReadOnlyDictionary<string, int> InstantaneousBurnEvidenceCounts,
    IReadOnlyDictionary<string, int> MeasuredBurnEvidenceCounts,
    IReadOnlyDictionary<string, int> BaselineEligibilityEvidenceCounts);

internal sealed record PositionCadenceDiagnosticsSummary(
    int ObservedFrames,
    int OverallPositionChanges,
    int ClassPositionChanges,
    int IntraLapOverallPositionChanges,
    int IntraLapClassPositionChanges,
    int TrackedCarCount);

internal sealed record LiveOverlayDiagnosticsFrameSample(
    DateTimeOffset CapturedAtUtc,
    long Sequence,
    double? SessionTimeSeconds,
    string SessionKind,
    int? PlayerCarIdx,
    int? FocusCarIdx,
    string FocusKind,
    double? ClassGapSeconds,
    double? ClassGapLaps,
    string ClassGapSource,
    int ClassRowCount,
    int NearbyCarCount,
    int SpatialCarCount,
    int TimingRowCount,
    bool HasSideSignal,
    bool HasFuelLevel,
    bool HasFuelBurn,
    string FuelLevelEvidence,
    string FuelBurnEvidence);

internal sealed record LiveOverlayDiagnosticsEventSample(
    string Kind,
    string Detail,
    DateTimeOffset CapturedAtUtc,
    long Sequence,
    double? SessionTimeSeconds,
    string SessionKind,
    int? PlayerCarIdx,
    int? FocusCarIdx,
    double? ClassGapSeconds,
    double? ClassGapLaps,
    int NearbyCarCount,
    int TimingRowCount,
    int SpatialCarCount);
