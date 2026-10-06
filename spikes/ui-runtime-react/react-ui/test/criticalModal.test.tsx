// WJSS Stage 0.2.1A — critical Main Pump modal (jsdom; structure / behaviour only, no layout).
// Owner critical Pump decision: alertdialog, aria-modal, labelled; no close (X); Escape never
// dismisses; Acknowledge is the only action and is not a clear; the page behind is inert; the
// modal is runtime-authoritative (open exactly when sequence.critical.modalOpen). Geometry,
// centring, viewport fit and real focus behaviour are checked Owner-local in Edge (e2e).
import { act, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';
import { SyntheticTestControl } from '../src/components/SyntheticTestControl';
import { StoreContext } from '../src/store/hooks';
import { PresentationStore } from '../src/store/presentationStore';
import type { SafeReturnState } from '../../contracts/operational';
import { makeCritical, makeJob, makeSequence, makeSnapshot } from './helpers';

function mount(over: Parameters<typeof makeSnapshot>[1] = {}) {
  const store = new PresentationStore();
  store.applySnapshot(makeSnapshot(1, over));
  const r = render(
    <StoreContext.Provider value={store}>
      <App showTrend={false} />
    </StoreContext.Provider>,
  );
  return { ...r, store };
}

function sr(over: Partial<SafeReturnState> = {}): SafeReturnState {
  return {
    synthetic: true,
    step: 'SR3',
    trigger: 'SYN_PUMP_TRIP',
    pendingOutcome: 'ABORTED',
    phaseAtTrigger: 'P4',
    startedAt: '2026-01-01T00:00:10.000Z',
    valve: { valveId: 'SYN-VLV-3', command: 'CLOSE_COMMANDED', commandSeq: 4, feedback: 'NOT_CONFIRMED', feedbackSeq: null },
    axis: { command: 'NOT_COMMANDED', commandSeq: null, standby: 'NOT_CONFIRMED', standbySeq: null },
    failure: null,
    events: [
      { seq: 2, step: null, event: 'TRIGGER_ACCEPTED SYN_PUMP_TRIP', at: '2026-01-01T00:00:10.000Z' },
      { seq: 3, step: 'SR1', event: 'NORMAL_CLEANING_AND_WATER_COMMAND_STOPPED', at: '2026-01-01T00:00:10.000Z' },
      { seq: 4, step: 'SR2', event: 'ISOLATION_VALVE_CLOSE_COMMANDED', at: '2026-01-01T00:00:11.500Z' },
    ],
    ...over,
  };
}
const srJob = (over: Partial<SafeReturnState> = {}) =>
  makeJob({ phase: 'P4', phaseIndex: 3, lifecycle: 'SAFE_RETURN_VERIFY_VALVE_CLOSED', cleaningPhase: 'CLEANING_STOPPED', phaseLabel: 'Mandatory Safe Return — confirming Isolation Valve closed', safeReturn: sr(over) });

afterEach(() => vi.restoreAllMocks());

describe('critical Main Pump modal', () => {
  it('is absent without a critical event and the page is not inert', () => {
    const { queryByTestId, getByTestId } = mount();
    expect(queryByTestId('critical-modal')).toBeNull();
    expect(getByTestId('operations-scope').hasAttribute('inert')).toBe(false);
  });

  it('alertdialog, aria-modal, labelled title + description; required content; no X / close button', () => {
    const { getByTestId, getByRole } = mount({ activeJob: srJob(), sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical() }) });
    const d = getByRole('alertdialog');
    expect(d).toBe(getByTestId('critical-modal'));
    expect(d.getAttribute('aria-modal')).toBe('true');
    expect(document.getElementById(d.getAttribute('aria-labelledby')!)!.textContent).toBe('CRITICAL ALARM — MAIN PUMP TRIPPED');
    for (const id of d.getAttribute('aria-describedby')!.split(' ')) expect(document.getElementById(id)).not.toBeNull();
    expect(getByTestId('critical-condition').textContent).toBe('ACTIVE');
    expect(getByTestId('critical-ack-state').textContent).toBe('NOT ACKNOWLEDGED');
    expect(getByTestId('critical-autosequence').textContent).toContain('CRITICAL SUSPENDED');
    expect(getByTestId('critical-job').textContent).toBe('SYN-JOB-0001 · target G+203 · phase P4');
    expect(getByTestId('critical-sr').textContent).toContain('IN PROGRESS — SR3');
    expect(getByTestId('critical-valve').textContent).toBe('SYN-VLV-3 · CLOSE COMMANDED · NOT CONFIRMED');
    expect(getByTestId('critical-axis').textContent).toBe('NOT COMMANDED · NOT CONFIRMED');
    const resp = getByTestId('critical-response').textContent!;
    for (const t of ['Cleaning stopped.', 'AutoSequence suspended.', 'Inspect the Main Pump and the process.', 'Do not resume until the cause is cleared and resumption is authorised.']) expect(resp).toContain(t);
    // Only one button: Acknowledge. No close / X / Resume.
    const buttons = d.querySelectorAll('button');
    expect(buttons).toHaveLength(1);
    expect(buttons[0].textContent).toBe('Acknowledge');
    expect(d.textContent).not.toMatch(/\bResume\b(?! until)|×|✖/);
    expect(d.querySelector('[aria-label*="close" i]')).toBeNull();
    // Steps: SR1/SR2 done, SR3 current; states are text, not colour-only.
    const steps = [...getByTestId('critical-sr-steps').querySelectorAll('li')].map((li) => li.getAttribute('data-step-state'));
    expect(steps).toEqual(['done', 'done', 'current', 'todo', 'todo']);
    expect(getByTestId('critical-sr-steps').textContent).toContain('in progress');
    // Polite live announcement, no assertive interruption beyond the alertdialog role.
    expect(getByTestId('critical-live').getAttribute('aria-live')).toBe('polite');
  });

  it('page behind is inert while open; Diagnostics drawer stays outside the inert scope', () => {
    const { getByTestId, container } = mount({ sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ jobId: null, targetSensorId: null, phaseAtEvent: null, safeReturnRequired: false }) }) });
    const scope = getByTestId('operations-scope');
    expect(scope.hasAttribute('inert')).toBe(true);
    expect(scope.contains(getByTestId('critical-modal'))).toBe(false);
    expect(scope.contains(getByTestId('queue-preview'))).toBe(true);
    fireEvent.keyDown(window, { key: 'd' });
    const diag = container.querySelector('[data-testid="diagnostics"]');
    if (diag) expect(scope.contains(diag)).toBe(false);
  });

  it('Escape does not dismiss; Tab stays inside; focus starts on Acknowledge', () => {
    const { getByTestId } = mount({ sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ jobId: null, safeReturnRequired: false }) }) });
    const d = getByTestId('critical-modal');
    const ack = getByTestId('critical-ack');
    expect(document.activeElement).toBe(ack);
    fireEvent.keyDown(d, { key: 'Escape' });
    expect(getByTestId('critical-modal')).toBeTruthy();
    fireEvent.keyDown(d, { key: 'Tab' });
    expect(document.activeElement).toBe(ack);
    fireEvent.keyDown(d, { key: 'Tab', shiftKey: true });
    expect(document.activeElement).toBe(ack);
    // Focus moved outside (e.g. programmatically) is pulled back into the dialog.
    const outside = document.createElement('button');
    document.body.appendChild(outside);
    outside.focus();
    expect(d.contains(document.activeElement)).toBe(true);
    outside.remove();
  });

  it('Acknowledge sends exactly one request (no retry) and does not close the modal by itself', async () => {
    const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(JSON.stringify({ accepted: true }), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);
    const { getByTestId, store } = mount({ sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ jobId: null, safeReturnRequired: false }) }) });
    await act(async () => {
      fireEvent.click(getByTestId('critical-ack'));
    });
    const ackCalls = fetchMock.mock.calls.filter((c) => String(c[0]) === '/api/spike/critical-alarm-ack');
    expect(ackCalls).toHaveLength(1);
    expect(getByTestId('critical-modal')).toBeTruthy();
    // Runtime reports acknowledged but condition still active: still open, button disabled.
    act(() => store.applySnapshot(makeSnapshot(2, { sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ jobId: null, safeReturnRequired: false, acknowledged: true, acknowledgedAt: '2026-01-01T00:00:20.000Z' }) }) })));
    expect(getByTestId('critical-modal').getAttribute('data-acknowledged')).toBe('true');
    expect(getByTestId('critical-ack-state').textContent).toMatch(/^ACKNOWLEDGED at /);
    expect((getByTestId('critical-ack') as HTMLButtonElement).disabled).toBe(true);
    expect(getByTestId('critical-ack').textContent).toBe('Acknowledged');
    expect(document.activeElement).toBe(getByTestId('critical-modal'));
    vi.unstubAllGlobals();
  });

  it('cleared before Safe Return complete stays open; runtime close -> modal gone, page interactive, no Resume control', () => {
    const { getByTestId, queryByTestId, store, container } = mount({
      activeJob: srJob(),
      sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ conditionActive: false, clearedAt: '2026-01-01T00:00:12.000Z', acknowledged: true, acknowledgedAt: '2026-01-01T00:00:11.000Z' }) }),
    });
    expect(getByTestId('critical-condition').textContent).toMatch(/^CLEARED at /);
    expect(getByTestId('critical-modal').getAttribute('data-safe-return')).toBe('SR3');
    act(() =>
      store.applySnapshot(
        makeSnapshot(3, {
          activeJob: null,
          sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ conditionActive: false, acknowledged: true, safeReturnComplete: true, modalOpen: false, modalClosedAt: '2026-01-01T00:00:30.000Z' }) }),
        }),
      ),
    );
    expect(queryByTestId('critical-modal')).toBeNull();
    expect(getByTestId('operations-scope').hasAttribute('inert')).toBe(false);
    expect([...container.querySelectorAll('button')].some((b) => /resume/i.test(b.textContent ?? ''))).toBe(false);
  });

  it('Safe Return failure: FAILED shown as text (not colour only), Active Job retained, modal stays', () => {
    const failed = srJob({ step: 'SR_FAILED', valve: { valveId: 'SYN-VLV-3', command: 'CLOSE_COMMANDED', commandSeq: 4, feedback: 'ABSENT', feedbackSeq: null }, failure: { reason: 'ISOLATION_VALVE_CLOSED_FEEDBACK_ABSENT', atLifecycle: 'SAFE_RETURN_VERIFY_VALVE_CLOSED', at: '2026-01-01T00:00:31.000Z', seq: 5 } });
    failed.lifecycle = 'SAFE_RETURN_FAILED';
    const { getByTestId } = mount({ activeJob: failed, sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ conditionActive: false, acknowledged: true, safeReturnFailed: true }) }) });
    expect(getByTestId('critical-sr').textContent).toContain('FAILED — ISOLATION_VALVE_CLOSED_FEEDBACK_ABSENT · Active Job retained · OWNER DECISION REQUIRED');
    expect(getByTestId('critical-valve').textContent).toContain('ABSENT');
    expect(getByTestId('critical-sr-steps').textContent).toContain('failed');
    expect(getByTestId('job-safe-return').textContent).toContain('SR FAILED');
  });

  it('disconnected: last state shown, Acknowledge disabled (nothing queued)', () => {
    const { getByTestId, store } = mount({ sequence: makeSequence({ autoSequence: 'CRITICAL_SUSPENDED', critical: makeCritical({ jobId: null, safeReturnRequired: false }) }) });
    act(() => store.setConnection({ state: 'DISCONNECTED' }));
    expect((getByTestId('critical-ack') as HTMLButtonElement).disabled).toBe(true);
    expect(getByTestId('critical-modal').textContent).toContain('Connection lost');
  });

  it('Active Job panel shows the Safe Return step, valve and axis state from runtime evidence', () => {
    const { getByTestId } = mount({ activeJob: srJob() });
    const s = getByTestId('job-safe-return');
    expect(s.getAttribute('data-sr-step')).toBe('SR3');
    expect(s.textContent).toContain('Valve close commanded · not confirmed');
    expect(s.textContent).toContain('Axis not commanded · not confirmed');
    expect(getByTestId('job-status').getAttribute('data-lifecycle')).toBe('SAFE_RETURN_VERIFY_VALVE_CLOSED');
  });

  it('Critical Pump / Safe Return synthetic controls: 10 Owner controls + valve-absent; disabled when controls are off', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ error: 'NOT_FOUND' }), { status: 404 })));
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1));
    const r = render(
      <StoreContext.Provider value={store}>
        <SyntheticTestControl selectedId={null} />
      </StoreContext.Provider>,
    );
    await act(async () => {});
    const group = r.getByTestId('stc-critical-group');
    const ids = [...group.querySelectorAll('button')].map((b) => b.getAttribute('data-testid'));
    expect(ids).toEqual([
      'stc-crit-stop-no-job',
      'stc-crit-trip-no-job',
      'stc-crit-trip-p1',
      'stc-crit-trip-p4',
      'stc-crit-normal-sr',
      'stc-crit-valve-delay',
      'stc-crit-standby-delay',
      'stc-crit-clear',
      'stc-crit-ack',
      'stc-crit-reset',
      'stc-crit-valve-absent',
    ]);
    for (const b of group.querySelectorAll('button')) expect((b as HTMLButtonElement).disabled).toBe(true);
    expect(group.textContent).toContain('not a Resume');
    vi.unstubAllGlobals();
  });
});
