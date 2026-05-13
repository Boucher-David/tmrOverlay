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
    fadeWhenTelemetryUnavailable: true,
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
            : Number.isFinite(spatial.strongestMulticlassApproach?.relativeSeconds)
              ? 'class traffic'
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
      strongestMulticlassApproach: spatial.strongestMulticlassApproach || null,
      showMulticlassWarning: true,
      previewVisible: false,
      hasCurrentSignal: Boolean(spatial.hasCarLeft || spatial.hasCarRight || spatial.strongestMulticlassApproach || spatial.cars?.length)
    }
  };
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
      includeUserMaps: true
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
    markers.set(referenceCarIdx, {
      carIdx: referenceCarIdx,
      lapDistPct: normalizeProgress(latest.focusLapDistPct),
      isFocus: true,
      classColorHex: null,
      position: null
    });
  }

  return [...markers.values()].sort((left, right) => Number(left.isFocus) - Number(right.isFocus) || left.carIdx - right.carIdx);
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
