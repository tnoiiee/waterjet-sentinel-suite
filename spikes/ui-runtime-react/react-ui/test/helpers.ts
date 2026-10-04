// WJSS Stage 0.2.1A — test helpers: synthetic records, snapshots, deltas, fake EventSource.
import type { OperationalDelta, OperationalSnapshot, SensorPresentationState, Wall } from '../../contracts/operational';
import type { EventSourceLike, MessageEventLike } from '../src/store/feed';

const WALLS: Array<[Wall, number]> = [
  ['LEFT', 24],
  ['REAR', 28],
  ['RIGHT', 24],
  ['FRONT', 28],
];

export function makeSensor(over: Partial<SensorPresentationState> & { sensorId: string }): SensorPresentationState {
  return {
    wall: 'LEFT',
    index: 1,
    deviceId: 'SYN-TC-01',
    tcChannels: ['SYN-TC-01:CH00', 'SYN-TC-01:CH01'],
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
  const out: SensorPresentationState[] = [];
  let g = 0;
  for (const [wall, n] of WALLS) {
    for (let i = 1; i <= n; i += 1) {
      const dev = `SYN-TC-${String(Math.floor((2 * g) / 26) + 1).padStart(2, '0')}`;
      out.push(makeSensor({ sensorId: `SYN-${wall}-${String(i).padStart(2, '0')}`, wall, index: i, deviceId: dev, tcChannels: [`${dev}:CH${String((2 * g) % 26).padStart(2, '0')}`, `${dev}:CH${String((2 * g + 1) % 26).padStart(2, '0')}`] }));
      g += 1;
    }
  }
  return out;
}

export function makeSnapshot(revision = 10, over: Partial<OperationalSnapshot> = {}): OperationalSnapshot {
  return {
    kind: 'snapshot',
    schema: 'wjss.spike.snapshot/1',
    synthetic: true,
    revision,
    generatedAt: new Date().toISOString(),
    config: { revision: 1, publishedAt: '2026-01-01T00:00:00.000Z', dirtyThreshold: 50, staleThresholdMs: 5000, label: 'SYNTHETIC SPIKE PARAMETER - NOT A PRODUCTION VALUE' },
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
