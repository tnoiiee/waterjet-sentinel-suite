// WJSS Stage 0.2.1A — bounded Historian slowdown simulator.
//
// SYNTHETIC SPIKE INFRASTRUCTURE. No database, no SQL, no durable spool.
// The acquisition path calls offer() synchronously and never awaits the consumer, so a slow
// consumer cannot block acquisition, revisions, SSE delivery, queue, alarm, or pump commands.
// Overflow policy for Production remains [OPEN]; the spike rejects new samples when full and
// records a data-gap marker for each contiguous rejection run.

import { performance } from 'node:perf_hooks';

export class HistorianChannel {
  constructor({ capacity, batchSize, nearOverflowRatio, delayMs }) {
    this.capacity = capacity;
    this.batchSize = batchSize;
    this.nearOverflowRatio = nearOverflowRatio;
    this._delayMs = delayMs;
    this.pending = null; // { started, n, fire } - batch currently being "written"
    // Fixed-size circular storage: memory is bounded by capacity.
    this.buf = new Array(capacity);
    this.head = 0;
    this.depth = 0;
    this.accepted = 0;
    this.rejected = 0;
    this.written = 0;
    this.batches = 0;
    this.gapMarkers = [];
    this.inRejectRun = false;
    this.nearOverflowEvents = 0;
    this.wasNear = false;
    this.lastBatchLatencyMs = null;
    this.maxDepth = 0;
    this.timer = null;
    this.running = false;
  }
  get delayMs() {
    return this._delayMs;
  }
  /**
   * Changing the simulated write delay also applies to a batch already in flight: it completes
   * at min(original completion, start + new delay). This models the write backend recovering;
   * without it a 600 s stall would hold the consumer for 600 s after "recovery".
   */
  set delayMs(d) {
    this._delayMs = d;
    if (this.pending && this.running) {
      clearTimeout(this.timer);
      const remaining = Math.max(0, this.pending.started + d - performance.now());
      this.timer = setTimeout(this.pending.fire, remaining);
    }
  }
  get nearOverflow() {
    return this.depth >= this.capacity * this.nearOverflowRatio;
  }
  /** Synchronous, O(1). Never blocks. */
  offer(sample) {
    if (this.depth >= this.capacity) {
      this.rejected += 1;
      if (!this.inRejectRun) {
        this.inRejectRun = true;
        this.gapMarkers.push({ startedAt: new Date().toISOString(), endedAt: null, rejected: 0 });
        if (this.gapMarkers.length > 100) this.gapMarkers.shift();
      }
      this.gapMarkers[this.gapMarkers.length - 1].rejected += 1;
      return false;
    }
    if (this.inRejectRun) {
      this.inRejectRun = false;
      this.gapMarkers[this.gapMarkers.length - 1].endedAt = new Date().toISOString();
    }
    this.buf[(this.head + this.depth) % this.capacity] = sample;
    this.depth += 1;
    this.accepted += 1;
    if (this.depth > this.maxDepth) this.maxDepth = this.depth;
    const near = this.nearOverflow;
    if (near && !this.wasNear) this.nearOverflowEvents += 1;
    this.wasNear = near;
    return true;
  }
  take(n) {
    const count = Math.min(n, this.depth);
    for (let i = 0; i < count; i += 1) this.buf[(this.head + i) % this.capacity] = undefined;
    this.head = (this.head + count) % this.capacity;
    this.depth -= count;
    this.wasNear = this.nearOverflow;
    return count;
  }
  start() {
    this.running = true;
    const loop = () => {
      if (!this.running) return;
      if (this.depth === 0) {
        this.timer = setTimeout(loop, 50);
        return;
      }
      const started = performance.now();
      const n = Math.min(this.batchSize, this.depth);
      // Simulated slow write: the batch is only removed after the configured delay.
      const fire = () => {
        this.pending = null;
        this.take(n);
        this.batches += 1;
        this.written += n;
        this.lastBatchLatencyMs = performance.now() - started;
        loop();
      };
      this.pending = { started, n, fire };
      this.timer = setTimeout(fire, this._delayMs);
    };
    loop();
  }
  stop() {
    this.running = false;
    clearTimeout(this.timer);
  }
  health() {
    return {
      depth: this.depth,
      capacity: this.capacity,
      nearOverflow: this.nearOverflow,
      rejected: this.rejected,
      lastBatchLatencyMs: this.lastBatchLatencyMs === null ? null : Math.round(this.lastBatchLatencyMs),
      delayMs: this.delayMs,
      gapMarkers: this.gapMarkers.length,
    };
  }
}
