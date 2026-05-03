namespace TmrOverlay.App.Overlays.SimpleTelemetry;

internal sealed record SimpleTelemetryOverlayMetrics(
    string Refresh,
    string Snapshot,
    string ViewModel,
    string ApplyUi,
    string Rows,
    string Paint);
