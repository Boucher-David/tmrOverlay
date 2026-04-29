# Fuel Calculator Logic

This file explains how the fuel calculator derives strategy numbers and display rows.

Implementation files:

- `src/TmrOverlay.Core/Telemetry/Live/LiveTelemetrySnapshot.cs`
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

It uses live telemetry first, then exact user history for the same car/track/session combo, then optional baseline history only when baseline lookup is enabled.

## Refresh Loop

The Windows fuel overlay refreshes once per second.

Each refresh:

1. Reads the latest `LiveTelemetrySnapshot`.
2. Looks up exact combo history with a 30 second cache.
3. Builds a `FuelStrategySnapshot`.
4. Converts the snapshot to display text and rows.
5. Applies status color, source visibility, advice-column visibility, and row text.
6. Keeps all six stint rows visible, using blank future rows when fewer rows are available.

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

1. Live fuel per lap from `LiveFuelSnapshot`.
2. Preferred history aggregate mean fuel per lap.
3. Unavailable.

When live fuel per lap is selected, history min/max can still appear as context in the source row.

## Lap Time Selection

The calculator selects strategy lap time in this order:

1. Live fuel snapshot lap time.
2. Team last lap from latest sample.
3. History median lap.
4. History average lap.
5. Driver estimated lap time from live context.
6. Driver estimated lap time from history aggregate car metadata.
7. Unavailable.

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

The calculator estimates race laps remaining in this order:

1. If session state indicates ended, return `0`.
2. If `SessionLapsRemainEx` is valid, use it.
3. If `SessionLapsTotal` is valid:
   - If team/player progress exists, return `SessionLapsTotal - progress`.
   - Otherwise return `SessionLapsTotal`.
4. If timed race remaining seconds and race pace exist:
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
- Strategy suggestions should distinguish measured facts from model assumptions.
- The table layout should stay stable during a run.

