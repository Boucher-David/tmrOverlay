# Version History

## v0.2 - 2026-04-26

This version turns the repo from a minimal Windows capture scaffold into a paired Windows/mac development baseline with local app boilerplate and historical session storage.

### Highlights

- Added a root `.gitignore`, ignored root `run.sh`, and ignored `local-mac/` macOS development harness.
- Reorganized the Windows app into clearer modules for shell, overlays, storage, history, logging, settings, diagnostics, runtime state, retention, replay, and app/version metadata.
- Added a local macOS harness that mirrors the Windows app structure and behavior for local development while using mock telemetry instead of iRacing.
- Added app-owned storage defaults so installed users write to local application data instead of the development repo.
- Added compact car/track/session history summaries for future fuel and race-planning estimates, with tracked baseline history separated from user-generated history.
- Added persisted overlay settings so the status overlay position and size can survive restarts.
- Added rolling logs, JSONL app events, runtime clean-shutdown markers, diagnostics bundles, and startup retention cleanup.
- Added a replay-mode seam for future overlay development against existing capture artifacts.
- Added xUnit test scaffolding for the Windows app and SwiftPM XCTest scaffolding for the mac harness.
- Updated repo documentation and agent context so shared app/overlay changes should be mirrored in both Windows and mac going forward.

### Verification

- `swift build` passes for `local-mac/TmrOverlayMac`.
- `swift test` is currently blocked on this Mac because the installed Command Line Tools toolchain does not provide `XCTest`.
- Windows build/test verification is still pending because `dotnet` is not installed on this machine.
- `git diff --check` passes.
