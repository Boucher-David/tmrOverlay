# Telemetry Analysis Fixtures

These fixtures are compact, sanitized examples derived from raw capture and IBT investigations.

Do not commit source `telemetry.bin` or `.ibt` files here. Those files are large, local, and may contain complete session traces. Commit only small JSON examples that preserve the shape of the analysis artifacts and the field-level assumptions we want future tooling to honor.

Before adding telemetry-backed behavior, compare the tracked SDK availability corpus with current local raw-capture schemas:

```bash
python3 tools/analysis/check_sdk_schema_against_corpus.py
```

If local iRacing SDK output exposes fields or declared shapes that are not in the tracked corpus, update `sdk-field-availability-corpus.json`/`.md` from redacted captures or record the gap here so new SDK features stay visible for future product work.

## Files

- `ibt-local-car-summary.example.json`
  - Example shape for the post-session `ibt-analysis/ibt-local-car-summary.json` sidecar.
  - Shows IBT as a local-car trajectory/fuel/vehicle-dynamics source, with opponent arrays explicitly missing.
- `live-vs-ibt-signal-availability.example.json`
  - Compact summary of the May 2026 IBT inventory and raw/live capture source split.
  - Useful when reviewing whether a future model should use raw/live capture, IBT, or both.
- `live-telemetry-state-corpus.json`
  - Compact redacted state corpus derived from the May 11, 2026 AI multi-session and open-player practice captures plus local four-hour and 24-hour endurance captures.
  - Focuses on Standings, Relative, and Gap To Leader source-selection behavior in AI/spectated, player-practice, normal endurance running, and pit/service contexts.
  - Remaining missing targets are listed in the markdown index so future capture passes can fill the gaps without committing raw captures.
- `live-telemetry-state-corpus.md`
  - Human-readable index for the compact state corpus.
- `live-telemetry-state-corpus-sdk-coverage.md`
  - Notes which current app-read SDK variables are represented by the compact behavior corpus and which remain available only in broader SDK captures.
- `sdk-field-availability-corpus.json`
  - Compact redacted SDK availability corpus derived from local four-hour and 24-hour endurance raw captures.
  - Preserves SDK variable names, types, units, descriptions, declared array/storage maximums, primitive type bounds, sampled observed ranges, and identity shape counts without committing raw telemetry frames or full session-info payloads.
- `sdk-field-availability-corpus.md`
  - Human-readable index for the SDK availability corpus.
- `raw-capture-overlay-assumptions.example.json`
  - Compact summary of the long raw-capture assumptions pass for fuel, radar, class-gap, and position-cadence logic.
  - Keeps the important measured risks in git without tracking the multi-GB captures.
