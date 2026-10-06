// WJSS Stage 0.2.1A — U-shaped wall map rendering from the runtime wall map (jsdom).
// This is NOT a browser rendering result; the Owner-local visual review remains required.
import { act, fireEvent, render } from '@testing-library/react';
import { useCallback, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { WallOverview } from '../src/components/WallOverview';
import { SensorDetail } from '../src/components/Panels';
import { StoreContext } from '../src/store/hooks';
import { PresentationStore } from '../src/store/presentationStore';
import { makeDelta, makeSensor, makeSnapshot } from './helpers';

function Harness({ store, spy }: { store: PresentationStore; spy?: (id: string) => void }) {
  const [sel, setSel] = useState<string | null>(null);
  const onSelect = useCallback(
    (id: string) => {
      spy?.(id);
      setSel(id);
    },
    [spy],
  );
  return (
    <StoreContext.Provider value={store}>
      <WallOverview selectedId={sel} onSelect={onSelect} />
      <SensorDetail sensorId={sel} />
    </StoreContext.Provider>
  );
}

function mount(spy?: (id: string) => void) {
  const store = new PresentationStore();
  store.applySnapshot(makeSnapshot(1));
  return { store, ...render(<Harness store={store} spy={spy} />) };
}

const rowIds = (wall: Element, r: number) =>
  [...wall.querySelectorAll(`[data-wall-row="${r}"] > *`)].map((el) => el.getAttribute('data-sensor-id') ?? el.getAttribute('data-equipment-id'));

describe('U-shaped Sensor map (106 Sensors, 2 Cannon slots)', () => {
  it('renders four walls in U order with 6 rows each and 4/5/4/5 columns', () => {
    const { container } = mount();
    const walls = [...container.querySelectorAll('[data-wall]')];
    expect(walls.map((w) => w.getAttribute('data-wall'))).toEqual(['REAR', 'LEFT', 'RIGHT', 'FRONT']);
    expect(walls.map((w) => w.getAttribute('data-wall-rows'))).toEqual(['6', '6', '6', '6']);
    expect(walls.map((w) => w.getAttribute('data-wall-columns'))).toEqual(['5', '4', '4', '5']);
    expect(walls.map((w) => w.className.split(' ').find((c) => c.startsWith('wall_')))).toEqual(['wall_REAR', 'wall_LEFT', 'wall_RIGHT', 'wall_FRONT']);
  });

  it('renders 106 Sensor cells and 2 Cannon slots; totals legend reads 106 / 2', () => {
    const { container, getByTestId } = mount();
    expect(container.querySelectorAll('[data-sensor-id]').length).toBe(106);
    expect(container.querySelectorAll('[data-slot-type="SENSOR"]').length).toBe(106);
    const cannons = [...container.querySelectorAll('[data-slot-type="CANNON"]')];
    expect(cannons.map((c) => c.getAttribute('data-equipment-id'))).toEqual(['CANNON_REAR', 'CANNON_FRONT']);
    expect(container.querySelector('[data-sensor-id="I7"]')).toBeNull();
    expect(container.querySelector('[data-sensor-id="I16"]')).toBeNull();
    expect(getByTestId('map-sensor-count').textContent).toBe('106 Sensors');
    // Visible terminology: two logical Water Jet reference slots — never "2 Water Jets".
    expect(getByTestId('map-cannon-count').textContent).toBe('2 Water Jet reference slots · synthetic');
    expect(getByTestId('map-center').textContent).not.toMatch(/\b2 Water Jets\b/);
    expect(container.textContent).not.toMatch(/Cannon|C-R|C-F/);
    const per = Object.fromEntries([...container.querySelectorAll('[data-wall]')].map((w) => [w.getAttribute('data-wall'), w.querySelectorAll('[data-sensor-id]').length]));
    expect(per).toEqual({ REAR: 29, LEFT: 24, RIGHT: 24, FRONT: 29 });
  });

  it('keeps canonical row order (G+2xx, G+1xx, G, H, I, J) and logical column order inside each wall', () => {
    const { container } = mount();
    const wall = (w: string) => container.querySelector(`[data-wall="${w}"]`)!;
    expect([1, 2, 3, 4, 5, 6].map((r) => rowIds(wall('LEFT'), r)[0])).toEqual(['G+201', 'G+101', 'G1', 'H1', 'I1', 'J1']);
    expect(rowIds(wall('LEFT'), 1)).toEqual(['G+201', 'G+202', 'G+203', 'G+204']);
    expect(rowIds(wall('REAR'), 1)).toEqual(['G+205', 'G+206', 'G+207', 'G+208', 'G+209']);
    expect(rowIds(wall('REAR'), 5)).toEqual(['I5', 'I6', 'CANNON_REAR', 'I8', 'I9']);
    expect(rowIds(wall('RIGHT'), 6)).toEqual(['J10', 'J11', 'J12', 'J13']);
    expect(rowIds(wall('FRONT'), 2)).toEqual(['G+114', 'G+115', 'G+116', 'G+117', 'G+118']);
    expect(rowIds(wall('FRONT'), 5)).toEqual(['I14', 'I15', 'CANNON_FRONT', 'I17', 'I18']);
    expect(rowIds(wall('FRONT'), 6)).toEqual(['J14', 'J15', 'J16', 'J17', 'J18']);
  });

  it('Cannon slots are neutral equipment: not buttons, not selectable, no Sensor overlays', () => {
    const spy = vi.fn();
    const { container } = mount(spy);
    for (const c of container.querySelectorAll('[data-slot-type="CANNON"]')) {
      expect(c.tagName).not.toBe('BUTTON');
      expect(c.getAttribute('tabindex')).toBeNull();
      expect(c.hasAttribute('data-process')).toBe(false);
      expect(c.hasAttribute('data-quality')).toBe(false);
      expect(c.hasAttribute('data-alarm')).toBe(false);
      expect(c.querySelector('[data-queue]')).toBeNull();
      expect(c.getAttribute('aria-label')).toMatch(/not a Sensor/);
      fireEvent.click(c);
    }
    expect(spy).not.toHaveBeenCalled();
    expect(container.querySelectorAll('[aria-pressed="true"]').length).toBe(0);
  });

  it('selecting a Sensor shows its logical position; the layout is Snapshot-only and survives Deltas', () => {
    const { container, store, getByTestId } = mount();
    fireEvent.click(container.querySelector('[data-sensor-id="H7"]')!);
    expect(getByTestId('detail-position').textContent).toBe('REAR · logical column 7 · logical row 4');
    const layoutBefore = store.getSlice('layout');
    act(() => {
      store.applyDelta(makeDelta(1, { sensors: [makeSensor({ sensorId: 'H7', dirtyScore: 80, classification: 'DIRTY' })] }));
    });
    expect(store.getSlice('layout')).toBe(layoutBefore);
    expect(container.querySelector('[data-sensor-id="H7"]')!.getAttribute('aria-pressed')).toBe('true');
  });
});
