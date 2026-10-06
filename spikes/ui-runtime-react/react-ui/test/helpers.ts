// WJSS Stage 0.2.1A — test helpers: synthetic records, snapshots, deltas, fake EventSource.
import type { ActiveCleaningJobState, CriticalPumpEvent, DispatchRecord, OperationalDelta, OperationalSnapshot, QueueSummary, SensorPresentationState, SequenceState, Wall } from '../../contracts/operational';
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
    queue: makeQueue([]),
    sequence: makeSequence(),
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

/** Bounded synthetic GlobalQueue slice (ready-to-dispatch entries only; no per-entry status). */
export function makeQueue(
  entries: { sensorId: string; sourceReason?: string; dirtyScore?: number | null; secondsSinceLastClean?: number }[],
  over: Partial<QueueSummary> = {},
): QueueSummary {
  return {
    synthetic: true,
    label: 'GlobalQueue · synthetic · FIFO · not Production scheduling',
    capacity: 8,
    totalQueued: entries.length,
    revision: entries.length,
    entries: entries.map((e, i) => ({
      position: i + 1,
      entryId: `SYN-QE-${String(i + 1).padStart(5, '0')}`,
      sensorId: e.sensorId,
      sourceReason: e.sourceReason ?? 'SYN_DIRTY_SCORE_ABOVE_THRESHOLD',
      dirtyScore: e.dirtyScore ?? 70,
      secondsSinceLastClean: e.secondsSinceLastClean ?? 600,
    })),
    autoSequence: 'OFF',
    lastDispatch: null,
    eligibilityDiagnostics: [],
    ...over,
  };
}

/** Synthetic dispatch record linking a Job to the queue head it was created from. */
export function makeDispatch(sensorId: string, jobId: string, queueRevisionBefore = 10): DispatchRecord {
  return {
    dispatchId: `SYN-DSP-${jobId.slice(-4)}`,
    synthetic: true,
    queueRevisionBefore,
    queueRevisionAfter: queueRevisionBefore + 1,
    queueEntryId: 'SYN-QE-00001',
    positionBefore: 1,
    sensorId,
    sourceReason: 'SYN_DIRTY_SCORE_ABOVE_THRESHOLD',
    jobId,
    origin: 'SYN_AUTO_SEQUENCE',
    dispatchedAt: '2026-01-01T00:00:00.000Z',
  };
}

/** AutoSequence / critical / last-outcome slice (synthetic). */
export function makeSequence(over: Partial<SequenceState> = {}): SequenceState {
  return {
    synthetic: true,
    autoSequence: 'OFF',
    critical: null,
    safeReturnConfig: { valveFeedbackDelayMs: 0, standbyFeedbackDelayMs: 0, valveFeedback: 'NORMAL', standbyFeedback: 'NORMAL' },
    lastJobOutcome: null,
    ...over,
  };
}

/** Synthetic critical Pump event (modal open by default). */
export function makeCritical(over: Partial<CriticalPumpEvent> = {}): CriticalPumpEvent {
  return {
    eventId: 'SYN-CRIT-0001',
    kind: 'MAIN_PUMP_TRIP',
    severity: 'HIGH',
    raisedAt: '2026-01-01T00:00:10.000Z',
    evidenceSeq: 3,
    conditionActive: true,
    clearedAt: null,
    acknowledged: false,
    acknowledgedAt: null,
    alarmId: 'SYN-ALM-0001',
    jobId: 'SYN-JOB-0001',
    targetSensorId: 'G+203',
    phaseAtEvent: 'P4',
    safeReturnRequired: true,
    safeReturnComplete: false,
    safeReturnFailed: false,
    modalOpen: true,
    modalClosedAt: null,
    ...over,
  };
}

/** Running Active Job (no Safe Return yet). */
export function makeJob(over: Partial<ActiveCleaningJobState> = {}): ActiveCleaningJobState {
  return {
    jobId: 'SYN-JOB-0001',
    targetSensorId: 'G+203',
    jetId: 'SYN-JET-3',
    valveId: 'SYN-VLV-3',
    phase: 'P1',
    phaseLabel: 'Pre-check (synthetic)',
    phaseIndex: 0,
    startedAt: '2026-01-01T00:00:00Z',
    phaseStartedAt: '2026-01-01T00:00:00Z',
    phaseProgress: 0,
    lifecycle: 'RUNNING',
    cleaningPhase: 'IN_PROGRESS',
    dispatch: makeDispatch(over.targetSensorId ?? 'G+203', over.jobId ?? 'SYN-JOB-0001'),
    safeReturn: null,
    ...over,
  };
}
