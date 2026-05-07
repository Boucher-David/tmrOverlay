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
        fadeWhenTelemetryUnavailable: TrackMapOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        refreshIntervalMilliseconds: 50,
        script: Script);

    private const string Script = """
    let cachedTrackMap = null;
    let cachedTrackMapSettings = { internalOpacity: 0.88 };
    let nextTrackMapFetchAt = 0;
    const smoothedMarkerProgress = new Map();
    let lastMarkerSmoothingAt = 0;

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
          : null;
      const markers = smoothTrackMapMarkers(trackMapMarkers(live, focusPct));
      const sectors = live?.models?.trackMap?.sectors || [];
      const svg = trackMapSvg(trackMap, markers, sectors, trackMapSettings);
      contentEl.innerHTML = `
        <div class="track">
          ${svg}
        </div>`;
      setStatus(live, spatial.hasData || race.hasData ? 'live | track map' : 'waiting for position');
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
            ? `<path d="${pathForGeometry(trackMap.pitLane, transform)}" fill="none" stroke="rgba(98,199,255,0.74)" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"></path>`
            : '';
          const sectorPaths = sectorHighlightPaths(sectors, racingLine, transform);
          const dots = markers.map((marker) => markerSvg(marker, pointOnGeometry(racingLine, transform, marker.lapDistPct))).join('');
          return `
            <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
              ${interiorPath}
              <path d="${racingPath}" fill="none" stroke="rgba(255,255,255,0.32)" stroke-width="11" stroke-linecap="round" stroke-linejoin="round"></path>
              <path d="${racingPath}" fill="none" stroke="rgba(222,238,246,1)" stroke-width="4.4" stroke-linecap="round" stroke-linejoin="round"></path>
              ${sectorPaths}
              ${pitPath}
              ${dots}
            </svg>`;
        }
      }

      const sectorPaths = circleSectorHighlightPaths(sectors);
      const dots = markers.map((marker) => markerSvg(marker, pointOnCircle(marker.lapDistPct))).join('');
      return `
        <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
          <circle cx="160" cy="160" r="138" fill="${interior}" stroke="none"></circle>
          <circle cx="160" cy="160" r="138" fill="none" stroke="rgba(255,255,255,0.32)" stroke-width="11"></circle>
          <circle cx="160" cy="160" r="138" fill="none" stroke="rgba(222,238,246,1)" stroke-width="4.4"></circle>
          ${sectorPaths}
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
      return highlight === 'best-lap' ? '#b65cff' : '#50d67c';
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
      const scoring = live?.models?.scoring || {};
      const scoringByCarIdx = new Map((scoring.rows || []).map((row) => [row.carIdx, row]));
      const referenceCarIdx = scoring.referenceCarIdx
        ?? live?.models?.timing?.focusCarIdx
        ?? live?.models?.timing?.playerCarIdx
        ?? live?.models?.spatial?.referenceCarIdx
        ?? live?.latestSample?.focusCarIdx
        ?? live?.latestSample?.playerCarIdx
        ?? null;
      const rows = [
        ...(live?.models?.timing?.overallRows || []),
        ...(live?.models?.timing?.classRows || [])
      ];
      for (const row of rows) {
        if (row.hasSpatialProgress === false) continue;
        if (!Number.isFinite(row.lapDistPct) || row.lapDistPct < 0) continue;
        const scoringRow = scoringByCarIdx.get(row.carIdx);
        const isFocus = Boolean(
          row.isFocus
          || row.isPlayer
          || row.carIdx === referenceCarIdx
          || scoringRow?.isFocus
          || scoringRow?.isPlayer);
        const marker = {
          carIdx: row.carIdx,
          lapDistPct: normalizeProgress(row.lapDistPct),
          isFocus,
          color: isFocus ? '#62c7ff' : markerColor(scoringRow?.carClassColorHex || row.carClassColorHex),
          positionLabel: isFocus ? positionLabel(scoringRow) || positionLabel(row) : null
        };
        const existing = markers.get(row.carIdx);
        if (!existing || marker.isFocus || !existing.isFocus) {
          markers.set(row.carIdx, marker);
        }
      }

      const focusCarIdx = referenceCarIdx ?? -1;
      if (Number.isFinite(fallbackFocusPct) && fallbackFocusPct >= 0) {
        markers.set(focusCarIdx, {
          carIdx: focusCarIdx,
          lapDistPct: normalizeProgress(fallbackFocusPct),
          isFocus: true,
          color: '#62c7ff',
          positionLabel: focusPositionLabel(live, scoringByCarIdx, focusCarIdx)
        });
      }

      return [...markers.values()].sort((left, right) => Number(left.isFocus) - Number(right.isFocus) || left.carIdx - right.carIdx);
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

    function positionLabel(row) {
      const position = row?.classPosition ?? row?.overallPosition;
      return Number.isFinite(position) && position > 0 ? `P${position}` : null;
    }

    function focusPositionLabel(live, scoringByCarIdx, focusCarIdx) {
      const timing = live?.models?.timing || {};
      const scoringRow = scoringByCarIdx?.get(focusCarIdx);
      if (scoringRow) return positionLabel(scoringRow);
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
