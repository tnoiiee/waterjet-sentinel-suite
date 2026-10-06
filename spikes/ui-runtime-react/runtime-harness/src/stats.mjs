// WJSS Stage 0.2.1A — small statistics helpers (synthetic spike tooling).

export function percentile(values, p) {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const idx = Math.min(sorted.length - 1, Math.max(0, Math.ceil((p / 100) * sorted.length) - 1));
  return sorted[idx];
}

export function summarize(values, digits = 2) {
  if (!values.length) return { n: 0, p50: null, p95: null, max: null, mean: null };
  const r = (v) => (v === null ? null : Number(v.toFixed(digits)));
  const mean = values.reduce((a, b) => a + b, 0) / values.length;
  return { n: values.length, p50: r(percentile(values, 50)), p95: r(percentile(values, 95)), max: r(Math.max(...values)), mean: r(mean) };
}

/** Bounded FIFO ring for numeric samples or objects. */
export class Ring {
  constructor(capacity) {
    this.capacity = capacity;
    this.items = [];
  }
  push(v) {
    this.items.push(v);
    if (this.items.length > this.capacity) this.items.shift();
  }
  toArray() {
    return this.items.slice();
  }
  get length() {
    return this.items.length;
  }
}
