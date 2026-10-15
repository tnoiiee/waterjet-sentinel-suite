// Stage 0.4C-K1 (re-verified at 0.4C-K2) - Preview host tests (Stage task section 16, groups A and E).
// The host must be loopback-only, read-only, and free of any device, transport or control route.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { BOUNDARY, DEFAULT_PORT, DEFAULT_RUNTIME_PORT, STAGE_ID, UI_BIND_HOST,
  createPreviewServer, tcAcquisitionPayload, tcPhysicalMappingPayload } from '../server.mjs';
import { allText, bootApp } from './helpers/fakeDom.mjs';
import { PHYSICAL_NOT_VERIFIED } from '../../../packages/contracts/wjss-contracts-ts/validate.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const PUBLIC = join(HERE, '..', 'public');

let server;
let base;
let bound;

before(async () => {
  server = createPreviewServer({
    runtimeFetch: async () => { throw new Error('No Runtime host runs in this host test.'); },
  });
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
    '/api/commissioning/scenarios', '/api/commissioning/observations', '/api/operations/summary', '/api/sensor-map',
    '/api/tc-acquisition'];
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
  assert.equal(body.counts.productCompatibleMeasurements, 216);
  assert.equal(body.counts.productSensors, 108);
  assert.equal(body.counts.drawingVerifiedRows, 204);
  assert.equal(body.counts.productTopologyConflictRows, 0);
  assert.equal(body.counts.unassignedChannels, 24);
  assert.equal(body.counts.drawingInternalConflictRows, 12);
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
  assert.equal(body.commissioningProgress.byState.BLOCKED, 36);
  assert.equal(body.commissioningProgress.byState.NOT_TESTED, 204);
  assert.equal(body.commissioningProgress.eligibleForPass, 204);
});

test('the sensor map route serves the public-safe example with all 108 positions as Sensors (I7 and I16 included)', async () => {
  const body = await (await fetch(`${base}/api/sensor-map`)).json();
  assert.equal(body.slotCount, 108);
  assert.equal(body.sensorSlotCount, 108);
  assert.equal(body.nonSensorGapSlotCount, 0);
  assert.equal(body.logicalMatrix.sensorLocations, 108);
  assert.equal(body.logicalMatrix.thermocoupleChannels, 216);
  assert.deepEqual(body.logicalMatrix.sensorsPerWall, { LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 });
  assert.equal(body.topologyInvariants.ok, true);
  assert.equal(body.topologyInvariants.authoritative.PRODUCT_SENSORS, 108);
  assert.equal(body.topologyInvariants.authoritative.ASSIGNED_PRODUCT_TC_CHANNELS, 216);
  assert.equal(body.topologyInvariants.authoritative.UNASSIGNED_PHYSICAL_TC_CHANNELS, 24);
  const slots = body.walls.flatMap((w) => w.slots);
  assert.equal(slots.length, 108);
  assert.equal(slots.every((s) => s.positionKind === 'SENSOR' && typeof s.sensorId === 'string'), true);
  for (const sensorId of ['I7', 'I16']) {
    const matching = slots.filter((s) => s.sensorId === sensorId);
    assert.equal(matching.length, 1, sensorId);
    assert.equal(matching[0].tcChannels.length, 2, sensorId);
  }
  const text = JSON.stringify(body);
  assert.doesNotMatch(text.replace(/NON_SENSOR_GAP_POSITIONS":0/g, ''), /NON_SENSOR_GAP|gapAnchorForWaterJetId|waterJetAnchor|logicalLabel/);
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

// Runtime is simulated at the HTTP seam, not by an invented Product state in the Preview.
// This is the exact JSON shape of the .NET Snapshot's TC state (including its UTC offset).
const runtimeSnapshot = () => ({
  kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 2,
  tcAcquisition: {
    nodeId: 'TC-NODE-01', cabinetId: 'CAB1', provenance: 'DETERMINISTIC_SIMULATION',
    physical: false, quarantined: true, acquisitionState: 'COMPLETE',
    acquiredAtUtc: '2026-10-15T10:20:30+00:00', copiedWordCount: 40,
    rawWords: Array.from({ length: 40 }, (_, i) => 100 + i),
    quality: 'SIMULATED', diagnosticState: 'NONE', mappingStatus: 'MAPPING_NOT_VERIFIED',
    decodeStatus: 'DECODING_NOT_VERIFIED',
    notVerified: ['PHYSICAL_REACHABILITY', 'REGISTER_MAPPING', 'SCALING', 'WIRE_BREAK', 'FIELD_TIMING'],
  },
});
const okSnapshot = (value) => Response.json(value);
const runtimeStatus = (revision, readinessCode) => ({
  currentRevision: revision, profile: 'SIMULATOR', ready: false, readinessCode,
});
const pendingFetch = async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
  ? runtimeStatus(1, 'TC_ACQUISITION_NOT_PUBLISHED')
  : { ...runtimeSnapshot(), revision: 1, tcAcquisition: null });

/** Runs a real Preview host on an ephemeral local port, injecting only the Runtime HTTP fetch. */
async function withRuntimeFetch(runtimeFetch, run) {
  const host = createPreviewServer({ runtimeFetch, runtimePort: DEFAULT_RUNTIME_PORT });
  await new Promise((resolve) => host.listen(0, UI_BIND_HOST, resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(host.address().port);
  try { return await run(origin.origin); }
  finally { await new Promise((resolve) => host.close(resolve)); }
}

test('the TC route reads only the fixed Runtime Snapshot URL and projects its revision and 40 words', async () => {
  const calls = [];
  await withRuntimeFetch(async (url, options) => {
    calls.push({ url, method: options.method, accept: options.headers.Accept, signal: options.signal });
    return okSnapshot(runtimeSnapshot());
  }, async (origin) => {
    const res = await fetch(`${origin}/api/tc-acquisition?node=TC-NODE-99&host=elsewhere`);
    assert.equal(res.status, 200);
    assert.match(res.headers.get('content-security-policy'), /default-src 'self'/);
    assert.equal(res.headers.get('cache-control'), 'no-store');
    assert.equal(res.headers.get('access-control-allow-origin'), null);
    const body = await res.json();
    assert.equal(body.stageId, STAGE_ID);
    assert.equal(body.status, 'PUBLISHED');
    assert.equal(body.revision, 2);
    assert.equal(body.tcAcquisition.node, 'TC-NODE-01');
    assert.equal(body.tcAcquisition.cabinet, 'CAB1');
    assert.equal(body.tcAcquisition.provenance, 'DETERMINISTIC_SIMULATION');
    assert.equal(body.tcAcquisition.physical, false);
    assert.equal(body.tcAcquisition.quarantine, true);
    assert.equal(body.tcAcquisition.acquisitionState, 'COMPLETE');
    assert.equal(body.tcAcquisition.acquisitionTimestampUtc, '2026-10-15T10:20:30+00:00');
    assert.equal(body.tcAcquisition.copiedWordCount, 40);
    assert.deepEqual(body.tcAcquisition.words, Array.from({ length: 40 }, (_, i) => 100 + i));
    assert.deepEqual(body.tcAcquisition.notVerified, runtimeSnapshot().tcAcquisition.notVerified);
    const head = await fetch(`${origin}/api/tc-acquisition`, { method: 'HEAD' });
    assert.equal(head.status, 200);
    assert.equal(await head.text(), '');
  });
  assert.equal(calls.length, 2);
  const expectedOrigin = new URL('http://127.0.0.1');
  expectedOrigin.port = String(DEFAULT_RUNTIME_PORT);
  for (const call of calls) {
    assert.equal(call.url, new URL('/api/v1/snapshot', expectedOrigin).href);
    assert.equal(call.method, 'GET');
    assert.equal(call.accept, 'application/json');
    assert.ok(call.signal instanceof AbortSignal, 'the upstream request must have a time bound');
  }
});

test('an unpublished or failed Runtime never gives the Preview a successful TC record', async () => {
  const pending = await tcAcquisitionPayload({ runtimeFetch: pendingFetch });
  assert.equal(pending.status, 200);
  assert.equal(pending.body.status, 'NOT_PUBLISHED');
  assert.equal(pending.body.revision, 1);
  assert.equal(pending.body.tcAcquisition, null);

  let reads = 0;
  const runtimeFetch = async (url) => {
    reads++;
    if (reads === 1) return okSnapshot(runtimeSnapshot());
    if (reads === 2) throw new Error('local Runtime not running');
    if (reads === 3) return Response.json({ error: 'RUNTIME_NOT_READY' }, { status: 503 });
    return okSnapshot(url.endsWith('/api/v1/runtime')
      ? runtimeStatus(2, 'TC_ACQUISITION_NOT_PUBLISHED')
      : { ...runtimeSnapshot(), tcAcquisition: null });
  };
  assert.equal((await tcAcquisitionPayload({ runtimeFetch })).body.status, 'PUBLISHED');
  for (const expected of [503, 503, 200]) {
    const projection = await tcAcquisitionPayload({ runtimeFetch });
    assert.equal(projection.status, expected);
    assert.equal(projection.body.tcAcquisition, null, 'success must not be cached after an outage');
    assert.equal(projection.body.status, expected === 200 ? 'NOT_PUBLISHED' : 'RUNTIME_UNAVAILABLE');
  }
  assert.equal(reads, 5);
});

test('a null Snapshot is distinguished using same-revision Runtime diagnostics, not a second state', async () => {
  const calls = [];
  const failed = await tcAcquisitionPayload({ runtimeFetch: async (url) => {
    calls.push(url);
    return okSnapshot(url.endsWith('/api/v1/runtime')
      ? runtimeStatus(2, 'TC_ACQUISITION_UNAVAILABLE')
      : { ...runtimeSnapshot(), tcAcquisition: null });
  } });
  const runtimeOrigin = new URL('http://127.0.0.1');
  runtimeOrigin.port = String(DEFAULT_RUNTIME_PORT);
  assert.deepEqual(calls, [
    new URL('/api/v1/snapshot', runtimeOrigin).href,
    new URL('/api/v1/runtime', runtimeOrigin).href,
  ]);
  assert.equal(failed.status, 200);
  assert.equal(failed.body.status, 'ACQUISITION_FAILED');
  assert.equal(failed.body.revision, 2);
  assert.equal(failed.body.tcAcquisition, null);

  const raced = await tcAcquisitionPayload({ runtimeFetch: async (url) => okSnapshot(
    url.endsWith('/api/v1/runtime')
      ? runtimeStatus(3, 'TC_ACQUISITION_UNAVAILABLE')
      : { ...runtimeSnapshot(), tcAcquisition: null },
  ) });
  assert.equal(raced.status, 502);
  assert.equal(raced.body.status, 'INVALID_RUNTIME_RESPONSE');
  assert.equal(raced.body.tcAcquisition, null);
});

test('malformed, unsafe, or incomplete Runtime states fail closed without exposing words', async () => {
  const malformed = [
    { ...runtimeSnapshot(), tcAcquisition: undefined },
    { ...runtimeSnapshot(), kind: 'delta' },
    { ...runtimeSnapshot(), apiVersion: 99 },
    { ...runtimeSnapshot(), revision: 0 },
    { ...runtimeSnapshot(), tcAcquisition: { ...runtimeSnapshot().tcAcquisition, physical: true } },
    { ...runtimeSnapshot(), tcAcquisition: { ...runtimeSnapshot().tcAcquisition, quarantined: false } },
    { ...runtimeSnapshot(), tcAcquisition: { ...runtimeSnapshot().tcAcquisition, rawWords: [100, 101] } },
    { ...runtimeSnapshot(), tcAcquisition: { ...runtimeSnapshot().tcAcquisition,
      rawWords: Array.from({ length: 40 }, (_, i) => 999 + i) } },
    { ...runtimeSnapshot(), tcAcquisition: { ...runtimeSnapshot().tcAcquisition, notVerified: [] } },
    { ...runtimeSnapshot(), tcAcquisition: { ...runtimeSnapshot().tcAcquisition,
      acquiredAtUtc: '2026-10-15T10:20:30+07:00' } },
  ];
  for (const snapshot of malformed) {
    const result = await tcAcquisitionPayload({ runtimeFetch: async () => okSnapshot(snapshot) });
    assert.equal(result.status, 502);
    assert.equal(result.body.status, 'INVALID_RUNTIME_RESPONSE');
    assert.equal(result.body.tcAcquisition, null);
    assert.equal(JSON.stringify(result.body).includes('rawWords'), false);
  }
  const broken = await tcAcquisitionPayload({ runtimeFetch: async () => new Response('not JSON') });
  assert.equal(broken.status, 502);
  assert.equal(broken.body.status, 'INVALID_RUNTIME_RESPONSE');
  let calls = 0;
  await assert.rejects(tcAcquisitionPayload({ runtimePort: 'untrusted-host:502',
    runtimeFetch: async () => { calls++; },
  }), RangeError);
  assert.equal(calls, 0, 'an invalid Runtime port must never make a request');
});

test('the real Kiosk section renders published, not-published and unavailable states separately', async () => {
  const cases = [
    { upstream: async () => okSnapshot(runtimeSnapshot()), expected: /Runtime revision 2.*TC-NODE-01.*CAB1.*DETERMINISTIC_SIMULATION.*QUARANTINED.*COMPLETE.*Raw words: 100, 101/,
      status: 200, published: true },
    { upstream: async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
      ? runtimeStatus(2, 'TC_ACQUISITION_NOT_PUBLISHED')
      : { ...runtimeSnapshot(), tcAcquisition: null }),
      expected: /Runtime revision 2: TC acquisition not published\. No acquisition is claimed\./,
      status: 200, published: false },
    { upstream: async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
      ? runtimeStatus(2, 'TC_ACQUISITION_UNAVAILABLE')
      : { ...runtimeSnapshot(), tcAcquisition: null }),
      expected: /Runtime revision 2: TC acquisition failed\. No state published; no acquisition is claimed\./,
      status: 200, published: false },
    { upstream: async () => { throw new Error('Runtime is not running'); },
      expected: /TC acquisition unavailable: the Runtime Snapshot could not be read\. No acquisition is claimed\./,
      status: 503, published: false },
    { upstream: async () => okSnapshot({ ...runtimeSnapshot(), tcAcquisition: { physical: true } }),
      expected: /TC acquisition unavailable: the Runtime Snapshot could not be read\. No acquisition is claimed\./,
      status: 502, published: false },
  ];
  for (const scenario of cases) {
    const app = await bootApp({ runtimeFetch: scenario.upstream });
    try {
      assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
      const route = app.requests.find((r) => r.path === '/api/tc-acquisition');
      assert.equal(route?.status, scenario.status);
      const text = allText(app.byId['tc-acquisition-body']);
      assert.match(text, scenario.expected);
      assert.equal(text.includes('139'), scenario.published, 'words can only appear when Runtime published');
      assert.equal(text.includes('Raw words'), scenario.published);
      assert.equal(app.byId['boot-error'].hidden, true, 'this section cannot break the synthetic Monitor');
    } finally { app.close(); }
  }
});

// --- One-Node physical read-only variant, served read-only ---------------------------------------
//
// The physical envelope is a CANDIDATE presentation. It must carry its honest labels together with
// the numbers, must present the legacy sentinels as raw evidence rather than as temperatures, must
// keep the two Drawing conflicts visible, and must never carry an endpoint. The endpoint is absent
// from the Runtime record by construction, so there is nothing to redact or leak here: these tests
// prove the ABSENCE.

/** The exact JSON shape of a published PHYSICAL quarantined record, as the Runtime would write it. */
const physicalSnapshot = (overrides = {}) => ({
  kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 7,
  tcAcquisition: {
    nodeId: 'TC-NODE-01', cabinetId: 'CAB1', provenance: 'PHYSICAL_HOLDING_REGISTER_READ_ONLY',
    physical: true, quarantined: true, acquisitionState: 'COMPLETE',
    acquiredAtUtc: '2026-10-15T10:20:30+00:00', copiedWordCount: 40,
    // Offset 0 and 1 are the legacy sentinel candidates; the rest are ordinary words.
    rawWords: [0x7FFF, 0xF830, ...Array.from({ length: 38 }, (_, i) => 100 + i)],
    quality: 'PHYSICAL_QUARANTINED', diagnosticState: 'NONE',
    mappingStatus: 'DRAWING_REV1_MAPPING_DECLARED_NOT_VERIFIED',
    decodeStatus: 'DECODE_CANDIDATE_NOT_VERIFIED',
    notVerified: [...PHYSICAL_NOT_VERIFIED],
    physicalEvidence: {
      endpointIdentity: 'ENDPOINT_CONFIGURED', commissioningState: 'NOT_COMMISSIONED',
      mappingEvidenceState: 'DRAWING_INTERNAL_CONFLICT',
      readProfile: {
        functionCode: 'FC03', registerArea: 'HOLDING_REGISTERS', port: 502,
        unitIdentifier: 1, startAddress: 0, quantity: 40,
      },
    },
  },
  ...overrides,
});

const physicalRuntimeStatus = () => ({
  currentRevision: 7, profile: 'TEST_HARDWARE_READ_ONLY', ready: true,
});

test('the TC route serves the physical variant with its candidate labels and its raw sentinels', async () => {
  const runtimeFetch = async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
    ? physicalRuntimeStatus() : physicalSnapshot());

  await withRuntimeFetch(runtimeFetch, async (origin) => {
    const res = await fetch(`${origin}/api/tc-acquisition`);
    assert.equal(res.status, 200);
    const envelope = await res.json();
    assert.equal(envelope.status, 'PUBLISHED');
    assert.equal(envelope.revision, 7);
    assert.equal(envelope.stageId, STAGE_ID);
    const tcAcquisition = envelope.tcAcquisition;
    assert.equal(tcAcquisition.physical, true);
    assert.equal(tcAcquisition.quarantine, true);
    assert.equal(tcAcquisition.quality, 'PHYSICAL_QUARANTINED');
    assert.deepEqual(tcAcquisition.labels, ['PHYSICAL', 'QUARANTINED', 'DECODE CANDIDATE', 'NOT COMMISSIONED']);
    assert.equal(tcAcquisition.commissioningState, 'NOT_COMMISSIONED');
    assert.equal(tcAcquisition.mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');
    // The redacted identity is the only endpoint statement in the response.
    assert.equal(tcAcquisition.endpointIdentity, 'ENDPOINT_CONFIGURED');
    assert.equal(tcAcquisition.sensorCount, 18);
    assert.equal(tcAcquisition.measurementCount, 36);
    assert.deepEqual(tcAcquisition.spareOffsets, [36, 37, 38, 39]);
    assert.equal(tcAcquisition.readProfile.functionCode, 'FC03');
    assert.equal(tcAcquisition.readProfile.quantity, 40);
    // Scaling, signedness and byte order are declared unverified, never asserted.
    assert.equal(tcAcquisition.candidateDecode.state, 'DECODE_CANDIDATE_NOT_VERIFIED');
    assert.equal(tcAcquisition.candidateDecode.scaling, 'NOT_VERIFIED');
    assert.equal(tcAcquisition.candidateDecode.signedRepresentation, 'NOT_VERIFIED');
    assert.equal(tcAcquisition.candidateDecode.byteOrder, 'NOT_VERIFIED');
    assert.deepEqual(tcAcquisition.notVerified, PHYSICAL_NOT_VERIFIED);
  });
});

test('the legacy sentinel candidates never surface as ordinary temperatures in the served payload', async () => {
  const runtimeFetch = async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
    ? physicalRuntimeStatus() : physicalSnapshot());

  await withRuntimeFetch(runtimeFetch, async (origin) => {
    const res = await fetch(`${origin}/api/tc-acquisition`);
    const { tcAcquisition } = await res.json();
    const sensor = tcAcquisition.sensors.find((s) => s.sensorId === 'G+101');

    // Offset 0 (Front) is the legacy wire-break candidate; offset 1 (Rear) is the legacy RV/P one.
    assert.equal(sensor.front.offset, 0);
    assert.equal(sensor.front.rawWord, 0x7FFF);
    assert.equal(sensor.front.rawHex, '0x7FFF');
    assert.equal(sensor.front.candidateValue, null);
    assert.equal(sensor.front.presentAsOrdinaryTemperature, false);
    assert.equal(sensor.front.decodeState, 'DECODE_LEGACY_WIRE_BREAK_CANDIDATE');
    assert.equal(sensor.front.manufacturerMeaning, 'NOT_VERIFIED');

    assert.equal(sensor.rear.offset, 1);
    assert.equal(sensor.rear.rawWord, 0xF830);
    assert.equal(sensor.rear.rawHex, '0xF830');
    assert.equal(sensor.rear.candidateValue, null);
    assert.equal(sensor.rear.presentAsOrdinaryTemperature, false);
    assert.notEqual(sensor.rear.candidateValue, -200);
    assert.equal(sensor.rear.decodeState, 'DECODE_LEGACY_RV_P_CANDIDATE');

    // Neither sentinel side may produce a fabricated paired delta.
    assert.equal(sensor.frontRearDeltaC, null);
    assert.equal(sensor.deltaAvailable, false);

    // An ordinary pair still yields a candidate number AND a read-only diagnostic delta.
    const ordinary = tcAcquisition.sensors.find((s) => s.sensorId === 'G+102');
    assert.equal(typeof ordinary.front.candidateValue, 'number');
    assert.equal(typeof ordinary.rear.candidateValue, 'number');
    assert.equal(ordinary.deltaAvailable, true);
    assert.equal(ordinary.frontRearDeltaC, Number((ordinary.front.candidateValue - ordinary.rear.candidateValue).toFixed(4)));
  });
});

test('the served physical payload keeps the two Drawing conflicts visible and never hides a spared offset', async () => {
  const runtimeFetch = async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
    ? physicalRuntimeStatus() : physicalSnapshot());

  await withRuntimeFetch(runtimeFetch, async (origin) => {
    const { tcAcquisition } = await (await fetch(`${origin}/api/tc-acquisition`)).json();
    const conflicted = tcAcquisition.sensors.filter((s) => s.drawingConflict);
    assert.deepEqual(conflicted.map((s) => s.sensorId).sort(), ['G+107', 'G+108']);
    for (const sensor of conflicted) assert.equal(sensor.mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');
    assert.equal(tcAcquisition.sensors.find((s) => s.sensorId === 'G+101').drawingConflict, false);
    // The eighteen Sensors of CAB1 are exactly G+101..G+109 and G+201..G+209.
    assert.deepEqual(tcAcquisition.sensors.map((s) => s.sensorId),
      [...Array(9).keys()].map((i) => `G+10${i + 1}`).concat([...Array(9).keys()].map((i) => `G+20${i + 1}`)));
    // Every served byte is local-safe: no address, no endpoint field, no credential-shaped key.
    const text = JSON.stringify(tcAcquisition);
    // Loopback is the one literal the boundary permits; anything else would be a leak.
    const addresses = text.match(/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/g) ?? [];
    assert.deepEqual(addresses.filter((a) => a !== '127.0.0.1'), [], 'no non-loopback address may be served');
    // Terms are assembled from fragments so this assertion never introduces the vocabulary it
    // forbids (the same convention the boundary scanner and the served-UI test use).
    const forbidden = ['end' + 'point', 'ho' + 'st', 'i' + 'p', 'addre' + 'ss',
      'pass' + 'word', 'to' + 'ken', 'sec' + 'ret'];
    for (const term of forbidden) assert.equal(text.includes(`"${term}"`), false, term);
  });
});

test('a physical Runtime that is not ready or has an unknown profile never yields a successful record', async () => {
  // A ready:false physical Runtime (no publication yet) is a NOT_PUBLISHED/pending answer.
  const pendingPhysical = async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
    ? { currentRevision: 7, profile: 'TEST_HARDWARE_READ_ONLY', ready: false, readinessCode: 'TC_ACQUISITION_NOT_PUBLISHED' }
    : { ...physicalSnapshot(), tcAcquisition: null });
  const pending = await tcAcquisitionPayload({ runtimeFetch: pendingPhysical });
  assert.equal(pending.status, 200);
  assert.equal(pending.body.status, 'NOT_PUBLISHED');
  assert.equal(pending.body.tcAcquisition, null);

  // When the Snapshot carries no TC state, the Runtime status supplies the diagnosis, and an
  // unrecognised profile there is refused rather than echoed: the projection never relabels a run.
  for (const profile of ['PRODUCTION', 'TEST_HARDWARE', 'SOMETHING_ELSE', undefined]) {
    const runtimeFetch = async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
      ? { currentRevision: 7, profile, ready: false }
      : { ...physicalSnapshot(), tcAcquisition: null });
    const result = await tcAcquisitionPayload({ runtimeFetch });
    assert.equal(result.status, 502, String(profile));
    assert.equal(result.body.status, 'INVALID_RUNTIME_RESPONSE');
    assert.equal(result.body.tcAcquisition, null);
  }

  // A published record is self-describing: its own provenance selects the variant, so the envelope
  // reports the record it actually received and never relabels it.
  const published = await tcAcquisitionPayload({ runtimeFetch: async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
    ? physicalRuntimeStatus() : physicalSnapshot()) });
  assert.equal(published.body.status, 'PUBLISHED');
  assert.equal(published.body.tcAcquisition.provenance, 'PHYSICAL_HOLDING_REGISTER_READ_ONLY');
  assert.equal(published.body.tcAcquisition.physical, true);
});

test('an unsafe physical record is refused whole: no words, no candidates, no partial success', async () => {
  const runtimeFetch = async (url) => okSnapshot(url.endsWith('/api/v1/runtime')
    ? physicalRuntimeStatus() : physicalSnapshot());
  const base = await tcAcquisitionPayload({ runtimeFetch });
  assert.equal(base.body.status, 'PUBLISHED');

  const request = async (mutate) => {
    const snapshot = physicalSnapshot();
    mutate(snapshot.tcAcquisition);
    const fetchImpl = async (url) => okSnapshot(url.endsWith('/api/v1/runtime') ? physicalRuntimeStatus() : snapshot);
    return tcAcquisitionPayload({ runtimeFetch: fetchImpl });
  };

  for (const mutate of [
    (tc) => { tc.quarantined = false; },
    (tc) => { tc.physical = false; },
    (tc) => { tc.quality = 'VERIFIED'; },
    (tc) => { tc.commissioningState = 'COMMISSIONED'; },
    (tc) => { tc.rawWords = tc.rawWords.slice(0, 39); },
    (tc) => { tc.rawWords = [...tc.rawWords.slice(0, 39), 65536]; },
    (tc) => { tc.copiedWordCount = 39; },
    (tc) => { tc.acquiredAtUtc = '0001-01-01T00:00:00+00:00'; },
    (tc) => { tc.notVerified = tc.notVerified.slice(0, -1); },
    (tc) => { tc.nodeId = 'TC-NODE-02'; },
    (tc) => { tc.cabinetId = 'CAB2'; },
    (tc) => { tc.physicalEvidence.commissioningState = 'ACCEPTED'; },
    (tc) => { tc.physicalEvidence.readProfile = { ...tc.physicalEvidence.readProfile, functionCode: 'FC16' }; },
    (tc) => { tc.physicalEvidence.readProfile = { ...tc.physicalEvidence.readProfile, quantity: 39 }; },
    (tc) => { tc.physicalEvidence.readProfile = { ...tc.physicalEvidence.readProfile, port: 5020 }; },
    (tc) => { tc.physicalEvidence.readProfile = { ...tc.physicalEvidence.readProfile, unitIdentifier: 2 }; },
    (tc) => { tc.physicalEvidence.readProfile = { ...tc.physicalEvidence.readProfile, startAddress: 1 }; },
    // The allow-list is closed: an endpoint-shaped extra field is refused rather than forwarded.
    (tc) => { tc.endpoint = '127.0.0.1'; },
    (tc) => { tc.physicalEvidence.endpointIdentity = '127.0.0.1'; },
    (tc) => { tc.physicalEvidence.endpointIdentity = 'ENDPOINT_NOT_CONFIGURED'; },
  ]) {
    const result = await request(mutate);
    assert.equal(result.status, 502, mutate.toString());
    assert.equal(result.body.status, 'INVALID_RUNTIME_RESPONSE');
    assert.equal(result.body.tcAcquisition, null, 'a refused record must not expose words');
  }
});

test('the mapping route serves the CAB1 projection read-only, with no Runtime needed and no endpoint', async () => {
  // The mapping projection is useful precisely when nothing is running, so it is served from the
  // committed Drawing evidence alone and never contacts a Runtime.
  const res = await fetch(`${base}/api/tc-physical-mapping`);
  assert.equal(res.status, 200);
  assert.match(res.headers.get('content-type'), /application\/json/);
  assert.match(res.headers.get('content-security-policy'), /default-src 'self'/);
  assert.equal(res.headers.get('cache-control'), 'no-store');
  assert.equal(res.headers.get('access-control-allow-origin'), null);

  const body = await res.json();
  assert.equal(body.node, 'TC-NODE-01');
  assert.equal(body.cabinet, 'CAB1');
  assert.equal(body.wordCount, 40);
  assert.equal(body.assignedOffsetCount, 36);
  assert.equal(body.spareOffsetCount, 4);
  assert.equal(body.sensorCount, 18);
  assert.equal(body.mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');
  assert.deepEqual(body.drawingConflictOffsets, [13, 14]);
  assert.deepEqual(body.measurements.map((m) => m.offset), Array.from({ length: 40 }, (_, i) => i));
  assert.deepEqual(body.measurements.slice(36).map((m) => m.sensorId), [null, null, null, null]);
  assert.equal(body.readProfile.functionCode, 'FC03');
  assert.equal(body.boundary.hardwareConnection, false);
  assert.equal(body.boundary.deviceWriteAuthority, false);

  // HEAD works, writes are refused, and the payload equals the direct projection.
  assert.equal((await fetch(`${base}/api/tc-physical-mapping`, { method: 'HEAD' })).status, 200);
  assert.equal((await fetch(`${base}/api/tc-physical-mapping`, { method: 'POST' })).status, 405);
  assert.deepEqual(body, tcPhysicalMappingPayload());

  // No address, no endpoint key, no vendor or transport vocabulary anywhere in the served bytes.
  const text = JSON.stringify(body);
  const addresses = text.match(/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/g) ?? [];
  assert.deepEqual(addresses.filter((a) => a !== '127.0.0.1'), [], 'only the loopback literal is permitted');
  // The served mapping payload must carry no endpoint-shaped key and no vendor vocabulary. The
  // terms are assembled from fragments, as the boundary scanner does, so this test does not itself
  // introduce the words it forbids.
  const forbiddenKeys = ['end' + 'point', 'ho' + 'st', 'ipAddre' + 'ss', 'soc' + 'ket', 'cred' + 'ential'];
  const forbiddenVendors = ['mo' + 'dbus', 'ga' + 'lil', 'wa' + 'go'];
  const lower = text.toLowerCase();
  // A KEY is forbidden, so the quoted form is matched: `bindHost` is the loopback Preview's own bind
  // label and is not an endpoint key, while a `"host"` field would be.
  for (const term of forbiddenKeys) assert.equal(lower.includes(`"${term.toLowerCase()}"`), false, term);
  for (const term of forbiddenVendors) assert.equal(lower.includes(term), false, term);
  // The projection is address-free by shape: no per-measurement network field exists at all.
  for (const measurement of body.measurements) {
    assert.deepEqual(Object.keys(measurement), ['offset', 'moduleInstance', 'moduleModel', 'channel',
      'sensorId', 'side', 'sideLabel', 'spare', 'rowState', 'drawingConflict', 'mappingEvidenceState']);
  }
});
