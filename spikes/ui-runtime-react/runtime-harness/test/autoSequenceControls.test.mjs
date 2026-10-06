// WJSS Stage 0.2.1A — Owner final closeout: synthetic AutoSequence controls (START, PAUSE AFTER
// CURRENT JOB, RESUME, ABORT ACTIVE JOB, RESET CRITICAL SCENARIO). Gates A..G.
// SYNTHETIC SPIKE TESTS — review tooling only, not the Production operator-control model. The
// Production Pause / Resume policy, roles and permissions are OWNER DECISION REQUIRED.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JOB_LIFECYCLE, QUEUE_CAPACITY, SyntheticRuntime } from '../src/runtime.mjs';
import { validateSnapshot } from '../../contracts/validate.mjs';

const PROHIBITED = /\b(BLOCKED|HELD|HELD_BY_OPERATOR|WAITING_FOR_PUMP|WAITING_FOR_EQUIPMENT|EXCLUDED|PAUSED|SUSPENDED)\b/;

/** In-process runtime: empty queue, AutoSequence OFF, synthetic Pump ready. */
function fresh(extra = {}) {
  const rt = new SyntheticRuntime({ autoJobs: false, ...extra });
  rt.classifyAll(Date.now());
  rt.clearQueue();
  prime(rt);
  return rt;
}
function prime(rt) {
  rt.pressure.values = [100, 97, 94, 95];
  rt.pressure.sourceTs = Date.now();
}
const seed = (rt, ids) => ids.forEach((id) => assert.equal(rt.enqueueWithReason(id, 'TIME_DUE').accepted, true, id));
/** Publish one revision and return a validated Snapshot. */
function snap(rt) {
  rt.publish({ tick: false });
  const s = rt.snapshot().snap;
  assert.deepEqual(validateSnapshot(s), [], 'snapshot validates');
  assertQueueInvariants(s);
  return s;
}
function assertQueueInvariants(s) {
  assert.ok(s.queue.entries.length <= QUEUE_CAPACITY);
  assert.equal(new Set(s.queue.entries.map((e) => e.sensorId)).size, s.queue.entries.length);
  for (const e of s.queue.entries) {
    assert.equal('status' in e, false, 'queue entries carry no status');
    assert.ok(!/^CANNON/.test(e.sensorId) && !/WJ/.test(e.sensorId), 'Water Jet references never queued');
  }
  assert.ok(!PROHIBITED.test(JSON.stringify(s.queue.entries)), 'no queue-level entry state');
}
const cmd = (rt, name, params = {}) => rt.command(name, params);
const order = (rt) => rt.queueOrder;
/** Run the Job to the end of its cleaning phases and through the timed Mandatory Safe Return. */
function finishJob(rt) {
  const job = rt.activeJobs[0];
  const now = Date.now();
  job.startedAtMs = now - 6 * rt.params.jobPhaseMs - 100;
  return driveSafeReturn(rt, now);
}
function driveSafeReturn(rt, fromMs = Date.now(), toMs = fromMs + 60000) {
  const seen = [];
  for (let t = fromMs; t <= toMs && rt.activeJobs.length; t += 250) {
    rt.advanceJob(t);
    const j = rt.activeJobs[0];
    if (j && seen.at(-1) !== j.lifecycle) seen.push(j.lifecycle);
  }
  return seen;
}
const srSteps = (events) => events.filter((e) => e.step).map((e) => e.step);

test('A — START AUTOSEQUENCE dispatches queue Position 1 only; the selection never affects the target', () => {
  const rt = fresh();
  assert.equal(cmd(rt, 'autosequence-start').reason, 'QUEUE_EMPTY');
  seed(rt, ['H5', 'H6', 'H7']);
  rt.pressure.values = [null, null, null, null];
  assert.equal(cmd(rt, 'autosequence-start').reason, 'PUMP_NOT_READY');
  prime(rt);
  let s = snap(rt);
  assert.equal(s.sequence.mode, 'OFF');
  assert.deepEqual(s.sequence.controls.start, { enabled: true, reason: null });
  assert.equal(s.sequence.controls.resume.reason, 'NOT_PAUSED');
  const dispatchesBefore = rt.dispatchSeq;
  // A selected / requested Sensor is ignored: START has no target parameter.
  const r = cmd(rt, 'autosequence-start', { sensorId: 'J12' });
  assert.equal(r.accepted, true);
  assert.equal(rt.dispatchSeq, dispatchesBefore + 1, 'exactly one dispatch');
  const job = rt.activeJobs[0];
  assert.equal(job.targetSensorId, 'H5');
  assert.equal(job.dispatch.positionBefore, 1);
  assert.equal(job.dispatch.sensorId, 'H5');
  assert.equal(job.dispatch.origin, 'SYN_AUTOSEQUENCE_START');
  assert.deepEqual(order(rt).slice(0, 2), ['H6', 'H7'], 'FIFO preserved for the remaining entries');
  s = snap(rt);
  assert.equal(s.sequence.mode, 'RUNNING');
  assert.equal(s.sequence.autoSequence, 'JOB_ACTIVE');
  assert.equal(s.activeJob.targetSensorId, 'H5');
  assert.equal(cmd(rt, 'autosequence-start').reason, 'ACTIVE_JOB_PRESENT', 'repeat START never dispatches a second Job');
  assert.equal(rt.dispatchSeq, dispatchesBefore + 1);
  assert.equal(rt.acceptedSecondJobs, 0);
  assert.equal(rt.violationCount, 0);
});

test('B — PAUSE AFTER CURRENT JOB: Job continues through Safe Return, no next dispatch, then PAUSED', () => {
  const rt = fresh();
  seed(rt, ['H5', 'H6', 'H7']);
  cmd(rt, 'autosequence-start');
  const job = rt.activeJobs[0];
  const r = cmd(rt, 'autosequence-pause-after-current-job');
  assert.equal(r.accepted, true);
  let s = snap(rt);
  assert.equal(s.sequence.mode, 'PAUSE_REQUESTED');
  assert.equal(s.sequence.autoSequence, 'PAUSE_REQUESTED');
  assert.equal(s.activeJob.jobId, job.jobId, 'current Job continues');
  assert.equal(s.activeJob.lifecycle, JOB_LIFECYCLE.RUNNING, 'not a mid-phase freeze');
  assert.equal(s.sequence.controls.pauseAfterCurrentJob.reason, 'PAUSE_ALREADY_REQUESTED');
  assert.equal(s.sequence.controls.resume.reason, 'PAUSE_REQUESTED_JOB_ACTIVE');
  const dispatches = rt.dispatchSeq;
  const seen = finishJob(rt);
  assert.ok(seen.includes(JOB_LIFECYCLE.VERIFY_STANDBY) || seen.includes('SAFE_RETURN_VERIFY_STANDBY'));
  assert.equal(rt.lastJobOutcome.jobId, job.jobId);
  assert.equal(rt.lastJobOutcome.outcome, 'COMPLETED');
  assert.deepEqual(srSteps(rt.lastJobOutcome.events), ['SR1', 'SR2', 'SR3', 'SR4', 'SR5', 'SR6', 'SR7', 'SR8']);
  assert.equal(rt.lastJobOutcome.events.at(-1).event, 'LATER_DISPATCH_NOT_PERMITTED_PAUSED');
  for (let i = 0; i < 3; i += 1) s = snap(rt);
  assert.equal(rt.dispatchSeq, dispatches, 'no B dispatch');
  assert.equal(s.activeJob, null);
  assert.equal(s.sequence.mode, 'PAUSED');
  assert.equal(order(rt)[0], 'H6', 'queue head remains B');
  assert.equal(s.sequence.controls.resume.enabled, true);
});

test('C — PAUSE without an Active Job: PAUSED immediately, queue unchanged, no Job', () => {
  const rt = fresh();
  seed(rt, ['J2', 'J3']);
  assert.equal(cmd(rt, 'autosequence-pause-after-current-job').reason, 'AUTOSEQUENCE_OFF');
  rt.autoJobs = true; // RUNNING, no Job yet (no publish in between)
  const rev = rt.queueRevision;
  const r = cmd(rt, 'autosequence-pause-after-current-job');
  assert.equal(r.accepted, true);
  assert.equal(r.detail.mode, 'PAUSED');
  let s;
  for (let i = 0; i < 3; i += 1) s = snap(rt);
  assert.equal(s.activeJob, null);
  assert.equal(s.sequence.mode, 'PAUSED');
  assert.deepEqual(order(rt).slice(0, 2), ['J2', 'J3']);
  assert.equal(rt.dispatchSeq, 0);
  assert.ok(rt.queueRevision >= rev);
});

test('D — RESUME only from PAUSED: dispatches the current head; refused while critical or with a Job', () => {
  const rt = fresh();
  seed(rt, ['H5', 'H6', 'H7']);
  cmd(rt, 'autosequence-start');
  cmd(rt, 'autosequence-pause-after-current-job');
  assert.equal(cmd(rt, 'autosequence-resume').reason, 'PAUSE_REQUESTED_JOB_ACTIVE', 'Resume with an Active Job is refused');
  finishJob(rt);
  snap(rt);
  snap(rt);
  const r = cmd(rt, 'autosequence-resume');
  assert.equal(r.accepted, true);
  assert.equal(rt.activeJobs[0].targetSensorId, 'H6');
  assert.equal(rt.activeJobs[0].dispatch.positionBefore, 1);
  assert.equal(rt.activeJobs[0].dispatch.origin, 'SYN_AUTOSEQUENCE_RESUME');
  assert.equal(order(rt)[0], 'H7');
  assert.equal(snap(rt).sequence.mode, 'RUNNING');
  assert.equal(cmd(rt, 'autosequence-resume').reason, 'NOT_PAUSED');
  // Critical: Resume never clears CRITICAL_SUSPENDED.
  cmd(rt, 'pump-trip');
  const s = snap(rt);
  assert.equal(s.sequence.mode, 'CRITICAL_SUSPENDED');
  assert.equal(s.sequence.controls.resume.reason, 'CRITICAL_SUSPENDED');
  assert.equal(s.sequence.controls.start.reason, 'CRITICAL_RESET_REQUIRED');
  assert.equal(cmd(rt, 'autosequence-resume').reason, 'CRITICAL_SUSPENDED');
  assert.equal(cmd(rt, 'autosequence-pause-after-current-job').reason, 'CRITICAL_SUSPENDED');
  assert.equal(snap(rt).sequence.autoSequence, 'CRITICAL_SUSPENDED');
});

test('E — Critical reset: Clear + Acknowledge leave CRITICAL_SUSPENDED; reset gated; OFF; no dispatch; explicit START', () => {
  const rt = fresh();
  seed(rt, ['H5', 'H6', 'H7']);
  cmd(rt, 'autosequence-start');
  const job = rt.activeJobs[0];
  const alarm = cmd(rt, 'raise-alarm', { sensorId: 'J12' }).detail.alarmId; // unrelated Alarm
  cmd(rt, 'pump-trip');
  const frozen = [...order(rt)];
  const dispatches = rt.dispatchSeq;
  assert.equal(cmd(rt, 'critical-review-reset').reason, 'PUMP_CONDITION_ACTIVE');
  cmd(rt, 'pump-fault-clear');
  assert.equal(cmd(rt, 'critical-review-reset').reason, 'CRITICAL_NOT_ACKNOWLEDGED');
  cmd(rt, 'critical-alarm-ack');
  let s = snap(rt);
  assert.equal(s.sequence.autoSequence, 'CRITICAL_SUSPENDED', 'Clear + Acknowledge alone do not Resume');
  assert.equal(s.sequence.controls.resume.enabled, false);
  assert.equal(s.sequence.controls.resetCritical.reason, 'SAFE_RETURN_INCOMPLETE');
  assert.equal(cmd(rt, 'critical-review-reset').reason, 'SAFE_RETURN_INCOMPLETE');
  assert.equal(rt.activeJobs[0], job, 'Job Active until Safe Return completes');
  driveSafeReturn(rt);
  assert.equal(rt.lastJobOutcome.outcome, 'ABORTED');
  s = snap(rt);
  assert.equal(s.sequence.critical.modalOpen, false);
  assert.equal(s.sequence.autoSequence, 'CRITICAL_SUSPENDED');
  assert.deepEqual(s.sequence.controls.resetCritical, { enabled: true, reason: null });
  const pumpBefore = rt.pump.state;
  const r = cmd(rt, 'critical-review-reset');
  assert.equal(r.accepted, true);
  assert.equal(r.detail.mode, 'OFF');
  assert.equal(cmd(rt, 'critical-review-reset').reason, 'NO_CRITICAL_SCENARIO');
  for (let i = 0; i < 3; i += 1) s = snap(rt);
  assert.equal(s.sequence.mode, 'OFF');
  assert.equal(s.sequence.critical, null);
  assert.equal(s.activeJob, null, 'reset never creates a Job');
  assert.equal(rt.dispatchSeq, dispatches, 'reset never dispatches');
  assert.deepEqual(order(rt).slice(0, frozen.length), frozen, 'queue entries and order preserved');
  assert.equal(rt.pump.state, pumpBefore, 'reset does not start the Pump');
  assert.ok(rt.alarms.has(alarm), 'unrelated Alarm state untouched');
  assert.equal(s.sequence.controls.start.reason, 'PUMP_NOT_READY');
  assert.equal(cmd(rt, 'pump-start').accepted, true);
  rt.setPump('RUNNING', Date.now() - 10000); // synthetic time travel past the Pump start ramp
  prime(rt);
  s = snap(rt);
  assert.equal(s.activeJob, null, 'still no Job without an explicit START');
  assert.equal(s.sequence.controls.start.enabled, true);
  cmd(rt, 'autosequence-start');
  assert.equal(rt.activeJobs[0].targetSensorId, frozen[0], 'explicit START dispatches the queue head');
  assert.equal(rt.violationCount, 0);
});

test('E2 — Critical reset refused while a Safe Return failure remains', () => {
  const rt = fresh();
  seed(rt, ['H5', 'H6']);
  cmd(rt, 'autosequence-start');
  cmd(rt, 'safe-return-config', { valveFeedback: 'ABSENT' });
  cmd(rt, 'pump-trip');
  cmd(rt, 'pump-fault-clear');
  cmd(rt, 'critical-alarm-ack');
  driveSafeReturn(rt, Date.now(), Date.now() + 40000);
  assert.equal(rt.activeJobs[0].lifecycle, JOB_LIFECYCLE.FAILED);
  assert.equal(cmd(rt, 'critical-review-reset').reason, 'SAFE_RETURN_FAILED');
  assert.equal(snap(rt).sequence.autoSequence, 'CRITICAL_SUSPENDED');
});

test('F — ABORT ACTIVE JOB: ABORTING, ordered Mandatory Safe Return, Active until Standby; next-dispatch rules', () => {
  const rt = fresh();
  assert.equal(cmd(rt, 'abort-active-job').reason, 'NO_ACTIVE_JOB');
  seed(rt, ['H5', 'H6', 'H7']);
  cmd(rt, 'autosequence-start');
  const job = rt.activeJobs[0];
  const r = cmd(rt, 'abort-active-job');
  assert.equal(r.accepted, true);
  assert.equal(job.lifecycle, JOB_LIFECYCLE.ABORTING);
  assert.equal(cmd(rt, 'abort-active-job').reason, 'SAFE_RETURN_IN_PROGRESS', 'one click, one abort');
  const seen = [];
  const t0 = Date.now();
  for (let t = t0; t <= t0 + 60000 && rt.activeJobs.length; t += 250) {
    rt.advanceJob(t);
    if (rt.activeJobs.length) {
      assert.equal(rt.activeJobs[0], job, 'Job stays Active until Standby is confirmed');
      if (seen.at(-1) !== job.lifecycle) seen.push(job.lifecycle);
    }
  }
  assert.deepEqual(seen.filter((l) => l !== JOB_LIFECYCLE.ABORTING), [
    'SAFE_RETURN_CLOSE_VALVE',
    'SAFE_RETURN_VERIFY_VALVE_CLOSED',
    'SAFE_RETURN_TO_STANDBY',
    'SAFE_RETURN_VERIFY_STANDBY',
  ]);
  const o = rt.lastJobOutcome;
  assert.equal(o.outcome, 'ABORTED');
  assert.equal(o.trigger, 'SYN_OPERATOR_ABORT');
  assert.ok(o.valveClosedConfirmedSeq < o.axisReturnCommandSeq && o.standbyConfirmedSeq < o.releaseSeq);
  // RUNNING: documented synthetic rule — no dispatch in the release revision; next cycle = head.
  let s = snap(rt);
  assert.equal(s.activeJob, null, 'no dispatch in the release revision');
  s = snap(rt);
  assert.equal(s.activeJob.targetSensorId, 'H6', 'head-only dispatch at the next cycle');
  assert.equal(rt.acceptedSecondJobs, 0);
});

test('F2 — ABORT with PAUSE_REQUESTED ends PAUSED; with CRITICAL_SUSPENDED stays suspended; no next Job', () => {
  const rt = fresh();
  seed(rt, ['H5', 'H6', 'H7']);
  cmd(rt, 'autosequence-start');
  cmd(rt, 'autosequence-pause-after-current-job');
  cmd(rt, 'abort-active-job');
  driveSafeReturn(rt);
  let s;
  for (let i = 0; i < 3; i += 1) s = snap(rt);
  assert.equal(s.sequence.mode, 'PAUSED');
  assert.equal(s.activeJob, null);
  assert.equal(order(rt)[0], 'H6');
  cmd(rt, 'autosequence-resume');
  assert.equal(rt.activeJobs[0].targetSensorId, 'H6');
  cmd(rt, 'pump-trip');
  assert.equal(cmd(rt, 'abort-active-job').reason, 'SAFE_RETURN_IN_PROGRESS', 'trip already started Safe Return');
  const dispatches = rt.dispatchSeq;
  driveSafeReturn(rt);
  for (let i = 0; i < 3; i += 1) s = snap(rt);
  assert.equal(s.sequence.autoSequence, 'CRITICAL_SUSPENDED');
  assert.equal(s.activeJob, null);
  assert.equal(rt.dispatchSeq, dispatches, 'no next Job after the Pump trip');
  assert.equal(rt.acceptedSecondJobs, 0);
});

test('G — invariants across a full control sequence: one Job, ≤ 8, head-only, Water Jet excluded, no replay', () => {
  const rt = fresh();
  assert.equal(cmd(rt, 'enqueue', { sensorId: 'CANNON_REAR' }).accepted, false);
  seed(rt, ['H5', 'H6', 'H7', 'H8']);
  const heads = [];
  const log = [];
  const record = () => log.push(rt.dispatchSeq);
  heads.push(order(rt)[0]);
  cmd(rt, 'autosequence-start');
  record();
  cmd(rt, 'autosequence-pause-after-current-job');
  finishJob(rt);
  snap(rt);
  heads.push(order(rt)[0]);
  cmd(rt, 'autosequence-resume');
  record();
  cmd(rt, 'abort-active-job');
  driveSafeReturn(rt);
  snap(rt);
  heads.push(order(rt)[0]);
  snap(rt);
  record();
  const targets = rt.dispatchLog.map((d) => d.sensorId);
  assert.deepEqual(targets.slice(0, 3), heads, 'every dispatch took the then-current Position 1');
  assert.ok(rt.dispatchLog.every((d) => d.positionBefore === 1));
  assert.deepEqual(log, [1, 2, 3], 'each accepted command produced exactly one dispatch (no replay)');
  assert.ok(rt.activeJobs.length <= 1);
  assert.equal(rt.acceptedSecondJobs, 0);
  assert.equal(rt.violationCount, 0);
});
