using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.TrackMap;

internal static class TrackMapBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: TrackMapOverlayDefinition.Definition.Id,
        title: TrackMapOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/track-map",
        bodyClass: "track-map-page",
        renderWhenTelemetryUnavailable: true,
        refreshIntervalMilliseconds: 250,
        script: Script);

    private const string Script = """
    let cachedTrackMap = null;
    let cachedTrackMapSettings = { internalOpacity: 0.88 };
    let nextTrackMapFetchAt = 0;

    TmrBrowserOverlay.register({
      async beforeRefresh() {
        await refreshTrackMapAsset();
      },
      render(live) {
        renderTrackMap(live, cachedTrackMap, cachedTrackMapSettings);
      },
      renderOffline() {
        renderTrackMap(null, cachedTrackMap, cachedTrackMapSettings);
      }
    });

    async function refreshTrackMapAsset() {
      if (Date.now() < nextTrackMapFetchAt) {
        return;
      }

      nextTrackMapFetchAt = Date.now() + 10000;
      try {
        const response = await fetch('/api/track-map', { cache: 'no-store' });
        if (!response.ok) return;
        const payload = await response.json();
        cachedTrackMap = payload.trackMap || null;
        cachedTrackMapSettings = payload.trackMapSettings || cachedTrackMapSettings;
      } catch {
        cachedTrackMap = null;
      }
    }

    function renderTrackMap(live, trackMap, trackMapSettings) {
      const spatial = live?.models?.spatial || {};
      const race = live?.models?.raceEvents || {};
      const focusPct = Number.isFinite(spatial.referenceLapDistPct)
        ? spatial.referenceLapDistPct
        : Number.isFinite(race.lapDistPct)
          ? race.lapDistPct
          : 0;
      const markers = trackMapMarkers(live, focusPct);
      const svg = trackMapSvg(trackMap, markers, trackMapSettings);
      contentEl.innerHTML = `
        <div class="track">
          ${svg}
        </div>`;
      setStatus(live, spatial.hasData || race.hasData ? 'live | track map' : 'waiting for position');
    }

    function trackMapSvg(trackMap, markers, settings) {
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
            ? `<path d="${pathForGeometry(trackMap.pitLane, transform)}" fill="none" stroke="rgba(98,199,255,0.74)" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"></path>`
            : '';
          const dots = markers.map((marker) => markerSvg(marker, pointOnGeometry(racingLine, transform, marker.lapDistPct))).join('');
          return `
            <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
              ${interiorPath}
              <path d="${racingPath}" fill="none" stroke="rgba(255,255,255,0.32)" stroke-width="11" stroke-linecap="round" stroke-linejoin="round"></path>
              <path d="${racingPath}" fill="none" stroke="rgba(222,238,246,1)" stroke-width="4.4" stroke-linecap="round" stroke-linejoin="round"></path>
              ${pitPath}
              ${dots}
            </svg>`;
        }
      }

      const dots = markers.map((marker) => markerSvg(marker, pointOnCircle(marker.lapDistPct))).join('');
      return `
        <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
          <circle cx="160" cy="160" r="138" fill="${interior}" stroke="none"></circle>
          <circle cx="160" cy="160" r="138" fill="none" stroke="rgba(255,255,255,0.32)" stroke-width="11"></circle>
          <circle cx="160" cy="160" r="138" fill="none" stroke="rgba(222,238,246,1)" stroke-width="4.4"></circle>
          ${dots}
        </svg>`;
    }

    function trackMapInteriorFill(settings) {
      const opacity = Math.max(0.2, Math.min(1, Number(settings?.internalOpacity ?? 0.88)));
      return `rgba(9,14,18,${(0.59 * opacity).toFixed(3)})`;
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
      const radius = marker.isFocus && marker.positionLabel
        ? focusMarkerRadius(marker.positionLabel)
        : marker.isFocus ? 5.7 : 3.6;
      const circle = `<circle cx="${point.x.toFixed(1)}" cy="${point.y.toFixed(1)}" r="${radius}" fill="${marker.color}" stroke="rgb(8,14,18)" stroke-width="${marker.isFocus ? 2 : 1.4}"></circle>`;
      if (!marker.isFocus || !marker.positionLabel) {
        return circle;
      }

      return `
        <g>
          ${circle}
          <text x="${point.x.toFixed(1)}" y="${(point.y + 2.9).toFixed(1)}" text-anchor="middle" font-size="7.6" font-weight="800" fill="rgb(5,12,16)">${escapeHtml(marker.positionLabel)}</text>
        </g>`;
    }

    function focusMarkerRadius(label) {
      return Math.max(5.7, 5.1 + String(label || '').length * 2.9);
    }

    function trackMapMarkers(live, fallbackFocusPct) {
      const markers = new Map();
      const rows = [
        ...(live?.models?.timing?.overallRows || []),
        ...(live?.models?.timing?.classRows || [])
      ];
      for (const row of rows) {
        if (!Number.isFinite(row.lapDistPct) || row.lapDistPct < 0) continue;
        const isFocus = Boolean(row.isFocus || row.isPlayer);
        const marker = {
          carIdx: row.carIdx,
          lapDistPct: normalizeProgress(row.lapDistPct),
          isFocus,
          color: isFocus ? '#62c7ff' : markerColor(row.carClassColorHex),
          positionLabel: isFocus ? positionLabel(row) : null
        };
        const existing = markers.get(row.carIdx);
        if (!existing || marker.isFocus || !existing.isFocus) {
          markers.set(row.carIdx, marker);
        }
      }

      const focusCarIdx = live?.models?.timing?.focusCarIdx
        ?? live?.models?.timing?.playerCarIdx
        ?? live?.models?.spatial?.referenceCarIdx
        ?? live?.latestSample?.focusCarIdx
        ?? live?.latestSample?.playerCarIdx
        ?? -1;
      if (Number.isFinite(fallbackFocusPct) && fallbackFocusPct >= 0) {
        markers.set(focusCarIdx, {
          carIdx: focusCarIdx,
          lapDistPct: normalizeProgress(fallbackFocusPct),
          isFocus: true,
          color: '#62c7ff',
          positionLabel: focusPositionLabel(live)
        });
      }

      return [...markers.values()].sort((left, right) => Number(left.isFocus) - Number(right.isFocus) || left.carIdx - right.carIdx);
    }

    function positionLabel(row) {
      const position = row?.classPosition ?? row?.overallPosition;
      return Number.isFinite(position) && position > 0 ? `P${position}` : null;
    }

    function focusPositionLabel(live) {
      const timing = live?.models?.timing || {};
      return positionLabel(timing.focusRow) || positionLabel(timing.playerRow) || samplePositionLabel(live?.latestSample);
    }

    function samplePositionLabel(sample) {
      const position = sample?.focusClassPosition
        ?? sample?.teamClassPosition
        ?? sample?.focusPosition
        ?? sample?.teamPosition;
      return Number.isFinite(position) && position > 0 ? `P${position}` : null;
    }

    function markerColor(value) {
      const text = String(value || '').trim();
      return /^#[0-9a-f]{6}$/i.test(text) ? text : '#ecf4f8';
    }

    function normalizeProgress(value) {
      if (!Number.isFinite(value)) return 0;
      const normalized = value % 1;
      return normalized < 0 ? normalized + 1 : normalized;
    }
    """;
}
