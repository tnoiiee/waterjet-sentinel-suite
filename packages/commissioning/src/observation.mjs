// Stage 0.4C-K1 - the synthetic observation contract and its summaries.
//
// Every observation in this Stage is synthetic. This module never reads a device, never opens a
// socket and never writes anything: it validates the shape of an observation, summarises a set of
// them, and derives per-node communication and commissioning-progress presentation.

import {
  COMMISSIONING_STATE, DIAGNOSTIC_REASON, DIAGNOSTIC_REASON_VALUES, NODE_COMMUNICATION_STATE,
  OBSERVATION_PROVENANCE, QUALITY, QUALITY_VALUES,
} from './quality.mjs';

export const REQUIRED_OBSERVATION_FIELDS = Object.freeze([
  'channelKey', 'nodeId', 'moduleInstance', 'channel', 'quality', 'diagnosticReason',
  'isCurrentValueValid', 'engineeringUnit', 'observationTick', 'observationTimestamp',
  'scenarioSource', 'fieldVerificationState',
]);

/** Structural check used by tests and by the server before it serves an observation set. */
export function requireObservationContract(observation) {
  for (const field of REQUIRED_OBSERVATION_FIELDS) {
    if (!(field in observation)) throw new Error(`observation is missing '${field}'`);
  }
  if (!QUALITY_VALUES.includes(observation.quality)) throw new Error(`observation Quality '${observation.quality}' is not a Product Quality`);
  if (!DIAGNOSTIC_REASON_VALUES.includes(observation.diagnosticReason)) {
    throw new Error(`observation DiagnosticReason '${observation.diagnosticReason}' is not in the Preview model`);
  }
  if (observation.isCurrentValueValid === false && observation.engineeringValue !== null && observation.engineeringValue !== undefined) {
    throw new Error('an observation with no valid current value must not carry an engineering value');
  }
  if (observation.engineeringValue !== null && observation.engineeringValue !== undefined && observation.isCurrentValueValid !== true) {
    throw new Error('an observation carrying an engineering value must be marked current-value valid');
  }
  if (!Object.values(OBSERVATION_PROVENANCE).includes(observation.provenance ?? OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW)) {
    throw new Error('observation provenance must be a Preview provenance');
  }
  return true;
}

/** Counts by Quality, by DiagnosticReason, and the two safety-relevant totals. */
export function summariseObservations(observations) {
  const byQuality = Object.fromEntries(QUALITY_VALUES.map((q) => [q, 0]));
  const byDiagnosticReason = Object.fromEntries(DIAGNOSTIC_REASON_VALUES.map((d) => [d, 0]));
  let currentValueValid = 0;
  let currentValueUnavailable = 0;
  let simulatedDiagnostic = 0;
  for (const observation of observations.values()) {
    requireObservationContract(observation);
    byQuality[observation.quality] += 1;
    byDiagnosticReason[observation.diagnosticReason] += 1;
    if (observation.isCurrentValueValid === true) currentValueValid += 1; else currentValueUnavailable += 1;
    if (observation.provenance === OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO) simulatedDiagnostic += 1;
  }
  return Object.freeze({
    total: observations.size,
    byQuality: Object.freeze(byQuality),
    byDiagnosticReason: Object.freeze(byDiagnosticReason),
    currentValueValid,
    currentValueUnavailable,
    simulatedDiagnostic,
    good: byQuality[QUALITY.GOOD],
    uncertain: byQuality[QUALITY.UNCERTAIN],
    bad: byQuality[QUALITY.BAD],
    stale: byQuality[QUALITY.STALE],
    disabled: byQuality[QUALITY.DISABLED],
    communicationLoss: byDiagnosticReason[DIAGNOSTIC_REASON.COMMUNICATION_LOSS],
    wireBreakSimulated: byDiagnosticReason[DIAGNOSTIC_REASON.WIRE_BREAK],
    outOfRange: byDiagnosticReason[DIAGNOSTIC_REASON.OUT_OF_RANGE],
  });
}

/**
 * Per-node communication summary. A node's state is COMMUNICATION_LOSS only when every one of its
 * Channels reports loss; a partial state is reported as RECOVERING so a single node can never be
 * presented as fully down while it still answers on some Channels.
 */
export function nodeCommunicationSummary(schedule, observations) {
  const nodes = new Map();
  for (const row of schedule.rows) {
    if (!nodes.has(row.nodeId)) {
      nodes.set(row.nodeId, { nodeId: row.nodeId, cabinet: row.cabinet, total: 0, loss: 0, channels: [] });
    }
    const entry = nodes.get(row.nodeId);
    const observation = observations.get(row.physicalChannelKey);
    entry.total += 1;
    if (observation?.nodeCommunicationState === NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS) entry.loss += 1;
    entry.channels.push(row.channel);
  }
  return Object.freeze([...nodes.values()].map((n) => {
    const state = n.loss === 0
      ? NODE_COMMUNICATION_STATE.ONLINE
      : (n.loss === n.total ? NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS : NODE_COMMUNICATION_STATE.RECOVERING);
    return Object.freeze({
      nodeId: n.nodeId,
      cabinet: n.cabinet,
      physicalChannels: n.total,
      channelsInCommunicationLoss: n.loss,
      state,
      affectedModules: Object.freeze([...new Set(schedule.rows
        .filter((r) => r.nodeId === n.nodeId
          && observations.get(r.physicalChannelKey)?.nodeCommunicationState === NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS)
        .map((r) => r.moduleInstance))]),
    });
  }));
}

/** True when exactly one node is affected and the other five remain available. */
export function communicationLossIsNodeScoped(nodeSummary, expectedNodeId) {
  const affected = nodeSummary.filter((n) => n.state === NODE_COMMUNICATION_STATE.COMMUNICATION_LOSS);
  if (affected.length !== 1) return false;
  if (affected[0].nodeId !== expectedNodeId) return false;
  return nodeSummary.filter((n) => n.nodeId !== expectedNodeId)
    .every((n) => n.state === NODE_COMMUNICATION_STATE.ONLINE && n.channelsInCommunicationLoss === 0);
}

/**
 * Commissioning progress over the whole schedule: how many rows can never receive a PASS, how many
 * are still untested, and the per-state totals. BLOCKED is derived from the evidence, never from a
 * workflow action, so a filter or a verdict cannot change it.
 */
export function commissioningProgress(schedule, sessionState = null) {
  const byState = Object.fromEntries(Object.values(COMMISSIONING_STATE).map((s) => [s, 0]));
  for (const row of schedule.rows) {
    const state = sessionState ? sessionState.stateOf(row) : null;
    if (state) {
      byState[state] += 1;
    } else if (row.passBlockedReason) {
      byState[COMMISSIONING_STATE.BLOCKED] += 1;
    } else {
      byState[COMMISSIONING_STATE.NOT_TESTED] += 1;
    }
  }
  return Object.freeze({
    total: schedule.rows.length,
    byState: Object.freeze(byState),
    eligibleForPass: schedule.rows.filter((r) => !r.passBlockedReason).length,
    blockedFromPass: schedule.rows.filter((r) => r.passBlockedReason).length,
  });
}
