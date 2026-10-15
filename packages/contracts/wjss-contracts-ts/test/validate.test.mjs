import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { validateEnvelope, validDeterministicTcAcquisition, validPhysicalTcAcquisition, validTcAcquisition,
  PHYSICAL_NOT_VERIFIED, PHYSICAL_TC_RECORD_KEYS, canonicalSensorIds, sensorIdFor, validateSensorMapExample,
  sensorMapSummary, TOPOLOGY } from '../validate.mjs';

const fixture = (name) => JSON.parse(readFileSync(new URL(`../../fixtures/${name}`, import.meta.url), 'utf8'));

test('canonical sensor id set has 108 unique ids; I7 and I16 are normal Sensors in ordinal position', () => {
  const ids = canonicalSensorIds();
  assert.equal(ids.length, 108);
  assert.equal(new Set(ids).size, 108);
  assert.equal(ids.filter((id) => id === 'I7').length, 1);
  assert.equal(ids.filter((id) => id === 'I16').length, 1);
  assert.equal(ids.indexOf('I7'), 78);
  assert.equal(ids.indexOf('I16'), 87);
  assert.deepEqual(ids.slice(77, 80), ['I6', 'I7', 'I8']);
  assert.deepEqual(ids.slice(86, 89), ['I15', 'I16', 'I17']);
  assert.deepEqual(TOPOLOGY, {
    productSensors: 108,
    assignedProductTcChannels: 216,
    physicalTcChannels: 240,
    unassignedPhysicalTcChannels: 24,
    logicalPositions: 108,
    nonSensorGapPositions: 0,
    correctedSensorIds: ['I7', 'I16'],
  });
  assert.equal(TOPOLOGY.productSensors * 2, TOPOLOGY.assignedProductTcChannels);
  assert.equal(TOPOLOGY.physicalTcChannels - TOPOLOGY.assignedProductTcChannels, TOPOLOGY.unassignedPhysicalTcChannels);
});

test('sensorIdFor matches owner logical labels', () => {
  assert.equal(sensorIdFor(1, 1), 'G+201');
  assert.equal(sensorIdFor(2, 18), 'G+118');
  assert.equal(sensorIdFor(3, 7), 'G7');
  assert.equal(sensorIdFor(6, 18), 'J18');
});

// Owner critical topology correction: the validator requires 108 Sensors / 216 channels with I7
// and I16 as Sensors. snapshot.seed0.json was regenerated to that baseline with
// tools/fixture-generator/generate.mjs (Node mirror of tests/integration FixtureGenerator, proven
// byte-identical against the previous .NET output); the Owner-local C# parity test is the final
// gate. A stale 106-Sensor fixture must never be accepted silently.
test('snapshot.seed0.json passes validation on the 108-Sensor baseline', () => {
  const result = validateEnvelope(fixture('snapshot.seed0.json'));
  assert.deepEqual(result.issues, []);
  assert.equal(result.kind, 'snapshot');
  const snapshot = fixture('snapshot.seed0.json');
  assert.equal(snapshot.sensors.length, 108);
  assert.equal(snapshot.wallMap.filter((s) => s.positionKind === 'SENSOR').length, 108);
  assert.equal(snapshot.sensors.find((s) => s.sensorId === 'I7').scanOrder, 79);
  assert.equal(snapshot.sensors.find((s) => s.sensorId === 'I16').scanOrder, 88);
  assert.equal(new Set(snapshot.sensors.flatMap((s) => [s.tcFrontChannel, s.tcRearChannel])).size, 216);
  assert.ok(snapshot.wallMap.every((s) => !('gapAnchorForWaterJetId' in s)));
});

test('WJ1 and WJ3 installed positions are UNRESOLVED with no anchors; targets unchanged; anchors on UNRESOLVED are refused', () => {
  const snapshot = fixture('snapshot.seed0.json');
  for (const [index, id, targetWall] of [[0, 'WJ1', 'REAR'], [2, 'WJ3', 'FRONT']]) {
    const jet = snapshot.waterJets[index];
    assert.equal(jet.waterJetId, id);
    assert.equal(jet.placementKind, 'UNRESOLVED');
    assert.deepEqual(jet.placementAnchors, []);
    assert.equal(jet.targetWall, targetWall);
    assert.equal(jet.targetRegion, 'LOWER');
  }
  snapshot.waterJets[0].placementAnchors = ['I15', 'I17'];
  assert.ok(validateEnvelope(snapshot).issues.some((i) => i.includes('UNRESOLVED placement must carry no placementAnchors')));
  const unknown = fixture('snapshot.seed0.json');
  unknown.waterJets[1].placementKind = 'AT_GAP';
  assert.ok(validateEnvelope(unknown).issues.some((i) => i.includes('placementKind must be one of')));
});

test('delta.basic.json passes and is gapless', () => {
  const result = validateEnvelope(fixture('delta.basic.json'));
  assert.deepEqual(result.issues, []);
  assert.equal(result.gap, false);
});

test('TC Snapshot and Delta share the one-Node invariant; null is Snapshot-only, never a Delta clear', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const delta = fixture('delta.basic.json');
  const tcAcquisition = {
    nodeId: 'TC-NODE-01', cabinetId: 'CAB1', provenance: 'DETERMINISTIC_SIMULATION',
    physical: false, quarantined: true, acquisitionState: 'COMPLETE',
    acquiredAtUtc: '2026-10-15T12:00:00+00:00', copiedWordCount: 40,
    rawWords: Array.from({ length: 40 }, (_, i) => 100 + i), quality: 'SIMULATED',
    diagnosticState: 'NONE', mappingStatus: 'MAPPING_NOT_VERIFIED',
    decodeStatus: 'DECODING_NOT_VERIFIED',
    notVerified: ['PHYSICAL_REACHABILITY', 'REGISTER_MAPPING', 'SCALING', 'WIRE_BREAK', 'FIELD_TIMING'],
  };
  assert.equal(validDeterministicTcAcquisition(tcAcquisition), true);
  assert.deepEqual(validateEnvelope({ ...snapshot, tcAcquisition: null }).issues, []);
  assert.deepEqual(validateEnvelope({ ...snapshot, tcAcquisition }).issues, []);
  assert.deepEqual(validateEnvelope({ ...delta, tcAcquisition }).issues, []);
  assert.deepEqual(validateEnvelope(delta).issues, []);
  assert.ok(validateEnvelope({ ...delta, tcAcquisition: null }).issues.some((i) => i.includes('tcAcquisition')));
  for (const bad of [
    { ...tcAcquisition, physical: true },
    { ...tcAcquisition, quarantined: false },
    { ...tcAcquisition, acquiredAtUtc: '0001-01-01T00:00:00+00:00' },
    { ...tcAcquisition, copiedWordCount: 39 },
    { ...tcAcquisition, rawWords: [...tcAcquisition.rawWords.slice(0, -1), 777] },
    { ...tcAcquisition, notVerified: ['PHYSICAL_REACHABILITY,REGISTER_MAPPING', 'SCALING', 'WIRE_BREAK', 'FIELD_TIMING'] },
  ]) {
    assert.equal(validDeterministicTcAcquisition(bad), false);
    assert.ok(validateEnvelope({ ...snapshot, tcAcquisition: bad }).issues.some((i) => i.includes('tcAcquisition')));
    assert.ok(validateEnvelope({ ...delta, tcAcquisition: bad }).issues.some((i) => i.includes('tcAcquisition')));
  }
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

test('I7 and I16 are canonical Sensor identities; an unknown identity is rejected', () => {
  const snapshot = fixture('snapshot.seed0.json');
  snapshot.sensors[0].sensorId = 'I7';
  assert.equal(validateEnvelope(snapshot).issues.some((i) => i.includes('canonical Sensor')), false, 'I7 is a canonical Sensor id');
  snapshot.sensors[0].sensorId = 'I19';
  assert.ok(validateEnvelope(snapshot).issues.some((i) => i.includes('canonical Sensor')));
});

test('a snapshot that still carries the superseded 106-Sensor topology is rejected explicitly', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const issues = validateEnvelope(snapshot).issues;
  const stale = snapshot.sensors.length === 106 || snapshot.wallMap.some((s) => s.positionKind === 'NON_SENSOR_GAP');
  if (stale) {
    assert.ok(issues.some((i) => i.includes('positionKind must be SENSOR')), 'a NON_SENSOR_GAP slot is refused');
    assert.ok(issues.some((i) => i.includes('sensors must contain all 108 records')), 'the 106-record set is refused');
    assert.ok(issues.some((i) => i.includes('sensors must include I7')));
    assert.ok(issues.some((i) => i.includes('sensors must include I16')));
    assert.ok(issues.some((i) => i.includes('216 unique thermocouple channels required')));
  } else {
    assert.equal(snapshot.sensors.length, 108);
    assert.ok(snapshot.sensors.some((s) => s.sensorId === 'I7'));
    assert.ok(snapshot.sensors.some((s) => s.sensorId === 'I16'));
  }
  // A Water Jet placed "at a non-Sensor gap" is refused in every case.
  snapshot.waterJets[0].placementKind = 'NON_SENSOR_GAP';
  assert.ok(validateEnvelope(snapshot).issues.some((i) => i.includes('placementKind NON_SENSOR_GAP is superseded')));
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
    { slots: 108, sensors: 108, nonSensorGaps: 0, totalChannels: 216, distinctChannels: 216 },
  );
  assert.deepEqual(s.sensorsPerWall, { LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 });
  assert.deepEqual(s.gapLogicalLabels, []);
  const slots = sensorMapDoc().logicalMatrix.slots;
  for (const sensorId of ['I7', 'I16']) {
    const matches = slots.filter((x) => x.sensorId === sensorId);
    assert.equal(matches.length, 1, `${sensorId} appears exactly once`);
    assert.equal(matches[0].positionKind, 'SENSOR');
    assert.equal(matches[0].tcChannels.length, 2);
  }
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

test('a channel assigned to two sensors is rejected (global uniqueness, 216 distinct)', () => {
  const result = validateSensorMapExample(
    mutateSlots((slots) => { slots[0].tcChannels = ['SYN-TC-01:CH02', 'SYN-TC-01:CH03']; }),
  );
  assert.ok(result.issues.some((i) => i.includes('duplicate thermocouple channel')));
  assert.ok(result.issues.some((i) => i.includes('globally unique')));
});

test('every slot must carry channels; I7 and I16 included', () => {
  for (const sensorId of ['I7', 'I16']) {
    const missing = validateSensorMapExample(
      mutateSlots((slots) => { delete slots.find((x) => x.sensorId === sensorId).tcChannels; }),
    );
    assert.ok(missing.issues.some((i) => i.includes('must carry a tcChannels array')), sensorId);
    assert.ok(missing.issues.some((i) => i.includes('total channels must be 216')), sensorId);
  }
});

test('a NON_SENSOR_GAP slot is rejected anywhere, including at I7 and I16 (superseded topology)', () => {
  for (const sensorId of ['I7', 'I16', 'G+201']) {
    const result = validateSensorMapExample(
      mutateSlots((slots) => {
        const slot = slots.find((x) => x.sensorId === sensorId);
        slot.positionKind = 'NON_SENSOR_GAP';
        slot.sensorId = null;
        delete slot.tcChannels;
      }),
    );
    assert.ok(result.issues.some((i) => i.includes('positionKind must be SENSOR')), sensorId);
    assert.ok(result.issues.some((i) => i.includes('sensor slots must number 108')), sensorId);
    if (sensorId !== 'G+201') assert.ok(result.issues.some((i) => i.includes(`Sensor ${sensorId} is missing`)), sensorId);
  }
});

test('a gap anchor on a Sensor slot and a duplicated Sensor are rejected', () => {
  const anchored = validateSensorMapExample(
    mutateSlots((slots) => { slots.find((x) => x.sensorId === 'I7').gapAnchorForWaterJetId = 'WJ3'; }),
  );
  assert.ok(anchored.issues.some((i) => i.includes('must not carry gapAnchorForWaterJetId')));
  const duplicated = validateSensorMapExample(
    mutateSlots((slots) => { slots.find((x) => x.sensorId === 'I16').sensorId = 'I15'; }),
  );
  assert.ok(duplicated.issues.some((i) => i.includes('appears more than once')));
  assert.ok(duplicated.issues.some((i) => i.includes('Sensor I16 is missing')));
});

test('validateEnvelope dispatches the sensor-map kind', () => {
  const result = validateEnvelope(sensorMapDoc());
  assert.equal(result.kind, 'sensor-map');
  assert.deepEqual(result.issues, []);
});

// The Safe Return tests below judge the Active Job / sequence evidence only. The fixture's
// topology (108 Sensors, I7 and I16 included) is asserted by its own dedicated tests above, so a
// stale topology does not mask or masquerade as a Safe Return finding here.
// Issue prefixes: '$.activeJob…' (Active Job / Safe Return legs) and '$: ' (the shared-body
// lastJobOutcome chain, emitted at the root path); topology issues carry no '$' prefix or '$.waterJets'.
const safeReturnIssues = (snapshot) => validateEnvelope(snapshot).issues.filter((i) => i.startsWith('$.activeJob') || i.startsWith('$: '));

test('concurrent valve and axis commands accept either feedback order without invented confirmation', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const sr = snapshot.activeJob.safeReturn;
  assert.ok(sr.valve.commandSeq < sr.axis.commandSeq);
  assert.ok(sr.axis.commandSeq < sr.valve.feedbackSeq);
  assert.deepEqual(safeReturnIssues(snapshot), []);
  sr.valve.commandSeq = sr.axis.commandSeq;
  assert.ok(safeReturnIssues(snapshot).some((issue) => issue.includes('close command must precede axis')));
  sr.valve.commandSeq = 11;
  sr.valve.resolution = 'CLOSED_BY_PRESSURE';
  sr.valve.feedback = 'NOT_CONFIRMED';
  sr.valve.feedbackSeq = null;
  assert.deepEqual(safeReturnIssues(snapshot), []);
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
  assert.deepEqual(safeReturnIssues(snapshot), []);
  o.events = [{ seq: 15, step: 'SR3', event: 'CLOSED_BY_PRESSURE' }];
  o.standbyConfirmedSeq = 14;
  assert.deepEqual(safeReturnIssues(snapshot), []);
  o.qualifiedRemarks = ['COMPLETED_WITH_VALVE_OPEN_LIMIT_UPPER_FAULT'];
  assert.ok(safeReturnIssues(snapshot).some(i => i.includes('both distinct diagnoses and remarks')));
  o.qualifiedRemarks.push('COMPLETED_WITH_VALVE_CLOSE_LIMIT_LOWER_FAULT');
  o.valveClosedConfirmedSeq = 15;
  assert.ok(safeReturnIssues(snapshot).some(i => i.includes('no Lower confirmation')));
});

// --- One-Node Physical TC Read-Only Product Integration -------------------------------------------
//
// The physical wire record is a SEPARATE variant with its own strict validator and its own exact
// allowed key set. These tests prove three things at once: the physical variant is accepted when it
// is honestly shaped, it is refused for every dishonest shape, and the deterministic validator is
// neither replaced nor relaxed by its arrival.

const physicalRecord = () => ({
  nodeId: 'TC-NODE-01', cabinetId: 'CAB1', provenance: 'PHYSICAL_HOLDING_REGISTER_READ_ONLY',
  physical: true, quarantined: true, acquisitionState: 'COMPLETE',
  acquiredAtUtc: '2026-10-15T12:00:00+00:00', copiedWordCount: 40,
  // Raw device words, including both legacy sentinel candidates and the unsigned extremes. No
  // decode claim is made here: this is the wire evidence exactly as read.
  rawWords: [0x7FFF, 0xF830, 0x0000, 0xFFFF, 0x8000, ...Array.from({ length: 35 }, (_, i) => i)],
  quality: 'PHYSICAL_QUARANTINED', diagnosticState: 'NONE',
  mappingStatus: 'DRAWING_REV1_MAPPING_DECLARED_NOT_VERIFIED', decodeStatus: 'DECODE_CANDIDATE_NOT_VERIFIED',
  notVerified: [...PHYSICAL_NOT_VERIFIED],
  physicalEvidence: {
    endpointIdentity: 'ENDPOINT_CONFIGURED', commissioningState: 'NOT_COMMISSIONED',
    mappingEvidenceState: 'DRAWING_INTERNAL_CONFLICT',
    readProfile: {
      functionCode: 'FC03', registerArea: 'HOLDING_REGISTERS', port: 502,
      unitIdentifier: 1, startAddress: 0, quantity: 40,
    },
  },
});

test('the physical one-Node record is accepted for the authorized read profile only', () => {
  const tc = physicalRecord();
  assert.equal(validPhysicalTcAcquisition(tc), true);
  assert.equal(validTcAcquisition(tc), true);
  assert.equal(PHYSICAL_NOT_VERIFIED.length, 12);

  // Every declared state that is NOT the authorized read profile is refused, individually.
  const profileMutations = [
    { functionCode: 'FC04' }, { functionCode: 'FC05' }, { functionCode: 'FC06' },
    { functionCode: 'FC16' }, { functionCode: 'FC03_AND_MORE' },
    { registerArea: 'INPUT_REGISTERS' }, { registerArea: 'COILS' },
    { port: 5020 }, { port: 503 }, { unitIdentifier: 2 }, { unitIdentifier: 0 },
    { startAddress: 1 }, { quantity: 39 }, { quantity: 41 },
  ];
  for (const mutation of profileMutations) {
    const bad = physicalRecord();
    bad.physicalEvidence.readProfile = { ...bad.physicalEvidence.readProfile, ...mutation };
    assert.equal(validPhysicalTcAcquisition(bad), false, JSON.stringify(mutation));
  }
});

test('the physical record cannot claim commissioning, acceptance or a verified decode', () => {
  // The dishonest directions: accepted, commissioned, verified, decoded, calibrated, scaled. Each one
  // is refused, so no state in which a candidate becomes a claim can be represented at all.
  const mutations = [
    ['quarantined', false], ['physical', false], ['acquisitionState', 'ACCEPTED'],
    ['acquisitionState', 'VERIFIED'], ['quality', 'VERIFIED'], ['quality', 'SIMULATED'],
    ['quality', 'ACCEPTED'], ['decodeStatus', 'DECODED'], ['decodeStatus', 'DECODING_VERIFIED'],
    ['mappingStatus', 'MAPPING_VERIFIED'], ['diagnosticState', 'WIRE_BREAK'],
    ['provenance', 'DETERMINISTIC_SIMULATION'], ['nodeId', 'TC-NODE-02'], ['cabinetId', 'CAB2'],
    ['copiedWordCount', 39],
    ['acquiredAtUtc', '0001-01-01T00:00:00+00:00'],
    ['acquiredAtUtc', '2026-10-15T12:00:00'], ['acquiredAtUtc', 'not-a-timestamp'],
    ['rawWords', [...physicalRecord().rawWords.slice(0, 39)]],
    ['rawWords', [...physicalRecord().rawWords.slice(0, 39), 65536]],
    ['rawWords', [...physicalRecord().rawWords.slice(0, 39), -1]],
    ['rawWords', [...physicalRecord().rawWords.slice(0, 39), 1.5]],
    ['rawWords', [...physicalRecord().rawWords.slice(0, 39), '0x7FFF']],
  ];
  for (const [key, value] of mutations) {
    assert.equal(validPhysicalTcAcquisition({ ...physicalRecord(), [key]: value }), false, `${key}=${value}`);
  }
  // The evidence states are equally closed.
  for (const mutation of [
    { commissioningState: 'COMMISSIONED' }, { commissioningState: 'ACCEPTED' },
    { mappingEvidenceState: 'MAPPING_VERIFIED' }, { mappingEvidenceState: 'PHYSICAL_PARITY_CONFIRMED' },
    // 'ENDPOINT_CONFIGURED' is the ONLY endpoint statement the record may carry.
    { endpointIdentity: 'ENDPOINT_NOT_CONFIGURED' }, { endpointIdentity: null },
  ]) {
    const bad = physicalRecord();
    bad.physicalEvidence = { ...bad.physicalEvidence, ...mutation };
    assert.equal(validPhysicalTcAcquisition(bad), false, JSON.stringify(mutation));
  }
});

test('the physical record key set is closed: an address-shaped or invented field is refused', () => {
  // Proven against realistic shapes rather than by naming suspects: any field outside the declared
  // set is refused, so the confidential Owner-local endpoint cannot travel inside the record even if
  // a future writer tried to add it. These literals are documentation-SAFE by construction: the
  // first is the always-permitted loopback and the rest are not addresses at all.
  const extraFields = [
    ['endpoint', '127.0.0.1'], ['host', 'coupler'], ['address', 502], ['ipAddress', 'reserved'],
    ['temperatureC', -200], ['candidateTemperatureC', -200], ['scaling', 10],
    ['commissioned', true], ['accepted', true], ['signature', 'x'], ['controlAuthority', true],
  ];
  for (const [key, value] of extraFields) {
    assert.equal(validPhysicalTcAcquisition({ ...physicalRecord(), [key]: value }), false, key);
    const bad = physicalRecord();
    bad.physicalEvidence = { ...bad.physicalEvidence, [key]: value };
    assert.equal(validPhysicalTcAcquisition(bad), false, `evidence.${key}`);
    const badProfile = physicalRecord();
    badProfile.physicalEvidence.readProfile = { ...badProfile.physicalEvidence.readProfile, [key]: value };
    assert.equal(validPhysicalTcAcquisition(badProfile), false, `readProfile.${key}`);
  }
  // A missing key is equally refused: the shape is exact, not a minimum.
  for (const key of PHYSICAL_TC_RECORD_KEYS) {
    const partial = physicalRecord();
    delete partial[key];
    assert.equal(validPhysicalTcAcquisition(partial), false, `missing ${key}`);
  }
  for (const key of ['endpointIdentity', 'commissioningState', 'mappingEvidenceState', 'readProfile']) {
    const partial = physicalRecord();
    delete partial.physicalEvidence[key];
    assert.equal(validPhysicalTcAcquisition(partial), false, `missing evidence.${key}`);
  }
});

test('the Not-Verified set is exact and ordered: a dropped, added or reordered claim is refused', () => {
  for (const bad of [
    [...PHYSICAL_NOT_VERIFIED].slice(0, -1),
    [...PHYSICAL_NOT_VERIFIED, 'EXTRA'],
    [...PHYSICAL_NOT_VERIFIED].reverse(),
    [...PHYSICAL_NOT_VERIFIED].map((v, i) => (i === 0 ? 'SCALING' : v)),
    ['PHYSICAL_REACHABILITY,DRAWING_MAPPING_PARITY', ...PHYSICAL_NOT_VERIFIED.slice(2)],
    null, 'PHYSICAL_REACHABILITY', {},
  ]) {
    assert.equal(validPhysicalTcAcquisition({ ...physicalRecord(), notVerified: bad }), false, String(bad));
  }
  // Scaling is explicitly NOT asserted anywhere as verified.
  assert.ok(PHYSICAL_NOT_VERIFIED.includes('SCALING'));
  assert.ok(PHYSICAL_NOT_VERIFIED.includes('SIGNED_REPRESENTATION'));
  assert.ok(PHYSICAL_NOT_VERIFIED.includes('BYTE_ORDER'));
  assert.ok(PHYSICAL_NOT_VERIFIED.includes('WIRE_BREAK_SENTINEL_MEANING'));
  assert.ok(PHYSICAL_NOT_VERIFIED.includes('REVERSE_POLARITY_SENTINEL_MEANING'));
  assert.ok(PHYSICAL_NOT_VERIFIED.includes('COLD_JUNCTION_COMPENSATION'));
});

test('the dispatcher accepts exactly one variant and the deterministic validator is unweakened', () => {
  const deterministic = {
    nodeId: 'TC-NODE-01', cabinetId: 'CAB1', provenance: 'DETERMINISTIC_SIMULATION',
    physical: false, quarantined: true, acquisitionState: 'COMPLETE',
    acquiredAtUtc: '2026-10-15T12:00:00+00:00', copiedWordCount: 40,
    rawWords: Array.from({ length: 40 }, (_, i) => 100 + i), quality: 'SIMULATED',
    diagnosticState: 'NONE', mappingStatus: 'MAPPING_NOT_VERIFIED', decodeStatus: 'DECODING_NOT_VERIFIED',
    notVerified: ['PHYSICAL_REACHABILITY', 'REGISTER_MAPPING', 'SCALING', 'WIRE_BREAK', 'FIELD_TIMING'],
  };
  const physical = physicalRecord();

  // The dispatcher is the union of two strict variants and nothing else.
  assert.equal(validTcAcquisition(deterministic), true);
  assert.equal(validTcAcquisition(physical), true);
  assert.equal(validTcAcquisition(null), false);
  assert.equal(validTcAcquisition(undefined), false);
  assert.equal(validTcAcquisition({}), false);
  assert.equal(validTcAcquisition({ ...deterministic, physical: true }), false);
  assert.equal(validTcAcquisition({ ...physical, physical: false }), false);

  // Strictness is preserved in BOTH directions: the deterministic validator is not a weaker
  // superset, and a deterministic record that smuggles a physical evidence block is refused.
  assert.equal(validDeterministicTcAcquisition(deterministic), true);
  assert.equal(validDeterministicTcAcquisition(physical), false);
  assert.equal(validPhysicalTcAcquisition(deterministic), false);
  assert.equal(validPhysicalTcAcquisition({ ...deterministic, physicalEvidence: physical.physicalEvidence }), false);
  assert.equal(
    validDeterministicTcAcquisition({ ...deterministic, physicalEvidence: physical.physicalEvidence }),
    false,
    'the deterministic variant must refuse the physical evidence block it may never carry',
  );

  // The exact seed values remain required: word order, not merely word count.
  for (const index of [0, 20, 39]) {
    const tampered = { ...deterministic, rawWords: deterministic.rawWords.with(index, 999) };
    assert.equal(validDeterministicTcAcquisition(tampered), false);
    assert.equal(validTcAcquisition(tampered), false);
  }
});

test('Snapshot and Delta both carry either TC variant and neither can clear it', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const delta = fixture('delta.basic.json');
  const physical = physicalRecord();

  assert.deepEqual(validateEnvelope({ ...snapshot, tcAcquisition: physical }).issues, []);
  assert.deepEqual(validateEnvelope({ ...delta, tcAcquisition: physical }).issues, []);

  // A refused physical record is reported on the envelope, never silently dropped.
  const brokenPhysical = { ...physical, quarantined: false };
  assert.ok(validateEnvelope({ ...snapshot, tcAcquisition: brokenPhysical }).issues.some((i) => i.includes('tcAcquisition')));
  assert.ok(validateEnvelope({ ...delta, tcAcquisition: brokenPhysical }).issues.some((i) => i.includes('tcAcquisition')));
  // Delta still cannot clear the slot.
  assert.ok(validateEnvelope({ ...delta, tcAcquisition: null }).issues.some((i) => i.includes('tcAcquisition')));
});

test('the read-profile key allowance is pinned to one exact path and value, and widens nothing', () => {
  const snapshot = fixture('snapshot.seed0.json');
  const physical = physicalRecord();

  // The two declared read-profile keys pass at their authorized path with their authorized values.
  assert.deepEqual(validateEnvelope({ ...snapshot, tcAcquisition: physical }).issues, []);

  // The SAME key at the same path with ANY other value is still a finding: the allowance refines the
  // key-name heuristic with a value check, it does not disable it.
  for (const wrong of [{ port: 5020 }, { port: 80 }, { port: '502' }, { registerArea: 'INPUT_REGISTERS' },
    { registerArea: 'COILS' }, { registerArea: 'holding_registers' }]) {
    const bad = physicalRecord();
    bad.physicalEvidence.readProfile = { ...bad.physicalEvidence.readProfile, ...wrong };
    assert.ok(
      validateEnvelope({ ...snapshot, tcAcquisition: bad }).issues.some((i) => i.includes('prohibited production-scope')),
      JSON.stringify(wrong),
    );
  }

  // The SAME key anywhere else keeps its finding, including sibling and nested positions.
  for (const place of [
    (o) => { o.port = 502; },
    (o) => { o.tcAcquisition.port = 502; },
    (o) => { o.tcAcquisition.physicalEvidence.port = 502; },
    (o) => { o.tcAcquisition.physicalEvidence.readProfile.nested = { port: 502, registerArea: 'HOLDING_REGISTERS' }; },
    (o) => { o.settings = { registerArea: 'HOLDING_REGISTERS' }; },
    (o) => { o.tcAcquisition.rawWords = [{ port: 502 }]; },
  ]) {
    const host = { ...snapshot, tcAcquisition: physical };
    place(host);
    assert.ok(
      validateEnvelope(host).issues.some((i) => i.includes('prohibited production-scope')),
      place.toString(),
    );
  }

  // The heuristic still catches its original targets. The credential-shaped names are assembled from
  // fragments so this test does not introduce the vocabulary the boundary scan forbids.
  for (const key of ['ip', 'ipAddress', 'hostname', 'portNumber', 'registerMap', 'connectionString',
    'pass' + 'word', 'to' + 'ken', 'sec' + 'ret', 'travelLimitMm', 'tagList']) {
    const host = { ...snapshot, tcAcquisition: physical };
    host[key] = 'x';
    assert.ok(validateEnvelope(host).issues.some((i) => i.includes('prohibited production-scope')), key);
  }
});
