// Stage 0.4C-K3 - Owner critical topology correction: central invariant tests.
//
// Numbering follows the Owner's required test list where the test is realisable in the Arena-native
// (Node) layers: topology 1-11, measurements 12-22 and the regression items 35-44 that concern these
// layers. Parameter-migration tests (23-27) and Disabled-Sensor queue tests (28-34) concern the .NET
// importer / runtime and are NOT realised here (see the checkpoint record).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  AUTHORITATIVE_TOPOLOGY, TOPOLOGY_REFUSAL, validateSensorSequence, validateSensorCatalogue, validateSchedule,
  validateWallMapCounts, validateRuntimeCounts, validateTopologyInvariants, requireTopologyInvariants,
} from '../src/topologyInvariants.mjs';
import { PRODUCT_SENSOR_SEQUENCE, PROTECTED_BASELINE, ROW_CLASS, parseSchedule, scheduleCounts } from '../src/schedule.mjs';
import { buildWallMap, WALL_MAP_EXPECTED } from '../src/wallmap.mjs';
import { schedule, counts, scheduleCsvText } from './helpers/schedule.mjs';
import { servedSensorMap, sensorMapExample } from './helpers/wallMap.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const TEMPLATE_PATH = join(HERE, '..', '..', '..', 'docs', 'templates', 'sensor-to-tc-channel-schedule.template.csv');
const WALL_MAP = buildWallMap(servedSensorMap(), schedule.rows);
const ALL_LAYERS = Object.freeze({ sensorMap: sensorMapExample, schedule, scheduleCounts: counts, wallMap: WALL_MAP });
const codes = (result) => [...new Set(result.violations.map((v) => v.code))];

// ------------------------------------------------------------------ topology (1-11)

test('T1 the authoritative Sensor catalogue carries exactly 108 Product Sensors', () => {
  assert.equal(AUTHORITATIVE_TOPOLOGY.PRODUCT_SENSORS, 108);
  const slots = sensorMapExample.logicalMatrix.slots;
  assert.equal(slots.length, 108);
  assert.equal(slots.filter((s) => s.positionKind === 'SENSOR').length, 108);
  assert.equal(sensorMapExample.logicalMatrix.sensorLocations, 108);
  assert.equal(sensorMapExample.logicalMatrix.thermocoupleChannels, 216);
  assert.deepEqual(validateSensorCatalogue(sensorMapExample), []);
});

test('T2 the Sensor sequence is exactly G+201-G+218, G+101-G+118, G1-G18, H1-H18, I1-I18, J1-J18', () => {
  const expected = [];
  for (const label of [(c) => `G+${200 + c}`, (c) => `G+${100 + c}`, (c) => `G${c}`, (c) => `H${c}`, (c) => `I${c}`, (c) => `J${c}`]) {
    for (let c = 1; c <= 18; c += 1) expected.push(label(c));
  }
  assert.deepEqual([...PRODUCT_SENSOR_SEQUENCE], expected);
  assert.deepEqual(validateSensorSequence(PRODUCT_SENSOR_SEQUENCE), []);
  // The served catalogue names the same sequence in the same order (row-major logical order).
  assert.deepEqual(sensorMapExample.logicalMatrix.slots.map((s) => s.sensorId), expected);
});

test('T3 I7 appears exactly once, in its normal ordinal position (between I6 and I8)', () => {
  assert.equal(PRODUCT_SENSOR_SEQUENCE.filter((id) => id === 'I7').length, 1);
  assert.equal(PRODUCT_SENSOR_SEQUENCE.indexOf('I7'), 78);
  assert.deepEqual(PRODUCT_SENSOR_SEQUENCE.slice(77, 80), ['I6', 'I7', 'I8']);
  const slot = sensorMapExample.logicalMatrix.slots.find((s) => s.sensorId === 'I7');
  assert.deepEqual([slot.slotId, slot.wall, slot.logicalRow, slot.logicalColumn, slot.wallRow, slot.wallColumn], ['SLOT-R5-C07', 'REAR', 5, 7, 5, 3]);
});

test('T4 I16 appears exactly once, in its normal ordinal position (between I15 and I17)', () => {
  assert.equal(PRODUCT_SENSOR_SEQUENCE.filter((id) => id === 'I16').length, 1);
  assert.equal(PRODUCT_SENSOR_SEQUENCE.indexOf('I16'), 87);
  assert.deepEqual(PRODUCT_SENSOR_SEQUENCE.slice(86, 89), ['I15', 'I16', 'I17']);
  const slot = sensorMapExample.logicalMatrix.slots.find((s) => s.sensorId === 'I16');
  assert.deepEqual([slot.slotId, slot.wall, slot.logicalRow, slot.logicalColumn, slot.wallRow, slot.wallColumn], ['SLOT-R5-C16', 'FRONT', 5, 16, 5, 3]);
});

test('T5 no logical position is a NON_SENSOR_GAP anywhere in the current-facing layers', () => {
  assert.equal(AUTHORITATIVE_TOPOLOGY.NON_SENSOR_GAP_POSITIONS, 0);
  assert.equal(PROTECTED_BASELINE.NON_SENSOR_GAP_SENSOR_IDS.length, 0);
  assert.equal(WALL_MAP_EXPECTED.nonSensorGaps, 0);
  assert.equal(sensorMapExample.logicalMatrix.slots.some((s) => s.positionKind === 'NON_SENSOR_GAP'), false);
  // The superseded gap-anchor key no longer exists on any slot (not even as an explicit null).
  assert.equal(sensorMapExample.logicalMatrix.slots.some((s) => 'gapAnchorForWaterJetId' in s), false);
  assert.equal(sensorMapExample.logicalMatrix.slots.some((s) => 'logicalLabel' in s), false);
  assert.equal(JSON.stringify(sensorMapExample).includes('NON_SENSOR_GAP'), false);
  assert.equal(WALL_MAP.counts.nonSensorGaps, 0);
  assert.equal(WALL_MAP.gapPositions.length, 0);
  assert.equal(schedule.rows.some((r) => r.templateCompatibility === 'GAP_CONFLICT'), false);
  assert.equal(schedule.rows.some((r) => r.rowState === 'DRAWING_TOPOLOGY_CONFLICT'), false);
  assert.equal(schedule.rows.some((r) => (r.notes ?? '').includes('authoritative NON_SENSOR_GAP position')), false);
  assert.equal(counts.productTopologyConflictRows, 0);
});

test('T6 no Sensor identity is duplicated in any layer', () => {
  assert.equal(new Set(PRODUCT_SENSOR_SEQUENCE).size, 108);
  const catalogueIds = sensorMapExample.logicalMatrix.slots.map((s) => s.sensorId);
  assert.equal(new Set(catalogueIds).size, catalogueIds.length);
  const scheduleIds = schedule.rows.filter((r) => r.productCompatible).map((r) => r.sensorId);
  assert.equal(new Set(scheduleIds).size, 108);
  assert.equal(scheduleIds.length, 216);
  assert.equal(WALL_MAP.sensorPositionBySensorId.size, 108);
  assert.equal(validateSensorSequence([...PRODUCT_SENSOR_SEQUENCE.slice(0, 107), 'I7']).some((v) => v.code === TOPOLOGY_REFUSAL.DUPLICATE_SENSOR_ID), true);
});

test('T7 the sequence has no hole, no appended identity and no renumbering', () => {
  const broken = PRODUCT_SENSOR_SEQUENCE.filter((id) => id !== 'I7');
  assert.equal(validateSensorSequence(broken)[0].code, TOPOLOGY_REFUSAL.SEQUENCE_LENGTH);
  const appended = [...broken, 'I7'];
  const appendedCodes = validateSensorSequence(appended).map((v) => v.code);
  assert.equal(appendedCodes.includes(TOPOLOGY_REFUSAL.SEQUENCE_ORDER), true);
  const renumbered = PRODUCT_SENSOR_SEQUENCE.map((id) => (id === 'I16' ? 'I16A' : id));
  const renumberedCodes = validateSensorSequence(renumbered).map((v) => v.code);
  assert.equal(renumberedCodes.includes(TOPOLOGY_REFUSAL.SEQUENCE_ORDER), true);
  assert.equal(renumberedCodes.includes(TOPOLOGY_REFUSAL.SENSOR_MISSING), true);
  const replaced = PRODUCT_SENSOR_SEQUENCE.map((id) => (id === 'I7' ? 'I19' : id));
  assert.equal(validateSensorSequence(replaced).some((v) => v.code === TOPOLOGY_REFUSAL.SENSOR_MISSING), true);
});

test('T8 the U-shaped layout is unchanged except that I7 and I16 are populated Sensor cells', () => {
  assert.deepEqual(WALL_MAP.walls.map((w) => w.wall), ['LEFT', 'REAR', 'RIGHT', 'FRONT']);
  assert.deepEqual(WALL_MAP.walls.map((w) => w.wallColumns), [4, 5, 4, 5]);
  assert.deepEqual(WALL_MAP.counts.sensorsPerWall, { LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 });
  assert.deepEqual(sensorMapExample.logicalMatrix.sensorsPerWall, { LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 });
  const wallOf = (wall) => WALL_MAP.walls.find((w) => w.wall === wall);
  assert.deepEqual(wallOf('REAR').rows[4].map((p) => p.label), ['I5', 'I6', 'I7', 'I8', 'I9']);
  assert.deepEqual(wallOf('FRONT').rows[4].map((p) => p.label), ['I14', 'I15', 'I16', 'I17', 'I18']);
  for (const wall of WALL_MAP.walls) {
    assert.equal(wall.rows.length, 6);
    for (const row of wall.rows) {
      assert.equal(row.length, wall.wallColumns);
      for (const cell of row) assert.equal(cell.selectableAsSensor, true, cell.slotId);
    }
  }
  // Every logical position keeps its slot id, wall, logical and wall coordinates; only I7 / I16 changed kind.
  for (const slot of sensorMapExample.logicalMatrix.slots) {
    const position = WALL_MAP.bySlotId.get(slot.slotId);
    assert.deepEqual([position.wall, position.logicalRow, position.logicalColumn, position.wallRow, position.wallColumn],
      [slot.wall, slot.logicalRow, slot.logicalColumn, slot.wallRow, slot.wallColumn], slot.slotId);
  }
});

test('T9 exactly 216 assigned Product TC channels (108 x 2)', () => {
  assert.equal(AUTHORITATIVE_TOPOLOGY.ASSIGNED_PRODUCT_TC_CHANNELS, 216);
  assert.equal(counts.productCompatibleMeasurements, 216);
  assert.equal(counts.measurementIdentities, 216);
  assert.equal(counts.productSensors * 2, 216);
  assert.equal(WALL_MAP.order.length, 216);
  assert.equal(WALL_MAP.channelOrder.length, 216);
  assert.equal(new Set(WALL_MAP.channelOrder).size, 216);
  const channels = sensorMapExample.logicalMatrix.slots.flatMap((s) => s.tcChannels);
  assert.equal(channels.length, 216);
  assert.equal(new Set(channels).size, 216);
});

test('T10 exactly 240 physical TC channels (6 nodes x 5 modules x 8 channels)', () => {
  assert.equal(AUTHORITATIVE_TOPOLOGY.PHYSICAL_TC_CHANNELS, 240);
  assert.equal(counts.physicalChannels, 240);
  assert.equal(counts.nodes, 6);
  assert.equal(counts.modules, 30);
  assert.equal(new Set(schedule.rows.map((r) => r.physicalChannelKey)).size, 240);
});

test('T11 exactly 24 unassigned physical TC channels (240 - 216)', () => {
  assert.equal(AUTHORITATIVE_TOPOLOGY.UNASSIGNED_PHYSICAL_TC_CHANNELS, 24);
  assert.equal(counts.unassignedChannels, 24);
  assert.equal(counts.unassignedRows, 24);
  assert.equal(counts.physicalChannels - counts.productCompatibleMeasurements, 24);
  for (const row of schedule.rows.filter((r) => r.rowClass === ROW_CLASS.UNASSIGNED)) {
    assert.equal(row.sensorId, null, row.physicalChannelKey);
    assert.equal(row.measurementId, null, row.physicalChannelKey);
  }
});

// ------------------------------------------------------------------ measurements (12-22)

const COMMITTED_ASSIGNMENTS = Object.freeze([
  ['I7', 'F', 'TC-NODE-05|A102.4|7', 'CAB5', '-X110.31', '-X111.31'],
  ['I7', 'R', 'TC-NODE-05|A102.4|8', 'CAB5', '-X110.32', '-X111.32'],
  ['I16', 'F', 'TC-NODE-06|A102.4|7', 'CAB6', '-X110.31', '-X111.31'],
  ['I16', 'R', 'TC-NODE-06|A102.4|8', 'CAB6', '-X110.32', '-X111.32'],
]);

test('M12 I7 Front exists exactly once and is a used Product measurement assignment', () => {
  const rows = schedule.rows.filter((r) => r.measurementId === 'I7:F');
  assert.equal(rows.length, 1);
  assert.equal(rows[0].rowClass, ROW_CLASS.PRODUCT_COMPATIBLE);
  assert.equal(rows[0].physicalChannelKey, 'TC-NODE-05|A102.4|7');
});

test('M13 I7 Rear exists exactly once and is a used Product measurement assignment', () => {
  const rows = schedule.rows.filter((r) => r.measurementId === 'I7:R');
  assert.equal(rows.length, 1);
  assert.equal(rows[0].rowClass, ROW_CLASS.PRODUCT_COMPATIBLE);
  assert.equal(rows[0].physicalChannelKey, 'TC-NODE-05|A102.4|8');
});

test('M14 I16 Front exists exactly once and is a used Product measurement assignment', () => {
  const rows = schedule.rows.filter((r) => r.measurementId === 'I16:F');
  assert.equal(rows.length, 1);
  assert.equal(rows[0].rowClass, ROW_CLASS.PRODUCT_COMPATIBLE);
  assert.equal(rows[0].physicalChannelKey, 'TC-NODE-06|A102.4|7');
});

test('M15 I16 Rear exists exactly once and is a used Product measurement assignment', () => {
  const rows = schedule.rows.filter((r) => r.measurementId === 'I16:R');
  assert.equal(rows.length, 1);
  assert.equal(rows[0].rowClass, ROW_CLASS.PRODUCT_COMPATIBLE);
  assert.equal(rows[0].physicalChannelKey, 'TC-NODE-06|A102.4|8');
});

test('M16 the four assignments are the committed drawing rows - cabinet, module, channel and conductor labels - not invented', () => {
  for (const [sensorId, side, channelKey, cabinet, poleDest, pePair] of COMMITTED_ASSIGNMENTS) {
    const row = schedule.rows.find((r) => r.physicalChannelKey === channelKey);
    assert.equal(row.sensorId, sensorId, channelKey);
    assert.equal(row.side, side, channelKey);
    assert.equal(row.cabinet, cabinet, channelKey);
    assert.equal(row.moduleInstance, 'A102.4', channelKey);
    assert.equal(row.moduleModel, '750-498', channelKey);
    assert.equal(row.polePlusDest, poleDest, channelKey);
    assert.equal(row.pePair, pePair, channelKey);
    assert.equal(row.sheathLabel, `${sensorId}-${side}`, channelKey);
    assert.equal(row.conductorLabel, `${sensorId}-${side}`, channelKey);
    assert.equal(row.rowState, 'DRAWING_VERIFIED', channelKey);
    assert.equal(row.templateCompatibility, 'MATCHED', channelKey);
    assert.equal(row.drawingVerified, true, channelKey);
    assert.equal(row.passBlockedReason, null, channelKey);
    assert.match(row.notes, /Owner critical topology correction/, channelKey);
  }
});

test('M17 every one of the 108 Sensors owns exactly one Front and one Rear measurement, on distinct physical channels', () => {
  for (const sensorId of PRODUCT_SENSOR_SEQUENCE) {
    const rows = schedule.rows.filter((r) => r.productCompatible && r.sensorId === sensorId);
    assert.equal(rows.length, 2, sensorId);
    assert.deepEqual(rows.map((r) => r.side).sort(), ['F', 'R'], sensorId);
    assert.notEqual(rows[0].physicalChannelKey, rows[1].physicalChannelKey, sensorId);
  }
  assert.deepEqual(validateSchedule(schedule, counts), []);
});

test('M18 Front and Rear counts are 108 each and are never merged or substituted', () => {
  const front = schedule.rows.filter((r) => r.productCompatible && r.side === 'F');
  const rear = schedule.rows.filter((r) => r.productCompatible && r.side === 'R');
  assert.equal(front.length, 108);
  assert.equal(rear.length, 108);
  assert.equal(new Set(front.map((r) => r.sensorId)).size, 108);
  assert.equal(new Set(rear.map((r) => r.sensorId)).size, 108);
  for (const sensorId of ['I7', 'I16']) {
    const position = WALL_MAP.sensorPositionBySensorId.get(sensorId);
    assert.notEqual(position.frontKey, position.rearKey);
    assert.notEqual(position.frontChannelKey, position.rearChannelKey);
  }
});

test('M19 no physical channel is assigned to two measurements and no measurement identity is assigned twice', () => {
  const channelKeys = schedule.rows.map((r) => r.physicalChannelKey);
  assert.equal(new Set(channelKeys).size, channelKeys.length);
  const measurementIds = schedule.rows.map((r) => r.measurementId).filter(Boolean);
  assert.equal(new Set(measurementIds).size, measurementIds.length);
  const duplicated = scheduleCsvText.replace('I8,F,DRAWING_VERIFIED', 'I7,F,DRAWING_VERIFIED');
  assert.throws(() => parseSchedule(duplicated), /duplicate Product measurement identity I7:F/);
});

test('M20 the template schedule carries the 216 measurement rows including I7 and I16, in ordinal position', () => {
  const lines = readFileSync(TEMPLATE_PATH, 'utf8').split('\n').filter((l) => l.length > 0);
  const rows = lines.slice(1).map((l) => l.split(','));
  assert.equal(rows.length, 216);
  const ids = rows.map((r) => r[0]);
  assert.deepEqual([...new Set(ids)], [...PRODUCT_SENSOR_SEQUENCE]);
  for (const sensorId of PRODUCT_SENSOR_SEQUENCE) {
    assert.deepEqual(rows.filter((r) => r[0] === sensorId).map((r) => r[5]), ['FRONT', 'REAR'], sensorId);
  }
  const i7 = rows.filter((r) => r[0] === 'I7');
  assert.deepEqual(i7.map((r) => [r[1], r[2], r[3], r[4]]), [['REAR', 'SLOT-R5-C07', '5', '7'], ['REAR', 'SLOT-R5-C07', '5', '7']]);
  const i16 = rows.filter((r) => r[0] === 'I16');
  assert.deepEqual(i16.map((r) => [r[1], r[2], r[3], r[4]]), [['FRONT', 'SLOT-R5-C16', '5', '16'], ['FRONT', 'SLOT-R5-C16', '5', '16']]);
});

test('M21 the unrelated Drawing internal conflicts stay unresolved and are not touched by the correction', () => {
  const internal = schedule.rows.filter((r) => r.rowClass === ROW_CLASS.DRAWING_INTERNAL_CONFLICT);
  assert.equal(internal.length, 12);
  assert.deepEqual(internal.map((r) => r.measurementId).sort(), [
    'G+107:R', 'G+108:F', 'G+116:R', 'G+117:F', 'H16:R', 'H17:F', 'H7:R', 'H8:F', 'J16:R', 'J17:F', 'J7:R', 'J8:F',
  ]);
  for (const row of internal) {
    assert.equal(row.passBlockedReason, 'DRAWING_INTERNAL_CONFLICT_BLOCKED', row.measurementId);
    assert.equal(row.productCompatible, true, row.measurementId);
    assert.equal(['I7', 'I16'].includes(row.sensorId), false, 'I7 / I16 are not among the internal conflicts');
  }
});

test('M22 the Wall Map and the schedule name the same 216 measurements; I7 / I16 Wall cells own their committed channels', () => {
  const scheduleKeys = new Set(schedule.rows.filter((r) => r.productCompatible).map((r) => r.measurementId));
  assert.equal(scheduleKeys.size, 216);
  assert.equal(WALL_MAP.order.every((key) => scheduleKeys.has(key)), true);
  assert.equal([...scheduleKeys].every((key) => WALL_MAP.order.includes(key)), true);
  for (const [sensorId, side, channelKey] of COMMITTED_ASSIGNMENTS) {
    const position = WALL_MAP.sensorPositionBySensorId.get(sensorId);
    assert.equal(side === 'F' ? position.frontChannelKey : position.rearChannelKey, channelKey);
  }
  assert.deepEqual(validateWallMapCounts(WALL_MAP), []);
});

// ------------------------------------------------------------------ central check and regression (35-44)

test('R35 the central check accepts the corrected repository layers together', () => {
  const result = validateTopologyInvariants(ALL_LAYERS);
  assert.deepEqual(result.violations, []);
  assert.equal(result.ok, true);
  assert.equal(requireTopologyInvariants(ALL_LAYERS).ok, true);
  assert.equal(result.authoritative, AUTHORITATIVE_TOPOLOGY);
});

test('R36 a catalogue that still reports 106 Sensors is refused even when the schedule reports 216 measurements', () => {
  const stale = JSON.parse(JSON.stringify(sensorMapExample));
  for (const [slotId, label, anchor] of [['SLOT-R5-C07', 'I7', 'WJ3'], ['SLOT-R5-C16', 'I16', 'WJ1']]) {
    const slot = stale.logicalMatrix.slots.find((s) => s.slotId === slotId);
    Object.assign(slot, { positionKind: 'NON_SENSOR_GAP', sensorId: null, gapAnchorForWaterJetId: anchor, logicalLabel: label });
    delete slot.tcChannels;
    delete slot.scanOrderSynthetic;
  }
  stale.logicalMatrix.sensorLocations = 106;
  stale.logicalMatrix.thermocoupleChannels = 212;
  stale.logicalMatrix.sensorsPerWall = { LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 };
  const result = validateTopologyInvariants({ ...ALL_LAYERS, sensorMap: stale });
  assert.equal(result.ok, false);
  const found = codes(result);
  for (const code of [TOPOLOGY_REFUSAL.CATALOGUE_COUNT, TOPOLOGY_REFUSAL.NON_SENSOR_GAP_PRESENT, TOPOLOGY_REFUSAL.SENSOR_MISSING,
    TOPOLOGY_REFUSAL.LAYER_DISAGREEMENT, TOPOLOGY_REFUSAL.CATALOGUE_WALL_COUNT]) {
    assert.equal(found.includes(code), true, code);
  }
  assert.throws(() => requireTopologyInvariants({ ...ALL_LAYERS, sensorMap: stale }), /topology invariant violation/);
  assert.throws(() => buildWallMap({ label: 'stale', walls: ['LEFT', 'REAR', 'RIGHT', 'FRONT'].map((wall) => ({ wall, slots: stale.logicalMatrix.slots.filter((s) => s.wall === wall) })) }, schedule.rows),
    /every logical position is a Product Sensor/);
});

test('R37 a schedule that still carries the I7 / I16 topology-conflict rows is refused by the central check', () => {
  const revoked = scheduleCsvText
    .split('\n')
    .map((line) => (/,I(7|16),(F|R),DRAWING_VERIFIED,MATCHED,/.test(line) ? line.replace('DRAWING_VERIFIED,MATCHED,', 'DRAWING_TOPOLOGY_CONFLICT,GAP_CONFLICT,') : line))
    .join('\n');
  const parsed = parseSchedule(revoked);
  const revokedCounts = scheduleCounts(parsed);
  assert.equal(revokedCounts.productSensors, 106);
  assert.equal(revokedCounts.productCompatibleMeasurements, 212);
  assert.equal(revokedCounts.matchesProtectedBaseline, false);
  const result = validateTopologyInvariants({ schedule: parsed, scheduleCounts: revokedCounts });
  assert.equal(result.ok, false);
  const found = codes(result);
  for (const code of [TOPOLOGY_REFUSAL.SCHEDULE_ASSIGNED_COUNT, TOPOLOGY_REFUSAL.SCHEDULE_SENSOR_COUNT, TOPOLOGY_REFUSAL.NON_SENSOR_GAP_PRESENT, TOPOLOGY_REFUSAL.FRONT_REAR_PAIRING]) {
    assert.equal(found.includes(code), true, code);
  }
  assert.equal(result.violations.some((v) => /Sensor I7 must own exactly one Front and one Rear/.test(v.message)), true);
  assert.equal(result.violations.some((v) => /Sensor I16 must own exactly one Front and one Rear/.test(v.message)), true);
});

test('R38 a runtime projection that disagrees with the catalogue is refused, field by field', () => {
  assert.deepEqual(validateRuntimeCounts({ sensorCount: 108, logicalPositions: 108, nonSensorGaps: 0, thermocoupleChannels: 216, physicalChannels: 240, unassignedChannels: 24, sensorIds: [...PRODUCT_SENSOR_SEQUENCE] }), []);
  const stale = validateRuntimeCounts({ sensorCount: 106, nonSensorGaps: 2, thermocoupleChannels: 212, sensorIds: PRODUCT_SENSOR_SEQUENCE.filter((id) => id !== 'I7' && id !== 'I16') });
  assert.equal(stale.length >= 5, true);
  assert.equal(stale.every((v) => v.code === TOPOLOGY_REFUSAL.RUNTIME_COUNT), true);
  assert.equal(stale.some((v) => /Sensor I7 exactly once/.test(v.message)), true);
  assert.equal(stale.some((v) => /Sensor I16 exactly once/.test(v.message)), true);
  const mixed = validateTopologyInvariants({ ...ALL_LAYERS, runtime: { sensorCount: 106, sensorIds: PRODUCT_SENSOR_SEQUENCE.slice(0, 106) } });
  assert.equal(mixed.ok, false);
  assert.equal(codes(mixed).includes(TOPOLOGY_REFUSAL.LAYER_DISAGREEMENT), true, 'catalogue 108 vs runtime 106 is a layer disagreement');
});

test('R39 the authoritative constants are arithmetically consistent and agree with the protected schedule baseline', () => {
  const A = AUTHORITATIVE_TOPOLOGY;
  assert.equal(A.PRODUCT_SENSORS * A.MEASUREMENTS_PER_SENSOR, A.ASSIGNED_PRODUCT_TC_CHANNELS);
  assert.equal(A.PHYSICAL_TC_CHANNELS - A.ASSIGNED_PRODUCT_TC_CHANNELS, A.UNASSIGNED_PHYSICAL_TC_CHANNELS);
  assert.equal(A.FRONT_MEASUREMENTS + A.REAR_MEASUREMENTS, A.ASSIGNED_PRODUCT_TC_CHANNELS);
  assert.equal(Object.values(A.SENSORS_PER_WALL).reduce((s, n) => s + n, 0), A.PRODUCT_SENSORS);
  assert.equal(PROTECTED_BASELINE.PRODUCT_SENSORS, A.PRODUCT_SENSORS);
  assert.equal(PROTECTED_BASELINE.PRODUCT_COMPATIBLE_MEASUREMENTS, A.ASSIGNED_PRODUCT_TC_CHANNELS);
  assert.equal(PROTECTED_BASELINE.MAPPED_ROWS, A.ASSIGNED_PRODUCT_TC_CHANNELS);
  assert.equal(PROTECTED_BASELINE.UNASSIGNED_ROWS, A.UNASSIGNED_PHYSICAL_TC_CHANNELS);
  assert.equal(PROTECTED_BASELINE.PRODUCT_TOPOLOGY_CONFLICT_ROWS, 0);
  assert.equal(PROTECTED_BASELINE.DRAWING_VERIFIED_ROWS + PROTECTED_BASELINE.DRAWING_INTERNAL_CONFLICT_ROWS + PROTECTED_BASELINE.UNASSIGNED_ROWS, 240);
  assert.equal(counts.matchesProtectedBaseline, true);
});

test('R40 the committed sensor-map example is reproduced exactly by the Node reference generator (108 form)', async () => {
  const { buildSensorMapExample, serializeLikeContractJson, CORRECTED } = await import('../../../tools/sensor-map-example/generate.mjs');
  const generated = serializeLikeContractJson(buildSensorMapExample(CORRECTED));
  const committed = readFileSync(join(HERE, '..', '..', '..', 'config', 'examples', 'sensor-map.example.json'), 'utf8');
  assert.equal(generated, committed);
  assert.equal(CORRECTED.sensorLocations, 108);
  assert.equal(CORRECTED.deviceCounts.reduce((s, n) => s + n, 0), 108);
});

test('R41 I7 and I16 carry two synthetic example channels each in the catalogue, like every other Sensor, and no channel is shared', () => {
  const byId = new Map(sensorMapExample.logicalMatrix.slots.map((s) => [s.sensorId, s]));
  for (const sensorId of ['I7', 'I16', 'I6', 'J18']) {
    const slot = byId.get(sensorId);
    assert.equal(slot.positionKind, 'SENSOR', sensorId);
    assert.equal(Array.isArray(slot.tcChannels) && slot.tcChannels.length === 2, true, sensorId);
    assert.notEqual(slot.tcChannels[0], slot.tcChannels[1], sensorId);
    assert.equal(typeof slot.scanOrderSynthetic, 'number', sensorId);
  }
  assert.equal(byId.get('I7').scanOrderSynthetic, 79);
  assert.equal(byId.get('I16').scanOrderSynthetic, 88);
  assert.equal(byId.get('J18').scanOrderSynthetic, 108);
});

test('R42 the Wall Map refuses a served matrix with 107 or 109 positions and a schedule with a missing I16 row', () => {
  const served = JSON.parse(JSON.stringify(servedSensorMap()));
  const front = served.walls.find((w) => w.wall === 'FRONT');
  front.slots = front.slots.filter((s) => s.slotId !== 'SLOT-R6-C18');
  assert.throws(() => buildWallMap(served, schedule.rows), /missing the position at wall row 6, wall column 5/);
  const withoutI16Rear = schedule.rows.filter((r) => r.measurementId !== 'I16:R');
  assert.throws(() => buildWallMap(servedSensorMap(), withoutI16Rear), /no Rear measurement exists for wall position 'I16'/);
  const withoutI7Front = schedule.rows.filter((r) => r.measurementId !== 'I7:F');
  assert.throws(() => buildWallMap(servedSensorMap(), withoutI7Front), /no Front measurement exists for wall position 'I7'/);
});

test('R43 the K3 evidence-honesty positions are untouched by the topology correction', async () => {
  const evidence = await import('../src/tcAcquisitionEvidence.mjs');
  const profile = evidence.buildTcAcquisitionEvidence ? evidence.buildTcAcquisitionEvidence() : null;
  const text = JSON.stringify(profile ?? evidence);
  for (const token of ['PROTOCOL_UNRESOLVED', 'DERIVATION_BLOCKED', 'RECORD_TO_RECORD_CONSISTENCY_ONLY', 'WIRE_BREAK_DECODING_BLOCKED']) {
    assert.equal(text.includes(token), true, token);
  }
  for (const token of ['startWord', 'endWord', 'OWNER_SOURCE_LOCATED_NOT_EXTRACTED', '"port":502']) {
    assert.equal(text.includes(token), false, token);
  }
});

test('R44 the K2 TC Node Configuration is unchanged: six nodes, 40 channels each, 240 total, no topology count reinterpreted', async () => {
  const k2 = await import('../src/tcNodeConfiguration.mjs');
  const text = JSON.stringify(Object.fromEntries(Object.entries(k2).filter(([, v]) => typeof v !== 'function')));
  assert.equal(text.includes('TC-NODE-01') && text.includes('TC-NODE-06'), true);
  assert.equal(text.includes('NON_SENSOR_GAP'), false);
  assert.equal(/"?\b106\b"?/.test(text.replace(/192\.0\.2\.1\d/g, '')), false, 'no 106 Sensor count in K2');
  assert.equal(counts.nodes, 6);
  for (const node of (await import('./helpers/schedule.mjs')).nodes) assert.equal(node.physicalChannels, 40, node.nodeId);
});
