// WJSS Stage 0.2.1A — Owner critical Pump decision: hard gates 1..6 for Mandatory Safe Return,
// Isolation Valve ordering, Main Pump critical suspension and the frozen GlobalQueue.
// SYNTHETIC SPIKE TESTS — synthetic proof only. Not a physical Pump / protection relay / VFD /
// Isolation Valve / axis / motion / interlock test. No safety certification is claimed.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JOB_LIFECYCLE, QUEUE_CAPACITY, SAFE_RETURN_TRIGGERS, SyntheticRuntime } from '../src/runtime.mjs';
import { validateSnapshot } from '../../contracts/validate.mjs';

const PROHIBITED = /\b(BLOCKED|HELD|HELD_BY_OPERATOR|WAITING_FOR_PUMP|WAITING_FOR_EQUIPMENT|EXCLUDED|INVALID|OUT_OF_SERVICE|DISABLED|BAD|STALE)\b/;
const L = JOB_LIFECYCLE;

/** In-process runtime: empty queue, AutoSequence off, synthetic Pump ready. */
function fresh(extra = {}) {
  const rt = new SyntheticRuntime({ autoJobs: false, ...extra });
  rt.classifyAll(Date.now());
  rt.clearQueue();
  rt.pressure.values = [100, 97, 94, 95];
  rt.pressure.sourceTs = Date.now();
  return rt;
}
const seed = (rt, ids) => ids.forEach((id) => assert.equal(rt.enqueueWithReason(id, 'TIME_DUE').accepted, true, id));
const snap = (rt) => {
  rt.publish({ tick: false });
  return rt.snapshot().snap;
};
const steps = (events) => events.filter((e) => e.step).map((e) => e.step);
const seqOf = (events, step) => events.find((e) => e.step === step)?.seq;
function assertOrdered(events) {
  const order = ['SR1', 'SR2', 'SR3', 'SR4', 'SR5', 'SR6', 'SR7', 'SR8'];
  assert.deepEqual(steps(events), order);
  for (let i = 1; i < order.length; i += 1) assert.ok(seqOf(events, order[i]) > seqOf(events, order[i - 1]), `${order[i]} after ${order[i - 1]}`);
}
/** Drive the Job's Safe Return with synthetic time, recording every lifecycle seen. */
function drive(rt, fromMs, toMs, stepMs = 250) {
  const seen = [];
  for (let t = fromMs; t <= toMs && rt.activeJobs.length; t += stepMs) {
    rt.advanceJob(t);
    const j = rt.activeJobs[0];
    if (j && seen.at(-1) !== j.lifecycle) seen.push(j.lifecycle);
  }
  return seen;
}

test('Gate 1 — normal completion: P6 end -> Mandatory Safe Return; Job Active until Standby confirmed; COMPLETED only after SR5', () => {
  const rt = fresh();
  seed(rt, ['H5', 'H6']);
  assert.equal(rt.dispatchHead('SYN_TEST').accepted, true);
  const job = rt.activeJobs[0];
  const p = rt.params;
  const endAt = job.startedAtMs + 6 * p.jobPhaseMs;
  const scoreBefore = rt.proc.get('H5').trueScore;
  rt.advanceJob(endAt - 1);
  assert.equal(job.lifecycle, L.RUNNING);
  rt.advanceJob(endAt);
  // Cleaning phases complete is NOT Job complete.
  assert.equal(rt.activeJobs[0], job, 'Job still Active at P6 end');
  assert.equal(job.lifecycle, L.CLOSE_VALVE);
  assert.equal(job.safeReturn.trigger, SAFE_RETURN_TRIGGERS.CLEANING_COMPLETE);
  assert.equal(rt.isJetting(job, endAt), false, 'no water output during Safe Return');
  let s = snap(rt);
  assert.equal(s.activeJob.cleaningPhase, 'CLEANING_PHASES_COMPLETE');
  assert.equal(s.activeJob.safeReturn.step, 'SR2');
  assert.equal(s.activeJob.phase, 'P6');
  assert.equal(s.sequence.lastJobOutcome, null, 'no outcome before Standby');
  assert.equal(rt.proc.get('H5').trueScore, scoreBefore, 'clean effect not applied before COMPLETED');
  assert.equal(rt.dispatchHead('SYN_TEST').reason, 'ACTIVE_JOB_EXISTS', 'no dispatch during Safe Return');
  assert.deepEqual(validateSnapshot(s), []);
  const seen = drive(rt, endAt, endAt + 30000);
  assert.deepEqual(seen, [L.CLOSE_VALVE, L.VERIFY_VALVE, L.TO_STANDBY, L.VERIFY_STANDBY]);
  assert.equal(rt.activeJobs.length, 0);
  const o = rt.lastJobOutcome;
  assert.equal(o.outcome, 'COMPLETED');
  assert.equal(o.cleaningPhasesComplete, true);
  assertOrdered(o.events);
  assert.equal(o.events.at(-1).event, 'LATER_DISPATCH_NOT_PERMITTED_OFF');
  assert.ok(o.standbyConfirmedSeq < o.outcomeSeq && o.outcomeSeq < o.releaseSeq);
  assert.ok(Object.isFrozen(o));
  assert.equal(o.dispatchId, job.dispatch.dispatchId, 'outcome linked to the dispatch record');
  s = snap(rt);
  assert.equal(s.activeJob, null);
  assert.equal(s.sequence.lastJobOutcome.outcome, 'COMPLETED');
  assert.deepEqual(validateSnapshot(s), []);
  assert.equal(rt.violationCount, 0);
});

test('Gate 1b — AutoSequence considers the next head only after SR7 (SR8)', () => {
  const rt = fresh({ autoJobs: true });
  seed(rt, ['H5', 'H6']);
  snap(rt);
  const job = rt.activeJobs[0];
  assert.equal(job.targetSensorId, 'H5');
  job.startedAtMs = Date.now() - 6 * rt.params.jobPhaseMs - 100; // P6 just ended
  snap(rt);
  assert.equal(rt.activeJobs[0], job, 'H6 not dispatched while H5 is in Safe Return');
  assert.deepEqual(rt.queueOrder, ['H6']);
  job.safeReturn.immediate = true; // synthetic time travel to the end of Safe Return
  snap(rt);
  assert.equal(rt.lastJobOutcome.jobId, job.jobId);
  assert.equal(rt.lastJobOutcome.events.at(-1).event, 'LATER_DISPATCH_MAY_BE_CONSIDERED');
  assert.equal(rt.activeJobs[0].targetSensorId, 'H6', 'the next head is dispatched only after release');
  assert.ok(rt.activeJobs[0].dispatch.dispatchedAt >= rt.lastJobOutcome.finalizedAt);
});

test('Gate 2 — abort / cancel / failure: SR1 first (ABORTING), Job Active until SR7, outcome ABORTED (never COMPLETED)', () => {
  for (const cause of ['OPERATOR_ABORT', 'CANCELLED', 'FAILURE']) {
    const rt = fresh();
    seed(rt, ['J2', 'J3']);
    rt.dispatchHead('SYN_TEST');
    const job = rt.activeJobs[0];
    job.startedAtMs = Date.now() - 3.5 * rt.params.jobPhaseMs; // P4 jetting
    assert.equal(rt.isJetting(job, Date.now()), true);
    const r = rt.dispatch('abort-job', { cause });
    assert.equal(r.accepted, true);
    assert.equal(r.detail.trigger, `SYN_${cause}`);
    assert.equal(job.lifecycle, L.ABORTING);
    assert.equal(rt.isJetting(job, Date.now()), false, 'water command stopped at SR1');
    assert.equal(job.safeReturn.valve.command, 'NOT_COMMANDED');
    const s = snap(rt);
    assert.equal(s.activeJob.safeReturn.step, 'SR1');
    assert.equal(s.activeJob.cleaningPhase, 'CLEANING_STOPPED');
    assert.equal(s.activeJob.phase, 'P4', 'phase frozen at trigger');
    assert.deepEqual(validateSnapshot(s), []);
    // A second trigger during Safe Return is noted, not restarted.
    rt.dispatch('abort-job', { cause: 'OPERATOR_ABORT' });
    assert.ok(job.safeReturn.events.some((e) => e.event.startsWith('ADDITIONAL_TRIGGER_NOTED')));
    const seen = drive(rt, Date.now(), Date.now() + 30000);
    assert.deepEqual(seen, [L.ABORTING, L.CLOSE_VALVE, L.VERIFY_VALVE, L.TO_STANDBY, L.VERIFY_STANDBY]);
    assert.equal(rt.lastJobOutcome.outcome, 'ABORTED');
    assert.equal(rt.lastJobOutcome.trigger, `SYN_${cause}`);
    assert.equal(rt.lastJobOutcome.phaseAtTrigger, 'P4');
    assertOrdered(rt.lastJobOutcome.events);
    assert.deepEqual(rt.queueOrder, ['J3'], 'aborted Sensor not re-queued automatically (policy pending)');
  }
  assert.equal(fresh().dispatch('abort-job', { cause: 'BOGUS' }).reason, 'INVALID_ABORT_CAUSE');
});

test('Gate 3 — Pump trip with Active Job: CRITICAL_SUSPENDED, Safe Return, modal; ack/clear rules; no automatic Resume / next Job', () => {
  const rt = fresh({ autoJobs: true });
  seed(rt, ['G4', 'G5', 'G6']);
  snap(rt);
  const job = rt.activeJobs[0];
  job.startedAtMs = Date.now() - 3.5 * rt.params.jobPhaseMs; // P4
  const queueBefore = rt.queueOrder;
  const qRev = rt.queueRevision;
  const dSeq = rt.dispatchSeq;
  const t = rt.dispatch('pump-trip');
  assert.equal(t.accepted, true);
  assert.equal(t.detail.autoSequence, 'CRITICAL_SUSPENDED');
  assert.equal(rt.pump.state, 'TRIPPED');
  assert.equal(job.lifecycle, L.ABORTING);
  assert.equal(job.safeReturn.trigger, 'SYN_PUMP_TRIP');
  let s = snap(rt);
  assert.equal(s.sequence.autoSequence, 'CRITICAL_SUSPENDED');
  assert.equal(s.queue.autoSequence, 'CRITICAL_SUSPENDED');
  const c = s.sequence.critical;
  assert.equal(c.kind, 'MAIN_PUMP_TRIP');
  assert.equal(c.severity, 'HIGH');
  assert.equal(c.modalOpen, true, 'modal immediately');
  assert.equal(c.jobId, job.jobId);
  assert.equal(c.phaseAtEvent, 'P4');
  assert.equal(c.safeReturnRequired, true);
  assert.ok(s.alarms.items.some((a) => a.alarmId === c.alarmId && a.code === 'SYN-MAIN-PUMP-TRIP' && a.severity === 'HIGH'));
  assert.deepEqual(validateSnapshot(s), []);
  // Second critical event while active is refused (no stacking); Pump restart refused.
  assert.equal(rt.dispatch('pump-unexpected-stop').reason, 'CRITICAL_CONDITION_ALREADY_ACTIVE');
  assert.equal(rt.dispatch('pump-start').reason, 'CRITICAL_SUSPENDED');
  // Acknowledge: not a clear, modal stays.
  assert.equal(rt.dispatch('critical-alarm-ack').accepted, true);
  s = snap(rt);
  assert.equal(s.sequence.critical.acknowledged, true);
  assert.equal(s.sequence.critical.conditionActive, true);
  assert.equal(s.sequence.critical.modalOpen, true);
  // Clear before Safe Return complete: modal stays.
  assert.equal(rt.dispatch('pump-fault-clear').accepted, true);
  s = snap(rt);
  assert.equal(s.pump.state, 'STOPPED', 'clear does not restart the Pump');
  assert.equal(s.sequence.critical.conditionActive, false);
  assert.equal(s.sequence.critical.safeReturnComplete, false);
  assert.equal(s.sequence.critical.modalOpen, true, 'modal stays until Safe Return complete');
  assert.equal(s.activeJob.jobId, job.jobId);
  drive(rt, Date.now(), Date.now() + 30000);
  assert.equal(rt.lastJobOutcome.outcome, 'ABORTED');
  assert.equal(rt.lastJobOutcome.events.at(-1).event, 'LATER_DISPATCH_NOT_PERMITTED_CRITICAL_SUSPENDED');
  s = snap(rt);
  assert.equal(s.sequence.critical.modalOpen, false, 'cleared + acknowledged + Safe Return complete closes the modal');
  assert.equal(s.sequence.autoSequence, 'CRITICAL_SUSPENDED', 'suspension persists after the modal closes');
  // No automatic Resume / next Job, even with the Pump restarted and ready.
  rt.setPump('RUNNING', Date.now());
  for (let i = 0; i < 5; i += 1) snap(rt);
  assert.equal(rt.activeJobs.length, 0);
  assert.equal(rt.dispatchSeq, dSeq, 'no dispatch while suspended');
  assert.deepEqual(rt.queueOrder, queueBefore, 'queue unchanged FIFO');
  assert.equal(rt.queueRevision, qRev);
  assert.equal(rt.violationCount, 0);
});

test('Gate 3b — unexpected stop / trip with no Job: critical modal, suspension, no Safe Return required; ack-then-clear closes', () => {
  for (const [command, kind] of [['pump-unexpected-stop', 'MAIN_PUMP_UNEXPECTED_STOP'], ['pump-trip', 'MAIN_PUMP_TRIP']]) {
    const rt = fresh({ autoJobs: true });
    rt.autoSequencePaused = true;
    seed(rt, ['I3', 'I4']);
    assert.equal(rt.dispatch(command).accepted, true);
    let s = snap(rt);
    assert.equal(s.sequence.critical.kind, kind);
    assert.equal(s.sequence.critical.safeReturnRequired, false);
    assert.equal(s.sequence.critical.jobId, null);
    assert.equal(s.sequence.critical.modalOpen, true);
    assert.equal(s.activeJob, null);
    rt.dispatch('pump-fault-clear');
    s = snap(rt);
    assert.equal(s.sequence.critical.modalOpen, true, 'clear without ack keeps the modal');
    rt.dispatch('critical-alarm-ack');
    s = snap(rt);
    assert.equal(s.sequence.critical.modalOpen, false);
    assert.equal(s.sequence.autoSequence, 'CRITICAL_SUSPENDED');
    assert.deepEqual(validateSnapshot(s), []);
    // Test reset (not a Resume) leaves the AutoSequence OFF.
    const r = rt.dispatch('critical-reset');
    assert.equal(r.accepted, true);
    s = snap(rt);
    assert.equal(s.sequence.critical, null);
    assert.equal(s.sequence.autoSequence, 'OFF');
    assert.equal(s.activeJob, null);
  }
});

test('Gate 3c — expected commanded Pump stop is not a fault: no modal, no suspension, Job still Safe Returns', () => {
  const rt = fresh();
  seed(rt, ['G8']);
  rt.dispatchHead('SYN_TEST');
  const r = rt.dispatch('pump-stop');
  assert.equal(r.accepted, true);
  assert.equal(r.detail.safeReturn, true);
  const s = snap(rt);
  assert.equal(s.sequence.critical, null);
  assert.notEqual(s.sequence.autoSequence, 'CRITICAL_SUSPENDED');
  assert.equal(s.activeJob.lifecycle, L.ABORTING);
  drive(rt, Date.now(), Date.now() + 30000);
  assert.equal(rt.lastJobOutcome.trigger, 'SYN_COMMANDED_PUMP_STOP');
  assert.equal(rt.lastJobOutcome.outcome, 'ABORTED');
});

test('Gate 4 — ordering: valve close cmd < valve confirm < axis cmd < Standby confirm < outcome < release; axis waits for valve feedback', () => {
  const rt = fresh();
  seed(rt, ['H9']);
  rt.dispatch('safe-return-config', { valveFeedbackDelayMs: 6000, standbyFeedbackDelayMs: 6000 });
  rt.dispatchHead('SYN_TEST');
  const job = rt.activeJobs[0];
  const t0 = Date.now();
  rt.abortJob(SAFE_RETURN_TRIGGERS.OPERATOR_ABORT);
  const p = rt.params;
  // Long after the nominal feedback time the valve is still unconfirmed: axis must not move.
  rt.advanceJob(t0 + p.safeReturnStepMs + p.safeReturnFeedbackMs + 5000);
  assert.equal(job.lifecycle, L.VERIFY_VALVE);
  assert.equal(job.safeReturn.axis.command, 'NOT_COMMANDED', 'axis not commanded before valve closed confirmed');
  let s = snap(rt);
  assert.equal(s.activeJob.safeReturn.step, 'SR3');
  assert.equal(s.activeJob.safeReturn.valve.feedback, 'NOT_CONFIRMED');
  assert.deepEqual(validateSnapshot(s), []);
  rt.advanceJob(t0 + 60000);
  const o = rt.lastJobOutcome;
  assert.ok(o.valveCloseCommandSeq < o.valveClosedConfirmedSeq);
  assert.ok(o.valveClosedConfirmedSeq < o.axisReturnCommandSeq);
  assert.ok(o.axisReturnCommandSeq < o.standbyConfirmedSeq);
  assert.ok(o.standbyConfirmedSeq < o.outcomeSeq && o.outcomeSeq < o.releaseSeq);
  const at = (step) => Date.parse(o.events.find((e) => e.step === step).at);
  assert.ok(at('SR3') - at('SR2') >= p.safeReturnFeedbackMs + 6000, 'valve feedback delay honoured');
  assert.ok(at('SR5') - at('SR4') >= p.safeReturnFeedbackMs + 6000, 'Standby feedback delay honoured');
  assert.equal(o.valveId, job.valveId);
  // Evidence carries no Galil coordinates / addresses.
  assert.ok(!/coord|address|galil|axisPos/i.test(JSON.stringify(o)));
  // Invariant checker rejects a tampered order.
  const bad = structuredClone(snap(rt));
  const ev = bad.sequence.lastJobOutcome.events;
  const i3 = ev.findIndex((e) => e.step === 'SR3');
  const i4 = ev.findIndex((e) => e.step === 'SR4');
  [ev[i3].seq, ev[i4].seq] = [ev[i4].seq, ev[i3].seq];
  assert.ok(validateSnapshot(bad).some((m) => /order invalid/.test(m)));
  assert.equal(rt.violationCount, 0);
});

test('Gate 5 — Safe Return failure: no COMPLETED, Job retained, no release, no dispatch, modal stays open', () => {
  for (const [key, failAt, reason] of [
    ['valveFeedback', L.VERIFY_VALVE, 'ISOLATION_VALVE_CLOSED_FEEDBACK_ABSENT'],
    ['standbyFeedback', L.VERIFY_STANDBY, 'STANDBY_POSITION_FEEDBACK_ABSENT'],
  ]) {
    const rt = fresh({ autoJobs: true });
    seed(rt, ['G10', 'G11']);
    snap(rt);
    rt.dispatch('safe-return-config', { [key]: 'ABSENT' });
    const job = rt.activeJobs[0];
    job.safeReturn = null; // config applies to the next Safe Return: this Job has not started one
    const dSeq = rt.dispatchSeq;
    rt.dispatch('pump-trip');
    rt.dispatch('critical-alarm-ack');
    rt.dispatch('pump-fault-clear');
    drive(rt, Date.now(), Date.now() + 60000, 500);
    assert.equal(rt.activeJobs[0], job, 'Active Job retained on Safe Return failure');
    assert.equal(job.lifecycle, L.FAILED);
    assert.equal(job.safeReturn.failure.reason, reason);
    assert.equal(job.safeReturn.failure.atLifecycle, failAt);
    if (key === 'valveFeedback') assert.equal(job.safeReturn.axis.command, 'NOT_COMMANDED', 'no axis return without valve confirmation');
    assert.equal(rt.lastJobOutcome, null, 'no outcome finalized');
    assert.ok(!job.safeReturn.events.some((e) => ['SR6', 'SR7', 'SR8'].includes(e.step)));
    const s = snap(rt);
    assert.equal(s.activeJob.safeReturn.step, 'SR_FAILED');
    assert.equal(s.sequence.critical.safeReturnFailed, true);
    assert.equal(s.sequence.critical.modalOpen, true, 'modal stays open on Safe Return failure');
    assert.equal(rt.dispatchSeq, dSeq);
    assert.deepEqual(validateSnapshot(s), []);
    // Synthetic test reset restores the feedback and ends the Job through the ordered steps.
    assert.equal(rt.dispatch('critical-reset').accepted, true);
    assert.equal(rt.activeJobs.length, 0);
    assert.equal(rt.lastJobOutcome.trigger, 'SYN_PUMP_TRIP', 'original trigger retained');
    assert.equal(rt.lastJobOutcome.outcome, 'ABORTED');
    assert.ok(rt.lastJobOutcome.events.some((e) => e.event === 'SYN_TEST_RESET_FEEDBACK_RESTORED'));
    assertOrdered(rt.lastJobOutcome.events);
    assert.equal(rt.violationCount, 0);
  }
});

test('Gate 6 — GlobalQueue during critical suspension: <= 8, no entry status, FIFO unchanged, frozen, no dispatch', () => {
  const rt = fresh({ autoJobs: true });
  rt.applyDirtyMode('dirty70');
  const now = Date.now();
  for (const x of rt.sensors) {
    const acq = rt.acq.get(x.sensorId);
    acq.rawScore = Math.round(rt.proc.get(x.sensorId).trueScore * 10) / 10;
    acq.sourceTs = now;
  }
  rt.autoSequencePaused = true;
  snap(rt);
  assert.equal(rt.queue.length, QUEUE_CAPACITY);
  rt.dispatch('pump-unexpected-stop');
  const order = rt.queueOrder;
  const rev = rt.queueRevision;
  const head = order[0];
  for (const [c, p] of [
    ['enqueue', { sensorId: rt.sensors.find((x) => !rt.isQueued(x.sensorId)).sensorId }],
    ['dequeue', { sensorId: head }],
    ['dispatch-head', {}],
    ['start-job', { sensorId: head }],
    ['review-job', { sensorId: head }],
    ['visual-preset', { preset: 'queued-dirty', sensorId: head }],
    ['queue-mixed-sources', {}],
    ['second-job-attempt', {}],
    ['reset-sensor', { sensorId: head }],
  ]) {
    const r = rt.dispatch(c, p);
    assert.equal(r.accepted, false, c);
    assert.equal(r.reason, 'CRITICAL_SUSPENDED', c);
  }
  rt.dispatch('pause-auto-sequence', { paused: false });
  rt.dispatch('pump-fault-clear');
  rt.dispatch('critical-alarm-ack');
  for (let i = 0; i < 5; i += 1) {
    rt.advanceProcess();
    snap(rt);
  }
  const s = snap(rt);
  assert.deepEqual(rt.queueOrder, order, 'FIFO order unchanged');
  assert.equal(rt.queueRevision, rev, 'queue revision unchanged');
  assert.ok(s.queue.entries.length <= QUEUE_CAPACITY);
  for (const e of s.queue.entries) assert.deepEqual(Object.keys(e).sort(), ['dirtyScore', 'entryId', 'position', 'secondsSinceLastClean', 'sensorId', 'sourceReason']);
  assert.ok(!PROHIBITED.test(JSON.stringify(s.queue.entries)));
  assert.ok(!/SUSPENDED|CRITICAL/.test(JSON.stringify(s.queue.entries)), 'suspension is an AutoSequence state, not an entry state');
  assert.equal(s.activeJob, null);
  assert.equal(s.queue.autoSequence, 'CRITICAL_SUSPENDED');
  assert.deepEqual(validateSnapshot(s), []);
  assert.equal(rt.violationCount, 0);
});

test('critical review scenarios: deterministic, refused while a critical event is active, test reset is not a Resume', () => {
  const rt = fresh();
  seed(rt, ['G2', 'G3']);
  let r = rt.dispatch('critical-scenario', { scenario: 'pump-trip-p1' });
  assert.equal(r.accepted, true, JSON.stringify(r));
  let s = snap(rt);
  assert.equal(s.sequence.critical.phaseAtEvent, 'P1');
  assert.equal(s.activeJob.targetSensorId, 'G2');
  assert.equal(rt.dispatch('critical-scenario', { scenario: 'pump-trip-p4' }).reason, 'CRITICAL_SCENARIO_ACTIVE');
  rt.dispatch('critical-reset');
  // P4 trip.
  rt.setPump('RUNNING', Date.now());
  r = rt.dispatch('critical-scenario', { scenario: 'pump-trip-p4' });
  assert.equal(r.accepted, true, JSON.stringify(r));
  assert.equal(snap(rt).sequence.critical.phaseAtEvent, 'P4');
  rt.dispatch('critical-reset');
  rt.setPump('RUNNING', Date.now());
  // Normal completion Safe Return: Job ends P6 shortly and then Safe Returns (no critical event).
  r = rt.dispatch('critical-scenario', { scenario: 'normal-completion-safe-return' });
  assert.equal(r.accepted, true, JSON.stringify(r));
  const job = rt.activeJobs[0];
  rt.advanceJob(job.startedAtMs + 6 * rt.params.jobPhaseMs);
  s = snap(rt);
  assert.equal(s.sequence.critical, null);
  assert.equal(s.activeJob.cleaningPhase, 'CLEANING_PHASES_COMPLETE');
  assert.equal(rt.dispatch('critical-scenario', { scenario: 'nope' }).reason, 'UNKNOWN_CRITICAL_SCENARIO');
  for (const bad of [{ valveFeedbackDelayMs: -1 }, { standbyFeedbackDelayMs: 99999 }, { valveFeedback: 'MAYBE' }]) assert.equal(rt.dispatch('safe-return-config', bad).accepted, false);
  assert.equal(rt.violationCount, 0);
});
