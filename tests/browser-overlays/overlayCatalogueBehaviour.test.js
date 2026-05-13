import { afterEach, describe, expect, it } from 'vitest';
import { browserOverlayPages, freshLiveSnapshot, renderBrowserOverlay } from './browserOverlayTestHost.js';

let currentOverlay;

afterEach(() => {
  currentOverlay?.close();
  currentOverlay = null;
});

describe('browser overlay catalogue behaviour', () => {
  it('covers every supported browser overlay route', () => {
    expect(browserOverlayPages().map((page) => page.page.id).sort()).toEqual([
      'car-radar',
      'fuel-calculator',
      'gap-to-leader',
      'garage-cover',
      'input-state',
      'pit-service',
      'relative',
      'session-weather',
      'standings',
      'stream-chat',
      'track-map'
    ]);
  });

  for (const scenario of browserScenarios()) {
    it(`renders ${scenario.id} catalogue behaviour`, async () => {
      currentOverlay = await renderBrowserOverlay(scenario.id, scenario.fixture());

      await scenario.assert(currentOverlay);
    });
  }
});

function browserScenarios() {
  return [
    {
      id: 'standings',
      fixture: () => ({
        live: freshLiveSnapshot({}),
        model: tableModel('standings', 'Standings', 'P2 | 3 shown', standingsColumns(), [
          row(['1', '#11', 'Class Leader', 'Lap 13', '0.0']),
          row(['2', '#10', 'Reference Driver', '--', '--'], { isReference: true }),
          row(['3', '#12', 'Chase Driver', '--', '--'])
        ])
      }),
      assert: ({ document }) => {
        expect(rowText(document)).toEqual([
          '1 #11 Class Leader Lap 13 0.0',
          '2 #10 Reference Driver -- --',
          '3 #12 Chase Driver -- --'
        ]);
        expect(document.body.textContent).not.toMatch(/\d(?:\.\d+)?L\b/i);
      }
    },
    {
      id: 'relative',
      fixture: () => ({
        live: freshLiveSnapshot({}),
        model: tableModel('relative', 'Relative', '5 - 2/4 cars', relativeColumns(), [
          row(['3', '#34 Near Ahead', '-2.350']),
          row(['5', '#55 Focus Driver', '0.000'], { isReference: true }),
          row(['6', '#61 Near Behind', '+1.200', 'IN'], { isPit: true })
        ])
      }),
      assert: ({ document }) => {
        expect(rowText(document)).toEqual([
          '3 #34 Near Ahead -2.350',
          '5 #55 Focus Driver 0.000',
          '6 #61 Near Behind +1.200 IN'
        ]);
        expect(document.body.textContent).not.toMatch(/\d(?:\.\d+)?L\b/i);
      }
    },
    {
      id: 'fuel-calculator',
      fixture: () => ({
        live: freshLiveSnapshot({}),
        model: metricsModel('fuel-calculator', 'Fuel Calculator', 'fuel live | 31.2 laps', [
          metric('Fuel', '73.4 L', 'info'),
          metric('Burn', '2.35 L/lap', 'normal'),
          metric('Window', '24 laps', 'success')
        ]),
        waitForSelector: '.metric'
      }),
      assert: ({ document }) => {
        expect(metricText(document)).toContain('Fuel 73.4 L');
        expect(document.getElementById('status').textContent).toBe('fuel live | 31.2 laps');
      }
    },
    {
      id: 'session-weather',
      fixture: () => ({
        live: freshLiveSnapshot({}),
        model: metricsModel('session-weather', 'Session / Weather', 'Race', [
          metric('Session', 'Race | team', 'info'),
          metric('Surface', 'Dry | rubber moderate', 'success'),
          metric('Wind', 'NW 13 km/h', 'normal')
        ]),
        waitForSelector: '.metric'
      }),
      assert: ({ document }) => {
        expect(metricText(document)).toContain('Surface Dry | rubber moderate');
        expect(document.getElementById('status').textContent).toBe('Race');
      }
    },
    {
      id: 'pit-service',
      fixture: () => ({
        live: freshLiveSnapshot({}),
        model: metricsModel('pit-service', 'Pit Service', 'hold', [
          metric('Release', 'RED - service active', 'error'),
          metric('Fuel request', '31.6 L', 'normal'),
          metric('Tires', 'four tires', 'warning')
        ]),
        waitForSelector: '.metric'
      }),
      assert: ({ document }) => {
        expect(metricText(document)).toContain('Release RED - service active');
        expect(document.getElementById('status').textContent).toBe('hold');
      }
    },
    {
      id: 'gap-to-leader',
      fixture: () => ({
        live: freshLiveSnapshot({}),
        model: {
          overlayId: 'gap-to-leader',
          title: 'Gap To Leader',
          status: 'P2 +4.2',
          source: 'source: race-progress',
          bodyKind: 'graph',
          columns: [],
          rows: [],
          metrics: [],
          points: [8, 6, 4.2]
        },
        waitForSelector: '.model-graph'
      }),
      assert: ({ document }) => {
        expect(document.querySelector('.model-graph')).not.toBeNull();
        expect(document.getElementById('status').textContent).toBe('P2 +4.2');
      }
    },
    {
      id: 'input-state',
      fixture: () => ({
        live: freshLiveSnapshot({
          raceEvents: { hasData: true, isOnTrack: true, isInGarage: false },
          inputs: {
            hasData: true,
            quality: 'raw',
            throttle: 0.72,
            brake: 1,
            clutch: 0.4,
            steeringWheelAngle: 0.25,
            gear: 3,
            speedMetersPerSecond: 64,
            brakeAbsActive: true
          }
        }),
        settings: {
          showThrottle: true,
          showBrake: true,
          showClutch: true,
          showSteering: true,
          showGear: true,
          showSpeed: true
        },
        waitForSelector: '.input-layout'
      }),
      assert: ({ document }) => {
        expect(document.querySelector('.input-graph')).not.toBeNull();
        expect(document.body.textContent).toContain('ABS');
        expect(document.getElementById('status').textContent).toContain('ABS');
      }
    },
    {
      id: 'car-radar',
      fixture: () => ({
        live: freshLiveSnapshot({
          raceEvents: { hasData: true, isOnTrack: true, isInGarage: false },
          spatial: {
            hasData: true,
            sideStatus: 'left',
            hasCarLeft: true,
            hasCarRight: false,
            strongestMulticlassApproach: { relativeSeconds: -8.4 },
            cars: [
              { carIdx: 12, relativeSeconds: -1.2, relativeMeters: -8, carClassColorHex: '#FFDA59' },
              { carIdx: 14, relativeSeconds: 1.7, relativeMeters: 12, carClassColorHex: '#33CEFF' }
            ]
          }
        }),
        waitForSelector: '.radar-v2'
      }),
      assert: ({ document }) => {
        expect(document.querySelector('.radar-v2')).not.toBeNull();
        expect(document.body.textContent).toContain('8.4s');
      }
    },
    {
      id: 'car-radar',
      fixture: () => ({
        live: freshLiveSnapshot({
          raceEvents: { hasData: true, isOnTrack: false, isInGarage: false },
          spatial: {
            hasData: true,
            sideStatus: 'left',
            hasCarLeft: true,
            hasCarRight: false,
            strongestMulticlassApproach: { relativeSeconds: -8.4 },
            cars: [
              { carIdx: 12, relativeSeconds: -1.2, relativeMeters: -8, carClassColorHex: '#FFDA59' }
            ]
          }
        }),
        waitForSelector: '.empty'
      }),
      assert: ({ document }) => {
        expect(document.querySelector('.radar-v2')).toBeNull();
        expect(document.body.textContent).toContain('Waiting for player in car.');
        expect(document.getElementById('status').textContent).toMatch(/waiting( for player in car)?/);
      }
    },
    {
      id: 'track-map',
      fixture: () => ({
        live: freshLiveSnapshot({
          latestSample: { focusCarIdx: 10, playerCarIdx: 10, focusLapDistPct: 0.42, onPitRoad: false, playerTrackSurface: 3 },
          reference: { focusCarIdx: 10 },
          timing: {
            focusCarIdx: 10,
            focusRow: { carIdx: 10, isFocus: true, lapDistPct: 0.42, hasSpatialProgress: true, hasTakenGrid: false, classPosition: 5 },
            overallRows: [
              { carIdx: 10, isFocus: true, lapDistPct: 0.42, hasSpatialProgress: true, hasTakenGrid: false, classPosition: 5 },
              { carIdx: 11, lapDistPct: 0.28, hasSpatialProgress: true, hasTakenGrid: false, carClassColorHex: '#33CEFF' },
              { carIdx: 12, lapDistPct: 0.58, hasSpatialProgress: true, hasTakenGrid: true, carClassColorHex: '#FFDA59' }
            ],
            classRows: []
          },
          spatial: {
            hasData: true,
            referenceCarIdx: 10,
            cars: [{ carIdx: 11, lapDistPct: 0.28, carClassColorHex: '#FFDA59' }]
          },
          raceEvents: { hasData: true, lapDistPct: 0.42 },
          trackMap: {
            hasData: true,
            sectors: [
              { startPct: 0, endPct: 0.33, highlight: 'personal-best' },
              { startPct: 0.33, endPct: 0.66, highlight: 'best-lap' }
            ]
          }
        }),
        settings: {
          trackMap: trackMapAsset(),
          trackMapSettings: { internalOpacity: 0.88, showSectorBoundaries: true }
        },
        waitForSelector: '.track svg'
      }),
      assert: ({ document }) => {
        expect(document.querySelector('.track svg')).not.toBeNull();
        expect(document.querySelectorAll('.track circle, .track path').length).toBeGreaterThan(2);
        expect(document.querySelectorAll('.track circle[fill="#33CEFF"]').length).toBe(0);
        expect(document.querySelectorAll('.track circle[fill="#FFDA59"]').length).toBe(1);
        expect(document.querySelectorAll('.track circle[fill="var(--tmr-cyan)"]').length).toBe(1);
        expect(document.getElementById('status').textContent).toBe('live | track map');
      }
    },
    {
      id: 'garage-cover',
      fixture: () => ({
        live: freshLiveSnapshot({
          raceEvents: { hasData: true, isGarageVisible: true }
        }),
        settings: { hasImage: false, imageVersion: null, fallbackReason: 'test', previewVisible: false },
        waitForSelector: '.garage-cover'
      }),
      assert: ({ document }) => {
        expect(document.querySelector('.garage-cover')).not.toBeNull();
        expect(document.body.textContent).toContain('TMR');
        expect(document.getElementById('status').textContent).toBe('garage visible');
      }
    },
    {
      id: 'stream-chat',
      fixture: () => ({
        live: freshLiveSnapshot({}),
        settings: {
          provider: 'none',
          isConfigured: false,
          streamlabsWidgetUrl: null,
          twitchChannel: null,
          status: 'not_configured'
        },
        waitForSelector: '.chat-line'
      }),
      assert: ({ document }) => {
        expect(document.querySelector('.chat-line')).not.toBeNull();
        expect(document.querySelector('.chat-name').textContent).toBe('TMR');
        expect(document.getElementById('status').textContent).toBe('waiting for chat source');
      }
    }
  ];
}

function tableModel(overlayId, title, status, columns, rows) {
  return {
    overlayId,
    title,
    status,
    source: 'source: catalogue behaviour',
    bodyKind: 'table',
    columns,
    rows,
    metrics: []
  };
}

function metricsModel(overlayId, title, status, metrics) {
  return {
    overlayId,
    title,
    status,
    source: 'source: catalogue behaviour',
    bodyKind: 'metrics',
    columns: [],
    rows: [],
    metrics
  };
}

function standingsColumns() {
  return [
    column('standings.class-position', 'CLS', 'class-position', 35, 'right'),
    column('standings.car-number', 'CAR', 'car-number', 50, 'right'),
    column('standings.driver', 'Driver', 'driver', 250, 'left'),
    column('standings.gap', 'GAP', 'gap', 60, 'right'),
    column('standings.interval', 'INT', 'interval', 60, 'right'),
    column('standings.pit', 'PIT', 'pit', 30, 'right')
  ];
}

function relativeColumns() {
  return [
    column('relative.position', 'Pos', 'relative-position', 38, 'right'),
    column('relative.driver', 'Driver', 'driver', 180, 'left'),
    column('relative.gap', 'Delta', 'gap', 70, 'right'),
    column('relative.pit', 'Pit', 'pit', 30, 'right')
  ];
}

function column(id, label, dataKey, width, alignment) {
  return { id, label, dataKey, width, alignment };
}

function row(cells, extra = {}) {
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

function metric(label, value, tone) {
  return { label, value, tone };
}

function rowText(document) {
  return [...document.querySelectorAll('tbody tr')]
    .map((rowElement) => [...rowElement.querySelectorAll('td')]
      .map((cell) => cell.textContent.replace(/\s+/g, ' ').trim())
      .join(' ')
      .trim());
}

function metricText(document) {
  return [...document.querySelectorAll('.metric')]
    .map((metricElement) => metricElement.textContent.replace(/\s+/g, ' ').trim());
}

function trackMapAsset() {
  return {
    racingLine: {
      closed: true,
      points: [
        { x: 0, y: 48, lapDistPct: 0 },
        { x: 65, y: 92, lapDistPct: 0.16 },
        { x: 142, y: 74, lapDistPct: 0.34 },
        { x: 176, y: 0, lapDistPct: 0.52 },
        { x: 102, y: -62, lapDistPct: 0.72 },
        { x: 18, y: -28, lapDistPct: 0.88 },
        { x: 0, y: 48, lapDistPct: 1 }
      ]
    },
    pitLane: {
      closed: false,
      points: [
        { x: 22, y: 36, lapDistPct: 0.03 },
        { x: 78, y: 56, lapDistPct: 0.11 }
      ]
    }
  };
}
