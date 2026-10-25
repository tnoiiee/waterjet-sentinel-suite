#!/usr/bin/env node
// Deterministic generator for config/examples/sensor-parameters.migrated.example.json.
//
// The example shows the canonical shape of the migrated legacy sensor parameters: the shape the
// .NET importer (packages/domain/SensorParameterCsvImporter.cs) emits for the synthetic CSV
// formulas documented in tests/config.tests/SyntheticSensorParameterCsv.cs, with every deferred
// raw value and the acquisition-binding provenance null (public-repository boundary). Formulas
// (scanOrder s): diffLowerBound 15+(s%10), diffUpperBound 65+(s%10), dirtyScoreThreshold
// 55+(s%20), enabled = s%17 != 0, cleaningCount = s%4; legacyRecordId SYN-REC-<orderTotal:000>.
//
// Owner critical topology correction (Stage 0.4C-K3): all 108 logical positions are Product
// Sensors (I7 and I16 included), 108 Sensor configurations with dense scanOrder 1–108, no
// gapAnchorForWaterJetId key. Owner-approved canonical Equipment topology adoption
// (2026-10-25): the equipment rows
// and every Sensor assignment come from the canonical table in
// tools/fixture-generator/generate.mjs (WJ1-WJ4 clean the UPPER tier, WJ5-WJ8 the LOWER;
// fine-grained placement UNRESOLVED with no anchors for all eight Water Jets), while
// `--legacy-proof` still rebuilds the superseded 106-Sensor form over the frozen
// historical table and must be byte-identical to the file committed at PR #21 commit
// 7dc7c691 (port proof). The current form is never hand-edited.
//
// Usage:
//   node tools/migrated-example/generate.mjs            # print the corrected example
//   node tools/migrated-example/generate.mjs --write    # overwrite the committed example
//   node tools/migrated-example/generate.mjs --legacy-proof

import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { sensorIdFor, wallForColumn, regionForLogicalRow, waterJets, ISOLATION_VALVES, CORRECTED as FIXTURE_CORRECTED, LEGACY_SUPERSEDED as FIXTURE_LEGACY } from '../fixture-generator/generate.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const TARGET = path.resolve(HERE, '../../config/examples/sensor-parameters.migrated.example.json');

const LABEL = 'PUBLIC-SAFE EXAMPLE - SYNTHETIC - INCOMPLETE BY DESIGN - NOT FOR DEPLOYMENT';
const WALL_FIRST_COLUMN = Object.freeze({ LEFT: 1, REAR: 5, RIGHT: 10, FRONT: 14 });

export const CORRECTED = Object.freeze({
  stage: 'Stage 0.3A-3 Checkpoint B - canonical shape of the migrated legacy sensor parameters (ADR-0017), corrected by the Owner critical topology correction (Stage 0.4C-K3): I7 and I16 are normal Product Sensors; 108 Sensor configurations, scanOrder 1-108. All values synthetic; acquisition-binding provenance and deferred raw values are null; the Owner CSV itself never enters Git.',
  gapIds: Object.freeze([]),
  emitsGapAnchorKey: false,
  fixtureProfile: FIXTURE_CORRECTED,
});

export const LEGACY_SUPERSEDED = Object.freeze({
  stage: 'Stage 0.3A-3 Checkpoint B - canonical shape of the migrated legacy sensor parameters (ADR-0017). All values synthetic; acquisition-binding provenance and deferred raw values are null; the Owner CSV itself never enters Git.',
  gapIds: Object.freeze(['I7', 'I16']),
  gapAnchors: Object.freeze({ I7: 'WJ3', I16: 'WJ1' }),
  emitsGapAnchorKey: true,
  fixtureProfile: FIXTURE_LEGACY,
});

export function buildMigratedExample(profile) {
  const positions = [];
  const sensors = [];
  const wallOrdinal = {};
  const jets = waterJets(profile.fixtureProfile);
  let scanOrder = 0;
  for (let orderTotal = 0; orderTotal < 108; orderTotal += 1) {
    const logicalRow = Math.floor(orderTotal / 18) + 1;
    const logicalColumn = (orderTotal % 18) + 1;
    const logicalId = sensorIdFor(logicalRow, logicalColumn);
    const wall = wallForColumn(logicalColumn);
    wallOrdinal[wall] = (wallOrdinal[wall] ?? 0) + 1;
    const wallColumn = logicalColumn - WALL_FIRST_COLUMN[wall] + 1;
    const recordId = `SYN-REC-${String(orderTotal).padStart(3, '0')}`;
    const isGap = profile.gapIds.includes(logicalId);

    const position = {
      logicalId,
      positionKind: isGap ? 'NON_SENSOR_GAP' : 'SENSOR',
      wall,
      logicalColumn,
      logicalRow,
      wallColumn,
      wallRow: logicalRow,
      orderTotal,
      orderWall: wallOrdinal[wall],
      legacyRecordId: recordId,
    };
    if (profile.emitsGapAnchorKey) position.gapAnchorForWaterJetId = isGap ? profile.gapAnchors[logicalId] : null;
    positions.push(position);
    if (isGap) continue;

    scanOrder += 1;
    const region = regionForLogicalRow(logicalRow);
    const assigned = jets.filter((w) => w.targetWall === wall && w.targetRegion === region);
    if (assigned.length !== 1) throw new Error(`no single Water Jet targets ${wall}/${region}`);
    const lower = 15 + (scanOrder % 10);
    sensors.push({
      sensorId: logicalId,
      wall,
      logicalColumn,
      logicalRow,
      wallColumn,
      wallRow: logicalRow,
      orderWall: wallOrdinal[wall],
      scanOrder,
      diffLowerBound: lower,
      diffUpperBound: lower + 50,
      dirtyScoreThreshold: 55 + (scanOrder % 20),
      enabled: scanOrder % 17 !== 0,
      cleaningCount: scanOrder % 4,
      assignedWaterJetId: assigned[0].waterJetId,
      assignedIsolationValveId: assigned[0].dedicatedIsolationValveId,
      legacyRecordId: recordId,
      lastCleanTimestampRaw: null,
      minTimeAllowAddToQueueRaw: null,
      maxTimeAllowAddToQueueRaw: null,
      maxTimeEnableRaw: null,
      latestUpdateParamsTimestampRaw: null,
      deferredAcquisition: null,
    });
  }

  return {
    _label: LABEL,
    _stage: profile.stage,
    logicalPositions: positions,
    sensors,
    waterJets: jets,
    isolationValves: ISOLATION_VALVES.map((v) => ({ ...v })),
    warnings: [],
  };
}

/** The committed example is plain indented JSON ('+' unescaped) with a trailing newline. */
export function serializeExample(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}

const invokedDirectly = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (invokedDirectly) {
  const legacy = process.argv.includes('--legacy-proof');
  const text = serializeExample(buildMigratedExample(legacy ? LEGACY_SUPERSEDED : CORRECTED));
  if (process.argv.includes('--write')) {
    if (legacy) throw new Error('--legacy-proof is read-only; the superseded form is never written');
    writeFileSync(TARGET, text);
    process.stdout.write(`wrote ${TARGET}\n`);
  } else {
    process.stdout.write(text);
  }
}
