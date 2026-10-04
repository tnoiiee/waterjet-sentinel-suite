// WJSS Stage 0.2.1A — render isolation in jsdom. This is NOT a browser rendering result.
import { act, fireEvent, render } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { App } from '../src/App';
import { WallOverview } from '../src/components/WallOverview';
import { renderCounter } from '../src/diagnostics/renderCounter';
import { StoreContext } from '../src/store/hooks';
import { PresentationStore } from '../src/store/presentationStore';
import { makeDelta, makeSensor, makeSnapshot } from './helpers';
import { useCallback, useState } from 'react';

function Harness({ store }: { store: PresentationStore }) {
  const [sel, setSel] = useState<string | null>(null);
  const onSelect = useCallback((id: string) => setSel(id), []);
  return (
    <StoreContext.Provider value={store}>
      <WallOverview selectedId={sel} onSelect={onSelect} />
    </StoreContext.Provider>
  );
}

describe('render isolation (jsdom)', () => {
  beforeEach(() => renderCounter.reset());

  it('renders 104 Sensor cells from the Snapshot', () => {
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1));
    const { container } = render(<Harness store={store} />);
    expect(container.querySelectorAll('[data-sensor-id]').length).toBe(104);
  });

  it('a Delta with one changed Sensor re-renders only that cell', () => {
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1));
    render(<Harness store={store} />);
    renderCounter.reset();
    act(() => {
      store.applyDelta(makeDelta(1, { sensors: [makeSensor({ sensorId: 'SYN-RIGHT-07', wall: 'RIGHT', index: 7, dirtyScore: 72, lastValidatedScore: 72, classification: 'DIRTY' })] }));
    });
    expect(renderCounter.sensorCellRenders).toBe(1);
    expect([...renderCounter.perSensor.keys()]).toEqual(['SYN-RIGHT-07']);
  });

  it('a Delta with only panel singletons re-renders no Sensor cell', () => {
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1));
    render(<Harness store={store} />);
    renderCounter.reset();
    act(() => {
      store.applyDelta(makeDelta(1, { queue: { totalQueued: 3, entries: [] }, trendPoint: { t: 1, series: [1, 2, 3, 4], setpoint: 100, jobActive: false, alarmActive: false } }));
    });
    expect(renderCounter.sensorCellRenders).toBe(0);
  });

  it('a Delta with 10 changed Sensors renders exactly those 10 cells', () => {
    const store = new PresentationStore();
    const snap = makeSnapshot(1);
    store.applySnapshot(snap);
    render(<Harness store={store} />);
    renderCounter.reset();
    const changed = snap.sensors.slice(30, 40).map((s) => ({ ...s, dirtyScore: 61, classification: 'DIRTY' as const }));
    act(() => {
      store.applyDelta(makeDelta(1, { sensors: changed }));
    });
    expect(renderCounter.sensorCellRenders).toBe(10);
  });

  it('selection changes re-render only the previously and newly selected cells, and selection survives Deltas and Snapshots', () => {
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1));
    const { container } = render(<Harness store={store} />);
    const cell = (id: string) => container.querySelector(`[data-sensor-id="${id}"]`) as HTMLElement;
    fireEvent.click(cell('SYN-LEFT-05'));
    renderCounter.reset();
    fireEvent.click(cell('SYN-REAR-12'));
    expect(renderCounter.sensorCellRenders).toBe(2);
    expect(new Set(renderCounter.perSensor.keys())).toEqual(new Set(['SYN-LEFT-05', 'SYN-REAR-12']));
    act(() => {
      store.applyDelta(makeDelta(1, { sensors: [makeSensor({ sensorId: 'SYN-REAR-12', wall: 'REAR', index: 12, dirtyScore: 80, classification: 'DIRTY' })] }));
    });
    expect(cell('SYN-REAR-12').getAttribute('aria-pressed')).toBe('true');
    act(() => store.applySnapshot(makeSnapshot(99)));
    expect(cell('SYN-REAR-12').getAttribute('aria-pressed')).toBe('true');
  });

  it('full Operations page mounts in jsdom with all required areas (trend excluded: needs canvas)', () => {
    const store = new PresentationStore();
    store.applySnapshot(makeSnapshot(1));
    const { getByTestId } = render(
      <StoreContext.Provider value={store}>
        <App showTrend={false} />
      </StoreContext.Provider>,
    );
    for (const id of ['status-bar', 'wall-overview', 'sensor-detail', 'active-job', 'queue-preview', 'camera-placeholder', 'alarm-strip']) expect(getByTestId(id)).toBeTruthy();
    const svg = getByTestId('camera-placeholder').querySelector('svg')!;
    expect(svg.outerHTML).not.toMatch(/https?:\/\/(?!www\.w3\.org)/);
  });
});
