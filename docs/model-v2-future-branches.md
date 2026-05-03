# Model V2 Future Branch Notes

This investigation branch should stay focused on evidence and passive tooling, with one production simplification: radar proximity is now local in-car only. The branch adds model-v2 contracts, parity artifacts, IBT local-car analysis, raw-capture assumption analysis, and live overlay diagnostics.

Relative now consumes `LiveTelemetrySnapshot.Models.Relative` directly, and Car Radar consumes `LiveTelemetrySnapshot.Models.Spatial` for the simplified local in-car radar contract. Fuel and gap still read the legacy fuel/gap slices, while the rest of the `LiveTelemetrySnapshot.Models` layer and diagnostics artifacts remain review inputs for the next implementation branches.

## This Branch Scope

- Keep raw capture and advanced diagnostics bounded with failure isolation. For tester builds, raw capture, model parity, live overlay diagnostics, and edge-case clips should stay opt-in/disabled by default unless a branch explicitly collects evidence.
- Preserve compatibility with already collected raw captures and synthesized data. New sidecars are additive; older captures remain readable.
- Record enough evidence to decide future overlay behavior from data:
  - `live-model-parity.json` for model-v2 coverage/mismatch and promotion-readiness.
  - `live-overlay-diagnostics.json` for gap/radar/fuel/position-cadence/lap-delta/sector-timing assumptions that came from the 24-hour race and design-v2 candidates.
  - `ibt-analysis/ibt-local-car-summary.json` for local-car trajectory, fuel, and vehicle-dynamics readiness.
  - `tools/analysis/analyze_capture_assumptions.py` for offline raw-capture checks, including sampled intra-lap position/class-position changes.
- Use the existing `live_model_v2_promotion_candidate` app event as the first "enough evidence to review cutover" signal. It is not an automatic migration trigger.

## Suggested V0.X Roadmap

Treat these as branch-size product bets, not fixed promises. If teammate testing exposes a painful install, support, or telemetry issue, pull that forward even if the nominal version order says otherwise.

### v0.11.0 - Teammate Beta Hardening

Goal: make the first shared builds easier to install, understand, and support.

Likely scope:

- Add visible in-app version/build metadata in Settings or Support.
- Add a manual `Check for updates` action that can compare the running version against the latest GitHub Release or release manifest, then point the user to the release page. Avoid modal prompts during active sessions.
- Tighten the Support tab from real teammate feedback: clearer current issue text, diagnostics bundle status, copied-path behavior, and what to send back.
- Validate portable upgrade behavior against existing `%LOCALAPPDATA%\TmrOverlay` settings/history/diagnostics data.
- Polish first-run and no-iRacing-connected states so testers do not confuse expected waiting states with broken installs.
- Keep signed installer/update automation out of scope unless v0.9 release friction proves the portable zip is not enough for private testers.

### v0.12.0 - Overlay Bridge V1

Goal: turn the disabled local bridge into a documented developer/platform boundary.

Likely scope:

- Define versioned local JSON contracts for live telemetry, app health, overlay metadata, selected display settings, and schema capabilities.
- Keep the bridge local and disabled by default with explicit settings/support visibility for enabled state, port, client count, last error, and schema version.
- Use normalized `LiveTelemetrySnapshot.Models` where available instead of exporting overlay-local temporary calculations.
- Add deterministic bridge fixture tests and sample payloads so external clients can be developed without iRacing running.
- Document a future peer/missed-history path without implementing broad teammate sync yet. If a small proof is included, it should exchange derived session context only: provenance, session identity, observation window, roster/timing coverage, schema version, and trust labels.

### v0.13.0 - Model V2 Overlay Migration

Goal: continue moving production overlays to the normalized model layer one overlay at a time.

Likely scope:

- Migrate the remaining simple overlays that are ready: standings, session/weather, pit service, inputs, and selected timing-table surfaces.
- Add explicit model-v2 availability/completeness state for mid-session joins and iRacing car-coverage limits.
- Keep legacy live slices stable until no production overlay depends on them.
- Expand tests around partial fields, stale data, unavailable reasons, and mixed full-roster/live-row views.
- Use Windows screenshot artifacts and tracked mac mocks to catch visual regressions as each overlay changes input source.

### v0.14.0 - Overlay Style V2 / Designer-Friendly UI

Goal: make the overlays and settings surface easier to review, maintain, and hand to a designer.

Likely scope:

- Promote shared visual primitives: overlay headers, quiet status badges, timing rows, relative rows, metric rows, flag strips, compact weather widgets, graph panels, borders, state tones, and text-fitting rules.
- Keep typography, row heights, spacing, opacity defaults, and semantic colors in shared tokens instead of scattered form-local constants.
- Use the mac harness for fast style iteration, then port stable primitives back to Windows.
- Keep normal telemetry-first overlays quiet; only surface source/evidence chrome for stale, unavailable, modeled, or derived values.
- Refresh tracked `mocks/` and compare Windows CI artifacts before calling the branch done.

### v0.15.0 - Session Completeness And Bootstrap

Goal: make standings, relative, gap, and analysis overlays honest about what the local SDK knows when the user joins late or has limited car coverage.

Likely scope:

- Promote mid-session join handling into explicit model-v2 state: local observation start, session clock, roster availability, current timing rows, best/last lap availability, and missing local history.
- Treat iRacing car-count/transmitted-row limits as first-class completeness signals rather than silently hiding missing competitors.
- Expose roster count, live row count, rows with timing, rows with spatial progress, and missing-row reasons.
- Decide how each overlay labels full-field, nearby-transmitted, partial, or mixed views.
- Use real captures plus diagnostics to validate the assumptions before updating user-facing copy.

### v0.16.0 - Capture Replay And Evidence Loop

Goal: make real race evidence easier to replay through overlays without manually driving the app.

Likely scope:

- Add a development-only raw-capture replay provider for the mac harness or a separate tooling path.
- Decode selected four-hour/twenty-four-hour captures into normalized live snapshots at controllable playback speed.
- Drive one instance of each overlay from replayed snapshots, generate screenshots/contact sheets, and emit replay-side `live-overlay-diagnostics.json`.
- Keep replay isolated from the Windows runtime collector and private capture folders.
- Use replay artifacts to decide model-v2 promotions, overlay simplifications, and edge-case UI behavior.

### v0.17.0 - IBT Track Map Store

Goal: turn post-session IBT readiness checks into reusable local track-map assets.

Likely scope:

- Add app-owned storage such as `%LOCALAPPDATA%\TmrOverlay\track-maps`.
- Build `IbtTrackMapBuilder` and `TrackMapStore` around compact derived geometry rather than storing source `.ibt` files.
- Persist schema version, track identity, provenance, quality metrics, local coordinate metadata, lap-distance percentages, and a resampled closed polyline.
- Score clean laps, filter pit/outlap/noisy samples, simplify/smooth geometry, and merge only when a new source improves an existing map.
- Keep overlays consuming maps through the store; do not let overlays read IBT files directly.

### v0.18.0 - Fuel And Strategy V2

Goal: rebuild fuel strategy around team-stint evidence instead of stitched scalar estimates.

Likely scope:

- Combine local measured fuel windows, team/focus progress, completed stint lengths, pit/service history, max-fuel constraints, and source labels.
- Reject impossible stint rhythms when current-session evidence and history disagree.
- Treat tire/repair/pit-service/setup-change evidence as input to strategy but avoid command-capable pit controls in this branch.
- Keep user-facing strategy recommendations conservative until enough teammate race data supports them.

### v0.19.0 - Pit Crew / Engineer Workspace

Goal: explore the operator surface for a spotter or engineer supporting a team car.

Likely scope:

- Start read-only: pit service state, fuel/repair/tire choices, stint/fuel analysis, driver-control context, team-car status, and useful spectator telemetry.
- Add an explicit pit-command service design before any simulator writes.
- Validate iRacing command behavior for active-car, teammate, spectator, and driver-swap states before promising remote/team control.
- Keep command-capable controls gated and obvious if they ever ship.

### v0.20.0 - Streaming And External Overlay Clients

Goal: let the app support broadcast-style surfaces without coupling chat or browser rendering to the Windows collector.

Likely scope:

- Add Twitch/YouTube chat overlay exploration as a separate stream-facing feature set.
- Keep chat auth, tokens, moderation, reconnect behavior, and rate limits isolated from iRacing telemetry.
- Use Overlay Bridge contracts for browser/stream clients once those contracts are stable enough.
- Add deterministic offline preview states for chat-only and mixed telemetry/chat overlays.

### v0.21.0 - Overlay Builder And Designer Tooling

Goal: move toward configurable layouts only after the primitives and bridge contracts are stable.

Likely scope:

- Start as a local development/designer tool for arranging simple telemetry widgets, choosing shared theme tokens, and exporting deterministic previews.
- Generate or validate layouts against readability, session filters, stale-state handling, screenshot validation, and performance rules.
- Keep production overlays hand-authored until generated layouts can meet the same quality bar.

### v0.22.0+ - Installer, Signing, And Broader Platform Work

Goal: graduate from private tester zip builds and desktop overlays toward a broader product platform.

Candidate branches:

- Signed Windows artifacts plus an installer/update channel such as Velopack or MSIX/App Installer.
- Passive update checks tied to the chosen release feed.
- A stronger external-client/plugin story after bridge schemas, auth/local access, versioning, and update policy are settled.

Do not start broad plugin or renderer work by modifying WinForms overlay internals directly. Keep the tray app as telemetry/settings/storage/diagnostics owner and treat new renderers as clients of normalized app state.

## Suggested V1.X Roadmap

### v1.x - VR Renderer / Headset Client

Goal: add VR support only after the desktop app, model-v2 contracts, overlay bridge, and update/publishing path are stable enough to support a separate renderer.

Likely scope:

- Build VR as a dedicated renderer/client using compact model-v2 state through Overlay Bridge or a future snapshot boundary.
- Keep the Windows tray app as the telemetry, settings, storage, diagnostics, update, and release owner.
- Start with sparse local overlays: flag status, blindspot/radar warnings, and compact relative.
- Keep dense standings tables, gap graphs, strategy grids, long diagnostics, and chat-heavy surfaces desktop-first until they have a VR-specific interaction model.
- Treat VR frame budget and comfort as hard product requirements, not style polish. Avoid disk IO, JSON parsing, history lookup, network calls, image decode, or avoidable allocations in the render loop.
- Validate rendering at headset refresh targets before exposing it to teammates.

## Supporting Topic Notes

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

### Windows Screenshot Parity Validation

The mac harness remains the fast local design surface, but Windows is the production/iRacing runtime. v0.10 adds a Windows-only screenshot generator that renders the real WinForms forms with deterministic telemetry fixtures and uploads the resulting contact sheet plus per-state PNGs as GitHub Actions artifacts.

Use this as a parity gate, not as a replacement for the tracked `mocks/` screenshots. The tracked mac screenshots document the intended review states; the Windows artifacts prove the production forms still render, size, and arrange those states under the WinForms runtime. The first parity set should cover settings tabs plus the current production overlays: status, fuel calculator, relative, flags, session/weather, pit service, inputs, radar, and gap to leader.

Keep the fixtures isolated from local history, app data, raw captures, and real machine paths. If a Windows screenshot state needs live telemetry, build it from normalized `LiveTelemetrySnapshot` data with explicit fixture values. If a future overlay needs replay evidence, add that through a separate capture-replay branch rather than letting the screenshot generator read private capture directories.

Use the parity artifacts to identify which visual differences are intentional platform rendering and which should become shared tokens. The likely shared set is color roles, title/status/table font sizes, row heights, padding, border opacity, state tones, graph grid alpha, and empty/error/waiting treatment. Font parity should start with a fallback policy rather than a bundled font or user-facing font picker: Windows defaults to `Segoe UI`, mac defaults to SF/System, and both should keep matching sizes/weights/line heights closely enough for review. If the screenshots still drift after token cleanup, evaluate bundling an OFL font such as Inter as a separate asset/licensing change.

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

### VR Renderer

Treat VR support as a future renderer/client, not as a tweak to the current WinForms desktop overlays. Keep the Windows tray app as the telemetry, settings, storage, diagnostics, and release owner; use the Overlay Bridge or a future model-v2 snapshot boundary for a separate VR renderer when the normalized contracts are stable enough.

The first VR candidates should be sparse and local: flag border/status, blindspot/radar warnings, and a compact relative surface. Dense standings tables, gap graphs, strategy grids, and long text diagnostics should stay desktop-first until they have a VR-specific interaction model.

VR has stricter performance constraints than desktop overlays. At 90 Hz, the frame budget is about 11.1 ms; at 120 Hz, it is about 8.3 ms. Dropped frames are a comfort problem, not just a visual quality issue. A VR renderer should never perform disk IO, JSON parsing, history lookup, image decode, network calls, or avoidable allocations in the render loop. Precompute and double-buffer telemetry snapshots, cache/rasterize text only when values change, minimize transparent overdraw and many independent quads, and prefer stable low-motion positions with larger text and fewer simultaneous overlays.

The model-v2 implication is that VR needs compact, already-interpreted overlay state: local safety signals, current flag category, nearby relative rows, freshness, and unavailable reasons. It should not duplicate raw telemetry interpretation in a headset renderer.

### Streaming / Broadcast Overlays

Twitch and YouTube chat overlays belong in a streaming/broadcast group. They are not model-v2 telemetry primitives, but they can sit beside telemetry overlays as stream-facing presentation surfaces.

The first streaming branch should keep chat ingestion isolated from iRacing telemetry and define rate limits, authentication/storage, moderation controls, reconnect behavior, and deterministic offline preview states. Once the overlay bridge exists, browser-based stream overlays can consume either chat-only state or a mixed bridge feed with telemetry plus chat, depending on the product shape.

### Overlay Builder

An overlay builder is a future creator/development platform, not a prerequisite for the first Windows overlays. It should build on design-v2 primitives and the overlay bridge schema after both are stable.

Builder v1 should likely start as a local development tool for arranging simple telemetry widgets, choosing shared theme tokens, and exporting deterministic preview states. Only later should it become a user-facing editor for custom overlays. Keep the production Windows overlays hand-authored until the builder can generate layouts that meet the same readability, session-filter, stale-state, screenshot-validation, and performance expectations.

### Application Publishing

Publishing is a separate app-platform branch and is the v0.9 target. The v0.9 baseline turns product tags into portable Windows GitHub Release assets with a checksum, but that is still only the first release channel.

A complete publishing path should still define signed Windows artifacts, installer or portable packaging, update-channel policy, rollback/compatibility expectations for durable user settings/history, diagnostics bundle expectations for tester builds, and passive update checks. v0.9 derives the Windows executable icon from `assets/brand/` into `src/TmrOverlay.App/Assets/`; future overlay-branding derivatives should follow the same source-to-platform-asset pattern.

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
