import type { Wall, WallMapSlot } from './operational';

export interface CanonicalSensor {
  sensorId: string;
  slotType: 'SENSOR';
  wall: Wall;
  logicalColumn: number;
  logicalRow: number;
  wallColumn: number;
  wallRow: number;
  rowKey: string;
  scanOrder: number;
  assignedWaterJet: string;
  assignedIsolationValve: string;
  deviceId: string;
  tcFrontChannel: string;
  tcRearChannel: string;
  tcFrontOffset: number;
  tcRearOffset: number;
}
export interface SyntheticTcDevice {
  deviceId: string;
  sensorCount: number;
  channelCount: number;
  firstScanOrder: number;
  lastScanOrder: number;
}
export interface SensorMap {
  label: string;
  sensors: readonly CanonicalSensor[];
  slots: readonly WallMapSlot[];
  devices: readonly SyntheticTcDevice[];
}
export const MAP_LABEL: string;
export const LOGICAL_COLUMN_COUNT: number;
export const LOGICAL_ROWS: readonly { row: number; key: string; id: (col: number) => string }[];
export const WALL_GROUPS: readonly { wall: Wall; firstColumn: number; lastColumn: number }[];
export const CANNON_SLOTS: readonly { equipmentId: string; logicalRow: number; logicalColumn: number; logicalLabel: string }[];
export const EXPECTED: { sensors: number; channels: number; logicalSlots: number; cannonSlots: number; perWall: Record<Wall, number> };
export const TC_DEVICE_COUNT: number;
export const SENSORS_PER_TC_DEVICE: readonly number[];
export const JET_COUNT: number;
export const SENSOR_IDS: readonly string[];
export const CANNON_IDS: readonly string[];
export function wallForColumn(col: number): { wall: Wall; firstColumn: number; lastColumn: number };
export function getSensorMap(): SensorMap;
export function sensorById(id: string): CanonicalSensor | null;
export function isSensorId(id: string): boolean;
export function isCannonId(id: string): boolean;
export function wallMapSlots(): WallMapSlot[];
