// Stage 0.4C-K1 - Quality, diagnostic and invalid-value presentation tests (Stage task section 16,
// group C, and section 12 safety presentation).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  COMMISSIONING_STATE, CURRENT_VALUE_UNAVAILABLE_TEXT, DIAGNOSTIC_REASON, DIAGNOSTIC_REASON_VALUES,
  FIELD_VERIFICATION_STATE, HISTORICAL_LABEL, OBSERVATION_PROVENANCE, PREVIEW_STALE_THRESHOLD_MS,
  QUALITY, QUALITY_VALUES, RECORDABLE_VERDICT, SIMULATED_DIAGNOSTIC_LABEL, diagnosticAllowedForQuality,
  formatEngineeringValue, isCurrentValueValid, isInvalidPresentation, presentCurrentValue, presentDiagnosticReason,
  presentLastValidValue, presentMeasurement, presentQuality, presentRawValue, presentValueAge, requireQualityContract,
  requireObservationContract,
} from '../src/index.mjs';
import { buildObservations, firstScenarioChannel, scenarioObservationOf } from './helpers/scenarios.mjs';

const base = (over = {}) => ({
  channelKey: 'TC-NODE-01|A102.1|1',
  nodeId: 'TC-NODE-01',
  moduleInstance: 'A102.1',
  channel: 1,
  quality: QUALITY.GOOD,
  diagnosticReason: DIAGNOSTIC_REASON.NONE,
  isCurrentValueValid: true,
  engineeringUnit: 'degC',
  engineeringValue: 42.5,
  rawValue: 425,
  lastValidValue: 42.5,
  lastValidTimestamp: '2026-01-01T00:00:00.000Z',
  observationTick: 0,
  observationTimestamp: '2026-01-01T00:00:01.000Z',
  valueAgeMs: 1000,
  scenarioSource: 'SYNTHETIC_PREVIEW',
  provenance: OBSERVATION_PROVENANCE.SYNTHETIC_PREVIEW,
  fieldVerificationState: FIELD_VERIFICATION_STATE.FIELD_VERIFICATION_PENDING,
  ...over,
});

test('Quality stays the Product contract vocabulary and never gains INVALID', () => {
  assert.deepEqual(QUALITY_VALUES, ['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED']);
  assert.equal(QUALITY_VALUES.includes('INVALID'), false);
  assert.equal(requireQualityContract(), true);
});

test('DiagnosticReason and CommissioningState are separate vocabularies', () => {
  assert.deepEqual(DIAGNOSTIC_REASON_VALUES, [
    'NONE', 'WIRE_BREAK', 'SUSPECTED_WIRE_BREAK', 'OUT_OF_RANGE', 'MODULE_FAULT',
    'COMMUNICATION_LOSS', 'NOT_MAPPED', 'NOT_COMMISSIONED', 'CONFIGURATION_INVALID',
  ]);
  for (const value of Object.values(COMMISSIONING_STATE)) {
    assert.equal(QUALITY_VALUES.includes(value), false, value);
    assert.equal(DIAGNOSTIC_REASON_VALUES.includes(value), false, value);
  }
  assert.deepEqual(Object.values(RECORDABLE_VERDICT), ['PASS', 'FAIL', 'PARTIAL']);
  assert.equal(Object.values(RECORDABLE_VERDICT).includes('BLOCKED'), false);
});

test('a BAD current value never presents a numeric value, a unit or a zero', () => {
  const observation = base({
    quality: QUALITY.BAD,
    diagnosticReason: DIAGNOSTIC_REASON.OUT_OF_RANGE,
    isCurrentValueValid: false,
    engineeringValue: null,
  });
  const current = presentCurrentValue(observation);
  assert.equal(current.valid, false);
  assert.equal(current.numeric, null);
  assert.equal(current.text, CURRENT_VALUE_UNAVAILABLE_TEXT);
  assert.equal(current.unit, null);
  assert.equal(/[0-9]/.test(current.text), false);
  assert.equal(current.cause, DIAGNOSTIC_REASON.OUT_OF_RANGE);
});

test('a valid flag with no engineering value is refused rather than rendered blank or zero', () => {
  assert.throws(
    () => presentCurrentValue(base({ engineeringValue: null })),
    /must carry an engineering value/,
  );
});

test('presentMeasurement refuses an invalid observation that still carries an engineering value', () => {
  const observation = base({
    quality: QUALITY.BAD,
    diagnosticReason: DIAGNOSTIC_REASON.COMMUNICATION_LOSS,
    isCurrentValueValid: false,
    engineeringValue: 0,
  });
  assert.throws(() => presentMeasurement(observation), /must not carry an engineering value/);
  assert.throws(() => requireObservationContract(observation), /must not carry an engineering value/);
});

test('STALE is distinct from communication loss and from a Channel fault', () => {
  const stale = base({
    quality: QUALITY.STALE,
    diagnosticReason: DIAGNOSTIC_REASON.MODULE_FAULT,
    isCurrentValueValid: false,
    engineeringValue: null,
    valueAgeMs: PREVIEW_STALE_THRESHOLD_MS + 1,
  });
  const loss = base({
    quality: QUALITY.BAD,
    diagnosticReason: DIAGNOSTIC_REASON.COMMUNICATION_LOSS,
    isCurrentValueValid: false,
    engineeringValue: null,
    nodeCommunicationState: 'COMMUNICATION_LOSS',
  });
  const staleAge = presentValueAge(stale);
  assert.equal(staleAge.exceedsPreviewStaleThreshold, true);
  assert.match(staleAge.note, /exceeds the Preview staleness threshold/);
  assert.equal(presentValueAge(base()).exceedsPreviewStaleThreshold, false);
  assert.notEqual(presentMeasurement(stale).quality.text, presentMeasurement(loss).quality.text);
  assert.notEqual(stale.diagnosticReason, loss.diagnosticReason);
  assert.equal(presentQuality(QUALITY.STALE).marker, '[~]');
  assert.notEqual(presentQuality(QUALITY.STALE).marker, presentQuality(QUALITY.BAD).marker);
});

test('out of range is distinct from wire break and both carry their own cause text', () => {
  assert.notEqual(DIAGNOSTIC_REASON.OUT_OF_RANGE, DIAGNOSTIC_REASON.WIRE_BREAK);
  assert.match(presentDiagnosticReason(DIAGNOSTIC_REASON.OUT_OF_RANGE).text, /Out of range/);
  assert.match(presentDiagnosticReason(DIAGNOSTIC_REASON.WIRE_BREAK).text, /Wire break/);
  assert.equal(presentDiagnosticReason(DIAGNOSTIC_REASON.SUSPECTED_WIRE_BREAK).text.includes('not confirmed'), true);
});

test('the last valid value is labelled historical and is never presented as current', () => {
  const observation = base({
    quality: QUALITY.STALE,
    diagnosticReason: DIAGNOSTIC_REASON.MODULE_FAULT,
    isCurrentValueValid: false,
    engineeringValue: null,
  });
  const lastValid = presentLastValidValue(observation);
  assert.equal(lastValid.kind, 'HISTORICAL');
  assert.equal(lastValid.isCurrent, false);
  assert.equal(lastValid.label, HISTORICAL_LABEL);
  assert.equal(lastValid.timestamp, observation.lastValidTimestamp);
  assert.match(lastValid.label, /not the current value/);
  const none = presentLastValidValue(base({ lastValidValue: null }));
  assert.equal(none.kind, 'NONE');
  assert.equal(none.isCurrent, false);
});

test('a simulated wire break is labelled as a simulation everywhere it is presented', () => {
  const observation = base({
    quality: QUALITY.BAD,
    diagnosticReason: DIAGNOSTIC_REASON.WIRE_BREAK,
    isCurrentValueValid: false,
    engineeringValue: null,
    rawValue: 0x7FFF,
    provenance: OBSERVATION_PROVENANCE.SIMULATED_DIAGNOSTIC_SCENARIO,
  });
  const presented = presentMeasurement(observation);
  assert.equal(presented.simulatedDiagnostic, true);
  assert.equal(presented.simulatedDiagnosticLabel, SIMULATED_DIAGNOSTIC_LABEL);
  assert.match(presented.simulatedDiagnosticLabel, /NOT HARDWARE DETECTED/);
  assert.equal(presented.current.valid, false);
  // The raw word is shown as evidence only, next to a verdict that is explicitly simulated.
  assert.match(presentRawValue(observation).text, /0x7FFF/);
});

test('WIRE_BREAK is not allowed with a GOOD quality, so a healthy sample cannot claim a wire break', () => {
  assert.equal(diagnosticAllowedForQuality(QUALITY.GOOD, DIAGNOSTIC_REASON.WIRE_BREAK), false);
  assert.equal(diagnosticAllowedForQuality(QUALITY.GOOD, DIAGNOSTIC_REASON.NONE), true);
  assert.equal(isCurrentValueValid({ quality: QUALITY.GOOD, diagnosticReason: DIAGNOSTIC_REASON.WIRE_BREAK }), true,
    'the validity rule follows Quality only; the combination itself is refused below');
  assert.throws(() => presentMeasurement(base({ diagnosticReason: DIAGNOSTIC_REASON.WIRE_BREAK })), /WIRE_BREAK/);
});

test('UNCERTAIN keeps a marked current value and produces no control effect', () => {
  const observation = base({ quality: QUALITY.UNCERTAIN, diagnosticReason: DIAGNOSTIC_REASON.SUSPECTED_WIRE_BREAK });
  assert.equal(isCurrentValueValid(observation), true);
  const presented = presentMeasurement(observation);
  assert.equal(presented.current.valid, true);
  assert.equal(presented.quality.text, 'UNCERTAIN');
  assert.equal(presented.quality.marker, '[?]');
  // No control-affecting field exists on an observation: a synthetic sample cannot command anything.
  for (const forbidden of ['command', 'queueState', 'controlState', 'writeAuthority', 'deviceCommand']) {
    assert.equal(forbidden in observation, false);
  }
});

test('the INVALID presentation concept applies to BAD and STALE only, never to DISABLED', () => {
  assert.equal(isInvalidPresentation({ quality: QUALITY.BAD, diagnosticReason: DIAGNOSTIC_REASON.OUT_OF_RANGE }), true);
  assert.equal(isInvalidPresentation({ quality: QUALITY.STALE, diagnosticReason: DIAGNOSTIC_REASON.MODULE_FAULT }), true);
  assert.equal(isInvalidPresentation({ quality: QUALITY.DISABLED, diagnosticReason: DIAGNOSTIC_REASON.NOT_COMMISSIONED }), false);
  assert.equal(isInvalidPresentation({ quality: QUALITY.GOOD, diagnosticReason: DIAGNOSTIC_REASON.NONE }), false);
});

test('a not-commissioned Channel is presented as DISABLED with a cause, not as BAD', () => {
  const observation = base({
    quality: QUALITY.DISABLED,
    diagnosticReason: DIAGNOSTIC_REASON.NOT_COMMISSIONED,
    isCurrentValueValid: false,
    engineeringValue: null,
    lastValidValue: null,
  });
  const presented = presentMeasurement(observation);
  assert.equal(presented.quality.text, 'DISABLED');
  assert.notEqual(presented.quality.text, QUALITY.BAD);
  assert.equal(presented.diagnostic.text, 'Not commissioned');
  assert.equal(presented.invalidPresentation, false);
  assert.equal(presented.current.valid, false);
});

test('the observation contract refuses an unknown Quality or DiagnosticReason', () => {
  assert.throws(() => requireObservationContract(base({ quality: 'INVALID' })), /not a Product Quality/);
  assert.throws(() => requireObservationContract(base({ diagnosticReason: 'PANIC' })), /not in the Preview model/);
  const { nodeId, ...withoutNode } = base();
  assert.equal(nodeId, 'TC-NODE-01');
  assert.throws(() => requireObservationContract(withoutNode), /missing 'nodeId'/);
});

test('engineering values are formatted deterministically and refuse non-finite input', () => {
  assert.equal(formatEngineeringValue(20), '20.0');
  assert.equal(formatEngineeringValue(-199.95), '-199.9');
  assert.throws(() => formatEngineeringValue(Number.NaN), /finite number/);
  assert.throws(() => formatEngineeringValue('20'), /finite number/);
});

test('every scenario observation satisfies the presentation rules it is rendered through', () => {
  const observations = buildObservations('SIMULATED_WIRE_BREAK');
  const channel = firstScenarioChannel(observations, (o) => o.diagnosticReason === DIAGNOSTIC_REASON.WIRE_BREAK);
  const presented = presentMeasurement(observations.get(channel));
  assert.equal(presented.simulatedDiagnostic, true);
  assert.equal(presented.current.valid, false);
  assert.equal(scenarioObservationOf(observations, channel).engineeringValue, null);
});
