import { readdirSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { resolve } from 'node:path';
import {
  browserAssetRoot,
  browserOverlayApiResponse,
  browserOverlayPage,
  browserOverlayPages,
  freshLiveSnapshot,
  renderOverlayHtml,
  renderOverlayIndexHtml,
  renderAppValidatorReviewHtml,
  renderSettingsGeneralReviewHtml
} from '../../tests/browser-overlays/browserOverlayAssets.js';

const port = Number.parseInt(process.env.TMR_BROWSER_REVIEW_PORT || '5177', 10);
const clients = new Set();
let reloadTimer = null;

const server = createServer((request, response) => {
  const url = new URL(request.url || '/', `http://${request.headers.host || `localhost:${port}`}`);
  const path = normalizePath(url.pathname);

  try {
    if (path === '/review/events') {
      serveEvents(request, response);
      return;
    }

    const apiPayload = reviewApiResponse(path, url.searchParams);
    if (apiPayload) {
      serveJson(response, apiPayload);
      return;
    }

    if (path === '/' || path === '/review' || path === '/review/overlays') {
      serveHtml(response, withLiveReload(renderOverlayIndexHtml(port)));
      return;
    }

    if (path === '/review/app') {
      serveHtml(response, withLiveReload(renderAppValidatorReviewHtml({
        previewMode: url.searchParams.get('preview') || 'off'
      })));
      return;
    }

    if (path === '/review/settings/general') {
      serveHtml(response, withLiveReload(renderSettingsGeneralReviewHtml({
        previewMode: url.searchParams.get('preview') || 'off'
      })));
      return;
    }

    const overlayId = overlayIdFromPath(path);
    if (overlayId) {
      serveHtml(response, withLiveReload(renderOverlayHtml(overlayId)));
      return;
    }

    serveText(response, 404, 'Not found');
  } catch (error) {
    serveText(response, 500, error instanceof Error ? error.stack || error.message : String(error));
  }
});

server.listen(port, '127.0.0.1', () => {
  console.log(`Browser review server: http://127.0.0.1:${port}/review`);
  console.log(`Overlay routes:        http://127.0.0.1:${port}/overlays/standings`);
  console.log(`Asset root:            ${browserAssetRoot}`);
});

startAssetPolling();

function reviewApiResponse(path, searchParams = new URLSearchParams()) {
  const previewMode = normalizePreviewMode(searchParams.get('preview'));
  if (path === '/api/snapshot') {
    return { live: reviewLiveSnapshot(previewMode) };
  }

  if (path.startsWith('/api/overlay-model/')) {
    const overlayId = decodeURIComponent(path.slice('/api/overlay-model/'.length));
    const page = browserOverlayPage(overlayId);
    return { model: reviewDisplayModel(page.page.id, previewMode) };
  }

  const page = browserOverlayPages().find((candidate) => candidate.settingsRoute === path);
  if (!page) {
    return null;
  }

  return browserOverlayApiResponse(page.page.id, path, {
    live: reviewLiveSnapshot(previewMode),
    settings: reviewSettings(page.page.id, previewMode),
    model: reviewDisplayModel(page.page.id, previewMode)
  });
}

function startAssetPolling() {
  let assetSignature = readAssetSignature(browserAssetRoot);
  const interval = setInterval(() => {
    try {
      const nextSignature = readAssetSignature(browserAssetRoot);
      if (nextSignature !== assetSignature) {
        assetSignature = nextSignature;
        broadcastReload();
      }
    } catch (error) {
      console.warn(`Live reload polling failed: ${formatError(error)}`);
    }
  }, 500);

  if (typeof interval.unref === 'function') {
    interval.unref();
  }

  console.log('Live reload polling browser assets every 500ms.');
}

function readAssetSignature(root) {
  const files = listAssetFiles(root);
  let maxMtimeMs = 0;

  for (const file of files) {
    const stats = statSync(file);
    if (stats.mtimeMs > maxMtimeMs) {
      maxMtimeMs = stats.mtimeMs;
    }
  }

  return `${files.length}:${maxMtimeMs}`;
}

function listAssetFiles(root) {
  const files = [];

  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const child = resolve(root, entry.name);
    if (entry.isDirectory()) {
      files.push(...listAssetFiles(child));
    } else if (entry.isFile()) {
      files.push(child);
    }
  }

  return files;
}

function formatError(error) {
  return error instanceof Error ? error.message : String(error);
}

function overlayIdFromPath(path) {
  const reviewPrefix = '/review/overlays/';
  if (path.startsWith(reviewPrefix)) {
    return decodeURIComponent(path.slice(reviewPrefix.length));
  }

  const overlayPrefix = '/overlays/';
  if (path.startsWith(overlayPrefix)) {
    return decodeURIComponent(path.slice(overlayPrefix.length));
  }

  return null;
}

function reviewLiveSnapshot(previewMode = 'off') {
  const sessionKind = previewMode === 'off' ? 'practice' : previewMode;
  const sessionType = sessionKind === 'qualifying' ? 'Qualify' : titleCase(sessionKind);
  return freshLiveSnapshot({
    session: {
      hasData: true,
      sessionType,
      sessionName: `${sessionType} Preview`,
      eventType: sessionType,
      currentSessionNum: sessionKind === 'race' ? 2 : sessionKind === 'qualifying' ? 1 : 0,
      sessionTimeSeconds: sessionKind === 'race' ? 62571.436719 : 460,
      sessionTimeRemainSeconds: sessionKind === 'race' ? 23828.563281 : 740,
      sessionLapsRemainEx: sessionKind === 'race' ? 32767 : null,
      sessionLapsTotal: sessionKind === 'race' ? 32767 : null,
      trackDisplayName: 'Gesamtstrecke 24h',
      carScreenName: 'Aston Martin Vantage GT3 EVO'
    },
    inputs: {
      hasData: true,
      throttle: 0.78,
      brake: 0.16,
      clutch: 0,
      steeringWheelAngle: -0.18,
      gear: 4,
      speedMetersPerSecond: sessionKind === 'race' ? 77.889366 : 63.4,
      brakeAbsActive: true
    },
    raceEvents: {
      hasData: true,
      isOnTrack: true,
      isInGarage: false,
      isGarageVisible: false,
      lapDistPct: 0.42
    },
    spatial: {
      hasData: true,
      referenceCarIdx: 71,
      referenceLapDistPct: 0.42,
      hasCarLeft: true,
      hasCarRight: false,
      sideStatus: 'left',
      strongestMulticlassApproach: {
        relativeSeconds: -2.4
      },
      cars: [
        { carIdx: 33, relativeSeconds: -1.1, carClassColorHex: '#33ceff' },
        { carIdx: 91, relativeSeconds: 1.7, carClassColorHex: '#ffaa00' },
        { carIdx: 12, relativeSeconds: -2.8, carClassColorHex: '#ff4fd8' }
      ]
    },
    trackMap: {
      sectors: [
        { startPct: 0, endPct: 0.32, highlight: 'personal-best' },
        { startPct: 0.32, endPct: 0.68, highlight: 'none' },
        { startPct: 0.68, endPct: 1, highlight: 'best-lap' }
      ]
    }
  });
}

function reviewSettings(overlayId, previewMode = 'off') {
  if (overlayId === 'stream-chat') {
    return {
      provider: 'none',
      isConfigured: false,
      streamlabsWidgetUrl: null,
      twitchChannel: null,
      status: 'not_configured'
    };
  }

  if (overlayId === 'garage-cover') {
    return {
      hasImage: false,
      imageVersion: null,
      fallbackReason: 'not_configured',
      previewVisible: previewMode !== 'off'
    };
  }

  if (overlayId === 'track-map') {
    return {
      trackMap: reviewTrackMap(),
      trackMapSettings: {
        internalOpacity: 0.88,
        showSectorBoundaries: true
      }
    };
  }

  if (overlayId === 'input-state') {
    return {
      showThrottle: true,
      showBrake: true,
      showClutch: true,
      showSteering: true,
      showGear: true,
      showSpeed: true
    };
  }

  return {};
}

function reviewDisplayModel(overlayId, previewMode = 'off') {
  const previewLabel = previewMode === 'off' ? 'review fixture' : `${previewMode} preview`;
  switch (overlayId) {
    case 'standings':
      return standingsDisplayModel(previewLabel);
    case 'relative':
      return tableModel('relative', 'Relative', `live | ${previewLabel}`, [
        ['AHEAD', 'Kousuke Konishi', '-2.400', 'LMP2'],
        ['YOU', 'Tech Mates Racing', '0.000', 'GT3'],
        ['BEHIND', 'Tommie Wittens', '+3.182', 'GT3']
      ]);
    case 'fuel-calculator':
      return metricsModel('fuel-calculator', 'Fuel Calculator', `live | ${previewLabel}`, [
        ['Fuel', '104.94 L', 'info'],
        ['Laps Left', '12.4', 'normal'],
        ['Target', '3.07 L/lap', 'normal'],
        ['Pit Window', '8 laps', 'warning']
      ]);
    case 'session-weather':
      return metricsModel('session-weather', 'Session / Weather', `live | ${previewLabel}`, [
        ['Track', '31 C', 'normal'],
        ['Air', '22 C', 'normal'],
        ['Wind', '9 km/h', 'normal'],
        ['Humidity', '48%', 'normal']
      ]);
    case 'pit-service':
      return metricsModel('pit-service', 'Pit Service', `live | ${previewLabel}`, [
        ['Fuel Add', '104.94 L', 'info'],
        ['Tires', 'Lefts', 'normal'],
        ['Fast Repair', 'Available', 'success'],
        ['Box', 'Open', 'warning']
      ]);
    case 'gap-to-leader':
      return {
        overlayId,
        title: 'Gap To Leader',
        status: `live | ${previewLabel}`,
        source: 'source: review fixture',
        bodyKind: 'graph',
        columns: [],
        rows: [],
        metrics: [],
        points: [74, 72, 70, 68, 66, 65, 63, 61, 60, 58, 55, 53]
      };
    default:
      return tableModel(overlayId, browserOverlayPage(overlayId).title, `live | ${previewLabel}`, []);
  }
}

function tableModel(overlayId, title, status, rows) {
  return {
    overlayId,
    title,
    status,
    source: 'source: review fixture',
    bodyKind: 'table',
    columns: [
      { label: 'POS', dataKey: 'position', width: 52, alignment: 'left' },
      { label: 'Driver', dataKey: 'driver', width: 190, alignment: 'left' },
      { label: 'GAP', dataKey: 'gap', width: 70, alignment: 'right' },
      { label: 'Class', dataKey: 'class', width: 70, alignment: 'left' }
    ],
    rows: rows.map((cells, index) => ({
      cells,
      isClassHeader: false,
      isReference: index === 1,
      isPit: false,
      isPartial: false,
      carClassColorHex: null,
      headerTitle: null,
      headerDetail: null
    })),
    metrics: []
  };
}

function metricsModel(overlayId, title, status, metrics) {
  return {
    overlayId,
    title,
    status,
    source: 'source: review fixture',
    bodyKind: 'metrics',
    columns: [],
    rows: [],
    metrics: metrics.map(([label, value, tone]) => ({ label, value, tone }))
  };
}

function standingsDisplayModel(previewLabel = 'review fixture') {
  return {
    overlayId: 'standings',
    title: 'Standings',
    status: `scoring | ${previewLabel}`,
    source: 'source: preview fixture extremes',
    bodyKind: 'table',
    columns: [
      { label: 'CLS', dataKey: 'class-position', width: 35, alignment: 'right' },
      { label: 'CAR', dataKey: 'car-number', width: 50, alignment: 'right' },
      { label: 'Driver', dataKey: 'driver', width: 250, alignment: 'left' },
      { label: 'GAP', dataKey: 'gap', width: 60, alignment: 'right' },
      { label: 'INT', dataKey: 'interval', width: 60, alignment: 'right' },
      { label: 'PIT', dataKey: 'pit', width: 30, alignment: 'right' }
    ],
    rows: [
      headerRow('LMP2', '2 cars | ~10 laps', '#33CEFF'),
      carRow(['1', '#8', 'Kousuke Konishi', 'Leader', '-45.0', '']),
      headerRow('GT3', '3 cars | ~12.4 laps', '#FFAA00'),
      carRow(['1', '#000', 'Kauan Vigliazzi Teixeira Lemos', 'Leader', '-2.0', '']),
      carRow(['24', '#3094', 'Tech Mates Racing', '+3.4', '0.0', ''], { isReference: true }),
      carRow(['49', '#60', 'Tommie Wittens', '+8.9', '+5.5', 'IN'], { isPit: true })
    ],
    metrics: []
  };
}

function normalizePreviewMode(mode) {
  const normalized = String(mode || '').trim().toLowerCase();
  return ['practice', 'qualifying', 'race'].includes(normalized) ? normalized : 'off';
}

function titleCase(value) {
  const text = String(value || '');
  return text.length ? text.charAt(0).toUpperCase() + text.slice(1) : '';
}

function headerRow(headerTitle, headerDetail, carClassColorHex) {
  return {
    cells: [],
    isClassHeader: true,
    isReference: false,
    isPit: false,
    isPartial: false,
    carClassColorHex,
    headerTitle,
    headerDetail
  };
}

function carRow(cells, extra = {}) {
  return {
    cells,
    isClassHeader: false,
    isReference: false,
    isPit: false,
    isPartial: false,
    carClassColorHex: null,
    headerTitle: null,
    headerDetail: null,
    ...extra
  };
}

function reviewTrackMap() {
  return {
    racingLine: {
      closed: true,
      points: [
        { x: 0, y: 48, lapDistPct: 0 },
        { x: 65, y: 92, lapDistPct: 0.16 },
        { x: 132, y: 82, lapDistPct: 0.32 },
        { x: 170, y: 12, lapDistPct: 0.48 },
        { x: 112, y: -48, lapDistPct: 0.64 },
        { x: 28, y: -36, lapDistPct: 0.82 },
        { x: 0, y: 48, lapDistPct: 1 }
      ]
    },
    pitLane: {
      closed: false,
      points: [
        { x: 6, y: 42, lapDistPct: 0.02 },
        { x: 42, y: 24, lapDistPct: 0.08 },
        { x: 88, y: 30, lapDistPct: 0.14 }
      ]
    }
  };
}

function withLiveReload(html) {
  const script = `
  <script>
    (() => {
      const events = new EventSource('/review/events');
      events.addEventListener('reload', () => window.location.reload());
    })();
  </script>`;
  return html.replace('</body>', `${script}\n</body>`);
}

function serveEvents(request, response) {
  response.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    Connection: 'keep-alive'
  });
  response.write('\n');
  clients.add(response);
  request.on('close', () => clients.delete(response));
}

function broadcastReload() {
  for (const client of clients) {
    client.write('event: reload\ndata: assets\n\n');
  }
}

function serveHtml(response, body) {
  response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
  response.end(body);
}

function serveJson(response, payload) {
  response.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
  response.end(JSON.stringify(payload));
}

function serveText(response, status, body) {
  response.writeHead(status, { 'Content-Type': 'text/plain; charset=utf-8' });
  response.end(body);
}

function normalizePath(path) {
  return resolve('/', path).replaceAll('\\', '/');
}
