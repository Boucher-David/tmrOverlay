# Model V2 Future Branch Notes

This investigation branch should stay focused on evidence and passive tooling, with one production simplification: radar proximity is now local in-car only. The branch adds model-v2 contracts, parity artifacts, IBT local-car analysis, raw-capture assumption analysis, and live overlay diagnostics.

Relative now consumes `LiveTelemetrySnapshot.Models.Relative` directly, and Car Radar consumes `LiveTelemetrySnapshot.Models.Spatial` for the simplified local in-car radar contract. Fuel and gap still read the legacy fuel/gap slices, while the rest of the `LiveTelemetrySnapshot.Models` layer and diagnostics artifacts remain review inputs for the next implementation branches.

## This Branch Scope

- Keep raw capture, IBT logging, capture synthesis, IBT sidecars, model parity, and live overlay diagnostics enabled by default with bounded output and failure isolation.
- Preserve compatibility with already collected raw captures and synthesized data. New sidecars are additive; older captures remain readable.
- Record enough evidence to decide future overlay behavior from data:
  - `live-model-parity.json` for model-v2 coverage/mismatch and promotion-readiness.
  - `live-overlay-diagnostics.json` for gap/radar/fuel/position-cadence/lap-delta/sector-timing assumptions that came from the 24-hour race and design-v2 candidates.
  - `ibt-analysis/ibt-local-car-summary.json` for local-car trajectory, fuel, and vehicle-dynamics readiness.
  - `tools/analysis/analyze_capture_assumptions.py` for offline raw-capture checks, including sampled intra-lap position/class-position changes.
- Use the existing `live_model_v2_promotion_candidate` app event as the first "enough evidence to review cutover" signal. It is not an automatic migration trigger.

## Future Branches

### Fuel Strategy V2

Rebuild strategy around a team-stint model rather than stitched scalar fuel. The model should combine local measured fuel windows, team/focus progress, completed stint lengths, pit/service history, max-fuel constraints, and explicit measured/model/source labels.

Do this before trusting time-saving stint suggestions. Strategy output should reject impossible or misleading stint rhythms when current-session stints and historical evidence disagree with a shorter suggestion.

### Local In-Car Radar V2

Keep the first radar v2 path simple by wiring it entirely to the local player while the user is in the car. In that mode, `CarLeftRight` and local proximity/timing can be treated as direct telemetry for a compact warning surface instead of a broad focus-relative analysis product.

Hide or degrade the local radar when the user is not in the car, is spectating, is in replay/garage, or when the local-player telemetry needed for side/proximity display is unavailable. Do not try to explain teammate/spectator focus in this simple overlay. The current live proximity slice follows that rule by suppressing explicit non-player focus and pit/garage contexts.

Windows Car Radar now follows this v2 path by reading `LiveTelemetrySnapshot.Models.Spatial` for local side occupancy, local placement, timing fallback, and multiclass warning state. The legacy `LiveProximitySnapshot` remains as an internal compatibility/diagnostic slice until parity and diagnostics no longer need it.

### Radar Focus And Multiclass V2

Treat non-local focus, teammate focus, spectator mode, and multiclass warning as the advanced radar branch. Move those cases to model-v2 focus-relative evidence. Keep `CarLeftRight` local-player scoped, suppress or relabel it for non-player focus, and make teammate/team-car focus states explicit.

This branch should also define what the radar displays when spatial progress is missing but timing rows are valid.

Keep the collector looking for examples instead of dropping the evidence. `live-overlay-diagnostics.json` now records local-only radar suppression, pit/garage unavailability, local progress-missing frames, raw side signals suppressed for non-player focus, side-without-placement frames, timing-only placement rows, spatial placement rows, and multiclass approach frames so real captures can drive the advanced branch.

### Gap Graph V2

Split race-position gap behavior from practice/qualifying/test timing behavior. Race sessions can use leader/class gap semantics; non-race sessions need a separate timing mode or should stay hidden by default.

Improve readability for multi-lap gaps by separating leader-gap context from local-battle deltas, using lap-aware scaling, or moving threat/leader interpretation into future relative/standings/strategy overlays.

### Position Cadence And Timing Tables

Use raw-capture and live-overlay diagnostics to confirm whether `CarIdxPosition` and `CarIdxClassPosition` update intra-lap often enough for standings/relative overlays. The same diagnostics now count live lap-delta channel availability and best-effort sector-boundary intervals derived from session-info sectors plus `CarIdxLapCompleted`/`CarIdxLapDistPct`. If confirmed across enough sessions, promote position cadence, lap-delta readiness, and sector timing into explicit model-v2 timing-table assumptions.

### Session Bootstrap And Car Coverage

Treat mid-session joins as a first-class model-v2 availability case. The app should assume raw frame history starts when the local SDK connection starts, but current session context can still exist immediately through session YAML and current `CarIdx*` rows: roster, car classes, event/session metadata, current standings/result snapshots, completed laps, best/last laps where exposed, current session clock, and current track position for transmitted cars. A future diagnostics pass should compare a known mid-race join against a full-session capture and label which fields are real current-session context versus local-observation-only history.

Treat user-configured iRacing car coverage limits as a separate model-v2 completeness signal. Session YAML can describe the entered field, while live `CarIdx*` arrays may only carry currently transmitted cars. Standings, relative, gap, and post-race analysis should not silently treat missing live rows as retired or unknown competitors. Model v2 should expose roster count, live-row count, rows with timing, rows with spatial progress, missing-row reasons, and whether each overlay is showing full-field standings, nearby transmitted cars, or a partial mixed view.

### Capture-Backed Mac Overlay Replay

The mac harness now records live overlay diagnostics from mock/demo snapshots, including the four-hour preview and capture-derived radar/gap demos. A future harness branch should add a full raw-capture replay provider that decodes selected 4-hour/24-hour captures into normalized live snapshots at high playback speed, drives one instance of each overlay, and writes screenshots plus `live-overlay-diagnostics.json` from that replay.

That replay provider should be a development tool only. It should read existing captures, skip or downsample aggressively, and avoid changing the Windows collector/runtime path.

### IBT-Derived Track Map Store

Treat post-session track-map generation as a future derived-asset product branch, not as part of the current compact IBT sidecar contract. Today `ibt-analysis/ibt-local-car-summary.json` only records whether `Lat`/`Lon`/`Alt` plus lap-distance fields make an IBT file track-map-ready. A track-map branch should turn that readiness signal into a durable reusable map asset.

Generated user maps should live in app-owned local storage, outside the iRacing telemetry folder and outside retention-managed capture directories. Add an explicit storage root such as `Storage:TrackMapRoot`, defaulting under `%LOCALAPPDATA%\TmrOverlay\track-maps`, with user-discovered maps separated from any future bundled/baseline maps.

The first implementation should keep source `.ibt` files external and persist only compact derived geometry: schema version, generated time, track identity, source/provenance summary, quality metrics, coordinate-system metadata, and a resampled closed polyline in local meter coordinates with lap-distance percentages. Prefer normalized local coordinates over raw latitude/longitude in the reusable map file unless raw geographic values are explicitly needed for diagnostics.

Build this behind a separate `IbtTrackMapBuilder` and `TrackMapStore` instead of extending overlays to read IBT files directly. The builder should select clean complete laps, filter pit/outlap/noisy samples where possible, convert `Lat`/`Lon` to a local tangent-plane coordinate system, smooth/resample/simplify the line, score coverage and closure quality, and merge only when a new source improves an existing map for the same track identity.

Overlays should consume maps through the store. A live track-map overlay can then place the local car and other cars from normalized live progress fields such as `CarIdxLapDistPct`; it should treat an IBT-derived map as the learned driven line for that track/config, not as official track boundaries.

### Uniform Model V2 Migration

Relative and Car Radar are production overlays consuming `LiveTelemetrySnapshot.Models` directly. After several clean `live_model_v2_promotion_candidate` sessions cover race, practice/test, pit cycles, driver swaps, focus changes, multiclass traffic, and large-gap cases, continue migrating the remaining overlays one at a time to `LiveTelemetrySnapshot.Models`.

Keep migration additive:

1. Switch one overlay to model-v2 inputs behind tests.
2. Keep legacy slice fields stable until no current overlay depends on them.
3. Compare screenshots and captured sidecars before removing old overlay-local interpretation.

### Overlay UI/Style V2

Model v2 does not standardize visual code by itself. Treat overlay UI/style v2 as the presentation language for model-v2 telemetry first, with evidence/source UI reserved for stale, unavailable, modeled, or derived values.

The alignment is:

- Data Model V2 defines normalized telemetry: stable rows, session context, direct iRacing values, availability, source, freshness, quality, usability, and missing reasons.
- Design Paradigm V2 renders simple telemetry windows by default: standings, relative, local in-car radar, flags, session/weather context, timing tables, and similar overlays should be dense, stable, and quiet unless data is stale, unavailable, modeled, or derived.
- Evidence/source UI is exception chrome for analysis products: fuel strategy, non-local radar focus/multiclass interpretation, gap graphs, stint planning, and other app-derived decisions can show measured/model/history labels, source footers, and deterministic unavailable states.
- Competitor overlay analysis validates the product shape: small purpose-built overlays, low-noise dark translucent styling, dense information layout, strong semantic color, and multiple overlays visible at once rather than one monolithic dashboard.

A separate UI/style branch should add shared semantic theme tokens and reusable WinForms primitives for headers, quiet status badges, metric rows, timing tables, relative tables, flag strips, compact weather widgets, optional header/footer context slots, validation/admin source footers, graph panels, borders, class/severity colors, text fitting, and empty/error/waiting states. Those primitives should be able to consume model-v2 source/evidence state directly, but the normal rendering path should not make confidence the center of the UI.

Use the ignored mac harness as the design-v2 proving ground while model-v2 evidence is still being collected. The mac preview path can render deterministic design-v2 states under `mocks/design-v2/` for standings, relative, flag display, and the narrower analysis-exception state. Promote only the primitives and semantics that survive screenshot review back into Windows.

Migrate style one overlay at a time with screenshot validation.

### Overlay Bridge / External Overlay Platform

The disabled-by-default localhost overlay bridge should become the boundary for external overlay clients after the normalized live snapshot schema is stable enough. Treat it as a platform branch, not as another in-process overlay.

Bridge v2 should define:

1. Versioned snapshot contracts for model-v2 telemetry, app health, overlay metadata, and selected display settings.
2. Safe local access controls, explicit enable/disable controls, port/client status, and schema-version display in the settings panel.
3. A browser/client development path that consumes normalized app state rather than talking to iRacing directly.
4. Compatibility rules for bridge clients when model-v2 fields are added, renamed, deprecated, or unavailable.
5. An opt-in peer/session context path for trusted teammates to share missed-history summaries when one user joins mid-race after another user has already observed the session.

This branch fits after enough Windows overlays consume `LiveTelemetrySnapshot.Models` that the external schema reflects real product semantics instead of temporary overlay-local assumptions.

The peer context path should be treated as derived context exchange, not raw telemetry sync. It should carry provenance, session identity, observation window, roster/timing coverage, schema version, and trust/source labels, then merge only into model-v2 availability as partial remote context. It should not silently overwrite local telemetry, and it should not share raw `telemetry.bin`, source `.ibt` files, or private local history by default.

### Streaming / Broadcast Overlays

Twitch and YouTube chat overlays belong in a streaming/broadcast group. They are not model-v2 telemetry primitives, but they can sit beside telemetry overlays as stream-facing presentation surfaces.

The first streaming branch should keep chat ingestion isolated from iRacing telemetry and define rate limits, authentication/storage, moderation controls, reconnect behavior, and deterministic offline preview states. Once the overlay bridge exists, browser-based stream overlays can consume either chat-only state or a mixed bridge feed with telemetry plus chat, depending on the product shape.

### Overlay Builder

An overlay builder is a future creator/development platform, not a prerequisite for the first Windows overlays. It should build on design-v2 primitives and the overlay bridge schema after both are stable.

Builder v1 should likely start as a local development tool for arranging simple telemetry widgets, choosing shared theme tokens, and exporting deterministic preview states. Only later should it become a user-facing editor for custom overlays. Keep the production Windows overlays hand-authored until the builder can generate layouts that meet the same readability, session-filter, stale-state, screenshot-validation, and performance expectations.

### Application Publishing

Publishing is a separate app-platform branch and is the planned v0.9 target. The current settings panel exposes copyable local clean/build/publish/zip commands, but that is not a release system.

A real publishing branch should define signed Windows artifacts, versioning, installer or portable packaging, update-channel policy, release notes, rollback/compatibility expectations for durable user settings/history, diagnostics bundle expectations for tester builds, and CI validation that can replace the current "copy a command and zip a folder" workflow. It should also derive executable, tray, and future overlay-branding icon assets from `assets/brand/` source images rather than binding wide source PNGs directly into the app.

### Telemetry-First Overlay Branches

Do not wait for the deep-dive analysis products before building every overlay. Standings, relative, local in-car radar, flag display, session/weather, and timing-table overlays can be much simpler windows into normalized iRacing telemetry.

For these overlays, model v2 should prioritize stable row identity, column formatting, class/session labels, pit/flag/session state, freshness, and predictable unavailable states. It should still carry `LiveSignalEvidence`, but normal UI should only surface that evidence when the data is stale, unavailable, modeled, or derived.

The flag pass adds one model-v2 requirement: normalize flag categories rather than making each overlay inspect raw SDK bits. Preserve raw global `SessionFlags` and per-car `CarIdxSessionFlags`, but expose user-facing categories such as green/start, blue, yellow/debris/caution, finish/countdown, and critical driver flags. Treat `serviceable` and `start hidden` as background context unless combined with a displayable category.

Current design-v2 candidate readiness:

- Standings can consume `LiveTelemetrySnapshot.Models.Timing` rows and `TimingColumnRegistry` formatting once a production overlay is added.
- Flags, session/weather, pit-service snapshot, and input/car-state overlays now have first-pass Windows implementations that consume `LiveTelemetrySnapshot.Models` directly. Flags and input/car-state use custom graphical forms while session/weather and pit-service continue through the simple telemetry shell.
- Sector comparison is a simple table visually, and live diagnostics now test whether sector metadata plus car progress can produce enough sector-boundary intervals. It still needs an explicit model-v2 row contract before it should be promoted beyond the mac design surface.
- Blindspot signal should stay local in-car only and can use the existing local-player `CarLeftRight`/proximity state without the advanced non-local radar branch.
- Laptime delta can now use diagnostics to confirm live delta-channel availability and `_OK` usability across sessions. Until that evidence is broad enough and represented in model v2, it remains a design/model target rather than a reliable Windows overlay.
- Stint laptime log can stay simple if it plots completed local lap times, resets on stint boundaries, and avoids strategy interpretation.

### Pit Crew / Engineer Overlay

Treat pit crew/engineer as a future analysis/operator overlay, not as a hidden feature inside the read-only pit-service snapshot. A spotter or engineer watching a team race needs a different telemetry surface than the in-car driver: pit stop request state, repair/fuel/tire choices, stint/fuel analysis, driver-control context, team-car status, and potentially command-capable controls for iRacing pit service variables.

Before adding command controls, isolate simulator writes behind an explicit pit-command service and UI state that makes scope clear. iRacing pit commands are active-car commands, so spectator/teammate behavior needs live validation before any team-operator workflow can promise control over the car being watched. The first pit-service overlay should therefore stay read-only while captures collect the pit/service/setup-change evidence needed for the engineer product.
