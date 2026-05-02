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

## Parity Mode

The current product overlays still read the existing overlay-specific snapshot slices. Model v2 is observed in parallel, not yet authoritative.

`LiveModelParityAnalyzer` compares those existing slices against equivalent values in `LiveTelemetrySnapshot.Models` for fuel/pit, proximity/relative/spatial, timing/leader-gap, weather, session, and race-event state. The Windows collector records sampled parity frames and mismatch summaries through `LiveModelParityRecorder`.

At session finalization the app writes `live-model-parity.json`. When raw capture is active, the file is written beside the raw capture sidecars. When raw capture is not active, it is written under the logs model-parity folder. The artifact includes:

- sampled parity frames
- aggregate mismatch counts by family/key
- model-v2 coverage counts versus legacy overlay-input coverage
- `promotionReadiness`, which marks a session as a model-v2 promotion candidate only after it has enough frames, low enough mismatch rate, and enough model coverage for the legacy overlay inputs observed in that session
- post-session signal availability from `capture-synthesis.json`, `telemetry-schema.json`, and `ibt-analysis/*.json`

When `promotionReadiness.isCandidate` is true, the recorder also emits a `live_model_v2_promotion_candidate` app event. Treat that as a review signal, not an automatic migration: overlays should move to model v2 only after several candidate artifacts cover the normal session types and edge cases we care about.

This lets collected raw/IBT sessions evaluate whether model v2 matches current overlay behavior before any overlay is migrated to the new model.

## Deferred Overlay UI V2

Model v2 is data-contract work, not a visual architecture rewrite. A separate overlay UI/style v2 pass should eventually standardize shared visual primitives across the WinForms overlays:

- semantic theme tokens for spacing, typography, borders, severity, table, and graph roles
- reusable header, status badge, source footer, metric row, table cell, graph panel, and empty/error/waiting-state helpers
- shared text fitting, border drawing, severity color, class-color, and stale-data styling helpers

That work should be additive first and migrate one overlay at a time with screenshot validation, keeping overlay-specific domain layout local.
