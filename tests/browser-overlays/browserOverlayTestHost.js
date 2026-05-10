import { JSDOM } from 'jsdom';
import {
  browserOverlayApiResponse,
  browserOverlayPage,
  browserOverlayPages,
  freshLiveSnapshot,
  renderAppValidatorReviewHtml,
  renderOverlayHtml,
  renderOverlayIndexHtml
} from './browserOverlayAssets.js';

export {
  browserOverlayApiResponse,
  browserOverlayPage,
  browserOverlayPages,
  freshLiveSnapshot,
  renderAppValidatorReviewHtml,
  renderOverlayHtml,
  renderOverlayIndexHtml
} from './browserOverlayAssets.js';

export async function renderBrowserOverlay(name, { live, settings = {}, model = null, waitForSelector = 'table' }) {
  const fetchCalls = [];
  const dom = new JSDOM(renderOverlayHtml(name), {
    pretendToBeVisual: true,
    runScripts: 'dangerously',
    url: `http://localhost:8765/overlays/${name}`,
    beforeParse(window) {
      window.fetch = async (input) => {
        const path = new URL(String(input), window.location.href).pathname;
        fetchCalls.push(path);
        const payload = browserOverlayApiResponse(name, path, { live, settings, model });
        if (payload) {
          return jsonResponse(payload);
        }
        return { ok: false, status: 404, json: async () => ({}) };
      };
    }
  });

  if (waitForSelector) {
    await waitFor(() => dom.window.document.querySelector(waitForSelector));
  } else {
    await waitFor(() => fetchCalls.includes('/api/snapshot'));
  }

  return {
    dom,
    document: dom.window.document,
    fetchCalls,
    close: () => dom.window.close()
  };
}

export async function waitFor(assertion, timeoutMilliseconds = 1000) {
  const deadline = Date.now() + timeoutMilliseconds;
  let lastError;

  while (Date.now() < deadline) {
    try {
      const result = assertion();
      if (result) {
        return result;
      }
    } catch (error) {
      lastError = error;
    }

    await new Promise((resolveTimer) => setTimeout(resolveTimer, 10));
  }

  if (lastError) {
    throw lastError;
  }

  throw new Error('Timed out waiting for browser overlay condition.');
}

function jsonResponse(payload) {
  return {
    ok: true,
    status: 200,
    json: async () => payload
  };
}
