# Live Model Groundwork

The live telemetry boundary now has two layers:

1. Existing overlay-specific slices: fuel, proximity, and leader gap.
2. Additive shared models under `LiveTelemetrySnapshot.Models`.

The shared models are intended to support mature overlays such as standings, relative, weather, timing tables, and future map/analysis surfaces without letting each overlay rediscover the same race state in its form code.

## Compatibility

`LiveTelemetrySnapshot.Models` is live-only and defaulted to `LiveRaceModels.Empty`.

This does not change:

- raw capture files
- compact history summaries or aggregates
- post-race analysis JSON
- existing capture synthesis artifacts
- existing overlay-specific snapshot properties

Older data collected on Windows remains readable because the new models are derived from the normalized `HistoricalTelemetrySample` already produced by the collector. The builder tolerates missing timing, driver, weather, pit, and proximity fields by marking the affected model as `Unavailable` or `Partial` instead of throwing.

## Model Families

- `LiveSessionModel`: session clock, lap limits, session labels, track/car display labels, and missing live session signals.
- `LiveDriverDirectoryModel`: session-info driver identity keyed by `CarIdx`, plus player/focus references.
- `LiveTimingModel`: reusable overall/class rows with position, class, lap progress, timing, gap, pit, and driver identity fields.
- `LiveRelativeModel`: focus-relative cars from proximity first, with class-gap timing as a fallback.
- `LiveSpatialModel`: focus-relative lap and meter placement for map/radar-style consumers.
- `LiveWeatherModel`: live wetness, declared-wet state, temperatures, skies, precipitation, and rubber state.
- `LiveFuelPitModel`: live fuel plus pit-road/service signals.
- `LiveRaceEventModel`: basic on-track, garage, lap, and driver-change context.
- `LiveInputTelemetryModel`: currently exposes only speed/tire-compound availability from normalized samples; pedal/steering channels can be added later without changing current consumers.

## Timing Columns

`TimingColumnRegistry` defines reusable column metadata and formatters for table-style overlays. It is groundwork only: no new overlay is registered by this change.

Current default columns cover:

- overall position
- class position
- car number
- driver
- car class
- gap
- interval
- last lap
- best lap
- pit state

The registry keeps display keys stable before any standings or relative overlay exists, so future UI can share column settings and tests can validate table semantics without copying formatter logic.
