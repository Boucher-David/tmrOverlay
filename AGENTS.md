# TmrOverlay Agent Notes

Start here when continuing work in this repo.

## Current Product Shape

- Windows tray application in `src/TmrOverlay.App/`
- Platform-neutral settings, history, live telemetry, fuel, overlay metadata, and post-race analysis models in `src/TmrOverlay.Core/`
- Ignored local macOS harness in `local-mac/TmrOverlayMac/`
- Startup surface: fixed-size settings app window; driving/support overlays are opt-in from settings and default hidden
- Settings panel owns overlay visibility, scale/custom size, session filters where relevant, shared font/units, and support capture/diagnostics controls; future product surfaces such as Overlay Bridge and post-race analysis should not be exposed as ordinary overlay tabs without a product pass
- iRacing ingestion through `irsdkSharp`
- Default-on localhost browser-source routes for supported OBS overlays; future Overlay Bridge work remains separate from localhost
- Raw capture pipeline that writes:
  - `capture-manifest.json`
  - `telemetry-schema.json`
  - `telemetry.bin`
  - `latest-session.yaml`
  - `session-info/`

## Read Next

`AGENTS.md` is the authoritative repo-level contract. The repo skill below is supplemental context for deeper product/current-state details.

- `skills/tmr-overlay-context/SKILL.md`
- `skills/tmr-overlay-validation/SKILL.md`
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
- Waiting/unavailable/error preview states must use deterministic isolated fixtures. Do not let local user history, cached telemetry, or machine-specific paths make an empty state look populated unless the scenario explicitly tests history fallback or support-path display.
- For wider app changes, carry validation discipline into tests and fixtures: assert both data that should appear and data that must stay hidden, cover failure/degraded paths, and keep performance/diagnostics/update flows fixture-driven where possible.
- If a durable user-data schema changes, treat backwards compatibility as part of the same validation sweep: update version constants, migrations or compatible readers, docs, and the schema-compatibility test before final validation.
- When implementation behavior, calculations, defaults, source labels, fixture data, or validation semantics change, update the affected build test assertions and test fixtures in the same pass. Treat stale passing or failing assertions as stale references, not as a separate cleanup task.
- Before declaring a branch complete, run the branch-complete release hygiene in `skills/tmr-overlay-validation/SKILL.md`: patch stale docs/context references, regenerate and validate screenshot artifacts for overlay/settings UI changes, inspect branch commits, sanitize the first commit or planned squash text, update `VERSION.md`, align `Directory.Build.props` version metadata for milestone branches, and create annotated tags only after the release commit is on `main` or explicitly designated as the release point.
- The authoring machine used for the initial scaffold did not have `dotnet` installed, so build/test verification still needs to happen on Windows.
