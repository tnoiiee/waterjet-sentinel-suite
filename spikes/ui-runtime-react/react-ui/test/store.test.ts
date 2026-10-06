// WJSS Stage 0.2.1A — presentation store: revision apply, gap, snapshot replacement,
// per-Sensor notification, bounded trend.
import { describe, expect, it, vi } from 'vitest';
import { PresentationStore } from '../src/store/presentationStore';
import { makeDelta, makeDispatch, makeSensor, makeSnapshot, makeQueue } from './helpers';

describe('PresentationStore', () => {
  it('applies a chained Delta and advances the revision', () => {
    const s = new PresentationStore();
    s.applySnapshot(makeSnapshot(10));
    const r = s.applyDelta(makeDelta(10, { sensors: [makeSensor({ sensorId: 'G+201', dirtyScore: 77, classification: 'DIRTY', lastValidatedScore: 77 })] }));
    expect(r).toBe('applied');
    expect(s.revision).toBe(11);
    expect(s.getSensor('G+201')?.classification).toBe('DIRTY');
  });

  it('detects a revision gap and applies nothing', () => {
    const s = new PresentationStore();
    s.applySnapshot(makeSnapshot(10));
    const before = s.getSensor('G+201');
    const r = s.applyDelta(makeDelta(12, { sensors: [makeSensor({ sensorId: 'G+201', dirtyScore: 99 })] }));
    expect(r).toBe('gap');
    expect(s.revision).toBe(10);
    expect(s.getSensor('G+201')).toBe(before);
    expect(s.stats.gapsDetected).toBe(1);
  });

  it('treats a Delta before any Snapshot as a gap', () => {
    const s = new PresentationStore();
    expect(s.applyDelta(makeDelta(0))).toBe('gap');
  });

  it('ignores duplicate or old revisions', () => {
    const s = new PresentationStore();
    s.applySnapshot(makeSnapshot(10));
    expect(s.applyDelta(makeDelta(9))).toBe('duplicate');
    expect(s.revision).toBe(10);
  });

  it('Snapshot replaces all state (no merge with stale cache)', () => {
    const s = new PresentationStore();
    s.applySnapshot(makeSnapshot(10, { activeJob: { jobId: 'SYN-JOB-0001', targetSensorId: 'G+203', jetId: 'SYN-JET-3', valveId: 'SYN-VLV-3', phase: 'P2', phaseLabel: 'x', phaseIndex: 1, startedAt: '2026-01-01T00:00:00Z', phaseStartedAt: '2026-01-01T00:00:04Z', phaseProgress: 0.5, lifecycle: 'RUNNING', cleaningPhase: 'IN_PROGRESS', safeReturn: null, dispatch: makeDispatch('G+203', 'SYN-JOB-0001') } }));
    s.applyDelta(makeDelta(10, { sensors: [makeSensor({ sensorId: 'G+202', alarmState: 'ACTIVE_UNACK' })] }));
    s.applySnapshot(makeSnapshot(50));
    expect(s.revision).toBe(50);
    expect(s.getSlice('activeJob')).toBeNull();
    expect(s.getSensor('G+202')?.alarmState).toBe('NONE');
    expect(s.sensorCount).toBe(106);
  });

  it('notifies only the subscriptions of changed Sensors', () => {
    const s = new PresentationStore();
    s.applySnapshot(makeSnapshot(1));
    const spies = new Map<string, ReturnType<typeof vi.fn>>();
    for (const id of ['G+201', 'G+202', 'H8', 'J18']) {
      const fn = vi.fn();
      spies.set(id, fn);
      s.subscribeSensor(id, fn);
    }
    s.applyDelta(makeDelta(1, { sensors: [makeSensor({ sensorId: 'H8', wall: 'REAR', dirtyScore: 60 })] }));
    expect(spies.get('H8')).toHaveBeenCalledTimes(1);
    expect(spies.get('G+201')).not.toHaveBeenCalled();
    expect(spies.get('G+202')).not.toHaveBeenCalled();
    expect(spies.get('J18')).not.toHaveBeenCalled();
    expect(s.stats.lastDeltaSensorNotifications).toBe(1);
  });

  it('notifies a panel slice only when it is present in the Delta', () => {
    const s = new PresentationStore();
    s.applySnapshot(makeSnapshot(1));
    const pump = vi.fn();
    const queue = vi.fn();
    s.subscribe('pump', pump);
    s.subscribe('queue', queue);
    s.applyDelta(makeDelta(1, { queue: makeQueue([], { revision: 1 }) }));
    expect(queue).toHaveBeenCalledTimes(1);
    expect(pump).not.toHaveBeenCalled();
  });

  it('applies activeJob null as a clear, and absent as unchanged', () => {
    const s = new PresentationStore();
    const job = { jobId: 'SYN-JOB-0001', targetSensorId: 'G+203', jetId: 'SYN-JET-3', valveId: 'SYN-VLV-3', phase: 'P1' as const, phaseLabel: 'x', phaseIndex: 0, startedAt: '2026-01-01T00:00:00Z', phaseStartedAt: '2026-01-01T00:00:00Z', phaseProgress: 0, lifecycle: 'RUNNING' as const, cleaningPhase: 'IN_PROGRESS' as const, safeReturn: null, dispatch: makeDispatch('G+203', 'SYN-JOB-0001') };
    s.applySnapshot(makeSnapshot(1, { activeJob: job }));
    s.applyDelta(makeDelta(1));
    expect(s.getSlice('activeJob')).toEqual(job);
    s.applyDelta(makeDelta(2, { activeJob: null }));
    expect(s.getSlice('activeJob')).toBeNull();
  });

  it('keeps the trend bounded by its capacity', () => {
    const s = new PresentationStore();
    s.applySnapshot(makeSnapshot(0, { trend: { capacity: 5, seriesNames: ['A', 'B', 'C', 'D'], points: [] } }));
    for (let i = 0; i < 50; i += 1) s.applyDelta(makeDelta(i, { trendPoint: { t: i, series: [1, 2, 3, 4], setpoint: 100, jobActive: false, alarmActive: false } }));
    expect(s.trendLength).toBe(5);
    expect(s.getSlice('trend').points.map((p) => p.t)).toEqual([45, 46, 47, 48, 49]);
  });
});
