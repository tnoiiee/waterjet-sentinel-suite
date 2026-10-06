// WJSS Stage 0.2.1A — Node synthetic runtime harness HTTP server (loopback only).
//
// SYNTHETIC SPIKE INFRASTRUCTURE. Not the Production ASP.NET Core Local Application API.
// ASP.NET Core integration, Windows Service behaviour, and WebView2 kiosk behaviour are
// NOT VERIFIED by this harness.
//
// Endpoints:
//   GET  /api/stream               SSE: snapshot first, then deltas
//   GET  /api/snapshot             current OperationalSnapshot (JSON)
//   POST /api/spike/close-request  synthetic close-guard evaluation (no token)
//   POST /api/spike/scenario       synthetic scenario command (X-Spike-Token required)
//   GET  /api/spike/metrics        harness metrics
//   GET  /api/spike/poll-plan      synthetic poll plan
//   GET  /api/spike/test-controls  ONLY with --synthetic-test-controls: per-run scenario token for
//                                  the same-origin spike UI (Owner review tooling; NOT an
//                                  authentication model; 404 when the flag is off)
//   GET  /healthz                  liveness
//   GET  /*                        static React build (react-ui/dist), when present

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { assertLoopbackHost } from './config.mjs';
import { SyntheticRuntime } from './runtime.mjs';
import { SsePublisher } from './sse.mjs';

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.json': 'application/json; charset=utf-8',
  '.ico': 'image/x-icon',
  '.png': 'image/png',
  '.woff2': 'font/woff2',
};
const SECURITY_HEADERS = {
  'X-Content-Type-Options': 'nosniff',
  'Referrer-Policy': 'no-referrer',
  'Content-Security-Policy': "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'",
};

function sendJson(res, status, body) {
  const json = JSON.stringify(body);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store', ...SECURITY_HEADERS });
  res.end(json);
}

function readBody(req, limit = 16 * 1024) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on('data', (c) => {
      size += c.length;
      if (size > limit) {
        reject(new Error('BODY_TOO_LARGE'));
        req.destroy();
      } else chunks.push(c);
    });
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

/**
 * Same-origin, loopback-host check for the test-controls token endpoint. Browsers send
 * Sec-Fetch-Site; a cross-site page is refused. The Host header must name a loopback host
 * (defence against DNS rebinding). Non-browser local clients (no Sec-Fetch-Site) are allowed:
 * they can already read the git-ignored .run-token file.
 */
function testControlsRequestAllowed(req, port) {
  const site = req.headers['sec-fetch-site'];
  if (site !== undefined && site !== 'same-origin' && site !== 'none') return false;
  const host = String(req.headers.host ?? '');
  return [`127.0.0.1:${port}`, `localhost:${port}`, `[::1]:${port}`].includes(host);
}

/**
 * @param {{ port?: number, host?: string, token?: string, staticDir?: string|null, params?: object, testControls?: boolean }} opts
 */
export function createHarness(opts = {}) {
  const host = assertLoopbackHost(opts.host ?? '127.0.0.1');
  const token = opts.token ?? crypto.randomBytes(16).toString('hex');
  const runtime = new SyntheticRuntime(opts.params ?? {});
  const sse = new SsePublisher({ maxBufferedBytes: runtime.params.sseMaxBufferedBytes, heartbeatMs: runtime.params.heartbeatMs });
  runtime.sseClientCount = () => sse.clients.size;
  runtime.on('delta', (delta, json) => sse.broadcastDelta(delta.revision, json));
  const staticDir = opts.staticDir ? path.resolve(opts.staticDir) : null;
  // Opt-in Owner review tooling (Diagnostics "SYNTHETIC TEST CONTROL"). Off by default.
  const testControls = opts.testControls === true;

  const server = http.createServer(async (req, res) => {
    const url = new URL(req.url ?? '/', 'http://127.0.0.1');
    try {
      if (req.method === 'GET' && url.pathname === '/api/stream') {
        const { json } = runtime.snapshot();
        sse.accept(req, res, json, runtime.revision);
        return;
      }
      if (req.method === 'GET' && url.pathname === '/api/snapshot') return sendJson(res, 200, runtime.snapshot().snap);
      if (req.method === 'GET' && url.pathname === '/healthz') return sendJson(res, 200, { ok: true, synthetic: true, revision: runtime.revision });
      if (req.method === 'GET' && url.pathname === '/api/spike/metrics') return sendJson(res, 200, { ...runtime.metricsReport(), sse: { clients: sse.clients.size, ...sse.stats } });
      if (req.method === 'GET' && url.pathname === '/api/spike/poll-plan') return sendJson(res, 200, { label: runtime.params.label, plan: runtime.plan });
      if (req.method === 'POST' && url.pathname === '/api/spike/close-request') return sendJson(res, 200, runtime.closeRequest());
      if (testControls && req.method === 'GET' && url.pathname === '/api/spike/test-controls') {
        if (!testControlsRequestAllowed(req, server.address()?.port)) return sendJson(res, 403, { enabled: true, reason: 'SAME_ORIGIN_LOOPBACK_REQUIRED' });
        // Per-run random token (memory only in the UI); synthetic scenario API scope only.
        return sendJson(res, 200, { enabled: true, synthetic: true, scope: 'synthetic-scenario-api', token, note: 'Spike review tooling only. Not an authentication model. Per-run token, never persisted.' });
      }
      if (req.method === 'POST' && url.pathname === '/api/spike/scenario') {
        const supplied = String(req.headers['x-spike-token'] ?? '');
        const a = Buffer.from(supplied);
        const b = Buffer.from(token);
        if (a.length !== b.length || !crypto.timingSafeEqual(a, b)) return sendJson(res, 403, { accepted: false, reason: 'INVALID_SPIKE_TOKEN' });
        let body;
        try {
          body = JSON.parse((await readBody(req)) || '{}');
        } catch {
          return sendJson(res, 400, { accepted: false, reason: 'INVALID_JSON' });
        }
        if (typeof body.command !== 'string') return sendJson(res, 400, { accepted: false, reason: 'MISSING_COMMAND' });
        if (body.command === 'drop-clients') return sendJson(res, 200, { accepted: true, command: 'drop-clients', detail: { dropped: sse.dropAll() } });
        if (body.command === 'inject-revision-gap') {
          sse.skipNextBroadcast = true;
          runtime.requestPublish();
          return sendJson(res, 200, { accepted: true, command: 'inject-revision-gap' });
        }
        return sendJson(res, 200, runtime.command(body.command, body.params));
      }
      // Unknown API routes are a JSON 404 (never the SPA fallback): e.g. /api/spike/test-controls
      // without --synthetic-test-controls must read as "absent", not as index.html.
      if (url.pathname.startsWith('/api/')) return sendJson(res, 404, { error: 'NOT_FOUND' });
      if (req.method === 'GET' && staticDir) return serveStatic(staticDir, url.pathname, res);
      sendJson(res, 404, { error: 'NOT_FOUND' });
    } catch (err) {
      sendJson(res, 500, { error: 'INTERNAL', message: String(err?.message ?? err) });
    }
  });

  return {
    runtime,
    sse,
    token,
    server,
    host,
    testControls,
    async start() {
      runtime.start();
      await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(opts.port ?? 5181, host, resolve);
      });
      const addr = server.address();
      // Defence in depth: verify the bound address is loopback.
      assertLoopbackHost(addr.address);
      this.port = addr.port;
      this.url = `http://${host === '::1' ? '[::1]' : host}:${addr.port}`;
      return this;
    },
    async stop() {
      sse.close();
      runtime.stop();
      server.closeAllConnections?.();
      await new Promise((resolve) => server.close(() => resolve()));
    },
  };
}

function serveStatic(root, pathname, res) {
  let rel = decodeURIComponent(pathname);
  if (rel === '/' || rel === '') rel = '/index.html';
  const full = path.resolve(root, '.' + rel);
  if (!full.startsWith(root + path.sep) && full !== root) return sendJson(res, 403, { error: 'FORBIDDEN' });
  let file = full;
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(root, 'index.html');
  if (!fs.existsSync(file)) return sendJson(res, 404, { error: 'BUILD_NOT_FOUND', hint: 'Run npm run build in react-ui first.' });
  const ext = path.extname(file);
  res.writeHead(200, {
    'Content-Type': MIME[ext] ?? 'application/octet-stream',
    'Cache-Control': file.includes(`${path.sep}assets${path.sep}`) ? 'public, max-age=31536000, immutable' : 'no-cache',
    ...SECURITY_HEADERS,
  });
  fs.createReadStream(file).pipe(res);
}
