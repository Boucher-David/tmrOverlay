---
name: tmr-overlay-context
description: Use when continuing work in the tmrOverlay repo. Summarizes the current Windows tray app, draggable live-status overlay, iRacing telemetry capture pipeline, raw capture format, analyzed sample capture, fuel-overlay findings, overlay research notes, known limitations, and next priorities. Read references/current-state.md before making architectural changes.
---

# TmrOverlay Context

Use this repo-local skill when the task is about continuing or extending `tmrOverlay`.

## Workflow

1. Read `references/current-state.md`.
2. If the task is about fuel, strategy, stint logic, or telemetry interpretation, read `references/fuel-overlay-context.md`.
3. If the task is about overlay features, layout, or UI direction, read `references/overlay-research.md`.
4. Inspect git status before editing because this repo may accumulate ongoing local changes.
5. Preserve the current split:
   tray shell,
   live status overlay,
   telemetry capture service,
   raw capture artifacts on disk.
6. When adding or changing overlay features, include defensive error handling and useful debug/error logs. Telemetry gaps should degrade to an unavailable/waiting state; unexpected refresh/render failures should be logged and surfaced in the relevant overlay or status path without taking down the whole app.
7. When adding or materially changing overlays, update review images under `mocks/<overlay-id>/`. Prefer at least one focused screenshot plus a multi-state contact sheet that covers waiting/unavailable, normal/healthy, edge/warning, and error/fallback behavior when those states apply.
8. If you touch the capture format, update `docs/capture-format.md`, `telemetry.md`, and `README.md` together when needed.
9. If you change product direction or analysis assumptions, update the relevant reference file so future sessions inherit the new context.

## Primary Files

- `src/TmrOverlay.App/Program.cs`
- `src/TmrOverlay.App/Shell/NotifyIconApplicationContext.cs`
- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
- `src/TmrOverlay.App/Telemetry/`
- `docs/capture-format.md`
- `telemetry.md`
- `README.md`

## Intent

The immediate goal is not a full overlay suite yet. It is a dependable Windows background collector with just enough on-screen feedback to confirm:

- the app is alive
- iRacing is connected
- live session data is actually being captured

The next major milestone is a live fuel/stint overlay backed by a longer endurance-event capture.
