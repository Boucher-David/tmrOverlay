# Simple Telemetry Overlays Logic

The simple telemetry overlays are small read-only windows into normalized iRacing telemetry. They use `LiveTelemetrySnapshot.Models` directly and keep source/evidence text quiet in the normal case.

All four overlays use the shared `SimpleTelemetryOverlayForm` shell:

- snapshots older than 1.5 seconds show a waiting state
- disconnected or non-collecting telemetry shows `waiting for iRacing`
- unexpected refresh/render exceptions show a compact visible error and are logged
- rows stay table-shaped so the window does not resize as values appear or disappear

## Flags

`Flags` reads `LiveSessionModel.SessionState` and `SessionFlags`.

It displays:

- session state
- decoded common flag labels
- raw hexadecimal flag bits
- remaining session time
- remaining/total race laps when present

Flag color is semantic: green for green-flag running, amber for yellow/caution, blue/info for checkered or neutral race-state information, and red for service, repair, black, red, or disqualification flags.

## Session / Weather

`Session / Weather` combines `LiveSessionModel` and `LiveWeatherModel`.

It displays:

- session type/name/team label
- elapsed and remaining clock
- remaining and total laps
- track display name and length
- air and track temperatures in the selected unit system
- track wetness and declared-wet state
- skies/weather type/precipitation
- rubber state

Wet or declared-wet states use an info tone. Declared-wet surface mismatches use a warning tone.

## Pit Service

`Pit Service` reads `LiveFuelPitModel` and is read-only in this branch.

It displays:

- player/team pit-road or pit-stall state
- active/requested service flags
- requested fuel amount in the selected unit system
- required and optional repair time
- requested tire service and tire sets used
- local/team fast repair counters
- raw pit service flags

The overlay does not send iRacing pit commands. A future pit crew/engineer overlay should own command-capable controls for refuel amount, tire/repair/tearoff choices, and operator workflow so read-only pit telemetry and active simulator control do not get mixed.

## Input / Car State

`Input / Car State` reads `LiveInputTelemetryModel`.

It displays:

- speed in the selected unit system
- gear and RPM
- throttle/brake/clutch percentages
- steering angle
- raw engine warning bits
- voltage
- water temperature
- oil temperature plus oil/fuel pressure

Engine warning bits use a warning tone. Otherwise this overlay stays neutral and acts as a compact local-car telemetry surface.
