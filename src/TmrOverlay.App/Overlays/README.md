# Overlay Modules

Each overlay type should live in its own folder under `Overlays/`.

Overlay modules own presentation-specific code:

- default size and identity
- view/form implementation
- overlay-specific labels and layout
- overlay-specific persisted settings

Every user-visible overlay should be draggable and should remember its position, size, opacity, and always-on-top preference by overlay id. On Windows, derive overlay forms from `Abstractions/PersistentOverlayForm` and register child controls that should act as drag handles. In the mac harness, create overlay windows through `OverlayManager` so shared `OverlayWindow` frame persistence is used.

Put common Windows overlay colors, typography, and chrome/layout constants in `Styling/OverlayTheme.cs`. User-editable overrides can live in `overlay-theme.json` under the app settings root. Keep chart/car-specific palette decisions next to the drawing code when the color is part of the data visualization itself. The mac harness mirrors this convention with `Overlays/Styling/OverlayTheme.swift`.

Overlay modules should not talk directly to iRacing or raw capture files. They should consume shared state, metrics, and history services from the app/core layers so multiple overlays can reuse the same telemetry interpretation.

The tray shell should not construct overlay windows directly. Add new overlay windows through `OverlayManager` so multiple overlay types can run together and share the same lifecycle/settings behavior.

Shared data sources intended for overlays:

- `src/TmrOverlay.Core/Telemetry/Live/ILiveTelemetrySource` - latest normalized live frame and derived live fuel/proximity/gap state. Providers should write through `ILiveTelemetrySink`.
- `History/SessionHistoryQueryService` - user aggregate lookup by car/track/session combo, with opt-in baseline/sample lookup.
- `Telemetry/TelemetryCaptureState` - collector health/status only, not product metrics.

Current modules:

- `Status/` - tiny display-only collector status overlay
- `SettingsPanel/` - centered tabbed settings overlay for user-managed visibility, scale, session filters, shared font/units, runtime raw-capture requests, placeholder Overlay Bridge controls, post-race analysis browsing, and overlay-specific display options
- `FuelCalculator/` - fuel/stint strategy overlay with tire-stop guidance from history
- `CarRadar/` - transparent circular proximity radar from `CarLeftRight` plus `CarIdx*` track-position arrays, with car rectangles fading from red to yellow to transparent as traffic moves away
- `GapToLeader/` - four-hour in-class gap trend graph from `CarIdxF2Time`, standings, and `CarIdx*` progress, with a bounded in-memory trace, adaptive Y-axis scaling, left-side axis labels, lap reference lines, weather bands, driver/leader-change markers, and endpoint position labels

Windows overlay code is production-facing and should stay real-data-driven. Use the ignored mac harness for looser development scenes such as fixed race offsets, named mock drivers, synthetic weather windows, and exaggerated graph events.

Expected future modules:

- `Standings/`
- `Relative/`
- `CompetitionDistanceGraph/`
- `Weather/`
