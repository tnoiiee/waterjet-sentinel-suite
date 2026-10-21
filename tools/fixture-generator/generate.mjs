#!/usr/bin/env node
// Node port of tests/integration/FixtureGenerator.cs::BuildSnapshot and ::BuildBasicDelta
// (+ SerializeWithStatus) for packages/contracts/fixtures/snapshot.seed0.json and
// packages/contracts/fixtures/delta.basic.json.
//
// WHY THIS EXISTS: the canonical generator is the .NET test project
// (`WJSS_UPDATE_FIXTURES=1 dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj`,
// fixtures README §5). No .NET SDK exists in Arena, so Owner topology corrections are
// applied to the golden fixtures through this deterministic port. The port is proven
// against previously committed .NET output: `--legacy-proof` rebuilds the superseded
// 106-Sensor snapshot form and must be byte-identical to the committed file at PR #21
// commit 7dc7c691, and `--legacy-proof --delta` rebuilds the pre-adoption delta and must
// be byte-identical to the delta committed before the Canonical Topology Adoption. The
// Owner-local C# parity test (FixtureParityTests) remains the final gate; it compares
// semantically (number- and key-order-tolerant), so number spellings and the trailing
// newline are not contract content — they are matched here only to keep generated diffs
// limited to genuine content changes.
//
// CANONICAL TOPOLOGY ADOPTION (2026-10-21) — current/historical separation: current
// output is generated ONLY from CANONICAL_WATER_JET_TABLE (canonical installed wall/tier
// and Target Coverage; fine-grained placement UNRESOLVED with no anchors for all eight
// Water Jets). The pre-adoption equipment table survives ONLY as
// HISTORICAL_FROZEN_WATER_JET_TABLE, explicitly historical and non-Product, referenced
// solely by the LEGACY_SUPERSEDED proof profile so the byte-identical historical proofs
// keep their exact meaning. Current Product generation never imports it. The current
// snapshot serialization also matches the last .NET-transferred committed bytes (the
// dispatch member precedes the pressure members per Jobs.cs, `tcAcquisition: null` is
// emitted per Snapshot.cs, whole doubles spell as integers, and the file has no trailing
// newline); the legacy serialization keeps the exact form of the 7dc7c691 file.
//
// Every value is a pure function of fixed formulas over ScanOrder (no clock, no random source,
// no Production value). Running it twice yields identical bytes.
//
// Usage:
//   node tools/fixture-generator/generate.mjs                       # print the current snapshot fixture
//   node tools/fixture-generator/generate.mjs --write               # overwrite snapshot.seed0.json
//   node tools/fixture-generator/generate.mjs --delta               # print the current delta.basic fixture
//   node tools/fixture-generator/generate.mjs --delta --write       # overwrite delta.basic.json
//   node tools/fixture-generator/generate.mjs --legacy-proof        # print the superseded snapshot form (never written)
//   node tools/fixture-generator/generate.mjs --legacy-proof --delta # print the pre-adoption delta form (never written)

import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const TARGET = path.resolve(HERE, '../../packages/contracts/fixtures/snapshot.seed0.json');
const DELTA_TARGET = path.resolve(HERE, '../../packages/contracts/fixtures/delta.basic.json');

export const TIMESTAMP = '2026-10-07T00:00:00.000Z';
export const SNAPSHOT_REVISION = 10;
export const FIXTURE_STATUS = 'PROVISIONAL STRUCTURAL FIXTURE - OWNER-LOCAL .NET GENERATION REQUIRED';
export const DIRTY_THRESHOLD = 50.5;

const LOGICAL_COLUMNS = 18;
const LOGICAL_ROWS = 6;
const WALL_COLUMNS = Object.freeze({ LEFT: [1, 4], REAR: [5, 9], RIGHT: [10, 13], FRONT: [14, 18] });

// ---- Equipment tables (installed wall / region, placement, target coverage, WJn <-> IVn) ----
//
// CURRENT Product authority: the canonical table adopted by the Owner Canonical Topology
// Adoption (2026-10-21), mirroring packages/domain/WaterJetTopologyCatalog.cs. WJ1-WJ4 are
// installed on the UPPER tier and WJ5-WJ8 on the LOWER tier; every Water Jet targets the
// opposite wall in the same tier; the fine-grained placement of all eight is UNRESOLVED
// with NO anchors (no anchor is inferred, mirrored or transferred).
const CANONICAL_WATER_JET_TABLE = Object.freeze([
  { waterJetId: 'WJ1', installedWall: 'FRONT', installedRegion: 'UPPER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'REAR', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV1' },
  { waterJetId: 'WJ2', installedWall: 'LEFT', installedRegion: 'UPPER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'RIGHT', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV2' },
  { waterJetId: 'WJ3', installedWall: 'REAR', installedRegion: 'UPPER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'FRONT', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV3' },
  { waterJetId: 'WJ4', installedWall: 'RIGHT', installedRegion: 'UPPER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'LEFT', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV4' },
  { waterJetId: 'WJ5', installedWall: 'FRONT', installedRegion: 'LOWER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'REAR', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV5' },
  { waterJetId: 'WJ6', installedWall: 'LEFT', installedRegion: 'LOWER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'RIGHT', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV6' },
  { waterJetId: 'WJ7', installedWall: 'REAR', installedRegion: 'LOWER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'FRONT', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV7' },
  { waterJetId: 'WJ8', installedWall: 'RIGHT', installedRegion: 'LOWER', placementKind: 'UNRESOLVED', placementAnchors: [], targetWall: 'LEFT', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV8' },
]);

// HISTORICAL ONLY — FROZEN, NON-PRODUCT. The pre-adoption equipment table (WJ1-WJ4 on the
// LOWER tier, WJ5-WJ8 on the UPPER tier, with the former fine-grained anchors), retained
// byte-for-byte so the `--legacy-proof` modes keep proving this port against previously
// committed .NET output. It is NOT Product authority, it is referenced ONLY by the
// LEGACY_SUPERSEDED proof profile below, and current Product generation must never use
// it. WJ1 / WJ3 carry null placement here because the proof profile supplies the
// superseded NON_SENSOR_GAP placement exactly as the historical file carried it.
const HISTORICAL_FROZEN_WATER_JET_TABLE = Object.freeze([
  { waterJetId: 'WJ1', installedWall: 'FRONT', installedRegion: 'LOWER', placementKind: null, placementAnchors: null, targetWall: 'REAR', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV1' },
  { waterJetId: 'WJ2', installedWall: 'LEFT', installedRegion: 'LOWER', placementKind: 'BETWEEN_HORIZONTAL', placementAnchors: ['J2', 'J3'], targetWall: 'RIGHT', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV2' },
  { waterJetId: 'WJ3', installedWall: 'REAR', installedRegion: 'LOWER', placementKind: null, placementAnchors: null, targetWall: 'FRONT', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV3' },
  { waterJetId: 'WJ4', installedWall: 'RIGHT', installedRegion: 'LOWER', placementKind: 'BETWEEN_HORIZONTAL', placementAnchors: ['J11', 'J12'], targetWall: 'LEFT', targetRegion: 'LOWER', dedicatedIsolationValveId: 'IV4' },
  { waterJetId: 'WJ5', installedWall: 'FRONT', installedRegion: 'UPPER', placementKind: 'BETWEEN_VERTICAL', placementAnchors: ['G+216', 'G+116'], targetWall: 'REAR', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV5' },
  { waterJetId: 'WJ6', installedWall: 'LEFT', installedRegion: 'UPPER', placementKind: 'JUNCTION', placementAnchors: ['G+102', 'G+202', 'G+203'], targetWall: 'RIGHT', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV6' },
  { waterJetId: 'WJ7', installedWall: 'REAR', installedRegion: 'UPPER', placementKind: 'BETWEEN_VERTICAL', placementAnchors: ['G+207', 'G+107'], targetWall: 'FRONT', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV7' },
  { waterJetId: 'WJ8', installedWall: 'RIGHT', installedRegion: 'UPPER', placementKind: 'JUNCTION', placementAnchors: ['G+111', 'G+211', 'G+212'], targetWall: 'LEFT', targetRegion: 'UPPER', dedicatedIsolationValveId: 'IV8' },
]);

/**
 * Current profile: every one of the 108 logical positions is a Product Sensor, equipment
 * from CANONICAL_WATER_JET_TABLE only, serialized in the current .NET-transferred form.
 */
export const CORRECTED = Object.freeze({
  sensorLocations: 108,
  nonSensorGapSlots: Object.freeze([]),
  deviceCounts: Object.freeze([14, 14, 14, 14, 13, 13, 13, 13]),
  emitsGapAnchorKey: false,
  waterJetTable: CANONICAL_WATER_JET_TABLE,
  // The canonical table carries UNRESOLVED / [] for every Water Jet directly; no override.
  waterJetPlacement: Object.freeze({}),
  serialization: 'current',
});

/**
 * HISTORICAL proof profile ONLY: the superseded 106-Sensor form over the frozen
 * historical equipment table, kept solely to prove the port byte-for-byte against
 * previously committed files. Never written, never Product authority.
 */
export const LEGACY_SUPERSEDED = Object.freeze({
  sensorLocations: 106,
  nonSensorGapSlots: Object.freeze([
    Object.freeze({ logicalId: 'I7', anchorWaterJetId: 'WJ3', row: 5, column: 7 }),
    Object.freeze({ logicalId: 'I16', anchorWaterJetId: 'WJ1', row: 5, column: 16 }),
  ]),
  deviceCounts: Object.freeze([14, 14, 13, 13, 13, 13, 13, 13]),
  emitsGapAnchorKey: true,
  waterJetTable: HISTORICAL_FROZEN_WATER_JET_TABLE,
  waterJetPlacement: Object.freeze({
    WJ1: Object.freeze({ placementKind: 'NON_SENSOR_GAP', placementAnchors: Object.freeze(['I15', 'I17']) }),
    WJ3: Object.freeze({ placementKind: 'NON_SENSOR_GAP', placementAnchors: Object.freeze(['I6', 'I8']) }),
  }),
  serialization: 'legacy106',
});

export function waterJets(profile) {
  return profile.waterJetTable.map((jet) => {
    const override = profile.waterJetPlacement[jet.waterJetId];
    return {
      waterJetId: jet.waterJetId,
      installedWall: jet.installedWall,
      installedRegion: jet.installedRegion,
      placementKind: override ? override.placementKind : jet.placementKind,
      placementAnchors: [...(override ? override.placementAnchors : jet.placementAnchors)],
      targetWall: jet.targetWall,
      targetRegion: jet.targetRegion,
      dedicatedIsolationValveId: jet.dedicatedIsolationValveId,
    };
  });
}

export const ISOLATION_VALVES = Object.freeze(
  [1, 2, 3, 4, 5, 6, 7, 8].map((n) => Object.freeze({ valveId: `IV${n}`, servedWaterJetId: `WJ${n}` })),
);

// ---- CanonicalSensorMap helpers (packages/contracts/WallMap.cs) ----
export function wallForColumn(column) {
  for (const [wall, [first, last]] of Object.entries(WALL_COLUMNS)) {
    if (column >= first && column <= last) return wall;
  }
  throw new RangeError(`logical column ${column} is outside the 18-column matrix`);
}

export function sensorIdFor(row, column) {
  switch (row) {
    case 1: return `G+${200 + column}`;
    case 2: return `G+${100 + column}`;
    case 3: return `G${column}`;
    case 4: return `H${column}`;
    case 5: return `I${column}`;
    case 6: return `J${column}`;
    default: throw new RangeError('logical row must be 1-6');
  }
}

export function regionForLogicalRow(row) {
  if (row === 1 || row === 2) return 'UPPER';
  if (row >= 3 && row <= 6) return 'LOWER';
  throw new RangeError('logical row must be 1-6');
}

// ---- FixtureGenerator formulas ----
export const scoreFor = (scanOrder) => ((scanOrder * 37) % 101) + 0.5;

function deviceFor(counts, scanOrder) {
  let remaining = scanOrder;
  for (let d = 0; d < counts.length; d += 1) {
    if (remaining <= counts[d]) return { deviceId: `SYN-TC-${String(d + 1).padStart(2, '0')}`, index: remaining - 1 };
    remaining -= counts[d];
  }
  throw new RangeError('scan order outside device distribution');
}

function isGap(profile, row, col) {
  return profile.nonSensorGapSlots.find((g) => g.row === row && g.column === col) ?? null;
}

export function sensorPositions(profile) {
  const list = [];
  for (let row = 1; row <= LOGICAL_ROWS; row += 1) {
    for (let col = 1; col <= LOGICAL_COLUMNS; col += 1) {
      if (isGap(profile, row, col)) continue;
      list.push([row, col]);
    }
  }
  return list;
}

export function buildWallMap(profile) {
  const slots = [];
  for (let row = 1; row <= LOGICAL_ROWS; row += 1) {
    for (let col = 1; col <= LOGICAL_COLUMNS; col += 1) {
      const wall = wallForColumn(col);
      const [first] = WALL_COLUMNS[wall];
      const gap = isGap(profile, row, col);
      const slot = {
        slotId: `SLOT-R${row}-C${String(col).padStart(2, '0')}`,
        positionKind: gap ? 'NON_SENSOR_GAP' : 'SENSOR',
        wall,
        logicalColumn: col,
        logicalRow: row,
        wallColumn: col - first + 1,
        wallRow: row,
        sensorId: gap ? null : sensorIdFor(row, col),
      };
      if (profile.emitsGapAnchorKey) slot.gapAnchorForWaterJetId = gap ? gap.anchorWaterJetId : null;
      slots.push(slot);
    }
  }
  return slots;
}

export function buildSensor(profile, scanOrder, inQueue, isJobTarget) {
  const [row, col] = sensorPositions(profile)[scanOrder - 1];
  const wall = wallForColumn(col);
  const [first] = WALL_COLUMNS[wall];
  const score = scoreFor(scanOrder);
  const uncertain = scanOrder % 7 === 0;
  const { deviceId, index } = deviceFor(profile.deviceCounts, scanOrder);
  const region = regionForLogicalRow(row);
  const assignedList = waterJets(profile).filter((w) => w.targetWall === wall && w.targetRegion === region);
  if (assignedList.length !== 1) throw new Error(`no single Water Jet targets ${wall}/${region}`);
  const assigned = assignedList[0];
  return {
    sensorId: sensorIdFor(row, col),
    positionKind: 'SENSOR',
    wall,
    logicalColumn: col,
    logicalRow: row,
    wallColumn: col - first + 1,
    wallRow: row,
    scanOrder,
    deviceId,
    assignedWaterJetId: assigned.waterJetId,
    assignedIsolationValveId: assigned.dedicatedIsolationValveId,
    tcFrontChannel: `${deviceId}:CH${String(2 * index).padStart(2, '0')}`,
    tcRearChannel: `${deviceId}:CH${String(2 * index + 1).padStart(2, '0')}`,
    dirtyScore: score,
    lastValidatedScore: uncertain ? score : null,
    lastValidatedAt: uncertain ? TIMESTAMP : null,
    classification: score > DIRTY_THRESHOLD ? 'DIRTY' : 'CLEANER',
    classificationBasis: uncertain ? 'LAST_VALIDATED' : 'CURRENT',
    quality: uncertain ? 'UNCERTAIN' : 'GOOD',
    qualityReason: uncertain ? 'FIXTURE_FORCED' : null,
    sourceTimestamp: TIMESTAMP,
    queueState: isJobTarget ? 'ACTIVE' : inQueue ? 'QUEUED' : 'NONE',
    isActiveJobTarget: isJobTarget,
    alarmState: 'NONE',
    alarmSeverity: null,
  };
}

export const QUEUE_ENTRIES = Object.freeze([
  { position: 1, entryId: 'Q-201', sensorId: 'G+210', sourceReason: 'DIRTY_SCORE', dirtyScore: 60.5, secondsSinceLastClean: 3600 },
  { position: 2, entryId: 'Q-202', sensorId: 'H12', sourceReason: 'TIME_INTERVAL', dirtyScore: 52.5, secondsSinceLastClean: 7200 },
  { position: 3, entryId: 'Q-203', sensorId: 'J5', sourceReason: 'OPERATOR_REQUEST', dirtyScore: null, secondsSinceLastClean: 10800 },
]);

const DISPATCH = Object.freeze({
  dispatchId: 'D-100',
  queueRevisionBefore: 4,
  queueRevisionAfter: 5,
  queueEntryId: 'Q-200',
  positionBefore: 1,
  sensorId: 'H7',
  sourceReason: 'DIRTY_SCORE',
  jobId: 'J-300',
  origin: 'FIXTURE',
  dispatchedAt: TIMESTAMP,
});

// Numbers the historical committed file (7dc7c691) spells with a trailing ".0".
// JSON.stringify cannot emit that spelling, so in the legacy serialization they are
// carried as tagged strings and unwrapped at the end. The current serialization spells
// whole doubles as integers, exactly like the last .NET-transferred committed file.
const num = (text) => `__WJSS_NUM__${text}`;

function buildActiveJob(profile) {
  const current = profile.serialization === 'current';
  const pressureMembers = {
    pumpOutletPressureBar: current ? 16 : num('16.0'),
    pumpPressureQuality: 'GOOD',
    pumpPressureSourceId: 'PUMP_OUTLET',
    pumpPressureInputValid: true,
    pumpReadySetpointBar: current ? 15 : num('15.0'),
    valveOutletPressureBar: 0.5,
    valvePressureQuality: 'GOOD',
    valvePressureSourceId: 'SYN-VLV-05_OUTLET',
    valvePressureInputValid: true,
    valveOpenResolution: 'OPEN_CONFIRMED',
    valveDiagnosis: 'NONE',
  };
  const job = {
    jobId: 'J-300',
    targetSensorId: 'H7',
    jetId: 'SYN-JET-05',
    valveId: 'SYN-VLV-05',
    phase: 'P4',
    phaseLabel: 'PHASE-4 (synthetic fixture)',
    phaseIndex: 4,
    startedAt: TIMESTAMP,
    phaseStartedAt: TIMESTAMP,
    phaseProgress: 0.5,
    lifecycle: 'SAFE_RETURN_TO_STANDBY',
    cleaningPhase: 'CLEANING_STOPPED',
  };
  // Member order follows the C# record (Jobs.cs): in the current serialization the
  // dispatch member precedes the pressure members; the historical file carried the
  // pressure members first. Content is identical either way (parity is semantic).
  if (current) {
    job.dispatch = { ...DISPATCH };
    Object.assign(job, pressureMembers);
  } else {
    Object.assign(job, pressureMembers);
    job.dispatch = { ...DISPATCH };
  }
  job.safeReturn = {
      step: 'SR4',
      trigger: 'FIXTURE_TRIGGER',
      pendingOutcome: 'ABORTED',
      phaseAtTrigger: 'P4',
      startedAt: TIMESTAMP,
      valve: {
        valveId: 'SYN-VLV-05',
        command: 'CLOSE_COMMANDED',
        commandSeq: 11,
        feedback: 'CLOSED_CONFIRMED',
        upperLimitDetected: false,
        lowerLimitDetected: true,
        pressureBar: 0.5,
        pressureQuality: 'GOOD',
        lowPressureThresholdBar: 1,
        highPressureThresholdBar: 15,
        pressureInputValid: true,
        resolution: 'LOWER_LIMIT_CONFIRMED',
        diagnosis: 'NONE',
        feedbackSeq: 13,
      },
      axis: { command: 'RETURN_COMMANDED', commandSeq: 12, standby: 'NOT_CONFIRMED', standbySeq: null },
      failure: null,
      events: [
        { seq: 9, step: 'SR1', event: 'WATER_STOPPED', at: TIMESTAMP },
        { seq: 10, step: 'SR2', event: 'VALVE_CLOSE_QUEUED', at: TIMESTAMP },
        { seq: 11, step: 'SR2', event: 'VALVE_CLOSE_COMMANDED', at: TIMESTAMP },
        { seq: 12, step: 'SR4', event: 'AXIS_RETURN_COMMANDED', at: TIMESTAMP },
        { seq: 13, step: 'SR3', event: 'VALVE_CLOSED_CONFIRMED', at: TIMESTAMP },
      ],
  };
  return job;
}

export function buildSnapshot(profile) {
  const queueSensorIds = new Set(QUEUE_ENTRIES.map((e) => e.sensorId));
  const positions = sensorPositions(profile);
  if (positions.length !== profile.sensorLocations) {
    throw new Error(`generated ${positions.length} Sensor positions, profile expects ${profile.sensorLocations}`);
  }
  const sensors = [];
  for (let scan = 1; scan <= profile.sensorLocations; scan += 1) {
    const [row, col] = positions[scan - 1];
    const id = sensorIdFor(row, col);
    sensors.push(buildSensor(profile, scan, queueSensorIds.has(id), id === 'H7'));
  }

  const walls = ['LEFT', 'REAR', 'RIGHT', 'FRONT'].map((wall) => {
    const own = sensors.filter((s) => s.wall === wall);
    return {
      wall,
      total: own.length,
      dirty: own.filter((s) => s.classification === 'DIRTY').length,
      cleaner: own.filter((s) => s.classification === 'CLEANER').length,
      notClassified: own.filter((s) => s.classification === 'NOT_CLASSIFIED').length,
      uncertain: own.filter((s) => s.quality === 'UNCERTAIN').length,
      maxScore: Math.max(...own.map((s) => s.dirtyScore ?? 0)),
    };
  });

  return {
    fixtureStatus: FIXTURE_STATUS,
    kind: 'snapshot',
    schema: 'wjss.snapshot/2',
    apiVersion: 1,
    revision: SNAPSHOT_REVISION,
    generatedAt: TIMESTAMP,
    config: {
      revision: 1,
      publishedAt: TIMESTAMP,
      label: 'SYNTHETIC EXAMPLE - NOT A PRODUCTION VALUE',
      dirtyThreshold: DIRTY_THRESHOLD,
      staleThresholdMs: 5000,
    },
    deviceProfile: 'SIMULATOR',
    wallMap: buildWallMap(profile),
    sensors,
    waterJets: waterJets(profile),
    isolationValves: ISOLATION_VALVES.map((v) => ({ ...v })),
    walls,
    activeJob: buildActiveJob(profile),
    pump: { state: 'RUNNING', pressure: 37.5, setpoint: 40.5, readyBandLow: 36.5, readyBandHigh: 42.5, ready: true, stopRequestedAt: null },
    queue: {
      label: 'GlobalQueue (ready-to-dispatch, bounded)',
      capacity: 8,
      totalQueued: 3,
      revision: 5,
      entries: QUEUE_ENTRIES.map((e) => ({ ...e })),
      autoSequence: 'JOB_ACTIVE',
      lastDispatch: { ...DISPATCH },
    },
    sequence: {
      autoSequence: 'JOB_ACTIVE',
      mode: 'RUNNING',
      controls: {
        start: { enabled: false, reason: 'ALREADY_RUNNING' },
        pauseAfterCurrentJob: { enabled: true, reason: null },
        resume: { enabled: false, reason: 'NOT_PAUSED' },
        abortActiveJob: { enabled: false, reason: 'SAFE_RETURN_IN_PROGRESS' },
        resetCritical: { enabled: false, reason: 'CRITICAL_NOT_LATCHED' },
        pumpStart: { enabled: false, reason: 'PUMP_RUNNING' },
      },
      critical: null,
      lastJobOutcome: null,
      equipmentFaults: null,
    },
    alarms: {
      activeUnack: 1,
      activeAck: 0,
      clearedUnack: 0,
      items: [
        {
          alarmId: 'A-1',
          code: 'SYN_DEVICE_TIMEOUT_EXAMPLE',
          text: 'Synthetic timeout example (fixture presentation; not a Production alarm definition)',
          severity: 'LOW',
          state: 'ACTIVE_UNACK',
          sensorId: null,
          deviceId: 'SYN-TC-08',
          raisedAt: TIMESTAMP,
        },
      ],
    },
    communication: {
      devices: [1, 2, 3, 4, 5, 6, 7, 8].map((i) => ({
        deviceId: `SYN-TC-${String(i).padStart(2, '0')}`,
        state: 'ONLINE',
        consecutiveTimeouts: 0,
        lastSuccessAt: TIMESTAMP,
        lastLatencyMs: 5 + i,
        pollsOk: 60 + i,
        pollsFailed: 0,
      })),
    },
    // The current C# Snapshot record carries the nullable tcAcquisition member
    // (Snapshot.cs), serialized as null by the fixture generator; the historical
    // 106-Sensor form predates the member and must not gain it.
    ...(profile.serialization === 'current' ? { tcAcquisition: null } : {}),
    // The Snapshot carries NO motion member at all. The static motion commissioning catalogue lives
    // on the dedicated read-only catalogue route, so it is neither composed here nor serialized as a
    // null placeholder: a Snapshot with a motion member is refused by the contract validator.
    runtime: {
      uptimeSeconds: 42,
      sseClients: 1,
      invariantViolations: 0,
      acceptedSecondJobs: 0,
      refusedSecondJobs: 0,
      historian: { wired: false, depth: 0, capacity: 0, nearOverflow: false, rejected: 0, lastBatchLatencyMs: null, gapMarkers: 0 },
      currentRevision: SNAPSHOT_REVISION,
      stageMarker: 'STAGE_03A1_SKELETON',
    },
    trend: {
      capacity: 600,
      seriesNames: ['fixture-1', 'fixture-2', 'fixture-3', 'fixture-4'],
      points: [
        { t: 1791331200, series: [1.5, null, 2.5, 2], setpoint: 40.5, jobActive: true, alarmActive: false },
        { t: 1791331201, series: [2, 2, 2.5, 3], setpoint: 40.5, jobActive: true, alarmActive: false },
      ],
    },
  };
}

/** System.Text.Json indented output with the default (strict) encoder ('+' → \u002B), committed trailing newline kept. */
/**
 * Node port of FixtureGenerator.BuildBasicDelta: Sensors at scanOrder 1 and 2 (their
 * assigned Water Jet / Isolation Valve follow the profile's equipment table, so the
 * current delta carries the canonical assignment and the historical proof carries the
 * pre-adoption one), an explicit-null activeJob (the accepted three-state encoding: an
 * explicit null CLEARS the Active Job), the stopping Pump and one trend point.
 */
export function buildBasicDelta(profile) {
  return {
    fixtureStatus: FIXTURE_STATUS,
    kind: 'delta',
    schema: 'wjss.delta/2',
    apiVersion: 1,
    previousRevision: 10,
    revision: 11,
    generatedAt: TIMESTAMP,
    sensors: [buildSensor(profile, 1, false, false), buildSensor(profile, 2, false, false)],
    activeJob: null,
    pump: {
      state: 'STOPPING',
      pressure: 20.5,
      setpoint: 40.5,
      readyBandLow: 36.5,
      readyBandHigh: 42.5,
      ready: false,
      stopRequestedAt: TIMESTAMP,
    },
    trendPoint: {
      t: 1791331202,
      series: [2.5, 2, 1.5, 3.5],
      setpoint: 40.5,
      jobActive: true,
      alarmActive: false,
    },
  };
}

export function serializeLikeCommittedFixture(value, { trailingNewline = true } = {}) {
  const text = JSON.stringify(value, null, 2)
    .replace(/"__WJSS_NUM__([0-9.]+)"/g, '$1')
    .replace(/\+/g, '\\u002B');
  return trailingNewline ? `${text}\n` : text;
}

const invokedDirectly = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (invokedDirectly) {
  const legacy = process.argv.includes('--legacy-proof');
  const delta = process.argv.includes('--delta');
  const profile = legacy ? LEGACY_SUPERSEDED : CORRECTED;
  const payload = delta ? buildBasicDelta(profile) : buildSnapshot(profile);
  // Trailing-newline form matches each committed file: the delta and the historical
  // snapshot end with one; the current snapshot does not.
  const text = serializeLikeCommittedFixture(payload, {
    trailingNewline: delta || profile.serialization !== 'current',
  });
  if (process.argv.includes('--write')) {
    if (legacy) throw new Error('--legacy-proof is read-only; the historical form is never written');
    const target = delta ? DELTA_TARGET : TARGET;
    writeFileSync(target, text);
    process.stdout.write(`wrote ${target}\n`);
  } else {
    process.stdout.write(text);
  }
}
