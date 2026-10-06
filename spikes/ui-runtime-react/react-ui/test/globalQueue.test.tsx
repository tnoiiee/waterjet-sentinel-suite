// WJSS Stage 0.2.1A — Gate E (jsdom): the bounded synthetic GlobalQueue is explainable in the UI.
// Owner domain correction: entries are ready-to-dispatch only (no status chips / column), count
// 0..8 of 8, labelled synthetic and not Production scheduling; dispatch evidence and Queue
// Eligibility Diagnostics live in Diagnostics only. jsdom performs no layout (structure only).
import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from '../src/App';
import { QueueDispatchDiagnostics } from '../src/components/DiagnosticsOverlay';
import { StoreContext } from '../src/store/hooks';
import { PresentationStore } from '../src/store/presentationStore';
import type { ActiveCleaningJobState, DispatchRecord } from '../../contracts/operational';
import { makeQueue, makeSensor, makeSnapshot } from './helpers';

const PROHIBITED = /\b(READY|BLOCKED|HELD|HELD_BY_OPERATOR|WAITING_FOR_PUMP|WAITING_FOR_EQUIPMENT|EXCLUDED|INVALID|OUT_OF_SERVICE)\b/;

function mount(node: React.ReactNode, over: Parameters<typeof makeSnapshot>[1] = {}) {
  const store = new PresentationStore();
  store.applySnapshot(makeSnapshot(1, over));
  return render(<StoreContext.Provider value={store}>{node}</StoreContext.Provider>);
}

const dispatch: DispatchRecord = {
  dispatchId: 'SYN-DSP-0007',
  synthetic: true,
  queueRevisionBefore: 41,
  queueRevisionAfter: 42,
  queueEntryId: 'SYN-QE-00031',
  positionBefore: 1,
  sensorId: 'G+110',
  sourceReason: 'SYN_DIRTY_SCORE_ABOVE_THRESHOLD',
  jobId: 'SYN-JOB-0007',
  origin: 'SYN_AUTO_SEQUENCE',
  dispatchedAt: '2026-10-06T00:00:00.000Z',
};
const job: ActiveCleaningJobState = {
  jobId: 'SYN-JOB-0007',
  targetSensorId: 'G+110',
  jetId: 'SYN-JET-1',
  valveId: 'SYN-VLV-1',
  phase: 'P2',
  phaseLabel: 'Isolation valve open (synthetic)',
  phaseIndex: 1,
  startedAt: '2026-10-06T00:00:00.000Z',
  phaseStartedAt: '2026-10-06T00:00:05.000Z',
  phaseProgress: 0.4,
  preCheck: 'PASSED',
  dispatch,
};

describe('Gate E — GlobalQueue panel (synthetic, bounded, no entry status)', () => {
  it('heading and count read "GlobalQueue · synthetic" and "N / 8 queued · FIFO · not Production scheduling"', () => {
    const ids = ['G9', 'G8', 'I12', 'H3', 'H4', 'J2'];
    const { getByTestId } = mount(<App showTrend={false} />, { queue: makeQueue(ids.map((sensorId) => ({ sensorId }))) });
    expect(getByTestId('queue-title').textContent).toBe('GlobalQueue · synthetic');
    expect(getByTestId('queue-count').textContent).toBe('6 / 8 queued · FIFO · not Production scheduling');
    const q = getByTestId('queue-preview');
    expect(q.getAttribute('data-queue-count')).toBe('6');
    expect(q.getAttribute('data-queue-capacity')).toBe('8');
    expect([...q.querySelectorAll('tbody tr')].map((r) => r.children[1].textContent)).toEqual(ids);
  });

  it('no status column, no status chips, no prohibited entry-state words; positions 1..n', () => {
    const ids = ['G9', 'G8', 'I12', 'H3', 'H4', 'J2', 'J3', 'J4'];
    const { getByTestId } = mount(<App showTrend={false} />, { queue: makeQueue(ids.map((sensorId) => ({ sensorId }))) });
    const q = getByTestId('queue-preview');
    expect([...q.querySelectorAll('th')].map((h) => h.textContent)).not.toContain('Status');
    expect(q.querySelectorAll('[data-status]')).toHaveLength(0);
    expect(PROHIBITED.test(q.textContent ?? '')).toBe(false);
    expect([...q.querySelectorAll('tbody tr')].map((r) => r.getAttribute('data-queue-position'))).toEqual(['1', '2', '3', '4', '5', '6', '7', '8']);
    expect(getByTestId('queue-count').textContent).toMatch(/^8 \/ 8 queued/);
  });

  it('empty queue reads 0 / 8', () => {
    const { getByTestId } = mount(<App showTrend={false} />);
    expect(getByTestId('queue-count').textContent).toBe('0 / 8 queued · FIFO · not Production scheduling');
    expect(getByTestId('queue-preview').querySelectorAll('tbody tr')).toHaveLength(0);
  });

  it('cell queue badge: Q for a queued Sensor (no R/H/B/X status codes), J for the Active Job target', () => {
    const sensors = makeSnapshot(1).sensors.map((s) =>
      s.sensorId === 'H3' ? makeSensor({ sensorId: 'H3', queueState: 'QUEUED' }) : s.sensorId === 'G+110' ? makeSensor({ sensorId: 'G+110', queueState: 'ACTIVE', isActiveJobTarget: true }) : s,
    );
    const { container } = mount(<App showTrend={false} />, { sensors, activeJob: job, queue: makeQueue([{ sensorId: 'H3' }]) });
    const badge = (id: string) => container.querySelector(`[data-sensor-id="${id}"] [data-part="queue-badge"]`);
    expect(badge('H3')!.textContent).toBe('Q');
    expect(badge('H3')!.getAttribute('title')).toMatch(/ready to dispatch/);
    expect(badge('G+110')!.textContent).toBe('J');
    const codes = [...container.querySelectorAll('[data-part="queue-badge"]')].map((b) => b.textContent);
    expect(codes.some((c) => ['R', 'H', 'B', 'X'].includes(c ?? ''))).toBe(false);
  });
});

describe('Gate E — Diagnostics: dispatch evidence and Queue Eligibility Diagnostics', () => {
  it('shows "Position 1 · Sensor · Queue revision · Dispatch ID" for the Active Job', () => {
    const { getByTestId } = mount(<QueueDispatchDiagnostics />, { activeJob: job, queue: makeQueue([{ sensorId: 'G9' }], { autoSequence: 'JOB_ACTIVE', lastDispatch: dispatch }) });
    expect(getByTestId('diag-dispatch-summary').textContent).toBe('Position 1 · G+110 · Queue revision 41→42 · SYN-DSP-0007');
    expect(getByTestId('diag-auto-sequence').textContent).toBe('JOB_ACTIVE');
  });

  it('lists NOT ADMITTED Sensors only when a scenario sets them; never inside the queue', () => {
    const none = mount(<QueueDispatchDiagnostics />);
    expect(none.queryByTestId('diag-eligibility')).toBeNull();
    none.unmount();
    const reason = 'Reason pending Owner-approved eligibility policy (synthetic demonstration)';
    const { getByTestId } = mount(<QueueDispatchDiagnostics />, {
      queue: makeQueue([{ sensorId: 'G9' }], { eligibilityDiagnostics: [{ sensorId: 'G+110', decision: 'NOT_ADMITTED', reason, synthetic: true }] }),
    });
    expect(getByTestId('diag-eligibility').textContent).toBe(`G+110 · NOT ADMITTED · ${reason}`);
  });
});
