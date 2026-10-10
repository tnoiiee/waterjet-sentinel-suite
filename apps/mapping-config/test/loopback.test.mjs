// Stage 0.4B-1 — B1 regression: the Mapping Configuration server binds to 127.0.0.1 only.
// No environment variable, argument or configuration can change the bind host, and no
// public-preview bypass exists. Only GET and HEAD are accepted.
import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createServer as createNetServer } from 'node:net';
import { spawn } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createUiServer, UI_BIND_HOST } from '../server.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const SERVER_PATH = join(HERE, '..', 'server.mjs');
const serverSrc = readFileSync(SERVER_PATH, 'utf8');
// A name that cannot be listened on. If the server still read a bind host from the
// environment, the child process below would fail to start.
const NON_LOOPBACK_HOST = 'not-loopback.invalid';

const open = [];
after(() => { for (const s of open) s.close(); });

function freePort() {
  return new Promise((resolve, reject) => {
    const probe = createNetServer();
    probe.once('error', reject);
    probe.listen(0, UI_BIND_HOST, () => {
      const { port } = probe.address();
      probe.close(() => resolve(port));
    });
  });
}

// Loopback URLs are built from a loopback literal, then the port is set (as in ui.test.mjs).
function loopbackUrl(port, path = '/') {
  const u = new URL('http://127.0.0.1');
  u.port = String(port);
  u.pathname = path;
  return u.href;
}

function listenLoopback(server) {
  return new Promise((resolve) => server.listen(0, UI_BIND_HOST, () => resolve(server.address().port)));
}

function firstMatchingLine(child, pattern, timeoutMs) {
  return new Promise((resolve, reject) => {
    let buf = '';
    const timer = setTimeout(() => reject(new Error('timed out waiting for the server line')), timeoutMs);
    child.stdout.on('data', (chunk) => {
      buf += chunk.toString('utf8');
      const hit = buf.split('\n').find((l) => pattern.test(l));
      if (hit) { clearTimeout(timer); resolve(hit.trim()); }
    });
    child.once('exit', (code) => { clearTimeout(timer); reject(new Error(`server exited early (${code})`)); });
  });
}

test('the bind host is the constant 127.0.0.1', () => {
  assert.equal(UI_BIND_HOST, '127.0.0.1');
});

test('the real server binds exactly to 127.0.0.1, even when MAPPING_UI_HOST is set', async () => {
  const port = await freePort();
  const child = spawn(process.execPath, [SERVER_PATH], {
    env: { MAPPING_UI_PORT: String(port), MAPPING_UI_HOST: NON_LOOPBACK_HOST },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  try {
    const line = await firstMatchingLine(child, /listening on /, 15000);
    assert.equal(line, `Mapping Configuration (SIMULATION ONLY) listening on 127.0.0.1:${port}`);
    const r = await fetch(loopbackUrl(port));
    assert.equal(r.status, 200);
  } finally {
    child.kill();
  }
});

test('the server source reads no bind host from the environment', () => {
  assert.doesNotMatch(serverSrc, /MAPPING_UI_HOST/);
  assert.doesNotMatch(serverSrc, /env\s*(\.|\[)[^\n]*HOST/i);
});

test('listen is called only with the constant bind host (no omitted host)', () => {
  const hosts = [...serverSrc.matchAll(/\.listen\(([^,]+),\s*([^,]+),/g)].map((m) => m[2].trim());
  assert.deepEqual(hosts, ['UI_BIND_HOST']);
});

test('the server source contains no all-interface bind literal', () => {
  assert.doesNotMatch(serverSrc, /0\.0\.0\.0/);
  assert.doesNotMatch(serverSrc, /['"`]::['"`]/);
});

test('only GET and HEAD are accepted; every other method is refused with 405', async () => {
  const server = createUiServer();
  open.push(server);
  const port = await listenLoopback(server);
  for (const method of ['OPTIONS', 'POST', 'PUT', 'PATCH', 'DELETE']) {
    const r = await fetch(loopbackUrl(port), { method, body: method === 'DELETE' ? undefined : '{}' });
    assert.equal(r.status, 405, method);
  }
  const head = await fetch(loopbackUrl(port), { method: 'HEAD' });
  assert.equal(head.status, 200);
});

test('no public-preview bypass: no CORS allow header, and framing is denied', async () => {
  const server = createUiServer();
  open.push(server);
  const port = await listenLoopback(server);
  const r = await fetch(loopbackUrl(port), { headers: { Origin: 'x-loopback-probe' } });
  assert.equal(r.headers.get('access-control-allow-origin'), null);
  assert.match(r.headers.get('content-security-policy') ?? '', /frame-ancestors 'none'/);
});
