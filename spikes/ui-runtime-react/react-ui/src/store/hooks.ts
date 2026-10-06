// WJSS Stage 0.2.1A — React bindings for the presentation store (useSyncExternalStore).

import { createContext, useCallback, useContext, useSyncExternalStore } from 'react';
import type { SensorPresentationState } from '../../../contracts/operational';
import type { PresentationStore, SliceKey, Slices } from './presentationStore';

export const StoreContext = createContext<PresentationStore | null>(null);

export function useStore(): PresentationStore {
  const s = useContext(StoreContext);
  if (!s) throw new Error('PresentationStore missing: wrap the tree in <StoreContext.Provider>.');
  return s;
}

/** Subscribes to one Sensor only. Re-renders only when that Sensor's record is replaced. */
export function useSensor(sensorId: string): SensorPresentationState | undefined {
  const store = useStore();
  const subscribe = useCallback((cb: () => void) => store.subscribeSensor(sensorId, cb), [store, sensorId]);
  const get = useCallback(() => store.getSensor(sensorId), [store, sensorId]);
  return useSyncExternalStore(subscribe, get, get);
}

export function useSlice<K extends SliceKey>(key: K): Slices[K] {
  const store = useStore();
  const subscribe = useCallback((cb: () => void) => store.subscribe(key, cb), [store, key]);
  const get = useCallback(() => store.getSlice(key), [store, key]);
  return useSyncExternalStore(subscribe, get, get);
}
