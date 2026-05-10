ensureRadarStyle();
const radarModel = window.TmrBrowserModel;

TmrBrowserOverlay.register({
  render(live) {
    const spatial = radarModel.spatial(live);
    const cars = (spatial.cars || [])
      .filter((row) => radarModel.hasRelativePlacement(row));
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
      width: min(300px, calc(100vw - 8px));
      aspect-ratio: 1;
      display: grid;
      place-items: center;
      border: 2px solid var(--tmr-cyan);
      border-radius: 50%;
      background: var(--tmr-surface);
      overflow: hidden;
      box-shadow: 0 0 26px rgba(var(--tmr-cyan-rgb), 0.16);
    }

    .radar-v2::before,
    .radar-v2::after {
      content: "";
      position: absolute;
      inset: 22px;
      border: 1px solid rgba(var(--tmr-text-muted-rgb), 0.24);
      border-radius: 50%;
    }

    .radar-v2::after {
      inset: 68px;
    }

    .radar-axis {
      position: absolute;
      background: rgba(var(--tmr-text-muted-rgb), 0.22);
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

    .radar-title,
    .radar-status,
    .radar-multiclass-label {
      position: absolute;
      z-index: 5;
      font-weight: 800;
      letter-spacing: 0;
      white-space: nowrap;
    }

    .radar-title {
      left: 20px;
      top: 18px;
      color: var(--tmr-text);
      font-size: 12px;
    }

    .radar-status {
      right: 20px;
      top: 18px;
      color: var(--tmr-error);
      font-size: 10px;
    }

    .radar-multiclass-label {
      left: 58px;
      right: 58px;
      bottom: 36px;
      color: var(--tmr-amber);
      font-size: 10px;
      text-align: center;
    }

    .radar-approach {
      position: absolute;
      inset: 14px;
      z-index: 2;
      pointer-events: none;
    }

    .radar-car {
      position: absolute;
      width: 24px;
      height: 50px;
      border-radius: 5px;
      border: 1px solid rgba(var(--tmr-text-rgb), 0.30);
      background: var(--tmr-text-secondary);
      transform: translate(-50%, -50%);
      box-shadow: 0 0 14px rgba(var(--tmr-cyan-rgb), 0.14);
    }

    .radar-car.focus {
      width: 24px;
      height: 48px;
      background: var(--tmr-text);
      box-shadow: 0 0 18px rgba(var(--tmr-text-rgb), 0.22);
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
  const approach = spatial.strongestMulticlassApproach;
  const carMarkup = cars.map((car, index) => radarCarMarkup(car, index)).join('');
  const approachSeconds = Number.isFinite(approach?.relativeSeconds)
    ? Math.abs(approach.relativeSeconds).toFixed(1)
    : null;
  return `
    <div class="radar-v2">
      <div class="radar-title">CAR RADAR</div>
      <div class="radar-status">${escapeHtml(radarStatusText(spatial))}</div>
      <div class="radar-axis radar-axis-x"></div>
      <div class="radar-axis radar-axis-y"></div>
      ${approachSeconds ? `
        <svg class="radar-approach" viewBox="0 0 272 272" aria-hidden="true">
          <path d="M83 233 A118 118 0 0 1 137 254" fill="none" stroke="var(--tmr-amber)" stroke-width="5" stroke-linecap="round"></path>
        </svg>
        <div class="radar-multiclass-label">${approachSeconds}s</div>` : ''}
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

function radarStatusText(spatial) {
  if (Number.isFinite(spatial?.strongestMulticlassApproach?.relativeSeconds)) {
    return `${Math.abs(spatial.strongestMulticlassApproach.relativeSeconds).toFixed(1)}s`;
  }
  if (spatial?.hasCarLeft || spatial?.hasCarRight) {
    return 'SIDE';
  }
  return spatial?.hasData ? 'CLEAR' : 'WAIT';
}
