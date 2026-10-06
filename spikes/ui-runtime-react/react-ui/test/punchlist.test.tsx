// WJSS Stage 0.2.1A — Final Owner UI punchlist component tests (jsdom: structure and state only;
// rendered geometry, font use, and bounds are asserted in Owner-local Edge, e2e/layout.spec.ts).
import { act, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';
import { LEGEND_KINDS, LegendSwatch } from '../src/components/LegendSwatch';
import { SyntheticTestControl } from '../src/components/SyntheticTestControl';
import { StoreContext } from '../src/store/hooks';
import { PresentationStore } from '../src/store/presentationStore';
import { compactReason } from '../src/visual/queueReason';
import { makeSnapshot } from './helpers';

function mountApp() {
  const store = new PresentationStore();
  store.applySnapshot(makeSnapshot(1));
  const r = render(
    <StoreContext.Provider value={store}>
      <App showTrend={false} />
    </StoreContext.Provider>,
  );
  return { store, ...r };
}

afterEach(() => vi.unstubAllGlobals());

describe('application identity', () => {
  it('shows the product name and the "Operations Console" subtitle; badge is separate; full accessible label', () => {
    const { getByTestId } = mountApp();
    const id = getByTestId('app-identity');
    expect(id.tagName).toBe('H1');
    expect(id.getAttribute('aria-label')).toBe('WaterJet Sentinel Suite — Operations Console');
    expect(getByTestId('app-name').textContent).toBe('WaterJet Sentinel Suite');
    expect(getByTestId('app-subtitle').textContent).toBe('Operations Console');
    const badge = getByTestId('synthetic-badge');
    expect(id.contains(badge)).toBe(false);
    expect(badge.textContent).toMatch(/SYNTHETIC/);
  });
});

describe('Sensor map legend', () => {
  it('renders the eight approved entries in order with a fixed icon column and a text label', () => {
    const { getByTestId } = mountApp();
    const legend = getByTestId('map-legend');
    const items = [...legend.querySelectorAll('[data-legend]')];
    expect(items.map((i) => i.textContent)).toEqual(['Dirty', 'Cleaner', 'Not classified', 'Uncertain', 'Alarm', 'Selected', 'Active Job', 'Water Jet']);
    for (const i of items) {
      expect(i.children).toHaveLength(2);
      expect(i.children[0].querySelector('svg')).not.toBeNull();
    }
  });

  it('every swatch is a 16 x 16 SVG whose shapes stay inside the inset box (no negative offsets)', () => {
    for (const kind of LEGEND_KINDS) {
      const { container, unmount } = render(<LegendSwatch kind={kind} />);
      const svg = container.querySelector('svg')!;
      expect(svg.getAttribute('width')).toBe('16');
      expect(svg.getAttribute('height')).toBe('16');
      expect(svg.getAttribute('viewBox')).toBe('0 0 16 16');
      expect(svg.getAttribute('aria-hidden')).toBe('true');
      for (const el of svg.querySelectorAll('*')) {
        const n = (a: string) => (el.hasAttribute(a) ? Number(el.getAttribute(a)) : null);
        const x = n('x') ?? 0;
        const y = n('y') ?? 0;
        const w = n('width') ?? 0;
        const h = n('height') ?? 0;
        expect(x, `${kind} x`).toBeGreaterThanOrEqual(0);
        expect(y, `${kind} y`).toBeGreaterThanOrEqual(0);
        expect(x + w, `${kind} right`).toBeLessThanOrEqual(16);
        expect(y + h, `${kind} bottom`).toBeLessThanOrEqual(16);
        const r = n('r');
        if (r !== null) {
          expect(n('cx')! - r).toBeGreaterThanOrEqual(0);
          expect(n('cx')! + r).toBeLessThanOrEqual(16);
        }
        for (const v of (el.getAttribute('d') ?? el.getAttribute('points') ?? '').match(/-?\d+(\.\d+)?/g) ?? []) {
          expect(Number(v), `${kind} path coordinate`).toBeGreaterThanOrEqual(0);
          expect(Number(v), `${kind} path coordinate`).toBeLessThanOrEqual(16);
        }
      }
      unmount();
    }
  });
});

describe('Water Jet terminology (display) — CANNON_* stays the internal legacy id', () => {
  it('slots read WJ REAR / WJ FRONT, are non-Sensor, non-selectable, and keep I7 / I16', () => {
    const { getByTestId, container } = mountApp();
    const rear = getByTestId('wj-slot-rear');
    const front = getByTestId('wj-slot-front');
    expect(rear.textContent).toBe('WJREAR');
    expect(front.textContent).toBe('WJFRONT');
    for (const [el, col] of [
      [rear, '7'],
      [front, '16'],
    ] as const) {
      expect(el.getAttribute('data-slot-type')).toBe('CANNON');
      expect(el.getAttribute('data-logical-column')).toBe(col);
      expect(el.getAttribute('data-logical-row')).toBe('5'); // logical row I (canonical CANNON_SLOTS)
      expect(el.getAttribute('aria-label')).toMatch(/Water Jet reference slot — equipment, not a Sensor/);
      expect(el.tagName).not.toBe('BUTTON');
      expect(el.hasAttribute('data-sensor-id')).toBe(false);
    }
    expect(container.querySelectorAll('[data-sensor-id]')).toHaveLength(106);
    expect(getByTestId('map-cannon-count').textContent).toBe('2 Water Jet reference slots · synthetic');
    expect(container.textContent).not.toMatch(/\b2 Water Jets\b|Cannon/);
  });
});

describe('GlobalQueue source labels', () => {
  it('TIME DUE is a distinct compact label; existing labels are unchanged', () => {
    expect(compactReason('SYN_TIME_DUE').label).toBe('TIME DUE');
    expect(compactReason('SYN_TIME_SINCE_CLEAN').label).toBe('TIME');
    expect(compactReason('SYN_TEMP_AND_TIME').label).toBe('TEMP + TIME');
    expect(compactReason('SYN_TEMP_RISE').label).toBe('TEMP');
    expect(compactReason('SYN_OPERATOR_REQUEST').label).toBe('OPERATOR');
    expect(compactReason('SYN_DIRTY_SCORE_ABOVE_THRESHOLD').label).toBe('DIRTY SCORE');
  });
});

describe('SYNTHETIC TEST CONTROL (opt-in spike review tooling)', () => {
  function mountControl(selectedId: string | null, fetchImpl: ReturnType<typeof vi.fn>) {
    vi.stubGlobal('fetch', fetchImpl);
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1)); // LIVE
    const r = render(
      <StoreContext.Provider value={store}>
        <SyntheticTestControl selectedId={selectedId} />
      </StoreContext.Provider>,
    );
    return { store, ...r };
  }
  const json = (status: number, body: unknown) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } }));

  it('is off (all controls disabled) when the harness does not enable it (endpoint 404)', async () => {
    const f = vi.fn(() => json(404, { error: 'not found' }));
    const { getByTestId } = mountControl('G+201', f);
    await waitFor(() => expect(getByTestId('synthetic-test-control').getAttribute('data-availability')).toBe('disabled'));
    expect(getByTestId('stc-state').textContent).toMatch(/--synthetic-test-controls/);
    expect((getByTestId('stc-alarm-raise') as HTMLButtonElement).disabled).toBe(true);
    expect(f).toHaveBeenCalledTimes(1);
  });

  it('is disabled without a selected Sensor; shows the selected ID prominently when selected', async () => {
    const f = vi.fn(() => json(200, { enabled: true, token: 't-1' }));
    const none = mountControl(null, f);
    await waitFor(() => expect(none.getByTestId('synthetic-test-control').getAttribute('data-availability')).toBe('enabled'));
    expect((none.getByTestId('stc-preset-queued-dirty') as HTMLButtonElement).disabled).toBe(true);
    expect(none.getByTestId('stc-selected').textContent).toBe('—');
    expect((none.getByTestId('stc-preset-reset') as HTMLButtonElement).disabled).toBe(false);
    none.unmount();
    const sel = mountControl('H7', f);
    await waitFor(() => expect((sel.getByTestId('stc-preset-queued-dirty') as HTMLButtonElement).disabled).toBe(false));
    expect(sel.getByTestId('stc-selected').textContent).toBe('H7');
  });

  it('a click sends exactly one synthetic scenario command with the in-memory token; no retry', async () => {
    const calls: Array<[string, RequestInit | undefined]> = [];
    const f = vi.fn((url: string, init?: RequestInit) => {
      calls.push([url, init]);
      if (url === '/api/spike/test-controls') return json(200, { enabled: true, token: 'tok-xyz' });
      return json(200, { accepted: true, detail: {} });
    });
    const { getByTestId } = mountControl('G+205', f);
    await waitFor(() => expect((getByTestId('stc-preset-queued-dirty') as HTMLButtonElement).disabled).toBe(false));
    await act(async () => {
      fireEvent.click(getByTestId('stc-preset-queued-dirty'));
    });
    await waitFor(() => expect(getByTestId('stc-result').textContent).toBe('visual-preset: accepted'));
    const posts = calls.filter(([u]) => u === '/api/spike/scenario');
    expect(posts).toHaveLength(1);
    const [, init] = posts[0];
    expect((init!.headers as Record<string, string>)['x-spike-token']).toBe('tok-xyz');
    expect(JSON.parse(String(init!.body))).toEqual({ command: 'visual-preset', params: { preset: 'queued-dirty', sensorId: 'G+205' } });
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });

  it('disconnect disables every control and drops the token; nothing is sent while disconnected', async () => {
    const f = vi.fn((url: string) => (url === '/api/spike/test-controls' ? json(200, { enabled: true, token: 'tok-1' }) : json(200, { accepted: true })));
    const { getByTestId, store } = mountControl('G+205', f);
    await waitFor(() => expect((getByTestId('stc-alarm-raise') as HTMLButtonElement).disabled).toBe(false));
    act(() => store.setConnection({ state: 'DISCONNECTED' }));
    expect((getByTestId('stc-alarm-raise') as HTMLButtonElement).disabled).toBe(true);
    expect(getByTestId('stc-state').textContent).toMatch(/Disconnected/);
    fireEvent.click(getByTestId('stc-alarm-raise'));
    expect(f.mock.calls.filter(([u]) => u === '/api/spike/scenario')).toHaveLength(0);
    // Reconnect: the token is fetched again (not reused from before the disconnect).
    act(() => store.setConnection({ state: 'LIVE' }));
    await waitFor(() => expect(f.mock.calls.filter(([u]) => u === '/api/spike/test-controls')).toHaveLength(2));
  });
});
