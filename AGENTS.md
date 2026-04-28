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

- `skills/tmr-overlay-context/SKILL.md`
- `skills/tmr-overlay-context/references/current-state.md`
- `skills/tmr-overlay-context/references/fuel-overlay-context.md`
- `skills/tmr-overlay-context/references/overlay-research.md`
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
- The authoring machine used for the initial scaffold did not have `dotnet` installed, so build/test verification still needs to happen on Windows.
