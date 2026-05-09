# Live Overlay Diagnostics

`live-overlay-diagnostics.json` is a default-on passive observer artifact for the current fuel, radar, gap, timing, and design-v2 candidate overlays. It is intended to test overlay assumptions from real sessions before model-v2 behavior changes are made while keeping raw capture opt-in.

The recorder does not change overlay output. It watches normalized live snapshots and writes bounded summaries for:

- gap semantics: race vs non-race frames, class-gap source counts, large gaps, gap jumps, class row availability, and pit context
- radar semantics: focus kind, local-only suppression, pit/garage unavailability, local progress gaps, side-signal frames, side signal without placement candidates, timing-only vs spatial placement coverage, nearby-car counts, and multiclass approach frames
- fuel semantics: valid level frames, instantaneous burn frames, burn-without-level frames, team timing without local fuel, pit context, and driver-control changes
- position cadence: sampled position/class-position changes that happen before the car completes another lap
- lap-delta readiness: availability and `_OK` usability counts for live iRacing delta channels such as best lap, optimal lap, session best, session optimal, and session last lap
- relative lap-relationship probe: diagnostics-only counts for official completed-lap relationships among nearby cars, pit-road relationship counts, and same-lap cars near the wrap where a future branch might infer "about to lap" or "about to be lapped" behavior
- sector-timing readiness: session-info sector metadata coverage, focus/ahead/behind progress coverage, missing lap-counter frames, synthetic start/finish wraps, progress discontinuities, derived sector-boundary crossings, and bounded examples of completed sector intervals derived from car progress
- track-map sector highlights: model-v2 sector availability, live timing frames, personal-best sector frames, best-lap sector frames, full-lap highlight frames, and highlight counts by status

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

- Enabled by default for tester builds; disable it with `LiveOverlayDiagnostics:Enabled=false` or a `TMR_LiveOverlayDiagnostics__Enabled=false` override when a session should not write this observer artifact.
- Bounded by sampled frame and event caps.
- Event examples are exact-duplicate suppressed and capped per kind before the global cap, so a stable condition such as a multi-lap class gap cannot crowd out unrelated radar/fuel/position examples.
- Radar event examples include the focus kind, raw `CarLeftRight`, raw nearby-car count, whether the production radar had data, and nearby/timing/spatial row counts. This lets suppressed spectator/teammate focus and other partial radar cases be reviewed from the capture without making them normal overlay UI.
- Relative lap relationship examples are probe-only. They use raw nearby `CarIdxLapCompleted` and `CarIdxLapDistPct` against the current focus/player progress to help decide a later Relative V2.5 color treatment; current overlays do not consume these counts.
- Sector timing interval examples are derived diagnostics only for future timing-table work. They can use valid `LapDistPct` when lap counters are unavailable, but large reset-style progress jumps are counted as discontinuities instead of completed sectors. Track Map sector highlight state is now a production model-v2 contract under `LiveTelemetrySnapshot.Models.TrackMap`.
- Best-effort: failures are logged and must not stop live telemetry, history, raw capture, IBT analysis, or overlays.
- Additive: older captures without this file remain valid.
- Diagnostic only: event counts are evidence for future branches, not automatic behavior changes.

Use it with `live-model-parity.json`, `ibt-analysis/ibt-local-car-summary.json`, and `captures/_analysis/raw-capture-overlay-assumptions.json` when deciding whether model v2 is ready to power overlays.

Settings/Flags freeze triage is intentionally separate from `live-overlay-diagnostics.json`. Diagnostics bundles include `metadata/ui-freeze-watch.json`, which summarizes rolling performance data for settings save/apply churn, UI timer lateness, flags refresh/render timings, and overlay window click-through/topmost/no-activate/input-intercept state. Use that file with `metadata/performance.json` and recent logs when validating Windows reports where enabling Flags or Standings appears to freeze or block the Settings UI.

Installer/update residue triage is also bundled separately. Diagnostics bundles include `metadata/installer-cleanup.json`, which records whether startup legacy cleanup ran, which stale `TechMatesRacing.TmrOverlay` package folders or shortcuts were removed, and which paths were skipped. Use it when a tester reports that a Start Menu or Desktop shortcut opens an older installed build after an MSI update.
