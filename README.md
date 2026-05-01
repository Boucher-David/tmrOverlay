# TmrOverlay

`TmrOverlay` starts as a Windows iRacing companion. The current scaffold runs as a tray application, watches for iRacing to come online, analyzes live telemetry, and writes compact per-combo history that future overlays can use for fuel and stint estimates.

## What It Does Today

- Starts as a WinForms tray application with no main window.
- Shows a tiny always-on-top status overlay in the top-left corner.
- Shows a centered tabbed settings overlay for managing overlay visibility, scale, session filters, shared overlay font, units, and overlay-specific display options.
- Includes a placeholder Overlay Bridge settings tab for post-v1.0 bridge controls.
- Lets you drag overlays and remembers each overlay position between app launches.
- Connects to iRacing through the `irsdkSharp` wrapper.
- Starts live telemetry analysis whenever iRacing sends usable frame data.
- Can request iRacing `.ibt` telemetry logging from the same capture control as raw capture and writes compact IBT investigation sidecars after sessions when a matching file is available.
- Writes compact per-combo session history under app-owned local storage.
- Stitches reconnect/rejoin segments from the same iRacing session into one historical session group while keeping each raw capture immutable.
- Shows an early fuel calculator overlay that estimates race laps, whole-lap stint targets, final-stint length, realistic fuel-saving alerts, and stop-by-stop tire-change timing guidance.
- Shows first-pass radar and gap-to-leader overlays backed by live `CarLeftRight`, `CarIdxF2Time`, `CarIdxEstTime`, and `CarIdx*` progress/position telemetry.
  The radar is a transparent circular proximity view that only paints when traffic is nearby, fades car rectangles from red to yellow to transparent as traffic moves away, and can show an outer-ring multiclass approaching warning with a live seconds gap. The gap overlay is a four-hour in-class trend graph with the class leader as the top baseline, adaptive Y-axis scaling, left-side axis labels, lap reference lines, subtle weather bands, driver/leader-change markers, dimmed non-team context lines, and endpoint `P<N>` labels. It keeps bounded in-memory traces for all available same-class timing rows, while dynamically rendering the leader, the team car, nearby class traffic, and recently visible cars.
- Stores early pit-service history signals such as pit-lane time, pit-stall/service time, observed fuel fill rate, tire/repair indicators, and confidence flags.
- Keeps raw capture as an opt-in diagnostic/development mode; the status overlay can start or deliberately stop raw capture at runtime while live analysis/history collection continues.
- When raw capture is enabled, stores `telemetry.bin`, `telemetry-schema.json`, `latest-session.yaml`, optional `session-info/`, and `capture-manifest.json`.
- Writes optional `ibt-analysis/` sidecars beside capture artifacts without copying the source `.ibt` by default.
- Includes `tools/analysis/synthesize_capture.py` for turning a large raw capture into a GitHub-friendly all-telemetry JSON synthesis with a focused weather/rain/radar-candidate section.
- Shows live-analysis health signals in the overlay, plus disk-write health while raw capture is active.
- Writes rolling local logs, versioned JSONL app events with `appRunId` / `collectionId` correlation, runtime-state markers, persisted settings, and diagnostics bundles for triage.
- Includes retention cleanup for old captures and diagnostics bundles.
- Includes a replay-mode seam for overlay development against an existing capture.

## Project Layout

- `src/TmrOverlay.App/` contains the Windows application, tray shell, and telemetry collector.
- `src/TmrOverlay.App/Overlays/` contains overlay modules. Each overlay type gets its own folder and uses shared draggable/persisted window behavior.
- `src/TmrOverlay.App/Overlays/Styling/OverlayTheme.cs` contains the common Windows overlay visual tokens, including shared colors, font helpers, and basic layout constants.
- `src/TmrOverlay.App/Bridge/` contains the optional localhost overlay bridge for future external UI clients.
- `src/TmrOverlay.App/Shell/` contains the tray/menu shell.
- `src/TmrOverlay.Core/` contains platform-neutral settings models, overlay metadata, historical telemetry/session models, live telemetry derivation, post-race analysis models, and fuel strategy logic.
- `src/TmrOverlay.App/Storage/` contains app-owned local storage path resolution.
- `src/TmrOverlay.App/History/` contains compact session-history disk storage and lookup services.
- `src/TmrOverlay.App/Logging/`, `Events/`, `Runtime/`, `Diagnostics/`, `Retention/`, `Replay/`, `Analysis/`, and `Settings/` contain Windows app boilerplate and persistence services.
- `history/baseline/` contains tracked development/sample historical summaries. The app does not read these by default.
- `tests/TmrOverlay.App.Tests/` contains the xUnit test project for non-UI logic.
- `local-mac/TmrOverlayMac/` is the ignored local macOS harness. It mirrors the Windows structure for overlay iteration but uses mock telemetry.
- `docs/capture-format.md` documents the binary frame format used by `telemetry.bin`.
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

If the app is already running and you forgot the startup flag, use the `Capture` button on the Collector Status overlay. That requests capture artifacts for the current process and starts a raw segment on the next live SDK frame. When `IbtAnalysis:TelemetryLoggingEnabled` is true, the same raw segment also asks iRacing to start an `.ibt` log. Press `Stop capture` to close the raw writer and request IBT logging stop; live telemetry analysis, compact history, and post-race analysis continue.

Each capture folder contains:

- `capture-manifest.json`
- `telemetry-schema.json`
- `telemetry.bin`
- `latest-session.yaml`
- `session-info/`

For sharing capture evidence without uploading `telemetry.bin`, synthesize the capture:

```powershell
python tools\analysis\synthesize_capture.py --capture .\captures\capture-YYYYMMDD-HHMMSS-mmm --output .\capture-synthesis.json
```

The synthesis summarizes every telemetry variable and defaults to a 24 MiB output budget so it stays below GitHub's browser upload cap. The standalone tool and app-side synthesis auto-stride large captures to keep CPU/output bounded, while `--sample-stride 1` remains available when every frame is required. The app defers synthesis while iRacing is still connected or a known iRacing sim process is still running, then starts as soon as the sim closes; if the app itself is shutting down while iRacing is still active, synthesis is skipped rather than blocking exit. On startup, the app scans for raw capture folders that still have no stable `capture-synthesis.json` and queues them for the same guarded synthesis path. The standalone tool refuses on Windows unless `--allow-while-iracing-running` is passed intentionally. The app writes a stable `capture-synthesis.json` plus a context-named copy when session/car/track metadata is available. It also records raw-capture write timing and synthesis process CPU metrics in status snapshots, app events, diagnostics bundles, and newer capture manifests. Use a larger `--sample-stride` or lower `--max-timeline-events` if a future capture grows beyond that.

Diagnostics bundles include explicit degradation codes for design/analysis work, such as spectated timing only, idle local scalars, missing local driving fuel scalars, focus-car changes, side-callout availability, fuel-model availability, and weather wetness mismatches. These codes are meant to separate expected telemetry gaps from real failures.

## IBT Investigation Output

IBT analysis is enabled by default for Windows. IBT logging is allowed by default, but it follows the same capture-artifacts switch as raw capture: the startup raw-capture flag or the Collector Status `Capture` button starts both the raw writer and iRacing's `.ibt` logging request. After the session ends and iRacing has closed, the app looks for a matching `.ibt` under:

```text
%USERPROFILE%\Documents\iRacing\telemetry
```

When a match is found, compact derived files are written under the capture directory:

```text
ibt-analysis/status.json
ibt-analysis/ibt-schema-summary.json
ibt-analysis/ibt-vs-live-schema.json
ibt-analysis/ibt-field-summary.json
```

The source `.ibt` is not copied by default. Missing roots or candidates, oversized files, active files, parser failures, and timeouts are recorded in `status.json` and app events instead of failing compact history, post-race analysis, or capture synthesis. See [IBT Analysis](/Users/davidboucher/Code/tmrOverlay/docs/ibt-analysis.md) for details.

## Build And Run On Windows

1. Install the .NET 8 SDK or Visual Studio 2022 with .NET desktop development tools.
2. Open `tmrOverlay.sln`.
3. Restore packages.
4. Run `TmrOverlay.App`.
5. Look for the app in the Windows notification area.

You can also double-click [TmrOverlay.cmd](/Users/davidboucher/Code/tmrOverlay/TmrOverlay.cmd) from the repo root after the app has been built once. It launches the built executable from the expected `Debug` or `Release` output folder.
For copyable PowerShell build, test, run, publish, and zip commands, see [build.md](/Users/davidboucher/Code/tmrOverlay/build.md) or [Windows .NET Commands](/Users/davidboucher/Code/tmrOverlay/docs/windows-dotnet-commands.md).

The tray menu lets you open the raw capture folder, open the current raw capture when one exists, open logs, open the settings overlay, create a diagnostics bundle, or exit the app.
The status overlay stays visible over the sim so you can confirm the app is running and whether live telemetry analysis has started. With capture artifacts disabled it shows live frame freshness, session-history activity, and end-of-session summary saves. With capture artifacts enabled it also shows queued frames, written frames, dropped frames, telemetry file size, disk-write freshness, write latency, and explicit warning/error messages. The status overlay `Capture` button records app events and local logs when raw/IBT artifact capture is armed, stopped, or cancelled. You can drag overlays to new positions, and each overlay restores its saved frame on restart. Use the tray menu to reopen settings or exit the application.

During local development, the overlay also warns when source files in this checkout are newer than the running build. That is a rebuild reminder only; it does not block capture.

Future overlays should consume shared live data through `TmrOverlay.Core.Telemetry.Live.ILiveTelemetrySource` and user-history lookups from `SessionHistoryQueryService`; they should not read directly from iRacing or raw capture files. Live/replay providers write through `ILiveTelemetrySink`. Prefer changing shared overlay colors and basic typography through `OverlayTheme` or the optional `overlay-theme.json` file instead of editing one-off color values in each form.

## macOS Local Harness

The ignored macOS harness is for local overlay and boilerplate iteration on this machine:

```bash
./run.sh
```

It writes mock session history to `~/Library/Application Support/TmrOverlayMac/history/user` by default and mirrors the Windows storage layout for captures, user history, logs, events, settings, diagnostics, runtime state, and retention cleanup. Set `TMR_MAC_USE_REPOSITORY_LOCAL_STORAGE=true` if you intentionally want mac harness data under the ignored `local-mac/TmrOverlayMac/` folder.

The mac harness opens the same startup overlay set as Windows: status, fuel calculator, radar, and gap-to-leader. Its mock live race uses the tracked four-hour Nürburgring baseline shape at 4x speed so long-run gap and fuel behavior can be inspected quickly. Treat Windows overlay code as production-facing and real-data-driven; the ignored mac harness can use looser mock offsets, named drivers, synthetic weather windows, and exaggerated events for visual iteration.

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

Each finalized collection still writes its own compact summary. When iRacing exposes a stable `SubSessionID` or `SessionID`, those summaries are also grouped under `session-groups/` so quitting/rejoining the same race or restarting after an app crash can update one historical session and one post-race analysis instead of creating separate race records. Raw capture folders are not merged or rewritten; grouping happens only in derived history and analysis metadata. Segment records include the capture source id, end reason, reconnect gap, and previous app runtime state when the previous run was not clean.

The fuel calculator uses live race telemetry first, then exact car/track/session user history only as a fallback while the current session is still sparse. For timed races, it continuously estimates the likely lap count from session time, overall-leader pace/progress, class-leader context, and team-car progress, then converts that into whole-lap stint targets. If completed user/team history shows an 8-lap stint is realistic, future rows can be biased toward that shape, such as `7/8/7/8` for the local Nürburgring development sample. The table also performs strategy analysis across race lengths by comparing a shorter conservative stint rhythm against the longest realistic target, then surfaces extra stops and estimated pit-time loss as a strategy row. Stint rows show target laps and target liters-per-lap, plus tire-change guidance based on historical fill-rate and tire-service timing. As live progress advances, completed stint rows roll off the top of the table. If no fuel stop is needed, the table collapses to a single `Stint 1` row that says no fuel stop is needed.

Completed stint history is stored separately from the active/future fuel table so completed stints can improve future user-specific estimates without continuing to occupy overlay rows. Current-session observed stints for focused/team cars live in the shared live telemetry snapshot so fuel, gap, radar, diagnostics, and future relative/standings overlays can reason from the same car-session context. Pit-service history is stored as derived stop summaries plus aggregate metrics, including average tire-change service time, no-tire service time when known, and observed fuel fill rate. Fuel fill rates are only treated as measured when local scalar fuel telemetry is valid; team-driver or inferred values carry confidence flags.

Tracked baseline/sample history may live under `history/baseline/` for development analysis, but production lookup is opt-in. By default the app uses only user-generated history so local development samples do not affect a fresh install.

## Configuration

The app reads `src/TmrOverlay.App/appsettings.json`.

Available settings:

- `TelemetryCapture:StoreSessionInfoSnapshots`
- `TelemetryCapture:RawCaptureEnabled`
- `TelemetryCapture:QueueCapacity`
- `IbtAnalysis:Enabled`
- `IbtAnalysis:TelemetryLoggingEnabled`
- `IbtAnalysis:TelemetryRoot`
- `IbtAnalysis:MaxCandidateAgeMinutes`
- `IbtAnalysis:MaxCandidateBytes`
- `IbtAnalysis:MaxAnalysisMilliseconds`
- `IbtAnalysis:MaxSampledRecords`
- `IbtAnalysis:MinStableAgeSeconds`
- `IbtAnalysis:MaxCandidateFiles`
- `IbtAnalysis:CopyIbtIntoCaptureDirectory`
- `IbtAnalysis:OutputDirectoryName`
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

By default, writable data resolves under `%LOCALAPPDATA%\TmrOverlay`.

By default, `SessionHistory:UseBaselineHistory` is `false`, so the fuel calculator reads only user-generated history. Enable it only when intentionally testing tracked sample/baseline data:

```powershell
$env:TMR_SessionHistory__UseBaselineHistory = "true"
```

Path settings may be absolute or relative. Relative path settings resolve under the selected app data root.

User-facing overlay preferences are stored in the local settings file under the app settings root. The settings overlay can update each current overlay's visibility, scale, test/practice/qualifying/race session filters, shared font family, metric/imperial units, and overlay-specific display options. It also includes a placeholder Overlay Bridge tab for post-v1.0 bridge controls. Settings files are versioned and normalized on load so older local files receive safe defaults as customization expands.

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

### Local Overlay Bridge

The optional bridge is disabled by default. Enable it only for local development or future external overlay clients:

```powershell
$env:TMR_OverlayBridge__Enabled = "true"
$env:TMR_OverlayBridge__Port = "8765"
```

When enabled, it listens on `http://localhost:{port}/` and serves `GET /health` plus `GET /snapshot`. The snapshot is sourced from `ILiveTelemetrySource`, so bridge clients receive normalized app state rather than direct iRacing SDK data.

For repo-local development storage, set:

```powershell
$env:TMR_Storage__UseRepositoryLocalStorage = "true"
```

Environment overrides use the `TMR_` prefix, for example `TMR_Storage__CaptureRoot`.

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

Bundles include app/storage metadata, runtime state, live telemetry/overlay summaries, telemetry availability counters, settings, recent logs/events, latest capture metadata, and compact IBT analysis sidecars when available. They intentionally exclude raw `telemetry.bin` payloads and source `.ibt` files.

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

## Next Steps

- Add a replay tool that can decode `telemetry.bin` with `telemetry-schema.json`.
- Harden the radar and gap overlays against longer multi-class traffic captures.
- Design the post-race strategy review/export flow described in [docs/post-race-strategy-analysis.md](/Users/davidboucher/Code/tmrOverlay/docs/post-race-strategy-analysis.md).
