# Repository Surface

This repo contains production source, tester-facing docs, internal development assets, and local runtime data. Keep those categories visibly separated so release work does not accidentally package development logs or make support tooling look like normal product UI.

## Customer And Tester Facing

- `README.md` should explain the product, supported workflows, local development basics, and where tester release instructions live.
- `docs/windows-release.md` is the tester handoff: download, checksum, install, upgrade, rollback, signing expectations, and diagnostics handoff.
- `docs/assets/` contains generated tester-facing documentation images, including the one-page Windows install/support tutorial.
- `assets/brand/` contains source brand assets that release tooling can derive platform-specific icons from.

These files can be shown to teammates without requiring private development context.

## Product Source And Validation

- `.github/workflows/` owns CI, PR gates, and release packaging.
- `src/` contains the Windows app and platform-neutral core code.
- `tests/` contains automated validation for non-UI product behavior.
- `Directory.Build.props`, `tmrOverlay.sln`, and `TmrOverlay.cmd` are product build/launch support files.

Release packaging should include only published runtime output from `src/TmrOverlay.App`, not these source folders.

## Internal Development Assets

- `docs/` files other than tester release docs are mostly product-engineering notes: overlay logic, capture format, history evolution, IBT analysis, update strategy, and future branches.
- `fixtures/` contains compact deterministic examples for tests and analysis assumptions.
- `history/baseline/` contains small tracked sample history for development; the app does not read it by default.
- `mocks/` contains screenshot and visual-review artifacts. These are validation/design artifacts, not publish output.
- `skills/` contains agent workflow context and validation instructions.
- `tools/` contains local analysis and rendering tools, including the Windows WinForms screenshot generator used by CI for parity artifacts.
- `local-mac/TmrOverlayMac/` is tracked local harness source for macOS review, mock telemetry, and screenshot iteration. Generated `.build`, app data, logs, captures, and local screenshots stay ignored.

These are useful inside the repo but should not appear in a Windows tester package.

## Local Runtime Data

The app writes user/runtime data outside the install folder by default, under `%LOCALAPPDATA%\TmrOverlay` on Windows. Repo-local runtime data exists only when development overrides are used and should stay ignored:

- `captures/`
- `logs/`
- `diagnostics/`
- `settings/`
- `history/user/`
- `runtime-state.json`
- `artifacts/`
- `tmroverlay-diagnostics-*/`
- `tmroverlay-diagnostics-*.zip`

The release workflow should continue auditing publish output so these folders cannot leak into a shipped package.

## Cleanup Candidates

- `artifacts/` is generated build/screenshot output, including Windows screenshot parity artifacts, and can be deleted locally whenever those files are no longer being inspected.
- Root `tmroverlay-diagnostics-*` folders and `tmroverlay-diagnostics-*.zip` files are extracted or generated diagnostics bundles and can be deleted locally after support review.
- Ignored raw capture folders under `captures/`, especially `captures/IBT/` and large capture directories, should live outside git or in external storage once analysis is complete.
- The tracked legacy raw capture under `captures/capture-20260426-032822-916/` is not customer-facing and still contains `telemetry.bin`. Replace any remaining references with compact fixtures under `fixtures/` before removing it from git in a dedicated cleanup branch.
- `mocks/overlay-catalog/` is exploratory reference material. Keep it under `mocks/` while useful, but do not treat it as product documentation.
