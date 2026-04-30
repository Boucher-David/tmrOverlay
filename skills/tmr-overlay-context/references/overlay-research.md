# Overlay Research

Last updated: 2026-04-27

## External Reference Reviewed

Reviewed:

- `https://ioverlay.app/`

Purpose of the review:

- understand common overlay types used by iRacing drivers
- understand visual density and placement patterns
- inform the first useful overlays without copying implementation details

## Overlay Types Noted

From the public site and help pages, the reference product appears to support multiple overlay families such as:

- standings
- relative
- fuel
- track map
- flat map
- spotter / incident indicator
- pit entry / pit exit helper
- multiclass traffic indicator
- Twitch chat
- garage/setup cover
- race control

That confirms there is a mature market expectation for multiple small purpose-built overlays rather than one monolithic dashboard.

## Visual Patterns Worth Keeping In Mind

Observed design tendencies:

- compact widgets intended for corners and screen edges
- dark translucent panels rather than bright chrome
- dense information layout with strong semantic color
- tables for standings/relative
- compact matrix-style layouts for fuel and strategy information
- multiple overlays visible at once rather than one full-screen surface

## Fuel Overlay Takeaways

The most relevant external reference is the fuel overlay direction.

The useful pattern is not just:

- current fuel

It is usually a combination of:

- current fuel
- recent / rolling consumption
- projected stint length
- projected laps remaining
- refuel target or fuel-to-finish style guidance
- pit-window or strategy cues

That aligns with the current direction for `tmrOverlay`.

## Practical Constraints Mentioned In The Research

Public help/FAQ notes from the reference product suggested:

- overlays are intended for borderless or windowed mode
- incomplete max-cars settings can reduce telemetry completeness for position/fuel-related features

That is useful context for future troubleshooting if a user reports missing live data in larger sessions.

## Design Direction For This Repo

Use the research as a benchmark for usefulness, not as a design template.

Good direction for `tmrOverlay`:

- small, purpose-driven overlays
- low-noise dark styling
- clear semantic state color
- prioritize live decision support over decorative telemetry
- build fuel/stint first, then expand through small race-context overlays such as radar, leader gap, relative, and strategy views

## Current Expansion Notes

The first post-fuel overlays are now bootstrapped:

- radar: transparent 300px circular proximity view; scalar side occupancy from `CarLeftRight`, plus fresh live nearby car rectangles from `CarIdx*` progress converted to physical distance when track length is known, with reliable `CarIdxEstTime`/`CarIdxF2Time` as timing fallback. Car rectangles stay neutral white, fade in between radar entry and the yellow-warning threshold, and move through yellow toward saturated alert red only inside the close bumper-gap warning buffer; side slots are only attached to radar cars inside the side-overlap contact window, and the overlay itself fades in/out. Faster approaching multiclass traffic can show a short outer warning arc with a live seconds gap when it is behind outside the 2-second timing fallback range but within 5 seconds.
- gap-to-leader: rolling four-hour in-class inverse line graph; class leader is the top baseline, all available same-class timing rows are retained in a bounded overlay-local in-memory render buffer, and the currently rendered set is selected dynamically as leader, focused/reference car, nearest same-class cars ahead/behind, plus recently visible cars that need continuity as they enter/leave the nearby window. The Windows live path keeps gap graph timing rows separate from radar proximity rows so standings/F2 data can still be graphed when lap-distance progress is unavailable. The visible history anchors at the left edge and grows horizontally until the four-hour window is full, Y-axis scale follows the visible field spread, left-side axis labels avoid covering lines, whole-lap gap reference lines appear when the field spreads by a lap or more, vertical 5-lap markers show visible-history duration, and line endpoints show compact `P<N>` class-position labels.
- gap-to-leader odd-event policy: the class-leader baseline is a role, not a fixed car. When the leader changes, the old leader's car line should continue downward as a normal trace, the new leader becomes the baseline reference, and a compact leader-change marker should explain the reset. Cars with missing telemetry should end their line cleanly instead of connecting across gaps; recently visible cars should fade/dash out after a sticky window so crashes, spins, disconnects, or position losses remain understandable.
- gap-to-leader weather and driver context: weather should render as subtle full-height vertical bands behind the graph from live wetness/declared-wet telemetry. Driver changes should preserve each line's color and add compact ticks/dots on the affected line. Windows should infer real driver swaps from session-info `DriverInfo.Drivers[]` changes by `CarIdx`, with `DCDriversSoFar` as the explicit local-team signal. Mac may use named mock handoffs for visual iteration.

## Development Boundary

Treat the Windows app as production-facing during overlay development: keep it conservative, real-data-driven, and free of mock offsets or named sample assumptions. The ignored mac harness should mirror feature behavior, but it can be much looser about data generation, including fixed preview race offsets, named mock drivers, synthetic all-class timing rows, artificial weather windows, and exaggerated graph events that help validate visuals quickly.

The radar should be treated as an early proximity surface until we collect enough side-by-side traffic samples to validate lateral behavior beyond the scalar left/right signal.
