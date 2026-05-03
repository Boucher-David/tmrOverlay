# Simple Telemetry Overlays Logic

The simple telemetry overlays are small read-only windows into normalized iRacing telemetry. They use `LiveTelemetrySnapshot.Models` directly and keep source/evidence text quiet in the normal case.

Session / Weather and Pit Service use the shared `SimpleTelemetryOverlayForm` shell. Flags and Input / Car State now use custom lightweight forms because their first useful shape is graphical rather than table-first.

- snapshots older than 1.5 seconds show a waiting state
- disconnected or non-collecting telemetry shows `waiting for iRacing`
- unexpected refresh/render exceptions show a compact visible error and are logged
- rows stay table-shaped so the window does not resize as values appear or disappear
- source footers are validation/admin chrome, not default user-facing overlay furniture

## Flags

`Flags` reads `LiveSessionModel.SessionState` and `SessionFlags`. It renders as a transparent primary-screen border overlay rather than a table. It requires recognized live session context before the window is shown; generated mac screenshots and demo states can still force review visuals.

The settings tab lets the user choose which flag categories can display and set the custom border size. There are no per-flag display timers: the border paints while the current telemetry state maps to an enabled user-facing flag category and clears when that telemetry state clears or changes to a disabled category.

Background SDK bits such as `serviceable` and `start hidden` are decoded for diagnostics but do not trigger the border by themselves. User-facing categories are:

- green/start-go
- blue
- yellow/caution/debris/one-to-green
- critical red/black/service/repair/furled/disqualification/driver-flag states
- finish/countdown/white/checkered

Flag color is semantic: green for green-flag running, amber for yellow/caution, blue for blue flags, white for finish/countdown, and red for critical driver/session flags.

## Session / Weather

`Session / Weather` combines `LiveSessionModel` and `LiveWeatherModel`. It is useful as a v0.8 review surface, but most of its values are candidates for shared overlay header/footer context rather than a permanent standalone table. Live wind direction and temperature may work better later as compact weather widgets.

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

The mac harness standings candidate uses synthetic pit windows derived from the four-hour preview pit entry/exit timing to show how a live `Pit` column can behave before a production standings data contract is added.

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
