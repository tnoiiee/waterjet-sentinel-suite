// WJSS Stage 0.2.1A — SSE feed: reconnect re-snapshot, gap resync, no command replay.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SseFeed } from '../src/store/feed';
import { PresentationStore } from '../src/store/presentationStore';
import { FakeEventSource, makeDelta, makeSensor, makeSnapshot } from './helpers';

describe('SseFeed', () => {
  let fetchSpy: ReturnType<typeof vi.fn>;
  beforeEach(() => {
    FakeEventSource.instances = [];
    fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  const setup = () => {
    const store = new PresentationStore();
    const feed = new SseFeed(store, { url: '/api/stream', createEventSource: (u) => new FakeEventSource(u), reopenDelayMs: 500 });
    feed.start();
    return { store, feed, es: () => FakeEventSource.instances.at(-1)! };
  };

  it('applies the initial Snapshot then Deltas', () => {
    const { store, es } = setup();
    es().emit('snapshot', makeSnapshot(5));
    es().emit('delta', makeDelta(5));
    expect(store.revision).toBe(6);
    expect(store.getSlice('connection').state).toBe('LIVE');
  });

  it('on revision gap: closes the stream and opens a new one; a fresh Snapshot replaces state', () => {
    const { store, feed, es } = setup();
    const first = es();
    first.emit('snapshot', makeSnapshot(5));
    first.emit('delta', makeDelta(7, { sensors: [makeSensor({ sensorId: 'SYN-LEFT-01', dirtyScore: 99 })] }));
    expect(first.closed).toBe(true);
    expect(feed.opened).toBe(2);
    expect(store.getSlice('connection').state).toBe('RESYNCING');
    expect(store.revision).toBe(5);
    expect(store.getSensor('SYN-LEFT-01')?.dirtyScore).toBe(30);
    es().emit('snapshot', makeSnapshot(9));
    expect(store.revision).toBe(9);
    expect(store.getSlice('connection').state).toBe('LIVE');
    expect(store.getSlice('connection').gaps).toBe(1);
  });

  it('on disconnect: keeps last state, marks DISCONNECTED, and does not infer', () => {
    const { store, es } = setup();
    es().emit('snapshot', makeSnapshot(5, { activeJob: null }));
    es().fail(0);
    expect(store.getSlice('connection').state).toBe('DISCONNECTED');
    expect(store.revision).toBe(5);
    expect(store.getSlice('activeJob')).toBeNull();
    // Native EventSource reconnects on the same instance; server sends a Snapshot first.
    es().emit('snapshot', makeSnapshot(40));
    expect(store.getSlice('connection').state).toBe('LIVE');
    expect(store.revision).toBe(40);
  });

  it('reopens after the EventSource gives up (CLOSED) and re-snapshots', () => {
    const { store, feed, es } = setup();
    es().emit('snapshot', makeSnapshot(5));
    es().fail(2);
    expect(feed.opened).toBe(1);
    vi.advanceTimersByTime(600);
    expect(feed.opened).toBe(2);
    es().emit('snapshot', makeSnapshot(77));
    expect(store.revision).toBe(77);
  });

  it('never replays outbound commands on reconnect or resync', () => {
    const { es } = setup();
    es().emit('snapshot', makeSnapshot(5));
    es().fail(2);
    vi.advanceTimersByTime(2000);
    es().emit('snapshot', makeSnapshot(6));
    es().emit('delta', makeDelta(8));
    es().emit('snapshot', makeSnapshot(10));
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('ignores messages from a superseded EventSource', () => {
    const { store, es } = setup();
    const first = es();
    first.emit('snapshot', makeSnapshot(5));
    first.emit('delta', makeDelta(9)); // gap -> new source
    first.emit('snapshot', makeSnapshot(1000));
    expect(store.revision).toBe(5);
  });
});
