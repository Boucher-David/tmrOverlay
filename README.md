# TmrOverlay

`TmrOverlay` starts as a Windows background collector for the iRacing SDK. The current scaffold runs as a tray application, watches for iRacing to come online, and writes raw capture artifacts that we can analyze later when we start building overlays and derived metrics.

## What It Does Today

- Starts as a WinForms tray application with no main window.
- Shows a tiny always-on-top status overlay in the top-left corner.
- Lets you drag the status overlay and close the whole app from its `X` button.
- Connects to iRacing through the `irsdkSharp` wrapper.
- Opens a new capture whenever iRacing starts sending live data.
- Stores the raw telemetry buffer for every frame in `telemetry.bin`.
- Stores the telemetry schema once per capture in `telemetry-schema.json`.
- Stores raw session YAML snapshots in `session-info/` and updates `latest-session.yaml`.
- Finalizes a `capture-manifest.json` file when the sim disconnects or the app exits.

## Project Layout

- `src/TmrOverlay.App/` contains the Windows application, tray shell, and telemetry collector.
- `docs/capture-format.md` documents the binary frame format used by `telemetry.bin`.
- `telemetry.md` summarizes the event/session/car schema exposed by the current raw capture model.

## Capture Output

By default captures are written under the repo-local `captures/` directory:

```text
tmrOverlay\captures
```

When the app is run from this checkout, relative capture paths resolve against the repo root. That keeps new captures next to the existing sample data already committed in this repository.

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

The tray menu lets you open the captures folder, open the current capture, or exit the app.
The overlay stays visible over the sim so you can confirm the app is running and whether live telemetry capture has started. You can drag it to a new position, and the `X` button fully exits the application.

## Configuration

The app reads `src/TmrOverlay.App/appsettings.json`.

Available settings:

- `TelemetryCapture:CaptureRoot`
- `TelemetryCapture:StoreSessionInfoSnapshots`
- `TelemetryCapture:QueueCapacity`

`CaptureRoot` may be absolute or relative. Relative paths resolve against the repo root when `tmrOverlay.sln` is found by walking up from the app's output directory.

`CaptureRoot` can also be overridden with the environment variable `TMR_TelemetryCapture__CaptureRoot`.

## Next Steps

- Add a lightweight local bridge for downstream overlay processes.
- Add a replay tool that can decode `telemetry.bin` with `telemetry-schema.json`.
- Add overlay windows as separate UI surfaces without changing the collector.
