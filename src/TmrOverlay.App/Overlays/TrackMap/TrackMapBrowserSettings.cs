using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.TrackMap;

internal sealed record TrackMapBrowserSettings(bool IncludeUserMaps, double InternalOpacity, bool ShowSectorBoundaries)
{
    public static TrackMapBrowserSettings Default { get; } = new(
        IncludeUserMaps: true,
        InternalOpacity: 0.88d,
        ShowSectorBoundaries: true);

    public static TrackMapBrowserSettings From(ApplicationSettings settings)
    {
        var trackMap = settings.Overlays.FirstOrDefault(
            overlay => string.Equals(overlay.Id, TrackMapOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase));
        return new TrackMapBrowserSettings(
            IncludeUserMaps: trackMap?.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: true) ?? true,
            InternalOpacity: Math.Clamp(trackMap?.Opacity ?? Default.InternalOpacity, 0.2d, 1d),
            ShowSectorBoundaries: trackMap?.GetBooleanOption(OverlayOptionKeys.TrackMapSectorBoundariesEnabled, defaultValue: true) ?? true);
    }
}
