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
    private const double SectorBoundarySeedThreshold = 0.0125d;
    private const double LapStartSeedThreshold = 0.02d;
    private const double MaximumContinuousSectorProgressDelta = 0.12d;

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
    private readonly Dictionary<string, int> _lapDeltaValueCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lapDeltaUsableCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _eventSampleCountsByKind = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _eventSampleKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, PositionState> _positionStates = [];
    private readonly Dictionary<int, SectorTimingState> _sectorStates = [];
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
    private int _radarLocalSuppressedNonPlayerFocusFrames;
    private int _radarLocalUnavailablePitOrGarageFrames;
    private int _radarLocalProgressMissingFrames;
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
    private int _lapDeltaObservedFrames;
    private int _lapDeltaFramesWithAnyValue;
    private int _lapDeltaFramesWithAnyUsableValue;
    private double? _maxAbsLapDeltaSeconds;
    private IReadOnlyList<HistoricalTrackSector> _sectorDefinitions = [];
    private int _sectorMetadataFrames;
    private int _sectorMissingMetadataFrames;
    private int _sectorObservedFrames;
    private int _sectorFocusTrackedFrames;
    private int _sectorAheadTrackedFrames;
    private int _sectorBehindTrackedFrames;
    private int _sectorComparisonFrames;
    private int _sectorInvalidProgressFrames;
    private int _sectorResetFrames;
    private int _sectorLapCounterUnavailableFrames;
    private int _sectorSyntheticLapWrapFrames;
    private int _sectorProgressDiscontinuityFrames;
    private int _sectorCrossingCount;
    private int _sectorCompletedIntervalCount;
    private readonly Dictionary<string, int> _trackMapSectorHighlightCounts = new(StringComparer.OrdinalIgnoreCase);
    private int _trackMapSectorFrames;
    private int _trackMapLiveTimingFrames;
    private int _trackMapHighlightedSectorFrames;
    private int _trackMapPersonalBestSectorFrames;
    private int _trackMapBestLapSectorFrames;
    private int _trackMapFullLapHighlightFrames;

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
            _radarLocalSuppressedNonPlayerFocusFrames = 0;
            _radarLocalUnavailablePitOrGarageFrames = 0;
            _radarLocalProgressMissingFrames = 0;
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
            _lapDeltaObservedFrames = 0;
            _lapDeltaFramesWithAnyValue = 0;
            _lapDeltaFramesWithAnyUsableValue = 0;
            _maxAbsLapDeltaSeconds = null;
            _sectorDefinitions = [];
            _sectorMetadataFrames = 0;
            _sectorMissingMetadataFrames = 0;
            _sectorObservedFrames = 0;
            _sectorFocusTrackedFrames = 0;
            _sectorAheadTrackedFrames = 0;
            _sectorBehindTrackedFrames = 0;
            _sectorComparisonFrames = 0;
            _sectorInvalidProgressFrames = 0;
            _sectorResetFrames = 0;
            _sectorLapCounterUnavailableFrames = 0;
            _sectorSyntheticLapWrapFrames = 0;
            _sectorProgressDiscontinuityFrames = 0;
            _sectorCrossingCount = 0;
            _sectorCompletedIntervalCount = 0;
            _trackMapSectorFrames = 0;
            _trackMapLiveTimingFrames = 0;
            _trackMapHighlightedSectorFrames = 0;
            _trackMapPersonalBestSectorFrames = 0;
            _trackMapBestLapSectorFrames = 0;
            _trackMapFullLapHighlightFrames = 0;
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
            _lapDeltaValueCounts.Clear();
            _lapDeltaUsableCounts.Clear();
            _trackMapSectorHighlightCounts.Clear();
            _eventSampleCountsByKind.Clear();
            _eventSampleKeys.Clear();
            _positionStates.Clear();
            _sectorStates.Clear();
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
            RecordLapDelta(snapshot);
            RecordSectorTiming(snapshot, capturedAtUtc);
            RecordTrackMap(snapshot);
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
                        LocalSuppressedNonPlayerFocusFrames: _radarLocalSuppressedNonPlayerFocusFrames,
                        LocalUnavailablePitOrGarageFrames: _radarLocalUnavailablePitOrGarageFrames,
                        LocalProgressMissingFrames: _radarLocalProgressMissingFrames,
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
                    LapDelta: new LapDeltaDiagnosticsSummary(
                        ObservedFrames: _lapDeltaObservedFrames,
                        FramesWithAnyValue: _lapDeltaFramesWithAnyValue,
                        FramesWithAnyUsableValue: _lapDeltaFramesWithAnyUsableValue,
                        MaxAbsDeltaSeconds: Round(_maxAbsLapDeltaSeconds),
                        ValueFrameCounts: Sorted(_lapDeltaValueCounts),
                        UsableFrameCounts: Sorted(_lapDeltaUsableCounts)),
                    SectorTiming: new SectorTimingDiagnosticsSummary(
                        SectorCount: _sectorDefinitions.Count,
                        SectorStartPcts: _sectorDefinitions.Select(sector => Round(sector.SectorStartPct) ?? sector.SectorStartPct).ToArray(),
                        MetadataFrames: _sectorMetadataFrames,
                        MissingMetadataFrames: _sectorMissingMetadataFrames,
                        ObservedFrames: _sectorObservedFrames,
                        FocusTrackedFrames: _sectorFocusTrackedFrames,
                        AheadTrackedFrames: _sectorAheadTrackedFrames,
                        BehindTrackedFrames: _sectorBehindTrackedFrames,
                        ComparisonFrames: _sectorComparisonFrames,
                        InvalidProgressFrames: _sectorInvalidProgressFrames,
                        ResetFrames: _sectorResetFrames,
                        LapCounterUnavailableFrames: _sectorLapCounterUnavailableFrames,
                        SyntheticLapWrapFrames: _sectorSyntheticLapWrapFrames,
                        ProgressDiscontinuityFrames: _sectorProgressDiscontinuityFrames,
                        CrossingCount: _sectorCrossingCount,
                        CompletedIntervalCount: _sectorCompletedIntervalCount,
                        TrackedCarCount: _sectorStates.Count),
                    TrackMap: new TrackMapOverlayDiagnosticsSummary(
                        FramesWithSectors: _trackMapSectorFrames,
                        FramesWithLiveTiming: _trackMapLiveTimingFrames,
                        FramesWithHighlightedSectors: _trackMapHighlightedSectorFrames,
                        PersonalBestSectorFrames: _trackMapPersonalBestSectorFrames,
                        BestLapSectorFrames: _trackMapBestLapSectorFrames,
                        FullLapHighlightFrames: _trackMapFullLapHighlightFrames,
                        SectorHighlightCounts: Sorted(_trackMapSectorHighlightCounts)),
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
        var sample = snapshot.LatestSample;
        var playerCarIdx = snapshot.Models.DriverDirectory.PlayerCarIdx ?? snapshot.LatestSample?.PlayerCarIdx;
        var focusCarIdx = snapshot.Models.DriverDirectory.FocusCarIdx
            ?? snapshot.LatestSample?.FocusCarIdx
            ?? playerCarIdx;
        var focusKind = FocusKind(playerCarIdx, focusCarIdx);
        Increment(_radarFocusFrameCounts, focusKind);

        if (string.Equals(focusKind, "non-player", StringComparison.OrdinalIgnoreCase))
        {
            _radarNonPlayerFocusFrames++;
            _radarLocalSuppressedNonPlayerFocusFrames++;
            AddEvent(
                "radar.local-suppressed-non-player-focus",
                "local-only radar hidden while camera focus is another car",
                snapshot,
                capturedAtUtc);

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

        if (sample is not null && IsLocalRadarPitOrGarage(sample))
        {
            _radarLocalUnavailablePitOrGarageFrames++;
            AddEvent(
                "radar.local-unavailable-pit-or-garage",
                "local-only radar unavailable while local car is off track, in garage, or in pit context",
                snapshot,
                capturedAtUtc);
        }

        if (sample is not null
            && CanUseLocalRadarContext(sample)
            && !IsLocalRadarPitOrGarage(sample)
            && LocalRadarLapDistPct(sample) is null)
        {
            _radarLocalProgressMissingFrames++;
            AddEvent(
                "radar.local-progress-missing",
                "local side/timing context exists but local lap-distance progress is unavailable",
                snapshot,
                capturedAtUtc);
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

    private void RecordLapDelta(LiveTelemetrySnapshot snapshot)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        _lapDeltaObservedFrames++;
        var anyValue = false;
        var anyUsable = false;
        foreach (var signal in LapDeltaSignals(sample))
        {
            if (IsFinite(signal.Seconds))
            {
                anyValue = true;
                Increment(_lapDeltaValueCounts, signal.Key);
                _maxAbsLapDeltaSeconds = Max(_maxAbsLapDeltaSeconds, Math.Abs(signal.Seconds!.Value));
            }

            if (signal.IsUsable)
            {
                anyUsable = true;
                Increment(_lapDeltaUsableCounts, signal.Key);
            }
        }

        if (anyValue)
        {
            _lapDeltaFramesWithAnyValue++;
        }

        if (anyUsable)
        {
            _lapDeltaFramesWithAnyUsableValue++;
        }
    }

    private void RecordSectorTiming(LiveTelemetrySnapshot snapshot, DateTimeOffset capturedAtUtc)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        var sectors = SectorDefinitions(snapshot);
        if (sectors.Count < 2)
        {
            _sectorMissingMetadataFrames++;
            return;
        }

        _sectorMetadataFrames++;
        var observations = SectorObservations(snapshot).ToArray();
        if (observations.Length == 0)
        {
            return;
        }

        _sectorObservedFrames++;
        if (observations.Any(observation => string.Equals(observation.Role, "focus", StringComparison.OrdinalIgnoreCase)))
        {
            _sectorFocusTrackedFrames++;
        }

        if (observations.Any(observation => string.Equals(observation.Role, "ahead", StringComparison.OrdinalIgnoreCase)))
        {
            _sectorAheadTrackedFrames++;
        }

        if (observations.Any(observation => string.Equals(observation.Role, "behind", StringComparison.OrdinalIgnoreCase)))
        {
            _sectorBehindTrackedFrames++;
        }

        if (observations.Any(observation => string.Equals(observation.Role, "focus", StringComparison.OrdinalIgnoreCase))
            && observations.Any(observation => !string.Equals(observation.Role, "focus", StringComparison.OrdinalIgnoreCase)))
        {
            _sectorComparisonFrames++;
        }

        foreach (var observation in observations)
        {
            RecordSectorObservation(snapshot, capturedAtUtc, sectors, observation);
        }
    }

    private void RecordSectorObservation(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<HistoricalTrackSector> sectors,
        SectorObservation observation)
    {
        if (observation.OnPitRoad == true
            || !IsValidSectorLapDistPct(observation.LapDistPct)
            || !IsFinite(observation.SessionTimeSeconds))
        {
            _sectorInvalidProgressFrames++;
            if (_sectorStates.Remove(observation.CarIdx))
            {
                _sectorResetFrames++;
            }

            return;
        }

        var currentLapDistPct = observation.LapDistPct!.Value;
        if (ValidLapCompleted(observation.LapCompleted) is null)
        {
            _sectorLapCounterUnavailableFrames++;
        }

        if (!_sectorStates.TryGetValue(observation.CarIdx, out var previous))
        {
            _sectorStates[observation.CarIdx] = SeedSectorTimingState(sectors, observation, ValidLapCompleted(observation.LapCompleted) ?? 0, currentLapDistPct);
            return;
        }

        var currentLapCompleted = EffectiveLapCompleted(previous, observation, currentLapDistPct);
        var previousProgress = previous.LapCompleted + previous.LapDistPct;
        var currentProgress = currentLapCompleted + currentLapDistPct;
        var progressDelta = currentProgress - previousProgress;
        if (observation.LapCompleted is not >= 0 && currentLapCompleted > previous.LapCompleted)
        {
            _sectorSyntheticLapWrapFrames++;
        }

        if (currentLapCompleted < previous.LapCompleted
            || currentLapCompleted - previous.LapCompleted > 1
            || progressDelta < 0d
            || progressDelta > MaximumContinuousSectorProgressDelta
            || !IsFinite(progressDelta)
            || !IsFinite(observation.SessionTimeSeconds)
            || observation.SessionTimeSeconds <= previous.SessionTimeSeconds)
        {
            _sectorResetFrames++;
            _sectorProgressDiscontinuityFrames++;
            AddEvent(
                "sector.progress-discontinuity",
                $"car {observation.CarIdx} {observation.Role} previous {previous.LapCompleted + previous.LapDistPct:0.###} current {currentProgress:0.###} delta {progressDelta:0.###}",
                snapshot,
                capturedAtUtc);
            _sectorStates[observation.CarIdx] = SeedSectorTimingState(sectors, observation, currentLapCompleted, currentLapDistPct);
            return;
        }

        var crossings = SectorCrossings(sectors, previous, observation, currentLapCompleted).ToArray();
        var state = previous;
        foreach (var crossing in crossings)
        {
            _sectorCrossingCount++;
            if (state.LastCrossingSector is { } previousSector
                && state.LastCrossingSessionTimeSeconds is { } previousCrossingTime)
            {
                var elapsed = crossing.SessionTimeSeconds - previousCrossingTime;
                if (elapsed is > 0d and < 900d)
                {
                    _sectorCompletedIntervalCount++;
                    AddEvent(
                        "sector.interval-derived",
                        $"car {observation.CarIdx} {observation.Role} sector {previousSector}->{crossing.SectorNum} {elapsed:0.###}s",
                        snapshot,
                        capturedAtUtc);
                }
            }

            state = state with
            {
                LastCrossingSector = crossing.SectorNum,
                LastCrossingSessionTimeSeconds = crossing.SessionTimeSeconds
            };
        }

        _sectorStates[observation.CarIdx] = state with
        {
            LapCompleted = currentLapCompleted,
            LapDistPct = currentLapDistPct,
            SessionTimeSeconds = observation.SessionTimeSeconds
        };
    }

    private static SectorTimingState SeedSectorTimingState(
        IReadOnlyList<HistoricalTrackSector> sectors,
        SectorObservation observation,
        int lapCompleted,
        double lapDistPct)
    {
        var boundarySectorNum = SeedBoundarySectorNum(sectors, lapDistPct);
        return new SectorTimingState(
            observation.CarIdx,
            lapCompleted,
            lapDistPct,
            observation.SessionTimeSeconds,
            LastCrossingSector: boundarySectorNum,
            LastCrossingSessionTimeSeconds: boundarySectorNum is null ? null : observation.SessionTimeSeconds);
    }

    private void RecordTrackMap(LiveTelemetrySnapshot snapshot)
    {
        var trackMap = snapshot.Models.TrackMap;
        if (trackMap.HasSectors)
        {
            _trackMapSectorFrames++;
        }

        if (trackMap.HasLiveTiming)
        {
            _trackMapLiveTimingFrames++;
        }

        var highlighted = trackMap.Sectors
            .Where(sector => !string.Equals(sector.Highlight, LiveTrackSectorHighlights.None, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (highlighted.Length == 0)
        {
            return;
        }

        _trackMapHighlightedSectorFrames++;
        foreach (var sector in highlighted)
        {
            Increment(_trackMapSectorHighlightCounts, sector.Highlight);
        }

        if (highlighted.Any(sector => string.Equals(sector.Highlight, LiveTrackSectorHighlights.PersonalBest, StringComparison.OrdinalIgnoreCase)))
        {
            _trackMapPersonalBestSectorFrames++;
        }

        if (highlighted.Any(sector => string.Equals(sector.Highlight, LiveTrackSectorHighlights.BestLap, StringComparison.OrdinalIgnoreCase)))
        {
            _trackMapBestLapSectorFrames++;
        }

        if (trackMap.Sectors.Count > 0
            && highlighted.Length == trackMap.Sectors.Count
            && highlighted
                .Select(sector => sector.Highlight)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == 1)
        {
            _trackMapFullLapHighlightFrames++;
        }
    }

    private IEnumerable<LapDeltaSignal> LapDeltaSignals(HistoricalTelemetrySample sample)
    {
        yield return new LapDeltaSignal(
            "toBestLap",
            sample.LapDeltaToBestLapSeconds,
            sample.LapDeltaToBestLapRate,
            sample.LapDeltaToBestLapOk);
        yield return new LapDeltaSignal(
            "toOptimalLap",
            sample.LapDeltaToOptimalLapSeconds,
            sample.LapDeltaToOptimalLapRate,
            sample.LapDeltaToOptimalLapOk);
        yield return new LapDeltaSignal(
            "toSessionBestLap",
            sample.LapDeltaToSessionBestLapSeconds,
            sample.LapDeltaToSessionBestLapRate,
            sample.LapDeltaToSessionBestLapOk);
        yield return new LapDeltaSignal(
            "toSessionOptimalLap",
            sample.LapDeltaToSessionOptimalLapSeconds,
            sample.LapDeltaToSessionOptimalLapRate,
            sample.LapDeltaToSessionOptimalLapOk);
        yield return new LapDeltaSignal(
            "toSessionLastLap",
            sample.LapDeltaToSessionLastLapSeconds,
            sample.LapDeltaToSessionLastLapRate,
            sample.LapDeltaToSessionLastLapOk);
    }

    private IReadOnlyList<HistoricalTrackSector> SectorDefinitions(LiveTelemetrySnapshot snapshot)
    {
        if (_sectorDefinitions.Count >= 2)
        {
            return _sectorDefinitions;
        }

        var sectors = snapshot.Context.Sectors
            .Where(sector => IsFinite(sector.SectorStartPct) && sector.SectorStartPct >= 0d && sector.SectorStartPct < 1d)
            .GroupBy(sector => sector.SectorNum)
            .Select(group => group.OrderBy(sector => sector.SectorStartPct).First())
            .OrderBy(sector => sector.SectorStartPct)
            .ToArray();
        if (sectors.Length >= 2)
        {
            _sectorDefinitions = sectors;
        }

        return _sectorDefinitions;
    }

    private IEnumerable<SectorObservation> SectorObservations(LiveTelemetrySnapshot snapshot)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            yield break;
        }

        var focusCarIdx = snapshot.Models.DriverDirectory.FocusCarIdx
            ?? sample.FocusCarIdx
            ?? sample.PlayerCarIdx;
        var timingByCarIdx = snapshot.Models.Timing.OverallRows
            .GroupBy(row => row.CarIdx)
            .ToDictionary(group => group.Key, group => group.First());

        if (focusCarIdx is { } focusIdx)
        {
            var focusRow = MatchingTimingRow(snapshot.Models.Timing.FocusRow, focusIdx)
                ?? MatchingTimingRow(snapshot.Models.Timing.PlayerRow, focusIdx)
                ?? (timingByCarIdx.TryGetValue(focusIdx, out var timingRow) ? timingRow : null);
            yield return new SectorObservation(
                focusIdx,
                "focus",
                focusRow?.LapCompleted ?? FocusLapCompleted(sample),
                focusRow?.LapDistPct ?? FocusLapDistPct(sample),
                sample.SessionTime,
                focusRow?.OnPitRoad ?? FocusOnPitRoad(sample));
        }

        foreach (var observation in RelativeSectorObservations(snapshot, sample, timingByCarIdx))
        {
            yield return observation;
        }
    }

    private static IEnumerable<SectorObservation> RelativeSectorObservations(
        LiveTelemetrySnapshot snapshot,
        HistoricalTelemetrySample sample,
        IReadOnlyDictionary<int, LiveTimingRow> timingByCarIdx)
    {
        var ahead = snapshot.Models.Relative.Rows
            .Where(row => row.IsAhead)
            .OrderBy(RelativeSortKey)
            .FirstOrDefault();
        if (ahead is not null && TryCreateRelativeSectorObservation(ahead, "ahead", sample, timingByCarIdx, out var aheadObservation))
        {
            yield return aheadObservation;
        }

        var behind = snapshot.Models.Relative.Rows
            .Where(row => row.IsBehind)
            .OrderBy(RelativeSortKey)
            .FirstOrDefault();
        if (behind is not null && TryCreateRelativeSectorObservation(behind, "behind", sample, timingByCarIdx, out var behindObservation))
        {
            yield return behindObservation;
        }
    }

    private static bool TryCreateRelativeSectorObservation(
        LiveRelativeRow row,
        string role,
        HistoricalTelemetrySample sample,
        IReadOnlyDictionary<int, LiveTimingRow> timingByCarIdx,
        out SectorObservation observation)
    {
        var timingRow = timingByCarIdx.TryGetValue(row.CarIdx, out var candidate) ? candidate : null;
        var proximity = FindProximity(sample, row.CarIdx);
        var lapCompleted = timingRow?.LapCompleted ?? proximity?.LapCompleted;
        var lapDistPct = timingRow?.LapDistPct ?? proximity?.LapDistPct;
        observation = new SectorObservation(
            row.CarIdx,
            role,
            lapCompleted,
            lapDistPct,
            sample.SessionTime,
            row.OnPitRoad ?? timingRow?.OnPitRoad ?? proximity?.OnPitRoad);
        return lapCompleted is not null || lapDistPct is not null;
    }

    private static HistoricalCarProximity? FindProximity(HistoricalTelemetrySample sample, int carIdx)
    {
        return (sample.FocusClassCars ?? [])
            .Concat(sample.ClassCars ?? [])
            .Concat(sample.NearbyCars ?? [])
            .FirstOrDefault(car => car.CarIdx == carIdx);
    }

    private static IEnumerable<SectorCrossing> SectorCrossings(
        IReadOnlyList<HistoricalTrackSector> sectors,
        SectorTimingState previous,
        SectorObservation current,
        int currentLapCompleted)
    {
        var currentLapDistPct = current.LapDistPct!.Value;
        var previousProgress = previous.LapCompleted + previous.LapDistPct;
        var currentProgress = currentLapCompleted + currentLapDistPct;
        var progressDelta = currentProgress - previousProgress;
        if (progressDelta <= 0d || !IsFinite(progressDelta))
        {
            yield break;
        }

        var timeDelta = current.SessionTimeSeconds - previous.SessionTimeSeconds;
        for (var lap = previous.LapCompleted; lap <= currentLapCompleted; lap++)
        {
            foreach (var sector in sectors)
            {
                var boundaryProgress = lap + sector.SectorStartPct;
                if (boundaryProgress <= previousProgress || boundaryProgress > currentProgress)
                {
                    continue;
                }

                var interpolation = (boundaryProgress - previousProgress) / progressDelta;
                if (!IsFinite(interpolation) || interpolation < 0d || interpolation > 1d)
                {
                    continue;
                }

                yield return new SectorCrossing(
                    sector.SectorNum,
                    previous.SessionTimeSeconds + (timeDelta * interpolation));
            }
        }
    }

    private static int EffectiveLapCompleted(SectorTimingState previous, SectorObservation current, double currentLapDistPct)
    {
        if (ValidLapCompleted(current.LapCompleted) is { } reportedLapCompleted)
        {
            return reportedLapCompleted;
        }

        return previous.LapDistPct >= 0.75d && currentLapDistPct <= 0.25d
            ? previous.LapCompleted + 1
            : previous.LapCompleted;
    }

    private static int? ValidLapCompleted(int? lapCompleted)
    {
        return lapCompleted is >= 0 ? lapCompleted : null;
    }

    private static bool IsValidSectorLapDistPct(double? lapDistPct)
    {
        return lapDistPct is { } pct
            && IsFinite(pct)
            && pct >= 0d
            && pct <= 1d;
    }

    private static int? SeedBoundarySectorNum(IReadOnlyList<HistoricalTrackSector> sectors, double lapDistPct)
    {
        if (lapDistPct <= LapStartSeedThreshold)
        {
            return sectors
                .OrderBy(sector => Math.Abs(sector.SectorStartPct))
                .FirstOrDefault()
                ?.SectorNum;
        }

        var nearest = sectors
            .Select(sector => new
            {
                sector.SectorNum,
                Distance = lapDistPct - sector.SectorStartPct
            })
            .Where(candidate => candidate.Distance >= 0d)
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();
        return nearest is not null && nearest.Distance <= SectorBoundarySeedThreshold
            ? nearest.SectorNum
            : null;
    }

    private static LiveTimingRow? MatchingTimingRow(LiveTimingRow? row, int carIdx)
    {
        return row?.CarIdx == carIdx ? row : null;
    }

    private static int? FocusLapCompleted(HistoricalTelemetrySample sample)
    {
        return HasExplicitNonPlayerFocus(sample)
            ? sample.FocusLapCompleted
            : sample.FocusLapCompleted ?? sample.TeamLapCompleted ?? sample.LapCompleted;
    }

    private static double? FocusLapDistPct(HistoricalTelemetrySample sample)
    {
        return HasExplicitNonPlayerFocus(sample)
            ? sample.FocusLapDistPct
            : sample.FocusLapDistPct ?? sample.TeamLapDistPct ?? sample.LapDistPct;
    }

    private static bool? FocusOnPitRoad(HistoricalTelemetrySample sample)
    {
        return HasExplicitNonPlayerFocus(sample)
            ? sample.FocusOnPitRoad
            : sample.FocusOnPitRoad ?? sample.TeamOnPitRoad ?? sample.OnPitRoad;
    }

    private static double RelativeSortKey(LiveRelativeRow row)
    {
        if (row.RelativeSeconds is { } seconds && IsFinite(seconds))
        {
            return Math.Abs(seconds);
        }

        if (row.RelativeMeters is { } meters && IsFinite(meters))
        {
            return Math.Abs(meters);
        }

        if (row.RelativeLaps is { } laps && IsFinite(laps))
        {
            return Math.Abs(laps);
        }

        return double.MaxValue;
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
            FocusKind: FocusKind(playerCarIdx, focusCarIdx),
            RawCarLeftRight: snapshot.LatestSample?.CarLeftRight,
            RawNearbyCarCount: snapshot.LatestSample?.NearbyCars?.Count ?? 0,
            HasRadarData: snapshot.Proximity.HasData,
            HasSideSignal: snapshot.Proximity.CarLeftRight is not null,
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

    private static bool CanUseLocalRadarContext(HistoricalTelemetrySample sample)
    {
        return sample.IsOnTrack
            && !sample.IsInGarage
            && (sample.PlayerCarIdx is not null || sample.FocusCarIdx is null)
            && !HasExplicitNonPlayerFocus(sample);
    }

    private static bool IsLocalRadarPitOrGarage(HistoricalTelemetrySample sample)
    {
        return !sample.IsOnTrack
            || sample.IsInGarage
            || sample.OnPitRoad
            || sample.PlayerCarInPitStall
            || sample.TeamOnPitRoad == true
            || IsPitRoadTrackSurface(sample.FocusTrackSurface)
            || IsPitRoadTrackSurface(sample.PlayerTrackSurface);
    }

    private static double? LocalRadarLapDistPct(HistoricalTelemetrySample sample)
    {
        if (!HasExplicitNonPlayerFocus(sample)
            && sample.FocusLapDistPct is { } focusLapDistPct
            && IsFinite(focusLapDistPct)
            && focusLapDistPct >= 0d)
        {
            return focusLapDistPct;
        }

        if (sample.TeamLapDistPct is { } teamLapDistPct && IsFinite(teamLapDistPct) && teamLapDistPct >= 0d)
        {
            return teamLapDistPct;
        }

        return IsFinite(sample.LapDistPct) && sample.LapDistPct >= 0d
            ? sample.LapDistPct
            : null;
    }

    private static bool HasExplicitNonPlayerFocus(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is not null
            && sample.PlayerCarIdx is not null
            && sample.FocusCarIdx != sample.PlayerCarIdx;
    }

    private static bool IsPitRoadTrackSurface(int? trackSurface)
    {
        return trackSurface is 1 or 2;
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

    private sealed record LapDeltaSignal(
        string Key,
        double? Seconds,
        double? Rate,
        bool? Ok)
    {
        public bool IsUsable => Ok == true && Seconds is { } seconds && !double.IsNaN(seconds) && !double.IsInfinity(seconds);
    }

    private sealed record SectorObservation(
        int CarIdx,
        string Role,
        int? LapCompleted,
        double? LapDistPct,
        double SessionTimeSeconds,
        bool? OnPitRoad);

    private sealed record SectorTimingState(
        int CarIdx,
        int LapCompleted,
        double LapDistPct,
        double SessionTimeSeconds,
        int? LastCrossingSector,
        double? LastCrossingSessionTimeSeconds);

    private sealed record SectorCrossing(
        int SectorNum,
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
    LapDeltaDiagnosticsSummary LapDelta,
    SectorTimingDiagnosticsSummary SectorTiming,
    TrackMapOverlayDiagnosticsSummary TrackMap,
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
    int LocalSuppressedNonPlayerFocusFrames,
    int LocalUnavailablePitOrGarageFrames,
    int LocalProgressMissingFrames,
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

internal sealed record LapDeltaDiagnosticsSummary(
    int ObservedFrames,
    int FramesWithAnyValue,
    int FramesWithAnyUsableValue,
    double? MaxAbsDeltaSeconds,
    IReadOnlyDictionary<string, int> ValueFrameCounts,
    IReadOnlyDictionary<string, int> UsableFrameCounts);

internal sealed record SectorTimingDiagnosticsSummary(
    int SectorCount,
    IReadOnlyList<double> SectorStartPcts,
    int MetadataFrames,
    int MissingMetadataFrames,
    int ObservedFrames,
    int FocusTrackedFrames,
    int AheadTrackedFrames,
    int BehindTrackedFrames,
    int ComparisonFrames,
    int InvalidProgressFrames,
    int ResetFrames,
    int LapCounterUnavailableFrames,
    int SyntheticLapWrapFrames,
    int ProgressDiscontinuityFrames,
    int CrossingCount,
    int CompletedIntervalCount,
    int TrackedCarCount);

internal sealed record TrackMapOverlayDiagnosticsSummary(
    int FramesWithSectors,
    int FramesWithLiveTiming,
    int FramesWithHighlightedSectors,
    int PersonalBestSectorFrames,
    int BestLapSectorFrames,
    int FullLapHighlightFrames,
    IReadOnlyDictionary<string, int> SectorHighlightCounts);

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
    string FocusKind,
    int? RawCarLeftRight,
    int RawNearbyCarCount,
    bool HasRadarData,
    bool HasSideSignal,
    double? ClassGapSeconds,
    double? ClassGapLaps,
    int NearbyCarCount,
    int TimingRowCount,
    int SpatialCarCount);
