import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { chromium } from '@playwright/test';
import { browserOverlayPages } from '../../tests/browser-overlays/browserOverlayAssets.js';

const { baseUrl, outputDir, relativeSeconds } = parseArguments(process.argv.slice(2));
mkdirSync(outputDir, { recursive: true });

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 900, height: 600 },
  deviceScaleFactor: 1
});

const results = [];
try {
  for (const frameSeconds of relativeSeconds) {
    for (const overlay of browserOverlayPages()) {
      results.push(await validateOverlayFrame(context, overlay, frameSeconds));
    }
  }
} finally {
  await browser.close();
}

const reportPath = join(outputDir, 'race-start-overlay-validation.json');
writeFileSync(reportPath, `${JSON.stringify({
  baseUrl,
  relativeSeconds,
  generatedAtUtc: new Date().toISOString(),
  results
}, null, 2)}\n`);

const failures = results.flatMap((result) => result.failures.map((failure) => `${result.overlayId}@${result.relativeSeconds}s: ${failure}`));
console.log(`Validated ${results.length} overlay frames from ${baseUrl}`);
console.log(`Screenshots/report: ${outputDir}`);
if (failures.length > 0) {
  console.error(failures.join('\n'));
  process.exitCode = 1;
}

async function validateOverlayFrame(context, overlay, frameSeconds) {
  const page = await context.newPage();
  const consoleErrors = [];
  const pageErrors = [];
  const failedRequests = [];
  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('requestfailed', (request) => failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText || ''}`));

  const url = overlayUrl(overlay, frameSeconds);
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.overlay', { timeout: 5000 });
    await page.waitForTimeout(800);

    const screenshotPath = join(outputDir, screenshotName(overlay.page.id, frameSeconds));
    await page.screenshot({ path: screenshotPath, fullPage: true });

    const metrics = await page.evaluate(() => {
      const overlay = document.querySelector('.overlay');
      const content = document.querySelector('#content');
      const status = document.querySelector('#status');
      const timer = document.querySelector('#time-remaining');
      const box = overlay?.getBoundingClientRect();
      return {
        overlayWidth: box?.width ?? 0,
        overlayHeight: box?.height ?? 0,
        scrollWidth: document.documentElement.scrollWidth,
        scrollHeight: document.documentElement.scrollHeight,
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight,
        bodyText: document.body.innerText.trim(),
        contentText: content?.innerText.trim() ?? '',
        statusText: status?.textContent?.trim() ?? '',
        timeRemainingText: timer && !timer.hidden ? timer.textContent.trim() : '',
        tableRows: document.querySelectorAll('tbody tr').length,
        metricCount: document.querySelectorAll('.metric').length,
        canvasCount: document.querySelectorAll('canvas').length,
        svgCount: document.querySelectorAll('svg').length,
        hasRadar: Boolean(document.querySelector('.radar-v2')),
        radarChromeVisible: radarChromeVisible(),
        hasTrackMap: Boolean(document.querySelector('.track svg')),
        hasChatLine: Boolean(document.querySelector('.chat-line')),
        hasGarageCover: Boolean(document.querySelector('.garage-cover')),
        hasWaitingText: (document.body.innerText || '').toLowerCase().includes('waiting')
      };

      function radarChromeVisible() {
        const radar = document.querySelector('.radar-v2');
        if (!radar) return null;
        return ['.radar-title', '.radar-status']
          .map((selector) => document.querySelector(selector))
          .filter(Boolean)
          .every((element) => isInsideRadarCircle(radar, element));
      }

      function isInsideRadarCircle(radar, element) {
        const radarBox = radar.getBoundingClientRect();
        const elementBox = element.getBoundingClientRect();
        if (getComputedStyle(radar).overflow === 'visible') {
          return elementBox.left >= 0
            && elementBox.top >= 0
            && elementBox.right <= window.innerWidth
            && elementBox.bottom <= window.innerHeight;
        }

        const radius = Math.min(radarBox.width, radarBox.height) / 2;
        const centerX = radarBox.left + radarBox.width / 2;
        const centerY = radarBox.top + radarBox.height / 2;
        const elementY = elementBox.top + elementBox.height / 2 - centerY;
        const halfWidthAtY = Math.sqrt(Math.max(0, radius * radius - elementY * elementY));
        return elementBox.left >= centerX - halfWidthAtY + 1
          && elementBox.right <= centerX + halfWidthAtY - 1;
      }
    });
    const canvasPixels = await canvasHasVisiblePixels(page);
    const failures = validateMetrics(overlay.page.id, metrics, canvasPixels)
      .concat(consoleErrors.map((error) => `console error: ${error}`))
      .concat(pageErrors.map((error) => `page error: ${error}`))
      .concat(failedRequests.map((request) => `request failed: ${request}`));

    return {
      overlayId: overlay.page.id,
      route: overlay.route,
      relativeSeconds: frameSeconds,
      url,
      screenshotPath,
      metrics,
      canvasPixels,
      failures
    };
  } catch (error) {
    return {
      overlayId: overlay.page.id,
      route: overlay.route,
      relativeSeconds: frameSeconds,
      url,
      screenshotPath: null,
      metrics: null,
      canvasPixels: [],
      failures: [error instanceof Error ? error.message : String(error)]
    };
  } finally {
    await page.close();
  }
}

function validateMetrics(overlayId, metrics, canvasPixels) {
  const failures = [];
  if (!metrics) {
    return ['metrics missing'];
  }

  if (metrics.overlayWidth < 40 || metrics.overlayHeight < 40) {
    failures.push(`overlay box too small (${metrics.overlayWidth}x${metrics.overlayHeight})`);
  }
  if (metrics.scrollWidth > metrics.viewportWidth + 1) {
    failures.push(`horizontal overflow (${metrics.scrollWidth}px > ${metrics.viewportWidth}px)`);
  }
  if (!metrics.bodyText && !isTextlessOverlay(overlayId)) {
    failures.push('page rendered no visible text');
  }
  if (metrics.statusText === 'localhost offline') {
    failures.push('overlay reported localhost offline');
  }

  if (overlayId === 'standings' || overlayId === 'relative') {
    if (metrics.tableRows <= 0) {
      failures.push('table overlay rendered no rows');
    }
  }
  if (['fuel-calculator', 'session-weather', 'pit-service'].includes(overlayId) && metrics.metricCount <= 0) {
    failures.push('metric overlay rendered no metrics');
  }
  if (overlayId === 'input-state' && !canvasPixels.some((canvas) => canvas.selector === '.input-graph' && canvas.hasPixels)) {
    failures.push('input graph canvas is blank');
  }
  if (overlayId === 'gap-to-leader' && !canvasPixels.some((canvas) => canvas.selector === '.model-graph' && canvas.hasPixels)) {
    failures.push('gap trend canvas is blank');
  }
  if (overlayId === 'track-map' && !metrics.hasTrackMap) {
    failures.push('track map SVG missing');
  }
  if (overlayId === 'car-radar' && !metrics.hasRadar) {
    failures.push('radar body missing');
  }
  if (overlayId === 'car-radar' && metrics.radarChromeVisible === false) {
    failures.push('radar title/status is clipped by circular viewport');
  }
  if (overlayId === 'stream-chat' && !metrics.hasChatLine) {
    failures.push('stream chat status line missing');
  }
  if (overlayId === 'garage-cover' && !metrics.hasGarageCover) {
    failures.push('garage cover body missing');
  }

  return failures;
}

function isTextlessOverlay(overlayId) {
  return overlayId === 'track-map';
}

async function canvasHasVisiblePixels(page) {
  return page.evaluate(() => Array.from(document.querySelectorAll('canvas')).map((canvas) => {
    const selector = canvas.className
      ? `.${String(canvas.className).split(/\s+/).filter(Boolean).join('.')}`
      : 'canvas';
    const context = canvas.getContext('2d');
    if (!context || canvas.width <= 0 || canvas.height <= 0) {
      return { selector, width: canvas.width, height: canvas.height, hasPixels: false };
    }

    const image = context.getImageData(0, 0, canvas.width, canvas.height).data;
    for (let offset = 0; offset < image.length; offset += 4) {
      if (image[offset + 3] > 0) {
        return { selector, width: canvas.width, height: canvas.height, hasPixels: true };
      }
    }
    return { selector, width: canvas.width, height: canvas.height, hasPixels: false };
  }));
}

function overlayUrl(overlay, relativeSeconds) {
  const url = new URL(overlay.route, normalizedBaseUrl());
  url.searchParams.set('rel', String(relativeSeconds));
  return url.toString();
}

function screenshotName(overlayId, relativeSeconds) {
  const label = relativeSeconds === 0
    ? 'green'
    : `${relativeSeconds > 0 ? 'plus' : 'minus'}${Math.abs(relativeSeconds)}s`;
  return `${overlayId}-${label}.png`;
}

function normalizedBaseUrl() {
  return baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`;
}

function parseArguments(args) {
  const [rawBaseUrl, rawOutputDir, ...options] = args;
  if (!rawBaseUrl || !rawOutputDir) {
    console.error('Usage: node tools/browser-review/validate-race-start-replay.mjs <base-url> <output-dir> [--rel=-120,0,120]');
    process.exit(2);
  }

  const relOption = options.find((option) => option.startsWith('--rel='));
  const relValues = relOption
    ? relOption.slice('--rel='.length).split(',').map((value) => Number.parseFloat(value))
    : [-120, 0, 120];
  if (relValues.length === 0 || relValues.some((value) => !Number.isFinite(value))) {
    console.error(`Invalid --rel value: ${relOption}`);
    process.exit(2);
  }

  return {
    baseUrl: rawBaseUrl,
    outputDir: resolve(rawOutputDir),
    relativeSeconds: relValues
  };
}
