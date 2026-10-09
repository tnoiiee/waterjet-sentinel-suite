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
const GAP_SLOTS = [
  ['I7', 'WJ3', 5, 7],
  ['I16', 'WJ1', 5, 16],
];

/** Approved target coverage: assignment follows the TARGET wall/region (legacy cleaning-device ordinal maps directly to WJn). */
export const TARGET_COVERAGE = [
  ['REAR', 'LOWER', 'WJ1'],
  ['RIGHT', 'LOWER', 'WJ2'],
  ['FRONT', 'LOWER', 'WJ3'],
  ['LEFT', 'LOWER', 'WJ4'],
  ['REAR', 'UPPER', 'WJ5'],
  ['RIGHT', 'UPPER', 'WJ6'],
  ['FRONT', 'UPPER', 'WJ7'],
  ['LEFT', 'UPPER', 'WJ8'],
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
      if (GAP_SLOTS.some(([, , r, c]) => r === row && c === col)) continue;
      ids.push(sensorIdFor(row, col));
    }
  }
  return ids;
}

const SENSOR_ID_SET = new Set(canonicalSensorIds());
const WATER_JET_IDS = TARGET_COVERAGE.map(([, , id]) => id);
const ISOLATION_VALVE_IDS = WATER_JET_IDS.map((id) => `IV${id.slice(2)}`);

// ---------------------------------------------------------------- helpers
const isInt = (n) => Number.isInteger(n);
const isStr = (s) => typeof s === 'string' && s.length > 0;

const PROHIBITED_KEY = new RegExp(
  '^(ip|host|port|regi' + 'ster|coordi' + 'nate|travelLim' + 'it|tagLis' + 't|connectio' + 'nString|pass' + 'word|tok' + 'en|sec' + 'ret)', 'i');

function scanProhibitedKeys(node, path, issues, depth = 0) {
  if (depth > 12 || node === null || typeof node !== 'object') return;
  if (Array.isArray(node)) {
    for (const [i, item] of node.entries()) scanProhibitedKeys(item, `${path}[${i}]`, issues, depth + 1);
    return;
  }
  for (const [key, value] of Object.entries(node)) {
    // 'host' is the health-payload host LABEL (never a network host); allowed.
    if (PROHIBITED_KEY.test(key) && key !== 'host') {
      issues.push(`${path}.${key}: prohibited production-scope key name`);
    }
    scanProhibitedKeys(value, `${path}.${key}`, issues, depth + 1);
  }
}

function validateSlot(slot, path, issues) {
  if (!isStr(slot.slotId)) issues.push(`${path}.slotId`);
  if (slot.positionKind === 'NON_SENSOR_GAP') {
    if (slot.sensorId !== null) issues.push(`${path}: NON_SENSOR_GAP position carries sensorId`);
    if (slot.gapAnchorForWaterJetId === null || slot.gapAnchorForWaterJetId === undefined) {
      issues.push(`${path}: NON_SENSOR_GAP position requires gapAnchorForWaterJetId`);
    } else {
      const match = GAP_SLOTS.find(([, anchor, r, c]) => anchor === slot.gapAnchorForWaterJetId && r === slot.logicalRow && c === slot.logicalColumn);
      if (!match) issues.push(`${path}: gap anchor must match the I7/WJ3 or I16/WJ1 position`);
    }
  } else if (slot.positionKind === 'SENSOR') {
    if (slot.gapAnchorForWaterJetId !== null) issues.push(`${path}: SENSOR slot carries gapAnchorForWaterJetId`);
    if (!SENSOR_ID_SET.has(slot.sensorId)) issues.push(`${path}: sensorId not in canonical set`);
  } else {
    issues.push(`${path}: bad positionKind`);
  }
  const group = WALL_GROUPS.find(([, f, l]) => slot.logicalColumn >= f && slot.logicalColumn <= l);
  if (!group || group[0] !== slot.wall) issues.push(`${path}: wall/column mismatch`);
  if (slot.wallColumn !== slot.logicalColumn - (group ? group[1] : 1) + 1) issues.push(`${path}: wallColumn mismatch`);
  if (slot.wallRow !== slot.logicalRow) issues.push(`${path}: wallRow must equal logicalRow`);
  const expectedId = `SLOT-R${slot.logicalRow}-C${String(slot.logicalColumn).padStart(2, '0')}`;
  if (slot.slotId !== expectedId) issues.push(`${path}: slotId ${slot.slotId} != ${expectedId}`);
  const isGapSlot = GAP_SLOTS.some(([, , r, c]) => r === slot.logicalRow && c === slot.logicalColumn);
  if (isGapSlot !== (slot.positionKind === 'NON_SENSOR_GAP')) issues.push(`${path}: NON_SENSOR_GAP position mismatch (I7/I16 only)`);
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
    if (!Array.isArray(w.placementAnchors) || w.placementAnchors.length === 0 || !w.placementAnchors.every(isStr)) {
      issues.push(`${path}[${i}]: placementAnchors must be a non-empty string array`);
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
    issues.push(`${path}: sensorId must be a canonical Sensor id (a gap position is never a Sensor)`);
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

function validateSharedBody(obj, path, issues) {
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
 * strings per SENSOR slot, absent on NON_SENSOR_GAP slots; a comma-delimited scalar is a
 * rejection, never a normalization. logicalColumn/logicalRow are 0-based here.
 */
export const SENSOR_MAP = Object.freeze({
  columns: 18,
  rows: 6,
  sensorLocations: 106,
  thermocoupleChannels: 212,
  matrixSlots: 108,
  nonSensorGapSlots: 2,
  channelsPerSensor: 2,
  sensorsPerWall: Object.freeze({ LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 }),
  gapLogicalRow: 5,
  gapLogicalColumns: Object.freeze([7, 16]),
  waterJets: 8,
  isolationValves: 8,
});

export function validateSensorMapChannels(channels, { positionKind = 'SENSOR', allowAbsent = false } = {}) {
  const issues = [];
  if (channels === undefined || channels === null) {
    if (allowAbsent || positionKind === 'NON_SENSOR_GAP') return issues;
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
  if (positionKind === 'NON_SENSOR_GAP') {
    if (channels.length > 0) issues.push('NON_SENSOR_GAP slot must not carry thermocouple channels');
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
  let nonSensorGaps = 0;
  for (const s of slots) {
    if (s?.positionKind === 'NON_SENSOR_GAP') {
      nonSensorGaps++;
      issues.push(...validateSensorMapChannels(s.tcChannels, { positionKind: 'NON_SENSOR_GAP', allowAbsent: true }));
      if (s.sensorId != null) issues.push(`${s.slotId}: NON_SENSOR_GAP slot must not carry a sensorId`);
      const match = GAP_SLOTS.find(([, anchor, r, c]) => anchor === s.gapAnchorForWaterJetId && r === s.logicalRow && c === s.logicalColumn);
      if (s.gapAnchorForWaterJetId == null || !match) issues.push(`${s.slotId}: NON_SENSOR_GAP slot requires its gap anchor (I7 → WJ3, I16 → WJ1)`);
    } else if (s?.positionKind === 'SENSOR') {
      sensors++;
      const chIssues = validateSensorMapChannels(s.tcChannels, { positionKind: 'SENSOR' });
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
      if (s.gapAnchorForWaterJetId != null) issues.push(`${s.slotId}: sensor slot must not carry gapAnchorForWaterJetId`);
    } else {
      issues.push(`${s?.slotId ?? '?'}: positionKind must be SENSOR|NON_SENSOR_GAP`);
    }
  }
  if (sensors !== SENSOR_MAP.sensorLocations) issues.push(`sensor slots must number ${SENSOR_MAP.sensorLocations}; got ${sensors}`);
  if (nonSensorGaps !== SENSOR_MAP.nonSensorGapSlots) issues.push(`NON_SENSOR_GAP slots must number ${SENSOR_MAP.nonSensorGapSlots}; got ${nonSensorGaps}`);
  if (allChannels.length !== SENSOR_MAP.thermocoupleChannels) issues.push(`total channels must be ${SENSOR_MAP.thermocoupleChannels}; got ${allChannels.length}`);
  if (seenChannels.size !== SENSOR_MAP.thermocoupleChannels) issues.push(`channels must be globally unique across the 212; got ${seenChannels.size}`);
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
    // Canonical row-major order (orderTotal), never lexicographic label order.
    gapLogicalLabels: nonSensorGaps.map((c) => c.logicalLabel),
  };
}

function validateSnapshot(obj, issues) {
  if (obj.schema !== SNAPSHOT_SCHEMA) issues.push(`schema must be ${SNAPSHOT_SCHEMA}`);
  if (obj.apiVersion !== API_VERSION) issues.push(`apiVersion must be ${API_VERSION}`);
  if (!isInt(obj.revision) || obj.revision < 1) issues.push('revision must be an integer >= 1');
  if (!isStr(obj.generatedAt)) issues.push('generatedAt required');
  if (!['SIMULATOR', 'TEST_HARDWARE', 'PRODUCTION'].includes(obj.deviceProfile)) issues.push('deviceProfile invalid');

  const map = obj.wallMap ?? [];
  if (map.length !== 108) issues.push(`wallMap must contain all 108 slots (got ${map.length})`);
  const sensors = obj.sensors ?? [];
  if (sensors.length !== 106) issues.push(`sensors must contain all 106 records (got ${sensors.length})`);
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
  if (ids.size !== 106) issues.push('sensorIds must be unique');
  if (scans.size !== 106 || ![...Array.from({ length: 106 }, (_, i) => i + 1)].every((n) => scans.has(n))) {
    issues.push('scanOrder must be exactly 1..106');
  }
  if (channels.size !== 212) issues.push(`212 unique thermocouple channels required (got ${channels.size})`);

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
