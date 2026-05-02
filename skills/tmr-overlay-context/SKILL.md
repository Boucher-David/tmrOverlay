---
name: tmr-overlay-context
description: Use when continuing work in the tmrOverlay repo and deeper product/current-state context is needed beyond AGENTS.md. Summarizes the Windows tray app, normal-desktop settings UI, live overlay suite, iRacing telemetry pipeline, opt-in raw capture format, diagnostics/performance support paths, mac harness, screenshot validation workflow, fuel-overlay findings, overlay research notes, known limitations, and next priorities.
---

# TmrOverlay Context

Use this repo-local skill when the task is about continuing or extending `tmrOverlay`.

`AGENTS.md` is the authoritative repo-level contract. This skill is a supplemental context loader; keep detailed product state in the referenced files instead of duplicating every guardrail here.

## Workflow

1. Read `AGENTS.md` first if it is not already in context.
2. Read `references/current-state.md`.
3. If the task is about fuel, strategy, stint logic, or telemetry interpretation, read `references/fuel-overlay-context.md`.
4. If the task is about overlay features, layout, UI direction, or screenshot review, read `references/overlay-research.md`.
5. Inspect git status before editing because this repo may accumulate ongoing local changes.
6. For overlay/settings changes, regenerate mac-harness screenshots and run `python3 tools/validate_overlay_screenshots.py`; still do visual review for text overlap and scenario correctness.
7. Treat fixtures as contracts: assert visible data, assert absent data, and isolate waiting/unavailable/error states from local user history or cached telemetry unless the scenario explicitly tests those paths.
8. After code changes, run a stale-reference sweep before final validation. Search docs, mocks, tests, the ignored mac harness, and repo skills for old behavior names/descriptions/API call patterns that no longer match the implementation, then patch those references in the same pass.
9. When implementation behavior, calculations, defaults, source labels, fixture data, or validation semantics change, update the affected build test assertions and test fixtures in the same pass. Treat stale passing or failing assertions as stale references, not as a separate cleanup task.
10. If overlay behavior or analysis logic changes, update the matching English logic note under `docs/overlay-logic.md` so future design review can happen from readable rules.
11. If you change product direction, validation assumptions, capture format, or analysis assumptions, update the relevant reference/docs file so future sessions inherit the new context.

## Primary Files

- `src/TmrOverlay.App/Program.cs`
- `src/TmrOverlay.App/Shell/NotifyIconApplicationContext.cs`
- `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/FuelCalculator/`
- `src/TmrOverlay.App/Overlays/CarRadar/`
- `src/TmrOverlay.App/Overlays/GapToLeader/`
- `src/TmrOverlay.App/Telemetry/`
- `local-mac/TmrOverlayMac/Sources/TmrOverlayMac/Preview/OverlayScreenshotGenerator.swift`
- `tools/validate_overlay_screenshots.py`
- `mocks/README.md`
- `docs/overlay-logic.md`
- `docs/capture-format.md`
- `telemetry.md`
- `README.md`

## Intent

The current goal is a dependable Windows iRacing companion with a small customizable overlay suite:

- the app is alive
- iRacing is connected
- live session data is actually being captured
- fuel/radar/class-gap overlays consume normalized live state
- users can manage overlay visibility, scale, sessions, font, units, and basic overlay display options
- raw capture remains an opt-in diagnostic/development mode

The next major milestone is hardening the live overlay suite, settings/customization surface, diagnostics/performance visibility, and update-notification path enough for a v1.0 production pass while keeping the mac harness useful for mock-telemetry and screenshot iteration.
