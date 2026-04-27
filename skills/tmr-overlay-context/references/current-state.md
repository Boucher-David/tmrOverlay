# Current State

Last updated: 2026-04-26

## Project Goal

`tmrOverlay` is being built as a Windows-native iRacing companion. The product direction has shifted from always-on raw capture toward live telemetry analysis plus compact historical session summaries. Raw capture remains available as an opt-in diagnostic/development mode.

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

- `src/TmrOverlay.App/Shell/NotifyIconApplicationContext.cs`
  - owns the tray icon
  - exposes:
    - open latest capture
    - open capture root
    - open logs
    - create diagnostics bundle
    - exit
  - shows the overlay form on startup

### On-screen feedback

- `src/TmrOverlay.App/Overlays/`
  - overlay modules are separated by type
  - `Abstractions/` contains small shared overlay contracts
  - `PersistentOverlayForm` centralizes Windows overlay frame setup, drag handling, and per-overlay settings persistence
  - `Status/` contains the current collector status overlay
  - `FuelCalculator/` contains the first strategy overlay, backed by live telemetry plus exact car/track/session history

- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
  - tiny always-on-top overlay placed at `(24, 24)`
  - intended for the top-left of the primary display
  - no taskbar icon
  - draggable during runtime
  - persists position/size under app-owned settings
  - includes an `X` button that exits the entire app
  - includes a `Raw capture` checkbox that can request raw capture at runtime when the app was started without `TelemetryCapture:RawCaptureEnabled=true`
  - shows live telemetry analysis state, latest SDK-frame freshness, warning/error text, and raw capture write health only when raw capture is enabled
  - state colors:
    - gray: waiting for iRacing
    - amber: waiting, connected-without-capture, app warning, or dropped frames
    - green: actively analyzing live telemetry, or actively collecting raw telemetry with confirmed disk writes
  - red: telemetry read errors, stale SDK frames, capture writer errors, queued frames not reaching disk, or stale disk writes when raw capture is enabled

- `src/TmrOverlay.App/Overlays/FuelCalculator/`
  - draggable three-column fuel strategy overlay placed below the status overlay by default
  - estimates timed-race laps from session time, selected lap time, and leader/team progress
  - uses live fuel burn first, then exact user history for car/track/session combos; tracked baseline/sample history is opt-in
  - renders whole-lap stint targets, target liters-per-lap, planned stint/stop count, final stint length, laps-per-tank, and min/avg/max burn when history exists
  - collapses unnecessary future rows when no fuel stop is needed
  - adds an advice column that estimates whether tire service is likely free under refueling time or costs extra stationary time, using historical fill-rate and tire-service aggregates when available
  - adds a strategy row when useful, comparing a shorter conservative stint rhythm against the longest realistic target and quantifying extra stops plus estimated pit-time loss
  - accounts for overall leader pace/progress for timed-race lap count, stores class-leader context, and shows leader/class gaps in the source row when available
  - can bias future stint targets toward historical team-stint evidence, currently 8 laps for the 4-hour Nürburgring baseline, without labeling teammate rows in the UI
  - warns when a target such as an 8-lap stint needs realistic fuel saving versus nominal tank range or avoids extra stops over longer races

### Telemetry collection pipeline

- `src/TmrOverlay.App/Telemetry/TelemetryCaptureHostedService.cs`
  - owns the `IRacingSDK` instance
  - subscribes to `OnConnected`, `OnDisconnected`, and `OnDataChanged`
  - starts live telemetry collection on first usable live data
  - records compact historical telemetry samples every frame
  - snapshots session YAML into the history accumulator whenever `SessionInfoUpdate` changes
  - creates raw capture directories and copies the raw telemetry buffer only when raw capture is enabled by startup configuration or runtime overlay request
  - logs and records app events for runtime raw-capture start failures instead of silently failing

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
  - current live-collection state
  - current raw capture directory when raw capture is enabled
  - frame counts
  - dropped-frame counts

### Session history

- `src/TmrOverlay.App/History/`
  - collects compact end-of-session summaries during live telemetry analysis
  - stores user summaries under `%LOCALAPPDATA%/TmrOverlay/history/user/cars/{car}/tracks/{track}/sessions/{session}/`
  - writes a per-capture summary plus an aggregate for baseline lookup
  - low-confidence samples are still stored but do not contribute to baseline aggregate values

- `history/baseline/`
  - contains tracked, sanitized development/sample data from the 4-hour Nürburgring VLN Mercedes-AMG GT3 race capture
  - is not read by default because `SessionHistory:UseBaselineHistory` defaults to `false`
  - fuel baseline uses local-driver scalar fuel frames only
  - lap timing and pit counts use team-car `CarIdx*` telemetry because those remain valid during teammate stints
  - session summaries can store completed stint summaries and per-stop pit-service timing estimates with confidence flags
  - teammate stint fuel is modeled from user/baseline history unless a reliable live scalar source is available
  - the 4-hour baseline summary now includes sanitized local-driver 7-lap and teammate-driver 8-lap stint history for planning hints
  - the 4-hour baseline aggregate includes observed fuel fill rate and inferred tire-service timing for early tire guidance

### Storage

- `src/TmrOverlay.App/Storage/AppStorageOptions.cs`
  - centralizes app-owned local storage roots
  - default writable root is `%LOCALAPPDATA%/TmrOverlay`
  - repository-local storage is opt-in with `Storage:UseRepositoryLocalStorage`
  - tracked sample/baseline history belongs under `history/baseline` but production lookup is opt-in

### Logging

- `src/TmrOverlay.App/Logging/`
  - custom local file logger provider
  - writes rolling logs under `%LOCALAPPDATA%/TmrOverlay/logs`
  - logs startup/shutdown, storage roots, telemetry service messages, and unhandled exceptions
  - logging cleanup is best-effort and must not affect capture reliability

### Application boilerplate

- `src/TmrOverlay.App/Settings/`
  - persists overlay settings under `%LOCALAPPDATA%/TmrOverlay/settings`

- `src/TmrOverlay.App/Events/`
  - writes JSONL app-event breadcrumbs under `%LOCALAPPDATA%/TmrOverlay/logs/events`

- `src/TmrOverlay.App/Runtime/`
  - writes a heartbeat/runtime-state file and detects the previous unclean shutdown
  - includes a local-development build freshness check that warns when source files in the checkout are newer than the running build

- `src/TmrOverlay.App/Diagnostics/`
  - creates support bundles with app/storage metadata, runtime state, settings, logs/events, and latest capture metadata
  - intentionally excludes raw `telemetry.bin`

- `src/TmrOverlay.App/Retention/`
  - removes old capture directories and diagnostics bundles on startup

- `src/TmrOverlay.App/Replay/`
  - provides a replay-mode seam for overlay development against an existing capture

### Local mac harness

- `local-mac/TmrOverlayMac/`
  - ignored by git
  - mirrors the Windows app structure for local macOS development
  - uses mock telemetry instead of iRacing
  - defaults to live mock telemetry analysis plus compact history; raw mock capture is opt-in with `TMR_MAC_RAW_CAPTURE_ENABLED=true`
  - defaults writable data to `~/Library/Application Support/TmrOverlayMac`
  - includes the same categories of logs, events, settings, runtime state, diagnostics, retention, session history, and overlay folder structure
  - mirrors the capture-health overlay fields and build freshness warning
  - can preview spoofed overlay states with `TMR_MAC_DEMO_STATES=true ./run.sh`
  - the mac menu exposes manual demo states for waiting, connected-without-capture, healthy live-analysis, healthy raw-capture, stale build, dropped frames, frames-not-written, disk-stalled, and capture-error
  - going forward, shared product and app-boilerplate changes should be mirrored in both Windows and mac unless the change is explicitly Windows/iRacing-specific
  - overlay windows use the shared mac `OverlayWindow` through `OverlayManager`, so each overlay id can restore its saved position/size
  - the mac fuel overlay uses live mock telemetry plus local user history, matching the Windows baseline-disabled default
  - the mac harness only opens the real-use fuel overlay at startup, matching Windows startup overlay parity
  - the mac status overlay mirrors the runtime raw-capture checkbox and logs/events behavior

### Tests

- `tests/TmrOverlay.App.Tests/`
  - xUnit test project for non-UI logic
  - currently covers storage path resolution, history path slugs, local file log writing, settings persistence, diagnostics bundle contents, retention cleanup, runtime-state markers, and fuel strategy calculations

## Capture Format

See `docs/capture-format.md`.

Short version for opt-in raw capture:

- `telemetry-schema.json` stores variable metadata
- `telemetry.bin` stores raw frame payloads with a small per-frame header
- `latest-session.yaml` stores the latest raw session string
- `session-info/` preserves session-history snapshots

Raw capture format is preserved for diagnostics and future deep-dive analysis, but it is no longer the default production data path.

## Telemetry Summary

See `telemetry.md`.

That file is the human-oriented outline of the current schema in three layers:

- event
- session
- car

It should be kept in sync with how we interpret raw capture artifacts for analysis work.

## Analysis State

Additional preserved context lives in:

- `references/fuel-overlay-context.md`
- `references/overlay-research.md`

Those files should be read when work shifts from capture plumbing into:

- fuel and stint logic
- telemetry interpretation
- overlay feature design
- overlay UX/layout direction

## Configuration

`src/TmrOverlay.App/appsettings.json`

Current keys:

- `TelemetryCapture:StoreSessionInfoSnapshots`
- `TelemetryCapture:RawCaptureEnabled`
- `TelemetryCapture:QueueCapacity`
- `SessionHistory:Enabled`
- `SessionHistory:UseBaselineHistory`
- `Storage:UseRepositoryLocalStorage`
- `Storage:AppDataRoot`
- `Storage:CaptureRoot`
- `Storage:UserHistoryRoot`
- `Storage:BaselineHistoryRoot`
- `Storage:LogsRoot`
- `Storage:SettingsRoot`
- `Storage:DiagnosticsRoot`
- `Storage:EventsRoot`
- `Storage:RuntimeStatePath`
- `Logging:File:Enabled`
- `Logging:File:MinimumLevel`
- `Logging:File:MaxFileBytes`
- `Logging:File:RetainedFileCount`
- `Retention:Enabled`
- `Retention:CaptureRetentionDays`
- `Retention:MaxCaptureDirectories`
- `Retention:DiagnosticsRetentionDays`
- `Retention:MaxDiagnosticsBundles`
- `Replay:Enabled`
- `Replay:CaptureDirectory`
- `Replay:SpeedMultiplier`

Current default:

- writable storage resolves under `%LOCALAPPDATA%/TmrOverlay`
- raw captures default to `%LOCALAPPDATA%/TmrOverlay/captures` but are disabled unless `TelemetryCapture:RawCaptureEnabled=true`
- user history defaults to `%LOCALAPPDATA%/TmrOverlay/history/user`
- local logs default to `%LOCALAPPDATA%/TmrOverlay/logs`
- app events default to `%LOCALAPPDATA%/TmrOverlay/logs/events`
- settings default to `%LOCALAPPDATA%/TmrOverlay/settings`
- diagnostics default to `%LOCALAPPDATA%/TmrOverlay/diagnostics`
- runtime state defaults to `%LOCALAPPDATA%/TmrOverlay/runtime-state.json`
- baseline/sample lookup defaults off with `SessionHistory:UseBaselineHistory=false`

Environment override pattern:

- `TMR_Storage__UseRepositoryLocalStorage=true`
- `TMR_TelemetryCapture__RawCaptureEnabled=true`
- `TMR_Storage__CaptureRoot`
- `TMR_Storage__UserHistoryRoot`
- `TMR_Storage__AppDataRoot`
- `TMR_Replay__Enabled=true`
- `TMR_Replay__CaptureDirectory`

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

- The current machine does not have `dotnet`, so Windows build/test verification still needs to happen on a .NET-equipped machine.
- The local mac Swift package builds, but `swift test` currently requires an XCTest-capable Swift/Xcode toolchain.
- No general-purpose replay/decoder tool exists yet for `telemetry.bin`; targeted analysis scripts exist under `tools/analysis/`.
- Overlay modules now live under `src/TmrOverlay.App/Overlays/`; status and fuel-calculator overlays are wired, but the remaining overlay folders are placeholders.
- The root-level launcher is `TmrOverlay.cmd`, not a standalone copied `.exe`, because a normal framework-dependent .NET build needs its companion output files.
- The primary analyzed real capture is the 4-hour Nürburgring VLN race capture. It proved that teammate stints retain `CarIdx*` timing/position data but do not expose direct scalar fuel fields.

## Recommended Next Steps

1. Add live-burn smoothing and reserve/margin settings to the fuel calculator.
2. Add a live analysis snapshot/bridge layer so overlay windows can subscribe to derived metrics instead of talking to the SDK directly.
3. Improve historical aggregation and confidence/source tracking as more user sessions are collected.
4. Keep raw capture available for diagnostics, but avoid making it the normal user data path.
5. Treat post-race strategy review/export as a future branch; see `docs/post-race-strategy-analysis.md`.

## Files Most Likely To Change Next

- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
- `src/TmrOverlay.App/Shell/NotifyIconApplicationContext.cs`
- `src/TmrOverlay.App/History/`
- `src/TmrOverlay.App/Telemetry/TelemetryCaptureHostedService.cs`
- `src/TmrOverlay.App/Telemetry/TelemetryCaptureSession.cs`
- `telemetry.md`
- `skills/tmr-overlay-context/references/fuel-overlay-context.md`
- `skills/tmr-overlay-context/references/overlay-research.md`
- `docs/capture-format.md`
- `README.md`
