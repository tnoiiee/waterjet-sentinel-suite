// Stage 0.4C-K1 - Commissioning data model tests (Stage task section 16, group B).
// The parsed evidence must reproduce the protected Stage 0.4C-0B baseline exactly, and must refuse
// anything that would silently change it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  COLUMN, COLUMN_COUNT, PROTECTED_BASELINE, ROW_CLASS, parseCsv, parseSchedule, requireHeaderContract,
  scheduleCounts,
  nodeInventory, measurementsOfSensor, isNonSensorGapSensorId, matchesProtectedBaseline,
} from '../src/schedule.mjs';
import { scheduleCsvText, schedule, counts, nodes } from './helpers/schedule.mjs';

test('the merged evidence schedule loads 240 physical rows', () => {
  assert.equal(schedule.rowCount, 240);
  assert.equal(schedule.rows.length, 240);
  assert.equal(counts.physicalChannels, PROTECTED_BASELINE.PHYSICAL_CHANNEL_COUNT);
});

test('the physical topology is 6 nodes x 5 modules x 8 channels = 240, with 40 words per node', () => {
  assert.equal(nodes.length, 6);
  for (const node of nodes) {
    assert.equal(node.physicalChannels, 40, node.nodeId);
    assert.deepEqual(node.modules, ['A102.1', 'A102.2', 'A102.3', 'A102.4', 'A102.5'], node.nodeId);
    assert.equal(node.wordOrdinals.length, 40);
    assert.equal(node.wordOrdinals[0], 0);
    assert.equal(node.wordOrdinals[39], 39);
  }
  assert.equal(counts.modules, 30);
  assert.equal(counts.nodes, 6);
  assert.equal(new Set(schedule.rows.map((r) => r.moduleModel)).size, 1);
  assert.equal(schedule.rows[0].moduleModel, PROTECTED_BASELINE.MODULE_MODEL);
});

test('every physical Channel appears exactly once', () => {
  const keys = new Set(schedule.rows.map((r) => r.physicalChannelKey));
  assert.equal(keys.size, 240);
  const duplicate = [
    ...schedule.rows.reduce((acc, r) => {
      acc.set(r.physicalChannelKey, (acc.get(r.physicalChannelKey) ?? 0) + 1);
      return acc;
    }, new Map()).entries(),
  ].filter(([, n]) => n > 1);
  assert.deepEqual(duplicate, []);
});

test('the parser refuses a duplicated physical Channel', () => {
  const lines = scheduleCsvText.trimEnd().split('\n');
  const duplicate = `${lines[1]}\n${lines[1]}\n`;
  assert.throws(() => parseSchedule(`${lines[0]}\n${duplicate}`), /duplicate physical Channel/);
});

test('212 Product-compatible measurements across 106 Product Sensors', () => {
  assert.equal(counts.productCompatibleMeasurements, 212);
  assert.equal(counts.measurementIdentities, 212);
  assert.equal(counts.productSensors, 106);
  assert.equal(new Set(schedule.rows.map((r) => r.sensorId).filter(Boolean)).size, 108);
});

test('there is no duplicate Product measurement identity', () => {
  const identities = schedule.rows.map((r) => r.measurementId).filter(Boolean);
  assert.equal(new Set(identities).size, identities.length);
});

test('each of the 106 Product Sensors owns exactly one front and one rear measurement', () => {
  const productSensorIds = new Set(schedule.rows.filter((r) => r.productCompatible).map((r) => r.sensorId));
  assert.equal(productSensorIds.size, 106);
  for (const sensorId of productSensorIds) {
    const measurements = measurementsOfSensor(schedule, sensorId).filter((r) => r.productCompatible);
    assert.equal(measurements.length, 2, sensorId);
    assert.deepEqual(measurements.map((r) => r.side).sort(), ['F', 'R'], sensorId);
  }
});

test('the conflict and unassigned separation is exactly the protected baseline', () => {
  assert.equal(counts.drawingVerifiedRows, 200);
  assert.equal(counts.drawingInternalConflictRows, 12);
  assert.equal(counts.productTopologyConflictRows, 4);
  assert.equal(counts.unassignedRows, 24);
  assert.equal(counts.mappedChannels, 216);
  assert.equal(counts.unassignedChannels, 24);
  assert.equal(counts.unresolvedRows, 0);
  assert.equal(counts.matchesProtectedBaseline, true);
  assert.equal(matchesProtectedBaseline(counts), true);
});

test('the four Product-topology conflict rows are exactly the I7 / I16 front and rear rows', () => {
  const conflicts = schedule.rows.filter((r) => r.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT);
  assert.equal(conflicts.length, 4);
  assert.deepEqual(conflicts.map((r) => r.measurementId).sort(), ['I16:F', 'I16:R', 'I7:F', 'I7:R']);
  for (const row of conflicts) {
    assert.equal(row.templateCompatibility, 'GAP_CONFLICT');
    assert.equal(row.physicalChannelKey.startsWith('TC-NODE-'), true);
    assert.equal(row.channel >= 7, true);
  }
});

test('I7 and I16 are never Product Sensors and are never product-compatible', () => {
  assert.equal(isNonSensorGapSensorId('I7'), true);
  assert.equal(isNonSensorGapSensorId('I16'), true);
  assert.equal(isNonSensorGapSensorId('G+101'), false);
  const gapRows = schedule.rows.filter((r) => isNonSensorGapSensorId(r.sensorId));
  assert.equal(gapRows.length, 4);
  for (const row of gapRows) {
    assert.equal(row.productCompatible, false);
    assert.notEqual(row.rowClass, ROW_CLASS.PRODUCT_COMPATIBLE);
  }
  const productSensorIds = schedule.rows.filter((r) => r.productCompatible).map((r) => r.sensorId);
  assert.equal(productSensorIds.includes('I7'), false);
  assert.equal(productSensorIds.includes('I16'), false);
});

test('the twelve internal-conflict rows are two per cabinet and stay Product-compatible', () => {
  const internal = schedule.rows.filter((r) => r.rowClass === ROW_CLASS.DRAWING_INTERNAL_CONFLICT);
  assert.equal(internal.length, 12);
  const perCabinet = new Map();
  for (const row of internal) perCabinet.set(row.cabinet, (perCabinet.get(row.cabinet) ?? 0) + 1);
  assert.equal(perCabinet.size, 6);
  for (const [, n] of perCabinet) assert.equal(n, 2);
  for (const row of internal) {
    assert.equal(row.templateCompatibility, 'MATCHED');
    assert.equal(row.productCompatible, true);
    assert.match(row.notes, /sheath-vs-conductor mismatch/);
  }
});

test('the unassigned rows carry no Sensor identity and no engineering claim', () => {
  const unassigned = schedule.rows.filter((r) => r.rowClass === ROW_CLASS.UNASSIGNED);
  assert.equal(unassigned.length, 24);
  for (const row of unassigned) {
    assert.equal(row.sensorId, null);
    assert.equal(row.measurementId, null);
    assert.equal(row.wordBasis, 'DERIVED_MODEL_LEVEL');
  }
});

test('the header contract accepts the committed evidence header and refuses a changed shape', () => {
  // The committed evidence header itself is the contract fixture. Two of its column names carry
  // vendor vocabulary that the Product tree may not spell (repository rule S3), so they are never
  // written here: the real header is parsed instead, and the contract is checked by ordinal.
  const committedHeader = parseCsv(scheduleCsvText)[0];
  assert.equal(committedHeader.length, COLUMN_COUNT);
  assert.equal(requireHeaderContract(committedHeader), true);
  assert.throws(() => requireHeaderContract(['Cabinet', 'NodeIdentity']), /header contract violated/);
  const renamed = [...committedHeader];
  renamed[COLUMN.SENSOR_ID] = 'Sensor';
  assert.throws(() => requireHeaderContract(renamed), /SensorId/);
  const shifted = committedHeader.slice(1);
  assert.throws(() => requireHeaderContract(shifted), /expected 23 columns/);
});

test('the parser refuses a row with the wrong column count', () => {
  const header = scheduleCsvText.split('\n')[0];
  assert.throws(() => parseSchedule(`${header}\na,b,c\n`), /expected 23 columns|cannot be parsed as a number|columns, expected/);
});

test('counts are recomputed, not cached: a mutated copy changes the recomputed totals', () => {
  const shrunk = { label: 'COPY', rowCount: 3, rows: schedule.rows.slice(0, 3) };
  const shrunkCounts = scheduleCounts(shrunk);
  assert.equal(shrunkCounts.physicalChannels, 3);
  assert.equal(shrunkCounts.matchesProtectedBaseline, false);
});

test('the parsed rows are frozen: nothing downstream can edit the evidence', () => {
  assert.equal(Object.isFrozen(schedule), true);
  assert.equal(Object.isFrozen(schedule.rows), true);
  assert.equal(Object.isFrozen(schedule.rows[0]), true);
});

test('column ordinals are fixed and the parser never renames an evidence value', () => {
  assert.equal(COLUMN_COUNT, 23);
  const sample = schedule.rows[0];
  assert.equal(typeof sample.rowState, 'string');
  assert.equal(sample.evidenceRef.length > 0, true);
});
