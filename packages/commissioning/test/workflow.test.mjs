// Stage 0.4C-K1 - Preview workflow tests (Stage task section 16, group D, and section 9 actions).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  COMMISSIONING_STATE, OBSERVATION_KIND, PROVENANCE_APPROVAL_STATE, PROVENANCE_TESTER_PLACEHOLDER,
  PreviewCommissioningSession, REMARK_MAX_LENGTH, canRecordPass, commissioningProgress, normalizeRemark, previewSessionId,
} from '../src/index.mjs';
import { schedule } from './helpers/schedule.mjs';
import { buildObservations } from './helpers/scenarios.mjs';

const rows = {
  eligible: schedule.rows.find((r) => r.rowClass === 'PRODUCT_COMPATIBLE' && r.drawingVerified),
  internalConflict: schedule.rows.find((r) => r.rowClass === 'DRAWING_INTERNAL_CONFLICT'),
  // The corrected baseline carries no Product-topology conflict row (I7 and I16 are normal Product
  // Sensors). The blocking rule is kept and exercised with a synthetic row of that class.
  topologyConflict: Object.freeze({
    ...schedule.rows.find((r) => r.rowClass === 'DRAWING_INTERNAL_CONFLICT'),
    physicalChannelKey: 'SYNTHETIC|TOPOLOGY-CONFLICT|1',
    rowClass: 'PRODUCT_TOPOLOGY_CONFLICT',
    passBlockedReason: ['PRODUCT_TOPOLOGY_CONFLICT', 'BLOCKED'].join('_'),
  }),
  unassigned: schedule.rows.find((r) => r.rowClass === 'UNASSIGNED'),
};
const observations = buildObservations('NORMAL_GOOD');
const fresh = () => new PreviewCommissioningSession({ sessionIndex: 7 });

test('the session identity is deterministic, placeholder-only and never approved', () => {
  assert.equal(previewSessionId(7), 'PREVIEW-SESSION-0007');
  const session = fresh();
  assert.equal(session.sessionId, 'PREVIEW-SESSION-0007');
  assert.equal(session.tester, PROVENANCE_TESTER_PLACEHOLDER);
  assert.match(session.tester, /not a person record/);
  assert.equal(session.approvalState, PROVENANCE_APPROVAL_STATE);
  assert.match(session.approvalState, /NOT APPROVED/);
  assert.throws(() => previewSessionId(-1), /non-negative integer/);
});

test('beginning a Preview observation moves an eligible row to IN_PROGRESS without approving it', () => {
  const session = fresh();
  assert.equal(session.stateOf(rows.eligible), COMMISSIONING_STATE.NOT_TESTED);
  const record = session.beginObservation(rows.eligible, OBSERVATION_KIND.BASELINE, observations.get(rows.eligible.physicalChannelKey));
  assert.equal(session.stateOf(rows.eligible), COMMISSIONING_STATE.IN_PROGRESS);
  assert.equal(record.baselineObservation.kind, OBSERVATION_KIND.BASELINE);
  assert.equal(record.verdict, null);
  assert.equal(record.approvalState, PROVENANCE_APPROVAL_STATE);
  assert.throws(() => session.beginObservation(rows.eligible, 'MADE_UP'), /unknown observation kind/);
});

test('all four observation slots can be recorded for one measurement', () => {
  const session = fresh();
  const key = rows.eligible.physicalChannelKey;
  for (const kind of Object.values(OBSERVATION_KIND)) {
    session.beginObservation(rows.eligible, kind, observations.get(key));
  }
  const record = session.recordOf(rows.eligible);
  for (const field of ['baselineObservation', 'stimulusObservation', 'openCircuitObservation', 'recoveryObservation']) {
    assert.notEqual(record[field], null, field);
  }
  assert.deepEqual(record.baselineObservation.observationKinds, undefined);
});

test('an observation belonging to another Channel is refused rather than attached', () => {
  const session = fresh();
  const other = observations.get(rows.internalConflict.physicalChannelKey);
  assert.throws(() => session.beginObservation(rows.eligible, OBSERVATION_KIND.BASELINE, other), /does not belong to this physical Channel/);
});

test('a PASS verdict is possible only on an unblocked Product-compatible row', () => {
  const session = fresh();
  assert.equal(canRecordPass(rows.eligible), true);
  const record = session.recordVerdict(rows.eligible, { verdict: 'PASS', remark: 'preview pass' });
  assert.equal(record.verdict, 'PASS');
  assert.equal(session.stateOf(rows.eligible), COMMISSIONING_STATE.PASS);
  assert.equal(record.remark, 'preview pass');
  assert.equal(canRecordPass(rows.internalConflict), false);
  assert.equal(canRecordPass(rows.topologyConflict), false);
  assert.equal(canRecordPass(rows.unassigned), false);
});

test('a conflict row cannot be marked PASS, and an unassigned Channel cannot be judged at all', () => {
  const session = fresh();
  assert.throws(() => session.recordVerdict(rows.internalConflict, { verdict: 'PASS' }), /no PASS can be recorded/);
  assert.throws(() => session.recordVerdict(rows.topologyConflict, { verdict: 'PASS' }), /no PASS can be recorded/);
  assert.throws(() => session.recordVerdict(rows.unassigned, { verdict: 'PASS' }), /no Sensor identity/);
  assert.throws(() => session.recordVerdict(rows.unassigned, { verdict: 'FAIL' }), /no Sensor identity/);
  assert.equal(session.stateOf(rows.internalConflict), COMMISSIONING_STATE.BLOCKED);
  assert.equal(session.stateOf(rows.topologyConflict), COMMISSIONING_STATE.BLOCKED);
  assert.equal(session.stateOf(rows.unassigned), COMMISSIONING_STATE.BLOCKED);
});

test('FAIL and PARTIAL are recordable on a mapped row, including a Drawing-conflict row', () => {
  const session = fresh();
  const failed = session.recordVerdict(rows.internalConflict, { verdict: 'FAIL', remark: 'conflicting labels on site' });
  assert.equal(failed.verdict, 'FAIL');
  assert.equal(session.stateOf(rows.internalConflict), COMMISSIONING_STATE.FAIL);
  const partial = session.recordVerdict(rows.topologyConflict, { verdict: 'PARTIAL' });
  assert.equal(partial.verdict, 'PARTIAL');
});

test('BLOCKED can never be recorded as a verdict', () => {
  const session = fresh();
  assert.throws(() => session.recordVerdict(rows.internalConflict, { verdict: 'BLOCKED' }), /not recordable in the Preview/);
  assert.throws(() => session.recordVerdict(rows.eligible, { verdict: 'BLOCKED' }), /not recordable in the Preview/);
});

test('remarks are normalised, control characters are stripped and the length is capped', () => {
  assert.equal(normalizeRemark('  ok  '), 'ok');
  assert.equal(normalizeRemark('a\u0000b\nc'), 'a b c');
  assert.equal(normalizeRemark('x'.repeat(500)).length, REMARK_MAX_LENGTH);
  const session = fresh();
  session.recordVerdict(rows.eligible, { verdict: 'FAIL', remark: 'bad'.repeat(200) });
  assert.equal(session.recordOf(rows.eligible).remark.length, REMARK_MAX_LENGTH);
  session.recordRemark(rows.eligible, 'later note');
  assert.equal(session.recordOf(rows.eligible).remark, 'later note');
  assert.equal(session.stateOf(rows.eligible), COMMISSIONING_STATE.FAIL);
});

test('clearing a Preview result returns the row to NOT_TESTED, and evidence BLOCKED stays BLOCKED', () => {
  const session = fresh();
  session.recordVerdict(rows.eligible, { verdict: 'PASS' });
  assert.equal(session.clear(rows.eligible).state, COMMISSIONING_STATE.NOT_TESTED);
  assert.equal(session.stateOf(rows.eligible), COMMISSIONING_STATE.NOT_TESTED);
  assert.equal(session.clear(rows.eligible).cleared, false);
  session.recordVerdict(rows.internalConflict, { verdict: 'FAIL' });
  assert.equal(session.clear(rows.internalConflict).state, COMMISSIONING_STATE.BLOCKED);
  assert.equal(session.stateOf(rows.internalConflict), COMMISSIONING_STATE.BLOCKED);
});

test('clearAll empties every Preview record but changes no evidence row', () => {
  const session = fresh();
  session.recordVerdict(rows.eligible, { verdict: 'PASS' });
  session.recordVerdict(rows.internalConflict, { verdict: 'FAIL' });
  assert.equal(session.recordCount, 2);
  assert.equal(session.clearAll(), 2);
  assert.equal(session.recordCount, 0);
  assert.equal(session.stateOf(rows.internalConflict), COMMISSIONING_STATE.BLOCKED);
});

test('next and previous measurement navigation walks the schedule order', () => {
  const session = fresh();
  const first = schedule.rows[0].physicalChannelKey;
  const second = schedule.rows[1].physicalChannelKey;
  assert.equal(session.moveCursor(schedule, null, 1), first);
  assert.equal(session.moveCursor(schedule, first, 1), second);
  assert.equal(session.moveCursor(schedule, second, -1), first);
  assert.equal(session.moveCursor(schedule, null, -1), schedule.rows[239].physicalChannelKey);
  assert.equal(session.cursor, schedule.rows[239].physicalChannelKey);
});

test('the Preview snapshot reports in-memory-only state and carries no persistence claim', () => {
  const session = fresh();
  session.recordVerdict(rows.eligible, { verdict: 'PARTIAL', remark: 'partial' });
  const snapshot = session.snapshot();
  assert.equal(snapshot.persisted, false);
  assert.equal(snapshot.approvalState, PROVENANCE_APPROVAL_STATE);
  assert.equal(snapshot.records.length, 1);
  assert.equal(snapshot.records[0].verdict, 'PARTIAL');
  assert.deepEqual(snapshot.records[0].observationKinds, []);
});

test('workflow actions never mutate the evidence schedule or the observation set', () => {
  const scheduleBefore = JSON.stringify(schedule.rows);
  const observationsBefore = JSON.stringify([...observations.entries()]);
  const session = fresh();
  session.beginObservation(rows.eligible, OBSERVATION_KIND.BASELINE, observations.get(rows.eligible.physicalChannelKey));
  session.recordVerdict(rows.eligible, { verdict: 'PASS', remark: 'ok' });
  session.recordVerdict(rows.internalConflict, { verdict: 'FAIL' });
  session.recordRemark(rows.unassigned, 'should not matter');
  session.clear(rows.eligible);
  session.moveCursor(schedule, rows.eligible.physicalChannelKey, 1);
  assert.equal(JSON.stringify(schedule.rows), scheduleBefore);
  assert.equal(JSON.stringify([...observations.entries()]), observationsBefore);
});

test('the workflow module exposes no device, queue, cleaning or control surface', () => {
  const session = fresh();
  const surface = Object.getOwnPropertyNames(Object.getPrototypeOf(session)).concat(Object.getOwnPropertyNames(session));
  for (const forbidden of ['write', 'command', 'dispatch', 'activate', 'startJob', 'enqueue', 'persist', 'send', 'connect']) {
    assert.equal(surface.some((name) => name.toLowerCase().includes(forbidden)), false, forbidden);
  }
  const record = session.recordOf(rows.eligible);
  for (const forbidden of ['queueState', 'deviceCommand', 'writeAuthority', 'cleaningJobId']) {
    assert.equal(forbidden in record, false, forbidden);
  }
});

test('commissioning progress reflects Preview records without changing evidence blocking', () => {
  const session = fresh();
  const before = commissioningProgress(schedule, session);
  assert.equal(before.total, 240);
  assert.equal(before.byState.BLOCKED, 36);
  assert.equal(before.byState.NOT_TESTED, 204);
  session.recordVerdict(rows.eligible, { verdict: 'PASS' });
  const after = commissioningProgress(schedule, session);
  assert.equal(after.byState.PASS, 1);
  assert.equal(after.byState.NOT_TESTED, 203);
  assert.equal(after.byState.BLOCKED, 36);
  assert.equal(after.blockedFromPass, 36);
  assert.equal(after.eligibleForPass, 204);
  session.recordVerdict(rows.internalConflict, { verdict: 'FAIL' });
  const failed = commissioningProgress(schedule, session);
  assert.equal(failed.byState.BLOCKED, 35);
  assert.equal(failed.byState.FAIL, 1);
});
