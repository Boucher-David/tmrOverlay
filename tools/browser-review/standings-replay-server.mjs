import { readFileSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { resolve } from 'node:path';
import {
  browserAssetRoot,
  browserOverlayApiResponse,
  browserOverlayPage,
  browserOverlayPages,
  renderOverlayHtml,
  renderOverlayIndexHtml
} from '../../tests/browser-overlays/browserOverlayAssets.js';

const replayPath = resolve(process.argv[2] || process.env.TMR_STANDINGS_REPLAY_JSON || '');
const port = Number.parseInt(process.env.TMR_STANDINGS_REPLAY_PORT || '5187', 10);
const frameMilliseconds = Number.parseInt(process.env.TMR_STANDINGS_REPLAY_FRAME_MS || '500', 10);
const relativeCarsEachSide = clampInteger(process.env.TMR_RELATIVE_CARS_EACH_SIDE, 3, 0, 8);
const relativeShowPitColumn = parseBoolean(process.env.TMR_RELATIVE_SHOW_PIT_COLUMN, false);
const relativeShowHeaderStatus = parseBoolean(process.env.TMR_RELATIVE_SHOW_HEADER_STATUS, true);
const relativeShowHeaderTimeRemaining = parseBoolean(process.env.TMR_RELATIVE_SHOW_TIME_REMAINING, true);
const relativeShowFooterSource = parseBoolean(process.env.TMR_RELATIVE_SHOW_FOOTER_SOURCE, true);
const productionOverlayModelIds = new Set([
  'standings',
  'relative',
  'fuel-calculator',
  'session-weather',
  'pit-service',
  'input-state',
  'gap-to-leader'
]);
const replay = loadReplay(replayPath);
const startedAtMs = Date.now();

const server = createServer((request, response) => {
  const url = new URL(request.url || '/', `http://${request.headers.host || `localhost:${port}`}`);
  const path = normalizePath(url.pathname);
  try {
    if (path === '/' || path === '/review' || path === '/review/overlays') {
      serveHtml(response, renderOverlayIndexHtml(port));
      return;
    }

    const overlayId = overlayIdFromPath(path);
    if (overlayId) {
      browserOverlayPage(overlayId);
      serveHtml(response, renderOverlayHtml(overlayId));
      return;
    }

    if (path === '/api/snapshot') {
      const { frame, index } = currentFrame(url, request);
      serveJson(response, { live: liveSnapshot(frame, index) });
      return;
    }

    if (path.startsWith('/api/overlay-model/')) {
      const overlayId = decodeURIComponent(path.slice('/api/overlay-model/'.length)).trim().toLowerCase();
      if (!productionOverlayModelIds.has(overlayId)) {
        serveText(response, 404, 'Overlay model not configured');
        return;
      }

      const { frame, index } = currentFrame(url, request);
      serveJson(response, {
        generatedAtUtc: new Date().toISOString(),
        replay: frameMetadata(frame, index),
        model: displayModel(overlayId, frame, index)
      });
      return;
    }

    const settingsPage = browserOverlayPages().find((candidate) => candidate.settingsRoute === path);
    if (settingsPage) {
      const { frame, index } = currentFrame(url, request);
      serveJson(response, browserOverlayApiResponse(settingsPage.page.id, path, {
        live: liveSnapshot(frame, index),
        settings: settings(settingsPage.page.id, frame),
        model: displayModel(settingsPage.page.id, frame, index)
      }));
      return;
    }

    if (path === '/api/replay/status') {
      const { frame, index } = currentFrame(url, request);
      serveJson(response, {
        source: replay.source,
        frameCount: replay.frames.length,
        current: frameMetadata(frame, index),
        assetRoot: browserAssetRoot
      });
      return;
    }

    serveText(response, 404, 'Not found');
  } catch (error) {
    serveText(response, 500, error instanceof Error ? error.stack || error.message : String(error));
  }
});

server.listen(port, '127.0.0.1', () => {
  console.log(`Standings replay:      http://127.0.0.1:${port}/overlays/standings`);
  console.log(`All overlays review:   http://127.0.0.1:${port}/review/overlays`);
  console.log(`Replay status:         http://127.0.0.1:${port}/api/replay/status`);
  console.log(`Replay source:         ${replayPath}`);
  console.log(`Replay frames:         ${replay.frames.length}`);
  console.log(`Frame interval:        ${frameMilliseconds}ms`);
  console.log(`Relative rows:         ${relativeCarsEachSide * 2 + 1}`);
});

function loadReplay(path) {
  if (!path) {
    throw new Error('Pass a replay JSON path as the first argument or TMR_STANDINGS_REPLAY_JSON.');
  }
  const stats = statSync(path);
  if (!stats.isFile()) {
    throw new Error(`Replay path is not a file: ${path}`);
  }
  const parsed = JSON.parse(readFileSync(path, 'utf8'));
  if (!Array.isArray(parsed.frames) || parsed.frames.length === 0) {
    throw new Error(`Replay has no frames: ${path}`);
  }
  return parsed;
}

function currentFrame(url = null, request = null) {
  const override = frameOverride(url, request);
  if (override !== null) {
    const index = Math.max(0, Math.min(replay.frames.length - 1, override));
    return { index, frame: replay.frames[index] };
  }

  const elapsed = Math.max(0, Date.now() - startedAtMs);
  const index = Math.floor(elapsed / Math.max(1, frameMilliseconds)) % replay.frames.length;
  return { index, frame: replay.frames[index] };
}

function frameOverride(url, request) {
  const direct = parseFrame(url?.searchParams);
  if (direct !== null) return direct;
  const referrer = request?.headers?.referer;
  if (!referrer) return null;
  try {
    return parseFrame(new URL(referrer).searchParams);
  } catch {
    return null;
  }
}

function parseFrame(searchParams) {
  const rawFrame = searchParams?.get('frame');
  if (rawFrame !== null && rawFrame !== undefined) {
    const frame = Number.parseInt(rawFrame, 10);
    return Number.isFinite(frame) ? frame : null;
  }

  const rawRelativeSeconds = searchParams?.get('rel');
  if (rawRelativeSeconds !== null && rawRelativeSeconds !== undefined) {
    const relativeSeconds = Number.parseFloat(rawRelativeSeconds);
    if (Number.isFinite(relativeSeconds)) {
      const index = replay.frames.findIndex((frame) => frame.raceStartRelativeSeconds === relativeSeconds);
      return index >= 0 ? index : null;
    }
  }

  return null;
}

function frameMetadata(frame, index) {
  return {
    index,
    captureId: frame.captureId,
    frameIndex: frame.frameIndex,
    sessionTimeSeconds: frame.sessionTimeSeconds,
    sessionInfoUpdate: frame.sessionInfoUpdate,
    sessionState: frame.sessionState,
    sessionPhase: frame.sessionPhase,
    camCarIdx: frame.camCarIdx,
    playerCarIdx: frame.playerCarIdx
  };
}

function liveSnapshot(frame, index) {
  if (frame.live && typeof frame.live === 'object') {
    return {
      ...frame.live,
      sourceId: frame.live.sourceId || replay.source?.captureId || 'capture-replay',
      startedAtUtc: frame.live.startedAtUtc || replay.source?.startedAtUtc || null,
      lastUpdatedAtUtc: new Date().toISOString(),
      sequence: index + 1
    };
  }

  const relativeSeconds = Number.isFinite(frame.raceStartRelativeSeconds)
    ? frame.raceStartRelativeSeconds
    : (frame.sessionState === 4 ? index - 60 : index - 60) * 2;
  const lapProgress = normalizeProgress(0.18 + relativeSeconds / 360);
  const timer = headerTimeRemaining(frame);
  const referenceRow = referenceDisplayRow(frame);
  const onPitRoad = referenceRow?.isPit === true || referenceRow?.cells?.[5] === 'IN';
  const isPreGreen = Number.isFinite(frame.sessionState) && frame.sessionState < 4;
  const isGarageVisible = false;
  const now = new Date().toISOString();
  const inputPhase = (relativeSeconds + 120) / 240;
  const throttle = clamp(0.35 + 0.35 * Math.sin((relativeSeconds + 140) / 18), 0, 1);
  const brake = clamp(isPreGreen ? 0.08 : 0.04 + 0.16 * Math.max(0, Math.sin((relativeSeconds + 10) / 21)), 0, 1);
  const clutch = isPreGreen ? 0.18 : 0;
  const speed = isPreGreen ? Math.max(0, relativeSeconds + 120) * 0.25 : 34 + inputPhase * 38;

  return {
    isConnected: true,
    isCollecting: true,
    sourceId: replay.source?.captureId || 'standings-replay',
    startedAtUtc: replay.source?.startedAtUtc || null,
    lastUpdatedAtUtc: now,
    sequence: index + 1,
    context: {
      session: {
        sessionType: 'Race',
        sessionName: 'Race',
        eventType: 'Race'
      }
    },
    combo: {
      trackDisplayName: 'Race-start replay',
      carScreenName: referenceRow?.cells?.[2] || 'Replay car'
    },
    latestSample: {
      focusCarIdx: frame.camCarIdx,
      focusLapDistPct: lapProgress,
      sessionTime: frame.sessionTimeSeconds,
      sessionState: frame.sessionState
    },
    fuel: {
      fuelLevelLiters: Math.max(0, 74 - Math.max(0, relativeSeconds) * 0.006),
      fuelLevelPercent: 0.72,
      fuelUsePerHourKg: isPreGreen ? 8 : 82
    },
    proximity: {},
    leaderGap: {},
    models: {
      reference: {
        hasData: Number.isFinite(frame.camCarIdx),
        quality: 'inferred',
        playerCarIdx: Number.isFinite(frame.playerCarIdx) ? frame.playerCarIdx : null,
        focusCarIdx: Number.isFinite(frame.camCarIdx) ? frame.camCarIdx : null,
        focusIsPlayer: Number.isFinite(frame.camCarIdx) && Number.isFinite(frame.playerCarIdx) && frame.camCarIdx === frame.playerCarIdx,
        hasExplicitNonPlayerFocus: Number.isFinite(frame.camCarIdx) && Number.isFinite(frame.playerCarIdx) && frame.camCarIdx !== frame.playerCarIdx,
        referenceCarClass: null,
        lapDistPct: lapProgress,
        onPitRoad,
        isOnTrack: !isPreGreen || !onPitRoad,
        isInGarage: false,
        playerCarInPitStall: false
      },
      session: {
        hasData: true,
        quality: 'reliable',
        sessionType: 'Race',
        sessionName: isPreGreen ? 'Race Grid' : 'Race',
        eventType: 'Race',
        currentSessionNum: 2,
        sessionState: frame.sessionState,
        sessionPhase: frame.sessionPhase,
        sessionTimeSeconds: frame.sessionTimeSeconds,
        sessionTimeRemainSeconds: timer?.seconds ?? null,
        trackDisplayName: 'Race-start replay',
        carScreenName: referenceRow?.cells?.[2] || 'Replay car'
      },
      inputs: {
        hasData: true,
        quality: 'reliable',
        throttle,
        brake,
        clutch,
        steeringWheelAngle: 0.25 * Math.sin(relativeSeconds / 13),
        gear: isPreGreen ? 1 : 3,
        speedMetersPerSecond: speed,
        brakeAbsActive: !isPreGreen && brake > 0.12
      },
      raceEvents: {
        hasData: true,
        isOnTrack: !isGarageVisible,
        isInGarage: false,
        isGarageVisible,
        lapDistPct: lapProgress,
        onPitRoad
      },
      spatial: spatialModel(lapProgress, relativeSeconds),
      trackMap: trackMapModel(lapProgress),
      relative: relativeModel(relativeSeconds),
      fuelPit: {
        hasData: true,
        quality: isPreGreen ? 'partial' : 'reliable',
        fuel: {
          fuelLevelLiters: Math.max(0, 74 - Math.max(0, relativeSeconds) * 0.006),
          fuelLevelPercent: 0.72,
          fuelUsePerHourKg: isPreGreen ? 8 : 82
        }
      },
      raceProgress: {
        hasData: !isPreGreen,
        quality: isPreGreen ? 'partial' : 'inferred',
        referenceCarProgressLaps: isPreGreen ? null : 1 + lapProgress
      },
      raceProjection: {
        hasData: !isPreGreen,
        quality: isPreGreen ? 'partial' : 'inferred'
      },
      weather: {
        hasData: true,
        quality: 'reliable',
        airTempC: 21,
        trackTempCrewC: 29,
        trackWetness: 1,
        weatherDeclaredWet: false
      }
    }
  };
}

function displayModel(overlayId, frame, index) {
  if (overlayId === 'standings') {
    return frame.model;
  }

  if (frame.live?.models) {
    return captureDisplayModel(overlayId, frame, index);
  }

  const relativeSeconds = Number.isFinite(frame.raceStartRelativeSeconds)
    ? frame.raceStartRelativeSeconds
    : 0;
  const isPreGreen = Number.isFinite(frame.sessionState) && frame.sessionState < 4;
  const status = `${isPreGreen ? 'pre-green' : 'green'} | ${relativeSeconds >= 0 ? '+' : ''}${relativeSeconds}s`;

  if (overlayId === 'relative') {
    const relative = relativeSettings();
    const relativeModel = syntheticRelativeRows(frame, isPreGreen, relative.carsAhead, relative.carsBehind);
    return relativeTableModel(
      relativeModel.status,
      replayHeaderItems(frame, relativeModel.status, relative),
      relativeModel.rows,
      sourceFromSettings(relative, 'source: race-start replay'),
      relative.columns);
  }

  const headerItems = replayHeaderItems(frame, status);

  if (overlayId === 'fuel-calculator') {
    return metricsModel(overlayId, 'Fuel Calculator', status, headerItems, [
      ['Fuel', isPreGreen ? '74.0 L' : '73.4 L', 'info'],
      ['Burn', isPreGreen ? 'grid idle' : '2.9 L/lap', 'normal'],
      ['Window', isPreGreen ? 'after green' : '24 laps', 'normal'],
      ['Mode', isPreGreen ? 'countdown ignored' : 'timed race', isPreGreen ? 'warning' : 'success']
    ]);
  }

  if (overlayId === 'session-weather') {
    return metricsModel(overlayId, 'Session / Weather', status, headerItems, [
      ['Session', isPreGreen ? 'Race Grid' : 'Race', 'info'],
      ['Track', '29 C', 'normal'],
      ['Air', '21 C', 'normal'],
      ['Wetness', 'Dry', 'success']
    ]);
  }

  if (overlayId === 'pit-service') {
    return metricsModel(overlayId, 'Pit Service', status, headerItems, [
      ['Box', referenceDisplayRow(frame)?.isPit ? 'In pit' : 'Closed', referenceDisplayRow(frame)?.isPit ? 'warning' : 'normal'],
      ['Fuel Add', '--', 'normal'],
      ['Tires', 'None', 'normal'],
      ['Repair', 'Available', 'success']
    ]);
  }

  if (overlayId === 'gap-to-leader') {
    return {
      overlayId,
      title: 'Gap To Leader',
      status,
      source: 'source: race-start replay',
      bodyKind: 'graph',
      columns: [],
      rows: [],
      metrics: [],
      points: isPreGreen ? [] : Array.from({ length: 24 }, (_, point) => 30 - point * 0.7 + Math.sin(point / 2) * 1.4),
      headerItems
    };
  }

  return tableModel(overlayId, browserOverlayPage(overlayId).title, status, headerItems, []);
}

function captureDisplayModel(overlayId, frame, index) {
  const live = liveSnapshot(frame, index);
  const models = live.models || {};
  const relativeSeconds = Number.isFinite(frame.raceStartRelativeSeconds)
    ? frame.raceStartRelativeSeconds
    : null;
  const phase = frame.sessionPhase || models.session?.sessionPhase || 'capture';
  const status = relativeSeconds == null
    ? `${phase} | frame ${frame.frameIndex}`
    : `${phase} | ${relativeSeconds >= 0 ? '+' : ''}${relativeSeconds}s`;

  if (overlayId === 'relative') {
    const relative = relativeSettings();
    const relativeModel = relativeRows(models, relative.carsAhead, relative.carsBehind);
    return relativeTableModel(
      relativeModel.status,
      captureHeaderItems(models, relativeModel.status, relative),
      relativeModel.rows,
      sourceFromSettings(relative, 'source: capture-derived live replay'),
      relative.columns);
  }

  const headerItems = captureHeaderItems(models, status);

  if (overlayId === 'fuel-calculator') {
    const localContext = localInCarOrPitContext(models, 'waiting for local fuel context');
    if (!localContext.isAvailable) {
      return metricsModel(
        overlayId,
        'Fuel Calculator',
        localContext.statusText,
        captureHeaderItems(models, localContext.statusText),
        [],
        'source: waiting');
    }

    const fuel = models.fuelPit?.fuel || live.fuel || {};
    return metricsModel(overlayId, 'Fuel Calculator', status, headerItems, [
      ['Fuel', formatLiters(fuel.fuelLevelLiters), 'info'],
      ['Fuel %', formatPercent(fuel.fuelLevelPercent), 'normal'],
      ['Burn', formatFuelBurn(fuel.fuelUsePerHourKg), 'normal'],
      ['Source', models.fuelPit?.hasData ? 'capture frame' : 'unavailable', models.fuelPit?.hasData ? 'success' : 'waiting']
    ], 'source: capture-derived live replay');
  }

  if (overlayId === 'session-weather') {
    const session = models.session || {};
    const weather = models.weather || {};
    return metricsModel(overlayId, 'Session / Weather', status, headerItems, [
      ['Session', session.sessionName || session.sessionType || '--', 'info'],
      ['Track', formatTemp(weather.trackTempCrewC), 'normal'],
      ['Air', formatTemp(weather.airTempC), 'normal'],
      ['Wetness', trackWetnessLabel(weather.trackWetness, weather.weatherDeclaredWet), weather.weatherDeclaredWet ? 'warning' : 'success']
    ], 'source: capture-derived live replay');
  }

  if (overlayId === 'pit-service') {
    const localContext = localInCarOrPitContext(models, 'waiting for local pit-service context');
    if (!localContext.isAvailable) {
      return metricsModel(
        overlayId,
        'Pit Service',
        localContext.statusText,
        captureHeaderItems(models, localContext.statusText),
        [],
        'source: waiting');
    }

    const reference = models.reference || {};
    const race = models.raceEvents || {};
    return metricsModel(overlayId, 'Pit Service', status, headerItems, [
      ['Box', reference.playerCarInPitStall ? 'In stall' : race.onPitRoad ? 'Pit road' : 'Closed', race.onPitRoad ? 'warning' : 'normal'],
      ['Fuel Add', '--', 'normal'],
      ['Tires', '--', 'normal'],
      ['Repair', '--', 'normal']
    ], 'source: capture-derived live replay');
  }

  if (overlayId === 'gap-to-leader') {
    return {
      overlayId,
      title: 'Gap To Leader',
      status,
      source: 'source: capture-derived live replay',
      bodyKind: 'graph',
      columns: [],
      rows: [],
      metrics: [],
      points: gapTrendPoints(index),
      headerItems
    };
  }

  return tableModel(overlayId, browserOverlayPage(overlayId).title, status, headerItems, [], 'source: capture-derived live replay');
}

function localInCarOrPitContext(models, statusText) {
  const reference = models.reference || {};
  const race = models.raceEvents || {};
  const fuelPit = models.fuelPit || {};
  if (reference.focusIsPlayer === false) {
    return { isAvailable: false, reason: 'focus_on_another_car', statusText };
  }

  if (race.isInGarage === true || race.isGarageVisible === true) {
    return { isAvailable: false, reason: 'garage', statusText };
  }

  if (race.isOnTrack === true) {
    return { isAvailable: true, reason: 'available', statusText: 'live' };
  }

  if (race.onPitRoad === true
    || reference.onPitRoad === true
    || reference.playerCarInPitStall === true
    || fuelPit.onPitRoad === true
    || fuelPit.pitstopActive === true
    || fuelPit.playerCarInPitStall === true
    || fuelPit.teamOnPitRoad === true) {
    return { isAvailable: true, reason: 'available', statusText: 'live' };
  }

  return { isAvailable: false, reason: 'not_in_car', statusText };
}

function captureHeaderItems(models, status, overlaySettings = null) {
  const items = overlaySettings?.showHeaderStatus === false
    ? []
    : [{ key: 'status', value: status }];
  const seconds = models.session?.sessionTimeRemainSeconds;
  if (overlaySettings?.showHeaderTimeRemaining !== false && Number.isFinite(seconds) && seconds >= 0) {
    items.push({ key: 'timeRemaining', value: formatDuration(seconds, models.session?.sessionPhase) });
  }
  return items;
}

function syntheticRelativeRows(frame, isPreGreen, carsAhead, carsBehind) {
  const rows = [
    relativeDisplayRow('2', 'Lap Ahead LMP2', isPreGreen ? '--' : '-8.940', false, '#33CEFF', 'ahead', 1),
    relativeDisplayRow('3', 'Race Start Leader', isPreGreen ? '--' : '-2.412', false, '#FFDA59', 'ahead'),
    relativeDisplayRow('5', referenceDisplayRow(frame)?.cells?.[2] || 'Replay focus', '0.000', true, '#33CEFF', null, 0),
    relativeDisplayRow('6', 'Following GT3', isPreGreen ? '--' : '+3.184', false, '#FFDA59', 'behind'),
    relativeDisplayRow('12', 'Lap Behind GT4', isPreGreen ? '--' : '+11.720', false, '#FF4FD8', 'behind', -2)
  ];
  const ahead = rows.filter((row) => row.isAhead === true).slice(-clamp(carsAhead, 0, 8));
  const reference = rows.find((row) => row.isReference === true);
  const behind = rows.filter((row) => row.isBehind === true).slice(0, clamp(carsBehind, 0, 8));
  const actualRows = [
    ...ahead,
    ...(reference ? [reference] : []),
    ...behind
  ];
  return {
    rows: stableRelativeRows(actualRows, carsAhead, carsBehind),
    status: relativeStatus(reference?.valuesByKey?.['relative-position'], actualRows.filter((row) => !row.isReference).length, rows.length - 1)
  };
}

function relativeRows(models, carsAhead, carsBehind) {
  const referenceCarIdx = models.reference?.focusCarIdx ?? models.relative?.referenceCarIdx;
  const timingByCarIdx = new Map((models.timing?.overallRows || []).map((row) => [row.carIdx, row]));
  const scoringByCarIdx = new Map((models.scoring?.rows || []).map((row) => [row.carIdx, row]));
  const driverByCarIdx = new Map((models.driverDirectory?.drivers || []).map((row) => [row.carIdx, row]));
  const reference = timingByCarIdx.get(referenceCarIdx) || scoringByCarIdx.get(referenceCarIdx) || driverByCarIdx.get(referenceCarIdx);
  const rows = (models.relative?.rows || [])
    .filter((row) => hasRelativeDisplayEvidence(row));
  const ahead = rows
    .filter((row) => isRelativeAhead(row))
    .sort((a, b) => relativeSortKey(a) - relativeSortKey(b) || (a.carIdx ?? 0) - (b.carIdx ?? 0))
    .slice(0, clamp(carsAhead, 0, 8))
    .sort((a, b) => relativeSortKey(b) - relativeSortKey(a) || (a.carIdx ?? 0) - (b.carIdx ?? 0));
  const behind = rows
    .filter((row) => isRelativeBehind(row))
    .sort((a, b) => relativeSortKey(a) - relativeSortKey(b) || (a.carIdx ?? 0) - (b.carIdx ?? 0))
    .slice(0, clamp(carsBehind, 0, 8));
  const referencePosition = reference
    ? positionLabel(reference, scoringByCarIdx.get(referenceCarIdx))
    : null;
  const actualRows = [
    ...ahead.map((row) => relativeDisplayRow(
      positionLabel(row, scoringByCarIdx.get(row.carIdx)),
      displayDriver(row, scoringByCarIdx.get(row.carIdx), driverByCarIdx.get(row.carIdx)),
      relativeDelta(row, 'ahead'),
      false,
      row.carClassColorHex || scoringByCarIdx.get(row.carIdx)?.carClassColorHex || driverByCarIdx.get(row.carIdx)?.carClassColorHex,
      'ahead',
      relativeLapDelta(row, timingByCarIdx.get(row.carIdx), reference))),
    reference
      ? relativeDisplayRow(
        positionLabel(reference, scoringByCarIdx.get(referenceCarIdx)),
        displayDriver(reference, scoringByCarIdx.get(referenceCarIdx), driverByCarIdx.get(referenceCarIdx)),
        '0.000',
        true,
        reference.carClassColorHex || scoringByCarIdx.get(referenceCarIdx)?.carClassColorHex || driverByCarIdx.get(referenceCarIdx)?.carClassColorHex,
        null,
        0)
      : relativeDisplayRow('--', 'Replay focus', '0.000', true, null, null, 0),
    ...behind.map((row) => relativeDisplayRow(
      positionLabel(row, scoringByCarIdx.get(row.carIdx)),
      displayDriver(row, scoringByCarIdx.get(row.carIdx), driverByCarIdx.get(row.carIdx)),
      relativeDelta(row, 'behind'),
      false,
      row.carClassColorHex || scoringByCarIdx.get(row.carIdx)?.carClassColorHex || driverByCarIdx.get(row.carIdx)?.carClassColorHex,
      'behind',
      relativeLapDelta(row, timingByCarIdx.get(row.carIdx), reference)))
  ];
  return {
    rows: stableRelativeRows(actualRows, carsAhead, carsBehind),
    status: relativeStatus(referencePosition, actualRows.filter((row) => !row.isReference).length, rows.length)
  };
}

function stableRelativeRows(rows, carsAhead, carsBehind = carsAhead) {
  const aheadCapacity = clamp(carsAhead, 0, 8);
  const behindCapacity = clamp(carsBehind, 0, 8);
  const reference = rows.find((row) => row.isReference === true);
  const hasReference = Boolean(reference);
  const visibleRows = Math.max(1, Math.min(17, aheadCapacity + behindCapacity + (hasReference ? 1 : 0)));
  const stableRows = Array.from({ length: visibleRows }, () => relativePlaceholderRow());
  const ahead = rows.filter((row) => row.isAhead === true);
  const aheadStart = Math.max(0, aheadCapacity - ahead.length);
  ahead.forEach((row, index) => {
    if (aheadStart + index < stableRows.length) {
      stableRows[aheadStart + index] = row;
    }
  });

  if (hasReference && aheadCapacity < stableRows.length) {
    stableRows[aheadCapacity] = reference;
  }

  const behindStart = hasReference ? aheadCapacity + 1 : aheadCapacity;
  rows.filter((row) => row.isBehind === true).forEach((row, index) => {
    if (behindStart + index < stableRows.length) {
      stableRows[behindStart + index] = row;
    }
  });

  return stableRows;
}

function relativeStatus(referencePosition, shownRows, availableRows) {
  const position = String(referencePosition || '').trim();
  const prefix = position && position !== '--' ? position : 'live relative';
  return availableRows > shownRows
    ? `${prefix} - ${shownRows}/${availableRows} cars`
    : `${prefix} - ${shownRows} cars`;
}

function relativeDisplayRow(position, driver, delta, isReference, carClassColorHex, direction = null, relativeLapDelta = null) {
  return {
    cells: [position || '--', driver || 'Replay focus', delta || '--'],
    valuesByKey: {
      'relative-position': position || '--',
      driver: driver || 'Replay focus',
      gap: delta || '--',
      pit: ''
    },
    isReference,
    isAhead: direction === 'ahead',
    isBehind: direction === 'behind',
    carClassColorHex: carClassColorHex || null,
    isPlaceholder: false,
    relativeLapDelta
  };
}

function relativePlaceholderRow() {
  return {
    cells: ['', '', ''],
    valuesByKey: {},
    isReference: false,
    isAhead: false,
    isBehind: false,
    carClassColorHex: null,
    isPlaceholder: true,
    relativeLapDelta: null
  };
}

function relativeLapDelta(row, timingRow = null, referenceRow = null) {
  const value = row?.lapDeltaToReference ?? row?.relativeLapDelta;
  if (Number.isFinite(value)) return value;
  const carLap = completedLap(timingRow) ?? completedLap(row);
  const referenceLap = completedLap(referenceRow);
  return Number.isFinite(carLap) && Number.isFinite(referenceLap)
    ? carLap - referenceLap
    : null;
}

function completedLap(row) {
  if (Number.isFinite(row?.lapCompleted) && row.lapCompleted >= 0) return row.lapCompleted;
  if (Number.isFinite(row?.progressLaps) && row.progressLaps >= 0) return Math.floor(row.progressLaps);
  return null;
}

function hasRelativeDisplayEvidence(row) {
  return Number.isFinite(row?.relativeSeconds)
    || Number.isFinite(row?.relativeMeters)
    || Number.isFinite(row?.relativeLaps);
}

function isRelativeAhead(row) {
  if (row?.isAhead === true) return true;
  if (row?.isBehind === true) return false;
  return Number.isFinite(row?.relativeSeconds) && row.relativeSeconds < 0;
}

function isRelativeBehind(row) {
  if (row?.isBehind === true) return true;
  if (row?.isAhead === true) return false;
  return Number.isFinite(row?.relativeSeconds) && row.relativeSeconds > 0;
}

function relativeSortKey(row) {
  if (Number.isFinite(row?.relativeSeconds)) return Math.abs(row.relativeSeconds);
  if (Number.isFinite(row?.relativeMeters)) return Math.abs(row.relativeMeters);
  if (Number.isFinite(row?.relativeLaps)) return Math.abs(row.relativeLaps);
  return Number.MAX_VALUE;
}

function relativeDelta(row, direction) {
  const sign = direction === 'ahead' ? '-' : '+';
  if (Number.isFinite(row?.relativeSeconds)) {
    return `${sign}${Math.abs(row.relativeSeconds).toFixed(3)}`;
  }
  if (Number.isFinite(row?.relativeMeters)) {
    return `${sign}${Math.abs(row.relativeMeters).toFixed(0)}m`;
  }
  return '--';
}

function positionLabel(row, fallbackRow = null) {
  const classPosition = Number(row?.classPosition ?? fallbackRow?.classPosition);
  if (Number.isFinite(classPosition) && classPosition > 0) {
    return `${classPosition}`;
  }

  const overallPosition = Number(row?.overallPosition ?? fallbackRow?.overallPosition);
  return Number.isFinite(overallPosition) && overallPosition > 0 ? `${overallPosition}` : '--';
}

function gapTrendPoints(index) {
  const values = [];
  const start = Math.max(0, index - 23);
  for (let frameIndex = start; frameIndex <= index; frameIndex += 1) {
    const frame = replay.frames[frameIndex];
    const models = frame?.live?.models || {};
    const focusCarIdx = models.reference?.focusCarIdx;
    const focus = (models.timing?.classRows || models.timing?.overallRows || [])
      .find((row) => row?.carIdx === focusCarIdx);
    const gap = Number.isFinite(focus?.f2TimeSeconds)
      ? focus.f2TimeSeconds
      : Number.isFinite(focus?.estimatedTimeSeconds)
        ? focus.estimatedTimeSeconds
        : null;
    if (Number.isFinite(gap) && gap >= 0) {
      values.push(gap);
    }
  }
  return values;
}

function displayDriver(row, scoringRow = null, driverRow = null) {
  return row?.driverName
    || row?.teamName
    || scoringRow?.driverName
    || scoringRow?.teamName
    || driverRow?.driverName
    || driverRow?.teamName
    || `Car ${row?.carIdx ?? scoringRow?.carIdx ?? driverRow?.carIdx ?? '--'}`;
}

function formatSigned(value, digits = 1) {
  if (!Number.isFinite(value)) return '--';
  return `${value > 0 ? '+' : ''}${value.toFixed(digits)}`;
}

function formatLiters(value) {
  return Number.isFinite(value) ? `${value.toFixed(1)} L` : '--';
}

function formatPercent(value) {
  return Number.isFinite(value) ? `${Math.round(value * 100)}%` : '--';
}

function formatFuelBurn(value) {
  return Number.isFinite(value) ? `${value.toFixed(1)}/h` : '--';
}

function formatTemp(value) {
  return Number.isFinite(value) ? `${Math.round(value)} C` : '--';
}

function trackWetnessLabel(value, declaredWet) {
  if (declaredWet === true) return 'Declared wet';
  if (!Number.isFinite(value)) return '--';
  return value <= 1 ? 'Dry' : value <= 3 ? 'Damp' : 'Wet';
}

function formatDuration(seconds, phase) {
  const totalSeconds = Math.ceil(Math.max(0, seconds));
  if (phase === 'pre-green' || totalSeconds < 3600) {
    return `${String(Math.floor(totalSeconds / 60)).padStart(2, '0')}:${String(totalSeconds % 60).padStart(2, '0')}`;
  }
  const totalMinutes = Math.ceil(totalSeconds / 60);
  return `${String(Math.floor(totalMinutes / 60)).padStart(2, '0')}:${String(totalMinutes % 60).padStart(2, '0')}`;
}

function tableModel(overlayId, title, status, headerItems, rows, source = 'source: race-start replay') {
  return {
    overlayId,
    title,
    status,
    source,
    bodyKind: 'table',
    columns: [],
    rows: rows.map((cells, rowIndex) => ({
      cells,
      isClassHeader: false,
      isReference: rowIndex === 1,
      isPit: false,
      isPartial: false,
      isPendingGrid: false,
      carClassColorHex: null,
      headerTitle: null,
      headerDetail: null
    })),
    metrics: [],
    points: [],
    headerItems
  };
}

function relativeTableModel(status, headerItems, rows, source = 'source: race-start replay', columns = relativeColumns()) {
  return {
    overlayId: 'relative',
    title: 'Relative',
    status,
    source,
    bodyKind: 'table',
    columns,
    rows: rows.map((row) => ({
      cells: relativeCells(row, columns),
      isClassHeader: false,
      isReference: row.isReference === true,
      isPlaceholder: row.isPlaceholder === true,
      isAhead: row.isAhead === true,
      isBehind: row.isBehind === true,
      isPit: false,
      isPartial: false,
      isPendingGrid: false,
      carClassColorHex: row.carClassColorHex || null,
      headerTitle: null,
      headerDetail: null,
      relativeLapDelta: row.relativeLapDelta ?? null
    })),
    metrics: [],
    points: [],
    headerItems
  };
}

function relativeCells(row, columns) {
  if (row?.valuesByKey) {
    return columns.map((column) => row.valuesByKey[column.dataKey] ?? '');
  }

  return row?.cells || row || [];
}

function metricsModel(overlayId, title, status, headerItems, metrics, source = 'source: race-start replay') {
  return {
    overlayId,
    title,
    status,
    source,
    bodyKind: 'metrics',
    columns: [],
    rows: [],
    metrics: metrics.map(([label, value, tone]) => ({ label, value, tone })),
    points: [],
    headerItems
  };
}

function settings(overlayId, frame) {
  if (overlayId === 'standings') {
    return {
      maximumRows: 14,
      classSeparatorsEnabled: true,
      otherClassRowsPerClass: 2,
      columns: replay.frames[0]?.model?.columns || []
    };
  }

  if (overlayId === 'relative') {
    return relativeSettings();
  }

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
      fallbackReason: 'race_start_validation',
      previewVisible: true
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

function relativeSettings() {
  return {
    carsAhead: relativeCarsEachSide,
    carsBehind: relativeCarsEachSide,
    showHeaderStatus: relativeShowHeaderStatus,
    showHeaderTimeRemaining: relativeShowHeaderTimeRemaining,
    showFooterSource: relativeShowFooterSource,
    columns: relativeColumns()
  };
}

function relativeColumns() {
  return [
    { id: 'relative.position', label: 'Pos', dataKey: 'relative-position', width: 38, alignment: 'right' },
    { id: 'relative.driver', label: 'Driver', dataKey: 'driver', width: 250, alignment: 'left' },
    { id: 'relative.gap', label: 'Delta', dataKey: 'gap', width: 70, alignment: 'right' },
    ...(relativeShowPitColumn
      ? [{ id: 'relative.pit', label: 'Pit', dataKey: 'pit', width: 30, alignment: 'right' }]
      : [])
  ];
}

function sourceFromSettings(overlaySettings, source) {
  return overlaySettings?.showFooterSource === false ? '' : source;
}

function spatialModel(lapProgress, relativeSeconds) {
  return {
    hasData: true,
    quality: 'inferred',
    referenceCarIdx: 0,
    referenceLapDistPct: lapProgress,
    hasCarLeft: relativeSeconds > -20 && relativeSeconds < 20,
    hasCarRight: relativeSeconds > 40 && relativeSeconds < 80,
    sideStatus: relativeSeconds > -20 && relativeSeconds < 20 ? 'left' : 'clear',
    strongestMulticlassApproach: {
      relativeSeconds: relativeSeconds < 0 ? null : -2.4
    },
    cars: [
      { carIdx: 21, relativeSeconds: -1.4, relativeMeters: -42, relativeLaps: -0.01, carClassColorHex: '#33CEFF' },
      { carIdx: 44, relativeSeconds: 2.1, relativeMeters: 58, relativeLaps: 0.014, carClassColorHex: '#FFAA00' },
      { carIdx: 63, relativeSeconds: 3.4, relativeMeters: 90, relativeLaps: 0.022, carClassColorHex: '#FF4FD8' }
    ]
  };
}

function relativeModel(relativeSeconds) {
  return {
    hasData: true,
    quality: relativeSeconds < 0 ? 'partial' : 'inferred',
    referenceCarIdx: 0,
    rows: [
      {
        carIdx: 21,
        driverName: 'Lap Ahead LMP2',
        carNumber: '21',
        carClass: 4099,
        carClassColorHex: '#33CEFF',
        overallPosition: 2,
        classPosition: 2,
        relativeSeconds: relativeSeconds < 0 ? null : 8.94,
        relativeLaps: 1.02,
        isAhead: true,
        isBehind: false
      },
      {
        carIdx: 22,
        driverName: 'Race Start Leader',
        carNumber: '22',
        carClass: 4098,
        carClassColorHex: '#FFDA59',
        overallPosition: 3,
        classPosition: 3,
        relativeSeconds: relativeSeconds < 0 ? null : 2.412,
        relativeLaps: 0.01,
        isAhead: true,
        isBehind: false
      },
      {
        carIdx: 44,
        driverName: 'Following GT3',
        carNumber: '44',
        carClass: 4098,
        carClassColorHex: '#FFDA59',
        overallPosition: 6,
        classPosition: 6,
        relativeSeconds: relativeSeconds < 0 ? null : 3.184,
        relativeLaps: 0.014,
        isAhead: false,
        isBehind: true
      },
      {
        carIdx: 63,
        driverName: 'Lap Behind GT4',
        carNumber: '63',
        carClass: 4100,
        carClassColorHex: '#FF4FD8',
        overallPosition: 18,
        classPosition: 12,
        relativeSeconds: relativeSeconds < 0 ? null : 11.72,
        relativeLaps: -1.03,
        isAhead: false,
        isBehind: true
      }
    ]
  };
}

function trackMapModel(lapProgress) {
  return {
    hasData: true,
    sectors: [
      { startPct: 0, endPct: 0.34, highlight: lapProgress < 0.34 ? 'personal-best' : 'none' },
      { startPct: 0.34, endPct: 0.67, highlight: lapProgress >= 0.34 && lapProgress < 0.67 ? 'best-lap' : 'none' },
      { startPct: 0.67, endPct: 1, highlight: 'none' }
    ]
  };
}

function reviewTrackMap() {
  return {
    racingLine: {
      closed: true,
      points: [
        { x: 0, y: 48, lapDistPct: 0 },
        { x: 65, y: 92, lapDistPct: 0.16 },
        { x: 150, y: 60, lapDistPct: 0.34 },
        { x: 210, y: -24, lapDistPct: 0.52 },
        { x: 148, y: -86, lapDistPct: 0.70 },
        { x: 45, y: -64, lapDistPct: 0.86 },
        { x: 0, y: 48, lapDistPct: 1 }
      ]
    },
    pitLane: {
      closed: false,
      points: [
        { x: 72, y: 84, lapDistPct: 0.14 },
        { x: 118, y: 118, lapDistPct: 0.22 },
        { x: 168, y: 82, lapDistPct: 0.30 }
      ]
    }
  };
}

function referenceDisplayRow(frame) {
  return (frame.model?.rows || []).find((row) => row && row.isReference === true && !row.isClassHeader);
}

function replayHeaderItems(frame, status, overlaySettings = null) {
  const items = overlaySettings?.showHeaderStatus === false
    ? []
    : [{ key: 'status', value: status }];
  const timeRemaining = headerTimeRemaining(frame)?.value;
  if (overlaySettings?.showHeaderTimeRemaining !== false && timeRemaining) {
    items.push({ key: 'timeRemaining', value: timeRemaining });
  }
  return items;
}

function headerTimeRemaining(frame) {
  const item = (frame.model?.headerItems || [])
    .find((candidate) => String(candidate?.key || '').toLowerCase() === 'timeremaining');
  if (!item?.value) return null;
  return {
    value: String(item.value),
    seconds: parseDurationSeconds(String(item.value))
  };
}

function parseDurationSeconds(value) {
  const parts = value.split(':').map((part) => Number.parseInt(part, 10));
  if (parts.length !== 2 || parts.some((part) => !Number.isFinite(part))) {
    return null;
  }
  return parts[0] * 60 + parts[1];
}

function normalizeProgress(value) {
  const normalized = value % 1;
  return normalized < 0 ? normalized + 1 : normalized;
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function clampInteger(value, fallback, min, max) {
  const parsed = Number.parseInt(String(value ?? ''), 10);
  return Math.max(min, Math.min(max, Number.isFinite(parsed) ? parsed : fallback));
}

function parseBoolean(value, fallback) {
  if (value == null || value === '') {
    return fallback;
  }

  const normalized = String(value).trim().toLowerCase();
  if (['1', 'true', 'yes', 'on'].includes(normalized)) {
    return true;
  }

  if (['0', 'false', 'no', 'off'].includes(normalized)) {
    return false;
  }

  return fallback;
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

function normalizePath(path) {
  return path.length > 1 && path.endsWith('/') ? path.slice(0, -1) : path;
}

function serveJson(response, payload) {
  const body = JSON.stringify(payload);
  response.writeHead(200, {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
    'access-control-allow-origin': '*'
  });
  response.end(body);
}

function serveHtml(response, html) {
  response.writeHead(200, {
    'content-type': 'text/html; charset=utf-8',
    'cache-control': 'no-store'
  });
  response.end(html);
}

function serveText(response, status, text) {
  response.writeHead(status, {
    'content-type': 'text/plain; charset=utf-8',
    'cache-control': 'no-store'
  });
  response.end(text);
}
