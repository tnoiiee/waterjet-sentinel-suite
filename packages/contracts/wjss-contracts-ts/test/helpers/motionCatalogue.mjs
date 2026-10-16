// Test helper - the canonical read-only STATIC motion commissioning catalogue of this revision.
//
// It mirrors Wjss.Contracts.MotionCommissioningCatalogue (C# authoritative) exactly, so a test can
// exercise the JavaScript validator against the same shape the Product publishes. Every value is the
// standing not-configured / not-commissioned / not-authorized value: the gate is CLOSED, nothing is
// configured, connected, homed or executable.
//
// The catalogue is STATIC and is NOT a Snapshot member and NOT a Delta member.

import {
  MOTION_ALL_REFUSAL_REASONS, MOTION_AXIS_IDS, MOTION_AXIS_ROLE, MOTION_BRAKE_OUTPUT, MOTION_CABINETS,
  MOTION_GATE_CLOSED, MOTION_GATE_REASON, MOTION_INTENTS, MOTION_LIMIT_SIGNAL_ROLES, MOTION_WATER_JET_BINDINGS,
} from '../../validate.mjs';

const SENSE_BY_AXIS = { A: 'ENCODER', B: 'INSTALLED', C: 'INSTALLED', D: 'INSTALLED' };

function limitSignals(senseKind) {
  return MOTION_LIMIT_SIGNAL_ROLES.map((signalRole) => ({
    signalRole,
    signalKind: senseKind,
    polarityStatus: 'UNCONFIRMED',
    signalState: 'UNKNOWN',
    signalStateStatus: 'NOT_AVAILABLE',
    senseBasis: 'OWNER_CONFIRMED',
  }));
}

function axisProjection(controllerId, axisId) {
  const senseKind = SENSE_BY_AXIS[axisId];
  return {
    identity: {
      controllerId,
      axisId,
      axisRole: MOTION_AXIS_ROLE[axisId],
      axisRoleBasis: 'OWNER_CONFIRMED',
      motionSenseKind: senseKind,
      motionSenseBasis: 'OWNER_CONFIRMED',
      brakeOutputIdentity: MOTION_BRAKE_OUTPUT[axisId],
      brakeOutputMappingBasis: 'OWNER_CONFIRMED',
    },
    state: {
      commissioningState: 'NOT_COMMISSIONED',
      homingState: 'NOT_HOMED',
      homingStateStatus: 'UNKNOWN',
      brakeCommandState: 'NONE',
      brakeState: 'UNKNOWN',
      brakeStateStatus: 'NOT_AVAILABLE',
      motionState: 'UNKNOWN',
      motionStateStatus: 'NOT_AVAILABLE',
      limitSignals: limitSignals(senseKind),
      sense: {
        motionSenseKind: senseKind,
        motionSenseBasis: 'OWNER_CONFIRMED',
        senseChannelStatus: 'NOT_AVAILABLE',
        positionValueStatus: 'NOT_READ',
        positionDisplayValue: null,
        positionUnit: 'controller units',
      },
      safety: {
        emergencyStopVisibilityStatus: 'NOT_AVAILABLE',
        stopCircuitVerificationStatus: 'NOT_VERIFIED',
        controlledStopCapabilityStatus: 'UNRESOLVED',
        controlledStopVerificationStatus: 'NOT_VERIFIED',
        isolationVerificationStatus: 'NOT_VERIFIED',
        localProtectionStatus: 'NOT_VERIFIED',
        resetEncoderVerificationStatus: 'NOT_VERIFIED',
      },
      kinematic: {
        kinematicConversionStatus: 'UNRESOLVED',
        kinematicConversionIsPerAxis: true,
        publishedUnit: 'controller units',
        conversionProvenanceRequirement: 'MEASURED_DURING_COMMISSIONING',
      },
      moveAuthorization: {
        moveAuthorizationState: 'REFUSED',
        moveRefusalReasons: [...MOTION_ALL_REFUSAL_REASONS],
      },
    },
  };
}

/** The canonical static catalogue of this software revision: the gate CLOSED, nothing configured. */
export function canonicalMotionCatalogue() {
  return {
    sectionId: 'MOTION_COMMISSIONING_FOUNDATION',
    sectionRevision: 1,
    source: 'APPLICATION_OWNED_MOTION_COMMISSIONING_MODEL',
    motionTopologyRevision: 'OWNER_CONFIRMED_REV1',
    authorizationGateState: MOTION_GATE_CLOSED,
    authorizationGateReasons: [MOTION_GATE_REASON],
    motionIntents: [...MOTION_INTENTS],
    allowedMotionStates: [],
    sessionState: 'NO_SESSION',
    commandTotals: {
      evaluated: 0,
      authorized: 0,
      refused: 0,
      hardwareCommandsIssued: 0,
      commandState: 'NO_COMMANDS',
      journalPersistence: 'NONE',
    },
    controllers: MOTION_CABINETS.map((cabinetId) => ({
      identity: {
        controllerId: `MC-${cabinetId}`,
        cabinetId,
        axisCount: 4,
        controllerModel: 'DMC-B140-M',
        controllerModelStatus: 'DRAWING_CONFIRMED',
        controllerIdentityBasis: 'INFERRED_FROM_OWNER_CONFIRMED_MAPPING',
      },
      configuration: {
        motionPortIdentity: 'MOTION_PORT_NOT_CONFIGURED',
        motionPortStatus: 'NOT_CONFIGURED',
        endpointIdentity: 'ENDPOINT_NOT_CONFIGURED',
        controllerConfigurationStatus: 'NOT_CONFIGURED',
        libraryInstallationStatus: 'NOT_INSTALLED',
        communicationState: 'NOT_CONNECTED',
        motionDispatchReady: false,
        parameterConfigurationState: 'NOT_CONFIGURED',
        softwareLimitConfigurationState: 'NOT_COMMISSIONED',
        limitInputPolarityStatus: 'UNCONFIRMED',
        endstopConfigurationStatus: 'NOT_COMMISSIONED',
        motionDispatchBlockedReason: MOTION_GATE_REASON,
      },
    })),
    axes: MOTION_CABINETS.flatMap((cabinetId) => MOTION_AXIS_IDS.map((axisId) => axisProjection(`MC-${cabinetId}`, axisId))),
    waterJets: MOTION_WATER_JET_BINDINGS.map(([waterJetId, cabinetId, horizontalAxisId, verticalAxisId, mappingStatus]) => ({
      waterJetId,
      cabinetId,
      controllerId: `MC-${cabinetId}`,
      horizontalAxisId,
      verticalAxisId,
      mappingStatus,
      // Canonical provenance: every row's basis is the Owner's confirmation.
      mappingBasis: 'OWNER_CONFIRMED',
    })),
    mappingEvidenceNotes: [{
      evidenceId: 'MOTION-EVIDENCE-01',
      waterJetIds: ['WJ2', 'WJ4'],
      cabinetIds: ['WSB1', 'WSB2'],
      statement: 'The canonical allocation gives WJ2 the C,D axis pair on WSB1 and WJ4 the A,B axis pair on WSB2. The Drawing rev1 expectation for the same two Water Jets was the other axis pair on the same cabinet. The Owner has ruled the canonical allocation.',
      status: 'DRAWING_REV1_CONFLICT_RECORDED',
      classification: 'EVIDENCE_ONLY_NOT_CANONICAL',
      effect: 'This note changes no Product mapping: all eight Water Jets publish CONFIRMED_OWNER with OWNER_CONFIRMED provenance, WJ2 and WJ4 included.',
      physicalResolution: 'NOT_PHYSICALLY_RESOLVED',
    }],
    jogSpeed: {
      parameterId: 'JOG_SPEED',
      displayValue: '10',
      unit: 'controller units',
      status: 'LEGACY_CANDIDATE_UNVERIFIED',
      provenanceBasis: 'LEGACY_CANDIDATE_UNVERIFIED',
      executable: false,
    },
    notVerifiedClaims: [
      'NO_CONTROLLER_CONTACTED', 'NO_ENDPOINT_CONFIGURED', 'NO_LIBRARY_INSTALLED', 'NO_AXIS_HOMED',
      'NO_SOFTWARE_LIMIT_COMMISSIONED', 'NO_LIMIT_INPUT_POLARITY_CONFIRMED', 'NO_BRAKE_STATE_READ',
      'NO_MOTION_COMMAND_ISSUED', 'NO_UNIT_CONVERSION_VERIFIED', 'NO_HARDWARE_VERIFICATION',
    ],
    statusLegend: MOTION_ALL_REFUSAL_REASONS.map((code) => `${code} - wording`),
    canonicalText: 'MOTION COMMISSIONING FOUNDATION rev 1 (OWNER_CONFIRMED_REV1)\nGATE CLOSED\n',
    fingerprint: 'a'.repeat(64),
  };
}
