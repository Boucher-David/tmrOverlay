# Overlay Logic Docs

These files explain the current overlay behavior in English so design changes can be proposed against readable logic instead of C#.

They should be updated whenever overlay behavior, telemetry derivation, analysis rules, visibility rules, or user-facing state transitions change.

## Overlay Logic

- [Status Overlay Logic](status-overlay-logic.md)
- [Settings And Overlay Manager Logic](settings-overlay-logic.md)
- [Fuel Calculator Logic](fuel-calculator-logic.md)
- [Standings Overlay Logic](standings-overlay-logic.md)
- [Relative Overlay Logic](relative-overlay-logic.md)
- [Track Map Overlay Logic](track-map-overlay-logic.md)
- [Garage Cover Overlay Logic](garage-cover-overlay-logic.md)
- [Simple Telemetry Overlays Logic](simple-telemetry-overlays-logic.md)
- [Car Radar Logic](car-radar-logic.md)
- [Gap To Leader Logic](gap-to-leader-logic.md)

## Related Analysis Logic

- [Live Model Groundwork](live-model-groundwork.md)
- [Live Overlay Diagnostics](live-overlay-diagnostics.md)
- [24-Hour Live Overlay Findings](live-overlay-24h-findings.md)
- [Model V2 Future Branch Notes](model-v2-future-branches.md)
- [Edge-Case Telemetry Logic](edge-case-telemetry-logic.md)
- [IBT Analysis](ibt-analysis.md)
- [Post-Race Analysis Logic](post-race-analysis-logic.md)

## Deferred UI Style V2

The current model-v2 work standardizes live data, not visual structure. Relative, Standings, Track Map, Car Radar, and the simple Flags, Session / Weather, Pit Service, and Input / Car State overlays now consume model-v2 state directly for their promoted contracts. Treat overlay UI/style v2 as telemetry-first by default: standings, relative, local in-car radar/blindspot, flags, session/weather context, and timing tables should be dense, stable windows into iRacing telemetry. Normal rows should not look like confidence reports, and persistent source footers should be treated as validation/admin chrome rather than default end-user overlay furniture. Normalized source, quality, usability, freshness, and missing-reason states should become exception UI for stale, unavailable, modeled, or derived values, especially in analysis products like fuel strategy, non-local radar focus/multiclass interpretation, gap graphs, and a future pit crew/engineer control overlay. Competitor overlay analysis remains the product-shape check: small purpose-built overlays, dense information, low-noise dark styling, and semantic color rather than one monolithic dashboard. The tracked mac harness is the proving ground for generated `mocks/design-v2/` states while model-v2 evidence is still being collected, including the current standings, relative, sector comparison, blindspot signal, laptime delta, stint laptime log, flag, and analysis-exception previews. Design V2 also has current/default and outrun token sets plus generated component-review artifacts for overlay shells, controls, buttons, status pills, table rows, graph chrome, localhost blocks, sidebar tabs, section panels, and settings content blocks. The mac V2 settings shell is now the visual reference for the application settings window, but the current Windows settings code remains the legacy WinForms UI and will not visually match until Windows gets an explicit V2 surface. Port Windows as an additive WinForms V2 layer over the existing `ApplicationSettings`, `OverlaySettings`, content descriptors, browser-source routes, and support workflows rather than replacing those contracts. Keep the Windows migration additive and validate one tab or primitive family at a time with screenshots.

Race-data overlays opt into a shared live-telemetry fade through `OverlayAvailabilityEvaluator`. When the app is disconnected, not collecting, or the latest normalized live snapshot is stale, those overlays fade out instead of retaining stale race state on screen. Native overlays, simple telemetry view models, Garage Cover detection, and localhost browser sources now use the same connected/collecting/fresh terminology where practical. Status and Stream Chat are not treated as race-data overlays for this policy. Garage Cover is localhost-only and still fails closed to the configured cover or fallback when telemetry is unavailable, stale, or the Garage screen is visible.

## Maintenance Rule

When implementation changes:

1. Update the matching logic doc in the same pass.
2. Search docs, mocks, tests, skills, and the mac harness for old behavior names or old assumptions.
3. Regenerate screenshots when visual overlay behavior changed.
4. Run screenshot validation after regenerated artifacts are written.
