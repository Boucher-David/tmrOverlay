# Version History

TmrOverlay uses SemVer-style annotated Git tags for product milestones:

- Tags use `vMAJOR.MINOR.PATCH`, for example `v0.6.0`.
- `0.x` versions are pre-release product milestones, so breaking local data or settings changes are still possible when they are documented and migrated.
- Patch versions are reserved for hardening, diagnostics, and compatibility work inside the same product shape.
- The checked-in build version should move with the branch that introduces the next milestone.

## Current Branch Target

### v0.9.0 - Production Publishing And Updates

Planned branch name:

```text
v0.9-production-publishing
```

Planned scope:

- Replace manual teammate build sharing with a repeatable GitHub release/package flow for tester builds.
- Produce clearly versioned Windows `win-x64` artifacts from CI using Release build/test, `dotnet publish`, package-content auditing, artifact zipping, package manifest generation, SHA-256 checksum generation, release notes, and durable build metadata.
- Promote tag-triggered package artifacts into a GitHub Release instead of leaving them only as workflow artifacts.
- Derive Windows executable/tray icon assets from the checked-in brand source image instead of wiring the wide source PNG directly into the app.
- Decide the first install/update channel:
  - v0.9 minimum: portable self-contained zip with checksum, release notes, diagnostics version metadata, and manual install/upgrade instructions.
  - preferred installer/update candidate: Velopack, producing a setup executable, portable package, release feed, and later in-app update checks against GitHub Releases or static hosting.
  - Microsoft-native candidate: MSIX plus App Installer, but only after signing/package identity constraints are accepted.
- Add code-signing decision and CI secret plan. Unsigned zip builds are acceptable for private tester proofing, but a broadly publishable installer/package needs signed artifacts to reduce Windows trust friction.
- Add a passive update discovery surface: tray/settings version display, manual `Check for updates`, latest release/manifest lookup, release notes link, and diagnostics bundle fields for current version/update-check state. No modal prompts during active sessions.
- Keep diagnostics bundle and version reporting aligned with shared tester builds so feedback can be traced to an exact release.

Technical implementation checklist:

1. Harden `.github/workflows/windows-dotnet.yml` into two product flows: PR/main validation gate and tag/manual release packaging.
2. On release tags, run restore/build/test, publish `src/TmrOverlay.App` for `win-x64` as self-contained single-file output, audit the publish folder, zip it, generate manifest/checksum files, and upload release artifacts.
3. Add release creation/upload so `v*.*.*` tags produce a GitHub Release containing the zip, manifest, checksum, and notes derived from `VERSION.md` or a generated changelog.
4. Add app metadata polish: generated `.ico` from `assets/brand/TMRLogo.png`, Windows executable icon metadata, `AssemblyVersion/FileVersion/InformationalVersion`, and diagnostics output showing exact version/commit.
5. Add tester install docs: where to unzip/install, upgrade expectations, settings/history compatibility, SmartScreen/signing expectations, rollback instructions, and how to attach diagnostics.
6. Prototype signing: start with documented unsigned/private tester builds, then evaluate Azure Artifact Signing or an OV code-signing certificate for executable/MSIX signing before broader release.
7. Decide packaging path after the first portable release: Velopack for installer plus update feed, or MSIX/App Installer for Windows-native package identity and update policy.
8. Add passive update checking only after releases exist: latest GitHub Release or static manifest lookup, semantic version comparison, tray/settings notification, download/release-notes command, and diagnostics logging.

Implemented baseline in this branch:

- Bump shared .NET product/version metadata to `0.9.0`.
- Generate and wire a Windows executable icon derived from `assets/brand/TMRLogo.png`.
- Harden the Windows GitHub Actions workflow into a PR/main validation gate plus tag/manual package flow.
- On PRs, restore, build, test, validate tracked screenshots, and run a self-contained publish dry run with the same package audit used by release packaging.
- On `vMAJOR.MINOR.PATCH` tags, publish the app as a self-contained `win-x64` package, audit the publish folder for accidental repo/dev-folder leaks, zip it with a versioned file name, generate manifest/checksum files, upload release artifacts, and attach them to the GitHub Release.
- Keep manual workflow dispatch useful for branch package artifacts without creating a release unless the run is for a product tag.
- Add tester package-contents, install, checksum, user-data compatibility, schema-migration expectations, upgrade, rollback, signing, and diagnostics documentation in `docs/windows-release.md`.
- Update the update strategy with the new release baseline and the remaining signing/update-notification decisions.
- Keep tester AppData cleaner by disabling edge-case clips, model-v2 parity, live overlay diagnostics, and post-race analysis by default while preserving config/env overrides for analysis collection.
- Rework the Support tab into a task-oriented support surface with app status, current issue, diagnostics bundle actions, diagnostic telemetry capture, storage shortcuts, compact app activity, and discoverable advanced collection status.
- Add `docs/repo-surface.md` and `.gitignore` hardening so customer/tester-facing docs, product source, internal development assets, and local runtime data are separated clearly.
- Refresh settings screenshot artifacts for the Support tab changes.

Likely squash title:

```text
[v0.9.0] Add production publishing and teammate update flow
```

Likely squash body:

```text
- Bumped shared .NET product/version metadata to 0.9.0 and wired a generated Windows executable icon derived from the checked-in TMR logo.
- Hardened the Windows GitHub Actions workflow so PR/main run restore/build/test, tracked screenshot validation, and a self-contained publish dry run with package audit, while tag/manual release packaging publishes a self-contained win-x64 app, zips it, generates manifest/checksum files, and uploads release artifacts.
- Added tag-driven GitHub Release creation for vMAJOR.MINOR.PATCH tags with versioned Windows zip/manifest/checksum assets and generated release notes.
- Added Windows tester release docs covering package contents, download, checksum verification, portable install, app-data compatibility, schema-migration expectations, upgrade, rollback, unsigned SmartScreen expectations, and diagnostics handoff.
- Reworked the Support tab into a task-oriented surface for app status, diagnostics bundle actions, diagnostic telemetry capture, storage shortcuts, compact app activity, and discoverable advanced collection status.
- Disabled advanced collection outputs by default for tester builds, including edge-case clips, model-v2 parity, live overlay diagnostics, and post-race analysis, while keeping config/env overrides and tests for enabling them.
- Added repo-surface docs and ignore rules to separate customer/tester-facing material, product source, internal development assets, and generated local runtime data.
- Updated the update strategy to treat portable GitHub Releases as the v0.9 baseline while keeping signing, installer selection, and passive update checks as follow-up work.
- Validation: git diff --check, conflict-marker sweep, workflow YAML parse, stale-reference sweep, Swift build, mac settings screenshot generation, screenshot validator, and C# compile-shape scanner. Windows restore/build/test plus publish dry-run audit now run in PR CI; local Swift tests still require an XCTest-capable toolchain.
```

## Merged Mainline Milestones

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
- Added disabled-by-default localhost Overlay Bridge scaffolding.
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
