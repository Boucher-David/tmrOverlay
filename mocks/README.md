# Overlay Mocks

This folder stores overlay mock images and generated preview screenshots for user feedback and design review.

Each overlay type should get its own subfolder:

```text
mocks/
  fuel-calculator/
  car-radar/
  gap-to-leader/
  settings-overlay/
  design-v2/
  standings/
  relative/
  competition-distance-graph/
  weather/
```

These files are review artifacts, not publish output. Generated mac-harness screenshots are the source of truth for current runtime overlay review. The `relative/` folder is the production Relative overlay preview set; `design-v2/` is a separate generated mac-harness proving ground for future shared overlay primitives. Its default direction is telemetry-first overlays, including standings, relative, local blindspot/radar, laptime, stint log, and flag-style surfaces, with source/evidence chrome reserved for stale, unavailable, modeled, or derived values. Keep any future exploratory static design mock clearly labeled and separate from generated validation artifacts.

When adding or materially changing an overlay, update that overlay's mock folder with:

- one focused screenshot of the main expected live state
- one multi-state contact sheet covering waiting/unavailable, normal/healthy, edge/warning, and error/fallback behavior when those states apply
- one smaller per-state PNG under `mocks/<overlay-id>/states/` for each contact-sheet scenario, so individual states are easy to inspect, attach to issues, and validate

Validate generated screenshots before review:

```bash
python3 tools/validate_overlay_screenshots.py
```

That check verifies the expected PNGs exist, match their expected dimensions where the dimensions are fixed, and are not blank. It does not replace visual review: each screenshot state should still be checked against its scenario contract. In particular, waiting/unavailable states should use isolated fixtures with no local user history or cached live data, so stale development data cannot make an empty state look partially populated.

The same validation lesson applies outside screenshots. Use deterministic fixtures for collectors, diagnostics, retention, settings, updater, and performance paths; assert both the positive data shown and the negative data that must stay hidden; and keep mock/development state isolated from local machine history unless the test is explicitly about history fallback.
