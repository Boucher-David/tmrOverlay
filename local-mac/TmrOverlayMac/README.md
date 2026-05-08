# TmrOverlayMac

Tracked macOS development harness for the Windows `TmrOverlay.App`.

This folder is tracked source so mock-telemetry UI parity, screenshot generation, and local review demos can evolve with the Windows app. Generated `.build` output, local app data, captures, logs, and screenshots stay ignored.

## Run

```bash
./run.sh
```

From the repo root, use the ignored convenience wrapper:

```bash
./run.sh
```

The app creates a macOS menu-bar item, shows the current review overlay set plus the centered settings panel, and writes compact mock session history under app-owned local storage:

```text
~/Library/Application Support/TmrOverlayMac/history/user
```

The live mock uses the tracked four-hour Nürburgring race shape at 4x speed so fuel, standings, relative, track-map, simple telemetry, radar, and gap-to-leader overlays can be inspected without waiting through a full stint. The standings and track-map overlays mirror the production Windows surfaces while keeping mock-only timing and placeholder geometry available for fast visual iteration.

The mac harness also records bounded live overlay diagnostics while mock/demo overlays run. These files are written under:

```text
~/Library/Application Support/TmrOverlayMac/logs/overlay-diagnostics
```

Mock raw-capture sessions can also write `live-overlay-diagnostics.json` into the mock capture directory. This mirrors the Windows observer artifact for gap/radar/fuel/position-cadence assumptions, but it is still development evidence rather than production telemetry.

Raw mock captures are disabled by default. Enable them only when you want to exercise the raw capture writer and capture-health UI:

```bash
TMR_MAC_RAW_CAPTURE_ENABLED=true ./run.sh
```

When raw capture is enabled, mock artifacts are written under:

```text
~/Library/Application Support/TmrOverlayMac/captures
```

Use repo-local ignored storage only when you explicitly want throwaway development files in this folder:

```bash
TMR_MAC_USE_REPOSITORY_LOCAL_STORAGE=true ./run.sh
```

Override the whole app data root with:

```bash
TMR_MAC_APP_DATA_ROOT=/absolute/path ./run.sh
```

Override the capture root with:

```bash
TMR_MAC_CAPTURE_ROOT=/absolute/path ./run.sh
```

Override that with:

```bash
TMR_MAC_HISTORY_ROOT=/absolute/path ./run.sh
```

The raw mock capture emits the same artifact names and `telemetry.bin` framing as the Windows collector, but it does not connect to iRacing.

The mac harness also mirrors the Windows app boilerplate: persisted overlay settings, rolling logs, JSONL app events, runtime-state markers, diagnostics bundles, and retention cleanup. Runtime raw-capture requests live in the Support settings tab, matching Windows. The menu-bar item includes entries to open captures, open logs, open settings, and create diagnostics bundles.

## Overlay Health Demo

Launch with demo states enabled to preview the health/error overlay without needing iRacing:

```bash
TMR_MAC_DEMO_STATES=true ./run.sh
```

This cycles through waiting-for-sim, connected-without-capture, healthy live-analysis, healthy raw-capture, stale build, dropped-frame, frames-not-written, disk-stalled, and writer-error states. The menu-bar item also includes manual demo entries plus a clear option.

## Radar Capture Demo

Launch the app with capture-derived radar scenarios:

```bash
swift run TmrOverlayMac --radar-capture-demo
```

or:

```bash
TMR_MAC_RADAR_CAPTURE_DEMO=true ./run.sh
```

This opens four individual radar windows fed with fresh snapshots generated from `captures/capture-20260426-130334-932`: dense start, side-callout, pit-exit multiclass, and zero-timing traffic examples. These demo windows also draw each rendered car's signed gap to the center car for radar debugging.

To compare the current timing-row rendering with the conservative collision-free idea side by side:

```bash
TMR_MAC_RADAR_CAPTURE_COMPARE=true ./run.sh
```

Comparison windows show old timing rows, conservative de-overlap, and wide-row grouping variants.

## Capture-Backed Overlay Replay

The current mac harness can run four-hour mock overlays and capture-derived radar/gap demos with live diagnostics. It does not yet replay an arbitrary 24-hour raw capture through all overlay types. That should be added as a future development-only replay provider that decodes existing raw captures into normalized live snapshots at high playback speed, drives one overlay of each type, and saves screenshots plus `live-overlay-diagnostics.json`.

## Design V2 Proving Ground

The mac screenshot target also renders a design-v2 proving ground under:

```text
mocks/design-v2
```

Those previews are separate from the production Relative overlay screenshots under `mocks/relative/`. They exercise future telemetry-first standings, relative, local in-car radar direction, flag display, table primitives, and narrow analysis-exception states before shared primitives are promoted back into Windows. Source/evidence chrome should stay quiet for normal telemetry and appear when data is stale, unavailable, modeled, or derived.

The component-review path uses the same views for live review and generated artifacts. The current theme preserves the existing low-noise visual direction, while the outrun theme is a bolder review palette for token stress testing.

Render only the design-v2 contact sheet with:

```bash
TMR_MAC_SCREENSHOT_ONLY_DESIGN_V2=true swift run TmrOverlayMacScreenshots
```

Render only the Design V2 component review artifacts with:

```bash
TMR_MAC_SCREENSHOT_ONLY_DESIGN_V2_COMPONENTS=true swift run TmrOverlayMacScreenshots
```

Open the live mac-harness Design V2 component overlay with:

```bash
TMR_MAC_DESIGN_V2_COMPONENTS_DEMO=outrun ./run.sh
```

Use `TMR_MAC_DESIGN_V2_COMPONENTS_DEMO=current ./run.sh` to review the same component primitives against the current/default token set.

## Tests

The mac harness is split into a testable `TmrOverlayMacCore` target plus a tiny executable target. Run tests with:

```bash
swift test
```

This requires a Swift/Xcode toolchain that provides `XCTest`. The Command Line Tools-only install on this machine can build the app, but cannot currently run the XCTest target.
