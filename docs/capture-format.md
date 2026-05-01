# Capture Format

Raw capture is an opt-in diagnostic/development mode. When `TelemetryCapture:RawCaptureEnabled` is `true`, each live capture produces a directory with four core artifacts:

- `capture-manifest.json`
- `telemetry-schema.json`
- `telemetry.bin`
- `latest-session.yaml`

Optional historical session snapshots are stored under `session-info/`. When IBT analysis is enabled, the app may also write compact derived files under `ibt-analysis/`; the source `.ibt` file remains in iRacing's telemetry folder by default.

## Compact Synthesis

Raw captures can get large enough that sharing `telemetry.bin` is inconvenient. For GitHub-friendly analysis, create a compact all-telemetry synthesis instead:

```bash
python3 tools/analysis/synthesize_capture.py --capture captures/capture-YYYYMMDD-HHMMSS-mmm --output /tmp/capture-synthesis.json
```

The synthesis reads every field in `telemetry-schema.json` across `telemetry.bin` and writes a bounded JSON summary with:

- capture/frame counts and session-info update counts
- type/category counts for all telemetry variables
- per-field first/last values, min/max/mean, change counts, distinct scalar values, and active array indexes
- short timelines for scalar state changes
- a focused weather section derived from the same all-field summary

The tool defaults to a 24 MiB output budget so the artifact stays below GitHub's browser upload cap. It also auto-strides large captures to roughly 20,000 sampled frames by default; use `--sample-stride 1` only when you intentionally want every frame. If a longer capture or richer timeline exceeds that budget, rerun with a larger `--sample-stride`, lower `--max-timeline-events`, or lower `--top-indexes`.

The app writes a stable `capture-synthesis.json` plus a context-named copy when session YAML has enough metadata, for example `capture-synthesis-race-mercedes-amg-gt3-2020-gesamtstrecke-vln.json`. App-side synthesis is deferred while the iRacing SDK is still connected or a known iRacing sim process is still running, then starts as soon as that blocker clears; this keeps post-session reduction from competing with the live sim. If the app itself is shutting down while iRacing is still active, synthesis is skipped rather than blocking exit. On startup, the app scans for raw capture folders with `telemetry.bin` and `telemetry-schema.json` but no stable `capture-synthesis.json`; those folders are queued for the same guarded synthesis path. The standalone tool has the same Windows process guard unless `--allow-while-iracing-running` is passed intentionally.

The app also records synthesis timing and process CPU usage in app events, status snapshots, and diagnostics bundles so long post-session reduction can be reviewed without uploading `telemetry.bin`.

## IBT Analysis Sidecar

IBT analysis is a derived post-session sidecar, not part of the raw `telemetry.bin` format. When `IbtAnalysis:TelemetryLoggingEnabled` is true, the same status-overlay/startup switch that starts raw capture also asks iRacing to start binary telemetry logging, and stopping or finalizing the raw segment asks iRacing to stop logging. After the same iRacing-closed guard used for capture synthesis, the app looks for a matching `.ibt` file under `IbtAnalysis:TelemetryRoot`.

The default output directory inside a capture is:

```text
ibt-analysis/
```

Possible files are:

- `status.json`
- `ibt-schema-summary.json`
- `ibt-vs-live-schema.json`
- `ibt-field-summary.json`

`status.json` is written for success, skip, and failure outcomes. Missing telemetry roots, missing candidates, oversized candidates, active/still-writing files, and analysis timeouts are recorded as skipped or failed sidecars; they do not fail compact history, post-race analysis, or `capture-synthesis.json`.

The source `.ibt` is not copied into the capture directory unless `IbtAnalysis:CopyIbtIntoCaptureDirectory` is explicitly enabled.

## Session Stitching

Raw capture directories are immutable segments. If the app exits, crashes, or the user leaves and rejoins the same iRacing session, the app does not concatenate `telemetry.bin` files.

Instead, compact history writes a per-capture summary and also updates a derived session group when `SubSessionID` or `SessionID` is available from session YAML. Session group files live beside summaries under:

```text
history/user/cars/{car}/tracks/{track}/sessions/{session}/session-groups/{group-id}.json
```

Each segment records the source capture id, start/end time, duration, frame counts, quality confidence, whether it contributes to baseline history, the end reason, the reconnect gap from the previous segment, and previous app runtime state when the last app run was unclean. Post-race analysis uses the group id as its stable analysis id, so later reconnect segments update the same analysis row.

## `capture-manifest.json`

The manifest identifies the capture, SDK/header shape, frame counts, session-info snapshot counts, app version, and finalization timing. Newer captures also include correlation ids:

- `appRunId` - current TmrOverlay process run id
- `collectionId` - live telemetry collection id shared by raw capture, compact history, app events, diagnostics, and post-race analysis
- `captureId` - immutable raw capture segment id
- `endedReason` - why the raw segment closed, such as `iracing_disconnected`, `app_stopped`, or `manual_stop`

Newer captures also include lightweight performance fields:

- `rawCaptureElapsedMilliseconds`
- `processCpuMilliseconds`
- `processCpuPercentOfOneCore`
- `writeOperationCount`
- `averageWriteElapsedMilliseconds`
- `maxWriteElapsedMilliseconds`

These values are process-level diagnostics for the capture window. They are intended to show whether raw capture writes were slow or CPU-heavy while iRacing was active.

## `telemetry-schema.json`

This file is a JSON array describing every telemetry variable exposed by the SDK for the capture:

- variable name
- type name and numeric type code
- element count
- byte offset inside the telemetry buffer
- unit
- description

The schema is written once per capture and is intended to be used when decoding `telemetry.bin`.

## `telemetry.bin`

`telemetry.bin` is an append-only little-endian binary file.

### File Header

The file begins with a 32-byte header:

1. `magic` - 8 ASCII bytes: `TMRCAP01`
2. `sdkVersion` - `int32`
3. `tickRate` - `int32`
4. `bufferLength` - `int32`
5. `variableCount` - `int32`
6. `captureStartUnixMs` - `int64`

### Frame Record

Each telemetry frame is appended as:

1. `capturedUnixMs` - `int64`
2. `frameIndex` - `int32`
3. `sessionTick` - `int32`
4. `sessionInfoUpdate` - `int32`
5. `sessionTime` - `float64`
6. `payloadLength` - `int32`
7. `payload` - raw telemetry buffer bytes

`payloadLength` should normally match the `bufferLength` value in the file header.

## Session YAML

`latest-session.yaml` is overwritten whenever iRacing increments `SessionInfoUpdate`.

If `StoreSessionInfoSnapshots` is enabled, the same YAML content is also written to:

```text
session-info/session-0001.yaml
session-info/session-0002.yaml
...
```

Those snapshots let us reconstruct session metadata changes over time without parsing the binary stream.
