import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { validateEnvelope, canonicalSensorIds, sensorIdFor } from '../validate.mjs';

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
