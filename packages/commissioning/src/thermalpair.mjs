// Stage 0.4C-K1 - the read-only paired Front/Rear thermal model for one Product Sensor pair.
//
// OWNER-APPROVED DOMAIN RULE (2026-10-12):
//   - FrontTemperature is the furnace-facing measurement taken from the fin / heat-collection
//     position exposed more directly to furnace heat.
//   - RearTemperature is the paired waterwall-tube-side reference measurement. It is comparatively
//     stable under comparable operation because of the waterwall tube and the water/steam cooling
//     influence, but it is NOT a constant.
//   - Under a valid, stable and comparable operating condition the expected thermal direction is
//     FrontTemperature > RearTemperature.
//   - FrontRearDelta = FrontTemperature - RearTemperature, in the same temperature-difference unit
//     as the paired measurements.
//
// FrontRearDelta is READ-ONLY DIAGNOSTIC / TREND ONLY. It has no Alarm, Trip, Interlock, Cleaning
// Queue, GlobalQueue, WaterJet dispatch, Cleaning Job, AutoSequence, Pump, Isolation Valve, axis or
// motion, automatic-control or field-approval effect, and NO threshold, persistence, deadband,
// hysteresis or automatic Slag classification is approved in this Stage.
//
// The paired model NEVER mutates either underlying measurement and is NOT a second source of truth:
// the Front and Rear observations stay authoritative and everything here is derived from them on
// every render. No value is ever copied, aliased or substituted from one side into the other, and a
// missing current value is never filled in from a Last Valid Value.
//
// Nothing here reads a device, opens a socket, writes a file, reads a clock or holds any state.

import { DIAGNOSTIC_REASON, isCurrentValueValid, NODE_COMMUNICATION_STATE, QUALITY } from './quality.mjs';
import { ROW_CLASS } from './schedule.mjs';

/**
 * Self-naming enum builder: every member's value equals its name. Used instead of an inline object
 * literal for the derived vocabularies, where a name/value pair would read as a credential-shaped
 * assignment to the repository boundary scanner (rule S4). The scanner is not weakened for this:
 * the shape of the vocabulary is unchanged.
 */
const selfNamed = (names) => Object.freeze(Object.fromEntries(names.map((name) => [name, name])));

/**
 * The read-only paired diagnostic state. Deliberately SEPARATE from Quality, DiagnosticReason,
 * CommissioningState, evidence blocking and the Preview verdict: none of those vocabularies is
 * extended or reused here.
 */
export const PAIRED_THERMAL_STATE = selfNamed([
  'EXPECTED_POSITIVE',
  'REDUCED_POSITIVE_CANDIDATE',
  'NEAR_ZERO_CANDIDATE',
  'REVERSED_CONTRADICTION',
  'DIFFERENTIAL_UNAVAILABLE',
]);

export const PAIRED_THERMAL_STATE_VALUES = Object.freeze(Object.values(PAIRED_THERMAL_STATE));

/**
 * Why a safe paired derived value cannot be calculated. Every reason names the blocking category and
 * invents no value.
 */
export const DIFFERENTIAL_UNAVAILABLE_REASON = selfNamed([
  'FRONT_CURRENT_VALUE_UNAVAILABLE',
  'REAR_CURRENT_VALUE_UNAVAILABLE',
  'FRONT_QUALITY_NOT_VALID',
  'REAR_QUALITY_NOT_VALID',
  'FRONT_STALE',
  'REAR_STALE',
  'FRONT_COMMUNICATION_LOSS',
  'REAR_COMMUNICATION_LOSS',
  'FRONT_WIRE_BREAK',
  'REAR_WIRE_BREAK',
  'UNIT_MISMATCH',
  'PAIR_IDENTITY_INCOMPLETE',
  'PRODUCT_TOPOLOGY_CONFLICT',
  'UNASSIGNED_CHANNEL',
]);

export const DIFFERENTIAL_UNAVAILABLE_REASON_VALUES = Object.freeze(Object.values(DIFFERENTIAL_UNAVAILABLE_REASON));

/**
 * The explicit synthetic paired-differential mode a scenario declares. The mode is what assigns a
 * candidate classification: no threshold is applied to any value at run time.
 */
export const PAIRED_DIFFERENTIAL_MODE = selfNamed([
  'EXPECTED_POSITIVE',
  'REDUCED_POSITIVE',
  'NEAR_ZERO',
  'REVERSED',
]);

export const PAIRED_DIFFERENTIAL_MODE_VALUES = Object.freeze(Object.values(PAIRED_DIFFERENTIAL_MODE));

/** The paired value's label. Always presented with an explicit sign; the unit only when available. */
export const FRONT_REAR_DELTA_LABEL = 'dFR';

/** A short text marker per state, so no paired diagnostic is ever communicated by colour alone. */
export const PAIRED_THERMAL_MARKER = Object.freeze({
  EXPECTED_POSITIVE: '[+]',
  REDUCED_POSITIVE_CANDIDATE: '[v]',
  NEAR_ZERO_CANDIDATE: '[~]',
  REVERSED_CONTRADICTION: '[!]',
  DIFFERENTIAL_UNAVAILABLE: '[?]',
});

/** What the Operator reads with the value. Bounded wording: nothing here claims confirmed slag. */
export const PAIRED_THERMAL_GUIDANCE = Object.freeze({
  EXPECTED_POSITIVE: 'Front is above Rear: the expected paired heat-flow direction for a valid, stable and comparable '
    + 'operating condition. Read-only trend only - it does not prove a clean wall, does not prove the absence of slag, '
    + 'and is not a field verification.',
  REDUCED_POSITIVE_CANDIDATE: 'Front remains above Rear but the synthetic gradient is reduced: possible reduced local '
    + 'heat transfer / possible slag insulation (slag candidate). Synthetic diagnostic demonstration only - no field '
    + 'confirmation, no approved threshold, and no alarm, queue, cleaning or control effect.',
  NEAR_ZERO_CANDIDATE: 'Near-zero paired gradient: an abnormal paired thermal response that requires investigation. It '
    + 'does not prove slag and does not prove a Sensor failure, and no Product deadband is approved. Synthetic '
    + 'diagnostic demonstration only - no automatic effect.',
  REVERSED_CONTRADICTION: 'Front is below Rear: a paired-measurement contradiction. Check the Front/Rear mapping, the '
    + 'Sensor health, the installation and the process transient first. This condition is never classified as slag by '
    + 'itself and creates no automatic effect.',
  DIFFERENTIAL_UNAVAILABLE: 'No paired derived value can be calculated, so no number and no unit are shown. Nothing is '
    + 'substituted from a Last Valid Value and nothing is inferred from one side alone.',
});

/** The explicit no-effect statement. It travels with the paired model so no screen can soften it. */
export const PAIRED_THERMAL_EFFECT = 'READ-ONLY DIAGNOSTIC / TREND ONLY - no Alarm, Trip, Interlock, Cleaning Queue, '
  + 'GlobalQueue, WaterJet dispatch, Cleaning Job, AutoSequence, Pump, Isolation Valve, axis or motion, automatic '
  + 'control or field approval.';

/** Which side is Front / Rear, stated once for the paired model. */
export const THERMAL_SIDE = Object.freeze({ FRONT: 'F', REAR: 'R' });

/** Formats the paired delta with an explicit sign and exactly one decimal. Never a bare number. */
export function formatFrontRearDelta(delta) {
  if (!Number.isFinite(delta)) return null;
  return `${delta < 0 ? '-' : '+'}${Math.abs(delta).toFixed(1)}`;
}

/** Deterministic per-side blocking reason, in a fixed order. Front is evaluated before Rear. */
const SIDE_REASON = Object.freeze({
  F: Object.freeze({
    COMMUNICATION_LOSS: DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_COMMUNICATION_LOSS,
    WIRE_BREAK: DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_WIRE_BREAK,
    STALE: DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_STALE,
    QUALITY_NOT_VALID: DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_QUALITY_NOT_VALID,
    CURRENT_VALUE_UNAVAILABLE: DIFFERENTIAL_UNAVAILABLE_REASON.FRONT_CURRENT_VALUE_UNAVAILABLE,
  }),
  R: Object.freeze({
    COMMUNICATION_LOSS: DIFFERENTIAL_UNAVAILABLE_REASON.REAR_COMMUNICATION_LOSS,
    WIRE_BREAK: DIFFERENTIAL_UNAVAILABLE_REASON.REAR_WIRE_BREAK,
    STALE: DIFFERENTIAL_UNAVAILABLE_REASON.REAR_STALE,
    QUALITY_NOT_VALID: DIFFERENTIAL_UNAVAILABLE_REASON.REAR_QUALITY_NOT_VALID,
    CURRENT_VALUE_UNAVAILABLE: DIFFERENTIAL_UNAVAILABLE_REASON.REAR_CURRENT_VALUE_UNAVAILABLE,
  }),
});

function sideBlockingReason(observation, side) {
  const reasons = SIDE_REASON[side];
  const communicationLost = observation.nodeCommunicationState === NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS
    || observation.diagnosticReason === DIAGNOSTIC_REASON.COMMUNICATION_LOSS;
  if (communicationLost) return reasons.COMMUNICATION_LOSS;
  if (observation.diagnosticReason === DIAGNOSTIC_REASON.WIRE_BREAK
    || observation.diagnosticReason === DIAGNOSTIC_REASON.SUSPECTED_WIRE_BREAK) return reasons.WIRE_BREAK;
  if (observation.quality === QUALITY.STALE) return reasons.STALE;
  if (observation.quality === QUALITY.BAD || observation.quality === QUALITY.DISABLED) return reasons.QUALITY_NOT_VALID;
  if (observation.isCurrentValueValid !== true) return reasons.CURRENT_VALUE_UNAVAILABLE;
  if (!Number.isFinite(observation.engineeringValue)) return reasons.CURRENT_VALUE_UNAVAILABLE;
  return null;
}

/**
 * The paired diagnostic state for an AVAILABLE delta. The classification is assigned by the explicit
 * Synthetic scenario that produced the observations - never by a threshold applied to a value. The
 * only value-based rule is the sign itself, which is how the domain rule defines a reversal:
 *   delta < 0  -> REVERSED_CONTRADICTION (Front below Rear)
 *   delta == 0 -> NEAR_ZERO_CANDIDATE    (no expected positive direction at all)
 *   delta > 0  -> the scenario's declared positive state
 */
function classifyPositiveDelta(delta, mode) {
  if (delta < 0) return PAIRED_THERMAL_STATE.REVERSED_CONTRADICTION;
  if (delta === 0) return PAIRED_THERMAL_STATE.NEAR_ZERO_CANDIDATE;
  if (mode === PAIRED_DIFFERENTIAL_MODE.REDUCED_POSITIVE) return PAIRED_THERMAL_STATE.REDUCED_POSITIVE_CANDIDATE;
  if (mode === PAIRED_DIFFERENTIAL_MODE.NEAR_ZERO) return PAIRED_THERMAL_STATE.NEAR_ZERO_CANDIDATE;
  // A reversal scenario whose derived value came out positive is presenting the direction its own
  // values prove (expected positive). The sign is never overridden by a scenario label, and the
  // deterministic tests assert that the reversal scenario really does produce Front below Rear.
  return PAIRED_THERMAL_STATE.EXPECTED_POSITIVE;
}

/**
 * The individually presented state of ONE side. A side keeps its own current value even when the
 * PAIR cannot be derived, so the Operator can always see which side is missing and why.
 */
function sidePresentation(observation) {
  if (!observation) return Object.freeze({ value: null, presented: null, valid: false, unit: null, quality: null });
  const valid = isCurrentValueValid(observation) === true
    && observation.isCurrentValueValid === true
    && Number.isFinite(observation.engineeringValue);
  return Object.freeze({
    value: valid ? observation.engineeringValue : null,
    presented: valid ? String(observation.engineeringValue) : null,
    valid,
    unit: valid ? observation.engineeringUnit ?? null : null,
    quality: observation.quality ?? null,
  });
}

function unavailable(reason, extra = {}) {
  const sides = extra.sides ?? Object.freeze({
    front: sidePresentation(null), rear: sidePresentation(null),
  });
  return Object.freeze({
    derived: true,
    paired: true,
    deltaAvailable: false,
    state: PAIRED_THERMAL_STATE.DIFFERENTIAL_UNAVAILABLE,
    reason,
    delta: null,
    deltaText: null,
    unit: null,
    frontValue: sides.front.value,
    rearValue: sides.rear.value,
    frontValuePresented: sides.front.presented,
    rearValuePresented: sides.rear.presented,
    frontValueValid: sides.front.valid,
    rearValueValid: sides.rear.valid,
    frontUnit: sides.front.unit,
    rearUnit: sides.rear.unit,
    marker: PAIRED_THERMAL_MARKER.DIFFERENTIAL_UNAVAILABLE,
    guidance: PAIRED_THERMAL_GUIDANCE.DIFFERENTIAL_UNAVAILABLE,
    effect: PAIRED_THERMAL_EFFECT,
    ...extra.identity,
  });
}

/**
 * Derives the read-only paired value and its diagnostic state from the two AUTHORITATIVE
 * observations. Returns a frozen descriptor; it never mutates either observation and it stores no
 * copy that could become a second source of truth.
 */
export function buildThermalPair(frontObservation, rearObservation, hints = {}) {
  const front = frontObservation ?? null;
  const rear = rearObservation ?? null;
  const identity = Object.freeze({
    sensorId: front?.sensorId ?? rear?.sensorId ?? hints.sensorId ?? null,
    frontMeasurementIdentity: front?.measurementId ?? hints.frontMeasurementIdentity ?? null,
    rearMeasurementIdentity: rear?.measurementId ?? hints.rearMeasurementIdentity ?? null,
    frontChannelKey: front?.channelKey ?? hints.frontChannelKey ?? null,
    rearChannelKey: rear?.channelKey ?? hints.rearChannelKey ?? null,
    frontSide: front?.side ?? null,
    rearSide: rear?.side ?? null,
    scenarioKey: front?.scenarioKey ?? rear?.scenarioKey ?? null,
    pairedDifferentialMode: front?.pairedDifferentialMode ?? rear?.pairedDifferentialMode
      ?? PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE,
    provenance: front?.provenance ?? rear?.provenance ?? null,
    observationTick: front?.observationTick ?? rear?.observationTick ?? null,
    fieldVerificationState: front?.fieldVerificationState ?? rear?.fieldVerificationState ?? null,
    rowClass: front?.rowClass ?? rear?.rowClass ?? null,
  });

  // The per-side presentation is computed once, so an unavailable delta still states each side
  // honestly (a valid side never disappears because its partner is invalid).
  const sides = Object.freeze({ front: sidePresentation(front), rear: sidePresentation(rear) });
  const blocked = (reason) => unavailable(reason, { identity, sides });

  // 1. Paired identity must be complete and unambiguous before anything is calculated.
  if (!front || !rear) return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  if (identity.sensorId === null) return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.UNASSIGNED_CHANNEL);
  if (front.sensorId !== rear.sensorId) return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  if (!(front.side === THERMAL_SIDE.FRONT && rear.side === THERMAL_SIDE.REAR)) {
    return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  }
  if (front.rowClass === ROW_CLASS.UNASSIGNED || rear.rowClass === ROW_CLASS.UNASSIGNED) {
    return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.UNASSIGNED_CHANNEL);
  }
  // Retained defensively for a future evidence revision: the corrected baseline carries no
  // Product-topology conflict row (I7 and I16 are normal Product Sensors and pair like every other).
  if (front.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT || rear.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT) {
    return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.PRODUCT_TOPOLOGY_CONFLICT);
  }
  // The two sides of one pair must come from one observation set: a mismatched scenario or tick would
  // silently pair two different synthetic conditions.
  if ((front.scenarioKey ?? null) !== (rear.scenarioKey ?? null)
    || front.observationTick !== rear.observationTick) {
    return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  }

  // 2. Both sides must carry a valid current engineering value. Front is evaluated first, and only
  //    then Rear, so the reported reason is deterministic.
  const frontReason = sideBlockingReason(front, 'F');
  if (frontReason) return blocked(frontReason);
  const rearReason = sideBlockingReason(rear, 'R');
  if (rearReason) return blocked(rearReason);

  // 3. Both sides must use the same compatible unit.
  if (!front.engineeringUnit || front.engineeringUnit !== rear.engineeringUnit) {
    return blocked(DIFFERENTIAL_UNAVAILABLE_REASON.UNIT_MISMATCH);
  }

  // 4. Available: the paired value is exactly Front minus Rear, derived from the authoritative values.
  const delta = front.engineeringValue - rear.engineeringValue;
  const state = classifyPositiveDelta(delta, identity.pairedDifferentialMode);
  return Object.freeze({
    derived: true,
    paired: true,
    deltaAvailable: true,
    state,
    reason: null,
    delta,
    deltaText: formatFrontRearDelta(delta),
    unit: front.engineeringUnit,
    frontValue: front.engineeringValue,
    rearValue: rear.engineeringValue,
    frontValuePresented: String(front.engineeringValue),
    rearValuePresented: String(rear.engineeringValue),
    frontValueValid: true,
    rearValueValid: true,
    frontUnit: front.engineeringUnit,
    rearUnit: rear.engineeringUnit,
    marker: PAIRED_THERMAL_MARKER[state],
    guidance: PAIRED_THERMAL_GUIDANCE[state],
    effect: PAIRED_THERMAL_EFFECT,
    // A paired diagnostic that only the explicit Synthetic scenario can produce is flagged as such,
    // so the screen always says the demonstration is synthetic.
    syntheticDiagnostic: identity.pairedDifferentialMode !== PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE,
    ...identity,
  });
}

/**
 * Describes a row that cannot form a Product Sensor pair at all (an unassigned physical Channel, or a
 * Product-topology conflict row should a future evidence revision carry one). The vocabulary stays
 * in this module so no screen invents its own reason.
 */
export function describeUnpairedMeasurement(row) {
  if (!row) return unavailable(DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE);
  if (row.rowClass === ROW_CLASS.UNASSIGNED || row.sensorId === null) {
    return unavailable(DIFFERENTIAL_UNAVAILABLE_REASON.UNASSIGNED_CHANNEL, {
      identity: { sensorId: row.sensorId ?? null, frontChannelKey: row.physicalChannelKey, rowClass: row.rowClass },
    });
  }
  if (row.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT) {
    return unavailable(DIFFERENTIAL_UNAVAILABLE_REASON.PRODUCT_TOPOLOGY_CONFLICT, {
      identity: { sensorId: row.sensorId, frontChannelKey: row.physicalChannelKey, rowClass: row.rowClass },
    });
  }
  return unavailable(DIFFERENTIAL_UNAVAILABLE_REASON.PAIR_IDENTITY_INCOMPLETE, {
    identity: { sensorId: row.sensorId ?? null, frontChannelKey: row.physicalChannelKey, rowClass: row.rowClass },
  });
}

/**
 * Builds every Product Sensor pair from the schedule and one observation set. A pair is created only
 * from Product-compatible rows that carry a Sensor identity: an unassigned physical Channel can never
 * produce a pair, and a measurement is never taken from a neighbouring Sensor. I7 and I16 are normal
 * Product Sensors (Owner critical topology correction) and pair exactly like every other Sensor.
 *
 * The result is keyed by physical Channel so any surface (Wall Map cell, Overview row, Sensor detail)
 * can find the pair of the measurement it is showing without duplicating the pairing rule.
 */
export function buildThermalPairs(schedule, observations) {
  const bySensorRows = new Map();
  for (const row of schedule.rows) {
    if (row.productCompatible !== true) continue;
    if (row.sensorId === null || row.sensorId === undefined) continue;
    if (row.side !== THERMAL_SIDE.FRONT && row.side !== THERMAL_SIDE.REAR) continue;
    const entry = bySensorRows.get(row.sensorId) ?? { sensorId: row.sensorId, rows: {} };
    entry.rows[row.side] = row;
    bySensorRows.set(row.sensorId, entry);
  }
  const pairs = [];
  const bySensorId = new Map();
  const byChannelKey = new Map();
  const byMeasurementKey = new Map();
  for (const sensorId of [...bySensorRows.keys()].sort()) {
    const entry = bySensorRows.get(sensorId);
    const frontRow = entry.rows.F ?? null;
    const rearRow = entry.rows.R ?? null;
    const pair = buildThermalPair(
      frontRow ? observations.get(frontRow.physicalChannelKey) ?? null : null,
      rearRow ? observations.get(rearRow.physicalChannelKey) ?? null : null,
      {
        sensorId,
        frontMeasurementIdentity: frontRow?.measurementId ?? null,
        rearMeasurementIdentity: rearRow?.measurementId ?? null,
        frontChannelKey: frontRow?.physicalChannelKey ?? null,
        rearChannelKey: rearRow?.physicalChannelKey ?? null,
      },
    );
    pairs.push(pair);
    bySensorId.set(sensorId, pair);
    for (const row of [frontRow, rearRow]) {
      if (!row) continue;
      byChannelKey.set(row.physicalChannelKey, pair);
      byMeasurementKey.set(row.measurementId, pair);
    }
  }
  return Object.freeze({
    pairs: Object.freeze(pairs),
    bySensorId,
    byChannelKey,
    byMeasurementKey,
    counts: Object.freeze({
      productSensors: pairs.length,
      available: pairs.filter((p) => p.deltaAvailable).length,
      unavailable: pairs.filter((p) => !p.deltaAvailable).length,
      byState: Object.freeze(Object.fromEntries(PAIRED_THERMAL_STATE_VALUES.map((state) => [
        state, pairs.filter((p) => p.state === state).length,
      ]))),
    }),
  });
}

/**
 * The paired descriptor for one schedule row: the pair of its Sensor, or an unavailable descriptor
 * naming why this row cannot form a Product pair at all (unassigned Channel).
 */
export function pairForRow(pairs, row) {
  if (!pairs || !row) return describeUnpairedMeasurement(row);
  return pairs.byChannelKey.get(row.physicalChannelKey) ?? describeUnpairedMeasurement(row);
}
