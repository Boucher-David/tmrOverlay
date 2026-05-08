# Update Notification and Self-Update Strategy

Last updated: 2026-05-07

## Current State

The v0.16 release-channel branch makes Velopack the canonical Windows installer/update path while keeping the portable zip as a transitional fallback. The PR/main workflow restores, builds, tests, validates tracked screenshots, checks Windows screenshot expectations, runs a publish dry run with package audit, and dry-runs `vpk pack`. A `vMAJOR.MINOR.PATCH` tag publishes the app as a self-contained `win-x64` package, audits the publish folder, zips it, writes a package manifest and SHA-256 checksum, packs Velopack installer/update assets, uploads workflow artifacts, and attaches both the portable zip artifacts and Velopack assets to the public GitHub Release.

The public GitHub Release is the Velopack feed. Installed builds use Velopack `GithubSource` against `https://github.com/Boucher-David/TMROverlay` with no embedded token. The active pre-1.0 package id is `TMROverlay`; it intentionally replaces the first tester id, `TechMatesRacing.TmrOverlay`, so generated setup assets and installed identity use the shorter app name. Portable/dev runs skip update checks because they do not have a Velopack install identity.

Still pending after the first Velopack pass:

- release signing before broader distribution
- deciding whether to add user-initiated download/apply/restart inside the app after teammate installer testing
- deciding whether to keep the portable zip long term or only as a support fallback

## Recommendation

Use Velopack directly rather than building a custom zip updater.

### Velopack Baseline

The release channel should provide:

1. `VelopackApp.Build().SetAutoApplyOnStartup(false).Run()` at the start of app startup.
2. A public GitHub Releases update source with no client token.
3. A non-blocking startup update check after a short delay.
4. A manual `Check for Updates` command from the tray menu and Support tab.
5. Passive settings banner states for update available, pending restart, and check failure.
6. Support/diagnostics metadata for current version, latest version, source, check time, failure state, installed/portable state, and release URL.
7. CI dry-run Velopack packaging on PRs and publish Velopack installer/update assets on release tags.

This keeps the first installer branch focused on distribution and visibility. It does not yet auto-download, auto-apply, or restart the app over the simulator.

### Active Apply Later

After teammates have installed through Velopack and at least one update has been validated:

- add explicit user-initiated download/apply controls
- prefer apply-on-exit or apply-and-restart only when the user asks for it
- keep update prompts out of active sessions
- keep failure state visible in Support and diagnostics bundles

Microsoft-native option: MSIX with App Installer.

- Gives Windows a package identity and automatic update settings through `.appinstaller`.
- Supports update checks on launch, prompts, and activation blocking on supported Windows versions.
- Requires accepting MSIX packaging constraints and signing/distribution setup.

Legacy option: Squirrel.Windows.

- Still relevant historically for Windows desktop self-updating, but for a new .NET 8 WinForms app Velopack looks like the cleaner path.

## UI Behavior

- Tray menu: update status, `Check for Updates`, and `Open Releases`.
- Settings window: show update-available or update-warning states as a yellow banner above the tabs and below the main app title area.
- Startup behavior: check once per app launch, record success/failure quietly, and never block startup.
- Support tab diagnostics: include update check state, last failure, selected channel/source, and current app version.
- Never put an update prompt above the sim during an active session.

## Release Requirements

Before broader production distribution:

- sign release artifacts
- validate at least one installed Velopack update from GitHub Releases
- decide whether to keep the portable zip as a normal release asset
- add active download/apply controls only after the passive channel proves reliable
