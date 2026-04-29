# Car Radar Logic

This file explains how the car radar decides when to appear, where cars are drawn, and how proximity is colored.

Implementation files:

- `src/TmrOverlay.Core/Telemetry/Live/LiveTelemetrySnapshot.cs`
- `src/TmrOverlay.Core/Telemetry/Live/LiveTelemetryStore.cs`
- `src/TmrOverlay.App/Overlays/CarRadar/CarRadarForm.cs`

## Purpose

The radar is a live proximity overlay. It is not a historical or replay overlay.

It uses fresh live telemetry only:

- `CarLeftRight` for side occupancy.
- Nearby `CarIdxLapDistPct` and lap completion for relative placement.
- Nearby `CarIdxEstTime` or `CarIdxF2Time` for live relative seconds when available and plausible.

If live timing is missing, the radar may still place a car by current lap-distance progress, but it does not synthesize a seconds gap from fuel or history estimates.

## Freshness

The overlay reads `ILiveTelemetrySource.Snapshot()` every 100 ms.

The snapshot is usable only when:

- Connected.
- Collecting.
- `LastUpdatedAtUtc` exists.
- Snapshot age is between 0 and 1.5 seconds.

Stale snapshots are treated as unavailable so old traffic does not remain painted.

## Live Proximity Input

`LiveProximitySnapshot.From` derives proximity from the current telemetry sample.

1. Determine player/team lap distance:
   - Prefer `TeamLapDistPct`.
   - Fall back to player `LapDistPct`.
2. If player/team lap distance is unavailable:
   - Keep side occupancy from `CarLeftRight`.
   - Mark nearby cars unavailable.
3. Convert each nearby car:
   - `relativeLaps = car.LapDistPct - playerLapDistPct`
   - Wrap across start/finish so values stay within `-0.5` to `0.5`.
   - Preserve position, class, track surface, pit-road flag, F2 time, and estimated time.
   - Calculate relative meters when track length exists.
4. Keep cars where absolute relative laps is at most `0.5` and not effectively zero.
5. Sort by absolute relative laps.
6. Nearest ahead is the smallest positive relative laps.
7. Nearest behind is the largest negative relative laps.

Pit-road cars remain in the nearby set when live telemetry reports them, including when the player is also on pit road.

## Relative Seconds

Relative seconds are optional.

The first valid source wins:

1. `CarIdxEstTime` difference:
   - `delta = carEstimatedTimeSeconds - playerEstimatedTimeSeconds`
   - If a live lap time exists, wrap deltas larger than half a lap.
   - Accept only when plausible against relative lap direction.

2. `CarIdxF2Time` difference:
   - `delta = playerF2TimeSeconds - carF2TimeSeconds`
   - Accept only when plausible against relative lap direction.

3. Otherwise, relative seconds is null.

Plausibility rules:

- Delta must be finite.
- Delta sign must match relative lap sign when both are non-zero.
- If live lap time exists:
  - Calculate lap-distance estimate: `abs(relativeLaps * lapTimeSeconds)`.
  - Maximum accepted delta is `max(5, min(lapTime / 2, lapDistanceEstimate + 10))`.
- If live lap time does not exist:
  - Absolute delta must be at most 60 seconds.

Live lap time for this plausibility check comes from current live lap-time fields only:

- Team last lap.
- Team best lap.
- Player last lap.
- Player best lap.

It does not come from fuel strategy or history.

## Side Occupancy

`CarLeftRight` maps to side labels:

- `0`: off.
- `1`: clear.
- `2`: left.
- `3`: right.
- `4`: both sides.
- `5`: two left.
- `6`: two right.
- Missing: waiting.

Left side is active for `2`, `4`, and `5`.

Right side is active for `3`, `4`, and `6`.

## Radar Visibility

The radar fades in when any current signal exists:

- Overlay error.
- Settings preview mode.
- Left side occupancy.
- Right side occupancy.
- At least one car in radar range.
- Multiclass warning.

It fades out when no signal exists.

Settings preview mode is enabled while the radar settings tab is selected. Preview mode forces the radar visible even if normal visibility rules would hide it.

## Radar Range

Radar range is:

- 7 seconds when relative seconds exists.
- 0.02 laps when relative seconds is missing.

A car is in range when its absolute relative value is within that range.

Range ratio:

- `relativeSeconds / 7` when seconds exists.
- `relativeLaps / 0.02` otherwise.
- Clamped from `-1` to `1`.

Negative ratio draws behind the player. Positive ratio draws ahead.

## Fade State

Each refresh updates:

- Whole radar alpha.
- Left warning alpha.
- Right warning alpha.
- Per-car visual alpha.

Fade timing:

- Fade in: 0.25 seconds.
- Fade out: 0.55 seconds.

Cars are tracked by `CarIdx`. If a car disappears from current range, its visual alpha fades out before the visual state is removed.

## Drawing

The radar draws:

1. Circular dark radar background.
2. Optional multiclass warning arc.
3. Two time-gap rings with labels.
4. Nearby car rectangles.
5. Side-warning rectangles.
6. Player car rectangle.

Ring labels show approximate seconds:

- Inner/outer labels are based on the 7 second radar range divided into thirds.

## Car Color

The car color is based on closeness:

- `closeness = 1 - abs(rangeRatio)`
- Far traffic starts white.
- Mid traffic moves toward yellow.
- Close traffic moves toward red.

The car alpha is based on visual fade and radar fade. The overlay itself fades in/out; cars do not turn purple as a proximity state.

## Lateral Placement

The radar does not have true lane-level lateral telemetry.

Approximation:

- If side occupancy is active and the car is nearest ahead/behind, place it left or right.
- Otherwise distribute multiple cars across three simple lanes based on `CarIdx` and draw index.
- A single visible car is centered.

## Multiclass Warning

`LiveTelemetryStore` tracks short per-car proximity history and can build early multiclass warnings from other-class traffic behind the player.

The radar can draw:

- A red outer arc.
- A small text label with the relative seconds when known.

This is still live-only derived state. It is not persisted into compact history.

## Design Notes

- Keep radar logic live-only.
- Do not use fuel estimates or historical lap times to invent radar seconds.
- It is acceptable to show lap-distance placement without a seconds label.
- Pit-road test cases are useful because they increase available proximity scenarios.
- Color should communicate proximity only: white to yellow to red.
- Visibility should be communicated by alpha fade only.

