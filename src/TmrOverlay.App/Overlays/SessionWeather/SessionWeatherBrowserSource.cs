using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.SessionWeather;

internal static class SessionWeatherBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: SessionWeatherOverlayDefinition.Definition.Id,
        title: SessionWeatherOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/session-weather",
        fadeWhenTelemetryUnavailable: SessionWeatherOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const session = live?.models?.session || {};
        const weather = live?.models?.weather || {};
        contentEl.innerHTML = `
          <div class="grid">
            ${metric('Track', session.trackDisplayName || '--')}
            ${metric('Session', session.sessionType || session.sessionName || '--')}
            ${metric('Air', Number.isFinite(weather.airTempC) ? `${weather.airTempC.toFixed(1)} C` : '--')}
            ${metric('Track temp', Number.isFinite(weather.trackTempCrewC) ? `${weather.trackTempCrewC.toFixed(1)} C` : '--')}
            ${metric('Surface', weather.trackWetnessLabel || '--')}
            ${metric('Skies', weather.skiesLabel || weather.weatherType || '--')}
          </div>`;
        setStatus(live, session.hasData || weather.hasData ? 'live | session' : 'waiting for session');
      }
    });
    """;
}
