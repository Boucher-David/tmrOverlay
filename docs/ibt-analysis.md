# IBT Analysis

TmrOverlay can request iRacing's own binary telemetry logging and then analyze the resulting `.ibt` file after the live session ends. This is an investigation path for deciding whether IBT is a better post-race source than TmrOverlay's live raw capture.

## Runtime Behavior

- `IbtAnalysis:TelemetryLoggingEnabled` defaults to `true`.
- IBT logging follows the same switch as raw capture. The startup raw-capture flag or the Collector Status `Capture` button starts a raw segment and sends iRacing's telemetry start command; stopping or finalizing that raw segment sends the telemetry stop command.
- `IbtAnalysis:Enabled` defaults to `true`.
- After raw capture finalization, the app writes `capture-synthesis.json` immediately with its own timeout, then waits up to `IbtAnalysis:MaxIRacingExitWaitSeconds` for iRacing to stop writing telemetry before looking for the best matching `.ibt` under the configured telemetry root.
- The app writes compact JSON sidecars under the raw capture directory in `ibt-analysis/`.
- The app does not copy the source `.ibt` into the capture directory by default.

The default telemetry root is:

```text
%USERPROFILE%\Documents\iRacing\telemetry
```

Override it with:

```powershell
$env:TMR_IbtAnalysis__TelemetryRoot = "D:\iRacing\telemetry"
```

Disable only the iRacing start/stop command requests while keeping analysis available:

```powershell
$env:TMR_IbtAnalysis__TelemetryLoggingEnabled = "false"
```

Disable the whole IBT sidecar path:

```powershell
$env:TMR_IbtAnalysis__Enabled = "false"
```

## Sidecar Files

When analysis succeeds, the capture directory contains:

```text
ibt-analysis/status.json
ibt-analysis/ibt-schema-summary.json
ibt-analysis/ibt-vs-live-schema.json
ibt-analysis/ibt-field-summary.json
ibt-analysis/ibt-local-car-summary.json
```

`status.json` is always written when analysis runs. If no matching `.ibt` is available, the telemetry root is missing, the candidate is too large, or analysis times out, the file records `status: "skipped"` with a reason instead of throwing into history or post-race analysis finalization.

`ibt-schema-summary.json` records the selected IBT source path, file size, header, disk header, parsed session context, type counts, and post-race candidate fields.

`ibt-vs-live-schema.json` compares the IBT schema against the current TmrOverlay live/raw schema. This is the main artifact for finding disk-only fields such as position, acceleration, and other post-race-only signals.

`ibt-field-summary.json` samples bounded telemetry records and records compact per-field first/last values, min/max/mean, non-default counts, and change counts.

`ibt-local-car-summary.json` reduces those sampled fields into local-car analysis groups: trajectory, fuel, vehicle dynamics, tires/wheels, pit service, weather, and missing opponent context. It also records track-map readiness from `Lat`/`Lon`/`Alt` plus lap-distance coverage. This sidecar is the committed contract for future local-car post-race analysis; the source `.ibt` file still stays outside the capture directory unless explicitly configured.

## Guardrails

IBT analysis is deliberately best-effort:

- It is separate from `capture-synthesis.json`; synthesis can run as soon as the TmrOverlay raw capture closes.
- Capture synthesis is bounded by `TelemetryCapture:MaxSynthesisMilliseconds`.
- `live-model-parity.json` consumes the resulting raw/IBT sidecar metadata after finalization so model-v2 review can see which live-model signals were available in raw capture, common with IBT, IBT-only, live-only, or missing; it also carries `promotionReadiness` so clean sessions can be flagged for model-v2 cutover review.
- It waits only up to `IbtAnalysis:MaxIRacingExitWaitSeconds` for the simulator process to exit before skipping the current run and leaving the capture eligible for startup recovery.
- It has an analysis timeout.
- It samples at most `IbtAnalysis:MaxSampledRecords` records.
- It ignores candidate files larger than `IbtAnalysis:MaxCandidateBytes`.
- It ignores files newer than `IbtAnalysis:MinStableAgeSeconds` so an active writer is not parsed.
- It scans only `IbtAnalysis:MaxCandidateFiles` recent `.ibt` files.
- It writes local-car summaries from the same bounded sample scan instead of doing a second unbounded pass over the IBT payload.
- It records skipped/failed status files and app events instead of failing compact history, post-race analysis, or capture synthesis.

## Current Investigation Notes

Initial inspection of one Mercedes-AMG GT3 Nürburgring IBT sample showed a version 2 file at 60 Hz with 286 fields, 62,270 disk records, and about 17.3 minutes of telemetry. Compared with the latest live/raw schema sample, 257 fields overlapped, 29 fields were IBT-only, and 77 fields were live/raw-only. The IBT-only set included `Lat`, `Lon`, `Alt`, wheel speeds, tire pressure/temperature fields, ride heights, and splitter ride height. The live/raw-only set included many `CarIdx*` arrays, `CarLeftRight`, replay/camera state, and disk-logging flags. That makes IBT promising for car-local post-race physics/position analysis, while live/raw capture remains important for opponent/team timing and overlay context.

May 2026 uploaded-data pass:

- `captures/IBT` contained 876 parseable `.ibt` files totaling about 21.4 GiB.
- All parsed files contained local-position fields such as `Lat`, `Lon`, and `Alt`.
- None of the parsed files contained the live opponent/timing arrays used by current overlays, including `CarIdxLapDistPct`, `CarIdxF2Time`, `CarIdxEstTime`, `CarIdxPosition`, `CarIdxClassPosition`, `CarIdxOnPitRoad`, or `CarLeftRight`.
- Treat IBT as a strong local-car post-race enrichment source, especially for trajectory, fuel-level, tire, wheel, ride-height, and vehicle-dynamics analysis. Do not treat it as a replacement for raw/live capture when the analysis needs opponent timing, side occupancy, camera focus, team/focus `CarIdx`, or standings context.
- The repo keeps compact examples under `fixtures/telemetry-analysis/`. Do not commit raw `.ibt` files or raw `telemetry.bin` captures; commit derived example JSON when a new signal assumption needs to be preserved.
