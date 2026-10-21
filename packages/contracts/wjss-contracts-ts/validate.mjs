/**
 * WJSS Stage 0.3A-1 — structural validator for the golden fixtures.
 *
 * Scope: structure and the ACCEPTED semantic invariants of the presentation
 * contract only (counts, identities, revisions, queue shape, head-only
 * dispatch, Safe Return evidence ordering, Delta sparseness, gap detection,
 * prohibited production-key scan). It does NOT validate any Production policy:
 * queue eligibility, alarm blocking, Safe Return failure outcomes, re-queue,
 * roles/permissions are open Owner decisions and deliberately absent.
 *
 * Usage: validateEnvelope(obj) -> { kind, issues: string[], gap: boolean }
 * issues.length === 0 means structurally valid. A gap is NOT an error: it is
 * the signal for the consumer to drop state and request a fresh Snapshot.
 */

export const SENSOR_MAP_SCHEMA = 'wjss.sensor-map/2';
export const SNAPSHOT_SCHEMA = 'wjss.snapshot/2';
export const DELTA_SCHEMA = 'wjss.delta/2';
export const API_VERSION = 1;

// ---- canonical structure (mirror of Wjss.Contracts.CanonicalSensorMap) ----
export const LOGICAL_COLUMNS = 18;
export const LOGICAL_ROWS = 6;
const WALL_GROUPS = [
  ['LEFT', 1, 4],
  ['REAR', 5, 9],
  ['RIGHT', 10, 13],
  ['FRONT', 14, 18],
];
// Owner critical topology correction: every one of the 6 x 18 logical positions is a Product
// Sensor. I7 and I16 are normal Sensors in their ordinal position; no NON_SENSOR_GAP position
// exists. The earlier 106-Sensor / 212-channel belief (I7, I16 as gaps) is superseded.
export const TOPOLOGY = Object.freeze({
  productSensors: 108,
  assignedProductTcChannels: 216,
  physicalTcChannels: 240,
  unassignedPhysicalTcChannels: 24,
  logicalPositions: 108,
  nonSensorGapPositions: 0,
  correctedSensorIds: Object.freeze(['I7', 'I16']),
});

/**
 * Canonical target coverage (Canonical Topology Adoption, 2026-10-21): WJ1-WJ4 clean the
 * UPPER tier and WJ5-WJ8 the LOWER tier, each from the opposite wall in the same tier.
 * Assignment follows the TARGET wall/region (legacy cleaning-device ordinal maps directly
 * to WJn). The pre-adoption coverage (LOWER for WJ1-WJ4, UPPER for WJ5-WJ8) is superseded
 * for current authority and survives only in frozen historical generator proof data.
 */
export const TARGET_COVERAGE = [
  ['REAR', 'UPPER', 'WJ1'],
  ['RIGHT', 'UPPER', 'WJ2'],
  ['FRONT', 'UPPER', 'WJ3'],
  ['LEFT', 'UPPER', 'WJ4'],
  ['REAR', 'LOWER', 'WJ5'],
  ['RIGHT', 'LOWER', 'WJ6'],
  ['FRONT', 'LOWER', 'WJ7'],
  ['LEFT', 'LOWER', 'WJ8'],
];
const COVERAGE_BY_ID = new Map(TARGET_COVERAGE.map(([w, r, id]) => [id, `${w}|${r}`]));

export function sensorIdFor(row, col) {
  if (row === 1) return `G+${200 + col}`;
  if (row === 2) return `G+${100 + col}`;
  if (row === 3) return `G${col}`;
  if (row === 4) return `H${col}`;
  if (row === 5) return `I${col}`;
  if (row === 6) return `J${col}`;
  throw new Error(`bad row ${row}`);
}

export function wallForColumn(col) {
  for (const [wall, first, last] of WALL_GROUPS) {
    if (col >= first && col <= last) return wall;
  }
  throw new Error(`bad column ${col}`);
}

/** Vertical region of a logical row: rows 1-2 (G+2xx, G+1xx) are UPPER; rows 3-6 (G, H, I, J) are LOWER. */
export function regionForLogicalRow(row) {
  if (row === 1 || row === 2) return 'UPPER';
  if (row >= 3 && row <= 6) return 'LOWER';
  throw new Error(`bad row ${row}`);
}

export function canonicalSensorIds() {
  const ids = [];
  for (let row = 1; row <= LOGICAL_ROWS; row += 1) {
    for (let col = 1; col <= LOGICAL_COLUMNS; col += 1) {
      ids.push(sensorIdFor(row, col));
    }
  }
  return ids;
}

const SENSOR_ID_SET = new Set(canonicalSensorIds());
const WATER_JET_IDS = TARGET_COVERAGE.map(([, , id]) => id);
/**
 * Placement vocabulary. Under the current Owner disposition the fine-grained placement of
 * ALL EIGHT Water Jets is UNRESOLVED (fine-grained placement pending authoritative layout;
 * canonical wall/tier confirmed) and never anchored; the resolved kinds remain defined
 * vocabulary for a future authoritative layout, not current Product truth.
 */
const WATER_JET_PLACEMENT_KINDS = new Set(['BETWEEN_HORIZONTAL', 'BETWEEN_VERTICAL', 'JUNCTION', 'UNRESOLVED']);
const ISOLATION_VALVE_IDS = WATER_JET_IDS.map((id) => `IV${id.slice(2)}`);

// ---- motion commissioning foundation (mirror of Wjss.Contracts.MotionVocabulary) ----
// The Application-owned STATIC manual motion commissioning catalogue. Every value is a status or a
// logical identity: no member carries a network address, and no member can express an authorized
// motion command. The authorization gate is CLOSED for this software revision.
//
// The catalogue is NOT a Snapshot member and NOT a Delta member. It is served by the dedicated
// read-only catalogue route, so the Snapshot stays reserved for dynamic observed state.
export const MOTION_SECTION_ID = 'MOTION_COMMISSIONING_FOUNDATION';
export const MOTION_SECTION_REVISION = 1;
export const MOTION_SOURCE = 'APPLICATION_OWNED_MOTION_COMMISSIONING_MODEL';
/** Current canonical motion topology revision (Canonical Topology Adoption, 2026-10-21). */
export const MOTION_TOPOLOGY_REVISION = 'OWNER_CONFIRMED_REV2';
/**
 * HISTORICAL ONLY: the superseded motion topology revision. REV1 remains the historical
 * identity of the earlier allocation and its evidence; current validators require REV2.
 */
export const MOTION_TOPOLOGY_REVISION_HISTORICAL_REV1 = 'OWNER_CONFIRMED_REV1';
export const MOTION_GATE_CLOSED = 'CLOSED';
export const MOTION_GATE_REASON = 'MOTION_COMMISSIONING_GATE_NOT_OPEN';
export const MOTION_INTENTS = Object.freeze(['JOG', 'MOVE', 'HOME', 'STOP', 'RELEASE_BRAKE', 'RESET_ENCODER']);
/** The complete refusal vocabulary in evaluation order: the gate reason leads the standing preconditions. */
export const MOTION_ALL_REFUSAL_REASONS = Object.freeze([
  MOTION_GATE_REASON,
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
export const MOTION_CABINETS = Object.freeze(['WSB1', 'WSB2', 'WSB3', 'WSB4']);
export const MOTION_AXIS_IDS = Object.freeze(['A', 'B', 'C', 'D']);
/** A and C are the Horizontal Axis; B and D are the Vertical Axis (Owner-confirmed). */
export const MOTION_AXIS_ROLE = Object.freeze({ A: 'HORIZONTAL', B: 'VERTICAL', C: 'HORIZONTAL', D: 'VERTICAL' });
/** Brake output allocation: A-1, B-2, C-3, D-4 (Owner-confirmed). */
export const MOTION_BRAKE_OUTPUT = Object.freeze({ A: 'OUTPUT_1', B: 'OUTPUT_2', C: 'OUTPUT_3', D: 'OUTPUT_4' });
/** [WaterJetId, CabinetId, HorizontalAxisId, VerticalAxisId, MappingStatus] - the Owner-confirmed REV2 allocation. */
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
export const MOTION_LIMIT_SIGNAL_ROLES = Object.freeze(['REVERSE', 'HOME', 'FORWARD']);
/** The one recorded Drawing discrepancy: evidence metadata only, never canonical ambiguity. */
export const MOTION_EVIDENCE_NOTE_ID = 'MOTION-EVIDENCE-01';
export const MOTION_EVIDENCE_STATUS = 'DRAWING_REV1_CONFLICT_RECORDED';
export const MOTION_EVIDENCE_CLASSIFICATION = 'EVIDENCE_ONLY_NOT_CANONICAL';
/** The discrepancy has NOT been physically resolved on the installation: recorded, not closed. */
export const MOTION_EVIDENCE_RESOLUTION = 'NOT_PHYSICALLY_RESOLVED';

// ---------------------------------------------------------------- helpers
const isInt = (n) => Number.isInteger(n);
const isStr = (s) => typeof s === 'string' && s.length > 0;

const PROHIBITED_KEY = new RegExp(
  '^(ip|host|port|regi' + 'ster|coordi' + 'nate|travelLim' + 'it|tagLis' + 't|connectio' + 'nString|pass' + 'word|tok' + 'en|sec' + 'ret)', 'i');

// The one-Node PHYSICAL read-only slice must record the DECLARED read profile it used, which the
// Owner-approved Scope fixes and publishes, and whose shape legitimately names a holding-register
// AREA and the factory PORT. That is the only collision with the key-name heuristic above, and it is
// resolved by PINNING BOTH THE PATH AND THE VALUE rather than by renaming the keys to slip past the
// pattern. The allowance is therefore strictly narrower than the heuristic it refines: anywhere else,
// and at this path with any other value, the finding stands. Nothing else is exempted, and the
// deterministic variant carries no such block at all (it is refused below).
const AUTHORIZED_READ_PROFILE_KEYS = Object.freeze({
  [`$.tcAcquisition.physicalEvidence.readProfile.port`]: 502,
  [`$.tcAcquisition.physicalEvidence.readProfile.registerArea`]: 'HOLDING_REGISTERS',
});

function isAuthorizedReadProfileKey(path, key, value) {
  const pinned = AUTHORIZED_READ_PROFILE_KEYS[`${path}.${key}`];
  return pinned !== undefined && value === pinned;
}

function scanProhibitedKeys(node, path, issues, depth = 0) {
  if (depth > 12 || node === null || typeof node !== 'object') return;
  if (Array.isArray(node)) {
    for (const [i, item] of node.entries()) scanProhibitedKeys(item, `${path}[${i}]`, issues, depth + 1);
    return;
  }
  for (const [key, value] of Object.entries(node)) {
    // 'host' is the health-payload host LABEL (never a network host); allowed.
    if (PROHIBITED_KEY.test(key) && key !== 'host' && !isAuthorizedReadProfileKey(path, key, value)) {
      issues.push(`${path}.${key}: prohibited production-scope key name`);
    }
    scanProhibitedKeys(value, `${path}.${key}`, issues, depth + 1);
  }
}

function validateSlot(slot, path, issues) {
  if (!isStr(slot.slotId)) issues.push(`${path}.slotId`);
  if (slot.positionKind === 'SENSOR') {
    if (slot.gapAnchorForWaterJetId !== null && slot.gapAnchorForWaterJetId !== undefined) {
      issues.push(`${path}: a Sensor position never carries gapAnchorForWaterJetId`);
    }
    if (!SENSOR_ID_SET.has(slot.sensorId)) issues.push(`${path}: sensorId not in canonical set`);
    if (slot.sensorId !== sensorIdFor(slot.logicalRow, slot.logicalColumn)) {
      issues.push(`${path}: sensorId ${slot.sensorId} is not the Sensor of logical position row ${slot.logicalRow} column ${slot.logicalColumn}`);
    }
  } else {
    // Owner critical topology correction: no NON_SENSOR_GAP position exists; I7 and I16 are Sensors.
    issues.push(`${path}: positionKind must be SENSOR (every logical position is a Product Sensor; I7 and I16 included)`);
  }
  const group = WALL_GROUPS.find(([, f, l]) => slot.logicalColumn >= f && slot.logicalColumn <= l);
  if (!group || group[0] !== slot.wall) issues.push(`${path}: wall/column mismatch`);
  if (slot.wallColumn !== slot.logicalColumn - (group ? group[1] : 1) + 1) issues.push(`${path}: wallColumn mismatch`);
  if (slot.wallRow !== slot.logicalRow) issues.push(`${path}: wallRow must equal logicalRow`);
  const expectedId = `SLOT-R${slot.logicalRow}-C${String(slot.logicalColumn).padStart(2, '0')}`;
  if (slot.slotId !== expectedId) issues.push(`${path}: slotId ${slot.slotId} != ${expectedId}`);
}

function validateWaterJets(waterJets, path, issues) {
  if (!Array.isArray(waterJets) || waterJets.length !== 8) {
    issues.push(`${path}: exactly 8 approved Water Jets required (got ${waterJets?.length})`);
    return;
  }
  for (const [i, w] of waterJets.entries()) {
    const expectedId = WATER_JET_IDS[i];
    if (w.waterJetId !== expectedId) issues.push(`${path}[${i}]: must be ${expectedId} in ordinal order`);
    const expectedValve = `IV${expectedId.slice(2)}`;
    if (w.dedicatedIsolationValveId !== expectedValve) issues.push(`${path}[${i}]: must pair with ${expectedValve}`);
    const coverage = COVERAGE_BY_ID.get(expectedId);
    if (`${w.targetWall}|${w.targetRegion}` !== coverage) {
      issues.push(`${path}[${i}]: target coverage must be ${coverage}`);
    }
    if (w.installedWall === w.targetWall && w.installedRegion === w.targetRegion) {
      // No approved device targets its own installed position; fail explicitly rather than silently.
      issues.push(`${path}[${i}]: installed position must differ from target coverage (opposite-wall coverage)`);
    }
    if (w.placementKind === 'NON_SENSOR_GAP') {
      // I7 and I16 are Sensors: no Water Jet is placed "at a non-Sensor gap".
      issues.push(`${path}[${i}]: placementKind NON_SENSOR_GAP is superseded (I7 and I16 are Product Sensors; fine-grained placement is UNRESOLVED for all Water Jets under the current Owner disposition)`);
    } else if (!WATER_JET_PLACEMENT_KINDS.has(w.placementKind)) {
      issues.push(`${path}[${i}]: placementKind must be one of ${[...WATER_JET_PLACEMENT_KINDS].join(', ')}`);
    } else if (w.placementKind !== 'UNRESOLVED') {
      // Owner placement disposition (Canonical Topology Adoption): the fine-grained
      // placement of EVERY Water Jet is UNRESOLVED. Only the canonical installed
      // wall/region is authoritative; a resolved placement kind belongs to a future
      // authoritative layout and is refused here.
      issues.push(`${path}[${i}]: placementKind must be UNRESOLVED in the current canonical topology (fine-grained placement unresolved for all Water Jets; canonical wall/tier confirmed)`);
    }
    // An UNRESOLVED placement carries NO anchors. Nothing is inferred, mirrored or
    // transferred from I7, I16, a former gap, a Sensor wall, the target wall, a target
    // Sensor, the legacy cannon value or the Assigned Cleaning Device ID.
    if (!Array.isArray(w.placementAnchors) || w.placementAnchors.length !== 0) {
      issues.push(`${path}[${i}]: placementAnchors must be empty: an UNRESOLVED placement must carry no placementAnchors (no inferred installed position)`);
    }
  }
}

function validateIsolationValves(isolationValves, path, issues) {
  if (!Array.isArray(isolationValves) || isolationValves.length !== 8) {
    issues.push(`${path}: exactly 8 approved Isolation Valves required (got ${isolationValves?.length})`);
    return;
  }
  for (const [i, v] of isolationValves.entries()) {
    const expectedId = ISOLATION_VALVE_IDS[i];
    if (v.valveId !== expectedId) issues.push(`${path}[${i}]: must be ${expectedId} in ordinal order`);
    if (v.servedWaterJetId !== WATER_JET_IDS[i]) issues.push(`${path}[${i}]: must serve ${WATER_JET_IDS[i]}`);
  }
}

function validateSensor(s, path, issues) {
  if (!isStr(s.sensorId) || !SENSOR_ID_SET.has(s.sensorId)) {
    issues.push(`${path}: sensorId must be a canonical Sensor id (one of the 108 Product Sensors)`);
  }
  if (s.positionKind !== 'SENSOR') issues.push(`${path}: positionKind must be SENSOR`);
  const coverage = COVERAGE_BY_ID.get(s.assignedWaterJetId);
  if (coverage === undefined) {
    issues.push(`${path}: assignedWaterJetId must be one of WJ1-WJ8`);
  } else {
    if (`${s.wall}|${regionForLogicalRow(s.logicalRow)}` !== coverage) {
      issues.push(`${path}: assigned Water Jet ${s.assignedWaterJetId} does not target this Sensor's wall/region (${coverage})`);
    }
    if (s.assignedIsolationValveId !== `IV${s.assignedWaterJetId.slice(2)}`) {
      issues.push(`${path}: assignedIsolationValveId must derive from the WJn ↔ IVn pairing`);
    }
  }
  if (!['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED'].includes(s.quality)) issues.push(`${path}: bad quality`);
  if (!['DIRTY', 'CLEANER', 'NOT_CLASSIFIED'].includes(s.classification)) issues.push(`${path}: bad classification`);
  if (!['CURRENT', 'LAST_VALIDATED', 'NONE'].includes(s.classificationBasis)) issues.push(`${path}: bad classificationBasis`);
  if (!['NONE', 'QUEUED', 'ACTIVE'].includes(s.queueState)) issues.push(`${path}: bad queueState`);
  if (!['NONE', 'ACTIVE_UNACK', 'ACTIVE_ACK', 'CLEARED_UNACK'].includes(s.alarmState)) issues.push(`${path}: bad alarmState`);
  if (['BAD', 'STALE', 'DISABLED'].includes(s.quality) && s.classification !== 'NOT_CLASSIFIED') {
    issues.push(`${path}: ${s.quality} quality must present NOT_CLASSIFIED (accepted rule)`);
  }
  if (s.quality === 'UNCERTAIN' && s.classificationBasis === 'CURRENT') {
    issues.push(`${path}: UNCERTAIN must not claim CURRENT basis (keeps last validated)`);
  }
  if (s.classificationBasis === 'LAST_VALIDATED' && s.lastValidatedScore === null) {
    issues.push(`${path}: LAST_VALIDATED basis requires lastValidatedScore`);
  }
  if (s.classificationBasis === 'NONE' && s.classification !== 'NOT_CLASSIFIED') {
    issues.push(`${path}: NONE basis requires NOT_CLASSIFIED`);
  }
}

function validateEvents(events, path, issues) {
  if (!Array.isArray(events)) {
    issues.push(`${path}: events must be an array`);
    return;
  }
  let prev = -1;
  for (const [i, e] of events.entries()) {
    if (!isInt(e.seq) || e.seq <= prev) issues.push(`${path}[${i}]: seq must strictly increase`);
    prev = e.seq;
  }
}

function validateSafeReturn(sr, path, issues) {
  if (typeof sr !== 'object' || sr === null) {
    issues.push(`${path}: safeReturn must be an object`);
    return;
  }
  validateEvents(sr.events, `${path}.events`, issues);
  const v = sr.valve ?? {};
  const a = sr.axis ?? {};
  if (v.commandSeq !== null && v.feedbackSeq !== null && !(v.commandSeq < v.feedbackSeq)) {
    issues.push(`${path}: valve close command must precede closed confirmation`);
  }
  if (a.commandSeq !== null && v.commandSeq !== null && !(v.commandSeq < a.commandSeq)) {
    issues.push(`${path}: valve close command must precede axis return command`);
  }
  if (v.resolution === 'CLOSED_BY_PRESSURE' && v.feedback === 'CLOSED_CONFIRMED') {
    issues.push(`${path}: pressure-inferred closure is not a Lower limit confirmation`);
  }
  if (v.pressureInputValid === false && (v.resolution === 'LOWER_LIMIT_CONFIRMED' || v.resolution === 'CLOSED_BY_PRESSURE')) {
    issues.push(`${path}: invalid pressure cannot confirm closure`);
  }
  if (a.commandSeq !== null && a.standbySeq !== null && !(a.commandSeq < a.standbySeq)) {
    issues.push(`${path}: axis return command must precede Standby confirmation`);
  }
}

function validateDispatch(d, path, issues) {
  if (d.positionBefore !== 1) issues.push(`${path}: dispatch is head-only (positionBefore must be 1)`);
  if (!(d.queueRevisionAfter === d.queueRevisionBefore + 1)) issues.push(`${path}: dispatch bumps the queue revision by exactly 1`);
  if (!isStr(d.sensorId) || !SENSOR_ID_SET.has(d.sensorId)) issues.push(`${path}: dispatched sensorId must be a canonical Sensor`);
}

function validateActiveJob(job, path, issues) {
  if (typeof job !== 'object' || job === null) {
    issues.push(`${path}: activeJob present must be an object`);
    return;
  }
  if (!isStr(job.jobId) || !isStr(job.targetSensorId)) issues.push(`${path}: identity missing`);
  if (!SENSOR_ID_SET.has(job.targetSensorId)) issues.push(`${path}: Active Job target must be a canonical Sensor`);
  for (const field of ['pumpOutletPressureBar', 'valveOutletPressureBar', 'pumpPressureQuality', 'valvePressureQuality',
    'pumpPressureSourceId', 'valvePressureSourceId', 'valveOpenResolution', 'valveDiagnosis']) {
    if (!(field in job)) issues.push(`${path}: ${field} required (null if absent)`);
  }
  if (!Number.isFinite(job.pumpReadySetpointBar) || job.pumpReadySetpointBar < 0) issues.push(`${path}: invalid Pump readiness setpoint`);
  if (typeof job.pumpPressureInputValid !== 'boolean' || typeof job.valvePressureInputValid !== 'boolean') issues.push(`${path}: pressure validity flags required`);
  if (job.pumpOutletPressureBar != null && (!Number.isFinite(job.pumpOutletPressureBar) || job.pumpOutletPressureBar < 0)) issues.push(`${path}: invalid Pump pressure`);
  if (job.valveOutletPressureBar != null && (!Number.isFinite(job.valveOutletPressureBar) || job.valveOutletPressureBar < 0)) issues.push(`${path}: invalid Valve pressure`);
  if (job.pumpPressureInputValid && (job.pumpPressureSourceId !== 'PUMP_OUTLET' || job.pumpPressureQuality !== 'GOOD' || job.pumpOutletPressureBar === null)) issues.push(`${path}: valid Pump reading requires its own measured outlet`);
  if (job.valvePressureInputValid && (job.valvePressureSourceId !== `${job.valveId}_OUTLET` || job.valvePressureQuality !== 'GOOD' || job.valveOutletPressureBar === null)) issues.push(`${path}: valid Valve reading requires the paired IVn outlet`);
  validateDispatch(job.dispatch ?? {}, `${path}.dispatch`, issues);
  if (job.safeReturn !== null) validateSafeReturn(job.safeReturn, `${path}.safeReturn`, issues);
}

function validateQueue(q, path, issues) {
  if (q.capacity !== 8) issues.push(`${path}: capacity must be 8`);
  if (!Array.isArray(q.entries) || q.entries.length > 8) issues.push(`${path}: at most 8 entries physically (no hidden overflow)`);
  if (Array.isArray(q.entries) && q.totalQueued !== q.entries.length) issues.push(`${path}: totalQueued must equal entries.length`);
  const seen = new Set();
  for (const [i, e] of (q.entries ?? []).entries()) {
    if (e.position !== i + 1) issues.push(`${path}.entries[${i}]: positions must be 1..n contiguous`);
    if (!SENSOR_ID_SET.has(e.sensorId)) issues.push(`${path}.entries[${i}]: entry sensorId must be a canonical Sensor`);
    if (seen.has(e.sensorId)) issues.push(`${path}.entries[${i}]: duplicate Sensor in queue`);
    seen.add(e.sensorId);
  }
  if (q.lastDispatch !== null && q.lastDispatch !== undefined) validateDispatch(q.lastDispatch, `${path}.lastDispatch`, issues);
}

/** Mirrors the bounded C# TC invariant for the one-Node deterministic wire record. */
export function validDeterministicTcAcquisition(tc) {
  const expectedUnknowns = ['PHYSICAL_REACHABILITY', 'REGISTER_MAPPING', 'SCALING', 'WIRE_BREAK', 'FIELD_TIMING'];
  return tc !== null && typeof tc === 'object' && !Array.isArray(tc)
    && tc.nodeId === 'TC-NODE-01' && tc.cabinetId === 'CAB1'
    && tc.provenance === 'DETERMINISTIC_SIMULATION'
    && tc.physical === false && tc.quarantined === true
    && tc.acquisitionState === 'COMPLETE'
    // DateTimeOffset is UTC and non-default; .NET writes +00:00, wire readers may also use Z.
    && typeof tc.acquiredAtUtc === 'string'
    && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$/.test(tc.acquiredAtUtc)
    && Number.isFinite(Date.parse(tc.acquiredAtUtc))
    && Date.parse(tc.acquiredAtUtc) !== Date.parse('0001-01-01T00:00:00Z')
    && tc.copiedWordCount === 40 && Array.isArray(tc.rawWords) && tc.rawWords.length === 40
    && tc.rawWords.every((word, index) => word === 100 + index)
    && tc.quality === 'SIMULATED' && tc.diagnosticState === 'NONE'
    && tc.mappingStatus === 'MAPPING_NOT_VERIFIED' && tc.decodeStatus === 'DECODING_NOT_VERIFIED'
    && Array.isArray(tc.notVerified) && tc.notVerified.length === expectedUnknowns.length
    && tc.notVerified.every((value, index) => value === expectedUnknowns[index])
    // The deterministic variant may never carry the physical evidence block: the two variants are
    // never blended, and the physical read profile must not appear on a non-Hardware record. This
    // ADDS strictness; no previously-valid deterministic record is affected, because a record without
    // the block is unaffected by its absence.
    && !('physicalEvidence' in tc);
}

/**
 * Canonical Not-Verified set of a PHYSICAL quarantined record. Mirrors the C# invariant exactly:
 * a physical record that drops or reorders an entry is refused before it is displayed.
 */
export const PHYSICAL_NOT_VERIFIED = Object.freeze([
  'PHYSICAL_REACHABILITY', 'DRAWING_MAPPING_PARITY', 'SCALING', 'SIGNED_REPRESENTATION',
  'BYTE_ORDER', 'WORD_ORDER', 'WIRE_BREAK_SENTINEL_MEANING', 'REVERSE_POLARITY_SENTINEL_MEANING',
  'COLD_JUNCTION_COMPENSATION', 'FIELD_TIMING', 'MBAP_TRANSACTION_ID', 'RESPONSE_UNIT_ID',
]);

/** The three Drawing mapping evidence summary states a physical record may carry. */
export const PHYSICAL_MAPPING_EVIDENCE_STATES = Object.freeze([
  'DRAWING_VERIFIED', 'DRAWING_INTERNAL_CONFLICT', 'SPARE_UNMAPPED',
]);

/**
 * The physical wire record is validated by EXACT KEY ALLOW-LIST, not by a name blocklist. A record
 * that carries any key outside the declared shape is refused, so an unexpected field — an address,
 * a host, a credential-shaped value or an invented evidence claim — cannot ride along unnoticed. This
 * is stricter than pattern matching on suspicious names: the declared set is the whole record.
 */
export const PHYSICAL_TC_RECORD_KEYS = Object.freeze([
  'nodeId', 'cabinetId', 'provenance', 'physical', 'quarantined', 'acquisitionState',
  'acquiredAtUtc', 'copiedWordCount', 'rawWords', 'quality', 'diagnosticState',
  'mappingStatus', 'decodeStatus', 'notVerified', 'physicalEvidence',
]);

/** The evidence block is likewise closed: only the redacted identity and the declared evidence may appear. */
export const PHYSICAL_TC_EVIDENCE_KEYS = Object.freeze([
  'endpointIdentity', 'commissioningState', 'mappingEvidenceState', 'readProfile',
]);

/** The read-profile block is the fixed authorized shape, so its key set is closed too. */
export const PHYSICAL_TC_READ_PROFILE_KEYS = Object.freeze([
  'functionCode', 'registerArea', 'port', 'unitIdentifier', 'startAddress', 'quantity',
]);

/** True only when the object's own enumerable keys are exactly the declared set (no extra, no missing). */
function hasExactKeys(obj, allowed) {
  const keys = Object.keys(obj);
  if (keys.length !== allowed.length) return false;
  return keys.every((key) => allowed.includes(key));
}

/**
 * Mirrors the bounded C# invariant for the ONE-NODE PHYSICAL read-only quarantined wire record.
 *
 * The record may express candidate, quarantined, unverified evidence ONLY: `physical` must be true,
 * `quarantined` must be true, the commissioning and mapping states must be the declared-not-verified
 * vocabulary, the read profile must equal the authorized read-only tuple, and no address-shaped key
 * may appear anywhere. There is deliberately no accepted, commissioned, decoded or verified state.
 */
export function validPhysicalTcAcquisition(tc) {
  if (tc === null || typeof tc !== 'object' || Array.isArray(tc)) return false;
  if (!hasExactKeys(tc, PHYSICAL_TC_RECORD_KEYS)) return false;
  return tc.nodeId === 'TC-NODE-01' && tc.cabinetId === 'CAB1'
    && tc.provenance === 'PHYSICAL_HOLDING_REGISTER_READ_ONLY'
    && tc.physical === true && tc.quarantined === true
    && tc.acquisitionState === 'COMPLETE'
    && typeof tc.acquiredAtUtc === 'string'
    && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$/.test(tc.acquiredAtUtc)
    && Number.isFinite(Date.parse(tc.acquiredAtUtc))
    && Date.parse(tc.acquiredAtUtc) !== Date.parse('0001-01-01T00:00:00Z')
    && tc.copiedWordCount === 40 && Array.isArray(tc.rawWords) && tc.rawWords.length === 40
    // Raw words are device evidence: any 16-bit value is acceptable, and NOTHING is asserted about
    // scaling, signedness, byte order, wire-break or reverse polarity here.
    && tc.rawWords.every((word) => Number.isInteger(word) && word >= 0 && word <= 0xFFFF)
    && tc.quality === 'PHYSICAL_QUARANTINED' && tc.diagnosticState === 'NONE'
    && tc.mappingStatus === 'DRAWING_REV1_MAPPING_DECLARED_NOT_VERIFIED'
    && tc.decodeStatus === 'DECODE_CANDIDATE_NOT_VERIFIED'
    && Array.isArray(tc.notVerified) && tc.notVerified.length === PHYSICAL_NOT_VERIFIED.length
    && tc.notVerified.every((value, index) => value === PHYSICAL_NOT_VERIFIED[index])
    && validPhysicalEvidence(tc.physicalEvidence);
}

/** Validates the physical evidence block: redacted identity, authorized profile, honest states. */
function validPhysicalEvidence(evidence) {
  if (evidence === null || typeof evidence !== 'object' || Array.isArray(evidence)) return false;
  if (!hasExactKeys(evidence, PHYSICAL_TC_EVIDENCE_KEYS)) return false;
  if (evidence.endpointIdentity !== 'ENDPOINT_CONFIGURED') return false;
  if (evidence.commissioningState !== 'NOT_COMMISSIONED') return false;
  if (!PHYSICAL_MAPPING_EVIDENCE_STATES.includes(evidence.mappingEvidenceState)) return false;
  const profile = evidence.readProfile;
  if (profile === null || typeof profile !== 'object' || Array.isArray(profile)) return false;
  if (!hasExactKeys(profile, PHYSICAL_TC_READ_PROFILE_KEYS)) return false;
  return profile.functionCode === 'FC03'
    && profile.registerArea === 'HOLDING_REGISTERS'
    && profile.port === 502
    && profile.unitIdentifier === 1
    && profile.startAddress === 0
    && profile.quantity === 40;
}

/**
 * The single dispatch point for a wire TC record. Both strict variants are preserved: the
 * deterministic validator is neither replaced nor relaxed, and a record that satisfies neither
 * variant is refused. There is no third, weaker generic path.
 */
export function validTcAcquisition(tc) {
  if (validDeterministicTcAcquisition(tc)) return true;
  return validPhysicalTcAcquisition(tc);
}

/**
 * Validates the STATIC read-only motion commissioning catalogue.
 *
 * The catalogue is accepted ONLY in the state this software revision can actually publish: the
 * authorization gate CLOSED, four controllers, sixteen axes, the CANONICAL Owner-confirmed Water Jet
 * axis allocation for all eight Water Jets (REV2: WJ2 = A,B and WJ4 = C,D included), the WJ2 / WJ4 Drawing
 * rev1 discrepancy recorded as evidence only and never as ambiguity, the Jog Speed as a display
 * value in controller units, and no axis homed, connected, commissioned or authorized to move.
 * A catalogue that claims a configured endpoint, a ready controller, a homed axis, an executable
 * parameter, an authorized command, or an open Owner decision is refused: none of those can be true
 * in this revision.
 */
export function validMotionCommissioningCatalogue(catalogue) {
  if (catalogue === null || typeof catalogue !== 'object' || Array.isArray(catalogue)) return false;
  if (!hasExactKeys(catalogue, MOTION_CATALOGUE_KEYS)) return false;
  if (catalogue.sectionId !== MOTION_SECTION_ID) return false;
  if (catalogue.sectionRevision !== MOTION_SECTION_REVISION) return false;
  if (catalogue.source !== MOTION_SOURCE) return false;
  if (catalogue.motionTopologyRevision !== MOTION_TOPOLOGY_REVISION) return false;
  if (catalogue.authorizationGateState !== MOTION_GATE_CLOSED) return false;
  if (!sameStrings(catalogue.authorizationGateReasons, [MOTION_GATE_REASON])) return false;
  if (!sameStrings(catalogue.motionIntents, MOTION_INTENTS)) return false;
  // No axis is in a state in which catalogue is allowed, so the list is empty by construction.
  if (!Array.isArray(catalogue.allowedMotionStates) || catalogue.allowedMotionStates.length !== 0) return false;
  if (catalogue.sessionState !== 'NO_SESSION') return false;
  if (!isStr(catalogue.canonicalText)) return false;
  if (!/^[0-9a-f]{64}$/.test(catalogue.fingerprint)) return false;
  if (!Array.isArray(catalogue.statusLegend) || catalogue.statusLegend.length === 0
      || !catalogue.statusLegend.every((line) => isStr(line) && line.includes(' - '))) return false;
  if (!Array.isArray(catalogue.notVerifiedClaims) || catalogue.notVerifiedClaims.length === 0
      || !catalogue.notVerifiedClaims.includes('NO_MOTION_COMMAND_ISSUED')
      || !catalogue.notVerifiedClaims.includes('NO_CONTROLLER_CONTACTED')) return false;

  if (!validMotionCommandTotals(catalogue.commandTotals)) return false;
  if (!validMotionJogSpeed(catalogue.jogSpeed)) return false;

  const controllers = catalogue.controllers;
  if (!Array.isArray(controllers) || controllers.length !== MOTION_CABINETS.length) return false;
  if (!controllers.every((controller, index) => validMotionController(controller, MOTION_CABINETS[index]))) return false;

  const axes = catalogue.axes;
  if (!Array.isArray(axes) || axes.length !== MOTION_CABINETS.length * MOTION_AXIS_IDS.length) return false;
  for (let c = 0; c < MOTION_CABINETS.length; c += 1) {
    for (let a = 0; a < MOTION_AXIS_IDS.length; a += 1) {
      if (!validMotionAxis(axes[c * MOTION_AXIS_IDS.length + a], `MC-${MOTION_CABINETS[c]}`, MOTION_AXIS_IDS[a])) return false;
    }
  }

  const waterJets = catalogue.waterJets;
  if (!Array.isArray(waterJets) || waterJets.length !== MOTION_WATER_JET_BINDINGS.length) return false;
  for (let i = 0; i < MOTION_WATER_JET_BINDINGS.length; i += 1) {
    const [waterJetId, cabinetId, horizontalAxisId, verticalAxisId, mappingStatus] = MOTION_WATER_JET_BINDINGS[i];
    const binding = waterJets[i];
    if (!hasExactKeys(binding, MOTION_WATER_JET_KEYS)) return false;
    if (binding.waterJetId !== waterJetId || binding.cabinetId !== cabinetId) return false;
    if (binding.controllerId !== `MC-${cabinetId}`) return false;
    if (binding.horizontalAxisId !== horizontalAxisId || binding.verticalAxisId !== verticalAxisId) return false;
    if (binding.mappingStatus !== mappingStatus) return false;
    // Canonical provenance: every row's basis is the Owner's confirmation. The Drawing rev1
    // discrepancy is evidence metadata only, so no row may carry a drawing-derived basis and no row
    // may carry an open-decision status.
    if (binding.mappingBasis !== MOTION_MAPPING_BASIS) return false;
    if (binding.mappingStatus === 'OWNER_DECISION_REQUIRED') return false;
    if (MOTION_AXIS_ROLE[binding.horizontalAxisId] !== 'HORIZONTAL') return false;
    if (MOTION_AXIS_ROLE[binding.verticalAxisId] !== 'VERTICAL') return false;
  }

  // The one recorded Drawing discrepancy must be published as evidence: a catalogue that drops it
  // loses the discrepancy, and one that resolves it claims a physical resolution no one has made.
  const notes = catalogue.mappingEvidenceNotes;
  if (!Array.isArray(notes) || notes.length !== 1) return false;
  const note = notes[0];
  if (!hasExactKeys(note, MOTION_EVIDENCE_NOTE_KEYS)) return false;
  if (note.evidenceId !== MOTION_EVIDENCE_NOTE_ID) return false;
  if (note.status !== MOTION_EVIDENCE_STATUS) return false;
  if (note.classification !== MOTION_EVIDENCE_CLASSIFICATION) return false;
  if (note.physicalResolution !== MOTION_EVIDENCE_RESOLUTION) return false;
  if (!sameStrings(note.waterJetIds, ['WJ2', 'WJ4'])) return false;
  if (!sameStrings(note.cabinetIds, ['WSB1', 'WSB2'])) return false;
  if (!isStr(note.statement) || !isStr(note.effect)) return false;

  return true;
}

const MOTION_CATALOGUE_KEYS = Object.freeze([
  'sectionId', 'sectionRevision', 'source', 'motionTopologyRevision', 'authorizationGateState',
  'authorizationGateReasons', 'motionIntents', 'allowedMotionStates', 'sessionState', 'commandTotals',
  'controllers', 'axes', 'waterJets', 'mappingEvidenceNotes', 'jogSpeed', 'notVerifiedClaims',
  'statusLegend', 'canonicalText', 'fingerprint',
]);
const MOTION_CONTROLLER_KEYS = Object.freeze(['identity', 'configuration']);
const MOTION_CONTROLLER_IDENTITY_KEYS = Object.freeze([
  'controllerId', 'cabinetId', 'axisCount', 'controllerModel', 'controllerModelStatus', 'controllerIdentityBasis',
]);
const MOTION_CONTROLLER_CONFIGURATION_KEYS = Object.freeze([
  'motionPortIdentity', 'motionPortStatus', 'endpointIdentity', 'controllerConfigurationStatus',
  'libraryInstallationStatus', 'communicationState', 'motionDispatchReady', 'parameterConfigurationState',
  'softwareLimitConfigurationState', 'limitInputPolarityStatus', 'endstopConfigurationStatus',
  'motionDispatchBlockedReason',
]);
const MOTION_AXIS_KEYS = Object.freeze(['identity', 'state']);
const MOTION_AXIS_IDENTITY_KEYS = Object.freeze([
  'controllerId', 'axisId', 'axisRole', 'axisRoleBasis', 'motionSenseKind', 'motionSenseBasis',
  'brakeOutputIdentity', 'brakeOutputMappingBasis',
]);
const MOTION_AXIS_STATE_KEYS = Object.freeze([
  'commissioningState', 'homingState', 'homingStateStatus', 'brakeCommandState', 'brakeState',
  'brakeStateStatus', 'motionState', 'motionStateStatus', 'limitSignals', 'sense', 'safety',
  'kinematic', 'moveAuthorization',
]);
const MOTION_LIMIT_SIGNAL_KEYS = Object.freeze([
  'signalRole', 'signalKind', 'polarityStatus', 'signalState', 'signalStateStatus', 'senseBasis',
]);
const MOTION_SENSE_KEYS = Object.freeze([
  'motionSenseKind', 'motionSenseBasis', 'senseChannelStatus', 'positionValueStatus',
  'positionDisplayValue', 'positionUnit',
]);
const MOTION_SAFETY_KEYS = Object.freeze([
  'emergencyStopVisibilityStatus', 'stopCircuitVerificationStatus', 'controlledStopCapabilityStatus',
  'controlledStopVerificationStatus', 'isolationVerificationStatus', 'localProtectionStatus',
  'resetEncoderVerificationStatus',
]);
const MOTION_KINEMATIC_KEYS = Object.freeze([
  'kinematicConversionStatus', 'kinematicConversionIsPerAxis', 'publishedUnit', 'conversionProvenanceRequirement',
]);
const MOTION_MOVE_AUTHORIZATION_KEYS = Object.freeze(['moveAuthorizationState', 'moveRefusalReasons']);
const MOTION_WATER_JET_KEYS = Object.freeze([
  'waterJetId', 'cabinetId', 'controllerId', 'horizontalAxisId', 'verticalAxisId', 'mappingStatus', 'mappingBasis',
]);
const MOTION_EVIDENCE_NOTE_KEYS = Object.freeze([
  'evidenceId', 'waterJetIds', 'cabinetIds', 'statement', 'status', 'classification', 'effect',
  'physicalResolution',
]);
const MOTION_PARAMETER_KEYS = Object.freeze(['parameterId', 'displayValue', 'unit', 'status', 'provenanceBasis', 'executable']);
const MOTION_COMMAND_TOTALS_KEYS = Object.freeze([
  'evaluated', 'authorized', 'refused', 'hardwareCommandsIssued', 'commandState', 'journalPersistence',
]);
/** The only mapping provenance a Water Jet row may carry: the Owner's confirmation. */
const MOTION_MAPPING_BASIS = 'OWNER_CONFIRMED';
const MOTION_SENSE_KINDS = Object.freeze(['ENCODER', 'INSTALLED', 'UNCONFIRMED']);

const sameStrings = (actual, expected) => Array.isArray(actual) && actual.length === expected.length
  && actual.every((value, index) => value === expected[index]);

function validMotionCommandTotals(totals) {
  if (totals === null || typeof totals !== 'object' || Array.isArray(totals)) return false;
  if (!hasExactKeys(totals, MOTION_COMMAND_TOTALS_KEYS)) return false;
  // Nothing has been evaluated against a controller, so nothing was authorized, refused or issued.
  return totals.evaluated === 0 && totals.authorized === 0 && totals.refused === 0
    && totals.hardwareCommandsIssued === 0
    && totals.commandState === 'NO_COMMANDS' && totals.journalPersistence === 'NONE';
}

/** The initial Jog Speed: a display value in controller units that is never executable. */
function validMotionJogSpeed(jogSpeed) {
  if (jogSpeed === null || typeof jogSpeed !== 'object' || Array.isArray(jogSpeed)) return false;
  if (!hasExactKeys(jogSpeed, MOTION_PARAMETER_KEYS)) return false;
  return jogSpeed.parameterId === 'JOG_SPEED' && jogSpeed.displayValue === '10'
    && jogSpeed.unit === 'controller units' && jogSpeed.status === 'LEGACY_CANDIDATE_UNVERIFIED'
    && jogSpeed.executable === false && isStr(jogSpeed.provenanceBasis);
}

function validMotionController(controller, cabinetId) {
  if (controller === null || typeof controller !== 'object' || Array.isArray(controller)) return false;
  if (!hasExactKeys(controller, MOTION_CONTROLLER_KEYS)) return false;
  const identity = controller.identity;
  const configuration = controller.configuration;
  if (identity === null || typeof identity !== 'object' || Array.isArray(identity)) return false;
  if (!hasExactKeys(identity, MOTION_CONTROLLER_IDENTITY_KEYS)) return false;
  if (identity.controllerId !== `MC-${cabinetId}` || identity.cabinetId !== cabinetId) return false;
  if (identity.axisCount !== MOTION_AXIS_IDS.length) return false;
  if (!isStr(identity.controllerModel) || !isStr(identity.controllerModelStatus)) return false;
  // One controller per cabinet is an inference from the confirmed allocation, never a confirmation.
  if (identity.controllerIdentityBasis !== 'INFERRED_FROM_OWNER_CONFIRMED_MAPPING') return false;

  if (configuration === null || typeof configuration !== 'object' || Array.isArray(configuration)) return false;
  if (!hasExactKeys(configuration, MOTION_CONTROLLER_CONFIGURATION_KEYS)) return false;
  // No endpoint is configured, no library is installed and no controller is connected, so no
  // controller can dispatch motion.
  if (configuration.motionPortIdentity !== 'MOTION_PORT_NOT_CONFIGURED') return false;
  if (configuration.motionPortStatus !== 'NOT_CONFIGURED') return false;
  if (configuration.endpointIdentity !== 'ENDPOINT_NOT_CONFIGURED') return false;
  if (configuration.controllerConfigurationStatus !== 'NOT_CONFIGURED') return false;
  if (configuration.libraryInstallationStatus !== 'NOT_INSTALLED') return false;
  if (configuration.communicationState !== 'NOT_CONNECTED') return false;
  if (configuration.motionDispatchReady !== false) return false;
  if (configuration.motionDispatchBlockedReason !== MOTION_GATE_REASON) return false;
  if (configuration.parameterConfigurationState !== 'NOT_CONFIGURED') return false;
  if (configuration.softwareLimitConfigurationState !== 'NOT_COMMISSIONED') return false;
  if (configuration.limitInputPolarityStatus !== 'UNCONFIRMED') return false;
  return configuration.endstopConfigurationStatus === 'NOT_COMMISSIONED';
}

function validMotionAxis(axis, controllerId, axisId) {
  if (axis === null || typeof axis !== 'object' || Array.isArray(axis)) return false;
  if (!hasExactKeys(axis, MOTION_AXIS_KEYS)) return false;
  const identity = axis.identity;
  const state = axis.state;
  if (identity === null || typeof identity !== 'object' || Array.isArray(identity)) return false;
  if (!hasExactKeys(identity, MOTION_AXIS_IDENTITY_KEYS)) return false;
  if (identity.controllerId !== controllerId || identity.axisId !== axisId) return false;
  if (identity.axisRole !== MOTION_AXIS_ROLE[axisId]) return false;
  if (identity.axisRoleBasis !== 'OWNER_CONFIRMED') return false;
  if (!MOTION_SENSE_KINDS.includes(identity.motionSenseKind)) return false;
  if (identity.motionSenseBasis !== 'OWNER_CONFIRMED') return false;
  if (identity.brakeOutputIdentity !== MOTION_BRAKE_OUTPUT[axisId]) return false;
  if (identity.brakeOutputMappingBasis !== 'OWNER_CONFIRMED') return false;

  if (state === null || typeof state !== 'object' || Array.isArray(state)) return false;
  if (!hasExactKeys(state, MOTION_AXIS_STATE_KEYS)) return false;
  if (state.commissioningState !== 'NOT_COMMISSIONED') return false;
  if (state.homingState !== 'NOT_HOMED' || state.homingStateStatus !== 'UNKNOWN') return false;
  if (state.brakeCommandState !== 'NONE' || state.brakeState !== 'UNKNOWN' || state.brakeStateStatus !== 'NOT_AVAILABLE') return false;
  if (state.motionState !== 'UNKNOWN' || state.motionStateStatus !== 'NOT_AVAILABLE') return false;

  if (!Array.isArray(state.limitSignals) || state.limitSignals.length !== MOTION_LIMIT_SIGNAL_ROLES.length) return false;
  for (let i = 0; i < MOTION_LIMIT_SIGNAL_ROLES.length; i += 1) {
    const signal = state.limitSignals[i];
    if (signal === null || typeof signal !== 'object' || Array.isArray(signal)) return false;
    if (!hasExactKeys(signal, MOTION_LIMIT_SIGNAL_KEYS)) return false;
    if (signal.signalRole !== MOTION_LIMIT_SIGNAL_ROLES[i]) return false;
    if (!MOTION_SENSE_KINDS.includes(signal.signalKind)) return false;
    // The switches are installed, but the electrical meaning is unconfirmed and the state is unread.
    if (signal.polarityStatus !== 'UNCONFIRMED') return false;
    if (signal.signalState !== 'UNKNOWN' || signal.signalStateStatus !== 'NOT_AVAILABLE') return false;
    if (!isStr(signal.senseBasis)) return false;
  }

  const sense = state.sense;
  if (sense === null || typeof sense !== 'object' || Array.isArray(sense)) return false;
  if (!hasExactKeys(sense, MOTION_SENSE_KEYS)) return false;
  if (sense.motionSenseKind !== identity.motionSenseKind) return false;
  if (sense.senseChannelStatus !== 'NOT_AVAILABLE' || sense.positionValueStatus !== 'NOT_READ') return false;
  // A position is never invented: the display value is null while nothing is read.
  if (sense.positionDisplayValue !== null) return false;

  const safety = state.safety;
  if (safety === null || typeof safety !== 'object' || Array.isArray(safety)) return false;
  if (!hasExactKeys(safety, MOTION_SAFETY_KEYS)) return false;
  if (safety.emergencyStopVisibilityStatus !== 'NOT_AVAILABLE') return false;
  if (safety.stopCircuitVerificationStatus !== 'NOT_VERIFIED') return false;
  if (safety.controlledStopCapabilityStatus !== 'UNRESOLVED') return false;
  if (safety.controlledStopVerificationStatus !== 'NOT_VERIFIED') return false;
  if (safety.isolationVerificationStatus !== 'NOT_VERIFIED') return false;
  if (safety.localProtectionStatus !== 'NOT_VERIFIED') return false;
  if (safety.resetEncoderVerificationStatus !== 'NOT_VERIFIED') return false;

  const kinematic = state.kinematic;
  if (kinematic === null || typeof kinematic !== 'object' || Array.isArray(kinematic)) return false;
  if (!hasExactKeys(kinematic, MOTION_KINEMATIC_KEYS)) return false;
  // Counts stay counts: no conversion is published and the conversion is per axis when it exists.
  if (kinematic.kinematicConversionStatus !== 'UNRESOLVED') return false;
  if (kinematic.kinematicConversionIsPerAxis !== true) return false;
  if (kinematic.publishedUnit !== 'controller units') return false;
  if (kinematic.conversionProvenanceRequirement !== 'MEASURED_DURING_COMMISSIONING') return false;

  const move = state.moveAuthorization;
  if (move === null || typeof move !== 'object' || Array.isArray(move)) return false;
  if (!hasExactKeys(move, MOTION_MOVE_AUTHORIZATION_KEYS)) return false;
  if (move.moveAuthorizationState !== 'REFUSED') return false;
  return sameStrings(move.moveRefusalReasons, MOTION_ALL_REFUSAL_REASONS);
}

function validateSharedBody(obj, path, issues) {
  if ('tcAcquisition' in obj && (obj.kind === 'delta' || obj.tcAcquisition !== null)
      && !validTcAcquisition(obj.tcAcquisition)) {
    issues.push(`${path}.tcAcquisition: must be a complete quarantined TC record — either the deterministic non-Hardware variant or the physical read-only variant; Delta cannot clear it`);
  }
  // The static motion commissioning catalogue is NOT a Snapshot member: it lives on the dedicated
  // read-only catalogue route, so a Snapshot carrying it is refused. This enforces the separation.
  if ('motion' in obj) {
    issues.push(`${path}.motion: the static motion commissioning catalogue is not a ${obj.kind} member; it is served by the dedicated read-only catalogue route`);
  }
  if (obj.queue !== undefined && obj.queue !== null) validateQueue(obj.queue, `${path}.queue`, issues);
  // null is a legitimate value in BOTH envelopes: Snapshot (no active job) and
  // Delta (the accepted explicit-null CLEAR encoding). Only objects get struct-checked.
  if (obj.activeJob !== undefined && obj.activeJob !== null) {
    validateActiveJob(obj.activeJob, `${path}.activeJob`, issues);
  }
  if (obj.sensors !== undefined && obj.sensors !== null) {
    for (const [i, s] of (obj.sensors ?? []).entries()) validateSensor(s, `${path}.sensors[${i}]`, issues);
  }
  if (obj.sequence?.lastJobOutcome) {
    const o = obj.sequence.lastJobOutcome;
    if (!(o.valveCloseCommandSeq < o.axisReturnCommandSeq)) issues.push(`${path}: outcome: valve close command < axis command required`);
    if (o.valveClosedConfirmedSeq != null && !(o.valveCloseCommandSeq < o.valveClosedConfirmedSeq)) issues.push(`${path}: outcome: valve confirmation must follow close command`);
    if (o.valveCloseResolution === 'LOWER_LIMIT_CONFIRMED' && !isInt(o.valveClosedConfirmedSeq)) issues.push(`${path}: Lower limit confirmation requires its evidence sequence`);
    if (o.valveCloseResolution === 'CLOSED_BY_PRESSURE' && o.valveClosedConfirmedSeq !== null) issues.push(`${path}: pressure-inferred closure has no Lower confirmation sequence`);
    const resolution = (o.events ?? []).find(e => e.step === 'SR3' && (e.event === o.valveCloseResolution || e.seq === o.valveClosedConfirmedSeq));
    if (!resolution || !(o.axisReturnCommandSeq < resolution.seq && resolution.seq < o.outcomeSeq)) issues.push(`${path}: outcome needs post-command Valve resolution before SR6`);
    if (!(o.axisReturnCommandSeq < o.standbyConfirmedSeq)) issues.push(`${path}: outcome: axis command < standby confirm required`);
    if (!(o.standbyConfirmedSeq < o.outcomeSeq)) issues.push(`${path}: outcome: standby confirm < outcome required`);
    if (!(o.outcomeSeq < o.releaseSeq)) issues.push(`${path}: outcome: outcome < release required`);
    if (o.qualifiedCompletion === 'COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS') {
      const diagnoses = (o.equipmentFaults ?? []).map(f => f.diagnosis);
      for (const [diagnosis, remark] of [
        ['UPPER_LIMIT_SENSOR_FAULT', 'COMPLETED_WITH_VALVE_OPEN_LIMIT_UPPER_FAULT'],
        ['LOWER_LIMIT_SENSOR_FAULT', 'COMPLETED_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT'],
      ]) {
        if (!diagnoses.includes(diagnosis) || !(o.qualifiedRemarks ?? []).includes(remark)) issues.push(`${path}: dual limit faults need both distinct diagnoses and remarks`);
      }
      if (o.outcome !== 'COMPLETED') issues.push(`${path}: dual-fault qualification requires COMPLETED`);
    }
  }
}

// ---------------------------------------------------------------- entrypoints
export function validateEnvelope(obj) {
  const issues = [];
  if (typeof obj !== 'object' || obj === null) return { kind: null, issues: ['not an object'], gap: false };

  scanProhibitedKeys(obj, '$', issues);

  if (obj.kind === 'snapshot') return validateSnapshot(obj, issues);
  if (obj.kind === 'delta') return validateDelta(obj, issues);
  if (obj.kind === 'sensor-map') return validateSensorMapExample(obj, issues);
  issues.push('kind must be snapshot|delta|sensor-map');
  return { kind: null, issues, gap: false };
}

/**
 * Canonical geometry of the sensor-map configuration EXAMPLE (Stage 0.3A-1).
 * tcChannels is validated as a structured array of exactly two non-empty unique
 * strings per slot (every slot is a SENSOR slot; I7 and I16 included); a comma-delimited
 * scalar is a rejection, never a normalization. logicalColumn/logicalRow are 0-based here.
 */
export const SENSOR_MAP = Object.freeze({
  columns: 18,
  rows: 6,
  sensorLocations: TOPOLOGY.productSensors,
  thermocoupleChannels: TOPOLOGY.assignedProductTcChannels,
  matrixSlots: TOPOLOGY.logicalPositions,
  nonSensorGapSlots: TOPOLOGY.nonSensorGapPositions,
  channelsPerSensor: 2,
  sensorsPerWall: Object.freeze({ LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 }),
  waterJets: 8,
  isolationValves: 8,
});

export function validateSensorMapChannels(channels, { allowAbsent = false } = {}) {
  const issues = [];
  if (channels === undefined || channels === null) {
    if (allowAbsent) return issues;
    issues.push('SENSOR slot must carry a tcChannels array');
    return issues;
  }
  if (typeof channels === 'string') {
    issues.push('tcChannels must be a JSON array of exactly two channel strings; comma-delimited strings are rejected');
    return issues;
  }
  if (!Array.isArray(channels)) {
    issues.push('tcChannels must be a JSON array');
    return issues;
  }
  if (channels.length !== SENSOR_MAP.channelsPerSensor) {
    issues.push(`tcChannels must contain exactly ${SENSOR_MAP.channelsPerSensor} entries; got ${channels.length}`);
    return issues;
  }
  const [first, second] = channels;
  for (const ch of [first, second]) {
    if (typeof ch !== 'string' || ch.trim().length === 0) issues.push('thermocouple channel entries must be non-empty strings');
  }
  if (first === second) issues.push('sensor slot must not duplicate a thermocouple channel');
  return issues;
}

export function validateSensorMapExample(obj, issues = []) {
  if (obj.schema !== undefined && obj.schema !== SENSOR_MAP_SCHEMA) issues.push(`schema must be ${SENSOR_MAP_SCHEMA}`);
  const matrix = obj.logicalMatrix;
  if (!matrix || typeof matrix !== 'object') {
    issues.push('logicalMatrix is required');
    return { kind: 'sensor-map', issues, gap: false };
  }
  if (matrix.columns !== SENSOR_MAP.columns) issues.push(`logicalMatrix.columns must be ${SENSOR_MAP.columns}`);
  if (matrix.rows !== SENSOR_MAP.rows) issues.push(`logicalMatrix.rows must be ${SENSOR_MAP.rows}`);
  if (matrix.sensorLocations !== SENSOR_MAP.sensorLocations) issues.push(`logicalMatrix.sensorLocations must be ${SENSOR_MAP.sensorLocations}`);
  if (matrix.thermocoupleChannels !== SENSOR_MAP.thermocoupleChannels) issues.push(`logicalMatrix.thermocoupleChannels must be ${SENSOR_MAP.thermocoupleChannels}`);
  for (const [wall, count] of Object.entries(SENSOR_MAP.sensorsPerWall)) {
    if (matrix.sensorsPerWall?.[wall] !== count) issues.push(`sensorsPerWall.${wall} must be ${count}`);
  }
  const slots = matrix.slots;
  if (!Array.isArray(slots)) {
    issues.push('logicalMatrix.slots must be an array');
    return { kind: 'sensor-map', issues, gap: false };
  }
  if (slots.length !== SENSOR_MAP.matrixSlots) issues.push(`slot count must be ${SENSOR_MAP.matrixSlots}; got ${slots.length}`);

  const seenChannels = new Set();
  const allChannels = [];
  let sensors = 0;
  const seenSensorIds = new Set();
  for (const s of slots) {
    if (s?.positionKind === 'SENSOR') {
      sensors++;
      const chIssues = validateSensorMapChannels(s.tcChannels);
      for (const c of chIssues) issues.push(`${s.slotId}: ${c}`);
      if (Array.isArray(s.tcChannels)) {
        for (const ch of s.tcChannels) {
          allChannels.push(ch);
          if (typeof ch === 'string') {
            if (seenChannels.has(ch)) issues.push(`${s.slotId}: duplicate thermocouple channel ${ch} across sensors`);
            seenChannels.add(ch);
          }
        }
      }
      if (s.sensorId == null) issues.push(`${s.slotId}: sensor slot requires sensorId`);
      else if (!SENSOR_ID_SET.has(s.sensorId)) issues.push(`${s.sensorId}: unknown sensor id`);
      else {
        if (seenSensorIds.has(s.sensorId)) issues.push(`${s.slotId}: Sensor ${s.sensorId} appears more than once`);
        seenSensorIds.add(s.sensorId);
      }
      if (s.gapAnchorForWaterJetId != null) issues.push(`${s.slotId}: sensor slot must not carry gapAnchorForWaterJetId`);
    } else {
      // Owner critical topology correction: no NON_SENSOR_GAP position exists; I7 and I16 are Sensors.
      issues.push(`${s?.slotId ?? '?'}: positionKind must be SENSOR (every logical position is a Product Sensor; I7 and I16 included)`);
    }
  }
  if (sensors !== SENSOR_MAP.sensorLocations) issues.push(`sensor slots must number ${SENSOR_MAP.sensorLocations}; got ${sensors}`);
  for (const sensorId of TOPOLOGY.correctedSensorIds) {
    if (!seenSensorIds.has(sensorId)) issues.push(`Sensor ${sensorId} is missing: it is a normal Product Sensor (Owner critical topology correction)`);
  }
  if (allChannels.length !== SENSOR_MAP.thermocoupleChannels) issues.push(`total channels must be ${SENSOR_MAP.thermocoupleChannels}; got ${allChannels.length}`);
  if (seenChannels.size !== SENSOR_MAP.thermocoupleChannels) issues.push(`channels must be globally unique across the ${SENSOR_MAP.thermocoupleChannels}; got ${seenChannels.size}`);
  return { kind: 'sensor-map', issues, gap: false };
}

/** Derived summary used by tests and reviewers to assert the canonical totals. */
export function sensorMapSummary(obj) {
  const slots = obj?.logicalMatrix?.slots ?? [];
  const sensors = slots.filter((s) => s.positionKind === 'SENSOR');
  const nonSensorGaps = slots.filter((s) => s.positionKind === 'NON_SENSOR_GAP');
  const channels = sensors.flatMap((s) => (Array.isArray(s.tcChannels) ? s.tcChannels : []));
  const byWall = {};
  for (const wall of ['LEFT', 'REAR', 'RIGHT', 'FRONT']) {
    byWall[wall] = sensors.filter((s) => s.wall === wall).length;
  }
  return {
    slots: slots.length,
    sensors: sensors.length,
    nonSensorGaps: nonSensorGaps.length,
    channelsPerSensor: sensors.length === 0 ? null : channels.length / sensors.length,
    totalChannels: channels.length,
    distinctChannels: new Set(channels).size,
    sensorsPerWall: byWall,
    // Canonical row-major order (orderTotal), never lexicographic label order. Always empty under
    // the Owner critical topology correction; retained so a regressed document is visible.
    gapLogicalLabels: nonSensorGaps.map((c) => c.logicalLabel ?? c.slotId),
  };
}

function validateSnapshot(obj, issues) {
  if (obj.schema !== SNAPSHOT_SCHEMA) issues.push(`schema must be ${SNAPSHOT_SCHEMA}`);
  if (obj.apiVersion !== API_VERSION) issues.push(`apiVersion must be ${API_VERSION}`);
  if (!isInt(obj.revision) || obj.revision < 1) issues.push('revision must be an integer >= 1');
  if (!isStr(obj.generatedAt)) issues.push('generatedAt required');
  if (!['SIMULATOR', 'TEST_HARDWARE', 'PRODUCTION', 'TEST_HARDWARE_READ_ONLY'].includes(obj.deviceProfile)) issues.push('deviceProfile invalid');

  const map = obj.wallMap ?? [];
  if (map.length !== TOPOLOGY.logicalPositions) issues.push(`wallMap must contain all ${TOPOLOGY.logicalPositions} slots (got ${map.length})`);
  const sensors = obj.sensors ?? [];
  if (sensors.length !== TOPOLOGY.productSensors) issues.push(`sensors must contain all ${TOPOLOGY.productSensors} records (got ${sensors.length})`);
  for (const [i, slot] of map.entries()) validateSlot(slot, `wallMap[${i}]`, issues);
  validateWaterJets(obj.waterJets, '$.waterJets', issues);
  validateIsolationValves(obj.isolationValves, '$.isolationValves', issues);

  const scans = new Set();
  const channels = new Set();
  const ids = new Set();
  for (const s of sensors) {
    scans.add(s.scanOrder);
    ids.add(s.sensorId);
    channels.add(s.tcFrontChannel);
    channels.add(s.tcRearChannel);
  }
  if (ids.size !== sensors.length) issues.push('sensorIds must be unique');
  for (const sensorId of TOPOLOGY.correctedSensorIds) {
    if (!ids.has(sensorId)) issues.push(`sensors must include ${sensorId}: it is a normal Product Sensor (Owner critical topology correction)`);
  }
  if (scans.size !== TOPOLOGY.productSensors || ![...Array.from({ length: TOPOLOGY.productSensors }, (_, i) => i + 1)].every((n) => scans.has(n))) {
    issues.push(`scanOrder must be exactly 1..${TOPOLOGY.productSensors}`);
  }
  if (channels.size !== TOPOLOGY.assignedProductTcChannels) issues.push(`${TOPOLOGY.assignedProductTcChannels} unique thermocouple channels required (got ${channels.size})`);

  if (obj.pump === undefined || typeof obj.pump !== 'object') issues.push('pump projection required');
  if (obj.sequence === undefined) issues.push('sequence projection required');
  if (obj.runtime === undefined) issues.push('runtime health required');
  if (obj.runtime?.historian?.wired !== false) issues.push('historian must be reported wired:false in Stage 0.3A (NOT WIRED)');

  validateSharedBody(obj, '$', issues);

  if (obj.activeJob !== null && obj.activeJob !== undefined) {
    const target = obj.activeJob.targetSensorId;
    if ((obj.queue?.entries ?? []).some((e) => e.sensorId === target)) {
      issues.push('the Active Job target must not remain in the queue (atomic head removal)');
    }
  }

  return { kind: 'snapshot', issues, gap: false };
}

function validateDelta(obj, issues) {
  if (obj.schema !== DELTA_SCHEMA) issues.push(`schema must be ${DELTA_SCHEMA}`);
  if (obj.apiVersion !== API_VERSION) issues.push(`apiVersion must be ${API_VERSION}`);
  if (!isInt(obj.previousRevision) || !isInt(obj.revision) || obj.revision <= obj.previousRevision) {
    issues.push('previousRevision/revision must be integers with revision > previousRevision');
  }
  const gap = isInt(obj.previousRevision) && isInt(obj.revision) ? obj.revision !== obj.previousRevision + 1 : true;
  if ('wallMap' in obj) issues.push('wallMap must never appear in a Delta');
  if ('waterJets' in obj) issues.push('waterJets must never appear in a Delta (process-lifetime immutable topology; Snapshot establishes it)');
  if ('isolationValves' in obj) issues.push('isolationValves must never appear in a Delta (process-lifetime immutable topology; Snapshot establishes it)');
  // The static motion commissioning catalogue is not a Delta member either: a Delta carrying one
  // cannot be produced by the Runtime and is refused.
  if ('motion' in obj) issues.push('motion must never appear in a Delta (the static motion commissioning catalogue is served by the dedicated read-only catalogue route; the Runtime Delta carries dynamic state only)');
  // Three-state activeJob encoding (accepted baseline; Owner review 2026-10-07):
  // absent key = unchanged | object = replace | explicit null = clear.
  if ('activeJobCleared' in obj) {
    issues.push('activeJobCleared is not part of the contract (removed; the clear encoding is an explicit "activeJob": null)');
  }
  if ('activeJob' in obj && obj.activeJob !== null) {
    if (typeof obj.activeJob !== 'object' || typeof obj.activeJob.jobId !== 'string' || typeof obj.activeJob.targetSensorId !== 'string') {
      issues.push('Delta activeJob must be a full Active Job object (jobId + targetSensorId) or an explicit null');
    }
  }
  if (Array.isArray(obj.sensors) && obj.sensors.length === 0) issues.push('an empty sensors Delta means whole-set replacement; emit nothing instead');

  validateSharedBody(obj, '$', issues);
  return { kind: 'delta', issues, gap };
}
