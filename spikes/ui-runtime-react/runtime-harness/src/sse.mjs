// WJSS Stage 0.2.1A — Server-Sent Events publisher (spike transport only).
//
// SSE is used for the spike only. The Production transport remains [OPEN].
// Rules:
//   - every connection (initial or reconnect) receives a fresh Snapshot first;
//     Last-Event-ID is ignored: no replay of missed Deltas, no command replay
//   - Deltas are broadcast in revision order
//   - a client whose socket buffer exceeds the bound is closed (it reconnects and
//     receives a Snapshot)

export class SsePublisher {
  constructor({ maxBufferedBytes, heartbeatMs }) {
    this.clients = new Set();
    this.maxBufferedBytes = maxBufferedBytes;
    this.heartbeatMs = heartbeatMs;
    this.stats = { connections: 0, reconnectsWithLastEventId: 0, slowClientDisconnects: 0, messagesSent: 0, bytesSent: 0, droppedByScenario: 0 };
    this.skipNextBroadcast = false;
    this.hb = setInterval(() => this.writeAll(': heartbeat\n\n'), heartbeatMs);
  }
  accept(req, res, snapshotJson, revision) {
    res.writeHead(200, {
      'Content-Type': 'text/event-stream; charset=utf-8',
      'Cache-Control': 'no-cache, no-transform',
      Connection: 'keep-alive',
      'X-Content-Type-Options': 'nosniff',
    });
    this.stats.connections += 1;
    if (req.headers['last-event-id']) this.stats.reconnectsWithLastEventId += 1;
    const client = { res, openedAt: Date.now() };
    this.clients.add(client);
    req.on('close', () => this.clients.delete(client));
    this.write(client, `retry: 1000\n\n`);
    this.write(client, `event: snapshot\nid: ${revision}\ndata: ${snapshotJson}\n\n`);
  }
  write(client, chunk) {
    if (client.res.destroyed) {
      this.clients.delete(client);
      return;
    }
    client.res.write(chunk);
    this.stats.messagesSent += 1;
    this.stats.bytesSent += Buffer.byteLength(chunk);
    if (client.res.writableLength > this.maxBufferedBytes) {
      this.stats.slowClientDisconnects += 1;
      this.clients.delete(client);
      client.res.destroy();
    }
  }
  writeAll(chunk) {
    for (const c of [...this.clients]) this.write(c, chunk);
  }
  broadcastDelta(revision, json) {
    if (this.skipNextBroadcast) {
      // Scenario: simulate one lost Delta so clients observe a revision gap.
      this.skipNextBroadcast = false;
      this.stats.droppedByScenario += 1;
      return;
    }
    this.writeAll(`event: delta\nid: ${revision}\ndata: ${json}\n\n`);
  }
  dropAll() {
    const n = this.clients.size;
    for (const c of [...this.clients]) c.res.destroy();
    this.clients.clear();
    return n;
  }
  close() {
    clearInterval(this.hb);
    this.dropAll();
  }
}
