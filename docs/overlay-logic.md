# Overlay Logic Docs

These files explain the current overlay behavior in English so design changes can be proposed against readable logic instead of C#.

They should be updated whenever overlay behavior, telemetry derivation, analysis rules, visibility rules, or user-facing state transitions change.

## Overlay Logic

- [Status Overlay Logic](status-overlay-logic.md)
- [Settings And Overlay Manager Logic](settings-overlay-logic.md)
- [Fuel Calculator Logic](fuel-calculator-logic.md)
- [Car Radar Logic](car-radar-logic.md)
- [Gap To Leader Logic](gap-to-leader-logic.md)

## Related Analysis Logic

- [Live Model Groundwork](live-model-groundwork.md)
- [Edge-Case Telemetry Logic](edge-case-telemetry-logic.md)
- [IBT Analysis](ibt-analysis.md)
- [Post-Race Analysis Logic](post-race-analysis-logic.md)

## Maintenance Rule

When implementation changes:

1. Update the matching logic doc in the same pass.
2. Search docs, mocks, tests, skills, and the mac harness for old behavior names or old assumptions.
3. Regenerate screenshots when visual overlay behavior changed.
4. Run screenshot validation after regenerated artifacts are written.
