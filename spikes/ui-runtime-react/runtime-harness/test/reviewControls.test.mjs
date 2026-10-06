// WJSS Stage 0.2.1A — mixed GlobalQueue sources and SYNTHETIC TEST CONTROL harness tests.
// SYNTHETIC SPIKE TESTS — not Production queue / alarm validation.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHarness } from '../src/server.mjs';
import { NOT_ADMITTED_DEMO_REASON, PRESETS, QUEUE_CAPACITY, QUEUE_REASONS, SyntheticRuntime } from '../src/runtime.mjs';
import { validateSnapshot } from '../../contracts/validate.mjs';

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
// GlobalQueue membership per Sensor: QUEUED (ready-to-dispatch entry), ACTIVE (Job target), NONE.
const isQueued = (s) => s.queueState === 'QUEUED';
test('mixed-source GlobalQueue scenario: bounded to 8, >= 3 source types, FIFO, owner kept, no Water Jet', async () => {
  await withHarness(async (h) => {
    const r = await cmd(h, 'queue-mixed-sources', {});
    assert.equal(r.accepted, true);
    const snap = await snapshot(h);
    assert.ok(snap.queue.entries.length <= QUEUE_CAPACITY);
    assert.equal(snap.queue.totalQueued, snap.queue.entries.length);
    assert.equal(r.detail.totalQueued, snap.queue.entries.length);
    const first8 = snap.queue.entries;
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
      ['visual-preset', { preset: 'queued-dirty', sensorId: 'CANNON_FRONT' }],
      ['review-job', { sensorId: 'CANNON_REAR' }],
      ['start-job', { sensorId: 'CANNON_FRONT' }],
      ['set-sensor-score', { sensorId: 'CANNON_FRONT', classification: 'DIRTY' }],
      ['raise-alarm', { sensorId: 'CANNON_REAR' }],
    ]) {
      const r = await cmd(h, c, p);
      assert.equal(r.accepted, false, c);
      assert.equal(r.reason, 'CANNON_NOT_A_SENSOR', c);
    }
  });
});

test('default behaviour: synthetic score source only, bounded to 8; operator enqueue keeps OPERATOR reason', () => {
  const rt = new SyntheticRuntime({ autoJobs: false });
  rt.classifyAll(Date.now());
  rt.updateQueue();
  assert.ok(rt.queue.length <= QUEUE_CAPACITY);
  for (const e of rt.queue) assert.equal(e.reason, QUEUE_REASONS.DIRTY_SCORE);
  rt.clearQueue();
  const free = rt.sensors.find((s) => !rt.isQueued(s.sensorId)).sensorId;
  assert.equal(rt.dispatch('enqueue', { sensorId: free }).accepted, true);
  assert.equal(rt.queueEntry(free).reason, QUEUE_REASONS.OPERATOR);
  assert.equal(rt.queueOrder.at(-1), free);
});

test('eight review presets: valid queue / job states, head-only dispatch, never two jobs, Reset bounded', async () => {
  await withHarness(async (h) => {
    assert.equal(PRESETS.length, 7); // + 'reset' = 8 controls
    const id = (await snapshot(h)).sensors[40].sensorId;
    const valid = (snap) => assert.deepEqual(validateSnapshot(snap), []);

    // 1. Queued DIRTY Sensor (score source) at Position 1.
    let r = await cmd(h, 'visual-preset', { preset: 'queued-dirty', sensorId: id });
    assert.equal(r.accepted, true, JSON.stringify(r));
    let snap = await snapshot(h);
    valid(snap);
    let s = sensorOf(snap, id);
    assert.equal(s.classification, 'DIRTY');
    assert.equal(s.queueState, 'QUEUED');
    assert.equal(snap.queue.entries[0].sensorId, id);
    assert.equal(snap.queue.entries[0].sourceReason, QUEUE_REASONS.DIRTY_SCORE);
    assert.equal(snap.activeJob, null);

    // 2. Queued CLEANER Sensor from a synthetic non-score source.
    r = await cmd(h, 'visual-preset', { preset: 'queued-cleaner-non-score', sensorId: id });
    snap = await snapshot(h);
    valid(snap);
    s = sensorOf(snap, id);
    assert.equal(s.classification, 'CLEANER');
    assert.equal(s.queueState, 'QUEUED');
    assert.equal(r.detail.queueOwner, QUEUE_REASONS.OPERATOR);
    assert.equal(snap.queue.entries[0].sensorId, id);

    // 3. Selected queued Sensor (not the head: Position 2).
    r = await cmd(h, 'visual-preset', { preset: 'selected-queued', sensorId: id });
    assert.equal(r.detail.queuePosition, 2);
    snap = await snapshot(h);
    valid(snap);
    assert.equal(snap.queue.entries[1].sensorId, id);

    // 4. Dispatched head becomes the Active Job (held review Job), atomic removal.
    r = await cmd(h, 'visual-preset', { preset: 'dispatched-head-job', sensorId: id });
    assert.equal(r.accepted, true, JSON.stringify(r));
    assert.equal(r.detail.queueBefore[0], id);
    assert.deepEqual(r.detail.queueAfter, r.detail.queueBefore.slice(1));
    snap = await snapshot(h);
    valid(snap);
    assert.equal(snap.activeJob.targetSensorId, id);
    assert.equal(snap.activeJob.dispatch.positionBefore, 1);
    assert.equal(snap.activeJob.dispatch.sensorId, id);
    assert.equal(sensorOf(snap, id).queueState, 'ACTIVE');
    assert.ok(!snap.queue.entries.some((e) => e.sensorId === id));

    // 5. Alarm on the Active Job Sensor.
    r = await cmd(h, 'visual-preset', { preset: 'alarm-on-active-job', sensorId: id });
    snap = await snapshot(h);
    valid(snap);
    s = sensorOf(snap, id);
    assert.equal(s.isActiveJobTarget, true);
    assert.equal(s.alarmState, 'ACTIVE_UNACK');

    // 6. Alarm Sensor not admitted (synthetic demonstration; policy pending).
    r = await cmd(h, 'visual-preset', { preset: 'alarm-not-admitted', sensorId: id });
    snap = await snapshot(h);
    valid(snap);
    s = sensorOf(snap, id);
    assert.equal(s.alarmState, 'ACTIVE_UNACK');
    assert.equal(s.classification, 'DIRTY');
    assert.equal(s.queueState, 'NONE');
    assert.deepEqual(snap.queue.eligibilityDiagnostics, [{ sensorId: id, decision: 'NOT_ADMITTED', reason: NOT_ADMITTED_DEMO_REASON, synthetic: true }]);
    assert.equal(snap.activeJob, null);

    // 7. Queue head -> Job atomic transition (AutoSequence dispatches Position 1; the Job runs).
    r = await cmd(h, 'visual-preset', { preset: 'head-to-job-transition', sensorId: id });
    assert.equal(r.accepted, true, JSON.stringify(r));
    assert.equal(r.detail.dispatch.sensorId, r.detail.queueBefore[0]);
    assert.equal(r.detail.dispatch.queueRevisionAfter, r.detail.dispatch.queueRevisionBefore + 1);
    snap = await snapshot(h);
    valid(snap);
    assert.equal(snap.activeJob.targetSensorId, id);
    assert.equal(snap.queue.entries[0].sensorId, r.detail.queueBefore[1]); // old Position 2 is now Position 1
    assert.equal(snap.queue.eligibilityDiagnostics.length, 0);

    // 8. Reset: valid bounded queue state, demonstration cleared.
    r = await cmd(h, 'visual-preset', { preset: 'reset' });
    assert.equal(r.accepted, true);
    assert.ok(r.detail.restored.includes(id));
    snap = await snapshot(h);
    valid(snap);
    assert.ok(snap.queue.entries.length <= QUEUE_CAPACITY);
    assert.equal(snap.queue.eligibilityDiagnostics.length, 0);
    assert.equal(sensorOf(snap, id).alarmState, 'NONE');
    assert.equal(snap.runtime.acceptedSecondJobs, 0);
    assert.equal(snap.runtime.invariantViolations, 0);
    assert.equal((await cmd(h, 'visual-preset', { preset: 'alarm-queue-dirty', sensorId: id })).reason, 'UNKNOWN_PRESET');
  });
});

test('per-Sensor review controls: alarm raise / clear / ack, queue reasons, remove, quality, Review Job (no retarget)', async () => {
  await withHarness(async (h) => {
    await cmd(h, 'auto-jobs', { enabled: false });
    await cmd(h, 'abort-job');
    const id = (await snapshot(h)).sensors[12].sensorId;
    await cmd(h, 'raise-alarm', { sensorId: id });
    assert.equal(sensorOf(await snapshot(h), id).alarmState, 'ACTIVE_UNACK');
    await cmd(h, 'clear-alarm', { sensorId: id });
    assert.equal(sensorOf(await snapshot(h), id).alarmState, 'CLEARED_UNACK');
    await cmd(h, 'ack-alarm', { sensorId: id });
    assert.equal(sensorOf(await snapshot(h), id).alarmState, 'NONE');

    await cmd(h, 'dequeue', { sensorId: id });
    await cmd(h, 'set-sensor-score', { sensorId: id, classification: 'CLEANER' });
    // The live score source keeps the bounded queue full; make room and admit in one synchronous
    // step (no publish in between), then confirm a full queue refuses admission (no overflow).
    h.runtime.clearQueue();
    const q = h.runtime.command('enqueue', { sensorId: id, reason: 'TEMP_AND_TIME' });
    // Fill the remaining capacity explicitly (deterministic; independent of how many are DIRTY).
    for (const x of h.runtime.sensors) {
      if (h.runtime.queue.length >= QUEUE_CAPACITY) break;
      if (!h.runtime.isQueued(x.sensorId)) h.runtime.command('enqueue', { sensorId: x.sensorId, reason: 'TIME_DUE' });
    }
    assert.equal(h.runtime.queue.length, QUEUE_CAPACITY);
    const outside = h.runtime.sensors.find((x) => !h.runtime.isQueued(x.sensorId)).sensorId;
    assert.equal(h.runtime.command('enqueue', { sensorId: outside, reason: 'TEMP' }).reason, 'QUEUE_FULL');
    assert.equal(q.accepted, true, JSON.stringify(q));
    assert.equal(q.detail.owner, QUEUE_REASONS.TEMP_AND_TIME);
    assert.equal(h.runtime.queueEntry(id).reason, QUEUE_REASONS.TEMP_AND_TIME);
    assert.ok(isQueued(sensorOf(await snapshot(h), id)));
    const rm = await cmd(h, 'dequeue', { sensorId: id });
    assert.equal(rm.detail.removed, true);
    assert.equal(sensorOf(await snapshot(h), id).queueState, 'NONE');
    assert.equal((await cmd(h, 'enqueue', { sensorId: id, reason: 'BOGUS' })).reason, 'INVALID_QUEUE_REASON');

    for (const q2 of ['UNCERTAIN', 'BAD', 'STALE', 'GOOD']) {
      assert.equal((await cmd(h, 'force-quality', { sensorId: id, quality: q2 })).accepted, true, q2);
    }
    // Review Job: the Sensor is prepared as Position 1 first, then dispatched head-only.
    const j = await cmd(h, 'review-job', { sensorId: id, enabled: true });
    assert.equal(j.accepted, true, JSON.stringify(j));
    assert.equal(j.detail.preparedHead, true);
    assert.equal(j.detail.dispatch.sensorId, id);
    assert.equal(j.detail.dispatch.positionBefore, 1);
    let snap = await snapshot(h);
    assert.equal(snap.activeJob.targetSensorId, id);
    // No arbitrary retarget: Review Job for another Sensor is refused while the Job is active.
    const other = snap.sensors[13].sensorId;
    const re = await cmd(h, 'review-job', { sensorId: other, enabled: true });
    assert.equal(re.accepted, false);
    assert.equal(re.reason, 'ACTIVE_JOB_EXISTS');
    snap = await snapshot(h);
    assert.equal(snap.activeJob.targetSensorId, id);
    assert.equal(snap.runtime.acceptedSecondJobs, 0);
    assert.equal((await cmd(h, 'review-job', { sensorId: id, enabled: false })).accepted, true);
    assert.equal((await snapshot(h)).activeJob, null);
    assert.equal((await cmd(h, 'reset-sensor', { sensorId: id })).accepted, true);
    assert.equal((await snapshot(h)).runtime.invariantViolations, 0);
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
