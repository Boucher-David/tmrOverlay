# TmrOverlay

TmrOverlay is a pre-1.0 Windows companion app for iRacing. It runs as a tray application, reads live iRacing telemetry, and renders small purpose-built overlays for racing, streaming, and support workflows.

The production app is Windows-only because it talks to iRacing through the SDK. The repository also contains a tracked macOS harness for mock-telemetry development and screenshot iteration.

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
2. Prefer the Velopack setup executable for normal installs and upgrades.
3. Use the portable `TmrOverlay-<version>-win-x64.zip` only as a fallback or support artifact.
4. Start TmrOverlay and open the settings window from the tray icon.

## Upgrade

Velopack-installed builds check GitHub Releases passively on startup and from the tray or Support tab. The current app does not auto-download or auto-restart; when an update is available, open the release page, close TmrOverlay, run the latest setup executable, and launch from the installed shortcut.

Portable zip users should close TmrOverlay, unzip the newer package into a fresh folder or replace the old app files while the app is closed, then run `TMROverlay.exe`. Settings, history, logs, diagnostics, and captures stay under `%LOCALAPPDATA%\TmrOverlay`, so switching from the portable zip to the setup installer should keep existing user data.

Current builds may be unsigned, so Windows SmartScreen can warn on first launch. See [docs/windows-release.md](docs/windows-release.md) for package contents, checksum verification, install, upgrade, rollback, signing expectations, and diagnostics handoff.

## Data And Privacy

By default, TmrOverlay stores user data outside the install folder under:

```text
%LOCALAPPDATA%\TmrOverlay
```

That app-data root contains settings, history, logs, diagnostics, runtime state, generated track maps, and optional captures. Updating or replacing the app does not delete this data.

Raw telemetry capture is normally opt-in. The `v0.18.2` frozen-UI diagnostic patch temporarily forces raw capture on from startup so testers can collect evidence without reaching the Support checkbox. Normal diagnostics are compact summaries and logs; diagnostics bundles intentionally exclude raw `telemetry.bin` and source `.ibt` files.

Streamlabs widget URLs and other private local settings are redacted from diagnostics bundles.

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

The macOS harness is for local development only:

- `local-mac/TmrOverlayMac/` mirrors enough app shape to iterate on overlays with mock telemetry.
- Its source is tracked so mac review parity and CI build coverage move with Windows overlay/settings changes.
- Generated review screenshots live under `mocks/`.
- Windows-rendered screenshots are generated by CI from real WinForms forms and uploaded as workflow artifacts.

Useful local validation:

```bash
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

## Diagnostics

The settings Support tab and tray menu can create diagnostics bundles under:

```text
%LOCALAPPDATA%\TmrOverlay\diagnostics
```

Bundles include app/storage metadata, telemetry state, release update state, localhost request state, track-map inventory metadata, performance snapshots, recent logs/events, runtime state, settings, latest capture metadata and compact sidecars, recent history summaries, and advanced collection artifacts when present.

If the Settings UI is frozen and a bundle cannot be created, collect `%LOCALAPPDATA%\TmrOverlay\logs` and the latest `%LOCALAPPDATA%\TmrOverlay\captures` folder from the diagnostic patch build.

Performance diagnostics are always on and write periodic JSONL snapshots under:

```text
%LOCALAPPDATA%\TmrOverlay\logs\performance
```

Those snapshots include telemetry throughput, iRacing network/system values, overlay refresh timings, timer cadence and late-tick summaries, lifecycle visibility/fade states, overlay window/input-intercept state, settings save/apply queue metrics, skipped unchanged-sequence updates, paint samples, localhost activity, process memory, GDI/USER handle counts, and GC counts. Diagnostics bundles also include `metadata/ui-freeze-watch.json` for focused Settings/Flags freeze triage.

## Documentation

- [docs/windows-release.md](docs/windows-release.md) - release assets, install, update, rollback, signing, and diagnostics.
- [docs/update-strategy.md](docs/update-strategy.md) - Velopack release-channel plan and follow-up work.
- [docs/overlay-logic.md](docs/overlay-logic.md) - index of overlay behavior notes.
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
