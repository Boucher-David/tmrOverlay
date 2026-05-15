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
const overlayIds = browserOverlayPages().map((page) => page.page.id);
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
const outputRoot = resolve(repoRoot, args.output || 'artifacts/browser-review-screenshots');
const port = args.port || Number.parseInt(process.env.TMR_BROWSER_SCREENSHOT_PORT || '5199', 10);
const baseUrl = stripTrailingSlash(args.baseUrl || `http://127.0.0.1:${port}`);
const settleMilliseconds = args.settleMilliseconds ?? 350;
let serverProcess = null;

try {
  if (!args.baseUrl) {
    serverProcess = startReviewServer(port);
  }

  await waitForServer(`${baseUrl}/review/app`);
  rmSync(outputRoot, { recursive: true, force: true });
  mkdirSync(outputRoot, { recursive: true });

  const browser = await chromium.launch();
  const context = await browser.newContext({
    viewport: { width: 1280, height: 760 },
    deviceScaleFactor: 1
  });
  const page = await context.newPage();
  const manifest = [];

  for (const route of screenshotRoutes()) {
    await captureRoute(page, route, manifest);
  }

  await browser.close();
  writeFileSync(
    join(outputRoot, 'manifest.json'),
    `${JSON.stringify({
      generatedAtUtc: new Date().toISOString(),
      baseUrl,
      screenshots: manifest
    }, null, 2)}\n`);
  console.log(`Wrote ${manifest.length} browser review screenshots to ${outputRoot}`);
} finally {
  if (serverProcess) {
    serverProcess.kill('SIGTERM');
  }
}

function screenshotRoutes() {
  const routes = [
    settingsRoute('settings/general.png', '/review/app', { tab: 'general', region: 'general' }),
    settingsRoute('settings/diagnostics.png', '/review/app?tab=support', { tab: 'support', region: 'general' }),
    ...previewModes.map((mode) =>
      settingsRoute(
        `settings/general-preview-${mode}.png`,
        `/review/app?preview=${encodeURIComponent(mode)}`,
        { tab: 'general', region: 'general', previewMode: mode }))
  ];

  for (const overlayId of overlayIds) {
    for (const region of regionsForOverlay(overlayId)) {
      const suffix = region === 'general' ? '' : `-${region}`;
      routes.push(settingsRoute(
        `settings/${overlayId}${suffix}.png`,
        `/review/app?tab=${encodeURIComponent(overlayId)}${region === 'general' ? '' : `&region=${encodeURIComponent(region)}`}`,
        { tab: overlayId, overlayId, region }));
    }
  }

  for (const overlayId of overlayIds) {
    routes.push(overlayRoute(
      `browser-overlays/${overlayId}.png`,
      withPreview(`/review/overlays/${encodeURIComponent(overlayId)}`, 'race'),
      { surface: 'browser-review-overlay', overlayId, previewMode: 'race' }));
    routes.push(overlayRoute(
      `localhost-overlays/${overlayId}.png`,
      withPreview(`/overlays/${encodeURIComponent(overlayId)}`, 'race'),
      { surface: 'localhost-overlay', overlayId, previewMode: 'race' }));
    for (const mode of previewModesForOverlay(overlayId)) {
      routes.push(overlayRoute(
        `browser-overlays/${overlayId}-${mode}.png`,
        withPreview(`/review/overlays/${encodeURIComponent(overlayId)}`, mode),
        { surface: 'browser-review-overlay', overlayId, previewMode: mode }));
      routes.push(overlayRoute(
        `localhost-overlays/${overlayId}-${mode}.png`,
        withPreview(`/overlays/${encodeURIComponent(overlayId)}`, mode),
        { surface: 'localhost-overlay', overlayId, previewMode: mode }));
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
    const text = String(node.innerText || node.textContent || '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 500);
    const selectors = [
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
    const rects = selectors
      .flatMap((selector) => Array.from(node.querySelectorAll(selector)))
      .map((element) => element.getBoundingClientRect())
      .filter((rect) => rect.width > 0 && rect.height > 0);
    if (!rects.length) {
      return { textSample: text || null, contentBounds: null };
    }

    const left = Math.min(...rects.map((rect) => rect.left));
    const top = Math.min(...rects.map((rect) => rect.top));
    const right = Math.max(...rects.map((rect) => rect.right));
    const bottom = Math.max(...rects.map((rect) => rect.bottom));
    const width = right - left;
    const height = bottom - top;
    return {
      textSample: text || null,
      contentBounds: {
        x: Math.round(left - rootRect.left),
        y: Math.round(top - rootRect.top),
        width: Math.round(width),
        height: Math.round(height),
        aspectRatio: height > 0 ? Number((width / height).toFixed(4)) : null
      }
    };
  });
}

function stringOrNull(value) {
  const text = typeof value === 'string' ? value.trim() : '';
  return text || null;
}

function booleanOrNull(value) {
  return typeof value === 'boolean' ? value : null;
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

  return parsed;
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
