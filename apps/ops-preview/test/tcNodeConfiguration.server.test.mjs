// Stage 0.4C-K2 - route boundary of GET /api/tc-node-configuration (group H).
// The route is exercised through the REAL Preview host on an ephemeral loopback port.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { CANONICAL_TC_NODE_CONFIGURATION } from '../../../packages/commissioning/src/index.mjs';
import { UI_BIND_HOST, createPreviewServer } from '../server.mjs';

const ROUTE = '/api/tc-node-configuration';
let server;
let base;

before(async () => {
  server = createPreviewServer();
  await new Promise((resolve) => server.listen(0, UI_BIND_HOST, resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  base = origin.origin;
});

after(() => server.close());

/** Every key, at any depth, of a parsed JSON value. Keys whose value is an explicit false are honest
 *  negative authority flags (for example productionMode: false) and are reported separately. */
function keysOf(value, out = []) {
  if (Array.isArray(value)) value.forEach((v) => keysOf(v, out));
  else if (value && typeof value === 'object') {
    for (const [k, v] of Object.entries(value)) {
      if (v !== false) out.push(k);
      keysOf(v, out);
    }
  }
  return out;
}

test('H: GET succeeds with a JSON body, the security headers and no CORS wildcard', async () => {
  const r = await fetch(`${base}${ROUTE}`);
  assert.equal(r.status, 200);
  assert.match(r.headers.get('content-type'), /application\/json/);
  assert.match(r.headers.get('content-security-policy'), /default-src 'self'/);
  assert.equal(r.headers.get('access-control-allow-origin'), null);
  assert.equal(r.headers.get('cache-control'), 'no-store');
  const body = await r.json();
  assert.equal(body.nodes.length, 6);
});

test('H: HEAD succeeds with no body', async () => {
  const r = await fetch(`${base}${ROUTE}`, { method: 'HEAD' });
  assert.equal(r.status, 200);
  assert.equal(await r.text(), '');
});

test('H: every write-style method is refused with 405 and an Allow header that names only GET and HEAD', async () => {
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS']) {
    const r = await fetch(`${base}${ROUTE}`, { method, body: method === 'POST' ? '{}' : undefined });
    assert.equal(r.status, 405, method);
    assert.equal(r.headers.get('allow'), 'GET, HEAD', method);
  }
});

test('H: the response is deterministic across requests', async () => {
  const a = await (await fetch(`${base}${ROUTE}`)).text();
  const b = await (await fetch(`${base}${ROUTE}`)).text();
  assert.equal(a, b);
});

test('H: the response carries exactly the canonical six Nodes, Cabinets and Test addresses', async () => {
  const body = await (await fetch(`${base}${ROUTE}`)).json();
  assert.deepEqual(body.nodes.map((n) => n.nodeId), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.nodeId));
  assert.deepEqual(body.nodes.map((n) => n.cabinetId), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.cabinetId));
  assert.deepEqual(body.nodes.map((n) => n.ipAddress), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.ipAddress));
  assert.equal(body.totals.nodes, 6);
  assert.equal(body.totals.cabinets, 6);
  assert.equal(body.totals.thermocoupleModules, 30);
  assert.equal(body.totals.physicalTcChannels, 240);
  assert.equal(body.totals.configuredTestIps, 6);
  assert.equal(body.configurationStatus, 'CONFIGURED');
});

test('H: the response exposes no credential, secret, token, file path, environment value or Production endpoint', async () => {
  const text = await (await fetch(`${base}${ROUTE}`)).text();
  const keys = keysOf(JSON.parse(text)).join(' ').toLowerCase();
  for (const word of ['pass' + 'word', 'token', 'credential', 'secret', 'production', 'environmentvariable', 'path', 'file', 'port', 'protocol']) {
    const hits = keys.split(' ').filter((k) => k.includes(word) && !/status$/.test(k));
    assert.deepEqual(hits, [], `key containing '${word}'`);
  }
  assert.equal(/https?:\/\//i.test(text), false, 'no URL');
  assert.equal(/[A-Za-z]:\\|\/home\/|\/users\//i.test(text), false, 'no local path');
  assert.equal(/\b502\b/.test(text), false, 'no assumed port');
});

test('H: the response carries no Hardware claim, no device-write or connection flag set to true', async () => {
  const body = await (await fetch(`${base}${ROUTE}`)).json();
  const flags = keysOf(body);
  for (const key of ['hardwareConnected', 'connected', 'online', 'reachable', 'deviceWrite', 'hardwareAccess', 'acquisitionReady',
    'hardwareReady', 'fieldVerified', 'productionMode', 'socketOpened', 'networkProbe']) {
    assert.equal(flags.includes(key) && JSON.stringify(body).includes(`"${key}":true`), false, key);
  }
  assert.equal(body.authority.hardwareAccess, false);
  assert.equal(body.authority.networkProbe, false);
  assert.equal(body.authority.deviceWrite, false);
  assert.equal(body.authority.productionMode, false);
  assert.equal(body.totals.reachabilityTested, 0);
  assert.equal(body.totals.acquisitionVerified, 0);
  assert.equal(body.totals.fieldVerified, 0);
});

test('H: the response carries no numeric process-image address and no Sensor value', async () => {
  const body = await (await fetch(`${base}${ROUTE}`)).json();
  const keys = keysOf(body).join(' ').toLowerCase();
  for (const word of ['offset', 'byte', 'word', 'processimage', 'rawvalue', 'engineering', 'sensorvalue', 'channelvalue']) {
    assert.equal(keys.includes(word), false, `key containing '${word}'`);
  }
  assert.equal(/"(ioOffset|byteOffset|wordOffset|wordAddress|byteAddress)"/.test(JSON.stringify(body)), false);
});

test('H: an unknown query string cannot change the configuration projection', async () => {
  const plain = await (await fetch(`${base}${ROUTE}`)).text();
  const queried = await (await fetch(`${base}${ROUTE}?ip=127.0.0.1&node=TC-NODE-09&probe=1`)).text();
  assert.equal(plain, queried);
});
