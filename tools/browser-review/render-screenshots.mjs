#!/usr/bin/env node
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync
} from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { browserOverlayPages } from '../../tests/browser-overlays/browserOverlayAssets.js';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const overlayPages = browserOverlayPages();
const overlayIds = overlayPages.map((page) => page.page.id);
const overlayPagesById = new Map(overlayPages.map((page) => [page.page.id, page]));
const sharedChromeOverlayIds = new Set([
  'standings',
  'relative',
  'fuel-calculator',
  'gap-to-leader',
  'session-weather',
  'pit-service'
]);
const previewModes = ['practice', 'qualifying', 'race'];

const args = parseArgs(process.argv.slice(2));
const outputRoot = resolve(repoRoot, args.output || defaultOutputFor(args.surface));
const port = args.port || Number.parseInt(process.env.TMR_BROWSER_SCREENSHOT_PORT || '5199', 10);
const baseUrl = stripTrailingSlash(args.baseUrl || `http://127.0.0.1:${port}`);
const settleMilliseconds = args.settleMilliseconds ?? 350;
let serverProcess = null;

try {
  if (!args.baseUrl) {
    serverProcess = startReviewServer(port);
  }

  await waitForServer(`${baseUrl}${serverProbePath(args.surface)}`);
  rmSync(outputRoot, { recursive: true, force: true });
  mkdirSync(outputRoot, { recursive: true });

  const browser = await chromium.launch();
  const context = await browser.newContext({
    viewport: { width: 1280, height: 760 },
    deviceScaleFactor: 1
  });
  const page = await context.newPage();
  const manifest = [];

  for (const route of screenshotRoutes(args.surface)) {
    await captureRoute(page, route, manifest);
  }

  await browser.close();
  writeFileSync(
    join(outputRoot, 'manifest.json'),
    `${JSON.stringify({
      generatedAtUtc: new Date().toISOString(),
      baseUrl,
      surfaceMode: args.surface,
      screenshots: manifest
    }, null, 2)}\n`);
  console.log(`Wrote ${manifest.length} ${args.surface} screenshots to ${outputRoot}`);
} finally {
  if (serverProcess) {
    serverProcess.kill('SIGTERM');
  }
}

function screenshotRoutes(surface) {
  const routes = [];
  if (surface === 'browser-review' || surface === 'all') {
    routes.push(
      settingsRoute('settings/general.png', '/review/app', { tab: 'general', region: 'general' }),
      settingsRoute('settings/diagnostics.png', '/review/app?tab=support', { tab: 'support', region: 'general' }),
      ...previewModes.map((mode) =>
        settingsRoute(
          `settings/general-preview-${mode}.png`,
          `/review/app?preview=${encodeURIComponent(mode)}`,
          { tab: 'general', region: 'general', previewMode: mode }))
    );

    for (const overlayId of overlayIds) {
      for (const region of regionsForOverlay(overlayId)) {
        const suffix = region === 'general' ? '' : `-${region}`;
        routes.push(settingsRoute(
          `settings/${overlayId}${suffix}.png`,
          `/review/app?tab=${encodeURIComponent(overlayId)}${region === 'general' ? '' : `&region=${encodeURIComponent(region)}`}`,
          { tab: overlayId, overlayId, region }));
      }
    }
  }

  for (const overlayId of overlayIds) {
    if (surface === 'browser-review' || surface === 'all') {
      routes.push(overlayRoute(
        `browser-overlays/${overlayId}.png`,
        withPreview(`/review/overlays/${encodeURIComponent(overlayId)}`, 'race'),
        { surface: 'browser-review-overlay', overlayId, previewMode: 'race' }));
    }
    if (surface === 'localhost' || surface === 'all') {
      routes.push(overlayRoute(
        `localhost-overlays/${overlayId}.png`,
        withPreview(`/overlays/${encodeURIComponent(overlayId)}`, 'race'),
        { surface: 'localhost-overlay', overlayId, previewMode: 'race' }));
      for (const alias of localhostAliasesForOverlay(overlayId)) {
        routes.push(overlayRoute(
          `localhost-overlays/${overlayId}-alias-${aliasSlug(alias)}.png`,
          withPreview(alias, 'race'),
          { surface: 'localhost-overlay', overlayId, previewMode: 'race', routeAlias: alias }));
      }
    }
    for (const mode of previewModesForOverlay(overlayId)) {
      if (surface === 'browser-review' || surface === 'all') {
        routes.push(overlayRoute(
          `browser-overlays/${overlayId}-${mode}.png`,
          withPreview(`/review/overlays/${encodeURIComponent(overlayId)}`, mode),
          { surface: 'browser-review-overlay', overlayId, previewMode: mode }));
      }
      if (surface === 'localhost' || surface === 'all') {
        routes.push(overlayRoute(
          `localhost-overlays/${overlayId}-${mode}.png`,
          withPreview(`/overlays/${encodeURIComponent(overlayId)}`, mode),
          { surface: 'localhost-overlay', overlayId, previewMode: mode }));
        for (const alias of localhostAliasesForOverlay(overlayId)) {
          routes.push(overlayRoute(
            `localhost-overlays/${overlayId}-alias-${aliasSlug(alias)}-${mode}.png`,
            withPreview(alias, mode),
            { surface: 'localhost-overlay', overlayId, previewMode: mode, routeAlias: alias }));
        }
      }
    }
  }

  return routes;
}

function withPreview(urlPath, mode) {
  return `${urlPath}${urlPath.includes('?') ? '&' : '?'}preview=${encodeURIComponent(mode)}`;
}

function regionsForOverlay(overlayId) {
  if (overlayId === 'garage-cover') {
    return ['general', 'preview'];
  }
  if (overlayId === 'stream-chat') {
    return ['general', 'content', 'twitch', 'streamlabs'];
  }
  return sharedChromeOverlayIds.has(overlayId)
    ? ['general', 'content', 'header', 'footer']
    : ['general', 'content'];
}

function previewModesForOverlay(overlayId) {
  return overlayId === 'gap-to-leader' ? ['race'] : previewModes;
}

function localhostAliasesForOverlay(overlayId) {
  return overlayPagesById.get(overlayId)?.aliases || [];
}

function aliasSlug(alias) {
  const slug = String(alias || '')
    .split('/')
    .filter(Boolean)
    .pop();
  return String(slug || 'alias')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'alias';
}

function settingsRoute(relativePath, urlPath, metadata = {}) {
  return {
    relativePath,
    urlPath,
    selector: '#settings-app',
    viewport: { width: 1280, height: 760 },
    minBytes: 10_000,
    surface: 'browser-review-settings',
    renderer: 'settings-general.html',
    sourceContract: 'src/TmrOverlay.App/Overlays/BrowserSources/Assets/templates/settings-general.html',
    ...metadata
  };
}

function overlayRoute(relativePath, urlPath, metadata = {}) {
  return {
    relativePath,
    urlPath,
    selector: '.overlay',
    viewport: { width: 1440, height: 900 },
    minBytes: 1_000,
    renderer: 'browser-overlay-assets',
    moduleAsset: metadata.overlayId ? `src/TmrOverlay.App/Overlays/BrowserSources/Assets/modules/${metadata.overlayId}.js` : null,
    sourceContract: 'src/TmrOverlay.App/Overlays/BrowserSources/BrowserOverlayModelFactory.cs',
    ...metadata
  };
}

async function captureRoute(page, route, manifest) {
  await page.setViewportSize(route.viewport);
  const url = `${baseUrl}${route.urlPath}`;
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  const element = page.locator(route.selector).first();
  await element.waitFor({ state: 'visible', timeout: 5_000 });
  await page.waitForTimeout(settleMilliseconds);
  const model = await readOverlayModel(route);
  const dom = await readDomDiagnostics(element);

  const screenshotPath = join(outputRoot, route.relativePath);
  mkdirSync(dirname(screenshotPath), { recursive: true });
  await element.screenshot({
    path: screenshotPath,
    animations: 'disabled'
  });

  const artifact = inspectPng(screenshotPath, route.minBytes);
  manifest.push({
    path: route.relativePath,
    url: route.urlPath,
    selector: route.selector,
    surface: route.surface,
    renderer: route.renderer,
    sourceContract: route.sourceContract,
    moduleAsset: route.moduleAsset || null,
    overlayId: route.overlayId || null,
    tab: route.tab || null,
    region: route.region || null,
    activeRegion: dom.activeRegion,
    routeAlias: route.routeAlias || null,
    previewMode: route.previewMode || null,
    status: stringOrNull(model?.status),
    source: stringOrNull(model?.source),
    bodyKind: stringOrNull(model?.bodyKind),
    shouldRender: booleanOrNull(model?.shouldRender),
    rowCount: arrayLength(model?.rows),
    metricCount: arrayLength(model?.metrics) + arrayLength(model?.metricSections) + arrayLength(model?.gridSections),
    flagCount: arrayLength(model?.flags?.flags),
    radarShouldRender: booleanOrNull(model?.carRadar?.renderModel?.shouldRender),
    trackMapMarkerCount: arrayLength(model?.trackMap?.markers),
    textSample: dom.textSample,
    contentBounds: dom.contentBounds,
    layout: dom.layout,
    modelEvidence: modelLayoutEvidence(model, dom.layout),
    width: artifact.width,
    height: artifact.height,
    bytes: artifact.bytes
  });
}

async function readOverlayModel(route) {
  if (!route.overlayId || !route.surface?.endsWith('-overlay')) {
    return null;
  }

  const query = route.urlPath.includes('?') ? route.urlPath.slice(route.urlPath.indexOf('?')) : '';
  const response = await fetch(`${baseUrl}/api/overlay-model/${encodeURIComponent(route.overlayId)}${query}`, {
    headers: { accept: 'application/json' }
  });
  if (!response.ok) {
    throw new Error(`Failed to read ${route.overlayId} model for ${route.relativePath}: HTTP ${response.status}`);
  }

  const payload = await response.json();
  return payload?.model || null;
}

async function readDomDiagnostics(element) {
  return element.evaluate((node) => {
    const round = (value) => Math.round(Number(value || 0) * 1000) / 1000;
    const rectFor = (element, rootRect) => {
      const rect = element.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) return null;
      return {
        x: round(rect.left - rootRect.left),
        y: round(rect.top - rootRect.top),
        width: round(rect.width),
        height: round(rect.height)
      };
    };
    const textFor = (element, limit = 160) => String(element.innerText || element.textContent || '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, limit) || null;
    const styleFor = (element) => {
      const style = window.getComputedStyle(element);
      return {
        color: style.color || null,
        backgroundColor: style.backgroundColor || null,
        borderColor: style.borderColor || null,
        fontFamily: style.fontFamily || null,
        fontSize: style.fontSize || null,
        fontWeight: style.fontWeight || null,
        display: style.display || null,
        gridTemplateColumns: style.gridTemplateColumns && style.gridTemplateColumns !== 'none'
          ? style.gridTemplateColumns
          : null,
        gridTemplateRows: style.gridTemplateRows && style.gridTemplateRows !== 'none'
          ? style.gridTemplateRows
          : null
      };
    };
    const roleSelectors = [
      ['settings-app', '#settings-app'],
      ['settings-titlebar', '.titlebar'],
      ['settings-sidebar', '.sidebar'],
      ['settings-sidebar-tab', '.sidebar-tab'],
      ['settings-content', '.content-card'],
      ['settings-content-header', '.content-header'],
      ['settings-content-body', '.content-body'],
      ['settings-region-segment', '.region-segment'],
      ['settings-panel', '.panel'],
      ['settings-field-row', '.field-row, .status-row'],
      ['settings-button', 'button'],
      ['settings-segmented', '.segmented'],
      ['settings-choice', '.choice'],
      ['settings-matrix', '.matrix-table, .toggle-grid, .chrome-table'],
      ['settings-matrix-row', '.matrix-item, .grid-toggle-row, .chrome-row-label'],
      ['settings-matrix-cell', '.matrix-session, .matrix-visible, .matrix-head, .chrome-check, .chrome-head'],
      ['overlay', '.overlay'],
      ['header', '.header'],
      ['title', '.title'],
      ['status', '#status'],
      ['time-remaining', '#time-remaining'],
      ['content', '#content'],
      ['source', '#source'],
      ['table', 'table'],
      ['thead-row', 'thead tr'],
      ['table-header-cell', 'th'],
      ['table-row', 'tbody tr'],
      ['table-cell', 'tbody td'],
      ['class-header-band', '.class-header-band'],
      ['metric-list', '.metric-list'],
      ['metric-row', '.metric'],
      ['metric-label', '.metric .label'],
      ['metric-value', '.metric .value'],
      ['metric-segment', '.value-segment'],
      ['metric-section', '.metric-section'],
      ['metric-section-title', '.metric-section-title'],
      ['tire-grid', '.tire-grid'],
      ['tire-grid-row', '.tire-grid-row'],
      ['tire-grid-cell', '.tire-grid-cell, .tire-grid-label, .tire-grid-header'],
      ['graph-panel', '.model-graph-panel'],
      ['graph-canvas', '.model-graph, canvas'],
      ['flags', '.flags-v2'],
      ['flag-cell', '.flag-cell'],
      ['car-radar', '.radar-v2, .car-radar-v2'],
      ['radar-shape', '.radar-v2 rect, .radar-v2 circle, .radar-v2 path, .radar-v2 text, .car-radar-v2 rect, .car-radar-v2 circle, .car-radar-v2 path, .car-radar-v2 text'],
      ['track-map', '.track-map-v2, .track'],
      ['track-map-shape', '.track-map-v2 path, .track-map-v2 circle, .track-map-v2 text, .track svg path, .track svg circle, .track svg text'],
      ['input-shell', '.input-state-v2, .input-layout'],
      ['input-graph', '.input-graph-panel, .input-graph'],
      ['input-rail', '.input-rail'],
      ['input-item', '.input-bar, .input-readout, .input-wheel'],
      ['stream-chat-body', '.stream-chat-body, .chat'],
      ['chat-line', '.chat-line'],
      ['chat-badge', '.chat-badge, .chat-badge.image'],
      ['chat-text', '.chat-text']
    ];
    const text = String(node.innerText || node.textContent || '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 500);
    const activeRegion = String(node.querySelector('.region-segment.active')?.textContent || '')
      .replace(/\s+/g, ' ')
      .trim();
    const selectors = [
      '#settings-app',
      '.titlebar',
      '.sidebar',
      '.content-card',
      '.content-body',
      '.panel',
      '.region-segments',
      '.overlay-panel',
      '.overlay-content',
      '.content',
      '.metric-list',
      '.table',
      '.model-graph-panel',
      '.flags-v2',
      '.car-radar-v2',
      '.track-map-v2',
      '.garage-cover',
      '.stream-chat-body',
      'canvas',
      'svg'
    ];
    const rootRect = node.getBoundingClientRect();
    const elements = [];
    for (const [role, selector] of roleSelectors) {
      Array.from(node.querySelectorAll(selector)).forEach((element, index) => {
        const bounds = rectFor(element, rootRect);
        if (!bounds) return;
        elements.push({
          role,
          index,
          tag: element.tagName.toLowerCase(),
          className: String(element.className?.baseVal || element.className || '') || null,
          text: textFor(element),
          bounds,
          styles: styleFor(element)
        });
      });
    }
    const rects = selectors
      .flatMap((selector) => Array.from(node.querySelectorAll(selector)))
      .map((element) => element.getBoundingClientRect())
      .filter((rect) => rect.width > 0 && rect.height > 0);
    if (!rects.length) {
      return {
        textSample: text || null,
        activeRegion: activeRegion || null,
        contentBounds: null,
        layout: {
          contract: 'browser-layout/v1',
          root: { x: 0, y: 0, width: round(rootRect.width), height: round(rootRect.height) },
          elements
        }
      };
    }

    const left = Math.min(...rects.map((rect) => rect.left));
    const top = Math.min(...rects.map((rect) => rect.top));
    const right = Math.max(...rects.map((rect) => rect.right));
    const bottom = Math.max(...rects.map((rect) => rect.bottom));
    const width = right - left;
    const height = bottom - top;
    return {
      textSample: text || null,
      activeRegion: activeRegion || null,
      contentBounds: {
        x: Math.round(left - rootRect.left),
        y: Math.round(top - rootRect.top),
        width: Math.round(width),
        height: Math.round(height),
        aspectRatio: height > 0 ? Number((width / height).toFixed(4)) : null
      },
      layout: {
        contract: 'browser-layout/v1',
        root: { x: 0, y: 0, width: round(rootRect.width), height: round(rootRect.height) },
        contentBounds: {
          x: round(left - rootRect.left),
          y: round(top - rootRect.top),
          width: round(width),
          height: round(height)
        },
        elements
      }
    };
  });
}

function modelLayoutEvidence(model, layout) {
  if (!model || typeof model !== 'object') {
    return null;
  }

  return {
    contract: 'overlay-model-layout-evidence/v1',
    bodyKind: stringOrNull(model.bodyKind),
    columns: (Array.isArray(model.columns) ? model.columns : []).map((column, index) => ({
      index,
      label: stringOrNull(column?.label),
      dataKey: stringOrNull(column?.dataKey),
      configuredWidth: Number.isFinite(Number(column?.width)) ? Number(column.width) : null,
      alignment: stringOrNull(column?.alignment)
    })),
    rows: (Array.isArray(model.rows) ? model.rows : []).slice(0, 80).map((row, index) => ({
      index,
      kind: row?.rowKind || (row?.isClassHeader ? 'class-header' : 'row'),
      isReference: Boolean(row?.isReference || row?.isFocus || row?.isReferenceCar),
      isPartial: Boolean(row?.isPartial),
      classColorHex: stringOrNull(row?.carClassColorHex),
      cells: Array.isArray(row?.cells) ? row.cells.map((cell) => String(cell ?? '')) : []
    })),
    metrics: (Array.isArray(model.metrics) ? model.metrics : []).map(metricEvidence),
    metricSections: (Array.isArray(model.metricSections) ? model.metricSections : []).map((section) => ({
      title: stringOrNull(section?.title),
      rows: (Array.isArray(section?.rows) ? section.rows : []).map(metricEvidence)
    })),
    gridSections: (Array.isArray(model.gridSections) ? model.gridSections : []).map((section) => ({
      title: stringOrNull(section?.title),
      headers: Array.isArray(section?.headers) ? section.headers.map((header) => String(header ?? '')) : [],
      rows: (Array.isArray(section?.rows) ? section.rows : []).map((row, index) => ({
        index,
        label: stringOrNull(row?.label),
        tone: stringOrNull(row?.tone),
        cells: (Array.isArray(row?.cells) ? row.cells : []).map((cell) => ({
          value: stringOrNull(cell?.value),
          tone: stringOrNull(cell?.tone)
        }))
      }))
    })),
    graph: graphModelEvidence(model, layout),
    inputs: model.inputs ? inputEvidence(model.inputs, layout) : null,
    flags: model.flags ? {
      count: arrayLength(model.flags?.flags),
      kinds: (Array.isArray(model.flags?.flags) ? model.flags.flags : []).map((flag) => stringOrNull(flag?.kind))
    } : null,
    carRadar: model.carRadar?.renderModel ? {
      shouldRender: booleanOrNull(model.carRadar.renderModel.shouldRender),
      width: numberOrNull(model.carRadar.renderModel.width),
      height: numberOrNull(model.carRadar.renderModel.height),
      carCount: arrayLength(model.carRadar.renderModel.cars),
      labelCount: arrayLength(model.carRadar.renderModel.labels),
      ringCount: arrayLength(model.carRadar.renderModel.rings)
    } : null,
    trackMap: model.trackMap ? {
      markerCount: arrayLength(model.trackMap.markers),
      primitiveCount: arrayLength(model.trackMap.primitives),
      width: numberOrNull(model.trackMap.width),
      height: numberOrNull(model.trackMap.height)
    } : null
  };
}

function metricEvidence(row) {
  return {
    label: stringOrNull(row?.label),
    value: stringOrNull(row?.value),
    tone: stringOrNull(row?.tone),
    rowColorHex: stringOrNull(row?.rowColorHex || row?.carClassColorHex),
    segments: (Array.isArray(row?.segments) ? row.segments : []).map((segment, index) => ({
      index,
      label: stringOrNull(segment?.label),
      value: stringOrNull(segment?.value),
      tone: stringOrNull(segment?.tone),
      accentHex: stringOrNull(segment?.accentHex),
      rotationDegrees: numberOrNull(segment?.rotationDegrees)
    }))
  };
}

function graphModelEvidence(model, layout) {
  if (model?.graph) {
    return graphEvidence(model.graph, layout);
  }

  if (model?.bodyKind === 'graph' && Array.isArray(model?.points)) {
    return graphEvidence({
      points: model.points,
      startSeconds: 0,
      endSeconds: Math.max(1, model.points.length - 1),
      maxGapSeconds: Math.max(1, ...model.points.map(Number).filter(Number.isFinite)),
      comparisonLabel: stringOrNull(model?.status) || '--'
    }, layout);
  }

  return null;
}

function graphEvidence(graph, layout) {
  const canvasBounds = findElementBounds(layout, 'graph-canvas', 'model-graph');
  const geometry = canvasBounds ? browserGraphGeometry(graph, canvasBounds) : null;
  return {
    startSeconds: numberOrNull(graph?.startSeconds),
    endSeconds: numberOrNull(graph?.endSeconds),
    maxGapSeconds: numberOrNull(graph?.maxGapSeconds),
    lapReferenceSeconds: numberOrNull(graph?.lapReferenceSeconds),
    selectedSeriesCount: numberOrNull(graph?.selectedSeriesCount),
    metricDeadbandSeconds: numberOrNull(graph?.metricDeadbandSeconds),
    comparisonLabel: stringOrNull(graph?.comparisonLabel),
    canvasBounds,
    geometry,
    series: (Array.isArray(graph?.series) ? graph.series : []).map((series, index) => ({
      index,
      carIdx: numberOrNull(series?.carIdx),
      classPosition: numberOrNull(series?.classPosition),
      isReference: Boolean(series?.isReference),
      isClassLeader: Boolean(series?.isClassLeader),
      alpha: numberOrNull(series?.alpha),
      isStickyExit: Boolean(series?.isStickyExit),
      isStale: Boolean(series?.isStale),
      pointCount: arrayLength(series?.points)
    })),
    trendMetricCount: arrayLength(graph?.trendMetrics),
    trendMetrics: (Array.isArray(graph?.trendMetrics) ? graph.trendMetrics : []).map((metric, index) => ({
      index,
      label: stringOrNull(metric?.label),
      state: stringOrNull(metric?.state),
      stateLabel: stringOrNull(metric?.stateLabel),
      valueText: graphMetricValueText(metric),
      chaserText: graphMetricChaserText(metric)
    })),
    weatherCount: arrayLength(graph?.weather),
    leaderChangeCount: arrayLength(graph?.leaderChanges),
    driverChangeCount: arrayLength(graph?.driverChanges)
  };
}

function browserGraphGeometry(graph, canvasBounds) {
  const local = browserGapGraphLayout(canvasBounds.width, canvasBounds.height);
  const scale = graph?.scale || { isFocusRelative: false, maxGapSeconds: graph?.maxGapSeconds };
  const maxGapSeconds = Math.max(1, numberOr(scale?.maxGapSeconds, graph?.maxGapSeconds, 1));
  const rawSeries = Array.isArray(graph?.series) ? graph.series : [];
  if (rawSeries.length === 0 && Array.isArray(graph?.points) && graph.points.length > 0) {
    return browserFallbackGraphGeometry(graph, canvasBounds, local);
  }

  const orderedSeries = rawSeries
    .map((series, sourceIndex) => ({ series, sourceIndex }))
    .sort((a, b) =>
      Number(Boolean(a.series?.isClassLeader)) - Number(Boolean(b.series?.isClassLeader))
      || Number(Boolean(a.series?.isReference)) - Number(Boolean(b.series?.isReference)));
  return {
    frame: canvasBounds,
    plot: offsetRect(canvasBounds, local.plot),
    labelLane: offsetRect(canvasBounds, local.labelLane),
    metricsTable: local.metricsRect ? offsetRect(canvasBounds, local.metricsRect) : null,
    scale: scale?.isFocusRelative === true ? 'focus-relative' : 'leader',
    aheadSeconds: scale?.isFocusRelative === true ? numberOrNull(scale?.aheadSeconds) : null,
    behindSeconds: scale?.isFocusRelative === true ? numberOrNull(scale?.behindSeconds) : null,
    latestReferenceGapSeconds: scale?.isFocusRelative === true ? numberOrNull(scale?.latestReferenceGapSeconds) : null,
    weatherBands: graphWeatherBands(graph, local.plot, canvasBounds),
    markers: graphMarkers(graph, scale, local.plot, maxGapSeconds, canvasBounds),
    metricRows: graphMetricRows(local.metricsRect, graph, canvasBounds),
    series: orderedSeries.map(({ series, sourceIndex }, drawIndex) =>
      graphSeriesGeometry(graph, scale, local.plot, maxGapSeconds, canvasBounds, series, sourceIndex, drawIndex))
  };
}

function browserFallbackGraphGeometry(graph, canvasBounds, local) {
  const values = graph.points.map(Number).filter(Number.isFinite);
  const max = Math.max(1, ...values.map((value) => Math.max(0, value)));
  const points = values.map((value, index) => {
    const progress = index / Math.max(1, values.length - 1);
    const normalized = Math.max(0, value) / max;
    return {
      axisSeconds: index,
      gapSeconds: value,
      startsSegment: index === 0,
      point: offsetPoint(canvasBounds, {
        x: local.plot.x + progress * local.plot.width,
        y: local.plot.y + normalized * local.plot.height
      })
    };
  });
  return {
    frame: canvasBounds,
    plot: offsetRect(canvasBounds, local.plot),
    labelLane: offsetRect(canvasBounds, local.labelLane),
    metricsTable: local.metricsRect ? offsetRect(canvasBounds, local.metricsRect) : null,
    scale: 'leader',
    aheadSeconds: null,
    behindSeconds: null,
    latestReferenceGapSeconds: null,
    weatherBands: [],
    markers: [],
    metricRows: [],
    series: [{
      sourceIndex: 0,
      drawIndex: 0,
      carIdx: null,
      classPosition: null,
      isReference: true,
      isClassLeader: false,
      pointCount: values.length,
      baseColor: '#00e8ff',
      alpha: 1,
      effectiveAlpha: 1,
      strokeWidth: 2,
      isDashed: false,
      endpointLabel: 'trend',
      latestPoint: points[points.length - 1]?.point || null,
      points
    }]
  };
}

function graphSeriesGeometry(graph, scale, plot, maxGapSeconds, canvasBounds, series, sourceIndex, drawIndex) {
  const baseColor = graphSeriesColor(series, drawIndex, graph?.threatCarIdx);
  const alpha = clamp01(numberOr(series?.alpha, 1));
  const effectiveAlpha = alpha * graphSeriesAlphaMultiplier(series, graph?.threatCarIdx);
  const points = (Array.isArray(series?.points) ? series.points : [])
    .filter((point) => Number.isFinite(point?.axisSeconds) && Number.isFinite(point?.gapSeconds))
    .sort((a, b) => a.axisSeconds - b.axisSeconds)
    .map((point) => ({
      axisSeconds: numberOrNull(point.axisSeconds),
      gapSeconds: numberOrNull(point.gapSeconds),
      startsSegment: Boolean(point.startsSegment),
      point: offsetPoint(canvasBounds, graphPoint(graph, scale, plot, maxGapSeconds, point.axisSeconds, point.gapSeconds))
    }));
  const latest = points[points.length - 1] || null;
  return {
    sourceIndex,
    drawIndex,
    carIdx: numberOrNull(series?.carIdx),
    classPosition: numberOrNull(series?.classPosition),
    isReference: Boolean(series?.isReference),
    isClassLeader: Boolean(series?.isClassLeader),
    pointCount: arrayLength(series?.points),
    baseColor,
    alpha,
    effectiveAlpha,
    strokeWidth: series?.isReference ? 2.6 : series?.isClassLeader ? 1.8 : 1.25,
    isDashed: Boolean(series?.isStale || series?.isStickyExit),
    endpointLabel: series?.isClassLeader
      ? 'P1'
      : Number.isFinite(series?.classPosition)
        ? `P${series.classPosition}`
        : `#${series?.carIdx ?? '--'}`,
    latestPoint: latest?.point || null,
    points
  };
}

function graphWeatherBands(graph, plot, canvasBounds) {
  const weather = Array.isArray(graph?.weather) ? graph.weather : [];
  const domain = graphDomain(graph);
  return weather
    .map((point, index) => {
      const color = weatherColor(point?.condition);
      if (!color) return null;
      const nextAxis = Number.isFinite(weather[index + 1]?.axisSeconds) ? weather[index + 1].axisSeconds : domain.end;
      const start = Math.max(domain.start, numberOr(point?.axisSeconds, domain.start));
      const end = Math.min(domain.end, nextAxis);
      if (end <= start) return null;
      const x = axisToX(graph, plot, start);
      const nextX = axisToX(graph, plot, end);
      return {
        kind: String(point?.condition ?? ''),
        startAxisSeconds: round(start),
        endAxisSeconds: round(end),
        bounds: offsetRect(canvasBounds, { x, y: plot.y, width: Math.max(1, nextX - x), height: plot.height }),
        color
      };
    })
    .filter(Boolean);
}

function graphMarkers(graph, scale, plot, maxGapSeconds, canvasBounds) {
  const markers = [];
  for (const marker of Array.isArray(graph?.leaderChanges) ? graph.leaderChanges : []) {
    if (!Number.isFinite(marker?.axisSeconds)) continue;
    const x = axisToX(graph, plot, marker.axisSeconds);
    markers.push({
      kind: 'leader-change',
      label: 'leader',
      axisSeconds: numberOrNull(marker.axisSeconds),
      start: offsetPoint(canvasBounds, { x, y: plot.y }),
      end: offsetPoint(canvasBounds, { x, y: plot.y + plot.height }),
      color: 'rgba(255, 255, 255, 0.45)'
    });
  }

  for (const marker of Array.isArray(graph?.driverChanges) ? graph.driverChanges : []) {
    if (!Number.isFinite(marker?.axisSeconds) || !Number.isFinite(marker?.gapSeconds)) continue;
    const point = graphPoint(graph, scale, plot, maxGapSeconds, marker.axisSeconds, marker.gapSeconds);
    markers.push({
      kind: 'driver-change',
      label: stringOrNull(marker?.label),
      axisSeconds: numberOrNull(marker.axisSeconds),
      gapSeconds: numberOrNull(marker.gapSeconds),
      carIdx: numberOrNull(marker?.carIdx),
      isReference: Boolean(marker?.isReference),
      start: offsetPoint(canvasBounds, point),
      end: offsetPoint(canvasBounds, point),
      color: marker?.isReference ? '#70e092' : '#cdd8e4'
    });
  }

  return markers;
}

function graphMetricRows(metricsRect, graph, canvasBounds) {
  const metrics = Array.isArray(graph?.trendMetrics) ? graph.trendMetrics.filter(Boolean) : [];
  if (!metricsRect || metrics.length === 0) return [];
  const showThreatFooter = metrics.length <= 6;
  const rowAreaBottomPadding = showThreatFooter ? 48 : 8;
  const rowHeight = Math.max(9.5, Math.min(26, (metricsRect.height - rowAreaBottomPadding - 38) / Math.max(1, metrics.length)));
  return metrics.map((metric, index) => {
    const y = metricsRect.y + 43 + index * rowHeight;
    const row = { x: metricsRect.x + 8, y, width: metricsRect.width - 16, height: Math.max(10, Math.min(14, rowHeight)) };
    return {
      index,
      text: stringOrNull(metric?.label),
      state: stringOrNull(metric?.state),
      bounds: offsetRect(canvasBounds, row),
      cells: [
        { column: 'Metric', text: stringOrNull(metric?.label), bounds: offsetRect(canvasBounds, { x: metricsRect.x + 8, y, width: 32, height: row.height }) },
        { column: stringOrNull(graph?.comparisonLabel) || '--', text: graphMetricValueText(metric), bounds: offsetRect(canvasBounds, { x: metricsRect.x + 43, y, width: 58, height: row.height }) },
        { column: 'Threat', text: graphMetricChaserText(metric), bounds: offsetRect(canvasBounds, { x: metricsRect.x + 104, y, width: metricsRect.width - 110, height: row.height }) }
      ]
    };
  });
}

function inputEvidence(inputs, layout) {
  const canvasBounds = findElementBounds(layout, 'input-graph', 'input-graph')
    || findElementBounds(layout, 'graph-canvas', 'input-graph');
  return {
    hasContent: booleanOrNull(inputs?.hasContent),
    hasGraph: booleanOrNull(inputs?.hasGraph),
    hasRail: booleanOrNull(inputs?.hasRail),
    isAvailable: booleanOrNull(inputs?.isAvailable),
    sampleIntervalMilliseconds: numberOrNull(inputs?.sampleIntervalMilliseconds),
    maximumTracePoints: numberOrNull(inputs?.maximumTracePoints),
    tracePointCount: arrayLength(inputs?.trace),
    graph: canvasBounds ? {
      bounds: canvasBounds,
      gridLines: inputGridLines(canvasBounds),
      series: inputTraceSeries(inputs, canvasBounds)
    } : null
  };
}

function inputGridLines(bounds) {
  return [1, 2, 3].map((step) => {
    const y = bounds.y + bounds.height * step / 4;
    return {
      kind: 'input-grid',
      start: { x: round(bounds.x), y: round(y) },
      end: { x: round(bounds.x + bounds.width), y: round(y) },
      color: 'rgba(140, 174, 212, 0.18)',
      strokeWidth: 1
    };
  });
}

function inputTraceSeries(inputs, bounds) {
  const trace = Array.isArray(inputs?.trace) ? inputs.trace : [];
  const series = [];
  addTrace(Boolean(inputs?.showThrottleTrace), 'throttle', '#62ff9f', 'throttle', 2);
  addTrace(Boolean(inputs?.showBrakeTrace), 'brake', '#ff6274', 'brake', 2);
  addTrace(Boolean(inputs?.showClutchTrace), 'clutch', '#00e8ff', 'clutch', 2);
  if (inputs?.showBrakeTrace === true && trace.length >= 2) {
    const curves = [];
    for (let index = 1; index < trace.length; index += 1) {
      if (trace[index]?.brakeAbsActive !== true) continue;
      const start = inputTracePoint(trace[index - 1]?.brake, index - 1, trace.length, bounds);
      const end = inputTracePoint(trace[index]?.brake, index, trace.length, bounds);
      curves.push({ start, control1: start, control2: end, end });
    }
    if (curves.length) {
      series.push({ kind: 'brake-abs', color: '#ffd15b', strokeWidth: 3, points: [], curves });
    }
  }
  return series;

  function addTrace(enabled, kind, color, key, strokeWidth) {
    if (!enabled) return;
    if (trace.length < 2) {
      const y = bounds.y + bounds.height / 2;
      const start = { x: round(bounds.x + 8), y: round(y) };
      const end = { x: round(bounds.x + bounds.width - 8), y: round(y) };
      series.push({ kind, color: colorWithAlpha(color, 0.24), strokeWidth, points: [], curves: [{ start, control1: start, control2: end, end }] });
      return;
    }
    const points = trace.map((point, index) => inputTracePoint(point?.[key], index, trace.length, bounds));
    series.push({ kind, color, strokeWidth, points, curves: smoothTraceCurves(points) });
  }
}

function browserGapGraphLayout(width, height) {
  const axisWidth = 58;
  const xAxisHeight = 17;
  const labelLaneWidth = 38;
  const metricsWidth = gapMetricsTableWidth(width);
  const plotHeight = Math.max(40, height - xAxisHeight);
  const metricsRect = metricsWidth > 0
    ? { x: width - metricsWidth, y: 0, width: metricsWidth, height: plotHeight }
    : null;
  const chartRight = metricsRect ? metricsRect.x - 10 : width - 4;
  const labelLane = { x: chartRight - labelLaneWidth, y: 0, width: labelLaneWidth, height: plotHeight };
  const plot = { x: axisWidth, y: 0, width: Math.max(40, labelLane.x - axisWidth), height: plotHeight };
  return { plot, labelLane, metricsRect };
}

function gapMetricsTableWidth(width) {
  const metricsWidth = 184;
  const availableAfterTable = width - 58 - 38 - 10 - metricsWidth;
  return availableAfterTable >= 300 ? metricsWidth : 0;
}

function graphPoint(graph, scale, plot, maxGapSeconds, axisSeconds, gapSeconds) {
  const x = axisToX(graph, plot, axisSeconds);
  const y = scale?.isFocusRelative === true
    ? gapDeltaToY(gapSeconds - referenceGapAt(scale.referencePoints || [], axisSeconds), scale, plot)
    : gapToY(gapSeconds, maxGapSeconds, plot);
  return { x, y };
}

function axisToX(graph, plot, axisSeconds) {
  const domain = graphDomain(graph);
  const ratio = (axisSeconds - domain.start) / Math.max(1, domain.end - domain.start);
  return plot.x + Math.max(0, Math.min(1, ratio)) * plot.width;
}

function gapToY(gapSeconds, maxGapSeconds, plot) {
  return plot.y + Math.max(0, Math.min(1, gapSeconds / Math.max(1, maxGapSeconds))) * plot.height;
}

function gapDeltaToY(deltaSeconds, scale, plot) {
  const referenceY = plot.y + plot.height * 0.56;
  if (deltaSeconds < 0) {
    const ratio = Math.max(0, Math.min(1, Math.abs(deltaSeconds) / Math.max(1, numberOr(scale?.aheadSeconds, 1))));
    return referenceY - ratio * Math.max(1, referenceY - (plot.y + 18));
  }
  const ratio = Math.max(0, Math.min(1, deltaSeconds / Math.max(1, numberOr(scale?.behindSeconds, 1))));
  return referenceY + ratio * Math.max(1, plot.y + plot.height - 8 - referenceY);
}

function referenceGapAt(points, axisSeconds) {
  const ordered = (Array.isArray(points) ? points : [])
    .filter((point) => Number.isFinite(point?.axisSeconds) && Number.isFinite(point?.gapSeconds))
    .sort((a, b) => a.axisSeconds - b.axisSeconds);
  if (!ordered.length) return 0;
  if (axisSeconds <= ordered[0].axisSeconds) return ordered[0].gapSeconds;
  const last = ordered[ordered.length - 1];
  if (axisSeconds >= last.axisSeconds) return last.gapSeconds;
  const afterIndex = ordered.findIndex((point) => point.axisSeconds >= axisSeconds);
  const after = ordered[Math.max(0, afterIndex)];
  const before = ordered[Math.max(0, afterIndex - 1)];
  const span = after.axisSeconds - before.axisSeconds;
  if (span <= 0.001) return before.gapSeconds;
  const ratio = Math.max(0, Math.min(1, (axisSeconds - before.axisSeconds) / span));
  return before.gapSeconds + (after.gapSeconds - before.gapSeconds) * ratio;
}

function graphDomain(graph) {
  const start = numberOr(graph?.startSeconds, 0);
  const end = Math.max(start + 1, numberOr(graph?.endSeconds, start + 1));
  return { start, end };
}

function graphSeriesColor(series, index, threatCarIdx) {
  if (Number.isFinite(Number(threatCarIdx)) && Number(series?.carIdx) === Number(threatCarIdx)) return '#ec7063';
  if (series?.isReference) return '#00e8ff';
  if (series?.isClassLeader) return '#f7fbff';
  return ['#ffd15b', '#70e092', '#ff62d2'][index % 3];
}

function graphSeriesAlphaMultiplier(series, threatCarIdx) {
  return series?.isClassLeader || series?.isReference || (Number.isFinite(Number(threatCarIdx)) && Number(series?.carIdx) === Number(threatCarIdx)) ? 1 : 0.48;
}

function weatherColor(condition) {
  if (condition === 2 || String(condition).toLowerCase() === 'damp') return 'rgba(75, 170, 205, 0.08)';
  if (condition === 3 || String(condition).toLowerCase() === 'wet') return 'rgba(70, 135, 230, 0.13)';
  if (condition === 4 || String(condition).toLowerCase() === 'declaredwet' || String(condition).toLowerCase() === 'declared-wet') return 'rgba(78, 142, 238, 0.17)';
  return null;
}

function inputTracePoint(value, index, count, bounds) {
  return {
    x: round(bounds.x + index / Math.max(1, count - 1) * bounds.width),
    y: round(bounds.y + bounds.height - clamp01(Number(value)) * bounds.height)
  };
}

function smoothTraceCurves(points) {
  const curves = [];
  for (let index = 0; index < points.length - 1; index += 1) {
    const p0 = index === 0 ? points[index] : points[index - 1];
    const p1 = points[index];
    const p2 = points[index + 1];
    const p3 = index + 2 < points.length ? points[index + 2] : p2;
    const control1 = {
      x: round(p1.x + (p2.x - p0.x) / 6),
      y: round(clampSmoothTraceControlY(p1.y + (p2.y - p0.y) / 6, p1.y, p2.y))
    };
    const control2 = {
      x: round(p2.x - (p3.x - p1.x) / 6),
      y: round(clampSmoothTraceControlY(p2.y - (p3.y - p1.y) / 6, p1.y, p2.y))
    };
    curves.push({ start: p1, control1, control2, end: p2 });
  }
  return curves;
}

function clampSmoothTraceControlY(value, segmentStartY, segmentEndY) {
  return Math.max(Math.min(segmentStartY, segmentEndY), Math.min(Math.max(segmentStartY, segmentEndY), value));
}

function graphMetricValueText(metric) {
  const state = String(metric?.state || '').toLowerCase();
  if (state === 'pit') return pitSecondsText(metric?.comparisonPit);
  if (state === 'pitlap') return pitLapText(metric?.comparisonPit);
  if (state === 'tire') return tireText(metric?.comparisonTire);
  if (state === 'stint' || state === 'last' || state === 'status') return stringOrNull(metric?.comparisonText) || '--';
  if (state === 'ready' && Number.isFinite(Number(metric?.focusGapChangeSeconds))) return formatChangeSeconds(-Number(metric.focusGapChangeSeconds));
  if (state === 'ready' && stringOrNull(metric?.stateLabel)) return stringOrNull(metric.stateLabel);
  if (state === 'warming') return stringOrNull(metric?.stateLabel) || '--';
  if (state === 'leaderchanged') return 'leader';
  return '--';
}

function graphMetricChaserText(metric) {
  const state = String(metric?.state || '').toLowerCase();
  if (state === 'pit') return pitSecondsText(metric?.threatPit);
  if (state === 'pitlap') return pitLapText(metric?.threatPit);
  if (state === 'tire') return tireText(metric?.threatTire);
  if (state === 'stint' || state === 'last' || state === 'status') return stringOrNull(metric?.threatText) || '--';
  if (state === 'ready' && metric?.chaser) {
    return `${metric.chaser.label || `#${metric.chaser.carIdx ?? '--'}`} ${formatChangeSeconds(-Number(metric.chaser.gainSeconds))}`;
  }
  if (state === 'leaderchanged') return 'reset';
  return '--';
}

function pitSecondsText(pit) {
  const seconds = Number(pit?.seconds);
  if (!Number.isFinite(seconds)) return '--';
  return seconds >= 60 ? `${Math.floor(seconds / 60)}:${Math.round(seconds % 60).toString().padStart(2, '0')}` : `${Math.round(seconds)}s`;
}

function pitLapText(pit) {
  const lap = Number(pit?.lap);
  return Number.isFinite(lap) && lap > 0 ? `L${lap}` : '--';
}

function tireText(tire) {
  return stringOrNull(tire?.shortLabel) || stringOrNull(tire?.label) || '--';
}

function formatChangeSeconds(value) {
  if (!Number.isFinite(value)) return '--';
  if (Math.abs(value) < 0.05) return '0.0';
  return `${value > 0 ? '+' : ''}${value.toFixed(1)}`;
}

function findElementBounds(layout, role, classToken) {
  const elements = Array.isArray(layout?.elements) ? layout.elements : [];
  const element = elements.find((item) =>
    item?.role === role
    && (!classToken || String(item?.className || '').split(/\s+/).includes(classToken)));
  return element?.bounds || null;
}

function offsetRect(offset, rect) {
  return {
    x: round(offset.x + rect.x),
    y: round(offset.y + rect.y),
    width: round(rect.width),
    height: round(rect.height)
  };
}

function offsetPoint(offset, point) {
  return {
    x: round(offset.x + point.x),
    y: round(offset.y + point.y)
  };
}

function colorWithAlpha(hex, alpha) {
  const value = String(hex || '').replace('#', '');
  if (!/^[0-9a-f]{6}$/i.test(value)) return hex;
  const red = Number.parseInt(value.slice(0, 2), 16);
  const green = Number.parseInt(value.slice(2, 4), 16);
  const blue = Number.parseInt(value.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${round(alpha)})`;
}

function stringOrNull(value) {
  const text = typeof value === 'string' ? value.trim() : '';
  return text || null;
}

function round(value) {
  return Math.round(Number(value || 0) * 1000) / 1000;
}

function numberOr(...values) {
  for (const value of values) {
    const number = Number(value);
    if (Number.isFinite(number)) return number;
  }
  return 0;
}

function clamp01(value) {
  return Number.isFinite(value) ? Math.max(0, Math.min(1, value)) : 0;
}

function booleanOrNull(value) {
  return typeof value === 'boolean' ? value : null;
}

function numberOrNull(value) {
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}

function arrayLength(value) {
  return Array.isArray(value) ? value.length : 0;
}

function inspectPng(path, minBytes) {
  if (!existsSync(path)) {
    throw new Error(`Screenshot missing: ${path}`);
  }
  const stats = statSync(path);
  if (stats.size < minBytes) {
    throw new Error(`Screenshot too small: ${path} (${stats.size} bytes)`);
  }

  const data = readFileSync(path);
  const pngSignature = '89504e470d0a1a0a';
  if (data.subarray(0, 8).toString('hex') !== pngSignature) {
    throw new Error(`Screenshot is not a PNG: ${path}`);
  }

  const width = data.readUInt32BE(16);
  const height = data.readUInt32BE(20);
  if (width <= 0 || height <= 0) {
    throw new Error(`Screenshot has invalid dimensions: ${path} (${width}x${height})`);
  }

  return {
    width,
    height,
    bytes: stats.size
  };
}

function startReviewServer(port) {
  const child = spawn(
    process.execPath,
    [resolve(repoRoot, 'tools/browser-review/server.mjs')],
    {
      cwd: repoRoot,
      env: {
        ...process.env,
        TMR_BROWSER_REVIEW_PORT: String(port)
      },
      stdio: ['ignore', 'pipe', 'pipe']
    });

  child.stdout.on('data', (chunk) => {
    if (args.verbose) {
      process.stderr.write(chunk);
    }
  });
  child.stderr.on('data', (chunk) => process.stderr.write(chunk));
  child.on('exit', (code, signal) => {
    if (code !== null && code !== 0) {
      process.stderr.write(`Browser review server exited with code ${code}\n`);
    } else if (signal && signal !== 'SIGTERM') {
      process.stderr.write(`Browser review server exited with signal ${signal}\n`);
    }
  });

  return child;
}

async function waitForServer(url) {
  const deadline = Date.now() + 10_000;
  let lastError = null;

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        return;
      }
      lastError = new Error(`${response.status} ${response.statusText}`);
    } catch (error) {
      lastError = error;
    }

    await new Promise((resolveTimer) => setTimeout(resolveTimer, 150));
  }

  throw new Error(`Timed out waiting for browser review server at ${url}: ${formatError(lastError)}`);
}

function parseArgs(values) {
  const parsed = {
    baseUrl: '',
    output: '',
    port: 0,
    settleMilliseconds: null,
    surface: 'browser-review',
    verbose: false
  };

  for (let index = 0; index < values.length; index++) {
    const value = values[index];
    if (value === '--base-url') {
      parsed.baseUrl = requiredValue(values, ++index, value);
    } else if (value === '--output') {
      parsed.output = requiredValue(values, ++index, value);
    } else if (value === '--port') {
      parsed.port = Number.parseInt(requiredValue(values, ++index, value), 10);
    } else if (value === '--settle-ms') {
      parsed.settleMilliseconds = Number.parseInt(requiredValue(values, ++index, value), 10);
    } else if (value === '--surface') {
      parsed.surface = requiredValue(values, ++index, value);
    } else if (value === '--verbose') {
      parsed.verbose = true;
    } else {
      throw new Error(`Unknown argument: ${value}`);
    }
  }

  if (parsed.port && !Number.isFinite(parsed.port)) {
    throw new Error('Invalid --port value.');
  }
  if (parsed.settleMilliseconds !== null && !Number.isFinite(parsed.settleMilliseconds)) {
    throw new Error('Invalid --settle-ms value.');
  }
  if (!['browser-review', 'localhost', 'all'].includes(parsed.surface)) {
    throw new Error('Invalid --surface value. Expected browser-review, localhost, or all.');
  }

  return parsed;
}

function defaultOutputFor(surface) {
  if (surface === 'localhost') {
    return 'artifacts/localhost-screenshots';
  }
  if (surface === 'all') {
    return 'artifacts/browser-localhost-screenshots';
  }
  return 'artifacts/browser-review-screenshots';
}

function serverProbePath(surface) {
  return surface === 'localhost'
    ? '/overlays/standings?preview=race'
    : '/review/app';
}

function requiredValue(values, index, flag) {
  const value = values[index];
  if (!value || value.startsWith('--')) {
    throw new Error(`${flag} requires a value.`);
  }
  return value;
}

function stripTrailingSlash(value) {
  return String(value || '').replace(/\/+$/, '');
}

function formatError(error) {
  return error instanceof Error ? error.message : String(error || 'unknown error');
}
