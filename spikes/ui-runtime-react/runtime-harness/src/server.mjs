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
 * @param {{ port?: number, host?: string, token?: string, staticDir?: string|null, params?: object }} opts
 */
export function createHarness(opts = {}) {
  const host = assertLoopbackHost(opts.host ?? '127.0.0.1');
  const token = opts.token ?? crypto.randomBytes(16).toString('hex');
  const runtime = new SyntheticRuntime(opts.params ?? {});
  const sse = new SsePublisher({ maxBufferedBytes: runtime.params.sseMaxBufferedBytes, heartbeatMs: runtime.params.heartbeatMs });
  runtime.sseClientCount = () => sse.clients.size;
  runtime.on('delta', (delta, json) => sse.broadcastDelta(delta.revision, json));
  const staticDir = opts.staticDir ? path.resolve(opts.staticDir) : null;

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
