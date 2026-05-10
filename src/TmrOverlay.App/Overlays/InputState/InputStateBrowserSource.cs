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
        bodyClass: "input-state-page",
        script: Script);

    private const string Script = """
    const defaultInputSettings = {
      showThrottle: true,
      showBrake: true,
      showClutch: true,
      showSteering: true,
      showGear: true,
      showSpeed: true
    };
    let inputSettings = defaultInputSettings;
    let nextInputSettingsFetchAt = 0;
    const inputTrace = [];
    const maximumInputTracePoints = 180;
    ensureInputStyle();

    TmrBrowserOverlay.register({
      async beforeRefresh() {
        await refreshInputSettings();
      },
      render(live) {
        const inputs = live?.models?.inputs || {};
        const race = live?.models?.raceEvents || {};
        if (race.hasData && (race.isOnTrack !== true || race.isInGarage === true)) {
          contentEl.innerHTML = '<div class="empty">Waiting for player in car.</div>';
          setStatus(live, 'waiting for player in car');
          return;
        }

        if (inputs.hasData) {
          appendInputTrace(inputs);
        }

        const brakeAbsActive = inputs.brakeAbsActive === true;
        const railEnabled = inputRailEnabled();
        contentEl.innerHTML = `
          <div class="input-layout${railEnabled ? '' : ' no-rail'}">
            <div class="input-graph-panel">
              <canvas class="input-graph" aria-label="Input trace graph"></canvas>
            </div>
            ${railEnabled ? renderInputRail(inputs, brakeAbsActive) : ''}
          </div>`;
        drawInputGraph(contentEl.querySelector('.input-graph'), inputs, brakeAbsActive);
        setStatus(live, inputs.hasData ? `live | ${quality(inputs)}${brakeAbsActive ? ' | ABS' : ''}` : 'waiting for inputs');
      }
    });

    async function refreshInputSettings() {
      if (Date.now() < nextInputSettingsFetchAt) {
        return;
      }

      nextInputSettingsFetchAt = Date.now() + 2000;
      try {
        const response = await fetch('/api/input-state', { cache: 'no-store' });
        if (!response.ok) return;
        const payload = await response.json();
        inputSettings = normalizeInputSettings(payload.inputStateSettings || inputSettings);
      } catch {
        inputSettings = defaultInputSettings;
      }
    }

    function normalizeInputSettings(settings) {
      return {
        showThrottle: settings?.showThrottle ?? defaultInputSettings.showThrottle,
        showBrake: settings?.showBrake ?? defaultInputSettings.showBrake,
        showClutch: settings?.showClutch ?? defaultInputSettings.showClutch,
        showSteering: settings?.showSteering ?? defaultInputSettings.showSteering,
        showGear: settings?.showGear ?? defaultInputSettings.showGear,
        showSpeed: settings?.showSpeed ?? defaultInputSettings.showSpeed
      };
    }

    function ensureInputStyle() {
      if (document.getElementById('input-state-browser-style')) {
        return;
      }

      const style = document.createElement('style');
      style.id = 'input-state-browser-style';
      style.textContent = `
        body.input-state-page .overlay {
          width: calc(100vw - 16px);
          height: calc(100vh - 16px);
          min-width: 0;
          max-width: none;
        }

        body.input-state-page .content {
          width: 100%;
          height: calc(100% - 38px);
          padding: 12px 16px 14px;
        }

        .input-layout {
          display: grid;
          grid-template-columns: minmax(220px, 1fr) minmax(126px, 30%);
          gap: 12px;
          width: 100%;
          height: 100%;
          min-width: 0;
        }

        .input-layout.no-rail {
          grid-template-columns: minmax(220px, 1fr);
        }

        .input-graph-panel,
        .input-rail {
          min-width: 0;
          min-height: 0;
          border: 1px solid var(--tmr-border-muted);
          border-radius: 5px;
          background: var(--tmr-surface-inset);
        }

        .input-graph {
          display: block;
          width: 100%;
          height: 100%;
        }

        .input-rail {
          display: grid;
          grid-template-rows: auto auto minmax(0, 1fr);
          gap: 8px;
          padding: 8px;
          overflow: hidden;
          background: var(--tmr-surface-raised);
        }

        .input-numbers {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(50px, 1fr));
          gap: 6px;
        }

        .input-number {
          display: grid;
          gap: 2px;
          min-width: 0;
          padding: 4px 5px;
          border: 1px solid var(--tmr-border-muted);
          border-radius: 4px;
          background: var(--tmr-surface-inset);
        }

        .input-number .label,
        .input-wheel-label,
        .input-pedal-label {
          color: var(--tmr-text-muted);
          font-size: 9px;
          font-weight: 800;
          text-transform: uppercase;
          white-space: nowrap;
        }

        .input-number .value,
        .input-wheel-value,
        .input-pedal-value {
          color: var(--tmr-text);
          font-family: Consolas, "Courier New", monospace;
          font-size: 12px;
          font-weight: 800;
          white-space: nowrap;
        }

        .input-wheel {
          display: grid;
          grid-template-columns: 1fr auto;
          grid-template-rows: 14px minmax(32px, 1fr);
          column-gap: 8px;
          align-items: center;
          min-height: 48px;
        }

        .input-wheel svg {
          grid-column: 1 / -1;
          justify-self: center;
          width: min(58px, 100%);
          height: min(58px, 100%);
        }

        .input-pedals {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(30px, 1fr));
          gap: 6px;
          min-height: 44px;
        }

        .input-pedal {
          display: grid;
          grid-template-rows: 15px minmax(20px, 1fr) 15px;
          justify-items: center;
          min-width: 0;
        }

        .input-pedal-track {
          position: relative;
          width: 14px;
          min-height: 18px;
          border-radius: 7px;
          background: var(--tmr-surface-inset);
          overflow: hidden;
        }

        .input-pedal-track span {
          position: absolute;
          left: 0;
          bottom: 0;
          width: 100%;
        }
      `;
      document.head.appendChild(style);
    }

    function appendInputTrace(inputs) {
      inputTrace.push({
        throttle: clamp01(inputs.throttle),
        brake: clamp01(inputs.brake),
        clutch: clamp01(inputs.clutch),
        brakeAbsActive: inputs.brakeAbsActive === true
      });
      if (inputTrace.length > maximumInputTracePoints) {
        inputTrace.splice(0, inputTrace.length - maximumInputTracePoints);
      }
    }

    function inputRailEnabled() {
      return inputSettings.showThrottle
        || inputSettings.showBrake
        || inputSettings.showClutch
        || inputSettings.showSteering
        || inputSettings.showGear
        || inputSettings.showSpeed;
    }

    function renderInputRail(inputs, brakeAbsActive) {
      const numbers = [
        inputSettings.showGear ? railNumber('Gear', formatGear(inputs.gear)) : '',
        inputSettings.showSpeed ? railNumber('Speed', formatSpeed(inputs.speedMetersPerSecond)) : ''
      ].filter(Boolean).join('');
      const pedals = [
        inputSettings.showThrottle ? railPedal('T', inputs.throttle, 'var(--tmr-green)') : '',
        inputSettings.showBrake ? railPedal(brakeAbsActive ? 'ABS' : 'B', inputs.brake, brakeAbsActive ? 'var(--tmr-amber)' : 'var(--tmr-error)') : '',
        inputSettings.showClutch ? railPedal('C', inputs.clutch, 'var(--tmr-cyan)') : ''
      ].filter(Boolean).join('');
      return `
        <div class="input-rail">
          ${numbers ? `<div class="input-numbers">${numbers}</div>` : '<div></div>'}
          ${inputSettings.showSteering ? renderWheel(inputs.steeringWheelAngle) : '<div></div>'}
          ${pedals ? `<div class="input-pedals">${pedals}</div>` : '<div></div>'}
        </div>`;
    }

    function railNumber(label, value) {
      return `
        <div class="input-number">
          <div class="label">${escapeHtml(label)}</div>
          <div class="value">${escapeHtml(value)}</div>
        </div>`;
    }

    function railPedal(label, value, color) {
      const normalized = clamp01(value);
      return `
        <div class="input-pedal">
          <div class="input-pedal-label">${escapeHtml(label)}</div>
          <div class="input-pedal-track"><span style="height:${(normalized * 100).toFixed(0)}%; background:${color};"></span></div>
          <div class="input-pedal-value">${formatPercent(Number.isFinite(value) ? normalized : null)}</div>
        </div>`;
    }

    function renderWheel(angleDegrees) {
      const angle = Number.isFinite(angleDegrees) ? angleDegrees : 0;
      return `
        <div class="input-wheel">
          <div class="input-wheel-label">Wheel</div>
          <div class="input-wheel-value">${Number.isFinite(angleDegrees) ? `${angleDegrees.toFixed(0)} deg` : '--'}</div>
          <svg viewBox="0 0 64 64" aria-hidden="true">
            <circle cx="32" cy="32" r="25" fill="none" stroke="var(--tmr-text-secondary)" stroke-width="4"></circle>
            <g transform="rotate(${escapeAttribute(angle * 2.1)} 32 32)" stroke="var(--tmr-cyan)" stroke-width="3" stroke-linecap="round">
              <line x1="32" y1="32" x2="32" y2="12"></line>
              <line x1="32" y1="32" x2="49" y2="43"></line>
              <line x1="32" y1="32" x2="15" y2="43"></line>
            </g>
          </svg>
        </div>`;
    }

    function drawInputGraph(canvas, inputs, brakeAbsActive) {
      if (!canvas) {
        return;
      }

      const rect = canvas.getBoundingClientRect();
      const width = Math.max(1, rect.width);
      const height = Math.max(1, rect.height);
      const dpr = window.devicePixelRatio || 1;
      canvas.width = Math.round(width * dpr);
      canvas.height = Math.round(height * dpr);

      const ctx = canvas.getContext('2d');
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, width, height);
      ctx.strokeStyle = 'rgba(140, 174, 212, 0.18)';
      ctx.lineWidth = 1;
      for (let step = 1; step < 4; step += 1) {
        const y = Math.round(height * step / 4) + 0.5;
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(width, y);
        ctx.stroke();
      }

      drawTraceLine(ctx, width, height, 'throttle', themeColor('--tmr-green', '#62ff9f'), 2);
      drawTraceLine(ctx, width, height, 'brake', themeColor('--tmr-error', '#ff6274'), 2);
      drawTraceLine(ctx, width, height, 'clutch', themeColor('--tmr-cyan', '#00e8ff'), 2);
      drawAbsSegments(ctx, width, height);
      drawInputLegend(ctx, brakeAbsActive);

      if (inputTrace.length < 2 && !inputs.hasData) {
        ctx.fillStyle = themeColor('--tmr-text-muted', '#8caed4');
        ctx.font = '700 13px "Segoe UI", Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('waiting for inputs', width / 2, height / 2);
      }
    }

    function drawTraceLine(ctx, width, height, key, color, lineWidth) {
      if (inputTrace.length < 2) {
        return;
      }

      ctx.strokeStyle = color;
      ctx.lineWidth = lineWidth;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.beginPath();
      inputTrace.forEach((point, index) => {
        const x = xForTracePoint(index, width);
        const y = yForTraceValue(point[key], height);
        if (index === 0) {
          ctx.moveTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }
      });
      ctx.stroke();
    }

    function drawAbsSegments(ctx, width, height) {
      if (inputTrace.length < 2) {
        return;
      }

      ctx.strokeStyle = themeColor('--tmr-amber', '#ffd15b');
      ctx.lineWidth = 3;
      ctx.lineCap = 'round';
      for (let index = 1; index < inputTrace.length; index += 1) {
        if (inputTrace[index].brakeAbsActive !== true) {
          continue;
        }

        ctx.beginPath();
        ctx.moveTo(xForTracePoint(index - 1, width), yForTraceValue(inputTrace[index - 1].brake, height));
        ctx.lineTo(xForTracePoint(index, width), yForTraceValue(inputTrace[index].brake, height));
        ctx.stroke();
      }
    }

    function drawInputLegend(ctx, brakeAbsActive) {
      const items = [
        ['Throttle', themeColor('--tmr-green', '#62ff9f')],
        [brakeAbsActive ? 'Brake ABS' : 'Brake', brakeAbsActive ? themeColor('--tmr-amber', '#ffd15b') : themeColor('--tmr-error', '#ff6274')],
        ['Clutch', themeColor('--tmr-cyan', '#00e8ff')]
      ];
      let x = 8;
      ctx.font = '700 11px "Segoe UI", Arial, sans-serif';
      ctx.textBaseline = 'top';
      for (const [label, color] of items) {
        ctx.fillStyle = color;
        ctx.fillRect(x, 14, 14, 3);
        x += 18;
        ctx.fillText(label, x, 7);
        x += ctx.measureText(label).width + 14;
      }
    }

    function xForTracePoint(index, width) {
      return inputTrace.length <= 1 ? 0 : index / (inputTrace.length - 1) * width;
    }

    function yForTraceValue(value, height) {
      return height - clamp01(value) * height;
    }

    function clamp01(value) {
      return Number.isFinite(value) ? Math.max(0, Math.min(1, value)) : 0;
    }

    function formatGear(value) {
      return value === -1 ? 'R' : value === 0 ? 'N' : Number.isFinite(value) ? String(value) : '--';
    }
    """;
}
