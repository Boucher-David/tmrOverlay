# TmrOverlay Agent Notes

Start here when continuing work in this repo.

## Current Product Shape

- Windows tray application in `src/TmrOverlay.App/`
- Platform-neutral settings, history, live telemetry, fuel, overlay metadata, and post-race analysis models in `src/TmrOverlay.Core/`
- Ignored local macOS harness in `local-mac/TmrOverlayMac/`
- Startup overlay set: settings panel, status, fuel calculator, car radar, and class gap trend
- Settings panel owns overlay visibility, scale, session filters, shared font/units, raw capture requests, placeholder Overlay Bridge controls, and post-race analysis browsing
- iRacing ingestion through `irsdkSharp`
- Optional disabled-by-default localhost overlay bridge serving normalized live snapshots
- Raw capture pipeline that writes:
  - `capture-manifest.json`
  - `telemetry-schema.json`
  - `telemetry.bin`
  - `latest-session.yaml`
  - `session-info/`

## Read Next

`AGENTS.md` is the authoritative repo-level contract. The repo skill below is supplemental context for deeper product/current-state details.

- `skills/tmr-overlay-context/SKILL.md`
- `skills/tmr-overlay-context/references/current-state.md`
- `skills/tmr-overlay-context/references/fuel-overlay-context.md`
- `skills/tmr-overlay-context/references/overlay-research.md`
- `docs/overlay-logic.md`
- `docs/capture-format.md`
- `telemetry.md`
- `README.md`

## Guardrails

- Preserve the collector-first architecture unless there is a strong reason to change it.
- Keep Windows as the production/iRacing runtime and the ignored mac harness as the mock-telemetry development surface.
- If you change the raw capture format, update `docs/capture-format.md` and `README.md` in the same pass.
- Prefer shared Core models/read services, descriptor-driven overlay options, and `OverlayTheme` tokens over one-off UI contracts.
- Product overlays should read normalized live state through `ILiveTelemetrySource`; telemetry providers should write through `ILiveTelemetrySink`.
- Mirror shared app/overlay/boilerplate changes in both the Windows app and ignored mac harness unless the work is explicitly Windows/iRacing-specific.
- In the Windows app, fully qualify timer types: use `System.Threading.Timer` for hosted/background services and `System.Windows.Forms.Timer` for UI refresh loops. WinForms implicit globals import both namespaces, so bare `Timer` is ambiguous on Windows.
- Treat rendered overlay screenshots as validation artifacts, not only design artifacts: update `mocks/<overlay-id>/` when overlays or settings UI change, keep one contact sheet plus smaller per-state PNGs, and run `python3 tools/validate_overlay_screenshots.py` after generating them.
- Waiting/unavailable/error preview states must use deterministic isolated fixtures. Do not let local user history, cached telemetry, or machine-specific paths make an empty state look populated unless the scenario explicitly tests history fallback or support-path display.
- For wider app changes, carry the same validation discipline beyond screenshots: assert both data that should appear and data that must stay hidden, cover failure/degraded paths, and keep performance/diagnostics/update flows fixture-driven where possible.
- After making code changes, do a stale-reference sweep before final validation: search docs, mocks, tests, the ignored mac harness, and repo skills for old behavior names/descriptions/API call patterns that no longer match the implementation. Patch those references in the same pass so future agents inherit the current behavior instead of stale assumptions.
- When implementation behavior, calculations, defaults, source labels, fixture data, or validation semantics change, update the affected build test assertions and test fixtures in the same pass. Treat stale passing or failing assertions as stale references, not as a separate cleanup task.
- When overlay behavior or analysis logic changes, update the matching English logic note under `docs/overlay-logic.md` in the same pass so future design tweaks can be reviewed from readable rules instead of code.
- The authoring machine used for the initial scaffold did not have `dotnet` installed, so build/test verification still needs to happen on Windows.
