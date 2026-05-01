# Telemetry Schema Summary

This file describes the current raw capture model in three layers:

- `event`: capture-level and weekend-level context
- `session`: the active iRacing session state
- `car`: static vehicle identity plus dynamic usage telemetry

The current production path analyzes live SDK data and stores compact session history. Raw capture remains available as an opt-in diagnostic/development mode; when enabled it stores raw frame payloads in `telemetry.bin`, variable definitions in `telemetry-schema.json`, session YAML in `latest-session.yaml`, and capture metadata in `capture-manifest.json`.

## Event Schema

Event-level data is the highest-level context for a capture. It combines the local capture manifest with the `WeekendInfo` block from the iRacing session YAML.

### Primary sources

- `capture-manifest.json`
- `latest-session.yaml` -> `WeekendInfo`

### Core fields

- Capture identity:
  - `captureId`
  - `startedAtUtc`
  - `finishedAtUtc`
  - `frameCount`
  - `droppedFrameCount`
  - `tickRate`
  - `bufferLength`
  - `variableCount`
- Track and venue:
  - `TrackName`
  - `TrackDisplayName`
  - `TrackConfigName`
  - `TrackID`
  - `TrackLength`
  - `TrackCountry`
  - `TrackCity`
  - `TrackNumTurns`
- Event and build context:
  - `EventType`
  - `Category`
  - `Official`
  - `SeriesID`
  - `SeasonID`
  - `SessionID`
  - `SubSessionID`
  - `BuildVersion`
  - `TrackVersion`
- Weather and environment:
  - `TrackWeatherType`
  - `TrackSkies`
  - `TrackSurfaceTemp`
  - `TrackSurfaceTempCrew`
  - `TrackAirTemp`
  - `TrackWindVel`
  - `TrackWindDir`
  - `TrackRelativeHumidity`
  - `TrackFogLevel`
  - `TrackPrecipitation`
  - live telemetry `TrackTemp`, `TrackTempCrew`, `AirTemp`, `TrackWetness`, `Skies`, `WindVel`, `WindDir`, `RelativeHumidity`, `FogLevel`, `Precipitation`, and `WeatherDeclaredWet`

### Example from short diagnostic capture

- Event type: offline test
- Track: Nürburgring Combined / Gesamtstrecke 24h
- Build version: `2026.04.21.01`
- Weather: overcast, static weather, 35% precipitation

## Session Schema

Session-level data explains what is happening in the current iRacing session and how that state changes over time.

### Primary sources

- `latest-session.yaml` -> `SessionInfo`
- `telemetry-schema.json` / `telemetry.bin`

### Static or slowly changing fields

- `CurrentSessionNum`
- `Sessions[].SessionNum`
- `Sessions[].SessionType`
- `Sessions[].SessionName`
- `Sessions[].SessionTime`
- `Sessions[].SessionLaps`
- `Sessions[].SessionTrackRubberState`
- `ResultsPositions`
- `ResultsFastestLap`

### Live session-state fields

- `SessionTime`
- `SessionTick`
- `SessionNum`
- `SessionState`
- `SessionFlags`
- `SessionTimeRemain`
- `SessionLapsRemainEx`
- `SessionTimeOfDay`
- `IsOnTrack`
- `IsReplayPlaying`
- `SessionInfoUpdate`

### Useful analysis questions

- Was the sim connected and streaming valid data?
- What was the current session type when the capture occurred?
- How long was the car on track vs off track or on pit road?
- How far through the lap or session did the driver get?
- Did flags, wetness, or session status change mid-capture?

### Example from short diagnostic capture

- Session type: `Offline Testing`
- Captured duration: about 77 seconds
- On-track time: about 67 seconds
- On-pit-road time: about 6.6 seconds
- Max lap progress reached: about 8.2% of a lap

## Car Schema

Car data splits into two parts:

1. static identity and setup context
2. dynamic usage and state telemetry

### Car identity and setup

Primary source:

- `latest-session.yaml` -> `DriverInfo` and `Drivers[]`

Useful fields:

- Driver identity:
  - `DriverCarIdx`
  - `DriverUserID`
  - `UserName`
  - `TeamName`
- Vehicle identity:
  - `CarPath`
  - `CarID`
  - `CarScreenName`
  - `CarScreenNameShort`
  - `CarIsElectric`
- Configuration and setup:
  - `DriverSetupName`
  - `DriverSetupIsModified`
  - `DriverSetupLoadTypeName`
  - `DriverSetupPassedTech`
  - `DriverCarVersion`
  - `DriverGearboxType`
  - `DriverCarFuelMaxLtr`
  - `DriverCarRedLine`
  - `DriverCarGearNumForward`
  - `DriverCarSLShiftRPM`
  - `DriverTires`

### Dynamic car usage telemetry

Primary source:

- `telemetry-schema.json` + `telemetry.bin`

#### Driver inputs

- `Throttle`
- `Brake`
- `Clutch`
- `ThrottleRaw`
- `BrakeRaw`
- `ClutchRaw`
- `SteeringWheelAngle`
- `BrakeABSactive`

#### Motion and powertrain

- `Speed`
- `Gear`
- `RPM`
- `LapDistPct`
- `OnPitRoad`
- `PlayerTrackSurface`
- `PlayerTrackSurfaceMaterial`

#### Fuel, tires, and service state

- `FuelLevel`
- `FuelLevelPct`
- `FuelUsePerHour`
- `PlayerTireCompound`
- `TireSetsUsed`
- `TireSetsAvailable`
- `PitstopActive`
- `PitRepairLeft`
- `PitOptRepairLeft`
- `PitSvFuel`
- `PitSvTireCompound`

#### In-car controls and condition

- `dcBrakeBias`
- `dcABS`
- `dcTractionControl`
- `OilTemp`
- `OilPress`
- `WaterTemp`
- `WaterLevel`
- `FuelPress`
- `Voltage`
- `EngineWarnings`

#### Multi-car arrays for race/session context

- `CarIdxLap`
- `CarIdxLapCompleted`
- `CarIdxLapDistPct`
- `CarIdxTrackSurface`
- `CarIdxOnPitRoad`
- `CarIdxPosition`
- `CarIdxClassPosition`
- `CarIdxClass`
- `CarIdxF2Time`
- `CarIdxEstTime`
- `CarIdxRPM`
- `CarIdxGear`
- `CarIdxBestLapTime`
- `CarIdxTireCompound`
- `CarLeftRight`

#### Proximity and leader-gap fields

- `CarLeftRight` is the scalar side-warning signal for cars to the left, right, or both sides of the driver.
- `CarIdxF2Time` is used for race gap timing to the overall leader, class leader, and same-class cars in the gap trend graph when race-position data is available.
- Gap graph class rows are kept separate from radar proximity rows: cars with valid standings/F2 timing can remain graphable even when `CarIdxLapCompleted` or `CarIdxLapDistPct` is unavailable.
- `CarIdxEstTime`, `CarIdxLapCompleted`, and `CarIdxLapDistPct` provide the first-pass relative track-position model for nearby cars.
- `CarIdxTrackSurface` and `CarIdxOnPitRoad` help distinguish cars physically on track from pit-road or off-track cars as the radar model gets refined.
- The live radar model keeps a short in-memory proximity history so different-class cars approaching from behind can raise a multiclass warning before they reach the close-range radar circle. This derived warning is not persisted in compact history.

### Example from short diagnostic capture

- Car: Mercedes-AMG GT3 2020
- Setup: custom user setup loaded
- Gearbox: sequential
- Max fuel: 106 L
- Tire options listed in session YAML: hard and wet
- Usage seen in telemetry:
  - throttle/brake/clutch input
  - gear and RPM progression
  - ABS activity
  - brake-bias, ABS, and TC settings
  - fuel burn from about 106.0 L down to about 104.934 L

## Gaps And Limits

This schema summary is intentionally practical, not exhaustive. Not every iRacing variable is always present in every capture or every car/session combination.

The captured diagnostic schema does **not** appear to expose richer engineering channels such as:

- tire wear
- tire temperature distribution
- brake temperatures
- wheel-speed channels
- suspension travel or damper position

If those metrics matter later, the first check should be whether iRacing exposes them for the target car/session at all. If they do, raw diagnostic capture can still record them for analysis, but the normal app path should prefer derived live metrics and compact history over storing full-frame payloads.
