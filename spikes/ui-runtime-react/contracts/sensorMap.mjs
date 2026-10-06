// WJSS Stage 0.2.1A — canonical Sensor map (single mapping source for the spike).
//
// Owner-confirmed logical matrix (Stage 0.2.1A sensor-mapping correction):
//   18 logical columns x 6 logical rows = 108 logical positions
//   2 Cannon equipment slots (logical I7 = CANNON_REAR, logical I16 = CANNON_FRONT)
//   106 Sensor locations, each with two Thermocouple channels (TC_F, TC_R) = 212 channels
//   Walls: LEFT columns 1-4 (24), REAR 5-9 (29 + 1 Cannon), RIGHT 10-13 (24), FRONT 14-18 (29 + 1 Cannon)
//
// The Sensor labels (G+2xx, G+1xx, G, H, I, J + logical column) and the matrix are the Owner's
// logical reference. Everything else here — scan order, synthetic device distribution, channel
// identifiers, Water Jet and Isolation Valve assignment — is a SYNTHETIC SPIKE PARAMETER, NOT A
// PRODUCTION VALUE. No register map, address, coordinate, or Production assignment is implied.
//
// Consumers: runtime harness, validator, fixtures, scenario runner, tests. The React UI does NOT
// import this module; it renders the wall map delivered in the runtime Snapshot.

export const MAP_LABEL = 'OWNER LOGICAL MATRIX + SYNTHETIC DEVICE/CHANNEL/JET ASSIGNMENT - NOT A PRODUCTION MAPPING';

export const LOGICAL_COLUMN_COUNT = 18;

/** Logical rows, top to bottom. `id(col)` builds the Sensor ID for a logical column. */
export const LOGICAL_ROWS = Object.freeze([
  Object.freeze({ row: 1, key: 'G+2xx', id: (c) => `G+${200 + c}` }),
  Object.freeze({ row: 2, key: 'G+1xx', id: (c) => `G+${100 + c}` }),
  Object.freeze({ row: 3, key: 'G', id: (c) => `G${c}` }),
  Object.freeze({ row: 4, key: 'H', id: (c) => `H${c}` }),
  Object.freeze({ row: 5, key: 'I', id: (c) => `I${c}` }),
  Object.freeze({ row: 6, key: 'J', id: (c) => `J${c}` }),
]);

/** Wall column groups in logical-column order. */
export const WALL_GROUPS = Object.freeze([
  Object.freeze({ wall: 'LEFT', firstColumn: 1, lastColumn: 4 }),
  Object.freeze({ wall: 'REAR', firstColumn: 5, lastColumn: 9 }),
  Object.freeze({ wall: 'RIGHT', firstColumn: 10, lastColumn: 13 }),
  Object.freeze({ wall: 'FRONT', firstColumn: 14, lastColumn: 18 }),
]);

/** Cannon equipment slots. Equipment, not Sensors. */
export const CANNON_SLOTS = Object.freeze([
  Object.freeze({ equipmentId: 'CANNON_REAR', logicalRow: 5, logicalColumn: 7, logicalLabel: 'I7' }),
  Object.freeze({ equipmentId: 'CANNON_FRONT', logicalRow: 5, logicalColumn: 16, logicalLabel: 'I16' }),
]);

export const EXPECTED = Object.freeze({
  sensors: 106,
  channels: 212,
  logicalSlots: 108,
  cannonSlots: 2,
  perWall: Object.freeze({ LEFT: 24, REAR: 29, RIGHT: 24, FRONT: 29 }),
});

/** Synthetic: 8 Thermocouple devices; sensors are assigned in scan order. */
export const TC_DEVICE_COUNT = 8;
/** Synthetic Sensor count per TC device (devices 1-2: 14 Sensors / 28 channels; 3-8: 13 / 26). */
export const SENSORS_PER_TC_DEVICE = Object.freeze([14, 14, 13, 13, 13, 13, 13, 13]);
export const JET_COUNT = 8;

const pad2 = (n) => String(n).padStart(2, '0');

export function wallForColumn(col) {
  const g = WALL_GROUPS.find((w) => col >= w.firstColumn && col <= w.lastColumn);
  if (!g) throw new Error(`logical column out of range: ${col}`);
  return g;
}

function cannonAt(row, col) {
  return CANNON_SLOTS.find((c) => c.logicalRow === row && c.logicalColumn === col) ?? null;
}

function build() {
  if (SENSORS_PER_TC_DEVICE.reduce((a, b) => a + b, 0) !== EXPECTED.sensors) throw new Error('device distribution does not cover 106 Sensors');
  const slots = [];
  const sensors = [];
  // Scan order: logical rows top to bottom, logical columns 1-18 left to right, Cannon slots skipped.
  let scan = 0;
  let device = 0;
  let onDevice = 0;
  for (const r of LOGICAL_ROWS) {
    for (let col = 1; col <= LOGICAL_COLUMN_COUNT; col += 1) {
      const g = wallForColumn(col);
      const pos = { wall: g.wall, logicalColumn: col, logicalRow: r.row, wallColumn: col - g.firstColumn + 1, wallRow: r.row };
      const cannon = cannonAt(r.row, col);
      if (cannon) {
        slots.push(Object.freeze({ slotId: `SLOT-R${r.row}-C${pad2(col)}`, slotType: 'CANNON', ...pos, sensorId: null, equipmentId: cannon.equipmentId }));
        continue;
      }
      scan += 1;
      if (onDevice === SENSORS_PER_TC_DEVICE[device]) {
        device += 1;
        onDevice = 0;
      }
      const deviceId = `SYN-TC-${pad2(device + 1)}`;
      const frontOffset = 2 * onDevice;
      const rearOffset = 2 * onDevice + 1;
      onDevice += 1;
      const jet = ((scan - 1) % JET_COUNT) + 1;
      const sensorId = r.id(col);
      sensors.push(
        Object.freeze({
          sensorId,
          slotType: 'SENSOR',
          ...pos,
          rowKey: r.key,
          scanOrder: scan,
          assignedWaterJet: `SYN-JET-${jet}`,
          assignedIsolationValve: `SYN-VLV-${jet}`,
          deviceId,
          tcFrontChannel: `${deviceId}:CH${pad2(frontOffset)}`,
          tcRearChannel: `${deviceId}:CH${pad2(rearOffset)}`,
          tcFrontOffset: frontOffset,
          tcRearOffset: rearOffset,
        }),
      );
      slots.push(Object.freeze({ slotId: `SLOT-R${r.row}-C${pad2(col)}`, slotType: 'SENSOR', ...pos, sensorId, equipmentId: null }));
    }
  }
  const devices = Array.from({ length: TC_DEVICE_COUNT }, (_, i) => {
    const deviceId = `SYN-TC-${pad2(i + 1)}`;
    const own = sensors.filter((s) => s.deviceId === deviceId);
    return Object.freeze({ deviceId, sensorCount: own.length, channelCount: own.length * 2, firstScanOrder: own[0].scanOrder, lastScanOrder: own[own.length - 1].scanOrder });
  });
  return Object.freeze({ label: MAP_LABEL, sensors: Object.freeze(sensors), slots: Object.freeze(slots), devices: Object.freeze(devices) });
}

const MAP = build();

/** The canonical map: { sensors[106], slots[108] (wall map), devices[8] }. Frozen; build once. */
export function getSensorMap() {
  return MAP;
}

export const SENSOR_IDS = Object.freeze(MAP.sensors.map((s) => s.sensorId));
export const CANNON_IDS = Object.freeze(CANNON_SLOTS.map((c) => c.equipmentId));
const BY_ID = new Map(MAP.sensors.map((s) => [s.sensorId, s]));

export function sensorById(id) {
  return BY_ID.get(id) ?? null;
}
export function isSensorId(id) {
  return BY_ID.has(id);
}
export function isCannonId(id) {
  return CANNON_IDS.includes(id);
}

/** Public wall-map slots for the Snapshot (contract WallMapSlot). */
export function wallMapSlots() {
  return MAP.slots.map((s) => ({ ...s }));
}
