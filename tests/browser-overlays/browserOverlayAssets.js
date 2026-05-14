import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export const browserAssetRoot = resolve(repoRoot, 'src/TmrOverlay.App/Overlays/BrowserSources/Assets');

export const pages = {
  standings: pageDefinition('standings', 'Standings', '/overlays/standings', {
    fadeWhenTelemetryUnavailable: true,
    modelRoute: '/api/overlay-model/standings',
    settingsRoute: '/api/standings',
    settingsProperty: 'standingsSettings'
  }),
  relative: pageDefinition('relative', 'Relative', '/overlays/relative', {
    fadeWhenTelemetryUnavailable: true,
    modelRoute: '/api/overlay-model/relative',
    settingsRoute: '/api/relative',
    settingsProperty: 'relativeSettings'
  }),
  'fuel-calculator': pageDefinition('fuel-calculator', 'Fuel Calculator', '/overlays/fuel-calculator', {
    aliases: ['/overlays/calculator'],
    fadeWhenTelemetryUnavailable: true,
    modelRoute: '/api/overlay-model/fuel-calculator'
  }),
  'session-weather': pageDefinition('session-weather', 'Session / Weather', '/overlays/session-weather', {
    fadeWhenTelemetryUnavailable: true,
    modelRoute: '/api/overlay-model/session-weather'
  }),
  'pit-service': pageDefinition('pit-service', 'Pit Service', '/overlays/pit-service', {
    fadeWhenTelemetryUnavailable: true,
    modelRoute: '/api/overlay-model/pit-service'
  }),
  'input-state': pageDefinition('input-state', 'Inputs', '/overlays/input-state', {
    aliases: ['/overlays/inputs'],
    bodyClass: 'input-state-page',
    renderWhenTelemetryUnavailable: true,
    fadeWhenTelemetryUnavailable: true,
    refreshIntervalMilliseconds: 50,
    modelRoute: '/api/overlay-model/input-state',
    settingsRoute: '/api/input-state',
    settingsProperty: 'inputStateSettings'
  }),
  'car-radar': pageDefinition('car-radar', 'Car Radar', '/overlays/car-radar', {
    bodyClass: 'car-radar-page',
    renderWhenTelemetryUnavailable: true,
    fadeWhenTelemetryUnavailable: true,
    refreshIntervalMilliseconds: 100,
    modelRoute: '/api/overlay-model/car-radar'
  }),
  'gap-to-leader': pageDefinition('gap-to-leader', 'Gap To Leader', '/overlays/gap-to-leader', {
    fadeWhenTelemetryUnavailable: true,
    modelRoute: '/api/overlay-model/gap-to-leader'
  }),
  'track-map': pageDefinition('track-map', 'Track Map', '/overlays/track-map', {
    bodyClass: 'track-map-page',
    renderWhenTelemetryUnavailable: true,
    fadeWhenTelemetryUnavailable: true,
    refreshIntervalMilliseconds: 100,
    modelRoute: '/api/overlay-model/track-map',
    settingsRoute: '/api/track-map',
    settingsProperty: 'trackMapSettings'
  }),
  'garage-cover': pageDefinition('garage-cover', 'Garage Cover', '/overlays/garage-cover', {
    bodyClass: 'garage-cover-page',
    renderWhenTelemetryUnavailable: true,
    fadeWhenTelemetryUnavailable: false,
    modelRoute: '/api/overlay-model/garage-cover',
    settingsRoute: '/api/garage-cover',
    settingsProperty: 'garageCover'
  }),
  'stream-chat': pageDefinition('stream-chat', 'Stream Chat', '/overlays/stream-chat', {
    requiresTelemetry: false,
    modelRoute: '/api/overlay-model/stream-chat',
    settingsRoute: '/api/stream-chat',
    settingsProperty: 'streamChat'
  })
};

export function browserOverlayPages() {
  return Object.values(pages);
}

export function browserOverlayPage(nameOrRoute) {
  const normalized = normalizeRoute(nameOrRoute);
  const page = pages[nameOrRoute]
    || Object.values(pages).find((candidate) =>
      candidate.page.id === nameOrRoute
      || candidate.route === normalized
      || candidate.aliases.includes(normalized));
  if (!page) {
    throw new Error(`Unknown browser overlay page: ${nameOrRoute}`);
  }

  return page;
}

export function renderOverlayIndexHtml(port = 8765) {
  const links = [
    '<a href="/review/app">Application Validator</a>',
    '<a href="/review/settings/general">Settings - General</a>',
    ...Object.values(pages)
    .map((page) => `<a href="${page.route}">${page.title}</a>`)
  ].join('\n');
  return assetText('templates/index.html')
    .replace('{{PORT}}', String(port))
    .replace('{{LINKS}}', links)
    .replace('{{INDEX_CSS}}', assetText('styles/index.css'));
}

export function renderSettingsGeneralReviewHtml({ previewMode = 'off' } = {}) {
  return renderSettingsReviewHtml({
    previewMode,
    appActive: false,
    generalActive: true,
    pageTitle: 'General',
    pageSubtitle: 'Shared units and preview tools.',
    previewTitle: 'Show Preview',
    previewLabel: 'Automatic application overlay preview stage'
  });
}

export function renderAppValidatorReviewHtml({ previewMode = 'off' } = {}) {
  return renderSettingsReviewHtml({
    previewMode,
    appActive: true,
    generalActive: false,
    pageTitle: 'Application Validator',
    pageSubtitle: 'Settings shell and live overlay review.',
    previewTitle: 'Live Application Preview',
    previewLabel: 'Full application overlay validator stage'
  });
}

function renderSettingsReviewHtml({
  previewMode = 'off',
  appActive,
  generalActive,
  pageTitle,
  pageSubtitle,
  previewTitle,
  previewLabel
}) {
  const settingsCss = assetText('styles/settings-general.css')
    .replace('{{THEME_CSS_VARIABLES}}', themeCssVariables());

  return assetText('templates/settings-general.html')
    .replace('{{PREVIEW_MODE}}', normalizePreviewMode(previewMode))
    .replace('{{APP_ACTIVE}}', appActive ? 'active' : '')
    .replace('{{GENERAL_ACTIVE}}', generalActive ? 'active' : '')
    .replace('{{PAGE_TITLE}}', pageTitle)
    .replace('{{PAGE_SUBTITLE}}', pageSubtitle)
    .replace('{{PREVIEW_TITLE}}', previewTitle)
    .replace('{{PREVIEW_LABEL}}', previewLabel)
    .replace('{{SETTINGS_CSS}}', settingsCss);
}

export function renderOverlayHtml(name) {
  const page = browserOverlayPage(name);
  const overlayCss = assetText('styles/overlay.css')
    .replace('{{THEME_CSS_VARIABLES}}', themeCssVariables());
  const overlayScript = assetText('scripts/overlay-shell.js')
    .replace('{{PAGE_JSON}}', JSON.stringify(page.page))
    .replace('{{MODULE_SCRIPT}}', assetText(`modules/${page.module}.js`));

  return assetText('templates/overlay.html')
    .replaceAll('{{TITLE}}', page.title)
    .replace('{{BODY_CLASS}}', page.bodyClass)
    .replace('{{OVERLAY_CSS}}', overlayCss)
    .replace('{{OVERLAY_SCRIPT}}', overlayScript);
}

export function browserOverlayApiResponse(name, path, { live, settings = {}, model = null }) {
  const page = browserOverlayPage(name);
  if (page.modelRoute && path === page.modelRoute) {
    return { model: model ?? defaultDisplayModel(page, live, settings) };
  }
  if (path === page.settingsRoute) {
    if (page.page.id === 'track-map') {
      return {
        trackMap: settings.trackMap ?? null,
        trackMapSettings: settings.trackMapSettings ?? settings
      };
    }

    return { [page.settingsProperty]: settings };
  }
  if (path === '/api/snapshot') {
    return { live };
  }

  return null;
}

export function freshLiveSnapshot(models) {
  return {
    isConnected: true,
    isCollecting: true,
    lastUpdatedAtUtc: new Date().toISOString(),
    sequence: 100,
    models
  };
}

export function assetText(relativePath) {
  return readFileSync(resolve(browserAssetRoot, relativePath), 'utf8');
}

function pageDefinition(id, title, route, options = {}) {
  return {
    title,
    route,
    aliases: (options.aliases || []).map(normalizeRoute),
    bodyClass: options.bodyClass || '',
    module: id,
    modelRoute: options.modelRoute ?? null,
    settingsRoute: options.settingsRoute ?? null,
    settingsProperty: options.settingsProperty ?? null,
    page: {
      id,
      title,
      requiresTelemetry: options.requiresTelemetry ?? true,
      renderWhenTelemetryUnavailable: options.renderWhenTelemetryUnavailable ?? false,
      fadeWhenTelemetryUnavailable: options.fadeWhenTelemetryUnavailable ?? false,
      refreshIntervalMilliseconds: options.refreshIntervalMilliseconds ?? 250,
      forwardQueryParameters: options.forwardQueryParameters ?? ['preview', 'frame', 'rel', 'spoofFocus', 'focus', 'pitService', 'sourceStart', 'sourceEnd', 'frameStart', 'frameEnd', 'replaySpeed']
    }
  };
}

function emptyDisplayModel(overlayId, title) {
  return {
    overlayId,
    title,
    status: 'waiting',
    source: 'source: waiting',
    bodyKind: 'table',
    columns: [],
    rows: [],
    metrics: []
  };
}

function defaultDisplayModel(page, live, settings) {
  switch (page.page.id) {
    case 'car-radar':
      return carRadarDisplayModel(page, live);
    case 'track-map':
      return trackMapDisplayModel(page, live, settings);
    case 'garage-cover':
      return garageCoverDisplayModel(page, live, settings);
    case 'stream-chat':
      return streamChatDisplayModel(page, settings);
    case 'input-state':
      return inputStateDisplayModel(page, live, settings);
    default:
      return emptyDisplayModel(page.page.id, page.title);
  }
}

function inputStateDisplayModel(page, live, settings) {
  const inputs = live?.models?.inputs || {};
  const unitSystem = normalizeUnitSystem(settings?.unitSystem ?? settings?.general?.unitSystem);
  const inCar = isPlayerInCar(live);
  const isAvailable = inCar && inputs.hasData === true;
  const brakeAbsActive = inputs.brakeAbsActive === true;
  const gearText = formatInputGear(inputs.gear);
  const showThrottleTrace = settings.showThrottleTrace ?? true;
  const showBrakeTrace = settings.showBrakeTrace ?? true;
  const showClutchTrace = settings.showClutchTrace ?? true;
  const showThrottle = settings.showThrottle ?? true;
  const showBrake = settings.showBrake ?? true;
  const showClutch = settings.showClutch ?? true;
  const showSteering = settings.showSteering ?? true;
  const showGear = settings.showGear ?? true;
  const showSpeed = settings.showSpeed ?? true;
  const hasGraph = showThrottleTrace || showBrakeTrace || showClutchTrace;
  const hasRail = showThrottle || showBrake || showClutch || showSteering || showGear || showSpeed;
  const hasContent = hasGraph || hasRail;
  const status = !inCar
    ? 'waiting for player in car'
    : !inputs.hasData
      ? 'waiting for car telemetry'
      : hasContent
        ? [gearText, brakeAbsActive ? 'ABS' : null].filter(Boolean).join(' | ')
        : 'no input content enabled';
  return {
    ...emptyDisplayModel(page.page.id, page.title),
    status,
    source: '',
    headerItems: [],
    bodyKind: 'inputs',
    inputs: {
      isAvailable,
      throttle: inputs.throttle,
      brake: inputs.brake,
      clutch: inputs.clutch,
      steeringWheelAngle: inputs.steeringWheelAngle,
      speedMetersPerSecond: inputs.speedMetersPerSecond,
      gear: inputs.gear,
      speedText: formatInputSpeed(inputs.speedMetersPerSecond, unitSystem),
      gearText,
      steeringText: Number.isFinite(inputs.steeringWheelAngle)
        ? `${Math.round(inputs.steeringWheelAngle * 180 / Math.PI)} deg`
        : '--',
      brakeAbsActive,
      showThrottleTrace,
      showBrakeTrace,
      showClutchTrace,
      showThrottle,
      showBrake,
      showClutch,
      showSteering,
      showGear,
      showSpeed,
      hasGraph,
      hasRail,
      hasContent,
      sampleIntervalMilliseconds: 50,
      maximumTracePoints: 180,
      trace: isAvailable
        ? Array.isArray(inputs.trace)
          ? inputs.trace
          : [{
            throttle: clamp01(inputs.throttle),
            brake: clamp01(inputs.brake),
            clutch: clamp01(inputs.clutch),
            brakeAbsActive
          }]
        : []
    }
  };
}

function normalizeUnitSystem(value) {
  return String(value || '').trim().toLowerCase() === 'imperial'
    ? 'Imperial'
    : 'Metric';
}

function formatInputSpeed(metersPerSecond, unitSystem) {
  if (!Number.isFinite(metersPerSecond)) return '--';
  return unitSystem === 'Imperial'
    ? `${Math.round(metersPerSecond * 2.2369362921)} mph`
    : `${Math.round(metersPerSecond * 3.6)} km/h`;
}

function formatInputGear(value) {
  if (value === -1) return 'R';
  if (value === 0) return 'N';
  return Number.isFinite(value) ? String(value) : '--';
}

function clamp01(value) {
  return Number.isFinite(value) ? Math.max(0, Math.min(1, value)) : 0;
}

function carRadarDisplayModel(page, live) {
  const spatial = live?.models?.spatial || {};
  const inCar = isPlayerInCar(live);
  const strongestMulticlassApproach = carRadarMulticlassApproach(spatial);
  const hasCurrentSignal = Boolean(
    spatial.hasCarLeft
    || spatial.hasCarRight
    || strongestMulticlassApproach
    || spatial.cars?.length);
  const status = !inCar
    ? 'waiting for player in car'
    : spatial.hasData === false
      ? 'waiting for radar'
      : spatial.hasCarLeft && spatial.hasCarRight
        ? 'cars both sides'
        : spatial.hasCarLeft
        ? 'car left'
        : spatial.hasCarRight
          ? 'car right'
            : strongestMulticlassApproach
              ? 'faster class'
              : 'clear';
  return {
    ...emptyDisplayModel(page.page.id, page.title),
    status,
    source: inCar && spatial.hasData !== false ? 'source: spatial telemetry' : 'source: waiting',
    bodyKind: 'car-radar',
    carRadar: {
      isAvailable: inCar,
      hasCarLeft: spatial.hasCarLeft === true,
      hasCarRight: spatial.hasCarRight === true,
      cars: spatial.cars || [],
      strongestMulticlassApproach,
      showMulticlassWarning: true,
      previewVisible: false,
      hasCurrentSignal,
      renderModel: carRadarRenderModelFromState({
        isAvailable: inCar,
        hasCarLeft: spatial.hasCarLeft === true,
        hasCarRight: spatial.hasCarRight === true,
        cars: spatial.cars || [],
        strongestMulticlassApproach,
        showMulticlassWarning: true,
        previewVisible: false,
        hasCurrentSignal,
        referenceCarClassColorHex: spatial.referenceCarClassColorHex
      })
    }
  };
}

function carRadarMulticlassApproach(spatial) {
  const approaches = Array.isArray(spatial?.multiclassApproaches)
    ? spatial.multiclassApproaches
    : spatial?.strongestMulticlassApproach
      ? [spatial.strongestMulticlassApproach]
      : [];
  return approaches
    .filter(isInCarRadarMulticlassWarningRange)
    .sort((left, right) =>
      Math.abs(left?.relativeSeconds ?? Number.POSITIVE_INFINITY)
        - Math.abs(right?.relativeSeconds ?? Number.POSITIVE_INFINITY)
      || Number(right?.urgency || 0) - Number(left?.urgency || 0))[0] || null;
}

function isInCarRadarMulticlassWarningRange(approach) {
  const seconds = approach?.relativeSeconds;
  return Number.isFinite(seconds) && seconds < -2 && seconds >= -5;
}

export function carRadarRenderModelFromState({
  isAvailable = false,
  hasCarLeft = false,
  hasCarRight = false,
  cars = [],
  strongestMulticlassApproach = null,
  showMulticlassWarning = true,
  previewVisible = false,
  hasCurrentSignal = false,
  referenceCarClassColorHex = null
} = {}) {
  const shouldRender = (isAvailable && hasCurrentSignal) || previewVisible;
  const empty = () => ({
    shouldRender: false,
    width: 300,
    height: 300,
    fadeInMilliseconds: 250,
    fadeOutMilliseconds: 850,
    minimumVisibleAlpha: 0.02,
    background: radarBackground(),
    rings: [],
    cars: [],
    labels: [],
    multiclassArc: null
  });
  if (!shouldRender) return empty();

  const currentCars = uniqueRadarCars((Array.isArray(cars) ? cars : []).filter(isInRadarRange));
  const sideAttachments = sideWarningAttachments(hasCarLeft, hasCarRight, currentCars);
  const renderCars = [
    ...radarCarPlacements(currentCars, sideAttachments).map(nearbyCarRectangle),
    ...sideWarningRectangles(hasCarLeft, hasCarRight, sideAttachments),
    playerCarRectangle(referenceCarClassColorHex)
  ];
  const rings = [distanceRing(1), distanceRing(2)];
  const labels = rings.map((ring) => ring.label).filter(Boolean);
  const multiclassArc = showMulticlassWarning && strongestMulticlassApproach
    ? multiclassApproachArc(strongestMulticlassApproach)
    : null;
  if (multiclassArc?.label) labels.push(multiclassArc.label);

  return {
    shouldRender: true,
    width: 300,
    height: 300,
    fadeInMilliseconds: 250,
    fadeOutMilliseconds: 850,
    minimumVisibleAlpha: 0.02,
    background: radarBackground(),
    rings,
    cars: renderCars,
    labels,
    multiclassArc
  };
}

const radarConstants = {
  radarInset: 4,
  radarDiameter: 292,
  radarCenter: 150,
  focusedCarLengthMeters: 4.746,
  contactWindowMeters: 4.746,
  proximityWarningGapMeters: 2,
  proximityRedStart: 0.74,
  timingAwareVisibilitySeconds: 2,
  maximumTimingAwareRangeMultiplier: 15,
  timingAwareEdgeOpacity: 0.2,
  maxWideRowRadarCars: 18,
  focusedCarWidth: 20,
  focusedCarHeight: 36,
  radarCarWidth: 20,
  radarCarHeight: 36,
  carCornerRadius: 4,
  separatedCarPaddingPixels: 2,
  radarEdgeCenterPaddingPixels: 2,
  gridRowReferenceMeters: 8,
  minimumDistinctRowGapPixels: 48,
  wideRowBucketPixels: 30,
  wideRowLongitudinalBucketMeters: 2,
  wideRowSlotPitchPixels: 36,
  multiclassWarningArcStartDegrees: 62.5,
  multiclassWarningArcSweepDegrees: 55
};
radarConstants.radarRadius = radarConstants.radarDiameter / 2;
radarConstants.radarRangeMeters = radarConstants.focusedCarLengthMeters * 6;
radarConstants.maximumTimingAwareRangeMeters = radarConstants.focusedCarLengthMeters * radarConstants.maximumTimingAwareRangeMultiplier;
radarConstants.sideAttachmentWindowMeters = radarConstants.focusedCarLengthMeters * 2;
radarConstants.usableRadarRadius = radarConstants.radarRadius - radarConstants.radarEdgeCenterPaddingPixels;
radarConstants.distinctRowPixelsPerMeter = radarConstants.minimumDistinctRowGapPixels / radarConstants.gridRowReferenceMeters;

function radarBackground() {
  return {
    x: radarConstants.radarInset,
    y: radarConstants.radarInset,
    width: radarConstants.radarDiameter,
    height: radarConstants.radarDiameter,
    fill: rgba(12, 18, 22, 82),
    stroke: rgba(0, 232, 255, 88),
    strokeWidth: 1.2,
    label: null
  };
}

function distanceRing(index) {
  const inset = radarConstants.radarDiameter * index / 6;
  const radius = radarConstants.radarDiameter / 2 - inset;
  return {
    x: radarConstants.radarInset + inset,
    y: radarConstants.radarInset + inset,
    width: radarConstants.radarDiameter - inset * 2,
    height: radarConstants.radarDiameter - inset * 2,
    fill: null,
    stroke: rgba(255, 255, 255, 40),
    strokeWidth: 1,
    label: {
      text: formatRingDistance(radius),
      x: radarConstants.radarCenter + radius * 0.35,
      y: radarConstants.radarCenter - radius - 8,
      width: 58,
      height: 16,
      fontSize: 7.5,
      bold: false,
      alignment: 'near',
      color: rgba(220, 230, 236, 118)
    }
  };
}

function formatRingDistance(offsetPixels) {
  return `${Math.round(distanceForLongitudinalOffset(offsetPixels))}m`;
}

function distanceForLongitudinalOffset(offsetPixels) {
  const separatedCenterOffset = Math.min(
    radarConstants.usableRadarRadius,
    radarConstants.focusedCarHeight / 2 + radarConstants.radarCarHeight / 2 + radarConstants.separatedCarPaddingPixels);
  const absOffset = Math.max(0, Math.min(radarConstants.usableRadarRadius, Math.abs(offsetPixels)));
  if (absOffset <= separatedCenterOffset) {
    return radarConstants.contactWindowMeters * absOffset / Math.max(0.001, separatedCenterOffset);
  }

  return radarConstants.contactWindowMeters
    + (absOffset - separatedCenterOffset) / radarConstants.distinctRowPixelsPerMeter;
}

function uniqueRadarCars(cars) {
  const byCar = new Map();
  for (const car of cars) {
    const current = byCar.get(car.carIdx);
    if (!current || Math.abs(rangeRatio(car)) < Math.abs(rangeRatio(current))) {
      byCar.set(car.carIdx, car);
    }
  }
  return [...byCar.values()];
}

function sideWarningAttachments(hasCarLeft, hasCarRight, cars) {
  const used = new Set();
  const left = hasCarLeft ? selectSideAttachment(cars, used) : null;
  if (left) used.add(left.carIdx);
  const right = hasCarRight ? selectSideAttachment(cars, used) : null;
  return { left, right };
}

function selectSideAttachment(cars, used) {
  return cars
    .filter((car) => !used.has(car.carIdx) && isSideAttachmentCandidate(car))
    .sort((a, b) => Math.abs(rangeRatio(a)) - Math.abs(rangeRatio(b)) || a.carIdx - b.carIdx)[0] || null;
}

function isSideAttachmentCandidate(car) {
  return isInRadarRange(car)
    && Number.isFinite(car.relativeMeters)
    && Math.abs(car.relativeMeters) <= radarConstants.sideAttachmentWindowMeters;
}

function radarCarPlacements(cars, sideAttachments) {
  const usableRadius = radarConstants.usableRadarRadius;
  const visibleCars = cars
    .filter((car) => car.carIdx !== sideAttachments.left?.carIdx && car.carIdx !== sideAttachments.right?.carIdx)
    .sort((a, b) => Math.abs(rangeRatio(a)) - Math.abs(rangeRatio(b)))
    .slice(0, radarConstants.maxWideRowRadarCars);
  const candidates = visibleCars.map((car, index) => {
    const offset = longitudinalOffset(car, usableRadius);
    return {
      car,
      sourceIndex: index,
      idealOffset: offset,
      longitudinalMeters: Number.isFinite(car.relativeMeters) ? car.relativeMeters : null,
      direction: placementDirection(car, index, offset)
    };
  });
  const rows = [];
  for (const candidate of candidates.sort((a, b) => a.idealOffset - b.idealOffset)) {
    const row = rows.find((entry) => isSameWideRadarRow(entry, candidate));
    if (row) {
      row.candidates.push(candidate);
    } else {
      rows.push({
        anchorOffset: candidate.idealOffset,
        anchorMeters: candidate.longitudinalMeters,
        direction: candidate.direction,
        candidates: [candidate]
      });
    }
  }
  return rows.flatMap((row) => placementsForRow(row, usableRadius));
}

function isSameWideRadarRow(row, candidate) {
  if (Math.abs(row.direction - candidate.direction) > 0.001) return false;
  if (Number.isFinite(row.anchorMeters) && Number.isFinite(candidate.longitudinalMeters)) {
    return Math.abs(row.anchorMeters - candidate.longitudinalMeters) <= radarConstants.wideRowLongitudinalBucketMeters;
  }
  return Math.abs(row.anchorOffset - candidate.idealOffset) <= radarConstants.wideRowBucketPixels;
}

function placementsForRow(row, usableRadius) {
  if (!row.candidates.length) return [];
  const rowOffset = row.candidates.reduce((sum, candidate) => sum + candidate.idealOffset, 0) / row.candidates.length;
  const clampedRowMagnitude = Math.min(Math.abs(rowOffset), usableRadius);
  const availableHalfWidth = Math.sqrt(Math.max(0, usableRadius * usableRadius - clampedRowMagnitude * clampedRowMagnitude));
  const maxCenterOffset = Math.max(0, availableHalfWidth - radarConstants.radarCarWidth / 2 - 4);
  const minimumSlots = row.candidates.length > 1 ? 2 : 1;
  const maxSlots = Math.max(minimumSlots, Math.floor(maxCenterOffset * 2 / radarConstants.wideRowSlotPitchPixels) + 1);
  const visibleCandidates = row.candidates
    .sort((a, b) => a.sourceIndex - b.sourceIndex || a.car.carIdx - b.car.carIdx)
    .slice(0, maxSlots);
  const xOffsets = focusOverlappingRow(rowOffset)
    ? focusAvoidingXOffsets(visibleCandidates.length, maxCenterOffset)
    : centeredXOffsets(visibleCandidates.length);
  return visibleCandidates.slice(0, xOffsets.length).map((candidate, slotIndex) => {
    return {
      car: candidate.car,
      x: radarConstants.radarCenter + xOffsets[slotIndex] - radarConstants.radarCarWidth / 2,
      y: radarConstants.radarCenter - rowOffset - radarConstants.radarCarHeight / 2,
      offset: rowOffset
    };
  });
}

function centeredXOffsets(count) {
  const lineWidth = radarConstants.wideRowSlotPitchPixels * Math.max(0, count - 1);
  return Array.from({ length: count }, (_, index) => index * radarConstants.wideRowSlotPitchPixels - lineWidth / 2);
}

function focusOverlappingRow(rowOffset) {
  const verticalSeparation = radarConstants.focusedCarHeight / 2 + radarConstants.radarCarHeight / 2 + 4;
  return Math.abs(rowOffset) < verticalSeparation;
}

function focusAvoidingXOffsets(count, maxCenterOffset) {
  const minimumOffset = radarConstants.focusedCarWidth / 2 + radarConstants.radarCarWidth / 2 + 22;
  if (maxCenterOffset < minimumOffset) {
    return centeredXOffsets(count);
  }

  const offsets = [];
  const signs = count === 1 ? [1] : [-1, 1];
  for (let lane = 0; offsets.length < count; lane += 1) {
    for (const sign of signs) {
      const offset = sign * (minimumOffset + lane * radarConstants.wideRowSlotPitchPixels);
      if (Math.abs(offset) <= maxCenterOffset) {
        offsets.push(offset);
      }
      if (offsets.length >= count) break;
    }
    if (minimumOffset + lane * radarConstants.wideRowSlotPitchPixels > maxCenterOffset + radarConstants.wideRowSlotPitchPixels) break;
  }
  return offsets.length ? offsets : centeredXOffsets(count);
}

function nearbyCarRectangle(placement) {
  const visualAlpha = radarEntryOpacity(placement.car);
  return {
    kind: 'nearby',
    carIdx: placement.car.carIdx,
    x: placement.x,
    y: placement.y,
    width: radarConstants.radarCarWidth,
    height: radarConstants.radarCarHeight,
    radius: radarConstants.carCornerRadius,
    fill: proximityColor(proximityTint(placement.car), visualAlpha),
    stroke: classBorderColor(placement.car.carClassColorHex, visualAlpha),
    strokeWidth: 2
  };
}

function sideWarningRectangles(hasCarLeft, hasCarRight, sideAttachments) {
  const rectangles = [];
  const usableRadius = radarConstants.usableRadarRadius;
  if (hasCarLeft) {
    rectangles.push(sideWarningRectangle('left', radarConstants.radarCenter - 42, sideWarningCenterY(usableRadius, sideAttachments.left), sideAttachments.left));
  }
  if (hasCarRight) {
    rectangles.push(sideWarningRectangle('right', radarConstants.radarCenter + 42, sideWarningCenterY(usableRadius, sideAttachments.right), sideAttachments.right));
  }
  return rectangles;
}

function sideWarningRectangle(side, centerX, centerY, attachment) {
  const fillAlpha = attachment ? 245 : 238;
  return {
    kind: `side-${side}`,
    carIdx: attachment?.carIdx ?? null,
    x: centerX - radarConstants.radarCarWidth / 2,
    y: centerY - radarConstants.radarCarHeight / 2,
    width: radarConstants.radarCarWidth,
    height: radarConstants.radarCarHeight,
    radius: radarConstants.carCornerRadius,
    fill: rgba(236, 112, 99, fillAlpha),
    stroke: classBorderColor(attachment?.carClassColorHex, fillAlpha / 255),
    strokeWidth: 2
  };
}

function sideWarningCenterY(usableRadius, car) {
  if (!car) return radarConstants.radarCenter;
  const maximumBias = radarConstants.focusedCarHeight * 0.55;
  const offset = Math.max(-maximumBias, Math.min(maximumBias, longitudinalOffset(car, usableRadius)));
  return radarConstants.radarCenter - offset;
}

function playerCarRectangle(referenceCarClassColorHex) {
  return {
    kind: 'focus',
    carIdx: null,
    x: radarConstants.radarCenter - radarConstants.focusedCarWidth / 2,
    y: radarConstants.radarCenter - radarConstants.focusedCarHeight / 2,
    width: radarConstants.focusedCarWidth,
    height: radarConstants.focusedCarHeight,
    radius: radarConstants.carCornerRadius,
    fill: rgba(255, 255, 255, 240),
    stroke: classBorderColor(referenceCarClassColorHex, 1),
    strokeWidth: 2
  };
}

function multiclassApproachArc(approach) {
  const urgency = Math.max(0, Math.min(1, Number.isFinite(approach.urgency) ? approach.urgency : 0));
  const alpha = Math.round(120 + urgency * 110);
  return {
    x: radarConstants.radarInset + 4,
    y: radarConstants.radarInset + 4,
    width: radarConstants.radarDiameter - 8,
    height: radarConstants.radarDiameter - 8,
    startDegrees: radarConstants.multiclassWarningArcStartDegrees,
    sweepDegrees: radarConstants.multiclassWarningArcSweepDegrees,
    strokeWidth: 5,
    stroke: rgba(236, 112, 99, alpha),
    label: {
      text: Number.isFinite(approach.relativeSeconds)
        ? `Faster class approaching ${Math.abs(approach.relativeSeconds).toFixed(1)}s`
        : 'Faster class approaching',
      x: radarConstants.radarInset + 28,
      y: radarConstants.radarInset + radarConstants.radarDiameter - 48,
      width: radarConstants.radarDiameter - 56,
      height: 18,
      fontSize: 9,
      bold: true,
      alignment: 'center',
      color: rgba(255, 225, 220, alpha)
    }
  };
}

function longitudinalOffset(car, usableRadius) {
  if (Number.isFinite(car.relativeMeters)) {
    return longitudinalOffsetFromDistance(placementMeters(car.relativeMeters), usableRadius);
  }
  return Math.sign(car.relativeLaps || 0) * usableRadius;
}

function longitudinalOffsetFromDistance(meters, usableRadius) {
  const sign = Math.sign(meters);
  if (sign === 0) return 0;
  const absMeters = Math.abs(meters);
  const separatedCenterOffset = Math.min(
    usableRadius,
    radarConstants.focusedCarHeight / 2 + radarConstants.radarCarHeight / 2 + radarConstants.separatedCarPaddingPixels);
  if (absMeters <= radarConstants.contactWindowMeters) {
    return sign * (absMeters / radarConstants.contactWindowMeters) * separatedCenterOffset;
  }
  const rowAwareOffset = separatedCenterOffset
    + (absMeters - radarConstants.contactWindowMeters) * radarConstants.distinctRowPixelsPerMeter;
  return sign * rowAwareOffset;
}

function placementDirection(car, index, idealOffset) {
  if (idealOffset < 0) return -1;
  if (idealOffset > 0) return 1;
  if (Math.abs(car.relativeLaps || 0) > 0.0001) return car.relativeLaps < 0 ? -1 : 1;
  return index % 2 === 0 ? 1 : -1;
}

function rangeRatio(car) {
  if (Number.isFinite(car.relativeMeters)) {
    return Math.max(-1, Math.min(1, car.relativeMeters / visualRadarRangeMeters(car)));
  }
  return Math.sign(car.relativeLaps || 0);
}

function isInRadarRange(car) {
  return Number.isFinite(car?.relativeMeters) && Math.abs(car.relativeMeters) <= visualRadarRangeMeters(car);
}

function proximityColor(proximityTintValue, visualAlpha) {
  const normalized = Math.max(0, Math.min(1, proximityTintValue));
  const alpha = scaleAlpha(238, visualAlpha);
  if (normalized <= 0) return rgba(255, 255, 255, alpha);
  if (normalized < radarConstants.proximityRedStart) {
    const yellowMix = smoothStep(0, radarConstants.proximityRedStart, normalized);
    return rgba(lerp(255, 255, yellowMix), lerp(255, 220, yellowMix), lerp(255, 66, yellowMix), alpha);
  }
  const redMix = smoothStep(radarConstants.proximityRedStart, 1, normalized);
  return rgba(lerp(255, 255, redMix), lerp(220, 24, redMix), lerp(66, 16, redMix), alpha);
}

function proximityTint(car) {
  return Number.isFinite(car.relativeMeters) ? bumperGapProximity(Math.abs(car.relativeMeters)) : 0;
}

function radarEntryOpacity(car) {
  if (!Number.isFinite(car.relativeMeters)) return 0;
  const physicalOpacity = opacityBetweenRangeEdgeAndWarningStart(
    Math.abs(car.relativeMeters),
    radarConstants.contactWindowMeters + radarConstants.proximityWarningGapMeters,
    radarConstants.radarRangeMeters);
  const timingAwareOpacity = timingAwareEntryOpacity(
    Math.abs(car.relativeMeters),
    visualRadarRangeMeters(car));
  return Math.max(physicalOpacity, timingAwareOpacity);
}

function timingAwareEntryOpacity(absoluteMeters, visualRangeMeters) {
  if (visualRangeMeters <= radarConstants.radarRangeMeters || absoluteMeters >= visualRangeMeters) return 0;
  if (absoluteMeters <= radarConstants.radarRangeMeters) return radarConstants.timingAwareEdgeOpacity;
  const normalized = 1 - Math.max(
    0,
    Math.min(1, (absoluteMeters - radarConstants.radarRangeMeters) / Math.max(0.001, visualRangeMeters - radarConstants.radarRangeMeters)));
  return radarConstants.timingAwareEdgeOpacity * smoothStep(0, 1, normalized);
}

function placementMeters(meters) {
  return Math.sign(meters) * Math.min(Math.abs(meters), radarConstants.radarRangeMeters);
}

function visualRadarRangeMeters(car) {
  const range = radarConstants.radarRangeMeters;
  if (!Number.isFinite(car?.relativeMeters) || !Number.isFinite(car?.relativeSeconds)) return range;
  const absMeters = Math.abs(car.relativeMeters);
  const absSeconds = Math.abs(car.relativeSeconds);
  if (absMeters <= range || absSeconds <= 0.05) return range;
  const inferredMetersPerSecond = absMeters / absSeconds;
  if (!Number.isFinite(inferredMetersPerSecond) || inferredMetersPerSecond <= 0) return range;
  const timingAwareRange = inferredMetersPerSecond * radarConstants.timingAwareVisibilitySeconds;
  return Math.max(
    range,
    Math.min(radarConstants.maximumTimingAwareRangeMeters, timingAwareRange));
}

function opacityBetweenRangeEdgeAndWarningStart(absoluteValue, warningStart, radarRange) {
  if (absoluteValue <= warningStart) return 1;
  if (radarRange <= warningStart) return absoluteValue <= radarRange ? 1 : 0;
  const normalized = 1 - Math.max(0, Math.min(1, (absoluteValue - warningStart) / (radarRange - warningStart)));
  return smoothStep(0, 1, normalized);
}

function bumperGapProximity(centerDistanceMeters) {
  return 1 - Math.max(0, Math.min(1, (centerDistanceMeters - radarConstants.focusedCarLengthMeters) / radarConstants.proximityWarningGapMeters));
}

function scaleAlpha(alpha, multiplier) {
  return Math.round(Math.max(0, Math.min(255, alpha * multiplier)));
}

function classBorderColor(colorHex, visualAlpha) {
  const parsed = parseHexColor(colorHex);
  const alpha = scaleAlpha(245, visualAlpha);
  return parsed
    ? rgba(parsed.red, parsed.green, parsed.blue, alpha)
    : rgba(255, 255, 255, alpha);
}

function parseHexColor(value) {
  if (typeof value !== 'string') return null;
  const token = value.trim().replace(/^#/, '');
  if (!/^[0-9a-fA-F]{6}$/.test(token)) return null;
  return {
    red: Number.parseInt(token.slice(0, 2), 16),
    green: Number.parseInt(token.slice(2, 4), 16),
    blue: Number.parseInt(token.slice(4, 6), 16)
  };
}

function smoothStep(edge0, edge1, value) {
  const ratio = Math.max(0, Math.min(1, (value - edge0) / (edge1 - edge0)));
  return ratio * ratio * (3 - 2 * ratio);
}

function lerp(start, end, ratio) {
  return Math.round(start + (end - start) * ratio);
}

function rgba(red, green, blue, alpha) {
  return { red, green, blue, alpha };
}

function trackMapDisplayModel(page, live, settings) {
  return {
    ...emptyDisplayModel(page.page.id, page.title),
    status: 'live | track map',
    source: 'source: live position telemetry',
    bodyKind: 'track-map',
    trackMap: {
      markers: trackMapMarkers(live),
      sectors: live?.models?.trackMap?.sectors || [],
      showSectorBoundaries: settings?.trackMapSettings?.showSectorBoundaries ?? settings?.showSectorBoundaries ?? true,
      internalOpacity: settings?.trackMapSettings?.internalOpacity ?? settings?.internalOpacity ?? 0.88,
      includeUserMaps: true,
      renderModel: trackMapRenderModel(live, settings)
    }
  };
}

function garageCoverDisplayModel(page, live, settings) {
  const garageVisible = live?.models?.raceEvents?.isGarageVisible === true;
  const browserSettings = settings?.garageCover || settings || {};
  const previewVisible = browserSettings.previewVisible === true;
  const status = previewVisible ? 'preview visible' : garageVisible ? 'garage visible' : 'garage hidden';
  return {
    ...emptyDisplayModel(page.page.id, page.title),
    status,
    source: 'source: garage telemetry/settings',
    bodyKind: 'garage-cover',
    garageCover: {
      shouldCover: previewVisible || garageVisible,
      browserSettings,
      detection: {
        state: garageVisible ? 'garage_visible' : 'garage_hidden',
        displayText: status,
        isFresh: true
      }
    }
  };
}

function streamChatDisplayModel(page, settings) {
  const streamSettings = settings?.streamChat || settings || {};
  const message = streamSettings.isConfigured
    ? streamSettings.provider === 'twitch'
      ? `Connecting to #${streamSettings.twitchChannel || 'channel'}...`
      : 'Connecting to Streamlabs chat...'
    : streamChatStatusText(streamSettings.status);
  return {
    ...emptyDisplayModel(page.page.id, page.title),
    status: streamSettings.isConfigured ? `connecting | ${streamSettings.provider}` : 'waiting for chat source',
    source: 'source: stream chat settings',
    bodyKind: 'stream-chat',
    streamChat: {
      settings: streamSettings,
      rows: [{ name: 'TMR', text: message, kind: 'system' }]
    }
  };
}

function isPlayerInCar(live) {
  const race = live?.models?.raceEvents || {};
  const reference = live?.models?.reference || {};
  if (reference.focusIsPlayer === false) return false;
  if (Number.isFinite(reference.focusCarIdx)
    && Number.isFinite(reference.playerCarIdx)
    && reference.focusCarIdx !== reference.playerCarIdx) {
    return false;
  }
  if (race.isInGarage === true || race.isGarageVisible === true || reference.isInGarage === true) {
    return false;
  }
  const hasRaceContext = race.hasData === true || reference.hasData === true;
  if (!hasRaceContext) return true;
  return race.isOnTrack === true || reference.isOnTrack === true;
}

function trackMapMarkers(live) {
  const rows = [
    ...(live?.models?.timing?.overallRows || []),
    ...(live?.models?.timing?.classRows || [])
  ];
  const referenceCarIdx = live?.models?.reference?.focusCarIdx
    ?? live?.models?.timing?.focusCarIdx
    ?? live?.latestSample?.focusCarIdx;
  const markers = new Map();
  for (const row of rows) {
    if (row.hasSpatialProgress === false || !Number.isFinite(row.lapDistPct) || row.lapDistPct < 0) continue;
    const isFocus = row.isFocus === true || row.carIdx === referenceCarIdx;
    if (!isFocus && row.hasTakenGrid !== true) continue;
    markers.set(row.carIdx, {
      carIdx: row.carIdx,
      lapDistPct: normalizeProgress(row.lapDistPct),
      isFocus,
      classColorHex: row.carClassColorHex || null,
      position: isFocus ? row.classPosition ?? row.overallPosition ?? null : null
    });
  }

  const latest = live?.latestSample || {};
  if (Number.isFinite(referenceCarIdx)
    && Number.isFinite(latest.focusLapDistPct)
    && latest.focusLapDistPct >= 0
    && latest.onPitRoad !== true
    && latest.playerTrackSurface !== 1
    && latest.playerTrackSurface !== 2) {
    const existing = markers.get(referenceCarIdx);
    const focusRow = live?.models?.timing?.focusRow;
    markers.set(referenceCarIdx, {
      carIdx: referenceCarIdx,
      lapDistPct: normalizeProgress(latest.focusLapDistPct),
      isFocus: true,
      classColorHex: null,
      position: existing?.position
        ?? focusRow?.classPosition
        ?? focusRow?.overallPosition
        ?? latest.focusClassPosition
        ?? latest.focusPosition
        ?? null
    });
  }

  return [...markers.values()].sort((left, right) => Number(left.isFocus) - Number(right.isFocus) || left.carIdx - right.carIdx);
}

function trackMapRenderModel(live, settings) {
  const markers = trackMapMarkers(live);
  const sectors = live?.models?.trackMap?.sectors || [];
  const trackMap = settings?.trackMap ?? null;
  const internalOpacity = settings?.trackMapSettings?.internalOpacity ?? settings?.internalOpacity ?? 0.88;
  const showSectorBoundaries = settings?.trackMapSettings?.showSectorBoundaries ?? settings?.showSectorBoundaries ?? true;
  const primitives = trackMap?.racingLine?.points?.length >= 3
    ? generatedTrackMapPrimitives(trackMap, sectors, internalOpacity, showSectorBoundaries)
    : circleTrackMapPrimitives(sectors, internalOpacity, showSectorBoundaries);
  return {
    width: 360,
    height: 360,
    isAvailable: true,
    mapKind: trackMap?.racingLine?.points?.length >= 3 ? 'generated' : 'circle',
    primitives,
    markers: markers.map((marker) => trackMapRenderMarker(marker, trackMap))
  };
}

function generatedTrackMapPrimitives(trackMap, sectors, internalOpacity, showSectorBoundaries) {
  const transform = trackMapTransform(trackMap);
  if (!transform) return circleTrackMapPrimitives(sectors, internalOpacity, showSectorBoundaries);
  const racing = renderPath(trackMap.racingLine, transform);
  const primitives = [];
  if (trackMap.racingLine?.closed === true && racing.length >= 3) {
    primitives.push(primitivePath(racing, true, trackInteriorFill(internalOpacity), null, 0));
  }
  primitives.push(
    primitivePath(racing, trackMap.racingLine?.closed === true, null, rgba(255, 255, 255, 82), 11),
    primitivePath(racing, trackMap.racingLine?.closed === true, null, rgba(222, 237, 245, 255), 4.4));
  for (const sector of sectors.filter(hasTrackMapHighlight)) {
    for (const range of segmentRanges(sector.startPct, sector.endPct)) {
      const points = renderSegment(trackMap.racingLine, transform, range.startPct, range.endPct);
      if (points.length >= 2) {
        primitives.push(primitivePath(points, false, null, sectorHighlightColor(sector.highlight), 5.8));
      }
    }
  }
  if (showSectorBoundaries) {
    for (const progress of boundaryProgresses(sectors)) {
      const tick = geometryBoundaryTick(trackMap.racingLine, transform, progress);
      if (tick) addBoundaryPrimitives(primitives, tick.start, tick.end, isStartFinish(progress));
    }
  }
  if (trackMap?.pitLane?.points?.length >= 2) {
    primitives.push(primitivePath(renderPath(trackMap.pitLane, transform), false, null, rgba(98, 199, 255, 190), 2.2));
  }
  return primitives;
}

function circleTrackMapPrimitives(sectors, internalOpacity, showSectorBoundaries) {
  const rect = { x: 20, y: 20, width: 320, height: 320 };
  const primitives = [
    primitiveEllipse(rect, trackInteriorFill(internalOpacity), null, 0),
    primitiveEllipse(rect, null, rgba(255, 255, 255, 82), 11),
    primitiveEllipse(rect, null, rgba(222, 237, 245, 255), 4.4)
  ];
  for (const sector of sectors.filter(hasTrackMapHighlight)) {
    for (const range of segmentRanges(sector.startPct, sector.endPct)) {
      primitives.push({
        kind: 'arc',
        points: [],
        closed: false,
        rect,
        startDegrees: range.startPct * 360 - 90,
        sweepDegrees: (range.endPct - range.startPct) * 360,
        fill: null,
        stroke: sectorHighlightColor(sector.highlight),
        strokeWidth: 5.8
      });
    }
  }
  if (showSectorBoundaries) {
    for (const progress of boundaryProgresses(sectors)) {
      const point = pointOnCircle(progress);
      const dx = point.x - 180;
      const dy = point.y - 180;
      const length = Math.max(0.001, Math.hypot(dx, dy));
      const half = boundaryTickLength(progress) / 2;
      addBoundaryPrimitives(
        primitives,
        { x: point.x - dx / length * half, y: point.y - dy / length * half },
        { x: point.x + dx / length * half, y: point.y + dy / length * half },
        isStartFinish(progress));
    }
  }
  return primitives;
}

function trackMapRenderMarker(marker, trackMap) {
  const transform = trackMap?.racingLine?.points?.length >= 3 ? trackMapTransform(trackMap) : null;
  const point = transform ? pointOnGeometry(trackMap.racingLine, transform, marker.lapDistPct) : pointOnCircle(marker.lapDistPct);
  const label = marker.isFocus && Number.isFinite(marker.position) && marker.position > 0 ? String(marker.position) : null;
  return {
    carIdx: marker.carIdx,
    x: point.x,
    y: point.y,
    radius: label ? Math.max(5.7, 5.1 + label.length * 2.9) : marker.isFocus ? 5.7 : 3.6,
    isFocus: marker.isFocus,
    fill: marker.isFocus ? rgba(0, 232, 255, 255) : classBorderColor(marker.classColorHex, 1),
    stroke: rgba(8, 14, 18, 230),
    strokeWidth: marker.isFocus ? 2 : 1.4,
    label,
    labelColor: rgba(5, 13, 17, 255),
    labelFontSize: 7.6
  };
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
  const scale = Math.min(320 / width, 320 / height);
  return {
    minX,
    maxY,
    scale,
    left: 20 + (320 - width * scale) / 2,
    top: 20 + (320 - height * scale) / 2
  };
}

function mapTrackPoint(point, transform) {
  return {
    x: transform.left + (point.x - transform.minX) * transform.scale,
    y: transform.top + (transform.maxY - point.y) * transform.scale
  };
}

function renderPath(geometry, transform) {
  return (geometry?.points || []).map((point) => mapTrackPoint(point, transform));
}

function renderSegment(geometry, transform, startPct, endPct) {
  const start = pointOnGeometry(geometry, transform, startPct);
  const end = pointOnGeometry(geometry, transform, endPct);
  if (!start || !end) return [];
  return [
    start,
    ...(geometry?.points || [])
      .filter((point) => point.lapDistPct > startPct && point.lapDistPct < endPct)
      .map((point) => mapTrackPoint(point, transform)),
    end
  ];
}

function pointOnGeometry(geometry, transform, progress) {
  const points = geometry?.points || [];
  if (!points.length) return pointOnCircle(progress);
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
    const target = pct < previous.lapDistPct ? pct + 1 : pct;
    return interpolateTrackPoint(previous, current, target, transform);
  }
  return mapTrackPoint(points[0], transform);
}

function interpolateTrackPoint(previous, current, target, transform) {
  const span = current.lapDistPct - previous.lapDistPct;
  const ratio = span <= 0 ? 0 : Math.max(0, Math.min(1, (target - previous.lapDistPct) / span));
  return mapTrackPoint({
    x: previous.x + (current.x - previous.x) * ratio,
    y: previous.y + (current.y - previous.y) * ratio
  }, transform);
}

function geometryBoundaryTick(geometry, transform, progress) {
  const center = pointOnGeometry(geometry, transform, progress);
  const before = pointOnGeometry(geometry, transform, progress - 0.002);
  const after = pointOnGeometry(geometry, transform, progress + 0.002);
  const dx = after.x - before.x;
  const dy = after.y - before.y;
  const length = Math.max(0.001, Math.hypot(dx, dy));
  const normalX = -dy / length;
  const normalY = dx / length;
  const half = boundaryTickLength(progress) / 2;
  return {
    start: { x: center.x - normalX * half, y: center.y - normalY * half },
    end: { x: center.x + normalX * half, y: center.y + normalY * half }
  };
}

function pointOnCircle(progress) {
  const angle = normalizeProgress(progress) * Math.PI * 2 - Math.PI / 2;
  return { x: 180 + Math.cos(angle) * 160, y: 180 + Math.sin(angle) * 160 };
}

function primitivePath(points, closed, fill, stroke, strokeWidth) {
  return { kind: 'path', points, closed, rect: null, startDegrees: 0, sweepDegrees: 0, fill, stroke, strokeWidth };
}

function primitiveEllipse(rect, fill, stroke, strokeWidth) {
  return { kind: 'ellipse', points: [], closed: false, rect, startDegrees: 0, sweepDegrees: 0, fill, stroke, strokeWidth };
}

function addBoundaryPrimitives(primitives, start, end, isStartFinishLine) {
  if (isStartFinishLine) {
    primitives.push(primitiveLine(start, end, rgba(5, 9, 14, 210), 5.8));
    primitives.push(primitiveLine(start, end, rgba(255, 209, 91, 255), 3.2));
    primitives.push(primitiveLine(start, end, rgba(255, 247, 255, 235), 1.2));
    return;
  }
  primitives.push(primitiveLine(start, end, rgba(0, 232, 255, 255), 2.2));
}

function primitiveLine(start, end, stroke, strokeWidth) {
  return { kind: 'line', points: [start, end], closed: false, rect: null, startDegrees: 0, sweepDegrees: 0, fill: null, stroke, strokeWidth };
}

function segmentRanges(startPct, endPct) {
  const start = normalizeProgress(startPct);
  const end = endPct >= 1 ? 1 : normalizeProgress(endPct);
  if (end <= start && endPct < 1) return [{ startPct: start, endPct: 1 }, { startPct: 0, endPct: end }];
  return [{ startPct: start, endPct: Math.max(0, Math.min(1, end)) }];
}

function boundaryProgresses(sectors) {
  if (!Array.isArray(sectors) || sectors.length < 2) return [];
  const seen = new Set();
  return sectors.map((sector) => normalizeProgress(sector.startPct)).filter((progress) => {
    const key = Math.round(progress * 100000);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function hasTrackMapHighlight(sector) {
  return sector?.highlight === 'personal-best' || sector?.highlight === 'best-lap';
}

function sectorHighlightColor(highlight) {
  return highlight === 'best-lap' ? rgba(182, 92, 255, 255) : rgba(80, 214, 124, 255);
}

function trackInteriorFill(opacity) {
  return rgba(9, 14, 18, Math.round(150 * Math.max(0.2, Math.min(1, opacity))));
}

function boundaryTickLength(progress) {
  return isStartFinish(progress) ? 17 * 1.45 : 17;
}

function isStartFinish(progress) {
  const normalized = normalizeProgress(progress);
  return normalized <= 0.0005 || normalized >= 0.9995;
}

function normalizeProgress(value) {
  const normalized = value % 1;
  return normalized < 0 ? normalized + 1 : normalized;
}

function streamChatStatusText(status) {
  switch (status) {
    case 'missing_or_invalid_streamlabs_url':
      return 'Choose Streamlabs and paste a valid Streamlabs Chat Box widget URL.';
    case 'missing_or_invalid_twitch_channel':
      return 'Choose Twitch and enter a valid public channel name.';
    default:
      return 'Choose Streamlabs or Twitch in the Stream Chat settings tab.';
  }
}

function normalizeRoute(route) {
  const text = String(route || '').trim().toLowerCase();
  return text.endsWith('/') && text.length > 1 ? text.slice(0, -1) : text;
}

function normalizePreviewMode(mode) {
  const normalized = String(mode || '').trim().toLowerCase();
  return ['practice', 'qualifying', 'race'].includes(normalized) ? normalized : 'off';
}

function themeCssVariables() {
  const contract = JSON.parse(readFileSync(resolve(repoRoot, 'shared/tmr-overlay-contract.json'), 'utf8'));
  const colors = contract.design.v2.colors;
  const variables = [
    ['--tmr-surface', colors.surface],
    ['--tmr-surface-inset', colors.surfaceInset],
    ['--tmr-surface-raised', colors.surfaceRaised],
    ['--tmr-title', colors.titleBar],
    ['--tmr-border', colors.border],
    ['--tmr-border-muted', colors.borderMuted],
    ['--tmr-text', colors.textPrimary],
    ['--tmr-text-rgb', colors.textPrimary],
    ['--tmr-text-secondary', colors.textSecondary],
    ['--tmr-text-secondary-rgb', colors.textSecondary],
    ['--tmr-text-muted', colors.textMuted],
    ['--tmr-text-muted-rgb', colors.textMuted],
    ['--tmr-cyan', colors.cyan],
    ['--tmr-cyan-rgb', colors.cyan],
    ['--tmr-magenta', colors.magenta],
    ['--tmr-magenta-rgb', colors.magenta],
    ['--tmr-amber', colors.amber],
    ['--tmr-amber-rgb', colors.amber],
    ['--tmr-green', colors.green],
    ['--tmr-green-rgb', colors.green],
    ['--tmr-orange', colors.orange],
    ['--tmr-orange-rgb', colors.orange],
    ['--tmr-error', colors.error],
    ['--tmr-error-rgb', colors.error],
    ['--tmr-track-line', colors.trackLine],
    ['--tmr-start-finish-boundary', colors.startFinishBoundary],
    ['--tmr-start-finish-boundary-shadow', colors.startFinishBoundaryShadow]
  ];
  return variables.map(([name, value]) => `      ${name}: ${name.endsWith('-rgb') ? rgbTuple(value) : cssColor(value)};`).join('\n');
}

function cssColor(value) {
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(value);
  if (!match) return value;
  if (!match[2]) return `#${match[1].toLowerCase()}`;
  const rgb = Number.parseInt(match[1], 16);
  const alpha = Number.parseInt(match[2], 16) / 255;
  return `rgba(${(rgb >> 16) & 255}, ${(rgb >> 8) & 255}, ${rgb & 255}, ${Number(alpha.toFixed(3))})`;
}

function rgbTuple(value) {
  const match = /^#([0-9a-f]{6})/i.exec(value);
  if (!match) return value;
  const rgb = Number.parseInt(match[1], 16);
  return `${(rgb >> 16) & 255}, ${(rgb >> 8) & 255}, ${rgb & 255}`;
}
