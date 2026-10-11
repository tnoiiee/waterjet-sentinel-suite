// Test helper: the committed Baseline REV1 evidence schedule, parsed once per test process.
// The schedule is read from the repository evidence file exactly as committed. No test rewrites it.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseSchedule, scheduleCounts, nodeInventory } from '../../src/schedule.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
export const EVIDENCE_PATH = join(HERE, '..', '..', '..', '..', 'docs', 'evidence', 'tc-drawing-schedule-rev1.csv');

export const scheduleCsvText = readFileSync(EVIDENCE_PATH, 'utf8');
export const schedule = parseSchedule(scheduleCsvText);
export const counts = scheduleCounts(schedule);
export const nodes = nodeInventory(schedule);

export const rowsByClass = (rowClass) => schedule.rows.filter((r) => r.rowClass === rowClass);
export const rowForKey = (physicalChannelKey) => schedule.rows.find((r) => r.physicalChannelKey === physicalChannelKey);
export const rowForMeasurement = (measurementId) => schedule.rows.find((r) => r.measurementId === measurementId);

export const FIRST_PRODUCT_COMPATIBLE = schedule.rows.find((r) => r.rowClass === 'PRODUCT_COMPATIBLE' && r.drawingVerified);
export const FIRST_INTERNAL_CONFLICT = schedule.rows.find((r) => r.rowClass === 'DRAWING_INTERNAL_CONFLICT');
export const FIRST_TOPOLOGY_CONFLICT = schedule.rows.find((r) => r.rowClass === 'PRODUCT_TOPOLOGY_CONFLICT');
export const FIRST_UNASSIGNED = schedule.rows.find((r) => r.rowClass === 'UNASSIGNED');
