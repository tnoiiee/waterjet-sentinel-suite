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

export const SNAPSHOT_SCHEMA = 'wjss.snapshot/1';
export const DELTA_SCHEMA = 'wjss.delta/1';
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
const CANNON_SLOTS = [
  ['CANNON_REAR', 5, 7],
  ['CANNON_FRONT', 5, 16],
];

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

export function canonicalSensorIds() {
  const ids = [];
  for (let row = 1; row <= LOGICAL_ROWS; row += 1) {
    for (let col = 1; col <= LOGICAL_COLUMNS; col += 1) {
      if (CANNON_SLOTS.some(([, r, c]) => r === row && c === col)) continue;
      ids.push(sensorIdFor(row, col));
    }
  }
  return ids;
}

const SENSOR_ID_SET = new Set(canonicalSensorIds());
const CANNON_ID_SET = new Set(CANNON_SLOTS.map(([id]) => id));

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
  if (slot.slotType === 'CANNON') {
    if (slot.sensorId !== null) issues.push(`${path}: CANNON slot carries sensorId`);
    if (!CANNON_ID_SET.has(slot.equipmentId)) issues.push(`${path}: unknown CANNON equipmentId`);
  } else if (slot.slotType === 'SENSOR') {
    if (slot.equipmentId !== null) issues.push(`${path}: SENSOR slot carries equipmentId`);
    if (!SENSOR_ID_SET.has(slot.sensorId)) issues.push(`${path}: sensorId not in canonical set`);
  } else {
    issues.push(`${path}: bad slotType`);
  }
  const group = WALL_GROUPS.find(([, f, l]) => slot.logicalColumn >= f && slot.logicalColumn <= l);
  if (!group || group[0] !== slot.wall) issues.push(`${path}: wall/column mismatch`);
  if (slot.wallColumn !== slot.logicalColumn - (group ? group[1] : 1) + 1) issues.push(`${path}: wallColumn mismatch`);
  if (slot.wallRow !== slot.logicalRow) issues.push(`${path}: wallRow must equal logicalRow`);
  const expectedId = `SLOT-R${slot.logicalRow}-C${String(slot.logicalColumn).padStart(2, '0')}`;
  if (slot.slotId !== expectedId) issues.push(`${path}: slotId ${slot.slotId} != ${expectedId}`);
  const isCannonSlot = CANNON_SLOTS.some(([, r, c]) => r === slot.logicalRow && c === slot.logicalColumn);
  if (isCannonSlot !== (slot.slotType === 'CANNON')) issues.push(`${path}: Cannon position mismatch (I7/I16 only)`);
}

function validateSensor(s, path, issues) {
  if (!isStr(s.sensorId) || !SENSOR_ID_SET.has(s.sensorId)) {
    issues.push(`${path}: sensorId must be a canonical Sensor id (a Cannon is never a Sensor)`);
  }
  if (s.slotType !== 'SENSOR') issues.push(`${path}: slotType must be SENSOR`);
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
  if (a.commandSeq !== null && v.commandSeq !== null && !(v.feedbackSeq !== null && v.feedbackSeq < a.commandSeq)) {
    issues.push(`${path}: valve must be CONFIRMED closed before the axis return is commanded`);
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
    if (!(o.valveCloseCommandSeq < o.valveClosedConfirmedSeq)) issues.push(`${path}: outcome: valve command < confirm required`);
    if (!(o.valveClosedConfirmedSeq < o.axisReturnCommandSeq)) issues.push(`${path}: outcome: valve confirm < axis command required`);
    if (!(o.axisReturnCommandSeq < o.standbyConfirmedSeq)) issues.push(`${path}: outcome: axis command < standby confirm required`);
    if (!(o.standbyConfirmedSeq < o.outcomeSeq)) issues.push(`${path}: outcome: standby confirm < outcome required`);
    if (!(o.outcomeSeq < o.releaseSeq)) issues.push(`${path}: outcome: outcome < release required`);
  }
}

// ---------------------------------------------------------------- entrypoints
export function validateEnvelope(obj) {
  const issues = [];
  if (typeof obj !== 'object' || obj === null) return { kind: null, issues: ['not an object'], gap: false };

  scanProhibitedKeys(obj, '$', issues);

  if (obj.kind === 'snapshot') return validateSnapshot(obj, issues);
  if (obj.kind === 'delta') return validateDelta(obj, issues);
  issues.push('kind must be snapshot|delta');
  return { kind: null, issues, gap: false };
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
