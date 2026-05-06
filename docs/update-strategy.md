# Update Notification and Self-Update Strategy

Last updated: 2026-05-06

## Current State

The v0.9 publishing branch defines a tag-driven portable Windows release path. The PR/main workflow restores, builds, tests, validates tracked screenshots, and runs a publish dry run with package audit. A `vMAJOR.MINOR.PATCH` tag publishes the app as a self-contained `win-x64` package, audits the publish folder, zips it, writes a package manifest and SHA-256 checksum, uploads workflow artifacts, and attaches the zip/manifest/checksum to a GitHub Release.

That is enough for teammate tester downloads, but it is not a full updater surface:

- the running executable should not try to replace itself in place
- zipped releases do not give Windows a durable install/update identity
- there is no release signing, installer channel, automatic rollback mechanism, or update manifest yet
- the safest current behavior is update notification plus a link to the release download

## Recommendation

Use two phases.

### v0.9 Publishing Baseline

The v0.9 branch should make the app publishable for teammates even before automatic install/update is solved:

1. Keep the PR/main Windows validation workflow as the merge gate, including restore/build/test, tracked screenshot validation, and a self-contained publish dry run with package audit. Implemented in `.github/workflows/windows-dotnet.yml`.
2. On `v*.*.*` tags or manual release dispatch, run Release restore/build/test, then publish `src/TmrOverlay.App` for `win-x64` as a self-contained single-file output. Implemented for tags and manual package artifacts.
3. Audit the publish folder, zip it, generate a package manifest and SHA-256 checksum file, and upload the release artifacts. Implemented.
4. Promote the zip/manifest/checksum into a GitHub Release with versioned release notes, rather than requiring teammates to download from a workflow run. Implemented for `vMAJOR.MINOR.PATCH` tags.
5. Embed release metadata in the app: product version, informational version/commit, executable/tray icon, and diagnostics bundle fields that identify the exact build. Implemented through shared version properties, release publish metadata, diagnostics app-version output, and a generated Windows `.ico`.
6. Document portable install/upgrade behavior: package contents, unzip location, replacing old files, settings/history compatibility, rollback, SmartScreen/signing expectations, and how to send diagnostics. Implemented in `docs/windows-release.md`.
7. Decide signing before broad distribution. Private tester zip builds can remain unsigned temporarily, but a publishable installer/package should sign the executable or package. Still pending.
8. Defer active self-update until a durable installer/update channel exists; add passive update discovery first. Still pending after release artifacts exist.

This keeps v0.9 focused on a reliable release artifact and teammate feedback loop. Installer frameworks and automatic updates can then build on top of a known-good release feed.

### Phase 1: Update Notification Only

Add an `UpdateCheckService` that checks once on each app startup. The preferred source is Velopack-compatible release metadata/feed, even before we expose automatic install/apply behavior. If the portable zip channel remains the only published artifact during v0.12, use a stable HTTPS manifest or latest GitHub Release lookup as a temporary compatibility path behind the same service interface.

Suggested manifest:

```json
{
  "version": "0.9.4",
  "publishedAtUtc": "2026-04-28T00:00:00Z",
  "downloadUrl": "https://example.invalid/TmrOverlay-v0.9.4-win-x64.zip",
  "sha256": "hex-encoded-release-hash",
  "releaseNotesUrl": "https://example.invalid/releases/0.9.4",
  "minimumVersion": "0.9.0",
  "critical": false
}
```

The app should compare the release version with `AppVersionInfo.Current`, then expose the result through the tray menu and settings window. During an active race, the notification should be passive: no modal dialog, no forced restart, and no update prompt on top of the sim. The command should open the release page or copy the download URL.

This phase is compatible with the current zip distribution and keeps failure modes simple.

### Phase 2: Installer-Based Self-Update

Move to an updater framework or Windows package format before trying to replace files automatically.

Preferred option: Velopack.

- Works with C#/.NET desktop apps.
- Provides installer/update tooling plus a client SDK for checking and applying updates.
- Can host release packages on static file storage or GitHub Releases.
- Handles the important separate updater process problem that a self-replacing exe cannot solve cleanly.

Microsoft-native option: MSIX with App Installer.

- Gives Windows a package identity and automatic update settings through `.appinstaller`.
- Supports update checks on launch, prompts, and activation blocking on supported Windows versions.
- Requires accepting MSIX packaging constraints and signing/distribution setup.

Legacy option: Squirrel.Windows.

- Still relevant historically for Windows desktop self-updating, but for a new .NET 8 WinForms app Velopack looks like the cleaner path.

## Self-Updating a Zip Build

If we keep zipped builds for a while and need self-update before adopting an installer framework, do it with a separate updater helper:

1. Main app downloads the update zip to a temp folder.
2. Main app validates the expected SHA-256 hash.
3. Main app launches `TmrOverlay.Updater.exe` with current install path, downloaded zip path, and restart command.
4. Main app exits.
5. Updater waits for the main process to stop, replaces files, writes a rollback backup, restarts the app, and logs the outcome.

This should be treated as temporary infrastructure. It is easy to get wrong around locked files, antivirus, partial copies, rollback, and user permissions.

## UI Behavior

- Tray menu: `Check for updates` plus `Update available...` when a newer version exists.
- Settings window: show update-available or update-warning states as a yellow banner above the tabs and below the main app title area, then expose detail/action controls in an Updates section or tab with current version, latest version, last checked time, release notes link, and download/install button.
- Startup behavior: check once per app launch, record success/failure quietly, and never block startup.
- Support tab diagnostics: include update check state, last failure, selected channel/source, and current app version.
- Never put an update prompt above the sim during an active session.

## Release Requirements

Before automatic install:

- choose the update channel host
- sign release artifacts
- publish a stable manifest or framework feed
- record SHA-256 hashes for downloadable artifacts
- log update checks and failures into diagnostics bundles
- add a manual `Check for updates` command for support
