# Overlay Modules

Each overlay type should live in its own folder under `Overlays/`.

Overlay modules own presentation-specific code:

- default size and identity
- view/form implementation
- overlay-specific labels and layout
- overlay-specific persisted settings

Every driving overlay should be draggable and should remember its position, size, opacity, and always-on-top preference by overlay id. On Windows, derive overlay forms from `Abstractions/PersistentOverlayForm` and register child controls that should act as drag handles. Product overlays should keep the tool-window/always-on-top behavior that lets them sit over the sim; the settings window opts out so it behaves like a normal desktop window, recenters whenever it opens, and does not persist user placement. In the mac harness, create overlay windows through `OverlayManager` so shared `OverlayWindow` frame persistence is used for driving overlays.

Put common Windows overlay colors, typography, and chrome/layout constants in `Styling/OverlayTheme.cs`. User-editable overrides can live in `overlay-theme.json` under the app settings root. Keep chart/car-specific palette decisions next to the drawing code when the color is part of the data visualization itself. The mac harness mirrors this convention with `Overlays/Styling/OverlayTheme.swift`; when mac and Windows screenshots drift, prefer aligning shared role tokens, sizes, weights, row heights, padding, and state colors before changing overlay-specific domain drawing.

Overlay modules should not talk directly to iRacing or raw capture files. They should consume shared state, metrics, and history services from the app/core layers so multiple overlays can reuse the same telemetry interpretation.

The tray shell should not construct overlay windows directly. Add new overlay windows through `OverlayManager` so multiple overlay types can run together and share the same lifecycle/settings behavior.

Shared data sources intended for overlays:

- `src/TmrOverlay.Core/Telemetry/Live/ILiveTelemetrySource` - latest normalized live frame and derived live fuel/proximity/gap state. Providers should write through `ILiveTelemetrySink`.
- `History/SessionHistoryQueryService` - user aggregate lookup by car/track/session combo, with opt-in baseline/sample lookup.
- `Telemetry/TelemetryCaptureState` - collector health/status only, not product metrics.

Current modules:

- `Status/` - tiny display-only collector status overlay
- `SettingsPanel/` - branded fixed-size tabbed settings window with the TMR logo, `Tech Mates Racing Overlay` header, and flat vertical left tabs for user-managed visibility, scale when applicable, session filters, units, support/log/performance access, runtime diagnostic capture, and overlay-specific display options
- `FuelCalculator/` - fuel/stint strategy overlay with tire-stop guidance from history
- `Standings/` - compact same-class timing table from `LiveTelemetrySnapshot.Models.Timing`, with class position, car number, driver, leader gap, focus interval, pit-road status, and waiting/error states
- `Relative/` - telemetry-first relative table from `LiveTelemetrySnapshot.Models.Relative`, with stable configured ahead/reference/behind row slots, live or inferred display-time gaps, timing fallback labeling, and quiet source text unless rows are waiting or degraded
- `TrackMap/` - transparent map-only track surface with generated local geometry when available, circle fallback otherwise, and live car dots placed from lap-distance progress
- `StreamChat/` - native read-only Twitch chat overlay plus localhost browser-source route for one saved chat source at a time: Streamlabs Chat Box widget URL or public Twitch channel chat
- `GarageCover/` - opaque streamer privacy cover that appears only while iRacing reports the Garage screen as visible; imported cover images are copied into app-owned settings storage
- `CarRadar/` - transparent circular local in-car proximity radar from `LiveTelemetrySnapshot.Models.Spatial`, using fresh local player/team progress, `CarLeftRight`, `CarIdxF2Time`, `CarIdxEstTime`, and `CarIdx*` progress arrays, hidden for explicit non-player focus/garage/pit contexts, with car rectangles placed from physical lap-distance meters when available, timing fallback when needed, side occupancy anchored by local `CarLeftRight`, likely side-warning cars attached to the side slot instead of duplicated in the center lane, neutral-white car rectangles fading in between radar entry and the yellow-warning threshold, and proximity color moving through yellow toward saturated alert red only inside the close bumper-gap warning buffer around the local car
- `GapToLeader/` - four-hour in-class gap trend graph from `CamCarIdx`, `CarIdxF2Time`, standings, and `CarIdx*` progress, with a bounded in-memory trace, adaptive Y-axis scaling, left-side axis labels, lap reference lines, weather bands, driver/leader-change markers, and endpoint position labels for the focused car context

Windows overlay code is production-facing and should stay real-data-driven. Use the ignored mac harness for looser development scenes such as fixed race offsets, named mock drivers, synthetic weather windows, and exaggerated graph events.

Expected future modules:

- `CompetitionDistanceGraph/`
- `Weather/`
