import { expect, test } from '@playwright/test';
import {
  browserOverlayApiResponse,
  freshLiveSnapshot,
  renderOverlayHtml
} from './browserOverlayTestHost.js';
import {
  renderAppValidatorReviewHtml,
  renderSettingsGeneralReviewHtml
} from './browserOverlayAssets.js';

test.describe('browser overlay Playwright integration', () => {
  test('renders standings in a real browser layout without horizontal overflow', async ({ page }) => {
    const requests = await installBrowserOverlayRoutes(page, 'standings', {
      live: freshLiveSnapshot({}),
      model: standingsDisplayModel()
    });

    await page.setViewportSize({ width: 692, height: 520 });
    await page.goto('http://localhost:8765/overlays/standings');

    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(6);
    await expect(page.locator('#status')).toHaveText('scoring | 5/5 live');
    await expect(rows.nth(4)).toHaveClass(/focus/);

    const overlayBox = await page.locator('.overlay').boundingBox();
    expect(overlayBox?.width).toBeGreaterThan(480);
    expect(overlayBox?.height).toBeGreaterThan(180);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    expect(requests).toContain('/api/overlay-model/standings');
    expect(requests).toContain('/api/snapshot');
  });

  test('renders stream chat unavailable state without telemetry fade', async ({ page }) => {
    const requests = await installBrowserOverlayRoutes(page, 'stream-chat', {
      live: {
        isConnected: false,
        isCollecting: false,
        lastUpdatedAtUtc: null,
        sequence: 0,
        models: {}
      },
      settings: {
        provider: 'none',
        isConfigured: false,
        streamlabsWidgetUrl: null,
        twitchChannel: null,
        status: 'not_configured'
      }
    });

    await page.setViewportSize({ width: 380, height: 520 });
    await page.goto('http://localhost:8765/overlays/stream-chat');

    await expect(page.locator('.chat-line')).toHaveCount(1);
    await expect(page.locator('.chat-name')).toHaveText('TMR');
    await expect(page.locator('.chat-text')).toHaveText('Choose Streamlabs or Twitch in the Stream Chat settings tab.');
    await expect(page.locator('#status')).toHaveText('waiting for chat source');
    await expect(page.locator('.overlay')).toHaveCSS('opacity', '1');
    expect(requests).toContain('/api/stream-chat');
  });

  test('keeps input graph smoothing inside each trace segment', async ({ page }) => {
    await page.addInitScript(() => {
      window.__tmrBezierCalls = [];
      const originalMoveTo = CanvasRenderingContext2D.prototype.moveTo;
      const originalLineTo = CanvasRenderingContext2D.prototype.lineTo;
      const originalBezierCurveTo = CanvasRenderingContext2D.prototype.bezierCurveTo;
      CanvasRenderingContext2D.prototype.moveTo = function patchedMoveTo(x, y) {
        this.__tmrCurrentPoint = { x, y };
        return originalMoveTo.call(this, x, y);
      };
      CanvasRenderingContext2D.prototype.lineTo = function patchedLineTo(x, y) {
        this.__tmrCurrentPoint = { x, y };
        return originalLineTo.call(this, x, y);
      };
      CanvasRenderingContext2D.prototype.bezierCurveTo = function patchedBezierCurveTo(c1x, c1y, c2x, c2y, x, y) {
        window.__tmrBezierCalls.push({
          startY: this.__tmrCurrentPoint?.y,
          c1y,
          c2y,
          endY: y
        });
        this.__tmrCurrentPoint = { x, y };
        return originalBezierCurveTo.call(this, c1x, c1y, c2x, c2y, x, y);
      };
    });

    await installBrowserOverlayRoutes(page, 'input-state', {
      live: [0.12, 1, 1, 0.12, 0.88, 0, 0, 0.88].map((throttle, index) =>
        inputStateLiveSnapshot(index, throttle)),
      settings: {
        showThrottle: true,
        showBrake: true,
        showClutch: true,
        showSteering: false,
        showGear: false,
        showSpeed: false
      }
    });

    await page.setViewportSize({ width: 460, height: 220 });
    await page.goto('http://localhost:8765/overlays/input-state');
    await expect(page.locator('.input-graph')).toBeVisible();
    await expect.poll(
      async () => page.evaluate(() => window.__tmrBezierCalls?.length ?? 0),
      { timeout: 3500 }
    ).toBeGreaterThanOrEqual(12);

    const violations = await page.evaluate(() => {
      const tolerance = 0.001;
      return (window.__tmrBezierCalls || []).filter((call) => {
        if (!Number.isFinite(call.startY) || !Number.isFinite(call.endY)) {
          return false;
        }

        const min = Math.min(call.startY, call.endY) - tolerance;
        const max = Math.max(call.startY, call.endY) + tolerance;
        return call.c1y < min || call.c1y > max || call.c2y < min || call.c2y > max;
      });
    });
    expect(violations).toEqual([]);
  });

  test('renders General settings preview controls without forcing hidden overlays', async ({ page }) => {
    await page.route('**/*', async (route) => {
      const url = new URL(route.request().url());
      if (url.hostname === 'localhost' && url.pathname === '/review/settings/general') {
        await route.fulfill({
          status: 200,
          contentType: 'text/html; charset=utf-8',
          body: renderSettingsGeneralReviewHtml({ previewMode: 'off' })
        });
        return;
      }

      await route.fulfill({
        status: 404,
        contentType: 'text/plain; charset=utf-8',
        body: 'not found'
      });
    });

    await page.setViewportSize({ width: 1180, height: 720 });
    await page.goto('http://localhost:8765/review/settings/general');

    await expect(page.locator('h1')).toHaveText('General');
    await expect(page.locator('.nav-item')).toHaveCount(15);
    await expect(page.locator('#preview-state')).toHaveText('Preview off');
    await expect(page.locator('.overlay-frame')).toHaveCount(11);
    await expect(page.getByRole('button', { name: 'Race' })).toHaveAttribute('aria-pressed', 'false');

    await page.getByRole('button', { name: 'Race' }).click();

    await expect(page.locator('#preview-state')).toHaveText('Race preview active');
    await expect(page.getByRole('button', { name: 'Race' })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.locator('.standings-frame')).toHaveAttribute('src', '/review/overlays/standings?preview=race');
    await expect(page.locator('.chat-frame')).toHaveAttribute('src', '/review/overlays/stream-chat?preview=race');
    await expect(page.locator('.garage-frame')).toHaveAttribute('src', '/review/overlays/garage-cover?preview=race');
    await expect(page.locator('.preview-rules')).toContainText('Hidden overlays stay hidden; Stream Chat is not forced open.');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  });

  test('renders application validator with the whole overlay catalog', async ({ page }) => {
    await page.route('**/*', async (route) => {
      const url = new URL(route.request().url());
      if (url.hostname === 'localhost' && url.pathname === '/review/app') {
        await route.fulfill({
          status: 200,
          contentType: 'text/html; charset=utf-8',
          body: renderAppValidatorReviewHtml({ previewMode: 'qualifying' })
        });
        return;
      }

      await route.fulfill({
        status: 404,
        contentType: 'text/plain; charset=utf-8',
        body: 'not found'
      });
    });

    await page.setViewportSize({ width: 1180, height: 760 });
    await page.goto('http://localhost:8765/review/app?preview=qualifying');

    await expect(page.locator('h1')).toHaveText('Application Validator');
    await expect(page.locator('.nav-item.active')).toHaveText('App Validator');
    await expect(page.locator('.overlay-frame')).toHaveCount(11);
    await expect(page.locator('.standings-frame')).toHaveAttribute('src', '/review/overlays/standings?preview=qualifying');
    await expect(page.locator('.input-frame')).toHaveAttribute('src', '/review/overlays/input-state?preview=qualifying');
    await expect(page.locator('.preview-stage')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  });

  test('preserves preview mode on browser overlay API calls', async ({ page }) => {
    const requests = await installBrowserOverlayRoutes(page, 'standings', {
      live: freshLiveSnapshot({}),
      model: standingsDisplayModel()
    });

    await page.setViewportSize({ width: 692, height: 520 });
    await page.goto('http://localhost:8765/overlays/standings?preview=race');
    await expect(page.locator('tbody tr')).toHaveCount(6);

    expect(requests).toContain('/api/snapshot?preview=race');
    expect(requests).toContain('/api/overlay-model/standings?preview=race');
  });
});

async function installBrowserOverlayRoutes(page, overlayId, fixture) {
  const requests = [];
  let liveFrameIndex = 0;
  await page.route('**/*', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    requests.push(url.pathname);
    if (url.search) {
      requests.push(`${url.pathname}${url.search}`);
    }

    if (url.hostname === 'localhost' && url.pathname === `/overlays/${overlayId}`) {
      await route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: renderOverlayHtml(overlayId)
      });
      return;
    }

    const payload = browserOverlayApiResponse(overlayId, url.pathname, {
      ...fixture,
      live: resolveLiveFixture(fixture.live, url.pathname, liveFrameIndex)
    });
    if (url.pathname === '/api/snapshot') {
      liveFrameIndex += 1;
    }
    if (payload) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json; charset=utf-8',
        body: JSON.stringify(payload)
      });
      return;
    }

    if (url.hostname === 'localhost') {
      await route.fulfill({
        status: 404,
        contentType: 'application/json; charset=utf-8',
        body: '{}'
      });
      return;
    }

    await route.abort();
  });

  return requests;
}

function resolveLiveFixture(live, path, frameIndex) {
  if (path !== '/api/snapshot') {
    return Array.isArray(live) ? live[0] : typeof live === 'function' ? live(0) : live;
  }

  if (Array.isArray(live)) {
    return live[Math.min(frameIndex, live.length - 1)];
  }

  return typeof live === 'function' ? live(frameIndex) : live;
}

function inputStateLiveSnapshot(index, throttle) {
  return {
    ...freshLiveSnapshot({
      raceEvents: {
        hasData: true,
        isOnTrack: true,
        isInGarage: false
      },
      inputs: {
        hasData: true,
        quality: 'raw',
        throttle,
        brake: 1,
        clutch: 0,
        steeringWheelAngle: 0,
        gear: 3,
        speedMetersPerSecond: 60,
        brakeAbsActive: false
      }
    }),
    sequence: 100 + index
  };
}

function standingsDisplayModel() {
  return {
    overlayId: 'standings',
    title: 'Standings',
    status: 'scoring | 5/5 live',
    source: 'source: scoring snapshot + live timing',
    bodyKind: 'table',
    columns: [
      { id: 'standings.class-position', label: 'CLS', dataKey: 'class-position', width: 35, alignment: 'right' },
      { id: 'standings.car-number', label: 'CAR', dataKey: 'car-number', width: 50, alignment: 'right' },
      { id: 'standings.driver', label: 'Driver', dataKey: 'driver', width: 250, alignment: 'left' },
      { id: 'standings.gap', label: 'GAP', dataKey: 'gap', width: 60, alignment: 'right' },
      { id: 'standings.interval', label: 'INT', dataKey: 'interval', width: 60, alignment: 'right' },
      { id: 'standings.pit', label: 'PIT', dataKey: 'pit', width: 30, alignment: 'right' }
    ],
    rows: [
      headerRow('LMP2', '2 cars | ~10 laps', '#33CEFF'),
      carRow(['1', '#8', 'Proto One', 'Leader', '-45.0', '']),
      headerRow('GT3', '3 cars | ~12.4 laps', '#FFAA00'),
      carRow(['1', '#11', 'GT3 Leader', 'Leader', '-2.0', '']),
      carRow(['2', '#71', 'Focus Racer', '+3.4', '0.0', ''], { isReference: true }),
      carRow(['3', '#91', 'Chaser', '+8.9', '+5.5', 'IN'], { isPit: true })
    ],
    metrics: []
  };
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
