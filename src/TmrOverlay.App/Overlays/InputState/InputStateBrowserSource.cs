using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.InputState;

internal static class InputStateBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: InputStateOverlayDefinition.Definition.Id,
        title: InputStateOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/input-state",
        aliases: ["/overlays/inputs"],
        fadeWhenTelemetryUnavailable: InputStateOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const inputs = live?.models?.inputs || {};
        contentEl.innerHTML = `
          <div class="bars">
            ${bar('Throttle', inputs.throttle, '#4dd77a')}
            ${bar('Brake', inputs.brake, '#ff6b63')}
            ${bar('Clutch', inputs.clutch, '#62c7ff')}
            <div class="grid">
              ${metric('Gear', inputs.gear === -1 ? 'R' : inputs.gear === 0 ? 'N' : inputs.gear ?? '--')}
              ${metric('RPM', Number.isFinite(inputs.rpm) ? Math.round(inputs.rpm).toLocaleString() : '--')}
              ${metric('Speed', formatSpeed(inputs.speedMetersPerSecond))}
              ${metric('Steering', Number.isFinite(inputs.steeringWheelAngle) ? `${inputs.steeringWheelAngle.toFixed(1)} deg` : '--')}
            </div>
          </div>`;
        setStatus(live, inputs.hasData ? `live | ${quality(inputs)}` : 'waiting for inputs');
      }
    });
    """;
}
