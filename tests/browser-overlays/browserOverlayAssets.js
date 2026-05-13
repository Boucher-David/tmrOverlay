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
    fadeWhenTelemetryUnavailable: true,
    modelRoute: '/api/overlay-model/input-state',
    settingsRoute: '/api/input-state',
    settingsProperty: 'inputStateSettings'
  }),
  'car-radar': pageDefinition('car-radar', 'Car Radar', '/overlays/car-radar', {
    bodyClass: 'car-radar-page',
    fadeWhenTelemetryUnavailable: true
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
    settingsRoute: '/api/track-map',
    settingsProperty: 'trackMapSettings'
  }),
  'garage-cover': pageDefinition('garage-cover', 'Garage Cover', '/overlays/garage-cover', {
    bodyClass: 'garage-cover-page',
    renderWhenTelemetryUnavailable: true,
    fadeWhenTelemetryUnavailable: false,
    settingsRoute: '/api/garage-cover',
    settingsProperty: 'garageCover'
  }),
  'stream-chat': pageDefinition('stream-chat', 'Stream Chat', '/overlays/stream-chat', {
    requiresTelemetry: false,
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
    return { model: model ?? emptyDisplayModel(page.page.id, page.title) };
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
      refreshIntervalMilliseconds: options.refreshIntervalMilliseconds ?? 250
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
