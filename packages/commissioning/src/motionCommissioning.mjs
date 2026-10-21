// Manual motion commissioning - the read-only PREVIEW projection of the Owner-confirmed motion
// design (four motion controllers, sixteen axes, eight Water Jet axis allocations, the brake plan
// and the limit-sense plan) and of the Application-owned authorization gate.
//
// This module is DATA and wording only. It contains NO device access, no command evaluation and no
// state machine: it never opens a connection, never reads a controller and never issues anything.
// The gate it reports is the one the Application model owns; this module repeats the closed state
// and its reasons as frozen text so the Preview can show them without inventing a second authority.
//
// Owner-confirmed facts carried here (never derived, never inferred from another layer):
//   - Axis A and C are the Horizontal Axis; axis B and D are the Vertical Axis.
//   - Brake outputs: A to output 1, B to output 2, C to output 3, D to output 4.
//   - The CANONICAL Water Jet mapping (MOTION_COMMISSIONING_DESIGN_REV2 / OWNER_CONFIRMED_REV2,
//     Canonical Topology Adoption 2026-10-21): WJ1 = A,B (WSB2) | WJ2 = A,B (WSB1) |
//     WJ3 = C,D (WSB1) | WJ4 = C,D (WSB2) | WJ5 = A,B (WSB3) | WJ6 = A,B (WSB4) |
//     WJ7 = C,D (WSB3) | WJ8 = C,D (WSB4). Every row is explicit data: the two even
//     Water Jets WJ2 and WJ4 take different axis pairs, and the two Water Jets of WSB1 take
//     different axis pairs, so no parity rule and no cabinet rule can derive this table.
//     The REV1 mapping this supersedes remains the historical identity of the earlier
//     revision; it is never presented as current.
//   - Under the superseded REV1 allocation, the Drawing rev1 expectation for WJ2 / WJ4 was
//     the other axis pair on the same cabinet. That earlier discrepancy is recorded as
//     EVIDENCE ONLY and is NOT physically resolved: REV2 does not assert which allocation
//     the installed wiring matches.
// One controller per WSB cabinet is INFERRED from that allocation, and is marked as an inference.
//
// This catalogue is STATIC: it never changes with a Runtime revision, it is not carried by the
// Runtime Snapshot or by a Runtime Delta, and it is served by the dedicated read-only catalogue
// route.
//
// No network address, no endpoint and no vendor-specific command string appears in this file: a
// controller is named by the cabinet that holds it, and the motion port is reported as not
// configured. The Operator-facing axis wording is "Horizontal Axis" / "Vertical Axis" only.

export const MOTION_COMMISSIONING_DESIGN_SCHEMA = 'wjss.motion-commissioning-design/1';
/** Current canonical design identity (Canonical Topology Adoption, 2026-10-21). */
export const MOTION_COMMISSIONING_DESIGN_ID = 'MOTION_COMMISSIONING_DESIGN_REV2';
/**
 * HISTORICAL ONLY: the superseded design identity. REV1 remains the historical identity
 * of the earlier allocation and its publication/projection; current design publication
 * and validation use MOTION_COMMISSIONING_DESIGN_REV2.
 */
export const MOTION_COMMISSIONING_DESIGN_ID_HISTORICAL_REV1 = 'MOTION_COMMISSIONING_DESIGN_REV1';

/** The only two axis wordings the Operator may ever see. */
export const HORIZONTAL_AXIS_WORDING = 'Horizontal Axis';
export const VERTICAL_AXIS_WORDING = 'Vertical Axis';

export const MOTION_GATE_CLOSED = 'CLOSED';
export const MOTION_GATE_REASON = 'MOTION_COMMISSIONING_GATE_NOT_OPEN';

/** The six motion intents the foundation knows. No other intent exists. */
export const MOTION_INTENTS = Object.freeze(['JOG', 'MOVE', 'HOME', 'STOP', 'RELEASE_BRAKE', 'RESET_ENCODER']);

/**
 * The standing preconditions that also refuse every motion intent today. They are preconditions of
 * execution, not alternatives to the gate: the gate closes first.
 */
export const MOTION_STANDING_REFUSAL_REASONS = Object.freeze([
  'GCLIB_NOT_INSTALLED',
  'CONTROLLER_ENDPOINT_NOT_CONFIGURED',
  'CONTROLLER_NOT_CONNECTED',
  'MOTION_PARAMETERS_NOT_CONFIGURED',
  'SOFTWARE_LIMITS_NOT_COMMISSIONED',
  'AXIS_NOT_HOMED',
  'LIMIT_STATE_UNKNOWN',
  'BRAKE_STATE_UNKNOWN',
  'HARDWARE_COMMANDS_NOT_AUTHORIZED',
]);

/** The complete refusal vocabulary, gate reason first, in evaluation order. */
export const MOTION_ALL_REFUSAL_REASONS = Object.freeze([MOTION_GATE_REASON, ...MOTION_STANDING_REFUSAL_REASONS]);

export const MOTION_REFUSAL_WORDING = Object.freeze({
  MOTION_COMMISSIONING_GATE_NOT_OPEN: 'The separate motion commissioning execution gate is not open',
  GCLIB_NOT_INSTALLED: 'The controller communication library is not installed',
  CONTROLLER_ENDPOINT_NOT_CONFIGURED: 'No controller endpoint is configured',
  CONTROLLER_NOT_CONNECTED: 'The controller is not connected',
  MOTION_PARAMETERS_NOT_CONFIGURED: 'Motion parameters are not configured',
  SOFTWARE_LIMITS_NOT_COMMISSIONED: 'Software limits are not commissioned',
  AXIS_NOT_HOMED: 'The axis is not homed',
  LIMIT_STATE_UNKNOWN: 'The limit input state is unknown',
  BRAKE_STATE_UNKNOWN: 'The brake state is unknown',
  HARDWARE_COMMANDS_NOT_AUTHORIZED: 'Hardware commands are not authorized',
});

export const MOTION_INTENT_WORDING = Object.freeze({
  JOG: 'Jog',
  MOVE: 'Move',
  HOME: 'Home',
  STOP: 'Stop',
  RELEASE_BRAKE: 'Release brake',
  RESET_ENCODER: 'Reset encoder',
});

/** Wording for the states the Preview renders. No state is ever carried by colour alone. */
export const MOTION_STATUS_WORDING = Object.freeze({
  CLOSED: 'Closed',
  HORIZONTAL: 'Horizontal',
  VERTICAL: 'Vertical',
  ENCODER: 'Encoder',
  INSTALLED: 'Installed',
  UNCONFIRMED: 'Unconfirmed',
  UNKNOWN: 'Unknown',
  NOT_AVAILABLE: 'Not available',
  NOT_COMMISSIONED: 'Not commissioned',
  NOT_CONFIGURED: 'Not configured',
  ENDPOINT_NOT_CONFIGURED: 'Not configured',
  MOTION_PORT_NOT_CONFIGURED: 'Not configured',
  NOT_INSTALLED: 'Not installed',
  NOT_CONNECTED: 'Not connected',
  NOT_READ: 'Not read',
  NOT_VERIFIED: 'Not verified',
  NOT_HOMED: 'Not homed',
  NONE: 'None',
  REFUSED: 'Refused',
  NO_SESSION: 'No session',
  'DRAWING_CONFIRMED': 'Drawing confirmed',
  CONFIRMED_OWNER: 'Confirmed by the Owner',
  DRAWING_REV1_CONFLICT_RECORDED: 'Drawing rev1 discrepancy recorded (evidence only)',
  EVIDENCE_ONLY_NOT_CANONICAL: 'Evidence only, not canonical',
  NOT_PHYSICALLY_RESOLVED: 'Not physically resolved',
  LEGACY_CANDIDATE_UNVERIFIED: 'Legacy candidate, unverified',
  INFERRED_FROM_OWNER_CONFIRMED_MAPPING: 'Inferred from the confirmed mapping',
  OWNER_CONFIRMED: 'Owner confirmed',
  UNRESOLVED: 'Unresolved',
  MEASURED_DURING_COMMISSIONING: 'Measured during commissioning',
});

export const MOTION_CABINETS = Object.freeze(['WSB1', 'WSB2', 'WSB3', 'WSB4']);
export const MOTION_AXIS_IDS = Object.freeze(['A', 'B', 'C', 'D']);
/** A and C are the Horizontal Axis; B and D are the Vertical Axis (Owner-confirmed). */
export const MOTION_AXIS_ROLE = Object.freeze({ A: 'HORIZONTAL', B: 'VERTICAL', C: 'HORIZONTAL', D: 'VERTICAL' });
/** Position sense per axis: axis A is read through an encoder; B, C and D carry the installed switch set. */
export const MOTION_AXIS_SENSE = Object.freeze({ A: 'ENCODER', B: 'INSTALLED', C: 'INSTALLED', D: 'INSTALLED' });
/** Brake output allocation A-1, B-2, C-3, D-4 (Owner-confirmed). */
export const MOTION_BRAKE_OUTPUT = Object.freeze({ A: 'OUTPUT_1', B: 'OUTPUT_2', C: 'OUTPUT_3', D: 'OUTPUT_4' });
/** The three installed limit inputs of an axis, in Reverse, Home, Forward order. */
export const MOTION_LIMIT_SIGNAL_ROLES = Object.freeze(['REVERSE', 'HOME', 'FORWARD']);

/** [WaterJetId, CabinetId, HorizontalAxisId, VerticalAxisId, MappingStatus] - the confirmed REV2 allocation. */
export const MOTION_WATER_JET_BINDINGS = Object.freeze([
  ['WJ1', 'WSB2', 'A', 'B', 'CONFIRMED_OWNER'],
  ['WJ2', 'WSB1', 'A', 'B', 'CONFIRMED_OWNER'],
  ['WJ3', 'WSB1', 'C', 'D', 'CONFIRMED_OWNER'],
  ['WJ4', 'WSB2', 'C', 'D', 'CONFIRMED_OWNER'],
  ['WJ5', 'WSB3', 'A', 'B', 'CONFIRMED_OWNER'],
  ['WJ6', 'WSB4', 'A', 'B', 'CONFIRMED_OWNER'],
  ['WJ7', 'WSB3', 'C', 'D', 'CONFIRMED_OWNER'],
  ['WJ8', 'WSB4', 'C', 'D', 'CONFIRMED_OWNER'],
]);

export const MOTION_NOT_VERIFIED_CLAIMS = Object.freeze([
  'NO_CONTROLLER_CONTACTED',
  'NO_ENDPOINT_CONFIGURED',
  'NO_LIBRARY_INSTALLED',
  'NO_AXIS_HOMED',
  'NO_SOFTWARE_LIMIT_COMMISSIONED',
  'NO_LIMIT_INPUT_POLARITY_CONFIRMED',
  'NO_BRAKE_STATE_READ',
  'NO_MOTION_COMMAND_ISSUED',
  'NO_UNIT_CONVERSION_VERIFIED',
  'NO_LEGACY_VALUE_PROMOTED_TO_CONFIGURATION',
  'NO_HARDWARE_VERIFICATION',
  'NO_FIELD_VERIFICATION',
]);

/**
 * The one recorded Drawing discrepancy, carried as EVIDENCE METADATA ONLY.
 *
 * The discrepancy was recorded against the superseded OWNER_CONFIRMED_REV1 allocation
 * (WJ2 = C,D; WJ4 = A,B). The current canonical allocation is OWNER_CONFIRMED_REV2
 * (WJ2 = A,B on WSB1; WJ4 = C,D on WSB2), so this is NOT an open decision and NOT
 * ambiguity. The note exists so the earlier discrepancy is not silently lost. It changes
 * no mapping, does not override the Owner, does not open the authorization gate, and
 * states explicitly that the physical installation has NOT been resolved: REV2 does not
 * assert which allocation the installed wiring matches.
 */
export const MOTION_MAPPING_EVIDENCE_NOTES = Object.freeze([
  {
    evidenceId: 'MOTION-EVIDENCE-01',
    waterJetIds: Object.freeze(['WJ2', 'WJ4']),
    cabinetIds: Object.freeze(['WSB1', 'WSB2']),
    statement: 'Historical evidence note (superseded for current allocation by OWNER_CONFIRMED_REV2): under the superseded OWNER_CONFIRMED_REV1 allocation, WJ2 held the C,D axis pair on WSB1 and WJ4 the A,B axis pair on WSB2, while the Drawing rev1 expectation for the same two Water Jets was the other axis pair on the same cabinet. The current canonical allocation (OWNER_CONFIRMED_REV2) gives WJ2 the A,B axis pair on WSB1 and WJ4 the C,D axis pair on WSB2. This note records the earlier discrepancy only; the physical installation remains NOT_PHYSICALLY_RESOLVED, and nothing here asserts which allocation the installed wiring matches.',
    status: 'DRAWING_REV1_CONFLICT_RECORDED',
    classification: 'EVIDENCE_ONLY_NOT_CANONICAL',
    effect: 'This note changes no Product mapping: all eight Water Jets publish CONFIRMED_OWNER with OWNER_CONFIRMED provenance, WJ2 and WJ4 included. The note exists so the Drawing discrepancy is not lost. It does not open the authorization gate and it does not authorise any motion.',
    physicalResolution: 'NOT_PHYSICALLY_RESOLVED',
  },
]);

/** Wording for the claims this projection deliberately does not make. */
export const MOTION_NOT_VERIFIED_WORDING = Object.freeze({
  NO_CONTROLLER_CONTACTED: 'No controller was contacted',
  NO_ENDPOINT_CONFIGURED: 'No controller endpoint is configured',
  NO_LIBRARY_INSTALLED: 'No controller communication library is installed',
  NO_AXIS_HOMED: 'No axis is homed',
  NO_SOFTWARE_LIMIT_COMMISSIONED: 'No software limit is commissioned',
  NO_LIMIT_INPUT_POLARITY_CONFIRMED: 'No limit input polarity is confirmed',
  NO_BRAKE_STATE_READ: 'No brake state was read',
  NO_MOTION_COMMAND_ISSUED: 'No motion command was issued',
  NO_UNIT_CONVERSION_VERIFIED: 'No unit conversion is verified',
  NO_LEGACY_VALUE_PROMOTED_TO_CONFIGURATION: 'No Legacy value was promoted to configuration',
  NO_HARDWARE_VERIFICATION: 'No hardware verification',
  NO_FIELD_VERIFICATION: 'No field verification',
});

/** Operator wording for one status code. An unknown code returns itself, never an empty string. */
export function motionStatusText(code) {
  return MOTION_STATUS_WORDING[code] ?? MOTION_REFUSAL_WORDING[code] ?? MOTION_INTENT_WORDING[code] ?? code;
}

/** The Operator-facing axis wording: "Horizontal Axis" or "Vertical Axis", never a letter on its own. */
export function axisWording(axisRole) {
  return axisRole === 'HORIZONTAL' ? HORIZONTAL_AXIS_WORDING : VERTICAL_AXIS_WORDING;
}

/**
 * The one-line Operator identity of one physical axis: the ROLE first, then the physical controller
 * axis letter as DIAGNOSTIC IDENTITY ONLY, e.g. "Horizontal Axis · C". The letter never replaces and
 * never precedes the role.
 *
 * The vendor name is deliberately NOT printed: this repository's boundary keeps vendor vocabulary
 * out of the served UI. The letter A/B/C/D is the controller axis identity and is enough for an
 * engineer standing at the cabinet.
 */
export function axisPresentation(axisRole, axisId) {
  return `${axisWording(axisRole)} · ${axisId}`;
}

const axisRows = () => MOTION_CABINETS.flatMap((cabinetId) => MOTION_AXIS_IDS.map((axisId) => {
  const senseKind = MOTION_AXIS_SENSE[axisId];
  return {
    controllerId: `MC-${cabinetId}`,
    cabinetId,
    axisId,
    axisRole: MOTION_AXIS_ROLE[axisId],
    axisWording: axisWording(MOTION_AXIS_ROLE[axisId]),
    axisRoleBasis: 'OWNER_CONFIRMED',
    motionSenseKind: senseKind,
    motionSenseBasis: 'OWNER_CONFIRMED',
    brakeOutputIdentity: MOTION_BRAKE_OUTPUT[axisId],
    brakeOutputMappingBasis: 'OWNER_CONFIRMED',
    commissioningState: 'NOT_COMMISSIONED',
    homingState: 'NOT_HOMED',
    homingStateStatus: 'UNKNOWN',
    brakeCommandState: 'NONE',
    brakeState: 'UNKNOWN',
    brakeStateStatus: 'NOT_AVAILABLE',
    motionState: 'UNKNOWN',
    motionStateStatus: 'NOT_AVAILABLE',
    limitSignals: MOTION_LIMIT_SIGNAL_ROLES.map((signalRole) => ({
      signalRole,
      signalKind: senseKind,
      polarityStatus: 'UNCONFIRMED',
      signalState: 'UNKNOWN',
      signalStateStatus: 'NOT_AVAILABLE',
    })),
    kinematic: {
      kinematicConversionStatus: 'UNRESOLVED',
      kinematicConversionIsPerAxis: true,
      publishedUnit: 'controller units',
      conversionProvenanceRequirement: 'MEASURED_DURING_COMMISSIONING',
    },
    moveAuthorizationState: 'REFUSED',
    moveRefusalReasons: [...MOTION_ALL_REFUSAL_REASONS],
  };
}));

const controllerRows = () => MOTION_CABINETS.map((cabinetId) => ({
  controllerId: `MC-${cabinetId}`,
  cabinetId,
  axisCount: MOTION_AXIS_IDS.length,
  controllerModel: 'DMC-B140-M',
  controllerModelStatus: 'DRAWING_CONFIRMED',
  // One four-axis controller per cabinet follows from the confirmed allocation; it is recorded as
  // an inference, never as a confirmation.
  controllerIdentityBasis: 'INFERRED_FROM_OWNER_CONFIRMED_MAPPING',
  motionPortIdentity: 'MOTION_PORT_NOT_CONFIGURED',
  motionPortStatus: 'NOT_CONFIGURED',
  endpointIdentity: 'ENDPOINT_NOT_CONFIGURED',
  controllerConfigurationStatus: 'NOT_CONFIGURED',
  libraryInstallationStatus: 'NOT_INSTALLED',
  communicationState: 'NOT_CONNECTED',
  motionDispatchReady: false,
  motionDispatchBlockedReason: MOTION_GATE_REASON,
  parameterConfigurationState: 'NOT_CONFIGURED',
  softwareLimitConfigurationState: 'NOT_COMMISSIONED',
  limitInputPolarityStatus: 'UNCONFIRMED',
  endstopConfigurationStatus: 'NOT_COMMISSIONED',
}));

const waterJetRows = () => MOTION_WATER_JET_BINDINGS.map(([waterJetId, cabinetId, horizontalAxisId, verticalAxisId, mappingStatus]) => ({
  waterJetId,
  cabinetId,
  controllerId: `MC-${cabinetId}`,
  horizontalAxisId,
  horizontalAxisWording: axisWording(MOTION_AXIS_ROLE[horizontalAxisId]),
  horizontalAxisPresentation: axisPresentation(MOTION_AXIS_ROLE[horizontalAxisId], horizontalAxisId),
  verticalAxisId,
  verticalAxisWording: axisWording(MOTION_AXIS_ROLE[verticalAxisId]),
  verticalAxisPresentation: axisPresentation(MOTION_AXIS_ROLE[verticalAxisId], verticalAxisId),
  mappingStatus,
  // Canonical provenance: the Owner's confirmation. No row carries a drawing-derived basis.
  mappingBasis: 'OWNER_CONFIRMED',
}));

/**
 * The frozen design projection. Every motion value it publishes is the standing not-configured /
 * not-commissioned value of this software revision: nothing here is read from a device.
 */
export const MOTION_COMMISSIONING_DESIGN = Object.freeze({
  schema: MOTION_COMMISSIONING_DESIGN_SCHEMA,
  designId: MOTION_COMMISSIONING_DESIGN_ID,
  source: 'PREVIEW_PROJECTION_OF_OWNER_CONFIRMED_MAPPING',
  authorizationGateState: MOTION_GATE_CLOSED,
  authorizationGateReasons: Object.freeze([MOTION_GATE_REASON]),
  motionIntents: MOTION_INTENTS,
  allowedMotionStates: Object.freeze([]),
  sessionState: 'NO_SESSION',
  jogSpeed: Object.freeze({
    parameterId: 'JOG_SPEED',
    displayValue: '10',
    unit: 'controller units',
    status: 'LEGACY_CANDIDATE_UNVERIFIED',
    executable: false,
  }),
  controllers: Object.freeze(controllerRows()),
  axes: Object.freeze(axisRows()),
  waterJets: Object.freeze(waterJetRows()),
  mappingEvidenceNotes: MOTION_MAPPING_EVIDENCE_NOTES,
  notVerifiedClaims: MOTION_NOT_VERIFIED_CLAIMS,
});

/** A deep copy of the frozen design projection, safe for a caller to hold. */
export function motionCommissioningDesign() {
  return JSON.parse(JSON.stringify(MOTION_COMMISSIONING_DESIGN));
}

/** The design projection as one deterministic text block: the Preview parity surface. */
export function canonicalMotionCommissioningText(design = MOTION_COMMISSIONING_DESIGN) {
  const lines = [];
  lines.push(`MOTION COMMISSIONING DESIGN ${design.designId} - ${design.source}`);
  lines.push(`GATE ${design.authorizationGateState} - ${design.authorizationGateReasons.join(', ')}`);
  lines.push(`SESSION ${design.sessionState} - INTENTS ${design.motionIntents.join(', ')}`);
  lines.push(`JOG SPEED ${design.jogSpeed.displayValue} ${design.jogSpeed.unit} - ${design.jogSpeed.status} - EXECUTABLE ${design.jogSpeed.executable}`);
  lines.push(`CONTROLLERS ${design.controllers.length}`);
  for (const controller of design.controllers) {
    lines.push([
      controller.controllerId, `cabinet ${controller.cabinetId}`, `axes ${controller.axisCount}`,
      `model ${controller.controllerModel} (${controller.controllerModelStatus})`,
      `identity ${controller.controllerIdentityBasis}`,
      `port ${controller.motionPortIdentity} (${controller.motionPortStatus})`,
      `endpoint ${controller.endpointIdentity}`,
      `library ${controller.libraryInstallationStatus}`,
      `communication ${controller.communicationState}`,
      `dispatch ready ${controller.motionDispatchReady}`,
      `blocked ${controller.motionDispatchBlockedReason}`,
    ].join(' '));
  }
  lines.push(`AXES ${design.axes.length}`);
  for (const axis of design.axes) {
    lines.push([
      `${axis.controllerId}/${axis.axisId}`, `${axis.axisRole} (${axis.axisRoleBasis})`,
      `sense ${axis.motionSenseKind} (${axis.motionSenseBasis})`,
      `brake ${axis.brakeOutputIdentity} (${axis.brakeOutputMappingBasis})`,
      `commissioning ${axis.commissioningState}`, `home ${axis.homingState} (${axis.homingStateStatus})`,
      `brake command ${axis.brakeCommandState}`, `brake state ${axis.brakeState} (${axis.brakeStateStatus})`,
      `motion ${axis.motionState} (${axis.motionStateStatus})`,
      `unit ${axis.kinematic.publishedUnit}`, `conversion ${axis.kinematic.kinematicConversionStatus}`,
      'signals',
      axis.limitSignals.map((signal) => `${signal.signalRole} ${signal.signalKind} ${signal.polarityStatus} ${signal.signalState}`).join(' | '),
      `move ${axis.moveAuthorizationState}`,
    ].join(' '));
  }
  lines.push(`WATER JETS ${design.waterJets.length}`);
  for (const waterJet of design.waterJets) {
    lines.push([
      waterJet.waterJetId, `cabinet ${waterJet.cabinetId}`, `controller ${waterJet.controllerId}`,
      `horizontal ${waterJet.horizontalAxisId}`, `vertical ${waterJet.verticalAxisId}`,
      '-', `${waterJet.mappingStatus} (${waterJet.mappingBasis})`,
    ].join(' '));
  }
  lines.push(`EVIDENCE NOTES ${design.mappingEvidenceNotes.length}`);
  for (const note of design.mappingEvidenceNotes) {
    lines.push([note.evidenceId, `water jets ${note.waterJetIds.join(',')}`,
      `cabinets ${note.cabinetIds.join(',')}`, `status ${note.status}`,
      `classification ${note.classification}`, `resolution ${note.physicalResolution}`].join(' '));
  }
  lines.push(`NOT VERIFIED ${design.notVerifiedClaims.length}`);
  for (const claim of design.notVerifiedClaims) lines.push(claim);
  return `${lines.join('\n')}\n`;
}

/** SHA-256 of the canonical text, lowercase hex. */
export async function motionCommissioningFingerprint(design = MOTION_COMMISSIONING_DESIGN) {
  const { createHash } = await import('node:crypto');
  return createHash('sha256').update(canonicalMotionCommissioningText(design), 'utf8').digest('hex');
}

/**
 * Validates a design projection. It is accepted ONLY in the state this revision can publish: the
 * gate CLOSED, four controllers, sixteen axes, the confirmed allocation, the conflict published and
 * no axis homed, connected or authorized.
 */
export function validateMotionCommissioningDesign(design) {
  if (design === null || typeof design !== 'object' || Array.isArray(design)) return false;
  if (design.schema !== MOTION_COMMISSIONING_DESIGN_SCHEMA) return false;
  // Current publication identity is REV2 (Canonical Topology Adoption). The REV1
  // designId is a historical identity only and must never validate as current.
  if (design.designId !== MOTION_COMMISSIONING_DESIGN_ID) return false;
  if (design.authorizationGateState !== MOTION_GATE_CLOSED) return false;
  if (!Array.isArray(design.authorizationGateReasons) || design.authorizationGateReasons.length !== 1
      || design.authorizationGateReasons[0] !== MOTION_GATE_REASON) return false;
  if (!Array.isArray(design.allowedMotionStates) || design.allowedMotionStates.length !== 0) return false;
  if (design.sessionState !== 'NO_SESSION') return false;
  if (design.jogSpeed?.displayValue !== '10' || design.jogSpeed?.unit !== 'controller units'
      || design.jogSpeed?.executable !== false) return false;
  if (design.controllers?.length !== MOTION_CABINETS.length) return false;
  if (design.axes?.length !== MOTION_CABINETS.length * MOTION_AXIS_IDS.length) return false;
  if (design.waterJets?.length !== MOTION_WATER_JET_BINDINGS.length) return false;
  if (!Array.isArray(design.mappingEvidenceNotes) || design.mappingEvidenceNotes.length !== 1) return false;
  {
    const [note] = design.mappingEvidenceNotes;
    if (note.evidenceId !== 'MOTION-EVIDENCE-01') return false;
    if (note.status !== 'DRAWING_REV1_CONFLICT_RECORDED') return false;
    if (note.classification !== 'EVIDENCE_ONLY_NOT_CANONICAL') return false;
    if (note.physicalResolution !== 'NOT_PHYSICALLY_RESOLVED') return false;
  }
  if (!Array.isArray(design.notVerifiedClaims) || design.notVerifiedClaims.length === 0) return false;

  for (const controller of design.controllers) {
    if (controller.motionDispatchReady !== false) return false;
    if (controller.endpointIdentity !== 'ENDPOINT_NOT_CONFIGURED') return false;
    if (controller.communicationState !== 'NOT_CONNECTED') return false;
    if (controller.motionDispatchBlockedReason !== MOTION_GATE_REASON) return false;
    if (controller.controllerIdentityBasis !== 'INFERRED_FROM_OWNER_CONFIRMED_MAPPING') return false;
  }

  for (const axis of design.axes) {
    if (axis.axisRole !== MOTION_AXIS_ROLE[axis.axisId]) return false;
    if (axis.brakeOutputIdentity !== MOTION_BRAKE_OUTPUT[axis.axisId]) return false;
    if (axis.homingState !== 'NOT_HOMED') return false;
    if (axis.brakeState !== 'UNKNOWN' || axis.brakeCommandState !== 'NONE') return false;
    if (axis.motionState !== 'UNKNOWN') return false;
    if (axis.moveAuthorizationState !== 'REFUSED') return false;
    if (!Array.isArray(axis.limitSignals) || axis.limitSignals.length !== MOTION_LIMIT_SIGNAL_ROLES.length) return false;
    for (const signal of axis.limitSignals) {
      if (signal.polarityStatus !== 'UNCONFIRMED' || signal.signalState !== 'UNKNOWN') return false;
    }
  }

  for (let index = 0; index < MOTION_WATER_JET_BINDINGS.length; index += 1) {
    const [waterJetId, cabinetId, horizontalAxisId, verticalAxisId, mappingStatus] = MOTION_WATER_JET_BINDINGS[index];
    const waterJet = design.waterJets[index];
    if (waterJet.waterJetId !== waterJetId || waterJet.cabinetId !== cabinetId) return false;
    if (waterJet.horizontalAxisId !== horizontalAxisId || waterJet.verticalAxisId !== verticalAxisId) return false;
    if (waterJet.mappingStatus !== mappingStatus) return false;
    // Canonical provenance: every row is Owner-confirmed. No open-decision status may appear.
    if (waterJet.mappingBasis !== 'OWNER_CONFIRMED') return false;
    if (waterJet.mappingStatus === 'OWNER_DECISION_REQUIRED') return false;
    if (waterJet.horizontalAxisPresentation !== axisPresentation(MOTION_AXIS_ROLE[waterJet.horizontalAxisId], waterJet.horizontalAxisId)) return false;
    if (waterJet.verticalAxisPresentation !== axisPresentation(MOTION_AXIS_ROLE[waterJet.verticalAxisId], waterJet.verticalAxisId)) return false;
    if (MOTION_AXIS_ROLE[waterJet.horizontalAxisId] !== 'HORIZONTAL') return false;
    if (MOTION_AXIS_ROLE[waterJet.verticalAxisId] !== 'VERTICAL') return false;
  }

  return true;
}
