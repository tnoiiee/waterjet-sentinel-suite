// WJSS Stage 0.2.1A — test helpers: synthetic records, snapshots, deltas, fake EventSource.
import type { OperationalDelta, OperationalSnapshot, SensorPresentationState, Wall } from '../../contracts/operational';
import type { EventSourceLike, MessageEventLike } from '../src/store/feed';
// Tests take Sensor identity and positions from the canonical mapping source (never generated here).
import { EXPECTED, getSensorMap, sensorById, wallMapSlots } from '../../contracts/sensorMap.mjs';

const WALLS: Array<[Wall, number]> = (['LEFT', 'REAR', 'RIGHT', 'FRONT'] as Wall[]).map((w) => [w, EXPECTED.perWall[w]]);

/** Canonical identity/position fields for a Sensor ID (falls back to G+201 for unknown IDs). */
function canonical(sensorId: string) {
  const m = sensorById(sensorId) ?? sensorById('G+201')!;
  return {
    slotType: 'SENSOR' as const,
    wall: m.wall,
    logicalColumn: m.logicalColumn,
    logicalRow: m.logicalRow,
    wallColumn: m.wallColumn,
    wallRow: m.wallRow,
    scanOrder: m.scanOrder,
    deviceId: m.deviceId,
    tcFrontChannel: m.tcFrontChannel,
    tcRearChannel: m.tcRearChannel,
  };
}

export function makeSensor(over: Partial<SensorPresentationState> & { sensorId: string }): SensorPresentationState {
  return {
    ...canonical(over.sensorId),
    dirtyScore: 30,
    lastValidatedScore: 30,
    lastValidatedAt: '2026-01-01T00:00:00.000Z',
    classification: 'CLEANER',
    classificationBasis: 'CURRENT',
    quality: 'GOOD',
    qualityReason: null,
    sourceTimestamp: '2026-01-01T00:00:00.000Z',
    queueState: 'NONE',
    isActiveJobTarget: false,
    alarmState: 'NONE',
    alarmSeverity: null,
    ...over,
  };
}

export function makeSensors(): SensorPresentationState[] {
  return getSensorMap().sensors.map((m) => makeSensor({ sensorId: m.sensorId }));
}

export function makeSnapshot(revision = 10, over: Partial<OperationalSnapshot> = {}): OperationalSnapshot {
  return {
    kind: 'snapshot',
    schema: 'wjss.spike.snapshot/1',
    synthetic: true,
    revision,
    generatedAt: new Date().toISOString(),
    config: { revision: 1, publishedAt: '2026-01-01T00:00:00.000Z', dirtyThreshold: 50, staleThresholdMs: 5000, label: 'SYNTHETIC SPIKE PARAMETER - NOT A PRODUCTION VALUE' },
    wallMap: wallMapSlots(),
    sensors: makeSensors(),
    walls: WALLS.map(([wall, total]) => ({ wall, total, dirty: 0, cleaner: total, notClassified: 0, uncertain: 0, maxScore: 30 })),
    activeJob: null,
    pump: { state: 'RUNNING', pressure: 100, setpoint: 100, readyBandLow: 90, readyBandHigh: 110, ready: true, stopRequestedAt: null },
    queue: { totalQueued: 0, entries: [] },
    alarms: { activeUnack: 0, activeAck: 0, clearedUnack: 0, items: [] },
    communication: { devices: [] },
    runtime: { uptimeS: 1, rssMb: 50, heapUsedMb: 10, eventLoopP99Ms: 1, sseClients: 1, historian: { depth: 0, capacity: 50000, nearOverflow: false, rejected: 0, lastBatchLatencyMs: null, delayMs: 20, gapMarkers: 0 }, invariantViolations: 0, acceptedSecondJobs: 0, refusedSecondJobs: 0 },
    trend: { capacity: 600, seriesNames: ['A', 'B', 'C', 'D'], points: [] },
    ...over,
  };
}

export function makeDelta(previousRevision: number, over: Partial<OperationalDelta> = {}): OperationalDelta {
  return { kind: 'delta', schema: 'wjss.spike.delta/1', synthetic: true, previousRevision, revision: previousRevision + 1, generatedAt: new Date().toISOString(), ...over };
}

/** In-memory EventSource double driven by the test. */
export class FakeEventSource implements EventSourceLike {
  static instances: FakeEventSource[] = [];
  readyState = 0;
  onopen: ((ev: unknown) => void) | null = null;
  onerror: ((ev: unknown) => void) | null = null;
  closed = false;
  private listeners: Record<string, Array<(ev: MessageEventLike) => void>> = {};
  constructor(readonly url: string) {
    FakeEventSource.instances.push(this);
  }
  addEventListener(type: 'snapshot' | 'delta', fn: (ev: MessageEventLike) => void) {
    (this.listeners[type] ??= []).push(fn);
  }
  close() {
    this.closed = true;
    this.readyState = 2;
  }
  emit(type: 'snapshot' | 'delta', payload: unknown) {
    this.readyState = 1;
    const data = JSON.stringify(payload);
    for (const fn of this.listeners[type] ?? []) fn({ data });
  }
  fail(readyState = 0) {
    this.readyState = readyState;
    this.onerror?.({});
  }
}
