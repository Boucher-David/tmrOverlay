# Design V2 Mocks

Generated mac-harness proving-ground screenshots for design-v2 overlay primitives.

- `design-v2-states.png` is the contact sheet for telemetry-first overlay states.
- `design-v2-components-outrun.png` is the contact sheet for component-level outrun theme review.
- `components/outrun/` contains individual component review cards rendered from the same mac-harness overlay view used by the live component demo.
- `states/standings-telemetry.png` shows a simple timing-board window into iRacing standings telemetry.
- `states/relative-telemetry.png` shows a focus-centered relative table.
- `states/flag-display.png` shows a minimal race-control/flag state.
- `states/analysis-exception.png` shows where source/evidence labels still belong: derived analysis products such as fuel, radar, gap, and strategy.

This folder is not the primary mac settings surface. The converted mac settings window now treats Design V2 as its main design; this folder remains the mac-only proving ground for shared overlay primitives before the Windows overlays adopt the same semantics. The default design direction is direct telemetry first, including local in-car radar. Confidence/source chrome should be quiet unless a value is stale, unavailable, modeled, or derived.

Component review artifacts are generated from the same mac-harness views used by the live Design V2 component overlay. The current theme preserves the default product direction; the outrun theme stress-tests the same semantic roles with a bolder review palette.

Design V2 review should pair new visual language with current production content contracts. For example, Relative visual mocks should still respect the current Relative content manager defaults and optional fields instead of adding visual-only columns. Only diverge from current production content when the mock is explicitly reviewing a proposed content change.
