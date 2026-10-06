// WJSS Stage 0.2.1A — ring buffer stays bounded.
import { describe, expect, it } from 'vitest';
import { RingBuffer } from '../src/lib/ringBuffer';

describe('RingBuffer', () => {
  it('never exceeds capacity and keeps the newest items in order', () => {
    const r = new RingBuffer<number>(600);
    for (let i = 0; i < 100_000; i += 1) r.push(i);
    expect(r.length).toBe(600);
    const a = r.toArray();
    expect(a[0]).toBe(99_400);
    expect(a[599]).toBe(99_999);
    expect(r.last()).toBe(99_999);
  });
  it('clear empties it', () => {
    const r = new RingBuffer<number>(3);
    r.push(1);
    r.clear();
    expect(r.length).toBe(0);
    expect(r.toArray()).toEqual([]);
  });
  it('rejects invalid capacity', () => {
    expect(() => new RingBuffer(0)).toThrow();
  });
});
