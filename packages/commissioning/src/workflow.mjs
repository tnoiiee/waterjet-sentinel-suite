// Stage 0.4C-K1 - the PREVIEW-ONLY commissioning workflow.
//
// This module holds workflow state for a commissioning review session. It is deliberately inert:
//   - it has no I/O, no imports of any runtime, mapping or transport module, no clock and no
//     persistence: the state lives in memory for the length of the Preview;
//   - it cannot write to Hardware, cannot modify the authoritative Mapping, cannot mark field
//     verification complete, cannot alter Runtime control state, cannot create a Queue entry and
//     cannot trigger Cleaning - there is no code path to any of them;
//   - the approval state is a fixed literal: nothing in this Stage approves anything.
//
// Eligibility rules are derived from the evidence row, never from the Operator:
//   PASS       requires an unblocked row (no Drawing conflict, no Product-topology conflict,
//              not unassigned, and a Sensor identity exists).
//   FAIL/PARTIAL require a Sensor identity; an unassigned physical Channel has nothing to test.
//   BLOCKED is never recordable: it is what the evidence says, not what a person chooses.

import { COMMISSIONING_STATE, RECORDABLE_VERDICT } from './quality.mjs';

export const PROVENANCE_APPROVAL_STATE = 'PREVIEW_ONLY - NOT APPROVED';
export const PROVENANCE_TESTER_PLACEHOLDER = 'PREVIEW-TESTER-PLACEHOLDER (not a person record)';
export const PREVIEW_SESSION_ID_PREFIX = 'PREVIEW-SESSION';
export const REMARK_MAX_LENGTH = 240;

/** The four Preview observation slots an Operator may begin for one measurement. */
export const OBSERVATION_KIND = Object.freeze({
  BASELINE: 'BASELINE',
  STIMULUS: 'STIMULUS',
  OPEN_CIRCUIT: 'OPEN_CIRCUIT',
  RECOVERY: 'RECOVERY',
});

export const OBSERVATION_KIND_VALUES = Object.freeze(Object.values(OBSERVATION_KIND));

function requireMeasurementRow(row) {
  if (!row || typeof row !== 'object') throw new Error('workflow requires a schedule row');
  if (!row.physicalChannelKey) throw new Error('schedule row has no physical Channel identity');
  return row;
}

/** Deterministic session id: derived from the supplied index, never from a clock or a random source. */
export function previewSessionId(index) {
  if (!Number.isInteger(index) || index < 0) throw new Error('preview session index must be a non-negative integer');
  return `${PREVIEW_SESSION_ID_PREFIX}-${String(index).padStart(4, '0')}`;
}

export class PreviewCommissioningSession {
  #records = new Map();
  #sessionId;
  #tester;
  #cursor = null;

  constructor({ sessionIndex = 0, tester = PROVENANCE_TESTER_PLACEHOLDER } = {}) {
    this.#sessionId = previewSessionId(sessionIndex);
    this.#tester = String(tester);
  }

  get sessionId() { return this.#sessionId; }
  get tester() { return this.#tester; }
  get approvalState() { return PROVENANCE_APPROVAL_STATE; }
  get cursor() { return this.#cursor; }
  get recordCount() { return this.#records.size; }

  #recordFor(row) {
    const key = requireMeasurementRow(row).physicalChannelKey;
    if (!this.#records.has(key)) {
      this.#records.set(key, {
        channelKey: key,
        state: COMMISSIONING_STATE.NOT_TESTED,
        observations: {},
        verdict: null,
        remark: '',
        blockingReason: row.passBlockedReason,
      });
    }
    return this.#records.get(key);
  }

  /** Current commissioning state of one row: BLOCKED stays BLOCKED unless a workflow state applies. */
  stateOf(row) {
    const key = requireMeasurementRow(row).physicalChannelKey;
    const record = this.#records.get(key);
    if (record && record.state !== COMMISSIONING_STATE.NOT_TESTED) return record.state;
    if (row.passBlockedReason) return COMMISSIONING_STATE.BLOCKED;
    return record ? record.state : COMMISSIONING_STATE.NOT_TESTED;
  }

  /** Starts a Preview observation slot. This records that a review began; it reads no device. */
  beginObservation(row, kind, observation) {
    requireMeasurementRow(row);
    if (!OBSERVATION_KIND_VALUES.includes(kind)) throw new Error(`unknown observation kind '${kind}'`);
    if (observation) {
      if (observation.channelKey !== row.physicalChannelKey) {
        throw new Error('the observation does not belong to this physical Channel');
      }
      if (observation.provenance === undefined && observation.scenarioSource === undefined) {
        throw new Error('an observation must carry its preview provenance');
      }
    }
    const record = this.#recordFor(row);
    record.observations[kind] = observation
      ? {
        kind,
        observationTick: observation.observationTick ?? null,
        observationTimestamp: observation.observationTimestamp ?? null,
        quality: observation.quality ?? null,
        diagnosticReason: observation.diagnosticReason ?? null,
        isCurrentValueValid: observation.isCurrentValueValid === true,
        engineeringValue: observation.engineeringValue ?? null,
        scenarioSource: observation.scenarioSource ?? null,
        provenance: observation.provenance ?? null,
        recordedAt: null,
      }
      : { kind, recordedAt: null };
    if (row.passBlockedReason) {
      record.blockingReason = row.passBlockedReason;
    } else if (record.state === COMMISSIONING_STATE.NOT_TESTED) {
      record.state = COMMISSIONING_STATE.IN_PROGRESS;
    }
    return this.recordOf(row);
  }

  /**
   * Records a Preview verdict. Only PASS is constrained by eligibility; FAIL and PARTIAL need an
   * identity to judge, and BLOCKED can never be recorded.
   */
  recordVerdict(row, { verdict, remark = '' } = {}) {
    requireMeasurementRow(row);
    if (!Object.values(RECORDABLE_VERDICT).includes(verdict)) {
      throw new Error(`verdict '${verdict}' is not recordable in the Preview`);
    }
    if (row.passBlockedReason === 'UNASSIGNED_BLOCKED' || row.passBlockedReason === 'NO_SENSOR_IDENTITY_BLOCKED' || !row.sensorId) {
      throw new Error(`no verdict can be recorded for ${row.physicalChannelKey}: the Channel has no Sensor identity`);
    }
    if (verdict === RECORDABLE_VERDICT.PASS && row.passBlockedReason) {
      throw new Error(`no PASS can be recorded for ${row.physicalChannelKey}: ${row.passBlockedReason}`);
    }
    const record = this.#recordFor(row);
    record.verdict = verdict;
    record.state = verdict;
    record.remark = normalizeRemark(remark);
    return this.recordOf(row);
  }

  /** Records or updates a remark without changing any state or verdict. */
  recordRemark(row, remark) {
    requireMeasurementRow(row);
    const record = this.#recordFor(row);
    record.remark = normalizeRemark(remark);
    return this.recordOf(row);
  }

  /** Clears the Preview-only result for one row. Evidence-derived BLOCKED remains. */
  clear(row) {
    requireMeasurementRow(row);
    const key = row.physicalChannelKey;
    const existed = this.#records.delete(key);
    return Object.freeze({
      cleared: existed,
      state: row.passBlockedReason ? COMMISSIONING_STATE.BLOCKED : COMMISSIONING_STATE.NOT_TESTED,
    });
  }

  /** Clears every Preview-only result. Used by tests and by the Preview reset control. */
  clearAll() {
    const count = this.#records.size;
    this.#records.clear();
    return count;
  }

  /** The §9 workflow record for one row, in presentation shape. */
  recordOf(row) {
    requireMeasurementRow(row);
    const record = this.#records.get(row.physicalChannelKey);
    return Object.freeze({
      commissioningSessionId: this.#sessionId,
      tester: this.#tester,
      startedAt: null,
      baselineObservation: record?.observations[OBSERVATION_KIND.BASELINE] ?? null,
      stimulusObservation: record?.observations[OBSERVATION_KIND.STIMULUS] ?? null,
      openCircuitObservation: record?.observations[OBSERVATION_KIND.OPEN_CIRCUIT] ?? null,
      recoveryObservation: record?.observations[OBSERVATION_KIND.RECOVERY] ?? null,
      verdict: record?.verdict ?? null,
      remark: record?.remark ?? '',
      commissioningState: this.stateOf(row),
      blockingReason: record?.blockingReason ?? row.passBlockedReason ?? null,
      approvalState: PROVENANCE_APPROVAL_STATE,
    });
  }

  /** Next / previous measurement by physical schedule order. Never changes any verdict. */
  moveCursor(schedule, fromKey, direction) {
    if (!schedule?.rows?.length) throw new Error('navigation requires the schedule');
    const keys = schedule.rows.map((r) => r.physicalChannelKey);
    const index = fromKey ? keys.indexOf(fromKey) : -1;
    if (index === -1) {
      this.#cursor = direction === -1 ? keys[keys.length - 1] : keys[0];
      return this.#cursor;
    }
    const next = index + direction;
    if (next < 0 || next >= keys.length) return this.#cursor ?? keys[index];
    this.#cursor = keys[next];
    return this.#cursor;
  }

  /** JSON-serialisable, in-memory-only snapshot of the Preview records. */
  snapshot() {
    return Object.freeze({
      commissioningSessionId: this.#sessionId,
      tester: this.#tester,
      approvalState: PROVENANCE_APPROVAL_STATE,
      persisted: false,
      records: Object.freeze([...this.#records.entries()].map(([channelKey, r]) => Object.freeze({
        channelKey,
        state: r.state,
        verdict: r.verdict,
        remark: r.remark,
        blockingReason: r.blockingReason ?? null,
        observationKinds: Object.freeze(Object.keys(r.observations)),
      }))),
    });
  }
}

/** Remarks are Preview text: trimmed, stripped of control characters and length-capped. */
export function normalizeRemark(remark) {
  const text = String(remark ?? '').replace(/[\u0000-\u001f\u007f]/g, ' ').trim();
  return text.length > REMARK_MAX_LENGTH ? text.slice(0, REMARK_MAX_LENGTH) : text;
}

/** True when a row is allowed to receive a Preview PASS. */
export function canRecordPass(row) {
  return Boolean(row) && !row.passBlockedReason && Boolean(row.sensorId);
}
