# Windows Tester Releases

TmrOverlay v0.9 starts with portable Windows tester builds. A release tag such as `v0.9.0` produces a GitHub Release with:

- `TmrOverlay-v0.9.0-win-x64.zip`
- `TmrOverlay-v0.9.0-win-x64.zip.sha256`
- `TmrOverlay-v0.9.0-win-x64-manifest.txt`
- generated GitHub release notes

The zip is self-contained for Windows x64. Testers do not need to install the .NET runtime.

## One-Page Teammate Guide

The current teammate-facing install/support handoff image is:

![Tech Mates Racing Overlay Windows tester install and feedback guide](assets/windows-release-teammate-tutorial.png)

Regenerate it after release-package or Support-tab flow changes:

```bash
swift tools/render_release_tutorial.swift
python3 tools/validate_overlay_screenshots.py --profile release-tutorial --root docs/assets
```

## Publishing

1. Merge the release branch to `main` after the Windows validation gate passes.
2. Create an annotated tag on the merge commit:

   ```bash
   git tag -a v0.9.0 -m "v0.9.0 - Production publishing baseline" -m "Publishes a portable Windows tester build with checksum and release metadata."
   git push origin v0.9.0
   ```

3. GitHub Actions runs `.github/workflows/windows-dotnet.yml`.
4. The PR/main validation job restores, builds, tests, validates tracked screenshots, generates/validates Windows-rendered overlay screenshots as workflow artifacts, and runs a self-contained publish dry run with the same package audit used by release packaging.
5. The tag workflow publishes `src/TmrOverlay.App` for `win-x64`, audits the publish folder, writes a package manifest, zips the publish folder, generates a SHA-256 checksum, uploads workflow artifacts, and creates or updates the GitHub Release assets.

Manual workflow dispatch can still produce package artifacts for a branch test run, but it does not create a GitHub Release unless the run is for a `vMAJOR.MINOR.PATCH` tag.

## Package Contents

The portable zip should contain the runtime app only. The release workflow fails if top-level repo/development folders such as `.github`, `captures`, `docs`, `history`, `local-mac`, `mocks`, `skills`, `tests`, or `tools` appear in the publish folder.

The expected v0.9 package shape is intentionally small:

- `TmrOverlay.App.exe`
- `appsettings.json`
- `Assets/TMRLogo.png`

The executable icon is embedded from `src/TmrOverlay.App/Assets/TmrOverlay.ico`. The release manifest asset lists the exact published files and sizes for each build, so package review can happen without downloading and unzipping the app.

## Tester Download

1. Open the repository's GitHub Releases page.
2. Open the latest `vMAJOR.MINOR.PATCH` release.
3. Download the `TmrOverlay-<version>-win-x64.zip` asset.
4. Download the matching `.sha256` asset when you want to verify the file.
5. Unzip the package into a normal user-writable folder, for example:

   ```text
   %LOCALAPPDATA%\Programs\TmrOverlay
   ```

6. Run `TmrOverlay.App.exe`.

The app stores settings, history, logs, diagnostics bundles, and captures under `%LOCALAPPDATA%\TmrOverlay` by default. Replacing the portable application folder does not delete that user data.

## User Data And Compatibility

Portable installs are replaceable application folders. User data is separate from the zip by default:

```text
%LOCALAPPDATA%\TmrOverlay
```

That app-data root contains settings, user history, logs, events, diagnostics, runtime state, and optional captures. A new install path should still find the same app-data root unless the user explicitly overrides storage through configuration or `TMR_` environment variables.

Settings are versioned in `settings/settings.json` and loaded through `AppSettingsMigrator`, which normalizes older settings and writes them back at the current settings version. Session history has explicit summary, collection-model, aggregate, and analysis version constants. On startup, `HistoryMaintenanceService` scans user history, backs up and normalizes compatible legacy summaries, skips incompatible/future/corrupt summaries into a maintenance manifest, and rebuilds aggregates before history-backed overlays use that data.

If a release changes a durable user-data schema, the branch must update the matching version constants, migrations or compatible readers, schema-compatibility tests, and docs before publishing. For car/track history specifically, changing the stored JSON shape should bump `summaryVersion` and add an ordered summary migration; changing the meaning of a derived metric or confidence rule should bump `collectionModelVersion` and either provide a compatible adapter or intentionally exclude old summaries from current aggregates. Unsupported future or incompatible old history must remain on disk and be excluded from overlays instead of causing startup or parsing failures.

## Checksum Verification

From PowerShell in the folder containing the downloaded zip:

```powershell
$zip = "TmrOverlay-v0.9.0-win-x64.zip"
$expected = (Get-Content "$zip.sha256").Split(" ")[0]
$actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$actual -eq $expected
```

The command should print `True`.

## Upgrade And Rollback

To upgrade a portable release:

1. Exit TmrOverlay from the settings window or tray menu.
2. Keep or rename the old unzipped application folder.
3. Unzip the new release into a fresh folder, or replace the old application files while the app is closed.
4. Start `TmrOverlay.App.exe`.

To roll back, close the app and run the previous unzipped folder again. User settings/history remain in `%LOCALAPPDATA%\TmrOverlay`; if a future release changes a durable schema, the branch must document migration and compatibility before release.

## Signing And SmartScreen

The v0.9 portable tester builds are expected to be unsigned unless signing is added before the milestone closes. Windows SmartScreen or antivirus tools may warn on first launch because the executable is new and unsigned. Broader distribution should not rely on unsigned builds; choose executable/package signing before publishing beyond private testers.

## Diagnostics For Feedback

For teammate feedback, ask testers to include:

- the release tag they installed
- the app version shown in diagnostics or support output
- a diagnostics bundle from `%LOCALAPPDATA%\TmrOverlay\diagnostics`
- notes about whether iRacing was live, replaying, or not running

Diagnostics bundles include app version/runtime metadata and local logs, but they intentionally exclude raw `telemetry.bin` and source `.ibt` payloads.
