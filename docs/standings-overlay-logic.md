# Standings Overlay Logic

Implementation files:

- `src/TmrOverlay.App/Overlays/Standings/StandingsOverlayDefinition.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsBrowserSource.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsOverlayViewModel.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsBrowserSettings.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsForm.cs`

The Standings overlay is a compact scoring-first timing table. It reads normalized live state through `ILiveTelemetrySource` and uses `LiveTelemetrySnapshot.Models.Scoring` as the row universe and ordering source when session YAML provides `ResultsPositions`. Live timing rows can enrich those scoring rows with gap, interval, lap-progress, and pit-road data, but they do not decide which cars exist or where the rows sort.

Scoring rows are grouped by class with the focused/reference class shown first. The `standings.other-class-rows` overlay option controls how many rows from each other class are shown after the reference-class group, with small class headers separating multiclass fields. The native overlay and localhost browser source both honor that setting. Cars present in the scoring snapshot remain visible even when live timing coverage is partial, so iRacing proximity/render limits cannot compress missing cars out of the standings. Rows with no live timing enrichment are muted and show unavailable live timing cells rather than disappearing.

When scoring is unavailable, the overlay falls back to the older timing-table behavior: it prefers `LiveTelemetrySnapshot.Models.Timing.ClassRows`, then `OverallRows`, groups by `CarIdx`, sorts by class position, overall position, and class-leader gap, and suppresses anonymous timing rows with no driver directory identity, team name, car number, or focused/player marker. Each car row shows class position, car number, driver/team label, class-leader gap, interval to the focused/reference car, and pit-road status. The focused/reference car is highlighted and pit-road rows show `IN`.

If iRacing is disconnected, live collection is stopped, the latest snapshot is stale, or neither scoring nor timing rows are available, the overlay clears the table and shows a waiting status instead of retaining old standings.

Row-swap animation is intentionally not part of the v0.13-core change. The current behavior updates at the scoring snapshot cadence; smooth row movement can be added later once the data contract is stable.
