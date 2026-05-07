# Relative Overlay Logic

The Relative overlay is the first production overlay using the model-v2 live state as its primary input. It is intentionally a simple telemetry-first window: nearby cars, the local/reference row, and quiet source text only when the source is waiting, partial, or timing fallback.

## Code Paths

- `src/TmrOverlay.App/Overlays/Relative/RelativeOverlayDefinition.cs`
- `src/TmrOverlay.App/Overlays/Relative/RelativeBrowserSource.cs`
- `src/TmrOverlay.App/Overlays/Relative/RelativeBrowserSettings.cs`
- `src/TmrOverlay.App/Overlays/Relative/RelativeOverlayViewModel.cs`
- `src/TmrOverlay.App/Overlays/Relative/RelativeForm.cs`
- `src/TmrOverlay.Core/Telemetry/Live/LiveRaceModelBuilder.cs`

The mac harness mirrors the surface in `local-mac/TmrOverlayMac/Sources/TmrOverlayMac/Overlays/Relative/` using mock proximity telemetry for local design iteration.

## Inputs

The Windows overlay reads:

- `LiveTelemetrySnapshot.Models.Relative` for relative rows and reference car index.
- `LiveTelemetrySnapshot.Models.Timing` for the reference row position/class context and pit-road status.
- `LiveTelemetrySnapshot.Models.Scoring` for scoring-only enrichment of rows that already exist in relative telemetry.
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

The live table uses fixed slots once a reference row exists: configured ahead slots, the reference row, then configured behind slots. Empty slots stay blank so the reference row and table height do not jump when nearby cars enter or leave. This lets live numbers update in place instead of forcing the whole relative table to re-layout every tick. Waiting and unavailable states still collapse to one placeholder row.

The reference row stays between the selected ahead and behind rows instead of drifting into a standings-style sort.

Scoring snapshot rows do not create Relative rows. If a car exists only in scoring/results data, it is deliberately excluded from Relative because there is no honest live relative placement for it. Scoring can only enrich cars that already have relative/timing placement.

Pit-road cars are allowed to stay in Relative when they come from live nearby-car telemetry. Radar and spatial placement still exclude them, but Relative keeps them visible so the driver can see pit-lane cars being passed.

## Display Rules

Rows show:

- Class position first, then overall position, otherwise `--`. The first cell includes a small class-color bar so class color does not require a separate table column.
- `#<car number> <driver>` when a car number is known.
- `0.000` for the reference gap.
- `-<value>` for cars ahead and `+<value>` for cars behind, regardless of whether the source row came from proximity or timing fallback.
- Seconds first, then meters, then lap fraction.
- Class detail, with `PIT` appended for pit-road rows.

Normal rows are quiet. Reference rows are visually emphasized; pit rows remain visible but are de-emphasized with muted text/background so the driver can see they are being passed in pit lane rather than treated as an on-track threat. Fully degraded rows are muted.

Relative seconds come from live proximity timing when available. If proximity has only lap-distance placement, the model-v2 relative row can infer a display seconds gap from live lap-distance delta multiplied by the current local/focus lap-time signal. Radar does not consume that inferred seconds value; it remains stricter and uses only live proximity seconds or physical distance for proximity placement.

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
- Theme font, visibility, scale, opacity, session filters, and persistence follow the same managed-overlay behavior as the other product overlays.

The localhost browser-source route also reads `/api/relative` so it can honor the same cars-ahead and cars-behind settings as the native overlay.
