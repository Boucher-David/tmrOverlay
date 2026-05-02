# Capture Format

Raw capture is an opt-in diagnostic/development mode. When `TelemetryCapture:RawCaptureEnabled` is `true`, each live capture produces a directory with four core artifacts:

- `capture-manifest.json`
- `telemetry-schema.json`
- `telemetry.bin`
- `latest-session.yaml`

Optional historical session snapshots are stored under `session-info/`.

After capture finalization, the app also writes compact post-session sidecars when possible:

- `capture-synthesis.json` summarizes the raw capture schema and sampled frame values for investigation without reopening the full binary payload. It is bounded by `TelemetryCapture:MaxSynthesisMilliseconds`.
- `ibt-analysis/*.json` summarizes the best matching iRacing `.ibt` file when IBT analysis is enabled and a stable candidate is available.
- `live-model-parity.json` summarizes whether the additive model-v2 live state matched the legacy overlay inputs during the session, records raw/IBT signal availability for later model review, and includes `promotionReadiness` so enough clean evidence can be flagged for model-v2 cutover review.

These sidecars are additive. Existing raw captures without them remain readable, startup recovery can fill missing sidecars later, and source `.ibt` files are not copied into capture directories by default.

Compact edge-case telemetry artifacts are not part of the raw capture format. They are written separately under the logs root as JSON and may be included in diagnostics bundles without including `telemetry.bin`.

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
