using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.EdgeCases;

internal sealed class TelemetryEdgeCaseDetector
{
    private const double SuspiciousZeroTimingSeconds = 0.05d;
    private const double SuspiciousZeroTimingLapsWithoutLapTime = 0.001d;
    private const double SideCandidateLapWindow = 0.0025d;
    private const double SideCandidateSecondsWindow = 0.5d;
    private const double TireWearResetPercent = 2.0d;
    private const double EngineOffRpmThreshold = 500d;
    private const double EngineOffOilPressureThreshold = 0.2d;
    private const int RacingSessionState = 4;
    private const double StartLapDistanceWindow = 0.05d;
    private const double MaximumContinuousLapProgressDelta = 0.12d;
    private const double LapWrapPreviousThreshold = 0.75d;
    private const double LapWrapCurrentThreshold = 0.25d;

    private readonly HashSet<string> _emittedOnce = new(StringComparer.OrdinalIgnoreCase);
    private HistoricalTelemetrySample? _previousSample;
    private RawTelemetryWatchSnapshot _previousRaw = RawTelemetryWatchSnapshot.Empty;

    public IReadOnlyList<TelemetryEdgeCaseObservation> Analyze(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw)
    {
        var observations = new List<TelemetryEdgeCaseObservation>();

        DetectTimingContradictions(sample, observations);
        DetectSideOccupancyWithoutAdjacentCar(sample, observations);
        DetectFocusDataGaps(sample, observations);
        DetectProgressDiscontinuities(sample, raw, observations);
        DetectPitStateConflicts(sample, observations);
        DetectFuelAnomalies(sample, observations);
        DetectTireAndServiceAnomalies(sample, raw, observations);
        DetectDriverChangeSignals(sample, observations);
        DetectWeatherTransitions(sample, raw, observations);
        DetectRawStateTransitions(sample, raw, observations);
        DetectRawEngineeringChannels(sample, raw, observations);
        DetectRawRuntimeWarnings(sample, raw, observations);

        _previousSample = sample;
        _previousRaw = raw;
        return observations;
    }

    public void Reset()
    {
        _emittedOnce.Clear();
        _previousSample = null;
        _previousRaw = RawTelemetryWatchSnapshot.Empty;
    }

    private void DetectTimingContradictions(
        HistoricalTelemetrySample sample,
        List<TelemetryEdgeCaseObservation> observations)
    {
        var focusLapDistPct = FocusLapDistPct(sample);
        if (focusLapDistPct is null)
        {
            return;
        }

        foreach (var car in sample.NearbyCars ?? [])
        {
            if (car.OnPitRoad == true)
            {
                continue;
            }

            var relativeLaps = RelativeLaps(car.LapDistPct, focusLapDistPct.Value);
            if (Math.Abs(relativeLaps) <= 0.00001d)
            {
                continue;
            }

            var lapTimeSeconds = LiveLapTimeSeconds(sample);
            DetectTimingContradiction(
                sample,
                car,
                relativeLaps,
                lapTimeSeconds,
                "CarIdxEstTime",
                car.EstimatedTimeSeconds,
                FocusEstimatedTimeSeconds(sample),
                estimatedTimeSource: true,
                observations);
            DetectTimingContradiction(
                sample,
                car,
                relativeLaps,
                lapTimeSeconds,
                "CarIdxF2Time",
                car.F2TimeSeconds,
                FocusF2TimeSeconds(sample),
                estimatedTimeSource: false,
                observations);
        }
    }

    private void DetectTimingContradiction(
        HistoricalTelemetrySample sample,
        HistoricalCarProximity car,
        double relativeLaps,
        double? lapTimeSeconds,
        string source,
        double? carSeconds,
        double? focusSeconds,
        bool estimatedTimeSource,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (!IsNonNegativeFinite(carSeconds) || !IsNonNegativeFinite(focusSeconds))
        {
            return;
        }

        var delta = estimatedTimeSource
            ? carSeconds!.Value - focusSeconds!.Value
            : focusSeconds!.Value - carSeconds!.Value;
        if (estimatedTimeSource && lapTimeSeconds is { } lapSeconds && IsPositiveFinite(lapSeconds))
        {
            if (delta > lapSeconds / 2d)
            {
                delta -= lapSeconds;
            }
            else if (delta < -lapSeconds / 2d)
            {
                delta += lapSeconds;
            }
        }

        if (Math.Abs(delta) <= SuspiciousZeroTimingSeconds
            && Math.Abs(relativeLaps) >= SuspiciousZeroTimingLapsWithoutLapTime)
        {
            if (IsNearZero(carSeconds)
                && IsNearZero(focusSeconds))
            {
                var startContext = StartupGridOrTowContext(_previousSample, sample);
                if (startContext is not null)
                {
                    EmitOnce(
                        observations,
                        $"timing.uninitialized-start-context.{source}.car-{car.CarIdx}",
                        TelemetryEdgeCaseSeverity.Info,
                        "Timing row appeared uninitialized during grid, start, pit, tow, or replay context while lap-distance progress showed physical separation.",
                        sample,
                        new Dictionary<string, string?>
                        {
                            ["source"] = source,
                            ["carIdx"] = car.CarIdx.ToString(),
                            ["context"] = startContext,
                            ["relativeLaps"] = Format(relativeLaps),
                            ["deltaSeconds"] = Format(delta),
                            ["carEstimatedTimeSeconds"] = Format(car.EstimatedTimeSeconds),
                            ["focusEstimatedTimeSeconds"] = Format(FocusEstimatedTimeSeconds(sample)),
                            ["carLapCompleted"] = car.LapCompleted.ToString(),
                            ["focusLapCompleted"] = sample.FocusLapCompleted?.ToString(),
                            ["carLapDistPct"] = Format(car.LapDistPct),
                            ["focusLapDistPct"] = Format(FocusLapDistPct(sample)),
                            ["sessionState"] = sample.SessionState?.ToString(),
                            ["isOnTrack"] = sample.IsOnTrack.ToString(),
                            ["onPitRoad"] = sample.OnPitRoad.ToString(),
                            ["speedMetersPerSecond"] = Format(sample.SpeedMetersPerSecond),
                            ["carClass"] = car.CarClass?.ToString(),
                            ["classPosition"] = car.ClassPosition?.ToString(),
                            ["trackSurface"] = car.TrackSurface?.ToString()
                        });
                    return;
                }
            }

            EmitOnce(
                observations,
                $"timing.zero.{source}.car-{car.CarIdx}",
                TelemetryEdgeCaseSeverity.Warning,
                "Timing row stayed near zero while lap-distance progress showed separation.",
                sample,
                new Dictionary<string, string?>
                {
                    ["source"] = source,
                    ["carIdx"] = car.CarIdx.ToString(),
                    ["relativeLaps"] = Format(relativeLaps),
                    ["deltaSeconds"] = Format(delta),
                    ["carClass"] = car.CarClass?.ToString(),
                    ["classPosition"] = car.ClassPosition?.ToString()
                });
            return;
        }

        var timingSign = Math.Sign(delta);
        var lapSign = Math.Sign(relativeLaps);
        if (timingSign != 0 && lapSign != 0 && timingSign != lapSign)
        {
            EmitOnce(
                observations,
                $"timing.sign-mismatch.{source}.car-{car.CarIdx}",
                TelemetryEdgeCaseSeverity.Warning,
                "Timing direction disagreed with lap-distance direction.",
                sample,
                new Dictionary<string, string?>
                {
                    ["source"] = source,
                    ["carIdx"] = car.CarIdx.ToString(),
                    ["relativeLaps"] = Format(relativeLaps),
                    ["deltaSeconds"] = Format(delta),
                    ["carClass"] = car.CarClass?.ToString(),
                    ["classPosition"] = car.ClassPosition?.ToString()
                });
        }
    }

    private void DetectSideOccupancyWithoutAdjacentCar(
        HistoricalTelemetrySample sample,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (!HasSideOccupancy(sample.CarLeftRight))
        {
            return;
        }

        var focusLapDistPct = FocusLapDistPct(sample);
        var focusF2 = FocusF2TimeSeconds(sample);
        var focusEst = FocusEstimatedTimeSeconds(sample);
        var hasCandidate = false;

        foreach (var car in sample.NearbyCars ?? [])
        {
            if (car.OnPitRoad == true)
            {
                continue;
            }

            if (focusLapDistPct is { } pct
                && Math.Abs(RelativeLaps(car.LapDistPct, pct)) <= SideCandidateLapWindow)
            {
                hasCandidate = true;
                break;
            }

            if (focusEst is { } est
                && car.EstimatedTimeSeconds is { } carEst
                && Math.Abs(carEst - est) <= SideCandidateSecondsWindow)
            {
                hasCandidate = true;
                break;
            }

            if (focusF2 is { } f2
                && car.F2TimeSeconds is { } carF2
                && Math.Abs(f2 - carF2) <= SideCandidateSecondsWindow)
            {
                hasCandidate = true;
                break;
            }
        }

        if (!hasCandidate)
        {
            EmitOnce(
                observations,
                "side-occupancy.no-adjacent-car",
                TelemetryEdgeCaseSeverity.Warning,
                "CarLeftRight reported side pressure but no nearby timed car matched the overlap window.",
                sample,
                new Dictionary<string, string?>
                {
                    ["carLeftRight"] = sample.CarLeftRight?.ToString(),
                    ["focusCarIdx"] = sample.FocusCarIdx?.ToString(),
                    ["playerCarIdx"] = sample.PlayerCarIdx?.ToString(),
                    ["nearbyCarCount"] = (sample.NearbyCars?.Count ?? 0).ToString()
                });
        }
    }

    private void DetectFocusDataGaps(
        HistoricalTelemetrySample sample,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (_previousSample?.FocusCarIdx is { } previousChangedFocus
            && sample.FocusCarIdx is { } currentChangedFocus
            && previousChangedFocus != currentChangedFocus)
        {
            EmitOnce(
                observations,
                "focus.changed",
                TelemetryEdgeCaseSeverity.Info,
                "Camera/focus car changed during collection.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousFocusCarIdx"] = previousChangedFocus.ToString(),
                    ["currentFocusCarIdx"] = currentChangedFocus.ToString(),
                    ["playerCarIdx"] = sample.PlayerCarIdx?.ToString(),
                    ["focusLapDistPct"] = Format(sample.FocusLapDistPct),
                    ["focusF2TimeSeconds"] = Format(sample.FocusF2TimeSeconds),
                    ["focusEstimatedTimeSeconds"] = Format(sample.FocusEstimatedTimeSeconds)
                });
        }

        if (sample.FocusCarIdx is { } focus
            && sample.PlayerCarIdx is { } player
            && focus != player
            && sample.FocusLapDistPct is null
            && sample.FocusF2TimeSeconds is null
            && sample.FocusEstimatedTimeSeconds is null)
        {
            EmitOnce(
                observations,
                $"focus.missing-progress.car-{focus}",
                TelemetryEdgeCaseSeverity.Warning,
                "Camera/focus car was selected but had no progress or timing data.",
                sample,
                new Dictionary<string, string?>
                {
                    ["focusCarIdx"] = focus.ToString(),
                    ["playerCarIdx"] = player.ToString(),
                    ["focusPosition"] = sample.FocusPosition?.ToString(),
                    ["focusClassPosition"] = sample.FocusClassPosition?.ToString()
                });
        }

        if (_previousSample?.FocusCarIdx is { } previousFocus
            && sample.FocusCarIdx is { } currentFocus
            && previousFocus != currentFocus
            && sample.FocusLapDistPct is null)
        {
            EmitOnce(
                observations,
                $"focus.change-data-gap.car-{currentFocus}",
                TelemetryEdgeCaseSeverity.Info,
                "Camera/focus changed and the new focus car initially had no lap-distance progress.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousFocusCarIdx"] = previousFocus.ToString(),
                    ["currentFocusCarIdx"] = currentFocus.ToString()
                });
        }
    }

    private void DetectPitStateConflicts(
        HistoricalTelemetrySample sample,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (sample.PlayerCarInPitStall && !sample.OnPitRoad)
        {
            EmitOnce(
                observations,
                "pit-state.stall-without-pit-road",
                TelemetryEdgeCaseSeverity.Warning,
                "PlayerCarInPitStall was true while OnPitRoad was false.",
                sample,
                new Dictionary<string, string?>
                {
                    ["onPitRoad"] = sample.OnPitRoad.ToString(),
                    ["teamOnPitRoad"] = sample.TeamOnPitRoad?.ToString(),
                    ["focusOnPitRoad"] = sample.FocusOnPitRoad?.ToString()
                });
        }

        if (sample.TeamOnPitRoad is { } teamOnPitRoad && teamOnPitRoad != sample.OnPitRoad)
        {
            EmitOnce(
                observations,
                "pit-state.player-team-disagreement",
                TelemetryEdgeCaseSeverity.Info,
                "Scalar OnPitRoad and team-car CarIdxOnPitRoad disagreed.",
                sample,
                new Dictionary<string, string?>
                {
                    ["onPitRoad"] = sample.OnPitRoad.ToString(),
                    ["teamOnPitRoad"] = teamOnPitRoad.ToString(),
                    ["playerCarIdx"] = sample.PlayerCarIdx?.ToString()
                });
        }
    }

    private void DetectProgressDiscontinuities(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (_previousSample is null)
        {
            return;
        }

        var previousProgress = LocalLapProgress(_previousSample);
        var currentProgress = LocalLapProgress(sample);
        if (previousProgress is null || currentProgress is null)
        {
            return;
        }

        var deltaLaps = LapProgressDelta(previousProgress, currentProgress);
        if (deltaLaps >= 0d && deltaLaps <= MaximumContinuousLapProgressDelta)
        {
            return;
        }

        var context = ProgressDiscontinuityContext(_previousSample, sample, raw);
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["context"] = context,
            ["previousSource"] = previousProgress.Source,
            ["currentSource"] = currentProgress.Source,
            ["previousLapCompleted"] = previousProgress.LapCompleted?.ToString(),
            ["currentLapCompleted"] = currentProgress.LapCompleted?.ToString(),
            ["previousLapDistPct"] = Format(previousProgress.LapDistPct),
            ["currentLapDistPct"] = Format(currentProgress.LapDistPct),
            ["deltaLaps"] = Format(deltaLaps),
            ["enterExitReset"] = Format(raw.Get("EnterExitReset")),
            ["playerCarTowTime"] = Format(raw.Get("PlayerCarTowTime")),
            ["isOnTrack"] = sample.IsOnTrack.ToString(),
            ["isInGarage"] = sample.IsInGarage.ToString(),
            ["onPitRoad"] = sample.OnPitRoad.ToString(),
            ["teamOnPitRoad"] = sample.TeamOnPitRoad?.ToString(),
            ["sessionState"] = sample.SessionState?.ToString()
        };
        EmitOnce(
            observations,
            context == "unknown"
                ? "progress.discontinuity.local"
                : $"progress.discontinuity.{context}",
            context == "unknown" ? TelemetryEdgeCaseSeverity.Warning : TelemetryEdgeCaseSeverity.Info,
            context == "active-reset"
                ? "Lap-distance progress jumped while the reset-key action indicated active reset context."
                : "Lap-distance progress jumped or moved backward outside a normal start/finish wrap.",
            sample,
            fields);
    }

    private void DetectFuelAnomalies(
        HistoricalTelemetrySample sample,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (IsTeamCarInGreenStint(sample) && !IsValidFuel(sample.FuelLevelLiters))
        {
            EmitOnce(
                observations,
                "fuel.scalar-unavailable-with-team-progress",
                TelemetryEdgeCaseSeverity.Info,
                "Team car had live progress while scalar fuel was unavailable.",
                sample,
                new Dictionary<string, string?>
                {
                    ["teamLapCompleted"] = sample.TeamLapCompleted?.ToString(),
                    ["teamLapDistPct"] = Format(sample.TeamLapDistPct),
                    ["teamOnPitRoad"] = sample.TeamOnPitRoad?.ToString()
                });
        }

        if (_previousSample is null)
        {
            return;
        }

        var deltaSeconds = sample.SessionTime - _previousSample.SessionTime;
        if (deltaSeconds <= 0d || deltaSeconds > 2d)
        {
            var duplicateStartupContext = Math.Abs(deltaSeconds) <= 0.000001d
                && IsStartupGridOrTowContext(_previousSample, sample);
            EmitOnce(
                observations,
                duplicateStartupContext ? "session-time.duplicate-startup" : "session-time.jump",
                duplicateStartupContext ? TelemetryEdgeCaseSeverity.Info : TelemetryEdgeCaseSeverity.Warning,
                duplicateStartupContext
                    ? "SessionTime repeated during grid, pit, tow, or replay startup context."
                    : "SessionTime jumped or moved backward between frames.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousSessionTime"] = Format(_previousSample.SessionTime),
                    ["currentSessionTime"] = Format(sample.SessionTime),
                    ["deltaSeconds"] = Format(deltaSeconds),
                    ["previousIsOnTrack"] = _previousSample.IsOnTrack.ToString(),
                    ["currentIsOnTrack"] = sample.IsOnTrack.ToString(),
                    ["onPitRoad"] = sample.OnPitRoad.ToString()
                });
        }

        if (!IsValidFuel(_previousSample.FuelLevelLiters) || !IsValidFuel(sample.FuelLevelLiters))
        {
            if (!IsValidFuel(_previousSample.FuelLevelLiters)
                && IsValidFuel(sample.FuelLevelLiters)
                && IsTeamCarInGreenStint(sample))
            {
                EmitOnce(
                    observations,
                    "fuel.scalar-returned-with-team-progress",
                    TelemetryEdgeCaseSeverity.Info,
                    "Scalar fuel returned while team-car progress was available.",
                    sample,
                    new Dictionary<string, string?>
                    {
                        ["fuelLevelLiters"] = Format(sample.FuelLevelLiters)
                    });
            }

            return;
        }

        var fuelDelta = sample.FuelLevelLiters - _previousSample.FuelLevelLiters;
        var pitContext = IsPitContext(_previousSample, sample);
        if (fuelDelta > 0.35d && !pitContext)
        {
            EmitOnce(
                observations,
                "fuel.added-outside-pit-context",
                TelemetryEdgeCaseSeverity.Warning,
                "Fuel level increased outside pit/service context.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousFuelLiters"] = Format(_previousSample.FuelLevelLiters),
                    ["currentFuelLiters"] = Format(sample.FuelLevelLiters),
                    ["deltaLiters"] = Format(fuelDelta)
                });
        }

        var maximumExpectedBurn = Math.Max(0.05d, Math.Max(deltaSeconds, 0d) * 0.15d);
        if (fuelDelta < -maximumExpectedBurn && !pitContext)
        {
            EmitOnce(
                observations,
                "fuel.drop-spike",
                TelemetryEdgeCaseSeverity.Warning,
                "Fuel level dropped faster than expected outside pit/service context.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousFuelLiters"] = Format(_previousSample.FuelLevelLiters),
                    ["currentFuelLiters"] = Format(sample.FuelLevelLiters),
                    ["deltaLiters"] = Format(fuelDelta),
                    ["deltaSeconds"] = Format(deltaSeconds)
                });
        }
    }

    private void DetectTireAndServiceAnomalies(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (_previousSample is not null)
        {
            var pitContext = IsPitContext(_previousSample, sample);
            if (sample.TireSetsUsed is { } currentTireSets
                && _previousSample.TireSetsUsed is { } previousTireSets
                && currentTireSets > previousTireSets
                && !pitContext)
            {
                var startupContext = previousTireSets == 0 && IsStartupGridOrTowContext(_previousSample, sample);
                EmitOnce(
                    observations,
                    startupContext ? "tires.set-count-initialized" : "tires.set-count-increased-outside-pit-context",
                    startupContext ? TelemetryEdgeCaseSeverity.Info : TelemetryEdgeCaseSeverity.Warning,
                    startupContext
                        ? "Tire set counter initialized during grid, pit, tow, or collection startup context."
                        : "Tire set counter increased outside pit/service context.",
                    sample,
                    new Dictionary<string, string?>
                    {
                        ["previousTireSetsUsed"] = previousTireSets.ToString(),
                        ["currentTireSetsUsed"] = currentTireSets.ToString(),
                        ["previousIsOnTrack"] = _previousSample.IsOnTrack.ToString(),
                        ["currentIsOnTrack"] = sample.IsOnTrack.ToString(),
                        ["onPitRoad"] = sample.OnPitRoad.ToString(),
                        ["teamOnPitRoad"] = sample.TeamOnPitRoad?.ToString(),
                        ["playerCarInPitStall"] = sample.PlayerCarInPitStall.ToString()
                    });
            }

            if (sample.FastRepairUsed is { } currentFastRepair
                && _previousSample.FastRepairUsed is { } previousFastRepair
                && currentFastRepair > previousFastRepair
                && !pitContext)
            {
                var startupContext = previousFastRepair == 0 && IsStartupGridOrTowContext(_previousSample, sample);
                EmitOnce(
                    observations,
                    startupContext ? "repair.fast-repair-initialized" : "repair.fast-repair-used-outside-pit-context",
                    startupContext ? TelemetryEdgeCaseSeverity.Info : TelemetryEdgeCaseSeverity.Warning,
                    startupContext
                        ? "Fast repair counter initialized during grid, pit, tow, or collection startup context."
                        : "Fast repair counter increased outside pit/service context.",
                    sample,
                    new Dictionary<string, string?>
                    {
                        ["previousFastRepairUsed"] = previousFastRepair.ToString(),
                        ["currentFastRepairUsed"] = currentFastRepair.ToString(),
                        ["previousIsOnTrack"] = _previousSample.IsOnTrack.ToString(),
                        ["currentIsOnTrack"] = sample.IsOnTrack.ToString(),
                        ["onPitRoad"] = sample.OnPitRoad.ToString(),
                        ["teamOnPitRoad"] = sample.TeamOnPitRoad?.ToString(),
                        ["playerCarInPitStall"] = sample.PlayerCarInPitStall.ToString()
                    });
            }
        }

        var activePitCommands = WatchedNamesForGroup(raw, "pit.commands")
            .Select(name => new { Name = name, Value = raw.Get(name) })
            .Where(row => row.Value is { } value && Math.Abs(value) > 0.0001d)
            .ToArray();
        if (activePitCommands.Length > 0)
        {
            var fields = activePitCommands
                .ToDictionary(row => row.Name, row => (string?)Format(row.Value), StringComparer.OrdinalIgnoreCase);
            fields["variables"] = string.Join(",", activePitCommands.Select(row => row.Name));
            AddPitCommandContext(fields, sample, raw);
            EmitOnce(
                observations,
                "raw.pit-commands.active",
                TelemetryEdgeCaseSeverity.Info,
                "Raw pit-service command channels became active.",
                sample,
                fields);
        }

        foreach (var name in WatchedNamesForGroup(raw, "pit.commands"))
        {
            var previous = _previousRaw.Get(name);
            var current = raw.Get(name);
            if (previous is null
                || current is null
                || Math.Abs(current.Value - previous.Value) <= 0.0001d)
            {
                continue;
            }

            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["variable"] = name,
                ["previousValue"] = Format(previous),
                ["currentValue"] = Format(current)
            };
            AddPitCommandContext(fields, sample, raw);
            EmitOnce(
                observations,
                $"raw.pit-command.changed.{name}.{current.Value:0.###}",
                TelemetryEdgeCaseSeverity.Info,
                "A raw pit-service request channel changed.",
                sample,
                fields);
        }

        DetectTireWearReset(sample, raw, observations);
    }

    private void DetectTireWearReset(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (_previousSample is null)
        {
            return;
        }

        var pitContext = IsPitContext(_previousSample, sample);
        foreach (var name in RawTelemetryWatchVariables.Groups["tires.wear"])
        {
            var previous = _previousRaw.Get(name);
            var current = raw.Get(name);
            if (previous is null || current is null)
            {
                continue;
            }

            var increase = current.Value - previous.Value;
            if (increase < TireWearResetPercent)
            {
                continue;
            }

            EmitOnce(
                observations,
                pitContext ? $"raw.tire-wear-reset-in-pit.{name}" : $"raw.tire-wear-reset-outside-pit.{name}",
                pitContext ? TelemetryEdgeCaseSeverity.Info : TelemetryEdgeCaseSeverity.Warning,
                pitContext
                    ? "Tire wear remaining increased during pit/service context."
                    : "Tire wear remaining increased outside pit/service context.",
                sample,
                new Dictionary<string, string?>
                {
                    ["variable"] = name,
                    ["previousValue"] = Format(previous),
                    ["currentValue"] = Format(current),
                    ["increase"] = Format(increase)
                });
        }
    }

    private void DetectDriverChangeSignals(
        HistoricalTelemetrySample sample,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (_previousSample is null)
        {
            return;
        }

        if (sample.DriversSoFar is { } driversSoFar
            && _previousSample.DriversSoFar is { } previousDriversSoFar
            && driversSoFar > previousDriversSoFar)
        {
            EmitOnce(
                observations,
                $"driver-change.drivers-so-far.{driversSoFar}",
                TelemetryEdgeCaseSeverity.Info,
                "DCDriversSoFar increased.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousDriversSoFar"] = previousDriversSoFar.ToString(),
                    ["currentDriversSoFar"] = driversSoFar.ToString(),
                    ["driverChangeLapStatus"] = sample.DriverChangeLapStatus?.ToString(),
                    ["teamOnPitRoad"] = sample.TeamOnPitRoad?.ToString()
                });
        }

        if (sample.DriverChangeLapStatus != _previousSample.DriverChangeLapStatus)
        {
            EmitOnce(
                observations,
                $"driver-change.lap-status.{sample.DriverChangeLapStatus}",
                TelemetryEdgeCaseSeverity.Info,
                "DCLapStatus changed.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousLapStatus"] = _previousSample.DriverChangeLapStatus?.ToString(),
                    ["currentLapStatus"] = sample.DriverChangeLapStatus?.ToString()
                });
        }
    }

    private void DetectWeatherTransitions(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (_previousSample is not null && sample.TrackWetness != _previousSample.TrackWetness)
        {
            EmitOnce(
                observations,
                $"weather.track-wetness.{sample.TrackWetness}",
                TelemetryEdgeCaseSeverity.Info,
                "TrackWetness changed.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousTrackWetness"] = _previousSample.TrackWetness.ToString(),
                    ["currentTrackWetness"] = sample.TrackWetness.ToString()
                });
        }

        if (_previousSample is not null && sample.WeatherDeclaredWet != _previousSample.WeatherDeclaredWet)
        {
            EmitOnce(
                observations,
                $"weather.declared-wet.{sample.WeatherDeclaredWet}",
                TelemetryEdgeCaseSeverity.Info,
                "WeatherDeclaredWet changed.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousDeclaredWet"] = _previousSample.WeatherDeclaredWet.ToString(),
                    ["currentDeclaredWet"] = sample.WeatherDeclaredWet.ToString()
                });
        }

        if (raw.Get("Precipitation") is { } precipitation && precipitation > 0.0001d)
        {
            EmitOnce(
                observations,
                "weather.precipitation-active",
                TelemetryEdgeCaseSeverity.Info,
                "Raw precipitation channel became active.",
                sample,
                new Dictionary<string, string?>
                {
                    ["precipitation"] = Format(precipitation),
                    ["trackWetness"] = sample.TrackWetness.ToString(),
                    ["declaredWet"] = sample.WeatherDeclaredWet.ToString()
                });
        }

        var forecastValues = raw.Values
            .Where(pair => RawTelemetryWatchVariables.GroupFor(pair.Key) == "weather.forecast"
                && Math.Abs(pair.Value) > 0.0001d)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        if (forecastValues.Length > 0)
        {
            EmitOnce(
                observations,
                "weather.forecast-signal-present",
                TelemetryEdgeCaseSeverity.Info,
                "A raw forecast-like weather channel was present and active.",
                sample,
                forecastValues.ToDictionary(
                    pair => pair.Key,
                    pair => (string?)Format(pair.Value),
                    StringComparer.OrdinalIgnoreCase));
        }
    }

    private void DetectRawEngineeringChannels(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (_previousSample is null)
        {
            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in new[] { "tires.wear", "tires.temperature", "tires.pressure", "tires.odometer", "suspension", "brakes", "wheels" })
            {
                var active = ActiveRawValues(group, raw);
                if (active.Count == 0)
                {
                    continue;
                }

                fields[$"{group}.count"] = active.Count.ToString();
                foreach (var pair in active.Take(3))
                {
                    fields[$"{group}.{pair.Key}"] = pair.Value;
                }
            }

            if (fields.Count > 0)
            {
                EmitOnce(
                    observations,
                    "raw.startup-engineering-baseline",
                    TelemetryEdgeCaseSeverity.Info,
                    "Raw engineering channels were already populated at collection start.",
                    sample,
                    fields);
            }

            return;
        }

        DetectRawGroupActive(sample, "tires.wear", raw, "Raw tire wear channels became finite/non-zero.", observations);
        DetectRawGroupActive(sample, "tires.temperature", raw, "Raw tire temperature channels became finite/non-zero.", observations);
        DetectRawGroupActive(sample, "tires.pressure", raw, "Raw tire pressure channels became finite/non-zero.", observations);
        DetectRawGroupActive(sample, "tires.odometer", raw, "Raw tire odometer channels became finite/non-zero.", observations);
        DetectRawGroupActive(sample, "suspension", raw, "Raw shock deflection/velocity channels became finite/non-zero.", observations);
        DetectRawGroupActive(sample, "brakes", raw, "Raw brake pressure channels became finite/non-zero.", observations);
        DetectRawGroupActive(sample, "wheels", raw, "Raw wheel-speed channels became finite/non-zero.", observations);
    }

    private void DetectRawStateTransitions(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        List<TelemetryEdgeCaseObservation> observations)
    {
        DetectRawChanged(
            sample,
            raw,
            "SessionState",
            "raw.session-state.changed",
            TelemetryEdgeCaseSeverity.Info,
            "Raw SessionState changed.",
            observations);
        DetectRawChanged(
            sample,
            raw,
            "PlayerTrackSurface",
            "raw.player-track-surface.changed",
            TelemetryEdgeCaseSeverity.Info,
            "Raw PlayerTrackSurface changed.",
            observations);
        DetectRawChanged(
            sample,
            raw,
            "EnterExitReset",
            "raw.active-reset.action-changed",
            TelemetryEdgeCaseSeverity.Info,
            "Raw EnterExitReset changed.",
            observations);

        if (raw.Get("EnterExitReset") is { } resetAction && Math.Abs(resetAction - 2d) <= 0.0001d)
        {
            EmitOnce(
                observations,
                "raw.active-reset.reset-key-action",
                TelemetryEdgeCaseSeverity.Info,
                "Raw EnterExitReset indicated the reset key would reset the car.",
                sample,
                new Dictionary<string, string?>
                {
                    ["enterExitReset"] = Format(resetAction),
                    ["playerCarTowTime"] = Format(raw.Get("PlayerCarTowTime")),
                    ["lapCompleted"] = sample.LapCompleted.ToString(),
                    ["lapDistPct"] = Format(sample.LapDistPct),
                    ["teamLapCompleted"] = sample.TeamLapCompleted?.ToString(),
                    ["teamLapDistPct"] = Format(sample.TeamLapDistPct)
                });
        }

        if (raw.Get("PlayerCarTowTime") is { } towTime && towTime > 0d)
        {
            EmitOnce(
                observations,
                "raw.tow.active",
                TelemetryEdgeCaseSeverity.Info,
                "PlayerCarTowTime was active.",
                sample,
                new Dictionary<string, string?>
                {
                    ["playerCarTowTime"] = Format(towTime),
                    ["enterExitReset"] = Format(raw.Get("EnterExitReset")),
                    ["isOnTrack"] = sample.IsOnTrack.ToString(),
                    ["isInGarage"] = sample.IsInGarage.ToString(),
                    ["onPitRoad"] = sample.OnPitRoad.ToString()
                });
        }

        var previousFlags = _previousRaw.Get("SessionFlags");
        var currentFlags = raw.Get("SessionFlags");
        if (currentFlags is { } flags
            && Math.Abs(flags) > 0.0001d
            && (previousFlags is null || Math.Abs(previousFlags.Value - flags) > 0.0001d))
        {
            EmitOnce(
                observations,
                $"raw.session-flags.{flags:0}",
                TelemetryEdgeCaseSeverity.Info,
                "Raw SessionFlags became active or changed.",
                sample,
                new Dictionary<string, string?>
                {
                    ["previousSessionFlags"] = Format(previousFlags),
                    ["currentSessionFlags"] = Format(flags)
                });
        }

        foreach (var name in RawTelemetryWatchVariables.Groups["incidents"])
        {
            var previous = _previousRaw.Get(name);
            var current = raw.Get(name);
            if (previous is null || current is null || current.Value <= previous.Value)
            {
                continue;
            }

            EmitOnce(
                observations,
                $"raw.incident-count-increased.{name}.{current.Value:0}",
                TelemetryEdgeCaseSeverity.Info,
                "A raw incident counter increased.",
                sample,
                new Dictionary<string, string?>
                {
                    ["variable"] = name,
                    ["previousValue"] = Format(previous),
                    ["currentValue"] = Format(current)
                });
        }

        foreach (var name in WatchedNamesForGroup(raw, "driver.controls"))
        {
            var previous = _previousRaw.Get(name);
            var current = raw.Get(name);
            if (previous is null
                || current is null
                || Math.Abs(current.Value - previous.Value) <= 0.0001d)
            {
                continue;
            }

            EmitOnce(
                observations,
                $"raw.driver-control.changed.{name}",
                TelemetryEdgeCaseSeverity.Info,
                "A raw in-car control channel changed.",
                sample,
                new Dictionary<string, string?>
                {
                    ["variable"] = name,
                    ["previousValue"] = Format(previous),
                    ["currentValue"] = Format(current)
                });
        }
    }

    private void DetectRawChanged(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        string variableName,
        string key,
        string severity,
        string summary,
        List<TelemetryEdgeCaseObservation> observations)
    {
        var previous = _previousRaw.Get(variableName);
        var current = raw.Get(variableName);
        if (previous is null
            || current is null
            || Math.Abs(current.Value - previous.Value) <= 0.0001d)
        {
            return;
        }

        EmitOnce(
            observations,
            key,
            severity,
            summary,
            sample,
            new Dictionary<string, string?>
            {
                ["variable"] = variableName,
                ["previousValue"] = Format(previous),
                ["currentValue"] = Format(current)
            });
    }

    private void DetectRawGroupActive(
        HistoricalTelemetrySample sample,
        string group,
        RawTelemetryWatchSnapshot raw,
        string summary,
        List<TelemetryEdgeCaseObservation> observations)
    {
        var active = ActiveRawValues(group, raw);
        if (active.Count == 0)
        {
            return;
        }

        EmitOnce(
            observations,
            $"raw.{group}.active",
            TelemetryEdgeCaseSeverity.Info,
            summary,
            sample,
            active.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, string> ActiveRawValues(
        string group,
        RawTelemetryWatchSnapshot raw)
    {
        return WatchedNamesForGroup(raw, group)
            .Select(name => new { Name = name, Value = raw.Get(name) })
            .Where(row => row.Value is { } value && Math.Abs(value) > 0.0001d)
            .Take(12)
            .ToDictionary(row => row.Name, row => row.Value!.Value.ToString("0.###"), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> WatchedNamesForGroup(
        RawTelemetryWatchSnapshot raw,
        string group)
    {
        return RawTelemetryWatchVariables.Groups[group]
            .Concat(raw.Values.Keys.Where(name => string.Equals(
                raw.GroupFor(name),
                group,
                StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddPitCommandContext(
        Dictionary<string, string?> fields,
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw)
    {
        fields["isOnTrack"] = sample.IsOnTrack.ToString();
        fields["isInGarage"] = sample.IsInGarage.ToString();
        fields["onPitRoad"] = sample.OnPitRoad.ToString();
        fields["teamOnPitRoad"] = sample.TeamOnPitRoad?.ToString();
        fields["playerCarInPitStall"] = sample.PlayerCarInPitStall.ToString();
        fields["isReplayPlaying"] = Format(raw.Get("IsReplayPlaying"));
        fields["isOnTrackCar"] = Format(raw.Get("IsOnTrackCar"));
        fields["camCarIdx"] = Format(raw.Get("CamCarIdx"));
        fields["camGroupNumber"] = Format(raw.Get("CamGroupNumber"));
        fields["camCameraNumber"] = Format(raw.Get("CamCameraNumber"));
        fields["camCameraState"] = Format(raw.Get("CamCameraState"));
    }

    private void DetectRawRuntimeWarnings(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw,
        List<TelemetryEdgeCaseObservation> observations)
    {
        if (raw.Get("EngineWarnings") is { } engineWarnings && engineWarnings != 0d)
        {
            var startupContext = IsStartupGridOrTowContext(_previousSample, sample);
            var engineOffContext = IsEngineOffContext(sample, raw);
            EmitOnce(
                observations,
                engineOffContext
                    ? "raw.engine-warning.engine-off"
                    : startupContext
                        ? "raw.engine-warning.startup"
                        : $"raw.engine-warning.{engineWarnings:0}",
                engineOffContext || startupContext
                    ? TelemetryEdgeCaseSeverity.Info
                    : TelemetryEdgeCaseSeverity.Warning,
                engineOffContext
                    ? "EngineWarnings was non-zero while the engine appeared off or not pressurized."
                    : startupContext
                        ? "EngineWarnings was non-zero during grid, pit, tow, or collection startup context."
                        : "EngineWarnings became non-zero.",
                sample,
                new Dictionary<string, string?>
                {
                    ["engineWarnings"] = Format(engineWarnings),
                    ["rpm"] = Format(raw.Get("RPM")),
                    ["oilTemp"] = Format(raw.Get("OilTemp")),
                    ["oilPress"] = Format(raw.Get("OilPress")),
                    ["waterTemp"] = Format(raw.Get("WaterTemp")),
                    ["waterLevel"] = Format(raw.Get("WaterLevel")),
                    ["voltage"] = Format(raw.Get("Voltage")),
                    ["isOnTrack"] = sample.IsOnTrack.ToString(),
                    ["speedMetersPerSecond"] = Format(sample.SpeedMetersPerSecond)
                });
        }

        if (raw.Get("IsReplayPlaying") is { } replayPlaying && replayPlaying > 0.5d)
        {
            var startupContext = IsStartupGridOrTowContext(_previousSample, sample);
            EmitOnce(
                observations,
                startupContext ? "raw.replay-playing-startup" : "raw.replay-playing-during-collection",
                startupContext ? TelemetryEdgeCaseSeverity.Info : TelemetryEdgeCaseSeverity.Warning,
                startupContext
                    ? "Replay playback was active near collection start, which can happen when iRacing is a few seconds behind live coverage."
                    : "Replay playback was active during telemetry collection.",
                sample,
                new Dictionary<string, string?>
                {
                    ["isReplayPlaying"] = Format(replayPlaying),
                    ["isOnTrack"] = sample.IsOnTrack.ToString(),
                    ["onPitRoad"] = sample.OnPitRoad.ToString(),
                    ["sessionState"] = sample.SessionState?.ToString()
                });
        }

        if (sample.IsOnTrack && raw.Get("FrameRate") is { } frameRate && frameRate > 0d && frameRate < 45d)
        {
            EmitOnce(
                observations,
                "raw.system.low-frame-rate",
                TelemetryEdgeCaseSeverity.Info,
                "FrameRate dropped below the watch threshold while on track.",
                sample,
                new Dictionary<string, string?>
                {
                    ["frameRate"] = Format(frameRate),
                    ["cpuUsageFG"] = Format(raw.Get("CpuUsageFG")),
                    ["gpuUsage"] = Format(raw.Get("GpuUsage"))
                });
        }

        if (raw.Get("ChanLatency") is { } channelLatency && channelLatency > 0.35d)
        {
            EmitOnce(
                observations,
                "raw.network.high-latency",
                TelemetryEdgeCaseSeverity.Info,
                "iRacing channel latency exceeded the watch threshold.",
                sample,
                new Dictionary<string, string?>
                {
                    ["chanLatency"] = Format(channelLatency),
                    ["chanAvgLatency"] = Format(raw.Get("ChanAvgLatency")),
                    ["chanQuality"] = Format(raw.Get("ChanQuality")),
                    ["chanPartnerQuality"] = Format(raw.Get("ChanPartnerQuality"))
                });
        }
    }

    private void EmitOnce(
        List<TelemetryEdgeCaseObservation> observations,
        string key,
        string severity,
        string summary,
        HistoricalTelemetrySample sample,
        IReadOnlyDictionary<string, string?> fields)
    {
        if (!_emittedOnce.Add(key))
        {
            return;
        }

        observations.Add(new TelemetryEdgeCaseObservation(
            Key: key,
            Severity: severity,
            Summary: summary,
            DetectedAtUtc: sample.CapturedAtUtc,
            SessionTime: IsFinite(sample.SessionTime) ? sample.SessionTime : null,
            SessionTick: sample.SessionTick,
            Fields: fields));
    }

    private static bool HasSideOccupancy(int? carLeftRight)
    {
        return carLeftRight is 2 or 3 or 4 or 5 or 6;
    }

    private static bool IsTeamCarInGreenStint(HistoricalTelemetrySample sample)
    {
        return sample.IsOnTrack
            && sample.IsInGarage == false
            && sample.TeamOnPitRoad != true
            && sample.SpeedMetersPerSecond > 5d
            && sample.TeamLapCompleted is >= 0
            && sample.TeamLapDistPct is { } pct
            && IsFinite(pct)
            && pct >= 0d;
    }

    private static bool IsPitContext(HistoricalTelemetrySample previous, HistoricalTelemetrySample current)
    {
        return previous.OnPitRoad
            || current.OnPitRoad
            || previous.TeamOnPitRoad == true
            || current.TeamOnPitRoad == true
            || previous.PitstopActive
            || current.PitstopActive
            || previous.PlayerCarInPitStall
            || current.PlayerCarInPitStall;
    }

    private static LapProgress? LocalLapProgress(HistoricalTelemetrySample sample)
    {
        if (sample.TeamLapDistPct is { } teamPct
            && IsFinite(teamPct)
            && teamPct >= 0d
            && teamPct <= 1.000001d)
        {
            int? teamLapCompleted = sample.TeamLapCompleted is >= 0 ? sample.TeamLapCompleted.Value : null;
            return new LapProgress(
                "team",
                teamLapCompleted,
                Math.Clamp(teamPct, 0d, 1d));
        }

        if (IsFinite(sample.LapDistPct)
            && sample.LapDistPct >= 0d
            && sample.LapDistPct <= 1.000001d)
        {
            int? lapCompleted = sample.LapCompleted >= 0 ? sample.LapCompleted : null;
            return new LapProgress(
                "scalar",
                lapCompleted,
                Math.Clamp(sample.LapDistPct, 0d, 1d));
        }

        return null;
    }

    private static double LapProgressDelta(LapProgress previous, LapProgress current)
    {
        if (previous.LapCompleted is { } previousLapCompleted
            && current.LapCompleted is { } currentLapCompleted)
        {
            return currentLapCompleted + current.LapDistPct - (previousLapCompleted + previous.LapDistPct);
        }

        return previous.LapDistPct >= LapWrapPreviousThreshold && current.LapDistPct <= LapWrapCurrentThreshold
            ? 1d - previous.LapDistPct + current.LapDistPct
            : current.LapDistPct - previous.LapDistPct;
    }

    private static string ProgressDiscontinuityContext(
        HistoricalTelemetrySample previous,
        HistoricalTelemetrySample current,
        RawTelemetryWatchSnapshot raw)
    {
        if (raw.Get("EnterExitReset") is { } resetAction && Math.Abs(resetAction - 2d) <= 0.0001d)
        {
            return "active-reset";
        }

        if (raw.Get("PlayerCarTowTime") is { } towTime && towTime > 0d)
        {
            return "tow";
        }

        if (previous.IsInGarage || current.IsInGarage)
        {
            return "garage";
        }

        if (!previous.IsOnTrack || !current.IsOnTrack)
        {
            return "off-track-or-replay";
        }

        if (IsPitContext(previous, current))
        {
            return "pit-or-tow";
        }

        return "unknown";
    }

    private static bool IsStartupGridOrTowContext(
        HistoricalTelemetrySample? previous,
        HistoricalTelemetrySample current)
    {
        return StartupGridOrTowContext(previous, current) is not null;
    }

    private static string? StartupGridOrTowContext(
        HistoricalTelemetrySample? previous,
        HistoricalTelemetrySample current)
    {
        if (previous is null)
        {
            return "collection-start";
        }

        if (previous.IsInGarage || current.IsInGarage)
        {
            return "garage";
        }

        if (!previous.IsOnTrack || !current.IsOnTrack)
        {
            return "off-track-or-replay";
        }

        if (previous.OnPitRoad
            || current.OnPitRoad
            || previous.TeamOnPitRoad == true
            || current.TeamOnPitRoad == true
            || previous.PlayerCarInPitStall
            || current.PlayerCarInPitStall)
        {
            return "pit-or-tow";
        }

        if (current.SessionState is { } state && state < RacingSessionState)
        {
            return current.SpeedMetersPerSecond < 2d
                ? "stationary-grid"
                : "grid-or-parade";
        }

        if (current.LapCompleted <= 0
            && current.LapDistPct >= 0d
            && current.LapDistPct <= StartLapDistanceWindow)
        {
            return current.SpeedMetersPerSecond < 2d
                ? "stationary-start-grid"
                : "race-start";
        }

        return null;
    }

    private static bool IsEngineOffContext(
        HistoricalTelemetrySample sample,
        RawTelemetryWatchSnapshot raw)
    {
        return raw.Get("RPM") is { } rpm && rpm <= EngineOffRpmThreshold
            || raw.Get("OilPress") is { } oilPress && oilPress <= EngineOffOilPressureThreshold
            || (!sample.IsOnTrack && sample.SpeedMetersPerSecond < 1d);
    }

    private static double? FocusLapDistPct(HistoricalTelemetrySample sample)
    {
        if (sample.FocusLapDistPct is { } focusPct && IsFinite(focusPct) && focusPct >= 0d)
        {
            return Math.Clamp(focusPct, 0d, 1d);
        }

        if (FocusIsPlayer(sample) && IsFinite(sample.LapDistPct) && sample.LapDistPct >= 0d)
        {
            return Math.Clamp(sample.LapDistPct, 0d, 1d);
        }

        return null;
    }

    private static double? FocusF2TimeSeconds(HistoricalTelemetrySample sample)
    {
        return FirstNonNegativeFinite(
            sample.FocusF2TimeSeconds,
            FocusIsPlayer(sample) ? sample.TeamF2TimeSeconds : null);
    }

    private static double? FocusEstimatedTimeSeconds(HistoricalTelemetrySample sample)
    {
        return FirstNonNegativeFinite(
            sample.FocusEstimatedTimeSeconds,
            FocusIsPlayer(sample) ? sample.TeamEstimatedTimeSeconds : null);
    }

    private static double? LiveLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        return FirstPositiveFinite(
            sample.FocusLastLapTimeSeconds,
            sample.FocusBestLapTimeSeconds,
            sample.TeamLastLapTimeSeconds,
            sample.TeamBestLapTimeSeconds,
            sample.LapLastLapTimeSeconds,
            sample.LapBestLapTimeSeconds);
    }

    private static bool FocusIsPlayer(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is null
            || sample.PlayerCarIdx is null
            || sample.FocusCarIdx == sample.PlayerCarIdx;
    }

    private static double RelativeLaps(double carLapDistPct, double focusLapDistPct)
    {
        var relativeLaps = carLapDistPct - focusLapDistPct;
        if (relativeLaps > 0.5d)
        {
            relativeLaps -= 1d;
        }
        else if (relativeLaps < -0.5d)
        {
            relativeLaps += 1d;
        }

        return relativeLaps;
    }

    private static bool IsValidFuel(double value)
    {
        return IsPositiveFinite(value);
    }

    private static double? FirstNonNegativeFinite(params double?[] values)
    {
        foreach (var value in values)
        {
            if (IsNonNegativeFinite(value))
            {
                return value;
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

    private static bool IsNonNegativeFinite(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d;
    }

    private static bool IsPositiveFinite(double? value)
    {
        return value is { } number && IsFinite(number) && number > 0d;
    }

    private static bool IsNearZero(double? value)
    {
        return value is { } number && IsFinite(number) && Math.Abs(number) <= SuspiciousZeroTimingSeconds;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string? Format(double? value)
    {
        return value is { } number && IsFinite(number) ? number.ToString("0.###") : null;
    }
}

internal static class TelemetryEdgeCaseSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

internal sealed record TelemetryEdgeCaseObservation(
    string Key,
    string Severity,
    string Summary,
    DateTimeOffset DetectedAtUtc,
    double? SessionTime,
    int SessionTick,
    IReadOnlyDictionary<string, string?> Fields);

internal sealed record LapProgress(
    string Source,
    int? LapCompleted,
    double LapDistPct);
