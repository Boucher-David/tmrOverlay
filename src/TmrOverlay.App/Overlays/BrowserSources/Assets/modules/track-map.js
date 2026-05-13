let cachedTrackMap = null;
let cachedTrackMapSettings = { internalOpacity: 0.88, showSectorBoundaries: true };
let trackMapDisplayModel = null;
let nextTrackMapFetchAt = 0;
const smoothedMarkerProgress = new Map();
let lastMarkerSmoothingAt = 0;
const trackMapModel = window.TmrBrowserModel;

TmrBrowserOverlay.register({
  async beforeRefresh() {
    trackMapDisplayModel = await fetchOverlayModel('track-map');
    await refreshTrackMapAsset();
  },
  render() {
    renderTrackMap(trackMapDisplayModel, cachedTrackMap);
  },
  renderOffline() {
    renderTrackMap(trackMapDisplayModel, cachedTrackMap);
  }
});

async function refreshTrackMapAsset() {
  if (Date.now() < nextTrackMapFetchAt) {
    return;
  }

  nextTrackMapFetchAt = Date.now() + 10000;
  try {
    const response = await fetch(TmrBrowserApiPath('/api/track-map'), { cache: 'no-store' });
    if (!response.ok) return;
    const payload = await response.json();
    cachedTrackMap = payload.trackMap || null;
    cachedTrackMapSettings = payload.trackMapSettings || cachedTrackMapSettings;
  } catch {
    cachedTrackMap = null;
  }
}

function renderTrackMap(model, trackMap) {
  const view = model?.trackMap || {};
  const settings = {
    internalOpacity: Number.isFinite(view.internalOpacity) ? view.internalOpacity : cachedTrackMapSettings.internalOpacity,
    showSectorBoundaries: view.showSectorBoundaries ?? cachedTrackMapSettings.showSectorBoundaries
  };
  const markers = smoothTrackMapMarkers(view.markers || []);
  const sectors = view.sectors || [];
  const svg = trackMapSvg(trackMap, markers, sectors, settings);
  contentEl.innerHTML = `
    <div class="track">
      ${svg}
    </div>`;
  renderHeaderItems(model, model?.status || 'waiting for position');
  renderFooterSource(model);
}

function trackMapSvg(trackMap, markers, sectors, settings) {
  const interior = trackMapInteriorFill(settings);
  const racingLine = trackMap?.racingLine;
  if (racingLine?.points?.length >= 3) {
    const transform = trackMapTransform(trackMap);
    if (transform) {
      const racingPath = pathForGeometry(racingLine, transform);
      const interiorPath = racingLine.closed
        ? `<path d="${racingPath}" fill="${interior}" stroke="none"></path>`
        : '';
      const pitPath = trackMap?.pitLane?.points?.length >= 2
        ? `<path d="${pathForGeometry(trackMap.pitLane, transform)}" fill="none" stroke="var(--tmr-cyan)" stroke-opacity="0.74" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"></path>`
        : '';
      const sectorPaths = sectorHighlightPaths(sectors, racingLine, transform);
      const boundaryPaths = trackMapSectorBoundaryPaths(sectors, racingLine, transform, settings);
      const dots = markers.map((marker) => markerSvg(marker, pointOnGeometry(racingLine, transform, marker.lapDistPct))).join('');
      return `
        <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
          ${interiorPath}
          <path d="${racingPath}" fill="none" stroke="var(--tmr-text)" stroke-opacity="0.32" stroke-width="11" stroke-linecap="round" stroke-linejoin="round"></path>
          <path d="${racingPath}" fill="none" stroke="var(--tmr-text-secondary)" stroke-width="4.4" stroke-linecap="round" stroke-linejoin="round"></path>
          ${sectorPaths}
          ${boundaryPaths}
          ${pitPath}
          ${dots}
        </svg>`;
    }
  }

  const sectorPaths = circleSectorHighlightPaths(sectors);
  const boundaryPaths = circleSectorBoundaryPaths(sectors, settings);
  const dots = markers.map((marker) => markerSvg(marker, pointOnCircle(marker.lapDistPct))).join('');
  return `
    <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
      <circle cx="160" cy="160" r="138" fill="${interior}" stroke="none"></circle>
      <circle cx="160" cy="160" r="138" fill="none" stroke="var(--tmr-text)" stroke-opacity="0.32" stroke-width="11"></circle>
      <circle cx="160" cy="160" r="138" fill="none" stroke="var(--tmr-text-secondary)" stroke-width="4.4"></circle>
      ${sectorPaths}
      ${boundaryPaths}
      ${dots}
    </svg>`;
}

function trackMapInteriorFill(settings) {
  const opacity = Math.max(0.2, Math.min(1, Number(settings?.internalOpacity ?? 0.88)));
  return `rgba(9,14,32,${(0.59 * opacity).toFixed(3)})`;
}

function trackMapTransform(trackMap) {
  const points = [
    ...(trackMap?.racingLine?.points || []),
    ...(trackMap?.pitLane?.points || [])
  ].filter((point) => Number.isFinite(point.x) && Number.isFinite(point.y));
  if (!points.length) return null;
  const minX = Math.min(...points.map((point) => point.x));
  const maxX = Math.max(...points.map((point) => point.x));
  const minY = Math.min(...points.map((point) => point.y));
  const maxY = Math.max(...points.map((point) => point.y));
  const width = Math.max(1, maxX - minX);
  const height = Math.max(1, maxY - minY);
  const scale = Math.min(284 / width, 284 / height);
  if (!Number.isFinite(scale) || scale <= 0) return null;
  return {
    minX,
    maxY,
    scale,
    left: 18 + (284 - width * scale) / 2,
    top: 18 + (284 - height * scale) / 2
  };
}

function mapPoint(point, transform) {
  return {
    x: transform.left + (point.x - transform.minX) * transform.scale,
    y: transform.top + (transform.maxY - point.y) * transform.scale
  };
}

function pathForGeometry(geometry, transform) {
  const points = geometry.points || [];
  if (!points.length) return '';
  const mapped = points.map((point) => mapPoint(point, transform));
  const commands = mapped.map((point, index) => `${index === 0 ? 'M' : 'L'}${point.x.toFixed(1)} ${point.y.toFixed(1)}`);
  return `${commands.join(' ')}${geometry.closed ? ' Z' : ''}`;
}

function sectorHighlightPaths(sectors, geometry, transform) {
  return (sectors || [])
    .filter(hasSectorHighlight)
    .flatMap((sector) => segmentRanges(sector.startPct, sector.endPct).map((range) => {
      const d = pathForGeometrySegment(geometry, transform, range.startPct, range.endPct);
      if (!d) return '';
      return `<path d="${d}" fill="none" stroke="${sectorHighlightColor(sector.highlight)}" stroke-width="5.6" stroke-linecap="round" stroke-linejoin="round"></path>`;
    }))
    .join('');
}

function pathForGeometrySegment(geometry, transform, startPct, endPct) {
  const points = geometry?.points || [];
  if (points.length < 2 || endPct <= startPct) return '';
  const start = pointOnGeometry(geometry, transform, startPct);
  const end = pointOnGeometry(geometry, transform, endPct);
  if (!start || !end) return '';
  const commands = [`M${start.x.toFixed(1)} ${start.y.toFixed(1)}`];
  for (const point of points) {
    if (point.lapDistPct > startPct && point.lapDistPct < endPct) {
      const mapped = mapPoint(point, transform);
      commands.push(`L${mapped.x.toFixed(1)} ${mapped.y.toFixed(1)}`);
    }
  }
  commands.push(`L${end.x.toFixed(1)} ${end.y.toFixed(1)}`);
  return commands.join(' ');
}

function circleSectorHighlightPaths(sectors) {
  return (sectors || [])
    .filter(hasSectorHighlight)
    .flatMap((sector) => segmentRanges(sector.startPct, sector.endPct).map((range) => {
      const d = arcForCircleSegment(range.startPct, range.endPct);
      if (!d) return '';
      return `<path d="${d}" fill="none" stroke="${sectorHighlightColor(sector.highlight)}" stroke-width="5.6" stroke-linecap="round"></path>`;
    }))
    .join('');
}

function arcForCircleSegment(startPct, endPct) {
  if (endPct <= startPct) return '';
  const start = pointOnCircle(startPct);
  const end = pointOnCircle(endPct);
  const largeArc = endPct - startPct > 0.5 ? 1 : 0;
  return `M${start.x.toFixed(1)} ${start.y.toFixed(1)} A138 138 0 ${largeArc} 1 ${end.x.toFixed(1)} ${end.y.toFixed(1)}`;
}

function segmentRanges(startPct, endPct) {
  const start = normalizeProgress(startPct);
  const end = endPct >= 1 ? 1 : normalizeProgress(endPct);
  if (end <= start && endPct < 1) {
    return [
      { startPct: start, endPct: 1 },
      { startPct: 0, endPct: end }
    ];
  }
  return [{ startPct: start, endPct: Math.max(0, Math.min(1, end)) }];
}

function hasSectorHighlight(sector) {
  return sector?.highlight === 'personal-best' || sector?.highlight === 'best-lap';
}

function sectorHighlightColor(highlight) {
  return highlight === 'best-lap' ? 'var(--tmr-magenta)' : 'var(--tmr-green)';
}

function trackMapSectorBoundaryPaths(sectors, geometry, transform, settings) {
  if (!sectorBoundariesEnabled(settings)) return '';
  return sectorBoundaryProgresses(sectors)
    .map((progress) => boundaryTickMarkup(geometryBoundaryTickPath(geometry, transform, progress), progress))
    .join('');
}

function circleSectorBoundaryPaths(sectors, settings) {
  if (!sectorBoundariesEnabled(settings)) return '';
  return sectorBoundaryProgresses(sectors)
    .map((progress) => boundaryTickMarkup(circleBoundaryTickPath(progress), progress))
    .join('');
}

function sectorBoundariesEnabled(settings) {
  return settings?.showSectorBoundaries !== false;
}

function sectorBoundaryProgresses(sectors) {
  if (!Array.isArray(sectors) || sectors.length < 2) return [];
  const seen = new Set();
  const progresses = [];
  for (const sector of sectors) {
    const progress = normalizeProgress(Number(sector?.startPct ?? 0));
    const key = progress.toFixed(5);
    if (seen.has(key)) continue;
    seen.add(key);
    progresses.push(progress);
  }
  return progresses;
}

function geometryBoundaryTickPath(geometry, transform, progress) {
  const center = pointOnGeometry(geometry, transform, progress);
  const before = pointOnGeometry(geometry, transform, progress - 0.002);
  const after = pointOnGeometry(geometry, transform, progress + 0.002);
  if (!center || !before || !after) return '';
  const dx = after.x - before.x;
  const dy = after.y - before.y;
  const length = Math.max(0.001, Math.hypot(dx, dy));
  const normalX = -dy / length;
  const normalY = dx / length;
  const half = boundaryTickHalfLength(progress);
  return `M${(center.x - normalX * half).toFixed(1)} ${(center.y - normalY * half).toFixed(1)} L${(center.x + normalX * half).toFixed(1)} ${(center.y + normalY * half).toFixed(1)}`;
}

function circleBoundaryTickPath(progress) {
  const point = pointOnCircle(progress);
  const dx = point.x - 160;
  const dy = point.y - 160;
  const length = Math.max(0.001, Math.hypot(dx, dy));
  const unitX = dx / length;
  const unitY = dy / length;
  const half = boundaryTickHalfLength(progress);
  return `M${(point.x - unitX * half).toFixed(1)} ${(point.y - unitY * half).toFixed(1)} L${(point.x + unitX * half).toFixed(1)} ${(point.y + unitY * half).toFixed(1)}`;
}

function boundaryTickMarkup(path, progress) {
  if (!path) return '';
  if (isStartFinishProgress(progress)) {
    return `
      <path d="${path}" fill="none" stroke="var(--tmr-start-finish-boundary-shadow)" stroke-width="5.8" stroke-linecap="round"></path>
      <path d="${path}" fill="none" stroke="var(--tmr-start-finish-boundary)" stroke-width="3.2" stroke-linecap="round"></path>
      <path d="${path}" fill="none" stroke="var(--tmr-track-line)" stroke-opacity="0.92" stroke-width="1.2" stroke-linecap="round"></path>`;
  }

  return `<path d="${path}" fill="none" stroke="var(--tmr-cyan)" stroke-opacity="0.92" stroke-width="2.2" stroke-linecap="round"></path>`;
}

function boundaryTickHalfLength(progress) {
  return isStartFinishProgress(progress) ? 12.3 : 8.5;
}

function isStartFinishProgress(progress) {
  const normalized = normalizeProgress(progress);
  return normalized <= 0.0005 || normalized >= 0.9995;
}

function pointOnGeometry(geometry, transform, progress) {
  const points = geometry?.points || [];
  if (!points.length) return null;
  if (points.length === 1) return mapPoint(points[0], transform);
  const pct = normalizeProgress(progress);
  for (let index = 1; index < points.length; index += 1) {
    const previous = points[index - 1];
    const current = points[index];
    if (pct >= previous.lapDistPct && pct <= current.lapDistPct) {
      return interpolateTrackPoint(previous, current, pct, transform);
    }
  }
  if (geometry.closed) {
    const previous = points[points.length - 1];
    const current = { ...points[0], lapDistPct: points[0].lapDistPct + 1 };
    const adjusted = pct < previous.lapDistPct ? pct + 1 : pct;
    return interpolateTrackPoint(previous, current, adjusted, transform);
  }
  return mapPoint(points[0], transform);
}

function interpolateTrackPoint(previous, current, target, transform) {
  const span = current.lapDistPct - previous.lapDistPct;
  const ratio = span <= 0 ? 0 : Math.max(0, Math.min(1, (target - previous.lapDistPct) / span));
  return mapPoint({
    x: previous.x + (current.x - previous.x) * ratio,
    y: previous.y + (current.y - previous.y) * ratio
  }, transform);
}

function pointOnCircle(progress) {
  const angle = normalizeProgress(progress) * Math.PI * 2 - Math.PI / 2;
  return {
    x: 160 + Math.cos(angle) * 138,
    y: 160 + Math.sin(angle) * 138
  };
}

function markerSvg(marker, point) {
  if (!point) return '';
  const positionLabel = marker.positionLabel
    ?? (Number.isFinite(marker.position) ? String(marker.position) : null);
  const radius = marker.isFocus && positionLabel
    ? focusMarkerRadius(positionLabel)
    : marker.isFocus ? 5.7 : 3.6;
  const fill = marker.color || (marker.isFocus ? 'var(--tmr-cyan)' : markerColor(marker.classColorHex));
  const circle = `<circle cx="${point.x.toFixed(1)}" cy="${point.y.toFixed(1)}" r="${radius}" fill="${fill}" stroke="var(--tmr-title)" stroke-width="${marker.isFocus ? 2 : 1.4}"></circle>`;
  if (!marker.isFocus || !positionLabel) {
    return circle;
  }

  return `
    <g>
      ${circle}
      <text x="${point.x.toFixed(1)}" y="${(point.y + 2.9).toFixed(1)}" text-anchor="middle" font-size="7.6" font-weight="800" fill="var(--tmr-title)">${escapeHtml(positionLabel)}</text>
    </g>`;
}

function focusMarkerRadius(label) {
  return Math.max(5.7, 5.1 + String(label || '').length * 2.9);
}

function trackMapMarkers(live, fallbackFocusPct) {
  const markers = new Map();
  const scoring = trackMapModel.scoring(live);
  const scoringByCarIdx = trackMapModel.scoringByCarIdx(live);
  const referenceCarIdx = trackMapModel.referenceCarIdx(live);
  const rows = trackMapModel.timingRows(live);
  for (const row of rows) {
    if (row.hasSpatialProgress === false) continue;
    if (!Number.isFinite(row.lapDistPct) || row.lapDistPct < 0) continue;
    const scoringRow = scoringByCarIdx.get(row.carIdx);
    const isFocus = Boolean(
      row.isFocus
      || row.carIdx === referenceCarIdx
      || scoringRow?.isFocus);
    if (!isFocus && row.hasTakenGrid !== true) continue;
    const marker = {
      carIdx: row.carIdx,
      lapDistPct: normalizeProgress(row.lapDistPct),
      isFocus,
      color: isFocus ? 'var(--tmr-cyan)' : markerColor(scoringRow?.carClassColorHex || row.carClassColorHex),
      positionLabel: isFocus ? trackMapModel.positionLabel(scoringRow) || trackMapModel.positionLabel(row) : null
    };
    const existing = markers.get(row.carIdx);
    if (!existing || marker.isFocus || !existing.isFocus) {
      markers.set(row.carIdx, marker);
    }
  }

  const focusCarIdx = referenceCarIdx ?? -1;
  if (Number.isFinite(focusCarIdx) && Number.isFinite(fallbackFocusPct) && fallbackFocusPct >= 0) {
    markers.set(focusCarIdx, {
      carIdx: focusCarIdx,
      lapDistPct: normalizeProgress(fallbackFocusPct),
      isFocus: true,
      color: 'var(--tmr-cyan)',
      positionLabel: focusPositionLabel(live, scoringByCarIdx, focusCarIdx)
    });
  }

  return [...markers.values()].sort((left, right) => Number(left.isFocus) - Number(right.isFocus) || left.carIdx - right.carIdx);
}

function shouldRenderFocusSampleMarker(sample) {
  if (!sample || !Number.isFinite(sample.focusLapDistPct) || sample.focusLapDistPct < 0) {
    return false;
  }

  const localFocus = sample.focusCarIdx === sample.playerCarIdx;
  const pitSurface = sample.playerTrackSurface === 1 || sample.playerTrackSurface === 2;
  return !localFocus || (sample.onPitRoad !== true && !pitSurface);
}

function smoothTrackMapMarkers(markers) {
  if (!markers.length) {
    smoothedMarkerProgress.clear();
    lastMarkerSmoothingAt = 0;
    return markers;
  }

  const now = performance.now();
  const elapsed = lastMarkerSmoothingAt > 0 ? Math.max(0, Math.min(0.25, (now - lastMarkerSmoothingAt) / 1000)) : 0.05;
  lastMarkerSmoothingAt = now;
  const alpha = 1 - Math.exp(-elapsed / 0.14);
  const active = new Set(markers.map((marker) => marker.carIdx));
  for (const carIdx of smoothedMarkerProgress.keys()) {
    if (!active.has(carIdx)) {
      smoothedMarkerProgress.delete(carIdx);
    }
  }

  return markers.map((marker) => {
    const current = smoothedMarkerProgress.get(marker.carIdx);
    if (!Number.isFinite(current)) {
      smoothedMarkerProgress.set(marker.carIdx, marker.lapDistPct);
      return marker;
    }

    const lapDistPct = smoothProgress(current, marker.lapDistPct, alpha);
    smoothedMarkerProgress.set(marker.carIdx, lapDistPct);
    return { ...marker, lapDistPct };
  });
}

function smoothProgress(current, target, alpha) {
  let delta = target - current;
  if (delta > 0.5) {
    delta -= 1;
  } else if (delta < -0.5) {
    delta += 1;
  }

  return normalizeProgress(current + delta * Math.max(0, Math.min(1, alpha)));
}

function focusPositionLabel(live, scoringByCarIdx, focusCarIdx) {
  const timing = trackMapModel.timing(live);
  const scoringRow = scoringByCarIdx?.get(focusCarIdx);
  if (scoringRow) return trackMapModel.positionLabel(scoringRow);
  return trackMapModel.positionLabel(timing.focusRow)
    || trackMapModel.samplePositionLabel(live?.latestSample);
}

function markerColor(value) {
  return classColorCss(value);
}

function normalizeProgress(value) {
  if (!Number.isFinite(value)) return 0;
  const normalized = value % 1;
  return normalized < 0 ? normalized + 1 : normalized;
}
