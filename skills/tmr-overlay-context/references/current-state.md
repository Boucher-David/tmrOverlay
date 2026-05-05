# Current State

Last updated: 2026-05-05

## Project Goal

`tmrOverlay` is being built as a Windows-native iRacing companion. The product direction has shifted from always-on raw capture toward live telemetry analysis plus compact historical session summaries. Raw capture remains available as an opt-in diagnostic/development mode.

## Implemented So Far

### Application shell

- `assets/`
  - contains repo-owned source visual assets such as brand/logo images for future app icons, overlay branding, docs, and publishing work
  - the v0.9 publishing branch derives the Windows executable icon from the source logo into `src/TmrOverlay.App/Assets/TmrOverlay.ico`

- `src/TmrOverlay.App/TmrOverlay.App.csproj`
  - .NET 8 WinForms app
  - `WinExe`
  - uses `irsdkSharp` and `Microsoft.Extensions.Hosting`
  - references `src/TmrOverlay.Core/` for platform-neutral settings, telemetry/history models, live read models, fuel strategy logic, overlay metadata, and post-race analysis models

- `src/TmrOverlay.App/Program.cs`
  - uses a local named mutex so a second Windows launch exits instead of starting another telemetry collector
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
  - `Status/` contains the current internal collector status overlay, hidden by default for normal users
  - `SettingsPanel/` contains a branded fixed-size tabbed settings window for user-managed overlay visibility, scale/opacity when applicable, session filters, units, support capture/log/diagnostics access, and overlay-specific display options
  - `FuelCalculator/` contains the first strategy overlay, backed by live telemetry plus exact car/track/session history
  - `Standings/` contains a compact same-class timing table backed by `LiveTelemetrySnapshot.Models.Timing`
  - `Relative/` contains the first production model-v2 overlay, a telemetry-first relative table backed by `LiveTelemetrySnapshot.Models.Relative`
  - `TrackMap/` contains the transparent map-only track-map overlay: generated local map geometry when available, circle fallback otherwise, live car dots placed by lap-distance progress, focused/player `P<N>` text inside the marker dot, full-opacity track lines, and setting-driven internal map fill opacity
  - `GarageCover/` contains a streamer privacy cover overlay; when enabled, it appears only while live telemetry reports the iRacing Garage screen as visible, paints an opaque imported image or black TMR logo fallback, and copies imported images into app-owned settings storage
  - `Flags/` contains a transparent primary-screen border overlay for live session flag categories; it defaults to the main monitor size, centers a 4:3 frame on ultrawide displays, and requires recognized live session context before the runtime window is shown
  - `CarRadar/` contains a transparent circular local in-car proximity overlay, backed by local player/team progress, player-only `CarLeftRight`, reliable live timing gaps, and nearby `CarIdx*` progress/position telemetry
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
  - wide 1240x680 settings window with a TMR logo plus `Tech Mates Racing Overlay` title bar and flat vertical left-side tabs so all current tabs fit without horizontal tab-strip scrolling
  - the settings window is recentered whenever it opens and does not persist user-dragged placement between runs
  - opens on startup and can be reopened from the tray menu
  - acts as the main UI; clicking its `X` or otherwise closing it through the user close path exits the application instead of hiding the app to the tray
  - uses normal desktop z-order, taskbar, and Alt+Tab behavior instead of the product overlays' tool-window/always-on-top behavior
  - tabs include General, user-facing overlay tabs ordered by common race workflow, and Support last
  - General exposes a metric/imperial unit selector; user-facing font selection stays hidden while cross-platform screenshot parity remains a theme-level concern
  - Support is task-oriented: compact app status, live/session state, current issue, diagnostics bundle actions, diagnostic telemetry capture, storage shortcuts, discoverable advanced collection status, and compact app activity
  - per-overlay tabs expose visibility, scale, opacity, test/practice/qualifying/race session filters, and descriptor-driven overlay-specific display options when those controls make sense for that overlay
  - opening the radar settings tab previews the radar overlay only when the overlay is enabled, so the tab no longer overrides the `Visible` checkbox
  - visibility, scale, opacity, unit, and display-option changes apply to open overlays immediately; session filters are rechecked against live session type

- Deferred overlay UI/style v2:
  - current model-v2 work standardizes live data, not visual structure
  - treat UI/style v2 as telemetry-first by default: standings, relative, local in-car radar, flags, session/weather context, and timing tables should be dense, stable windows into iRacing telemetry
  - persistent source footers should be validation/admin chrome, not default end-user overlay furniture
  - reserve model-v2 source, quality, usability, freshness, and missing-reason chrome for stale, unavailable, modeled, or derived values, especially analysis products like fuel strategy, non-local radar focus/multiclass interpretation, and gap graphs
  - use competitor overlay analysis as the product-shape check: small purpose-built overlays, dense information, low-noise dark styling, and semantic color instead of one monolithic dashboard
  - the ignored mac harness now owns a generated `mocks/design-v2/` proving ground for telemetry-first standings, relative, local in-car radar, flag display, table semantics, and narrower analysis-exception states while model-v2 race evidence is still being collected
  - future style groundwork should add semantic theme tokens and reusable WinForms primitives for headers, status badges, source footers, metric rows, table cells, graph panels, shared borders, severity colors, class colors, text fitting, and empty/error/waiting states
  - migrate this additively one overlay at a time with screenshot validation, keeping overlay-specific domain layout local

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
  - skips expensive strategy/UI work when the live snapshot sequence and display options are unchanged, and only mutates labels/table layout when target values changed

- `src/TmrOverlay.App/Overlays/Relative/`
  - draggable telemetry-first relative overlay placed below the fuel calculator by default
  - is the first production overlay to consume `LiveTelemetrySnapshot.Models.Relative` directly instead of a legacy overlay-specific snapshot slice
  - renders actual cars ahead above a reference row and actual cars behind below it, with configurable 0-8 maximum row counts per side; during live telemetry it keeps those configured row slots stable and leaves unused slots blank so the reference row does not jump
  - draws the class color as a small horizontal bar inside the position cell instead of allocating a separate class-color column
  - uses model-v2 relative rows from proximity first and timing/class-gap fallback when proximity is unavailable; when proximity has lap-distance placement but no direct relative seconds, the relative model can infer display seconds from live lap distance times the current lap-time signal while leaving radar timing stricter
  - normalizes gap signs by row direction so cars ahead display negative gaps and cars behind display positive gaps regardless of source sign
  - keeps configured ahead/reference/behind row slots stable during live telemetry so nearby-car churn updates labels in place instead of causing table-height and reference-row jumps
  - keeps source/evidence chrome quiet in the normal case; the footer only calls out live proximity, model-v2 timing fallback, partial timing, or waiting
  - treats snapshots older than 1.5 seconds as stale and shows waiting instead of retaining old rows
  - has a matching generated mac-harness screenshot set under `mocks/relative/`

- `src/TmrOverlay.App/Overlays/CarRadar/`
  - draggable 300px circular radar overlay placed to the right of the status overlay by default
  - transparent outside the circle and paints nothing when no cars are within proximity or multiclass warning range
  - is local in-car first: hides for explicit non-player focus, garage/off-track, and pit contexts instead of trying to reinterpret spectator/teammate focus as production radar
  - uses local `CarLeftRight` for side occupancy and local player/team progress/timing for nearby placement; `Focus*` fields are used only when focus is local or not explicitly another car
  - uses only fresh live nearby-car telemetry for placement/timing around the local car; live `CarIdxLapDistPct` plus track length provides preferred physical-distance placement, while `CarIdxEstTime` and `CarIdxF2Time` provide timing fallback and multiclass warning timing when reliable without inventing seconds from history or fuel estimates
  - excludes pit-road cars from radar proximity and hides the radar while the local car is in pit-road states
  - draws the local car as a white rectangle and nearby traffic from any class as neutral-white rectangles that fade in between radar entry and the yellow-warning threshold, then move through yellow toward saturated alert red only inside the close bumper-gap warning buffer, using physical distance inside a car-length-based radar window when possible with timing fallback labels on the rings
  - keeps per-car visual state by `CarIdx`, fades the whole radar and side-warning rectangles in/out, and treats stale live snapshots as unavailable so old proximity does not stay painted forever
  - clips the Windows form to a circular region, uses a black transparency key instead of fuchsia, and drops form opacity to zero after fade-out so a transparency-key failure cannot leave a purple backing window visible
  - when `CarLeftRight` is active and a close decoded radar car likely caused the side warning, attaches that car to the side slot, suppresses its normal center-lane rectangle, and biases the side marker forward/back from the local car using longitudinal gap
  - tracks recent relative timing for other-class cars and can draw a short outer red arc with a live seconds gap when faster multiclass traffic is behind outside the 2-second timing fallback range but within 5 seconds
  - currently does not have true per-car lateral telemetry; side occupancy comes from the scalar iRacing left/right signal, and decoded radar cars only attach to side slots when their distance or fallback timing gap is inside the close side-attachment window

- `src/TmrOverlay.App/Overlays/GapToLeader/`
  - draggable in-class gap trend graph placed below the radar by default
  - draws the class leader as the fixed top baseline
  - keeps bounded overlay-local four-hour in-memory traces for all available same-class timing rows; these traces are only for rendering and are not persisted
  - consumes a separate same-class timing row list so cars with valid standings/F2 timing but invalid lap-distance progress can still appear in the graph without polluting radar proximity placement
  - dynamically renders the focused car's class leader, the focused car, nearest five same-class cars ahead and behind, plus recently visible cars that need continuity as they enter/leave the nearby window
  - anchors the X-axis at the first visible sample, starts with a readable short window, grows toward the four-hour cap, then slides the window after four hours; it scales the Y-axis to the visible field spread, keeps axis labels in a left gutter, highlights whole-lap gap reference lines when the field spreads far enough, draws vertical 5-lap duration markers, and labels current line endpoints with compact current `P<N>` class-position tags
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
  - ties IBT telemetry logging to the same raw-capture switch by default, so raw capture start/stop requests also ask iRacing to start/stop `.ibt` logging
  - writes post-session sidecars after raw capture finalization: `capture-synthesis.json` runs immediately from the closed TmrOverlay capture with a timeout, while `ibt-analysis/*.json` waits only a bounded time for iRacing to stop writing before skipping and leaving the capture eligible for startup recovery
  - IBT analysis now includes `ibt-local-car-summary.json`, a bounded local-car trajectory/fuel/vehicle-dynamics summary with track-map readiness and explicit missing opponent-context fields; source `.ibt` files still stay out of capture directories by default
  - records live model-v2 parity in observer mode; Relative now consumes `LiveTelemetrySnapshot.Models.Relative` directly, while the remaining legacy overlay slices are compared against the additive models and summarized in `live-model-parity.json`
  - records passive live overlay diagnostics in observer mode; `live-overlay-diagnostics.json` summarizes gap session semantics, large gap/jump evidence, local-only radar suppression, side/focus/placement evidence, fuel source evidence, sampled position cadence, lap-delta channel availability, and derived sector-timing coverage
  - records bounded compact edge-case telemetry artifacts for every live session by combining normalized live samples with selected scalar raw watch channels for fuel, tires, suspension, brakes, wheel speed, pit service, weather, engine/replay/system/network state, incidents, and driver-control changes; artifacts also count observations dropped after the clip cap and include a final sampled context tail so long spectated/parked sessions still have late-session telemetry context
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
    - post-session `capture-synthesis.json` is written by `CaptureSynthesisService`, not by the live writer
    - post-session `ibt-analysis/*.json` is written by `IbtAnalysisService` when enabled and a candidate `.ibt` can be selected or a skipped/failed status is recorded
    - optional post-session `live-model-parity.json` is written by `LiveModelParityRecorder` when model-v2 parity collection is enabled
    - optional post-session `live-overlay-diagnostics.json` is written by `LiveOverlayDiagnosticsRecorder` when overlay diagnostics collection is enabled; without raw capture the same observer artifact is written under `logs/overlay-diagnostics`
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
  - tracks telemetry callback throughput, normalized live sink time, edge-case recorder time, history accumulation time, continuous iRacing network/system values, capture writer write time, capture queue depth, overlay refresh timings, overlay update-decision rates, dropped/written raw frames, process memory, and GC counts
  - intentionally stores aggregate/recent-window metrics rather than every telemetry frame
  - `AppPerformanceHostedService` writes periodic JSONL snapshots under the logs performance folder regardless of raw-capture state

- `src/TmrOverlay.Core/Telemetry/Live/`
  - `LiveTelemetryStore` is the shared normalized live source for product overlays
  - `ILiveTelemetrySource` is the read boundary for overlays and the local bridge
  - `ILiveTelemetrySink` is the write boundary for live iRacing collection and replay/dev providers
  - `LiveTelemetrySnapshot` includes fuel, proximity, leader-gap, and same-class gap graph compatibility inputs derived from each frame
  - `LiveTelemetrySnapshot.Models` is an additive live-only model layer with session, driver-directory, timing, relative, spatial, weather, fuel/pit, race-event, and input availability families; it is derived from existing normalized samples and keeps raw capture, compact history, post-race analysis, and existing synthesized data backwards-compatible
  - model-v2 rows carry `LiveSignalEvidence` so overlays can distinguish reliable timing, timing-only rows, spatial/radar placement eligibility, partial leader-gap timing, diagnostic instantaneous fuel burn, and rolling measured-fuel baseline eligibility as overlays migrate
  - `TimingColumnRegistry` defines reusable table-column keys and formatters for future standings/relative-style overlays; the first production Relative overlay currently keeps its table formatting local while shared primitives are refined

- `local-mac/TmrOverlayMac/`
  - mirrors the live overlay diagnostics recorder for mock/demo runs so the four-hour preview and capture-derived radar/gap demos can emit `live-overlay-diagnostics.json`
  - does not yet replay arbitrary 24-hour raw captures through every overlay; that is tracked as future harness work in `docs/model-v2-future-branches.md`
  - mirrors the Relative overlay for design-v2 iteration and generated validation screenshots

### Session history

- `src/TmrOverlay.App/History/`
  - owns disk storage and lookup for compact end-of-session summaries
  - stores user summaries under `%LOCALAPPDATA%/TmrOverlay/history/user/cars/{car}/tracks/{track}/sessions/{session}/`
  - writes a per-capture summary plus an aggregate for baseline lookup
  - runs startup history maintenance that normalizes compatible legacy summary metadata, backs up rewritten summaries, rebuilds session aggregates from compatible summaries, rejects incompatible aggregate versions during lookup, and writes `.maintenance/manifest.json`
  - low-confidence samples are still stored but do not contribute to baseline aggregate values

- `src/TmrOverlay.Core/History/`
  - contains platform-neutral historical session context, telemetry samples, summary/aggregate models, slug/path helpers, session-info parsing, and the in-memory historical accumulator

- `src/TmrOverlay.App/Analysis/`
  - can persist post-race analysis JSON under the user history root when `PostRaceAnalysis:Enabled=true`; settings exposes it only as advanced collection status, not as a normal user-facing overlay tab
  - `PostRaceAnalysisPipeline` saves an analysis row after compact session history is saved when enabled and isolates analysis persistence/event failures from telemetry finalization

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
  - settings are versioned, migrate legacy per-overlay boolean/integer fields into the keyed overlay option bag, and prune obsolete option keys when an overlay control is removed

- `src/TmrOverlay.App/Events/`
  - writes JSONL app-event breadcrumbs under `%LOCALAPPDATA%/TmrOverlay/logs/events`

- `src/TmrOverlay.App/Runtime/`
  - writes a heartbeat/runtime-state file and detects the previous unclean shutdown
  - includes a local-development build freshness check that warns when source files in the checkout are newer than the running build

- `src/TmrOverlay.App/Diagnostics/`
  - creates support bundles with app/storage metadata, telemetry state, localhost overlay state/request counters, track-map inventory metadata, lightweight performance snapshots, recent performance logs, runtime state, settings, logs/events, and latest capture metadata plus compact capture/IBT sidecars
  - includes recent compact edge-case telemetry artifacts under `edge-cases/`, including their final context tail when present
  - includes recent model-v2 parity artifacts under `model-parity/` plus the latest capture's `live-model-parity.json` when present
  - includes recent post-race analysis JSON at top-level `analysis/` plus recent user-history summaries and aggregates so collected car/track/session metrics can be inspected for accuracy
  - creates a best-effort diagnostics bundle automatically when a live telemetry session finalizes, and the Support tab reports the latest automatic bundle
  - intentionally excludes raw `telemetry.bin` and source `.ibt` payloads

- `docs/repo-surface.md`
  - separates customer/tester-facing material, product source, internal development assets, and ignored local runtime data
  - records cleanup candidates such as local `artifacts/`, root `tmroverlay-diagnostics-*` folders, ignored raw captures, and the tracked legacy raw capture that should eventually be replaced with compact fixtures

- `src/TmrOverlay.App/Retention/`
  - removes old capture directories and diagnostics bundles on startup
  - removes old always-on performance JSONL logs and compact edge-case telemetry artifacts on startup

- `src/TmrOverlay.App/Replay/`
  - provides a replay-mode seam for overlay development against an existing capture
  - is registered as the active telemetry provider through the shared provider registration path when `Replay:Enabled` is true

- `src/TmrOverlay.App/Localhost/`
  - optional disabled-by-default localhost browser-source server for OBS and other local capture tools
  - exposes `GET /health`, `GET /snapshot`, `GET /api/snapshot`, `GET /api/track-map`, `GET /api/stream-chat`, and per-overlay HTML routes under `/overlays/{id}`
  - current routes cover standings, relative, fuel calculator, session/weather, pit service, input state, car radar, gap to leader, track map, and stream chat; Flags and Garage Cover are intentionally disabled for localhost for now
  - browser-source route metadata and page scripts live with overlay modules under `src/TmrOverlay.App/Overlays/`; localhost owns HTTP transport and the generic page shell
  - pages poll `ILiveTelemetrySource`, so local browser overlays do not read directly from iRacing or raw capture files
  - records lifecycle status, request counts, route counts, failures, and last-request details into diagnostics bundles
  - Stream Chat reads one saved settings source at a time: the native overlay auto-connects to public Twitch channel chat, while the localhost route can also embed a Streamlabs Chat Box widget URL; Streamlabs widget URLs are redacted from diagnostics bundles
  - each overlay settings tab lists a selectable/copyable localhost URL, and the route remains usable even when the native overlay is hidden

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
  - the mac harness mirrors the current Windows overlay review set: status, standings, fuel calculator, relative, track map, stream chat settings/route controls, garage cover, flags, session/weather, pit-service, input/car-state, radar, and gap-to-leader
  - the mac status overlay is display-only, matching Windows; runtime raw-capture requests live in the settings window and still record logs/events
  - the mac harness mirrors the settings window schema and basic tabbed UI for visibility, scale/opacity when applicable, session filters, units, support capture, and a mock Support/performance snapshot tab; mac diagnostics bundles include matching telemetry-state/performance metadata stubs and recent mock performance JSONL logs
  - `swift run TmrOverlayMacScreenshots` renders tracked overlay review artifacts under `mocks/`: focused live-state screenshots, multi-state contact sheets, and smaller per-state PNG cards for status, fuel calculator, relative, settings, car radar, gap-to-leader, and design-v2 candidate states; the settings screenshots include the current standings and track-map tabs
  - screenshot waiting/unavailable fixtures should be isolated from local user history and cached live telemetry; for example, the fuel waiting preview uses an empty temporary history root so it cannot accidentally show stale stint rows from this machine

### Tests

- `tests/TmrOverlay.App.Tests/`
  - xUnit test project for non-UI logic
  - currently covers storage path resolution, localhost overlay options/routes, history path slugs, history maintenance/rebuild behavior, local file log writing, settings persistence/migration, diagnostics bundle contents, performance snapshot aggregation, retention cleanup, runtime-state markers, live fuel/proximity/gap derivation, fuel strategy calculations, and fuel view-model empty-state behavior

- `docs/edge-case-telemetry-logic.md`
- `docs/history-data-evolution.md`
- `docs/overlay-logic.md`
  - index for human-readable overlay and analysis logic notes; update the matching note whenever overlay derivation, display rules, visibility rules, or analysis rules change
- `docs/live-overlay-24h-findings.md`
  - live 24-hour race findings mapped to model-v2, fuel, radar, and gap follow-up work

- `tools/validate_overlay_screenshots.py`
  - validates that expected screenshot PNG artifacts exist, match fixed dimensions where appropriate, and are not blank
  - should be run after regenerating screenshots, but does not replace visual review for scenario correctness, text clipping/overlap, misleading populated-empty states, or platform-specific layout behavior

- Validation standard going forward:
  - rendered screenshots are validation artifacts as well as design artifacts
  - `skills/tmr-overlay-validation/SKILL.md` contains the repo validation checklist, including the local C# duplicate-member / primary-constructor scope scanner for Windows-only compile-shape hazards
  - each scenario fixture should make both positive expectations and negative expectations obvious
  - waiting/unavailable/error paths should be deterministic and isolated from local machine state unless the scenario explicitly tests history fallback or support-path display
  - the same fixture-driven approach applies to collectors, diagnostics bundles, retention, updater, settings, and performance telemetry paths
  - code changes should include a stale-reference sweep across docs, mocks, tests, the ignored mac harness, and repo skills before final validation so old behavior names/descriptions/API patterns do not survive alongside new implementation behavior
  - durable user-data schema changes should be treated as backwards-compatibility validation work in the same sweep: update version constants, migrations or compatible readers, docs, and the schema-compatibility test
  - behavior, calculation, default, source-label, fixture-data, and validation-semantics changes should update affected build test assertions and fixtures in the same pass; stale passing or failing assertions are stale references

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
- `capture-synthesis.json` is an additive compact sidecar written after finalization when possible and bounded by `TelemetryCapture:MaxSynthesisMilliseconds`
- `ibt-analysis/*.json` is an additive compact sidecar set written when IBT analysis is enabled; missing sidecars on older captures are expected and startup recovery can fill them later
- `live-model-parity.json` is an additive compact sidecar/log artifact written after finalization; it compares current overlay inputs with model v2, summarizes raw/IBT signal availability, and includes `promotionReadiness`
- `live_model_v2_promotion_candidate` app events are emitted when a session-level parity artifact passes the configured frame-count, mismatch-rate, and coverage thresholds; treat this as a review signal before migrating overlays, not an automatic cutover

Raw capture format is preserved for diagnostics and future deep-dive analysis, but it is no longer the default production data path.

Compact edge-case telemetry artifacts are separate from this raw capture format. They live under `%LOCALAPPDATA%/TmrOverlay/logs/edge-cases` and are included in diagnostics bundles when present. Their clip list is bounded, but they retain dropped-observation counts and a final sampled context tail for late-session diagnostics.

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
- `TelemetryCapture:MaxSynthesisMilliseconds`
- `TelemetryEdgeCases:Enabled`
- `TelemetryEdgeCases:PreTriggerSeconds`
- `TelemetryEdgeCases:PostTriggerSeconds`
- `TelemetryEdgeCases:MaxClipsPerSession`
- `TelemetryEdgeCases:MaxFramesPerClip`
- `TelemetryEdgeCases:MinimumFrameSpacingSeconds`
- `LiveModelParity:Enabled`
- `LiveModelParity:MinimumFrameSpacingSeconds`
- `LiveModelParity:MaxFramesPerSession`
- `LiveModelParity:MaxObservationsPerFrame`
- `LiveModelParity:MaxObservationSummaries`
- `LiveModelParity:PromotionCandidateMinimumFrames`
- `LiveModelParity:PromotionCandidateMaxMismatchFrameRate`
- `LiveModelParity:PromotionCandidateMinimumCoverageRatio`
- `LiveModelParity:OutputFileName`
- `LiveModelParity:LogDirectoryName`
- `LiveOverlayDiagnostics:Enabled`
- `LiveOverlayDiagnostics:MinimumFrameSpacingSeconds`
- `LiveOverlayDiagnostics:MaxSampleFramesPerSession`
- `LiveOverlayDiagnostics:MaxEventExamplesPerSession`
- `LiveOverlayDiagnostics:MaxEventExamplesPerKind`
- `LiveOverlayDiagnostics:LargeGapSeconds`
- `LiveOverlayDiagnostics:LargeGapLapEquivalent`
- `LiveOverlayDiagnostics:GapJumpSeconds`
- `LiveOverlayDiagnostics:OutputFileName`
- `LiveOverlayDiagnostics:LogDirectoryName`
- `IbtAnalysis:Enabled`
- `IbtAnalysis:TelemetryLoggingEnabled`
- `IbtAnalysis:TelemetryRoot`
- `IbtAnalysis:MaxCandidateAgeMinutes`
- `IbtAnalysis:MaxCandidateBytes`
- `IbtAnalysis:MaxAnalysisMilliseconds`
- `IbtAnalysis:MaxSampledRecords`
- `IbtAnalysis:MinStableAgeSeconds`
- `IbtAnalysis:MaxIRacingExitWaitSeconds`
- `IbtAnalysis:MaxCandidateFiles`
- `IbtAnalysis:CopyIbtIntoCaptureDirectory`
- `IbtAnalysis:OutputDirectoryName`
- `SessionHistory:Enabled`
- `SessionHistory:UseBaselineHistory`
- `PostRaceAnalysis:Enabled`
- `Storage:UseRepositoryLocalStorage`
- `Storage:AppDataRoot`
- `Storage:CaptureRoot`
- `Storage:UserHistoryRoot`
- `Storage:BaselineHistoryRoot`
- `Storage:LogsRoot`
- `Storage:SettingsRoot`
- `Storage:DiagnosticsRoot`
- `Storage:TrackMapRoot`
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
- `Retention:PerformanceLogRetentionDays`
- `Retention:MaxPerformanceLogFiles`
- `Retention:EdgeCaseRetentionDays`
- `Retention:MaxEdgeCaseFiles`
- `Replay:Enabled`
- `Replay:CaptureDirectory`
- `Replay:SpeedMultiplier`
- `LocalhostOverlays:Enabled`
- `LocalhostOverlays:Port`

Current default:

- writable storage resolves under `%LOCALAPPDATA%/TmrOverlay`
- raw captures default to `%LOCALAPPDATA%/TmrOverlay/captures` but are disabled unless `TelemetryCapture:RawCaptureEnabled=true`; post-session capture synthesis defaults to a 60-second timeout
- live model-v2 parity defaults on, samples clean frames at most once per second, keeps up to 600 sampled frames and 200 mismatch summaries, writes `live-model-parity.json` beside raw captures or under `%LOCALAPPDATA%/TmrOverlay/logs/model-parity` when no raw capture exists, and marks a session as a promotion candidate after at least 10,000 frames, mismatch-frame rate at or below 0.1%, and model coverage at or above 98% for observed legacy overlay-input families
- IBT analysis defaults on for raw captures, requests iRacing telemetry logging with the raw-capture switch, reads candidates from `%USERPROFILE%/Documents/iRacing/telemetry`, waits at most 60 seconds for iRacing to stop writing before skipping, can derive local track-map geometry after successful analysis while the default-on Track Map generation setting remains enabled, ships vetted bundled maps through app assets, and does not copy source `.ibt` files by default
- user history defaults to `%LOCALAPPDATA%/TmrOverlay/history/user`
- local logs default to `%LOCALAPPDATA%/TmrOverlay/logs`
- app events default to `%LOCALAPPDATA%/TmrOverlay/logs/events`
- settings default to `%LOCALAPPDATA%/TmrOverlay/settings`
- diagnostics default to `%LOCALAPPDATA%/TmrOverlay/diagnostics`
- track maps default to `%LOCALAPPDATA%/TmrOverlay/track-maps/user`
- runtime state defaults to `%LOCALAPPDATA%/TmrOverlay/runtime-state.json`
- baseline/sample lookup defaults off with `SessionHistory:UseBaselineHistory=false`
- localhost browser overlays default off with `LocalhostOverlays:Enabled=false` and `LocalhostOverlays:Port=8765`

Environment override pattern:

- `TMR_Storage__UseRepositoryLocalStorage=true`
- `TMR_TelemetryCapture__RawCaptureEnabled=true`
- `TMR_TelemetryEdgeCases__Enabled=false`
- `TMR_Storage__CaptureRoot`
- `TMR_Storage__UserHistoryRoot`
- `TMR_Storage__AppDataRoot`
- `TMR_Storage__TrackMapRoot`
- `TMR_Replay__Enabled=true`
- `TMR_Replay__CaptureDirectory`
- `TMR_LocalhostOverlays__Enabled=true`
- `TMR_LocalhostOverlays__Port`

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
- v0.9 portable tester publishing is tag-driven, but broad production distribution still needs a signing decision, installer/update channel, and passive update-check UI.
- The PR workflow runs restore/build/test, tracked screenshot validation, Windows screenshot artifact generation/validation, and a self-contained publish dry run with package audit. The release workflow audits the publish folder for accidental repo/dev-folder leaks, emits a package manifest, and keeps user data under the app-data root instead of the install folder.
- Because the app-data root persists across portable installs, durable settings/history schema changes must include version bumps plus migrations or compatible readers. Incompatible/future history is skipped and left on disk instead of being fed to overlays.
- Overlay modules now live under `src/TmrOverlay.App/Overlays/`; status, settings, standings, fuel-calculator, relative, track-map, stream-chat, garage-cover privacy cover, flags, session/weather, pit-service snapshot, input/car-state, car-radar, and gap-to-leader overlays are wired. Browser-source scripts for localhost routes live with their overlay modules. Remaining future product surfaces should be added deliberately rather than as placeholder overlay tabs.
- Pure models and calculations have started moving into `src/TmrOverlay.Core/`; Windows remains the production app/runtime, while the ignored mac harness remains the mock-telemetry development surface.
- The root-level launcher is `TmrOverlay.cmd`, not a standalone copied `.exe`, because a normal framework-dependent .NET build needs its companion output files.
- The primary analyzed real capture is the 4-hour Nürburgring VLN race capture. It proved that teammate stints retain `CarIdx*` timing/position data but do not expose direct scalar fuel fields.

## Recommended Next Steps

1. Close `v0.11-standings-track-map-localhost`: validate the CI-generated Windows screenshot artifact path, keep `Directory.Build.props` aligned with the milestone, confirm tracked mock screenshots still describe the intended review states, and leave only vetted bundled track-map JSON in the release package.
2. Start the next teammate-beta hardening pass: visible version/build metadata, manual update check, clearer first-run/no-iRacing states, Support-tab copy from real feedback, and portable upgrade validation against existing `%LOCALAPPDATA%\TmrOverlay` settings/history/diagnostics data.
3. Decide signing before broad distribution. Private teammate zip builds can remain unsigned, but production sharing should sign the executable or package.
4. Decide the installer/update channel after the portable baseline: Velopack for installer plus update feed, or MSIX/App Installer if package identity/signing constraints are acceptable.
5. Identify remaining v1/legacy overlay slices and choose the next safe model-v2 migration target without pulling fuel/gap analysis products into the simple-overlay path prematurely.
6. Keep collecting radar diagnostics for suppressed non-local focus, local progress-missing, side-without-placement, and multiclass cases before expanding beyond local in-car radar.
7. Harden Track Map after first real usage: current-map quality/status, manual rebuild/replace UX, bundled map QA, confidence/stale/pit-lane screenshot states, and pit-lane-aware live markers when reliable telemetry exists.
8. Decide the Overlay Bridge v2 shape for teammate-to-teammate data sharing: enable/disable, allowed peers, schema version, connection health, and which normalized model-v2 context should become the trusted peer contract.
9. Expand Stream Chat beyond the current Streamlabs-widget browser source and public Twitch channel modes when needed, keeping provider auth, moderation, rate limits, and offline preview states separate from iRacing telemetry.
10. Treat overlay builder as a later creator/development platform on top of design-v2 primitives and the bridge schema, not as a prerequisite for the first hand-authored production overlays.
11. Improve historical aggregation and confidence/source tracking as more user sessions are collected.
12. Keep raw capture available for diagnostics, but avoid making it the normal user data path.
13. Expand post-race strategy review/export beyond the first saved-analysis view; see `docs/post-race-strategy-analysis.md`.
14. When summary or other durable history shapes change, add ordered migrations or compatible readers to the historical data maintenance flow, update `HistorySchemaCompatibilityTests`, and keep car/track/session history higher priority than performance/logging data.

## Files Most Likely To Change Next

- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/Styling/OverlayTheme.cs`
- `src/TmrOverlay.Core/`
- `src/TmrOverlay.App/Localhost/`
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
