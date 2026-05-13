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
const productionOverlayModelIds = new Set([
  'standings',
  'relative',
  'fuel-calculator',
  'session-weather',
  'pit-service',
  'input-state',
  'gap-to-leader'
]);
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
    const overlayId = decodeURIComponent(path.slice('/api/overlay-model/'.length)).trim().toLowerCase();
    if (!productionOverlayModelIds.has(overlayId)) {
      return null;
    }

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
    model: productionOverlayModelIds.has(page.page.id)
      ? reviewDisplayModel(page.page.id, previewMode)
      : null
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
      return relativeDisplayModel(previewLabel);
    case 'fuel-calculator':
      return metricsModel('fuel-calculator', 'Fuel Calculator', '3 stints / 2 stops', [
        ['Plan', '31 laps | 3 stints | final 7', 'modeled'],
        ['Strategy', '12-lap rhythm avoids +1 stop | ~52s | save 0.2 L/lap', 'modeled'],
        ['Stint 1', '12 laps | target 3.1 L/lap | tires free (36.8 L)', 'modeled'],
        ['Stint 2', '12 laps | target 3.1 L/lap | tires free (36.8 L)', 'modeled'],
        ['Stint 3', '7 laps final | target 3.1 L/lap | --', 'modeled']
      ], 'burn 3.1 L/lap (live burn) | 34.2 laps/tank | history user | tires user pit history | gap O0.18 C0.04');
    case 'session-weather':
      return metricsModel('session-weather', 'Session / Weather', 'Race', [
        ['Session', `Race | ${previewLabel} | team`, 'normal'],
        ['Clock', '17:22 elapsed | 6:37:08 left', 'normal'],
        ['Laps', '-- left | 179 total', 'normal'],
        ['Track', 'Gesamtstrecke 24h | 25.38 km', 'normal'],
        ['Temps', 'air 22 C | track 31 C', 'normal'],
        ['Surface', 'dry | rubber moderate usage', 'normal'],
        ['Sky', 'partly cloudy | constant | rain:0%', 'normal'],
        ['Wind', 'S | 15 km/h | hum 48% | fog 0%', 'normal']
      ], 'source: session + live weather telemetry');
    case 'pit-service':
      return metricsModel('pit-service', 'Pit Service', '', [
        ['Time / Laps', '03:58 | 148/179 laps', 'normal'],
        metricRow('Release', 'RED - service active', 'error', undefined, { rowColorHex: '#FF6274' }),
        metricRow('Pit status', 'in progress', 'error', undefined, { rowColorHex: '#FF6274' }),
        metricRow('Fuel request', 'requested | 31.6 L', 'normal', [
          metricSegment('Requested', 'Yes', 'success'),
          metricSegment('Selected', '31.6 L', 'info')
        ]),
        metricRow('Tearoff', 'requested', 'normal', [
          metricSegment('Requested', 'Yes', 'success')
        ]),
        metricRow('Repair', '12s required', 'error', [
          metricSegment('Required', '12s', 'error'),
          metricSegment('Optional', '18s', 'warning')
        ]),
        metricRow('Fast repair', 'selected | available 1', 'normal', [
          metricSegment('Selected', 'Yes', 'success'),
          metricSegment('Available', '1', 'success')
        ])
      ], 'source: player/team pit service telemetry', [
        {
          title: 'Tire Analysis',
          headers: ['Info', 'FL', 'FR', 'RL', 'RR'],
          rows: [
            gridRow('Compound', [
              gridCell('S', 'success'),
              gridCell('S', 'success'),
              gridCell('S', 'success'),
              gridCell('S', 'success')
            ]),
            gridRow('Change', [
              gridCell('Change', 'success'),
              gridCell('Change', 'success'),
              gridCell('Keep', 'info'),
              gridCell('Change', 'success')
            ]),
            gridRow('Set limit', ['4 sets', '4 sets', '4 sets', '4 sets']),
            gridRow('Available', ['2', '2', gridCell('0', 'error'), '2']),
            gridRow('Wear', ['92/91/90%', '93/92/91%', '96/95/94%', '97/96/95%'])
          ]
        }
      ], [
        {
          title: 'Session',
          rows: [
            ['Time / Laps', '03:58 | 148/179 laps', 'normal']
          ]
        },
        {
          title: 'Pit Signal',
          rows: [
            metricRow('Release', 'RED - service active', 'error', undefined, { rowColorHex: '#FF6274' }),
            metricRow('Pit status', 'in progress', 'error', undefined, { rowColorHex: '#FF6274' })
          ]
        },
        {
          title: 'Service Request',
          rows: [
            metricRow('Fuel request', 'requested | 31.6 L', 'normal', [
              metricSegment('Requested', 'Yes', 'success'),
              metricSegment('Selected', '31.6 L', 'info')
            ]),
            metricRow('Tearoff', 'requested', 'normal', [
              metricSegment('Requested', 'Yes', 'success')
            ]),
            metricRow('Repair', '12s required', 'error', [
              metricSegment('Required', '12s', 'error'),
              metricSegment('Optional', '18s', 'warning')
            ]),
            metricRow('Fast repair', 'selected | available 1', 'normal', [
              metricSegment('Selected', 'Yes', 'success'),
              metricSegment('Available', '1', 'success')
            ])
          ]
        }
      ], [
        { key: 'status', value: '' },
        { key: 'timeRemaining', value: '03:58' }
      ]);
    case 'input-state':
      return metricsModel('input-state', 'Inputs', '4 | 7250 rpm | ABS', [
        ['Speed', '231 km/h', 'normal'],
        ['Gear / RPM', '4 | 7250 rpm', 'normal'],
        ['Pedals', 'T 78% | B 16% ABS | C 0%', 'normal'],
        ['Steering', '-10 deg', 'normal'],
        ['Warnings', 'none', 'normal'],
        ['Electrical', '13.8 V', 'normal'],
        ['Cooling', '88 C', 'normal'],
        ['Oil / Fuel', 'oil 96 C | oil 5.4 bar | fuel 4.1 bar', 'normal']
      ], 'source: local car telemetry');
    case 'gap-to-leader':
      return {
        overlayId,
        title: 'Gap To Leader',
        status: 'live | race gap',
        source: 'source: live gap telemetry | cars 4',
        bodyKind: 'graph',
        columns: [],
        rows: [],
        metrics: [],
        points: [74, 72, 70, 68, 66, 65, 63, 61, 60, 58, 55, 53],
        headerItems: [{ key: 'status', value: 'live | race gap' }]
      };
    default:
      return tableModel(overlayId, browserOverlayPage(overlayId).title, `live | ${previewLabel}`, []);
  }
}

function relativeDisplayModel(previewLabel = 'review fixture') {
  return {
    overlayId: 'relative',
    title: 'Relative',
    status: `5 - 2/4 cars | ${previewLabel}`,
    source: 'source: review fixture',
    bodyKind: 'table',
    columns: [
      { id: 'relative.position', label: 'Pos', dataKey: 'relative-position', width: 38, alignment: 'right' },
      { id: 'relative.driver', label: 'Driver', dataKey: 'driver', width: 180, alignment: 'left' },
      { id: 'relative.gap', label: 'Delta', dataKey: 'gap', width: 70, alignment: 'right' }
    ],
    rows: [
      relativeRow(['3', '#34 Near Ahead', '-2.350'], { carClassColorHex: '#33CEFF', relativeLapDelta: 1 }),
      relativeRow(['5', '#55 Focus Driver', '0.000'], { isReference: true, carClassColorHex: '#FFDA59', relativeLapDelta: 0 }),
      relativeRow(['6', '#61 Near Behind', '+1.200'], { carClassColorHex: '#FF4FD8', relativeLapDelta: -2 })
    ],
    metrics: [],
    points: [],
    headerItems: [{ key: 'status', value: `5 - 2/4 cars | ${previewLabel}` }]
  };
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

function relativeRow(cells, extra = {}) {
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

function metricsModel(
  overlayId,
  title,
  status,
  metrics,
  source = 'source: review fixture',
  gridSections = [],
  metricSections = [],
  headerItems = [{ key: 'status', value: status }]) {
  return {
    overlayId,
    title,
    status,
    source,
    bodyKind: 'metrics',
    columns: [],
    rows: [],
    metrics: metrics.map(metricModelRow),
    points: [],
    headerItems,
    gridSections,
    metricSections: metricSections.map((section) => ({
      title: section.title,
      rows: section.rows.map(metricModelRow)
    }))
  };
}

function metricRow(label, value, tone, segments = undefined, extra = {}) {
  return segments ? { label, value, tone, segments, ...extra } : { label, value, tone, ...extra };
}

function metricSegment(label, value, tone) {
  return { label, value, tone };
}

function metricModelRow(row) {
  if (!Array.isArray(row)) {
    return {
      label: row?.label || '',
      value: row?.value || '--',
      tone: row?.tone || 'normal',
      ...(Array.isArray(row?.segments) && row.segments.length > 0 ? { segments: row.segments } : {}),
      ...(row?.rowColorHex ? { rowColorHex: row.rowColorHex } : {})
    };
  }

  const [label, value, tone] = row;
  return { label, value, tone };
}

function gridRow(label, values, tone = 'normal') {
  return {
    label,
    tone,
    cells: values.map((value) => typeof value === 'object' && value !== null
      ? { value: value.value, tone: value.tone || tone }
      : { value, tone })
  };
}

function gridCell(value, tone) {
  return { value, tone };
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
