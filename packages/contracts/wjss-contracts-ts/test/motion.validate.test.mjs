// Motion commissioning foundation - structural contract of the read-only Snapshot section.
//
// The C# records in Wjss.Contracts are authoritative; this mirror proves the JavaScript validator
// accepts exactly the state this software revision can publish (gate CLOSED, nothing configured,
// nothing connected, nothing homed, nothing authorized) and refuses every claim that would imply
// device access, commissioning, a unit conversion or an executable motion parameter.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validMotionCommissioning, validateEnvelope } from '../validate.mjs';
import { canonicalMotionProjection } from './helpers/motionProjection.mjs';

test('the canonical closed-gate projection is accepted', () => {
  assert.equal(validMotionCommissioning(canonicalMotionProjection()), true);
});

test('null, non-objects and arrays are refused', () => {
  for (const value of [null, undefined, 'CLOSED', 1, [], {}]) {
    assert.equal(validMotionCommissioning(value), false, `refused: ${String(value)}`);
  }
});

test('a projection that claims an open gate, a ready controller or a configured endpoint is refused', () => {
  const openGate = canonicalMotionProjection();
  openGate.authorizationGateState = 'OPEN';
  assert.equal(validMotionCommissioning(openGate), false);

  const readyController = canonicalMotionProjection();
  readyController.controllers[0].configuration.motionDispatchReady = true;
  assert.equal(validMotionCommissioning(readyController), false);

  const configuredEndpoint = canonicalMotionProjection();
  configuredEndpoint.controllers[0].configuration.endpointIdentity = 'CONFIGURED';
  assert.equal(validMotionCommissioning(configuredEndpoint), false);

  const connected = canonicalMotionProjection();
  connected.controllers[1].configuration.communicationState = 'CONNECTED';
  assert.equal(validMotionCommissioning(connected), false);

  const unknownGateReason = canonicalMotionProjection();
  unknownGateReason.controllers[0].configuration.motionDispatchBlockedReason = 'NONE';
  assert.equal(validMotionCommissioning(unknownGateReason), false);
});

test('a homed axis, a known brake state or an authorized move is refused', () => {
  const homed = canonicalMotionProjection();
  homed.axes[0].state.homingState = 'HOMED';
  assert.equal(validMotionCommissioning(homed), false);

  const brakeKnown = canonicalMotionProjection();
  brakeKnown.axes[3].state.brakeState = 'RELEASED';
  assert.equal(validMotionCommissioning(brakeKnown), false);

  const moveAuthorized = canonicalMotionProjection();
  moveAuthorized.axes[5].state.moveAuthorization.moveAuthorizationState = 'AUTHORIZED';
  assert.equal(validMotionCommissioning(moveAuthorized), false);

  const droppedReason = canonicalMotionProjection();
  droppedReason.axes[2].state.moveAuthorization.moveRefusalReasons = ['AXIS_NOT_HOMED'];
  assert.equal(validMotionCommissioning(droppedReason), false);

  const inventedPosition = canonicalMotionProjection();
  inventedPosition.axes[0].state.sense.positionDisplayValue = '0';
  assert.equal(validMotionCommissioning(inventedPosition), false);
});

test('a confirmed limit polarity or an available signal state is refused', () => {
  const activeHigh = canonicalMotionProjection();
  activeHigh.axes[0].state.limitSignals[0].polarityStatus = 'ACTIVE_HIGH';
  assert.equal(validMotionCommissioning(activeHigh), false);

  const signalRead = canonicalMotionProjection();
  signalRead.axes[0].state.limitSignals[2].signalState = 'ACTIVE';
  assert.equal(validMotionCommissioning(signalRead), false);

  const missingSignal = canonicalMotionProjection();
  missingSignal.axes[0].state.limitSignals = missingSignal.axes[0].state.limitSignals.slice(0, 2);
  assert.equal(validMotionCommissioning(missingSignal), false);
});

test('the Owner-confirmed allocation is required exactly, and the conflict cannot be dropped', () => {
  const swappedAxes = canonicalMotionProjection();
  swappedAxes.waterJets[1].horizontalAxisId = 'A';
  assert.equal(validMotionCommissioning(swappedAxes), false);

  const wrongBrake = canonicalMotionProjection();
  wrongBrake.axes[1].identity.brakeOutputIdentity = 'OUTPUT_3';
  assert.equal(validMotionCommissioning(wrongBrake), false);

  const wrongRole = canonicalMotionProjection();
  wrongRole.axes[1].identity.axisRole = 'HORIZONTAL';
  assert.equal(validMotionCommissioning(wrongRole), false);

  const droppedConflict = canonicalMotionProjection();
  droppedConflict.mappingConflicts = [];
  assert.equal(validMotionCommissioning(droppedConflict), false);

  const resolvedConflict = canonicalMotionProjection();
  resolvedConflict.mappingConflicts[0].status = 'RESOLVED';
  assert.equal(validMotionCommissioning(resolvedConflict), false);
});

test('the Jog Speed is a display value in controller units and is never executable', () => {
  const executable = canonicalMotionProjection();
  executable.jogSpeed.executable = true;
  assert.equal(validMotionCommissioning(executable), false);

  const millimetres = canonicalMotionProjection();
  millimetres.jogSpeed.unit = 'mm/s';
  assert.equal(validMotionCommissioning(millimetres), false);

  const commissioned = canonicalMotionProjection();
  commissioned.jogSpeed.status = 'COMMISSIONED';
  assert.equal(validMotionCommissioning(commissioned), false);
});

test('issued commands or a persisted command record are refused', () => {
  const issued = canonicalMotionProjection();
  issued.commandTotals.hardwareCommandsIssued = 1;
  assert.equal(validMotionCommissioning(issued), false);

  const persisted = canonicalMotionProjection();
  persisted.commandTotals.journalPersistence = 'PERSISTED';
  assert.equal(validMotionCommissioning(persisted), false);

  const missingClaim = canonicalMotionProjection();
  missingClaim.notVerifiedClaims = ['NO_AXIS_HOMED'];
  assert.equal(validMotionCommissioning(missingClaim), false);
});

test('a bad fingerprint, an empty legend or an empty canonical text is refused', () => {
  const badFingerprint = canonicalMotionProjection();
  badFingerprint.fingerprint = 'ABCDEF';
  assert.equal(validMotionCommissioning(badFingerprint), false);

  const emptyLegend = canonicalMotionProjection();
  emptyLegend.statusLegend = [];
  assert.equal(validMotionCommissioning(emptyLegend), false);

  const emptyText = canonicalMotionProjection();
  emptyText.canonicalText = '';
  assert.equal(validMotionCommissioning(emptyText), false);
});

test('the motion section is never carried by a Delta', () => {
  const delta = {
    kind: 'delta',
    schema: 'wjss.delta/2',
    apiVersion: 1,
    previousRevision: 1,
    revision: 2,
    generatedAt: '2026-10-07T00:00:00Z',
    motion: canonicalMotionProjection(),
  };
  const result = validateEnvelope(delta);
  assert.equal(result.kind, 'delta');
  assert.ok(result.issues.some((issue) => issue.includes('motion must never appear in a Delta')), result.issues.join('; '));

  // A null motion is a legitimate Snapshot encoding (no motion state composed): it is not an error.
  const cleared = { ...delta };
  delete cleared.motion;
  assert.equal(validateEnvelope(cleared).issues.length, 0);
});
