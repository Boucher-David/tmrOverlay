# TmrOverlay

`TmrOverlay` starts as a Windows iRacing companion. The current scaffold runs as a tray application, watches for iRacing to come online, analyzes live telemetry, and writes compact per-combo history that future overlays can use for fuel and stint estimates.

## What It Does Today

- Starts as a WinForms tray application with a fixed-size settings window as the app control surface.
- Uses a local single-instance mutex so a second launch exits instead of attaching another telemetry collector to the same iRacing session.
- Includes an internal collector status overlay for development/support, hidden by default for normal users.
- Shows a branded centered settings window with flat left-side tabs for managing overlay visibility, scale, opacity where useful, session filters, units, support/log access, diagnostic capture, and overlay-specific display options.
- Treats the settings window as the main UI: clicking its `X` exits the application instead of hiding it to the tray.
- Keeps the settings window on the normal desktop layer with a taskbar/Alt+Tab entry, while driving overlays can stay above the sim.
- Lets you drag overlays and remembers each overlay position between app launches.
- Connects to iRacing through the `irsdkSharp` wrapper.
- Starts live telemetry analysis whenever iRacing sends usable frame data.
- Writes compact per-combo session history under app-owned local storage.
- Shows an early fuel calculator overlay that estimates race laps, whole-lap stint targets, final-stint length, realistic fuel-saving alerts, and stop-by-stop tire-change timing guidance.
- Shows first-pass standings, relative, track-map, garage-cover, flags, session/weather, pit-service, input/car-state, radar, and gap-to-leader overlays backed by model-v2 timing/relative/session/weather/fuel-pit/input/spatial/race-event rows plus live `CamCarIdx`, `CarLeftRight`, `CarIdxF2Time`, `CarIdxEstTime`, and `CarIdx*` progress/position telemetry, with a browser-source Stream Chat route that can use either a Streamlabs Chat Box widget URL or public Twitch channel chat.
  The standings overlay renders a compact same-class timing table from `LiveTelemetrySnapshot.Models.Timing`, including leader gap, focus interval, and pit-road status.
  The relative overlay is the first production model-v2 consumer: it renders a dense local/reference relative table from `LiveTelemetrySnapshot.Models.Relative`, configurable stable cars-ahead/behind slots, live or inferred display-time gaps, timing fallback labeling, and quiet source text unless data is waiting or degraded.
  The track-map overlay is a transparent map-only surface: it draws bundled app maps when available, uses local IBT-derived maps while default-on generation remains enabled, otherwise shows a circle fallback with live car dots placed by lap-distance progress. The focused/player marker can show its current `P<N>` position inside the dot, and the Track Map opacity setting adjusts only the map's internal fill while the track outline stays fully opaque.
  The garage-cover overlay is a streamer privacy surface: when enabled, it watches iRacing's Garage-visible state, then shows an opaque user-imported image or black TMR logo fallback over the configured cover frame so setup details are hidden from the stream. The flags overlay is a transparent primary-screen border driven by live session flags; ultrawide defaults keep monitor height with a centered 4:3 frame. Session/weather and pit-service use the shared simple telemetry shell, input/car-state uses graph traces, and all render direct iRacing telemetry quietly. Pit-service is read-only but now includes a red/green release row from pit service completion telemetry, with command-capable pit crew/engineer workflow deferred to a future analysis/operator overlay.
  The radar is a transparent circular proximity view that reads `LiveTelemetrySnapshot.Models.Spatial`, only paints from fresh local in-car telemetry, prefers physical distance from `CarIdxLapDistPct` and track length for close-range placement, falls back to reliable live time gaps when distance is unavailable, uses `CarLeftRight` as the authoritative alongside signal, attaches likely decoded cars to active side warnings instead of drawing the same opponent twice, fades nearby neutral-white car rectangles in between radar entry and the yellow-warning threshold, moves them through yellow toward saturated alert red only inside the close bumper-gap warning buffer, labels its range rings, and can show an outer-ring multiclass warning for cars behind outside the 2-second timing fallback but within 5 seconds. The gap overlay is a four-hour in-class trend graph with the focused car's class leader as the top baseline, adaptive Y-axis scaling, left-side axis labels, lap reference lines, subtle weather bands, driver/leader-change markers, dimmed context lines, and endpoint `P<N>` labels. It keeps bounded in-memory traces for all available same-class timing rows, while dynamically rendering the leader, the focused car, nearby class traffic, and recently visible cars.
- Stores early pit-service history signals such as pit-lane time, pit-stall/service time, observed fuel fill rate, tire/repair indicators, and confidence flags.
- Keeps raw capture as an opt-in diagnostic/development mode; the settings window can request raw capture at runtime if the app was started without the flag.
- When raw capture is enabled, stores `telemetry.bin`, `telemetry-schema.json`, `latest-session.yaml`, optional `session-info/`, and `capture-manifest.json`, then writes compact `capture-synthesis.json` plus optional advanced sidecars such as `live-model-parity.json`, `live-overlay-diagnostics.json`, and `ibt-analysis/*.json` when those collectors are enabled. Successful IBT analysis can also generate a local reusable track-map JSON under app-owned storage unless the user opts out on the Track Map settings tab.
- Keeps edge-case telemetry, model-v2 parity, live-overlay diagnostics, and post-race analysis disabled by default for tester builds; the Support tab shows their status as advanced collection tools.
- Shows live-analysis health signals in the overlay, plus disk-write health when raw capture is enabled.
- Writes rolling local logs, JSONL app events, runtime-state markers, persisted settings, lightweight performance snapshots, and diagnostics bundles for triage.
- Includes retention cleanup for old captures and diagnostics bundles.
- Includes a replay-mode seam for overlay development against an existing capture.

## Project Layout

- `src/TmrOverlay.App/` contains the Windows application, tray shell, and telemetry collector.
- `src/TmrOverlay.App/Overlays/` contains overlay modules. Each overlay type gets its own folder and uses shared draggable/persisted window behavior.
- `src/TmrOverlay.App/Overlays/Styling/OverlayTheme.cs` contains the common Windows overlay visual tokens, including shared colors, font helpers, and basic layout constants.
- `src/TmrOverlay.App/Localhost/` contains the optional localhost browser-source overlays for OBS and other local capture tools.
- `src/TmrOverlay.App/Shell/` contains the tray/menu shell.
- `src/TmrOverlay.Core/` contains platform-neutral settings models, overlay metadata, historical telemetry/session models, live telemetry derivation, post-race analysis models, and fuel strategy logic.
- `assets/` contains repo-owned source visual assets such as brand/logo images for future app icons, overlay branding, docs, and publishing work.
- `src/TmrOverlay.App/Storage/` contains app-owned local storage path resolution.
- `src/TmrOverlay.App/History/` contains compact session-history disk storage and lookup services.
- `src/TmrOverlay.App/Logging/`, `Events/`, `Runtime/`, `Diagnostics/`, `Retention/`, `Replay/`, `Analysis/`, and `Settings/` contain Windows app boilerplate and persistence services.
- `history/baseline/` contains tracked development/sample historical summaries. The app does not read these by default.
- `tests/TmrOverlay.App.Tests/` contains the xUnit test project for non-UI logic.
- `local-mac/TmrOverlayMac/` is the ignored local macOS harness. It mirrors the Windows structure for overlay iteration but uses mock telemetry.
- `docs/capture-format.md` documents the binary frame format used by `telemetry.bin`.
- `docs/edge-case-telemetry-logic.md` documents the compact edge-case detector/report rules.
- `docs/history-data-evolution.md` documents how future app versions should migrate or rebuild user history written by older versions.
- `docs/ibt-analysis.md` documents the compact IBT sidecar investigation path.
- `docs/overlay-logic.md` is the human-readable index for how each overlay derives and displays its state.
- `docs/repo-surface.md` separates customer/tester-facing material, product source, internal development assets, and ignored local runtime data.
- `docs/windows-release.md` documents the portable Windows tester release, checksum, install, upgrade, rollback, and diagnostics flow.
- `VERSION.md` records milestone tag meanings, corrected squash titles, and release-note summaries.
- `Directory.Build.props` holds shared .NET product/version metadata for the app and core assemblies.
- `telemetry.md` summarizes the event/session/car schema exposed by the current raw capture model.

## Raw Capture Output

Raw capture is disabled by default. Enable it only when you need a diagnostic/development capture:

```powershell
$env:TMR_TelemetryCapture__RawCaptureEnabled = "true"
```

When enabled, captures are written under the user-local application data directory:

```text
%LOCALAPPDATA%\TmrOverlay\captures
```

For development, set `TMR_Storage__UseRepositoryLocalStorage=true` to write under this checkout instead.

If the app is already running and you forgot the startup flag, check `Capture diagnostic telemetry` in the Support settings tab. That requests raw capture for the current process and starts a raw capture on the next live SDK frame. Active raw captures cannot be disabled mid-collection; the checkbox is locked until the current collection ends.

Each capture folder contains:

- `capture-manifest.json`
- `telemetry-schema.json`
- `telemetry.bin`
- `latest-session.yaml`
- `session-info/`
- `capture-synthesis.json` after post-session synthesis succeeds
- `live-model-parity.json` when model-v2 parity collection is enabled
- `live-overlay-diagnostics.json` when passive overlay-assumption diagnostics are enabled
- `ibt-analysis/*.json` when IBT analysis is enabled and a matching iRacing `.ibt` file can be selected or skipped/failure status is recorded

Model-v2 parity runs in observer mode for the legacy overlay-input families while Relative consumes `LiveTelemetrySnapshot.Models.Relative` directly and Car Radar consumes `LiveTelemetrySnapshot.Models.Spatial` for local in-car radar. When enabled, the collector compares existing fuel/proximity/gap inputs with `LiveTelemetrySnapshot.Models` and records compact mismatch/coverage evidence. `LiveTelemetrySnapshot.Models` also carries source evidence for timing, spatial/radar placement, gap, and fuel-baseline usability so future overlays can distinguish reliable raw signals from diagnostic or partial signals. `live-model-parity.json` includes `promotionReadiness`; when a session passes the configured data-volume, mismatch-rate, and coverage thresholds, the app also writes a `live_model_v2_promotion_candidate` event. `live-overlay-diagnostics.json` is a separate observer artifact for the 24-hour findings and design-v2 candidates: non-race gap semantics, multi-lap gap scaling, local-only radar suppression and side/placement evidence, fuel source stitching, position cadence, lap-delta channel availability, and derived sector-timing coverage. IBT logging uses the same raw-capture switch by default. Starting raw capture requests iRacing telemetry logging, and capture finalization requests logging to stop. The post-session analyzer writes compact JSON sidecars only, including `ibt-local-car-summary.json` for bounded local-car trajectory/fuel/vehicle-dynamics investigation, enforces a timeout for capture synthesis, scans a bounded set of recent `.ibt` candidates, enforces size/stability/sample/time limits, can derive compact local track-map geometry from complete positive laps, and does not copy source `.ibt` files into the capture directory unless `IbtAnalysis:CopyIbtIntoCaptureDirectory=true`.

## Edge-Case Telemetry Artifacts

Edge-case telemetry capture is disabled by default and is separate from raw capture. When enabled, it watches normalized live state plus a small set of scalar raw telemetry channels, then writes bounded JSON clips under:

```text
%LOCALAPPDATA%\TmrOverlay\logs\edge-cases
```

Each `*-edge-cases.json` file includes the watched raw schema, missing watched variables, clip triggers, a short pre-trigger window, a short post-trigger window, dropped-observation counts when the clip cap is reached, a final sampled context tail from the end of the session, selected nearby/class timing rows, and raw watch values for channels such as fuel, tires, suspension, brakes, wheel speed, pit service, weather, engine warnings, replay state, incidents, frame rate, and network latency. It intentionally does not include `telemetry.bin`.

## Live Overlay Diagnostics

Live overlay diagnostics are disabled by default and are separate from raw capture. When enabled without raw capture, they are written under:

```text
%LOCALAPPDATA%\TmrOverlay\logs\overlay-diagnostics
```

Each `*-live-overlay-diagnostics.json` file summarizes current-overlay and design-v2 candidate assumptions observed from normalized live snapshots: gap source/session semantics, large gap and jump examples, local-only radar suppression, radar side/focus/placement evidence, fuel level/burn/source evidence, sampled intra-lap position/class-position changes, live lap-delta channel availability, and derived sector-timing coverage from sector metadata plus car progress. Event examples are duplicate-suppressed and capped per kind so one stable condition cannot fill the whole sample budget. The mac harness mirrors this for mock/demo overlay runs.

## Build And Run On Windows

1. Install the .NET 8 SDK or Visual Studio 2022 with .NET desktop development tools.
2. Open `tmrOverlay.sln`.
3. Restore packages.
4. Run `TmrOverlay.App`.
5. Look for the app in the Windows notification area.

You can also double-click [TmrOverlay.cmd](/Users/davidboucher/Code/tmrOverlay/TmrOverlay.cmd) from the repo root after the app has been built once. It launches the built executable from the expected `Debug` or `Release` output folder.

The tray menu lets you open the raw capture folder, open the current raw capture when one exists, open logs, open the settings window, create a diagnostics bundle, or exit the app. Closing the settings window with its `X` also exits the app.
The settings window is the app control surface. Driving overlays default hidden, can be enabled from their settings tabs, and restore their saved frame on restart. Internal collector status is available for development/support but is not part of the normal end-user settings tab list. The Support tab is task-oriented: app status, current issue, diagnostics bundle actions, diagnostic telemetry capture, storage shortcuts, compact app activity, and discoverable advanced collection status.

During local development, the overlay also warns when source files in this checkout are newer than the running build. That is a rebuild reminder only; it does not block capture.

Future overlays should consume shared live data through `TmrOverlay.Core.Telemetry.Live.ILiveTelemetrySource` and user-history lookups from `SessionHistoryQueryService`; they should not read directly from iRacing or raw capture files. Live/replay providers write through `ILiveTelemetrySink`. Prefer changing shared overlay colors and basic typography through `OverlayTheme` or the optional `overlay-theme.json` file instead of editing one-off color values in each form.

## Windows Tester Releases

Release tags named `vMAJOR.MINOR.PATCH` publish a portable Windows x64 tester build through GitHub Actions. PR validation restores, builds, tests, validates tracked screenshot artifacts, and runs a self-contained publish dry run with the same package-content audit used by release packaging. The release workflow then publishes the WinForms app as a self-contained single-file package, audits the publish folder for accidental repo/dev-folder leaks, writes a package manifest, zips the publish folder, writes a SHA-256 checksum, uploads the release artifacts, and attaches them to the GitHub Release.

See [docs/windows-release.md](/Users/davidboucher/Code/tmrOverlay/docs/windows-release.md) for package contents, tester download, checksum verification, install, user-data compatibility, upgrade, rollback, signing, and diagnostics instructions. Current tester releases are unsigned portable zip builds; installer-based self-update remains deferred until the release channel and signing plan are settled.

See [docs/track-map-overlay-logic.md](/Users/davidboucher/Code/tmrOverlay/docs/track-map-overlay-logic.md) for the Windows track-map generation validation flow and the batch command for creating bundled derived map JSON assets from vetted IBT files.

## macOS Local Harness

The ignored macOS harness is for local overlay and boilerplate iteration on this machine:

```bash
./run.sh
```

It writes mock session history to `~/Library/Application Support/TmrOverlayMac/history/user` by default and mirrors the Windows storage layout for captures, user history, logs, events, settings, diagnostics, runtime state, and retention cleanup. Set `TMR_MAC_USE_REPOSITORY_LOCAL_STORAGE=true` if you intentionally want mac harness data under the ignored `local-mac/TmrOverlayMac/` folder.

The mac harness mirrors the current Windows overlay review set for local v0.11 iteration: status, standings, fuel calculator, relative, track map, garage cover, flags, session/weather, pit-service, input/car-state, radar, and gap-to-leader. Its mock live race uses the tracked four-hour Nürburgring baseline shape at 4x speed so long-run relative, standings, gap, fuel, and simple telemetry behavior can be inspected quickly. Treat Windows overlay code as production-facing and real-data-driven; the ignored mac harness can use looser mock offsets, named drivers, synthetic weather windows, and exaggerated events for visual iteration.

Raw mock capture is disabled by default. Enable it only when you want to exercise the raw capture writer and disk-health UI:

```bash
TMR_MAC_RAW_CAPTURE_ENABLED=true ./run.sh
```

For overlay-state previews on macOS, launch with:

```bash
TMR_MAC_DEMO_STATES=true ./run.sh
```

That cycles through waiting-for-sim, connected-without-capture, healthy live-analysis, healthy raw-capture, stale build, dropped-frame, frames-not-written, disk-stalled, and writer-error states. The menu-bar item also exposes manual demo-state entries.

## Session History

At the end of each live telemetry collection, the app writes a compact historical summary under:

```text
%LOCALAPPDATA%\TmrOverlay\history\user\cars\{car}\tracks\{track}\sessions\{session}
```

That data is intentionally much smaller than raw telemetry. It is meant to support future startup estimates for fuel usage, lap time, stint length, and pit behavior for a known car/track/session combo before the current live session has enough data.

The fuel calculator uses live race telemetry first, then exact car/track/session user history only as a fallback while the current session is still sparse. For timed races, it continuously estimates the likely lap count from session time, overall-leader pace/progress, class-leader context, and team-car progress, then converts that into whole-lap stint targets; it does not hard-code a four-hour race length in the production overlay. If completed user/team history shows an 8-lap stint is realistic, future rows can be biased toward that shape, such as `7/8/7/8` for the local Nürburgring development sample. The table also performs strategy analysis across race lengths by comparing a shorter conservative stint rhythm against the longest realistic target, then surfaces extra stops and estimated pit-time loss as a strategy row. Stint rows show target laps and target liters-per-lap, plus tire-change guidance based on historical fill-rate and tire-service timing. As live progress advances, completed stint rows roll off the top of the table while the table keeps its full row layout to avoid view changes during a run.

Completed stint history is stored separately from the active/future fuel table so completed stints can improve future user-specific estimates without continuing to occupy overlay rows. Pit-service history is stored as derived stop summaries plus aggregate metrics, including average tire-change service time, no-tire service time when known, and observed fuel fill rate. Fuel fill rates are only treated as measured when local scalar fuel telemetry is valid; team-driver or inferred values carry confidence flags.

On startup, session-history maintenance normalizes compatible legacy summary metadata, rebuilds `aggregate.json` from compatible per-session summaries, skips incompatible/corrupt summaries with a manifest entry, and keeps backups for rewritten summary files. This prioritizes car/track/session history that improves future overlay estimates over low-value operational logs.

Tracked baseline/sample history may live under `history/baseline/` for development analysis, but production lookup is opt-in. By default the app uses only user-generated history so local development samples do not affect a fresh install.

## Configuration

The app reads `src/TmrOverlay.App/appsettings.json`.

Available settings:

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

By default, writable data resolves under `%LOCALAPPDATA%\TmrOverlay`.

By default, `SessionHistory:UseBaselineHistory` is `false`, so the fuel calculator reads only user-generated history. Enable it only when intentionally testing tracked sample/baseline data:

```powershell
$env:TMR_SessionHistory__UseBaselineHistory = "true"
```

Path settings may be absolute or relative. Relative path settings resolve under the selected app data root.

User-facing overlay preferences are stored in the local settings file under the app settings root. The branded fixed-size settings window can update each current user-facing overlay's visibility, scale when applicable, test/practice/qualifying/race session filters, metric/imperial units, and overlay-specific display options. It appears on the normal desktop layer so it can sit behind the sim when the user switches away. The General tab owns shared app preferences. The Support tab is last and presents support tasks first: app status, current issue, diagnostics bundle actions, diagnostic telemetry capture, storage shortcuts, advanced collection status, and compact app activity. Settings files are versioned and normalized on load so older local files receive safe defaults as customization expands.

### Overlay Theme Overrides

Advanced visual tokens can be overridden with an optional `overlay-theme.json` file in the app settings root, for example:

```json
{
  "defaultFontFamily": "Segoe UI",
  "colors": {
    "windowBackground": "#0E1215",
    "textPrimary": "#FFFFFF",
    "warningText": "#F6B858"
  }
}
```

Color keys match `OverlayTheme.Colors` property names and can be written as camelCase, kebab-case, snake_case, or PascalCase. Colors accept `#RRGGBB` or `#AARRGGBB`.

### Localhost Browser Overlays

The optional localhost overlay server is disabled by default. Enable it when an overlay should be consumed as a local OBS/browser source instead of, or in addition to, the native desktop overlay:

```powershell
$env:TMR_LocalhostOverlays__Enabled = "true"
$env:TMR_LocalhostOverlays__Port = "8765"
```

When enabled, it listens on `http://localhost:{port}/`, serves `GET /health`, `GET /snapshot`, `GET /api/snapshot`, and per-overlay HTML routes for OBS browser sources:

- `/overlays/standings`
- `/overlays/relative`
- `/overlays/fuel-calculator`
- `/overlays/session-weather`
- `/overlays/pit-service`
- `/overlays/input-state`
- `/overlays/car-radar`
- `/overlays/gap-to-leader`
- `/overlays/track-map`
- `/overlays/stream-chat`

The localhost pages poll normalized `ILiveTelemetrySource` snapshots and do not talk to iRacing directly. Track Map fetches stored map geometry from `GET /api/track-map` separately from live snapshots. Flags intentionally has no browser-source route yet while the native overlay is still a simple border box. Stream Chat reads one selected source at a time from the Stream Chat settings tab: either a Streamlabs Chat Box widget URL embedded in the browser source, or public Twitch channel chat by channel name. Provider auth, moderation, and write/chat-command support are intentionally separate follow-up work. Streamlabs widget URLs are treated as private local settings and are redacted from diagnostics bundles. Overlay Bridge is reserved for a future teammate-to-teammate data-sharing boundary, not this local OBS/browser-source feature.

For repo-local development storage, set:

```powershell
$env:TMR_Storage__UseRepositoryLocalStorage = "true"
```

Environment overrides use the `TMR_` prefix, for example `TMR_Storage__CaptureRoot` or `TMR_TelemetryEdgeCases__Enabled=false`.

## Logs

The live app writes rolling local debug logs under:

```text
%LOCALAPPDATA%\TmrOverlay\logs
```

Logs include startup/shutdown markers, storage paths, telemetry service messages, and unhandled exceptions. JSONL app-event breadcrumbs are written under `%LOCALAPPDATA%\TmrOverlay\logs\events`.

## Diagnostics

The tray menu can create a diagnostics bundle under:

```text
%LOCALAPPDATA%\TmrOverlay\diagnostics
```

Performance diagnostics are always on, even when raw capture is disabled. The app writes periodic JSONL snapshots under `%LOCALAPPDATA%\TmrOverlay\logs\performance` with telemetry throughput, iRacing network/system values such as channel quality, latency, frame rate, CPU/GPU use, replay/on-track state, overlay refresh timing, overlay update-decision counters, capture writer state when available, process memory, and GC counts.

Bundles include app/storage metadata, telemetry state, lightweight performance snapshots, recent performance logs, runtime state, settings, recent logs/events, latest capture metadata and compact sidecars, recent user-history summaries/aggregates for car/track/session accuracy checks, and advanced collection artifacts such as edge-case telemetry, model-v2 parity, overlay diagnostics, or post-race analysis when those files exist. They intentionally exclude raw `telemetry.bin` and source `.ibt` payloads.

See `docs/update-strategy.md` for the current update notification and self-update plan.

## Tests

The Windows solution includes an xUnit test project at `tests/TmrOverlay.App.Tests/`.

```powershell
dotnet test
```

GitHub Actions runs the Windows .NET build and test gate on `windows-latest` through `.github/workflows/windows-dotnet.yml`.

The local mac harness includes a SwiftPM XCTest target under `local-mac/TmrOverlayMac/Tests/`.

```bash
cd local-mac/TmrOverlayMac
swift test
```

The current Command Line Tools-only setup on this Mac can run `swift build`, but `swift test` requires an XCTest-capable Xcode toolchain.

Generated overlay screenshots are also part of review. From the mac harness:

```bash
cd local-mac/TmrOverlayMac
swift run TmrOverlayMacScreenshots
cd ../..
python3 tools/validate_overlay_screenshots.py
```

That keeps the contact sheets and per-state PNGs under `mocks/` current and catches missing, wrong-size, or blank artifacts before visual review.

Windows-rendered screenshots are generated by CI from the real WinForms forms using deterministic telemetry fixtures:

```powershell
dotnet run --project .\tools\TmrOverlay.WindowsScreenshots\TmrOverlay.WindowsScreenshots.csproj --configuration Release -- artifacts\windows-overlay-screenshots
python tools/validate_overlay_screenshots.py --profile windows-ci --root artifacts/windows-overlay-screenshots
```

Those Windows PNGs are uploaded as workflow artifacts for parity review instead of being committed under `mocks/`.

The teammate-facing Windows install/support tutorial image is generated separately under `docs/assets/`:

```bash
swift tools/render_release_tutorial.swift
python3 tools/validate_overlay_screenshots.py --profile release-tutorial --root docs/assets
```

When overlay behavior changes, update the matching English logic note under [docs/overlay-logic.md](/Users/davidboucher/Code/tmrOverlay/docs/overlay-logic.md) in the same pass so later design tweaks can be made from readable rules instead of code spelunking.

## Next Steps

- Add a replay tool that can decode `telemetry.bin` with `telemetry-schema.json`.
- Harden the radar and gap overlays against longer multi-class traffic captures.
- Expand the post-race strategy review/export flow described in [docs/post-race-strategy-analysis.md](/Users/davidboucher/Code/tmrOverlay/docs/post-race-strategy-analysis.md) and [docs/post-race-analysis-logic.md](/Users/davidboucher/Code/tmrOverlay/docs/post-race-analysis-logic.md).
- Start with update notification before self-update; see [docs/update-strategy.md](/Users/davidboucher/Code/tmrOverlay/docs/update-strategy.md).
