# Capture Format

Raw capture is an opt-in diagnostic/development mode. When `TelemetryCapture:RawCaptureEnabled` is `true`, each live capture produces a directory with four core artifacts:

- `capture-manifest.json`
- `telemetry-schema.json`
- `telemetry.bin`
- `latest-session.yaml`

Optional historical session snapshots are stored under `session-info/`.

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

The tool defaults to a 24 MiB output budget so the artifact stays below GitHub's browser upload cap. If a longer capture or richer timeline exceeds that budget, rerun with `--sample-stride`, lower `--max-timeline-events`, or lower `--top-indexes`.

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
