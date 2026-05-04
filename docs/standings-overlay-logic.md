# Standings Overlay Logic

Implementation files:

- `src/TmrOverlay.App/Overlays/Standings/StandingsOverlayDefinition.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsOverlayViewModel.cs`
- `src/TmrOverlay.App/Overlays/Standings/StandingsForm.cs`

The Standings overlay is a compact same-class timing table. It reads normalized live timing through `ILiveTelemetrySource` and uses `LiveTelemetrySnapshot.Models.Timing`, preferring `ClassRows` and falling back to `OverallRows` only when class rows are unavailable.

Rows are grouped by `CarIdx`, sorted by class position, then overall position and class-leader gap. Each row shows class position, car number, driver/team label, class-leader gap, interval to the focused/reference car, and pit-road status. The focused/reference car is highlighted and pit-road rows show `IN`.

If iRacing is disconnected, live collection is stopped, the latest snapshot is stale, or no timing rows are available, the overlay clears the table and shows a waiting status instead of retaining old standings.
