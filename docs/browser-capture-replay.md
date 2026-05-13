# Browser Capture Replay

Development-only browser replay can stream sampled raw-capture frames into the
localhost browser overlay routes without running iRacing or the Windows app.

## Export

```bash
python3 tools/analysis/export_standings_browser_replay.py \
  --capture captures/capture-YYYYMMDD-HHMMSS-fff \
  --output /tmp/tmr-browser-replay.json \
  --stride 60 \
  --max-frames 20000
```

Real-capture replay used for graph or stream validation must keep dense raw
telemetry cadence. The exporter records raw capture `SessionTime`,
`capturedUnixMs`, frame-index deltas, and source-cadence summaries in
`source.cadence`; by default it rejects selected frames whose positive
`SessionTime` deltas exceed the Gap To Leader production missing-segment
threshold of 10 seconds. Export with an explicit stride small enough for the
capture tick rate, or a large enough `--max-frames`, so graph points remain
production-like. Use `--allow-sparse-review` only for table/status review; do
not patch overlay data, graph points, or segment flags to make sparse samples
look connected.

For race-start review, align sampled frames to green by choosing the first raw
frame and relative clock manually. The relative clock is navigation metadata
only; stream cadence still comes from raw capture frame/session-time deltas.

```bash
python3 tools/analysis/export_standings_browser_replay.py \
  --capture captures/capture-YYYYMMDD-HHMMSS-fff \
  --output /tmp/tmr-race-start-browser-replay.json \
  --start-frame 148236 \
  --stride 120 \
  --max-frames 121 \
  --start-relative-seconds -120 \
  --step-seconds 2
```

The exporter reads `capture-manifest.json`, `telemetry-schema.json`,
`telemetry.bin`, `latest-session.yaml`, and `session-info/*.yaml`. Each replay
frame contains:

- the existing Standings display model derived from raw telemetry/session data
- `live.models` for session, reference, driver directory, scoring, timing,
  relative, spatial/radar inputs, race events, fuel, weather, inputs, and
  track-map sector context
- raw frame metadata including frame index, session time, session state, camera
  car, and player car
- source cadence metadata including source elapsed time, frame/session-time
  deltas, captured-time deltas, and whether the selection is dense enough for
  Gap To Leader graph validation

## Serve

```bash
TMR_STANDINGS_REPLAY_TIMING=source \
TMR_STANDINGS_REPLAY_SPEED=60 \
node tools/browser-review/standings-replay-server.mjs /tmp/tmr-browser-replay.json
```

Open `http://127.0.0.1:5187/review/overlays` or a production-style browser
route such as `http://127.0.0.1:5187/overlays/standings`.

Use `?frame=N` to pin a sampled replay frame. Use `?rel=-120`, `?rel=0`, or
another exported relative race-start second when the export includes
`--start-relative-seconds` and `--step-seconds`.

The replay server defaults to source-elapsed timing, scaled by
`TMR_STANDINGS_REPLAY_SPEED`, so live routes advance according to captured
source time instead of a hidden fixed frame count. Set
`TMR_STANDINGS_REPLAY_TIMING=fixed-frame` with
`TMR_STANDINGS_REPLAY_FRAME_MS=250` only for explicit compressed review. The
`/api/replay/status` payload reports the effective timing mode, current source
position, and cadence summary so reviewers can confirm whether a replay is
dense enough for Gap To Leader.

## Validate

```bash
node tools/browser-review/validate-race-start-replay.mjs \
  http://127.0.0.1:5187 \
  /tmp/tmr-browser-replay-validation \
  --rel=-120,0,120 \
  --require-capture-live
```

The validator loads every browser overlay route, fetches each overlay model,
checks basic render/model invariants, and writes screenshots plus
`race-start-overlay-validation.json`. The `--require-capture-live` option also
asserts that `/api/snapshot` is serving capture-derived live models with timing,
driver-directory, scoring, and input data.

Gap To Leader validation rejects sparse replay streams whose graph points are
more than 10 seconds apart unless the segment is explicitly marked as intended
missing telemetry. A sparse failure means the replay should be re-exported from
denser raw capture frames; it is not a reason to change production/native graph
segmentation or to force sparse browser samples into connected lines.

## Limits

This is not a full app/runtime replay provider. It does not write through the
Windows `ILiveTelemetrySink`, does not exercise native WinForms windows, and
does not prove iRacing SDK connection, focus/topmost/click-through behavior, or
settings persistence.

Standings uses a capture-derived display model. Other model-route overlays use
small browser-review summaries built from the exported `live.models`; that is
enough to exercise browser routes and screenshot/model validation against real
frame timing, field coverage, focus/player identity, inputs, fuel, weather, and
race state, but it is not a byte-for-byte production overlay view-model replay.
