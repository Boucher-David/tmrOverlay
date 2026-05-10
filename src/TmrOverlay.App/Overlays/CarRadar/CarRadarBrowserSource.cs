using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.CarRadar;

internal static class CarRadarBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: CarRadarOverlayDefinition.Definition.Id,
        title: CarRadarOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/car-radar",
        fadeWhenTelemetryUnavailable: CarRadarOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        bodyClass: "car-radar-page",
        script: Script);

    private const string Script = """
    ensureRadarStyle();

    TmrBrowserOverlay.register({
      render(live) {
        const spatial = live?.models?.spatial || {};
        const cars = (spatial.cars || [])
          .filter((row) => Number.isFinite(row.relativeMeters) || Number.isFinite(row.relativeSeconds) || Number.isFinite(row.relativeLaps));
        contentEl.innerHTML = radarMarkup(spatial, cars.slice(0, 10));
        setStatus(live, spatial.hasData ? `live | ${spatial.sideStatus || 'radar'}` : 'waiting for radar');
      }
    });

    function ensureRadarStyle() {
      if (document.getElementById('car-radar-browser-v2-style')) {
        return;
      }

      const style = document.createElement('style');
      style.id = 'car-radar-browser-v2-style';
      style.textContent = `
        body.car-radar-page .overlay {
          min-width: 300px;
        }

        .radar-v2 {
          position: relative;
          width: min(300px, calc(100vw - 48px));
          aspect-ratio: 1;
          display: grid;
          place-items: center;
          border: 1px solid var(--tmr-border-muted);
          border-radius: 8px;
          background: var(--tmr-surface-inset);
          overflow: hidden;
        }

        .radar-v2::before,
        .radar-v2::after {
          content: "";
          position: absolute;
          inset: 22px;
          border: 1px solid rgba(140, 174, 212, 0.24);
          border-radius: 50%;
        }

        .radar-v2::after {
          inset: 68px;
        }

        .radar-axis {
          position: absolute;
          background: rgba(140, 174, 212, 0.22);
        }

        .radar-axis-x {
          left: 20px;
          right: 20px;
          top: 50%;
          height: 1px;
        }

        .radar-axis-y {
          top: 20px;
          bottom: 20px;
          left: 50%;
          width: 1px;
        }

        .radar-car {
          position: absolute;
          width: 24px;
          height: 50px;
          border-radius: 5px;
          border: 1px solid rgba(255, 247, 255, 0.30);
          background: var(--tmr-text-secondary);
          transform: translate(-50%, -50%);
          box-shadow: 0 0 14px rgba(0, 232, 255, 0.14);
        }

        .radar-car.focus {
          width: 24px;
          height: 48px;
          background: var(--tmr-text);
          box-shadow: 0 0 18px rgba(255, 247, 255, 0.22);
          z-index: 4;
        }

        .radar-car.side-left,
        .radar-car.side-right {
          background: var(--tmr-error);
          opacity: 0.98;
          z-index: 3;
        }
      `;
      document.head.appendChild(style);
    }

    function radarMarkup(spatial, cars) {
      const sideLeft = spatial.hasCarLeft === true;
      const sideRight = spatial.hasCarRight === true;
      const carMarkup = cars.map((car, index) => radarCarMarkup(car, index)).join('');
      return `
        <div class="radar-v2">
          <div class="radar-axis radar-axis-x"></div>
          <div class="radar-axis radar-axis-y"></div>
          ${carMarkup}
          ${sideLeft ? '<div class="radar-car side-left" style="left:28%;top:50%;"></div>' : ''}
          ${sideRight ? '<div class="radar-car side-right" style="left:72%;top:50%;"></div>' : ''}
          <div class="radar-car focus" style="left:50%;top:50%;"></div>
        </div>`;
    }

    function radarCarMarkup(car, index) {
      const seconds = Number.isFinite(car.relativeSeconds)
        ? car.relativeSeconds
        : Number.isFinite(car.relativeLaps) ? car.relativeLaps * 120 : 0;
      const normalized = Math.max(-1, Math.min(1, seconds / 3.5));
      const lane = (index % 3) - 1;
      const left = 50 + lane * 12;
      const top = 50 - normalized * 37;
      return `<div class="radar-car" style="left:${left.toFixed(1)}%;top:${top.toFixed(1)}%;background:${classColorCss(car.carClassColorHex)};"></div>`;
    }
    """;
}
