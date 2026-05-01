---
name: tmr-overlay-context
description: Use when continuing work in the tmrOverlay repo. Summarizes the current Windows tray app, settings/customization UI, live overlay suite, iRacing telemetry capture pipeline, raw capture format, analyzed sample capture, fuel-overlay findings, overlay research notes, known limitations, and next priorities. Read references/current-state.md before making architectural changes.
---

# TmrOverlay Context

Use this repo-local skill when the task is about continuing or extending `tmrOverlay`.

## Workflow

1. Read `references/current-state.md`.
2. If the task is about fuel, strategy, stint logic, or telemetry interpretation, read `references/fuel-overlay-context.md`.
3. If the task is about overlay features, layout, or UI direction, read `references/overlay-research.md`.
4. Inspect git status before editing because this repo may accumulate ongoing local changes.
5. When validating C# changes, read `../tmr-overlay-validation/SKILL.md` and run its compile-shape scanner before relying on a Windows-only `dotnet` build.
6. Preserve the current split:
   tray shell,
   settings/customization overlay,
   product overlays,
   Core read models and strategy logic,
   telemetry provider services,
   raw capture artifacts on disk.
   Treat the Windows tray app as the production/iRacing runtime. Keep demo-only, screenshot-only, mock playback, and other development "fluff" in the ignored `local-mac/TmrOverlayMac/` harness unless the user explicitly asks to promote behavior into the production Windows app.
7. Keep cross-overlay live-session facts in shared Core snapshots/read models, not in a single overlay view model. Per-car observed timing, stint, pit, weather, and availability context should be reusable by fuel, gap, radar, future relative/standings/map overlays, diagnostics, and the local bridge.
8. When adding or changing overlay features, include defensive error handling and useful debug/error logs. Telemetry gaps should degrade to an unavailable/waiting state; unexpected refresh/render failures should be logged and surfaced in the relevant overlay or status path without taking down the whole app.
9. When adding or materially changing overlays, update review images under `mocks/<overlay-id>/`. Prefer at least one focused screenshot plus a multi-state contact sheet that covers waiting/unavailable, normal/healthy, edge/warning, and error/fallback behavior when those states apply.
10. If you touch the capture format, update `docs/capture-format.md`, `telemetry.md`, and `README.md` together when needed.
11. If you change product direction or analysis assumptions, update the relevant reference file so future sessions inherit the new context.

## Primary Files

- `src/TmrOverlay.App/Program.cs`
- `src/TmrOverlay.App/Shell/NotifyIconApplicationContext.cs`
- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
- `src/TmrOverlay.App/Telemetry/`
- `docs/capture-format.md`
- `telemetry.md`
- `README.md`

## Intent

The immediate goal is a dependable Windows iRacing companion with a small customizable overlay suite:

- the app is alive
- iRacing is connected
- live session data is actually being captured
- fuel/radar/class-gap overlays consume normalized live state
- users can manage overlay visibility, scale, sessions, font, units, and basic overlay display options
- raw capture remains an opt-in diagnostic/development mode

The next major milestone is hardening the live overlay suite and settings/customization surface enough for a v1.0 production pass while keeping the mac harness useful for mock-telemetry iteration.
