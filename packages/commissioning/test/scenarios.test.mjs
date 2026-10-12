// Stage 0.4C-K1 - synthetic scenario tests (Stage task sections 10 and 16, groups C and D).
// Every scenario must be deterministic, honestly labelled, and scoped exactly as the Stage requires.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  COMMISSIONING_STATE, DIAGNOSTIC_REASON, FIELD_VERIFICATION_STATE, NODE_COMMUNICATION_STATE,
  OBSERVATION_PROVENANCE, PAIRED_DIFFERENTIAL_MODE, PREVIEW_STALE_THRESHOLD_MS, QUALITY, ROW_CLASS,
  SCENARIOS, SCENARIO_KEYS, buildObservations as buildFromSchedule, communicationLossIsNodeScoped,
  getScenario, nodeCommunicationSummary, presentMeasurement, requireObservationContract, scenarioCatalogue,
} from '../src/index.mjs';
import { schedule } from './helpers/schedule.mjs';
import { buildObservations, observedChannels, scenarioRows } from './helpers/scenarios.mjs';

/**
 * The ten PROTECTED scenarios of this Stage. They are never removed and never renamed: the paired
 * thermal pass only APPENDED three explicit paired diagnostic demonstrations to the catalogue.
 */
const REQUIRED_SCENARIOS = [
  'NORMAL_GOOD', 'UNCERTAIN_TRANSIENT', 'OUT_OF_RANGE', 'STALE', 'COMMUNICATION_LOSS_NODE',
  'SIMULATED_WIRE_BREAK', 'RECOVERY', 'DRAWING_CONFLICT', 'PRODUCT_TOPOLOGY_CONFLICT', 'UNASSIGNED_CHANNEL',
];

/** The three paired demonstrations added by the Owner-approved Front/Rear thermal-pair pass. */
const PAIRED_SCENARIOS = [
  'REDUCED_FRONT_REAR_DIFFERENTIAL', 'NEAR_ZERO_FRONT_REAR_DIFFERENTIAL', 'REVERSED_FRONT_REAR_DIFFERENTIAL',
];

test('the catalogue is the ten protected scenarios plus the three explicit paired demonstrations', () => {
  // Every protected scenario still exists with its exact key: nothing was removed or renamed.
  for (const key of REQUIRED_SCENARIOS) {
    assert.equal(SCENARIO_KEYS.includes(key), true, `${key} must still exist`);
  }
  for (const key of PAIRED_SCENARIOS) assert.equal(SCENARIO_KEYS.includes(key), true, key);
  assert.equal(SCENARIOS.length, 13, 'ten protected scenarios plus three paired demonstrations');
  assert.equal(SCENARIO_KEYS.length, 13);
  assert.equal(new Set(SCENARIO_KEYS).size, 13, 'no duplicated scenario key');
  // The protected ten keep their original relative order at the head of the catalogue.
  assert.deepEqual(SCENARIO_KEYS.slice(0, 10), REQUIRED_SCENARIOS);
  assert.deepEqual(SCENARIO_KEYS.slice(10), PAIRED_SCENARIOS);
  assert.equal(getScenario('NORMAL_GOOD').key, 'NORMAL_GOOD');
  assert.throws(() => getScenario('MADE_UP'), /unknown scenario/);
});

test('the three paired demonstrations declare their paired mode and their honesty text', () => {
  const catalogue = scenarioCatalogue();
  assert.equal(catalogue.length, 13);
  for (const entry of catalogue.filter((s) => PAIRED_SCENARIOS.includes(s.key))) {
    assert.equal(entry.pairedDiagnostic, true, entry.key);
    assert.equal(entry.provenance, OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO, entry.key);
    assert.equal(entry.simulatedDiagnostic, true, entry.key);
    assert.equal(entry.requiresNode, false, entry.key);
    assert.notEqual(entry.pairedMode, PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE, entry.key);
    assert.match(entry.summary, /Synthetic/i, entry.key);
    assert.match(entry.summary,
      /no (?:field confirmation|alarm|automatic action|automatic effect|approved threshold|Product deadband)/i, entry.key);
  }
  // Every protected scenario keeps the expected-positive paired physics.
  for (const entry of catalogue.filter((s) => REQUIRED_SCENARIOS.includes(s.key))) {
    assert.equal(entry.pairedMode, PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE, entry.key);
    assert.equal(entry.pairedDiagnostic, false, entry.key);
  }
  // The observations of a paired scenario carry the declared mode and the scenario key.
  const observations = buildObservations('REVERSED_FRONT_REAR_DIFFERENTIAL');
  for (const observation of observations.values()) {
    assert.equal(observation.scenarioKey, 'REVERSED_FRONT_REAR_DIFFERENTIAL');
    assert.equal(observation.pairedDifferentialMode, PAIRED_DIFFERENTIAL_MODE.REVERSED);
  }
});

test('every scenario is deterministic: identical inputs give deep-equal output', () => {
  for (const key of SCENARIO_KEYS) {
    const ctx = key === 'COMMUNICATION_LOSS_NODE' ? { nodeId: 'TC-NODE-02' } : {};
    const a = JSON.stringify([...buildObservations(key, { tick: 4, ...ctx }).entries()]);
    const b = JSON.stringify([...buildObservations(key, { tick: 4, ...ctx }).entries()]);
    assert.equal(a, b, key);
  }
});

test('every scenario observation satisfies the observation contract', () => {
  for (const key of SCENARIO_KEYS) {
    const ctx = key === 'COMMUNICATION_LOSS_NODE' ? { nodeId: 'TC-NODE-03' } : {};
    const observations = buildObservations(key, { tick: 2, ...ctx });
    assert.equal(observations.size, 240, key);
    for (const observation of observations.values()) requireObservationContract(observation);
  }
});

test('a node-scoped scenario refuses to run without naming its node', () => {
  assert.throws(() => buildFromSchedule(schedule, { scenarioKey: 'COMMUNICATION_LOSS_NODE' }), /requires an explicit nodeId/);
});

test('1 NORMAL_GOOD presents a plausible Type K sample with no diagnostic', () => {
  const observations = buildObservations('NORMAL_GOOD');
  const mapped = [...observations.values()].filter((o) => o.sensorId !== null);
  assert.equal(mapped.length, 216);
  for (const observation of mapped) {
    assert.equal(observation.quality, QUALITY.GOOD);
    assert.equal(observation.diagnosticReason, DIAGNOSTIC_REASON.NONE);
    assert.equal(observation.isCurrentValueValid, true);
    assert.equal(observation.engineeringUnit, 'degC');
    assert.equal(observation.engineeringValue > -200 && observation.engineeringValue < 1370, true);
    const presented = presentMeasurement(observation);
    assert.equal(presented.current.valid, true);
    assert.equal(presented.invalidPresentation, false);
  }
  // An unassigned position carries no measurement in ANY scenario, including the normal one.
  for (const observation of observations.values()) {
    if (observation.sensorId !== null) continue;
    assert.equal(observation.engineeringValue, null);
    assert.equal(observation.quality, QUALITY.DISABLED);
    assert.equal(observation.nodeCommunicationState, NODE_COMMUNICATION_STATE.ONLINE);
  }
});

test('2 UNCERTAIN_TRANSIENT keeps a marked current value and never claims control', () => {
  const observations = buildObservations('UNCERTAIN_TRANSIENT');
  const affected = observedChannels(observations, (o) => o.quality === QUALITY.UNCERTAIN);
  assert.equal(affected.length > 0, true);
  const observation = observations.get(affected[0]);
  const presented = presentMeasurement(observation);
  assert.equal(presented.current.valid, true);
  assert.equal(presented.quality.marker, '[?]');
  assert.match(observation.note, /no automatic action/);
  assert.equal('controlState' in observation, false);
});

test('3 OUT_OF_RANGE is BAD with the cause named and no current engineering value', () => {
  const observations = buildObservations('OUT_OF_RANGE');
  const affected = observedChannels(observations, (o) => o.diagnosticReason === DIAGNOSTIC_REASON.OUT_OF_RANGE);
  assert.equal(affected.length > 0, true);
  for (const key of affected) {
    const observation = observations.get(key);
    assert.equal(observation.quality, QUALITY.BAD);
    assert.equal(observation.engineeringValue, null);
    assert.equal(observation.isCurrentValueValid, false);
    assert.notEqual(observation.lastValidValue, null);
    assert.equal(observation.rawValue, 0x3584);
  }
});

test('4 STALE exceeds the Preview threshold and is not presented as the current value', () => {
  const observations = buildObservations('STALE');
  const affected = observedChannels(observations, (o) => o.quality === QUALITY.STALE);
  assert.equal(affected.length > 0, true);
  for (const key of affected) {
    const presented = presentMeasurement(observations.get(key));
    assert.equal(presented.age.exceedsPreviewStaleThreshold, true);
    assert.equal(presented.age.milliseconds > PREVIEW_STALE_THRESHOLD_MS, true);
    assert.equal(presented.current.valid, false);
    assert.equal(presented.lastValid.isCurrent, false);
  }
});

test('5 COMMUNICATION_LOSS_NODE affects exactly one node and leaves the other five available', () => {
  for (const nodeId of ['TC-NODE-01', 'TC-NODE-04', 'TC-NODE-06']) {
    const observations = buildObservations('COMMUNICATION_LOSS_NODE', { nodeId });
    const summary = nodeCommunicationSummary(schedule, observations);
    assert.equal(summary.length, 6);
    assert.equal(communicationLossIsNodeScoped(summary, nodeId), true, nodeId);
    const affected = observedChannels(observations, (o) => o.nodeCommunicationState === NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS);
    assert.equal(affected.length, 40, nodeId);
    for (const key of affected) assert.equal(key.startsWith(`${nodeId}|`), true, key);
    const lostMeasurements = observations.get(affected.find((k) => observations.get(k).sensorId !== null));
    assert.equal(lostMeasurements.quality, QUALITY.BAD);
    assert.equal(lostMeasurements.diagnosticReason, DIAGNOSTIC_REASON.COMMUNICATION_LOSS);
    for (const [key, observation] of observations.entries()) {
      if (key.startsWith(`${nodeId}|`)) {
        assert.equal(observation.nodeCommunicationState, NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS, key);
        // An unassigned position still has no measurement; a mapped one is BAD and invalid.
        if (observation.sensorId === null) {
          assert.equal(observation.quality, QUALITY.DISABLED);
          assert.equal(observation.engineeringValue, null);
        } else {
          assert.equal(observation.quality, QUALITY.BAD);
          assert.equal(observation.isCurrentValueValid, false);
        }
      } else if (observation.sensorId === null) {
        // Unassigned positions on an unaffected node still carry no measurement.
        assert.equal(observation.quality, QUALITY.DISABLED);
        assert.equal(observation.nodeCommunicationState, NODE_COMMUNICATION_STATE.ONLINE);
      } else {
        assert.equal(observation.quality, QUALITY.GOOD);
        assert.equal(observation.isCurrentValueValid, true);
        assert.equal(observation.nodeCommunicationState, NODE_COMMUNICATION_STATE.ONLINE);
      }
    }
  }
});

test('6 SIMULATED_WIRE_BREAK is explicitly labelled as a simulated diagnostic scenario', () => {
  const scenario = getScenario('SIMULATED_WIRE_BREAK');
  assert.equal(scenario.provenance, OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO);
  const catalogue = scenarioCatalogue().find((s) => s.key === 'SIMULATED_WIRE_BREAK');
  assert.equal(catalogue.simulatedDiagnostic, true);
  assert.match(catalogue.summary, /BLOCKED/);
  const observations = buildObservations('SIMULATED_WIRE_BREAK');
  const affected = observedChannels(observations, (o) => o.diagnosticReason === DIAGNOSTIC_REASON.WIRE_BREAK);
  assert.equal(affected.length > 0, true);
  for (const key of affected) {
    const observation = observations.get(key);
    const presented = presentMeasurement(observation);
    assert.equal(presented.simulatedDiagnostic, true);
    assert.match(presented.simulatedDiagnosticLabel, /NOT HARDWARE DETECTED/);
    assert.equal(presented.current.valid, false);
    assert.equal(presented.current.numeric, null);
    assert.equal(presented.lastValid.isCurrent, false);
    assert.equal(observation.engineeringValue, null);
    // The raw word is evidence, not a confirmed verdict: 0x7FFF alone never becomes WIRE_BREAK.
    assert.equal(observation.rawValue, 0x7FFF);
    assert.match(observation.note, /simulated, not decoded/);
  }
});

test('7 RECOVERY returns to a valid sample and shows a Preview stabilisation state only', () => {
  const early = buildObservations('RECOVERY', { tick: 0 });
  const late = buildObservations('RECOVERY', { tick: 5 });
  const affected = observedChannels(early, (o) => o.quality === QUALITY.UNCERTAIN);
  assert.equal(affected.length > 0, true);
  for (const key of affected) {
    assert.equal(early.get(key).stabilisation.state, 'PREVIEW_STABILIZING');
    assert.equal(late.get(key).quality, QUALITY.GOOD);
    assert.equal(late.get(key).isCurrentValueValid, true);
    assert.equal(late.get(key).stabilisation.state, 'PREVIEW_STABLE');
    assert.match(late.get(key).stabilisation.note, /no Production stabilisation duration/);
  }
});

test('8 DRAWING_CONFLICT shows exactly the internal-conflict rows and blocks PASS', () => {
  const rows = scenarioRows('DRAWING_CONFLICT');
  assert.equal(rows.length, 12);
  for (const row of rows) {
    assert.equal(row.rowClass, ROW_CLASS.DRAWING_INTERNAL_CONFLICT);
    assert.notEqual(row.passBlockedReason, null);
  }
  const observations = buildObservations('DRAWING_CONFLICT');
  for (const row of rows) {
    const observation = observations.get(row.physicalChannelKey);
    assert.equal(observation.commissioningState, COMMISSIONING_STATE.BLOCKED);
    assert.equal(observation.diagnosticReason, DIAGNOSTIC_REASON.CONFIGURATION_INVALID);
    assert.match(observation.blockingReason, /DRAWING_INTERNAL_CONFLICT/);
    assert.equal(observation.isCurrentValueValid, false);
  }
});

test('9 PRODUCT_TOPOLOGY_CONFLICT applies to no row: I7 and I16 are normal Product Sensors and fully selectable', () => {
  const rows = scenarioRows('PRODUCT_TOPOLOGY_CONFLICT');
  assert.equal(rows.length, 0);
  const observations = buildObservations('PRODUCT_TOPOLOGY_CONFLICT');
  assert.equal(observations.size, 240);
  const corrected = schedule.rows.filter((r) => r.sensorId === 'I7' || r.sensorId === 'I16');
  assert.equal(corrected.length, 4);
  for (const row of corrected) {
    const observation = observations.get(row.physicalChannelKey);
    assert.notEqual(observation.commissioningState, COMMISSIONING_STATE.BLOCKED);
    assert.equal(observation.selectableAsProductSensor, true);
    assert.equal(observation.blockingReason ?? null, null);
    assert.equal(observation.isCurrentValueValid, true, row.physicalChannelKey);
  }
  // Every Product-compatible row with a Sensor identity is selectable; no exclusion exists.
  for (const [key, observation] of observations.entries()) {
    if (observation.sensorId && observation.rowClass === 'PRODUCT_COMPATIBLE') {
      assert.equal(observation.selectableAsProductSensor, true, key);
    }
  }
});

test('10 UNASSIGNED_CHANNEL has no Sensor identity, no value and no field-verification claim', () => {
  const observations = buildObservations('UNASSIGNED_CHANNEL');
  const unassigned = schedule.rows.filter((r) => r.rowClass === ROW_CLASS.UNASSIGNED);
  assert.equal(unassigned.length, 24);
  for (const row of unassigned) {
    const observation = observations.get(row.physicalChannelKey);
    assert.equal(observation.sensorId, null);
    assert.equal(observation.engineeringValue, null);
    assert.equal(observation.rawValue, null);
    assert.equal(observation.lastValidValue, null);
    assert.equal(observation.quality, QUALITY.DISABLED);
    assert.equal(observation.diagnosticReason, DIAGNOSTIC_REASON.NOT_COMMISSIONED);
    assert.equal(observation.commissioningState, COMMISSIONING_STATE.BLOCKED);
    assert.equal(observation.fieldVerificationState, FIELD_VERIFICATION_STATE.NOT_APPLICABLE);
    assert.equal(observation.selectableAsProductSensor, false);
    assert.match(observation.note, /not a commissioned spare/);
    const presented = presentMeasurement(observation);
    assert.equal(presented.current.valid, false);
    assert.equal(presented.invalidPresentation, false);
    // No measurement exists here, so the value age is stated as not applicable rather than as a
    // fresh sample, and the last valid value is explicitly absent.
    assert.equal(presented.age.milliseconds, null);
    assert.match(presented.age.text, /no measurement exists/);
    assert.equal(presented.lastValid.kind, 'NONE');
  }
});

test('scenario provenance is carried on every observation and defaults to the synthetic preview', () => {
  const observations = buildObservations('NORMAL_GOOD');
  for (const observation of observations.values()) {
    assert.equal(observation.scenarioSource, 'SYNTHETIC_PREVIEW');
    assert.equal(observation.provenance ?? OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW, OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW);
    assert.equal(observation.nodeCommunicationState, NODE_COMMUNICATION_STATE.ONLINE);
  }
});

test('scenario timestamps are derived from an integer tick, never from a wall clock', () => {
  const a = buildObservations('NORMAL_GOOD', { tick: 7 });
  const b = buildObservations('NORMAL_GOOD', { tick: 7 });
  const firstKey = [...a.keys()][0];
  assert.equal(a.get(firstKey).observationTimestamp, b.get(firstKey).observationTimestamp);
  assert.equal(a.get(firstKey).observationTimestamp, '2026-01-01T00:00:07.000Z');
});
