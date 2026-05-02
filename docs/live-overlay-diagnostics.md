# Live Overlay Diagnostics

`live-overlay-diagnostics.json` is a passive observer artifact for the current fuel, radar, and gap overlays. It is intended to test overlay assumptions from real sessions before model-v2 behavior changes are made.

The recorder does not change overlay output. It watches normalized live snapshots and writes bounded summaries for:

- gap semantics: race vs non-race frames, class-gap source counts, large gaps, gap jumps, class row availability, and pit context
- radar semantics: focus kind, side-signal frames, side signal without placement candidates, timing-only vs spatial placement coverage, nearby-car counts, and multiclass approach frames
- fuel semantics: valid level frames, instantaneous burn frames, burn-without-level frames, team timing without local fuel, pit context, and driver-control changes
- position cadence: sampled position/class-position changes that happen before the car completes another lap

## Output Locations

When raw capture is active, the artifact is written beside the raw-capture sidecars:

```text
captures/capture-*/live-overlay-diagnostics.json
```

When raw capture is not active, it is written under the logs root:

```text
%LOCALAPPDATA%\TmrOverlay\logs\overlay-diagnostics\*-live-overlay-diagnostics.json
```

The mac harness mirrors this path under `~/Library/Application Support/TmrOverlayMac/logs/overlay-diagnostics/` and can also write into mock capture directories.

## Guardrails

- Enabled by default, bounded by sampled frame and event caps.
- Best-effort: failures are logged and must not stop live telemetry, history, raw capture, IBT analysis, or overlays.
- Additive: older captures without this file remain valid.
- Diagnostic only: event counts are evidence for future branches, not automatic behavior changes.

Use it with `live-model-parity.json`, `ibt-analysis/ibt-local-car-summary.json`, and `captures/_analysis/raw-capture-overlay-assumptions.json` when deciding whether model v2 is ready to power overlays.
