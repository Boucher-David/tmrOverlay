---
name: tmr-overlay-context
description: Use when continuing work in the tmrOverlay repo. Summarizes the current Windows tray app, top-left status overlay, iRacing telemetry capture pipeline, raw capture format, known limitations, and next priorities. Read references/current-state.md before making architectural changes.
---

# TmrOverlay Context

Use this repo-local skill when the task is about continuing or extending `tmrOverlay`.

## Workflow

1. Read `references/current-state.md`.
2. Inspect git status before editing because this repo may accumulate ongoing local changes.
3. Preserve the current split:
   tray shell,
   live status overlay,
   telemetry capture service,
   raw capture artifacts on disk.
4. If you touch the capture format, update `docs/capture-format.md` and `README.md`.
5. If you change product direction, update `references/current-state.md` so future sessions inherit the new context.

## Primary Files

- `src/TmrOverlay.App/Program.cs`
- `src/TmrOverlay.App/NotifyIconApplicationContext.cs`
- `src/TmrOverlay.App/StatusOverlayForm.cs`
- `src/TmrOverlay.App/Telemetry/`
- `docs/capture-format.md`
- `README.md`

## Intent

The current goal is not polished overlays yet. It is a dependable Windows background collector with just enough on-screen feedback to confirm:

- the app is alive
- iRacing is connected
- live session data is actually being captured

