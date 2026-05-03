# Assets

This folder holds repo-owned visual source assets that are useful across app, overlay, docs, and publishing work.

## Structure

- `brand/` - source brand images, logos, and future app-icon inputs.

## Usage

Keep source images here when they are not yet wired into a specific runtime. Platform-specific derivatives should live near the consuming project once they are generated:

- Windows app icons: `src/TmrOverlay.App/Assets/` or the WinForms project resource path chosen by the publishing branch.
- macOS harness icons: `local-mac/TmrOverlayMac/` if the ignored harness needs local-only resources.
- Screenshot/mock artifacts: `mocks/`, not this folder.

Prefer keeping the original source asset plus generated icon derivatives. For Windows publishing, derive square `.ico` assets from the source logo rather than using a wide PNG directly as the tray or executable icon.
