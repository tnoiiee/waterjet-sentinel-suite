// WJSS Operator presentation - the ONE topology source the screen reads.
//
// Why this file exists: a wall map, a badge, a queue row and a maintenance grid that each carry their
// own copy of the WJ / IV / WSB table will drift, and drift on an operator screen looks like data. So
// every tab takes geometry, identity and scaling from here, and nothing else. The table is not
// invented here: apps/ui/test/ui.contract.test.mjs parses the two .NET catalogs this mirrors
// (packages/domain/WaterJetTopologyCatalog.cs and packages/application/Motion/MotionTopologyCatalog.cs)
// and the constants in packages/domain/CleaningPathContracts.cs, then fails if any row below stops
// agreeing with them. That is what makes "one canonical module" a checked fact rather than a claim.
//
// Authority: the Owner-approved canonical Equipment topology adoption (2026-10-25) for the installed
// wall / tier and the target coverage of WJ1..WJ8, and the Owner topology ruling of 2026-11-11 for
// the tier split (logical rows 1-3 UPPER, rows 4-6 LOWER) and for the default assignment counts.
// An installed Water Jet sprays the OPPOSITE wall in the same tier; assignment follows the TARGET wall,
// never the installed position. The fine-grained installed placement of all eight jets is UNRESOLVED,
// so this module carries no placement anchor and derives none.

export const WALL_ORDER = Object.freeze(['LEFT', 'REAR', 'RIGHT', 'FRONT']);
// Wall wording, once. The map header, the summary pills and the breakdown table all read this, so a
// wall is never called two things on one screen.
export const WALL_LABELS = Object.freeze({ LEFT: 'LEFT wall', REAR: 'REAR wall', RIGHT: 'RIGHT wall', FRONT: 'FRONT wall' });

export const LOGICAL_ROWS = 6;
export const UPPER_ROWS = Object.freeze([1, 2, 3]);
export const LOWER_ROWS = Object.freeze([4, 5, 6]);
export const WALL_ROWS = LOGICAL_ROWS;

// Columns per wall and cells per wall. 4 + 5 + 4 + 5 columns across 6 rows = 24 / 30 / 24 / 30 = 108.
export const WALL_COLUMNS = Object.freeze({ LEFT: 4, REAR: 5, RIGHT: 4, FRONT: 5 });
export const EXPECTED_PER_WALL = Object.freeze({ LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 });
export const EXPECTED_TOTAL = 108;

// The Owner-approved default assignments by physical Sensor wall / tier, summed to 108. These are the
// numbers every tab shows beside a WJ identity; they are not recomputed from a per-tab table anywhere.
export const DEFAULT_ASSIGNMENTS = Object.freeze({
  WJ1: 12, WJ2: 15, WJ3: 12, WJ4: 15, WJ5: 15, WJ6: 15, WJ7: 12, WJ8: 12,
});
export const ASSIGNMENT_TOTAL = 108;

// Rows 1-3 of a wall are UPPER and rows 4-6 LOWER. A row outside 1 to 6 has no tier: it is reported,
// never guessed. This mirrors WaterJetTopologyCatalog.RegionForLogicalRow, which throws for the same
// input - a browser has no exception to catch here, so the answer is a value the screen can show.
export function tierOfRow(row) {
  if (UPPER_ROWS.includes(row)) return 'UPPER';
  if (LOWER_ROWS.includes(row)) return 'LOWER';
  return 'UNPLACED';
}

// WJ1..WJ8 exactly as WaterJetTopologyCatalog.WaterJets states them: installed wall and tier, target
// wall and tier, and the one dedicated isolation valve.
export const WATER_JETS = Object.freeze([
  { wj: 'WJ1', installedWall: 'LEFT', installedTier: 'UPPER', targetWall: 'RIGHT', targetTier: 'UPPER', iv: 'IV1', placementKind: 'UNRESOLVED', placementAnchors: [] },
  { wj: 'WJ2', installedWall: 'REAR', installedTier: 'UPPER', targetWall: 'FRONT', targetTier: 'UPPER', iv: 'IV2', placementKind: 'UNRESOLVED', placementAnchors: [] },
  { wj: 'WJ3', installedWall: 'RIGHT', installedTier: 'UPPER', targetWall: 'LEFT', targetTier: 'UPPER', iv: 'IV3', placementKind: 'UNRESOLVED', placementAnchors: [] },
  { wj: 'WJ4', installedWall: 'FRONT', installedTier: 'UPPER', targetWall: 'REAR', targetTier: 'UPPER', iv: 'IV4', placementKind: 'UNRESOLVED', placementAnchors: [] },
  { wj: 'WJ5', installedWall: 'FRONT', installedTier: 'LOWER', targetWall: 'REAR', targetTier: 'LOWER', iv: 'IV5', placementKind: 'UNRESOLVED', placementAnchors: [] },
  { wj: 'WJ6', installedWall: 'REAR', installedTier: 'LOWER', targetWall: 'FRONT', targetTier: 'LOWER', iv: 'IV6', placementKind: 'UNRESOLVED', placementAnchors: [] },
  { wj: 'WJ7', installedWall: 'LEFT', installedTier: 'LOWER', targetWall: 'RIGHT', targetTier: 'LOWER', iv: 'IV7', placementKind: 'UNRESOLVED', placementAnchors: [] },
  { wj: 'WJ8', installedWall: 'RIGHT', installedTier: 'LOWER', targetWall: 'LEFT', targetTier: 'LOWER', iv: 'IV8', placementKind: 'UNRESOLVED', placementAnchors: [] },
]);

// The placement disposition, carried as the catalog words it: all eight fine-grained placements are
// UNRESOLVED with no anchor, and this presentation therefore derives no position for any Water Jet. The
// sentence is compared against WaterJetTopologyCatalog.UnresolvedPlacementWording by the contract test.
export const UNRESOLVED_PLACEMENT_WORDING = 'Fine-grained installed placement pending authoritative layout (canonical wall/tier confirmed; no placement anchors)';

export const WATER_JETS_BY_ID = Object.freeze(
  WATER_JETS.reduce((map, jet) => {
    map[jet.wj] = jet;
    return map;
  }, Object.create(null)),
);

export const ISOLATION_VALVES = Object.freeze(WATER_JETS.map((jet) => ({ iv: jet.iv, wj: jet.wj })));

// Four motion controllers, one per WSB cabinet, four axes each, two Water Jets each - the Owner-
// confirmed motion topology as MotionTopologyCatalog records it. A and C are the Horizontal Axis,
// B and D the Vertical Axis; the operator wording is exactly "Horizontal Axis" / "Vertical Axis".
// The brake-output ordinal is A -> 1, B -> 2, C -> 3, D -> 4 and is diagnostic identity only.
//
// The Runtime does NOT publish the WSB cabinet or the axis letters in any payload, so this block is
// the presentation's own canonical table and the only place in apps/ui that states it. It is displayed
// as a topology fact with the sentence below, never as live equipment state.
export const WSB_CABINETS = Object.freeze([
  { wsb: 'WSB1', controller: 'MC-WSB1', jets: [
    { wj: 'WJ1', horizontalAxis: 'A', verticalAxis: 'B' },
    { wj: 'WJ2', horizontalAxis: 'C', verticalAxis: 'D' },
  ] },
  { wsb: 'WSB2', controller: 'MC-WSB2', jets: [
    { wj: 'WJ3', horizontalAxis: 'C', verticalAxis: 'D' },
    { wj: 'WJ4', horizontalAxis: 'A', verticalAxis: 'B' },
  ] },
  { wsb: 'WSB3', controller: 'MC-WSB3', jets: [
    { wj: 'WJ5', horizontalAxis: 'A', verticalAxis: 'B' },
    { wj: 'WJ6', horizontalAxis: 'C', verticalAxis: 'D' },
  ] },
  { wsb: 'WSB4', controller: 'MC-WSB4', jets: [
    { wj: 'WJ7', horizontalAxis: 'A', verticalAxis: 'B' },
    { wj: 'WJ8', horizontalAxis: 'C', verticalAxis: 'D' },
  ] },
]);

export const AXIS_ROLES = Object.freeze({ A: 'Horizontal', B: 'Vertical', C: 'Horizontal', D: 'Vertical' });
export const AXIS_WORDING = Object.freeze({ Horizontal: 'Horizontal Axis', Vertical: 'Vertical Axis' });
export const BRAKE_OUTPUT_ORDER = Object.freeze({ A: 1, B: 2, C: 3, D: 4 });
export const WSB_SOURCE_NOTE = Object.freeze(
  'WSB cabinet and axis letters are the canonical motion topology table carried by this presentation; '
  + 'the read-only Runtime publishes Water Jet and Isolation Valve identity, not a cabinet or an axis, so '
  + 'this grid is a topology statement and never a live motion state.',
);

// The commissioned Water Jet / Main Pump stop order, stated as a rule, not as an observation. The
// Runtime publishes pump state and the last requested stop; the ordering itself is the interlock the
// commissioning record carries, and the screen shows it as the rule the operator is working under.
export const PUMP_STOP_ORDER_RULE = Object.freeze(
  'COMMISSIONED STOP ORDER: DO-007 (Run) OFF before DO-003 (Enable) OFF. '
  + 'START ORDER: DO-003 (Enable) ON, then DO-007 (Run) ON.',
);

// ---- coordinate system: the exact hardware scaling, from CleaningPathContracts ------------------
// integer millimetres 0..280 inclusive, native = millimetres x 10000, checked, never clamped.
export const WORKSPACE_MIN_MM = 0;
export const WORKSPACE_MAX_MM = 280;
export const NATIVE_PER_MILLIMETRE = 10000;
export const MAX_NATIVE = 2800000;
export const SAFE_RETURN_CENTRE_MM = Object.freeze({ horizontal: 125, vertical: 125 });
export const SAFE_RETURN_CENTRE_NATIVE = Object.freeze({ horizontal: 1250000, vertical: 1250000 });
export const SCALING_NOTE = Object.freeze('1 mm = 10,000 native units (integer millimetres 0-280, checked, never clamped)');

// The millimetre / native pair as the screen presents it. tpToRpRatio is a MANUAL MOTION PROFILE value,
// not a constant of this product: the Runtime publishes no tpToRpRatio in any payload the Operator
// screen reads, so it is never printed as a number here. A profile that sets 2:1 belongs to the profile
// file and to the motion library's own report, not to a wall.
export function coordinatePair(millimetres) {
  const conversion = mmToNative(millimetres);
  if (!conversion.ok) {
    return { text: 'OUT OF RANGE (' + String(millimetres) + ' mm): ' + conversion.reason, ok: false };
  }
  return { text: String(millimetres) + ' mm  =  ' + grouped(conversion.native) + ' native', ok: true };
}

export function grouped(value) {
  return String(value).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

export function mmToNative(millimetres) {
  if (typeof millimetres !== 'number' || !Number.isFinite(millimetres)) {
    return { ok: false, reason: 'not a number' };
  }
  if (!Number.isInteger(millimetres)) {
    return { ok: false, reason: 'must be an integer millimetre value' };
  }
  if (millimetres < WORKSPACE_MIN_MM || millimetres > WORKSPACE_MAX_MM) {
    return { ok: false, reason: 'outside 0-280 mm' };
  }
  const native = millimetres * NATIVE_PER_MILLIMETRE;
  if (native > MAX_NATIVE) {
    return { ok: false, reason: 'native overflow' };
  }
  return { ok: true, native };
}

export function nativeToMm(native) {
  if (typeof native !== 'number' || !Number.isFinite(native)) return null;
  if (native % NATIVE_PER_MILLIMETRE !== 0) return null;
  const mm = native / NATIVE_PER_MILLIMETRE;
  if (mm < WORKSPACE_MIN_MM || mm > WORKSPACE_MAX_MM) return null;
  return mm;
}

// The positions a Cleaning Job walks, named by the stage identity the Runtime publishes and in the
// published order. This list is an INDEX into the workspace timeline, not a source of positions: the
// read-only API publishes a stage, its ordinal and its state, and it publishes NO coordinate for any
// stage, so a panel that showed a millimetre value for P1..P6 would be showing an invention. What the
// screen can state as fact is the scaling those coordinates will use and the one fixed position the
// contract does define, the Safe Return Centre below.
export const CLEANING_PATH_STAGES = Object.freeze([
  { key: 'P1', stage: 'MOVE_TO_P1' },
  { key: 'P2', stage: 'P2' },
  { key: 'P3', stage: 'P3' },
  { key: 'P4', stage: 'P4' },
  { key: 'P5', stage: 'P5' },
  { key: 'P6', stage: 'P6' },
]);

// The cockpit chain, in the order the timeline publishes it. The four framing stages are the valve and
// the mandatory return; a cockpit that drew only P1..P6 would hide what the job is actually waiting on.
export const COCKPIT_STAGES = Object.freeze([
  { key: 'CENTRE', stage: 'MANDATORY_SAFE_RETURN', note: 'fixed contract centre' },
  { key: 'P1', stage: 'MOVE_TO_P1' },
  { key: 'IV_OPEN', stage: 'OPEN_IV' },
  { key: 'P2', stage: 'P2' },
  { key: 'P3', stage: 'P3' },
  { key: 'P4', stage: 'P4' },
  { key: 'P5', stage: 'P5' },
  { key: 'P6', stage: 'P6' },
  { key: 'IV_CLOSE', stage: 'CLOSE_IV' },
  { key: 'SAFE_RETURN', stage: 'MANDATORY_SAFE_RETURN', note: 'to (125, 125)' },
]);

export const STAGE_COORDINATE_NOTE = Object.freeze(
  'P1-P6 COORDINATES: NOT PUBLISHED BY THE READ-ONLY RUNTIME - the workspace timeline publishes the stage, '
  + 'its ordinal and its state only, so no millimetre value is shown for a stage it does not report',
);

export const SAFE_RETURN_LABEL = Object.freeze('Safe Return (125, 125)');

// ---- cross-checks a tab can run against the published payload -----------------------------------

// Every Water Jet the host publishes must agree with this table, wall and tier and valve, or the screen
// has to say so: a wall map drawn from a table that disagrees with the Snapshot is worse than no map.
export function agreeWithSnapshot(waterJets) {
  if (!Array.isArray(waterJets) || waterJets.length === 0) {
    return { ok: false, reason: 'no Water Jet list published in this Snapshot' };
  }
  if (waterJets.length !== WATER_JETS.length) {
    return { ok: false, reason: String(waterJets.length) + ' Water Jets published, ' + String(WATER_JETS.length) + ' in the canonical table' };
  }
  for (const published of waterJets) {
    const canonical = WATER_JETS_BY_ID[published.waterJetId];
    if (!canonical) {
      return { ok: false, reason: String(published.waterJetId) + ' is not a canonical Water Jet identity' };
    }
    if (published.installedWall !== canonical.installedWall || published.installedRegion !== canonical.installedTier) {
      return { ok: false, reason: canonical.wj + ' is published on ' + String(published.installedWall) + '/' + String(published.installedRegion) + ', the canonical table says ' + canonical.installedWall + '/' + canonical.installedTier };
    }
    if (published.targetWall !== canonical.targetWall || published.targetRegion !== canonical.targetTier) {
      return { ok: false, reason: canonical.wj + ' is published to clean ' + String(published.targetWall) + '/' + String(published.targetRegion) + ', the canonical table says ' + canonical.targetWall + '/' + canonical.targetTier };
    }
    if (published.dedicatedIsolationValveId !== canonical.iv) {
      return { ok: false, reason: canonical.wj + ' is published with ' + String(published.dedicatedIsolationValveId) + ', the canonical pairing is ' + canonical.iv };
    }
  }
  return { ok: true, reason: 'WJ1-WJ8 match the canonical topology' };
}

// The three summary pills. Classification has three published values - DIRTY, CLEANER, NOT_CLASSIFIED -
// and Quality carries UNCERTAIN separately, so the third pill is uncertain plus notClassified and the
// three together are the whole wall. That is what makes the sum exactly 108 a real check instead of a
// rounding of three unrelated counters, and the caption always shows the split.
export function summaryPills(walls) {
  if (!Array.isArray(walls) || walls.length === 0) {
    return { state: 'no data', note: 'WALL SUMMARY NOT PUBLISHED IN THIS SNAPSHOT', pills: [], total: 0, expected: EXPECTED_TOTAL };
  }
  let cleaner = 0;
  let dirty = 0;
  let uncertain = 0;
  let notClassified = 0;
  let total = 0;
  for (const wall of walls) {
    cleaner += number(wall.cleaner);
    dirty += number(wall.dirty);
    uncertain += number(wall.uncertain);
    notClassified += number(wall.notClassified);
    total += number(wall.total);
  }
  const pills = [
    { key: 'cleaner', label: 'Cleaner', value: cleaner, tone: 'ok' },
    { key: 'dirty', label: 'Dirty', value: dirty, tone: 'bad' },
    { key: 'uncertain', label: 'Uncertain', value: uncertain + notClassified, tone: 'warn' },
  ];
  const sum = cleaner + dirty + uncertain + notClassified;
  const mismatch = sum !== EXPECTED_TOTAL || total !== EXPECTED_TOTAL;
  return {
    state: mismatch ? 'mismatch' : 'ok',
    pills,
    total: sum,
    expected: EXPECTED_TOTAL,
    breakdown: 'uncertain ' + String(uncertain) + ' + not classified ' + String(notClassified),
    note: mismatch
      ? 'TOPOLOGY_COUNT_MISMATCH: the published wall summary adds to ' + String(sum) + ', not ' + String(EXPECTED_TOTAL)
      : '108 Sensors across four walls; the third pill is uncertain plus not classified',
    perWall: walls.map((wall) => ({
      wall: wall.wall,
      total: number(wall.total),
      cleaner: number(wall.cleaner),
      dirty: number(wall.dirty),
      uncertain: number(wall.uncertain),
      notClassified: number(wall.notClassified),
      maxScore: wall.maxScore,
    })),
  };
}

// A per-Sensor count is only meaningful against the wall it belongs to, so the map checks the published
// sensor set against the canonical per-wall figure before it draws a single cell.
export function agreeWithSensors(sensors) {
  if (!Array.isArray(sensors) || sensors.length !== EXPECTED_TOTAL) {
    return {
      ok: false,
      reason: sensors ? String(sensors.length) + ' Sensors published, ' + String(EXPECTED_TOTAL) + ' expected' : 'no Sensor set published in this Snapshot',
    };
  }
  const perWall = Object.create(null);
  for (const sensor of sensors) {
    perWall[sensor.wall] = (perWall[sensor.wall] || 0) + 1;
    if (tierOfRow(sensor.logicalRow) === 'UNPLACED') {
      return { ok: false, reason: String(sensor.sensorId) + ' carries logical row ' + String(sensor.logicalRow) + ', which is outside 1-6' };
    }
  }
  for (const wall of WALL_ORDER) {
    if (perWall[wall] !== EXPECTED_PER_WALL[wall]) {
      return { ok: false, reason: wall + ' holds ' + String(perWall[wall]) + ' Sensors, the canonical figure is ' + String(EXPECTED_PER_WALL[wall]) };
    }
  }
  return { ok: true, reason: '108 Sensors across LEFT 24, REAR 30, RIGHT 24, FRONT 30' };
}

// Assignment counts by Water Jet, taken from the published Sensor set rather than from a table, and
// compared against the canonical default so a migrated profile that reassigns Sensors is shown as the
// difference it is instead of silently redrawing the badges.
export function assignmentCountByJet(sensors) {
  const counts = Object.create(null);
  for (const jet of WATER_JETS) {
    counts[jet.wj] = 0;
  }
  if (Array.isArray(sensors)) {
    for (const sensor of sensors) {
      if (Object.prototype.hasOwnProperty.call(counts, sensor.assignedWaterJetId)) {
        counts[sensor.assignedWaterJetId] += 1;
      }
    }
  }
  let total = 0;
  const rows = WATER_JETS.map((jet) => {
    const assigned = counts[jet.wj];
    total += assigned;
    return {
      wj: jet.wj,
      assigned,
      canonical: DEFAULT_ASSIGNMENTS[jet.wj],
      drift: assigned !== DEFAULT_ASSIGNMENTS[jet.wj],
      target: jet.targetWall + '/' + jet.targetTier,
      iv: jet.iv,
    };
  });
  const assigned = Object.create(null);
  for (const row of rows) {
    assigned[row.wj] = row.assigned;
  }
  return { rows, assigned, total, expected: ASSIGNMENT_TOTAL, complete: total === ASSIGNMENT_TOTAL };
}

function number(value) {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}
