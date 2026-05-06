using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.PitService;

internal static class PitServiceBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: PitServiceOverlayDefinition.Definition.Id,
        title: PitServiceOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/pit-service",
        fadeWhenTelemetryUnavailable: PitServiceOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const pit = live?.models?.fuelPit || {};
        contentEl.innerHTML = `
          <div class="grid">
            ${metric('Pit road', pit.onPitRoad ? 'IN' : 'OUT')}
            ${metric('Pit stall', pit.playerCarInPitStall ? 'IN' : 'OUT')}
            ${metric('Service', pit.pitstopActive ? 'ACTIVE' : 'idle')}
            ${metric('Fuel req', Number.isFinite(pit.pitServiceFuelLiters) ? `${pit.pitServiceFuelLiters.toFixed(1)} L` : '--')}
            ${metric('Repair', Number.isFinite(pit.pitRepairLeftSeconds) ? `${pit.pitRepairLeftSeconds.toFixed(0)} s` : '--')}
            ${metric('Tires used', Number.isFinite(pit.tireSetsUsed) ? pit.tireSetsUsed : '--')}
          </div>`;
        setStatus(live, pit.hasData ? 'live | pit' : 'waiting for pit');
      }
    });
    """;
}
