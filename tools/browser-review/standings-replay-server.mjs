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
const requestedReplayTimingMode = normalizeReplayTimingMode(process.env.TMR_STANDINGS_REPLAY_TIMING || 'source');
const replaySpeedMultiplier = positiveNumber(process.env.TMR_STANDINGS_REPLAY_SPEED, 60);
const replayWindowSourceStart = optionalNumber(process.env.TMR_STANDINGS_REPLAY_SOURCE_START);
const replayWindowSourceEnd = optionalNumber(process.env.TMR_STANDINGS_REPLAY_SOURCE_END);
const replayWindowFrameStart = optionalInteger(process.env.TMR_STANDINGS_REPLAY_FRAME_START);
const replayWindowFrameEnd = optionalInteger(process.env.TMR_STANDINGS_REPLAY_FRAME_END);
const relativeCarsEachSide = clampInteger(process.env.TMR_RELATIVE_CARS_EACH_SIDE, 3, 0, 8);
const relativeShowPitColumn = parseBoolean(process.env.TMR_RELATIVE_SHOW_PIT_COLUMN, false);
const relativeShowHeaderStatus = parseBoolean(process.env.TMR_RELATIVE_SHOW_HEADER_STATUS, true);
const relativeShowHeaderTimeRemaining = parseBoolean(process.env.TMR_RELATIVE_SHOW_TIME_REMAINING, true);
const relativeShowFooterSource = parseBoolean(process.env.TMR_RELATIVE_SHOW_FOOTER_SOURCE, true);
const gapMissingSegmentThresholdSeconds = 10;
const gapGraphTrendWindowSeconds = positiveNumber(process.env.TMR_GAP_GRAPH_WINDOW_SECONDS, 4 * 60 * 60);
const gapGraphMaxContexts = clampInteger(process.env.TMR_GAP_GRAPH_MAX_CONTEXTS, 36000, 120, 60000);
const gapCarsAhead = clampInteger(process.env.TMR_GAP_CARS_AHEAD, 5, 0, 12);
const gapCarsBehind = clampInteger(process.env.TMR_GAP_CARS_BEHIND, 5, 0, 12);
const gapShowHeaderStatus = parseBoolean(process.env.TMR_GAP_SHOW_HEADER_STATUS, true);
const gapShowHeaderTimeRemaining = parseBoolean(process.env.TMR_GAP_SHOW_TIME_REMAINING, true);
const gapShowFooterSource = parseBoolean(process.env.TMR_GAP_SHOW_FOOTER_SOURCE, true);
const reviewUnitSystem = normalizeUnitSystem(process.env.TMR_REVIEW_UNIT_SYSTEM || process.env.TMR_UNIT_SYSTEM || 'Metric');
const inputStateReviewSettings = {
  showThrottleTrace: parseBoolean(process.env.TMR_INPUT_SHOW_THROTTLE_TRACE, true),
  showBrakeTrace: parseBoolean(process.env.TMR_INPUT_SHOW_BRAKE_TRACE, true),
  showClutchTrace: parseBoolean(process.env.TMR_INPUT_SHOW_CLUTCH_TRACE, true),
  showThrottle: parseBoolean(process.env.TMR_INPUT_SHOW_THROTTLE, true),
  showBrake: parseBoolean(process.env.TMR_INPUT_SHOW_BRAKE, true),
  showClutch: parseBoolean(process.env.TMR_INPUT_SHOW_CLUTCH, true),
  showSteering: parseBoolean(process.env.TMR_INPUT_SHOW_STEERING, true),
  showGear: parseBoolean(process.env.TMR_INPUT_SHOW_GEAR, true),
  showSpeed: parseBoolean(process.env.TMR_INPUT_SHOW_SPEED, true)
};
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
const replayTiming = analyzeReplayTiming(replay);
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
      serveJson(response, { live: liveSnapshot(frame, index, url.searchParams) });
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
        replay: frameMetadata(frame, index, url.searchParams),
        model: displayModel(overlayId, frame, index, url.searchParams)
      });
      return;
    }

    const settingsPage = browserOverlayPages().find((candidate) => candidate.settingsRoute === path);
    if (settingsPage) {
      const { frame, index } = currentFrame(url, request);
      serveJson(response, browserOverlayApiResponse(settingsPage.page.id, path, {
        live: liveSnapshot(frame, index, url.searchParams),
        settings: settings(settingsPage.page.id, frame),
        model: displayModel(settingsPage.page.id, frame, index, url.searchParams)
      }));
      return;
    }

    if (path === '/api/replay/status') {
      const { frame, index } = currentFrame(url, request);
      serveJson(response, {
        source: replay.source,
        frameCount: replay.frames.length,
        current: frameMetadata(frame, index, url.searchParams),
        timing: replayStatusTiming(frame, index, url.searchParams),
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
  console.log(`Replay timing:         ${replayStatusTiming().effectiveMode}${replayStatusTiming().effectiveMode === 'source-elapsed' ? ` @ ${replaySpeedMultiplier}x` : ` @ ${frameMilliseconds}ms/frame`}`);
  console.log(`Source cadence:        ${formatCadenceSummary(replayTiming.sourceCadence)}`);
  console.log(`Relative rows:         ${relativeCarsEachSide * 2 + 1}`);
  console.log(`Gap cars ahead/behind: ${gapCarsAhead}/${gapCarsBehind}`);
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

function analyzeReplayTiming(replayDocument) {
  const frames = replayDocument.frames || [];
  const firstSessionTime = firstFinite(frames.map((frame) => frame.sessionTimeSeconds));
  const firstCapturedUnixMs = firstFinite(frames.map((frame) => frame.capturedUnixMs));
  let previous = null;
  const sessionDeltas = [];
  const capturedDeltas = [];
  const frameIndexDeltas = [];
  let hasNonMonotonicSessionTime = false;

  frames.forEach((frame, index) => {
    if (!Number.isFinite(frame.sourceElapsedSeconds)) {
      if (Number.isFinite(frame.sessionTimeSeconds) && firstSessionTime !== null) {
        frame.sourceElapsedSeconds = roundNumber(frame.sessionTimeSeconds - firstSessionTime);
      } else if (Number.isFinite(frame.capturedUnixMs) && firstCapturedUnixMs !== null) {
        frame.sourceElapsedSeconds = roundNumber((frame.capturedUnixMs - firstCapturedUnixMs) / 1000);
      } else {
        frame.sourceElapsedSeconds = roundNumber(index * Math.max(1, frameMilliseconds) / 1000);
      }
    }

    if (previous) {
      if (!Number.isFinite(frame.sourceSessionDeltaSeconds)
        && Number.isFinite(frame.sessionTimeSeconds)
        && Number.isFinite(previous.sessionTimeSeconds)) {
        frame.sourceSessionDeltaSeconds = roundNumber(frame.sessionTimeSeconds - previous.sessionTimeSeconds);
      }
      if (!Number.isFinite(frame.sourceCapturedDeltaSeconds)
        && Number.isFinite(frame.capturedUnixMs)
        && Number.isFinite(previous.capturedUnixMs)) {
        frame.sourceCapturedDeltaSeconds = roundNumber((frame.capturedUnixMs - previous.capturedUnixMs) / 1000);
      }
      if (!Number.isInteger(frame.sourceFrameDelta)
        && Number.isInteger(frame.frameIndex)
        && Number.isInteger(previous.frameIndex)) {
        frame.sourceFrameDelta = frame.frameIndex - previous.frameIndex;
      }
    }

    if (Number.isFinite(frame.sourceSessionDeltaSeconds)) {
      if (frame.sourceSessionDeltaSeconds < 0) {
        hasNonMonotonicSessionTime = true;
      } else {
        sessionDeltas.push(frame.sourceSessionDeltaSeconds);
      }
    }
    if (Number.isFinite(frame.sourceCapturedDeltaSeconds) && frame.sourceCapturedDeltaSeconds >= 0) {
      capturedDeltas.push(frame.sourceCapturedDeltaSeconds);
    }
    if (Number.isInteger(frame.sourceFrameDelta) && frame.sourceFrameDelta >= 0) {
      frameIndexDeltas.push(frame.sourceFrameDelta);
    }
    previous = frame;
  });

  const timeline = frames
    .map((frame, index) => ({ index, elapsed: finiteNumber(frame.sourceElapsedSeconds) }))
    .filter((item) => Number.isFinite(item.elapsed));
  const isTimelineMonotonic = timeline.every((item, index) => index === 0 || item.elapsed >= timeline[index - 1].elapsed);
  const sourceDurationSeconds = timeline.length > 1
    ? Math.max(0, timeline[timeline.length - 1].elapsed - timeline[0].elapsed)
    : 0;
  const exportedCadence = replayDocument.source?.cadence || {};
  const computedCadence = {
    basis: exportedCadence.basis || 'raw-capture frame header sessionTime',
    selectedFrameCount: frames.length,
    gapToLeaderMissingSegmentThresholdSeconds: gapMissingSegmentThresholdSeconds,
    denseForGapToLeader: frames.length < 2 || (sessionDeltas.length > 0 && Math.max(...sessionDeltas) <= gapMissingSegmentThresholdSeconds),
    hasNonMonotonicSessionTime,
    sourceElapsedSeconds: summarizeNumbers(timeline.map((item) => item.elapsed)),
    sourceSessionDeltaSeconds: summarizeNumbers(sessionDeltas),
    sourceCapturedDeltaSeconds: summarizeNumbers(capturedDeltas),
    sourceFrameDelta: summarizeNumbers(frameIndexDeltas, 0)
  };

  return {
    timeline,
    isTimelineMonotonic,
    sourceDurationSeconds,
    sourceCadence: mergeCadence(exportedCadence, computedCadence)
  };
}

function effectiveReplayTimingMode() {
  return requestedReplayTimingMode === 'source-elapsed'
    && replayTiming.isTimelineMonotonic
    && replayTiming.sourceDurationSeconds > 0
    ? 'source-elapsed'
    : 'fixed-frame';
}

function replayStatusTiming(frame = null, index = null, searchParams = null) {
  const effectiveMode = effectiveReplayTimingMode();
  return {
    requestedMode: requestedReplayTimingMode,
    effectiveMode,
    speedMultiplier: effectiveMode === 'source-elapsed'
      ? replaySpeedFromSearchParams(searchParams) ?? replaySpeedMultiplier
      : null,
    fixedFrameMilliseconds: effectiveMode === 'fixed-frame' ? frameMilliseconds : null,
    replayWindow: replayWindowFromSearchParams(searchParams) ?? replayWindowStatus(),
    sourceDurationSeconds: roundNumber(replayTiming.sourceDurationSeconds),
    sourceTimelineMonotonic: replayTiming.isTimelineMonotonic,
    sourceCadence: replayTiming.sourceCadence,
    current: frame && Number.isInteger(index)
      ? {
          index,
          frameIndex: frame.frameIndex,
          sourceElapsedSeconds: finiteNumber(frame.sourceElapsedSeconds),
          sourceSessionDeltaSeconds: finiteNumber(frame.sourceSessionDeltaSeconds),
          sourceFrameDelta: Number.isInteger(frame.sourceFrameDelta) ? frame.sourceFrameDelta : null
        }
      : null
  };
}

function frameIndexForSourceElapsed(sourceElapsedSeconds) {
  const timeline = replayTiming.timeline;
  if (timeline.length === 0) {
    return 0;
  }

  let low = 0;
  let high = timeline.length - 1;
  while (low < high) {
    const middle = Math.floor((low + high + 1) / 2);
    if (timeline[middle].elapsed <= sourceElapsedSeconds) {
      low = middle;
    } else {
      high = middle - 1;
    }
  }
  return timeline[low].index;
}

function firstFrameIndexAtOrAfterSourceElapsed(sourceElapsedSeconds, maximumIndex) {
  const timeline = replayTiming.timeline.filter((item) => item.index <= maximumIndex);
  if (timeline.length === 0) {
    return 0;
  }

  let low = 0;
  let high = timeline.length - 1;
  while (low < high) {
    const middle = Math.floor((low + high) / 2);
    if (timeline[middle].elapsed < sourceElapsedSeconds) {
      low = middle + 1;
    } else {
      high = middle;
    }
  }
  return timeline[low].index;
}

function mergeCadence(exportedCadence, computedCadence) {
  return {
    ...computedCadence,
    ...exportedCadence,
    sourceElapsedSeconds: exportedCadence.sourceElapsedSeconds || computedCadence.sourceElapsedSeconds,
    sourceSessionDeltaSeconds: exportedCadence.sourceSessionDeltaSeconds || computedCadence.sourceSessionDeltaSeconds,
    sourceCapturedDeltaSeconds: exportedCadence.sourceCapturedDeltaSeconds || computedCadence.sourceCapturedDeltaSeconds,
    sourceFrameDelta: exportedCadence.sourceFrameDelta || computedCadence.sourceFrameDelta,
    denseForGapToLeader: typeof exportedCadence.denseForGapToLeader === 'boolean'
      ? exportedCadence.denseForGapToLeader
      : computedCadence.denseForGapToLeader
  };
}

function summarizeNumbers(values, digits = 3) {
  const numbers = values.filter((value) => Number.isFinite(value)).sort((a, b) => a - b);
  if (numbers.length === 0) {
    return { count: 0, min: null, median: null, max: null };
  }

  const middle = Math.floor(numbers.length / 2);
  const median = numbers.length % 2 === 0
    ? (numbers[middle - 1] + numbers[middle]) / 2
    : numbers[middle];
  return {
    count: numbers.length,
    min: roundNumber(numbers[0], digits),
    median: roundNumber(median, digits),
    max: roundNumber(numbers[numbers.length - 1], digits)
  };
}

function firstFinite(values) {
  const value = values.find((candidate) => Number.isFinite(candidate));
  return Number.isFinite(value) ? value : null;
}

function currentFrame(url = null, request = null) {
  const override = frameOverride(url, request);
  if (override !== null) {
    const index = Math.max(0, Math.min(replay.frames.length - 1, override));
    return { index, frame: replay.frames[index] };
  }

  const window = replayWindow(url, request);
  if (effectiveReplayTimingMode() === 'source-elapsed') {
    const index = currentSourceElapsedFrameIndex(url, request, window);
    return { index, frame: replay.frames[index] };
  }

  const index = currentFixedFrameIndex(window);
  return { index, frame: replay.frames[index] };
}

function currentSourceElapsedFrameIndex(url = null, request = null, window = null) {
  const duration = replayTiming.sourceDurationSeconds;
  if (!Number.isFinite(duration) || duration <= 0) {
    return currentFixedFrameIndex(window);
  }

  const speedMultiplier = replaySpeed(url, request);
  const elapsedSeconds = (Math.max(0, Date.now() - startedAtMs) / 1000) * speedMultiplier;
  const sourceWindow = window?.source;
  if (sourceWindow) {
    return frameIndexForSourceElapsed(sourceWindow.start + (elapsedSeconds % sourceWindow.duration));
  }

  const startElapsed = replayTiming.timeline[0]?.elapsed || 0;
  return frameIndexForSourceElapsed(startElapsed + (elapsedSeconds % duration));
}

function currentFixedFrameIndex(window = null) {
  const elapsed = Math.max(0, Date.now() - startedAtMs);
  const frameWindow = window?.frame;
  if (frameWindow) {
    const offset = Math.floor(elapsed / Math.max(1, frameMilliseconds)) % frameWindow.count;
    return frameWindow.start + offset;
  }

  return Math.floor(elapsed / Math.max(1, frameMilliseconds)) % replay.frames.length;
}

function frameOverride(url, request) {
  for (const searchParams of requestSearchParams(url, request)) {
    const frame = parseFrame(searchParams);
    if (frame !== null) {
      return frame;
    }
  }

  return null;
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

function replayWindow(url = null, request = null) {
  for (const searchParams of requestSearchParams(url, request)) {
    const window = replayWindowFromSearchParams(searchParams);
    if (window) return window;
  }

  const envSourceWindow = boundedSourceWindow(replayWindowSourceStart, replayWindowSourceEnd);
  const envFrameWindow = boundedFrameWindow(replayWindowFrameStart, replayWindowFrameEnd);
  return envSourceWindow || envFrameWindow
    ? { source: envSourceWindow, frame: envFrameWindow }
    : null;
}

function replayWindowFromSearchParams(searchParams) {
  const sourceWindow = sourceReplayWindow(searchParams);
  const frameWindow = frameReplayWindow(searchParams);
  return sourceWindow || frameWindow
    ? { source: sourceWindow, frame: frameWindow }
    : null;
}

function sourceReplayWindow(searchParams) {
  const start = optionalNumber(searchParams?.get('sourceStart'));
  const end = optionalNumber(searchParams?.get('sourceEnd'));
  return boundedSourceWindow(start, end);
}

function frameReplayWindow(searchParams) {
  const start = optionalInteger(searchParams?.get('frameStart'));
  const end = optionalInteger(searchParams?.get('frameEnd'));
  return boundedFrameWindow(start, end);
}

function boundedSourceWindow(start, end) {
  if (!Number.isFinite(start) || !Number.isFinite(end) || start >= end || replayTiming.timeline.length === 0) {
    return null;
  }

  const timelineStart = replayTiming.timeline[0].elapsed;
  const timelineEnd = replayTiming.timeline[replayTiming.timeline.length - 1].elapsed;
  const boundedStart = clamp(start, timelineStart, timelineEnd);
  const boundedEnd = clamp(end, timelineStart, timelineEnd);
  return boundedStart < boundedEnd
    ? { start: boundedStart, end: boundedEnd, duration: boundedEnd - boundedStart }
    : null;
}

function boundedFrameWindow(start, end) {
  if (!Number.isInteger(start) || !Number.isInteger(end)) {
    return null;
  }

  const boundedStart = Math.max(0, Math.min(replay.frames.length - 1, start));
  const boundedEnd = Math.max(0, Math.min(replay.frames.length - 1, end));
  return boundedStart <= boundedEnd
    ? { start: boundedStart, end: boundedEnd, count: boundedEnd - boundedStart + 1 }
    : null;
}

function replaySpeed(url = null, request = null) {
  for (const searchParams of requestSearchParams(url, request)) {
    const speed = replaySpeedFromSearchParams(searchParams);
    if (speed !== null) {
      return speed;
    }
  }

  return replaySpeedMultiplier;
}

function replaySpeedFromSearchParams(searchParams = null) {
  return positiveNumber(searchParams?.get('replaySpeed'), null);
}

function replayWindowStatus() {
  const sourceWindow = boundedSourceWindow(replayWindowSourceStart, replayWindowSourceEnd);
  const frameWindow = boundedFrameWindow(replayWindowFrameStart, replayWindowFrameEnd);
  return sourceWindow || frameWindow
    ? { source: sourceWindow, frame: frameWindow }
    : null;
}

function requestSearchParams(url = null, request = null) {
  const candidates = [];
  if (url?.searchParams) {
    candidates.push(url.searchParams);
  }

  const referrer = request?.headers?.referer;
  if (referrer) {
    try {
      candidates.push(new URL(referrer).searchParams);
    } catch {
      // Ignore malformed referrers from external tools.
    }
  }

  return candidates;
}

function frameMetadata(frame, index, searchParams = null) {
  return {
    index,
    captureId: frame.captureId,
    frameIndex: frame.frameIndex,
    sessionTimeSeconds: frame.sessionTimeSeconds,
    sourceElapsedSeconds: finiteNumber(frame.sourceElapsedSeconds),
    sourceSessionDeltaSeconds: finiteNumber(frame.sourceSessionDeltaSeconds),
    sourceCapturedDeltaSeconds: finiteNumber(frame.sourceCapturedDeltaSeconds),
    sourceFrameDelta: Number.isInteger(frame.sourceFrameDelta) ? frame.sourceFrameDelta : null,
    sessionInfoUpdate: frame.sessionInfoUpdate,
    sessionState: frame.sessionState,
    sessionPhase: frame.sessionPhase,
    camCarIdx: frame.camCarIdx,
    playerCarIdx: frame.playerCarIdx,
    spoofFocus: spoofFocusMode(searchParams),
    timing: replayStatusTiming(frame, index, searchParams)
  };
}

function liveSnapshot(frame, index, searchParams = null) {
  if (frame.live && typeof frame.live === 'object') {
    return {
      ...frame.live,
      models: spoofLiveModels(frame.live.models || {}, frame, index, searchParams),
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

function liveModelsForReplayFrame(index, searchParams = null) {
  const frame = replay.frames[index];
  return spoofLiveModels(frame?.live?.models || {}, frame, index, searchParams);
}

function spoofLiveModels(models, frame, index, searchParams = null) {
  const targetPosition = spoofFocusClassPosition(models, frame, index, searchParams);
  if (!targetPosition) {
    return models;
  }

  const timing = models?.timing || {};
  const target = timingRowByClassPosition(timing, targetPosition);
  if (!target || !Number.isFinite(target.carIdx)) {
    return models;
  }

  const spoofed = clonePlainObject(models);
  const spoofedTiming = spoofed.timing || {};
  const playerCarIdx = Number.isFinite(spoofed.reference?.playerCarIdx)
    ? spoofed.reference.playerCarIdx
    : Number.isFinite(frame?.playerCarIdx)
      ? frame.playerCarIdx
      : null;

  for (const rowsKey of ['classRows', 'overallRows']) {
    if (!Array.isArray(spoofedTiming[rowsKey])) {
      continue;
    }

    spoofedTiming[rowsKey] = spoofedTiming[rowsKey].map((row) => ({
      ...row,
      isFocus: row?.carIdx === target.carIdx,
      isPlayer: playerCarIdx !== null && row?.carIdx === playerCarIdx
    }));
  }

  const targetRow = timingRowByCarIdx(spoofedTiming, target.carIdx) || { ...target };
  targetRow.isFocus = true;
  targetRow.isPlayer = playerCarIdx !== null && target.carIdx === playerCarIdx;
  spoofedTiming.focusCarIdx = target.carIdx;
  spoofedTiming.focusRow = targetRow;
  spoofed.timing = spoofedTiming;

  const reference = spoofed.reference || {};
  spoofed.reference = {
    ...reference,
    hasData: true,
    focusCarIdx: target.carIdx,
    focusIsPlayer: playerCarIdx !== null && target.carIdx === playerCarIdx,
    hasExplicitNonPlayerFocus: playerCarIdx !== null && target.carIdx !== playerCarIdx,
    referenceCarClass: target.carClass ?? reference.referenceCarClass ?? null,
    lapDistPct: Number.isFinite(target.lapDistPct) ? target.lapDistPct : reference.lapDistPct ?? null,
    onPitRoad: target.onPitRoad === true,
    isOnTrack: target.onPitRoad !== true,
    isInGarage: false
  };

  return spoofed;
}

function spoofFocusMode(searchParams = null) {
  const raw = searchParams?.get('spoofFocus') ?? searchParams?.get('focus');
  return raw ? String(raw).trim().toLowerCase() : null;
}

function spoofFocusClassPosition(models, frame, index, searchParams = null) {
  const mode = spoofFocusMode(searchParams);
  if (!mode) {
    return null;
  }

  if (mode === 'leader' || mode === 'p1') {
    return 1;
  }

  const explicitPosition = /^p?(\d+)$/i.exec(mode);
  if (explicitPosition) {
    const position = Number.parseInt(explicitPosition[1], 10);
    return position > 0 ? position : null;
  }

  if (mode === 'switch' || mode === 'focus-switch' || mode === 'mid-switch') {
    return replayProgress(frame, index) < 0.5 ? 3 : 5;
  }

  if (mode === 'leader-switch' || mode === 'switch-leader') {
    return replayProgress(frame, index) < 0.5 ? 3 : 1;
  }

  return null;
}

function replayProgress(frame, index) {
  const elapsed = finiteNumber(frame?.sourceElapsedSeconds);
  const firstElapsed = replayTiming.timeline[0]?.elapsed;
  if (Number.isFinite(elapsed)
    && Number.isFinite(firstElapsed)
    && replayTiming.sourceDurationSeconds > 0) {
    return clamp((elapsed - firstElapsed) / replayTiming.sourceDurationSeconds, 0, 1);
  }

  return replay.frames.length > 1
    ? clamp(index / (replay.frames.length - 1), 0, 1)
    : 0;
}

function timingRowByClassPosition(timing, classPosition) {
  const position = Number.parseInt(classPosition, 10);
  if (!Number.isFinite(position) || position <= 0) {
    return null;
  }

  const rows = [
    ...(Array.isArray(timing?.classRows) ? timing.classRows : []),
    ...(Array.isArray(timing?.overallRows) ? timing.overallRows : [])
  ];
  return rows.find((row) => toPositiveInteger(row?.classPosition) === position) || null;
}

function timingRowByCarIdx(timing, carIdx) {
  const rows = [
    ...(Array.isArray(timing?.classRows) ? timing.classRows : []),
    ...(Array.isArray(timing?.overallRows) ? timing.overallRows : [])
  ];
  return rows.find((row) => row?.carIdx === carIdx) || null;
}

function clonePlainObject(value) {
  return globalThis.structuredClone
    ? globalThis.structuredClone(value)
    : JSON.parse(JSON.stringify(value));
}

function displayModel(overlayId, frame, index, searchParams = null) {
  if (overlayId === 'standings') {
    return frame.model;
  }

  if (frame.live?.models) {
    return captureDisplayModel(overlayId, frame, index, searchParams);
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
      ['Fuel', formatLiters(isPreGreen ? 74.0 : 73.4), 'info'],
      ['Burn', isPreGreen ? 'grid idle' : formatFuelPerLap(2.9), 'normal'],
      ['Window', isPreGreen ? 'after green' : '24 laps', 'normal'],
      ['Mode', isPreGreen ? 'countdown ignored' : 'timed race', isPreGreen ? 'warning' : 'success']
    ]);
  }

  if (overlayId === 'session-weather') {
    return metricsModel(overlayId, 'Session / Weather', status, headerItems, [
      ['Session', isPreGreen ? 'Race Grid' : 'Race', 'info'],
      ['Track', formatTemp(29), 'normal'],
      ['Air', formatTemp(21), 'normal'],
      ['Wetness', 'Dry', 'success']
    ]);
  }

  if (overlayId === 'pit-service') {
    const releaseRow = ['Release', referenceDisplayRow(frame)?.isPit ? 'pit road' : '--', referenceDisplayRow(frame)?.isPit ? 'info' : 'normal'];
    const pitStatusRow = ['Pit status', status || '--', 'normal'];
    const fuelRequestRow = ['Fuel request', '--', 'normal', [
      pitSegment('Requested', '--', 'waiting'),
      pitSegment('Selected', '--', 'waiting')
    ]];
    const tearoffRow = ['Tearoff', '--', 'normal', [
      pitSegment('Requested', '--', 'waiting')
    ]];
    const repairRow = ['Repair', 'Available', 'success', [
      pitSegment('Required', '--', 'success'),
      pitSegment('Optional', '--', 'success')
    ]];
    const fastRepairRow = ['Fast repair', '--', 'normal', [
      pitSegment('Selected', '--', 'waiting'),
      pitSegment('Available', '--', 'waiting')
    ]];
    const metricSections = [
      ['Pit Signal', [releaseRow, pitStatusRow]],
      ['Service Request', [fuelRequestRow, tearoffRow, repairRow, fastRepairRow]]
    ];
    return metricsModel(
      overlayId,
      'Pit Service',
      status,
      headerItems,
      metricSections.flatMap(([, rows]) => rows),
      'source: race-start replay',
      [],
      metricSections);
  }

  if (overlayId === 'gap-to-leader') {
    const gapSettings = gapSettingsModel();
    return {
      overlayId,
      title: 'Gap To Leader',
      status: isPreGreen ? 'waiting for timing' : 'live | race gap',
      source: sourceFromSettings(gapSettings, 'source: race-start replay'),
      bodyKind: 'graph',
      columns: [],
      rows: [],
      metrics: [],
      points: isPreGreen ? [] : Array.from({ length: 24 }, (_, point) => 30 - point * 0.7 + Math.sin(point / 2) * 1.4),
      headerItems: replayHeaderItems(frame, isPreGreen ? 'waiting for timing' : 'live | race gap', gapSettings)
    };
  }

  return tableModel(overlayId, browserOverlayPage(overlayId).title, status, headerItems, []);
}

function captureDisplayModel(overlayId, frame, index, searchParams = null) {
  const live = liveSnapshot(frame, index, searchParams);
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

    return captureFuelModel(models, live, status, headerItems);
  }

  if (overlayId === 'session-weather') {
    return captureSessionWeatherModel(models, status, headerItems);
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

    return capturePitServiceModel(models, status, headerItems, searchParams);
  }

  if (overlayId === 'gap-to-leader') {
    return captureGapToLeaderModel(models, frame, index, searchParams);
  }

  if (overlayId === 'input-state') {
    return captureInputStateModel(models, status, index, searchParams);
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

function captureFuelModel(models, live, status, headerItems) {
  const fuel = models.fuelPit?.fuel || live.fuel || {};
  const progress = models.raceProgress || {};
  const source = models.fuelPit?.hasData ? 'source: capture-derived fuel telemetry' : 'source: waiting';
  return metricsModel('fuel-calculator', 'Fuel Calculator', status, headerItems, [
    ['Fuel', formatLiters(fuel.fuelLevelLiters), 'modeled'],
    ['Fuel %', formatPercent(fuel.fuelLevelPercent), 'modeled'],
    ['Burn', formatFuelBurn(fuel.fuelUsePerHourKg), 'modeled'],
    ['Progress', formatProgressLaps(progress.referenceCarProgressLaps), 'normal']
  ], source);
}

function captureSessionWeatherModel(models, fallbackStatus, headerItems) {
  const session = models.session || {};
  const weather = models.weather || {};
  const hasWeather = weather.hasData === true;
  const status = hasWetSurfaceSignal(weather)
    ? weather.trackWetnessLabel || 'wet declared'
    : session.sessionType || fallbackStatus || 'live session';
  const source = hasWeather ? 'source: session + live weather telemetry' : 'source: session telemetry';
  return metricsModel('session-weather', 'Session / Weather', status, headerItems, [
    ['Session', joinAvailable(session.sessionType, session.sessionName, session.teamRacing === true ? 'team' : null), 'normal'],
    ['Clock', formatSessionClock(session), 'normal'],
    ['Laps', formatSessionLaps(session, models.raceProgress, models.raceProjection), 'normal'],
    ['Track', joinAvailable(session.trackDisplayName, formatTrackLength(session.trackLengthKm)), 'normal'],
    ['Temps', formatWeatherTemps(weather), 'normal'],
    ['Surface', formatWeatherSurface(weather), hasWetSurfaceSignal(weather) ? 'info' : 'normal'],
    ['Sky', formatWeatherSky(weather), 'normal'],
    ['Wind', formatWindAtmosphere(weather), 'normal']
  ], source);
}

function capturePitServiceModel(models, fallbackStatus, headerItems, searchParams = null) {
  const spoofAllRows = pitServiceSpoofAllRowsEnabled(searchParams);
  const pitModels = spoofAllRows
    ? pitServiceAllRowsModels(models)
    : models;
  const pit = pitModels.fuelPit || {};
  const release = pitReleaseState(pit);
  const status = pitStatus(pit, release) || fallbackStatus || 'pit ready';
  const effectiveHeaderItems = spoofAllRows
    ? [
        { key: 'status', value: '' },
        { key: 'timeRemaining', value: formatPitTimeRemaining(pitModels.session) || '' }
      ]
    : headerItems;
  const releaseRow = pitSignalMetricRow('Release', release.value, release.tone);
  const pitStatusRow = pitSignalMetricRow('Pit status', pitServiceStatusText(pit.pitServiceStatus), pitServiceActivityTone(pit, release));
  const timeLaps = pitTimeLaps(pitModels);
  const fuelRequestRow = spoofAllRows ? pitFuelRequestSegmentedRow(pit) : ['Fuel request', pitFuelRequest(pit), 'normal'];
  const tearoffRow = spoofAllRows ? pitTearoffSegmentedRow(pit) : ['Tearoff', pitTearoff(pit), 'normal'];
  const repairRow = spoofAllRows ? pitRepairSegmentedRow(pit) : ['Repair', pitRepair(pit), pitRepairTone(pit)];
  const fastRepairRow = spoofAllRows ? pitFastRepairSegmentedRow(pit) : ['Fast repair', pitFastRepair(pit), 'normal'];
  const metricSections = [
    ...(timeLaps === '--' ? [] : [['Session', [['Time / Laps', timeLaps, 'normal']]]]),
    ['Pit Signal', [releaseRow, pitStatusRow]],
    ['Service Request', [fuelRequestRow, tearoffRow, repairRow, fastRepairRow]]
  ];
  const metrics = metricSections.flatMap(([, rows]) => rows);
  return metricsModel(
    'pit-service',
    'Pit Service',
    status,
    effectiveHeaderItems,
    metrics,
    spoofAllRows
      ? 'source: spoofed pit service all-rows preview'
      : 'source: player/team pit service telemetry',
    spoofAllRows ? pitServiceGridSectionsFromData(pitModels, pit) : [],
    metricSections);
}

function pitServiceSpoofAllRowsEnabled(searchParams) {
  const value = searchParams?.get('pitService') || '';
  return ['all', 'all-rows', 'full'].includes(value.trim().toLowerCase());
}

function pitServiceAllRowsModels(models) {
  const pit = pitServiceAllRowsPit();
  return {
    ...models,
    fuelPit: pit,
    session: {
      ...(models.session || {}),
      sessionType: 'Race',
      sessionName: 'Road Atlanta spoof',
      sessionTimeRemainSeconds: 86258.266667,
      sessionLapsRemain: 4,
      sessionLapsRemainEx: 4,
      sessionLapsTotal: 5
    },
    raceProgress: {
      ...(models.raceProgress || {}),
      raceLapsRemaining: 4
    },
    raceProjection: {
      ...(models.raceProjection || {}),
      estimatedTeamLapsRemaining: 4,
      estimatedFinishLap: 5
    },
    tireCompounds: pitServiceAllRowsTireCompounds(models),
    tireCondition: pitServiceAllRowsTireCondition()
  };
}

function pitServiceAllRowsPit() {
  return {
    hasData: true,
    quality: 'spoofed',
    onPitRoad: true,
    teamOnPitRoad: true,
    playerCarInPitStall: true,
    pitstopActive: true,
    pitServiceStatus: 1,
    pitServiceFlags: 0x7b,
    pitServiceFuelLiters: 45.5,
    pitRepairLeftSeconds: 12.2,
    pitOptRepairLeftSeconds: 18.4,
    playerCarDryTireSetLimit: 4,
    tireSetsUsed: 2,
    tireSetsAvailable: 2,
    leftTireSetsUsed: 1,
    rightTireSetsUsed: 1,
    frontTireSetsUsed: 1,
    rearTireSetsUsed: 1,
    leftTireSetsAvailable: 2,
    rightTireSetsAvailable: 2,
    frontTireSetsAvailable: 2,
    rearTireSetsAvailable: 2,
    leftFrontTiresUsed: 1,
    rightFrontTiresUsed: 1,
    leftRearTiresUsed: 1,
    rightRearTiresUsed: 1,
    leftFrontTiresAvailable: 2,
    rightFrontTiresAvailable: 2,
    leftRearTiresAvailable: 2,
    rightRearTiresAvailable: 2,
    requestedTireCompound: 1,
    fastRepairUsed: 0,
    fastRepairAvailable: 1,
    teamFastRepairsUsed: 1
  };
}

function pitServiceAllRowsTireCompounds(models) {
  const playerCarIdx = models.driverDirectory?.playerCarIdx ?? models.reference?.playerCarIdx ?? 15;
  return {
    hasData: true,
    quality: 'spoofed',
    definitions: [
      { index: 0, label: 'Medium', shortLabel: 'M', isWet: false },
      { index: 1, label: 'Soft', shortLabel: 'S', isWet: false },
      { index: 2, label: 'Wet', shortLabel: 'W', isWet: true }
    ],
    playerCar: {
      carIdx: playerCarIdx,
      compoundIndex: 0,
      label: 'Medium',
      shortLabel: 'M',
      isWet: false,
      isPlayer: true,
      isFocus: true
    },
    focusCar: {
      carIdx: playerCarIdx,
      compoundIndex: 0,
      label: 'Medium',
      shortLabel: 'M',
      isWet: false,
      isPlayer: true,
      isFocus: true
    },
    cars: []
  };
}

function pitServiceAllRowsTireCondition() {
  return {
    hasData: true,
    quality: 'spoofed',
    leftFront: pitServiceTireCorner('LF', [0.92, 0.91, 0.90], [80, 81, 82], 206.8, 20000, true),
    rightFront: pitServiceTireCorner('RF', [0.93, 0.92, 0.91], [79, 80, 81], 203.4, 19750, true),
    leftRear: pitServiceTireCorner('LR', [0.96, 0.95, 0.94], [72, 73, 74], 196.5, 20000, false),
    rightRear: pitServiceTireCorner('RR', [0.97, 0.96, 0.95], [73, 74, 75], 198.6, 19750, true)
  };
}

function pitServiceTireCorner(corner, wear, tempC, pressureKpa, odometerMeters, changeRequested) {
  return {
    corner,
    wear: { left: wear[0], middle: wear[1], right: wear[2] },
    temperatureC: { left: tempC[0], middle: tempC[1], right: tempC[2] },
    coldPressureKpa: pressureKpa,
    odometerMeters,
    pitServicePressureKpa: null,
    blackBoxColdPressurePa: null,
    changeRequested
  };
}

function pitServiceGridSectionsFromData(models, pit) {
  const condition = models.tireCondition || {};
  const rows = [
    pitServiceCompoundRow(models.tireCompounds || {}, pit),
    pitServiceChangeRow(condition, pit),
    pitServiceSetLimitRow(pit),
    pitServiceAvailableRow(pit),
    pitServiceUsedRow(pit),
    pitServicePressureRow(condition),
    pitServiceTemperatureRow(condition),
    pitServiceWearRow(condition),
    pitServiceDistanceRow(condition)
  ].filter(Boolean);
  return rows.length
    ? [{ title: 'Tire Analysis', headers: ['Info', 'FL', 'FR', 'RL', 'RR'], rows }]
    : [];
}

function pitServiceCompoundRow(tireCompounds, pit) {
  const current = firstText(tireCompounds.playerCar?.shortLabel, tireCompounds.playerCar?.label);
  const requestedDefinition = Array.isArray(tireCompounds.definitions)
    ? tireCompounds.definitions.find((definition) => definition?.index === pit?.requestedTireCompound)
    : null;
  const requested = firstText(requestedDefinition?.shortLabel, requestedDefinition?.label);
  const isChanging = Boolean(requested && requested !== current);
  const value = isChanging ? requested : firstText(current, requested);
  const cells = [value, value, value, value].map((cellValue) => ({
    value: cellValue,
    tone: isChanging ? 'success' : 'info'
  }));
  return value ? pitServiceGridRow('Compound', cells, 'normal') : null;
}

function pitServiceChangeRow(condition, pit) {
  const hasEvidence = Number.isInteger(pit?.pitServiceFlags)
    || pitServiceCorners().some(({ key }) => condition?.[key]?.changeRequested != null);
  if (!hasEvidence) return null;
  const values = pitServiceCorners().map(({ key, flag }) => {
    const requested = condition?.[key]?.changeRequested;
    const selected = requested === true || (requested == null && (pit?.pitServiceFlags & flag) !== 0);
    return {
      value: selected ? 'Change' : 'Keep',
      tone: selected ? 'success' : 'info'
    };
  });
  return pitServiceGridRow('Change', values);
}

function pitServiceSetLimitRow(pit) {
  const value = Number.isInteger(pit?.playerCarDryTireSetLimit) && pit.playerCarDryTireSetLimit > 0
    ? `${pit.playerCarDryTireSetLimit} sets`
    : null;
  return value ? pitServiceGridRow('Set limit', [value, value, value, value]) : null;
}

function pitServiceAvailableRow(pit) {
  const values = [
    pitServiceAvailableCell(pit?.leftFrontTiresAvailable ?? pit?.leftTireSetsAvailable ?? pit?.frontTireSetsAvailable ?? pit?.tireSetsAvailable),
    pitServiceAvailableCell(pit?.rightFrontTiresAvailable ?? pit?.rightTireSetsAvailable ?? pit?.frontTireSetsAvailable ?? pit?.tireSetsAvailable),
    pitServiceAvailableCell(pit?.leftRearTiresAvailable ?? pit?.leftTireSetsAvailable ?? pit?.rearTireSetsAvailable ?? pit?.tireSetsAvailable),
    pitServiceAvailableCell(pit?.rightRearTiresAvailable ?? pit?.rightTireSetsAvailable ?? pit?.rearTireSetsAvailable ?? pit?.tireSetsAvailable)
  ];
  return pitServiceAnyCellHasValue(values) ? pitServiceGridRow('Available', values) : null;
}

function pitServiceAvailableCell(value) {
  const display = pitServiceCounter(value, true);
  return {
    value: display,
    tone: display === '0' ? 'error' : 'normal'
  };
}

function pitServiceUsedRow(pit) {
  const values = [
    pitServiceCounter(pit?.leftFrontTiresUsed ?? pit?.leftTireSetsUsed ?? pit?.frontTireSetsUsed ?? pit?.tireSetsUsed, false),
    pitServiceCounter(pit?.rightFrontTiresUsed ?? pit?.rightTireSetsUsed ?? pit?.frontTireSetsUsed ?? pit?.tireSetsUsed, false),
    pitServiceCounter(pit?.leftRearTiresUsed ?? pit?.leftTireSetsUsed ?? pit?.rearTireSetsUsed ?? pit?.tireSetsUsed, false),
    pitServiceCounter(pit?.rightRearTiresUsed ?? pit?.rightTireSetsUsed ?? pit?.rearTireSetsUsed ?? pit?.tireSetsUsed, false)
  ];
  return pitServiceAnyCellHasValue(values) ? pitServiceGridRow('Used', values) : null;
}

function pitServicePressureRow(condition) {
  const values = pitServiceCorners().map(({ key }) => {
    const corner = condition?.[key] || {};
    const kpa = firstFinite([
      corner.pitServicePressureKpa,
      corner.coldPressureKpa,
      Number.isFinite(corner.blackBoxColdPressurePa) ? corner.blackBoxColdPressurePa / 1000 : null
    ]);
    return formatKpa(kpa);
  });
  return pitServiceAnyCellHasValue(values) ? pitServiceGridRow('Pressure', values) : null;
}

function pitServiceTemperatureRow(condition) {
  const values = pitServiceCorners().map(({ key }) => {
    const raw = acrossValues(condition?.[key]?.temperatureC)
      .filter((value) => Number.isFinite(value) && value > 0 && value < 250);
    const converted = raw.map((value) => isImperial() ? value * 9 / 5 + 32 : value);
    return pitServiceFormatAcross(converted, '0', isImperial() ? ' F' : ' C');
  });
  return pitServiceAnyCellHasValue(values) ? pitServiceGridRow('Temp', values) : null;
}

function pitServiceWearRow(condition) {
  const values = pitServiceCorners().map(({ key }) => {
    const raw = acrossValues(condition?.[key]?.wear)
      .filter((value) => Number.isFinite(value) && value > 0 && value <= 1)
      .map((value) => value * 100);
    return pitServiceFormatAcross(raw, '0', '%');
  });
  return pitServiceAnyCellHasValue(values) ? pitServiceGridRow('Wear', values) : null;
}

function pitServiceDistanceRow(condition) {
  const values = pitServiceCorners().map(({ key }) => {
    const meters = condition?.[key]?.odometerMeters;
    return formatMeters(meters);
  });
  return pitServiceAnyCellHasValue(values) ? pitServiceGridRow('Distance', values) : null;
}

function formatKpa(kpa) {
  if (!Number.isFinite(kpa) || kpa <= 0) return '--';
  return isImperial()
    ? `${Math.round(kpa * 0.145037738)} psi`
    : `${(kpa / 100).toFixed(1)} bar`;
}

function formatMeters(meters) {
  if (!Number.isFinite(meters) || meters <= 0) return '--';
  return isImperial()
    ? `${(meters / 1609.344).toFixed(1)} mi`
    : `${(meters / 1000).toFixed(1)} km`;
}

function pitServiceCorners() {
  return [
    { key: 'leftFront', label: 'LF', flag: 0x01 },
    { key: 'rightFront', label: 'RF', flag: 0x02 },
    { key: 'leftRear', label: 'LR', flag: 0x04 },
    { key: 'rightRear', label: 'RR', flag: 0x08 }
  ];
}

function pitServiceCounter(value, allowZero) {
  if (value === 255) return 'unlim';
  return Number.isInteger(value) && (value > 0 || allowZero && value === 0) ? String(value) : '--';
}

function acrossValues(values) {
  return [values?.left, values?.middle, values?.right];
}

function pitServiceFormatAcross(values, format, suffix) {
  if (!values.length) return '--';
  const formatted = values.map((value) => format === '0' ? Math.round(value).toFixed(0) : value.toFixed(1));
  const distinct = new Set(formatted);
  return `${distinct.size === 1 ? formatted[0] : formatted.join('/')}${suffix}`;
}

function pitServiceAnyCellHasValue(values) {
  return values.some((value) => value && value !== '--');
}

function pitServiceGridRow(label, values, tone = 'normal') {
  const cells = values.map((cell) => {
    if (cell && typeof cell === 'object') {
      return {
        value: cell.value || '--',
        tone: cell.tone || tone
      };
    }

    return { value: cell, tone };
  });
  return {
    label,
    cells,
    tone
  };
}

function captureInputStateModel(models, fallbackStatus, index, searchParams = null) {
  const inputs = models.inputs || {};
  if (inputs.hasData !== true) {
    return inputStateModel(
      'waiting for car telemetry',
      [],
      '',
      false,
      inputs,
      []);
  }

  const status = Number.isInteger(inputs.engineWarnings) && inputs.engineWarnings > 0
    ? 'engine warning'
    : joinAvailable(formatGear(inputs.gear), formatRpm(inputs.rpm), inputs.brakeAbsActive === true ? 'ABS' : null);
  return inputStateModel(
    status || fallbackStatus || 'live',
    [],
    '',
    true,
    inputs,
    captureInputTrace(index, inputs, searchParams));
}

function inputStateModel(status, headerItems, source, isAvailable, inputs, trace = null) {
  const hasGraph = inputStateReviewSettings.showThrottleTrace
    || inputStateReviewSettings.showBrakeTrace
    || inputStateReviewSettings.showClutchTrace;
  const hasRail = inputStateReviewSettings.showThrottle
    || inputStateReviewSettings.showBrake
    || inputStateReviewSettings.showClutch
    || inputStateReviewSettings.showSteering
    || inputStateReviewSettings.showGear
    || inputStateReviewSettings.showSpeed;
  const hasContent = hasGraph || hasRail;
  return {
    overlayId: 'input-state',
    title: 'Inputs',
    status: hasContent ? status : 'no input content enabled',
    source,
    bodyKind: 'inputs',
    columns: [],
    rows: [],
    metrics: [],
    points: [],
    headerItems,
    inputs: {
      isAvailable,
      throttle: inputs.throttle,
      brake: inputs.brake,
      clutch: inputs.clutch,
      steeringWheelAngle: inputs.steeringWheelAngle,
      speedMetersPerSecond: inputs.speedMetersPerSecond,
      gear: inputs.gear,
      speedText: formatSpeed(inputs.speedMetersPerSecond),
      gearText: formatGear(inputs.gear),
      steeringText: formatSteering(inputs.steeringWheelAngle),
      brakeAbsActive: inputs.brakeAbsActive === true,
      ...inputStateReviewSettings,
      hasGraph,
      hasRail,
      hasContent,
      sampleIntervalMilliseconds: 50,
      maximumTracePoints: 180,
      trace: isAvailable
        ? Array.isArray(trace) && trace.length > 0
          ? trace
          : [inputTracePoint(inputs)]
        : []
    }
  };
}

function captureInputTrace(index, currentInputs, searchParams = null) {
  const window = replayWindowFromSearchParams(searchParams) ?? replayWindowStatus();
  const sourceWindow = window?.source;
  const frameWindow = window?.frame;
  const windowStartIndex = sourceWindow
    ? firstFrameIndexAtOrAfterSourceElapsed(sourceWindow.start, index)
    : frameWindow?.start;
  const startIndex = Math.max(0, windowStartIndex ?? index - 179);
  const trace = replay.frames
    .slice(startIndex, index + 1)
    .map((frame) => frame.live?.models?.inputs)
    .filter((inputs) => inputs?.hasData === true)
    .map(inputTracePoint);

  if (trace.length === 0 && currentInputs?.hasData === true) {
    trace.push(inputTracePoint(currentInputs));
  }

  return trace;
}

function inputTracePoint(inputs) {
  return {
    throttle: clamp(Number.isFinite(inputs?.throttle) ? inputs.throttle : 0, 0, 1),
    brake: clamp(Number.isFinite(inputs?.brake) ? inputs.brake : 0, 0, 1),
    clutch: clamp(Number.isFinite(inputs?.clutch) ? inputs.clutch : 0, 0, 1),
    brakeAbsActive: inputs?.brakeAbsActive === true
  };
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

function captureGapToLeaderModel(models, frame, index, searchParams = null) {
  const gapSettings = gapSettingsModel();
  const currentGap = focusedClassLeaderGap(models);
  const graph = captureGapGraph(index, searchParams);
  const hasGraphData = graph?.series?.some((series) => Array.isArray(series?.points) && series.points.length > 0) === true;
  const status = currentGap?.hasData || hasGraphData ? 'live | race gap' : 'waiting for timing';
  return {
    overlayId: 'gap-to-leader',
    title: 'Gap To Leader',
    status,
    source: sourceFromSettings(gapSettings, currentGap?.hasData
      ? `source: live gap telemetry | cars ${currentGap.classCarCount}`
      : hasGraphData
        ? `source: live gap telemetry | cars ${graph.selectedSeriesCount}`
      : 'source: waiting'),
    bodyKind: 'graph',
    columns: [],
    rows: [],
    metrics: [],
    points: gapTrendPoints(index),
    graph,
    headerItems: captureHeaderItems(models, status, gapSettings)
  };
}

function captureGapGraph(index, searchParams = null) {
  const context = gapGraphContext(liveModelsForReplayFrame(index, searchParams));
  if (!context) {
    return null;
  }

  const selectedCars = selectGapGraphCars(context);
  const selectedIds = new Set(selectedCars.map((car) => car.carIdx));
  if (selectedIds.size === 0) {
    return null;
  }

  const frameContexts = gapGraphFrameContexts(index, context.focusClass, searchParams);

  if (frameContexts.length === 0) {
    return null;
  }

  const startSeconds = frameContexts[0].context.axisSeconds;
  const rawEndSeconds = frameContexts[frameContexts.length - 1].context.axisSeconds;
  const lapReferenceSeconds = context.lapReferenceSeconds;
  const minimumWindow = Math.max(120, isValidLapReference(lapReferenceSeconds) ? lapReferenceSeconds * 1.5 : 0);
  const rightPadding = Math.max(20, isValidLapReference(lapReferenceSeconds) ? lapReferenceSeconds * 0.15 : 0);
  const endSeconds = Math.max(rawEndSeconds, startSeconds + Math.max(minimumWindow, rawEndSeconds - startSeconds + rightPadding));
  const weather = gapWeatherPoints(frameContexts, startSeconds, endSeconds);
  const series = [...selectedIds].map((carIdx) => {
    let previousAxis = null;
    let previousContextPosition = null;
    const points = [];
    let latestRow = null;
    frameContexts.forEach((item, contextPosition) => {
      const row = item.context.rows.find((candidate) => candidate?.carIdx === carIdx);
      if (!row) {
        return;
      }

      const gapValue = gapValueForGraphRow(row, item.context.leader, item.context.lapReferenceSeconds);
      const gapSeconds = gapValue?.seconds;
      if (!Number.isFinite(gapSeconds)) {
        return;
      }

      const axisSeconds = item.context.axisSeconds;
      const sourceGraphDeltaSeconds = previousAxis === null ? null : axisSeconds - previousAxis;
      const segmentReason = gapSegmentReason(
        frameContexts,
        previousContextPosition,
        contextPosition,
        sourceGraphDeltaSeconds,
        points.length > 0 ? points[points.length - 1]?.gapSeconds : null,
        gapSeconds,
        points.length > 0 ? points[points.length - 1]?.gapSource : null,
        gapValue?.source,
        item.context.lapReferenceSeconds);
      points.push({
        timestampUtc: item.frame?.live?.lastUpdatedAtUtc || new Date().toISOString(),
        axisSeconds,
        gapSeconds,
        gapSource: gapValue?.source || null,
        carIdx,
        sourceReplayIndex: item.index,
        sourceFrameIndex: item.frame?.frameIndex ?? null,
        sourceSessionDeltaSeconds: finiteNumber(item.frame?.sourceSessionDeltaSeconds),
        sourceGraphDeltaSeconds: finiteNumber(sourceGraphDeltaSeconds),
        isReference: row.carIdx === item.context.focusCarIdx,
        isClassLeader: isClassLeaderRow(row, item.context.leader),
        classPosition: toPositiveInteger(row.classPosition),
        startsSegment: segmentReason !== 'continuous',
        segmentReason
      });
      previousAxis = axisSeconds;
      previousContextPosition = contextPosition;
      latestRow = row;
    });

    const selected = selectedCars.find((car) => car.carIdx === carIdx);
    return {
      carIdx,
      isReference: selected?.isReference === true || latestRow?.carIdx === context.focusCarIdx,
      isClassLeader: selected?.isClassLeader === true || isClassLeaderRow(latestRow, context.leader),
      classPosition: toPositiveInteger(selected?.classPosition ?? latestRow?.classPosition),
      alpha: 1,
      isStickyExit: false,
      isStale: false,
      points
    };
  }).filter((item) => item.points.length > 0);

  if (series.reduce((total, item) => total + item.points.length, 0) < 2) {
    return null;
  }

  const leaderChanges = gapLeaderChangeMarkers(frameContexts, startSeconds, endSeconds);
  const scale = selectGapGraphScale(series, startSeconds, endSeconds, lapReferenceSeconds);
  const trendMetrics = demoGapTrendMetrics(series, lapReferenceSeconds);
  const activeThreat = trendMetrics.find((metric) => metric?.chaser) || null;
  return {
    series,
    weather,
    leaderChanges,
    driverChanges: [],
    startSeconds,
    endSeconds,
    maxGapSeconds: scale.maxGapSeconds,
    lapReferenceSeconds,
    selectedSeriesCount: series.length,
    trendMetrics,
    activeThreat,
    threatCarIdx: activeThreat?.chaser?.carIdx ?? null,
    metricDeadbandSeconds: Math.max(0.25, isValidLapReference(lapReferenceSeconds) ? lapReferenceSeconds * 0.0025 : 0),
    comparisonLabel: gapComparisonLabel(context),
    sourceCadence: graphSourceCadence(frameContexts),
    scale
  };
}

function demoGapTrendMetrics(series, lapReferenceSeconds) {
  const candidates = [...series]
    .filter((item) => !item?.isClassLeader)
    .sort((a, b) => (a.classPosition ?? Number.MAX_SAFE_INTEGER) - (b.classPosition ?? Number.MAX_SAFE_INTEGER) || a.carIdx - b.carIdx);
  const threat = candidates[1] || candidates[0] || series.find((item) => !item?.isClassLeader) || null;
  const chaser = threat
    ? { carIdx: threat.carIdx, label: Number.isFinite(threat.classPosition) ? `P${threat.classPosition}` : `#${threat.carIdx}`, gainSeconds: 1.4 }
    : null;
  const lap = Math.max(1, Math.round(chartLapReferenceSeconds(lapReferenceSeconds) / 10));
  return [
    {
      label: '5L',
      focusGapChangeSeconds: -0.8,
      chaser,
      state: 'ready',
      stateLabel: null
    },
    {
      label: '10L',
      focusGapChangeSeconds: 1.2,
      chaser: chaser ? { ...chaser, gainSeconds: 2.1 } : null,
      state: 'ready',
      stateLabel: null
    },
    {
      label: 'Pit',
      focusGapChangeSeconds: null,
      chaser: null,
      state: 'pit',
      stateLabel: null,
      primaryPit: { seconds: 42, lap, isActive: false },
      threatPit: chaser ? { seconds: 39, lap: lap + 1, isActive: false } : null,
      comparisonPit: { seconds: 44, lap, isActive: false }
    },
    {
      label: 'PLap',
      focusGapChangeSeconds: null,
      chaser: null,
      state: 'pitLap',
      stateLabel: null,
      primaryPit: { seconds: 42, lap, isActive: false },
      threatPit: chaser ? { seconds: 39, lap: lap + 1, isActive: false } : null,
      comparisonPit: { seconds: 44, lap, isActive: false }
    },
    {
      label: 'Stint',
      focusGapChangeSeconds: null,
      chaser: null,
      state: 'stint',
      stateLabel: null,
      primaryText: '5L',
      threatText: '6L',
      comparisonText: '5L'
    },
    {
      label: 'Tire',
      focusGapChangeSeconds: null,
      chaser: null,
      state: 'tire',
      stateLabel: null,
      primaryTire: { label: 'Hard', shortLabel: 'H', isWet: false },
      threatTire: { label: 'Wet', shortLabel: 'W', isWet: true },
      comparisonTire: { label: 'Hard', shortLabel: 'H', isWet: false }
    },
    {
      label: 'Last',
      focusGapChangeSeconds: null,
      chaser: null,
      state: 'last',
      stateLabel: null,
      primaryText: '1:31.842',
      threatText: '1:30.913',
      comparisonText: '1:32.104'
    },
    {
      label: 'Status',
      focusGapChangeSeconds: null,
      chaser: null,
      state: 'status',
      stateLabel: null,
      primaryText: 'Track',
      threatText: 'Track',
      comparisonText: 'Pit'
    }
  ];
}

function gapComparisonLabel(context) {
  if (!Number.isFinite(context?.focusGapSeconds)) {
    return inferredGapComparisonLabel(context);
  }

  const candidates = context.rows
    .map((row) => {
      const gapValue = gapValueForGraphRow(row, context.leader, context.lapReferenceSeconds);
      const gapSeconds = gapValue?.seconds;
      return {
        row,
        gapSeconds,
        classPosition: toPositiveInteger(row?.classPosition)
      };
    })
    .filter((item) =>
      item.row
      && Number.isFinite(item.row.carIdx)
      && item.row.carIdx !== context.focusCarIdx
      && Number.isFinite(item.gapSeconds));

  const ahead = candidates
    .filter((item) => item.gapSeconds < context.focusGapSeconds - 0.001)
    .sort((a, b) => (context.focusGapSeconds - a.gapSeconds) - (context.focusGapSeconds - b.gapSeconds)
      || (a.classPosition ?? Number.MAX_SAFE_INTEGER) - (b.classPosition ?? Number.MAX_SAFE_INTEGER))[0];
  const comparison = ahead || candidates
    .filter((item) => item.gapSeconds > context.focusGapSeconds + 0.001)
    .sort((a, b) => (a.gapSeconds - context.focusGapSeconds) - (b.gapSeconds - context.focusGapSeconds)
      || (a.classPosition ?? Number.MAX_SAFE_INTEGER) - (b.classPosition ?? Number.MAX_SAFE_INTEGER))[0];

  return Number.isFinite(comparison?.classPosition) && comparison.classPosition > 0
    ? `P${comparison.classPosition}`
    : inferredGapComparisonLabel(context);
}

function inferredGapComparisonLabel(context) {
  const classPositions = (Array.isArray(context?.rows) ? context.rows : [])
    .map((row) => toPositiveInteger(row?.classPosition))
    .filter((position) => Number.isFinite(position) && position > 0)
    .sort((a, b) => a - b);
  if (classPositions.length === 0) {
    return '--';
  }

  const focusPosition = toPositiveInteger(context?.focus?.classPosition);
  if (focusPosition > 1) {
    return `P${focusPosition - 1}`;
  }

  if (focusPosition === 1) {
    const behind = classPositions.find((position) => position > 1);
    return behind ? `P${behind}` : '--';
  }

  const firstNonLeader = classPositions.find((position) => position > 1);
  return firstNonLeader ? `P${firstNonLeader}` : `P${classPositions[0]}`;
}

function gapGraphFrameContexts(index, focusClass, searchParams = null) {
  const currentElapsed = finiteNumber(replay.frames[index]?.sourceElapsedSeconds);
  const startFrame = Number.isFinite(currentElapsed) && replayTiming.isTimelineMonotonic
    ? firstFrameIndexAtOrAfterSourceElapsed(Math.max(0, currentElapsed - gapGraphTrendWindowSeconds), index)
    : Math.max(0, index - gapGraphMaxContexts + 1);
  const boundedStart = Math.max(0, Math.min(index, startFrame));
  const frameCount = index - boundedStart + 1;
  const step = Math.max(1, Math.ceil(frameCount / gapGraphMaxContexts));
  const selectedIndexes = [];
  for (let frameIndex = boundedStart; frameIndex <= index; frameIndex += step) {
    selectedIndexes.push(frameIndex);
  }
  if (selectedIndexes[selectedIndexes.length - 1] !== index) {
    selectedIndexes.push(index);
  }

  const frameContexts = [];
  for (const frameIndex of selectedIndexes) {
    const frame = replay.frames[frameIndex];
    const frameContext = gapGraphContext(liveModelsForReplayFrame(frameIndex, searchParams), focusClass);
    if (frameContext) {
      frameContexts.push({ frame, index: frameIndex, context: frameContext });
    }
  }
  return frameContexts;
}

function gapSegmentReason(
  frameContexts,
  previousContextPosition,
  contextPosition,
  sourceGraphDeltaSeconds,
  previousGapSeconds = null,
  gapSeconds = null,
  previousGapSource = null,
  gapSource = null,
  lapReferenceSeconds = null) {
  if (previousContextPosition === null || previousContextPosition === undefined) {
    return 'first-point';
  }

  if (String(previousGapSource || '') !== String(gapSource || '')
    && Number.isFinite(previousGapSeconds)
    && Number.isFinite(gapSeconds)
    && !acceptsGapTrendPoint(previousGapSeconds, gapSeconds, lapReferenceSeconds)) {
    return 'source-crossover';
  }

  if (!Number.isFinite(sourceGraphDeltaSeconds) || sourceGraphDeltaSeconds <= gapMissingSegmentThresholdSeconds) {
    return 'continuous';
  }

  return hasSourceCadenceGapBetween(frameContexts, previousContextPosition, contextPosition)
    ? 'source-cadence-gap'
    : 'data-unavailable';
}

function hasSourceCadenceGapBetween(frameContexts, previousContextPosition, contextPosition) {
  for (let index = previousContextPosition + 1; index <= contextPosition; index += 1) {
    const previousAxis = frameContexts[index - 1]?.context?.axisSeconds;
    const axis = frameContexts[index]?.context?.axisSeconds;
    if (Number.isFinite(previousAxis) && Number.isFinite(axis) && axis - previousAxis > gapMissingSegmentThresholdSeconds) {
      return true;
    }
  }
  return false;
}

function graphSourceCadence(frameContexts) {
  const deltas = [];
  for (let index = 1; index < frameContexts.length; index += 1) {
    const previousAxis = frameContexts[index - 1]?.context?.axisSeconds;
    const axis = frameContexts[index]?.context?.axisSeconds;
    if (Number.isFinite(previousAxis) && Number.isFinite(axis) && axis >= previousAxis) {
      deltas.push(axis - previousAxis);
    }
  }
  const summary = summarizeNumbers(deltas);
  return {
    basis: 'graph frame context axisSeconds',
    contextCount: frameContexts.length,
    gapToLeaderMissingSegmentThresholdSeconds: gapMissingSegmentThresholdSeconds,
    denseForGapToLeader: frameContexts.length < 2 || (deltas.length > 0 && Math.max(...deltas) <= gapMissingSegmentThresholdSeconds),
    sourceSessionDeltaSeconds: summary
  };
}

function gapGraphContext(models, preferredClass = null) {
  if (!isGreenRace(models)) {
    return null;
  }

  const timing = models?.timing || {};
  const focusCarIdx = models?.reference?.focusCarIdx ?? timing.focusCarIdx;
  const focus = timing.focusRow
    || timing.playerRow
    || (Array.isArray(timing.classRows) ? timing.classRows.find((row) => row?.carIdx === focusCarIdx) : null)
    || (Array.isArray(timing.overallRows) ? timing.overallRows.find((row) => row?.carIdx === focusCarIdx) : null);
  if (!focus) {
    return null;
  }

  const focusClass = preferredClass ?? focus.carClass;
  const classFocus = focusClass == null ? focus : { ...focus, carClass: focusClass };
  const rows = classTimingRows(timing, classFocus);
  if (rows.length === 0) {
    return null;
  }

  const leader = rows.find((row) => toPositiveInteger(row?.classPosition) === 1)
    || rows.find((row) => toPositiveInteger(row?.overallPosition) === 1)
    || rows[0];

  const lapReference = lapReferenceSeconds(rows);
  const axisSeconds = Number.isFinite(models?.session?.sessionTimeSeconds)
    ? models.session.sessionTimeSeconds
    : Number.isFinite(models?.latestSample?.sessionTime)
      ? models.latestSample.sessionTime
      : Date.now() / 1000;
  const focusGapSeconds = gapSecondsForGraphRow(focus, leader, lapReference);
  return {
    models,
    timing,
    focus,
    focusCarIdx: focus.carIdx,
    focusClass,
    rows,
    leader,
    lapReferenceSeconds: lapReference,
    focusGapSeconds,
    axisSeconds
  };
}

function selectGapGraphCars(context) {
  const selected = new Map();
  const add = (car) => {
    if (car && Number.isFinite(car.carIdx) && !selected.has(car.carIdx)) {
      selected.set(car.carIdx, car);
    }
  };
  const candidates = context.rows
    .map((row) => {
      const gapValue = gapValueForGraphRow(row, context.leader, context.lapReferenceSeconds);
      const gapSeconds = gapValue?.seconds;
      const deltaSeconds = Number.isFinite(gapSeconds) && Number.isFinite(context.focusGapSeconds)
        ? gapSeconds - context.focusGapSeconds
        : null;
      return {
        row,
        carIdx: row.carIdx,
        classPosition: toPositiveInteger(row.classPosition),
        gapSeconds,
        deltaSeconds,
        isReference: row.carIdx === context.focusCarIdx,
        isClassLeader: isClassLeaderRow(row, context.leader)
      };
    })
    .filter((car) => Number.isFinite(car.carIdx) && Number.isFinite(car.gapSeconds));

  const reference = candidates.find((car) => car.isReference);
  const referenceCanAnchor = reference && !isLappedGraphGap(reference.gapSeconds, context.lapReferenceSeconds);
  candidates.filter((car) => car.isClassLeader || (referenceCanAnchor && car.isReference)).forEach(add);
  if (!referenceCanAnchor) {
    candidates
      .filter((car) => !car.isClassLeader && !isLappedGraphGap(car.gapSeconds, context.lapReferenceSeconds))
      .sort((a, b) => a.gapSeconds - b.gapSeconds || (a.classPosition ?? Number.MAX_SAFE_INTEGER) - (b.classPosition ?? Number.MAX_SAFE_INTEGER))
      .slice(0, Math.max(1, gapCarsBehind))
      .forEach(add);

    if (selected.size <= 1) {
      candidates
        .sort((a, b) => a.gapSeconds - b.gapSeconds || (a.classPosition ?? Number.MAX_SAFE_INTEGER) - (b.classPosition ?? Number.MAX_SAFE_INTEGER))
        .slice(0, 6)
        .forEach(add);
    }

    return [...selected.values()].sort((a, b) => a.gapSeconds - b.gapSeconds || a.carIdx - b.carIdx);
  }

  candidates
    .filter((car) => !car.isClassLeader && !car.isReference && car.deltaSeconds < 0 && isSameLapGapCandidate(car, context))
    .sort((a, b) => b.deltaSeconds - a.deltaSeconds)
    .slice(0, gapCarsAhead)
    .forEach(add);
  candidates
    .filter((car) => !car.isClassLeader && !car.isReference && car.deltaSeconds > 0 && isSameLapGapCandidate(car, context))
    .sort((a, b) => a.deltaSeconds - b.deltaSeconds)
    .slice(0, gapCarsBehind)
    .forEach(add);

  if (selected.size <= 1) {
    candidates
      .sort((a, b) => a.gapSeconds - b.gapSeconds || (a.classPosition ?? Number.MAX_SAFE_INTEGER) - (b.classPosition ?? Number.MAX_SAFE_INTEGER))
      .slice(0, 6)
      .forEach(add);
  }

  return [...selected.values()].sort((a, b) => a.gapSeconds - b.gapSeconds || a.carIdx - b.carIdx);
}

function gapSecondsForGraphRow(row, leader, lapReferenceSeconds) {
  return gapValueForGraphRow(row, leader, lapReferenceSeconds)?.seconds ?? null;
}

function gapValueForGraphRow(row, leader, lapReferenceSeconds) {
  if (!row || !leader) {
    return null;
  }

  if (isPlaceholderPitGapRow(row)) {
    return null;
  }

  if (row.carIdx === leader.carIdx || toPositiveInteger(row.classPosition) === 1) {
    return { seconds: 0, source: 'class-leader-row' };
  }

  const gap = derivedClassGap(row, leader, lapReferenceSeconds);
  if (!gap) {
    return null;
  }

  if (Number.isFinite(gap.seconds)) {
    return { seconds: gap.seconds, source: gap.source || null };
  }

  return Number.isFinite(gap.laps)
    ? { seconds: gap.laps * chartLapReferenceSeconds(lapReferenceSeconds), source: gap.source || null }
    : null;
}

function isSameLapGapCandidate(car, context) {
  if (!Number.isFinite(car?.gapSeconds) || !Number.isFinite(context?.focusGapSeconds)) {
    return false;
  }

  const lapReference = chartLapReferenceSeconds(context.lapReferenceSeconds);
  return Math.abs((car.gapSeconds - context.focusGapSeconds) / lapReference) < 0.95;
}

function isLappedGraphGap(gapSeconds, lapReferenceSeconds) {
  return Number.isFinite(gapSeconds)
    && isValidLapReference(lapReferenceSeconds)
    && gapSeconds >= lapReferenceSeconds * 0.95;
}

function isClassLeaderRow(row, leader) {
  return Boolean(row) && (row.carIdx === leader?.carIdx || toPositiveInteger(row.classPosition) === 1);
}

function gapWeatherPoints(frameContexts, startSeconds, endSeconds) {
  const points = [];
  for (const item of frameContexts) {
    const condition = gapWeatherCondition(item.context.models);
    if (points.length > 0 && points[points.length - 1].condition === condition) {
      continue;
    }

    points.push({
      axisSeconds: Math.max(startSeconds, Math.min(endSeconds, item.context.axisSeconds)),
      condition
    });
  }
  return points;
}

function gapWeatherCondition(models) {
  const weather = models?.weather || {};
  if (weather.weatherDeclaredWet === true) return 'DeclaredWet';
  const wetness = Number(weather.trackWetness);
  if (!Number.isFinite(wetness)) return 'Unknown';
  if (wetness >= 4) return 'Wet';
  if (wetness >= 2) return 'Damp';
  return 'Dry';
}

function gapLeaderChangeMarkers(frameContexts, startSeconds, endSeconds) {
  const markers = [];
  let previousLeader = null;
  for (const item of frameContexts) {
    const leaderCarIdx = item.context.leader?.carIdx;
    if (!Number.isFinite(leaderCarIdx)) {
      continue;
    }

    if (previousLeader !== null && previousLeader !== leaderCarIdx) {
      markers.push({
        timestampUtc: item.frame?.live?.lastUpdatedAtUtc || new Date().toISOString(),
        axisSeconds: Math.max(startSeconds, Math.min(endSeconds, item.context.axisSeconds)),
        previousLeaderCarIdx: previousLeader,
        newLeaderCarIdx: leaderCarIdx
      });
    }

    previousLeader = leaderCarIdx;
  }
  return markers;
}

function selectGapGraphScale(series, startSeconds, endSeconds, lapReferenceSeconds) {
  const allPoints = series.flatMap((item) => item.points || [])
    .filter((point) => Number.isFinite(point.gapSeconds) && point.axisSeconds >= startSeconds && point.axisSeconds <= endSeconds);
  const leaderMax = niceCeiling(Math.max(1, ...allPoints.map((point) => Math.max(0, point.gapSeconds))));
  const referenceSeries = series.find((item) => item.isReference);
  const referencePoints = (referenceSeries?.points || [])
    .filter((point) => Number.isFinite(point.axisSeconds) && Number.isFinite(point.gapSeconds) && point.axisSeconds >= startSeconds && point.axisSeconds <= endSeconds)
    .sort((a, b) => a.axisSeconds - b.axisSeconds);
  if (referencePoints.length === 0) {
    return gapLeaderScale(leaderMax);
  }

  const latestReferenceGap = referenceGapAt(referencePoints, endSeconds);
  const triggerGap = Math.max(90, isValidLapReference(lapReferenceSeconds) ? lapReferenceSeconds * 0.5 : 0);
  if (latestReferenceGap < triggerGap) {
    return gapLeaderScale(leaderMax);
  }

  let maxAheadSeconds = 0;
  let maxBehindSeconds = 0;
  let hasLocalComparison = false;
  for (const item of series.filter((candidate) => !candidate.isClassLeader)) {
    for (const point of item.points || []) {
      if (!Number.isFinite(point.axisSeconds) || !Number.isFinite(point.gapSeconds) || point.axisSeconds < startSeconds || point.axisSeconds > endSeconds) {
        continue;
      }

      const delta = point.gapSeconds - referenceGapAt(referencePoints, point.axisSeconds);
      hasLocalComparison ||= !item.isReference && Math.abs(delta) > 0.001;
      if (delta < 0) {
        maxAheadSeconds = Math.max(maxAheadSeconds, Math.abs(delta));
      } else {
        maxBehindSeconds = Math.max(maxBehindSeconds, delta);
      }
    }
  }

  const minimumRange = Math.max(20, isValidLapReference(lapReferenceSeconds) ? lapReferenceSeconds * 0.1 : 0);
  const aheadSeconds = niceCeiling(Math.max(minimumRange, maxAheadSeconds * 1.18));
  const behindSeconds = niceCeiling(Math.max(minimumRange, maxBehindSeconds * 1.18));
  const localRange = Math.max(aheadSeconds, behindSeconds);
  const forceFocusScaleForLappedReference = isValidLapReference(lapReferenceSeconds)
    && latestReferenceGap >= lapReferenceSeconds * 0.95;
  if (!forceFocusScaleForLappedReference
    && (!hasLocalComparison || leaderMax < Math.max(triggerGap, localRange * 3))) {
    return gapLeaderScale(leaderMax);
  }

  return {
    maxGapSeconds: leaderMax,
    isFocusRelative: true,
    aheadSeconds,
    behindSeconds,
    referencePoints,
    latestReferenceGapSeconds: latestReferenceGap
  };
}

function gapLeaderScale(maxGapSeconds) {
  return {
    maxGapSeconds,
    isFocusRelative: false,
    aheadSeconds: 0,
    behindSeconds: 0,
    referencePoints: [],
    latestReferenceGapSeconds: 0
  };
}

function referenceGapAt(referencePoints, axisSeconds) {
  const points = (Array.isArray(referencePoints) ? referencePoints : [])
    .filter((point) => Number.isFinite(point?.axisSeconds) && Number.isFinite(point?.gapSeconds))
    .sort((a, b) => a.axisSeconds - b.axisSeconds);
  if (points.length === 0) {
    return 0;
  }

  if (axisSeconds <= points[0].axisSeconds) {
    return points[0].gapSeconds;
  }

  const last = points[points.length - 1];
  if (axisSeconds >= last.axisSeconds) {
    return last.gapSeconds;
  }

  const afterIndex = points.findIndex((point) => point.axisSeconds >= axisSeconds);
  const after = points[Math.max(0, afterIndex)];
  const before = points[Math.max(0, afterIndex - 1)];
  const span = after.axisSeconds - before.axisSeconds;
  if (span <= 0.001) {
    return before.gapSeconds;
  }

  const ratio = Math.max(0, Math.min(1, (axisSeconds - before.axisSeconds) / span));
  return before.gapSeconds + (after.gapSeconds - before.gapSeconds) * ratio;
}

function niceCeiling(value) {
  if (!Number.isFinite(value) || value <= 1) {
    return 1;
  }

  const magnitude = Math.pow(10, Math.floor(Math.log10(value)));
  const normalized = value / magnitude;
  for (const step of [1, 1.5, 2, 3, 5, 7.5, 10]) {
    if (normalized <= step) {
      return step * magnitude;
    }
  }

  return 10 * magnitude;
}

function gapTrendPoints(index) {
  const values = [];
  const start = Math.max(0, index - 23);
  for (let frameIndex = start; frameIndex <= index; frameIndex += 1) {
    const frame = replay.frames[frameIndex];
    const gap = focusedClassLeaderGap(frame?.live?.models || {});
    if (!gap?.hasData || !Number.isFinite(gap.trendSeconds) || gap.trendSeconds < 0) {
      continue;
    }

    if (values.length === 0 || acceptsGapTrendPoint(values[values.length - 1], gap.trendSeconds, gap.lapReferenceSeconds)) {
      values.push(gap.trendSeconds);
    }
  }
  return values;
}

function focusedClassLeaderGap(models) {
  if (!isGreenRace(models)) {
    return null;
  }

  const timing = models?.timing || {};
  const focusCarIdx = models?.reference?.focusCarIdx ?? timing.focusCarIdx;
  const focus = timing.focusRow
    || timing.playerRow
    || (Array.isArray(timing.classRows) ? timing.classRows.find((row) => row?.carIdx === focusCarIdx) : null)
    || (Array.isArray(timing.overallRows) ? timing.overallRows.find((row) => row?.carIdx === focusCarIdx) : null);
  if (!focus) {
    return null;
  }

  const rows = classTimingRows(timing, focus);
  const leader = rows.find((row) => toPositiveInteger(row?.classPosition) === 1)
    || rows.find((row) => toPositiveInteger(row?.overallPosition) === 1)
    || rows[0];
  const classPosition = toPositiveInteger(focus.classPosition);
  if (classPosition === 1 || leader?.carIdx === focus.carIdx) {
    return {
      hasData: true,
      classPosition,
      seconds: 0,
      laps: 0,
      trendSeconds: 0,
      lapReferenceSeconds: lapReferenceSeconds(rows),
      classCarCount: rows.length
    };
  }

  if (!leader) {
    return null;
  }

  const lapReference = lapReferenceSeconds(rows);
  const gap = derivedClassGap(focus, leader, lapReference);
  if (!gap) {
    return null;
  }

  return {
    hasData: true,
    classPosition,
    seconds: gap.seconds,
    laps: gap.laps,
    trendSeconds: Number.isFinite(gap.seconds) ? gap.seconds : (gap.laps || 0) * chartLapReferenceSeconds(lapReference),
    lapReferenceSeconds: lapReference,
    classCarCount: rows.length
  };
}

function classTimingRows(timing, focus) {
  const rows = Array.isArray(timing?.classRows) && timing.classRows.length > 0
    ? timing.classRows
    : Array.isArray(timing?.overallRows)
      ? timing.overallRows
      : [];
  const focusClass = focus?.carClass;
  return rows
    .filter((row) => row && (focusClass == null || row.carClass == null || row.carClass === focusClass))
    .sort((a, b) =>
      (toPositiveInteger(a.classPosition) ?? Number.MAX_SAFE_INTEGER) - (toPositiveInteger(b.classPosition) ?? Number.MAX_SAFE_INTEGER)
      || (toPositiveInteger(a.overallPosition) ?? Number.MAX_SAFE_INTEGER) - (toPositiveInteger(b.overallPosition) ?? Number.MAX_SAFE_INTEGER)
      || (toPositiveInteger(a.carIdx) ?? Number.MAX_SAFE_INTEGER) - (toPositiveInteger(b.carIdx) ?? Number.MAX_SAFE_INTEGER));
}

function derivedClassGap(row, leader, lapReferenceSeconds) {
  const lapGap = wholeLapGap(leader, row);
  if (Number.isFinite(lapGap) && lapGap > 0) {
    return { seconds: null, laps: lapGap, source: 'CarIdxLapCompleted' };
  }

  const projected = estimatedSecondsBehind(row, leader, lapReferenceSeconds);
  if (Number.isFinite(projected)) {
    return { seconds: projected, laps: null, source: 'CarIdxEstTime+CarIdxLapDistPct' };
  }

  const startGap = estimatedGreenStartSecondsBehind(row, leader, lapReferenceSeconds);
  if (Number.isFinite(startGap)) {
    return { seconds: startGap, laps: null, source: 'CarIdxEstTime+CarIdxPosition' };
  }

  const rowF2 = usableF2ForRace(row);
  const leaderF2 = usableF2ForRace(leader);
  if (Number.isFinite(rowF2) && Number.isFinite(leaderF2) && rowF2 >= leaderF2) {
    return { seconds: rowF2 - leaderF2, laps: null, source: 'CarIdxF2Time' };
  }

  return null;
}

function estimatedGreenStartSecondsBehind(row, leader, lapReferenceSeconds) {
  if (!isRaceLaunchEstimatedRow(row) || !isRaceLaunchEstimatedRow(leader) || !isPositionBehind(row, leader)) {
    return null;
  }

  const rowEstimated = validTimingSeconds(row?.estimatedTimeSeconds);
  const leaderEstimated = validTimingSeconds(leader?.estimatedTimeSeconds);
  if (!Number.isFinite(rowEstimated) || !Number.isFinite(leaderEstimated)) {
    return null;
  }

  const seconds = leaderEstimated - rowEstimated;
  const maximumSeconds = Math.min(30, Math.max(5, chartLapReferenceSeconds(lapReferenceSeconds) * 0.25));
  return Number.isFinite(seconds) && seconds >= 0 && seconds <= maximumSeconds ? seconds : null;
}

function isRaceLaunchEstimatedRow(row) {
  if (!row || row.hasTakenGrid !== true || validLapDistPct(row.lapDistPct) !== null) {
    return false;
  }

  return toPositiveInteger(row.classPosition) === 1 || isRaceF2Placeholder(row);
}

function isPositionBehind(row, leader) {
  const rowClassPosition = toPositiveInteger(row?.classPosition);
  const leaderClassPosition = toPositiveInteger(leader?.classPosition);
  if (rowClassPosition && leaderClassPosition) {
    return rowClassPosition > leaderClassPosition;
  }

  const rowOverallPosition = toPositiveInteger(row?.overallPosition);
  const leaderOverallPosition = toPositiveInteger(leader?.overallPosition);
  return rowOverallPosition && leaderOverallPosition
    ? rowOverallPosition > leaderOverallPosition
    : false;
}

function wholeLapGap(leader, row) {
  const leaderProgress = progressLaps(leader);
  const rowProgress = progressLaps(row);
  if (Number.isFinite(leaderProgress) && Number.isFinite(rowProgress)) {
    const progressGap = leaderProgress - rowProgress;
    if (progressGap < 0.95) {
      return null;
    }

    const roundedGap = Math.round(progressGap);
    return roundedGap >= 1 && Math.abs(progressGap - roundedGap) <= 0.35
      ? roundedGap
      : progressGap;
  }

  const leaderLap = Number.isInteger(leader?.lapCompleted) ? leader.lapCompleted : null;
  const rowLap = Number.isInteger(row?.lapCompleted) ? row.lapCompleted : null;
  if (leaderLap == null || rowLap == null) {
    return null;
  }

  const laps = leaderLap - rowLap;
  return laps > 0 ? laps : null;
}

function estimatedSecondsBehind(row, leader, lapReferenceSeconds) {
  const rowEstimated = validTimingSeconds(row?.estimatedTimeSeconds);
  const leaderEstimated = validTimingSeconds(leader?.estimatedTimeSeconds);
  const rowLapDist = validLapDistPct(row?.lapDistPct);
  const leaderLapDist = validLapDistPct(leader?.lapDistPct);
  if (!Number.isFinite(rowEstimated)
    || !Number.isFinite(leaderEstimated)
    || !Number.isFinite(rowLapDist)
    || !Number.isFinite(leaderLapDist)) {
    return null;
  }

  let relativeLaps = rowLapDist - leaderLapDist;
  if (relativeLaps > 0.5) {
    relativeLaps -= 1;
  } else if (relativeLaps < -0.5) {
    relativeLaps += 1;
  }

  let seconds = rowEstimated - leaderEstimated;
  if (Number.isFinite(lapReferenceSeconds)) {
    if (seconds > lapReferenceSeconds / 2) {
      seconds -= lapReferenceSeconds;
    } else if (seconds < -lapReferenceSeconds / 2) {
      seconds += lapReferenceSeconds;
    }
  }

  if (!Number.isFinite(seconds) || seconds > 0) {
    return null;
  }

  const timingSign = Math.sign(seconds);
  const lapSign = Math.sign(relativeLaps);
  if (timingSign !== 0 && lapSign !== 0 && timingSign !== lapSign) {
    return null;
  }

  if (Number.isFinite(lapReferenceSeconds)) {
    const lapBasedSeconds = Math.abs(relativeLaps * lapReferenceSeconds);
    const maximumDelta = Math.max(5, Math.min(lapReferenceSeconds / 2, lapBasedSeconds + 10));
    if (Math.abs(seconds) > maximumDelta) {
      return null;
    }
  } else if (Math.abs(seconds) > 60) {
    return null;
  }

  return Math.max(0, -seconds);
}

function progressLaps(row) {
  const lap = Number.isInteger(row?.lapCompleted) && row.lapCompleted >= 0 ? row.lapCompleted : null;
  const pct = validLapDistPct(row?.lapDistPct);
  return lap !== null && Number.isFinite(pct) ? lap + pct : null;
}

function usableF2ForRace(row) {
  const f2 = validNonNegative(row?.f2TimeSeconds);
  if (!Number.isFinite(f2)) {
    return null;
  }

  const overallPosition = toPositiveInteger(row?.overallPosition);
  const classPosition = toPositiveInteger(row?.classPosition);
  if (f2 === 0) {
    return overallPosition === 1 || classPosition === 1 ? 0 : null;
  }

  if (classPosition !== 1 && f2 < 0.1) {
    return null;
  }

  if (isRaceF2Placeholder(row)) {
    return null;
  }

  return f2;
}

function isRaceF2Placeholder(row) {
  const f2 = validNonNegative(row?.f2TimeSeconds);
  const overallPosition = toPositiveInteger(row?.overallPosition);
  return Number.isFinite(f2)
    && overallPosition
    && overallPosition > 1
    && Math.abs(f2 - ((overallPosition - 1) / 1000)) <= 0.00002;
}

function isPlaceholderPitGapRow(row) {
  if (!row || row.hasTakenGrid === true) {
    return false;
  }

  if (row.onPitRoad !== true && !isKnownNonTrackSurface(row.trackSurface)) {
    return false;
  }

  return !hasUsableRaceTiming(row) && (!Number.isInteger(row.lapCompleted) || row.lapCompleted <= 0);
}

function hasUsableRaceTiming(row) {
  const f2 = validNonNegative(row?.f2TimeSeconds);
  return Number.isFinite(f2) && f2 >= 0.1 && !isRaceF2Placeholder(row);
}

function isKnownNonTrackSurface(trackSurface) {
  return trackSurface !== null && trackSurface !== undefined && trackSurface !== 3;
}

function lapReferenceSeconds(rows) {
  const samples = [];
  for (const row of rows) {
    for (const key of ['lastLapTimeSeconds', 'bestLapTimeSeconds']) {
      const seconds = validTimingSeconds(row?.[key]);
      if (Number.isFinite(seconds) && seconds >= 20 && seconds <= 300) {
        samples.push(seconds);
        break;
      }
    }
  }

  if (samples.length === 0) {
    return null;
  }

  samples.sort((a, b) => a - b);
  const middle = Math.floor(samples.length / 2);
  return samples.length % 2 === 0
    ? (samples[middle - 1] + samples[middle]) / 2
    : samples[middle];
}

function acceptsGapTrendPoint(previous, next, lapReferenceSeconds) {
  const maximumJump = Math.max(30, Math.min(180, chartLapReferenceSeconds(lapReferenceSeconds) * 0.5));
  return Math.abs(next - previous) <= maximumJump;
}

function isGreenRace(models) {
  const state = models?.session?.sessionState;
  if (!Number.isFinite(state) || state < 4) {
    return false;
  }

  const sessionText = String(models?.session?.sessionType || models?.session?.eventType || models?.session?.sessionName || '').toLowerCase();
  return !/\b(practice|qualifying|qualify|test)\b/.test(sessionText) || /\brace\b/.test(sessionText);
}

function validTimingSeconds(value) {
  return Number.isFinite(value) && value > 0.05 ? value : null;
}

function validNonNegative(value) {
  return Number.isFinite(value) && value >= 0 ? value : null;
}

function validLapDistPct(value) {
  return Number.isFinite(value) && value >= 0 ? Math.max(0, Math.min(1, value)) : null;
}

function chartLapReferenceSeconds(value) {
  return Number.isFinite(value) && value > 20 && value < 1800 ? value : 60;
}

function isValidLapReference(value) {
  return Number.isFinite(value) && value > 20 && value < 1800;
}

function toPositiveInteger(value) {
  return Number.isInteger(value) && value > 0 ? value : null;
}

function formatPosition(position) {
  return Number.isInteger(position) && position > 0 ? String(position) : '--';
}

function formatGapValue(gap) {
  if (!gap?.hasData) {
    return '--';
  }

  if (gap.seconds === 0 || gap.laps === 0) {
    return 'leader';
  }

  if (Number.isFinite(gap.seconds)) {
    return formatGapSeconds(gap.seconds);
  }

  return Number.isFinite(gap.laps)
    ? `+${gap.laps.toFixed(2)} lap`
    : '--';
}

function formatGapSeconds(seconds) {
  if (!Number.isFinite(seconds)) {
    return '--';
  }

  if (seconds >= 60) {
    const minutes = Math.floor(seconds / 60);
    return `+${minutes}:${String((seconds % 60).toFixed(1)).padStart(4, '0')}`;
  }

  return `+${seconds.toFixed(1)}s`;
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

function joinAvailable(...values) {
  const parts = values
    .map((value) => String(value ?? '').trim())
    .filter((value) => value && value !== '--');
  return parts.length > 0 ? parts.join(' | ') : '--';
}

function firstText(...values) {
  return values.find((value) => typeof value === 'string' && value.trim().length > 0)?.trim() || null;
}

function formatLiters(value) {
  if (!Number.isFinite(value)) return '--';
  return isImperial()
    ? `${(value * 0.2641720524).toFixed(1)} gal`
    : `${value.toFixed(1)} L`;
}

function formatFuelPerLap(value) {
  if (!Number.isFinite(value)) return '--';
  return isImperial()
    ? `${(value * 0.2641720524).toFixed(1)} gal/lap`
    : `${value.toFixed(1)} L/lap`;
}

function formatPercent(value) {
  return Number.isFinite(value) ? `${Math.round(value * 100)}%` : '--';
}

function formatFuelBurn(value) {
  return Number.isFinite(value) ? `${value.toFixed(1)} kg/h` : '--';
}

function formatTemp(value) {
  if (!Number.isFinite(value)) return '--';
  return isImperial()
    ? `${Math.round(value * 9 / 5 + 32)} F`
    : `${Math.round(value)} C`;
}

function formatSpeed(value) {
  if (!Number.isFinite(value)) return '--';
  return isImperial()
    ? `${Math.round(value * 2.2369362921)} mph`
    : `${Math.round(value * 3.6)} km/h`;
}

function formatPressureBar(value) {
  if (!Number.isFinite(value)) return '--';
  return isImperial()
    ? `${Math.round(value * 14.5037738)} psi`
    : `${value.toFixed(1)} bar`;
}

function formatDurationCompact(seconds) {
  if (!Number.isFinite(seconds) || seconds < 0) return '--';
  const total = Math.floor(seconds);
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const remainder = total % 60;
  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}`
    : `${minutes}:${String(remainder).padStart(2, '0')}`;
}

function formatProgressLaps(value) {
  return Number.isFinite(value) ? `${value.toFixed(2)} laps` : '--';
}

function formatTrackLength(value) {
  return Number.isFinite(value) ? `${value.toFixed(2)} km` : null;
}

function trackWetnessLabel(value, declaredWet) {
  if (declaredWet === true) return 'Declared wet';
  if (!Number.isFinite(value)) return '--';
  return value <= 1 ? 'Dry' : value <= 3 ? 'Damp' : 'Wet';
}

function hasWetSurfaceSignal(weather) {
  return weather?.weatherDeclaredWet === true || (Number.isFinite(weather?.trackWetness) && weather.trackWetness > 1);
}

function formatSessionClock(session) {
  const elapsed = formatDurationCompact(session?.sessionTimeSeconds);
  const remain = formatDurationCompact(session?.sessionTimeRemainSeconds);
  if (elapsed === '--' && remain === '--') return '--';
  const suffix = isRacePreGreenSession(session) ? 'countdown' : 'left';
  return `${elapsed} elapsed | ${remain} ${suffix}`;
}

function isRacePreGreenSession(session) {
  const state = session?.sessionState;
  if (!Number.isFinite(state) || state < 1 || state > 3) return false;
  const text = `${session?.sessionType || ''} ${session?.sessionName || ''} ${session?.eventType || ''}`.toLowerCase();
  return text.includes('race');
}

function formatSessionLaps(session, raceProgress = {}, raceProjection = {}) {
  const remain = formatRemainingLapCount(session?.sessionLapsRemain ?? session?.sessionLapsRemainEx)
    || formatEstimatedLapCount(raceProjection?.estimatedTeamLapsRemaining)
    || formatEstimatedLapCount(raceProgress?.raceLapsRemaining);
  const total = formatLapCount(session?.sessionLapsTotal)
    || formatEstimatedTotalLapCount(raceProjection?.estimatedFinishLap)
    || formatEstimatedRaceProgressTotalLaps(raceProgress)
    || formatLapCount(session?.raceLaps);
  return `${remain || '--'} left | ${total || '--'} total`;
}

function formatLapCount(value) {
  return Number.isFinite(value) && value > 0 && value <= 1000 ? String(Math.round(value)) : null;
}

function formatRemainingLapCount(value) {
  return Number.isFinite(value) && value >= 0 && value <= 1000 ? String(Math.round(value)) : null;
}

function formatEstimatedLapCount(value) {
  return Number.isFinite(value) && value >= 0 && value <= 1000 ? `${value.toFixed(value % 1 === 0 ? 0 : 1)} est` : null;
}

function formatEstimatedTotalLapCount(value) {
  return Number.isFinite(value) && value >= 0 && value <= 1000 ? `${Math.ceil(value)} est` : null;
}

function formatEstimatedRaceProgressTotalLaps(raceProgress = {}) {
  const remaining = raceProgress?.raceLapsRemaining;
  if (!Number.isFinite(remaining) || remaining < 0 || remaining > 1000) return null;
  const progress = [
    raceProgress?.overallLeaderProgressLaps,
    raceProgress?.classLeaderProgressLaps,
    raceProgress?.strategyCarProgressLaps
  ].find((value) => Number.isFinite(value) && value >= 0);
  return Number.isFinite(progress) ? `${Math.ceil(progress + remaining)} est` : null;
}

function pitTimeLaps(models) {
  const session = models.session || {};
  const timeRemaining = formatPitTimeRemaining(session);
  return joinAvailable(timeRemaining, pitCompactLaps(session, models.raceProgress, models.raceProjection));
}

function pitCompactLaps(session, raceProgress = {}, raceProjection = {}) {
  const remain = formatRemainingLapCount(session?.sessionLapsRemain ?? session?.sessionLapsRemainEx)
    || formatEstimatedLapCount(raceProjection?.estimatedTeamLapsRemaining)
    || formatEstimatedLapCount(raceProgress?.raceLapsRemaining);
  const total = formatLapCount(session?.sessionLapsTotal)
    || formatEstimatedTotalLapCount(raceProjection?.estimatedFinishLap)
    || formatEstimatedRaceProgressTotalLaps(raceProgress)
    || formatLapCount(session?.raceLaps);
  if (remain && total) return `${remain}/${total} laps`;
  if (remain) return `${remain} laps`;
  if (total) return `${total} laps total`;
  return null;
}

function formatPitTimeRemaining(session) {
  const seconds = session?.sessionTimeRemainSeconds;
  if (!Number.isFinite(seconds) || seconds < 0) return null;
  const totalSeconds = Math.ceil(Math.max(0, seconds));
  if (isRacePreGreenSession(session)) {
    return `${String(Math.floor(totalSeconds / 60)).padStart(2, '0')}:${String(totalSeconds % 60).padStart(2, '0')}`;
  }
  const totalMinutes = Math.ceil(totalSeconds / 60);
  return `${String(Math.floor(totalMinutes / 60)).padStart(2, '0')}:${String(totalMinutes % 60).padStart(2, '0')}`;
}

function formatWeatherTemps(weather) {
  const air = formatTemp(weather?.airTempC);
  const track = formatTemp(weather?.trackTempCrewC);
  return air === '--' && track === '--' ? '--' : `air ${air} | track ${track}`;
}

function formatWeatherSurface(weather) {
  const wetness = weather?.trackWetnessLabel || trackWetnessLabel(weather?.trackWetness, weather?.weatherDeclaredWet);
  return joinAvailable(wetness, weather?.weatherDeclaredWet === true ? 'declared wet' : null, weather?.rubberState ? `rubber ${weather.rubberState}` : null);
}

function formatWeatherSky(weather) {
  const precipitation = Number.isFinite(weather?.precipitationPercent)
    ? `rain:${weather.precipitationPercent.toFixed(0)}%`
    : null;
  return joinAvailable(weather?.skiesLabel, weather?.weatherType, precipitation);
}

function formatWindAtmosphere(weather) {
  const windSpeed = formatSpeed(weather?.windVelocityMetersPerSecond);
  const windDirection = cardinalDirection(weather?.windDirectionRadians);
  const wind = windSpeed === '--' && !windDirection ? null : joinAvailable(windDirection, windSpeed === '--' ? null : windSpeed);
  const humidity = Number.isFinite(weather?.relativeHumidityPercent) ? `hum ${weather.relativeHumidityPercent.toFixed(0)}%` : null;
  const fog = Number.isFinite(weather?.fogLevelPercent) ? `fog ${weather.fogLevelPercent.toFixed(0)}%` : null;
  return joinAvailable(wind, humidity, fog);
}

function cardinalDirection(radians) {
  if (!Number.isFinite(radians)) return null;
  let degrees = radians * 180 / Math.PI;
  degrees %= 360;
  if (degrees < 0) degrees += 360;
  const directions = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  return directions[Math.round(degrees / 45) % directions.length];
}

function pitServiceStatusText(status) {
  switch (status) {
    case null:
    case undefined:
      return '--';
    case 0:
      return 'none';
    case 1:
      return 'in progress';
    case 2:
      return 'complete';
    case 100:
      return 'too far left';
    case 101:
      return 'too far right';
    case 102:
      return 'too far forward';
    case 103:
      return 'too far back';
    case 104:
      return 'bad angle';
    case 105:
      return 'cannot repair';
    default:
      return Number.isInteger(status) ? `status ${status}` : '--';
  }
}

function pitReleaseState(pit) {
  if (Number.isInteger(pit?.pitServiceStatus) && pit.pitServiceStatus >= 100) {
    return { kind: 'hold', value: `RED - ${pitServiceStatusText(pit.pitServiceStatus)}`, tone: 'error' };
  }
  if (pit?.pitServiceStatus === 2) {
    return { kind: 'go', value: 'GREEN - go', tone: 'success' };
  }
  if (pit?.pitstopActive || pit?.pitServiceStatus === 1) {
    return { kind: 'hold', value: 'RED - service active', tone: 'error' };
  }
  if (hasRequiredRepair(pit)) {
    return { kind: 'hold', value: 'RED - repair active', tone: 'error' };
  }
  if (hasOptionalRepair(pit)) {
    return { kind: 'advisory', value: 'YELLOW - optional repair', tone: 'warning' };
  }
  if (pit?.playerCarInPitStall) {
    return {
      kind: 'go',
      value: pit?.pitServiceStatus == null ? 'GREEN - go (inferred)' : 'GREEN - go',
      tone: 'success'
    };
  }
  if (pit?.onPitRoad || pit?.teamOnPitRoad === true) {
    return { kind: 'pending', value: 'pit road', tone: 'info' };
  }
  if (hasRequestedService(pit)) {
    return { kind: 'pending', value: 'armed', tone: 'info' };
  }
  return { kind: 'pending', value: '--', tone: 'normal' };
}

function pitStatus(pit, release) {
  if (Number.isInteger(pit?.pitServiceStatus) && pit.pitServiceStatus >= 100) return 'pit stall error';
  if (release?.kind === 'go') return 'release ready';
  if (release?.kind === 'advisory') return 'optional repair';
  if (release?.kind === 'hold') return 'hold';
  if (pit?.playerCarInPitStall) return 'in pit stall';
  if (pit?.pitstopActive) return 'service active';
  if (pit?.onPitRoad || pit?.teamOnPitRoad === true) return 'on pit road';
  return hasRequestedService(pit) ? 'service requested' : 'pit ready';
}

function pitServiceActivityTone(pit, release) {
  if (Number.isInteger(pit?.pitServiceStatus) && pit.pitServiceStatus >= 100) return 'error';
  if (pit?.pitServiceStatus === 2 || release?.kind === 'go') return 'success';
  if (pit?.pitstopActive || pit?.pitServiceStatus === 1) return 'warning';
  if (hasRequiredRepair(pit)) return 'error';
  if (hasOptionalRepair(pit)) return 'warning';
  return hasRequestedService(pit) ? 'success' : 'normal';
}

function pitFuelRequest(pit) {
  const requested = Number.isInteger(pit?.pitServiceFlags) && (pit.pitServiceFlags & 0x10) !== 0
    || Number.isFinite(pit?.pitServiceFuelLiters) && pit.pitServiceFuelLiters > 0;
  const amount = Number.isFinite(pit?.pitServiceFuelLiters) && pit.pitServiceFuelLiters > 0
    ? formatLiters(pit.pitServiceFuelLiters)
    : null;
  const value = joinAvailable(requested ? 'requested' : null, amount);
  if (value !== '--') return value;
  return Number.isInteger(pit?.pitServiceFlags) ? 'none' : '--';
}

function pitTearoff(pit) {
  if (Number.isInteger(pit?.pitServiceFlags) && (pit.pitServiceFlags & 0x20) !== 0) {
    return 'requested';
  }
  return Number.isInteger(pit?.pitServiceFlags) ? 'none' : '--';
}

function tireServiceCount(flags) {
  return Number.isInteger(flags) ? countBits(flags & 0x0f) : 0;
}

function countBits(value) {
  let count = 0;
  let remaining = value;
  while (remaining > 0) {
    count += remaining & 1;
    remaining >>= 1;
  }
  return count;
}

function pitRepair(pit) {
  return joinAvailable(
    Number.isFinite(pit?.pitRepairLeftSeconds) && pit.pitRepairLeftSeconds > 0 ? `${pit.pitRepairLeftSeconds.toFixed(0)}s required` : null,
    Number.isFinite(pit?.pitOptRepairLeftSeconds) && pit.pitOptRepairLeftSeconds > 0 ? `${pit.pitOptRepairLeftSeconds.toFixed(0)}s optional` : null);
}

function pitRepairTone(pit) {
  if (hasRequiredRepair(pit)) return 'error';
  return hasOptionalRepair(pit) ? 'warning' : 'normal';
}

function pitFastRepair(pit) {
  const selected = Number.isInteger(pit?.pitServiceFlags) && (pit.pitServiceFlags & 0x40) !== 0 ? 'selected' : null;
  const available = Number.isInteger(pit?.fastRepairAvailable) && pit.fastRepairAvailable >= 0
    ? `available ${pit.fastRepairAvailable}`
    : null;
  return joinAvailable(selected, available);
}

function pitFuelRequestSegmentedRow(pit) {
  const requested = pitFuelRequested(pit);
  const selectedLiters = Number.isFinite(pit?.pitServiceFuelLiters) && pit.pitServiceFuelLiters > 0
    ? pit.pitServiceFuelLiters
    : null;
  const selected = Number.isFinite(selectedLiters) ? formatLiters(selectedLiters) : '--';
  return ['Fuel request', pitFuelRequest(pit), 'normal', [
    pitSegment('Requested', requested ? 'Yes' : 'No', requested ? 'success' : 'error'),
    pitSegment('Selected', selected, selected === '--' ? 'waiting' : 'info')
  ]];
}

function pitTearoffSegmentedRow(pit) {
  const requested = pitTearoffRequested(pit);
  return ['Tearoff', pitTearoff(pit), 'normal', [
    pitSegment('Requested', requested ? 'Yes' : 'No', requested ? 'success' : 'error')
  ]];
}

function pitRepairSegmentedRow(pit) {
  const required = Number.isFinite(pit?.pitRepairLeftSeconds) && pit.pitRepairLeftSeconds > 0
    ? `${pit.pitRepairLeftSeconds.toFixed(0)}s`
    : '--';
  const optional = Number.isFinite(pit?.pitOptRepairLeftSeconds) && pit.pitOptRepairLeftSeconds > 0
    ? `${pit.pitOptRepairLeftSeconds.toFixed(0)}s`
    : '--';
  return ['Repair', pitRepair(pit), pitRepairTone(pit), [
    pitSegment('Required', required, required === '--' ? 'success' : 'error'),
    pitSegment('Optional', optional, optional === '--' ? 'success' : 'warning')
  ]];
}

function pitFastRepairSegmentedRow(pit) {
  const selected = pitFastRepairRequested(pit);
  const available = Number.isInteger(pit?.fastRepairAvailable) ? String(pit.fastRepairAvailable) : '--';
  return ['Fast repair', pitFastRepair(pit), 'normal', [
    pitSegment('Selected', selected ? 'Yes' : 'No', selected ? 'success' : 'error'),
    pitSegment('Available', available, Number.isInteger(pit?.fastRepairAvailable) && pit.fastRepairAvailable > 0 ? 'success' : 'warning')
  ]];
}

function pitSignalMetricRow(label, value, tone) {
  return {
    label,
    value: value || '--',
    tone: tone || 'normal',
    rowColorHex: pitSignalColorHex(tone)
  };
}

function pitSignalColorHex(tone) {
  switch (tone) {
    case 'error':
      return '#FF6274';
    case 'warning':
      return '#FFD15B';
    case 'success':
      return '#62FF9F';
    case 'info':
      return '#00E8FF';
    default:
      return null;
  }
}

function pitSegment(label, value, tone = 'normal') {
  return { label, value, tone };
}

function pitFuelRequested(pit) {
  return Boolean(
    Number.isInteger(pit?.pitServiceFlags) && (pit.pitServiceFlags & 0x10) !== 0
    || Number.isFinite(pit?.pitServiceFuelLiters) && pit.pitServiceFuelLiters > 0);
}

function pitTearoffRequested(pit) {
  return Number.isInteger(pit?.pitServiceFlags) && (pit.pitServiceFlags & 0x20) !== 0;
}

function pitFastRepairRequested(pit) {
  return Number.isInteger(pit?.pitServiceFlags) && (pit.pitServiceFlags & 0x40) !== 0;
}

function hasRequiredRepair(pit) {
  return Number.isFinite(pit?.pitRepairLeftSeconds) && pit.pitRepairLeftSeconds > 0;
}

function hasOptionalRepair(pit) {
  return Number.isFinite(pit?.pitOptRepairLeftSeconds) && pit.pitOptRepairLeftSeconds > 0;
}

function hasRequestedService(pit) {
  return (Number.isInteger(pit?.pitServiceFlags) && pit.pitServiceFlags !== 0)
    || (Number.isFinite(pit?.pitServiceFuelLiters) && pit.pitServiceFuelLiters > 0)
    || hasRequiredRepair(pit)
    || hasOptionalRepair(pit);
}

function formatGear(gear) {
  if (gear === -1) return 'R';
  if (gear === 0) return 'N';
  return Number.isInteger(gear) && gear > 0 ? String(gear) : '--';
}

function formatRpm(value) {
  return Number.isFinite(value) ? `${value.toFixed(0)} rpm` : '--';
}

function formatPedals(inputs) {
  if (inputs?.hasPedalInputs === false) return '--';
  return joinAvailable(
    `T ${formatPercent(inputs?.throttle)}`,
    `B ${formatPercent(inputs?.brake)}${inputs?.brakeAbsActive === true ? ' ABS' : ''}`,
    `C ${formatPercent(inputs?.clutch)}`);
}

function formatSteering(radians) {
  return Number.isFinite(radians) ? `${(radians * 180 / Math.PI).toFixed(0)} deg` : '--';
}

function formatWarnings(value) {
  if (!Number.isInteger(value)) return '--';
  return value === 0 ? 'none' : `0x${value.toString(16).toUpperCase()}`;
}

function formatVoltage(value) {
  return Number.isFinite(value) ? `${value.toFixed(1)} V` : '--';
}

function formatOilFuel(inputs) {
  const oilTemp = formatTemp(inputs?.oilTempC);
  const oilPressure = formatPressureBar(inputs?.oilPressureBar);
  const fuelPressure = formatPressureBar(inputs?.fuelPressureBar);
  return joinAvailable(
    oilTemp === '--' ? null : `oil ${oilTemp}`,
    oilPressure === '--' ? null : `oil ${oilPressure}`,
    fuelPressure === '--' ? null : `fuel ${fuelPressure}`);
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

function metricsModel(
  overlayId,
  title,
  status,
  headerItems,
  metrics,
  source = 'source: race-start replay',
  gridSections = [],
  metricSections = []) {
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
    metricSections: metricSections.map(([title, rows]) => ({
      title,
      rows: rows.map(metricModelRow)
    }))
  };
}

function metricModelRow(row) {
  if (!Array.isArray(row)) {
    const modelRow = {
      label: row?.label || '',
      value: row?.value || '--',
      tone: row?.tone || 'normal'
    };
    if (Array.isArray(row?.segments) && row.segments.length > 0) {
      modelRow.segments = row.segments.map(metricModelSegment);
    }
    if (row?.variant) {
      modelRow.variant = row.variant;
    }
    if (row?.rowColorHex) {
      modelRow.rowColorHex = row.rowColorHex;
    }
    if (row?.carClassColorHex) {
      modelRow.carClassColorHex = row.carClassColorHex;
    }

    return modelRow;
  }

  const [label, value, tone, segments] = row;
  const modelRow = { label, value, tone };
  if (Array.isArray(segments) && segments.length > 0) {
    modelRow.segments = segments.map(metricModelSegment);
  }

  return modelRow;
}

function metricModelSegment(segment) {
  return {
    label: segment?.label || '',
    value: segment?.value || '--',
    tone: segment?.tone || 'normal'
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

  if (overlayId === 'gap-to-leader') {
    return gapSettingsModel();
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
    return { ...inputStateReviewSettings };
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

function gapSettingsModel() {
  return {
    carsAhead: gapCarsAhead,
    carsBehind: gapCarsBehind,
    showHeaderStatus: gapShowHeaderStatus,
    showHeaderTimeRemaining: gapShowHeaderTimeRemaining,
    showFooterSource: gapShowFooterSource
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

function finiteNumber(value) {
  return Number.isFinite(value) ? value : null;
}

function roundNumber(value, digits = 3) {
  if (!Number.isFinite(value)) return null;
  const factor = 10 ** digits;
  const rounded = Math.round(value * factor) / factor;
  return Object.is(rounded, -0) ? 0 : rounded;
}

function positiveNumber(value, fallback) {
  const parsed = Number.parseFloat(String(value ?? ''));
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function optionalNumber(value) {
  const parsed = Number.parseFloat(String(value ?? ''));
  return Number.isFinite(parsed) ? parsed : null;
}

function optionalInteger(value) {
  const parsed = Number.parseInt(String(value ?? ''), 10);
  return Number.isInteger(parsed) ? parsed : null;
}

function normalizeReplayTimingMode(value) {
  const normalized = String(value || '').trim().toLowerCase();
  if (['source', 'source-elapsed', 'elapsed'].includes(normalized)) {
    return 'source-elapsed';
  }
  if (['fixed', 'fixed-frame', 'frame', 'compressed'].includes(normalized)) {
    return 'fixed-frame';
  }
  return 'source-elapsed';
}

function formatCadenceSummary(cadence) {
  const delta = cadence?.sourceSessionDeltaSeconds || {};
  const max = Number.isFinite(delta.max) ? `${delta.max}s max` : 'max unknown';
  const median = Number.isFinite(delta.median) ? `${delta.median}s median` : 'median unknown';
  const dense = cadence?.denseForGapToLeader === true ? 'dense' : 'sparse';
  return `${dense} (${median}, ${max}, threshold ${gapMissingSegmentThresholdSeconds}s)`;
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

function normalizeUnitSystem(value) {
  return String(value || '').trim().toLowerCase() === 'imperial'
    ? 'Imperial'
    : 'Metric';
}

function isImperial() {
  return reviewUnitSystem === 'Imperial';
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
