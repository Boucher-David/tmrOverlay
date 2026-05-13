using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.TrackMap;

internal static class TrackMapMarkerPolicy
{
    public static bool ShouldRenderTimingMarker(LiveTimingRow row, bool isFocus)
    {
        return row.HasSpatialProgress
            && row.LapDistPct is { } lapDistPct
            && IsValidProgress(lapDistPct)
            && (isFocus || row.HasTakenGrid);
    }

    public static bool ShouldRenderFocusSampleMarker(HistoricalTelemetrySample? sample)
    {
        if (sample?.FocusLapDistPct is not { } lapDistPct || !IsValidProgress(lapDistPct))
        {
            return false;
        }

        return sample.FocusCarIdx != sample.PlayerCarIdx
            || (sample.OnPitRoad != true && !IsPitRoadTrackSurface(sample.PlayerTrackSurface));
    }

    public static bool IsValidProgress(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }

    private static bool IsPitRoadTrackSurface(int? trackSurface)
    {
        return trackSurface is 1 or 2;
    }
}
