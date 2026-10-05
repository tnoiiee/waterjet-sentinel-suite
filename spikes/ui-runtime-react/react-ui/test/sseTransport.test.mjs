// @vitest-environment node
// WJSS Stage 0.2.1A — SSE transport integration: real Node harness on 127.0.0.1, real SseFeed
// and PresentationStore, EventSource implemented over fetch for Node. Not a browser test.
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { createHarness } from '../../runtime-harness/src/server.mjs';
import { validateDelta, validateSnapshot } from '../../contracts/validate.mjs';
import { SseFeed } from '../src/store/feed';
import { PresentationStore } from '../src/store/presentationStore';

const TOKEN = 'vitest-token';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Minimal EventSource over fetch (Node). readyState CLOSED + onerror when the stream ends. */
class FetchEventSource {
  constructor(url, headers = {}) {
    this.readyState = 0;
    this.onerror = null;
    this.onopen = null;
    this.listeners = {};
    this.ctrl = new AbortController();
    this.events = [];
    this.run(url, headers);
  }
  addEventListener(type, fn) {
    (this.listeners[type] ??= []).push(fn);
  }
  close() {
    this.readyState = 2;
    this.ctrl.abort();
  }
  async run(url, headers) {
    try {
      const res = await fetch(url, { headers, signal: this.ctrl.signal });
      this.readyState = 1;
      const reader = res.body.getReader();
      const dec = new TextDecoder();
      let buf = '';
      for (;;) {
        const { value, done } = await reader.read();
        if (done) break;
        buf += dec.decode(value, { stream: true });
        let i;
        while ((i = buf.indexOf('\n\n')) >= 0) {
          const block = buf.slice(0, i);
          buf = buf.slice(i + 2);
          let event = 'message';
          let data = '';
          for (const line of block.split('\n')) {
            if (line.startsWith('event: ')) event = line.slice(7);
            else if (line.startsWith('data: ')) data += line.slice(6);
          }
          if (!data) continue;
          this.events.push(event);
          for (const fn of this.listeners[event] ?? []) fn({ data });
        }
      }
    } catch {
      /* aborted */
    }
    if (this.readyState !== 2) {
      this.readyState = 2;
      this.onerror?.({});
    }
  }
}

let h;
const cmd = async (command, params) =>
  (await fetch(`${h.url}/api/spike/scenario`, { method: 'POST', headers: { 'content-type': 'application/json', 'x-spike-token': TOKEN }, body: JSON.stringify({ command, params }) })).json();

beforeAll(async () => {
  h = createHarness({ port: 0, token: TOKEN });
  await h.start();
  await sleep(1200);
});
afterAll(async () => {
  await h.stop();
});

describe('SSE transport against the Node harness', () => {
  it('first event is a valid Snapshot; following Deltas are valid and chained', async () => {
    const sources = [];
    const store = new PresentationStore();
    const feed = new SseFeed(store, { url: `${h.url}/api/stream`, createEventSource: (u) => (sources.push(new FetchEventSource(u)), sources.at(-1)) });
    const snaps = [];
    const deltas = [];
    const origS = store.applySnapshot.bind(store);
    const origD = store.applyDelta.bind(store);
    store.applySnapshot = (s, b) => (snaps.push(s), origS(s, b));
    store.applyDelta = (d, b) => (deltas.push(d), origD(d, b));
    feed.start();
    await sleep(3300);
    feed.stop();
    expect(sources[0].events[0]).toBe('snapshot');
    expect(validateSnapshot(snaps[0])).toEqual([]);
    expect(deltas.length).toBeGreaterThanOrEqual(2);
    for (const d of deltas) expect(validateDelta(d)).toEqual([]);
    expect(deltas[0].previousRevision).toBe(snaps[0].revision);
    expect(store.stats.gapsDetected).toBe(0);
    expect(store.sensorCount).toBe(106);
  });

  it('server ignores Last-Event-ID: reconnect always starts with a fresh Snapshot (no replay)', async () => {
    const es = new FetchEventSource(`${h.url}/api/stream`, { 'Last-Event-ID': '1' });
    const got = [];
    es.addEventListener('snapshot', (e) => got.push(['snapshot', JSON.parse(e.data).revision]));
    es.addEventListener('delta', (e) => got.push(['delta', JSON.parse(e.data).revision]));
    await sleep(1500);
    es.close();
    expect(got[0][0]).toBe('snapshot');
    expect(got[0][1]).toBeGreaterThan(1);
    expect(got.filter(([k]) => k === 'snapshot').length).toBe(1);
  });

  it('UI disconnect (server drops clients) -> feed reopens -> authoritative re-snapshot', async () => {
    const store = new PresentationStore();
    const feed = new SseFeed(store, { url: `${h.url}/api/stream`, createEventSource: (u) => new FetchEventSource(u), reopenDelayMs: 200 });
    feed.start();
    await sleep(1300);
    const before = store.getSlice('connection').snapshots;
    const r = await cmd('drop-clients');
    expect(r.detail.dropped).toBeGreaterThanOrEqual(1);
    await sleep(150);
    expect(['DISCONNECTED', 'LIVE']).toContain(store.getSlice('connection').state);
    await sleep(1200);
    const c = store.getSlice('connection');
    expect(c.snapshots).toBe(before + 1);
    expect(c.reconnects).toBeGreaterThanOrEqual(1);
    expect(c.state).toBe('LIVE');
    feed.stop();
  });

  it('a lost Delta (revision gap) makes the UI discard incremental state and re-snapshot', async () => {
    const store = new PresentationStore();
    const feed = new SseFeed(store, { url: `${h.url}/api/stream`, createEventSource: (u) => new FetchEventSource(u) });
    feed.start();
    await sleep(1300);
    await cmd('inject-revision-gap');
    await sleep(1800);
    const c = store.getSlice('connection');
    expect(store.stats.gapsDetected).toBeGreaterThanOrEqual(1);
    expect(c.gaps).toBeGreaterThanOrEqual(1);
    expect(c.snapshots).toBeGreaterThanOrEqual(2);
    expect(c.state).toBe('LIVE');
    expect(store.revision).toBe(h.runtime.revision);
    feed.stop();
  });
});
