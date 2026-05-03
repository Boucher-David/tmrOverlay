# Post-Race Strategy Analysis Design Note

The first implementation is intentionally narrow: session finalization builds a compact analysis from the saved historical summary, stores it as JSON, and the settings overlay can browse recent analyses plus the built-in four-hour sample. The broader strategy review/export flow remains post-v1.0 work.

For the current implementation's step-by-step English logic, see [post-race-analysis-logic.md](post-race-analysis-logic.md).

## Goal

After a race ends, generate a compact strategy review that explains how the race went and what realistic alternatives may have existed. The review should help users learn from their own sessions and give us a shareable, privacy-conscious artifact for improving the fuel algorithm.

## Candidate Output

Current implementation stores post-race analysis JSON under:

```text
%LOCALAPPDATA%/TmrOverlay/history/user/
  analysis/
    {analysis-id}.json
```

The file stays compact and derived. It does not include raw telemetry frames.

## Data To Include

- Session identity: car, track, session type, team-race flag, source id, app version.
- Race outcome context: estimated race laps, completed laps, overall/class position when available.
- Actual execution: stint lengths, stop count, pit-lane time, pit-stall/service time, tire-change indicators, fuel added, driver-role confidence.
- Fuel model: live/user-history/baseline source, min/avg/max burn, fuel saving required for key targets.
- Pace model: local/team lap pace, overall leader pace, class leader pace, gaps where available.
- Recommendation timeline: only meaningful recommendation changes, not every frame.
- Alternative strategies: stop-count deltas, estimated time deltas, fuel-saving requirements, tire-service tradeoffs.
- Confidence and caveats: teammate stint fuel unavailable, inferred tire changes, low-distance sample, stale/missing scalar fuel, dropped frames.

## Example Questions To Answer

- Did the chosen stint rhythm add stops versus the longest realistic rhythm?
- Could the final stop have been avoided with realistic fuel saving?
- Were tire changes likely free under refueling time, or did they cost stationary time?
- Did teammate/user stint lengths differ enough to change the optimal plan?
- Did leader pace or class leader pace materially change the estimated lap count?
- Did the app recommendation change early enough for the user to act?

## Shareable Feedback Artifact

Add an explicit export path later, likely via diagnostics or a dedicated tray item. The export should include:

- session summary
- strategy analysis
- app event breadcrumbs
- relevant app settings and version metadata

The export should exclude:

- raw `telemetry.bin`
- full session YAML unless needed and sanitized
- personally identifying driver/team fields unless the user explicitly opts in

## Implementation Notes

- `src/TmrOverlay.Core/Analysis/PostRaceAnalysisBuilder.cs` builds the first compact line-based analysis from `HistoricalSessionSummary`.
- `src/TmrOverlay.App/Analysis/PostRaceAnalysisStore.cs` persists and loads analysis JSON.
- `src/TmrOverlay.App/Analysis/PostRaceAnalysisPipeline.cs` isolates analysis persistence/event failures from telemetry finalization.
- Settings no longer exposes a user-facing Post-race Analysis tab in v0.8; future post-race analysis browsing should return through a dedicated analysis/product surface instead of the current overlay settings list.
- Record recommendation snapshots only when strategy output changes materially: stint count, stop count, target laps, required saving, tire advice, or confidence source.
- Keep the live overlay independent from post-race analysis. The live path should publish derived strategy snapshots; the analysis writer can subscribe to or receive those snapshots.
- Use the same source/confidence language as session history so teammate-mode model data is never treated as measured fuel.
- Prefer additive schema versions. The first implementation can be a narrow v1 JSON file and grow as more telemetry cases are understood.

## Not Implemented Yet

- Building the exporter UI.
- Building a full strategy comparison engine beyond the current fuel calculator logic.
- Uploading or syncing user race data.
- Replaying raw captures to regenerate strategy timelines.
