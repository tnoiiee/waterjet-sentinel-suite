// Stage 0.4C-K1 - focused paired Front/Rear thermal tests (Owner-approved domain decision,
// 2026-10-12). These tests cover the paired synthetic physics, the pair identity and isolation, the
// FrontRearDelta validity rules, the three explicit paired diagnostic demonstrations, the read-only
// no-effect boundary and the protected totals.
//
// Everything here runs against the REAL committed evidence schedule and the REAL scenario generator.
// Nothing is stubbed, and no test rewrites the evidence.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  buildThermalPair,
  buildThermalPairs,
  describeUnpairedMeasurement,
  DIFFERENTIAL_UNAVAILABLE_REASON,
  isCurrentValueValid,
  isPairedTemperatureRow,
  PAIRED_DIFFERENTIAL_MODE,
  PAIRED_THERMAL_EFFECT,
  PAIRED_THERMAL_GUIDANCE,
  PAIRED_THERMAL_MARKER,
  PAIRED_THERMAL_STATE,
  pairForRow,
  scheduleCounts,
  syntheticEngineeringValue,
  syntheticExpectedDifferential,
  syntheticPairedDifferential,
  syntheticPairedFrontTemperature,
  syntheticPairedReference,
} from '../src/index.mjs';
import { schedule, counts, rowsByClass, rowForMeasurement, FIRST_TOPOLOGY_CONFLICT, FIRST_UNASSIGNED } from './helpers/schedule.mjs';
import { buildObservations } from './helpers/scenarios.mjs';

const round1 = (value) => Math.round(value * 10) / 10;
const pairsFor = (scenarioKey, { tick = 0, nodeId = null } = {}) =>
  buildThermalPairs(schedule, buildObservations(scenarioKey, { tick, nodeId }));

/** One pair by Sensor identity, with its two authoritative observations. */
const pairOf = (pairs, sensorId) => pairs.pairs.find((p) => p.sensorId === sensorId);
const pairIds = (pairs) => pairs.pairs.map((p) => p.sensorId);

test('A1 every Product Sensor pair exists exactly once, with one Front and one Rear measurement', () => {
  const pairs = pairsFor('NORMAL_GOOD');
  assert.equal(pairs.counts.productSensors, 106);
  assert.equal(pairs.pairs.length, 106);
  assert.equal(new Set(pairIds(pairs)).size, 106);
  // The protected evidence totals are untouched by the paired model.
  assert.equal(counts.productSensors, 106);
  assert.equal(counts.productCompatibleMeasurements, 212);
  assert.equal(counts.physicalChannels, 240);
  for (const pair of pairs.pairs) {
    assert.match(pair.frontMeasurementIdentity, /:F$/);
    assert.match(pair.rearMeasurementIdentity, /:R$/);
    assert.equal(pair.frontMeasurementIdentity.slice(0, -2), pair.sensorId);
    assert.equal(pair.rearMeasurementIdentity.slice(0, -2), pair.sensorId);
    assert.notEqual(pair.frontChannelKey, pair.rearChannelKey, `${pair.sensorId} must own two distinct Channels`);
    assert.equal(pair.frontSide, 'F');
    assert.equal(pair.rearSide, 'R');
  }
});

test('A2 NORMAL_GOOD: Front is above Rear for every pair, with a finite delta equal to Front minus Rear', () => {
  const observations = buildObservations('NORMAL_GOOD');
  const pairs = buildThermalPairs(schedule, observations);
  assert.equal(pairs.counts.available, 106);
  assert.equal(pairs.counts.unavailable, 0);
  for (const pair of pairs.pairs) {
    assert.equal(Number.isFinite(pair.frontValue), true, pair.sensorId);
    assert.equal(Number.isFinite(pair.rearValue), true, pair.sensorId);
    assert.ok(pair.frontValue > pair.rearValue, `${pair.sensorId}: Front ${pair.frontValue} must be above Rear ${pair.rearValue}`);
    assert.ok(pair.delta > 0, `${pair.sensorId}: delta must be positive`);
    // The derived value is EXACTLY Front minus Rear of the authoritative observations, rounded only
    // for display (never re-derived from history, never substituted from one side).
    assert.equal(pair.delta, pair.frontValue - pair.rearValue, pair.sensorId);
    assert.equal(pair.deltaText, `${pair.delta < 0 ? '-' : '+'}${Math.abs(pair.delta).toFixed(1)}`, pair.sensorId);
    assert.match(pair.deltaText, /^[+-]\d+\.\d$/);
    assert.equal(pair.unit, 'degC');
    assert.equal(pair.state, PAIRED_THERMAL_STATE.EXPECTED_POSITIVE);
    assert.equal(pair.reason, null);
    assert.equal(pair.frontValuePresented, String(pair.frontValue));
    for (const side of ['frontChannelKey', 'rearChannelKey']) {
      const observation = observations.get(pair[side]);
      assert.equal(observation.isCurrentValueValid, true, pair.sensorId);
      assert.equal(observation.quality, 'GOOD');
    }
  }
});

test('A3 the generator is deterministic, bounded, plural and independent of rendering order', () => {
  const a = JSON.stringify([...buildObservations('NORMAL_GOOD', { tick: 6 }).entries()]);
  const b = JSON.stringify([...buildObservations('NORMAL_GOOD', { tick: 6 }).entries()]);
  assert.equal(a, b, 'identical inputs must give deep-equal observations');
  const deltas = pairsFor('NORMAL_GOOD').pairs.map((p) => round1(p.delta));
  const distinct = new Set(deltas);
  assert.ok(distinct.size >= 20, `the delta must vary across Sensors, saw ${distinct.size} distinct values`);
  assert.ok(Math.min(...deltas) > 0 && Math.max(...deltas) <= 8, 'the NORMAL_GOOD delta stays bounded and positive');
  assert.ok(Math.max(...deltas) - Math.min(...deltas) >= 1, 'the variation must be real, not a rounding artefact');
  // Independent of wall rendering order: the value is a function of the Sensor identity and the tick
  // only, so reading the schedule backwards cannot change one value.
  const reversed = { rows: [...schedule.rows].reverse() };
  const forwardPairs = buildThermalPairs(schedule, buildObservations('NORMAL_GOOD', { tick: 3 }));
  const reversePairs = buildThermalPairs(reversed, buildObservations('NORMAL_GOOD', { tick: 3 }));
  const byId = new Map(reversePairs.pairs.map((p) => [p.sensorId, p]));
  for (const pair of forwardPairs.pairs) {
    const other = byId.get(pair.sensorId);
    assert.equal(other.frontValue, pair.frontValue, pair.sensorId);
    assert.equal(other.rearValue, pair.rearValue, pair.sensorId);
    assert.equal(other.delta, pair.delta, pair.sensorId);
  }
  // The differential helper is a pure function: same Sensor, same value, every call.
  assert.equal(syntheticPairedDifferential('H7'), syntheticPairedDifferential('H7'));
  assert.equal(syntheticPairedReference('H7'), syntheticPairedReference('H7'));
  assert.notEqual(syntheticPairedDifferential('H7'), syntheticPairedDifferential('H8'));
  // Rear is a per-Sensor reference, not one constant value: it differs between Sensors and zones.
  assert.notEqual(syntheticPairedReference('H7'), syntheticPairedReference('J18'));
  assert.notEqual(syntheticPairedReference('H7'), syntheticPairedReference('G+101'));
  // The sample value is a pure function of the physical identity: it does NOT move with the tick
  // (only the observation timestamp and the value age do).
  for (const sensorId of ['H7', 'G+101', 'J18']) {
    assert.equal(syntheticPairedReference(sensorId, 6), syntheticPairedReference(sensorId, 0), sensorId);
    assert.equal(syntheticPairedFrontTemperature(sensorId, 6), syntheticPairedFrontTemperature(sensorId, 0), sensorId);
  }
  assert.equal(syntheticPairedFrontTemperature('H7', 0), round1(syntheticPairedReference('H7', 0) + syntheticExpectedDifferential('H7', 0)));
});

test('A4 the paired generator is used by the Front and Rear rows of the same pair only', () => {
  const frontRow = rowForMeasurement('H7:F');
  const rearRow = rowForMeasurement('H7:R');
  assert.equal(isPairedTemperatureRow(frontRow), true);
  assert.equal(isPairedTemperatureRow(rearRow), true);
  const tick = 5;
  assert.equal(syntheticEngineeringValue(rearRow, tick, PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE), syntheticPairedReference('H7', tick));
  assert.equal(syntheticEngineeringValue(frontRow, tick, PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE), syntheticPairedFrontTemperature('H7', tick));
  // A NON_SENSOR_GAP row and an unassigned Channel are never part of a pair.
  assert.equal(isPairedTemperatureRow(FIRST_TOPOLOGY_CONFLICT), false);
  assert.equal(isPairedTemperatureRow(FIRST_UNASSIGNED), false);
  const pairs = pairsFor('NORMAL_GOOD');
  assert.equal(pairIds(pairs).includes('I7'), false);
  assert.equal(pairIds(pairs).includes('I16'), false);
  assert.equal(pairOf(pairs, 'I7'), undefined);
  assert.equal(pairs.byChannelKey.get(FIRST_TOPOLOGY_CONFLICT.physicalChannelKey), undefined);
  assert.equal(pairs.byChannelKey.get(FIRST_UNASSIGNED.physicalChannelKey), undefined);
});

test('B1 pair identity is enforced: a mismatched pair is refused, never silently combined', () => {
  const observations = buildObservations('NORMAL_GOOD');
  const front = observations.get(rowForMeasurement('H7:F').physicalChannelKey);
  const rear = observations.get(rowForMeasurement('H7:R').physicalChannelKey);
  const foreignRear = observations.get(rowForMeasurement('H8:R').physicalChannelKey);
  assert.equal(buildThermalPair(front, rear).state, PAIRED_THERMAL_STATE.EXPECTED_POSITIVE);
  assert.equal(buildThermalPair(front, foreignRear).reason, DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  assert.equal(buildThermalPair(rear, front).reason, DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  assert.equal(buildThermalPair(front, front).reason, DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  assert.equal(buildThermalPair(front, null).reason, DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  assert.equal(buildThermalPair(null, rear).reason, DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  // Two observations from different observation sets can never form a pair.
  const otherTick = buildObservations('NORMAL_GOOD', { tick: 9 });
  assert.equal(buildThermalPair(front, otherTick.get(rear.channelKey)).reason, DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  // A gap position and an unassigned Channel can never form a Product pair.
  assert.equal(buildThermalPair(
    observations.get(FIRST_TOPOLOGY_CONFLICT.physicalChannelKey),
    observations.get(FIRST_TOPOLOGY_CONFLICT.physicalChannelKey),
  ).reason, DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  assert.equal(describeUnpairedMeasurement(FIRST_TOPOLOGY_CONFLICT).reason, DIFFERENTIAL_UNAVAILABLE_REASON.NON_SENSOR_GAP);
  assert.equal(describeUnpairedMeasurement(FIRST_UNASSIGNED).reason, DIFFERENTIAL_UNAVAILABLE_REASON.UNASSIGNED_CHANNEL);
});

test('B2 selecting a measurement resolves to its own Sensor pair and nothing else', () => {
  const pairs = pairsFor('NORMAL_GOOD');
  for (const sensorId of ['G+201', 'H7', 'J18']) {
    const frontRow = rowForMeasurement(`${sensorId}:F`);
    const rearRow = rowForMeasurement(`${sensorId}:R`);
    const fromFront = pairForRow(pairs, frontRow);
    const fromRear = pairForRow(pairs, rearRow);
    assert.equal(fromFront.sensorId, sensorId);
    assert.equal(fromRear.sensorId, sensorId);
    assert.equal(fromFront.frontMeasurementIdentity, `${sensorId}:F`);
    assert.equal(fromRear.rearMeasurementIdentity, `${sensorId}:R`);
    assert.equal(pairs.byMeasurementKey.get(`${sensorId}:F`), pairs.bySensorId.get(sensorId));
    assert.equal(pairs.byMeasurementKey.get(`${sensorId}:R`), pairs.bySensorId.get(sensorId));
  }
  // No fallback, alias or copy: every pair's Channels belong to that Sensor's own rows only.
  for (const pair of pairs.pairs) {
    const own = schedule.rows.filter((r) => r.sensorId === pair.sensorId).map((r) => r.physicalChannelKey);
    assert.equal(own.includes(pair.frontChannelKey), true, pair.sensorId);
    assert.equal(own.includes(pair.rearChannelKey), true, pair.sensorId);
  }
});

test('C1 every invalid pair condition yields DIFFERENTIAL_UNAVAILABLE with the blocking category named', () => {
  const observations = buildObservations('NORMAL_GOOD');
  const front = observations.get(rowForMeasurement('H7:F').physicalChannelKey);
  const rear = observations.get(rowForMeasurement('H7:R').physicalChannelKey);
  const cases = [
    [{ ...front, isCurrentValueValid: false, engineeringValue: null, quality: 'BAD', diagnosticReason: 'OUT_OF_RANGE' }, rear, DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_QUALITY_NOT_VALID],
    [front, { ...rear, isCurrentValueValid: false, engineeringValue: null, quality: 'BAD', diagnosticReason: 'OUT_OF_RANGE' }, DIFFERENTIAL_UNAVAILABLE_REASON.REAR_QUALITY_NOT_VALID],
    [{ ...front, isCurrentValueValid: false, engineeringValue: null, quality: 'STALE' }, rear, DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_STALE],
    [front, { ...rear, isCurrentValueValid: false, engineeringValue: null, quality: 'STALE' }, DIFFERENTIAL_UNAVAILABLE_REASON.REAR_STALE],
    [{ ...front, isCurrentValueValid: false, engineeringValue: null, quality: 'DISABLED', diagnosticReason: 'NOT_COMMISSIONED' }, rear, DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_QUALITY_NOT_VALID],
    [{ ...front, diagnosticReason: 'COMMUNICATION_LOSS' }, rear, DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_COMMUNICATION_LOSS],
    [front, { ...rear, nodeCommunicationState: 'COMMUNICATION_LOSS' }, DIFFERENTIAL_UNAVAILABLE_REASON.REAR_COMMUNICATION_LOSS],
    [{ ...front, diagnosticReason: 'WIRE_BREAK' }, rear, DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_WIRE_BREAK],
    [front, { ...rear, diagnosticReason: 'WIRE_BREAK' }, DIFFERENTIAL_UNAVAILABLE_REASON.REAR_WIRE_BREAK],
    [front, { ...rear, engineeringUnit: 'mV' }, DIFFERENTIAL_UNAVAILABLE_REASON.UNIT_MISMATCH],
    [{ ...front, engineeringUnit: null }, rear, DIFFERENTIAL_UNAVAILABLE_REASON.UNIT_MISMATCH],
    [{ ...front, engineeringValue: Number.POSITIVE_INFINITY }, rear, DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_CURRENT_VALUE_UNAVAILABLE],
    [front, { ...rear, engineeringValue: Number.NaN }, DIFFERENTIAL_UNAVAILABLE_REASON.REAR_CURRENT_VALUE_UNAVAILABLE],
  ];
  for (const [f, r, reason] of cases) {
    const pair = buildThermalPair(f, r);
    assert.equal(pair.state, PAIRED_THERMAL_STATE.DIFFERENTIAL_UNAVAILABLE, reason);
    assert.equal(pair.reason, reason);
    assert.equal(pair.delta, null);
    assert.equal(pair.deltaText, null);
    assert.equal(pair.unit, null, 'an unavailable delta never carries a unit');
    assert.equal(pair.marker, PAIRED_THERMAL_MARKER.DIFFERENTIAL_UNAVAILABLE);
    assert.equal(pair.guidance, PAIRED_THERMAL_GUIDANCE.DIFFERENTIAL_UNAVAILABLE);
    assert.equal(pair.effect, PAIRED_THERMAL_EFFECT);
  }
});

test('C2 a Last Valid Value is never substituted, and no fallback or aliasing exists', () => {
  const observations = buildObservations('STALE');
  const pairs = buildThermalPairs(schedule, observations);
  assert.ok(pairs.counts.unavailable > 0, 'the STALE scenario must make some pairs unavailable');
  for (const pair of pairs.pairs) {
    if (pair.deltaAvailable) continue;
    assert.equal(pair.delta, null);
    assert.equal(pair.deltaText, null);
    assert.equal(pair.unit, null);
    assert.ok(pair.reason, `${pair.sensorId} must name the blocking category`);
    assert.equal(Object.values(DIFFERENTIAL_UNAVAILABLE_REASON).includes(pair.reason), true, pair.reason);
  }
  // The two sides remain individually observable while the paired value is unavailable, and the
  // reason always names the side that actually broke the pair.
  const staleObservation = [...observations.values()]
    .find((o) => o.quality === 'STALE' && o.selectableAsProductSensor === true);
  assert.notEqual(staleObservation, undefined, 'the STALE scenario must stale at least one Product measurement');
  const pair = pairs.byChannelKey.get(staleObservation.channelKey);
  assert.equal(pair.state, PAIRED_THERMAL_STATE.DIFFERENTIAL_UNAVAILABLE);
  assert.equal(pair.reason, staleObservation.side === 'F'
    ? DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_STALE
    : DIFFERENTIAL_UNAVAILABLE_REASON.REAR_STALE);
  assert.equal(staleObservation.lastValidValue !== null, true, 'the historical value still exists, it is simply never used');
  assert.equal(pair.delta, null, 'the historical value is never used as the current value of the pair');
});

test('C3 every existing scenario keeps its Quality behavior and never fabricates a delta', () => {
  const scenarios = [
    'UNCERTAIN_TRANSIENT', 'OUT_OF_RANGE', 'STALE', 'COMMUNICATION_LOSS_NODE', 'SIMULATED_WIRE_BREAK',
    'RECOVERY', 'DRAWING_CONFLICT', 'PRODUCT_TOPOLOGY_CONFLICT', 'UNASSIGNED_CHANNEL',
  ];
  for (const key of scenarios) {
    const ctx = key === 'COMMUNICATION_LOSS_NODE' ? { nodeId: 'TC-NODE-04' } : {};
    const observations = buildObservations(key, ctx);
    const pairs = buildThermalPairs(schedule, observations);
    assert.equal(observations.size, 240, key);
    assert.equal(pairs.counts.productSensors, 106, key);
    // No pair is ever invented for a gap or an unassigned Channel, whatever the scenario is.
    assert.equal(pairIds(pairs).includes('I7'), false, key);
    assert.equal(pairIds(pairs).includes('I16'), false, key);
    // An available delta always means BOTH sides really do have valid current values.
    for (const pair of pairs.pairs) {
      if (!pair.deltaAvailable) continue;
      const front = observations.get(pair.frontChannelKey);
      const rear = observations.get(pair.rearChannelKey);
      assert.equal(front.isCurrentValueValid, true, `${key}/${pair.sensorId}`);
      assert.equal(rear.isCurrentValueValid, true, `${key}/${pair.sensorId}`);
      assert.equal(pair.delta, front.engineeringValue - rear.engineeringValue, `${key}/${pair.sensorId}`);
      assert.notEqual(pair.state, PAIRED_THERMAL_STATE.DIFFERENTIAL_UNAVAILABLE, `${key}/${pair.sensorId}`);
    }
    // Whatever the scenario does, the paired model adds no alarm/trip/control vocabulary.
    assert.equal(pairEffectIsReadOnly(pairs), true, key);
  }
  // Scenario-specific expectations: a scenario that must break the pair does break it.
  assert.ok(buildThermalPairs(schedule, buildObservations('OUT_OF_RANGE')).counts.unavailable > 0);
  assert.ok(buildThermalPairs(schedule, buildObservations('SIMULATED_WIRE_BREAK')).counts.unavailable > 0);
  assert.ok(buildThermalPairs(schedule, buildObservations('COMMUNICATION_LOSS_NODE', { nodeId: 'TC-NODE-04' })).counts.unavailable > 0);
  // RECOVERY returns to valid pairs from tick 3 onwards.
  assert.ok(buildThermalPairs(schedule, buildObservations('RECOVERY', { tick: 0 })).counts.unavailable > 0);
  assert.equal(buildThermalPairs(schedule, buildObservations('RECOVERY', { tick: 5 })).counts.unavailable, 0);
  // The internal-conflict rows are real Sensors, so their pairs stay valid and the PASS blocking is
  // an evidence matter, not a paired-value matter.
  const conflict = rowsByClass('DRAWING_INTERNAL_CONFLICT')[0];
  const conflictPair = buildThermalPairs(schedule, buildObservations('NORMAL_GOOD')).byChannelKey.get(conflict.physicalChannelKey);
  assert.equal(conflictPair.deltaAvailable, true);
  assert.equal(conflictPair.state, PAIRED_THERMAL_STATE.EXPECTED_POSITIVE);
});

test('C4 the I7 / I16 conflict rows never produce a delta and stay non-Product', () => {
  const observations = buildObservations('PRODUCT_TOPOLOGY_CONFLICT');
  const pairs = buildThermalPairs(schedule, observations);
  for (const row of rowsByClass('PRODUCT_TOPOLOGY_CONFLICT')) {
    const described = pairForRow(pairs, row);
    assert.equal(described.deltaAvailable, false, row.physicalChannelKey);
    assert.equal(described.reason, DIFFERENTIAL_UNAVAILABLE_REASON.NON_SENSOR_GAP, row.physicalChannelKey);
    assert.equal(described.delta, null);
    assert.equal(described.unit, null);
  }
  // An unassigned physical Channel has no Sensor identity at all, so it can produce no pair either.
  for (const row of rowsByClass('UNASSIGNED')) {
    const described = pairForRow(pairs, row);
    assert.equal(described.deltaAvailable, false);
    assert.equal(described.reason, DIFFERENTIAL_UNAVAILABLE_REASON.UNASSIGNED_CHANNEL);
  }
});

test('D1 REDUCED_FRONT_REAR_DIFFERENTIAL keeps Front above Rear and reduces the gradient per Sensor', () => {
  const normal = pairsFor('NORMAL_GOOD');
  const reduced = pairsFor('REDUCED_FRONT_REAR_DIFFERENTIAL');
  assert.equal(reduced.counts.productSensors, 106);
  assert.equal(reduced.counts.byState[PAIRED_THERMAL_STATE.REDUCED_POSITIVE_CANDIDATE], 106);
  assert.equal(reduced.counts.unavailable, 0);
  for (const pair of reduced.pairs) {
    assert.ok(pair.frontValue > pair.rearValue, pair.sensorId);
    assert.ok(pair.delta > 0, pair.sensorId);
    assert.equal(pair.state, PAIRED_THERMAL_STATE.REDUCED_POSITIVE_CANDIDATE);
    const normalPair = pairOf(normal, pair.sensorId);
    assert.ok(pair.delta < normalPair.delta, `${pair.sensorId}: the reduced delta must be lower than NORMAL_GOOD`);
    assert.equal(pair.syntheticDiagnostic, true);
    assert.equal(pair.pairedDifferentialMode, PAIRED_DIFFERENTIAL_MODE.REDUCED_POSITIVE);
  }
  const guidance = PAIRED_THERMAL_GUIDANCE[PAIRED_THERMAL_STATE.REDUCED_POSITIVE_CANDIDATE];
  assert.match(guidance, /possible reduced local heat transfer/);
  assert.match(guidance, /possible slag insulation/);
  assert.match(guidance, /Synthetic diagnostic demonstration only/);
  assert.match(guidance, /no field confirmation/);
  assert.match(guidance, /no alarm, queue, cleaning or control effect/);
  assert.equal(/SLAG_CONFIRMED|slag confirmed|cleaning required|dispatch WaterJet|Sensor failed/i.test(guidance), false);
});

test('D2 NEAR_ZERO_FRONT_REAR_DIFFERENTIAL is deterministic, valid, near zero and declares no deadband', () => {
  const nearZero = pairsFor('NEAR_ZERO_FRONT_REAR_DIFFERENTIAL');
  assert.equal(nearZero.counts.byState[PAIRED_THERMAL_STATE.NEAR_ZERO_CANDIDATE], 106);
  assert.equal(nearZero.counts.unavailable, 0);
  const deltas = nearZero.pairs.map((p) => round1(p.delta));
  for (const pair of nearZero.pairs) {
    assert.equal(Number.isFinite(pair.frontValue), true, pair.sensorId);
    assert.equal(Number.isFinite(pair.rearValue), true, pair.sensorId);
    assert.ok(pair.delta > 0 && pair.delta <= 0.3, `${pair.sensorId}: near zero and keeping its deterministic direction`);
    assert.equal(pair.syntheticDiagnostic, true);
    assert.match(pair.guidance, /abnormal paired thermal response/);
    assert.match(pair.guidance, /does not prove slag/);
    assert.match(pair.guidance, /no Product deadband is approved/);
    assert.match(pair.guidance, /no automatic effect/);
  }
  assert.ok(new Set(deltas).size >= 2, 'the near-zero pattern still varies deterministically');
  const again = pairsFor('NEAR_ZERO_FRONT_REAR_DIFFERENTIAL', { tick: 4 });
  const repeat = pairsFor('NEAR_ZERO_FRONT_REAR_DIFFERENTIAL', { tick: 4 });
  assert.deepEqual(again.pairs.map((p) => [p.sensorId, p.delta]), repeat.pairs.map((p) => [p.sensorId, p.delta]));
});

test('D3 REVERSED_FRONT_REAR_DIFFERENTIAL puts Front below Rear and never claims slag or a fault', () => {
  const reversed = pairsFor('REVERSED_FRONT_REAR_DIFFERENTIAL');
  assert.equal(reversed.counts.byState[PAIRED_THERMAL_STATE.REVERSED_CONTRADICTION], 106);
  assert.equal(reversed.counts.unavailable, 0);
  for (const pair of reversed.pairs) {
    assert.ok(pair.frontValue < pair.rearValue, pair.sensorId);
    assert.ok(pair.delta < 0, pair.sensorId);
    assert.match(pair.deltaText, /^-\d+\.\d$/);
    assert.equal(pair.state, PAIRED_THERMAL_STATE.REVERSED_CONTRADICTION);
    assert.match(pair.guidance, /paired-measurement contradiction/);
    assert.match(pair.guidance, /Front\/Rear mapping/);
    assert.match(pair.guidance, /Sensor health/);
    assert.match(pair.guidance, /installation/);
    assert.match(pair.guidance, /process transient/);
    assert.match(pair.guidance, /never classified as slag by itself/);
    assert.equal(/slag confirmed|Sensor failed|pair failed|alarm active|cleaning required/i.test(pair.guidance), false);
  }
  // The reversed condition exists only in this explicit Synthetic scenario: every other scenario in
  // this Stage keeps Front at or above Rear.
  for (const key of ['NORMAL_GOOD', 'REDUCED_FRONT_REAR_DIFFERENTIAL', 'NEAR_ZERO_FRONT_REAR_DIFFERENTIAL']) {
    for (const pair of pairsFor(key).pairs) assert.ok(pair.delta > 0, `${key}/${pair.sensorId}`);
  }
});

test('D4 no paired scenario carries alarm, trip, interlock, queue, cleaning, WaterJet or control vocabulary', () => {
  const forbidden = /alarm|trip|interlock|queue|job|dispatch|valve|pump|axis|motion|command|write/i;
  for (const pair of pairsFor('REDUCED_FRONT_REAR_DIFFERENTIAL').pairs) {
    assert.equal(forbidden.test(pair.guidance.replace(/no [^.;]*/gi, '')), false, pair.sensorId);
  }
  assert.match(PAIRED_THERMAL_EFFECT, /no Alarm, Trip, Interlock, Cleaning Queue/);
  assert.match(PAIRED_THERMAL_EFFECT, /WaterJet dispatch/);
  assert.match(PAIRED_THERMAL_EFFECT, /field approval/);
  // No paired state is a Quality, a DiagnosticReason or a CommissioningState: the vocabularies stay
  // separate and the Product Quality vocabulary gains no INVALID member.
  assert.equal(isCurrentValueValid({ quality: 'GOOD', diagnosticReason: 'NONE' }), true);
  const qualityModule = buildObservations('NORMAL_GOOD');
  for (const observation of qualityModule.values()) {
    assert.equal(['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED'].includes(observation.quality), true);
    assert.equal('INVALID' === observation.quality, false);
  }
  for (const state of Object.values(PAIRED_THERMAL_STATE)) {
    assert.equal(['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED'].includes(state), false);
    assert.equal(['NOT_TESTED', 'IN_PROGRESS', 'PASS', 'FAIL', 'PARTIAL', 'BLOCKED'].includes(state), false);
  }
});

test('E1 the paired model adds no Channel, Sensor, measurement or loop-test target', () => {
  const base = scheduleCounts(schedule);
  assert.equal(base.physicalChannels, 240);
  assert.equal(base.productCompatibleMeasurements, 212);
  assert.equal(base.productSensors, 106);
  const pairs = pairsFor('NORMAL_GOOD');
  // The paired VALUES are not schedule rows: the evidence totals cannot move.
  assert.equal(schedule.rows.length, 240);
  assert.equal(scheduleCounts(schedule).physicalChannels, base.physicalChannels);
  assert.equal(pairs.counts.productSensors, base.productSensors);
  assert.equal(pairs.pairs.length * 2, base.productCompatibleMeasurements, '106 pairs are exactly the 212 measurements');
  for (const pair of pairs.pairs) {
    assert.equal('commissioningState' in pair, false, 'the paired value has no commissioning state of its own');
    assert.equal('verdict' in pair, false, 'the paired value has no verdict of its own');
    assert.equal('record' in pair, false, 'the paired value has no workflow record of its own');
  }
});

test('E2 the same pair is returned for both of its measurements and shares one observation set', () => {
  const observations = buildObservations('REDUCED_FRONT_REAR_DIFFERENTIAL', { tick: 2 });
  const pairs = buildThermalPairs(schedule, observations);
  const sample = pairs.pairs.find((p) => p.sensorId === 'G+107');
  assert.equal(observations.get(sample.frontChannelKey).scenarioKey, 'REDUCED_FRONT_REAR_DIFFERENTIAL');
  assert.equal(observations.get(sample.rearChannelKey).scenarioKey, 'REDUCED_FRONT_REAR_DIFFERENTIAL');
  assert.equal(observations.get(sample.frontChannelKey).pairedDifferentialMode, PAIRED_DIFFERENTIAL_MODE.REDUCED_POSITIVE);
  assert.equal(sample.pairedDifferentialMode, PAIRED_DIFFERENTIAL_MODE.REDUCED_POSITIVE);
  assert.equal(sample.scenarioKey, 'REDUCED_FRONT_REAR_DIFFERENTIAL');
  assert.equal(sample.provenance, 'SIMULATED_DIAGNOSTIC_SCENARIO');
  assert.equal(sample.observationTick, 2);
  assert.equal(sample.fieldVerificationState, 'FIELD_VERIFICATION_PENDING');
  // Building the pair twice from the same observations is idempotent: nothing is mutated.
  const before = JSON.stringify([...observations.entries()]);
  buildThermalPairs(schedule, observations);
  assert.equal(JSON.stringify([...observations.entries()]), before, 'the paired model must never mutate an observation');
});

/** True when every pair in the set communicates a read-only, no-effect diagnostic. */
function pairEffectIsReadOnly(pairs) {
  return pairs.pairs.every((pair) => pair.effect === PAIRED_THERMAL_EFFECT
    && (pair.state !== PAIRED_THERMAL_STATE.DIFFERENTIAL_UNAVAILABLE || Object.values(DIFFERENTIAL_UNAVAILABLE_REASON).includes(pair.reason)));
}
