# Relative Overlay Logic

The Relative overlay is the first production overlay using the model-v2 live state as its primary input. It is intentionally a simple telemetry-first window: nearby cars, the local/reference row, and quiet source text only when the source is waiting, partial, or timing fallback.

## Code Paths

- `src/TmrOverlay.App/Overlays/Relative/RelativeOverlayDefinition.cs`
- `src/TmrOverlay.App/Overlays/Relative/RelativeOverlayViewModel.cs`
- `src/TmrOverlay.App/Overlays/Relative/RelativeForm.cs`
- `src/TmrOverlay.Core/Telemetry/Live/LiveRaceModelBuilder.cs`

The mac harness mirrors the surface in `local-mac/TmrOverlayMac/Sources/TmrOverlayMac/Overlays/Relative/` using mock proximity telemetry for local design iteration.

## Inputs

The Windows overlay reads:

- `LiveTelemetrySnapshot.Models.Relative` for relative rows and reference car index.
- `LiveTelemetrySnapshot.Models.Timing` for the reference row position/class context.
- `LiveTelemetrySnapshot.Models.DriverDirectory` for car number, driver name, class label, and class color.
- Snapshot connection, collection, sequence, and freshness metadata.

It treats snapshots older than 1.5 seconds as stale and shows a waiting state instead of keeping old rows visible.

## Row Selection

`RelativeOverlayViewModel.From` builds:

1. Cars ahead from `LiveRelativeRow.IsAhead`.
2. Cars behind from `LiveRelativeRow.IsBehind`.
3. One reference row from `Relative.ReferenceCarIdx`, then timing focus/player fallback.

Cars ahead and behind are sorted by the best available absolute relative value:

1. Relative seconds.
2. Relative meters.
3. Relative laps.

The nearest configured cars are selected first. Ahead rows are then rendered farthest-to-nearest above the reference row, while behind rows render nearest-to-farthest below it. User settings cap each side from 0 to 8 cars, defaulting to 5 ahead and 5 behind.

## Display Rules

Rows show:

- Class position first, then overall position, otherwise `--`.
- `#<car number> <driver>` when a car number is known.
- `0.000` for the reference gap.
- `-<value>` for cars ahead and `+<value>` for cars behind, regardless of whether the source row came from proximity or timing fallback.
- Seconds first, then meters, then lap fraction.
- Class detail, with `PIT` appended for pit-road rows.

Normal rows are quiet. Reference rows are visually emphasized; pit rows use warning color; fully degraded rows are muted.

## Source And Status Text

Status shows the reference position and visible car count, such as `C6 - 10 cars`. If more relative rows are available than the configured window shows, it uses `shown/available`, such as `C6 - 10/14 cars`.

Source text is intentionally low emphasis:

- `source: live proximity telemetry` when rows come from proximity.
- `source: model-v2 timing fallback` when relative rows come from timing/class-gap fallback.
- `source: partial timing` when rows are fully degraded or partial.
- `source: waiting` when no fresh relative telemetry exists.

## Waiting And Error States

The overlay shows waiting when:

- iRacing is disconnected.
- Live collection has not started.
- The latest snapshot is stale.
- There is no reference row and no relative row.

Unexpected refresh/render failures are logged through the overlay logger and surfaced as a compact visible `relative error` state.

## Managed Overlay Behavior

`OverlayManager` registers Relative as a normal managed driving overlay:

- Default id: `relative`.
- Default size: `520x360`.
- Default position: `(24, 530)`, below the fuel calculator.
- Settings expose cars ahead and cars behind.
- Shared font, visibility, scale, opacity, session filters, and persistence follow the same managed-overlay behavior as the other product overlays.
