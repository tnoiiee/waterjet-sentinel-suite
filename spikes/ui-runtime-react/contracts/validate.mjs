// WJSS Stage 0.2.1A — structural validator for spike Snapshot / Delta messages.
// SYNTHETIC SPIKE TOOLING. Hand-written, no schema or code-generation dependency.
// Sensor identity, positions, walls, and Cannon slots are checked against the canonical map.

import { CANNON_SLOTS, EXPECTED, getSensorMap, isCannonId, sensorById } from './sensorMap.mjs';

const WALLS = EXPECTED.perWall;
const QUALITIES = new Set(['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED']);
const CLASSES = new Set(['DIRTY', 'CLEANER', 'NOT_CLASSIFIED']);
const BASES = new Set(['CURRENT', 'LAST_VALIDATED', 'NONE']);
const QUEUE_STATES = new Set(['NONE', 'READY', 'HELD', 'BLOCKED', 'EXCLUDED', 'ACTIVE']);
const ALARM_STATES = new Set(['NONE', 'ACTIVE_UNACK', 'ACTIVE_ACK', 'CLEARED_UNACK']);
const POSITION_KEYS = ['wall', 'logicalColumn', 'logicalRow', 'wallColumn', 'wallRow', 'scanOrder', 'deviceId', 'tcFrontChannel', 'tcRearChannel'];
const SLOT_KEYS = ['slotId', 'slotType', 'wall', 'logicalColumn', 'logicalRow', 'wallColumn', 'wallRow', 'sensorId', 'equipmentId'];
const isSensorId = (id) => sensorById(id) !== null;
const PHASES = new Set(['P1', 'P2', 'P3', 'P4', 'P5', 'P6']);

function isIso(v) {
  return typeof v === 'string' && !Number.isNaN(Date.parse(v));
}

/** @returns {string[]} */
export function validateSensor(s, path = 'sensor') {
  const e = [];
  if (!s || typeof s !== 'object') return [`${path}: not an object`];
  if (isCannonId(s.sensorId)) return [`${path}: Cannon slot ${s.sensorId} must not appear as a Sensor`];
  const m = sensorById(s.sensorId);
  if (!m) return [`${path}.sensorId not in canonical map: ${s.sensorId}`];
  if (s.slotType !== 'SENSOR') e.push(`${path}.slotType must be SENSOR`);
  for (const k of POSITION_KEYS) if (s[k] !== m[k]) e.push(`${path}.${k} differs from canonical map (${s[k]} != ${m[k]})`);
  if (!(s.dirtyScore === null || (typeof s.dirtyScore === 'number' && s.dirtyScore >= 0 && s.dirtyScore <= 100))) e.push(`${path}.dirtyScore out of range`);
  if (!QUALITIES.has(s.quality)) e.push(`${path}.quality invalid`);
  if (!CLASSES.has(s.classification)) e.push(`${path}.classification invalid`);
  if (!BASES.has(s.classificationBasis)) e.push(`${path}.classificationBasis invalid`);
  if (!QUEUE_STATES.has(s.queueState)) e.push(`${path}.queueState invalid`);
  if (!ALARM_STATES.has(s.alarmState)) e.push(`${path}.alarmState invalid`);
  if (typeof s.isActiveJobTarget !== 'boolean') e.push(`${path}.isActiveJobTarget must be boolean`);
  // Protected presentation rules
  if (['BAD', 'STALE', 'DISABLED'].includes(s.quality) && s.classification !== 'NOT_CLASSIFIED') {
    e.push(`${path}: ${s.quality} must be NOT_CLASSIFIED`);
  }
  if (s.quality === 'UNCERTAIN' && s.classificationBasis === 'CURRENT') {
    e.push(`${path}: UNCERTAIN must not classify from the current value`);
  }
  if (s.lastValidatedAt !== null && !isIso(s.lastValidatedAt)) e.push(`${path}.lastValidatedAt invalid`);
  return e;
}

/** Wall map: 108 slots, 106 SENSOR + 2 CANNON, identical to the canonical map. @returns {string[]} */
export function validateWallMap(slots) {
  const e = [];
  const canon = getSensorMap().slots;
  if (!Array.isArray(slots) || slots.length !== EXPECTED.logicalSlots) return [`wallMap must contain ${EXPECTED.logicalSlots} slots (got ${slots?.length})`];
  slots.forEach((sl, i) => {
    for (const k of SLOT_KEYS) if (sl?.[k] !== canon[i][k]) e.push(`wallMap[${i}].${k} differs from canonical map`);
  });
  const cannons = slots.filter((sl) => sl.slotType === 'CANNON');
  if (cannons.length !== EXPECTED.cannonSlots) e.push(`wallMap must contain ${EXPECTED.cannonSlots} Cannon slots`);
  for (const c of CANNON_SLOTS) {
    const sl = slots.find((x) => x.logicalRow === c.logicalRow && x.logicalColumn === c.logicalColumn);
    if (sl?.slotType !== 'CANNON' || sl.equipmentId !== c.equipmentId || sl.sensorId !== null) e.push(`${c.logicalLabel} must be Cannon slot ${c.equipmentId}`);
  }
  return e;
}

function validateCommon(m, e) {
  if (m.synthetic !== true) e.push('synthetic flag must be true');
  if (!Number.isInteger(m.revision) || m.revision < 0) e.push('revision invalid');
  if (!isIso(m.generatedAt)) e.push('generatedAt invalid');
}

function validateJob(j, e) {
  if (j === null) return;
  if (typeof j !== 'object') return e.push('activeJob invalid');
  if (!isSensorId(j.targetSensorId)) e.push(`activeJob.targetSensorId invalid: ${j.targetSensorId}`);
  if (!PHASES.has(j.phase)) e.push('activeJob.phase invalid');
}

function validateQueue(q, e) {
  if (!q || !Array.isArray(q.entries)) return e.push('queue invalid');
  if (q.entries.length > 8) e.push('queue preview exceeds 8 entries');
  q.entries.forEach((en, i) => {
    if (en.position !== i + 1) e.push(`queue.entries[${i}].position not FIFO order`);
    if (!['READY', 'HELD', 'BLOCKED', 'EXCLUDED'].includes(en.status)) e.push(`queue.entries[${i}].status invalid`);
    if (!isSensorId(en.sensorId)) e.push(`queue.entries[${i}].sensorId not a Sensor: ${en.sensorId}`);
  });
}

/** @returns {string[]} */
export function validateSnapshot(m) {
  const e = [];
  if (!m || m.kind !== 'snapshot') return ['kind must be snapshot'];
  if (m.schema !== 'wjss.spike.snapshot/1') e.push('schema invalid');
  validateCommon(m, e);
  if (!m.config || typeof m.config.dirtyThreshold !== 'number' || !Number.isInteger(m.config.revision)) e.push('config invalid');
  e.push(...validateWallMap(m.wallMap));
  if (!Array.isArray(m.sensors) || m.sensors.length !== EXPECTED.sensors) e.push(`sensors must contain ${EXPECTED.sensors} records (got ${m.sensors?.length})`);
  else {
    const ids = new Set();
    const perWall = { LEFT: 0, REAR: 0, RIGHT: 0, FRONT: 0 };
    const channels = [];
    m.sensors.forEach((s, i) => {
      e.push(...validateSensor(s, `sensors[${i}]`));
      ids.add(s.sensorId);
      if (s.wall in perWall) perWall[s.wall] += 1;
      channels.push(s.tcFrontChannel, s.tcRearChannel);
    });
    if (ids.size !== EXPECTED.sensors) e.push('sensor IDs not unique');
    for (const [w, n] of Object.entries(WALLS)) if (perWall[w] !== n) e.push(`wall ${w} expected ${n} got ${perWall[w]}`);
    if (new Set(channels).size !== EXPECTED.channels || channels.length !== EXPECTED.channels) e.push(`expected ${EXPECTED.channels} unique Thermocouple channels, got ${new Set(channels).size}`);
  }
  if (!Array.isArray(m.walls) || m.walls.length !== 4) e.push('walls must contain 4 summaries');
  validateJob(m.activeJob, e);
  if (!m.pump || typeof m.pump.state !== 'string') e.push('pump invalid');
  validateQueue(m.queue, e);
  if (!m.alarms || !Array.isArray(m.alarms.items)) e.push('alarms invalid');
  if (!m.communication || !Array.isArray(m.communication.devices) || m.communication.devices.length !== 10) e.push('communication must list 10 devices');
  if (!m.runtime || typeof m.runtime.historian !== 'object') e.push('runtime invalid');
  if (!m.trend || !Array.isArray(m.trend.points) || m.trend.points.length > m.trend.capacity) e.push('trend invalid or exceeds capacity');
  else if (m.trend.seriesNames?.length < 4) e.push('trend needs at least 4 series');
  return e;
}

/** @returns {string[]} */
export function validateDelta(m) {
  const e = [];
  if (!m || m.kind !== 'delta') return ['kind must be delta'];
  if (m.schema !== 'wjss.spike.delta/1') e.push('schema invalid');
  validateCommon(m, e);
  if (!Number.isInteger(m.previousRevision) || m.previousRevision >= m.revision) e.push('previousRevision must be an integer below revision');
  if (m.sensors !== undefined) {
    if (!Array.isArray(m.sensors) || m.sensors.length > EXPECTED.sensors) e.push('delta sensors invalid');
    else m.sensors.forEach((s, i) => e.push(...validateSensor(s, `sensors[${i}]`)));
  }
  if ('wallMap' in m) e.push('wallMap is Snapshot-only and must not appear in a Delta');
  if ('activeJob' in m) validateJob(m.activeJob, e);
  if (m.queue !== undefined) validateQueue(m.queue, e);
  return e;
}
