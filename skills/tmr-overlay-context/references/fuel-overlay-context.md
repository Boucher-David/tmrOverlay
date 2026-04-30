# Fuel Overlay Context

Last updated: 2026-04-30

## Current Analyzed Captures

Short sample analyzed capture:

- `captures/capture-20260426-032822-916`

High-level context:

- track: Nürburgring Combined / Gesamtstrecke 24h
- event type: `Offline Testing`
- weather: overcast, static weather, 35% precipitation
- car: Mercedes-AMG GT3 2020
- setup: custom user setup loaded

Session/car values observed from session YAML:

- `DriverCarFuelMaxLtr = 106.000`
- `DriverCarFuelKgPerLtr = 0.750`
- `DriverCarEstLapTime = 529.7409`
- tire options listed: hard and wet

## Derived Summary From The Sample

Approximate decoded summary from the raw frames:

- total valid telemetry duration: about 76.1 s
- on-track time: about 67.0 s
- on-pit-road time: about 6.6 s
- moving time: about 61.9 s
- max speed: about 205.1 kph
- max RPM: about 7443
- max lap progress reached: about 8.2% of a lap

Fuel-specific rough derivation from the sample:

- fuel dropped from about `106.0 L` to about `104.934 L`
- fuel used: about `1.066 L`
- derived burn: about `50.45 L/h`
- derived burn using session density: about `37.84 kg/h`
- rough full-tank projection from this short sample:
  - about `126.1 min`
  - about `14.3 laps` using `DriverCarEstLapTime`

Treat those full-tank estimates as low-confidence because the capture is short, partial-lap, wet, and includes pit-road time.

Long endurance capture:

- `captures/capture-20260426-130334-932`
- Nürburgring Gesamtstrecke VLN / `nurburgring combinedshortb`
- Mercedes-AMG GT3 2020
- 4-hour team race, 30 completed laps, final P7 overall / P6 class
- raw capture: 1,036,026 frames, 0 dropped frames, 2,208 session-info snapshots

Derived development/sample baseline now tracked under `history/baseline/cars/car-156-mercedesamgevogt3/tracks/track-262-nurburgring-combinedshortb/sessions/race/`:

- fuel per lap: about `13.363 L/lap`
- fuel per hour: about `99.364 L/h`
- max fuel: `106 L`, which gives about `7.93 laps/tank` at the historical average
- for a 30-lap timed-race estimate, a neutral whole-lap target is `8/8/7/7`; when the 4-hour teammate stint evidence is enabled, the alternating team model becomes `7/8/7/8`
- each 8-lap stint needs about `0.11 L/lap` saving versus the baseline average
- extrapolating the same pace to 24 hours gives about `180 laps`; a 7-lap rhythm would be about `26 stints / 25 stops`, while an 8-lap rhythm is about `23 stints / 22 stops`, so the 8-lap rhythm avoids about 3 stops or about 194 seconds at the observed average pit-lane time
- valid local-driver fuel distance: about `14.112 laps`
- unique team lap-time samples: 27
- pit/service count: 3
- top-level sanitized stint history now records local-driver stints as 7 laps and teammate-driver stints as 8 laps for this combo
- baseline pit estimates include about `2.68 L/s` observed refuel rate and about `39.2 s` inferred tire/service time
- source limitation: fuel comes only from local-driver scalar frames; teammate stints have timing/position from `CarIdx*` arrays but no direct fuel scalar

## What iRacing Exposes For Fuel Work

The currently observed raw fields are enough to build a fuel/stint overlay even though the SDK does not appear to hand us a ready-made stint answer.

Fields already seen or expected to matter:

- raw fuel state:
  - `FuelLevel`
  - `FuelLevelPct`
  - `FuelUsePerHour`
- timing/session context:
  - `SessionTimeRemain`
  - `SessionLapsRemainEx`
  - `LapDistPct`
  - `SessionTime`
- car metadata:
  - `DriverCarFuelMaxLtr`
  - `DriverCarFuelKgPerLtr`
  - `DriverCarEstLapTime`
- service state:
  - `PitstopActive`
  - `PitSvFuel`
  - `PitSvTireCompound`

Current working assumption:

- iRacing exposes raw usage and context for the local driver
- in team events, `CarIdx*` arrays remain useful while spotting/teammate-driving, but scalar `FuelLevel`/`FuelUsePerHour` can become invalid or zero
- we derive:
  - burn rate
  - time remaining on current fuel
  - laps remaining on current fuel
  - estimated fuel to finish
  - pit-window suggestions

## Estimated Lap Time Scope

Important nuance for future multi-class work:

- the current sample capture only shows the player's `DriverCarEstLapTime` and one `CarClassEstLapTime` value in session YAML because the session is effectively single-car/single-class
- prior doc review suggested `CarClassEstLapTime` can exist as a per-car telemetry array when available

For overlay design:

- start with the player's own estimated lap time
- if future endurance captures include full-field estimated lap time data, derive class-level logic by grouping per-car values by class ID

## First Fuel Overlay Direction

The first real fuel overlay should favor clarity over breadth. The current implementation is a draggable fuel calculator window backed by `FuelStrategyCalculator`.

Implemented initial overlay contents:

- overview row: planned race laps, planned stint count, and final stint target
- optional strategy row: compares a conservative shorter stint rhythm against the longest realistic target, then shows extra stops and estimated pit-time loss across any race length
- stint rows: whole-lap targets such as neutral `8/8/7/7` or team-history-adjusted `7/8/7/8`
- per-stint target liters-per-lap only; live/model burn is kept out of the row for now
- advice column: per-stop guidance such as `tires free (106 L)`, `tires +4s`, `tire data pending`, `no tire stop`, or strategy time-loss/saving hints
- compact state: when no fuel stop remains, including after the final stop, the overlay collapses to a single actionable stint row instead of keeping the full multi-stint height
- source row: selected burn source, laps-per-tank, history source, and min/avg/max burn when available
- status color:
  - gray: waiting for usable fuel/burn
  - amber: realistic fuel-saving target, final-stop deletion opportunity, or rhythm strategy opportunity
  - green: current plan is covered by the selected model

Current derivations:

- `burn_lph = FuelUsePerHour / DriverCarFuelKgPerLtr`
- `minutes_left = FuelLevel / burn_lph * 60`
- `laps_left = FuelLevel / (burn_lph * est_lap_time_s / 3600)`
- `race_laps = ceil(overall_leader_progress + session_time_remaining / leader_pace) - team_progress`
- `full_tank_laps = DriverCarFuelMaxLtr / selected_fuel_per_lap`
- `whole_lap_targets = distribute(ceil(race_laps_remaining), planned_stint_count)`
- `target_fuel_per_lap = available_fuel_for_stint / target_laps`
- `required_save_per_lap = max(0, target_laps * selected_fuel_per_lap - available_fuel) / target_laps`
- `rhythm_delta = ceil(race_laps / short_target_laps) - ceil(race_laps / long_target_laps)`
- `rhythm_time_loss = rhythm_delta * historical_pit_lane_seconds`
- `refuel_seconds = fuel_to_add / observed_fill_rate`
- `tire_time_loss = max(refuel_seconds, tire_service_seconds) - max(refuel_seconds, no_tire_service_seconds)`

The overlay must keep live race telemetry as the primary source for strategy and fuel numbers. Live session time, leader/team progress, lap pace, fuel level, fuel burn, pit state, and completed-stint inference should continuously update the table. Exact user history is only a fallback/model until the current session has enough reliable live data. Baseline/sample history is opt-in for development and must not override live data.

During teammate stints, local scalar fuel can be unavailable. In that mode the overlay should continue using team-car `CarIdx*` progress plus user/baseline fuel history, label the usage as a model rather than live burn, and keep completed stints in historical storage instead of rendering them as active table rows.

The live overlay now treats scalar fuel as local-live fuel only when the latest local sample is on track and not in the garage. When the local driver is not in the car, the strategy calculation ignores scalar fuel level/burn, uses history as the modeled fuel source, and can still show observed team/focus stint context from `CarIdx*` progress and pit-road transitions.

When historical completed-stint data shows team stints around 8 laps and the fuel-saving requirement stays within the realistic threshold, the calculator can bias projected rows to 8 laps. This is a planning hint, not proof that current live teammate fuel is directly available, and the UI intentionally does not label rows as teammate stints.

Strategy analysis should feed table generation, not be a separate afterthought. For every race length, the calculator should check whether a slightly longer realistic stint rhythm avoids one or more stops versus the conservative rhythm, then show the stop/time cost in the table before listing the next actionable stints. Long endurance races should not render every future stint row; the overlay should stay compact and show near-term execution plus the strategic delta. As live progress advances, completed rows should disappear from the window so the top actionable row is the current or next stint.

Tire guidance is also a planning hint. It should only use measured or confidence-flagged historical pit-service data, and it should display `tire data pending` when fill-rate or tire-service history is not available for the combo.

## Next Direction

The next implementation step is to harden the fuel/stint overlay by adding:

- rolling smoothing around live fuel burn so instantaneous `FuelUsePerHour` spikes do not whipsaw stint targets
- explicit reserve/margin settings
- broader fallback lookup for same car or similar track when exact car/track/session history is missing
- confidence/source flags in the overlay so teammate-stint fuel is never treated as direct measured fuel unless a future source exposes it

## Pit Service Timing Direction

The long endurance capture confirms pit-service history is worth storing, but with source/confidence flags.

Strong signals:

- `CarIdxOnPitRoad[DriverCarIdx]` for team-car pit lane entry/exit.
- `SessionTime` for elapsed timing.
- `PlayerCarInPitStall`, `PitstopActive`, and low speed for pit-stall/service timing, with caveats during teammate handoff.
- `FuelLevel` and `FuelLevelPct` for fuel added/rate only while local-driver scalar telemetry is valid.
- `CarIdxFastRepairsUsed`, `FastRepairUsed`, `PitRepairLeft`, and `PitOptRepairLeft` for repair events.

Weaker/inferred signals:

- tire changes via `dpLFTireChange`, `dpRFTireChange`, `dpLRTireChange`, `dpRRTireChange`, tire-used counters, compound changes, and tire odometer resets.
- driver swaps via session-info active-driver changes, `DCDriversSoFar`, and `DCLapStatus`.

The 4-hour capture showed three physical stops:

- about 63.9s pit lane / 38.8s stationary, partial local fuel visibility.
- about 66.7s pit lane / 40.4s stationary, reliable full-fuel sample of about 97.3 L over 36.3s, about 2.68 L/s.
- about 63.9s pit lane / 38.4s stationary, partial local fuel visibility plus a fast-repair-used event.

Historical storage should keep both derived stop metrics and raw source flags so strategy calculations can learn per-car values such as tires/no-tires, full fuel, partial fuel, driver swap, and repair cost without overstating confidence.

The current tire guidance uses average tire-service seconds and observed fuel fill rate. A future pass should split this into stronger buckets such as no tires, left/right-side tires, four tires, full fuel, partial fuel, driver swap, and repair events.
