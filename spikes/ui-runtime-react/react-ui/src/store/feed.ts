// WJSS Stage 0.2.1A — SSE feed client (spike transport only; Production transport [OPEN]).
//
// Rules:
//   - first message of every connection is a Snapshot -> full store replacement
//   - Deltas apply only when revisions chain; on a gap the stream is closed and reopened
//     so that a fresh authoritative Snapshot is received
//   - on disconnect the UI keeps the last received state, marks it DISCONNECTED, and
//     infers nothing; there is no outbound command queue and nothing is replayed

import type { OperationalDelta, OperationalSnapshot } from '../../../contracts/operational';
import type { PresentationStore } from './presentationStore';

export interface MessageEventLike {
  data: string;
}
export interface EventSourceLike {
  readonly readyState: number;
  onopen: ((ev: unknown) => void) | null;
  onerror: ((ev: unknown) => void) | null;
  addEventListener(type: 'snapshot' | 'delta', listener: (ev: MessageEventLike) => void): void;
  close(): void;
}
export type EventSourceFactory = (url: string) => EventSourceLike;

const CLOSED = 2;

export interface FeedOptions {
  url?: string;
  createEventSource?: EventSourceFactory;
  reopenDelayMs?: number;
}

export class SseFeed {
  private es: EventSourceLike | null = null;
  private stopped = true;
  private reopenTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly url: string;
  private readonly create: EventSourceFactory;
  private readonly reopenDelayMs: number;
  /** Count of EventSource instances opened (initial + resync + reopen). */
  opened = 0;

  constructor(
    private readonly store: PresentationStore,
    opts: FeedOptions = {},
  ) {
    this.url = opts.url ?? '/api/stream';
    this.create = opts.createEventSource ?? ((u) => new EventSource(u) as unknown as EventSourceLike);
    this.reopenDelayMs = opts.reopenDelayMs ?? 1000;
  }

  start(): void {
    this.stopped = false;
    this.open();
  }

  stop(): void {
    this.stopped = true;
    if (this.reopenTimer) clearTimeout(this.reopenTimer);
    this.es?.close();
    this.es = null;
  }

  private open(): void {
    if (this.stopped) return;
    const es = this.create(this.url);
    this.es = es;
    this.opened += 1;
    es.addEventListener('snapshot', (ev) => {
      if (es !== this.es) return;
      this.store.applySnapshot(JSON.parse(ev.data) as OperationalSnapshot, ev.data.length);
    });
    es.addEventListener('delta', (ev) => {
      if (es !== this.es) return;
      const result = this.store.applyDelta(JSON.parse(ev.data) as OperationalDelta, ev.data.length);
      if (result === 'gap') this.resync();
    });
    es.onerror = () => {
      if (es !== this.es) return;
      const c = this.store.getSlice('connection');
      this.store.setConnection({ state: 'DISCONNECTED', reconnects: c.state === 'DISCONNECTED' ? c.reconnects : c.reconnects + 1 });
      // Native EventSource retries by itself while CONNECTING. If it gave up, reopen.
      if (es.readyState === CLOSED) this.scheduleReopen();
    };
  }

  /** Revision gap: discard incremental application and obtain a fresh Snapshot. */
  resync(): void {
    const c = this.store.getSlice('connection');
    this.store.setConnection({ state: 'RESYNCING', gaps: c.gaps + 1 });
    this.es?.close();
    this.es = null;
    this.open();
  }

  private scheduleReopen(): void {
    if (this.reopenTimer || this.stopped) return;
    this.es?.close();
    this.es = null;
    this.reopenTimer = setTimeout(() => {
      this.reopenTimer = null;
      this.open();
    }, this.reopenDelayMs);
  }
}
