# Car Radar Logic

This file explains how the car radar decides when to appear, where cars are drawn, and how proximity is colored.

Implementation files:

- `src/TmrOverlay.Core/Telemetry/Live/LiveTelemetrySnapshot.cs`
- `src/TmrOverlay.Core/Telemetry/Live/LiveTelemetryStore.cs`
- `src/TmrOverlay.App/Overlays/CarRadar/CarRadarForm.cs`

## Purpose

The radar is a live proximity overlay. It is not a historical or replay overlay.

It uses fresh live telemetry only:

- `CamCarIdx` for the current camera/focus car when valid, falling back to `PlayerCarIdx`.
- `CarLeftRight` for side occupancy.
- Nearby `CarIdxLapDistPct` and lap completion for physical-distance placement when track length is known.
- Nearby `CarIdxEstTime` or `CarIdxF2Time` for live relative seconds when available and plausible.

If live timing is missing or suspicious, the car may remain in the live proximity snapshot for diagnostics and non-radar consumers. When track length is known, the radar can still use current lap-distance progress as a physical distance; it does not synthesize a seconds gap from lap distance, fuel, or history estimates.

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

1. Determine focused-car lap distance:
   - Prefer `FocusLapDistPct`, sourced from `CarIdxLapDistPct[CamCarIdx]` when the camera focus car is valid.
   - Fall back to team/player lap distance only when the focus car is the player car.
2. If focused-car lap distance is unavailable:
   - Keep side occupancy from `CarLeftRight` only when the focus car is the player car.
   - Mark nearby cars unavailable.
3. If the focused car is on pit road or in a pit track-surface state:
   - Mark the radar unavailable.
   - Hide side occupancy.
4. Convert each nearby car:
   - `relativeLaps = car.LapDistPct - focusLapDistPct`
   - Wrap across start/finish so values stay within `-0.5` to `0.5`.
   - Preserve position, class, track surface, pit-road flag, F2 time, and estimated time.
   - Calculate relative meters when track length exists.
5. Exclude cars on pit road or in pit track-surface states.
6. Keep cars where absolute relative laps is at most `0.5` and not effectively zero.
7. Sort by absolute relative laps.
8. Nearest ahead is the smallest positive relative laps.
9. Nearest behind is the largest negative relative laps.

Pit-road cars do not appear on radar. The gap overlay can still use separate timing rows for race context.

## Relative Seconds

Relative seconds are optional.

The first valid source wins:

1. `CarIdxEstTime` difference:
   - `delta = carEstimatedTimeSeconds - focusEstimatedTimeSeconds`
   - If a live lap time exists, wrap deltas larger than half a lap.
   - Accept only when plausible against relative lap direction.

2. `CarIdxF2Time` difference:
   - `delta = focusF2TimeSeconds - carF2TimeSeconds`
   - Accept only when plausible against relative lap direction.

3. Otherwise, relative seconds is null.

Plausibility rules:

- Delta must be finite.
- Near-zero deltas are rejected when lap-distance progress says the car is meaningfully separated. This guards against uninitialized timing rows such as `CarIdxF2Time == 0` for multiple cars.
- Delta sign must match relative lap sign when both are non-zero.
- If live lap time exists:
  - Calculate lap-distance estimate: `abs(relativeLaps * lapTimeSeconds)`.
  - Maximum accepted delta is `max(5, min(lapTime / 2, lapDistanceEstimate + 10))`.
- If live lap time does not exist:
  - Absolute delta must be at most 60 seconds.

Live lap time for this plausibility check comes from current live lap-time fields only:

- Focus last lap.
- Focus best lap.
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

`CarLeftRight` is a player-car scalar. When the camera is focused on another car, side occupancy is hidden instead of applying the player's side warning to the watched car.

For side-by-side placement, `CarLeftRight` is authoritative. Timing never creates an alongside state by itself.

When a side warning exists, the radar may attach that side slot to a rendered timed car only when:

- The car has reliable relative meters inside the contact-length window, or it has reliable relative seconds inside the fallback timing window.

The distance window uses a 4.746 m focused-car-length baseline. The seconds fallback is derived from that same assumed length divided by focused-car speed, clamped between 0.18 and 0.45 seconds. If speed is unavailable, the fallback window is 0.22 seconds.

If no timed car qualifies, the radar still draws the generic side-warning rectangle from `CarLeftRight`. This keeps the actual spotter warning visible without pretending a random timed car is alongside.

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

The Windows form also clips the overlay window to a circular region and drives the form opacity to zero when the radar has fully faded. That is a defensive transparency fallback: if the WinForms transparency key fails to remove the fuchsia backing color in a particular desktop/compositor path, the hidden radar still cannot leave a purple square behind.

## Radar Range

Radar range is:

- 6 focused-car lengths, currently `4.746 m * 6 = 28.476 m`, when relative meters exists.
- 2 seconds for cars without relative meters but with reliable relative seconds.

Cars are in range when the best available live relative value is inside that range. Distance is preferred over seconds because it is a direct physical threshold and avoids showing multiple cars as overlapping simply because their timing rows are similar.

Range ratio:

- `relativeMeters / RadarRangeMeters` when meters exists.
- `relativeSeconds / RadarRangeSeconds` otherwise.
- Clamped from `-1` to `1`.

Range ratio drives color and ordering.

Longitudinal placement uses signed relative meters with a car-length contact window when possible, falling back to signed relative seconds:

- `0.0 m` maps to the focused car rectangle.
- A non-zero car whose absolute gap is at least the focused-car-length contact window is placed outside the focused car rectangle.
- A car rectangle should overlap the focused car only when the reliable distance gap is inside that contact window. In practice, that visual overlap means contact, a near-contact stack, or an actual side-overlap/alongside condition.
- Outside the contact window, remaining distance is scaled out to the radar edge.

Negative values draw behind the focused car. Positive values draw ahead.

## Fade State

Each refresh updates:

- Whole radar alpha.
- Left warning alpha.
- Right warning alpha.
- Per-car visual alpha.

Fade timing:

- Fade in: 0.25 seconds.
- Fade out: 0.85 seconds.

Cars are tracked by `CarIdx`. If a car disappears from current range, its visual alpha fades out before the visual state is removed.

## Drawing

The radar draws:

1. Circular dark radar background.
2. Optional multiclass warning arc.
3. Two range rings with labels.
4. Nearby car rectangles.
5. Side-warning rectangles.
6. Focused car rectangle.

Ring labels show approximate seconds because timing is more useful to drivers than the internal physical range threshold:

- Inner/outer labels show seconds within the fallback timing window, for example `1.3s` and `0.7s`.
- Distance remains an internal placement/range input when relative meters exists.

## Car Color

The production car color is neutral proximity color, not iRacing class color. Class color is still parsed and carried in the live model for future overlays, but the radar does not use it because common yellow or red class colors can make normal traffic look like a warning.

Proximity color does not begin across the full radar range; it begins only when bumper gap is inside the close warning buffer:

- With relative meters, bumper gap is `abs(relativeMeters) - 4.746 m`.
- The warning buffer is currently `2.0 m`, roughly one rendered car width in the radar UI.
- Outside that buffer, the car stays white.
- Inside that buffer, the car blends from white toward yellow, then saturated alert red.
- At nose-to-tail contact or overlap, the car reaches alert red.

When only timing fallback exists, the same idea is approximated from the side-overlap timing window: the side-overlap seconds represent contact, and the extra warning seconds are scaled from the same 2.0 m buffer.

The focused/user car remains white.

The car alpha is based on visual fade, radar fade, and distance inside the radar range. A car entering at the outer radar range is faint, then fades toward full opacity by the time it reaches the yellow-warning threshold. The overlay itself fades in/out; cars do not turn purple as a proximity state.

## Lateral Placement

The radar does not have true lane-level lateral telemetry.

Approximation:

- `CarLeftRight` creates side slots: one left, one right, both sides, two left, or two right.
- A rendered car can occupy a side slot only when it is within the side-overlap contact window.
- Otherwise distribute multiple radar cars across three simple lanes based on `CarIdx` and draw index.
- A single visible car is centered.

## Multiclass Warning

`LiveTelemetryStore` tracks short per-car proximity history and can build early multiclass warnings from other-class traffic behind the focused car.

The early-warning seconds range is outside the fallback timing proximity range:

- Fallback timing proximity range: 2 seconds.
- Multiclass warning range: greater than 2 seconds behind and up to 5 seconds behind.

When camera focus changes, the short closing-rate history is reset so approach rates measured against the old reference car are not applied to the new focused car.

The radar can draw:

- A red outer arc.
- A small text label with the relative seconds when known.

This is still live-only derived state. It is not persisted into compact history.

## Design Notes

- Keep radar logic live-only.
- Do not use fuel estimates or historical lap times to invent radar seconds.
- Prefer physical distance from lap-distance progress and track length for radar thresholds.
- Use live relative seconds as a fallback and for multiclass warning timing.
- Do not render pit-road traffic on radar.
- Color should stay neutral white until close bumper-gap proximity, then move through yellow toward saturated alert red.
- Car opacity should also communicate early proximity: faint at the radar edge, full near the yellow-warning threshold.
- Visibility should be communicated by alpha fade only.
