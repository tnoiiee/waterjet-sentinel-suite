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

import { ROW_CLASS, isNonSensorGapSensorId } from './schedule.mjs';
import {
  COMMISSIONING_STATE, DIAGNOSTIC_REASON, FIELD_VERIFICATION_STATE, NODE_COMMUNICATION_STATE,
  OBSERVATION_PROVENANCE, PREVIEW_STALE_THRESHOLD_MS, QUALITY,
} from './quality.mjs';

/** Deterministic Type-K-plausible temperature: a function of the physical identity only. */
export function syntheticEngineeringValue(row) {
  const channel = (row.channel - 1) % 8;
  const moduleIndex = Number.parseInt(row.moduleInstance.split('.')[1] ?? '1', 10) - 1;
  const nodeIndex = Number.parseInt(row.nodeId.split('-')[2] ?? '1', 10) - 1;
  const sideBias = row.side === 'F' ? 0 : 4;
  const base = 20 + nodeIndex * 3 + moduleIndex * 1.5 + channel * 2 + sideBias;
  const fraction = ((row.scheduleRow * 7) % 10) / 10;
  return Math.round((base + fraction) * 10) / 10;
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
export const SCENARIOS = Object.freeze([
  Object.freeze({
    key: 'NORMAL_GOOD',
    label: 'NORMAL_GOOD - plausible Type K sample',
    summary: 'Quality GOOD, current value valid, no diagnostic.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: () => true,
    observation: (row, tick) => baseObservation(row, tick, {
      quality: QUALITY.GOOD,
      diagnosticReason: DIAGNOSTIC_REASON.NONE,
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
  Object.freeze({
    key: 'PRODUCT_TOPOLOGY_CONFLICT',
    label: 'PRODUCT_TOPOLOGY_CONFLICT - I7 / I16 evidence',
    summary: 'Shows the I7 / I16 Product-topology conflict evidence. These rows are not Product Sensors, no commissioning PASS is permitted, and they are excluded from Product Runtime Sensor selection.',
    provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
    appliesTo: (row) => row.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT,
    observation: (row, tick) => invalidObservation(row, tick, {
      quality: QUALITY.BAD,
      diagnosticReason: DIAGNOSTIC_REASON.NOT_MAPPED,
      rawValue: null,
      commissioningState: COMMISSIONING_STATE.BLOCKED,
      blockingReason: 'PRODUCT_TOPOLOGY_CONFLICT: drawing assigns a live channel to an authoritative NON_SENSOR_GAP position',
      note: 'NON_SENSOR_GAP position - never a Sensor, never a Water Jet',
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
]);

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
    // The I7 / I16 conflict rows are never selectable as a Product Runtime Sensor.
    selectableAsProductSensor: row.productCompatible && !isNonSensorGapSensorId(row.sensorId),
  };
}

function baseObservation(row, tick, { quality, diagnosticReason, uncertaintyNote, stabilisation }) {
  const engineeringValue = syntheticEngineeringValue(row);
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

function invalidObservation(row, tick, { quality, diagnosticReason, rawValue, ageMs, commissioningState, blockingReason, nodeCommunicationState, note }) {
  return Object.freeze({
    ...baseIdentity(row, tick),
    rawValue: rawValue === undefined ? null : rawValue,
    engineeringValue: null,
    engineeringUnit: 'degC',
    quality,
    diagnosticReason,
    isCurrentValueValid: false,
    lastValidValue: syntheticEngineeringValue(row),
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
  for (const row of schedule.rows) {
    const applies = scenario.appliesTo(row, { nodeId });
    let raw;
    if (row.rowClass === ROW_CLASS.UNASSIGNED) {
      // No measurement exists at an unassigned position, whatever scenario is selected.
      raw = unassignedObservation(row, tick);
    } else if (applies) {
      raw = scenario.observation(row, tick);
    } else {
      raw = baseObservation(row, tick, { quality: QUALITY.GOOD, diagnosticReason: DIAGNOSTIC_REASON.NONE });
    }
    // Only the rows the scenario actually covers carry the scenario's provenance. A row outside the
    // scenario condition is an ordinary synthetic preview sample, so a simulated diagnostic label
    // can never bleed onto a healthy row (and vice versa).
    const provenance = applies ? scenario.provenance : OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW;
    const nodeState = applies && scenario.nodeState ? scenario.nodeState : raw.nodeCommunicationState;
    observations.set(row.physicalChannelKey, Object.freeze({
      ...raw, provenance, nodeCommunicationState: nodeState,
    }));
  }
  return observations;
}
