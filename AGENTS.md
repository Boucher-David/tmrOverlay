# TmrOverlay Agent Notes

Start here when continuing work in this repo.

## Current Product Shape

- Windows tray application in `src/TmrOverlay.App/`
- Tiny always-on-top status overlay in the top-left
- iRacing ingestion through `irsdkSharp`
- Raw capture pipeline that writes:
  - `capture-manifest.json`
  - `telemetry-schema.json`
  - `telemetry.bin`
  - `latest-session.yaml`
  - `session-info/`

## Read Next

- `skills/tmr-overlay-context/SKILL.md`
- `skills/tmr-overlay-context/references/current-state.md`
- `docs/capture-format.md`
- `README.md`

## Guardrails

- Preserve the collector-first architecture unless there is a strong reason to change it.
- If you change the raw capture format, update `docs/capture-format.md` and `README.md` in the same pass.
- Prefer extending the status overlay and capture service instead of replacing them.
- The authoring machine used for the initial scaffold did not have `dotnet` installed, so build/test verification still needs to happen on Windows.

