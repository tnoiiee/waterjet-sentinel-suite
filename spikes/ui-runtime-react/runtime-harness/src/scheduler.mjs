// WJSS Stage 0.2.1A — synthetic device sessions and poll scheduler.
//
// SYNTHETIC SPIKE INFRASTRUCTURE. Simulates Modbus-like polling semantics only; no network
// I/O, no Modbus library, no real device. Rules exercised:
//   - one serialized request queue per device session (never two in flight per device)
//   - bounded concurrency across devices (semaphore)
//   - Fast / Medium / Slow poll groups; identical pending requests are coalesced
//   - timeout injection and recovery; a late response after timeout is discarded

import { performance } from 'node:perf_hooks';

class Semaphore {
  constructor(permits) {
    this.permits = permits;
    this.waiters = [];
  }
  acquire() {
    if (this.permits > 0) {
      this.permits -= 1;
      return Promise.resolve();
    }
    return new Promise((resolve) => this.waiters.push(resolve));
  }
  release() {
    const next = this.waiters.shift();
    if (next) next();
    else this.permits += 1;
  }
}

/** Simulated device. Responds after a seeded latency unless a fault is injected. */
export class SimDevice {
  constructor(deviceId, { rng, latencyMinMs, latencyMaxMs, registerSource }) {
    this.deviceId = deviceId;
    this.rng = rng;
    this.latencyMinMs = latencyMinMs;
    this.latencyMaxMs = latencyMaxMs;
    this.registerSource = registerSource;
    this.timeoutFault = false;
    this.staleFault = false;
    this.frozen = null;
    this.timers = new Set();
  }
  read(entry) {
    const latency = this.rng.range(this.latencyMinMs, this.latencyMaxMs);
    return new Promise((resolve) => {
      if (this.timeoutFault) return; // no response: the session's timeout fires
      const t = setTimeout(() => {
        this.timers.delete(t);
        if (this.staleFault) {
          if (!this.frozen?.[entry.id]) {
            this.frozen = this.frozen ?? {};
            this.frozen[entry.id] = { registers: this.registerSource(entry), sourceTimestamp: Date.now() };
          }
          const f = this.frozen[entry.id];
          resolve({ registers: f.registers, sourceTimestamp: f.sourceTimestamp });
          return;
        }
        resolve({ registers: this.registerSource(entry), sourceTimestamp: Date.now() });
      }, latency);
      this.timers.add(t);
    });
  }
  setStaleFault(on) {
    this.staleFault = on;
    if (!on) this.frozen = null;
  }
  dispose() {
    for (const t of this.timers) clearTimeout(t);
    this.timers.clear();
  }
}

export class DeviceSession {
  constructor(device, scheduler) {
    this.device = device;
    this.deviceId = device.deviceId;
    this.scheduler = scheduler;
    this.queue = [];
    this.busy = false;
    this.inFlight = 0;
    this.consecutiveTimeouts = 0;
    this.lastSuccessAt = null;
    this.lastLatencyMs = null;
    this.pollsOk = 0;
    this.pollsFailed = 0;
    this.coalesced = 0;
    this.everTimedOut = false;
  }
  enqueue(entry) {
    if (this.queue.some((q) => q.id === entry.id)) {
      this.coalesced += 1;
      return;
    }
    this.queue.push(entry);
    this.pump();
  }
  async pump() {
    if (this.busy || this.queue.length === 0 || this.scheduler.stopped) return;
    this.busy = true;
    await this.scheduler.semaphore.acquire();
    if (this.scheduler.stopped) {
      this.scheduler.semaphore.release();
      this.busy = false;
      return;
    }
    const entry = this.queue.shift();
    this.inFlight += 1;
    this.scheduler.globalInFlight += 1;
    this.scheduler.observeInFlight(this);
    const started = performance.now();
    let timer;
    const timeout = new Promise((resolve) => {
      timer = setTimeout(() => resolve({ timedOut: true }), this.scheduler.requestTimeoutMs);
    });
    const result = await Promise.race([this.device.read(entry), timeout]);
    clearTimeout(timer);
    const latency = performance.now() - started;
    this.inFlight -= 1;
    this.scheduler.globalInFlight -= 1;
    this.scheduler.semaphore.release();
    if (result.timedOut) {
      this.consecutiveTimeouts += 1;
      this.pollsFailed += 1;
      this.everTimedOut = true;
      this.scheduler.onTimeout(this, entry, latency);
    } else {
      const recovered = this.consecutiveTimeouts > 0;
      this.consecutiveTimeouts = 0;
      this.pollsOk += 1;
      this.lastSuccessAt = Date.now();
      this.lastLatencyMs = latency;
      this.scheduler.onResult(this, entry, result, latency, recovered);
    }
    this.busy = false;
    this.pump();
  }
}

export class PollScheduler {
  constructor({ devices, plan, params, onResult, onTimeout }) {
    this.params = params;
    this.requestTimeoutMs = params.requestTimeoutMs;
    this.semaphore = new Semaphore(params.concurrency);
    this.sessions = new Map(devices.map((d) => [d.deviceId, new DeviceSession(d, this)]));
    this.plan = plan;
    this.onResultCb = onResult;
    this.onTimeoutCb = onTimeout;
    this.globalInFlight = 0;
    this.maxGlobalInFlight = 0;
    this.maxPerDeviceInFlight = 0;
    this.latency = { FAST: [], MEDIUM: [], SLOW: [] };
    this.timeouts = 0;
    this.intervals = [];
    this.stopped = false;
  }
  observeInFlight(session) {
    this.maxGlobalInFlight = Math.max(this.maxGlobalInFlight, this.globalInFlight);
    this.maxPerDeviceInFlight = Math.max(this.maxPerDeviceInFlight, session.inFlight);
  }
  recordLatency(group, ms) {
    const arr = this.latency[group];
    arr.push(ms);
    if (arr.length > 2000) arr.shift();
  }
  onResult(session, entry, result, latency, recovered) {
    this.recordLatency(entry.pollGroup, latency);
    this.onResultCb(session, entry, result, recovered);
  }
  onTimeout(session, entry, latency) {
    this.timeouts += 1;
    this.recordLatency(entry.pollGroup, latency);
    this.onTimeoutCb(session, entry);
  }
  enqueueGroup(group) {
    for (const entry of this.plan) if (entry.pollGroup === group) this.sessions.get(entry.deviceId).enqueue(entry);
  }
  start() {
    this.enqueueGroup('FAST');
    this.enqueueGroup('MEDIUM');
    this.enqueueGroup('SLOW');
    const p = this.params;
    this.intervals.push(setInterval(() => this.enqueueGroup('FAST'), p.fastMs));
    this.intervals.push(setInterval(() => this.enqueueGroup('MEDIUM'), p.mediumMs));
    this.intervals.push(setInterval(() => this.enqueueGroup('SLOW'), p.slowMs));
  }
  stop() {
    this.stopped = true;
    for (const i of this.intervals) clearInterval(i);
    for (const s of this.sessions.values()) s.device.dispose();
  }
}
