# TmrOverlay

TmrOverlay is a pre-1.0 Windows companion app for iRacing. It runs as a tray application, reads live iRacing telemetry, and renders small purpose-built overlays for racing, streaming, and support workflows.

The production app is Windows-only because it talks to iRacing through the SDK. Browser-source overlays are the primary mac-friendly review surface; the tracked macOS harness is now secondary native-shell scaffolding for cases that still need it.

TmrOverlay is not affiliated with or endorsed by iRacing.

## Current Status

This project is still moving quickly toward a V1.0-quality core overlay app. Expect active changes to overlay behavior, release packaging, diagnostics, and internal data contracts while the 0.x releases mature.

Current releases focus on:

- reliable Windows install/update flow
- clear settings and support surfaces
- standings, relative, radar, flags, track map, fuel, and race-state overlays
- local OBS/browser-source routes
- diagnostic capture and support bundles that help validate behavior from real sessions

## Features

- **Settings app:** a fixed-size WinForms control surface for overlay visibility, scale, opacity where useful, session filters, units, content columns, OBS browser-source size hints, update state, support actions, and diagnostics.
- **Native overlays:** Standings, Relative, Track Map, Fuel Calculator, Flags, Session / Weather, Pit Service, Input / Car State, local in-car Radar, Gap To Leader, and Stream Chat.
- **Browser-source overlays:** localhost routes for OBS/capture workflows, including Standings, Relative, Fuel, Session / Weather, Pit Service, Inputs, Radar, Gap To Leader, Track Map, Stream Chat, and Garage Cover.
- **Garage Cover:** an OBS/browser-source privacy cover that hides garage/setup details with a user-imported image or a fail-closed fallback.
- **Telemetry model:** normalized live snapshot models for timing, relative, spatial/radar, weather, fuel/pit, race events, inputs, race projection, and track-map state.
- **Local history:** compact per-car/track/session summaries for fuel and stint context without making raw telemetry the normal data path.
- **Diagnostics:** support bundles, rolling logs, performance snapshots, model parity evidence, live overlay diagnostics, and opt-in raw capture for deeper investigation.
- **Release channel:** Velopack installer/update assets from public GitHub Releases, with a portable zip kept as a fallback.

## Install

Pre-release Windows builds are published from GitHub Actions on version tags.

1. Open the latest GitHub Release.
2. Prefer the Velopack MSI installer for normal installs and upgrades.
3. Use the portable `TmrOverlay-<version>-win-x64.zip` only as a fallback or support artifact.
4. Start TmrOverlay and open the settings window from the tray icon.

## Upgrade

Velopack-installed builds check GitHub Releases passively on startup and from the tray or Support tab. The current app does not auto-download or auto-restart; when an update is available, use `Download and Install Update` and then `Restart to Apply Update`, or run the latest MSI installer from the release page.

Portable zip users should close TmrOverlay, unzip the newer package into a fresh folder or replace the old app files while the app is closed, then run `TMROverlay.exe`. Settings, history, logs, diagnostics, and captures stay under `%LOCALAPPDATA%\TmrOverlay`, so switching from the portable zip to the setup installer should keep existing user data.

Current builds may be unsigned, so Windows SmartScreen can warn on first launch. See [docs/windows-release.md](docs/windows-release.md) for package contents, checksum verification, install, upgrade, rollback, signing expectations, and diagnostics handoff.

## Data And Privacy

By default, TmrOverlay stores user data outside the install folder under:

```text
%LOCALAPPDATA%\TmrOverlay
```

That app-data root contains settings, history, logs, diagnostics, runtime state, generated track maps, and optional captures. Updating or replacing the app does not delete this data. Startup/update cleanup only removes stale legacy installer identity folders/shortcuts; uninstalling an installed Velopack build removes this app-data root as part of uninstall cleanup.

Raw telemetry capture is opt-in. Use the Support checkbox or a `TelemetryCapture:RawCaptureEnabled=true` configuration override when a tester intentionally needs raw evidence. Normal diagnostics are compact summaries and logs; diagnostics bundles intentionally exclude raw `telemetry.bin` and source `.ibt` files.

Streamlabs widget URLs and other private local settings are redacted from diagnostics bundles.

Shared app defaults live in `shared/tmr-overlay-contract.json`, with the file shape documented by `shared/tmr-overlay-contract.schema.json`. Windows and the tracked mac harness both use that contract for the durable settings schema version, stream-chat defaults, and Design V2 color tokens; localhost browser overlays receive the resolved V2 tokens from the Windows renderer instead of reading local files directly. User-local Windows theme overrides can still live in `overlay-theme.json` under the app settings root and are applied after the shared contract.

## Build From Source

Requirements:

- Windows
- .NET 8 SDK or Visual Studio 2022 with .NET desktop development tools
- iRacing installed/running for real telemetry

Build and test:

```powershell
dotnet restore .\tmrOverlay.sln
dotnet build .\tmrOverlay.sln --configuration Release --no-restore
dotnet test .\tmrOverlay.sln --configuration Release --no-build
```

Run the app from Visual Studio, with `dotnet run`, or by double-clicking [TmrOverlay.cmd](TmrOverlay.cmd) after a build.

```powershell
dotnet run --project .\src\TmrOverlay.App\TmrOverlay.App.csproj
```

## Development Notes

The Windows app is the production runtime:

- `src/TmrOverlay.App/` contains the WinForms app, tray shell, overlays, telemetry collector, diagnostics, release update service, and localhost browser-source server.
- `src/TmrOverlay.Core/` contains platform-neutral settings, overlay metadata, live telemetry models, history models, post-race analysis models, and fuel logic.
- `tests/TmrOverlay.App.Tests/` contains xUnit tests for non-UI behavior.
- `.github/workflows/windows-dotnet.yml` is the Windows CI and release-packaging gate.

The browser review server is the preferred non-Windows overlay development loop:

- `npm run review:browser` serves fixture-backed overlay URLs from the same browser assets used by localhost/OBS pages.
- Review routes are available under `/review/app`, `/review/settings/general`, `/review/overlays/<overlay-id>`, and `/overlays/<overlay-id>`.
- Asset changes are read from source and trigger browser reloads through lightweight polling.
- This validates browser layout, JavaScript behavior, and localhost/OBS parity; Windows CI or a real Windows run still owns native focus, topmost, click-through, and iRacing SDK behavior.

The macOS harness is for secondary local development only:

- `local-mac/TmrOverlayMac/` mirrors enough app shape to iterate on native-shell ideas with mock telemetry.
- Its source is tracked so existing mac review coverage can keep moving while browser review replaces it as the primary parity surface.
- Generated review screenshots live under `mocks/`.
- Windows-rendered screenshots are generated by CI from real WinForms forms and uploaded as workflow artifacts.

Useful local validation:

```bash
npm run test:browser
npm run test:browser:install # first run only, when Playwright's Chromium cache is missing
git diff --check
python3 skills/tmr-overlay-validation/scripts/check-csharp-member-duplicates.py
python3 tools/validate_overlay_screenshots.py --profile windows-expectations
python3 tools/validate_overlay_screenshots.py --profile release-tutorial --root docs/assets
```

On Windows, also run the full .NET restore/build/test gate before relying on local changes. On non-Windows machines, the C# scanner is only a fallback; CI must still prove the real WinForms build.

## Localhost Browser Sources

The localhost overlay server is enabled by default for local OBS/capture workflows. It listens on the configured local port and serves overlay pages such as:

- `/overlays/standings`
- `/overlays/relative`
- `/overlays/fuel-calculator`
- `/overlays/track-map`
- `/overlays/stream-chat`
- `/overlays/garage-cover`

The browser-source pages consume normalized app snapshots from localhost. They do not talk to iRacing directly. Overlay Bridge, a future trusted peer/client data-sharing boundary, is intentionally separate from these local OBS routes.

For local design and functionality review without Windows/iRacing, run:

```bash
npm run review:browser
```

Then open `http://127.0.0.1:5177/review/app` for the full app validator, or `http://127.0.0.1:5177/review` for the route index.

## Diagnostics

The settings Support tab and tray menu can create diagnostics bundles under:

```text
%LOCALAPPDATA%\TmrOverlay\diagnostics
```

Bundles include app/storage metadata, telemetry state, release update state, localhost request state, track-map inventory metadata, live telemetry synthesis, performance snapshots, recent logs/events, runtime state, settings, latest capture metadata and compact sidecars, recent history summaries, and advanced collection artifacts when present.

If the Settings UI is frozen and a bundle cannot be created, collect `%LOCALAPPDATA%\TmrOverlay\logs` and the latest `%LOCALAPPDATA%\TmrOverlay\captures` folder from the diagnostic patch build.

Performance diagnostics are always on and write periodic JSONL snapshots under:

```text
%LOCALAPPDATA%\TmrOverlay\logs\performance
```

Those snapshots include telemetry throughput, iRacing network/system values, overlay refresh timings, timer cadence and late-tick summaries, lifecycle visibility/fade states, overlay window/input-intercept state, settings save/apply queue metrics, skipped unchanged-sequence updates, paint samples, localhost activity, process memory, GDI/USER handle counts, and GC counts. Diagnostics bundles also include `metadata/ui-freeze-watch.json` for focused Settings/Flags freeze triage, `metadata/live-telemetry-synthesis.json` for current focus/session/field-coverage and local-overlay context decisions, `metadata/browser-overlays.json` for the localhost route catalog, `metadata/shared-settings-contract.json` plus the shared contract/schema files for settings-token parity triage, `live-overlays/manifest.json`, optional live-window overlay crops when enabled, and `metadata/installer-cleanup.json` for stale legacy shortcut/package cleanup triage.

## Documentation

- [docs/windows-release.md](docs/windows-release.md) - release assets, install, update, rollback, signing, and diagnostics.
- [docs/update-strategy.md](docs/update-strategy.md) - Velopack release-channel plan and follow-up work.
- [docs/overlay-logic.md](docs/overlay-logic.md) - index of overlay behavior notes.
- [docs/overlay-behavior-reference.md](docs/overlay-behavior-reference.md) - plain-English behavior reference for each overlay.
- [docs/overlay-flow-diagrams.md](docs/overlay-flow-diagrams.md) - Mermaid flow diagrams for overlay decision paths.
- [docs/capture-format.md](docs/capture-format.md) - raw capture file format.
- [docs/history-data-evolution.md](docs/history-data-evolution.md) - durable user-history compatibility rules.
- [docs/repo-surface.md](docs/repo-surface.md) - what belongs in source, docs, validation artifacts, runtime data, and release packages.
- [VERSION.md](VERSION.md) - milestone history and current branch release summary.

## Roadmap

Near-term work is focused on validating the release channel and preparing V1.x foundations:

- validate installed Velopack updates from public GitHub Releases
- use the new diagnostics to measure overlay lifecycle and timer efficiency before changing refresh behavior
- add replay tooling for raw captures
- continue hardening radar, relative, standings, track map, and gap behavior with large multiclass sessions
- add future analysis and broadcast overlays such as a compact Timing Tower after the core app is stable
