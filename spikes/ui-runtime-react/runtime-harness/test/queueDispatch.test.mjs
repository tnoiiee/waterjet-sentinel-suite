// WJSS Stage 0.2.1A — Owner GlobalQueue domain correction: hard-gate tests A..D and F
// (bounded synthetic queue, head-only atomic dispatch, Queue -> Job linkage, no retarget).
// SYNTHETIC SPIKE TESTS — not Production queue / scheduling validation.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { QUEUE_CAPACITY, QUEUE_REASONS, SyntheticRuntime } from '../src/runtime.mjs';
import { validateSnapshot } from '../../contracts/validate.mjs';

const PROHIBITED = /\b(BLOCKED|HELD|HELD_BY_OPERATOR|WAITING_FOR_PUMP|WAITING_FOR_EQUIPMENT|EXCLUDED|INVALID|OUT_OF_SERVICE|DISABLED|BAD|STALE)\b/;

/** In-process runtime (no scheduler) with an empty queue and the AutoSequence off. */
function fresh(extra = {}) {
  const rt = new SyntheticRuntime({ autoJobs: false, ...extra });
  rt.classifyAll(Date.now());
  rt.clearQueue();
  return rt;
}
/** In-process acquisition stand-in: copy the synthetic process scores into fresh GOOD samples. */
function prime(rt) {
  const now = Date.now();
  for (const x of rt.sensors) {
    const acq = rt.acq.get(x.sensorId);
    acq.rawScore = Math.round(rt.proc.get(x.sensorId).trueScore * 10) / 10;
    acq.sourceTs = now;
  }
  rt.classifyAll(now);
}
const pub = (rt) => rt.publish({ tick: false });
const snap = (rt) => {
  pub(rt);
  return rt.snapshot().snap;
};
/** Finish the active Job immediately (synthetic time travel) and publish once. */
function completeJob(rt) {
  const job = rt.activeJobs[0];
  assert.ok(job, 'no active job to complete');
  job.preCheck = 'PASSED';
  delete job.heldPhaseIndex;
  job.startedAtMs = Date.now() - 7 * rt.params.jobPhaseMs;
  rt.advanceJob(Date.now());
  assert.equal(rt.activeJobs.length, 0);
}
/** Deterministic queue of Sensor IDs via the synthetic non-score source (Water Jet never used). */
function seed(rt, ids, reason = 'TIME_DUE') {
  for (const id of ids) assert.equal(rt.enqueueWithReason(id, reason).accepted, true, id);
}

test('Gate A — capacity <= 8, no hidden overflow, no duplicates, Water Jet slots never queued', () => {
  const rt = fresh();
  rt.applyDirtyMode('dirty70');
  for (let i = 0; i < 5; i += 1) {
    rt.advanceProcess();
    prime(rt);
    const s = snap(rt);
    assert.equal(s.queue.entries.length, QUEUE_CAPACITY, 'dirty70 fills the bounded queue to 8');
    assert.equal(s.queue.totalQueued, s.queue.entries.length, 'no hidden overflow');
    assert.equal(rt.queue.length, s.queue.entries.length, 'physical queue == published queue');
    const ids = s.queue.entries.map((e) => e.sensorId);
    assert.equal(new Set(ids).size, ids.length);
    assert.ok(!ids.some((id) => /^CANNON_|^I7$|^I16$/.test(id)));
    assert.equal(s.sensors.filter((x) => x.queueState === 'QUEUED').length, ids.length);
    assert.deepEqual(validateSnapshot(s), []);
  }
  // Explicit admissions beyond capacity are refused (no overflow list).
  const outside = rt.sensors.find((s) => !rt.isQueued(s.sensorId)).sensorId;
  assert.equal(rt.dispatch('enqueue', { sensorId: outside, reason: 'TEMP' }).reason, 'QUEUE_FULL');
  assert.equal(rt.queue.length, QUEUE_CAPACITY);
  for (const id of ['CANNON_REAR', 'CANNON_FRONT']) assert.equal(rt.admit(id, QUEUE_REASONS.OPERATOR).admitted, false);
  // Duplicate admission never adds a second entry.
  const head = rt.queue[0].sensorId;
  rt.dispatch('dequeue', { sensorId: rt.queue[7].sensorId });
  assert.equal(rt.dispatch('enqueue', { sensorId: head, reason: 'TEMP' }).detail.duplicate, true);
  assert.equal(rt.queueOrder.filter((x) => x === head).length, 1);
  assert.equal(rt.violationCount, 0);
});

test('Gate A/E — queue entries carry no status; per-Sensor queueState is NONE | QUEUED | ACTIVE only', () => {
  const rt = fresh({ autoJobs: true });
  rt.applyDirtyMode('dirty70');
  prime(rt);
  const s = snap(rt);
  for (const e of s.queue.entries) {
    assert.deepEqual(Object.keys(e).sort(), ['dirtyScore', 'entryId', 'position', 'secondsSinceLastClean', 'sensorId', 'sourceReason']);
  }
  assert.ok(!PROHIBITED.test(JSON.stringify(s.queue.entries)), 'prohibited entry state text in queue entries');
  assert.ok(s.sensors.every((x) => ['NONE', 'QUEUED', 'ACTIVE'].includes(x.queueState)));
  assert.equal(s.queue.synthetic, true);
  assert.equal(s.queue.capacity, 8);
  // A wrongly-shaped entry is rejected by the validator.
  const bad = structuredClone(s);
  bad.queue.entries[0].status = 'BLOCKED';
  assert.ok(validateSnapshot(bad).some((m) => /carry no status/.test(m)));
  const big = structuredClone(s);
  big.queue.entries = Array.from({ length: 9 }, (_, i) => ({ ...s.queue.entries[0], position: i + 1, sensorId: s.sensors[i].sensorId }));
  big.queue.totalQueued = 9;
  assert.ok(validateSnapshot(big).some((m) => /exceeds capacity/.test(m)));
});

test('Gate B — Job target = former head; atomic removal; Position 2 becomes Position 1; no scan-forward', () => {
  // Owner Example B reproduced: G+110, G9, G8, I12 queued. The old model skipped non-READY
  // entries and started I12. Head-only dispatch must take G+110, whatever its alarm / quality.
  const rt = fresh();
  seed(rt, ['G+110', 'G9', 'G8', 'I12']);
  rt.raiseAlarm({ code: 'SYN-TEST', text: 'alarm on head', sensorId: 'G+110' });
  rt.acq.get('G9').forcedQuality = 'BAD';
  rt.acq.get('G8').forcedQuality = 'STALE';
  rt.classifyAll(Date.now());
  const revBefore = rt.queueRevision;
  const headEntry = rt.queue[0];
  rt.autoJobs = true;
  const s = snap(rt);
  assert.equal(s.activeJob.targetSensorId, 'G+110');
  assert.notEqual(s.activeJob.targetSensorId, 'I12');
  assert.deepEqual(rt.queueOrder.slice(0, 3), ['G9', 'G8', 'I12']);
  assert.equal(s.queue.entries[0].sensorId, 'G9');
  assert.equal(s.queue.entries[0].position, 1);
  const d = s.activeJob.dispatch;
  assert.equal(d.positionBefore, 1);
  assert.equal(d.queueEntryId, headEntry.entryId);
  assert.equal(d.queueRevisionBefore, revBefore);
  assert.equal(d.queueRevisionAfter, revBefore + 1);
  assert.ok(!s.queue.entries.some((e) => e.sensorId === 'G+110'), 'dispatched head removed');
  assert.deepEqual(validateSnapshot(s), []);
  // Owner Example A: G+217 must never start while G+110, G9, G8, I12 are queued ahead of it.
  const rt2 = fresh();
  seed(rt2, ['G+110', 'G9', 'G8', 'I12', 'G+217']);
  rt2.autoJobs = true;
  pub(rt2);
  assert.equal(rt2.activeJobs[0].targetSensorId, 'G+110');
});

test('Gate B — Job pre-check waits for the Pump; the entry is not kept in the queue as WAITING', () => {
  const rt = fresh();
  seed(rt, ['H3', 'H4']);
  assert.equal(rt.pumpReady(), false); // in-process runtime: no pressure samples yet
  const r = rt.dispatchHead('SYN_TEST');
  assert.equal(r.accepted, true);
  const s = snap(rt);
  assert.equal(s.activeJob.targetSensorId, 'H3');
  assert.equal(s.activeJob.preCheck, 'WAITING_FOR_PUMP');
  assert.equal(s.activeJob.phase, 'P1');
  assert.deepEqual(rt.queueOrder, ['H4']);
  assert.deepEqual(validateSnapshot(s), []);
});

test('Gate C — at most one Job; no dispatch while a Job is active; sequence A then B', () => {
  const rt = fresh();
  seed(rt, ['H5', 'H6', 'H7']);
  rt.autoJobs = true;
  pub(rt);
  assert.equal(rt.activeJobs[0].targetSensorId, 'H5');
  const dispatches = rt.dispatchSeq;
  for (let i = 0; i < 5; i += 1) pub(rt);
  assert.equal(rt.dispatchSeq, dispatches, 'no dispatch while a Job is active');
  assert.equal(rt.activeJobs.length, 1);
  assert.deepEqual(rt.queueOrder.slice(0, 2), ['H6', 'H7']);
  const second = rt.dispatchHead('SYN_TEST_SECOND');
  assert.equal(second.reason, 'ACTIVE_JOB_EXISTS');
  completeJob(rt);
  pub(rt);
  assert.equal(rt.activeJobs[0].targetSensorId, 'H6', 'after A ends only the new head (B) is the candidate');
  completeJob(rt);
  pub(rt);
  assert.equal(rt.activeJobs[0].targetSensorId, 'H7');
  assert.deepEqual(
    rt.dispatchLog.map((d) => d.sensorId).slice(0, 3),
    ['H5', 'H6', 'H7'],
  );
  assert.equal(rt.acceptedSecondJobs, 0);
  assert.equal(rt.violationCount, 0);
});

test('Gate C — AutoSequence pause is dispatch control, not a queue entry state', () => {
  const rt = fresh({ autoJobs: true });
  seed(rt, ['J2', 'J3']);
  rt.dispatch('pause-auto-sequence', { paused: true });
  const s = snap(rt);
  assert.equal(s.activeJob, null);
  assert.equal(s.queue.autoSequence, 'PAUSED');
  assert.deepEqual(s.queue.entries.map((e) => e.sensorId).slice(0, 2), ['J2', 'J3']);
  rt.dispatch('hold-queue', { held: false }); // legacy alias, same AutoSequence control
  pub(rt);
  assert.equal(rt.activeJobs[0].targetSensorId, 'J2');
});

test('Gate D — every automatic Job has a dispatch record (sensor = target, Position 1, monotonic revisions)', () => {
  const rt = fresh({ autoJobs: true });
  rt.applyDirtyMode('dirty70');
  let lastRev = -1;
  for (let i = 0; i < 12; i += 1) {
    prime(rt);
    pub(rt);
    const job = rt.activeJobs[0];
    assert.ok(job);
    assert.equal(job.dispatch.sensorId, job.targetSensorId);
    assert.equal(job.dispatch.jobId, job.jobId);
    assert.equal(job.dispatch.positionBefore, 1);
    assert.ok(rt.queueRevision >= lastRev);
    lastRev = rt.queueRevision;
    completeJob(rt);
  }
  const log = rt.dispatchLog;
  assert.ok(log.length >= 12);
  for (let i = 1; i < log.length; i += 1) {
    assert.ok(log[i].queueRevisionBefore >= log[i - 1].queueRevisionAfter, 'queue revisions monotonic across dispatches');
    assert.equal(log[i].queueRevisionAfter, log[i].queueRevisionBefore + 1);
  }
  assert.equal(new Set(log.map((d) => d.dispatchId)).size, log.length);
  assert.equal(rt.violationCount, 0);
  assert.equal(rt.metricsReport().queue.dispatches, rt.dispatchSeq);
});

test('Gate F — no arbitrary retarget; start-job / Review Job make the Sensor the head first; Reset is bounded', () => {
  const rt = fresh();
  seed(rt, ['G4', 'G5', 'G6']);
  // start-job for a non-head Sensor: prepared as the only head, then dispatched head-only.
  const r = rt.dispatch('start-job', { sensorId: 'G6' });
  assert.equal(r.accepted, true, JSON.stringify(r));
  assert.equal(r.detail.dispatch.sensorId, 'G6');
  assert.equal(r.detail.dispatch.positionBefore, 1);
  assert.equal(r.detail.preparedHead, true);
  // While the Job is active nothing can retarget it.
  for (const [c, p] of [
    ['start-job', { sensorId: 'G4' }],
    ['review-job', { sensorId: 'G4' }],
    ['second-job-attempt', {}],
    ['dispatch-head', {}],
  ]) {
    const x = rt.dispatch(c, p);
    assert.equal(x.accepted, false, c);
    assert.equal(x.reason, 'ACTIVE_JOB_EXISTS', c);
    assert.equal(rt.activeJobs[0].targetSensorId, 'G6', c);
  }
  // Review Job on the current target only freezes it (same Job, same dispatch record).
  const same = rt.dispatch('review-job', { sensorId: 'G6' });
  assert.equal(same.accepted, true);
  assert.equal(same.detail.dispatch.sensorId, 'G6');
  rt.abortJob('TEST');
  // Review Job with no active Job: head prepared first, then dispatched.
  seed(rt, ['G7', 'G8']);
  const rv = rt.dispatch('review-job', { sensorId: 'G8' });
  assert.equal(rv.accepted, true);
  assert.equal(rv.detail.dispatch.sensorId, 'G8');
  assert.equal(rt.activeJobs[0].targetSensorId, 'G8');
  // Reset returns to a valid bounded state.
  const reset = rt.visualPreset('reset');
  assert.equal(reset.accepted, true);
  const s = snap(rt);
  assert.ok(s.queue.entries.length <= QUEUE_CAPACITY);
  assert.deepEqual(validateSnapshot(s), []);
  assert.equal(rt.acceptedSecondJobs, 0);
  assert.equal(rt.violationCount, 0);
});
