// Test helper: the approved public-safe logical matrix, projected exactly the way the Preview host
// serves it (one wall group per canonical wall, carrying the wall columns used for the U-shaped map).
//
// Two things are deliberately NOT projected: the synthetic device/channel assignment and the Water
// Jet anchor a gap records. Neither may be derived into a Sensor identity or a Water Jet identity
// from a wall position in this Stage.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
export const SENSOR_MAP_PATH = join(HERE, '..', '..', '..', '..', 'config', 'examples', 'sensor-map.example.json');

export const sensorMapExample = JSON.parse(readFileSync(SENSOR_MAP_PATH, 'utf8'));
export const matrixSlots = sensorMapExample.logicalMatrix.slots;

/** The served shape: { label, walls: [{ wall, slots }] }, in canonical wall order. */
export function servedSensorMap(walls = ['LEFT', 'REAR', 'RIGHT', 'FRONT']) {
  return {
    label: sensorMapExample._label,
    walls: walls.map((wall) => ({
      wall,
      slots: matrixSlots.filter((slot) => slot.wall === wall).map((slot) => ({
        slotId: slot.slotId,
        positionKind: slot.positionKind,
        logicalColumn: slot.logicalColumn,
        logicalRow: slot.logicalRow,
        wallColumn: slot.wallColumn,
        wallRow: slot.wallRow,
        sensorId: slot.sensorId,
        logicalLabel: slot.logicalLabel ?? null,
      })),
    })),
  };
}
