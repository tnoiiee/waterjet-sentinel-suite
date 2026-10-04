// WJSS Stage 0.2.1A — fixed-capacity ring buffer (bounded; never grows past capacity).

export class RingBuffer<T> {
  private readonly buf: (T | undefined)[];
  private start = 0;
  private count = 0;

  constructor(readonly capacity: number) {
    if (!Number.isInteger(capacity) || capacity < 1) throw new Error('capacity must be a positive integer');
    this.buf = new Array<T | undefined>(capacity);
  }

  get length(): number {
    return this.count;
  }

  push(item: T): void {
    if (this.count < this.capacity) {
      this.buf[(this.start + this.count) % this.capacity] = item;
      this.count += 1;
    } else {
      this.buf[this.start] = item;
      this.start = (this.start + 1) % this.capacity;
    }
  }

  clear(): void {
    this.buf.fill(undefined);
    this.start = 0;
    this.count = 0;
  }

  last(): T | undefined {
    return this.count ? this.buf[(this.start + this.count - 1) % this.capacity] : undefined;
  }

  toArray(): T[] {
    const out = new Array<T>(this.count);
    for (let i = 0; i < this.count; i += 1) out[i] = this.buf[(this.start + i) % this.capacity] as T;
    return out;
  }
}
