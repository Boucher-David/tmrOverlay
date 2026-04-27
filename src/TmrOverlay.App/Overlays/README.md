# Overlay Modules

Each overlay type should live in its own folder under `Overlays/`.

Overlay modules own presentation-specific code:

- default size and identity
- view/form implementation
- overlay-specific labels, colors, and layout
- overlay-specific persisted settings

Every user-visible overlay should be draggable and should remember its position, size, opacity, and always-on-top preference by overlay id. On Windows, derive overlay forms from `Abstractions/PersistentOverlayForm` and register child controls that should act as drag handles. In the mac harness, create overlay windows through `OverlayManager` so shared `OverlayWindow` frame persistence is used.

Overlay modules should not talk directly to iRacing or raw capture files. They should consume shared state, metrics, and history services from the app/core layers so multiple overlays can reuse the same telemetry interpretation.

The tray shell should not construct overlay windows directly. Add new overlay windows through `OverlayManager` so multiple overlay types can run together and share the same lifecycle/settings behavior.

Shared data sources intended for overlays:

- `Telemetry/Live/LiveTelemetryStore` - latest normalized live frame and derived live fuel state.
- `History/SessionHistoryQueryService` - user aggregate lookup by car/track/session combo, with opt-in baseline/sample lookup.
- `Telemetry/TelemetryCaptureState` - collector health/status only, not product metrics.

Current modules:

- `Status/` - tiny collector status overlay with runtime raw-capture request checkbox
- `FuelCalculator/` - fuel/stint strategy overlay with tire-stop guidance from history
- `CarRadar/` - transparent circular proximity radar from `CarLeftRight` plus `CarIdx*` track-position arrays, with car rectangles fading from red to yellow to transparent as traffic moves away
- `GapToLeader/` - four-hour in-class gap trend graph from `CarIdxF2Time`, standings, and `CarIdx*` progress, with a bounded in-memory trace, adaptive Y-axis scaling, left-side axis labels, lap reference lines, weather bands, driver/leader-change markers, and endpoint position labels

Windows overlay code is production-facing and should stay real-data-driven. Use the ignored mac harness for looser development scenes such as fixed race offsets, named mock drivers, synthetic weather windows, and exaggerated graph events.

Expected future modules:

- `Standings/`
- `Relative/`
- `CompetitionDistanceGraph/`
- `Weather/`
