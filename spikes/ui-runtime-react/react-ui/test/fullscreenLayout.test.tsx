// WJSS Stage 0.2.1A — fullscreen refinement component tests (jsdom).
// jsdom performs no layout: these tests cover structure and state only. Viewport fit, card
// bounds, and computed sizes are asserted in e2e/layout.spec.ts in Owner-local Microsoft Edge.
import { act, fireEvent, render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from '../src/App';
import { StoreContext } from '../src/store/hooks';
import { PresentationStore } from '../src/store/presentationStore';
import { compactReason } from '../src/visual/queueReason';
import { fmtElapsed } from '../src/components/Panels';
import { makeSnapshot } from './helpers';

function mountApp(over: Parameters<typeof makeSnapshot>[1] = {}) {
  const store = new PresentationStore();
  store.applySnapshot(makeSnapshot(1, over));
  const r = render(
    <StoreContext.Provider value={store}>
      <App showTrend={false} />
    </StoreContext.Provider>,
  );
  return { store, ...r };
}

describe('compact GlobalQueue source reason (presentation only)', () => {
  it('maps runtime codes to short labels and keeps the full reason for the tooltip', () => {
    expect(compactReason('SYN_DIRTY_SCORE_ABOVE_THRESHOLD').label).toBe('DIRTY SCORE');
    expect(compactReason('SYN_TEMP_RISE').label).toBe('TEMP');
    expect(compactReason('SYN_TIME_SINCE_CLEAN').label).toBe('TIME');
    expect(compactReason('SYN_TEMP_AND_TIME').label).toBe('TEMP + TIME');
    expect(compactReason('SYN_OPERATOR_REQUEST').label).toBe('OPERATOR');
    expect(compactReason('SYN_SCENARIO_COMMAND').label).toBe('SCENARIO');
    expect(compactReason('').label).toBe('--');
    expect(compactReason(null).label).toBe('--');
    const r = compactReason('SYN_DIRTY_SCORE_ABOVE_THRESHOLD');
    expect(r.full).toContain('SYN_DIRTY_SCORE_ABOVE_THRESHOLD');
    expect(r.full).toContain('dirty score above threshold');
  });

  it('queue rows show the compact label; order and membership come from the runtime unchanged', () => {
    const entries = ['G+201', 'H7', 'J18'].map((sensorId, i) => ({
      position: i + 1,
      sensorId,
      sourceReason: 'SYN_DIRTY_SCORE_ABOVE_THRESHOLD',
      dirtyScore: 70 + i,
      secondsSinceLastClean: 600,
      status: 'READY' as const,
    }));
    const { getByTestId } = mountApp({ queue: { totalQueued: 3, entries } });
    const rows = [...getByTestId('queue-preview').querySelectorAll('tbody tr')];
    expect(rows.map((r) => r.children[1].textContent)).toEqual(['G+201', 'H7', 'J18']);
    for (const r of rows) {
      const cell = r.querySelector('[data-testid="queue-reason"]')!;
      expect(cell.textContent).toBe('DIRTY SCORE');
      expect(cell.getAttribute('title')).toContain('SYN_DIRTY_SCORE_ABOVE_THRESHOLD');
    }
  });
});

describe('Diagnostics drawer', () => {
  it('opens from the status bar, closes from its own Close button, and toggles with D', () => {
    const { getByTestId, queryByTestId } = mountApp();
    expect(queryByTestId('diagnostics')).toBeNull();
    const toggle = getByTestId('diagnostics-toggle');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    fireEvent.click(toggle);
    expect(getByTestId('diagnostics')).toBeTruthy();
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    fireEvent.click(getByTestId('diagnostics-close'));
    expect(queryByTestId('diagnostics')).toBeNull();
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    act(() => {
      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));
    });
    expect(getByTestId('diagnostics')).toBeTruthy();
    act(() => {
      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'D' }));
    });
    expect(queryByTestId('diagnostics')).toBeNull();
  });

  it('the drawer is rendered outside the main layout grid (it never consumes layout space)', () => {
    const { getByTestId, container } = mountApp();
    fireEvent.click(getByTestId('diagnostics-toggle'));
    const drawer = getByTestId('diagnostics');
    expect(container.querySelector('main')!.contains(drawer)).toBe(false);
    // All metrics retained.
    for (const label of ['Revision', 'Connection', 'Snapshots / Deltas', 'Gaps / reconnects', 'Last message', 'Cell renders (total)', 'Trend points', 'DOM nodes', 'Long tasks', 'JS heap', 'Historian depth', 'Invariant violations']) {
      expect(drawer.textContent).toContain(label);
    }
  });
});

describe('compact right-side cards', () => {
  it('Sensor Detail: strong ID header and a two-column inspector grouped Process / Location | State / Source', () => {
    const { container, getByTestId } = mountApp();
    fireEvent.click(container.querySelector('[data-sensor-id="H7"]')!);
    const labels = (id: string) => [...getByTestId(id).querySelectorAll('dt')].map((d) => d.textContent);
    const groups = (id: string) => [...getByTestId(id).querySelectorAll('[role="group"]')].map((g) => g.getAttribute('aria-label'));
    expect(groups('detail-col-a')).toEqual(['Process', 'Location']);
    expect(groups('detail-col-b')).toEqual(['State', 'Source']);
    expect(labels('detail-col-a')).toEqual(['Classification', 'Dirty Score', 'Last validated', 'Wall / position']);
    expect(labels('detail-col-b')).toEqual(['Quality', 'Queue', 'Alarm', 'Device', 'TC_F / TC_R']);
    // The ID is a header element, not a table row; no nested cards inside the inspector.
    const id = getByTestId('detail-id');
    expect(id.textContent).toBe('H7');
    expect(id.closest('header')).not.toBeNull();
    expect(getByTestId('detail-inspector').querySelector('section')).toBeNull();
    expect(getByTestId('detail-position').textContent).toBe('REAR · logical column 7 · logical row 4');
    // Emphasis hierarchy: process / state values strong; device and channels de-emphasised.
    for (const t of ['detail-class', 'detail-score', 'detail-quality', 'detail-queue', 'detail-alarm']) expect(getByTestId(t).getAttribute('data-emphasis'), t).toBe('strong');
    expect(getByTestId('detail-device').getAttribute('data-emphasis')).toBe('low');
    expect(getByTestId('detail-tcf').closest('dd')!.getAttribute('data-emphasis')).toBe('low');
    // Long values stay available in full via the tooltip; synthetic notice retained.
    for (const t of ['detail-device', 'detail-tcf', 'detail-tcr', 'detail-position']) {
      const el = getByTestId(t);
      expect(el.getAttribute('title')).toBe(el.textContent);
    }
    expect(getByTestId('sensor-detail').querySelector('[title="SYNTHETIC — not a Production value"]')).not.toBeNull();
  });

  it('Active Cleaning Job keeps Job ID, Sensor, Water Jet, Isolation Valve, P1-P6, phase, progress, and the single-job notice', () => {
    const { getByTestId } = mountApp({
      activeJob: {
        jobId: 'SYN-JOB-0007',
        targetSensorId: 'H7',
        jetId: 'SYN-JET-3',
        valveId: 'SYN-IV-3',
        phase: 'P3',
        phaseLabel: 'Synthetic jetting',
        phaseIndex: 2,
        startedAt: '2026-01-01T00:00:00.000Z',
        phaseStartedAt: '2026-01-01T00:00:10.000Z',
        phaseProgress: 0.42,
      },
    });
    const job = getByTestId('active-job');
    for (const t of ['SYN-JOB-0007', 'H7', 'SYN-JET-3', 'SYN-IV-3', 'P1', 'P2', 'P3', 'P4', 'P5', 'P6', 'Synthetic jetting', '42%', 'At most one Cleaning Job may be active']) {
      expect(job.textContent).toContain(t);
    }
    expect(job.querySelector('[aria-current="step"]')!.textContent).toBe('P3');
    expect(job.querySelectorAll('[aria-current="step"]').length).toBe(1);
    expect(job.querySelector('[role="progressbar"]')!.getAttribute('aria-valuenow')).toBe('42');
    // Separated fields: Target, Water Jet, Isolation Valve; completed / current / future phases.
    expect([...getByTestId('job-facts').querySelectorAll('dt')].map((d) => d.textContent)).toEqual(['Target Sensor', 'Water Jet', 'Isolation Valve']);
    expect(getByTestId('job-target').textContent).toBe('H7');
    expect(getByTestId('job-id').textContent).toBe('SYN-JOB-0007');
    expect([...job.querySelectorAll('[data-phase-state]')].map((l) => l.getAttribute('data-phase-state'))).toEqual(['done', 'done', 'current', 'future', 'future', 'future']);
    expect(getByTestId('job-elapsed').textContent).toMatch(/elapsed \d+:\d{2}(:\d{2})?/);
  });

  it('fmtElapsed formats m:ss and h:mm:ss', () => {
    const t0 = '2026-01-01T00:00:00.000Z';
    const base = Date.parse(t0);
    expect(fmtElapsed(t0, base + 65_000)).toBe('1:05');
    expect(fmtElapsed(t0, base + 3_725_000)).toBe('1:02:05');
    expect(fmtElapsed(t0, base - 5_000)).toBe('0:00');
  });

  it('GlobalQueue: numeric columns are right-aligned tabular cells, status chips carry the status token', () => {
    const entries = (['READY', 'HELD', 'BLOCKED', 'EXCLUDED'] as const).map((status, i) => ({
      position: i + 1,
      sensorId: ['G+201', 'H7', 'J18', 'I8'][i],
      sourceReason: 'SYN_TEMP_RISE',
      dirtyScore: 60 + i,
      secondsSinceLastClean: 900,
      status,
    }));
    const { getByTestId } = mountApp({ queue: { totalQueued: 4, entries } });
    const q = getByTestId('queue-preview');
    expect([...q.querySelectorAll('th')].map((h) => h.textContent)).toEqual(['Pos', 'Sensor', 'Source reason', 'Score', 'Since clean', 'Status']);
    const row = q.querySelector('tbody tr')!;
    for (const i of [0, 3, 4]) expect(row.children[i].className, `col ${i}`).toMatch(/numCol/);
    expect([...q.querySelectorAll('[data-status]')].map((c) => c.getAttribute('data-status'))).toEqual(['READY', 'HELD', 'BLOCKED', 'EXCLUDED']);
    // Order and membership unchanged (presentation only).
    expect([...q.querySelectorAll('tbody tr')].map((r) => r.getAttribute('data-queue-position'))).toEqual(['1', '2', '3', '4']);
  });

  it('status bar groups follow the priority order Alarm > Process > System > Technical', () => {
    const { getByTestId } = mountApp();
    const bar = getByTestId('status-bar');
    expect([...bar.querySelectorAll('[data-status-group]')].map((g) => g.getAttribute('data-status-group'))).toEqual(['app', 'alarm', 'process', 'system', 'technical']);
    const process = bar.querySelector('[data-status-group="process"]')!;
    expect(process.contains(getByTestId('job-state'))).toBe(true);
    expect(process.contains(getByTestId('pump-state'))).toBe(true);
    expect(bar.querySelector('[data-status-group="system"]')!.contains(getByTestId('conn-state'))).toBe(true);
    expect(bar.querySelector('[data-status-group="technical"]')!.textContent).toMatch(/rev .*config r/);
  });

  it('alarm strip: compact normal state with zero alarms; condition plus required response when present', () => {
    const quiet = mountApp();
    expect(quiet.getByTestId('alarm-strip').textContent).toContain('No active alarms');
    quiet.unmount();
    const { getByTestId } = mountApp({
      alarms: {
        activeUnack: 1,
        activeAck: 0,
        clearedUnack: 1,
        items: [
          { alarmId: 'A1', code: 'SYN-ALM-1', text: 'synthetic', sensorId: 'H7', deviceId: null, state: 'ACTIVE_UNACK', severity: 'HIGH', raisedAt: '2026-01-01T00:00:00.000Z' },
          { alarmId: 'A2', code: 'SYN-ALM-2', text: 'synthetic', sensorId: 'J18', deviceId: null, state: 'CLEARED_UNACK', severity: 'LOW', raisedAt: '2026-01-01T00:00:00.000Z' },
        ],
      },
    });
    const strip = getByTestId('alarm-strip');
    expect(strip.textContent).toContain('SYN-ALM-1 H7 — active, unacknowledged');
    expect(strip.textContent).toContain('Response: acknowledge and inspect');
    expect(strip.textContent).toContain('acknowledgement required');
    expect(getByTestId('alarm-state').textContent).toMatch(/Alarms 2 · 2 need ack/);
  });

  it('camera placeholder: title, synthetic badge, no-signal state, icon, and no media or URL', () => {
    const { getByTestId } = mountApp();
    const cam = getByTestId('camera-placeholder');
    expect(getByTestId('camera-badge').textContent).toBe('SYNTHETIC PLACEHOLDER');
    expect(getByTestId('camera-state').textContent).toBe('NO SIGNAL');
    expect(cam.textContent).toContain('No video source configured');
    expect(cam.querySelector('svg')!.getAttribute('width')).toBe('48');
    expect(cam.querySelector('video, img, iframe, image')).toBeNull();
    expect(cam.outerHTML).not.toMatch(/https?:\/\/(?!www\.w3\.org)/);
  });

  it('Sensor cell DOM zones: ID, reserved marker zone, value, rail; markers never inside the ID element', () => {
    const { container } = mountApp();
    const cell = container.querySelector('[data-sensor-id="G+205"]')!;
    const parts = [...cell.children].map((c) => c.getAttribute('data-part'));
    expect(parts).toEqual(['id', 'marker-zone', 'value', 'rail']);
    expect(cell.querySelector('[data-part="id"]')!.textContent).toBe('G+205');
    expect(cell.querySelector('[data-part="id"]')!.children.length).toBe(0);
  });
});

describe('no mapping, count, or Cannon regressions from the layout refinement', () => {
  it('Sensor cell classes are mapping-independent (same state -> same classes on every wall)', () => {
    const { container } = mountApp();
    const ids = ['G+201', 'G+205', 'H12', 'J18', 'I8'];
    const classes = ids.map((id) => container.querySelector(`[data-sensor-id="${id}"]`)!.className);
    expect(new Set(classes).size).toBe(1);
    for (const c of classes) expect(c).not.toMatch(/LEFT|REAR|RIGHT|FRONT|G\+|wall/i);
  });

  it('still renders 106 Sensors, 2 Cannon slots, 24/29/24/29, and Cannons stay non-selectable', () => {
    const { container, getByTestId } = mountApp();
    expect(container.querySelectorAll('[data-sensor-id]').length).toBe(106);
    expect(container.querySelectorAll('[data-slot-type="CANNON"]').length).toBe(2);
    const per = Object.fromEntries([...container.querySelectorAll('[data-wall]')].map((w) => [w.getAttribute('data-wall'), w.querySelectorAll('[data-sensor-id]').length]));
    expect(per).toEqual({ REAR: 29, LEFT: 24, RIGHT: 24, FRONT: 29 });
    expect([...container.querySelectorAll('[data-wall]')].map((w) => w.getAttribute('data-wall'))).toEqual(['REAR', 'LEFT', 'RIGHT', 'FRONT']);
    for (const c of container.querySelectorAll('[data-slot-type="CANNON"]')) fireEvent.click(c);
    expect(getByTestId('sensor-detail').textContent).toContain('Select a Sensor cell.');
    // Walls and the compact center summary live inside one bounded map surface.
    const surface = getByTestId('u-surface');
    expect(surface.querySelectorAll('[data-wall]').length).toBe(4);
    expect(surface.contains(getByTestId('map-center'))).toBe(true);
    expect(getByTestId('map-center').textContent).toMatch(/106 Sensors.*2 Water Jet reference slots/);
  });
});
