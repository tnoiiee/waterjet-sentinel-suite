// Stage 0.4C-K1 - Preview host tests (Stage task section 16, groups A and E).
// The host must be loopback-only, read-only, and free of any device, transport or control route.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { BOUNDARY, DEFAULT_PORT, STAGE_ID, UI_BIND_HOST, createPreviewServer } from '../server.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const PUBLIC = join(HERE, '..', 'public');

let server;
let base;
let bound;

before(async () => {
  server = createPreviewServer();
  await new Promise((resolve) => server.listen(0, UI_BIND_HOST, resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  base = origin.origin;
  bound = server.address();
});

after(() => server.close());

test('the host binds the loopback address only', () => {
  assert.equal(UI_BIND_HOST, '127.0.0.1');
  assert.equal(bound.address, '127.0.0.1');
  assert.equal(typeof DEFAULT_PORT, 'number');
  const source = readFileSync(join(HERE, '..', 'server.mjs'), 'utf8');
  // The approved origin is the only URL literal in the file, and it is the loopback literal.
  const urls = source.match(/https?:\/\/[^\s"'`)]+/g) ?? [];
  assert.ok(urls.length > 0);
  for (const url of urls) assert.match(url, /^http:\/\/127\.0\.0\.1/);
});

test('the Operations route and its assets are served with a restrictive policy and no CORS wildcard', async () => {
  const r = await fetch(`${base}/`);
  assert.equal(r.status, 200);
  assert.match(r.headers.get('content-type'), /text\/html/);
  assert.match(r.headers.get('content-security-policy'), /default-src 'self'/);
  assert.match(r.headers.get('content-security-policy'), /frame-ancestors 'none'/);
  assert.equal(r.headers.get('x-content-type-options'), 'nosniff');
  assert.equal(r.headers.get('access-control-allow-origin'), null);
  const html = await r.text();
  assert.match(html, /id="monitor-table-body"/);
  for (const asset of ['/app.mjs', '/styles.css', '/pkg/index.mjs', '/pkg/scenarios.mjs', '/pkg/presentation.mjs']) {
    const a = await fetch(`${base}${asset}`);
    assert.equal(a.status, 200, asset);
  }
  const head = await fetch(`${base}/`, { method: 'HEAD' });
  assert.equal(head.status, 200);
  const missing = await fetch(`${base}/pkg/not-a-module.mjs`);
  assert.equal(missing.status, 404);
});

test('every non-GET method is refused on every route: the host has no write surface at all', async () => {
  const paths = ['/', '/app.mjs', '/api/health', '/api/boundary', '/api/commissioning/schedule',
    '/api/commissioning/scenarios', '/api/commissioning/observations', '/api/operations/summary', '/api/sensor-map'];
  for (const path of paths) {
    for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
      const r = await fetch(`${base}${path}`, { method, body: method === 'DELETE' ? undefined : 'x' });
      assert.equal(r.status, 405, `${method} ${path}`);
      assert.equal(r.headers.get('allow'), 'GET, HEAD');
    }
  }
});

test('there is no device, transport or control route', async () => {
  for (const path of ['/api/devices', '/api/hardware', '/api/commands', '/api/activate', '/api/queue',
    '/api/jobs', '/api/cleaning', '/api/pump', '/api/valves', '/api/motion']) {
    const r = await fetch(`${base}${path}`);
    assert.equal(r.status, 404, path);
  }
});

test('the boundary statement names the Stage and refuses every authority', async () => {
  const body = await (await fetch(`${base}/api/boundary`)).json();
  assert.equal(body.stageId, STAGE_ID);
  assert.equal(body.mode, 'SIMULATION / PREVIEW');
  assert.deepEqual([...body.statements], [
    'SIMULATION / PREVIEW', 'NO HARDWARE ACCESS', 'NO DEVICE WRITE', 'FIELD VERIFICATION PENDING',
  ]);
  assert.equal(body.hardwareConnection, false);
  assert.equal(body.deviceWriteAuthority, false);
  assert.equal(body.liveTransportImplemented, false);
  assert.equal(body.fieldVerified, false);
  assert.equal(body.evidenceBaseline, 'BASELINE_REV1');
  assert.equal(body.drawingStatus, 'DRAWING_PENDING_REVISION');
  assert.equal(body.scheduleRows, 240);
  assert.equal(body.approvedOrigin, 'http://127.0.0.1');
  assert.equal(body.bindHost, '127.0.0.1');
  assert.equal(BOUNDARY.deviceWriteAuthority, false);
});

test('the health route answers locally and reports no hardware', async () => {
  const body = await (await fetch(`${base}/api/health`)).json();
  assert.equal(body.status, 'ok');
  assert.equal(body.stageId, STAGE_ID);
  assert.equal(body.hardware, false);
});

test('the schedule payload reproduces the protected baseline exactly', async () => {
  const body = await (await fetch(`${base}/api/commissioning/schedule`)).json();
  assert.equal(body.label, 'BASELINE_REV1');
  assert.equal(body.rows.length, 240);
  assert.equal(body.counts.physicalChannels, 240);
  assert.equal(body.counts.mappedChannels, 216);
  assert.equal(body.counts.unassignedChannels, 24);
  assert.equal(body.counts.productCompatibleMeasurements, 212);
  assert.equal(body.counts.productSensors, 106);
  assert.equal(body.counts.drawingVerifiedRows, 200);
  assert.equal(body.counts.drawingInternalConflictRows, 12);
  assert.equal(body.counts.productTopologyConflictRows, 4);
  assert.equal(body.counts.unresolvedRows, 0);
  assert.equal(body.counts.matchesProtectedBaseline, true);
  const first = body.rows[0];
  for (const field of ['physicalChannelKey', 'nodeId', 'moduleInstance', 'channel', 'rowState',
    'templateCompatibility', 'rowClass', 'passBlockedReason', 'measurementId', 'drawingVerified']) {
    assert.equal(field in first, true, field);
  }
});

test('the scenario catalogue is served without observations', async () => {
  const body = await (await fetch(`${base}/api/commissioning/scenarios`)).json();
  // Ten PROTECTED scenarios (never removed, never renamed) plus the three explicit paired thermal
  // demonstrations added by the Front/Rear thermal-pair pass.
  assert.equal(body.scenarios.length, 13);
  assert.deepEqual(body.scenarios.map((s) => s.key), [
    'NORMAL_GOOD', 'UNCERTAIN_TRANSIENT', 'OUT_OF_RANGE', 'STALE', 'COMMUNICATION_LOSS_NODE',
    'SIMULATED_WIRE_BREAK', 'RECOVERY', 'DRAWING_CONFLICT', 'PRODUCT_TOPOLOGY_CONFLICT', 'UNASSIGNED_CHANNEL',
    'REDUCED_FRONT_REAR_DIFFERENTIAL', 'NEAR_ZERO_FRONT_REAR_DIFFERENTIAL', 'REVERSED_FRONT_REAR_DIFFERENTIAL',
  ]);
  const wireBreak = body.scenarios.find((s) => s.key === 'SIMULATED_WIRE_BREAK');
  assert.equal(wireBreak.simulatedDiagnostic, true);
  assert.match(wireBreak.summary, /BLOCKED/);
  for (const scenario of body.scenarios) assert.equal('observation' in scenario, false);
});

test('one scenario can be requested and returns its views plus its raw observations', async () => {
  const body = await (await fetch(`${base}/api/commissioning/observations?scenario=SIMULATED_WIRE_BREAK&tick=3`)).json();
  assert.equal(body.scenarioKey, 'SIMULATED_WIRE_BREAK');
  assert.equal(body.tick, 3);
  assert.equal(Object.keys(body.observations).length, 240);
  assert.equal(body.views.length, 240);
  assert.equal(body.summary.simulatedDiagnostic > 0, true);
  assert.equal(body.nodeSummary.length, 6);
  assert.equal(body.commissioningProgress.total, 240);
  const simulated = Object.values(body.observations).filter((o) => o.diagnosticReason === 'WIRE_BREAK');
  assert.equal(simulated.length > 0, true);
  for (const observation of simulated) {
    assert.equal(observation.provenance, 'SIMULATED_DIAGNOSTIC_SCENARIO');
    assert.equal(observation.engineeringValue, null);
  }
});

test('scenario and node scope are validated rather than guessed', async () => {
  assert.equal((await fetch(`${base}/api/commissioning/observations?scenario=MADE_UP`)).status, 404);
  assert.equal((await fetch(`${base}/api/commissioning/observations?scenario=NORMAL_GOOD&tick=-1`)).status, 400);
  assert.equal((await fetch(`${base}/api/commissioning/observations?scenario=NORMAL_GOOD&tick=abc`)).status, 400);
  const missingNode = await fetch(`${base}/api/commissioning/observations?scenario=COMMUNICATION_LOSS_NODE`);
  assert.equal(missingNode.status, 400);
  assert.match((await missingNode.json()).error, /requires an explicit nodeId/);
  const scoped = await (await fetch(`${base}/api/commissioning/observations?scenario=COMMUNICATION_LOSS_NODE&node=TC-NODE-03`)).json();
  assert.equal(scoped.nodeSummary.filter((n) => n.state === 'COMMUNICATION_LOSS').length, 1);
  assert.equal(scoped.nodeSummary.filter((n) => n.state === 'ONLINE').length, 5);
});

test('the operations summary reports six nodes and the evidence-derived progress', async () => {
  const body = await (await fetch(`${base}/api/operations/summary`)).json();
  assert.equal(body.nodeSummary.length, 6);
  assert.deepEqual(body.nodeSummary.map((n) => n.nodeId), ['TC-NODE-01', 'TC-NODE-02', 'TC-NODE-03', 'TC-NODE-04', 'TC-NODE-05', 'TC-NODE-06']);
  for (const node of body.nodeSummary) assert.equal(node.state, 'ONLINE');
  assert.equal(body.commissioningProgress.total, 240);
  assert.equal(body.commissioningProgress.byState.BLOCKED, 40);
  assert.equal(body.commissioningProgress.byState.NOT_TESTED, 200);
  assert.equal(body.commissioningProgress.eligibleForPass, 200);
});

test('the sensor map route serves the public-safe example with I7 and I16 as gaps', async () => {
  const body = await (await fetch(`${base}/api/sensor-map`)).json();
  assert.equal(body.slotCount, 108);
  assert.equal(body.sensorSlotCount, 106);
  assert.equal(body.logicalMatrix.thermocoupleChannels, 212);
  assert.equal(body.nonSensorGapSlots.length, 2);
  assert.deepEqual([...new Set(body.nonSensorGapSlots.map((g) => g.slotId))].length, 2);
  const gapText = JSON.stringify(body.nonSensorGapSlots);
  assert.match(gapText, /I7/);
  assert.match(gapText, /I16/);
  for (const gap of body.nonSensorGapSlots) assert.ok(gap.waterJetAnchor);
});

test('no served byte contains a non-loopback address, an external asset or a device endpoint', async () => {
  const assets = ['/', '/app.mjs', '/styles.css', '/api/boundary', '/api/commissioning/schedule'];
  for (const asset of assets) {
    const text = await (await fetch(`${base}${asset}`)).text();
    const urls = text.match(/https?:\/\/[^\s"'`)\\]*/g) ?? [];
    for (const url of urls) {
      assert.match(url, /^http:\/\/127\.0\.0\.1(?::\d+)?/, `${asset}: ${url}`);
    }
    const addresses = text.match(/\b(?:\d{1,3}\.){3}\d{1,3}\b/g) ?? [];
    for (const address of addresses) {
      assert.match(address, /^127\.0\.0\.1$/, `${asset}: ${address}`);
    }
    assert.equal(/fonts\.googleapis|cdn\.|unpkg|jsdelivr/i.test(text), false, asset);
  }
});

test('the served UI carries no vendor, transport or production vocabulary', () => {
  const forbidden = ['mo' + 'dbus', 'ga' + 'lil', 'wa' + 'go', 'mq' + 'tt', 'reg' + 'ister map', 'tra' + 'vel limit'];
  for (const file of ['index.html', 'app.mjs', 'styles.css']) {
    const text = readFileSync(join(PUBLIC, file), 'utf8').toLowerCase();
    for (const term of forbidden) assert.equal(text.includes(term), false, `${file}: ${term}`);
  }
});

test('the UI never navigates away and never issues a write request', async () => {
  const app = readFileSync(join(PUBLIC, 'app.mjs'), 'utf8');
  assert.equal(/location\s*=|location\.assign|location\.replace|window\.open/.test(app), false);
  assert.equal(/method:\s*['"](POST|PUT|PATCH|DELETE)/.test(app), false);
  const html = readFileSync(join(PUBLIC, 'index.html'), 'utf8');
  assert.equal(/<a [^>]*href="(?!#)/.test(html), false, 'no in-page link may leave the local route');
});
