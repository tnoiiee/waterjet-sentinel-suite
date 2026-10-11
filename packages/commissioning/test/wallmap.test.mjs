// Stage 0.4C-K1 - the Commissioning Wall Map model: the approved U-shaped topology, the Front/Rear
// measurement ownership, the deterministic Wall-order navigation and the Loop-Test progress.
//
// These tests prove the TOPOLOGY and the DERIVED COUNTS over the committed evidence. They do not
// render anything and prove nothing about layout or a browser.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  MEASUREMENT_VIEW, POSITION_KIND, PROTECTED_BASELINE, PreviewCommissioningSession, WALL_MAP_EXPECTED,
  WALL_MAP_ORDER_TEXT, WALL_ORDER, buildWallMap, loopTestSummary, visibleMeasurements, wallMeasurementKey,
  wallOrderIndex, wallOrderStep,
} from '../src/index.mjs';
import { ROW_CLASS } from '../src/schedule.mjs';
import { schedule, rowForMeasurement } from './helpers/schedule.mjs';
import { servedSensorMap } from './helpers/wallMap.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));

const WALL_MAP = buildWallMap(servedSensorMap(), schedule.rows);
const wallOf = (wall) => WALL_MAP.walls.find((w) => w.wall === wall);

/** A clone of the served matrix whose slots can be edited, so a refusal can be exercised. */
const mutableServedMap = () => JSON.parse(JSON.stringify(servedSensorMap()));
const slotIn = (payload, slotId) => payload.walls.flatMap((w) => w.slots).find((s) => s.slotId === slotId);

test('the Wall Map carries exactly 108 logical positions: 106 Product Sensors and 2 NON_SENSOR_GAP positions', () => {
  assert.equal(WALL_MAP.positions.length, 108);
  assert.equal(WALL_MAP.counts.logicalPositions, WALL_MAP_EXPECTED.logicalPositions);
  assert.equal(WALL_MAP.counts.productSensors, 106);
  assert.equal(WALL_MAP.counts.nonSensorGaps, 2);
  assert.equal(WALL_MAP.counts.productCompatibleMeasurements, 212);
  assert.equal(WALL_MAP.gapPositions.length, 2);
  // The gaps are exactly I7 and I16, and they are gaps - never Sensors.
  assert.deepEqual(WALL_MAP.gapPositions.map((p) => p.label).sort(), ['I16', 'I7']);
  for (const gap of WALL_MAP.gapPositions) {
    assert.equal(gap.positionKind, POSITION_KIND.NON_SENSOR_GAP);
    assert.equal(gap.sensorId, null, 'a NON_SENSOR_GAP position must never carry a Sensor identity');
    assert.equal(gap.selectableAsSensor, false);
  }
  // The protected baseline the evidence states is the same one this presentation asserts.
  assert.deepEqual(WALL_MAP_EXPECTED.gapLabels, PROTECTED_BASELINE.NON_SENSOR_GAP_SENSOR_IDS);
  assert.equal(PROTECTED_BASELINE.PRODUCT_SENSORS, 106);
});

test('every wall keeps its approved shape: 6 rows, 5/4/4/5 wall columns, 29/24/24/29 Sensors', () => {
  assert.deepEqual(WALL_MAP.walls.map((w) => w.wall), ['LEFT', 'REAR', 'RIGHT', 'FRONT']);
  assert.deepEqual(WALL_MAP.walls.map((w) => w.wallColumns), [4, 5, 4, 5]);
  for (const wall of WALL_MAP.walls) {
    assert.equal(wall.rows.length, 6, wall.wall);
    for (const row of wall.rows) assert.equal(row.length, wall.wallColumns, wall.wall);
    assert.equal(wall.sensorCount + wall.gapCount, wall.rows.flat().length);
  }
  assert.deepEqual(WALL_MAP.counts.sensorsPerWall, { LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 });
  // The gaps sit at wall row 5 (the I row), wall column 3 of their wall.
  const rear = wallOf('REAR');
  const front = wallOf('FRONT');
  assert.equal(rear.rows[4][2].label, 'I7');
  assert.equal(front.rows[4][2].label, 'I16');
  assert.equal(WALL_ORDER.join(','), 'LEFT,REAR,RIGHT,FRONT');
});

test('each wall row carries the approved logical identities in wall-column order (no rotation, no reversal)', () => {
  const firstRow = (wall) => wallOf(wall).rows[0].map((p) => p.label);
  assert.deepEqual(firstRow('LEFT'), ['G+201', 'G+202', 'G+203', 'G+204']);
  assert.deepEqual(firstRow('REAR'), ['G+205', 'G+206', 'G+207', 'G+208', 'G+209']);
  assert.deepEqual(firstRow('RIGHT'), ['G+210', 'G+211', 'G+212', 'G+213']);
  assert.deepEqual(firstRow('FRONT'), ['G+214', 'G+215', 'G+216', 'G+217', 'G+218']);
  const iRow = (wall) => wallOf(wall).rows[4].map((p) => p.label);
  assert.deepEqual(iRow('LEFT'), ['I1', 'I2', 'I3', 'I4']);
  assert.deepEqual(iRow('REAR'), ['I5', 'I6', 'I7', 'I8', 'I9']);
  assert.deepEqual(iRow('RIGHT'), ['I10', 'I11', 'I12', 'I13']);
  assert.deepEqual(iRow('FRONT'), ['I14', 'I15', 'I16', 'I17', 'I18']);
});

test('no Product Sensor identity is duplicated, invented or omitted, and every one owns Front and Rear', () => {
  const sensors = WALL_MAP.positions.filter((p) => p.positionKind === POSITION_KIND.SENSOR).map((p) => p.sensorId);
  assert.equal(new Set(sensors).size, 106);
  const evidenceSensors = [...new Set(schedule.rows.filter((r) => r.productCompatible && r.sensorId).map((r) => r.sensorId))];
  assert.deepEqual([...sensors].sort(), evidenceSensors.sort(), 'the Wall Map must carry exactly the evidence Sensor set');
  for (const position of WALL_MAP.positions) {
    if (position.positionKind !== POSITION_KIND.SENSOR) continue;
    assert.match(position.frontKey, /:F$/);
    assert.match(position.rearKey, /:R$/);
    assert.notEqual(position.frontKey, position.rearKey);
    const front = rowForMeasurement(position.frontKey);
    const rear = rowForMeasurement(position.rearKey);
    assert.equal(front.sensorId, position.sensorId);
    assert.equal(rear.sensorId, position.sensorId);
    assert.equal(front.side, 'F');
    assert.equal(rear.side, 'R');
    assert.equal(WALL_MAP.positionByMeasurementKey.get(position.frontKey), position);
    assert.equal(WALL_MAP.positionByMeasurementKey.get(position.rearKey), position);
  }
  const keys = WALL_MAP.positions.flatMap((p) => [p.frontKey, p.rearKey]);
  assert.equal(new Set(keys).size, keys.length, 'a measurement key may map to only one position');
  assert.equal(keys.length, 216, '108 positions x 2 measurements');
  // The physical Channel key is the ONE selection identity the screen uses, so it must be unique too.
  const channelKeys = WALL_MAP.positions.flatMap((p) => [p.frontChannelKey, p.rearChannelKey]);
  assert.equal(new Set(channelKeys).size, channelKeys.length, 'a physical Channel may map to only one position');
  assert.equal(channelKeys.length, 216);
  for (const position of WALL_MAP.positions) {
    assert.equal(WALL_MAP.positionByMeasurementKey.get(position.frontKey), position);
    assert.equal(WALL_MAP.positionByChannelKey.get(position.frontChannelKey), position);
    assert.equal(WALL_MAP.measurementKeyByChannelKey.get(position.frontChannelKey), position.frontKey);
    assert.equal(wallMeasurementKey(WALL_MAP, position.rearChannelKey), position.rearKey);
    assert.equal(wallMeasurementKey(WALL_MAP, 'not-a-channel'), null);
  }
});

test('the gap positions carry their drawing-conflict rows, never a Sensor measurement', () => {
  for (const gap of WALL_MAP.gapPositions) {
    for (const key of [gap.frontKey, gap.rearKey]) {
      const row = rowForMeasurement(key);
      assert.equal(row.rowClass, ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT, key);
      assert.equal(row.passBlockedReason, 'PRODUCT_TOPOLOGY_CONFLICT_BLOCKED', key);
      assert.equal(row.productCompatible, false, key);
      assert.equal(WALL_MAP.positionByMeasurementKey.get(key), gap);
    }
    assert.equal(WALL_MAP.order.includes(gap.frontKey), false, 'a gap measurement is never part of the Wall order');
    assert.equal(WALL_MAP.order.includes(gap.rearKey), false);
  }
});

test('the Wall order is the approved logical sequence, Front before Rear, with the gaps skipped', () => {
  assert.equal(WALL_MAP.order.length, 212);
  assert.equal(new Set(WALL_MAP.order).size, 212);
  assert.equal(WALL_MAP.orderText, WALL_MAP_ORDER_TEXT);
  assert.deepEqual(WALL_MAP.order.slice(0, 4), ['G+201:F', 'G+201:R', 'G+202:F', 'G+202:R']);
  assert.equal(WALL_MAP.order[WALL_MAP.order.length - 1], 'J18:R');
  assert.equal(WALL_MAP.order.some((key) => key.startsWith('I7:')), false);
  assert.equal(WALL_MAP.order.some((key) => key.startsWith('I16:')), false);
  // The step around the I7 gap skips it entirely: I6:R -> I8:F.
  const i6Rear = WALL_MAP.order.indexOf('I6:R');
  assert.deepEqual(WALL_MAP.order.slice(i6Rear, i6Rear + 2), ['I6:R', 'I8:F']);
  const i15Rear = WALL_MAP.order.indexOf('I15:R');
  assert.deepEqual(WALL_MAP.order.slice(i15Rear, i15Rear + 2), ['I15:R', 'I17:F']);
  // Every key of the order is a Product-compatible measurement of the evidence.
  for (const key of WALL_MAP.order) assert.equal(rowForMeasurement(key).productCompatible, true, key);
  // Deterministic: rebuilding from the same served matrix gives the same sequence.
  assert.deepEqual(buildWallMap(servedSensorMap(), schedule.rows).order, WALL_MAP.order);
});

test('Wall-order navigation is deterministic, starts at the first position and stops at the ends', () => {
  const channelOf = (measurementKey) => {
    const [sensorId, side] = measurementKey.split(':');
    const position = WALL_MAP.sensorPositionBySensorId.get(sensorId);
    return side === 'F' ? position.frontChannelKey : position.rearChannelKey;
  };
  assert.equal(WALL_MAP.order[0], 'G+201:F');
  assert.equal(WALL_MAP.channelOrder.length, 212);
  assert.equal(WALL_MAP.channelOrder[0], channelOf('G+201:F'));
  const fromNothing = wallOrderStep(WALL_MAP, null, 1);
  assert.deepEqual([fromNothing.key, fromNothing.moved, fromNothing.edge], [channelOf('G+201:F'), true, false]);
  // Front first, then Rear of the SAME Sensor - never merged, never skipped.
  assert.equal(wallOrderStep(WALL_MAP, channelOf('G+201:F'), 1).key, channelOf('G+201:R'));
  assert.equal(wallOrderStep(WALL_MAP, channelOf('G+201:R'), 1).key, channelOf('G+202:F'));
  assert.equal(wallOrderStep(WALL_MAP, channelOf('G+201:R'), -1).key, channelOf('G+201:F'));
  assert.equal(wallOrderStep(WALL_MAP, null, -1).key, channelOf('J18:R'));
  const last = wallOrderStep(WALL_MAP, channelOf('J18:R'), 1);
  assert.deepEqual([last.key, last.moved, last.edge], [channelOf('J18:R'), false, true]);
  const first = wallOrderStep(WALL_MAP, channelOf('G+201:F'), -1);
  assert.deepEqual([first.key, first.moved, first.edge], [channelOf('G+201:F'), false, true]);
  // A Channel without a Wall Map position starts the sequence instead of inventing one.
  const unassigned = schedule.rows.find((r) => r.rowClass === ROW_CLASS.UNASSIGNED);
  assert.equal(WALL_MAP.positionByChannelKey.get(unassigned.physicalChannelKey), undefined);
  assert.equal(wallOrderStep(WALL_MAP, unassigned.physicalChannelKey, 1).key, channelOf('G+201:F'));
  assert.equal(wallOrderIndex(WALL_MAP, channelOf('G+202:F')), 2);
  assert.equal(wallOrderIndex(WALL_MAP, channelOf('G+202:R')), 3);
  assert.equal(wallOrderIndex(WALL_MAP, 'I7:F'), -1);
  assert.throws(() => wallOrderStep(WALL_MAP, null, 0), /direction/);
});

test('the Front/Rear view control selects blocks and never merges the two measurements', () => {
  const position = WALL_MAP.sensorPositionBySensorId.get('H7');
  const both = visibleMeasurements(position, MEASUREMENT_VIEW.BOTH);
  assert.deepEqual(both.map((b) => b.key), ['H7:F', 'H7:R']);
  assert.deepEqual(both.map((b) => b.sideLabel), ['Front', 'Rear']);
  assert.deepEqual(visibleMeasurements(position, MEASUREMENT_VIEW.FRONT).map((b) => b.key), ['H7:F']);
  assert.deepEqual(visibleMeasurements(position, MEASUREMENT_VIEW.REAR).map((b) => b.key), ['H7:R']);
  assert.deepEqual(visibleMeasurements(position).map((b) => b.key), ['H7:F', 'H7:R']);
  assert.throws(() => visibleMeasurements(position, 'SIDE'), /unknown measurement view/);
  // A gap position renders its two drawing rows and stays labelled as a gap.
  const gap = WALL_MAP.gapPositions[0];
  assert.deepEqual(visibleMeasurements(gap, MEASUREMENT_VIEW.BOTH).map((b) => b.key), [gap.frontKey, gap.rearKey]);
});

test('the Wall Map refuses a topology it cannot prove', () => {
  const missingSensor = mutableServedMap();
  slotIn(missingSensor, 'SLOT-R1-C01').sensorId = null;
  assert.throws(() => buildWallMap(missingSensor, schedule.rows), /violates the Sensor \/ NON_SENSOR_GAP contract|no Front measurement/);

  const duplicated = mutableServedMap();
  slotIn(duplicated, 'SLOT-R1-C02').sensorId = 'G+201';
  assert.throws(() => buildWallMap(duplicated, schedule.rows), /appears more than once/);

  const wrongGap = mutableServedMap();
  slotIn(wrongGap, 'SLOT-R5-C07').logicalLabel = 'I16';
  assert.throws(() => buildWallMap(wrongGap, schedule.rows), /NON_SENSOR_GAP positions must be exactly I7 and I16|no Front measurement/);

  const gapAsSensor = mutableServedMap();
  slotIn(gapAsSensor, 'SLOT-R5-C07').sensorId = 'I7';
  assert.throws(() => buildWallMap(gapAsSensor, schedule.rows), /violates the Sensor \/ NON_SENSOR_GAP contract/);

  const missingWall = servedSensorMap(['LEFT', 'REAR', 'RIGHT']);
  assert.throws(() => buildWallMap(missingWall, schedule.rows), /four walls/);

  const missingRearRow = schedule.rows.filter((r) => r.measurementId !== 'G+201:R');
  assert.throws(() => buildWallMap(servedSensorMap(), missingRearRow), /no Rear measurement exists/);

  assert.throws(() => buildWallMap(servedSensorMap(), []), /requires the schedule rows/);
});

test('Loop-Test progress is derived from the evidence and the Preview session, never cached', () => {
  const fresh = loopTestSummary(schedule, null);
  assert.equal(fresh.productSensors, 106);
  assert.equal(fresh.productCompatibleMeasurements, 212);
  assert.equal(fresh.total, 212);
  assert.equal(fresh.byState.NOT_TESTED, 200);
  assert.equal(fresh.byState.BLOCKED, 12, 'the 12 Drawing internal-conflict rows cannot PASS');
  assert.equal(fresh.byState.PASS + fresh.byState.FAIL + fresh.byState.PARTIAL, 0);
  assert.equal(fresh.eligibleForPass, 200);
  assert.equal(fresh.blockedFromPass, 12);
  assert.equal(fresh.notLoopTestable, 28, '4 Product-topology conflicts + 24 unassigned Channels');

  const session = new PreviewCommissioningSession({ sessionIndex: 7 });
  const before = loopTestSummary(schedule, session);
  assert.equal(before.byState.NOT_TESTED, 200, 'an untouched session changes nothing');

  session.recordVerdict(rowForMeasurement('G+201:F'), { verdict: 'PASS', remark: 'preview only' });
  session.recordVerdict(rowForMeasurement('G+107:R'), { verdict: 'FAIL' });
  const after = loopTestSummary(schedule, session);
  assert.equal(after.byState.PASS, 1);
  assert.equal(after.byState.FAIL, 1, 'an internal conflict may record FAIL');
  assert.equal(after.byState.BLOCKED, 11, 'a recorded FAIL replaces the evidence BLOCKED state for that row');
  assert.equal(after.byState.NOT_TESTED, 199);
  assert.equal(after.byState.IN_PROGRESS, 0);
  // The summary is presentation only: reading it never records anything.
  assert.equal(session.recordCount, 2);
  // Front and Rear of the same Sensor stay independent.
  session.recordVerdict(rowForMeasurement('G+201:R'), { verdict: 'PARTIAL' });
  const split = loopTestSummary(schedule, session);
  assert.equal(split.byState.PASS, 1);
  assert.equal(split.byState.PARTIAL, 1);
});

test('the Wall Map module is browser-safe: no Node import, no dependency on another module', () => {
  const source = readFileSync(join(HERE, '..', 'src', 'wallmap.mjs'), 'utf8');
  assert.equal(/from\s+['"]node:/.test(source), false, 'no Node built-in may be imported');
  assert.equal(/^\s*import\s/m.test(source), false, 'the module must stay dependency-free so it can be served as-is');
  assert.equal(/fetch\(|XMLHttpRequest|document\./.test(source), false, 'no I/O and no DOM access');
});
