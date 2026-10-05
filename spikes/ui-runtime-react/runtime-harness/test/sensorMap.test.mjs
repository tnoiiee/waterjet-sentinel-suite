// WJSS Stage 0.2.1A — canonical Sensor map, Cannon exclusion, and workload determinism tests.
// SYNTHETIC SPIKE TESTS — not Production validation.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CANNON_IDS, EXPECTED, LOGICAL_ROWS, SENSOR_IDS, WALL_GROUPS, getSensorMap, wallForColumn } from '../../contracts/sensorMap.mjs';
import { validateSnapshot, validateWallMap } from '../../contracts/validate.mjs';
import { SyntheticRuntime } from '../src/runtime.mjs';
import { buildPollPlan, buildSensors, isOscillationSample } from '../src/config.mjs';

const map = getSensorMap();
const range = (a, b) => Array.from({ length: b - a + 1 }, (_, i) => a + i);
const dupes = (xs) => xs.filter((x, i) => xs.indexOf(x) !== i);

test('canonical counts: 106 Sensors, 212 channels, 24/29/24/29, 2 Cannons, 108 slots', () => {
  assert.equal(map.sensors.length, 106);
  assert.equal(map.slots.length, 108);
  assert.equal(map.slots.filter((s) => s.slotType === 'CANNON').length, 2);
  assert.equal(map.slots.filter((s) => s.slotType === 'SENSOR').length, 106);
  const ch = map.sensors.flatMap((s) => [s.tcFrontChannel, s.tcRearChannel]);
  assert.equal(ch.length, 212);
  assert.equal(new Set(ch).size, 212);
  const per = Object.fromEntries(WALL_GROUPS.map((g) => [g.wall, map.sensors.filter((s) => s.wall === g.wall).length]));
  assert.deepEqual(per, { LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 });
  assert.deepEqual(EXPECTED.perWall, per);
});

test('every ID range appears exactly once; I7 and I16 are not Sensors', () => {
  const expected = [
    ...range(201, 218).map((n) => `G+${n}`),
    ...range(101, 118).map((n) => `G+${n}`),
    ...range(1, 18).map((n) => `G${n}`),
    ...range(1, 18).map((n) => `H${n}`),
    ...range(1, 18).filter((n) => n !== 7 && n !== 16).map((n) => `I${n}`),
    ...range(1, 18).map((n) => `J${n}`),
  ];
  assert.equal(expected.length, 106);
  assert.deepEqual([...SENSOR_IDS].sort(), [...expected].sort());
  assert.deepEqual(dupes([...SENSOR_IDS]), []);
  assert.ok(!SENSOR_IDS.includes('I7'));
  assert.ok(!SENSOR_IDS.includes('I16'));
});

test('Cannon slots are at logical I7 (REAR) and I16 (FRONT) and carry no Sensor data', () => {
  const c = map.slots.filter((s) => s.slotType === 'CANNON');
  assert.deepEqual(
    c.map((s) => [s.equipmentId, s.logicalRow, s.logicalColumn, s.wall, s.sensorId]),
    [
      ['CANNON_REAR', 5, 7, 'REAR', null],
      ['CANNON_FRONT', 5, 16, 'FRONT', null],
    ],
  );
  assert.deepEqual([...CANNON_IDS], ['CANNON_REAR', 'CANNON_FRONT']);
  for (const id of CANNON_IDS) assert.ok(!map.sensors.some((s) => s.sensorId === id));
});

test('row order top to bottom: G+2xx, G+1xx, G, H, I, J; column order 1-18; no duplicate slots', () => {
  assert.deepEqual(LOGICAL_ROWS.map((r) => r.key), ['G+2xx', 'G+1xx', 'G', 'H', 'I', 'J']);
  for (const s of map.slots) {
    if (s.slotType !== 'SENSOR') continue;
    assert.equal(s.sensorId, LOGICAL_ROWS[s.logicalRow - 1].id(s.logicalColumn));
  }
  const pos = map.slots.map((s) => `${s.logicalRow}:${s.logicalColumn}`);
  assert.deepEqual(dupes(pos), []);
  assert.deepEqual(dupes(map.slots.map((s) => s.slotId)), []);
  for (let r = 1; r <= 6; r += 1) assert.deepEqual(map.slots.filter((s) => s.logicalRow === r).map((s) => s.logicalColumn), range(1, 18));
});

test('logical column to wall mapping: LEFT 1-4, REAR 5-9, RIGHT 10-13, FRONT 14-18; wall rows 6, no rotation', () => {
  const expectWall = (c) => (c <= 4 ? 'LEFT' : c <= 9 ? 'REAR' : c <= 13 ? 'RIGHT' : 'FRONT');
  for (let c = 1; c <= 18; c += 1) assert.equal(wallForColumn(c).wall, expectWall(c));
  for (const s of map.slots) {
    assert.equal(s.wall, expectWall(s.logicalColumn));
    assert.equal(s.wallRow, s.logicalRow, 'wall row equals logical row');
    assert.equal(s.wallColumn, s.logicalColumn - wallForColumn(s.logicalColumn).firstColumn + 1, 'wall column increases with logical column');
  }
  const dims = Object.fromEntries(WALL_GROUPS.map((g) => [g.wall, [Math.max(...map.slots.filter((s) => s.wall === g.wall).map((s) => s.wallColumn)), new Set(map.slots.filter((s) => s.wall === g.wall).map((s) => s.wallRow)).size]]));
  assert.deepEqual(dims, { LEFT: [4, 6], REAR: [5, 6], RIGHT: [4, 6], FRONT: [5, 6] });
});

test('no duplicate scan order, TC_F, TC_R, or shared Thermocouple channels; synthetic devices cover 212 channels', () => {
  assert.deepEqual(map.sensors.map((s) => s.scanOrder), range(1, 106));
  assert.deepEqual(dupes(map.sensors.map((s) => s.tcFrontChannel)), []);
  assert.deepEqual(dupes(map.sensors.map((s) => s.tcRearChannel)), []);
  const front = new Set(map.sensors.map((s) => s.tcFrontChannel));
  assert.ok(map.sensors.every((s) => !front.has(s.tcRearChannel)), 'no channel is both TC_F and TC_R');
  assert.equal(map.devices.reduce((n, d) => n + d.channelCount, 0), 212);
  assert.ok(map.sensors.every((s) => /^SYN-JET-[1-8]$/.test(s.assignedWaterJet) && /^SYN-VLV-[1-8]$/.test(s.assignedIsolationValve)));
});

test('single mapping source: harness Sensors and poll plan derive from the canonical map', () => {
  const s = buildSensors();
  assert.deepEqual(s.map((x) => x.sensorId), [...SENSOR_IDS]);
  for (const x of s) assert.deepEqual(x.channelOffsets, [x.tcFrontOffset, x.tcRearOffset]);
  const plan = buildPollPlan(s);
  const targets = plan.flatMap((e) => e.updateTargets).filter((t) => t.startsWith('sensor:'));
  assert.equal(targets.length, 106);
  assert.ok(!targets.some((t) => CANNON_IDS.some((c) => t.endsWith(c))));
  assert.equal(s.filter(isOscillationSample).length, 8);
});

test('Snapshot carries 106 Sensors, 212 channels, and a static 108-slot wall map', () => {
  const rt = new SyntheticRuntime({ autoJobs: false });
  const { snap } = rt.snapshot();
  assert.deepEqual(validateSnapshot(snap), []);
  assert.deepEqual(validateWallMap(snap.wallMap), []);
  assert.equal(snap.sensors.length, 106);
  assert.equal(new Set(snap.sensors.flatMap((s) => [s.tcFrontChannel, s.tcRearChannel])).size, 212);
  assert.equal(snap.walls.reduce((n, w) => n + w.total, 0), 106);
  assert.deepEqual(snap.walls.map((w) => [w.wall, w.total]), [['LEFT', 24], ['REAR', 29], ['RIGHT', 24], ['FRONT', 29]]);
  assert.ok(!snap.sensors.some((s) => CANNON_IDS.includes(s.sensorId)));
  const delta = rt.publish({ tick: true });
  assert.ok(!('wallMap' in delta), 'wall map is Snapshot-only');
  rt.stop();
});

test('validator rejects a Cannon presented as a Sensor and a corrupted wall map', () => {
  const rt = new SyntheticRuntime({ autoJobs: false });
  const { snap } = rt.snapshot();
  const bad = structuredClone(snap);
  bad.sensors[0] = { ...bad.sensors[0], sensorId: 'CANNON_REAR' };
  assert.ok(validateSnapshot(bad).some((e) => /Cannon/.test(e)));
  const bad2 = structuredClone(snap);
  const i7 = bad2.wallMap.findIndex((s) => s.equipmentId === 'CANNON_REAR');
  bad2.wallMap[i7] = { ...bad2.wallMap[i7], slotType: 'SENSOR', sensorId: 'I7', equipmentId: null };
  assert.ok(validateSnapshot(bad2).some((e) => /I7 must be Cannon/.test(e)));
  rt.stop();
});

test('Cannons are never quality, alarm, queue, or job targets', () => {
  const rt = new SyntheticRuntime({ autoJobs: false });
  for (const id of CANNON_IDS) {
    for (const [cmd, params] of [
      ['enqueue', { sensorId: id }],
      ['start-job', { sensorId: id }],
      ['raise-alarm', { sensorId: id }],
      ['force-quality', { sensorId: id, quality: 'BAD' }],
      ['disable-sensor', { sensorId: id }],
    ]) {
      const r = rt.dispatch(cmd, params);
      assert.equal(r.accepted, false, `${cmd} ${id}`);
      assert.equal(r.reason, 'CANNON_NOT_A_SENSOR', `${cmd} ${id}`);
    }
  }
  assert.equal(rt.queueOrder.length, 0);
  assert.equal(rt.activeJobs.length, 0);
  assert.equal(rt.alarms.size, 0);
  const q = rt.dispatch('quality-showcase', { enabled: true });
  assert.deepEqual(Object.keys(q.detail), ['G+201', 'G+202', 'G+203', 'G+204']);
  rt.stop();
});

test('dirty 30% / 70% modes are computed over the 106 Sensors', () => {
  const rt = new SyntheticRuntime({ autoJobs: false });
  for (const [mode, n] of [
    ['dirty30', Math.round(106 * 0.3)],
    ['dirty70', Math.round(106 * 0.7)],
  ]) {
    rt.applyDirtyMode(mode);
    const targets = [...rt.proc.values()].map((p) => p.target);
    assert.equal(targets.length, 106);
    assert.equal(targets.filter((t) => t >= 60).length, n, mode);
  }
  assert.equal(Math.round(106 * 0.3), 32);
  assert.equal(Math.round(106 * 0.7), 74);
  rt.stop();
});

test('synthetic process workload is deterministic for the same seed, timeline, and configuration', () => {
  const run = (seed) => {
    const rt = new SyntheticRuntime({ autoJobs: false, seed });
    const trace = [];
    const timeline = ['normal', 'dirty30', 'oscillate', 'dirty70', 'normal'];
    for (const mode of timeline) {
      rt.applyDirtyMode(mode);
      for (let i = 0; i < 20; i += 1) {
        rt.tickCount += 1;
        rt.advanceProcess();
      }
      trace.push([...rt.proc.values()].map((p) => Math.round(p.trueScore * 1e6)));
    }
    const latency = [...rt.devices.values()].map((d) => Array.from({ length: 10 }, () => d.rng.range(0, 1)));
    const noise = [...rt.noiseRng.values()].map((r) => Array.from({ length: 10 }, () => r.next()));
    rt.stop();
    return { trace, latency, noise };
  };
  const a = run(2101);
  const b = run(2101);
  assert.deepEqual(a.trace, b.trace);
  assert.deepEqual(a.latency, b.latency);
  assert.deepEqual(a.noise, b.noise);
  assert.notDeepEqual(run(2102).trace, a.trace, 'different seed gives a different workload');
  // Streams are independent per device: device latency sequences differ from each other.
  assert.notDeepEqual(a.latency[0], a.latency[1]);
});
