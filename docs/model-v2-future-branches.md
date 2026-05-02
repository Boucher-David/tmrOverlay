# Model V2 Future Branch Notes

This investigation branch should stay focused on evidence and passive tooling. It adds model-v2 contracts, parity artifacts, IBT local-car analysis, raw-capture assumption analysis, and live overlay diagnostics without changing current overlay behavior.

Current overlays still read the legacy fuel/proximity/gap slices. The new `LiveTelemetrySnapshot.Models` layer and diagnostics artifacts are review inputs for the next implementation branches.

## This Branch Scope

- Keep raw capture, IBT logging, capture synthesis, IBT sidecars, model parity, and live overlay diagnostics enabled by default with bounded output and failure isolation.
- Preserve compatibility with already collected raw captures and synthesized data. New sidecars are additive; older captures remain readable.
- Record enough evidence to decide future overlay behavior from data:
  - `live-model-parity.json` for model-v2 coverage/mismatch and promotion-readiness.
  - `live-overlay-diagnostics.json` for gap/radar/fuel/position-cadence assumptions that came from the 24-hour race.
  - `ibt-analysis/ibt-local-car-summary.json` for local-car trajectory, fuel, and vehicle-dynamics readiness.
  - `tools/analysis/analyze_capture_assumptions.py` for offline raw-capture checks, including sampled intra-lap position/class-position changes.
- Use the existing `live_model_v2_promotion_candidate` app event as the first "enough evidence to review cutover" signal. It is not an automatic migration trigger.

## Future Branches

### Fuel Strategy V2

Rebuild strategy around a team-stint model rather than stitched scalar fuel. The model should combine local measured fuel windows, team/focus progress, completed stint lengths, pit/service history, max-fuel constraints, and explicit measured/model/source labels.

Do this before trusting time-saving stint suggestions. Strategy output should reject impossible or misleading stint rhythms when current-session stints and historical evidence disagree with a shorter suggestion.

### Radar Focus And Multiclass V2

Move radar placement and multiclass warning to model-v2 focus-relative evidence. Keep `CarLeftRight` local-player scoped, suppress or relabel it for non-player focus, and make teammate/team-car focus states explicit.

This branch should also define what the radar displays when spatial progress is missing but timing rows are valid.

### Gap Graph V2

Split race-position gap behavior from practice/qualifying/test timing behavior. Race sessions can use leader/class gap semantics; non-race sessions need a separate timing mode or should stay hidden by default.

Improve readability for multi-lap gaps by separating leader-gap context from local-battle deltas, using lap-aware scaling, or moving threat/leader interpretation into future relative/standings/strategy overlays.

### Position Cadence And Timing Tables

Use raw-capture and live-overlay diagnostics to confirm whether `CarIdxPosition` and `CarIdxClassPosition` update intra-lap often enough for standings/relative overlays. If confirmed across enough sessions, promote position cadence into model-v2 timing-table assumptions.

### Capture-Backed Mac Overlay Replay

The mac harness now records live overlay diagnostics from mock/demo snapshots, including the four-hour preview and capture-derived radar/gap demos. A future harness branch should add a full raw-capture replay provider that decodes selected 4-hour/24-hour captures into normalized live snapshots at high playback speed, drives one instance of each overlay, and writes screenshots plus `live-overlay-diagnostics.json` from that replay.

That replay provider should be a development tool only. It should read existing captures, skip or downsample aggressively, and avoid changing the Windows collector/runtime path.

### Uniform Model V2 Migration

After several clean `live_model_v2_promotion_candidate` sessions cover race, practice/test, pit cycles, driver swaps, focus changes, multiclass traffic, and large-gap cases, migrate overlays one at a time to `LiveTelemetrySnapshot.Models`.

Keep migration additive:

1. Switch one overlay to model-v2 inputs behind tests.
2. Keep legacy slice fields stable until no current overlay depends on them.
3. Compare screenshots and captured sidecars before removing old overlay-local interpretation.

### Overlay UI/Style V2

Model v2 does not standardize visual code. A separate UI/style branch should add shared semantic theme tokens and reusable WinForms primitives for headers, status badges, source footers, metric rows, tables, graph panels, borders, class/severity colors, text fitting, and empty/error/waiting states.

Migrate style one overlay at a time with screenshot validation.
