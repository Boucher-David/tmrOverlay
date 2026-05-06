# Version History

TmrOverlay uses SemVer-style annotated Git tags for product milestones:

- Tags use `vMAJOR.MINOR.PATCH`, for example `v0.6.0`.
- `0.x` versions are pre-release product milestones, so breaking local data or settings changes are still possible when they are documented and migrated.
- Patch versions are reserved for hardening, diagnostics, and compatibility work inside the same product shape.
- The checked-in build version should move with the branch that introduces the next milestone.

## Current Branch Target

### v0.11.0 - Standings, Track Maps, Localhost, And Live Overlay Polish

Planned branch name:

```text
v0.11-standings-track-map-localhost
```

Planned scope:

- Add production Windows standings visibility backed by normalized timing rows.
- Add a map-only Track Map overlay with bundled-map support, default-on IBT-derived local map generation with explicit opt-out, circle fallback, live car dots, and model-v2 sector highlights.
- Add disabled-by-default localhost browser-source routes for OBS overlays, with selectable/copyable per-overlay settings URLs, separate from the future teammate-to-teammate Overlay Bridge.
- Keep settings as a flat-tab app control surface and make the fixed settings window tall/wide enough for current overlay and Support content.
- Harden the current live overlays from tester feedback: stable Relative/Fuel number refreshes, iRacing-style relative display-time gaps where available or inferable, a smaller usable Inputs overlay, less confusing side-warning Radar behavior, and fade-out behavior for stale race-data overlays.
- Carry the v0.9 zip/publish release path forward with shared version metadata aligned to `0.11.0`.
- Keep the ignored mac harness and tracked mock screenshots aligned with the Windows overlay/settings surface where practical.

Technical implementation checklist:

1. Add Standings overlay registration, view model, form, screenshot fixture, and focused unit coverage.
2. Add Track Map overlay registration, transparent map-only drawing, sector highlights, settings warning/opt-out, mac harness review surface, and screenshot fixtures.
3. Add `LocalhostOverlays` config, selectable/copyable per-overlay settings URLs, and per-overlay HTML routes for standings, relative, fuel calculator, session/weather, pit service, input state, car radar, gap to leader, track map, and stream chat. Keep Flags disabled for localhost until its browser-source design is worth shipping.
4. Add IBT-derived map generation, confidence classification, storage skip rules for complete maps, and a batch generator for bundled map JSON assets.
5. Keep future Overlay Bridge docs scoped to trusted teammate-to-teammate data sharing, not local OBS/browser-source routes.
6. Reduce Relative and Fuel Calculator repaint churn with stable table layouts and value-only label updates.
7. Add Relative model-v2 display-time fallback from lap-distance deltas while keeping Radar timing stricter.
8. Make the Inputs overlay usable at its smaller default size with compact pedal/readout rendering.
9. Attach likely decoded cars to active Radar side warnings so one passing car is not drawn twice.
10. Regenerate settings/overlay screenshots after restoring flat tabs, keeping the larger fixed settings window, and adding track-map sector states.
11. Update docs/context/version metadata and run branch validation available from macOS, with Windows build/test/publish left to Windows CI.

Implemented baseline in this branch:

- Bumped shared .NET product/version metadata to `0.11.0`.
- Added Windows Standings and Track Map overlay registrations plus deterministic screenshot coverage.
- Added a Standings view model that renders compact same-class timing rows from `LiveTelemetrySnapshot.Models.Timing`.
- Added a transparent map-only Track Map overlay with generated map/circle fallback rendering and live car dots.
- Added model-v2 track-map sector highlights: personal-best sectors render green, session-best lap sectors render purple, and full-lap highlights clear after sector 1 of the following lap.
- Added local IBT-derived map generation, confidence metrics, layout-aware map identity, complete-map skip rules, default-on generation with user opt-out, single-file/folder manual IBT conversion diagnostics, and a batch generator for bundled track-map JSON assets.
- Upgraded bundled track-map assets to schema v2 sector metadata and expanded track-map diagnostics for schema/generation/sector/highlight coverage.
- Added `LocalhostOverlays` options and a disabled-by-default localhost server with OBS-ready HTML routes and selectable/copyable settings-tab URLs for supported overlays, plus a Stream Chat route that can consume one selected source: Streamlabs Chat Box widget URL or public Twitch channel chat.
- Separated localhost browser-source overlays from the future Overlay Bridge concept, which remains scoped to trusted teammate-to-teammate data sharing.
- Restored flat settings tabs and kept the fixed settings window at 1240x680 so the expanded tab list and Support content fit.
- Added live-telemetry availability fade behavior for race-data overlays while keeping non-race support/status/privacy overlays persistent.
- Added a double-buffered overlay table primitive and updated Relative/Fuel Calculator refresh paths so routine number changes repaint cells instead of invalidating the whole overlay.
- Kept Relative live rows in stable configured ahead/reference/behind slots and added inferred display-time gaps from lap-distance delta plus local/focus lap-time context when direct relative seconds are missing.
- Reduced the default Inputs overlay size and added a compact current-pedal/readout mode so key car-state data remains visible when the overlay is small.
- Updated local Radar side-warning rendering to attach a likely decoded nearby car to the left/right warning slot, suppress the duplicate center-lane rectangle, and bias the side marker forward/back by longitudinal gap.

Likely squash title:

```text
[v0.11.0] Add standings, track maps, localhost, and live polish
```

Likely squash body:

```text
- Bumped shared .NET product/version metadata to 0.11.0.
- Added production Standings and Track Map overlay registrations, settings tabs, Windows screenshot fixtures, and mac harness review surfaces.
- Added compact Standings rendering from normalized model-v2 timing rows, including leader gap, focus interval, and pit-road status.
- Added a transparent map-only Track Map surface with bundled-map support, circle fallback, live car dots, model-v2 sector highlights, default-on IBT-derived map generation with explicit opt-out, confidence/schema/sector diagnostics, single-file/folder manual conversion diagnostics, and complete-map skip behavior.
- Upgraded bundled track-map assets to schema v2 with sector metadata and added deterministic screenshot states for normal, green personal-best sector, purple session-best lap, following-sector reset, and mixed live-sector states.
- Added disabled-by-default `LocalhostOverlays` support with selectable/copyable per-overlay OBS/browser-source HTML routes and settings-tab URLs for standings, relative, fuel calculator, session/weather, pit service, input state, car radar, gap to leader, track map, and stream chat, while leaving Flags native-only for now.
- Added Stream Chat source selection for Streamlabs Chat Box widget URLs or public Twitch channel chat, with connected status/messaging in the browser-source overlay and Streamlabs URL redaction in diagnostics bundles.
- Kept Overlay Bridge conceptually separate as a future trusted teammate-to-teammate sharing boundary.
- Restored flat settings tabs while keeping the larger 1240x680 settings window so current tabs and Support content fit.
- Added live-telemetry fade behavior and performance metrics for race-data overlays so stale/non-running telemetry surfaces fade out while status/support/privacy surfaces remain persistent.
- Hardened Relative and Fuel Calculator refresh behavior with a double-buffered table layout, stable Relative slots, and value-only number updates to reduce white flicker.
- Added Relative display-time gap fallback from lap-distance deltas and local/focus lap-time context while leaving Radar placement on stricter spatial timing/distance evidence.
- Reduced the default Inputs overlay size and added compact pedal/readout rendering so small layouts keep speed, gear, RPM, steering, water, and oil visible.
- Updated local Radar side-warning rendering to attach likely decoded cars to left/right warnings, suppress duplicate center-lane cars, and bias the side marker forward/back by longitudinal gap.
- Regenerated tracked settings/overlay screenshots and updated docs/context for the v0.11 product shape, live-overlay fade behavior, and track-map sector lifecycle.
- Validation: git diff --check, conflict-marker sweep, tracked screenshot validation, track-map asset schema-v2 validation, Python screenshot-validator compile, mac `swift build`, localhost overlay JS parse smoke, and C# compile-shape scanner. Windows restore/build/test/screenshot/publish validation remains CI-owned from this Mac because `dotnet` is not installed locally.
```

## Merged Mainline Milestones

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
