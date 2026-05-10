# Standings Overlay Logic

Implementation files:

- `src/TmrOverlay.App/Overlays/Standings/StandingsOverlayDefinition.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsBrowserSource.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsOverlayViewModel.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsBrowserSettings.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsForm.cs`

The Standings overlay is a compact scoring-first timing table. It reads normalized live state through `ILiveTelemetrySource` and uses `LiveTelemetrySnapshot.Models.Scoring` as the row universe and ordering source when session YAML provides `ResultsPositions`. Live timing rows can enrich those scoring rows with gap, interval, lap-progress, and pit-road data, but they do not decide which cars exist or where the rows sort. This keeps the table useful before green and during early-race timing gaps because starting/scoring order is still available before live position rows settle.

Scoring rows are grouped by class in the official multiclass table order from iRacing, not reordered around the focused/reference car class. A separate class-separator content block controls whether class separators are shown and how many rows from each other class are kept visible when the row budget is tight; the focused class still receives enough row budget to keep the reference context visible. Class separators render as full-width class bands instead of ordinary data rows with per-column cells. They use the iRacing class color and can show class-specific race projection context when a timed race has enough rolling lap evidence. The native overlay and localhost browser source both honor the same settings and display model. Cars present in the scoring snapshot remain visible even when live timing coverage is partial, so iRacing proximity/render limits cannot compress missing cars out of the standings. Rows with no live timing enrichment are muted and show unavailable live timing cells rather than disappearing.

The live timing model now includes a best-effort all-car timing pass from the iRacing `CarIdx*` arrays. This lets scoring rows from non-focused classes receive live F2-derived class-leader gap and focused-car interval values when iRacing is transmitting those rows, while preserving the official class grouping/order from scoring.

Standings table content is driven by `OverlayContentColumnSettings`. Columns have reusable data keys but Standings-owned setting keys, so Relative or future table overlays do not inherit Standings column choices by accident. The default visible columns are compact class position, car number, full driver name, gap, interval, and pit state. Position values are plain numbers rather than `C<N>` labels. Users can disable, reorder, and set pixel widths for columns. Disabled columns stay in the same list and are dimmed in Settings. The native overlay, localhost browser source, and OBS size recommendation all use the same column definition and compact default widths.

When scoring is unavailable, the overlay falls back to the older timing-table behavior: it prefers `LiveTelemetrySnapshot.Models.Timing.ClassRows`, then `OverallRows`, groups by `CarIdx`, sorts by class position, overall position, and class-leader gap, and suppresses anonymous timing rows with no driver directory identity, team name, car number, or focused/player marker. Each car row shows class position, car number, driver/team label, class-leader gap, interval to the focused/reference car, and pit-road status. The focused/reference car is highlighted and pit-road rows show `IN`.

If iRacing is disconnected, live collection is stopped, the latest snapshot is stale, or neither scoring nor timing rows are available, the overlay clears the table and shows a waiting status instead of retaining old standings.

Row-swap animation is intentionally not part of the v0.13-core change. The current behavior updates at the scoring snapshot cadence; smooth row movement can be added later once the data contract is stable.
