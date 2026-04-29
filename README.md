# TmrOverlay

`TmrOverlay` starts as a Windows iRacing companion. The current scaffold runs as a tray application, watches for iRacing to come online, analyzes live telemetry, and writes compact per-combo history that future overlays can use for fuel and stint estimates.

## What It Does Today

- Starts as a WinForms tray application with no main window.
- Shows a tiny always-on-top status overlay in the top-left corner.
- Shows a centered tabbed settings window for managing overlay visibility, scale, session filters, shared overlay font, units, copyable Windows local clean/build commands, support/log access, and overlay-specific display options.
- Treats the settings window as the main UI: clicking its `X` exits the application instead of hiding it to the tray.
- Keeps the settings window on the normal desktop layer with a taskbar/Alt+Tab entry, while driving overlays can stay above the sim.
- Includes a placeholder Overlay Bridge settings tab for post-v1.0 bridge controls.
- Lets you drag overlays and remembers each overlay position between app launches.
- Connects to iRacing through the `irsdkSharp` wrapper.
- Starts live telemetry analysis whenever iRacing sends usable frame data.
- Writes compact per-combo session history under app-owned local storage.
- Shows an early fuel calculator overlay that estimates race laps, whole-lap stint targets, final-stint length, realistic fuel-saving alerts, and stop-by-stop tire-change timing guidance.
- Shows first-pass radar and gap-to-leader overlays backed by live `CamCarIdx`, `CarLeftRight`, `CarIdxF2Time`, `CarIdxEstTime`, and `CarIdx*` progress/position telemetry.
  The radar is a transparent circular proximity view that only paints from fresh live telemetry, follows the current camera/focus car when available, colors nearby car rectangles from white to yellow to red as traffic gets closer, labels its time-gap rings, and can show an outer-ring multiclass approaching warning with a live seconds gap. The gap overlay is a four-hour in-class trend graph with the focused car's class leader as the top baseline, adaptive Y-axis scaling, left-side axis labels, lap reference lines, subtle weather bands, driver/leader-change markers, dimmed context lines, and endpoint `P<N>` labels. It keeps bounded in-memory traces for all available same-class timing rows, while dynamically rendering the leader, the focused car, nearby class traffic, and recently visible cars.
- Stores early pit-service history signals such as pit-lane time, pit-stall/service time, observed fuel fill rate, tire/repair indicators, and confidence flags.
- Keeps raw capture as an opt-in diagnostic/development mode; the settings window can request raw capture at runtime if the app was started without the flag.
- When raw capture is enabled, stores `telemetry.bin`, `telemetry-schema.json`, `latest-session.yaml`, optional `session-info/`, and `capture-manifest.json`.
- Shows live-analysis health signals in the overlay, plus disk-write health when raw capture is enabled.
- Writes rolling local logs, JSONL app events, runtime-state markers, persisted settings, lightweight performance snapshots, and diagnostics bundles for triage.
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
- `docs/overlay-logic.md` is the human-readable index for how each overlay derives and displays its state.
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

If the app is already running and you forgot the startup flag, check `Raw capture` in the Collector Status settings tab. That requests raw capture for the current process and starts a raw capture on the next live SDK frame. Active raw captures cannot be disabled mid-collection; the checkbox is locked until the current collection ends.

Each capture folder contains:

- `capture-manifest.json`
- `telemetry-schema.json`
- `telemetry.bin`
- `latest-session.yaml`
- `session-info/`

## Build And Run On Windows

1. Install the .NET 8 SDK or Visual Studio 2022 with .NET desktop development tools.
2. Open `tmrOverlay.sln`.
3. Restore packages.
4. Run `TmrOverlay.App`.
5. Look for the app in the Windows notification area.

You can also double-click [TmrOverlay.cmd](/Users/davidboucher/Code/tmrOverlay/TmrOverlay.cmd) from the repo root after the app has been built once. It launches the built executable from the expected `Debug` or `Release` output folder.

The tray menu lets you open the raw capture folder, open the current raw capture when one exists, open logs, open the settings window, create a diagnostics bundle, or exit the app. Closing the settings window with its `X` also exits the app.
The status overlay stays visible over the sim so you can confirm the app is running and whether live telemetry analysis has started. With raw capture disabled it shows live frame freshness and history-collection state. With raw capture enabled it also shows queued frames, written frames, dropped frames, telemetry file size, disk-write freshness, and explicit warning/error messages. The settings raw-capture checkbox records app events and local logs when toggled or rejected. You can drag overlays to new positions, and each overlay restores its saved frame on restart.

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

The fuel calculator uses live race telemetry first, then exact car/track/session user history only as a fallback while the current session is still sparse. For timed races, it continuously estimates the likely lap count from session time, overall-leader pace/progress, class-leader context, and team-car progress, then converts that into whole-lap stint targets; it does not hard-code a four-hour race length in the production overlay. If completed user/team history shows an 8-lap stint is realistic, future rows can be biased toward that shape, such as `7/8/7/8` for the local Nürburgring development sample. The table also performs strategy analysis across race lengths by comparing a shorter conservative stint rhythm against the longest realistic target, then surfaces extra stops and estimated pit-time loss as a strategy row. Stint rows show target laps and target liters-per-lap, plus tire-change guidance based on historical fill-rate and tire-service timing. As live progress advances, completed stint rows roll off the top of the table while the table keeps its full row layout to avoid view changes during a run.

Completed stint history is stored separately from the active/future fuel table so completed stints can improve future user-specific estimates without continuing to occupy overlay rows. Pit-service history is stored as derived stop summaries plus aggregate metrics, including average tire-change service time, no-tire service time when known, and observed fuel fill rate. Fuel fill rates are only treated as measured when local scalar fuel telemetry is valid; team-driver or inferred values carry confidence flags.

Tracked baseline/sample history may live under `history/baseline/` for development analysis, but production lookup is opt-in. By default the app uses only user-generated history so local development samples do not affect a fresh install.

## Configuration

The app reads `src/TmrOverlay.App/appsettings.json`.

Available settings:

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

By default, writable data resolves under `%LOCALAPPDATA%\TmrOverlay`.

By default, `SessionHistory:UseBaselineHistory` is `false`, so the fuel calculator reads only user-generated history. Enable it only when intentionally testing tracked sample/baseline data:

```powershell
$env:TMR_SessionHistory__UseBaselineHistory = "true"
```

Path settings may be absolute or relative. Relative path settings resolve under the selected app data root.

User-facing overlay preferences are stored in the local settings file under the app settings root. The settings window can update each current overlay's visibility, scale, test/practice/qualifying/race session filters, shared font family, metric/imperial units, and overlay-specific display options. It appears on the normal desktop layer so it can sit behind the sim when the user switches away. The General tab also includes copyable Windows clean, build, publish, and zip commands for local development; it does not execute builds from inside the running app. The clean command clears normal .NET build outputs plus the custom `artifacts/TmrOverlay-win-x64` publish folder so a rebuilt local app or republished tester build cannot silently reuse stale output. The Error Logging tab shows the current app warning/error, opens the local logs and diagnostics folders, shows a lightweight performance summary, and can create/copy a diagnostics bundle for sharing. It also includes a placeholder Overlay Bridge tab for post-v1.0 bridge controls. Settings files are versioned and normalized on load so older local files receive safe defaults as customization expands.

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

Performance diagnostics are always on, even when raw capture is disabled. The app writes periodic JSONL snapshots under `%LOCALAPPDATA%\TmrOverlay\logs\performance` with telemetry throughput, overlay refresh timing, capture writer state when available, process memory, and GC counts.

Bundles include app/storage metadata, telemetry state, lightweight performance snapshots, recent performance logs, runtime state, settings, recent logs/events, latest capture metadata, recent post-race analysis JSON under top-level `analysis/`, and recent user-history summaries/aggregates for car/track/session accuracy checks. They intentionally exclude raw `telemetry.bin` payloads.

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

When overlay behavior changes, update the matching English logic note under [docs/overlay-logic.md](/Users/davidboucher/Code/tmrOverlay/docs/overlay-logic.md) in the same pass so later design tweaks can be made from readable rules instead of code spelunking.

## Next Steps

- Add a replay tool that can decode `telemetry.bin` with `telemetry-schema.json`.
- Harden the radar and gap overlays against longer multi-class traffic captures.
- Expand the post-race strategy review/export flow described in [docs/post-race-strategy-analysis.md](/Users/davidboucher/Code/tmrOverlay/docs/post-race-strategy-analysis.md) and [docs/post-race-analysis-logic.md](/Users/davidboucher/Code/tmrOverlay/docs/post-race-analysis-logic.md).
- Start with update notification before self-update; see [docs/update-strategy.md](/Users/davidboucher/Code/tmrOverlay/docs/update-strategy.md).
