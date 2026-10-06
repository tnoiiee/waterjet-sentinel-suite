// WJSS Stage 0.2.1A — mixed GlobalQueue sources and SYNTHETIC TEST CONTROL harness tests.
// SYNTHETIC SPIKE TESTS — not Production queue / alarm validation.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHarness } from '../src/server.mjs';
import { QUEUE_REASONS, SyntheticRuntime } from '../src/runtime.mjs';

const TOKEN = 'review-test-token';

async function withHarness(fn, opts = {}) {
  const h = createHarness({ port: 0, token: TOKEN, ...opts });
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
const sensorOf = (snap, id) => snap.sensors.find((s) => s.sensorId === id);
// GlobalQueue membership is published as QUEUED / READY / HELD (pump not ready) / BLOCKED (alarm)
// / EXCLUDED (quality); NONE = not queued, ACTIVE = Active Job target.
const isQueued = (s) => !['NONE', 'ACTIVE'].includes(s.queueState);
async function waitPumpReady(h) {
  for (let i = 0; i < 100; i++) {
    if ((await snapshot(h)).pump.ready) return;
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error('synthetic pump never became ready');
}

test('mixed-source GlobalQueue scenario: >= 3 source types in the first 8 rows, FIFO, owner kept, no Water Jet', async () => {
  await withHarness(async (h) => {
    const r = await cmd(h, 'queue-mixed-sources', {});
    assert.equal(r.accepted, true);
    const snap = await snapshot(h);
    const first8 = snap.queue.entries.slice(0, 8);
    const reasons = new Set(first8.map((e) => e.sourceReason));
    // Four explicit sources first (fixed order), then the score source.
    assert.deepEqual(
      first8.slice(0, 4).map((e) => e.sourceReason),
      [QUEUE_REASONS.TIME_DUE, QUEUE_REASONS.TEMP_AND_TIME, QUEUE_REASONS.OPERATOR, QUEUE_REASONS.TEMP],
    );
    assert.ok(reasons.has(QUEUE_REASONS.DIRTY_SCORE), 'score source present in the first 8 rows');
    assert.ok(reasons.size >= 3, `>= 3 source types in the first 8 rows (got ${reasons.size})`);
    // FIFO: positions are 1..n in order, IDs unique, no Water Jet slot.
    assert.deepEqual(snap.queue.entries.map((e) => e.position), snap.queue.entries.map((_, i) => i + 1));
    const ids = snap.queue.entries.map((e) => e.sensorId);
    assert.equal(new Set(ids).size, ids.length);
    assert.ok(!ids.some((id) => /^CANNON_|^I7$|^I16$/.test(id)));
    // De-duplication: a second source for an already-queued Sensor keeps owner and position.
    const head = first8[0];
    const dup = await cmd(h, 'enqueue', { sensorId: head.sensorId, reason: 'TEMP' });
    assert.equal(dup.accepted, true);
    assert.equal(dup.detail.duplicate, true);
    assert.equal(dup.detail.owner, QUEUE_REASONS.TIME_DUE);
    const after = await snapshot(h);
    assert.equal(after.queue.entries[0].sensorId, head.sensorId);
    assert.equal(after.queue.entries[0].sourceReason, QUEUE_REASONS.TIME_DUE);
    // Source ownership is stable across publishes (non-score entries are not released by score).
    await new Promise((res) => setTimeout(res, 600));
    const later = await snapshot(h);
    assert.deepEqual(
      later.queue.entries.slice(0, 4).map((e) => [e.sensorId, e.sourceReason]),
      first8.slice(0, 4).map((e) => [e.sensorId, e.sourceReason]),
    );
    assert.equal(later.runtime.invariantViolations, 0);
  });
});

test('Water Jet reference slots cannot be queued, alarmed, or targeted by review commands', async () => {
  await withHarness(async (h) => {
    for (const [c, p] of [
      ['enqueue', { sensorId: 'CANNON_REAR', reason: 'TEMP' }],
      ['visual-preset', { preset: 'alarm-queue-dirty', sensorId: 'CANNON_FRONT' }],
      ['review-job', { sensorId: 'CANNON_REAR' }],
      ['set-sensor-score', { sensorId: 'CANNON_FRONT', classification: 'DIRTY' }],
      ['raise-alarm', { sensorId: 'CANNON_REAR' }],
    ]) {
      const r = await cmd(h, c, p);
      assert.equal(r.accepted, false, c);
      assert.equal(r.reason, 'CANNON_NOT_A_SENSOR', c);
    }
  });
});

test('default behaviour unchanged: score source only by default, operator enqueue keeps OPERATOR reason', () => {
  const rt = new SyntheticRuntime({});
  rt.classifyAll(Date.now());
  rt.updateQueue();
  for (const id of rt.queueOrder) assert.equal(rt.queueReason.get(id), QUEUE_REASONS.DIRTY_SCORE);
  const free = rt.sensors.find((s) => !rt.queueReason.has(s.sensorId)).sensorId;
  assert.equal(rt.dispatch('enqueue', { sensorId: free }).accepted, true);
  assert.equal(rt.queueReason.get(free), QUEUE_REASONS.OPERATOR);
  assert.equal(rt.queueOrder.at(-1), free);
  assert.equal(rt.autoJobs, rt.params.autoJobs);
});

test('visual presets 1..5 produce the combined states; preset 6 resets; never two jobs', async () => {
  await withHarness(async (h) => {
    await waitPumpReady(h); // a held review job needs the synthetic pump ready (no bypass)
    const id = (await snapshot(h)).sensors[40].sensorId;
    let r = await cmd(h, 'visual-preset', { preset: 'alarm-queue-dirty', sensorId: id });
    assert.equal(r.accepted, true);
    let s = sensorOf(await snapshot(h), id);
    assert.equal(s.classification, 'DIRTY');
    assert.ok(isQueued(s), s.queueState);
    assert.equal(s.alarmState, 'ACTIVE_UNACK');

    r = await cmd(h, 'visual-preset', { preset: 'alarm-queue-cleaner', sensorId: id });
    s = sensorOf(await snapshot(h), id);
    assert.equal(s.classification, 'CLEANER');
    assert.ok(isQueued(s), s.queueState);
    assert.equal(s.alarmState, 'ACTIVE_UNACK');
    assert.equal(r.detail.queueOwner, QUEUE_REASONS.TIME_DUE);
    assert.equal(h.runtime.queueReason.get(id), QUEUE_REASONS.TIME_DUE);

    r = await cmd(h, 'visual-preset', { preset: 'selected-alarm-queue', sensorId: id });
    s = sensorOf(await snapshot(h), id);
    assert.ok(isQueued(s), s.queueState);
    assert.equal(s.alarmState, 'ACTIVE_UNACK');

    r = await cmd(h, 'visual-preset', { preset: 'job-alarm-queue', sensorId: id });
    assert.equal(r.accepted, true, JSON.stringify(r));
    let snap = await snapshot(h);
    s = sensorOf(snap, id);
    assert.equal(s.isActiveJobTarget, true);
    assert.equal(s.queueState, 'ACTIVE');
    assert.equal(s.alarmState, 'ACTIVE_UNACK');
    assert.equal(snap.activeJob.targetSensorId, id);
    assert.equal(snap.runtime.acceptedSecondJobs, 0);

    r = await cmd(h, 'visual-preset', { preset: 'cleared-ack-queue', sensorId: id });
    snap = await snapshot(h);
    s = sensorOf(snap, id);
    assert.equal(s.alarmState, 'CLEARED_UNACK');
    assert.ok(isQueued(s), s.queueState);
    assert.equal(s.isActiveJobTarget, false);
    assert.equal(snap.activeJob, null);

    r = await cmd(h, 'visual-preset', { preset: 'reset' });
    assert.equal(r.accepted, true);
    assert.ok(r.detail.restored.includes(id));
    s = sensorOf(await snapshot(h), id);
    assert.equal(s.alarmState, 'NONE');
    assert.ok(s.queueState === 'NONE' || h.runtime.queueReason.get(id) === QUEUE_REASONS.DIRTY_SCORE);
    assert.equal((await snapshot(h)).runtime.invariantViolations, 0);
  });
});

test('per-Sensor review controls: alarm raise / clear (ACK REQUIRED) / ack, queue reasons, remove, quality, job', async () => {
  await withHarness(async (h) => {
    await waitPumpReady(h);
    const id = (await snapshot(h)).sensors[12].sensorId;
    await cmd(h, 'raise-alarm', { sensorId: id });
    assert.equal(sensorOf(await snapshot(h), id).alarmState, 'ACTIVE_UNACK');
    await cmd(h, 'clear-alarm', { sensorId: id });
    assert.equal(sensorOf(await snapshot(h), id).alarmState, 'CLEARED_UNACK');
    await cmd(h, 'ack-alarm', { sensorId: id });
    assert.equal(sensorOf(await snapshot(h), id).alarmState, 'NONE');

    await cmd(h, 'dequeue', { sensorId: id });
    await cmd(h, 'set-sensor-score', { sensorId: id, classification: 'CLEANER' });
    const q = await cmd(h, 'enqueue', { sensorId: id, reason: 'TEMP_AND_TIME' });
    assert.equal(q.detail.owner, QUEUE_REASONS.TEMP_AND_TIME);
    assert.equal(h.runtime.queueReason.get(id), QUEUE_REASONS.TEMP_AND_TIME);
    assert.ok(isQueued(sensorOf(await snapshot(h), id)));
    const rm = await cmd(h, 'dequeue', { sensorId: id });
    assert.equal(rm.detail.removed, true);
    assert.equal(sensorOf(await snapshot(h), id).queueState, 'NONE');
    assert.equal((await cmd(h, 'enqueue', { sensorId: id, reason: 'BOGUS' })).reason, 'INVALID_QUEUE_REASON');

    for (const q2 of ['UNCERTAIN', 'BAD', 'STALE', 'GOOD']) {
      assert.equal((await cmd(h, 'force-quality', { sensorId: id, quality: q2 })).accepted, true, q2);
    }
    const j = await cmd(h, 'review-job', { sensorId: id, enabled: true });
    assert.equal(j.accepted, true, JSON.stringify(j));
    let snap = await snapshot(h);
    assert.equal(snap.activeJob.targetSensorId, id);
    // Retargeting never creates a second job.
    const other = snap.sensors[13].sensorId;
    await cmd(h, 'review-job', { sensorId: other, enabled: true });
    snap = await snapshot(h);
    assert.equal(snap.activeJob.targetSensorId, other);
    assert.equal(snap.runtime.acceptedSecondJobs, 0);
    assert.equal((await cmd(h, 'review-job', { sensorId: other, enabled: false })).accepted, true);
    assert.equal((await snapshot(h)).activeJob, null);
    assert.equal((await cmd(h, 'reset-sensor', { sensorId: id })).accepted, true);
  });
});

test('test-controls token endpoint: absent by default; opt-in; same-origin loopback only; no-store', async () => {
  await withHarness(async (h) => {
    const r = await fetch(`${h.url}/api/spike/test-controls`);
    assert.equal(r.status, 404);
  });
  // Also absent (JSON 404, not the SPA index.html fallback) when the UI is served statically.
  await withHarness(
    async (h) => {
      const r = await fetch(`${h.url}/api/spike/test-controls`);
      assert.equal(r.status, 404);
      assert.match(r.headers.get('content-type') ?? '', /application\/json/);
    },
    { staticDir: new URL('../../react-ui', import.meta.url).pathname },
  );
  await withHarness(
    async (h) => {
      const r = await fetch(`${h.url}/api/spike/test-controls`);
      assert.equal(r.status, 200);
      assert.match(r.headers.get('cache-control') ?? '', /no-store/);
      assert.equal(r.headers.get('access-control-allow-origin'), null);
      const body = await r.json();
      assert.equal(body.enabled, true);
      assert.equal(body.token, TOKEN);
      assert.equal(body.scope, 'synthetic-scenario-api');
      const cross = await fetch(`${h.url}/api/spike/test-controls`, { headers: { 'sec-fetch-site': 'cross-site' } });
      assert.equal(cross.status, 403);
      const same = await fetch(`${h.url}/api/spike/test-controls`, { headers: { 'sec-fetch-site': 'same-origin' } });
      assert.equal(same.status, 200);
    },
    { testControls: true },
  );
});
