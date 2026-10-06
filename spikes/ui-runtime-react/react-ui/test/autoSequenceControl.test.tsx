// WJSS Stage 0.2.1A — Synthetic AutoSequence control (jsdom; structure / behaviour only).
// Owner final closeout: START AUTOSEQUENCE, PAUSE AFTER CURRENT JOB, RESUME AUTOSEQUENCE, ABORT
// ACTIVE JOB, RESET CRITICAL SCENARIO — only inside the opt-in Diagnostics drawer, every label
// Synthetic, availability and disabled reasons from the runtime (sequence.controls), one request
// per click, no target parameter (head-only dispatch), no retry, disabled when disconnected.
import { act, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';
import { SyntheticTestControl } from '../src/components/SyntheticTestControl';
import { StoreContext } from '../src/store/hooks';
import { PresentationStore } from '../src/store/presentationStore';
import { makeControls, makeCritical, makeJob, makeQueue, makeSequence, makeSnapshot } from './helpers';

const ON = { enabled: true, reason: null };
type FetchCall = { url: string; init?: RequestInit };

function stubControls(scenarioReply: () => Response = () => new Response(JSON.stringify({ accepted: true }), { status: 200 })) {
  const calls: FetchCall[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({ url, init });
      if (url === '/api/spike/test-controls') return new Response(JSON.stringify({ enabled: true, token: 'tok-test' }), { status: 200 });
      return scenarioReply();
    }),
  );
  return calls;
}

async function mountControl(over: Parameters<typeof makeSnapshot>[1] = {}, selectedId: string | null = null) {
  const store = new PresentationStore();
  store.applySnapshot(makeSnapshot(1, over));
  const r = render(
    <StoreContext.Provider value={store}>
      <SyntheticTestControl selectedId={selectedId} />
    </StoreContext.Provider>,
  );
  await act(async () => {});
  return { ...r, store };
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('Synthetic AutoSequence control', () => {
  it('lives only in the Diagnostics drawer, not on the Operations surface', async () => {
    stubControls();
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1));
    const r = render(
      <StoreContext.Provider value={store}>
        <App showTrend={false} />
      </StoreContext.Provider>,
    );
    await act(async () => {});
    expect(r.queryByTestId('asc-group')).toBeNull();
    fireEvent.keyDown(window, { key: 'd' });
    await act(async () => {});
    const drawer = r.getByTestId('diagnostics');
    expect(drawer.querySelector('[data-testid="asc-group"]')).not.toBeNull();
  });

  it('five Owner controls (+ synthetic Pump start), all labelled SYN; no Start-Job-for-Sensor control', async () => {
    stubControls();
    const r = await mountControl();
    const ids = [...r.getByTestId('asc-group').querySelectorAll('button')].map((b) => b.getAttribute('data-testid'));
    expect(ids).toEqual(['asc-start', 'asc-pause', 'asc-resume', 'asc-abort', 'asc-reset', 'asc-pump-start']);
    const labels = [...r.getByTestId('asc-group').querySelectorAll('button')].map((b) => b.textContent);
    expect(labels.slice(0, 5)).toEqual([
      'SYN · START AUTOSEQUENCE',
      'SYN · PAUSE AFTER CURRENT JOB',
      'SYN · RESUME AUTOSEQUENCE',
      'SYN · ABORT ACTIVE JOB',
      'SYN · RESET CRITICAL SCENARIO',
    ]);
    expect(r.container.textContent).not.toMatch(/PAUSE JOB(?! )/);
    expect([...r.container.querySelectorAll('button')].some((b) => /start job/i.test(b.textContent ?? ''))).toBe(false);
  });

  it('shows runtime state and a readable disabled reason for every control', async () => {
    stubControls();
    const r = await mountControl({
      queue: makeQueue([{ sensorId: 'G9' }, { sensorId: 'G8' }]),
      sequence: makeSequence({ autoSequence: 'OFF', mode: 'OFF', controls: makeControls({ start: ON }) }),
    });
    expect(r.getByTestId('asc-mode').textContent).toBe('OFF · OFF');
    expect(r.getByTestId('asc-head').textContent).toBe('G9');
    expect(r.getByTestId('asc-count').textContent).toBe('2 / 8');
    expect(r.getByTestId('asc-pump').textContent).toBe('RUNNING · READY');
    expect(r.getByTestId('asc-critical').textContent).toBe('None');
    expect((r.getByTestId('asc-start') as HTMLButtonElement).disabled).toBe(false);
    expect(r.getByTestId('asc-start-reason').textContent).toBe('Available');
    expect((r.getByTestId('asc-resume') as HTMLButtonElement).disabled).toBe(true);
    expect(r.getByTestId('asc-resume-reason').textContent).toBe('Only available from PAUSED');
    expect(r.getByTestId('asc-abort-reason').textContent).toBe('No Active Job');
    expect(r.getByTestId('asc-reset-reason').textContent).toBe('No critical scenario to reset');
    expect(r.getByTestId('asc-start').getAttribute('aria-describedby')).toBe('asc-start-reason');
  });

  it('one click sends exactly one scenario request, without any target Sensor, and never retries', async () => {
    const calls = stubControls(() => new Response(JSON.stringify({ accepted: false, reason: 'QUEUE_EMPTY' }), { status: 200 }));
    const r = await mountControl({ queue: makeQueue([{ sensorId: 'G9' }]), sequence: makeSequence({ controls: makeControls({ start: ON }) }) }, 'J12');
    await act(async () => {
      fireEvent.click(r.getByTestId('asc-start'));
    });
    const posts = calls.filter((c) => c.url === '/api/spike/scenario');
    expect(posts).toHaveLength(1);
    const body = JSON.parse(String(posts[0].init?.body));
    expect(body).toEqual({ command: 'autosequence-start', params: {} });
    expect(r.getByTestId('stc-result').textContent).toContain('refused (QUEUE_EMPTY)');
    await act(async () => {});
    expect(calls.filter((c) => c.url === '/api/spike/scenario')).toHaveLength(1);
  });

  it('CRITICAL_SUSPENDED: Start / Resume disabled with reasons; Reset offered only when the runtime allows it', async () => {
    stubControls();
    const crit = makeCritical({ conditionActive: false, acknowledged: true, modalOpen: false, safeReturnComplete: true });
    const r = await mountControl({
      sequence: makeSequence({
        autoSequence: 'CRITICAL_SUSPENDED',
        mode: 'CRITICAL_SUSPENDED',
        critical: crit,
        controls: makeControls({ start: { enabled: false, reason: 'CRITICAL_RESET_REQUIRED' }, resume: { enabled: false, reason: 'CRITICAL_SUSPENDED' }, resetCritical: ON }),
      }),
    });
    expect(r.getByTestId('asc-mode').textContent).toBe('CRITICAL_SUSPENDED · CRITICAL_SUSPENDED');
    expect(r.getByTestId('asc-critical').textContent).toBe('CLEARED · ACKNOWLEDGED');
    expect((r.getByTestId('asc-start') as HTMLButtonElement).disabled).toBe(true);
    expect(r.getByTestId('asc-start-reason').textContent).toContain('RESET CRITICAL SCENARIO');
    expect((r.getByTestId('asc-resume') as HTMLButtonElement).disabled).toBe(true);
    expect(r.getByTestId('asc-resume-reason').textContent).toContain('CRITICAL_SUSPENDED');
    expect((r.getByTestId('asc-reset') as HTMLButtonElement).disabled).toBe(false);
  });

  it('Active Job in Safe Return: Abort disabled with reason, Safe Return state shown', async () => {
    stubControls();
    const job = makeJob({ lifecycle: 'SAFE_RETURN_TO_STANDBY' });
    const r = await mountControl({
      activeJob: { ...job, safeReturn: { synthetic: true, step: 'SR4', trigger: 'SYN_OPERATOR_ABORT', pendingOutcome: 'ABORTED', phaseAtTrigger: 'P3', startedAt: '2026-01-01T00:00:00.000Z', valve: { valveId: 'V', command: 'CLOSE_COMMANDED', commandSeq: 2, feedback: 'CLOSED_CONFIRMED', feedbackSeq: 3 }, axis: { command: 'RETURN_COMMANDED', commandSeq: 4, standby: 'NOT_CONFIRMED', standbySeq: null }, failure: null, events: [] } },
      sequence: makeSequence({ autoSequence: 'PAUSE_REQUESTED', mode: 'PAUSE_REQUESTED', controls: makeControls({ abortActiveJob: { enabled: false, reason: 'SAFE_RETURN_IN_PROGRESS' }, resume: { enabled: false, reason: 'PAUSE_REQUESTED_JOB_ACTIVE' } }) }),
    });
    expect(r.getByTestId('asc-job').textContent).toBe(`${job.jobId} · ${job.targetSensorId}`);
    expect(r.getByTestId('asc-sr').textContent).toBe('SAFE_RETURN_TO_STANDBY · SR4');
    expect(r.getByTestId('asc-abort-reason').textContent).toBe('Mandatory Safe Return already in progress');
    expect(r.getByTestId('asc-resume-reason').textContent).toContain('wait for the current Job');
  });

  it('disconnected: every AutoSequence control disabled, nothing queued', async () => {
    const calls = stubControls();
    const r = await mountControl({ sequence: makeSequence({ controls: makeControls({ start: ON, pauseAfterCurrentJob: ON }) }) });
    act(() => r.store.setConnection({ state: 'DISCONNECTED' }));
    await act(async () => {});
    for (const b of r.getByTestId('asc-group').querySelectorAll('button')) expect((b as HTMLButtonElement).disabled).toBe(true);
    expect(r.getByTestId('asc-start-reason').textContent).toContain('Disconnected');
    fireEvent.click(r.getByTestId('asc-start'));
    await act(async () => {});
    expect(calls.filter((c) => c.url === '/api/spike/scenario')).toHaveLength(0);
  });
});
