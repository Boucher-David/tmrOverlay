# Fuel Overlay Context

Last updated: 2026-04-26

## Current Analyzed Capture

Sample analyzed capture:

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

- iRacing exposes raw usage and context
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

The first real fuel overlay should favor clarity over breadth.

Suggested initial overlay contents:

- current fuel in liters
- current fuel percent
- smoothed burn rate
- projected minutes left
- projected laps left
- estimated fuel to finish
- simple status color:
  - safe
  - close
  - short

Suggested derivations:

- `burn_lph = FuelUsePerHour / DriverCarFuelKgPerLtr`
- `minutes_left = FuelLevel / burn_lph * 60`
- `laps_left = FuelLevel / (burn_lph * est_lap_time_s / 3600)`

Use rolling smoothing and ignore invalid startup frames.

## Next Expected Input

The planned next dataset is a full endurance-event capture.

That is the right time to:

- validate stint projections over long runs
- separate pit-road and green-flag burn behavior
- add timed-session vs lap-limited logic
- test multi-class assumptions

