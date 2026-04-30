# Current State

Last updated: 2026-04-29

## Project Goal

`tmrOverlay` is being built as a Windows-native iRacing companion. The product direction has shifted from always-on raw capture toward live telemetry analysis plus compact historical session summaries. Raw capture remains available as an opt-in diagnostic/development mode.

## Implemented So Far

### Application shell

- `src/TmrOverlay.App/TmrOverlay.App.csproj`
  - .NET 8 WinForms app
  - `WinExe`
  - uses `irsdkSharp` and `Microsoft.Extensions.Hosting`
  - references `src/TmrOverlay.Core/` for platform-neutral settings, telemetry/history models, live read models, fuel strategy logic, overlay metadata, and post-race analysis models

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
  - `Abstractions/` contains Windows overlay form helpers such as `PersistentOverlayForm`, which centralizes frame setup, drag handling, and per-overlay settings persistence
  - `Styling/OverlayTheme.cs` contains human-editable shared Windows overlay colors, font helpers, and common layout tokens; data-visualization-specific colors can remain near their drawing code
  - optional `overlay-theme.json` under the app settings root can override shared font/color tokens without recompiling
  - `Status/` contains the current collector status overlay
  - `SettingsPanel/` contains a centered tabbed settings window for user-managed overlay visibility, scale, session filters, shared font/units, runtime raw-capture requests, placeholder Overlay Bridge controls, post-race analysis browsing, and overlay-specific display options
  - `FuelCalculator/` contains the first strategy overlay, backed by live telemetry plus exact car/track/session history
  - `CarRadar/` contains a transparent circular proximity overlay, backed by `CamCarIdx`, player-only `CarLeftRight`, reliable live timing gaps, and nearby `CarIdx*` progress/position telemetry
  - `GapToLeader/` contains a rolling in-class gap trend graph, backed by `CarIdxF2Time` with progress fallback

- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
  - tiny always-on-top overlay placed at `(24, 24)`
  - intended for the top-left of the primary display
  - no taskbar icon
  - draggable during runtime
  - persists position/size under app-owned settings
  - display-only; visibility, detail rows, and runtime raw-capture requests now live in the settings window
  - shows live telemetry analysis state, latest SDK-frame freshness, warning/error text, and raw capture write health only when raw capture is enabled
  - state colors:
    - gray: waiting for iRacing
    - amber: waiting, connected-without-capture, app warning, or dropped frames
    - green: actively analyzing live telemetry, or actively collecting raw telemetry with confirmed disk writes
  - red: telemetry read errors, stale SDK frames, capture writer errors, queued frames not reaching disk, or stale disk writes when raw capture is enabled
  - new overlay features should log unexpected refresh/render failures and surface a compact visible error state, while normal telemetry gaps should degrade to waiting/unavailable

- `src/TmrOverlay.App/Overlays/SettingsPanel/`
  - wide 1080x600 settings window with a `TMR Overlay` title bar and vertical left-side tabs so all current tabs fit without horizontal tab-strip scrolling
  - the settings window is recentered whenever it opens and does not persist user-dragged placement between runs
  - opens on startup and can be reopened from the tray menu
  - acts as the main UI; clicking its `X` or otherwise closing it through the user close path exits the application instead of hiding the app to the tray
  - uses normal desktop z-order, taskbar, and Alt+Tab behavior instead of the product overlays' tool-window/always-on-top behavior
  - tabs include General, Error Logging, each current overlay, an Overlay Bridge placeholder for post-v1.0 controls, and Post-race Analysis with a past-session picker backed by saved analysis rows plus the built-in four-hour sample; refreshes default to the most recent analysis
  - General exposes a shared overlay font-family selector from widely available fonts, a metric/imperial unit selector, and copy-only Windows clean/build/publish/zip commands for local development
  - Error Logging shows the current app warning/error from `TelemetryCaptureState`, opens local log/diagnostics folders, shows a lightweight performance summary, and can create/copy a diagnostics bundle using `DiagnosticsBundleService`
  - per-overlay tabs expose visibility, scale, test/practice/qualifying/race session filters, and descriptor-driven overlay-specific display options
  - opening the radar settings tab forces the radar overlay visible as a live preview even when user/session visibility would otherwise hide it
  - the Collector Status tab owns the runtime `Raw capture` checkbox
  - visibility, scale, font, unit, and display-option changes apply to open overlays immediately; session filters are rechecked against live session type

- `src/TmrOverlay.App/Overlays/FuelCalculator/`
  - draggable three-column fuel strategy overlay placed below the status overlay by default
  - estimates timed-race laps from session time, selected lap time, and leader/team progress
  - uses live fuel burn first, then exact user history for car/track/session combos; tracked baseline/sample history is opt-in
  - future fuel work should keep modeled stint-history analysis visible when a selected/focused driver lacks exposed live fuel scalars, instead of emptying the table
  - renders whole-lap stint targets, target liters-per-lap, planned stint/stop count, final stint length, laps-per-tank, and min/avg/max burn when history exists
  - keeps the full table row layout visible so strategy changes do not resize or switch the overlay view during live updates
  - adds an advice column that estimates whether tire service is likely free under refueling time or costs extra stationary time, using historical fill-rate and tire-service aggregates when available
  - adds a strategy row when useful, comparing a shorter conservative stint rhythm against the longest realistic target and quantifying extra stops plus estimated pit-time loss
  - accounts for overall leader pace/progress for timed-race lap count, stores class-leader context, and shows leader/class gaps in the source row when available
  - can bias future stint targets toward historical team-stint evidence, currently 8 laps for the 4-hour Nürburgring baseline, without labeling teammate rows in the UI
  - warns when a target such as an 8-lap stint needs realistic fuel saving versus nominal tank range or avoids extra stops over longer races
  - uses blank future rows when no fuel stop is needed or fewer stints are known, instead of hiding rows based on noisy collection thresholds
  - caches exact history lookups briefly by car/track/session so live fuel refreshes do not reread aggregate JSON every tick

- `src/TmrOverlay.App/Overlays/CarRadar/`
  - draggable 300px circular radar overlay placed to the right of the status overlay by default
  - transparent outside the circle and paints nothing when no cars are within proximity or multiclass warning range
  - follows the active camera/focus car through `CamCarIdx` when valid, falling back to the player/team car
  - uses `CarLeftRight` for side occupancy only when focused on the player car
  - uses only fresh live nearby-car telemetry for placement/timing around the focused car; live `CarIdxLapDistPct` plus track length provides preferred physical-distance placement, while `CarIdxEstTime` and `CarIdxF2Time` provide timing fallback and multiclass warning timing when reliable without inventing seconds from history or fuel estimates
  - excludes pit-road cars from radar proximity and hides the radar while the focused car is in pit-road states
  - draws the focused car as a white rectangle and nearby traffic from any class as neutral-white rectangles that fade in between radar entry and the yellow-warning threshold, then move through yellow toward saturated alert red only inside the close bumper-gap warning buffer, using physical distance inside a car-length-based radar window when possible with timing fallback labels on the rings
  - keeps per-car visual state by `CarIdx`, fades the whole radar and side-warning rectangles in/out, and treats stale live snapshots as unavailable so old proximity does not stay painted forever
  - tracks recent relative timing for other-class cars and can draw a short outer red arc with a live seconds gap when faster multiclass traffic is behind outside the 2-second timing fallback range but within 5 seconds
  - currently does not have true per-car lateral telemetry; side occupancy comes from the scalar iRacing left/right signal, and radar cars only occupy side slots when their distance or fallback timing gap is inside the side-overlap car-length window

- `src/TmrOverlay.App/Overlays/GapToLeader/`
  - draggable in-class gap trend graph placed below the radar by default
  - draws the class leader as the fixed top baseline
  - keeps bounded overlay-local four-hour in-memory traces for all available same-class timing rows; these traces are only for rendering and are not persisted
  - consumes a separate same-class timing row list so cars with valid standings/F2 timing but invalid lap-distance progress can still appear in the graph without polluting radar proximity placement
  - dynamically renders the focused car's class leader, the focused car, nearest five same-class cars ahead and behind, plus recently visible cars that need continuity as they enter/leave the nearby window
  - anchors the X-axis at the first visible sample and grows the line horizontally until the four-hour window is full, then slides the window; it scales the Y-axis to the visible field spread, keeps axis labels in a left gutter, highlights whole-lap gap reference lines when the field spreads far enough, draws vertical 5-lap duration markers, and labels current line endpoints with compact current `P<N>` class-position tags
  - draws subtle weather-condition bands behind the graph from live `TrackWetness` / `WeatherDeclaredWet`; non-focused/non-leader context lines are intentionally dimmed to keep the focused-car gap and position readable
  - marks driver swaps as compact ticks/dots on the affected line while preserving line color; Windows uses real session-info driver-row changes by `CarIdx` plus the local `DCDriversSoFar` signal, while the mac harness can use named mock handoffs
  - marks leader changes and keeps old leader/currently exiting/missing-telemetry car lines visually continuous with fade/dash behavior instead of disappearing abruptly
  - can label current line endpoints with compact `P<N>` class-position text
  - prefers `CarIdxF2Time` for timed gaps, with lap-progress fallback from `CarIdxLapCompleted` and `CarIdxLapDistPct`
  - defaults to race sessions only; a hidden migration marker prevents that default from overriding later user session-filter changes

### Telemetry collection pipeline

- `src/TmrOverlay.App/Telemetry/TelemetryCaptureHostedService.cs`
  - owns the `IRacingSDK` instance
  - subscribes to `OnConnected`, `OnDisconnected`, and `OnDataChanged`
  - starts live telemetry collection on first usable live data
  - records compact historical telemetry samples every frame
  - snapshots session YAML into the history accumulator whenever `SessionInfoUpdate` changes
  - creates raw capture directories and copies the raw telemetry buffer only when raw capture is enabled by startup configuration or runtime overlay request
  - isolates raw-capture frame queue/write/read failures from live history, normalized live telemetry, and overlay performance recording so overlays can keep updating while capture diagnostics run
  - logs and records app events for runtime raw-capture start failures instead of silently failing
  - writes normalized live frames through `ILiveTelemetrySink`, not directly to overlays

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

- `src/TmrOverlay.App/Performance/AppPerformanceState.cs`
  - stores lightweight in-memory performance counters and rolling timing summaries
  - tracks telemetry callback throughput, normalized live sink time, history accumulation time, capture writer write time, capture queue depth, overlay refresh timings, dropped/written raw frames, process memory, and GC counts
  - intentionally stores aggregate/recent-window metrics rather than every telemetry frame
  - `AppPerformanceHostedService` writes periodic JSONL snapshots under the logs performance folder regardless of raw-capture state

- `src/TmrOverlay.Core/Telemetry/Live/`
  - `LiveTelemetryStore` is the shared normalized live source for product overlays
  - `ILiveTelemetrySource` is the read boundary for overlays and the local bridge
  - `ILiveTelemetrySink` is the write boundary for live iRacing collection and replay/dev providers
  - `LiveTelemetrySnapshot` now includes fuel, proximity, leader-gap, and same-class gap graph inputs derived from each frame

### Session history

- `src/TmrOverlay.App/History/`
  - owns disk storage and lookup for compact end-of-session summaries
  - stores user summaries under `%LOCALAPPDATA%/TmrOverlay/history/user/cars/{car}/tracks/{track}/sessions/{session}/`
  - writes a per-capture summary plus an aggregate for baseline lookup
  - low-confidence samples are still stored but do not contribute to baseline aggregate values

- `src/TmrOverlay.Core/History/`
  - contains platform-neutral historical session context, telemetry samples, summary/aggregate models, slug/path helpers, session-info parsing, and the in-memory historical accumulator

- `src/TmrOverlay.App/Analysis/`
  - persists post-race analysis JSON under the user history root and populates the settings window dropdown
  - `PostRaceAnalysisPipeline` saves an analysis row after compact session history is saved and isolates analysis persistence/event failures from telemetry finalization

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
  - the disk store remains Windows app-owned; settings models and `AppSettingsMigrator` live in `src/TmrOverlay.Core/Settings/` so other development harnesses can follow the same schema
  - settings are versioned and currently migrate legacy per-overlay boolean/integer fields into the keyed overlay option bag

- `src/TmrOverlay.App/Events/`
  - writes JSONL app-event breadcrumbs under `%LOCALAPPDATA%/TmrOverlay/logs/events`

- `src/TmrOverlay.App/Runtime/`
  - writes a heartbeat/runtime-state file and detects the previous unclean shutdown
  - includes a local-development build freshness check that warns when source files in the checkout are newer than the running build

- `src/TmrOverlay.App/Diagnostics/`
  - creates support bundles with app/storage metadata, telemetry state, lightweight performance snapshots, recent performance logs, runtime state, settings, logs/events, and latest capture metadata
  - includes recent post-race analysis JSON at top-level `analysis/` plus recent user-history summaries and aggregates so collected car/track/session metrics can be inspected for accuracy
  - creates a best-effort diagnostics bundle automatically when a live telemetry session finalizes, and the Error Logging tab reports the latest automatic bundle
  - intentionally excludes raw `telemetry.bin`

- `src/TmrOverlay.App/Retention/`
  - removes old capture directories and diagnostics bundles on startup
  - removes old always-on performance JSONL logs on startup

- `src/TmrOverlay.App/Replay/`
  - provides a replay-mode seam for overlay development against an existing capture
  - is registered as the active telemetry provider through the shared provider registration path when `Replay:Enabled` is true

- `src/TmrOverlay.App/Bridge/`
  - optional disabled-by-default localhost HTTP bridge
  - exposes `GET /health` and `GET /snapshot`
  - snapshots come from `ILiveTelemetrySource`, so downstream UI clients do not read directly from iRacing or raw capture files
  - the settings window has a placeholder Overlay Bridge tab for post-v1.0 bridge controls; configuration still lives in app settings for now

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
  - the mac live mock uses the tracked four-hour Nürburgring baseline shape from race time zero at 4x speed for faster fuel/gap overlay iteration
  - for overlay development, Windows should stay production-facing and real-data-driven; the ignored mac harness can use looser mock scenes, fixed offsets, named sample drivers, and exaggerated events for fast visual iteration
  - the mac mock race mirrors the Windows radar/gap feature behavior with synthetic all-class timing rows, multiclass approach traffic, weather bands, and driver handoff events, while Windows remains real telemetry only
  - the mac harness opens the same startup overlay set as Windows: status, fuel calculator, radar, and gap-to-leader
  - the mac status overlay is display-only, matching Windows; runtime raw-capture requests live in the settings window and still record logs/events
  - the mac harness mirrors the settings window schema and basic tabbed UI for visibility, scale, session filters, font/units, raw capture, placeholder Overlay Bridge controls, post-race analysis, and a mock Error Logging/performance snapshot tab; mac diagnostics bundles include matching telemetry-state/performance metadata stubs and recent mock performance JSONL logs
  - `swift run TmrOverlayMacScreenshots` renders tracked overlay review artifacts under `mocks/`: focused live-state screenshots, multi-state contact sheets, and smaller per-state PNG cards for status, fuel calculator, settings, car radar, and gap-to-leader
  - screenshot waiting/unavailable fixtures should be isolated from local user history and cached live telemetry; for example, the fuel waiting preview uses an empty temporary history root so it cannot accidentally show stale stint rows from this machine

### Tests

- `tests/TmrOverlay.App.Tests/`
  - xUnit test project for non-UI logic
  - currently covers storage path resolution, bridge options, history path slugs, local file log writing, settings persistence/migration, diagnostics bundle contents, performance snapshot aggregation, retention cleanup, runtime-state markers, live fuel/proximity/gap derivation, fuel strategy calculations, and fuel view-model empty-state behavior

- `docs/overlay-logic.md`
  - index for human-readable overlay and analysis logic notes; update the matching note whenever overlay derivation, display rules, visibility rules, or analysis rules change

- `tools/validate_overlay_screenshots.py`
  - validates that expected screenshot PNG artifacts exist, match fixed dimensions where appropriate, and are not blank
  - should be run after regenerating screenshots, but does not replace visual review for scenario correctness, text clipping/overlap, misleading populated-empty states, or platform-specific layout behavior

- Validation standard going forward:
  - rendered screenshots are validation artifacts as well as design artifacts
  - each scenario fixture should make both positive expectations and negative expectations obvious
  - waiting/unavailable/error paths should be deterministic and isolated from local machine state unless the scenario explicitly tests history fallback or support-path display
  - the same fixture-driven approach applies to collectors, diagnostics bundles, retention, updater, settings, and performance telemetry paths
  - code changes should include a stale-reference sweep across docs, mocks, tests, the ignored mac harness, and repo skills before final validation so old behavior names/descriptions/API patterns do not survive alongside new implementation behavior

- `.github/workflows/windows-dotnet.yml`
  - restores, builds, and tests `tmrOverlay.sln` on `windows-latest`
  - this is the current production-platform verification gate because the local Mac does not have `dotnet`

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
- `OverlayBridge:Enabled`
- `OverlayBridge:Port`

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
- overlay bridge defaults off with `OverlayBridge:Enabled=false` and `OverlayBridge:Port=8765`

Environment override pattern:

- `TMR_Storage__UseRepositoryLocalStorage=true`
- `TMR_TelemetryCapture__RawCaptureEnabled=true`
- `TMR_Storage__CaptureRoot`
- `TMR_Storage__UserHistoryRoot`
- `TMR_Storage__AppDataRoot`
- `TMR_Replay__Enabled=true`
- `TMR_Replay__CaptureDirectory`
- `TMR_OverlayBridge__Enabled=true`
- `TMR_OverlayBridge__Port`

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
- Overlay modules now live under `src/TmrOverlay.App/Overlays/`; status, settings, fuel-calculator, car-radar, and gap-to-leader overlays are wired, while remaining future overlay folders are still placeholders.
- Pure models and calculations have started moving into `src/TmrOverlay.Core/`; Windows remains the production app/runtime, while the ignored mac harness remains the mock-telemetry development surface.
- The root-level launcher is `TmrOverlay.cmd`, not a standalone copied `.exe`, because a normal framework-dependent .NET build needs its companion output files.
- The primary analyzed real capture is the 4-hour Nürburgring VLN race capture. It proved that teammate stints retain `CarIdx*` timing/position data but do not expose direct scalar fuel fields.

## Recommended Next Steps

1. Add live-burn smoothing and reserve/margin settings to the fuel calculator.
2. Harden radar and leader-gap derivation against longer multi-class traffic captures, especially lapped traffic and pit-road edge cases.
3. Decide which post-v1.0 controls belong in the Overlay Bridge settings tab, such as enable/disable, port, allowed clients, schema version, and connection health.
4. Improve historical aggregation and confidence/source tracking as more user sessions are collected.
5. Keep raw capture available for diagnostics, but avoid making it the normal user data path.
6. Expand post-race strategy review/export beyond the first saved-analysis view; see `docs/post-race-strategy-analysis.md`.
7. Start with update notification before self-update; see `docs/update-strategy.md`.
8. Future branch idea: add a historical data maintenance flow that parses the user's stored history/analysis collection, detects schema or functionality-version gaps, and migrates/rebuilds data so older sessions stay compatible with newer app behavior.

## Files Most Likely To Change Next

- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/Styling/OverlayTheme.cs`
- `src/TmrOverlay.Core/`
- `src/TmrOverlay.App/Bridge/`
- `src/TmrOverlay.App/Shell/NotifyIconApplicationContext.cs`
- `src/TmrOverlay.App/History/`
- `src/TmrOverlay.App/Performance/AppPerformanceState.cs`
- `src/TmrOverlay.App/Telemetry/TelemetryCaptureHostedService.cs`
- `src/TmrOverlay.App/Telemetry/TelemetryCaptureSession.cs`
- `telemetry.md`
- `skills/tmr-overlay-context/references/fuel-overlay-context.md`
- `skills/tmr-overlay-context/references/overlay-research.md`
- `docs/capture-format.md`
- `README.md`
