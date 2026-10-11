// Stage 0.4C-K1 - the read-only Commissioning Wall Map: the approved U-shaped plan view of the
// logical Sensor matrix, presented for field loop testing.
//
// CANONICAL TOPOLOGY SOURCE (exactly one identity path, no second source):
//   - positions, wall assignment, Product Sensor identity and the two NON_SENSOR_GAP positions come
//     from the approved public-safe logical matrix (config/examples/sensor-map.example.json, the
//     public mirror of the Product wall-map contract), which the Preview host serves read-only at
//     /api/sensor-map. The UI renders that matrix; it never generates an identity of its own.
//   - measurement identity, Front/Rear ownership, evidence class and PASS blocking come from the
//     same protected 240-row drawing-derived schedule the Overview uses.
//
// The former Runtime Preview Wall Map of the Stage 0.2.1A spike established the APPROVED
// presentation this module reproduces: a U-shaped plan view (Rear top, Left and Right sides, Front
// bottom, compact centre), wall grids six rows deep with 5 / 4 / 4 / 5 wall columns, no rotation and
// no reversal, and cells rendered from the slot matrix. Only that presentation/topology portion is
// reused. The spike's other concerns - equipment slots as Water Jet anchors, Dirty Scores, alarms,
// queue and Cleaning state - are deliberately NOT part of this Stage: this is read-only commissioning
// presentation with no control authority.
//
// Nothing here reads a device, opens a socket, writes a file, reads a clock or holds any state.

/** The approved canonical wall order (Left, Rear, Right, Front). */
export const WALL_ORDER = Object.freeze(['LEFT', 'REAR', 'RIGHT', 'FRONT']);

export const WALL_LABELS = Object.freeze({
  LEFT: 'Left wall',
  REAR: 'Rear wall',
  RIGHT: 'Right wall',
  FRONT: 'Front wall',
});

/** Logical positions of the matrix. A NON_SENSOR_GAP is never a Sensor and never a Water Jet. */
export const POSITION_KIND = Object.freeze({
  SENSOR: 'SENSOR',
  NON_SENSOR_GAP: 'NON_SENSOR_GAP',
});

/** The two measurements a Product Sensor owns. They stay separate everywhere. */
export const MEASUREMENT_SIDE = Object.freeze({ F: 'F', R: 'R' });
export const SIDE_LABELS = Object.freeze({ F: 'Front', R: 'Rear' });

/** Presentation control: which measurement blocks a Wall Map cell renders. */
export const MEASUREMENT_VIEW = Object.freeze({ BOTH: 'BOTH', FRONT: 'FRONT', REAR: 'REAR' });
export const MEASUREMENT_VIEW_VALUES = Object.freeze(Object.values(MEASUREMENT_VIEW));

/** Logical rows per wall, top to bottom (G+2xx, G+1xx, G, H, I, J). */
export const WALL_ROWS = 6;

/** The protected structure of the approved matrix. Every one of these is asserted, not assumed. */
export const WALL_MAP_EXPECTED = Object.freeze({
  logicalPositions: 108,
  productSensors: 106,
  nonSensorGaps: 2,
  productCompatibleMeasurements: 212,
  gapLabels: Object.freeze(['I7', 'I16']),
  sensorsPerWall: Object.freeze({ LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 }),
  measurementsPerSensor: 2,
});

/**
 * The Wall-order navigation sequence, stated once so the screen, the tests and the documentation
 * cannot describe it differently: the approved logical position order (logical row 1 to 6, then
 * logical column 1 to 18), the I7 and I16 NON_SENSOR_GAP positions skipped as Sensors, and for each
 * Product Sensor the Front measurement before the Rear measurement.
 */
export const WALL_MAP_ORDER_TEXT = 'approved logical position order (logical row 1 to 6, then logical column 1 to 18), I7 and I16 skipped as NON_SENSOR_GAP positions, Front measurement before Rear measurement';

function require(condition, message) {
  if (!condition) throw new Error(`wall map: ${message}`);
}

/**
 * Builds the read-only Wall Map view model from the served logical matrix and the schedule rows.
 * Refuses loudly rather than presenting a topology it cannot prove: a missing wall, a duplicated
 * Sensor identity, a gap that is not I7 or I16, a Sensor without exactly one Front and one Rear
 * measurement, or a total that differs from the protected baseline all throw.
 */
export function buildWallMap(sensorMap, scheduleRows) {
  require(sensorMap && Array.isArray(sensorMap.walls) && sensorMap.walls.length === WALL_ORDER.length,
    'the served Sensor Map must carry the four walls of the logical matrix');
  require(Array.isArray(scheduleRows) && scheduleRows.length > 0, 'the Wall Map requires the schedule rows');
  const wallByName = new Map(sensorMap.walls.map((w) => [w.wall, w]));
  for (const wall of WALL_ORDER) {
    require(wallByName.has(wall), `the served Sensor Map is missing the ${wall} wall`);
    require(Array.isArray(wallByName.get(wall).slots), `the ${wall} wall carries no slots`);
  }

  // Measurement identity comes from the evidence rows only: one row per Sensor and side.
  const rowByMeasurement = new Map();
  for (const row of scheduleRows) {
    if (row && row.sensorId && row.side) rowByMeasurement.set(`${row.sensorId}:${row.side}`, row);
  }

  const positions = [];
  const bySlotId = new Map();
  const walls = [];
  for (const wall of WALL_ORDER) {
    const slots = [...wallByName.get(wall).slots];
    const wallColumns = Math.max(...slots.map((s) => s.wallColumn));
    require(wallColumns > 0 && Number.isInteger(wallColumns), `the ${wall} wall has no usable column structure`);
    const rows = [];
    for (let row = 1; row <= WALL_ROWS; row += 1) {
      const cells = [];
      for (let column = 1; column <= wallColumns; column += 1) {
        const slot = slots.find((s) => s.wallRow === row && s.wallColumn === column);
        require(Boolean(slot), `the ${wall} wall is missing the position at wall row ${row}, wall column ${column}`);
        cells.push(buildPosition(wall, slot, rowByMeasurement));
      }
      rows.push(Object.freeze(cells));
    }
    const flat = rows.flat();
    for (const position of flat) {
      require(!bySlotId.has(position.slotId), `duplicate wall position ${position.slotId}`);
      bySlotId.set(position.slotId, position);
      positions.push(position);
    }
    walls.push(Object.freeze({
      wall,
      label: WALL_LABELS[wall],
      wallColumns,
      rows: Object.freeze(rows),
      sensorCount: flat.filter((p) => p.positionKind === POSITION_KIND.SENSOR).length,
      gapCount: flat.filter((p) => p.positionKind === POSITION_KIND.NON_SENSOR_GAP).length,
    }));
  }

  const sensorPositions = positions.filter((p) => p.positionKind === POSITION_KIND.SENSOR);
  const gapPositions = positions.filter((p) => p.positionKind === POSITION_KIND.NON_SENSOR_GAP);
  const sensorIds = sensorPositions.map((p) => p.sensorId);

  require(positions.length === WALL_MAP_EXPECTED.logicalPositions, `expected ${WALL_MAP_EXPECTED.logicalPositions} logical positions, found ${positions.length}`);
  require(sensorPositions.length === WALL_MAP_EXPECTED.productSensors, `expected ${WALL_MAP_EXPECTED.productSensors} Product Sensors, found ${sensorPositions.length}`);
  require(gapPositions.length === WALL_MAP_EXPECTED.nonSensorGaps, `expected ${WALL_MAP_EXPECTED.nonSensorGaps} NON_SENSOR_GAP positions, found ${gapPositions.length}`);
  require(new Set(sensorIds).size === sensorIds.length, 'a Product Sensor identity appears more than once on the Wall Map');
  const gapLabels = gapPositions.map((p) => p.label).sort().join(',');
  require(gapLabels === [...WALL_MAP_EXPECTED.gapLabels].sort().join(','), `the NON_SENSOR_GAP positions must be exactly ${WALL_MAP_EXPECTED.gapLabels.join(' and ')}, found ${gapLabels}`);
  for (const wall of walls) {
    require(wall.sensorCount === WALL_MAP_EXPECTED.sensorsPerWall[wall.wall],
      `the ${wall.wall} wall must carry ${WALL_MAP_EXPECTED.sensorsPerWall[wall.wall]} Product Sensors, found ${wall.sensorCount}`);
  }

  // The deterministic Wall-order sequence: approved logical order, gaps skipped, Front then Rear.
  // It is published twice, in the same sequence: by measurement identity (the documented reading of
  // the order) and by physical Channel key (the ONE selection identity the whole screen uses, so the
  // Wall Map, the Overview, Sensor detail and the Preview workflow can never disagree).
  const order = [];
  const channelOrder = [];
  const ordered = new Set();
  for (const position of [...positions].sort((a, b) => (a.logicalRow - b.logicalRow) || (a.logicalColumn - b.logicalColumn))) {
    if (position.positionKind !== POSITION_KIND.SENSOR) continue;
    for (const [key, channelKey] of [[position.frontKey, position.frontChannelKey], [position.rearKey, position.rearChannelKey]]) {
      require(!ordered.has(key), `measurement ${key} appears twice in the Wall order`);
      ordered.add(key);
      order.push(key);
      channelOrder.push(channelKey);
    }
  }
  require(order.length === WALL_MAP_EXPECTED.productCompatibleMeasurements,
    `expected ${WALL_MAP_EXPECTED.productCompatibleMeasurements} measurements in the Wall order, found ${order.length}`);
  const productKeys = scheduleRows.filter((row) => row.productCompatible && row.measurementId).map((row) => row.measurementId);
  require(productKeys.length === order.length && productKeys.every((key) => ordered.has(key)),
    'the Wall order must cover exactly the Product-compatible measurements');

  const positionByMeasurementKey = new Map();
  const positionByChannelKey = new Map();
  const measurementKeyByChannelKey = new Map();
  for (const position of positions) {
    positionByMeasurementKey.set(position.frontKey, position);
    positionByMeasurementKey.set(position.rearKey, position);
    positionByChannelKey.set(position.frontChannelKey, position);
    positionByChannelKey.set(position.rearChannelKey, position);
    measurementKeyByChannelKey.set(position.frontChannelKey, position.frontKey);
    measurementKeyByChannelKey.set(position.rearChannelKey, position.rearKey);
  }
  for (const position of positions) {
    require(positionByChannelKey.get(position.frontChannelKey) === position && positionByChannelKey.get(position.rearChannelKey) === position,
      `wall position ${position.label} does not own two distinct physical Channels`);
  }

  const counts = Object.freeze({
    logicalPositions: positions.length,
    productSensors: sensorIds.length,
    nonSensorGaps: gapPositions.length,
    productCompatibleMeasurements: order.length,
    sensorsPerWall: Object.freeze(Object.fromEntries(walls.map((w) => [w.wall, w.sensorCount]))),
  });

  return Object.freeze({
    label: sensorMap.label ?? 'approved logical matrix (public-safe example)',
    walls: Object.freeze(walls),
    positions: Object.freeze(positions),
    gapPositions: Object.freeze(gapPositions),
    order: Object.freeze(order),
    channelOrder: Object.freeze(channelOrder),
    orderText: WALL_MAP_ORDER_TEXT,
    counts,
    positionByMeasurementKey,
    positionByChannelKey,
    measurementKeyByChannelKey,
    bySlotId,
    sensorPositionBySensorId: new Map(sensorPositions.map((p) => [p.sensorId, p])),
  });
}

function buildPosition(wall, slot, rowByMeasurement) {
  const isGap = slot.positionKind === POSITION_KIND.NON_SENSOR_GAP;
  require(slot.positionKind === POSITION_KIND.SENSOR || isGap, `wall position ${slot.slotId} has an unknown position kind '${slot.positionKind}'`);
  // A NON_SENSOR_GAP never carries a Sensor identity; a SENSOR position always does.
  require(isGap ? !slot.sensorId : Boolean(slot.sensorId), `wall position ${slot.slotId} violates the Sensor / NON_SENSOR_GAP contract`);
  const label = isGap ? slot.logicalLabel : slot.sensorId;
  require(typeof label === 'string' && label.length > 0, `wall position ${slot.slotId} carries no identity`);
  const front = rowByMeasurement.get(`${label}:F`);
  const rear = rowByMeasurement.get(`${label}:R`);
  require(Boolean(front), `no Front measurement exists for wall position '${label}'`);
  require(Boolean(rear), `no Rear measurement exists for wall position '${label}'`);
  require(front.measurementId !== rear.measurementId, `wall position '${label}' does not own two distinct measurements`);
  return Object.freeze({
    slotId: slot.slotId,
    positionKind: slot.positionKind,
    wall,
    logicalRow: slot.logicalRow,
    logicalColumn: slot.logicalColumn,
    wallRow: slot.wallRow,
    wallColumn: slot.wallColumn,
    sensorId: slot.sensorId ?? null,
    label,
    frontKey: front.measurementId,
    rearKey: rear.measurementId,
    frontChannelKey: front.physicalChannelKey,
    rearChannelKey: rear.physicalChannelKey,
    selectableAsSensor: !isGap,
  });
}

/**
 * The measurement blocks a cell renders under the selected view control. Front and Rear are never
 * merged, never copied into each other and never substituted for each other: with `FRONT` only the
 * Front block exists, with `REAR` only the Rear block exists.
 */
export function visibleMeasurements(position, view = MEASUREMENT_VIEW.BOTH) {
  if (!MEASUREMENT_VIEW_VALUES.includes(view)) throw new Error(`wall map: unknown measurement view '${view}'`);
  const blocks = [];
  if (view !== MEASUREMENT_VIEW.REAR) blocks.push(Object.freeze({ side: MEASUREMENT_SIDE.F, sideLabel: SIDE_LABELS.F, key: position.frontKey, channelKey: position.frontChannelKey }));
  if (view !== MEASUREMENT_VIEW.FRONT) blocks.push(Object.freeze({ side: MEASUREMENT_SIDE.R, sideLabel: SIDE_LABELS.R, key: position.rearKey, channelKey: position.rearChannelKey }));
  return Object.freeze(blocks);
}

/**
 * The measurement identity of one physical Channel key ('G+205:F'), or null when the Channel has no
 * Wall Map position (an unassigned physical Channel, for example).
 */
export function wallMeasurementKey(wallMap, channelKey) {
  return wallMap.measurementKeyByChannelKey.get(channelKey) ?? null;
}

/**
 * Position of one measurement in the Wall order, by physical Channel key, or -1 when that Channel has
 * no Wall Map position.
 */
export function wallOrderIndex(wallMap, channelKey) {
  return wallMap.channelOrder.indexOf(channelKey);
}

/**
 * One deterministic step of the Wall-order navigation. Direction -1 is Previous, +1 is Next.
 * With no current selection the step starts at the first (Next) or last (Previous) position; at
 * either end of the sequence the selection stays where it is and `edge` reports the boundary.
 * Nothing here changes a verdict, an observation or any evidence.
 */
export function wallOrderStep(wallMap, fromKey, direction) {
  require(direction === -1 || direction === 1, 'the Wall order step direction must be -1 or +1');
  const keys = wallMap.channelOrder;
  require(keys.length > 0, 'the Wall order is empty');
  const index = fromKey ? keys.indexOf(fromKey) : -1;
  if (index === -1) {
    return Object.freeze({ key: direction === -1 ? keys[keys.length - 1] : keys[0], moved: true, edge: false, index: direction === -1 ? keys.length - 1 : 0 });
  }
  const next = index + direction;
  if (next < 0 || next >= keys.length) {
    return Object.freeze({ key: keys[index], moved: false, edge: true, index });
  }
  return Object.freeze({ key: keys[next], moved: true, edge: false, index: next });
}
