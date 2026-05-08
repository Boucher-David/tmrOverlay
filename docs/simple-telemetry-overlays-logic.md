# Simple Telemetry Overlays Logic

The simple telemetry overlays are small read-only windows into normalized iRacing telemetry. They use `LiveTelemetrySnapshot.Models` directly and keep source/evidence text quiet in the normal case.

Session / Weather and Pit Service use the shared `SimpleTelemetryOverlayForm` shell. Flags and Input / Car State now use custom lightweight forms because their first useful shape is graphical rather than table-first.

- availability uses `OverlayAvailabilityEvaluator` so native and browser overlays share the same connected/collecting/fresh rules
- snapshots older than 1.5 seconds show a waiting state
- disconnected telemetry shows `waiting for iRacing`; connected but not collecting telemetry shows `waiting for telemetry`
- unexpected refresh/render exceptions show a compact visible error and are logged
- rows stay table-shaped so the window does not resize as values appear or disappear
- source footers are validation/admin chrome, not default user-facing overlay furniture

## Flags

`Flags` reads `LiveSessionModel.SessionState` and `SessionFlags`. It renders as a compact transparent icon-only overlay with procedurally drawn waving flags rather than a table, text label, or full-screen border. It requires recognized live session context before the window is shown; generated mac screenshots and demo states can still force review visuals.

The settings tab lets the user choose which flag categories can display and uses the shared scale control for overlay size. There are no per-flag display timers: each flag paints while the current telemetry state maps to an enabled user-facing flag category and clears when that telemetry state clears or changes to a disabled category. Multiple meaningful categories can render at once, so persistent states such as white, meatball, and black can stay up while transient yellow/debris/blue states appear or disappear around them.

The transparent native flags window is click-through/no-activate and is also hidden while the Settings window is active. Diagnostics bundles include UI-freeze-watch metrics that make Windows validation distinguish a real UI-thread stall from an overlay window/input-capture problem.

Background SDK bits such as `serviceable` and `start hidden` are decoded for diagnostics but do not trigger the overlay by themselves. User-facing categories are:

- green-held/start-ready/start-set/start-go; plain steady-state green running does not show by itself
- blue
- yellow/caution/debris/one-to-green
- critical red/black/repair/furled/disqualification/driver-flag states
- finish/countdown/white/checkered

Flag design is semantic: green for green-flag running, amber for yellow/caution, blue for blue flags, white/checkered for finish/countdown, red for stopped sessions, black for penalties, and black-with-orange-disc for repair/meatball states. Internal labels are retained for diagnostics and tests, but the user-facing renderer does not show penalty text until telemetry capture has a reliable source for exact instructions such as slow-down time or drive-through/stop-go penalty type.

## Session / Weather

`Session / Weather` combines `LiveSessionModel` and `LiveWeatherModel`. It remains a standalone driver/engineer/spotter overlay, and the same session/weather fields are also candidates for optional compact header/footer context in other overlays.

It displays:

- session type/name/team label
- elapsed and remaining clock
- remaining and total laps
- track display name and length
- air and track temperatures in the selected unit system
- track wetness, declared-wet state, and rubber state
- skies/weather type/current precipitation
- wind speed/direction plus humidity/fog when telemetry exposes them

Wet or declared-wet states use an info tone. Declared-wet surface mismatches use a warning tone. Temperature, surface, sky/rain, and wind/atmosphere rows get a short info-tone highlight after their formatted value changes so the driver or spotter can notice condition shifts without turning the overlay into a warning panel.

The live model carries additional atmospheric fields such as pressure and solar angle for future compact widgets/analysis. iRacing telemetry in current captures has not exposed a reliable forecast channel, so the overlay does not display forecast text. Compact edge-case diagnostics dynamically include forecast-like raw channels if a future SDK schema exposes a variable whose name or description contains `forecast`.

## Pit Service

`Pit Service` reads `LiveFuelPitModel` and is read-only in this branch.

It displays:

- release signal:
  - `GREEN - go` when `PlayerCarPitSvStatus` reports service complete
  - `RED - service active` while pit service is in progress or `PitstopActive` is true
  - `RED - repair active` while required repair time remains
  - `YELLOW - optional repair` while optional repair time remains without required repair or active service
  - `GREEN - go (inferred)` when the car is in the stall, service is not active, and no repair timer is blocking release but the explicit service-complete status is unavailable
- player/team pit-road or pit-stall state
- decoded pit service status from `PlayerCarPitSvStatus`
- active/requested service flags
- requested fuel amount in the selected unit system
- required and optional repair time
- requested tire service and tire sets used
- fast repair selected/requested state plus local/team fast repair counters

Requested-service rows get a short info/blue highlight when their formatted value changes, such as fuel amount, tire request, repair time, or fast repair selected/counter state. Red/yellow/green release and repair severity still wins over the change highlight.

Pit service details are local/team-car telemetry, not competitor-wide telemetry. Current captures expose per-car pit-road and fast-repair-used arrays (`CarIdxOnPitRoad`, `CarIdxFastRepairsUsed`) plus general per-car track-surface/timing arrays, but not per-car requested fuel, tire selections, repair timers, service-active state, or service-complete status. Standings/Relative can use those per-car arrays for pit-road context, but this overlay stays anchored to the player/team car service snapshot.

The overlay does not send iRacing pit commands. A future pit crew/engineer overlay should own command-capable controls for refuel amount, tire/repair/tearoff choices, and operator workflow so read-only pit telemetry and active simulator control do not get mixed. Suggested refuel numbers should consume the shared Fuel strategy/race-progress model instead of recalculating laps-to-go inside Pit Service. Future spotter/engineer surfaces should also normalize viewer context from `IsOnTrack`, `IsOnTrackCar`, `IsReplayPlaying`, `IsInGarage`, `IsGarageVisible`, `CamCarIdx`, and camera group data so in-car, replay/watching, garage, and other-car focus modes do not accidentally reuse driver-only assumptions. Do not show teammate/spotter pit-value-change notifications until a capture proves which telemetry signal distinguishes a teammate changing service requests from the local user changing them.

The mac harness standings view uses synthetic pit windows derived from the four-hour preview pit entry/exit timing so the local review surface can exercise the same `Pit` column now exposed by the production Windows standings overlay.

## Input / Car State

`Input / Car State` reads `LiveInputTelemetryModel`.

It displays:

- speed in the selected unit system
- gear and RPM
- throttle/brake/clutch percentages
- steering angle
- ABS activity when `BrakeABSactive` explicitly reports that ABS is reducing brake pressure
- raw engine warning bits
- voltage
- water temperature
- oil temperature plus oil/fuel pressure

The default native and browser input overlays are graph-first. The line graph remains the primary surface, while current-value widgets live in a right-side content rail. Pedals are shown as vertical percentage bars, steering remains a wheel visualization, and gear/speed are numeric. Throttle, brake, clutch, steering, gear, and speed are independent Content-tab options that can be turned on or off. Input / Car State does not expose Header/Footer settings in this branch.

Engine warning bits use a warning tone. Otherwise this overlay stays neutral and acts as a compact local-driver input surface. TC firing is deliberately not shown yet: current captures expose TC toggle/adjustment-style fields, but not a proven traction-control intervention signal equivalent to `BrakeABSactive`. Current captures also expose `Clutch` and `ClutchRaw`, while future clutch-like schema fields such as dual-clutch channels are watched diagnostically before being promoted into the production overlay.
