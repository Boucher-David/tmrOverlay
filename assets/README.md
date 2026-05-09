# Assets

This folder holds repo-owned visual source assets that are useful across app, overlay, docs, and publishing work.

## Structure

- `brand/` - source brand images, logos, installer welcome/splash assets, and future app-icon inputs.

## Usage

Keep source images here when they are not yet wired into a specific runtime. Platform-specific derivatives should live near the consuming project once they are generated:

- Windows app icons: `src/TmrOverlay.App/Assets/`.
- Windows installer artwork: generated under `assets/brand/` by `tools/render_windows_installer_splash.swift`.
- macOS harness icons: `local-mac/TmrOverlayMac/` if the ignored harness needs local-only resources.
- Screenshot/mock artifacts: `mocks/`, not this folder.

Prefer keeping the original source asset plus generated icon derivatives. For Windows publishing, derive square `.ico` assets from the source logo rather than using a wide PNG directly as the tray or executable icon.
