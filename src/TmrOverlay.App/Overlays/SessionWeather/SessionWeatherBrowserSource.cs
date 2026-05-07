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
            ${metric('Surface', surfaceText(weather))}
            ${metric('Rain / sky', rainSkyText(weather))}
            ${metric('Wind', windText(weather))}
          </div>`;
        setStatus(live, session.hasData || weather.hasData ? 'live | session' : 'waiting for session');
      }
    });

    function surfaceText(weather) {
      const parts = [];
      if (weather.trackWetnessLabel) parts.push(weather.trackWetnessLabel);
      if (weather.weatherDeclaredWet) parts.push('declared wet');
      if (weather.rubberState) parts.push(`rubber ${weather.rubberState}`);
      return parts.length ? parts.join(' | ') : '--';
    }

    function rainSkyText(weather) {
      const parts = [];
      if (weather.skiesLabel) parts.push(weather.skiesLabel);
      if (weather.weatherType) parts.push(weather.weatherType);
      if (Number.isFinite(weather.precipitationPercent)) parts.push(`rain ${weather.precipitationPercent.toFixed(0)}%`);
      return parts.length ? parts.join(' | ') : '--';
    }

    function windText(weather) {
      const parts = [];
      if (Number.isFinite(weather.windDirectionRadians)) parts.push(cardinal(weather.windDirectionRadians));
      if (Number.isFinite(weather.windVelocityMetersPerSecond)) parts.push(`${(weather.windVelocityMetersPerSecond * 3.6).toFixed(0)} km/h`);
      if (Number.isFinite(weather.relativeHumidityPercent)) parts.push(`hum ${weather.relativeHumidityPercent.toFixed(0)}%`);
      if (Number.isFinite(weather.fogLevelPercent)) parts.push(`fog ${weather.fogLevelPercent.toFixed(0)}%`);
      return parts.length ? parts.join(' | ') : '--';
    }

    function cardinal(radians) {
      let degrees = radians * 180 / Math.PI;
      degrees = ((degrees % 360) + 360) % 360;
      const directions = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
      return directions[Math.round(degrees / 45) % directions.length];
    }
    """;
}
