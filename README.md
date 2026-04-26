# TmrOverlay

`TmrOverlay` starts as a Windows iRacing companion. The current scaffold runs as a tray application, watches for iRacing to come online, analyzes live telemetry, and writes compact per-combo history that future overlays can use for fuel and stint estimates.

## What It Does Today

- Starts as a WinForms tray application with no main window.
- Shows a tiny always-on-top status overlay in the top-left corner.
- Lets you drag the status overlay and close the whole app from its `X` button.
- Connects to iRacing through the `irsdkSharp` wrapper.
- Starts live telemetry analysis whenever iRacing sends usable frame data.
- Writes compact per-combo session history under app-owned local storage.
- Keeps raw capture as an opt-in diagnostic/development mode.
- When raw capture is enabled, stores `telemetry.bin`, `telemetry-schema.json`, `latest-session.yaml`, optional `session-info/`, and `capture-manifest.json`.
- Shows live-analysis health signals in the overlay, plus disk-write health when raw capture is enabled.
- Writes rolling local logs, JSONL app events, runtime-state markers, persisted settings, and diagnostics bundles for triage.
- Includes retention cleanup for old captures and diagnostics bundles.
- Includes a replay-mode seam for overlay development against an existing capture.

## Project Layout

- `src/TmrOverlay.App/` contains the Windows application, tray shell, and telemetry collector.
- `src/TmrOverlay.App/Overlays/` contains overlay modules. Each overlay type gets its own folder.
- `src/TmrOverlay.App/Shell/` contains the tray/menu shell.
- `src/TmrOverlay.App/Storage/` contains app-owned local storage path resolution.
- `src/TmrOverlay.App/History/` contains compact session-history summary storage.
- `src/TmrOverlay.App/Logging/`, `Events/`, `Runtime/`, `Diagnostics/`, `Retention/`, `Replay/`, and `Settings/` contain shared application boilerplate.
- `history/baseline/` contains small car/track/session historical summaries intended to become bundled baseline knowledge.
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

The tray menu lets you open the raw capture folder, open the current raw capture when one exists, open logs, create a diagnostics bundle, or exit the app.
The overlay stays visible over the sim so you can confirm the app is running and whether live telemetry analysis has started. With raw capture disabled it shows live frame freshness and history-collection state. With raw capture enabled it also shows queued frames, written frames, dropped frames, telemetry file size, disk-write freshness, and explicit warning/error messages. You can drag it to a new position, and the `X` button fully exits the application.

During local development, the overlay also warns when source files in this checkout are newer than the running build. That is a rebuild reminder only; it does not block capture.

## macOS Local Harness

The ignored macOS harness is for local overlay and boilerplate iteration on this machine:

```bash
./run.sh
```

It writes mock session history to `~/Library/Application Support/TmrOverlayMac/history/user` by default and mirrors the Windows storage layout for captures, user history, logs, events, settings, diagnostics, runtime state, and retention cleanup. Set `TMR_MAC_USE_REPOSITORY_LOCAL_STORAGE=true` if you intentionally want mac harness data under the ignored `local-mac/TmrOverlayMac/` folder.

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

Tracked baseline history belongs under `history/baseline/` so a future packaged app can ship broad starter knowledge while keeping user-generated history separate.

## Configuration

The app reads `src/TmrOverlay.App/appsettings.json`.

Available settings:

- `TelemetryCapture:StoreSessionInfoSnapshots`
- `TelemetryCapture:RawCaptureEnabled`
- `TelemetryCapture:QueueCapacity`
- `SessionHistory:Enabled`
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

By default, writable data resolves under `%LOCALAPPDATA%\TmrOverlay`.

Path settings may be absolute or relative. Relative path settings resolve under the selected app data root.

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

Bundles include app/storage metadata, runtime state, settings, recent logs/events, and latest capture metadata. They intentionally exclude raw `telemetry.bin` payloads.

## Tests

The Windows solution includes an xUnit test project at `tests/TmrOverlay.App.Tests/`.

```powershell
dotnet test
```

The local mac harness includes a SwiftPM XCTest target under `local-mac/TmrOverlayMac/Tests/`.

```bash
cd local-mac/TmrOverlayMac
swift test
```

The current Command Line Tools-only setup on this Mac can run `swift build`, but `swift test` requires an XCTest-capable Xcode toolchain.

## Next Steps

- Add a lightweight local bridge for downstream overlay processes.
- Add a replay tool that can decode `telemetry.bin` with `telemetry-schema.json`.
- Add overlay windows as separate UI surfaces without changing the collector.
