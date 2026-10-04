// WJSS Stage 0.2.1A — golden fixture structural validation.
import { describe, expect, it } from 'vitest';
import snapshot from '../../contracts/fixtures/snapshot.example.json';
import delta from '../../contracts/fixtures/delta.example.json';
import { validateDelta, validateSnapshot } from '../../contracts/validate.mjs';
import { PresentationStore } from '../src/store/presentationStore';
import type { OperationalDelta, OperationalSnapshot } from '../../contracts/operational';

describe('golden fixtures', () => {
  it('snapshot fixture is structurally valid (104 sensors, 208 channels, 10 devices)', () => {
    expect(validateSnapshot(snapshot)).toEqual([]);
  });
  it('delta fixture is structurally valid and chains from the snapshot fixture', () => {
    expect(validateDelta(delta)).toEqual([]);
    expect(delta.previousRevision).toBe(snapshot.revision);
  });
  it('fixtures contain only synthetic identifiers', () => {
    const text = JSON.stringify(snapshot) + JSON.stringify(delta);
    expect(text).not.toMatch(/\b\d{1,3}(\.\d{1,3}){3}\b/);
    const ids = text.match(/"sensorId":"([^"]+)"/g) ?? [];
    expect(ids.every((m) => m.includes('SYN-'))).toBe(true);
  });
  it('fixtures exercise every quality state', () => {
    const q = new Set((snapshot as OperationalSnapshot).sensors.map((s) => s.quality));
    for (const k of ['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED']) expect(q.has(k as never)).toBe(true);
  });
  it('the store accepts the fixture pair', () => {
    const store = new PresentationStore();
    store.applySnapshot(snapshot as OperationalSnapshot);
    expect(store.applyDelta(delta as OperationalDelta)).toBe('applied');
  });
  it('validator rejects protected-rule violations', () => {
    const bad = structuredClone(snapshot) as OperationalSnapshot;
    const s = bad.sensors.find((x) => x.quality === 'BAD')!;
    s.classification = 'DIRTY';
    expect(validateSnapshot(bad).join()).toMatch(/BAD must be NOT_CLASSIFIED/);
    const bad2 = structuredClone(snapshot) as OperationalSnapshot;
    bad2.sensors.pop();
    expect(validateSnapshot(bad2).length).toBeGreaterThan(0);
    expect(validateDelta({ ...delta, previousRevision: delta.revision })).not.toEqual([]);
  });
});
