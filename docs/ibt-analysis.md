# IBT Analysis

TmrOverlay can request iRacing's own binary telemetry logging and then analyze the resulting `.ibt` file after the live session ends. This is an investigation path for deciding whether IBT is a better post-race source than TmrOverlay's live raw capture.

## Runtime Behavior

- `IbtAnalysis:TelemetryLoggingEnabled` defaults to `true`.
- IBT logging follows the same switch as raw capture. The startup raw-capture flag or the Collector Status `Capture` button starts a raw segment and sends iRacing's telemetry start command; stopping or finalizing that raw segment sends the telemetry stop command.
- `IbtAnalysis:Enabled` defaults to `true`.
- After the normal post-session guard has waited for iRacing to close, the app looks for the best matching `.ibt` under the configured telemetry root.
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
```

`status.json` is always written when analysis runs. If no matching `.ibt` is available, the telemetry root is missing, the candidate is too large, or analysis times out, the file records `status: "skipped"` with a reason instead of throwing into history or post-race analysis finalization.

`ibt-schema-summary.json` records the selected IBT source path, file size, header, disk header, parsed session context, type counts, and post-race candidate fields.

`ibt-vs-live-schema.json` compares the IBT schema against the current TmrOverlay live/raw schema. This is the main artifact for finding disk-only fields such as position, acceleration, and other post-race-only signals.

`ibt-field-summary.json` samples bounded telemetry records and records compact per-field first/last values, min/max/mean, non-default counts, and change counts.

## Guardrails

IBT analysis is deliberately best-effort:

- It runs after the same iRacing-closed guard used by `capture-synthesis.json`.
- It has an analysis timeout.
- It samples at most `IbtAnalysis:MaxSampledRecords` records.
- It ignores candidate files larger than `IbtAnalysis:MaxCandidateBytes`.
- It ignores files newer than `IbtAnalysis:MinStableAgeSeconds` so an active writer is not parsed.
- It scans only `IbtAnalysis:MaxCandidateFiles` recent `.ibt` files.
- It records skipped/failed status files and app events instead of failing compact history, post-race analysis, or capture synthesis.

## Current Investigation Notes

The real IBT fixture added on the remote branch is:

```text
ibt/mercedesamgevogt3_nurburgring combined 2026-05-01 09-46-29.ibt
```

Initial header inspection shows a version 2 file at 60 Hz with 286 fields, 62,270 disk records, and about 17.3 minutes of telemetry. Compared with the latest live/raw schema sample, 257 fields overlap, 29 fields are IBT-only, and 77 fields are live/raw-only. The IBT-only set includes `Lat`, `Lon`, `Alt`, wheel speeds, tire pressure/temperature fields, ride heights, and splitter ride height. The live/raw-only set includes many `CarIdx*` arrays, `CarLeftRight`, replay/camera state, and disk-logging flags. That makes IBT promising for car-local post-race physics/position analysis, while live/raw capture remains important for opponent/team timing and overlay context.
