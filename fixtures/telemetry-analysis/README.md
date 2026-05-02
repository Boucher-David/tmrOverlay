# Telemetry Analysis Fixtures

These fixtures are compact, sanitized examples derived from raw capture and IBT investigations.

Do not commit source `telemetry.bin` or `.ibt` files here. Those files are large, local, and may contain complete session traces. Commit only small JSON examples that preserve the shape of the analysis artifacts and the field-level assumptions we want future tooling to honor.

## Files

- `ibt-local-car-summary.example.json`
  - Example shape for the post-session `ibt-analysis/ibt-local-car-summary.json` sidecar.
  - Shows IBT as a local-car trajectory/fuel/vehicle-dynamics source, with opponent arrays explicitly missing.
- `live-vs-ibt-signal-availability.example.json`
  - Compact summary of the May 2026 IBT inventory and raw/live capture source split.
  - Useful when reviewing whether a future model should use raw/live capture, IBT, or both.
- `raw-capture-overlay-assumptions.example.json`
  - Compact summary of the long raw-capture assumptions pass for fuel, radar, class-gap, and position-cadence logic.
  - Keeps the important measured risks in git without tracking the multi-GB captures.
