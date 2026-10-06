// WJSS Stage 0.2.1A — Node SSE client + revision-checking mirror (scenario runner / measurement).
// Built-ins only (fetch, TextDecoder, AbortController). SYNTHETIC SPIKE TOOLING.

export class SseMirror {
  // resyncOnGap: mirror the UI contract — on a revision gap discard the Delta, close the
  // stream and reconnect for a fresh Snapshot (no replay). Also reconnects if the server ends
  // the stream. Default false (scenario runner inspects raw gap behaviour itself).
  constructor(url, { headers = {}, onEvent, resyncOnGap = false } = {}) {
    this.url = url;
    this.headers = headers;
    this.onEvent = onEvent;
    this.resyncOnGap = resyncOnGap;
    this.closed = false;
    this.resyncs = 0;
    this.connections = 0;
    this.ctrl = new AbortController();
    this.events = []; // { type, revision, previousRevision, bytes, at, gap? } - metadata only
    this.sensors = new Map();
    this.single = {};
    this.revision = -1;
    this.gaps = 0;
    this.ended = false;
    this.done = this.run();
  }
  close() {
    this.closed = true;
    this.ctrl.abort();
  }
  async run() {
    for (;;) {
      await this.connectOnce();
      if (this.closed || !this.resyncOnGap) break;
      this.ctrl = new AbortController();
      await new Promise((r) => setTimeout(r, 50));
    }
    this.ended = true;
  }
  async connectOnce() {
    this.connections += 1;
    try {
      const res = await fetch(this.url, { headers: this.headers, signal: this.ctrl.signal });
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
          let type = null;
          let data = '';
          for (const line of block.split('\n')) {
            if (line.startsWith('event: ')) type = line.slice(7);
            else if (line.startsWith('data: ')) data += line.slice(6);
          }
          if (!type || !data) continue;
          this.handle(type, data);
        }
      }
    } catch {
      /* aborted */
    }
  }
  handle(type, data) {
    const msg = JSON.parse(data);
    const ev = { type, revision: msg.revision, previousRevision: msg.previousRevision, bytes: Buffer.byteLength(data), at: Date.now() };
    if (type === 'snapshot') {
      this.sensors = new Map(msg.sensors.map((s) => [s.sensorId, s]));
      for (const k of ['config', 'walls', 'activeJob', 'pump', 'queue', 'alarms', 'communication', 'runtime']) this.single[k] = msg[k];
      this.revision = msg.revision;
    } else if (type === 'delta') {
      if (msg.previousRevision !== this.revision) {
        this.gaps += 1;
        ev.gap = true;
        if (this.resyncOnGap) {
          this.resyncs += 1;
          this.revision = -1;
          this.ctrl.abort(); // run() reconnects -> fresh Snapshot
        }
      } else {
        for (const s of msg.sensors ?? []) this.sensors.set(s.sensorId, s);
        for (const k of ['config', 'walls', 'activeJob', 'pump', 'queue', 'alarms', 'communication', 'runtime']) if (k in msg) this.single[k] = msg[k];
        this.revision = msg.revision;
      }
    }
    this.lastMessage = msg;
    // Metadata only (bounded); full messages are not retained.
    this.events.push(ev);
    if (this.events.length > 5000) this.events.splice(0, 1000);
    this.onEvent?.(ev, msg, this);
  }
  sensor(id) {
    return this.sensors.get(id);
  }
  all() {
    return [...this.sensors.values()];
  }
}
