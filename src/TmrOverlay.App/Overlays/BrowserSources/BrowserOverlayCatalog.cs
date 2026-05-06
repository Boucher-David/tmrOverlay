using TmrOverlay.App.Overlays.CarRadar;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GarageCover;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Overlays.TrackMap;

namespace TmrOverlay.App.Overlays.BrowserSources;

internal static class BrowserOverlayCatalog
{
    private static readonly IReadOnlyList<BrowserOverlayPage> AllPages =
    [
        StandingsBrowserSource.Page,
        RelativeBrowserSource.Page,
        FuelCalculatorBrowserSource.Page,
        SessionWeatherBrowserSource.Page,
        PitServiceBrowserSource.Page,
        InputStateBrowserSource.Page,
        CarRadarBrowserSource.Page,
        GapToLeaderBrowserSource.Page,
        TrackMapBrowserSource.Page,
        GarageCoverBrowserSource.Page,
        StreamChatBrowserSource.Page
    ];

    public static IReadOnlyList<BrowserOverlayPage> Pages => AllPages;

    public static IReadOnlyList<string> Routes { get; } = AllPages
        .Select(page => page.CanonicalRoute)
        .ToArray();

    public static bool TryGetPageByRoute(string route, out BrowserOverlayPage page)
    {
        var normalized = BrowserOverlayPage.NormalizeRoute(route);
        page = AllPages.FirstOrDefault(candidate => candidate.MatchesRoute(normalized))!;
        return page is not null;
    }

    public static bool TryGetRouteForOverlayId(string overlayId, out string route)
    {
        var page = AllPages.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, overlayId, StringComparison.OrdinalIgnoreCase));
        route = page?.CanonicalRoute ?? string.Empty;
        return page is not null;
    }
}
