using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.FuelCalculator;

internal static class FuelCalculatorBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: FuelCalculatorOverlayDefinition.Definition.Id,
        title: FuelCalculatorOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/fuel-calculator",
        aliases: ["/overlays/calculator"],
        fadeWhenTelemetryUnavailable: FuelCalculatorOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const fuel = live?.models?.fuelPit?.fuel || live?.fuel || {};
        const pit = live?.models?.fuelPit || {};
        contentEl.innerHTML = `
          <div class="grid">
            ${metric('Fuel', `${formatNumber(fuel.fuelLevelLiters)} L`)}
            ${metric('Tank', formatPercent(fuel.fuelLevelPercent))}
            ${metric('Burn', `${formatNumber(fuel.fuelPerLapLiters, 2)} L/lap`)}
            ${metric('Laps left', formatNumber(fuel.estimatedLapsRemaining))}
            ${metric('Time left', `${formatNumber(fuel.estimatedMinutesRemaining)} min`)}
            ${metric('Pit road', pit.onPitRoad ? 'IN' : 'OUT')}
          </div>`;
        setStatus(live, fuel.hasValidFuel ? `live | ${fuel.confidence || 'fuel'}` : 'waiting for fuel');
      }
    });
    """;
}
