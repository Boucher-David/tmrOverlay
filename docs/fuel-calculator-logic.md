# Fuel Calculator Logic

This file explains how the fuel calculator derives strategy numbers and display rows.

Implementation files:

- `src/TmrOverlay.Core/Telemetry/Live/LiveTelemetrySnapshot.cs`
- `src/TmrOverlay.Core/Telemetry/Live/LiveRaceModels.cs`
- `src/TmrOverlay.Core/Telemetry/Live/LiveRaceProgressProjector.cs`
- `src/TmrOverlay.Core/Telemetry/Live/LiveRaceProjectionTracker.cs`
- `src/TmrOverlay.Core/Fuel/FuelStrategyCalculator.cs`
- `src/TmrOverlay.App/Overlays/FuelCalculator/FuelCalculatorViewModel.cs`
- `src/TmrOverlay.App/Overlays/FuelCalculator/FuelCalculatorForm.cs`
- `src/TmrOverlay.App/History/SessionHistoryQueryService.cs`

## Purpose

The fuel calculator estimates:

- Fuel currently available.
- Fuel burn per lap.
- Race laps remaining.
- Planned stint count.
- Planned stop count.
- Whole-lap stint targets.
- Final stint length.
- Required fuel saving for target stints.
- Whether a longer rhythm avoids stops.
- Whether tire service is likely free under refueling time.

It uses model-v2 live telemetry first, then exact user history for the same car/track/session combo, then optional baseline history only when baseline lookup is enabled.

## Refresh Loop

The Windows fuel overlay refreshes once per second.

Each refresh:

1. Reads the latest `LiveTelemetrySnapshot`.
2. Skips the expensive path when the live snapshot sequence and relevant display options are unchanged.
3. Verifies the local active driver/team strategy context through `LiveLocalStrategyContext`.
4. Native overlay management hides the window before refresh when focus is unavailable, focus is another car, the player car is unavailable, the user is in garage/setup context, or there is no active local/pit context. Browser/model callers still receive a waiting state for the same condition instead of strategy rows.
5. Looks up exact combo history with a 30 second cache.
6. Builds a `FuelStrategySnapshot`.
7. Converts the snapshot to display text and rows.
8. Applies status color, source visibility, advice-column visibility, and row text only when the target UI value changed.
9. Keeps all six stint rows visible, using blank future rows when fewer rows are available.
10. Records whether the timer tick saw new input, how old that input was, and whether the tick actually changed UI state.

## Model V2 Inputs

Fuel V2 separates reusable live race facts from fuel strategy math.

The calculator builds `FuelStrategyInputs` from:

- `LiveFuelPitModel`: current fuel level/percent, fuel burn projection, fuel confidence, and pit/service signals.
- `LiveSessionModel`: session state, remaining clock, and lap limits.
- `LiveRaceProgressModel`: strategy-car progress, reference progress, leader progress, lap gaps, race pace, positions, and race-laps-remaining estimates.
- `LiveRaceProjectionModel`: rolling clean-lap pace and timed-race lap projections for overall leader, reference class, and team strategy.
- `HistoricalSessionAggregate`: fuel burn history, lap history, tank metadata fallback, teammate stint targets, fill-rate evidence, pit lane time, and tire-service estimates.

`LiveRaceProgressProjector` remains the frame-local compatibility layer. `LiveRaceProjectionTracker` owns the stateful rolling clean-lap calculation so Fuel, Session / Weather, Flags, Standings class separators, future Pit Service suggested-refuel rows, and strategy overlays do not each recalculate timed-race finish laps differently. It requires a small clean-lap window before overriding session lap fields and ignores yellow/caution, non-racing, pit-road team laps, and obvious pace outliers.

## Live Fuel Snapshot

`LiveFuelSnapshot.From` derives basic live fuel values from the latest sample.

If fuel level in liters is missing or not positive, live fuel is unavailable.

If fuel level exists:

1. Store fuel level liters.
2. Store fuel level percent when finite and non-negative.
3. Store fuel use per hour in kg when positive.
4. Convert fuel use per hour from kg to liters:
   - `fuelUsePerHourLiters = fuelUsePerHourKg / DriverCarFuelKgPerLiter`
5. Select lap time:
   - Team last lap.
   - Player last lap.
   - Driver estimated lap time.
   - Class estimated lap time.
   - Unavailable.
6. Convert burn to fuel per lap:
   - `fuelPerLapLiters = fuelUsePerHourLiters * lapTimeSeconds / 3600`
7. Estimate minutes remaining on current fuel:
   - `fuelLevelLiters / fuelUsePerHourLiters * 60`
8. Estimate laps remaining on current fuel:
   - `fuelLevelLiters / fuelPerLapLiters`

Live fuel confidence is:

- `live` when burn exists.
- `level-only` when fuel level exists but live burn does not.
- `none` when fuel level is unavailable.

Data review note from the May 2026 capture analysis:

- `FuelUsePerHour` is not stable enough to treat as lap-average burn by itself. In the 4-hour Nürburgring raw capture, sampled valid-level `FuelUsePerHour` commonly implied about 18-19 L/lap even though the historical fuel-delta baseline for the same combo is about 13.4 L/lap.
- Some frames expose positive `FuelUsePerHour` while `FuelLevel` is zero or unavailable. That must not become a measured fuel baseline.
- Future live fuel work should prefer a rolling measured fuel-level delta over valid green-flag distance/time, using the instantaneous burn channel as an activity/diagnostic signal or short-term hint only after smoothing and confidence checks.

For V1, the fuel calculator is local active driver/team context only. It does not display modeled strategy while the camera is focused on another car, while focus is unavailable, or while garage/setup context is active, even if historical data exists. In native overlay mode, that means the enabled overlay remains hidden until context is valid. Once local context is valid, history can still fill burn-rate/stint modeling gaps when live scalar fuel or live burn is unavailable, and the source text labels that as historical/model rather than live measured fuel.

## History Lookup

The fuel overlay queries `SessionHistoryQueryService` by the exact `HistoricalComboIdentity`.

The lookup returns:

- User aggregate if available.
- Baseline aggregate only when baseline history is enabled.
- Preferred aggregate is user first, baseline second.

History contributes:

- Mean/min/max fuel per lap.
- Mean fuel per hour.
- Median and average lap time.
- Fuel tank size fallback.
- Teammate stint length evidence.
- Pit lane time.
- Pit service time.
- Tire-change service time.
- No-tire service time.
- Observed fuel fill rate.

## Fuel Per Lap Selection

The calculator selects fuel per lap in this order:

1. Live fuel per lap from `LiveFuelPitModel.Fuel`.
2. Preferred history aggregate mean fuel per lap.
3. Unavailable.

When live fuel per lap is selected, history min/max can still appear as context in the source row.

Planned hardening:

- Add a live measured-burn estimator based on fuel-level deltas over valid on-track distance/time.
- Require minimum evidence windows before measured live burn can drive stint targets.
- Reject samples around pits, refuels, session resets, zero/unavailable fuel levels, and implausible fuel deltas.
- Keep `FuelUsePerHour` out of primary strategy selection until it is smoothed and agrees with measured fuel-level behavior.

## Lap Time Selection

The calculator selects strategy lap time in this order:

1. Live fuel/pit model lap time.
2. Live race-progress strategy lap time when it is based on a completed team/player lap.
3. History median lap.
4. History average lap.
5. Driver estimated lap time from live context.
6. Driver estimated lap time from history aggregate car metadata.
7. Live race-progress strategy lap time when only an estimate is available.
8. Unavailable.

Valid lap times must be finite and between 20 and 1800 seconds.

## Race Pace Selection

Race pace is used for timed race lap estimation.

Selection order:

1. Overall leader last lap.
2. Class leader last lap.
3. Overall leader best lap.
4. Class leader best lap.
5. Team strategy lap time.
6. Unavailable.

## Race Laps Remaining

`LiveRaceProgressProjector` estimates race laps remaining in this order:

1. If session state indicates ended, return `0`.
2. If `SessionLapsRemainEx` is valid, use it.
3. If `SessionLapsTotal` is valid:
   - If team/player progress exists, return `SessionLapsTotal - progress`.
   - Otherwise return `SessionLapsTotal`.
4. If timed race remaining seconds and race pace exist, and the active race is not in a pre-green `SessionState`:
   - If leader progress exists:
     - `finishLap = ceil(leaderProgress + timeRemaining / racePaceSeconds)`
     - `lapsRemaining = finishLap - teamOrLeaderProgress`
   - If no leader progress exists:
     - `lapsRemaining = ceil(timeRemaining / racePaceSeconds + 1)`
5. If scheduled session laps can be parsed, use scheduled laps.
6. If scheduled session time can be parsed and lap time exists:
   - `ceil(scheduledSeconds / lapTimeSeconds)`
7. Otherwise unavailable.

Progress prefers team car progress, then player progress, then `RaceLaps`.

Positive `SessionTimeRemain` during race pre-green can be the grid countdown rather than race time remaining, so fuel strategy skips that live-clock branch for race `SessionState` values `1`, `2`, and `3`. In that phase it falls back to scheduled race laps/time until green/running telemetry exposes normal remaining-race time.

Fuel level and burn telemetry still remain live during gridding, pacing, and early green. When a car reaches the grid and the engine is on, it can already consume fuel before green, including during `SessionState` `2`, `3`, and early `4`. The projection should keep using current fuel level/burn evidence while avoiding the pre-green countdown as the race-duration input.

Fuel reuses the shared projector with its selected race pace. That allows the live model to provide the normal estimate while still allowing Fuel to substitute a better history-derived lap time when live leader/team pace is unavailable.

## Fuel To Finish

When fuel per lap and race laps remaining both exist:

- `fuelToFinish = fuelPerLapLiters * raceLapsRemaining`

When current fuel also exists:

- `additionalFuelNeeded = max(0, fuelToFinish - currentFuelLiters)`

## Completed Stint Count

The calculator receives `CompletedStintCount` from live telemetry when available.

If it is zero, it estimates from current progress:

1. Determine current team/player progress.
2. Select the longest realistic stint target.
3. `floor(progress / targetLaps)`.

This is only a fallback for row numbering.

## Teammate Stint Target

History can bias projected teammate stints.

Rules:

1. Read mean teammate stint laps from history.
2. Round to nearest integer.
3. Ignore if target is less than or equal to 1 or greater than 20.
4. If fuel tank and fuel per lap exist, calculate required saving for that target.
5. Ignore the teammate target if required saving is more than 5 percent.

The selected target is a planning hint, not measured live teammate fuel.

## Stint Count

The calculator computes:

- `currentStintLaps = currentFuelLiters / fuelPerLapLiters`
- `fullTankStintLaps = maxFuelLiters / fuelPerLapLiters`

If race laps remaining is known:

1. `plannedRaceLaps = ceil(raceLapsRemaining)`.
2. If current fuel exists:
   - If current fuel covers all planned laps, stint count is 1.
   - Otherwise stint count is `1 + ceil(remainingAfterCurrent / fullTankStintLaps)`.
3. If current fuel is missing:
   - Stint count is `ceil(plannedRaceLaps / fullTankStintLaps)`.

## Whole-Lap Targets

When planned race laps and stint count are known:

1. Divide planned race laps evenly across stint count.
2. Put remainder laps into earlier stints.
3. Apply teammate stint target to projected teammate stints if possible:
   - Teammate stints alternate.
   - If current fuel is unavailable, first projected stint is treated as teammate.
   - Donor laps are taken from non-teammate stints above `target - 1`.

## Required Saving

For each target stint:

- `fuelRequired = targetLaps * fuelPerLapLiters`
- `extraFuelRequired = fuelRequired - availableFuelLiters`
- `requiredSavingPerLap = extraFuelRequired / targetLaps` when extra fuel is positive

The overall required saving displayed by the strategy is the maximum required saving across target stints.

The realistic saving threshold is currently 5 percent of selected fuel per lap.

## Tire Advice

Tire advice is only built for stops before the final stint.

If max fuel, available fuel, or fill/service history is missing, advice is pending.

For each stop:

1. Estimate fuel remaining at stop:
   - `fuelAtStop = max(0, availableFuel - targetLaps * fuelPerLap)`
2. Estimate fuel to add:
   - `fuelToAdd = max(0, maxFuel - fuelAtStop)`
3. If fill rate exists:
   - `refuelSeconds = fuelToAdd / fillRate`
4. If tire service seconds are missing:
   - Show pending tire data, optionally with fuel amount.
5. If refuel seconds are missing:
   - Show approximate tire service time.
6. If both refuel and tire service exist:
   - `noTireStopSeconds = max(refuelSeconds, noTireServiceSeconds or 0)`
   - `tireStopSeconds = max(refuelSeconds, tireServiceSeconds)`
   - `timeLoss = max(0, tireStopSeconds - noTireStopSeconds)`
   - If time loss is at most 1 second, tires are considered free.
   - Otherwise show the estimated tire time loss.

## Stop Optimization

The stop optimization asks: can we skip one planned stop?

It only runs when:

- Planned race laps are positive.
- Planned stint count is greater than 1.
- Max fuel exists.

Candidate:

- `candidateStintCount = plannedStintCount - 1`
- `savedStopCount = 1`

Available fuel:

- If current fuel exists:
  - `currentFuel + max(0, candidateStintCount - 1) * maxFuel`
- Otherwise:
  - `candidateStintCount * maxFuel`

Required fuel per lap:

- `requiredFuelPerLap = availableFuel / plannedRaceLaps`
- `requiredSaving = fuelPerLap - requiredFuelPerLap`
- `requiredSavingPercent = requiredSaving / fuelPerLap`

If required saving is at most 5 percent, it is realistic.

Estimated saved seconds uses the first available value from:

- Pit lane seconds.
- Tire change service seconds.
- No-tire service seconds.

Break-even loss per lap is:

- `estimatedSavedSeconds / plannedRaceLaps`

## Rhythm Comparison

The rhythm comparison asks: does the longest realistic target avoid stops compared with one lap shorter?

It only runs when race laps, fuel per lap, and max fuel are valid.

1. Select longest realistic stint target:
   - Prefer teammate long target when valid.
   - Otherwise use `ceil(maxFuel / fuelPerLap)`.
   - If that requires more than 5 percent saving, fall back to `floor(maxFuel / fuelPerLap)`.
2. Short target is `longTarget - 1`.
3. Calculate stint and stop counts:
   - `shortStintCount = ceil(plannedRaceLaps / shortTarget)`
   - `longStintCount = ceil(plannedRaceLaps / longTarget)`
   - `shortStopCount = shortStintCount - 1`
   - `longStopCount = longStintCount - 1`
4. If short rhythm has more stops:
   - `additionalStopCount = shortStopCount - longStopCount`
   - Estimate time loss from pit strategy if available.
   - Mark realistic when long target saving is at most 5 percent.

## Status Text Priority

The first matching rule wins:

1. Missing current fuel and missing enough model data: `waiting for fuel`.
2. Missing fuel per lap: `waiting for burn`.
3. Missing race laps remaining: `stint estimate`.
4. Realistic rhythm comparison with additional stops: rhythm message.
5. Realistic stop optimization: skip-stop message.
6. Required saving within 5 percent: target save message.
7. Planned stint count exists:
   - `fuel covers finish` when no stop.
   - `{stints} stints / {stops} stops` otherwise.
   - Prefix `model: ` when current fuel is missing.
8. Additional fuel needed exists: `+X L needed`.
9. Fallback: `fuel covers finish`.

## Display Rows

The view model builds rows in this order:

1. Strategy row when rhythm comparison has additional stops.
2. Stint rows, up to the maximum row count after the strategy row.

Stint row text:

- `finish` source: `no fuel stop needed`.
- Target laps available: `{targetLaps} laps | target {fuelPerLap}`.
- Final target adds `final`.
- Otherwise show decimal stint length.

The Windows table always keeps six stint rows visible. Rows with no current content are blank placeholders.

## Source Row

The source row includes:

- Selected burn and source.
- Laps per tank.
- History source: user, baseline, or none.
- Min/avg/max fuel per lap when history range exists.
- Tire model source when fill/tire data exists.
- Overall/class leader gap in laps when available.

## Design Notes

- Live data must win over history.
- Baseline history must stay opt-in.
- Teammate stint targets are hints, not measured live teammate fuel.
- Missing focused-driver fuel should still allow stint-history analysis rows when matching history exists.
- Strategy suggestions should distinguish measured facts from model assumptions.
- The table layout should stay stable during a run.

## 24-Hour Race Findings

Live endurance-race review found that fuel strategy needs a team-stint model rather than stitching scalar contexts through driver changes:

- When a teammate got in the car, the overlay stitched contexts instead of showing a holistic view that combined detailed local-driver fuel evidence with teammate stint-length evidence.
- It suggested impossible or unhelpful stint-length options, including 5-lap "time-saving" style options during a race where the team consistently ran 7-lap stints.

Future fuel work should keep local measured fuel windows, teammate stint lengths, pit/service history, and model assumptions as separate evidence families. Strategy suggestions should be gated by completed-stint history, max fuel, current stint phase, and realistic endurance-race feasibility before they are promoted to user-facing advice.
