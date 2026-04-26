# Current State

Last updated: 2026-04-25

## Project Goal

`tmrOverlay` is being built as a Windows-native iRacing companion. The initial focus is a background collector that can ingest live iRacing session data and raw telemetry reliably. Overlay rendering comes later, after the ingestion path is stable and the stored data is good enough to analyze.

## Implemented So Far

### Application shell

- `src/TmrOverlay.App/TmrOverlay.App.csproj`
  - .NET 8 WinForms app
  - `WinExe`
  - uses `irsdkSharp` and `Microsoft.Extensions.Hosting`

- `src/TmrOverlay.App/Program.cs`
  - builds a generic host
  - loads `appsettings.json`
  - starts the tray application context

- `src/TmrOverlay.App/NotifyIconApplicationContext.cs`
  - owns the tray icon
  - exposes:
    - open latest capture
    - open capture root
    - exit
  - shows the overlay form on startup

### On-screen feedback

- `src/TmrOverlay.App/StatusOverlayForm.cs`
  - tiny always-on-top overlay placed at `(24, 24)`
  - intended for the top-left of the primary display
  - no taskbar icon
  - no activation
  - click-through style via extended window flags
  - state colors:
    - gray: waiting for iRacing
    - amber: connected, waiting for first frame
    - green: actively collecting live session data

### Telemetry capture pipeline

- `src/TmrOverlay.App/Telemetry/TelemetryCaptureHostedService.cs`
  - owns the `IRacingSDK` instance
  - subscribes to `OnConnected`, `OnDisconnected`, and `OnDataChanged`
  - starts a new capture directory on first usable live data
  - copies the raw telemetry buffer each frame
  - snapshots session YAML whenever `SessionInfoUpdate` changes

- `src/TmrOverlay.App/Telemetry/TelemetryCaptureSession.cs`
  - owns the per-capture file writer
  - writes:
    - `telemetry.bin`
    - `telemetry-schema.json`
    - `capture-manifest.json`
    - `latest-session.yaml`
    - optional historical `session-info/*.yaml`
  - uses a bounded channel to decouple SDK callbacks from disk writes
  - drops frames when the queue fills instead of blocking the SDK callback path

- `src/TmrOverlay.App/Telemetry/TelemetryCaptureState.cs`
  - stores shared status for the tray and overlay
  - current connection state
  - current capture directory
  - frame counts
  - dropped-frame counts

## Capture Format

See `docs/capture-format.md`.

Short version:

- `telemetry-schema.json` stores variable metadata
- `telemetry.bin` stores raw frame payloads with a small per-frame header
- `latest-session.yaml` stores the latest raw session string
- `session-info/` preserves session-history snapshots

The important architectural choice is that we store the raw shared-memory buffer, not just high-level JSON snapshots. That keeps future analysis and overlay derivation flexible.

## Configuration

`src/TmrOverlay.App/appsettings.json`

Current keys:

- `TelemetryCapture:CaptureRoot`
- `TelemetryCapture:StoreSessionInfoSnapshots`
- `TelemetryCapture:QueueCapacity`

Environment override pattern:

- `TMR_TelemetryCapture__CaptureRoot`

## Real-World Data Sources Already Identified

### `irdashies`

This repo is useful as a reference implementation and as a source of captured fixtures.

What was found in the cloned repo:

- `test-data/` contains 40 `telemetry.json` files and 40 `session.json` files
- sample session inspected:
  - track: Road Atlanta
  - config: Full Course
  - event/session type: Practice
  - build version: `2026.02.02.02`
  - driver count: 2
- sample telemetry keys include:
  - `SessionTime`
  - `SessionTick`
  - `Brake`
  - `BrakeRaw`
  - `CarIdxBestLapTime`
  - `CarIdxClassPosition`
  - `AirTemp`

Use this repo when we want:

- realistic JSON fixture shapes
- ideas for session and telemetry modeling
- proof that downstream overlays can be developed against stored data

### `irsdkSharp`

Useful mainly as the current integration layer. The cloned repo also includes `tests/testdata/session.ibt`, which may be useful later for offline parsing or replay-style experiments.

### Official iRacing SDK docs

Treat the docs as schema/reference material, not as a ready-made real-world dataset source.

## Known Limitations

- The scaffold was authored on a machine without `dotnet`, so no compile or runtime verification has happened yet.
- No replay/decoder tool exists yet for `telemetry.bin`.
- No user-configurable overlay placement exists yet.
- No overlay rendering pipeline exists yet beyond the small live-status box.
- No persistence for logs exists yet beyond capture artifacts.

## Recommended Next Steps

1. Build and run on Windows to verify the tray app, overlay, and SDK connection.
2. Confirm the overlay remains visible over iRacing and that the collector writes capture folders as expected.
3. Add a tiny decoder or inspector utility for `telemetry.bin` so stored captures are easy to inspect.
4. Add a local bridge layer so later overlay windows can subscribe to live snapshots instead of talking to the SDK directly.
5. Decide whether overlays stay in WinForms or move to a dedicated rendering/UI layer.

## Files Most Likely To Change Next

- `src/TmrOverlay.App/StatusOverlayForm.cs`
- `src/TmrOverlay.App/NotifyIconApplicationContext.cs`
- `src/TmrOverlay.App/Telemetry/TelemetryCaptureHostedService.cs`
- `src/TmrOverlay.App/Telemetry/TelemetryCaptureSession.cs`
- `docs/capture-format.md`
- `README.md`

