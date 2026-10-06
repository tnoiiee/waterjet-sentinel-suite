// WJSS Stage 0.2.1A — Node harness unit/integration tests (node:test, no dependencies).
// SYNTHETIC SPIKE TESTS — not Production validation.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHarness } from '../src/server.mjs';
import { buildSensors, buildPollPlan, assertLoopbackHost, DEFAULT_PARAMS } from '../src/config.mjs';
import { HistorianChannel } from '../src/historian.mjs';
import { validateSnapshot, validateDelta } from '../../contracts/validate.mjs';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const TOKEN = 'unit-test-token';

async function withHarness(fn, params = {}) {
  const h = createHarness({ port: 0, token: TOKEN, params });
  await h.start();
  try {
    await fn(h);
  } finally {
    await h.stop();
  }
}
async function cmd(h, command, params) {
  const r = await fetch(`${h.url}/api/spike/scenario`, { method: 'POST', headers: { 'content-type': 'application/json', 'x-spike-token': TOKEN }, body: JSON.stringify({ command, params }) });
  return r.json();
}
const snapshot = async (h) => (await fetch(`${h.url}/api/snapshot`)).json();

test('synthetic model: 106 sensors, 212 channels, wall counts 24/29/24/29, canonical IDs', () => {
  const s = buildSensors();
  assert.equal(s.length, 106);
  assert.equal(new Set(s.flatMap((x) => [x.tcFrontChannel, x.tcRearChannel])).size, 212);
  const per = Object.groupBy(s, (x) => x.wall);
  assert.deepEqual([per.LEFT.length, per.REAR.length, per.RIGHT.length, per.FRONT.length], [24, 29, 24, 29]);
  assert.equal(s[0].sensorId, 'G+201');
  assert.equal(s[105].sensorId, 'J18');
  assert.ok(!s.some((x) => x.sensorId === 'I7' || x.sensorId === 'I16'));
});

test('poll plan entries carry the required fields and three poll groups', () => {
  const plan = buildPollPlan(buildSensors());
  for (const e of plan) for (const k of ['deviceId', 'functionCategory', 'startAddress', 'quantity', 'decodeInstructions', 'pollGroup', 'updateTargets']) assert.ok(k in e, `${e.id} missing ${k}`);
  assert.deepEqual([...new Set(plan.map((e) => e.pollGroup))].sort(), ['FAST', 'MEDIUM', 'SLOW']);
  assert.equal(new Set(plan.map((e) => e.deviceId)).size, 10);
  const tc = plan.filter((e) => e.pollGroup === 'FAST' && e.deviceId.startsWith('SYN-TC-'));
  assert.equal(tc.reduce((n, e) => n + e.quantity, 0), 212, 'TC poll quantity covers 212 channels');
  assert.deepEqual(tc.map((e) => e.quantity), [28, 28, 26, 26, 26, 26, 26, 26]);
  assert.equal(tc.reduce((n, e) => n + e.updateTargets.length, 0), 106);
});

test('synthetic parameters carry the synthetic label and planning values', () => {
  assert.match(DEFAULT_PARAMS.label, /SYNTHETIC SPIKE PARAMETER - NOT A PRODUCTION VALUE/);
  assert.equal(DEFAULT_PARAMS.seed, 2101);
  assert.equal(DEFAULT_PARAMS.concurrency, 4);
  assert.equal(DEFAULT_PARAMS.requestTimeoutMs, 750);
  assert.equal(DEFAULT_PARAMS.historianCapacity, 50000);
  assert.equal(DEFAULT_PARAMS.historianBatchSize, 500);
});

test('loopback-only bind guard', () => {
  assert.throws(() => assertLoopbackHost('0.0.0.0'));
  assert.throws(() => assertLoopbackHost('192.0.2.10'));
  assert.throws(() => createHarness({ host: '0.0.0.0' }));
  assert.equal(assertLoopbackHost('127.0.0.1'), '127.0.0.1');
});

test('scheduler: serialized per device, bounded across devices, snapshot valid, deltas chained', async () => {
  await withHarness(async (h) => {
    const revs = [];
    h.runtime.on('delta', (d) => {
      revs.push([d.previousRevision, d.revision]);
      assert.deepEqual(validateDelta(d), []);
    });
    await sleep(3200);
    const m = h.runtime.metricsReport();
    assert.equal(m.scheduler.maxPerDeviceInFlight, 1);
    assert.ok(m.scheduler.maxGlobalInFlight <= 4 && m.scheduler.maxGlobalInFlight >= 2, `global in flight ${m.scheduler.maxGlobalInFlight}`);
    for (let i = 1; i < revs.length; i += 1) assert.equal(revs[i][0], revs[i - 1][1]);
    const snap = await snapshot(h);
    assert.deepEqual(validateSnapshot(snap), []);
    assert.equal(m.invariants.violations, 0);
    const addr = h.server.address();
    assert.equal(addr.address, '127.0.0.1');
  });
});

test('scenario API requires the run token', async () => {
  await withHarness(async (h) => {
    const r = await fetch(`${h.url}/api/spike/scenario`, { method: 'POST', body: JSON.stringify({ command: 'pump-stop' }) });
    assert.equal(r.status, 403);
  });
});

test('device timeout: UNCERTAIN then BAD, other devices continue, recovery restores GOOD', async () => {
  await withHarness(async (h) => {
    await sleep(1500);
    await cmd(h, 'device-timeout', { deviceId: 'SYN-TC-01', enabled: true });
    const otherBefore = h.runtime.scheduler.sessions.get('SYN-TC-05').pollsOk;
    await sleep(1900);
    let snap = await snapshot(h);
    const s1 = snap.sensors.find((s) => s.sensorId === 'G+201');
    assert.ok(['UNCERTAIN', 'BAD'].includes(s1.quality), `quality ${s1.quality}`);
    if (s1.quality === 'UNCERTAIN') assert.equal(s1.classificationBasis, 'LAST_VALIDATED');
    await sleep(2500);
    snap = await snapshot(h);
    const bad = snap.sensors.filter((s) => s.deviceId === 'SYN-TC-01');
    assert.ok(bad.every((s) => s.quality === 'BAD' && s.classification === 'NOT_CLASSIFIED'));
    assert.ok(snap.sensors.filter((s) => s.deviceId === 'SYN-TC-05').every((s) => s.quality === 'GOOD'));
    assert.ok(h.runtime.scheduler.sessions.get('SYN-TC-05').pollsOk > otherBefore + 2, 'other device continued polling');
    assert.ok(snap.alarms.items.some((a) => a.code === 'SYN-COMM-TIMEOUT' && a.deviceId === 'SYN-TC-01'));
    await cmd(h, 'device-timeout', { deviceId: 'SYN-TC-01', enabled: false });
    await sleep(2300);
    snap = await snapshot(h);
    assert.ok(snap.sensors.filter((s) => s.deviceId === 'SYN-TC-01').every((s) => s.quality === 'GOOD'));
    assert.equal(h.runtime.metricsReport().invariants.violations, 0);
  });
});

test('at most one active Cleaning Job: a second job request is refused', async () => {
  await withHarness(async (h) => {
    await sleep(1500);
    const r = await cmd(h, 'second-job-attempt');
    assert.equal(r.accepted, false);
    assert.equal(r.reason, 'ACTIVE_JOB_EXISTS');
    assert.equal(r.detail.activeJobs, 1);
    const r2 = await cmd(h, 'start-job', { sensorId: 'G5' });
    assert.equal(r2.accepted, false);
    const m = h.runtime.metricsReport();
    assert.equal(m.jobs.acceptedSecondJobs, 0);
    assert.ok(m.jobs.refusedSecondJobs >= 2);
    assert.equal(m.jobs.active, 1);
    assert.equal(m.invariants.violations, 0);
  });
});

test('UNCERTAIN keeps last validated classification while values change', async () => {
  await withHarness(async (h) => {
    await sleep(1500);
    const before = (await snapshot(h)).sensors.find((s) => s.sensorId === 'G11');
    await cmd(h, 'force-quality', { sensorId: 'G11', quality: 'UNCERTAIN' });
    // move the raw value across the threshold while UNCERTAIN
    h.runtime.proc.get('G11').target = before.classification === 'DIRTY' ? 10 : 90;
    await sleep(2300);
    const after = (await snapshot(h)).sensors.find((s) => s.sensorId === 'G11');
    assert.equal(after.quality, 'UNCERTAIN');
    assert.equal(after.classificationBasis, 'LAST_VALIDATED');
    assert.equal(after.classification, before.classification);
    assert.notEqual(after.dirtyScore > 50, before.classification === 'DIRTY');
  });
});

test('published configuration revision is immutable and reclassifies', async () => {
  await withHarness(async (h) => {
    await sleep(1300);
    const cfg1 = h.runtime.config;
    const r = await cmd(h, 'publish-config', { dirtyThreshold: 99 });
    assert.equal(r.accepted, true);
    await sleep(200);
    assert.ok(Object.isFrozen(h.runtime.config));
    assert.equal(cfg1.dirtyThreshold, 50);
    const snap = await snapshot(h);
    assert.equal(snap.config.revision, 2);
    assert.ok(snap.sensors.filter((s) => s.quality === 'GOOD').every((s) => s.classification === 'CLEANER'));
    assert.equal((await cmd(h, 'publish-config', { dirtyThreshold: 'x' })).accepted, false);
  });
});

test('historian channel is bounded, rejects when full, records gap markers', () => {
  const ch = new HistorianChannel({ capacity: 100, batchSize: 10, nearOverflowRatio: 0.8, delayMs: 10 });
  for (let i = 0; i < 79; i += 1) ch.offer(i);
  assert.equal(ch.nearOverflow, false);
  ch.offer(79);
  assert.equal(ch.nearOverflow, true);
  for (let i = 0; i < 50; i += 1) ch.offer(i);
  assert.equal(ch.depth, 100);
  assert.equal(ch.rejected, 30);
  assert.equal(ch.gapMarkers.length, 1);
  ch.take(10);
  ch.offer(1);
  assert.ok(ch.gapMarkers[0].endedAt);
});

test('historian delay change applies to the batch already in flight (stall then recovery)', async () => {
  const ch = new HistorianChannel({ capacity: 1000, batchSize: 100, nearOverflowRatio: 0.8, delayMs: 600000 });
  for (let i = 0; i < 900; i += 1) ch.offer({ t: i, ch: 'syn', v: 0 });
  ch.start();
  await new Promise((r) => setTimeout(r, 100));
  assert.equal(ch.written, 0, 'stalled write must not complete');
  assert.equal(ch.nearOverflow, true);
  ch.delayMs = 5; // backend "recovers"
  await new Promise((r) => setTimeout(r, 600));
  ch.stop();
  assert.equal(ch.depth, 0, `drained after recovery (depth ${ch.depth})`);
  assert.equal(ch.written, 900);
  assert.equal(ch.nearOverflow, false);
});

test('historian slowdown does not block revisions, SSE publishing, or pump stop', async () => {
  await withHarness(async (h) => {
    await sleep(1200);
    await cmd(h, 'historian-delay', { delayMs: 600000 });
    const burst = await cmd(h, 'historian-burst', { count: 45000 });
    assert.ok(burst.detail.accepted > 0);
    const rev0 = h.runtime.revision;
    await sleep(3200);
    const m = h.runtime.metricsReport();
    assert.ok(m.historian.nearOverflow, 'near overflow reported');
    assert.ok(h.runtime.revision - rev0 >= 3, `revisions advanced ${h.runtime.revision - rev0}`);
    await cmd(h, 'historian-burst', { count: 10000 });
    assert.ok(h.runtime.historian.rejected > 0);
    const t0 = performance.now();
    const stop = await cmd(h, 'pump-stop');
    const ms = performance.now() - t0;
    assert.equal(stop.accepted, true);
    assert.ok(ms < 250, `pump stop round-trip ${ms} ms`);
    await sleep(300);
    assert.equal((await snapshot(h)).pump.state, 'STOPPING');
    assert.ok(h.runtime.historian.depth <= h.runtime.historian.capacity);
  });
});

test('close guard refuses while Active Job or Pump running', async () => {
  await withHarness(async (h) => {
    await sleep(1300);
    await cmd(h, 'auto-jobs', { enabled: false });
    await cmd(h, 'abort-job', { immediate: true });
    let ev = await (await fetch(`${h.url}/api/spike/close-request`, { method: 'POST' })).json();
    assert.deepEqual(ev.reasons, ['PUMP_RUNNING']);
    assert.match(ev.note, /not a safety protection/);
    const job = await cmd(h, 'second-job-attempt');
    assert.equal(job.detail.first.accepted, true);
    ev = await (await fetch(`${h.url}/api/spike/close-request`, { method: 'POST' })).json();
    assert.deepEqual(ev.reasons.sort(), ['ACTIVE_JOB', 'PUMP_RUNNING']);
    // Expected commanded stop: the Active Job stays Active through the timed Mandatory Safe Return.
    const stop = await cmd(h, 'pump-stop');
    assert.equal(stop.detail.safeReturn, true);
    await sleep(2300);
    ev = await (await fetch(`${h.url}/api/spike/close-request`, { method: 'POST' })).json();
    assert.deepEqual(ev.reasons, ['ACTIVE_JOB'], 'Job still Active during Safe Return');
    await sleep(6500);
    ev = await (await fetch(`${h.url}/api/spike/close-request`, { method: 'POST' })).json();
    assert.equal(ev.allowed, true);
    const snap = await snapshot(h);
    assert.equal(snap.sequence.lastJobOutcome.trigger, 'SYN_COMMANDED_PUMP_STOP');
    assert.equal(snap.sequence.lastJobOutcome.outcome, 'ABORTED');
    assert.equal(snap.sequence.critical, null, 'commanded stop is not a critical event');
  });
});

test('critical-alarm-ack endpoint: same-origin loopback only, no token; ack is not a clear; modal state from runtime', async () => {
  await withHarness(async (h) => {
    await sleep(1200);
    await cmd(h, 'auto-jobs', { enabled: false });
    await cmd(h, 'abort-job', { immediate: true }); // no Job at the event: no Safe Return required
    const trip = await cmd(h, 'pump-trip');
    assert.equal(trip.accepted, true);
    const cross = await fetch(`${h.url}/api/spike/critical-alarm-ack`, { method: 'POST', headers: { 'sec-fetch-site': 'cross-site' } });
    assert.equal(cross.status, 403);
    assert.equal(h.runtime.pumpFault.acknowledged, false);
    const r = await (await fetch(`${h.url}/api/spike/critical-alarm-ack`, { method: 'POST', headers: { 'sec-fetch-site': 'same-origin' } })).json();
    assert.equal(r.accepted, true);
    assert.equal(r.detail.conditionActive, true, 'acknowledge does not clear the condition');
    await sleep(200);
    let snap = await snapshot(h);
    assert.equal(snap.sequence.critical.acknowledged, true);
    assert.equal(snap.sequence.critical.modalOpen, true);
    assert.equal(snap.sequence.autoSequence, 'CRITICAL_SUSPENDED');
    const again = await (await fetch(`${h.url}/api/spike/critical-alarm-ack`, { method: 'POST' })).json();
    assert.equal(again.reason, 'ALREADY_ACKNOWLEDGED');
    await cmd(h, 'pump-fault-clear');
    await sleep(200);
    snap = await snapshot(h);
    assert.equal(snap.sequence.critical.modalOpen, false);
    assert.equal(snap.sequence.autoSequence, 'CRITICAL_SUSPENDED');
    assert.deepEqual(validateSnapshot(snap), []);
    await cmd(h, 'critical-reset');
  });
});
