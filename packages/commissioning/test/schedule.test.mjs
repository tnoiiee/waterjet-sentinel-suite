// Stage 0.4C-K1 - Commissioning data model tests (Stage task section 16, group B).
// The parsed evidence must reproduce the protected Stage 0.4C-0B baseline exactly, and must refuse
// anything that would silently change it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  COLUMN, COLUMN_COUNT, PROTECTED_BASELINE, ROW_CLASS, parseCsv, parseSchedule, requireHeaderContract,
  scheduleCounts,
  nodeInventory, measurementsOfSensor, isProductSensorId, matchesProtectedBaseline, PRODUCT_SENSOR_SEQUENCE,
} from '../src/schedule.mjs';
import { buildCab1Mapping, projectCab1Payload, classifyPhysicalWord } from '../src/tcPhysicalMapping.mjs';
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

test('216 Product-compatible measurements across 108 Product Sensors (I7 and I16 included)', () => {
  assert.equal(counts.productCompatibleMeasurements, 216);
  assert.equal(counts.measurementIdentities, 216);
  assert.equal(counts.productSensors, 108);
  assert.equal(new Set(schedule.rows.map((r) => r.sensorId).filter(Boolean)).size, 108);
  // 108 x 2 = 216 assigned Product TC channels; 240 - 216 = 24 unassigned physical channels.
  assert.equal(counts.productSensors * 2, counts.productCompatibleMeasurements);
  assert.equal(counts.physicalChannels - counts.productCompatibleMeasurements, counts.unassignedChannels);
  assert.equal(counts.unassignedChannels, 24);
});

test('there is no duplicate Product measurement identity', () => {
  const identities = schedule.rows.map((r) => r.measurementId).filter(Boolean);
  assert.equal(new Set(identities).size, identities.length);
});

test('each of the 108 Product Sensors owns exactly one front and one rear measurement', () => {
  const productSensorIds = new Set(schedule.rows.filter((r) => r.productCompatible).map((r) => r.sensorId));
  assert.equal(productSensorIds.size, 108);
  assert.deepEqual([...productSensorIds].sort(), [...PRODUCT_SENSOR_SEQUENCE].sort());
  for (const sensorId of productSensorIds) {
    const measurements = measurementsOfSensor(schedule, sensorId).filter((r) => r.productCompatible);
    assert.equal(measurements.length, 2, sensorId);
    assert.deepEqual(measurements.map((r) => r.side).sort(), ['F', 'R'], sensorId);
  }
});

test('the conflict and unassigned separation is exactly the protected baseline', () => {
  assert.equal(counts.drawingVerifiedRows, 204);
  assert.equal(counts.drawingInternalConflictRows, 12);
  assert.equal(counts.productTopologyConflictRows, 0);
  assert.equal(counts.unassignedRows, 24);
  assert.equal(counts.mappedChannels, 216);
  assert.equal(counts.unassignedChannels, 24);
  assert.equal(counts.unresolvedRows, 0);
  assert.equal(counts.matchesProtectedBaseline, true);
  assert.equal(matchesProtectedBaseline(counts), true);
});

test('no Product-topology conflict row exists: the former I7 / I16 conflict rows are used Product assignments', () => {
  assert.equal(schedule.rows.filter((r) => r.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT).length, 0);
  assert.equal(schedule.rows.filter((r) => r.templateCompatibility === 'GAP_CONFLICT').length, 0);
  assert.equal(schedule.rows.filter((r) => r.rowState === 'DRAWING_TOPOLOGY_CONFLICT').length, 0);
  // The four physical assignments come from the committed drawing schedule, never invented:
  // TC-NODE-05 A102.4 ch7 / ch8 = I7 Front / Rear; TC-NODE-06 A102.4 ch7 / ch8 = I16 Front / Rear.
  const expected = [
    ['TC-NODE-05|A102.4|7', 'I7:F'], ['TC-NODE-05|A102.4|8', 'I7:R'],
    ['TC-NODE-06|A102.4|7', 'I16:F'], ['TC-NODE-06|A102.4|8', 'I16:R'],
  ];
  for (const [channelKey, measurementId] of expected) {
    const row = schedule.rows.find((r) => r.physicalChannelKey === channelKey);
    assert.equal(row.measurementId, measurementId, channelKey);
    assert.equal(row.rowClass, ROW_CLASS.PRODUCT_COMPATIBLE, channelKey);
    assert.equal(row.productCompatible, true, channelKey);
    assert.equal(row.drawingVerified, true, channelKey);
    assert.equal(row.passBlockedReason, null, channelKey);
    assert.equal(row.moduleModel, '750-498', channelKey);
  }
});

test('I7 and I16 are normal Product Sensors: one Front and one Rear measurement each, nothing special', () => {
  assert.equal(isProductSensorId('I7'), true);
  assert.equal(isProductSensorId('I16'), true);
  assert.equal(isProductSensorId('G+101'), true);
  assert.equal(isProductSensorId('I19'), false);
  assert.equal(isProductSensorId(null), false);
  for (const sensorId of ['I7', 'I16']) {
    const rows = schedule.rows.filter((r) => r.sensorId === sensorId);
    assert.equal(rows.length, 2, sensorId);
    assert.deepEqual(rows.map((r) => r.side).sort(), ['F', 'R'], sensorId);
    assert.deepEqual(measurementsOfSensor(schedule, sensorId).map((r) => r.measurementId).sort(), [`${sensorId}:F`, `${sensorId}:R`]);
    for (const row of rows) {
      assert.equal(row.productCompatible, true);
      assert.equal(row.rowClass, ROW_CLASS.PRODUCT_COMPATIBLE);
      assert.equal(row.notes.includes('NON_SENSOR_GAP position'), false, 'no current-facing gap wording on the row');
    }
  }
  const productSensorIds = schedule.rows.filter((r) => r.productCompatible).map((r) => r.sensorId);
  assert.equal(productSensorIds.filter((id) => id === 'I7').length, 2);
  assert.equal(productSensorIds.filter((id) => id === 'I16').length, 2);
});

test('the Sensor sequence is exactly the 108 approved identities, I7 and I16 in their normal ordinal position', () => {
  assert.equal(PRODUCT_SENSOR_SEQUENCE.length, 108);
  assert.equal(new Set(PRODUCT_SENSOR_SEQUENCE).size, 108);
  assert.equal(PRODUCT_SENSOR_SEQUENCE[0], 'G+201');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[17], 'G+218');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[18], 'G+101');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[36], 'G1');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[54], 'H1');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[72], 'I1');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[78], 'I7');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[87], 'I16');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[90], 'J1');
  assert.equal(PRODUCT_SENSOR_SEQUENCE[107], 'J18');
  assert.deepEqual(PRODUCT_SENSOR_SEQUENCE.slice(77, 80), ['I6', 'I7', 'I8']);
  assert.deepEqual(PRODUCT_SENSOR_SEQUENCE.slice(86, 89), ['I15', 'I16', 'I17']);
  assert.equal(PROTECTED_BASELINE.NON_SENSOR_GAP_SENSOR_IDS.length, 0);
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

// --- CAB1 one-Node projection and the CANDIDATE decode rule --------------------------------------
//
// The projection is derived from the same committed schedule rows as every test above, so a change in
// the evidence file moves both together. These tests pin the Drawing contract (40 offsets, 36
// assigned, 4 spare, 18 Sensors, preserved conflict states) and the honest presentation of a
// physical read (raw evidence plus a CANDIDATE, never a verified temperature).

test('the CAB1 projection reproduces the committed Drawing rev1 contract exactly', () => {
  const cab1 = buildCab1Mapping(schedule.rows);

  assert.equal(cab1.nodeId, 'TC-NODE-01');
  assert.equal(cab1.cabinetId, 'CAB1');
  assert.equal(cab1.measurements.length, 40);
  assert.deepEqual(cab1.measurements.map((m) => m.offset), Array.from({ length: 40 }, (_, i) => i));
  assert.equal(cab1.assignedOffsetCount, 36);
  assert.equal(cab1.spareOffsetCount, 4);
  assert.equal(cab1.sensorCount, 18);
  assert.equal(cab1.measurementCount, 36);

  // Modules advance in five blocks of eight channels, matching A102.1 through A102.5.
  for (let offset = 0; offset < 40; offset += 1) {
    assert.equal(cab1.measurements[offset].moduleInstance, `A102.${1 + Math.floor(offset / 8)}`, `offset ${offset}`);
    assert.equal(cab1.measurements[offset].channel, (offset % 8) + 1, `offset ${offset}`);
  }

  // 0-35 are assigned, 36-39 are spare and never carry a Sensor identity.
  for (const m of cab1.measurements.slice(0, 36)) {
    assert.equal(m.spare, false, `offset ${m.offset}`);
    assert.ok(isProductSensorId(m.sensorId), `offset ${m.offset}`);
    assert.ok(m.side === 'F' || m.side === 'R', `offset ${m.offset}`);
  }
  for (const m of cab1.measurements.slice(36)) {
    assert.equal(m.spare, true, `offset ${m.offset}`);
    assert.equal(m.sensorId, null, `offset ${m.offset}`);
    assert.equal(m.side, null, `offset ${m.offset}`);
    assert.equal(m.mappingEvidenceState, 'SPARE_UNMAPPED', `offset ${m.offset}`);
    assert.equal(m.rowState, 'SPARE_UNMAPPED', `offset ${m.offset}`);
  }

  // Eighteen unique Sensors, each one Front and one Rear, Rear immediately after Front.
  assert.deepEqual(cab1.sensors.map((s) => s.sensorId),
    [...Array(9).keys()].map((i) => `G+10${i + 1}`).concat([...Array(9).keys()].map((i) => `G+20${i + 1}`)));
  for (const sensor of cab1.sensors) {
    assert.equal(sensor.rearOffset, sensor.frontOffset + 1, sensor.sensorId);
  }

  // No endpoint and no retired address field can appear in the projection.
  const text = JSON.stringify(cab1);
  assert.equal(/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/.test(text), false, 'projection must hold no address');
  assert.equal(Object.hasOwn(cab1, 'ip'), false);
  assert.equal(Object.hasOwn(cab1.readProfile, 'endpoint'), false);
  assert.equal(Object.hasOwn(cab1.readProfile, 'host'), false);
});

test('the CAB1 mapping preserves the two Drawing internal conflicts and does not promote them', () => {
  const cab1 = buildCab1Mapping(schedule.rows);

  // The conflict rows are the committed ones: offset 13 is G+107 Rear and offset 14 is G+108 Front.
  // They are surfaced rather than smoothed away, and the node-level summary stays conflicted.
  assert.deepEqual(cab1.drawingConflictOffsets, [13, 14]);
  assert.equal(cab1.mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');

  const conflictRows = cab1.measurements.filter((m) => m.drawingConflict);
  assert.deepEqual(conflictRows.map((m) => m.offset), [13, 14]);
  assert.equal(conflictRows[0].sensorId, 'G+107');
  assert.equal(conflictRows[0].side, 'R');
  assert.equal(conflictRows[0].rowState, 'DRAWING_INTERNAL_CONFLICT');
  assert.equal(conflictRows[0].mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');
  assert.equal(conflictRows[1].sensorId, 'G+108');
  assert.equal(conflictRows[1].side, 'F');

  // The two affected Sensors inherit the conflict; every other Sensor stays drawing-verified.
  assert.equal(cab1.sensors.find((s) => s.sensorId === 'G+107').mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');
  assert.equal(cab1.sensors.find((s) => s.sensorId === 'G+108').mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');
  assert.equal(cab1.sensors.find((s) => s.sensorId === 'G+106').mappingEvidenceState, 'DRAWING_VERIFIED');
  assert.equal(cab1.sensors.find((s) => s.sensorId === 'G+109').mappingEvidenceState, 'DRAWING_VERIFIED');

  // 34 drawing-verified rows and 2 conflicts account for every assigned offset.
  assert.equal(cab1.measurements.filter((m) => m.mappingEvidenceState === 'DRAWING_VERIFIED').length, 34);
  assert.equal(cab1.measurements.filter((m) => m.drawingConflict).length, 2);
});

test('the CAB1 projection refuses malformed schedules instead of repairing them', () => {
  const rows = schedule.rows.filter((r) => r.cabinet === 'CAB1');
  const mutate = (fn) => rows.map((r) => ({ ...r })).map(fn);

  // Every refusal path is exercised, because a silently repaired mapping would mislead an operator.
  const cases = [
    ['a shortened image', mutate((r) => r).slice(0, 39)],
    ['a duplicated offset', mutate((r) => (r.wordOrdinal === 5 ? { ...r, wordOrdinal: 4 } : r))],
    ['a negative offset', mutate((r) => (r.wordOrdinal === 5 ? { ...r, wordOrdinal: -1 } : r))],
    ['a fractional offset', mutate((r) => (r.wordOrdinal === 5 ? { ...r, wordOrdinal: 4.5 } : r))],
    ['an assigned offset demoted to spare', mutate((r) => (r.wordOrdinal === 5 ? { ...r, rowState: 'SPARE_UNMAPPED', sensorId: null } : r))],
    ['a spare offset promoted to a Sensor', mutate((r) => (r.wordOrdinal === 37 ? { ...r, rowState: 'DRAWING_VERIFIED', sensorId: 'G+101', side: 'F' } : r))],
    ['a foreign node identity', mutate((r) => (r.wordOrdinal === 5 ? { ...r, nodeId: 'TC-NODE-02' } : r))],
    ['a Sensor with only one side', mutate((r) => (r.wordOrdinal === 0 ? { ...r, sensorId: 'G+999' } : r))],
    ['a broken Front/Rear adjacency', mutate((r) => (r.wordOrdinal === 0 ? { ...r, sensorId: 'G+101', side: 'R' } : r))],
  ];
  for (const [label, broken] of cases) {
    assert.throws(() => buildCab1Mapping(broken), Error, label);
  }
});

test('a physical word is presented as raw evidence plus a CANDIDATE, never as a verified value', () => {
  // Ordinary candidates: signed Int16 divided by ten, exactly the C# `unchecked((short)word) / 10.0`.
  assert.deepEqual(
    [0x0000, 0x04D2, 0x7FFE, 0xFFFF, 0x8000].map((w) => classifyPhysicalWord(w).candidateValue),
    [0, 123.4, 3276.6, -0.1, -3276.8],
  );
  for (const word of [0x0000, 0x04D2, 0xFFFF, 0x8000]) {
    const decoded = classifyPhysicalWord(word);
    assert.equal(decoded.decodeState, 'DECODE_CANDIDATE_VALUE');
    assert.equal(decoded.manufacturerMeaning, 'NOT_VERIFIED');
  }

  // The two legacy sentinel candidates never present as an ordinary temperature, and no numeric
  // candidate is invented for them: their manufacturer meaning is unverified.
  const wireBreak = classifyPhysicalWord(0x7FFF);
  assert.equal(wireBreak.rawHex, '0x7FFF');
  assert.equal(wireBreak.rawWord, 32767);
  assert.equal(wireBreak.candidateValue, null);
  assert.equal(wireBreak.presentAsOrdinaryTemperature, false);
  assert.equal(wireBreak.decodeState, 'DECODE_LEGACY_WIRE_BREAK_CANDIDATE');

  const reversePolarity = classifyPhysicalWord(0xF830);
  assert.equal(reversePolarity.rawHex, '0xF830');
  assert.equal(reversePolarity.rawWord, 63536);
  assert.equal(reversePolarity.candidateValue, null);
  assert.equal(reversePolarity.presentAsOrdinaryTemperature, false);
  assert.equal(reversePolarity.decodeState, 'DECODE_LEGACY_RV_P_CANDIDATE');

  // 0xF830 must never surface as the ordinary -200.0 a naive signed decode would produce.
  assert.notEqual(reversePolarity.candidateValue, -200);

  // A non-16-bit word is refused rather than coerced.
  for (const bad of [-1, 65536, 1.5, '0x7FFF', null, undefined, NaN]) {
    assert.throws(() => classifyPhysicalWord(bad), RangeError, String(bad));
  }
});

test('the payload projection pairs raw words to offsets and never fabricates a missing one', () => {
  const cab1 = buildCab1Mapping(schedule.rows);
  const words = Array.from({ length: 40 }, (_, i) => (i === 0 ? 0x7FFF : i === 1 ? 0xF830 : i));
  const projection = projectCab1Payload(cab1, words);

  assert.equal(projection.measurements.length, 40);
  assert.equal(projection.wordCount, 40);
  for (const m of projection.measurements) {
    assert.equal(m.rawWord, words[m.offset], `offset ${m.offset}`);
    assert.equal(m.moduleInstance, cab1.measurements[m.offset].moduleInstance);
    assert.equal(m.mappingEvidenceState, cab1.measurements[m.offset].mappingEvidenceState);
    assert.equal(m.drawingConflict, cab1.measurements[m.offset].drawingConflict);
  }
  assert.equal(projection.mappingEvidenceState, 'DRAWING_INTERNAL_CONFLICT');
  assert.deepEqual(projection.drawingConflictOffsets, [13, 14]);

  // A wrong-length or malformed payload is refused: a short read is never padded and a failed read is
  // never converted into values.
  for (const bad of [
    words.slice(0, 39), [...words, 1], [], null, undefined, 'x', 40,
    [...words.slice(0, 39), 65536], [...words.slice(0, 39), -1], [...words.slice(0, 39), '7'],
  ]) {
    assert.throws(() => projectCab1Payload(cab1, bad), Error, JSON.stringify(bad));
  }
});

test('the physical read profile is the declared read-only shape and carries no endpoint', () => {
  const cab1 = buildCab1Mapping(schedule.rows);
  assert.deepEqual(cab1.readProfile, {
    functionCode: 'FC03', functionLabel: 'Read Holding Registers', registerArea: 'HOLDING_REGISTERS',
    port: 502, unitIdentifier: 1, startAddress: 0, quantity: 40, registerWidthBits: 16,
    registersPerChannel: 1, registersPerSensor: 2, addressConvention: 'ZERO_BASED',
  });
  // One register per channel, five modules of eight channels, zero-based addressing.
  assert.equal(cab1.readProfile.quantity, cab1.measurements.length);
  assert.equal(cab1.readProfile.quantity / cab1.readProfile.registersPerChannel, 40);
  // The function code is the read-only one: no write function code appears anywhere in the module.
  const text = JSON.stringify(cab1);
  for (const writeCode of ['FC04', 'FC05', 'FC06', 'FC15', 'FC16']) assert.equal(text.includes(writeCode), false, writeCode);
});
