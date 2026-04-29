# Post-Race Analysis Logic

This file explains the current post-race analysis in English so design feedback can be made against the logic without reading C#.

Implementation files:

- `src/TmrOverlay.App/Telemetry/TelemetryCaptureHostedService.cs`
- `src/TmrOverlay.App/Analysis/PostRaceAnalysisPipeline.cs`
- `src/TmrOverlay.App/Analysis/PostRaceAnalysisStore.cs`
- `src/TmrOverlay.Core/Analysis/PostRaceAnalysisBuilder.cs`
- `src/TmrOverlay.Core/Analysis/PostRaceAnalysisModels.cs`
- `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`

## Current Shape

The current implementation is intentionally narrow. It is not a full race-strategy engine yet. It turns the compact `HistoricalSessionSummary` saved at session end into a short line-based report, stores that report as JSON, and shows the newest report in the settings window.

It uses derived summary data only. It does not replay raw telemetry frames, it does not inspect every strategy recommendation that appeared during the race, and it does not compare many possible race plans yet.

## When It Runs

1. A live telemetry collection ends.
2. `TelemetryCaptureHostedService.FinalizeCollectionAsync` closes any active raw capture.
3. The service asks `HistoricalSessionAccumulator` to build a `HistoricalSessionSummary`.
4. The session summary is saved to user history.
5. `PostRaceAnalysisPipeline.SaveFromSummaryAsync` is called with that same summary.
6. The pipeline asks `PostRaceAnalysisStore` to build and write the analysis JSON.
7. The pipeline records either `post_race_analysis_saved` or `post_race_analysis_failed`.
8. End-of-session diagnostics are created separately after finalization.

Analysis failure is isolated from telemetry finalization. A failure to write analysis should be logged and evented, but it should not prevent history finalization from completing.

## Input Data

The builder receives one `HistoricalSessionSummary`.

Important fields used today:

- `Track`: display name, internal track name, fallback track key.
- `Session`: session type, event type, session name, fallback session key.
- `Car`: car display names, car path, fallback car key.
- `Combo`: car/track/session identity used for storage and filtering.
- `Metrics.CaptureDurationSeconds`
- `Metrics.CompletedValidLaps`
- `Metrics.ValidDistanceLaps`
- `Metrics.FuelPerLapLiters`
- `Car.DriverCarFuelMaxLiters`
- `Metrics.StintCount`
- `Metrics.AverageStintLaps`
- `Metrics.AverageStintSeconds`
- `Metrics.PitRoadEntryCount`
- `Metrics.PitServiceCount`
- `Metrics.AveragePitLaneSeconds`
- `Metrics.AveragePitServiceSeconds`
- `Metrics.ObservedFuelFillRateLitersPerSecond`
- `Quality.Confidence`
- `Quality.Reasons`
- `SourceCaptureId`
- `FinishedAtUtc`

## Output Data

The builder creates a `PostRaceAnalysis` object:

- `analysisVersion`: currently `1`.
- `id`: `{finishedAtUtc:yyyyMMdd-HHmmss}-{sourceCaptureId}`.
- `createdAtUtc`: current UTC time.
- `finishedAtUtc`: copied from the summary.
- `sourceId`: copied from the summary source/capture id.
- `title`: chosen track plus chosen session.
- `subtitle`: chosen car plus quality confidence.
- `combo`: copied from summary combo.
- `lines`: the human-readable report body.

`Body` is just `Lines` joined with newlines.

## Report Construction

The builder writes lines in this order.

1. Title:
   - Track display name if present.
   - Else track name.
   - Else track key.
   - Then `" - "`.
   - Then session type, event type, session name, or session key.

2. Subtitle:
   - Car screen name, short car name, car path, or car key.
   - Then `" | "`.
   - Then summary quality confidence.

3. Blank line.

4. Duration line:
   - Captured duration.
   - Completed valid laps.
   - Valid distance laps.

5. Fuel model line:
   - Average fuel per lap.
   - Fuel tank size.
   - Estimated laps per tank.

6. Optional stint line:
   - Included only when `Metrics.StintCount > 0`.
   - Shows stint count, average stint laps, and average stint duration.

7. Pit service line:
   - If pit-road entries or pit service stops exist, show service stops, pit-road entries, average lane time, and average service time.
   - Otherwise say no completed pit service was detected.

8. Optional observed fill-rate line:
   - Included only when `ObservedFuelFillRateLitersPerSecond` exists.

9. Blank line.

10. `"Strategy note:"`.

11. Strategy note text from the simple stint-rhythm rule below.

12. Blank line.

13. Quality line:
   - Quality confidence.
   - Joined quality reasons, or `"no quality warnings"`.

## Strategy Note Rule

The current strategy note is deliberately simple:

1. Read average fuel per lap.
2. Read fuel tank size.
3. If either value is missing or invalid, write:
   - `"Fuel data was not strong enough for a stint-rhythm recommendation yet."`
4. Otherwise calculate:
   - `tankLaps = tankLiters / fuelPerLapLiters`
   - `conservative = floor(tankLaps)`, with a minimum of 1 lap
   - `stretch = ceiling(tankLaps)`, with a minimum of `conservative`
5. If `stretch > conservative`, write:
   - `"{stretch}-lap stints are plausible only with fuel saving versus the average; compare against a conservative {conservative}-lap rhythm before the next race."`
6. Otherwise write:
   - `"{conservative}-lap stints match the current fuel model; review reserve before extending the rhythm."`

This rule does not currently use pit-loss estimates, tire-service estimates, class-leader pace, changing race length, or the live fuel calculator's rhythm comparison.

## Persistence And Loading

Analyses are stored under:

```text
{history-root}/analysis/{slugged-analysis-id}.json
```

`PostRaceAnalysisStore.LoadRecent`:

1. Reads all JSON files in the analysis directory.
2. Ignores unreadable files.
3. Sorts saved analyses by `FinishedAtUtc`, newest first.
4. Appends the built-in four-hour Nurburgring sample.
5. Removes duplicate ids.
6. Returns up to the requested maximum count, currently defaulting to 12.

The settings window refreshes the list and selects index `0`, so the newest saved analysis is selected by default.

## Current Limitations

- There is no timeline of recommendation changes.
- There is no direct comparison to the live fuel calculator's exact in-race plan.
- There is no multi-strategy search.
- There is no confidence scoring per recommendation line.
- There is no export flow beyond diagnostics/recent JSON inclusion.
- There is no raw-capture replay.

## Good Design Feedback Targets

Human review is especially useful for:

- Whether the report should lead with outcome, execution, or recommendation.
- Whether the strategy note should use the same rhythm/stop logic as the fuel calculator.
- Which confidence caveats should appear inline instead of only in the quality line.
- Whether pit-loss and tire-service estimates should be required before suggesting a stint rhythm.
- Whether the report should separate measured facts from inferred recommendations.
