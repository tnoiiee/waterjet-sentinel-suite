import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { validateEnvelope, canonicalSensorIds, sensorIdFor, validateSensorMapExample, sensorMapSummary } from '../validate.mjs';

const fixture = (name) => JSON.parse(readFileSync(new URL(`../../fixtures/${name}`, import.meta.url), 'utf8'));

test('canonical sensor id set has 106 unique ids, no gap identities', () => {
  const ids = canonicalSensorIds();
  assert.equal(ids.length, 106);
  assert.equal(new Set(ids).size, 106);
  assert.ok(!ids.includes('I7'));
  assert.ok(!ids.includes('I16'));
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

test('a gap identity as sensorId is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.sensors[0].sensorId = 'I7';
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('canonical Sensor')));
});

test('snapshot carries the approved topology: 8 WJs, 8 IVs, ordinal pairing, opposite-wall coverage', () => {
  const snapshot = fixture('snapshot.seed0.json');
  assert.equal(snapshot.waterJets.length, 8);
  assert.equal(snapshot.isolationValves.length, 8);
  snapshot.waterJets.forEach((w, i) => {
    assert.equal(w.waterJetId, `WJ${i + 1}`);
    assert.equal(w.dedicatedIsolationValveId, `IV${i + 1}`);
    assert.equal(w.targetWall, w.installedWall === 'LEFT' ? 'RIGHT' : w.installedWall === 'RIGHT' ? 'LEFT' : w.installedWall === 'REAR' ? 'FRONT' : 'REAR');
  });
  snapshot.isolationValves.forEach((v, i) => assert.equal(v.servedWaterJetId, `WJ${i + 1}`));
});

test('a WJn not paired with IVn is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.waterJets[2].dedicatedIsolationValveId = 'IV4';
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('must pair with IV3')));
});

test('a Sensor assigned a device that does not target its wall is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const rearLower = snapshot.sensors.find((s) => s.wall === 'REAR' && s.logicalRow === 5);
  assert.equal(rearLower.assignedWaterJetId, 'WJ1'); // rear-lower Sensor -> WJ1 (WJ1 installs FRONT-lower)
  rearLower.assignedWaterJetId = 'WJ3';
  rearLower.assignedIsolationValveId = 'IV3';
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('does not target this Sensor')));
});

test('an assigned isolation valve that breaks the WJn/IVn pairing is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.sensors[0].assignedIsolationValveId = 'IV9';
  const result = validateEnvelope(snapshot);
  assert.ok(result.issues.some((i) => i.includes('derive from the WJn')));
});

test('waterJets in a Delta is rejected (immutable topology, Snapshot-only)', () => {
  const delta = { ...fixture('delta.basic.json'), waterJets: fixture('snapshot.seed0.json').waterJets };
  const result = validateEnvelope(delta);
  assert.ok(result.issues.some((i) => i.includes('waterJets must never appear in a Delta')));
});

test('isolationValves in a Delta is rejected (immutable topology, Snapshot-only)', () => {
  const delta = { ...fixture('delta.basic.json'), isolationValves: fixture('snapshot.seed0.json').isolationValves };
  const result = validateEnvelope(delta);
  assert.ok(result.issues.some((i) => i.includes('isolationValves must never appear in a Delta')));
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
    { slots: s.slots, sensors: s.sensors, nonSensorGaps: s.nonSensorGaps, totalChannels: s.totalChannels, distinctChannels: s.distinctChannels },
    { slots: 108, sensors: 106, nonSensorGaps: 2, totalChannels: 212, distinctChannels: 212 },
  );
  assert.deepEqual(s.sensorsPerWall, { LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 });
  assert.deepEqual(s.gapLogicalLabels, ['I7', 'I16']); // canonical orderTotal order, never lexicographic
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

test('NON_SENSOR_GAP slots must not carry channels and sensor slots must carry them', () => {
  const gapWith = validateSensorMapExample(
    mutateSlots((slots) => { slots.find((x) => x.positionKind === 'NON_SENSOR_GAP').tcChannels = ['SYN-TC-01:CH99', 'SYN-TC-01:CH98']; }),
  );
  assert.ok(gapWith.issues.some((i) => i.includes('NON_SENSOR_GAP slot must not carry thermocouple channels')));

  const missing = validateSensorMapExample(
    mutateSlots((slots) => { delete slots[0].tcChannels; }),
  );
  assert.ok(missing.issues.some((i) => i.includes('must carry a tcChannels array')));
});

test('a gap position disguised as a sensor (wrong shape) is rejected', () => {
  const result = validateSensorMapExample(
    mutateSlots((slots) => {
      slots[0].positionKind = 'NON_SENSOR_GAP';
      slots[0].sensorId = null;
      slots[0].gapAnchorForWaterJetId = 'WJ3';
    }),
  );
  // 3 gaps / 105 sensors composition + the displaced sensor totals: all rejected
  assert.ok(result.issues.some((i) => i.includes('sensor slots must number 106')));
  assert.ok(result.issues.some((i) => i.includes('NON_SENSOR_GAP slots must number 2')));
});

test('a gap anchor that contradicts its position is rejected (I7 anchors WJ3, I16 anchors WJ1)', () => {
  const result = validateSensorMapExample(
    mutateSlots((slots) => {
      slots.find((x) => x.logicalLabel === 'I7').gapAnchorForWaterJetId = 'WJ1';
    }),
  );
  assert.ok(result.issues.some((i) => i.includes('gap anchor')));
});

test('validateEnvelope dispatches the sensor-map kind', () => {
  const result = validateEnvelope(sensorMapDoc());
  assert.equal(result.kind, 'sensor-map');
  assert.deepEqual(result.issues, []);
});

test('concurrent valve and axis commands accept either feedback order without invented confirmation', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const sr = snapshot.activeJob.safeReturn;
  assert.ok(sr.valve.commandSeq < sr.axis.commandSeq);
  assert.ok(sr.axis.commandSeq < sr.valve.feedbackSeq);
  assert.deepEqual(validateEnvelope(snapshot).issues, []);
  sr.valve.commandSeq = sr.axis.commandSeq;
  assert.ok(validateEnvelope(snapshot).issues.some((issue) => issue.includes('close command must precede axis')));
  sr.valve.commandSeq = 11;
  sr.valve.resolution = 'CLOSED_BY_PRESSURE';
  sr.valve.feedback = 'NOT_CONFIRMED';
  sr.valve.feedbackSeq = null;
  assert.deepEqual(validateEnvelope(snapshot).issues, []);
});


test('Pump and paired IVn outlets are separate sources with a strict Pump setpoint', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const job = snapshot.activeJob;
  assert.equal(job.pumpPressureSourceId, 'PUMP_OUTLET');
  assert.equal(job.valvePressureSourceId, `${job.valveId}_OUTLET`);
  assert.ok(job.pumpOutletPressureBar > job.pumpReadySetpointBar);
  job.pumpPressureSourceId = job.valvePressureSourceId;
  assert.ok(validateEnvelope(snapshot).issues.some(i => i.includes('own measured outlet')));
  job.pumpPressureSourceId = 'PUMP_OUTLET';
  job.valvePressureSourceId = 'IV8_OUTLET';
  assert.ok(validateEnvelope(snapshot).issues.some(i => i.includes('paired IVn outlet')));
});

test('released dual-fault Job needs both remarks and a post-command SR3 in either feedback order', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const o = {
    outcome: 'COMPLETED', valveCloseCommandSeq: 11, axisReturnCommandSeq: 12,
    valveClosedConfirmedSeq: null, valveCloseResolution: 'CLOSED_BY_PRESSURE',
    standbyConfirmedSeq: 15, outcomeSeq: 16, releaseSeq: 17,
    qualifiedCompletion: 'COMPLETE_WITH_MULTIPLE_VALVE_LIMIT_FAULTS',
    qualifiedRemarks: ['COMPLETED_WITH_VALVE_OPEN_LIMIT_UPPER_FAULT', 'COMPLETED_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT'],
    equipmentFaults: [{ diagnosis: 'UPPER_LIMIT_SENSOR_FAULT' }, { diagnosis: 'LOWER_LIMIT_SENSOR_FAULT' }],
    events: [{ seq: 14, step: 'SR3', event: 'CLOSED_BY_PRESSURE' }],
  };
  snapshot.sequence.lastJobOutcome = o;
  assert.deepEqual(validateEnvelope(snapshot).issues, []);
  o.events = [{ seq: 15, step: 'SR3', event: 'CLOSED_BY_PRESSURE' }];
  o.standbyConfirmedSeq = 14;
  assert.deepEqual(validateEnvelope(snapshot).issues, []);
  o.qualifiedRemarks = ['COMPLETED_WITH_VALVE_OPEN_LIMIT_UPPER_FAULT'];
  assert.ok(validateEnvelope(snapshot).issues.some(i => i.includes('both distinct diagnoses and remarks')));
  o.qualifiedRemarks.push('COMPLETED_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT');
  o.valveClosedConfirmedSeq = 15;
  assert.ok(validateEnvelope(snapshot).issues.some(i => i.includes('no Lower confirmation')));
});
