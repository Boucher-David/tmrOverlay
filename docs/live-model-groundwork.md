# Live Model Groundwork

The live telemetry boundary now has two layers:

1. Existing overlay-specific slices: fuel, proximity, and leader gap.
2. Additive shared models under `LiveTelemetrySnapshot.Models`, now used directly by the Relative overlay.

The shared models are intended to support simple telemetry-first overlays such as standings, relative, local in-car radar, flags, weather, timing tables, and future map surfaces, plus deeper analysis products, without letting each overlay rediscover the same race state in its form code.

## Compatibility

`LiveTelemetrySnapshot.Models` is live-only and defaulted to `LiveRaceModels.Empty`.

This does not change:

- raw capture files
- compact history summaries or aggregates
- post-race analysis JSON
- existing capture synthesis artifacts
- existing overlay-specific snapshot properties

Relative and Car Radar are additive on top of those compatibility rules: Relative reads `LiveTelemetrySnapshot.Models.Relative`, and Car Radar reads `LiveTelemetrySnapshot.Models.Spatial` for local in-car radar, while the older fuel/gap overlays keep their current inputs until each one is migrated deliberately.

Older data collected on Windows remains readable because the new models are derived from the normalized `HistoricalTelemetrySample` already produced by the collector. The builder tolerates missing timing, driver, weather, pit, and proximity fields by marking the affected model as `Unavailable` or `Partial` instead of throwing.

## Model Families

- `LiveSessionModel`: session clock, lap limits, session labels, track/car display labels, and missing live session signals.
- `LiveDriverDirectoryModel`: session-info driver identity keyed by `CarIdx`, plus player/focus references.
- `LiveTimingModel`: reusable overall/class rows with position, class, lap progress, timing, gap, pit, and driver identity fields.
- `LiveRelativeModel`: local-radar proximity first, with focus/class-gap timing as a fallback for timing-table and relative-style consumers. When proximity has lap-distance placement but no direct relative seconds, relative rows may carry an inferred display-time gap from live lap-distance delta and current lap-time context; radar keeps using the stricter spatial model instead.
- `LiveSpatialModel`: local-radar side occupancy, lap/meter/timing placement, nearest-car, and multiclass-approach state for radar-style consumers; broader focus-relative placement remains an advanced branch.
- `LiveWeatherModel`: live wetness, declared-wet state, temperatures, skies, precipitation, and rubber state.
- `LiveFuelPitModel`: live fuel plus pit-road/service signals.
- `LiveRaceEventModel`: basic on-track, in-garage, Garage-screen-visible, lap, and driver-change context.
- `LiveInputTelemetryModel`: local speed, tire compound, gear/RPM, pedals, steering, engine-warning, electrical, temperature, and pressure signals from normalized samples.

Model rows now carry explicit `LiveSignalEvidence` for source, quality, usability, and missing reason. This evidence is not meant to dominate every overlay. For telemetry-first overlays such as standings, relative, local in-car radar, flags, session/weather, and timing tables, the normal path should render direct iRacing telemetry quietly and only expose evidence when data is stale, unavailable, modeled, or derived. Local in-car radar can stay simple by using local-player side/proximity telemetry only while the user is driving; non-local focus, teammate focus, spectator mode, and multiclass interpretation remain advanced evidence-aware radar cases. Timing rows still distinguish timing availability from spatial progress and radar placement eligibility, so a same-class F2 row can remain usable for standings/relative/gap timing while staying unavailable for map/non-local radar placement. Fuel/pit models distinguish valid fuel level, instantaneous burn diagnostics, rolling measured burn, and measured-baseline eligibility. This keeps model-v2 consumers from treating first-pass overlay heuristics as equally reliable raw facts without turning every normal telemetry row into a confidence report.

Current long-capture findings reflected in the v2 contract:

- `FuelUsePerHour` is diagnostic until smoothed or confirmed by rolling fuel-level deltas.
- `CarLeftRight` side occupancy is separate from decoded nearby-car placement.
- Leader gaps with missing leader F2 timing are partial evidence, not reliable zero-based gaps.
- Raw session flags need semantic normalization before more overlays consume them: background bits such as `serviceable` and `start hidden` should not become user-facing alerts by themselves, while global `SessionFlags` and per-car `CarIdxSessionFlags` should be preserved separately so blue/debris/yellow and driver-specific black/repair/furled states can be displayed in the right scope.
- IBT can enrich local-car post-race trajectory and vehicle dynamics, but raw/live capture remains the source for opponent timing, radar side state, focus, and class-gap context.

The 24-hour live-overlay review adds product semantics on top of those source findings: race-gap graphs should not pretend practice/qualifying/test timing is the same thing as race-position gap; the first radar path should stay local in-car while focus/multiclass cases collect evidence for a later advanced branch; and endurance fuel strategy needs team-stint evidence rather than stitched local scalar fuel.

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

The registry keeps display keys stable for future shared standings/relative UI so table semantics can be validated without copying formatter logic. The first Relative overlay keeps formatting local while those shared primitives are still being refined.

## Parity Mode

Relative was the first product overlay to read the additive model-v2 live state directly through `LiveTelemetrySnapshot.Models.Relative`. The simple Flags, Session / Weather, Pit Service, and Input / Car State overlays also consume `LiveTelemetrySnapshot.Models` directly. Car Radar now reads `LiveTelemetrySnapshot.Models.Spatial` for the simplified local in-car radar contract. The fuel and gap overlays still read their existing overlay-specific snapshot slices while model v2 is observed in parallel.

`LiveModelParityAnalyzer` compares those existing slices against equivalent values in `LiveTelemetrySnapshot.Models` for fuel/pit, proximity/relative/spatial, timing/leader-gap, weather, session, and race-event state. Track Map now consumes `LiveTelemetrySnapshot.Models.TrackMap` directly for live sector-highlight segments over schema-v2 map assets. The Windows collector records sampled parity frames and mismatch summaries through `LiveModelParityRecorder`. A separate `LiveOverlayDiagnosticsRecorder` watches the same normalized snapshots for product assumptions found during the 24-hour race and design-v2 candidate work, including non-race gap semantics, large gap scaling, local-only radar suppression and side/placement evidence, fuel source stitching, intra-lap position cadence, lap-delta channel availability, derived sector-timing coverage, and track-map sector highlight coverage.

At session finalization the app writes `live-model-parity.json`. When raw capture is active, the file is written beside the raw capture sidecars. When raw capture is not active, it is written under the logs model-parity folder. The artifact includes:

- sampled parity frames
- aggregate mismatch counts by family/key
- model-v2 coverage counts versus legacy overlay-input coverage
- `promotionReadiness`, which marks a session as a model-v2 promotion candidate only after it has enough frames, low enough mismatch rate, and enough model coverage for the legacy overlay inputs observed in that session
- post-session signal availability from `capture-synthesis.json`, `telemetry-schema.json`, and `ibt-analysis/*.json`

When `promotionReadiness.isCandidate` is true, the recorder also emits a `live_model_v2_promotion_candidate` app event. Treat that as a review signal, not an automatic migration: overlays should move to model v2 only after several candidate artifacts cover the normal session types and edge cases we care about.

This lets collected raw/IBT sessions evaluate whether model v2 matches current overlay behavior before any overlay is migrated to the new model.

`live-overlay-diagnostics.json` is not a parity artifact and should not be used as a pass/fail gate. It is a bounded evidence artifact for deciding which model-v2 behavior branches to do next.

## Deferred Overlay UI V2

Model v2 is data-contract work, not a visual architecture rewrite. Overlay UI/style v2 should render model-v2 telemetry directly by default: standings, relative, local in-car radar, flags, session/weather, and timing tables should feel like simple windows into iRacing telemetry. Source, quality, usability, and missing-reason evidence should become exception UI for stale, unavailable, modeled, or derived values. Competitor overlay analysis keeps the product shape grounded in small, dense, purpose-built overlays.

A separate overlay UI/style v2 pass should eventually standardize shared visual primitives across the WinForms overlays:

- semantic theme tokens for spacing, typography, borders, severity, table, and graph roles
- reusable header, status badge, source footer, metric row, table cell, graph panel, and empty/error/waiting-state helpers
- shared text fitting, border drawing, severity color, class-color, and stale-data styling helpers

That work should be additive first and migrate one overlay at a time with screenshot validation, keeping overlay-specific domain layout local. Shared primitives should be able to consume model-v2 source/evidence state directly instead of each overlay inventing its own live, degraded, stale, history-backed, or unavailable display language.

While model-v2 parity data is still being collected, the ignored mac harness owns the design-v2 proving ground. Its generated `mocks/design-v2/` contact sheet should iterate first on telemetry-first standings, relative, local blindspot/radar signal, flag, session/weather, and table primitives, then keep evidence-aware badges, source footers, graph context, and deterministic unavailable states for the analysis overlays that need them before those primitives are ported into Windows. The current mac candidates also include sector comparison, laptime delta, and stint laptime log designs; sector comparison and laptime delta now have live diagnostics for input readiness, but still need an explicit model-v2 UI contract before promotion to Windows overlays.
