// WJSS Stage 0.2.1A — structural validator for spike Snapshot / Delta messages.
// SYNTHETIC SPIKE TOOLING. Hand-written, no schema or code-generation dependency.

const WALLS = { LEFT: 24, REAR: 28, RIGHT: 24, FRONT: 28 };
const QUALITIES = new Set(['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED']);
const CLASSES = new Set(['DIRTY', 'CLEANER', 'NOT_CLASSIFIED']);
const BASES = new Set(['CURRENT', 'LAST_VALIDATED', 'NONE']);
const QUEUE_STATES = new Set(['NONE', 'READY', 'HELD', 'BLOCKED', 'EXCLUDED', 'ACTIVE']);
const ALARM_STATES = new Set(['NONE', 'ACTIVE_UNACK', 'ACTIVE_ACK', 'CLEARED_UNACK']);
const SENSOR_ID = /^SYN-(LEFT|REAR|RIGHT|FRONT)-\d{2}$/;
const PHASES = new Set(['P1', 'P2', 'P3', 'P4', 'P5', 'P6']);

function isIso(v) {
  return typeof v === 'string' && !Number.isNaN(Date.parse(v));
}

/** @returns {string[]} */
export function validateSensor(s, path = 'sensor') {
  const e = [];
  if (!s || typeof s !== 'object') return [`${path}: not an object`];
  if (!SENSOR_ID.test(s.sensorId ?? '')) e.push(`${path}.sensorId invalid: ${s.sensorId}`);
  if (!(s.wall in WALLS)) e.push(`${path}.wall invalid`);
  if (!Number.isInteger(s.index) || s.index < 1) e.push(`${path}.index invalid`);
  if (!/^SYN-TC-\d{2}$/.test(s.deviceId ?? '')) e.push(`${path}.deviceId invalid`);
  if (!Array.isArray(s.tcChannels) || s.tcChannels.length !== 2) e.push(`${path}.tcChannels must have 2 entries`);
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

function validateCommon(m, e) {
  if (m.synthetic !== true) e.push('synthetic flag must be true');
  if (!Number.isInteger(m.revision) || m.revision < 0) e.push('revision invalid');
  if (!isIso(m.generatedAt)) e.push('generatedAt invalid');
}

function validateJob(j, e) {
  if (j === null) return;
  if (typeof j !== 'object') return e.push('activeJob invalid');
  if (!SENSOR_ID.test(j.targetSensorId ?? '')) e.push('activeJob.targetSensorId invalid');
  if (!PHASES.has(j.phase)) e.push('activeJob.phase invalid');
}

function validateQueue(q, e) {
  if (!q || !Array.isArray(q.entries)) return e.push('queue invalid');
  if (q.entries.length > 8) e.push('queue preview exceeds 8 entries');
  q.entries.forEach((en, i) => {
    if (en.position !== i + 1) e.push(`queue.entries[${i}].position not FIFO order`);
    if (!['READY', 'HELD', 'BLOCKED', 'EXCLUDED'].includes(en.status)) e.push(`queue.entries[${i}].status invalid`);
  });
}

/** @returns {string[]} */
export function validateSnapshot(m) {
  const e = [];
  if (!m || m.kind !== 'snapshot') return ['kind must be snapshot'];
  if (m.schema !== 'wjss.spike.snapshot/1') e.push('schema invalid');
  validateCommon(m, e);
  if (!m.config || typeof m.config.dirtyThreshold !== 'number' || !Number.isInteger(m.config.revision)) e.push('config invalid');
  if (!Array.isArray(m.sensors) || m.sensors.length !== 104) e.push(`sensors must contain 104 records (got ${m.sensors?.length})`);
  else {
    const ids = new Set();
    const perWall = { LEFT: 0, REAR: 0, RIGHT: 0, FRONT: 0 };
    const channels = new Set();
    m.sensors.forEach((s, i) => {
      e.push(...validateSensor(s, `sensors[${i}]`));
      ids.add(s.sensorId);
      if (s.wall in perWall) perWall[s.wall] += 1;
      (s.tcChannels ?? []).forEach((c) => channels.add(c));
    });
    if (ids.size !== 104) e.push('sensor IDs not unique');
    for (const [w, n] of Object.entries(WALLS)) if (perWall[w] !== n) e.push(`wall ${w} expected ${n} got ${perWall[w]}`);
    if (channels.size !== 208) e.push(`expected 208 unique Thermocouple channels, got ${channels.size}`);
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
    if (!Array.isArray(m.sensors) || m.sensors.length > 104) e.push('delta sensors invalid');
    else m.sensors.forEach((s, i) => e.push(...validateSensor(s, `sensors[${i}]`)));
  }
  if ('activeJob' in m) validateJob(m.activeJob, e);
  if (m.queue !== undefined) validateQueue(m.queue, e);
  return e;
}
