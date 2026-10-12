// Stage 0.4C-K3 - Owner critical topology correction: the CENTRAL topology invariant check.
//
// Authoritative Product topology (Owner ruling, final):
//   - 108 Product Sensors; I7 and I16 are normal Sensors (same parameters, state model, Front/Rear
//     pairing, runtime, commissioning, mapping and queue rules as every other Sensor).
//   - 108 x 2 = 216 assigned Product TC measurements; 240 physical TC channels; 240 - 216 = 24
//     unassigned physical channels.
//   - No logical position is a NON_SENSOR_GAP.
//
// This module is the single place where the layers are compared with each other: the Sensor
// catalogue (the served logical matrix), the Sensor sequence, the drawing-derived schedule, the
// built Wall Map and, when supplied, runtime projection counts. Disagreement between any two layers
// is a refusal: no summary may report 216 assigned channels while a catalogue still reports 106
// Sensors, and no surface may silently present a count another surface contradicts.
//
// Pure functions only: no I/O, no clock, no network, no device.

import { PROTECTED_BASELINE, PRODUCT_SENSOR_SEQUENCE } from './schedule.mjs';

/** The authoritative counts. Every number is asserted by validateTopologyInvariants, never assumed. */
export const AUTHORITATIVE_TOPOLOGY = Object.freeze({
  PRODUCT_SENSORS: 108,
  SENSOR_SEQUENCE_LENGTH: 108,
  UNIQUE_SENSOR_IDS: 108,
  FRONT_MEASUREMENTS: 108,
  REAR_MEASUREMENTS: 108,
  MEASUREMENTS_PER_SENSOR: 2,
  ASSIGNED_PRODUCT_TC_CHANNELS: 216,
  PHYSICAL_TC_CHANNELS: 240,
  UNASSIGNED_PHYSICAL_TC_CHANNELS: 24,
  NON_SENSOR_GAP_POSITIONS: 0,
  LOGICAL_POSITIONS: 108,
  SENSORS_PER_WALL: Object.freeze({ LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 }),
  /** The two Sensors the revoked baseline excluded; each must be present exactly once, with both sides. */
  CORRECTED_SENSOR_IDS: Object.freeze(['I7', 'I16']),
});

/** Refusal codes of the central check. Each names the layer or relation that disagrees. */
export const TOPOLOGY_REFUSAL = Object.freeze({
  ARITHMETIC_IDENTITY: 'TOPOLOGY_ARITHMETIC_IDENTITY',
  SEQUENCE_LENGTH: 'TOPOLOGY_SEQUENCE_LENGTH',
  SEQUENCE_ORDER: 'TOPOLOGY_SEQUENCE_ORDER',
  DUPLICATE_SENSOR_ID: 'TOPOLOGY_DUPLICATE_SENSOR_ID',
  SENSOR_MISSING: 'TOPOLOGY_SENSOR_MISSING',
  NON_SENSOR_GAP_PRESENT: 'TOPOLOGY_NON_SENSOR_GAP_PRESENT',
  CATALOGUE_COUNT: 'TOPOLOGY_CATALOGUE_COUNT',
  CATALOGUE_WALL_COUNT: 'TOPOLOGY_CATALOGUE_WALL_COUNT',
  SCHEDULE_PHYSICAL_COUNT: 'TOPOLOGY_SCHEDULE_PHYSICAL_COUNT',
  SCHEDULE_ASSIGNED_COUNT: 'TOPOLOGY_SCHEDULE_ASSIGNED_COUNT',
  SCHEDULE_UNASSIGNED_COUNT: 'TOPOLOGY_SCHEDULE_UNASSIGNED_COUNT',
  SCHEDULE_SENSOR_COUNT: 'TOPOLOGY_SCHEDULE_SENSOR_COUNT',
  FRONT_REAR_PAIRING: 'TOPOLOGY_FRONT_REAR_PAIRING',
  UI_MAP_COUNT: 'TOPOLOGY_UI_MAP_COUNT',
  RUNTIME_COUNT: 'TOPOLOGY_RUNTIME_COUNT',
  LAYER_DISAGREEMENT: 'TOPOLOGY_LAYER_DISAGREEMENT',
});

function violation(code, message, detail = null) {
  return Object.freeze({ code, message, detail });
}

/** Exact Sensor sequence order: G+201..G+218, G+101..G+118, G1..G18, H1..H18, I1..I18, J1..J18. */
export function validateSensorSequence(sequence = PRODUCT_SENSOR_SEQUENCE) {
  const violations = [];
  const expected = PRODUCT_SENSOR_SEQUENCE;
  if (!Array.isArray(sequence) || sequence.length !== AUTHORITATIVE_TOPOLOGY.SENSOR_SEQUENCE_LENGTH) {
    violations.push(violation(TOPOLOGY_REFUSAL.SEQUENCE_LENGTH,
      `the Sensor sequence must carry exactly ${AUTHORITATIVE_TOPOLOGY.SENSOR_SEQUENCE_LENGTH} identities; found ${Array.isArray(sequence) ? sequence.length : 'no sequence'}`));
    return Object.freeze(violations);
  }
  const unique = new Set(sequence);
  if (unique.size !== sequence.length) {
    const seen = new Set();
    const duplicates = sequence.filter((id) => (seen.has(id) ? true : (seen.add(id), false)));
    violations.push(violation(TOPOLOGY_REFUSAL.DUPLICATE_SENSOR_ID, `duplicate Sensor identities in the sequence: ${[...new Set(duplicates)].join(', ')}`));
  }
  for (let i = 0; i < expected.length; i += 1) {
    if (sequence[i] !== expected[i]) {
      violations.push(violation(TOPOLOGY_REFUSAL.SEQUENCE_ORDER,
        `Sensor sequence position ${i + 1} must be ${expected[i]}; found ${sequence[i]}`));
      break;
    }
  }
  for (const id of AUTHORITATIVE_TOPOLOGY.CORRECTED_SENSOR_IDS) {
    const occurrences = sequence.filter((s) => s === id).length;
    if (occurrences !== 1) {
      violations.push(violation(TOPOLOGY_REFUSAL.SENSOR_MISSING, `Sensor ${id} must appear exactly once in the sequence; found ${occurrences}`));
    }
  }
  return Object.freeze(violations);
}

/** The served logical matrix (the public-safe sensor-map example / Product wall-map contract mirror). */
export function validateSensorCatalogue(sensorMap) {
  const violations = [];
  const matrix = sensorMap?.logicalMatrix ?? sensorMap;
  const slots = Array.isArray(matrix?.slots) ? matrix.slots
    : Array.isArray(sensorMap?.walls) ? sensorMap.walls.flatMap((w) => w.slots ?? []) : null;
  if (!slots) {
    violations.push(violation(TOPOLOGY_REFUSAL.CATALOGUE_COUNT, 'the Sensor catalogue carries no logical positions'));
    return Object.freeze(violations);
  }
  const sensors = slots.filter((s) => s.positionKind === 'SENSOR');
  const nonSensor = slots.filter((s) => s.positionKind !== 'SENSOR');
  if (slots.length !== AUTHORITATIVE_TOPOLOGY.LOGICAL_POSITIONS) {
    violations.push(violation(TOPOLOGY_REFUSAL.CATALOGUE_COUNT, `the Sensor catalogue must carry ${AUTHORITATIVE_TOPOLOGY.LOGICAL_POSITIONS} logical positions; found ${slots.length}`));
  }
  if (sensors.length !== AUTHORITATIVE_TOPOLOGY.PRODUCT_SENSORS) {
    violations.push(violation(TOPOLOGY_REFUSAL.CATALOGUE_COUNT, `the Sensor catalogue must carry ${AUTHORITATIVE_TOPOLOGY.PRODUCT_SENSORS} Product Sensors; found ${sensors.length}`));
  }
  if (nonSensor.length !== AUTHORITATIVE_TOPOLOGY.NON_SENSOR_GAP_POSITIONS) {
    violations.push(violation(TOPOLOGY_REFUSAL.NON_SENSOR_GAP_PRESENT,
      `no logical position may be a NON_SENSOR_GAP (I7 and I16 are normal Product Sensors); found ${nonSensor.length}: ${nonSensor.map((s) => s.logicalLabel ?? s.sensorId ?? s.slotId).join(', ')}`));
  }
  const ids = sensors.map((s) => s.sensorId);
  const idSet = new Set(ids);
  if (idSet.size !== ids.length) {
    violations.push(violation(TOPOLOGY_REFUSAL.DUPLICATE_SENSOR_ID, 'a Sensor identity appears more than once in the Sensor catalogue'));
  }
  for (const id of PRODUCT_SENSOR_SEQUENCE) {
    if (!idSet.has(id)) violations.push(violation(TOPOLOGY_REFUSAL.SENSOR_MISSING, `Sensor ${id} is missing from the Sensor catalogue`));
  }
  for (const id of idSet) {
    if (!PRODUCT_SENSOR_SEQUENCE.includes(id)) violations.push(violation(TOPOLOGY_REFUSAL.SENSOR_MISSING, `identity ${id} in the Sensor catalogue is not one of the 108 Product Sensors`));
  }
  const declared = {
    sensorLocations: matrix?.sensorLocations,
    thermocoupleChannels: matrix?.thermocoupleChannels,
    sensorsPerWall: matrix?.sensorsPerWall,
  };
  if (declared.sensorLocations !== undefined && declared.sensorLocations !== AUTHORITATIVE_TOPOLOGY.PRODUCT_SENSORS) {
    violations.push(violation(TOPOLOGY_REFUSAL.LAYER_DISAGREEMENT, `the Sensor catalogue declares ${declared.sensorLocations} Sensor locations; the authoritative count is ${AUTHORITATIVE_TOPOLOGY.PRODUCT_SENSORS}`));
  }
  if (declared.thermocoupleChannels !== undefined && declared.thermocoupleChannels !== AUTHORITATIVE_TOPOLOGY.ASSIGNED_PRODUCT_TC_CHANNELS) {
    violations.push(violation(TOPOLOGY_REFUSAL.LAYER_DISAGREEMENT, `the Sensor catalogue declares ${declared.thermocoupleChannels} thermocouple channels; the authoritative count is ${AUTHORITATIVE_TOPOLOGY.ASSIGNED_PRODUCT_TC_CHANNELS}`));
  }
  const perWall = {};
  for (const s of sensors) perWall[s.wall] = (perWall[s.wall] ?? 0) + 1;
  for (const [wall, count] of Object.entries(AUTHORITATIVE_TOPOLOGY.SENSORS_PER_WALL)) {
    if ((perWall[wall] ?? 0) !== count) {
      violations.push(violation(TOPOLOGY_REFUSAL.CATALOGUE_WALL_COUNT, `the ${wall} wall must carry ${count} Product Sensors; found ${perWall[wall] ?? 0}`));
    }
    if (declared.sensorsPerWall && declared.sensorsPerWall[wall] !== undefined && declared.sensorsPerWall[wall] !== count) {
      violations.push(violation(TOPOLOGY_REFUSAL.LAYER_DISAGREEMENT, `the Sensor catalogue declares ${declared.sensorsPerWall[wall]} Sensors on the ${wall} wall; the authoritative count is ${count}`));
    }
  }
  const channels = sensors.flatMap((s) => (Array.isArray(s.tcChannels) ? s.tcChannels : []));
  if (channels.length > 0) {
    if (channels.length !== AUTHORITATIVE_TOPOLOGY.ASSIGNED_PRODUCT_TC_CHANNELS || new Set(channels).size !== channels.length) {
      violations.push(violation(TOPOLOGY_REFUSAL.CATALOGUE_COUNT, `the Sensor catalogue must assign ${AUTHORITATIVE_TOPOLOGY.ASSIGNED_PRODUCT_TC_CHANNELS} distinct thermocouple channels; found ${channels.length} (${new Set(channels).size} distinct)`));
    }
    for (const s of sensors) {
      if (!Array.isArray(s.tcChannels) || s.tcChannels.length !== AUTHORITATIVE_TOPOLOGY.MEASUREMENTS_PER_SENSOR) {
        violations.push(violation(TOPOLOGY_REFUSAL.FRONT_REAR_PAIRING, `Sensor ${s.sensorId} must carry exactly two thermocouple channels`));
      }
    }
  }
  return Object.freeze(violations);
}

/** The drawing-derived physical schedule (parsed rows + recomputed counts). */
export function validateSchedule(schedule, counts) {
  const violations = [];
  const A = AUTHORITATIVE_TOPOLOGY;
  if (counts.physicalChannels !== A.PHYSICAL_TC_CHANNELS) {
    violations.push(violation(TOPOLOGY_REFUSAL.SCHEDULE_PHYSICAL_COUNT, `the physical schedule must carry ${A.PHYSICAL_TC_CHANNELS} physical TC channels; found ${counts.physicalChannels}`));
  }
  if (counts.productCompatibleMeasurements !== A.ASSIGNED_PRODUCT_TC_CHANNELS || counts.measurementIdentities !== A.ASSIGNED_PRODUCT_TC_CHANNELS) {
    violations.push(violation(TOPOLOGY_REFUSAL.SCHEDULE_ASSIGNED_COUNT, `the schedule must assign ${A.ASSIGNED_PRODUCT_TC_CHANNELS} Product TC measurements; found ${counts.productCompatibleMeasurements} (${counts.measurementIdentities} identities)`));
  }
  if (counts.unassignedChannels !== A.UNASSIGNED_PHYSICAL_TC_CHANNELS) {
    violations.push(violation(TOPOLOGY_REFUSAL.SCHEDULE_UNASSIGNED_COUNT, `the schedule must leave ${A.UNASSIGNED_PHYSICAL_TC_CHANNELS} physical TC channels unassigned; found ${counts.unassignedChannels}`));
  }
  if (counts.productSensors !== A.PRODUCT_SENSORS) {
    violations.push(violation(TOPOLOGY_REFUSAL.SCHEDULE_SENSOR_COUNT, `the schedule must carry ${A.PRODUCT_SENSORS} Product Sensors; found ${counts.productSensors}`));
  }
  if (counts.productTopologyConflictRows !== 0) {
    violations.push(violation(TOPOLOGY_REFUSAL.NON_SENSOR_GAP_PRESENT, `the corrected baseline carries no Product-topology conflict row; found ${counts.productTopologyConflictRows}`));
  }
  const front = new Map();
  const rear = new Map();
  for (const row of schedule.rows) {
    if (!row.productCompatible || !row.sensorId) continue;
    const bucket = row.side === 'F' ? front : row.side === 'R' ? rear : null;
    if (!bucket) {
      violations.push(violation(TOPOLOGY_REFUSAL.FRONT_REAR_PAIRING, `Product-compatible row ${row.physicalChannelKey} of Sensor ${row.sensorId} carries no measurement side`));
      continue;
    }
    bucket.set(row.sensorId, (bucket.get(row.sensorId) ?? 0) + 1);
  }
  if (front.size !== A.FRONT_MEASUREMENTS || rear.size !== A.REAR_MEASUREMENTS) {
    violations.push(violation(TOPOLOGY_REFUSAL.FRONT_REAR_PAIRING, `the schedule must carry ${A.FRONT_MEASUREMENTS} Front and ${A.REAR_MEASUREMENTS} Rear Product measurements; found ${front.size} Front and ${rear.size} Rear`));
  }
  for (const id of PRODUCT_SENSOR_SEQUENCE) {
    if ((front.get(id) ?? 0) !== 1 || (rear.get(id) ?? 0) !== 1) {
      violations.push(violation(TOPOLOGY_REFUSAL.FRONT_REAR_PAIRING, `Sensor ${id} must own exactly one Front and one Rear measurement; found ${front.get(id) ?? 0} Front and ${rear.get(id) ?? 0} Rear`));
    }
  }
  for (const id of [...front.keys(), ...rear.keys()]) {
    if (!PRODUCT_SENSOR_SEQUENCE.includes(id)) {
      violations.push(violation(TOPOLOGY_REFUSAL.SENSOR_MISSING, `the schedule assigns measurements to ${id}, which is not one of the 108 Product Sensors`));
    }
  }
  return Object.freeze(violations);
}

/** The built Wall Map view model (packages/commissioning/src/wallmap.mjs). */
export function validateWallMapCounts(wallMap) {
  const violations = [];
  const A = AUTHORITATIVE_TOPOLOGY;
  const c = wallMap?.counts;
  if (!c) return Object.freeze([violation(TOPOLOGY_REFUSAL.UI_MAP_COUNT, 'the Wall Map carries no counts')]);
  const expect = (actual, expected, label) => {
    if (actual !== expected) violations.push(violation(TOPOLOGY_REFUSAL.UI_MAP_COUNT, `the Wall Map ${label} must be ${expected}; found ${actual}`));
  };
  expect(c.logicalPositions, A.LOGICAL_POSITIONS, 'logical position count');
  expect(c.productSensors, A.PRODUCT_SENSORS, 'Product Sensor count');
  expect(c.nonSensorGaps, A.NON_SENSOR_GAP_POSITIONS, 'NON_SENSOR_GAP count');
  expect(c.productCompatibleMeasurements, A.ASSIGNED_PRODUCT_TC_CHANNELS, 'measurement count');
  for (const [wall, count] of Object.entries(A.SENSORS_PER_WALL)) expect(c.sensorsPerWall?.[wall], count, `${wall} wall Sensor count`);
  if (Array.isArray(wallMap.order) && wallMap.order.length !== A.ASSIGNED_PRODUCT_TC_CHANNELS) {
    violations.push(violation(TOPOLOGY_REFUSAL.UI_MAP_COUNT, `the Wall order must carry ${A.ASSIGNED_PRODUCT_TC_CHANNELS} measurements; found ${wallMap.order.length}`));
  }
  for (const id of A.CORRECTED_SENSOR_IDS) {
    const position = wallMap.sensorPositionBySensorId?.get(id);
    if (!position || position.selectableAsSensor !== true) {
      violations.push(violation(TOPOLOGY_REFUSAL.UI_MAP_COUNT, `Sensor ${id} must be a selectable Sensor cell on the Wall Map`));
    }
  }
  return Object.freeze(violations);
}

/**
 * Optional runtime projection counts (for example a runtime snapshot or API summary). Only the
 * fields supplied are compared; a supplied field that disagrees is a refusal.
 */
export function validateRuntimeCounts(runtime) {
  const violations = [];
  if (!runtime) return Object.freeze(violations);
  const A = AUTHORITATIVE_TOPOLOGY;
  const expect = (key, expected, label) => {
    if (runtime[key] !== undefined && runtime[key] !== expected) {
      violations.push(violation(TOPOLOGY_REFUSAL.RUNTIME_COUNT, `the runtime projection reports ${runtime[key]} ${label}; the authoritative count is ${expected}`));
    }
  };
  expect('sensorCount', A.PRODUCT_SENSORS, 'Sensors');
  expect('logicalPositions', A.LOGICAL_POSITIONS, 'logical positions');
  expect('nonSensorGaps', A.NON_SENSOR_GAP_POSITIONS, 'NON_SENSOR_GAP positions');
  expect('thermocoupleChannels', A.ASSIGNED_PRODUCT_TC_CHANNELS, 'thermocouple channels');
  expect('physicalChannels', A.PHYSICAL_TC_CHANNELS, 'physical channels');
  expect('unassignedChannels', A.UNASSIGNED_PHYSICAL_TC_CHANNELS, 'unassigned channels');
  if (Array.isArray(runtime.sensorIds)) {
    const ids = runtime.sensorIds;
    if (ids.length !== A.PRODUCT_SENSORS || new Set(ids).size !== ids.length) {
      violations.push(violation(TOPOLOGY_REFUSAL.RUNTIME_COUNT, `the runtime projection must carry ${A.PRODUCT_SENSORS} distinct Sensors; found ${ids.length}`));
    }
    for (const id of A.CORRECTED_SENSOR_IDS) {
      if (ids.filter((s) => s === id).length !== 1) violations.push(violation(TOPOLOGY_REFUSAL.RUNTIME_COUNT, `the runtime projection must carry Sensor ${id} exactly once`));
    }
  }
  return Object.freeze(violations);
}

/**
 * The central check. Compares every supplied layer with the authoritative topology and with each
 * other. Returns { ok, violations } — it never repairs, never substitutes a count and never prefers
 * one layer over another: any disagreement is a refusal.
 *
 * @param {object} layers
 * @param {string[]=} layers.sequence       the Sensor sequence (defaults to the authoritative one)
 * @param {object=}   layers.sensorMap      the served logical matrix / Sensor catalogue
 * @param {object=}   layers.schedule       the parsed physical schedule
 * @param {object=}   layers.scheduleCounts the recomputed schedule counts
 * @param {object=}   layers.wallMap        the built Wall Map view model
 * @param {object=}   layers.runtime        runtime projection counts
 */
export function validateTopologyInvariants(layers = {}) {
  const violations = [];
  const A = AUTHORITATIVE_TOPOLOGY;
  // The arithmetic identities of the ruling hold inside the constants themselves.
  if (A.PRODUCT_SENSORS * A.MEASUREMENTS_PER_SENSOR !== A.ASSIGNED_PRODUCT_TC_CHANNELS
    || A.PHYSICAL_TC_CHANNELS - A.ASSIGNED_PRODUCT_TC_CHANNELS !== A.UNASSIGNED_PHYSICAL_TC_CHANNELS
    || A.FRONT_MEASUREMENTS + A.REAR_MEASUREMENTS !== A.ASSIGNED_PRODUCT_TC_CHANNELS
    || Object.values(A.SENSORS_PER_WALL).reduce((s, n) => s + n, 0) !== A.PRODUCT_SENSORS) {
    violations.push(violation(TOPOLOGY_REFUSAL.ARITHMETIC_IDENTITY, 'the authoritative topology constants are arithmetically inconsistent'));
  }
  if (PROTECTED_BASELINE.PRODUCT_SENSORS !== A.PRODUCT_SENSORS
    || PROTECTED_BASELINE.PRODUCT_COMPATIBLE_MEASUREMENTS !== A.ASSIGNED_PRODUCT_TC_CHANNELS
    || PROTECTED_BASELINE.PHYSICAL_CHANNEL_COUNT !== A.PHYSICAL_TC_CHANNELS
    || PROTECTED_BASELINE.UNASSIGNED_ROWS !== A.UNASSIGNED_PHYSICAL_TC_CHANNELS
    || PROTECTED_BASELINE.NON_SENSOR_GAP_SENSOR_IDS.length !== A.NON_SENSOR_GAP_POSITIONS) {
    violations.push(violation(TOPOLOGY_REFUSAL.LAYER_DISAGREEMENT, 'the protected schedule baseline disagrees with the authoritative topology'));
  }
  violations.push(...validateSensorSequence(layers.sequence ?? PRODUCT_SENSOR_SEQUENCE));
  if (layers.sensorMap) violations.push(...validateSensorCatalogue(layers.sensorMap));
  if (layers.schedule && layers.scheduleCounts) violations.push(...validateSchedule(layers.schedule, layers.scheduleCounts));
  if (layers.wallMap) violations.push(...validateWallMapCounts(layers.wallMap));
  if (layers.runtime) violations.push(...validateRuntimeCounts(layers.runtime));

  // Cross-layer: the catalogue, the schedule and the Wall Map must name the same Sensor set.
  const sets = [];
  if (layers.sensorMap) {
    const matrix = layers.sensorMap.logicalMatrix ?? layers.sensorMap;
    const slots = Array.isArray(matrix.slots) ? matrix.slots : (layers.sensorMap.walls ?? []).flatMap((w) => w.slots ?? []);
    sets.push(['Sensor catalogue', new Set(slots.filter((s) => s.positionKind === 'SENSOR').map((s) => s.sensorId))]);
  }
  if (layers.schedule) {
    sets.push(['physical schedule', new Set(layers.schedule.rows.filter((r) => r.productCompatible && r.sensorId).map((r) => r.sensorId))]);
  }
  if (layers.wallMap?.sensorPositionBySensorId) sets.push(['Wall Map', new Set(layers.wallMap.sensorPositionBySensorId.keys())]);
  if (Array.isArray(layers.runtime?.sensorIds)) sets.push(['runtime projection', new Set(layers.runtime.sensorIds)]);
  for (let i = 1; i < sets.length; i += 1) {
    const [nameA, a] = sets[0];
    const [nameB, b] = sets[i];
    if (a.size !== b.size || [...a].some((id) => !b.has(id))) {
      violations.push(violation(TOPOLOGY_REFUSAL.LAYER_DISAGREEMENT, `the ${nameA} (${a.size} Sensors) and the ${nameB} (${b.size} Sensors) do not name the same Sensor set`));
    }
  }
  return Object.freeze({ ok: violations.length === 0, violations: Object.freeze(violations), authoritative: AUTHORITATIVE_TOPOLOGY });
}

/** Throwing form for callers that must refuse rather than present a contradictory topology. */
export function requireTopologyInvariants(layers = {}) {
  const result = validateTopologyInvariants(layers);
  if (!result.ok) {
    throw new Error(`topology invariant violation: ${result.violations.map((v) => `${v.code}: ${v.message}`).join('; ')}`);
  }
  return result;
}
