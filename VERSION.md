# Version History

TmrOverlay uses SemVer-style annotated Git tags for product milestones:

- Tags use `vMAJOR.MINOR.PATCH`, for example `v0.6.0`.
- `0.x` versions are pre-release product milestones, so breaking local data or settings changes are still possible when they are documented and migrated.
- Patch versions are reserved for hardening, diagnostics, and compatibility work inside the same product shape.
- The checked-in build version should move with the branch that introduces the next milestone.

## Current Branch Target

### v0.16.2 - Design V2 Theme Foundation

Planned branch name:

```text
v0.16.2-design-v2-theme-foundation
```

Planned scope:

- Build the Design V2 theme foundation before V1.x so future visual changes, overlay additions, settings surfaces, and component iterations can share a tokenized architecture instead of one-off styling.
- Preserve the current production content contracts while making the tracked mac harness V2 settings shell the primary mac settings design for the main app window and the current overlay tabs.
- Make the mac harness capable of opening live Design V2 component-review overlays, not just generating static screenshots.
- Generate component-level outrun review artifacts and full settings-window review artifacts from the same mac-harness views used for live review.
- Keep Design V2 development surfaces out of the Windows production UI until the WinForms V2 layer is ported against the same settings contracts.
- Use stable semantic tokens for surfaces, text, borders, evidence colors, graph chrome, buttons, tabs, table rows, localhost blocks, settings content blocks, and app/overlay chrome so Windows can map the same roles.

Technical implementation checklist:

1. Bump shared .NET product/version metadata to `0.16.2`.
2. Refactor mac Design V2 tokens into named current/outrun theme instances while keeping current visuals as the default.
3. Add live mac-harness Design V2 component overlay entry points through the tray menu and env/CLI launch flags.
4. Render component-level screenshots from the same component overlay views used by the live mac harness.
5. Add the mac V2 settings shell as the primary mac settings design for General, Support, Standings, Relative, Gap To Leader, Fuel Calculator, Session / Weather, Pit Service, Track Map, Stream Chat, Inputs, Car Radar, Flags, and Garage Cover.
6. Preserve current settings semantics for visibility, scale, opacity, session filters, shared header/footer options, content toggles, browser-source routes, Garage Cover image import, and Stream Chat source configuration.
7. Refactor reusable mac settings helpers where V2 and legacy settings share behavior.
8. Expand screenshot validation expectations for Design V2 component and settings-window artifacts.
9. Defer the Windows production V2 port to the next branch, using this branch's mac settings shell and shared content descriptors as the parity reference.

Likely squash title:

```text
[v0.16.2] Add Design V2 theme and mac settings surfaces
```

Likely squash body:

```text
- Bumped shared .NET product/version metadata to 0.16.2.
- Refactored the mac Design V2 proving-ground theme into named current/outrun token sets while preserving current visuals by default.
- Added live mac-harness Design V2 component-review overlays and component screenshots generated from the same views.
- Made the mac V2 settings shell the primary mac settings design for the main app General/Support surfaces and the current overlay settings tabs, including Standings, Relative, Gap To Leader, Fuel Calculator, Session / Weather, Pit Service, Track Map, Stream Chat, Inputs, Car Radar, Flags, and Garage Cover.
- Preserved current production settings contracts while applying V2 visuals: visibility, scale, opacity, sessions, shared header/footer toggles, content toggles, browser-source routes, Garage Cover image import, and Stream Chat source setup remain settings-backed.
- Reused descriptor-driven content rows and shared mac helpers for Garage Cover image storage, Stream Chat provider normalization, sidebar ordering, steppers, toggles, segmented choices, and settings screenshot states.
- Regenerated tracked Design V2 and settings-window screenshot artifacts and updated screenshot validation expectations.
- Updated docs/context for the tracked mac harness, generated settings artifacts, and the Windows V2 parity handoff; the existing Windows settings UI remains the legacy WinForms surface until the explicit V2 port lands.
```

## Next Planned Milestone

### V1.x - Performance Foundation And Post-Candidate Hardening

Likely scope:

- Use the V1.x roadmap for overlay lifecycle/timer efficiency, rendering/cache performance, capture replay, and heavier analysis products.
- Keep hard release-blocking fixes in 0.x only if teammate testing finds installer/update, AppData compatibility, or V1 candidate regressions.

## Merged Mainline Milestones

### v0.16.1 - Overlay Feedback Hardening

Commit: `d39188e`

Squash title:

```text
[v0.16.1] Harden overlay layout, race projection, and diagnostics
```

Summary:

- Bumped shared .NET product/version metadata to 0.16.1.
- Added shared content-column architecture for Standings, Relative, and Inputs with overlay-owned keys, visible/editable pixel widths, stable disabled ordering, matching native/browser defaults, and recommended OBS browser-source dimensions.
- Reworked Standings native/browser behavior around scoring-first start rows, configurable columns, class-colored separators, configurable other-class row blocks, projection-backed class lap estimates, class color caching, and a wider default layout.
- Hardened Gap To Leader for long races with a four-hour visible window, same-lap reference selection, leader/reference transitions, clearer axis labels, and local 4-hour/24-hour telemetry demo coverage.
- Added shared live race projection data for timed-race lap estimates and reused it in Fuel, Session / Weather, Flags, Standings separators, browser-source metadata, and future strategy/fuel calculations.
- Updated Input / Car State native/browser rendering so the line graph is primary and current speed/gear/pedal/steering widgets are toggleable right-side content.
- Reworked Settings overlay tabs into left-side General/Content/Header/Footer sections, with Inputs suppressing unused header/footer tabs and settings save/apply work coalesced to avoid recursive UI churn.
- Added flags/settings freeze hardening and diagnostics, brought the mac harness into source control and CI, and aligned release packaging around the `TMROverlay` package id and `TMROverlay.exe` executable name.

### v0.13.0 - Core Overlay Readiness

Commit: `f9b7026`

Squash title:

```text
[v0.13.0] Harden core overlays on model-v2 telemetry
```

Summary:

- Bumped shared .NET product/version metadata to 0.13.0.
- Added shared model-v2 race-progress, overlay availability/freshness, and app diagnostics status contracts.
- Promoted core overlays to normalized model-v2 consumers across Standings, Relative, local Radar, Flags, Session / Weather, Pit Service, Input / Car State, Fuel, Gap To Leader, and Track Map.
- Reworked Standings around scoring-snapshot ordering, class grouping, configurable other-class rows, pit labels, and localhost browser-source settings.
- Kept Track Map marker placement honest under partial live coverage by plotting only cars with usable spatial progress and using scoring rows only for identity/color enrichment.
- Scoped Radar to accurate local player-in-car proximity and side-warning telemetry while leaving non-local focus and timing-only placement as future evidence-aware work.
- Added Garage Cover image import, preview, localhost fail-closed behavior, and diagnostics.
- Extended compact diagnostics and docs for forecast-like weather, spotter/teammate pit changes, TC/dual-clutch evidence, relative lap-relationship probes, and future V1.N model foundations.

### v0.12.0 - Teammate Beta Hardening

Commit: `267c841`

Squash title:

```text
[v0.12.0] Harden teammate beta install and support flow
```

Summary:

- Bumped shared .NET product/version metadata to 0.12.0.
- Added Support-tab version/build metadata and clearer teammate support actions.
- Hardened waiting/support states for beta handoff.
- Updated validation docs and Windows screenshot expectation checks.
- Kept release/update flow focused on portable teammate beta distribution.

### v0.11.0 - Standings, Track Maps, Localhost, And Live Overlay Polish

Commit: `f6be067`

Squash title:

```text
[v0.11.0] Add standings, track maps, localhost, and live polish
```

Summary:

- Bumped shared .NET product/version metadata to 0.11.0.
- Added production Standings and Track Map overlay registrations, settings tabs, Windows screenshot fixtures, and mac harness review surfaces.
- Added compact Standings rendering from normalized model-v2 timing rows, including leader gap, focus interval, and pit-road status.
- Added a transparent map-only Track Map surface with bundled-map support, circle fallback, live car dots, IBT-derived local map generation, confidence diagnostics, bundled-map assets, and sector-highlight diagnostics.
- Added `LocalhostOverlays` browser-source support for the supported overlay suite while keeping Overlay Bridge separate as a future trusted teammate-to-teammate sharing boundary.
- Added Stream Chat source selection for Streamlabs Chat Box widget URLs or public Twitch channel chat, with diagnostics redaction for private Streamlabs URLs.
- Restored flat settings tabs, kept the larger fixed settings window, and hardened live overlay refresh/fade behavior for Relative, Fuel Calculator, Inputs, and local Radar.
- Regenerated tracked settings/overlay screenshots and updated docs/context for the v0.11 product shape.

### v0.10.0 - Windows Screenshot Parity Validation

Commit: `526c47f`

Squash title:

```text
[v0.10.0] Add Windows screenshot parity validation
```

Summary:

- Added WinForms screenshot generation and CI validation for Windows overlay parity artifacts.
- Branded the app entry point with the TMR logo.
- Removed the user-facing font picker and kept font choice in theme/platform defaults.
- Added a teammate-facing release install/support tutorial image plus validation/docs for the handoff flow.
- Stabilized Windows screenshot rendering and validation against CI runner constraints.
- Updated v0.x/v1/v2 roadmap planning around teammate hardening, overlay bridge, model migration, style v2, replay, track maps, strategy, engineer, streaming, builder, broader platform work, and later VR scope.

### v0.9.0 - Production Publishing And Updates

Commit: `8e4961d`

Squash title:

```text
[v0.9.0] Add production publishing and teammate update flow
```

Summary:

- Bumped shared .NET product/version metadata to 0.9.0 and wired a generated Windows executable icon derived from the checked-in TMR logo.
- Hardened the Windows GitHub Actions workflow so PR/main run restore/build/test, tracked screenshot validation, and a self-contained publish dry run with package audit, while tag/manual release packaging publishes a self-contained win-x64 app, zips it, generates manifest/checksum files, and uploads release artifacts.
- Added tag-driven GitHub Release creation for vMAJOR.MINOR.PATCH tags with versioned Windows zip/manifest/checksum assets and generated release notes.
- Added Windows tester release docs covering package contents, download, checksum verification, portable install, app-data compatibility, schema-migration expectations, upgrade, rollback, unsigned SmartScreen expectations, and diagnostics handoff.
- Reworked the Support tab into a task-oriented surface for app status, diagnostics bundle actions, diagnostic telemetry capture, storage shortcuts, compact app activity, and discoverable advanced collection status.
- Disabled advanced collection outputs by default for tester builds, including edge-case clips, model-v2 parity, live overlay diagnostics, and post-race analysis, while keeping config/env overrides and tests for enabling them.
- Added repo-surface docs and ignore rules to separate customer/tester-facing material, product source, internal development assets, and generated local runtime data.
- Updated the update strategy to treat portable GitHub Releases as the v0.9 baseline while keeping signing, installer selection, and passive update checks as follow-up work.

### v0.8.0 - Settings And Overlay Polish

Commit: `516d54f`

Squash title:

```text
[v0.8.0] Polish settings, overlay controls, and mac review parity
```

Summary:

- Reworked Settings into a fixed-size app control surface with driving overlays hidden by default, ordered user-facing tabs, Support-owned diagnostic capture, and internal/deferred product surfaces removed from the normal tab list.
- Added per-overlay opacity controls where useful, while keeping Flags and Radar exempt, and kept Gap To Leader race-only in settings/runtime policy.
- Polished Relative, Flags, Input/Car State, and Gap behavior: compact actual relative rows, vertical class marker, screen-border flag display that follows active telemetry flags, graphical pedal traces/wheel input, and live-updating gap context.
- Added TMR brand assets plus Windows tray/settings icon loading from the checked-in source logo.
- Brought the ignored mac harness to v0.8 review parity, including matching settings options, telemetry-driven flag display, overlay defaults, regenerated screenshots, and validation updates.
- Bumped settings migration to prune obsolete flag timer options now that flag display lifetime is driven by telemetry.
- Updated branch-complete hygiene so docs, screenshots, stale-reference sweeps, version text, and release metadata are treated as final branch-readiness artifacts.

### v0.7.0 - Simple Model-V2 Overlays, Design-V2 Preview, And Release Hygiene

Commit: `79fbec7`

Squash title:

```text
[v0.7.0] Add simple model-v2 overlays, design-v2 previews, and release hygiene
```

Summary:

- Added production model-v2 overlay paths for relative, flags, session/weather, pit-service, input/car-state, and local in-car radar.
- Added shared simple telemetry overlay shell/view-model helpers for dense direct-telemetry windows.
- Expanded live model-v2 timing, spatial, sector, lap-delta, and diagnostics evidence for simple overlays and later analysis products.
- Added design-v2 mock states for relative, standings, flags, sector comparison, blindspot, laptime delta, and stint laptime log.
- Documented future product branches including IBT-derived track-map generation, pit crew/engineer workflows, overlay bridge, streaming overlays, overlay builder, and publishing.
- Added version-history metadata, branch-complete release hygiene validation, and shared .NET build version metadata.
- Split CI into a stable Windows build/test merge gate and tag-triggered Windows release package artifact.
- Fixed session/weather nullable wet-flag handling and covered unknown wet telemetry with a focused test.

## Tagged Mainline Milestones

### v0.6.0 - Model V2 Groundwork, IBT Analysis, And Overlay Diagnostics

Commit: `109b9b3`

Squash title:

```text
[v0.6.0] Add model-v2 groundwork, IBT analysis, parity, and diagnostics
```

Summary:

- Added normalized live model-v2 contracts, timing column registry, live race model builder, and parity documents.
- Added bounded post-session capture synthesis and model-v2 promotion-readiness evidence.
- Added IBT telemetry logging/analysis sidecars, including local-car trajectory/fuel/vehicle-dynamics readiness and live-vs-IBT schema comparison.
- Added live overlay diagnostics for gap, radar, fuel, position cadence, lap delta, and sector-timing assumptions.
- Added offline raw-capture analysis tooling and compact telemetry-analysis fixtures.
- Added overlay catalog mocks and validation tooling for future design work.

### v0.5.5 - Diagnostics, History Compatibility, And Data-Collection Hardening

Commit: `edf7306`

Squash title:

```text
[v0.5.5] Harden diagnostics, history migration, telemetry edge cases, and screenshots
```

Summary:

- Hardened Windows runtime behavior after data-collection and diagnostic runs.
- Added history schema version constants, startup maintenance, compatible summary normalization, aggregate rebuilds, and schema-compatibility tests.
- Added compact telemetry edge-case detection and recording for suspicious live states.
- Added performance snapshots, app health logging, diagnostics bundle expansion, and retention updates.
- Reworked radar, fuel calculator, gap-to-leader, settings, and status overlay behavior from captured evidence.
- Replaced SVG-only mock states with rendered screenshot artifacts and added screenshot validation tooling.
- Added human-readable overlay logic docs for current overlays and update strategy.

### v0.5.0 - Settings-Owned Overlay Control And Shared Core Refactor

Commit: `c327e64`

Squash title:

```text
[v0.5.0] Move overlay control into settings and split shared Core models
```

Summary:

- Added the settings panel as the primary app control surface for overlay visibility, scale, filters, support actions, and post-race analysis browsing.
- Moved platform-neutral app settings, overlay metadata, historical models, live telemetry abstractions, post-race analysis models, and fuel strategy logic into `TmrOverlay.Core`.
- Added descriptor-driven overlay options and settings migration.
- Added shared overlay theme tokens and centralized overlay manager behavior.
- Added disabled-by-default localhost HTTP scaffolding.
- Added a Windows .NET GitHub Actions build/test workflow.

### v0.4.0 - Radar And Gap-To-Leader Overlays

Commit: `5fa3174`

Squash title:

```text
[v0.4.0] Add proximity radar and gap-to-leader graph overlays
```

Summary:

- Added first-pass car radar and class gap-to-leader overlays.
- Expanded live telemetry snapshots with proximity, class timing, track progress, and gap graph inputs.
- Added car-radar and gap-to-leader screenshot mocks.
- Added parsing and history support needed for class/track context.
- Added proximity and live telemetry store tests.

### v0.3.0 - Fuel Calculator V1

Commit: `10f7d9d`

Squash title:

```text
[v0.3.0] Add fuel calculator v1 and stint-history support
```

Summary:

- Added the fuel calculator overlay and first fuel strategy calculator.
- Added live telemetry storage for fuel, lap, pit, and session context.
- Expanded session history summaries, aggregates, and query services for stint/fuel estimates.
- Added persistent overlay window behavior and overlay manager support for multiple overlays.
- Added fuel calculator mocks, post-race strategy notes, and focused tests.

### v0.2.1 - Post-Capture Collection Hardening

Commit: `838154b`

Squash title:

```text
[v0.2.1] Harden capture status, history export, and build freshness checks
```

Summary:

- Improved failed/waiting/live telemetry messaging in the status overlay.
- Added runtime build-freshness checks so local builds warn when source files are newer than the executable.
- Hardened raw capture session state, manifest metadata, status snapshots, and replay behavior.
- Added history export and YAML forensics tools for analyzing collected captures.
- Added baseline synthetic endurance history fixtures for future overlay work.

### v0.2.0 - App Boilerplate, Storage, History, And Mac Harness

Commit: `3d93d33`

Squash title:

```text
[v0.2.0] Add app storage, history, diagnostics, replay, and mac harness
```

Summary:

- Reorganized the Windows app into shell, overlay, storage, history, logging, settings, diagnostics, runtime, retention, and replay modules.
- Added app-owned local storage defaults for captures, history, logs, settings, diagnostics, events, and runtime state.
- Added compact car/track/session history summaries with baseline/user history separation.
- Added rolling logs, JSONL app events, runtime clean-shutdown markers, diagnostics bundles, and startup retention cleanup.
- Added persisted overlay settings and replay-mode scaffolding.
- Added the ignored local macOS harness for mock-telemetry overlay iteration.
- Added xUnit test scaffolding for Windows logic.

### v0.1.0 - Raw Capture Scaffold

Commit: `85dd734`

Squash title:

```text
[v0.1.0] Add raw capture scaffold and first telemetry sample
```

Summary:

- Added the first Windows tray/status-overlay capture scaffold.
- Added raw telemetry capture options and capture output documentation.
- Added the `TmrOverlay.cmd` local launcher.
- Added the first captured telemetry sample, schema, session info, and `telemetry.md` field summary.
- Added initial repo/agent context for fuel and overlay research.

### v0.0.0 - Initial Repo Boilerplate

Commit: `8d3358f`

Squash title:

```text
[v0.0.0] Add initial repo boilerplate
```

Summary:

- Created the initial repository scaffold before product versioning started.
