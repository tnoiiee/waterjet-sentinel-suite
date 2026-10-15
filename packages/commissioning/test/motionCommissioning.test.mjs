// Manual motion commissioning - the read-only Preview projection of the Owner-confirmed motion
// design and the Application-owned authorization gate.
//
// These tests prove the DATA of the design projection and the honesty of its statuses. They prove
// nothing about hardware: no controller is contacted, no command is issued, and no test result may
// be read as device evidence.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  MOTION_ALL_REFUSAL_REASONS, MOTION_AXIS_IDS, MOTION_AXIS_ROLE, MOTION_AXIS_SENSE, MOTION_BRAKE_OUTPUT,
  MOTION_CABINETS, MOTION_COMMISSIONING_DESIGN, MOTION_GATE_CLOSED, MOTION_GATE_REASON, MOTION_INTENTS,
  MOTION_LIMIT_SIGNAL_ROLES, MOTION_NOT_VERIFIED_CLAIMS, MOTION_REFUSAL_WORDING, MOTION_STANDING_REFUSAL_REASONS,
  MOTION_WATER_JET_BINDINGS, axisWording, canonicalMotionCommissioningText, motionCommissioningDesign,
  motionCommissioningFingerprint, motionStatusText, validateMotionCommissioningDesign,
} from '../src/index.mjs';

test('the design projection is frozen, valid and carries the whole topology', () => {
  assert.equal(validateMotionCommissioningDesign(MOTION_COMMISSIONING_DESIGN), true);
  assert.equal(MOTION_COMMISSIONING_DESIGN.controllers.length, 4);
  assert.equal(MOTION_COMMISSIONING_DESIGN.axes.length, 16);
  assert.equal(MOTION_COMMISSIONING_DESIGN.waterJets.length, 8);
  assert.deepEqual(MOTION_COMMISSIONING_DESIGN.controllers.map((c) => c.cabinetId), [...MOTION_CABINETS]);
  // Sixteen axes: four controllers x four axes, controller-major.
  assert.deepEqual(MOTION_COMMISSIONING_DESIGN.axes.map((a) => a.controllerId),
    MOTION_CABINETS.flatMap((cabinetId) => MOTION_AXIS_IDS.map(() => `MC-${cabinetId}`)));
});

test('a copy of the design is independent of the frozen original', () => {
  const copy = motionCommissioningDesign();
  assert.deepEqual(copy, JSON.parse(JSON.stringify(MOTION_COMMISSIONING_DESIGN)));
  copy.controllers[0].motionDispatchReady = true;
  assert.equal(MOTION_COMMISSIONING_DESIGN.controllers[0].motionDispatchReady, false);
  assert.equal(validateMotionCommissioningDesign(MOTION_COMMISSIONING_DESIGN), true);
});

test('the authorization gate is closed and the refusal vocabulary is complete and worded', () => {
  assert.equal(MOTION_COMMISSIONING_DESIGN.authorizationGateState, MOTION_GATE_CLOSED);
  assert.deepEqual(MOTION_COMMISSIONING_DESIGN.authorizationGateReasons, [MOTION_GATE_REASON]);
  assert.deepEqual([...MOTION_ALL_REFUSAL_REASONS], [MOTION_GATE_REASON, ...MOTION_STANDING_REFUSAL_REASONS]);
  assert.equal(MOTION_ALL_REFUSAL_REASONS.length, 10);
  for (const reason of MOTION_ALL_REFUSAL_REASONS) {
    assert.ok(MOTION_REFUSAL_WORDING[reason], `every refusal reason is worded: ${reason}`);
    assert.notEqual(motionStatusText(reason), reason, `the wording is not the bare code: ${reason}`);
  }
  assert.deepEqual([...MOTION_COMMISSIONING_DESIGN.motionIntents], [...MOTION_INTENTS]);
  assert.equal(MOTION_COMMISSIONING_DESIGN.allowedMotionStates.length, 0);
  assert.equal(MOTION_COMMISSIONING_DESIGN.sessionState, 'NO_SESSION');
});

test('the Owner-confirmed Water Jet axis allocation is carried exactly', () => {
  assert.equal(MOTION_COMMISSIONING_DESIGN.waterJets.length, MOTION_WATER_JET_BINDINGS.length);
  MOTION_WATER_JET_BINDINGS.forEach(([waterJetId, cabinetId, horizontal, vertical, mappingStatus], index) => {
    const row = MOTION_COMMISSIONING_DESIGN.waterJets[index];
    assert.equal(row.waterJetId, waterJetId);
    assert.equal(row.cabinetId, cabinetId);
    assert.equal(row.controllerId, `MC-${cabinetId}`);
    assert.equal(row.horizontalAxisId, horizontal);
    assert.equal(row.verticalAxisId, vertical);
    assert.equal(row.mappingStatus, mappingStatus);
    // The screen wording of a role is always "Horizontal Axis" or "Vertical Axis".
    assert.equal(row.horizontalAxisWording, axisWording(MOTION_AXIS_ROLE[horizontal]));
    assert.equal(row.verticalAxisWording, axisWording(MOTION_AXIS_ROLE[vertical]));
  });
});

test('A and C are the Horizontal Axis, B and D the Vertical Axis, on every controller', () => {
  for (const axis of MOTION_COMMISSIONING_DESIGN.axes) {
    assert.equal(axis.axisRole, MOTION_AXIS_ROLE[axis.axisId]);
    assert.equal(axis.axisRoleBasis, 'OWNER_CONFIRMED');
    assert.equal(axis.axisWording, axisWording(axis.axisRole));
    assert.equal(axis.motionSenseKind, MOTION_AXIS_SENSE[axis.axisId]);
    assert.equal(axis.brakeOutputIdentity, MOTION_BRAKE_OUTPUT[axis.axisId]);
    assert.equal(axis.brakeOutputMappingBasis, 'OWNER_CONFIRMED');
  }
  assert.equal(axisWording('HORIZONTAL'), 'Horizontal Axis');
  assert.equal(axisWording('VERTICAL'), 'Vertical Axis');
});

test('no controller is configured, connected or ready, and no endpoint is carried', () => {
  for (const controller of MOTION_COMMISSIONING_DESIGN.controllers) {
    assert.equal(controller.endpointIdentity, 'ENDPOINT_NOT_CONFIGURED');
    assert.equal(controller.motionPortIdentity, 'MOTION_PORT_NOT_CONFIGURED');
    assert.equal(controller.libraryInstallationStatus, 'NOT_INSTALLED');
    assert.equal(controller.communicationState, 'NOT_CONNECTED');
    assert.equal(controller.motionDispatchReady, false);
    assert.equal(controller.motionDispatchBlockedReason, MOTION_GATE_REASON);
    assert.equal(controller.softwareLimitConfigurationState, 'NOT_COMMISSIONED');
    assert.equal(controller.limitInputPolarityStatus, 'UNCONFIRMED');
    // One controller per cabinet is an inference, never reported as a confirmation.
    assert.equal(controller.controllerIdentityBasis, 'INFERRED_FROM_OWNER_CONFIRMED_MAPPING');
  }
});

test('no axis is homed, no brake state is known and every absolute move is refused', () => {
  for (const axis of MOTION_COMMISSIONING_DESIGN.axes) {
    assert.equal(axis.commissioningState, 'NOT_COMMISSIONED');
    assert.equal(axis.homingState, 'NOT_HOMED');
    assert.equal(axis.brakeCommandState, 'NONE');
    assert.equal(axis.brakeState, 'UNKNOWN');
    assert.equal(axis.motionState, 'UNKNOWN');
    assert.equal(axis.moveAuthorizationState, 'REFUSED');
    assert.deepEqual(axis.moveRefusalReasons, [...MOTION_ALL_REFUSAL_REASONS]);
    // Counts stay counts: no conversion is published and the conversion is per axis.
    assert.equal(axis.kinematic.kinematicConversionStatus, 'UNRESOLVED');
    assert.equal(axis.kinematic.kinematicConversionIsPerAxis, true);
    assert.equal(axis.kinematic.publishedUnit, 'controller units');
    assert.deepEqual(axis.limitSignals.map((signal) => signal.signalRole), [...MOTION_LIMIT_SIGNAL_ROLES]);
    for (const signal of axis.limitSignals) {
      assert.equal(signal.polarityStatus, 'UNCONFIRMED');
      assert.equal(signal.signalState, 'UNKNOWN');
    }
  }
});

test('the Jog Speed is a display value in controller units and is never executable', () => {
  const jogSpeed = MOTION_COMMISSIONING_DESIGN.jogSpeed;
  assert.equal(jogSpeed.displayValue, '10');
  assert.equal(jogSpeed.unit, 'controller units');
  assert.equal(jogSpeed.status, 'LEGACY_CANDIDATE_UNVERIFIED');
  assert.equal(jogSpeed.executable, false);
});

test('the WJ2 / WJ4 mapping conflict is published and never suppressed', () => {
  assert.equal(MOTION_COMMISSIONING_DESIGN.mappingConflicts.length, 1);
  const conflict = MOTION_COMMISSIONING_DESIGN.mappingConflicts[0];
  assert.deepEqual([...conflict.waterJetIds], ['WJ2', 'WJ4']);
  assert.deepEqual([...conflict.cabinetIds], ['WSB1', 'WSB2']);
  assert.equal(conflict.status, 'OWNER_DECISION_REQUIRED');
  assert.ok(conflict.description.length > 0);
  assert.ok(conflict.effect.length > 0);
  const flagged = MOTION_COMMISSIONING_DESIGN.waterJets.filter((w) => w.mappingStatus !== 'CONFIRMED_OWNER');
  assert.deepEqual(flagged.map((w) => w.waterJetId), ['WJ2', 'WJ4']);
});

test('the canonical text is deterministic, complete and carries no address', () => {
  const first = canonicalMotionCommissioningText();
  const second = canonicalMotionCommissioningText();
  assert.equal(first, second);
  assert.equal(first, canonicalMotionCommissioningText(motionCommissioningDesign()));
  assert.match(first, /^MOTION COMMISSIONING DESIGN MOTION_COMMISSIONING_DESIGN_REV1/);
  assert.match(first, /GATE CLOSED - MOTION_COMMISSIONING_GATE_NOT_OPEN/);
  assert.match(first, /CONTROLLERS 4/);
  assert.match(first, /AXES 16/);
  assert.match(first, /WATER JETS 8/);
  assert.match(first, /CONFLICTS 1/);
  assert.match(first, /ENDPOINT_NOT_CONFIGURED/);
  // The projection is public-safe: no IPv4 literal and no address-shaped value anywhere in it.
  assert.doesNotMatch(first, /\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b/);
  for (const claim of MOTION_NOT_VERIFIED_CLAIMS) assert.ok(first.includes(claim), claim);
});

test('the fingerprint is the sha-256 of the canonical text and changes with it', async () => {
  const fingerprint = await motionCommissioningFingerprint();
  assert.match(fingerprint, /^[0-9a-f]{64}$/);
  assert.equal(fingerprint, await motionCommissioningFingerprint(motionCommissioningDesign()));

  const altered = motionCommissioningDesign();
  altered.axes[0].homingState = 'HOMED';
  assert.notEqual(await motionCommissioningFingerprint(altered), fingerprint);
});

test('the validator refuses every claim the revision cannot make', () => {
  const mutate = (fn) => {
    const design = motionCommissioningDesign();
    fn(design);
    return validateMotionCommissioningDesign(design);
  };

  assert.equal(mutate((d) => { d.authorizationGateState = 'OPEN'; }), false);
  assert.equal(mutate((d) => { d.controllers[0].motionDispatchReady = true; }), false);
  assert.equal(mutate((d) => { d.controllers[0].endpointIdentity = 'CONFIGURED'; }), false);
  assert.equal(mutate((d) => { d.controllers[0].communicationState = 'CONNECTED'; }), false);
  assert.equal(mutate((d) => { d.axes[0].homingState = 'HOMED'; }), false);
  assert.equal(mutate((d) => { d.axes[0].brakeState = 'RELEASED'; }), false);
  assert.equal(mutate((d) => { d.axes[0].moveAuthorizationState = 'AUTHORIZED'; }), false);
  assert.equal(mutate((d) => { d.axes[0].limitSignals[0].polarityStatus = 'ACTIVE_HIGH'; }), false);
  assert.equal(mutate((d) => { d.axes[1].brakeOutputIdentity = 'OUTPUT_1'; }), false);
  assert.equal(mutate((d) => { d.jogSpeed.executable = true; }), false);
  assert.equal(mutate((d) => { d.jogSpeed.unit = 'mm/s'; }), false);
  assert.equal(mutate((d) => { d.waterJets[0].horizontalAxisId = 'C'; }), false);
  assert.equal(mutate((d) => { d.mappingConflicts = []; }), false);
  assert.equal(mutate((d) => { d.axes = d.axes.slice(0, 15); }), false);
  assert.equal(validateMotionCommissioningDesign(null), false);
  assert.equal(validateMotionCommissioningDesign({}), false);
});
