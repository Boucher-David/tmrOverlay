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
        return TrackMapOverlayViewModel.BrowserSettingsFrom(settings);
    }
}
