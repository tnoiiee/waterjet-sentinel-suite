// Motion commissioning foundation - structural contract of the STATIC read-only catalogue.
//
// The C# records in Wjss.Contracts are authoritative; this mirror proves the JavaScript validator
// accepts exactly the state this software revision can publish (gate CLOSED, nothing configured,
// nothing connected, nothing homed, nothing authorized) and refuses every claim that would imply
// device access, commissioning, a unit conversion or an executable motion parameter.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { validMotionCommissioningCatalogue, validateEnvelope } from '../validate.mjs';
import { canonicalMotionCatalogue } from './helpers/motionCatalogue.mjs';

test('the canonical closed-gate projection is accepted', () => {
  assert.equal(validMotionCommissioningCatalogue(canonicalMotionCatalogue()), true);
});

test('null, non-objects and arrays are refused', () => {
  for (const value of [null, undefined, 'CLOSED', 1, [], {}]) {
    assert.equal(validMotionCommissioningCatalogue(value), false, `refused: ${String(value)}`);
  }
});

test('a projection that claims an open gate, a ready controller or a configured endpoint is refused', () => {
  const openGate = canonicalMotionCatalogue();
  openGate.authorizationGateState = 'OPEN';
  assert.equal(validMotionCommissioningCatalogue(openGate), false);

  const readyController = canonicalMotionCatalogue();
  readyController.controllers[0].configuration.motionDispatchReady = true;
  assert.equal(validMotionCommissioningCatalogue(readyController), false);

  const configuredEndpoint = canonicalMotionCatalogue();
  configuredEndpoint.controllers[0].configuration.endpointIdentity = 'CONFIGURED';
  assert.equal(validMotionCommissioningCatalogue(configuredEndpoint), false);

  const connected = canonicalMotionCatalogue();
  connected.controllers[1].configuration.communicationState = 'CONNECTED';
  assert.equal(validMotionCommissioningCatalogue(connected), false);

  const unknownGateReason = canonicalMotionCatalogue();
  unknownGateReason.controllers[0].configuration.motionDispatchBlockedReason = 'NONE';
  assert.equal(validMotionCommissioningCatalogue(unknownGateReason), false);
});

test('a homed axis, a known brake state or an authorized move is refused', () => {
  const homed = canonicalMotionCatalogue();
  homed.axes[0].state.homingState = 'HOMED';
  assert.equal(validMotionCommissioningCatalogue(homed), false);

  const brakeKnown = canonicalMotionCatalogue();
  brakeKnown.axes[3].state.brakeState = 'RELEASED';
  assert.equal(validMotionCommissioningCatalogue(brakeKnown), false);

  const moveAuthorized = canonicalMotionCatalogue();
  moveAuthorized.axes[5].state.moveAuthorization.moveAuthorizationState = 'AUTHORIZED';
  assert.equal(validMotionCommissioningCatalogue(moveAuthorized), false);

  const droppedReason = canonicalMotionCatalogue();
  droppedReason.axes[2].state.moveAuthorization.moveRefusalReasons = ['AXIS_NOT_HOMED'];
  assert.equal(validMotionCommissioningCatalogue(droppedReason), false);

  const inventedPosition = canonicalMotionCatalogue();
  inventedPosition.axes[0].state.sense.positionDisplayValue = '0';
  assert.equal(validMotionCommissioningCatalogue(inventedPosition), false);
});

test('a confirmed limit polarity or an available signal state is refused', () => {
  const activeHigh = canonicalMotionCatalogue();
  activeHigh.axes[0].state.limitSignals[0].polarityStatus = 'ACTIVE_HIGH';
  assert.equal(validMotionCommissioningCatalogue(activeHigh), false);

  const signalRead = canonicalMotionCatalogue();
  signalRead.axes[0].state.limitSignals[2].signalState = 'ACTIVE';
  assert.equal(validMotionCommissioningCatalogue(signalRead), false);

  const missingSignal = canonicalMotionCatalogue();
  missingSignal.axes[0].state.limitSignals = missingSignal.axes[0].state.limitSignals.slice(0, 2);
  assert.equal(validMotionCommissioningCatalogue(missingSignal), false);
});

test('the Owner-confirmed allocation is required exactly, and the conflict cannot be dropped', () => {
  // The revoked REV2 axis for WJ2 (horizontal A) is refused under the current
  // adopted allocation (WJ2 horizontal C on WSB1), as is the revoked REV2 cabinet for WJ1
  // (WSB2, the cabinet WJ1 held under REV2).
  const swappedAxes = canonicalMotionCatalogue();
  swappedAxes.waterJets[1].horizontalAxisId = 'A';
  assert.equal(validMotionCommissioningCatalogue(swappedAxes), false);

  const revokedCabinet = canonicalMotionCatalogue();
  revokedCabinet.waterJets[0].cabinetId = 'WSB2';
  revokedCabinet.waterJets[0].controllerId = 'MC-WSB2';
  assert.equal(validMotionCommissioningCatalogue(revokedCabinet), false);

  // REV2 and REV1 remain historical identity only: a catalogue publishing either as the
  // current topology revision is refused.
  const historicalRevisionRev2 = canonicalMotionCatalogue();
  historicalRevisionRev2.motionTopologyRevision = 'OWNER_CONFIRMED_REV2';
  assert.equal(validMotionCommissioningCatalogue(historicalRevisionRev2), false);

  const historicalRevision = canonicalMotionCatalogue();
  historicalRevision.motionTopologyRevision = 'OWNER_CONFIRMED_REV1';
  assert.equal(validMotionCommissioningCatalogue(historicalRevision), false);

  const wrongBrake = canonicalMotionCatalogue();
  wrongBrake.axes[1].identity.brakeOutputIdentity = 'OUTPUT_3';
  assert.equal(validMotionCommissioningCatalogue(wrongBrake), false);

  const wrongRole = canonicalMotionCatalogue();
  wrongRole.axes[1].identity.axisRole = 'HORIZONTAL';
  assert.equal(validMotionCommissioningCatalogue(wrongRole), false);

  const droppedNote = canonicalMotionCatalogue();
  droppedNote.mappingEvidenceNotes = [];
  assert.equal(validMotionCommissioningCatalogue(droppedNote), false);

  // The discrepancy is recorded, not resolved: a note that claims a physical resolution is a claim
  // no one has made, and one that drops the classification turns evidence into canonical ambiguity.
  const resolvedNote = canonicalMotionCatalogue();
  resolvedNote.mappingEvidenceNotes[0].status = 'RESOLVED';
  assert.equal(validMotionCommissioningCatalogue(resolvedNote), false);

  const canonicalNote = canonicalMotionCatalogue();
  canonicalNote.mappingEvidenceNotes[0].classification = 'CANONICAL';
  assert.equal(validMotionCommissioningCatalogue(canonicalNote), false);

  const resolvedPhysically = canonicalMotionCatalogue();
  resolvedPhysically.mappingEvidenceNotes[0].physicalResolution = 'RESOLVED';
  assert.equal(validMotionCommissioningCatalogue(resolvedPhysically), false);

  // The mapping is canonical for all eight Water Jets: an open-decision status is refused, and so is
  // any row whose provenance is not the Owner's confirmation.
  const ownerDecision = canonicalMotionCatalogue();
  ownerDecision.waterJets[1].mappingStatus = 'OWNER_DECISION_REQUIRED';
  assert.equal(validMotionCommissioningCatalogue(ownerDecision), false);

  const drawingBasis = canonicalMotionCatalogue();
  drawingBasis.waterJets[3].mappingBasis = 'DRAWING_REV1';
  assert.equal(validMotionCommissioningCatalogue(drawingBasis), false);
});

test('the Jog Speed is a display value in controller units and is never executable', () => {
  const executable = canonicalMotionCatalogue();
  executable.jogSpeed.executable = true;
  assert.equal(validMotionCommissioningCatalogue(executable), false);

  const millimetres = canonicalMotionCatalogue();
  millimetres.jogSpeed.unit = 'mm/s';
  assert.equal(validMotionCommissioningCatalogue(millimetres), false);

  const commissioned = canonicalMotionCatalogue();
  commissioned.jogSpeed.status = 'COMMISSIONED';
  assert.equal(validMotionCommissioningCatalogue(commissioned), false);
});

test('issued commands or a persisted command record are refused', () => {
  const issued = canonicalMotionCatalogue();
  issued.commandTotals.hardwareCommandsIssued = 1;
  assert.equal(validMotionCommissioningCatalogue(issued), false);

  const persisted = canonicalMotionCatalogue();
  persisted.commandTotals.journalPersistence = 'PERSISTED';
  assert.equal(validMotionCommissioningCatalogue(persisted), false);

  const missingClaim = canonicalMotionCatalogue();
  missingClaim.notVerifiedClaims = ['NO_AXIS_HOMED'];
  assert.equal(validMotionCommissioningCatalogue(missingClaim), false);
});

test('a bad fingerprint, an empty legend or an empty canonical text is refused', () => {
  const badFingerprint = canonicalMotionCatalogue();
  badFingerprint.fingerprint = 'ABCDEF';
  assert.equal(validMotionCommissioningCatalogue(badFingerprint), false);

  const emptyLegend = canonicalMotionCatalogue();
  emptyLegend.statusLegend = [];
  assert.equal(validMotionCommissioningCatalogue(emptyLegend), false);

  const emptyText = canonicalMotionCatalogue();
  emptyText.canonicalText = '';
  assert.equal(validMotionCommissioningCatalogue(emptyText), false);
});

test('the motion section is never carried by a Delta', () => {
  const delta = {
    kind: 'delta',
    schema: 'wjss.delta/2',
    apiVersion: 1,
    previousRevision: 1,
    revision: 2,
    generatedAt: '2026-10-07T00:00:00Z',
    motion: canonicalMotionCatalogue(),
  };
  const result = validateEnvelope(delta);
  assert.equal(result.kind, 'delta');
  assert.ok(result.issues.some((issue) => issue.includes('motion must never appear in a Delta')), result.issues.join('; '));

  // A Delta without motion is the accepted encoding: the Delta carries dynamic state only.
  const cleared = { ...delta };
  delete cleared.motion;
  assert.equal(validateEnvelope(cleared).issues.length, 0);
});

test('the committed Snapshot carries NO static motion catalogue, and a Snapshot with one is refused', () => {
  const snapshot = JSON.parse(readFileSync(new URL('../../fixtures/snapshot.seed0.json', import.meta.url), 'utf8'));
  assert.equal(validateEnvelope(snapshot).issues.length, 0);
  assert.equal('motion' in snapshot, false);

  // Adding the catalogue is refused, so the Runtime cannot re-send it on every revision.
  const withCatalogue = { ...snapshot, motion: canonicalMotionCatalogue() };
  const result = validateEnvelope(withCatalogue);
  assert.ok(result.issues.some((issue) => issue.includes('motion')), result.issues.join('; '));

  // Even a null motion is refused: the Snapshot has no motion member at all.
  const withNull = { ...snapshot, motion: null };
  assert.ok(validateEnvelope(withNull).issues.some((issue) => issue.includes('motion')), 'null motion is refused too');
});
