import { readFileSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { resolve } from 'node:path';
import {
  browserAssetRoot,
  renderOverlayHtml,
  renderOverlayIndexHtml
} from '../../tests/browser-overlays/browserOverlayAssets.js';

const replayPath = resolve(process.argv[2] || process.env.TMR_STANDINGS_REPLAY_JSON || '');
const port = Number.parseInt(process.env.TMR_STANDINGS_REPLAY_PORT || '5187', 10);
const frameMilliseconds = Number.parseInt(process.env.TMR_STANDINGS_REPLAY_FRAME_MS || '500', 10);
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

    if (path === '/overlays/standings' || path === '/review/overlays/standings') {
      serveHtml(response, renderOverlayHtml('standings'));
      return;
    }

    if (path === '/api/snapshot') {
      const { index } = currentFrame();
      serveJson(response, {
        live: {
          isConnected: true,
          isCollecting: true,
          sourceId: replay.source?.captureId || 'standings-replay',
          startedAtUtc: replay.source?.startedAtUtc || null,
          lastUpdatedAtUtc: new Date().toISOString(),
          sequence: index + 1,
          context: {},
          combo: {},
          latestSample: null,
          fuel: {},
          proximity: {},
          leaderGap: {},
          models: {}
        }
      });
      return;
    }

    if (path === '/api/overlay-model/standings') {
      const { frame, index } = currentFrame();
      serveJson(response, {
        generatedAtUtc: new Date().toISOString(),
        replay: frameMetadata(frame, index),
        model: frame.model
      });
      return;
    }

    if (path === '/api/standings') {
      serveJson(response, {
        generatedAtUtc: new Date().toISOString(),
        standingsSettings: {
          maximumRows: 14,
          classSeparatorsEnabled: true,
          otherClassRowsPerClass: 2,
          columns: replay.frames[0]?.model?.columns || []
        }
      });
      return;
    }

    if (path === '/api/replay/status') {
      const { frame, index } = currentFrame();
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
  console.log(`Replay status:         http://127.0.0.1:${port}/api/replay/status`);
  console.log(`Replay source:         ${replayPath}`);
  console.log(`Replay frames:         ${replay.frames.length}`);
  console.log(`Frame interval:        ${frameMilliseconds}ms`);
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

function currentFrame() {
  const elapsed = Math.max(0, Date.now() - startedAtMs);
  const index = Math.floor(elapsed / Math.max(1, frameMilliseconds)) % replay.frames.length;
  return { index, frame: replay.frames[index] };
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
