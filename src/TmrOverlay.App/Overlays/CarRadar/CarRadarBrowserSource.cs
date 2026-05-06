using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.CarRadar;

internal static class CarRadarBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: CarRadarOverlayDefinition.Definition.Id,
        title: CarRadarOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/car-radar",
        fadeWhenTelemetryUnavailable: CarRadarOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const spatial = live?.models?.spatial || {};
        const cars = spatial.cars || [];
        contentEl.innerHTML = rowsTable([
          { label: 'Car', value: (row) => `#${row.carIdx}` },
          { label: 'Dir', value: (row) => row.relativeLaps > 0 ? 'Ahead' : 'Behind' },
          { label: 'Meters', value: (row) => Number.isFinite(row.relativeMeters) ? row.relativeMeters.toFixed(1) : '--' },
          { label: 'Gap', value: (row) => formatSeconds(row.relativeSeconds) },
          { label: 'Pit', value: (row) => row.onPitRoad ? 'IN' : '' }
        ], cars.slice(0, 12));
        setStatus(live, spatial.hasData ? `live | ${spatial.sideStatus || 'radar'}` : 'waiting for radar');
      }
    });
    """;
}
