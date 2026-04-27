# Overlay Mocks

This folder stores overlay mock images and generated preview screenshots for user feedback and design review.

Each overlay type should get its own subfolder:

```text
mocks/
  fuel-calculator/
  car-radar/
  gap-to-leader/
  standings/
  relative/
  competition-distance-graph/
  weather/
```

These files are review artifacts, not publish output. Static design mocks and generated mac-harness screenshots can both live here, but keep them small enough to review in git.

When adding or materially changing an overlay, update that overlay's mock folder with:

- one focused screenshot of the main expected live state
- one multi-state contact sheet covering waiting/unavailable, normal/healthy, edge/warning, and error/fallback behavior when those states apply
