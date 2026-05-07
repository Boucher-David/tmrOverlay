# Version History

TmrOverlay uses SemVer-style annotated Git tags for product milestones:

- Tags use `vMAJOR.MINOR.PATCH`, for example `v0.6.0`.
- `0.x` versions are pre-release product milestones, so breaking local data or settings changes are still possible when they are documented and migrated.
- Patch versions are reserved for hardening, diagnostics, and compatibility work inside the same product shape.
- The checked-in build version should move with the branch that introduces the next milestone.

## Current Branch Target

### v0.14.0 - UI Polish And V1 Candidate Prep

Planned branch name:

```text
v0.14-ui-polish-v1-candidate-prep
```

Planned scope:

- Make the core overlay/settings surface easier to review, maintain, and hand to a designer.
- Promote shared overlay chrome primitives for headers, status, source footers, tables, borders, and state tones.
- Move normal overlay dimensions to app-owned scale-derived sizing instead of independent width/height controls.
- Keep app-health/status diagnostics in Support instead of a standalone product overlay.
- Tighten startup, localhost/browser-source, performance, and diagnostics behavior before V1 candidate testing.
- Refresh screenshot parity expectations, docs, context, and branch-readiness metadata.

Technical implementation checklist:

1. Bump shared .NET product/version metadata to `0.14.0`.
2. Add shared WinForms overlay chrome helpers and session-scoped Header/Footer settings for common overlays.
3. Move scale-capable overlays to definition-size plus scale-derived dimensions, including Flags, Stream Chat, and Garage Cover.
4. Keep Track Map and Radar square-scaled, and keep Garage Cover browser/preview image fitting crop-to-cover.
5. Remove the floating Collector Status overlay from the managed product overlay set while preserving Support-tab and diagnostics status models.
6. Reduce startup and idle localhost overhead with deferred/background startup work, snapshot response caching, slower browser polling where appropriate, and better localhost activity diagnostics.
7. Refresh deterministic screenshot expectations/artifacts so retired status-overlay images are no longer a V1 parity target.
8. Update docs/context/version metadata and run branch validation available from macOS, with Windows build/test/publish left to Windows CI.

Implemented baseline in this branch:

- Bumped shared .NET product/version metadata to `0.14.0`.
- Added shared overlay chrome helpers and state models for title/status/source layout, common table cells, row sizing, borders, state colors, and header/footer slot fitting.
- Added session-scoped Header `Status` and Footer `Source` settings under horizontal General/Header/Footer sub-tabs for Standings, Relative, Fuel Calculator, Input / Car State, and Gap To Leader.
- Moved normal scale-capable overlays to scale-derived width/height normalization, including Flags, Stream Chat, Garage Cover, Track Map, and Radar.
- Kept Garage Cover localhost-only while adding scale control and crop-to-cover preview behavior so imported images always fill the cover area.
- Removed the floating Collector Status overlay and its Windows/mac screenshot parity targets; app-health status remains in the Support tab and diagnostics bundles.
- Tightened startup/performance work by moving startup history/retention maintenance off the blocking path and delaying the first performance log write.
- Reduced localhost/browser-source overhead with cached serialized snapshot responses, localhost recent-request diagnostics, and slower Track Map browser polling.
- Updated Windows screenshot expectations, tracked screenshot validation, docs, and repo context for the V1 candidate product shape.
- Added focused unit coverage for localhost response caching, performance startup behavior, shared chrome settings, and chrome slot fitting.

Likely squash title:

```text
[v0.14.0] Polish overlays and prepare the V1 candidate
```

Likely squash body:

```text
- Bumped shared .NET product/version metadata to 0.14.0.
- Added shared overlay chrome primitives for title/status/source layout, table cells, row sizing, borders, state colors, and header/footer slot fitting.
- Added session-scoped Header `Status` and Footer `Source` settings under horizontal General/Header/Footer sub-tabs for Standings, Relative, Fuel Calculator, Input / Car State, and Gap To Leader.
- Moved scale-capable overlays to app-owned definition-size plus scale-derived dimensions, including Flags, Stream Chat, Garage Cover, Track Map, and Radar.
- Kept Garage Cover localhost-only while adding scale control and crop-to-cover preview/rendering behavior so imported images fill the cover area reliably.
- Removed the floating Collector Status overlay from the product overlay set and screenshot parity targets; app-health status remains in Support and diagnostics.
- Moved startup history/retention maintenance off the blocking path and delayed the first performance log write.
- Reduced localhost/browser-source overhead with cached serialized snapshot responses, recent-request diagnostics, and slower Track Map browser polling.
- Updated docs, repo context, tracked screenshot validation, and Windows screenshot expectations for the V1 candidate product shape.
- Added focused unit coverage for localhost response caching, startup performance behavior, shared chrome settings, and chrome slot fitting.
- Validation: git diff --check, conflict-marker sweep, C# compile-shape scanner, tracked screenshot validation, Windows screenshot expectation validation, and mac harness build. Windows restore/build/test/screenshot/publish validation remains CI-owned from this Mac because `dotnet` is not installed locally; `swift test` is blocked by missing XCTest in the local toolchain.
```

## Next Planned Milestone

### v0.15.0 - Settings Layout And V1 UI Polish

Planned branch name:

```text
v0.15-settings-layout-v1-polish
```

Likely scope:

- Rework the Settings app layout as a focused product pass instead of expanding the v0.14 branch after branch-complete validation.
- Keep the v0.14 shared General/Header/Footer overlay settings behavior, then make the surrounding settings surface clearer, easier to scan, and easier to extend.
- Improve grouping for overlay controls, support/status controls, localhost/browser-source details, and shared app preferences without exposing development-only surfaces as normal overlay tabs.
- Preserve app-owned scale controls and header/footer slot-fitting assumptions while improving how crowded overlay option sets are presented.
- Regenerate tracked mac screenshots and rely on Windows CI for WinForms screenshot parity, build, test, publish, and package validation.

### v0.16.0 - Release Channel And V1 Candidate Escape Hatch

Likely scope:

- Keep installer/update-channel work, likely Velopack, as the next larger 0.x milestone unless teammate testing forces a different release-blocking fix first.
- Reserve this milestone for signed/installer distribution, update-check reliability, portable-upgrade hardening, durable AppData compatibility issues, or final V1 candidate stabilization.
- Keep deep fuel/strategy/engineer/advanced-track-map/streaming/builder work out of the V1.0 release candidate unless it remains hidden development tooling.

## Merged Mainline Milestones

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
