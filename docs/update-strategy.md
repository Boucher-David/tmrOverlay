# Update Notification and Self-Update Strategy

Last updated: 2026-04-28

## Current State

The current Windows tester build is a self-contained `win-x64` publish folder that can be zipped and shared. That is good for early testing, but it is not a full updater surface:

- the running executable should not try to replace itself in place
- zipped releases do not give Windows a durable install/update identity
- there is no release signing, installer channel, rollback mechanism, or update manifest yet
- the safest current behavior is update notification plus a link to the release download

## Recommendation

Use two phases.

### Phase 1: Update Notification Only

Add an `UpdateCheckService` that checks a static HTTPS manifest or the latest GitHub Release at startup and then no more than once per day.

Suggested manifest:

```json
{
  "version": "0.9.4",
  "publishedAtUtc": "2026-04-28T00:00:00Z",
  "downloadUrl": "https://example.invalid/TmrOverlay-win-x64.zip",
  "sha256": "hex-encoded-release-hash",
  "releaseNotesUrl": "https://example.invalid/releases/0.9.4",
  "minimumVersion": "0.9.0",
  "critical": false
}
```

The app should compare the manifest version with `AppVersionInfo.Current`, then expose the result through the tray menu and settings window. During an active race, the notification should be passive: no modal dialog, no forced restart, and no update prompt on top of the sim. The command should open the release page or copy the download URL.

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

- Tray menu: `Update available...` when a newer version exists.
- Settings window: an Updates section or tab with current version, latest version, last checked time, release notes link, and download/install button.
- Error Logging tab diagnostics: include update check state, last failure, selected channel, and current app version.
- Never put an update prompt above the sim during an active session.

## Release Requirements

Before automatic install:

- choose the update channel host
- sign release artifacts
- publish a stable manifest or framework feed
- record SHA-256 hashes for downloadable artifacts
- log update checks and failures into diagnostics bundles
- add a manual `Check for updates` command for support
