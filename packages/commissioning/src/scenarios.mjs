// Stage 0.4C-K1 - deterministic synthetic commissioning scenarios.
//
// Every scenario is a pure, deterministic function of the schedule row and an integer preview clock
// tick. Two runs with the same inputs produce deep-equal output; nothing here reads a clock, a
// random source, the environment or a device.
//
// The scenario catalogue also carries the HONESTY of each scenario:
//   - WIRE_BREAK is a SIMULATED DIAGNOSTIC SCENARIO. Physical cyclic wire-break decoding remains
//     BLOCKED in the merged baseline (the module capability is documented, but no explicit
//     wire-break status is available or established in the documented cyclic image). A raw 0x7FFF
//     word alone must never become a confirmed wire break, so the scenario presents the intended
//     future VISUAL and WORKFLOW result and says so on screen.
//   - Every other scenario is a synthetic preview observation. None of them is a Hardware reading.

import { ROW_CLASS, isProductSensorId } from './schedule.mjs';
import {
  COMMISSIONING_STATE, DIAGNOSTIC_REASON, FIELD_VERIFICATION_STATE, NODE_COMMUNICATION_STATE,
  OBSERVATION_PROVENANCE, PREVIEW_STALE_THRESHOLD_MS, QUALITY,
} from './quality.mjs';
import { PAIRED_DIFFERENTIAL_MODE } from './thermalpair.mjs';

// -----------------------------------------------------------------------------------------------
// Synthetic paired-temperature generation (Owner Manual Review correction, 2026-10-12).
//
// The generator previously biased the Rear side UP by a fixed offset (`sideBias = side === 'F' ? 0
// : 4`), so the Preview systematically rendered Rear above Front and taught the Operator to read the
// pair backwards. That is corrected here at the SOURCE of the values, not in the rendered text:
//
//   RearTemperature  = deterministic paired REFERENCE (the waterwall-tube-side sample)
//   FrontTemperature = RearTemperature + deterministic POSITIVE differential
//
// The reference and the differential are pure functions of the stable Sensor identity (FNV-1a over
// the identity, no random source, no clock, no environment) and deliberately do NOT depend on the
// preview tick: deterministic, finite, plausible, varied across Sensors (never one universal delta),
// independent of wall rendering order and independent of browser timing. A Temperature pair is generated ONLY for a Product-compatible row that
// carries a Product Sensor identity (all 108 Sensors, I7 and I16 included); an unassigned Channel and
// any non-Product row keep an identity-based value of their own and can never form a pair.
// -----------------------------------------------------------------------------------------------

/** FNV-1a over a short text - deterministic, no random source, no clock, no environment. */
function stableHash(text) {
  let hash = 0x811c9dc5;
  for (let i = 0; i < text.length; i += 1) {
    hash ^= text.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash >>> 0;
}

const round1 = (value) => Math.round(value * 10) / 10;

/** The stable numeric part of a Sensor identity ('G+201' -> 201), used only for zone variation. */
function sensorOrdinal(sensorId) {
  const digits = String(sensorId ?? '').match(/\d+/);
  return digits ? Number.parseInt(digits[0], 10) : 1;
}

/** True when this row is one of the two measurements of a Product Sensor pair. */
export function isPairedTemperatureRow(row) {
  return row.productCompatible === true
    && row.sensorId !== null && row.sensorId !== undefined
    && isProductSensorId(row.sensorId)
    && (row.side === 'F' || row.side === 'R');
}

/**
 * The deterministic paired thermal REFERENCE: the Rear (waterwall-tube-side) sample. It is
 * comparatively stable under comparable operation, but it is NOT a constant: it varies per Sensor
 * (the whole point of a per-Sensor baseline) and per physical zone.
 *
 * `tick` is accepted for call-site symmetry with the observation contract but deliberately does NOT
 * enter the value: a row's engineering value stays a pure function of the physical identity, so the
 * same Sensor renders the same sample at every tick while the observation timestamp and the value
 * age move with the deterministic preview clock.
 */
export function syntheticPairedReference(sensorId, tick = 0) {
  void tick;
  const hash = stableHash(`${sensorId}|rear-wall-reference`);
  return round1(26 + (sensorOrdinal(sensorId) % 9) * 1.2 + (hash % 400) / 100);
}

/** The NORMAL_GOOD paired differential: positive, bounded, varied per Sensor - never one fixed delta. */
export function syntheticExpectedDifferential(sensorId, tick = 0) {
  void tick;
  const hash = stableHash(`${sensorId}|paired-differential`);
  return round1(3.2 + (hash % 37) / 10);
}

/**
 * The differential the SELECTED scenario declares for this Sensor and tick. Only the explicit
 * Synthetic paired scenarios move it; no threshold is applied to any value at run time.
 */
export function syntheticPairedDifferential(sensorId, tick = 0, mode = PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE) {
  const expected = syntheticExpectedDifferential(sensorId, tick);
  const hash = stableHash(`${sensorId}|paired-differential-mode`);
  if (mode === PAIRED_DIFFERENTIAL_MODE.REDUCED_POSITIVE) return round1(Math.max(0.6, expected * 0.25));
  if (mode === PAIRED_DIFFERENTIAL_MODE.NEAR_ZERO) return round1(0.1 + (hash % 2) * 0.1);
  if (mode === PAIRED_DIFFERENTIAL_MODE.REVERSED) return -round1(1.0 + (hash % 25) / 10);
  return expected;
}

/** The deterministic paired FRONT temperature: the reference plus the scenario's differential. */
export function syntheticPairedFrontTemperature(sensorId, tick = 0, mode = PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE) {
  return round1(syntheticPairedReference(sensorId, tick) + syntheticPairedDifferential(sensorId, tick, mode));
}

/** Identity-based deterministic value for a row that cannot form a Product Sensor pair. */
function syntheticUnpairedEngineeringValue(row) {
  const channel = (row.channel - 1) % 8;
  const moduleIndex = Number.parseInt(row.moduleInstance.split('.')[1] ?? '1', 10) - 1;
  const nodeIndex = Number.parseInt(row.nodeId.split('-')[2] ?? '1', 10) - 1;
  const base = 20 + nodeIndex * 3 + moduleIndex * 1.5 + channel * 2;
  const fraction = ((row.scheduleRow * 7) % 10) / 10;
  return Math.round((base + fraction) * 10) / 10;
}

/**
 * Deterministic Type-K-plausible temperature for one row. For a Product Sensor pair the Rear row
 * returns the paired reference and the Front row returns the reference PLUS the scenario's positive
 * differential, so Front is above Rear for every complete pair under NORMAL_GOOD.
 */
export function syntheticEngineeringValue(row, tick = 0, pairedMode = PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE) {
  if (!isPairedTemperatureRow(row)) return syntheticUnpairedEngineeringValue(row);
  if (row.side === 'R') return syntheticPairedReference(row.sensorId, tick);
  return syntheticPairedFrontTemperature(row.sensorId, tick, pairedMode);
}

/** Deterministic raw word for a 0.1 unit-per-digit scaling: value x 10 as a 16-bit two's complement word. */
export function syntheticRawValue(engineeringValue) {
  const word = Math.round(engineeringValue * 10);
  return ((word % 65536) + 65536) % 65536;
}

const rowIndex = (row) => row.scheduleRow;

function assertDeterministicScenario(name) {
  if (!name) throw new Error('scenario requires a name');
}

/**
 * Scenario definitions. `appliesTo` decides which rows the scenario's condition covers; rows outside
 * the condition stay valid, which is how node scoping is expressed for COMMUNICATION_LOSS_NODE.
 */
const DEFAULT_PAIRED = Object.freeze({
  mode: PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE,
  note: 'The paired synthetic physics: Rear is the waterwall-tube-side reference and Front is the '
    + 'furnace-facing fin measurement, so Front is expected above Rear under a valid, stable and '
    + 'comparable operating condition.',
});

const SCENARIO_DEFINITIONS = [
  Object.freeze({
    key: 'NORMAL_GOOD',
    label: 'NORMAL_GOOD - plausible Type K sample',
    summary: 'Quality GOOD, current value valid, no diagnostic. Every complete Product Sensor pair shows Front above Rear: '
      + 'Rear is the paired waterwall-tube-side reference and Front is the furnace-facing measurement plus a deterministic '
      + 'positive differential.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    paired: Object.freeze({
      mode: PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE,
      note: 'Front above Rear for every complete Product Sensor pair: the expected paired heat-flow direction.',
    }),
    appliesTo: () => true,
    observation: (row, tick, ctx) => baseObservation(row, tick, {
      quality: QUALITY.GOOD,
      diagnosticReason: DIAGNOSTIC_REASON.NONE,
      pairedMode: ctx?.pairedMode,
    }),
  }),
  Object.freeze({
    key: 'UNCERTAIN_TRANSIENT',
    label: 'UNCERTAIN_TRANSIENT - presentation marked, no control effect',
    summary: 'Quality UNCERTAIN. The current value is presented and marked UNCERTAIN; it never triggers automatic control.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => rowIndex(row) % 3 === 0,
    observation: (row, tick) => baseObservation(row, tick, {
      quality: QUALITY.UNCERTAIN,
      diagnosticReason: DIAGNOSTIC_REASON.SUSPECTED_WIRE_BREAK,
      uncertaintyNote: 'transient sample deviation - presentation only, no automatic action',
    }),
  }),
  Object.freeze({
    key: 'OUT_OF_RANGE',
    label: 'OUT_OF_RANGE - Quality BAD, current value unavailable',
    summary: 'Quality BAD with DiagnosticReason OUT_OF_RANGE. The current engineering value is unavailable and the last valid value is history only.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => rowIndex(row) % 5 === 0,
    observation: (row, tick) => invalidObservation(row, tick, {
      quality: QUALITY.BAD,
      diagnosticReason: DIAGNOSTIC_REASON.OUT_OF_RANGE,
      rawValue: 0x3584,
      note: 'overrange clamp representation - raw word is evidence, not a current value',
    }),
  }),
  Object.freeze({
    key: 'STALE',
    label: 'STALE - sample older than the Preview threshold',
    summary: 'Quality STALE with value age beyond the Preview staleness threshold. The stale value is not presented as the current value.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => rowIndex(row) % 4 === 0,
    observation: (row, tick) => invalidObservation(row, tick, {
      quality: QUALITY.STALE,
      diagnosticReason: DIAGNOSTIC_REASON.MODULE_FAULT,
      rawValue: null,
      ageMs: PREVIEW_STALE_THRESHOLD_MS + 15000 + (tick % 5) * 1000,
      note: 'no fresh sample received - age exceeds the Preview staleness threshold',
    }),
  }),
  Object.freeze({
    key: 'COMMUNICATION_LOSS_NODE',
    label: 'COMMUNICATION_LOSS_NODE - one node, five unaffected',
    summary: 'Quality BAD with DiagnosticReason COMMUNICATION_LOSS, scoped to ONE selected node. The other five nodes stay available.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    requiresNode: true,
    // The whole node loses its link, so every Channel of that node reports the link state, including
    // the unassigned positions (which report no measurement either way).
    nodeState: NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS,
    appliesTo: (row, ctx) => row.nodeId === ctx?.nodeId,
    observation: (row, tick) => invalidObservation(row, tick, {
      quality: QUALITY.BAD,
      diagnosticReason: DIAGNOSTIC_REASON.COMMUNICATION_LOSS,
      rawValue: null,
      nodeCommunicationState: NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS,
      note: 'no response from this node - other nodes are unaffected',
    }),
  }),
  Object.freeze({
    key: 'SIMULATED_WIRE_BREAK',
    label: 'SIMULATED_WIRE_BREAK - simulated diagnostic scenario',
    summary: 'Intended future wire-break presentation. Physical cyclic wire-break decoding remains BLOCKED, so this result is simulated and labelled as such.',
    provenance: OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO,
    appliesTo: (row) => rowIndex(row) % 7 === 0,
    observation: (row, tick) => invalidObservation(row, tick, {
      quality: QUALITY.BAD,
      diagnosticReason: DIAGNOSTIC_REASON.WIRE_BREAK,
      // 0x7FFF alone is NOT a confirmed wire break; it is shown as raw evidence beside a simulated verdict.
      rawValue: 0x7FFF,
      note: 'raw word shown as evidence only - the wire-break verdict here is simulated, not decoded',
    }),
  }),
  Object.freeze({
    key: 'RECOVERY',
    label: 'RECOVERY - return to a valid sample',
    summary: 'Returns from BAD / STALE / COMMUNICATION_LOSS to a valid sample and shows a Preview stabilisation state. It does not infer a Production-approved stabilisation duration.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => rowIndex(row) % 6 === 0,
    observation: (row, tick) => {
      const stabilizing = tick < 3;
      const base = baseObservation(row, tick, {
        quality: stabilizing ? QUALITY.UNCERTAIN : QUALITY.GOOD,
        diagnosticReason: stabilizing ? DIAGNOSTIC_REASON.SUSPECTED_WIRE_BREAK : DIAGNOSTIC_REASON.NONE,
        stabilisation: {
          state: stabilizing ? 'PREVIEW_STABILIZING' : 'PREVIEW_STABLE',
          ticks: tick,
          note: 'Preview stabilisation display only - no Production stabilisation duration is defined or implied',
        },
      });
      return base;
    },
  }),
  Object.freeze({
    key: 'DRAWING_CONFLICT',
    label: 'DRAWING_CONFLICT - internal Drawing conflict row',
    summary: 'Shows one of the 12 internal Drawing conflict rows. Commissioning state is BLOCKED and no commissioning PASS is permitted.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => row.rowClass === ROW_CLASS.DRAWING_INTERNAL_CONFLICT,
    observation: (row, tick) => invalidObservation(row, tick, {
      quality: QUALITY.BAD,
      diagnosticReason: DIAGNOSTIC_REASON.CONFIGURATION_INVALID,
      rawValue: null,
      commissioningState: COMMISSIONING_STATE.BLOCKED,
      blockingReason: 'DRAWING_INTERNAL_CONFLICT: sheath and conductor disagree on this row; FIELD_RECONCILIATION_REQUIRED',
      note: 'raw drawing evidence retained - not a verified mapping',
    }),
  }),
  // Retained defensively: the corrected baseline carries no Product-topology conflict row (I7 and I16
  // are normal Product Sensors), so this scenario applies to no row of the committed evidence.
  Object.freeze({
    key: 'PRODUCT_TOPOLOGY_CONFLICT',
    label: 'PRODUCT_TOPOLOGY_CONFLICT - no row in the corrected baseline',
    summary: 'A Product-topology conflict row would be BLOCKED from any commissioning PASS. The corrected baseline carries none: I7 and I16 are normal Product Sensors.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => row.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT,
    observation: (row, tick) => invalidObservation(row, tick, {
      quality: QUALITY.BAD,
      diagnosticReason: DIAGNOSTIC_REASON.NOT_MAPPED,
      rawValue: null,
      commissioningState: COMMISSIONING_STATE.BLOCKED,
      blockingReason: 'PRODUCT_TOPOLOGY_CONFLICT: the drawing row contradicts the Product topology',
      note: 'raw drawing evidence retained - not a verified mapping',
    }),
  }),
  Object.freeze({
    key: 'UNASSIGNED_CHANNEL',
    label: 'UNASSIGNED_CHANNEL - spare zone, no Sensor identity',
    summary: 'No Sensor identity, no engineering value and no field-verification claim. Not treated as a commissioned spare.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => row.rowClass === ROW_CLASS.UNASSIGNED,
    observation: unassignedObservation,
  }),

  // ---------------------------------------------------------------------------------------------
  // Explicit Synthetic PAIRED-THERMAL diagnostic demonstrations. They are the ONLY source of the
  // REDUCED_POSITIVE_CANDIDATE / NEAR_ZERO_CANDIDATE / REVERSED_CONTRADICTION classifications: no
  // threshold, deadband or persistence is applied to any value at run time, and none of them is an
  // alarm, a Cleaning Queue trigger, a WaterJet dispatch request or a field conclusion.
  // ---------------------------------------------------------------------------------------------
  Object.freeze({
    key: 'REDUCED_FRONT_REAR_DIFFERENTIAL',
    label: 'REDUCED_FRONT_REAR_DIFFERENTIAL - synthetic reduced positive gradient',
    summary: 'Synthetic paired demonstration: Front stays above Rear but the differential is deliberately reduced - '
      + 'possible reduced local heat transfer / possible slag insulation (slag candidate). Synthetic diagnostic only: '
      + 'no field confirmation, no approved threshold and no alarm or control effect.',
    provenance: OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO,
    paired: Object.freeze({
      mode: PAIRED_DIFFERENTIAL_MODE.REDUCED_POSITIVE,
      note: 'Reduced positive paired gradient: a reduced local heat-transfer / possible slag-insulation CANDIDATE, '
        + 'never a confirmed result.',
    }),
    appliesTo: () => true,
    observation: (row, tick, ctx) => baseObservation(row, tick, {
      quality: QUALITY.GOOD,
      diagnosticReason: DIAGNOSTIC_REASON.NONE,
      pairedMode: ctx?.pairedMode,
      uncertaintyNote: 'synthetic reduced paired gradient - candidate wording only, no threshold and no automatic action',
    }),
  }),
  Object.freeze({
    key: 'NEAR_ZERO_FRONT_REAR_DIFFERENTIAL',
    label: 'NEAR_ZERO_FRONT_REAR_DIFFERENTIAL - synthetic near-zero gradient',
    summary: 'Synthetic paired demonstration: Front and Rear stay finite and valid while the paired gradient is near zero - '
      + 'an abnormal paired thermal response that requires investigation. No Product deadband and no automatic action exist.',
    provenance: OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO,
    paired: Object.freeze({
      mode: PAIRED_DIFFERENTIAL_MODE.NEAR_ZERO,
      note: 'Near-zero paired gradient: an abnormal paired response requiring investigation - it proves neither slag nor a '
        + 'Sensor failure.',
    }),
    appliesTo: () => true,
    observation: (row, tick, ctx) => baseObservation(row, tick, {
      quality: QUALITY.GOOD,
      diagnosticReason: DIAGNOSTIC_REASON.NONE,
      pairedMode: ctx?.pairedMode,
      uncertaintyNote: 'synthetic near-zero paired gradient - no Product deadband is defined, no automatic action exists',
    }),
  }),
  Object.freeze({
    key: 'REVERSED_FRONT_REAR_DIFFERENTIAL',
    label: 'REVERSED_FRONT_REAR_DIFFERENTIAL - synthetic reversed pair',
    summary: 'Synthetic paired demonstration: Front is below Rear, which is a paired-measurement contradiction. Check the '
      + 'Front/Rear mapping, the Sensor health, the installation and the process transient: this is never classified as '
      + 'slag by itself and creates no automatic effect.',
    provenance: OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO,
    paired: Object.freeze({
      mode: PAIRED_DIFFERENTIAL_MODE.REVERSED,
      note: 'Reversed pair: mapping / Sensor health / installation / process transient must be checked first, and the '
        + 'condition is never classified as slag by itself.',
    }),
    appliesTo: () => true,
    observation: (row, tick, ctx) => baseObservation(row, tick, {
      quality: QUALITY.GOOD,
      diagnosticReason: DIAGNOSTIC_REASON.NONE,
      pairedMode: ctx?.pairedMode,
      uncertaintyNote: 'synthetic reversed pair - check mapping, Sensor health, installation and the process transient first',
    }),
  }),
];

/**
 * The scenario catalogue, with the paired diagnostic declaration normalised in ONE place so no
 * scenario can forget it: a scenario that does not declare a paired mode is EXPECTED_POSITIVE.
 */
export const SCENARIOS = Object.freeze(SCENARIO_DEFINITIONS.map((definition) => Object.freeze({
  ...definition,
  paired: definition.paired ?? DEFAULT_PAIRED,
})));

/**
 * An unassigned physical Channel has no measurement at all: no raw word, no engineering value, no
 * Sensor identity and no field-verification claim. This is a property of the POSITION, not of the
 * selected scenario, so it is applied in every scenario, including the default view.
 */
export function unassignedObservation(row, tick) {
  return Object.freeze({
    ...baseIdentity(row, tick),
    rawValue: null,
    engineeringValue: null,
    engineeringUnit: null,
    quality: QUALITY.DISABLED,
    diagnosticReason: DIAGNOSTIC_REASON.NOT_COMMISSIONED,
    isCurrentValueValid: false,
    lastValidValue: null,
    lastValidTimestamp: null,
    valueAgeMs: null,
    nodeCommunicationState: NODE_COMMUNICATION_STATE.ONLINE,
    commissioningState: COMMISSIONING_STATE.BLOCKED,
    blockingReason: 'UNASSIGNED: no Sensor identity exists for this physical Channel',
    fieldVerificationState: FIELD_VERIFICATION_STATE.NOT_APPLICABLE,
    note: 'unassigned physical Channel - not a commissioned spare',
  });
}

export const SCENARIO_KEYS = Object.freeze(SCENARIOS.map((s) => s.key));

export function getScenario(key) {
  assertDeterministicScenario(key);
  const scenario = SCENARIOS.find((s) => s.key === key);
  if (!scenario) throw new Error(`unknown scenario '${key}'`);
  return scenario;
}

/** Scenario catalogue for presentation: keys and honesty text only, no observations. */
export function scenarioCatalogue() {
  return Object.freeze(SCENARIOS.map((s) => Object.freeze({
    key: s.key,
    label: s.label,
    summary: s.summary,
    provenance: s.provenance,
    simulatedDiagnostic: s.provenance === OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO,
    requiresNode: s.requiresNode === true,
    pairedMode: s.paired.mode,
    pairedDiagnostic: s.paired.mode !== PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE,
    pairedNote: s.paired.note,
  })));
}

function baseIdentity(row, tick) {
  return {
    channelKey: row.physicalChannelKey,
    scheduleRow: row.scheduleRow,
    nodeId: row.nodeId,
    moduleInstance: row.moduleInstance,
    channel: row.channel,
    sensorId: row.sensorId,
    side: row.side,
    rowClass: row.rowClass,
    observationTick: tick,
    observationTimestamp: previewTimestamp(tick),
    scenarioSource: 'SYNTHETIC_PREVIEW',
    // Every Product-compatible row of one of the 108 Product Sensors (I7 and I16 included) is selectable.
    selectableAsProductSensor: row.productCompatible && isProductSensorId(row.sensorId),
  };
}

function baseObservation(row, tick, {
  quality, diagnosticReason, uncertaintyNote, stabilisation,
  pairedMode = PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE,
}) {
  const engineeringValue = syntheticEngineeringValue(row, tick, pairedMode);
  return Object.freeze({
    ...baseIdentity(row, tick),
    rawValue: syntheticRawValue(engineeringValue),
    engineeringValue,
    engineeringUnit: 'degC',
    quality,
    diagnosticReason,
    isCurrentValueValid: true,
    lastValidValue: engineeringValue,
    lastValidTimestamp: previewTimestamp(tick),
    valueAgeMs: 1000 + (tick % 4) * 250,
    nodeCommunicationState: NODE_COMMUNICATION_STATE.ONLINE,
    commissioningState: COMMISSIONING_STATE.NOT_TESTED,
    blockingReason: row.passBlockedReason,
    fieldVerificationState: FIELD_VERIFICATION_STATE.FIELD_VERIFICATION_PENDING,
    stabilisation: stabilisation ?? null,
    note: uncertaintyNote ?? 'synthetic preview sample',
  });
}

function invalidObservation(row, tick, {
  quality, diagnosticReason, rawValue, ageMs, commissioningState, blockingReason, nodeCommunicationState, note,
  pairedMode = PAIRED_DIFFERENTIAL_MODE.EXPECTED_POSITIVE,
}) {
  return Object.freeze({
    ...baseIdentity(row, tick),
    rawValue: rawValue === undefined ? null : rawValue,
    engineeringValue: null,
    engineeringUnit: 'degC',
    quality,
    diagnosticReason,
    isCurrentValueValid: false,
    lastValidValue: syntheticEngineeringValue(row, tick, pairedMode),
    lastValidTimestamp: previewTimestamp(Math.max(0, tick - 1)),
    valueAgeMs: ageMs ?? 5000 + (tick % 3) * 500,
    nodeCommunicationState: nodeCommunicationState ?? NODE_COMMUNICATION_STATE.ONLINE,
    commissioningState: commissioningState ?? COMMISSIONING_STATE.NOT_TESTED,
    blockingReason: blockingReason ?? row.passBlockedReason,
    fieldVerificationState: FIELD_VERIFICATION_STATE.FIELD_VERIFICATION_PENDING,
    note: note ?? 'synthetic preview sample',
  });
}

/**
 * A deterministic preview clock. Timestamps are DERIVED from an integer tick count with a fixed
 * preview epoch, so identical inputs give identical timestamps on every machine and every run.
 */
export const PREVIEW_EPOCH_MS = Date.UTC(2026, 0, 1, 0, 0, 0);
export const PREVIEW_TICK_MS = 1000;

export function previewTimestamp(tick) {
  if (!Number.isInteger(tick) || tick < 0) throw new Error('preview tick must be a non-negative integer');
  return new Date(PREVIEW_EPOCH_MS + tick * PREVIEW_TICK_MS).toISOString();
}

/**
 * Builds the observation set for one scenario over the whole schedule.
 * `ctx.nodeId` is required by node-scoped scenarios and ignored elsewhere.
 */
export function buildObservations(schedule, { scenarioKey, tick = 0, nodeId = null }) {
  const scenario = getScenario(scenarioKey);
  if (scenario.requiresNode && !nodeId) {
    throw new Error(`scenario '${scenarioKey}' requires an explicit nodeId (its scope is one node)`);
  }
  const observations = new Map();
  const pairedMode = scenario.paired.mode;
  for (const row of schedule.rows) {
    const applies = scenario.appliesTo(row, { nodeId, pairedMode });
    let raw;
    if (row.rowClass === ROW_CLASS.UNASSIGNED) {
      // No measurement exists at an unassigned position, whatever scenario is selected.
      raw = unassignedObservation(row, tick);
    } else if (applies) {
      raw = scenario.observation(row, tick, { nodeId, pairedMode });
    } else {
      raw = baseObservation(row, tick, {
        quality: QUALITY.GOOD, diagnosticReason: DIAGNOSTIC_REASON.NONE, pairedMode,
      });
    }
    // Only the rows the scenario actually covers carry the scenario's provenance. A row outside the
    // scenario condition is an ordinary synthetic preview sample, so a simulated diagnostic label
    // can never bleed onto a healthy row (and vice versa).
    const provenance = applies ? scenario.provenance : OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW;
    const nodeState = applies && scenario.nodeState ? scenario.nodeState : raw.nodeCommunicationState;
    // The scenario identity and its declared paired-differential mode travel with EVERY observation of
    // the set, so the paired model derives its classification from the explicit scenario (never from a
    // threshold) and can refuse a pair whose two sides came from different observation sets.
    observations.set(row.physicalChannelKey, Object.freeze({
      ...raw, provenance, nodeCommunicationState: nodeState,
      scenarioKey: scenario.key,
      pairedDifferentialMode: pairedMode,
    }));
  }
  return observations;
}
