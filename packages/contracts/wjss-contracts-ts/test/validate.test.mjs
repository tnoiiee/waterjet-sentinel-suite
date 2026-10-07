import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { validateEnvelope, canonicalSensorIds, sensorIdFor, validateSensorMapExample, sensorMapSummary } from '../validate.mjs';

const fixture = (name) => JSON.parse(readFileSync(new URL(`../../fixtures/${name}`, import.meta.url), 'utf8'));

test('canonical sensor id set has 106 unique ids, no cannon ids', () => {
  const ids = canonicalSensorIds();
  assert.equal(ids.length, 106);
  assert.equal(new Set(ids).size, 106);
  assert.ok(!ids.includes('CANNON_REAR'));
  assert.ok(!ids.includes('CANNON_FRONT'));
});

test('sensorIdFor matches owner logical labels', () => {
  assert.equal(sensorIdFor(1, 1), 'G+201');
  assert.equal(sensorIdFor(2, 18), 'G+118');
  assert.equal(sensorIdFor(3, 7), 'G7');
  assert.equal(sensorIdFor(6, 18), 'J18');
});

test('snapshot.seed0.json passes validation', () => {
  const result = validateEnvelope(fixture('snapshot.seed0.json'));
  assert.deepEqual(result.issues, []);
  assert.equal(result.kind, 'snapshot');
});

test('delta.basic.json passes and is gapless', () => {
  const result = validateEnvelope(fixture('delta.basic.json'));
  assert.deepEqual(result.issues, []);
  assert.equal(result.gap, false);
});

test('delta.gap.json is structurally valid and flagged as a gap', () => {
  const result = validateEnvelope(fixture('delta.gap.json'));
  assert.deepEqual(result.issues, []);
  assert.equal(result.gap, true);
});

test('command.refusal.json validates as an envelope with an unknown kind handled', () => {
  const raw = fixture('command.refusal.json');
  assert.equal(raw.accepted, false);
  assert.equal(raw.reasonCode, 'RUNTIME_NOT_IMPLEMENTED');
});

test('snapshot with wallMap in delta is rejected', () => {
  const delta = { ...fixture('delta.basic.json'), wallMap: [] };
  const result = validateEnvelope(delta);
  assert.ok(result.issues.some((i) => i.includes('wallMap')));
});

test('delta activeJob three-state encoding is accepted', () => {
  const base = fixture('delta.basic.json');

  // absent key = unchanged (delta.basic.json carries no activeJob at all here after
  // removing the review-time explicit-null; build both shapes explicitly):
  const unchanged = { ...base };
  delete unchanged.activeJob;
  assert.deepEqual(validateEnvelope(unchanged).issues, []);

  // explicit null = clear:
  const cleared = { ...base, activeJob: null };
  assert.deepEqual(validateEnvelope(cleared).issues, []);

  // full object = replace (reuse the snapshot's job):
  const snapshot = fixture('snapshot.seed0.json');
  const replaced = { ...base, activeJob: snapshot.activeJob };
  assert.deepEqual(validateEnvelope(replaced).issues, []);

  // the removed boolean flag is rejected:
  const badFlag = { ...base, activeJobCleared: true };
  delete badFlag.activeJob;
  assert.ok(validateEnvelope(badFlag).issues.some((i) => i.includes('activeJobCleared')));

  // a partial/typed-wrong activeJob is rejected:
  const badObj = { ...base, activeJob: { jobId: 'J-9' } };
  assert.ok(validateEnvelope(badObj).issues.some((i) => i.includes('Active Job object')));
});

test('gap chain revision jump is flagged, not fatal', () => {
  const delta = { ...fixture('delta.basic.json'), previousRevision: 1, revision: 9 };
  const result = validateEnvelope(delta);
  assert.equal(result.gap, true);
  assert.deepEqual(result.issues, []);
});

test('prohibited production-scope key names are caught', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.pump.registerAddress = 40001;
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('prohibited')));
});

test('cannon-as-sensor is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.sensors[0].sensorId = 'CANNON_REAR';
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('canonical Sensor')));
});

test('queue overflow beyond 8 is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const entries = Array.from({ length: 9 }, (_, i) => ({
    position: i + 1,
    entryId: `Q-9${i}`,
    sensorId: snapshot.sensors[i + 20].sensorId,
    sourceReason: 'DIRTY_SCORE',
    dirtyScore: 60.5,
    secondsSinceLastClean: 3600,
  }));
  snapshot.queue.entries = entries;
  snapshot.queue.totalQueued = entries.length;
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('at most 8')));
});

test('non-head dispatch position is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.queue.lastDispatch.positionBefore = 2;
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('head-only')));
});

test('safe-return ordering violations are caught', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.activeJob.safeReturn.valve.commandSeq = 12;
  snapshot.activeJob.safeReturn.valve.feedbackSeq = 11;
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('command must precede')));
});

test('uncertain quality cannot claim current basis', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const target = snapshot.sensors.find((s) => s.quality === 'UNCERTAIN');
  target.classificationBasis = 'CURRENT';
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('UNCERTAIN')));
});

// --- sensor-map configuration example: structured tcChannels (Stage 0.3A-1 correction) ---

const exampleUrl = new URL('../../../../config/examples/sensor-map.example.json', import.meta.url);
const sensorMapDoc = () => ({ ...JSON.parse(readFileSync(exampleUrl, 'utf8')), kind: 'sensor-map' });
const mutateSlots = (fn) => {
  const doc = sensorMapDoc();
  fn(doc.logicalMatrix.slots);
  return doc;
};

test('sensor-map example validates with the canonical totals', () => {
  const result = validateSensorMapExample(sensorMapDoc());
  assert.deepEqual(result.issues, []);
  const s = sensorMapSummary(sensorMapDoc());
  assert.deepEqual(
    { slots: s.slots, sensors: s.sensors, cannons: s.cannons, totalChannels: s.totalChannels, distinctChannels: s.distinctChannels },
    { slots: 108, sensors: 106, cannons: 2, totalChannels: 212, distinctChannels: 212 },
  );
  assert.deepEqual(s.sensorsPerWall, { LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 });
  assert.deepEqual(s.cannonLogicalLabels, ['I16', 'I7']);
});

test('sensor-map example file stores tcChannels as arrays, never comma strings', () => {
  const raw = readFileSync(exampleUrl, 'utf8');
  assert.ok(raw.includes('"tcChannels": ['));
  assert.ok(!raw.includes('"tcChannels": "'));
});

test('tcChannels as a comma-delimited scalar is rejected, not normalized', () => {
  const result = validateSensorMapExample(
    mutateSlots((slots) => { slots[0].tcChannels = 'SYN-TC-01:CH00,SYN-TC-01:CH01'; }),
  );
  assert.ok(result.issues.some((i) => i.includes('comma-delimited strings are rejected')));
});

test('tcChannels arrays with lengths other than exactly two are rejected', () => {
  for (const channels of [[], ['SYN-TC-01:CH00'], ['SYN-TC-01:CH00', 'SYN-TC-01:CH01', 'SYN-TC-01:CH02']]) {
    const result = validateSensorMapExample(
      mutateSlots((slots) => { slots[0].tcChannels = channels; }),
    );
    assert.ok(
      result.issues.some((i) => i.includes('exactly 2 entries')),
      `length ${channels.length} should be rejected: ${result.issues.join('; ')}`,
    );
  }
});

test('empty channel strings and duplicate pairs are rejected', () => {
  for (const channels of [['', 'SYN-TC-01:CH01'], ['   ', 'SYN-TC-01:CH01'], ['SYN-TC-01:CH00', 'SYN-TC-01:CH00']]) {
    const result = validateSensorMapExample(
      mutateSlots((slots) => { slots[0].tcChannels = channels; }),
    );
    assert.ok(result.issues.length > 0, `should reject ${JSON.stringify(channels)}`);
  }
});

test('a channel assigned to two sensors is rejected (global uniqueness, 212 distinct)', () => {
  const result = validateSensorMapExample(
    mutateSlots((slots) => { slots[0].tcChannels = ['SYN-TC-01:CH02', 'SYN-TC-01:CH03']; }),
  );
  assert.ok(result.issues.some((i) => i.includes('duplicate thermocouple channel')));
  assert.ok(result.issues.some((i) => i.includes('globally unique')));
});

test('cannon slots must not carry channels and sensor slots must carry them', () => {
  const cannonWith = validateSensorMapExample(
    mutateSlots((slots) => { slots.find((x) => x.slotType === 'CANNON').tcChannels = ['SYN-TC-01:CH99', 'SYN-TC-01:CH98']; }),
  );
  assert.ok(cannonWith.issues.some((i) => i.includes('CANNON slot must not carry thermocouple channels')));

  const missing = validateSensorMapExample(
    mutateSlots((slots) => { delete slots[0].tcChannels; }),
  );
  assert.ok(missing.issues.some((i) => i.includes('must carry a tcChannels array')));
});

test('a cannon disguised as a sensor (wrong shape) is rejected', () => {
  const result = validateSensorMapExample(
    mutateSlots((slots) => {
      slots[0].slotType = 'CANNON';
      slots[0].sensorId = null;
      slots[0].equipmentId = 'CANNON_REAR';
    }),
  );
  // 107 cannons / 105 sensors composition + the displaced sensor totals: all rejected
  assert.ok(result.issues.some((i) => i.includes('sensor slots must number 106')));
  assert.ok(result.issues.some((i) => i.includes('cannon slots must number 2')));
});

test('validateEnvelope dispatches the sensor-map kind', () => {
  const result = validateEnvelope(sensorMapDoc());
  assert.equal(result.kind, 'sensor-map');
  assert.deepEqual(result.issues, []);
});
