// WJSS Stage 0.2.1A — Synthetic authoritative runtime state.
//
// SYNTHETIC SPIKE INFRASTRUCTURE — NOT THE PRODUCTION EQUIPMENT RUNTIME.
// The approved Production direction (.NET Equipment Runtime Windows Service, ASP.NET Core
// Local Application API) is unchanged and NOT VERIFIED by this harness.
//
// Responsibilities: synthetic process model, acquisition results, quality, classification
// (via contracts/classify.mjs), synthetic queue and single-job model, pump model, alarms,
// immutable published configuration revisions, bounded trend, Snapshot / Delta generation
// with one monotonic revision, invariant monitoring, and runtime metrics.

import { EventEmitter } from 'node:events';
import { performance, monitorEventLoopDelay } from 'node:perf_hooks';
import { classifySensor } from '../../contracts/classify.mjs';
import { DEFAULT_PARAMS, SYNTHETIC_LABEL, buildDevices, buildPollPlan, buildSensors, WALL_COUNTS } from './config.mjs';
import { createRng } from './rng.mjs';
import { PollScheduler, SimDevice } from './scheduler.mjs';
import { HistorianChannel } from './historian.mjs';
import { Ring, summarize } from './stats.mjs';

const SNAPSHOT_SCHEMA = 'wjss.spike.snapshot/1';
const DELTA_SCHEMA = 'wjss.spike.delta/1';
const PHASES = [
  ['P1', 'Pre-check (synthetic)'],
  ['P2', 'Isolation valve open (synthetic)'],
  ['P3', 'Jet positioning (synthetic)'],
  ['P4', 'Jetting (synthetic)'],
  ['P5', 'Verification (synthetic)'],
  ['P6', 'Close-out (synthetic)'],
];
const SERIES_NAMES = ['Pump discharge', 'Header', 'Manifold A', 'Manifold B'];
const ALARM_RANK = { NONE: 0, CLEARED_UNACK: 1, ACTIVE_ACK: 2, ACTIVE_UNACK: 3 };
const iso = (ms) => (ms === null || ms === undefined ? null : new Date(ms).toISOString());
const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));
const round1 = (v) => Math.round(v * 10) / 10;

export class SyntheticRuntime extends EventEmitter {
  constructor(overrides = {}) {
    super();
    const p = Object.freeze({ ...DEFAULT_PARAMS, ...overrides });
    this.params = p;
    this.rng = createRng(p.seed);
    this.startedAt = Date.now();
    this.sensors = buildSensors();
    this.sensorById = new Map(this.sensors.map((s) => [s.sensorId, s]));
    this.plan = buildPollPlan(this.sensors);
    this.dirtyMode = 'normal';
    this.tickCount = 0;

    // Synthetic process model (deterministic from the seed).
    this.proc = new Map();
    for (const s of this.sensors) {
      const dirtyAtStart = this.rng.next() < 0.15;
      this.proc.set(s.sensorId, {
        trueScore: dirtyAtStart ? this.rng.range(55, 80) : this.rng.range(10, 45),
        target: null,
        oscillate: false,
        lastCleanAt: this.startedAt - Math.round(this.rng.range(600, 86400) * 1000),
      });
    }
    // Acquisition-side state per Sensor.
    this.acq = new Map(
      this.sensors.map((s) => [
        s.sensorId,
        { rawScore: null, sourceTs: null, lastValidatedScore: null, lastValidatedAt: null, forcedQuality: null, disabled: false, cur: null },
      ]),
    );
    this.pressure = { values: [null, null, null, null], sourceTs: null };
    this.pump = { state: 'RUNNING', changedAt: this.startedAt, stopRequestedAt: null };
    this.queueOrder = [];
    this.queueReason = new Map();
    this.queueHeld = false;
    this.activeJobs = [];
    this.jobSeq = 0;
    this.jobsCompleted = 0;
    this.jobsAborted = 0;
    this.refusedSecondJobs = 0;
    this.acceptedSecondJobs = 0;
    this.jobRefusals = {};
    this.autoJobs = p.autoJobs;
    this.alarms = new Map();
    this.alarmSeq = 0;
    this.config = Object.freeze({
      revision: 1,
      publishedAt: iso(this.startedAt),
      dirtyThreshold: p.dirtyThreshold,
      staleThresholdMs: p.staleThresholdMs,
      label: SYNTHETIC_LABEL,
    });
    this.configHistory = [this.config];
    this.trend = new Ring(p.trendCapacity);
    this.historian = new HistorianChannel({
      capacity: p.historianCapacity,
      batchSize: p.historianBatchSize,
      nearOverflowRatio: p.historianNearOverflowRatio,
      delayMs: p.historianDelayMs,
    });

    // Publication state.
    this.revision = 0;
    this.pubSensors = new Map(); // sensorId -> { rec, json }
    this.pubSingle = {}; // key -> { value, json }
    this.lastInvariantRevision = 0;
    this.violations = [];
    this.violationCount = 0;
    this.publishPending = false;

    // Metrics.
    this.metrics = {
      publishMs: new Ring(3600),
      deltaBytes: new Ring(3600),
      deltaSensorCounts: new Ring(3600),
      snapshotBytes: new Ring(200),
      deltasPublished: 0,
      snapshotsBuilt: 0,
      commandLatencyMs: {},
      pumpStopLatencyMs: new Ring(100),
    };
    this.loopDelay = monitorEventLoopDelay({ resolution: 10 });
    this.lastCpu = { usage: process.cpuUsage(), at: performance.now() };

    // Devices and scheduler.
    const latRng = createRng(p.seed + 1);
    const devices = buildDevices().map(
      (d) =>
        new SimDevice(d.deviceId, {
          rng: latRng,
          latencyMinMs: p.deviceLatencyMinMs,
          latencyMaxMs: p.deviceLatencyMaxMs,
          registerSource: (entry) => this.registerSource(entry),
        }),
    );
    this.devices = new Map(devices.map((d) => [d.deviceId, d]));
    this.scheduler = new PollScheduler({
      devices,
      plan: this.plan,
      params: p,
      onResult: (session, entry, result, recovered) => this.onPollResult(session, entry, result, recovered),
      onTimeout: (session) => this.onPollTimeout(session),
    });
  }

  // ------------------------------------------------------------------ lifecycle
  start() {
    this.loopDelay.enable();
    this.historian.start();
    this.scheduler.start();
    this.publishTimer = setInterval(() => this.publish({ tick: true }), this.params.publishIntervalMs);
  }
  stop() {
    clearInterval(this.publishTimer);
    this.scheduler.stop();
    this.historian.stop();
    this.loopDelay.disable();
  }

  // ------------------------------------------------------------------ synthetic devices
  pumpLevel(now) {
    const { state, changedAt } = this.pump;
    if (state === 'RUNNING') return 1;
    if (state === 'STOPPED') return 0;
    if (state === 'STARTING') return clamp((now - changedAt) / this.params.pumpStartMs, 0, 1);
    return clamp(1 - (now - changedAt) / this.params.pumpStopMs, 0, 1);
  }
  registerSource(entry) {
    const regs = new Array(entry.quantity).fill(0);
    if (entry.functionCategory === 'INPUT_REGISTERS' && entry.deviceId.startsWith('SYN-TC-')) {
      for (const s of this.sensors) {
        if (s.deviceId !== entry.deviceId) continue;
        const score = this.proc.get(s.sensorId).trueScore;
        s.channelOffsets.forEach((off) => {
          const temp = 100 + score * 1.5 + this.rng.range(-0.3, 0.3);
          regs[off] = Math.round(temp * 10);
        });
      }
    } else if (entry.id === 'SYN-PIO-01/fast/pressure') {
      const level = this.pumpLevel(Date.now());
      const jetting = this.activeJobs[0] && this.currentPhaseIndex(this.activeJobs[0], Date.now()) === 3;
      const offsets = [0, -3, jetting ? -12 : -6, -5];
      for (let i = 0; i < 4; i += 1) {
        const v = level * (this.params.pumpSetpoint + offsets[i]) + this.rng.range(-1.5, 1.5) * level;
        regs[i] = Math.max(0, Math.round(v * 10));
      }
    }
    return regs;
  }
  onPollResult(session, entry, result, recovered) {
    if (entry.functionCategory === 'INPUT_REGISTERS' && entry.deviceId.startsWith('SYN-TC-')) {
      for (const s of this.sensors) {
        if (s.deviceId !== entry.deviceId) continue;
        const [a, b] = s.channelOffsets.map((off) => result.registers[off] * 0.1);
        const score = round1(clamp(((a + b) / 2 - 100) / 1.5, 0, 100));
        const acq = this.acq.get(s.sensorId);
        acq.rawScore = score;
        acq.sourceTs = result.sourceTimestamp;
        this.historian.offer({ t: result.sourceTimestamp, ch: s.tcChannels[0], v: a });
        this.historian.offer({ t: result.sourceTimestamp, ch: s.tcChannels[1], v: b });
      }
    } else if (entry.id === 'SYN-PIO-01/fast/pressure') {
      this.pressure.values = result.registers.map((r) => round1(r * 0.1));
      this.pressure.sourceTs = result.sourceTimestamp;
      this.pressure.values.forEach((v, i) => this.historian.offer({ t: result.sourceTimestamp, ch: `pressure:${i}`, v }));
    }
    if (recovered) this.clearDeviceAlarm(session.deviceId);
  }
  onPollTimeout(session) {
    if (session.consecutiveTimeouts === 1) {
      const exists = [...this.alarms.values()].some((a) => a.deviceId === session.deviceId && a.code === 'SYN-COMM-TIMEOUT' && a.state !== 'CLEARED_UNACK');
      if (!exists) this.raiseAlarm({ code: 'SYN-COMM-TIMEOUT', text: `Synthetic communication timeout ${session.deviceId}`, severity: 'MEDIUM', deviceId: session.deviceId });
    }
  }

  // ------------------------------------------------------------------ process model
  applyDirtyMode(mode) {
    this.dirtyMode = mode;
    const order = this.sensors.map((s) => s.sensorId);
    // deterministic shuffle from a mode-specific seed
    const r = createRng(this.params.seed + (mode === 'dirty30' ? 30 : mode === 'dirty70' ? 70 : 7));
    for (let i = order.length - 1; i > 0; i -= 1) {
      const j = Math.floor(r.next() * (i + 1));
      [order[i], order[j]] = [order[j], order[i]];
    }
    const frac = mode === 'dirty30' ? 0.3 : mode === 'dirty70' ? 0.7 : 0;
    const nDirty = Math.round(order.length * frac);
    order.forEach((id, i) => {
      const pr = this.proc.get(id);
      pr.oscillate = false;
      if (mode === 'dirty30' || mode === 'dirty70') {
        pr.target = i < nDirty ? r.range(60, 90) : r.range(10, 40);
        pr.trueScore = pr.target;
      } else {
        pr.target = null;
      }
    });
    if (mode === 'oscillate') {
      for (const s of this.sensors) if (s.index <= 2) this.proc.get(s.sensorId).oscillate = true;
    }
  }
  advanceProcess() {
    const t = this.tickCount;
    for (const s of this.sensors) {
      const pr = this.proc.get(s.sensorId);
      if (this.activeJobs[0]?.targetSensorId === s.sensorId) {
        if (this.currentPhaseIndex(this.activeJobs[0], Date.now()) === 3) pr.trueScore = pr.trueScore * 0.8;
        continue;
      }
      if (pr.oscillate) {
        pr.trueScore = 50 + 2 * Math.sin(t * 1.3 + s.globalIndex);
      } else if (pr.target !== null) {
        pr.trueScore = clamp(pr.target + this.rng.range(-0.4, 0.4), 0, 100);
      } else {
        pr.trueScore = clamp(pr.trueScore + this.rng.range(-0.3, 0.42), 0, 100);
      }
    }
  }

  // ------------------------------------------------------------------ quality + classification
  computeQuality(s, now) {
    const acq = this.acq.get(s.sensorId);
    const session = this.scheduler.sessions.get(s.deviceId);
    const p = this.params;
    if (acq.forcedQuality) return [acq.forcedQuality, 'SCENARIO_FORCED'];
    if (acq.disabled) return ['DISABLED', 'DISABLED_BY_SYNTHETIC_CONFIG'];
    if (session.consecutiveTimeouts >= p.badAfterTimeouts) return ['BAD', 'DEVICE_TIMEOUT'];
    if (acq.sourceTs === null) return ['BAD', 'NO_DATA'];
    if (now - acq.sourceTs > this.config.staleThresholdMs) return ['STALE', 'SOURCE_AGE_EXCEEDED'];
    if (session.consecutiveTimeouts >= p.uncertainAfterTimeouts) return ['UNCERTAIN', 'DEVICE_TIMEOUT_RECENT'];
    return ['GOOD', null];
  }
  classifyAll(now) {
    const threshold = this.config.dirtyThreshold;
    for (const s of this.sensors) {
      const acq = this.acq.get(s.sensorId);
      const [quality, reason] = this.computeQuality(s, now);
      const c = classifySensor({ quality, score: acq.rawScore, lastValidatedScore: acq.lastValidatedScore, threshold });
      if (c.updatesLastValidated) {
        acq.lastValidatedScore = acq.rawScore;
        acq.lastValidatedAt = acq.sourceTs;
      }
      acq.cur = { quality, reason, classification: c.classification, basis: c.basis };
    }
  }

  // ------------------------------------------------------------------ queue + job
  sensorAlarmState(sensorId) {
    let best = 'NONE';
    let sev = null;
    for (const a of this.alarms.values()) {
      if (a.sensorId !== sensorId) continue;
      if (ALARM_RANK[a.state] > ALARM_RANK[best]) {
        best = a.state;
        sev = a.severity;
      }
    }
    return [best, sev];
  }
  pumpReady() {
    const v = this.pressure.values[0];
    const pio = this.scheduler.sessions.get('SYN-PIO-01');
    return this.pump.state === 'RUNNING' && pio.consecutiveTimeouts === 0 && v !== null && Math.abs(v - this.params.pumpSetpoint) <= this.params.pumpReadyBand;
  }
  entryStatus(sensorId) {
    const cur = this.acq.get(sensorId).cur;
    if (!cur || cur.quality !== 'GOOD') return 'EXCLUDED';
    if (this.sensorAlarmState(sensorId)[0] !== 'NONE') return 'BLOCKED';
    if (this.queueHeld || !this.pumpReady()) return 'HELD';
    return 'READY';
  }
  updateQueue() {
    const target = this.activeJobs[0]?.targetSensorId;
    for (const s of this.sensors) {
      const cur = this.acq.get(s.sensorId).cur;
      const inQ = this.queueReason.has(s.sensorId);
      if (s.sensorId === target) continue;
      if (cur.basis === 'CURRENT' && cur.classification === 'DIRTY' && !inQ) {
        this.queueOrder.push(s.sensorId);
        this.queueReason.set(s.sensorId, 'SYN_DIRTY_SCORE_ABOVE_THRESHOLD');
      } else if (cur.basis === 'CURRENT' && cur.classification === 'CLEANER' && inQ && this.queueReason.get(s.sensorId) !== 'SYN_OPERATOR_REQUEST') {
        this.removeFromQueue(s.sensorId);
      }
    }
  }
  removeFromQueue(sensorId) {
    if (!this.queueReason.has(sensorId)) return;
    this.queueReason.delete(sensorId);
    this.queueOrder.splice(this.queueOrder.indexOf(sensorId), 1);
  }
  currentPhaseIndex(job, now) {
    return Math.floor((now - job.startedAtMs) / this.params.jobPhaseMs);
  }
  startJob(sensorId, origin) {
    if (this.activeJobs.length >= 1) {
      this.refusedSecondJobs += 1;
      return { accepted: false, reason: 'ACTIVE_JOB_EXISTS' };
    }
    const refuse = (reason) => {
      this.jobRefusals[reason] = (this.jobRefusals[reason] ?? 0) + 1;
      return { accepted: false, reason };
    };
    const s = this.sensorById.get(sensorId);
    if (!s) return refuse('UNKNOWN_SENSOR');
    if (!this.pumpReady()) return refuse('PUMP_NOT_READY');
    const cur = this.acq.get(sensorId).cur;
    if (cur && cur.quality !== 'GOOD') return refuse('SENSOR_QUALITY_NOT_GOOD');
    if (this.sensorAlarmState(sensorId)[0] !== 'NONE') return refuse('SENSOR_BLOCKED_BY_ALARM');
    const now = Date.now();
    this.jobSeq += 1;
    this.activeJobs.push({ jobId: `SYN-JOB-${String(this.jobSeq).padStart(4, '0')}`, targetSensorId: sensorId, jetId: s.jetId, valveId: s.valveId, origin, startedAtMs: now });
    this.removeFromQueue(sensorId);
    return { accepted: true, reason: null, detail: { jobId: this.activeJobs[0].jobId } };
  }
  abortJob(reason) {
    if (!this.activeJobs.length) return false;
    this.activeJobs.length = 0;
    this.jobsAborted += 1;
    this.lastJobEnd = reason;
    return true;
  }
  advanceJob(now) {
    const job = this.activeJobs[0];
    if (!job) return;
    if (this.currentPhaseIndex(job, now) >= PHASES.length) {
      const pr = this.proc.get(job.targetSensorId);
      pr.trueScore = this.rng.range(5, 15);
      if (pr.target !== null) pr.target = pr.trueScore;
      pr.lastCleanAt = now;
      this.activeJobs.length = 0;
      this.jobsCompleted += 1;
      this.lastJobEnd = 'COMPLETED';
    }
  }
  autoStart() {
    if (!this.autoJobs || this.activeJobs.length) return;
    const head = this.queueOrder.find((id) => this.entryStatus(id) === 'READY');
    if (head) this.startJob(head, 'SYN_AUTO_SEQUENCE');
  }
  advancePump(now) {
    if (this.pump.state === 'STARTING' && now - this.pump.changedAt >= this.params.pumpStartMs) this.setPump('RUNNING', now);
    if (this.pump.state === 'STOPPING' && now - this.pump.changedAt >= this.params.pumpStopMs) this.setPump('STOPPED', now);
  }
  setPump(state, now) {
    this.pump.state = state;
    this.pump.changedAt = now;
  }

  // ------------------------------------------------------------------ alarms
  raiseAlarm({ code, text, severity = 'HIGH', sensorId = null, deviceId = null }) {
    this.alarmSeq += 1;
    const alarmId = `SYN-ALM-${String(this.alarmSeq).padStart(4, '0')}`;
    this.alarms.set(alarmId, { alarmId, code, text, severity, state: 'ACTIVE_UNACK', sensorId, deviceId, raisedAt: iso(Date.now()) });
    return alarmId;
  }
  ackAlarm(alarmId) {
    const targets = alarmId ? [this.alarms.get(alarmId)].filter(Boolean) : [...this.alarms.values()];
    for (const a of targets) {
      if (a.state === 'ACTIVE_UNACK') a.state = 'ACTIVE_ACK';
      else if (a.state === 'CLEARED_UNACK') this.alarms.delete(a.alarmId);
    }
    return targets.length;
  }
  clearAlarm(alarmId) {
    const targets = alarmId ? [this.alarms.get(alarmId)].filter(Boolean) : [...this.alarms.values()];
    for (const a of targets) {
      if (a.state === 'ACTIVE_UNACK') a.state = 'CLEARED_UNACK';
      else if (a.state === 'ACTIVE_ACK') this.alarms.delete(a.alarmId);
    }
    return targets.length;
  }
  clearDeviceAlarm(deviceId) {
    for (const a of this.alarms.values()) if (a.deviceId === deviceId && a.state !== 'CLEARED_UNACK') this.clearAlarm(a.alarmId);
  }

  // ------------------------------------------------------------------ presentation builders
  buildSensorRecord(s) {
    const acq = this.acq.get(s.sensorId);
    const { quality, reason, classification, basis } = acq.cur;
    const job = this.activeJobs[0];
    const isTarget = job?.targetSensorId === s.sensorId;
    const [alarmState, alarmSeverity] = this.sensorAlarmState(s.sensorId);
    return {
      sensorId: s.sensorId,
      wall: s.wall,
      index: s.index,
      deviceId: s.deviceId,
      tcChannels: s.tcChannels,
      dirtyScore: quality === 'BAD' || quality === 'DISABLED' ? null : acq.rawScore,
      lastValidatedScore: acq.lastValidatedScore,
      lastValidatedAt: iso(acq.lastValidatedAt),
      classification,
      classificationBasis: basis,
      quality,
      qualityReason: reason,
      sourceTimestamp: iso(acq.sourceTs),
      queueState: isTarget ? 'ACTIVE' : this.queueReason.has(s.sensorId) ? this.entryStatus(s.sensorId) : 'NONE',
      isActiveJobTarget: isTarget,
      alarmState,
      alarmSeverity,
    };
  }
  buildWalls(records) {
    return WALL_COUNTS.map(([wall]) => {
      const rs = records.filter((r) => r.wall === wall);
      const scores = rs.map((r) => r.dirtyScore).filter((v) => v !== null);
      return {
        wall,
        total: rs.length,
        dirty: rs.filter((r) => r.classification === 'DIRTY').length,
        cleaner: rs.filter((r) => r.classification === 'CLEANER').length,
        notClassified: rs.filter((r) => r.classification === 'NOT_CLASSIFIED').length,
        uncertain: rs.filter((r) => r.quality === 'UNCERTAIN').length,
        maxScore: scores.length ? Math.max(...scores) : null,
      };
    });
  }
  buildActiveJob(now) {
    const job = this.activeJobs[0];
    if (!job) return null;
    const idx = clamp(this.currentPhaseIndex(job, now), 0, PHASES.length - 1);
    const phaseStart = job.startedAtMs + idx * this.params.jobPhaseMs;
    return {
      jobId: job.jobId,
      targetSensorId: job.targetSensorId,
      jetId: job.jetId,
      valveId: job.valveId,
      phase: PHASES[idx][0],
      phaseLabel: PHASES[idx][1],
      phaseIndex: idx,
      startedAt: iso(job.startedAtMs),
      phaseStartedAt: iso(phaseStart),
      phaseProgress: Math.round(clamp((now - phaseStart) / this.params.jobPhaseMs, 0, 1) * 10) / 10,
    };
  }
  pressureFresh() {
    return this.scheduler.sessions.get('SYN-PIO-01').consecutiveTimeouts === 0 && this.pressure.sourceTs !== null;
  }
  buildPump() {
    return {
      state: this.pump.state,
      pressure: this.pressureFresh() ? this.pressure.values[0] : null,
      setpoint: this.params.pumpSetpoint,
      readyBandLow: this.params.pumpSetpoint - this.params.pumpReadyBand,
      readyBandHigh: this.params.pumpSetpoint + this.params.pumpReadyBand,
      ready: this.pumpReady(),
      stopRequestedAt: iso(this.pump.stopRequestedAt),
    };
  }
  buildQueue(now) {
    return {
      totalQueued: this.queueOrder.length,
      entries: this.queueOrder.slice(0, 8).map((id, i) => ({
        position: i + 1,
        sensorId: id,
        sourceReason: this.queueReason.get(id),
        dirtyScore: this.acq.get(id).rawScore,
        secondsSinceLastClean: Math.round((now - this.proc.get(id).lastCleanAt) / 1000),
        status: this.entryStatus(id),
      })),
    };
  }
  buildAlarms() {
    const all = [...this.alarms.values()];
    return {
      activeUnack: all.filter((a) => a.state === 'ACTIVE_UNACK').length,
      activeAck: all.filter((a) => a.state === 'ACTIVE_ACK').length,
      clearedUnack: all.filter((a) => a.state === 'CLEARED_UNACK').length,
      items: all.slice(-this.params.alarmItemLimit).reverse().map((a) => ({ ...a })),
    };
  }
  buildCommunication() {
    return {
      devices: [...this.scheduler.sessions.values()].map((s) => ({
        deviceId: s.deviceId,
        state: s.device.timeoutFault || s.consecutiveTimeouts > 0 ? 'TIMEOUT' : s.device.staleFault ? 'STALE_SOURCE' : s.everTimedOut && s.pollsOk < 3 ? 'RECOVERING' : 'ONLINE',
        consecutiveTimeouts: s.consecutiveTimeouts,
        lastSuccessAt: iso(s.lastSuccessAt),
        lastLatencyMs: s.lastLatencyMs === null ? null : Math.round(s.lastLatencyMs),
        pollsOk: s.pollsOk,
        pollsFailed: s.pollsFailed,
      })),
    };
  }
  buildRuntime(sseClients = 0) {
    const mem = process.memoryUsage();
    return {
      uptimeS: Math.round((Date.now() - this.startedAt) / 1000),
      rssMb: round1(mem.rss / 1048576),
      heapUsedMb: round1(mem.heapUsed / 1048576),
      eventLoopP99Ms: round1(this.loopDelay.percentile(99) / 1e6 || 0),
      sseClients,
      historian: this.historian.health(),
      invariantViolations: this.violationCount,
      acceptedSecondJobs: this.acceptedSecondJobs,
      refusedSecondJobs: this.refusedSecondJobs,
    };
  }
  buildTrendPoint(now) {
    const fresh = this.pressureFresh();
    return {
      t: Math.floor(now / 1000),
      series: fresh ? [...this.pressure.values] : [null, null, null, null],
      setpoint: this.params.pumpSetpoint,
      jobActive: this.activeJobs.length > 0,
      alarmActive: [...this.alarms.values()].some((a) => a.state === 'ACTIVE_UNACK' || a.state === 'ACTIVE_ACK'),
    };
  }

  // ------------------------------------------------------------------ publication
  /** Immediate publish after a command (coalesced to one per event-loop turn). */
  requestPublish() {
    if (this.publishPending) return;
    this.publishPending = true;
    setImmediate(() => {
      this.publishPending = false;
      this.publish({ tick: false });
    });
  }
  publish({ tick }) {
    const t0 = performance.now();
    const now = Date.now();
    if (tick) {
      this.tickCount += 1;
      this.advanceProcess();
    }
    this.advancePump(now);
    this.advanceJob(now);
    this.classifyAll(now);
    this.updateQueue();
    this.autoStart();

    const delta = { kind: 'delta', schema: DELTA_SCHEMA, synthetic: true, previousRevision: this.revision, revision: this.revision + 1, generatedAt: iso(now) };
    const records = [];
    const changed = [];
    for (const s of this.sensors) {
      const rec = this.buildSensorRecord(s);
      records.push(rec);
      const json = JSON.stringify(rec);
      const prev = this.pubSensors.get(s.sensorId);
      if (!prev || prev.json !== json) {
        this.pubSensors.set(s.sensorId, { rec, json });
        changed.push(rec);
      }
    }
    if (changed.length) delta.sensors = changed;
    const singles = {
      config: this.config,
      walls: this.buildWalls(records),
      activeJob: this.buildActiveJob(now),
      pump: this.buildPump(),
      queue: this.buildQueue(now),
      alarms: this.buildAlarms(),
      communication: this.buildCommunication(),
      runtime: this.buildRuntime(this.sseClientCount?.() ?? 0),
    };
    for (const [k, v] of Object.entries(singles)) {
      const json = JSON.stringify(v);
      if (this.pubSingle[k]?.json !== json) {
        this.pubSingle[k] = { value: v, json };
        delta[k] = v;
      }
    }
    if (tick) {
      const tp = this.buildTrendPoint(now);
      this.trend.push(tp);
      delta.trendPoint = tp;
    }
    this.revision += 1;
    this.checkInvariants(records);
    const json = JSON.stringify(delta);
    this.metrics.publishMs.push(performance.now() - t0);
    this.metrics.deltaBytes.push(Buffer.byteLength(json));
    this.metrics.deltaSensorCounts.push(changed.length);
    this.metrics.deltasPublished += 1;
    this.emit('delta', delta, json);
    return delta;
  }
  /** Snapshot of the last published state (consistent with `revision`). */
  snapshot() {
    if (this.revision === 0) this.publish({ tick: false });
    const s = this.pubSingle;
    const snap = {
      kind: 'snapshot',
      schema: SNAPSHOT_SCHEMA,
      synthetic: true,
      revision: this.revision,
      generatedAt: iso(Date.now()),
      config: s.config.value,
      sensors: this.sensors.map((x) => this.pubSensors.get(x.sensorId).rec),
      walls: s.walls.value,
      activeJob: s.activeJob.value,
      pump: s.pump.value,
      queue: s.queue.value,
      alarms: s.alarms.value,
      communication: s.communication.value,
      runtime: s.runtime.value,
      trend: { capacity: this.params.trendCapacity, seriesNames: SERIES_NAMES, points: this.trend.toArray() },
    };
    const json = JSON.stringify(snap);
    this.metrics.snapshotBytes.push(Buffer.byteLength(json));
    this.metrics.snapshotsBuilt += 1;
    return { snap, json };
  }

  // ------------------------------------------------------------------ invariants
  violation(msg) {
    this.violationCount += 1;
    this.violations.push({ at: iso(Date.now()), revision: this.revision, msg });
    if (this.violations.length > 100) this.violations.shift();
  }
  checkInvariants(records) {
    if (this.activeJobs.length > 1) {
      this.acceptedSecondJobs += 1;
      this.violation(`more than one active Cleaning Job (${this.activeJobs.length})`);
    }
    if (this.scheduler.maxPerDeviceInFlight > 1) this.violation('overlapping requests within one device session');
    if (this.scheduler.maxGlobalInFlight > this.params.concurrency) this.violation('cross-device concurrency bound exceeded');
    if (this.revision <= this.lastInvariantRevision) this.violation('revision not monotonic');
    this.lastInvariantRevision = this.revision;
    if (new Set(this.queueOrder).size !== this.queueOrder.length) this.violation('duplicate GlobalQueue entry');
    const target = this.activeJobs[0]?.targetSensorId;
    if (target && this.queueReason.has(target)) this.violation('active job target still queued');
    if (this.trend.length > this.params.trendCapacity) this.violation('trend exceeds capacity');
    if (this.historian.depth > this.historian.capacity) this.violation('historian exceeds capacity');
    for (const r of records) {
      if (['BAD', 'STALE', 'DISABLED'].includes(r.quality) && r.classification !== 'NOT_CLASSIFIED') this.violation(`${r.sensorId} ${r.quality} classified`);
      if (r.quality === 'UNCERTAIN' && r.classificationBasis === 'CURRENT') this.violation(`${r.sensorId} UNCERTAIN classified from current value`);
      if (r.quality === 'UNCERTAIN' && r.lastValidatedScore !== null) {
        const expect = r.lastValidatedScore > this.config.dirtyThreshold ? 'DIRTY' : 'CLEANER';
        if (r.classification !== expect) this.violation(`${r.sensorId} UNCERTAIN classification differs from last validated`);
      }
    }
  }

  // ------------------------------------------------------------------ commands
  closeRequest() {
    const reasons = [];
    if (this.activeJobs.length) reasons.push('ACTIVE_JOB');
    if (this.pump.state === 'RUNNING' || this.pump.state === 'STARTING') reasons.push('PUMP_RUNNING');
    return {
      allowed: reasons.length === 0,
      reasons,
      note: 'Synthetic Operations UI close guard. Operational usability control only; not a safety protection and not hardware fail-safe.',
    };
  }
  command(name, params = {}) {
    const t0 = performance.now();
    const res = this.dispatch(name, params ?? {});
    const ms = performance.now() - t0;
    (this.metrics.commandLatencyMs[name] ??= new Ring(200)).push(ms);
    if (name === 'pump-stop') this.metrics.pumpStopLatencyMs.push(ms);
    this.requestPublish();
    return { command: name, ...res };
  }
  dispatch(name, p) {
    const sensorOk = (id) => this.sensorById.has(id);
    const deviceOk = (id) => this.devices.has(id);
    switch (name) {
      case 'set-dirty-mode': {
        if (!['normal', 'dirty30', 'dirty70', 'oscillate'].includes(p.mode)) return { accepted: false, reason: 'INVALID_MODE' };
        this.applyDirtyMode(p.mode);
        return { accepted: true };
      }
      case 'force-quality': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: 'UNKNOWN_SENSOR' };
        if (p.quality !== null && !['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED'].includes(p.quality)) return { accepted: false, reason: 'INVALID_QUALITY' };
        this.acq.get(p.sensorId).forcedQuality = p.quality === 'GOOD' ? null : p.quality;
        return { accepted: true };
      }
      case 'quality-showcase': {
        const map = { 'SYN-LEFT-01': 'UNCERTAIN', 'SYN-LEFT-02': 'BAD', 'SYN-LEFT-03': 'STALE', 'SYN-LEFT-04': 'DISABLED' };
        for (const [id, q] of Object.entries(map)) this.acq.get(id).forcedQuality = p.enabled === false ? null : q;
        return { accepted: true, detail: map };
      }
      case 'disable-sensor': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: 'UNKNOWN_SENSOR' };
        this.acq.get(p.sensorId).disabled = p.disabled !== false;
        return { accepted: true };
      }
      case 'device-timeout': {
        if (!deviceOk(p.deviceId)) return { accepted: false, reason: 'UNKNOWN_DEVICE' };
        this.devices.get(p.deviceId).timeoutFault = p.enabled !== false;
        return { accepted: true };
      }
      case 'device-stale': {
        if (!deviceOk(p.deviceId)) return { accepted: false, reason: 'UNKNOWN_DEVICE' };
        this.devices.get(p.deviceId).setStaleFault(p.enabled !== false);
        return { accepted: true };
      }
      case 'raise-alarm': {
        if (p.sensorId && !sensorOk(p.sensorId)) return { accepted: false, reason: 'UNKNOWN_SENSOR' };
        const id = this.raiseAlarm({ code: 'SYN-SENSOR-ALARM', text: `Synthetic alarm ${p.sensorId ?? 'system'}`, severity: p.severity ?? 'HIGH', sensorId: p.sensorId ?? null });
        return { accepted: true, detail: { alarmId: id } };
      }
      case 'ack-alarm':
        return { accepted: true, detail: { affected: this.ackAlarm(p.alarmId) } };
      case 'clear-alarm':
        return { accepted: true, detail: { affected: this.clearAlarm(p.alarmId) } };
      case 'enqueue': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: 'UNKNOWN_SENSOR' };
        if (!this.queueReason.has(p.sensorId) && this.activeJobs[0]?.targetSensorId !== p.sensorId) {
          this.queueOrder.push(p.sensorId);
          this.queueReason.set(p.sensorId, 'SYN_OPERATOR_REQUEST');
        }
        return { accepted: true };
      }
      case 'hold-queue':
        this.queueHeld = p.held !== false;
        return { accepted: true };
      case 'auto-jobs':
        this.autoJobs = p.enabled !== false;
        return { accepted: true };
      case 'start-job': {
        this.classifyAll(Date.now());
        return this.startJob(p.sensorId, 'SYN_SCENARIO_COMMAND');
      }
      case 'second-job-attempt': {
        // Ensure one job exists, then attempt a concurrent second job. Must be refused.
        this.classifyAll(Date.now());
        let first = null;
        if (!this.activeJobs.length) {
          const cand = this.sensors.find((s) => this.acq.get(s.sensorId).cur?.quality === 'GOOD' && this.sensorAlarmState(s.sensorId)[0] === 'NONE');
          first = cand ? this.startJob(cand.sensorId, 'SYN_SCENARIO_COMMAND') : { accepted: false, reason: 'NO_CANDIDATE' };
        }
        const other = this.sensors.find((s) => s.sensorId !== this.activeJobs[0]?.targetSensorId);
        const second = this.startJob(other.sensorId, 'SYN_SCENARIO_SECOND_JOB');
        return { accepted: second.accepted, reason: second.reason, detail: { first, second, activeJobs: this.activeJobs.length } };
      }
      case 'abort-job':
        return { accepted: this.abortJob('SYN_SCENARIO_ABORT') };
      case 'pump-start': {
        if (this.pump.state === 'RUNNING' || this.pump.state === 'STARTING') return { accepted: false, reason: 'ALREADY_RUNNING' };
        this.setPump('STARTING', Date.now());
        this.pump.stopRequestedAt = null;
        return { accepted: true };
      }
      case 'pump-stop': {
        if (this.pump.state === 'STOPPED' || this.pump.state === 'STOPPING') return { accepted: false, reason: 'ALREADY_STOPPED' };
        const now = Date.now();
        this.abortJob('SYN_PUMP_STOP');
        this.setPump('STOPPING', now);
        this.pump.stopRequestedAt = now;
        return { accepted: true };
      }
      case 'historian-delay': {
        const d = Number(p.delayMs);
        if (!Number.isFinite(d) || d < 0 || d > 600000) return { accepted: false, reason: 'INVALID_DELAY' };
        this.historian.delayMs = d;
        return { accepted: true };
      }
      case 'historian-burst': {
        const n = Math.min(Number(p.count) || 0, this.historian.capacity * 2);
        let ok = 0;
        for (let i = 0; i < n; i += 1) if (this.historian.offer({ t: Date.now(), ch: 'syn-burst', v: 0 })) ok += 1;
        return { accepted: true, detail: { offered: n, accepted: ok } };
      }
      case 'publish-config': {
        const th = Number(p.dirtyThreshold);
        if (!Number.isFinite(th) || th < 1 || th > 99) return { accepted: false, reason: 'INVALID_THRESHOLD' };
        this.config = Object.freeze({ ...this.config, revision: this.config.revision + 1, publishedAt: iso(Date.now()), dirtyThreshold: th });
        this.configHistory.push(this.config);
        return { accepted: true, detail: { revision: this.config.revision } };
      }
      default:
        return { accepted: false, reason: 'UNKNOWN_COMMAND' };
    }
  }

  // ------------------------------------------------------------------ metrics
  metricsReport() {
    const now = performance.now();
    const usage = process.cpuUsage(this.lastCpu.usage);
    const elapsedMs = now - this.lastCpu.at;
    this.lastCpu = { usage: process.cpuUsage(), at: now };
    const mem = process.memoryUsage();
    const lat = this.scheduler.latency;
    const cmd = {};
    for (const [k, r] of Object.entries(this.metrics.commandLatencyMs)) cmd[k] = summarize(r.toArray(), 3);
    return {
      label: SYNTHETIC_LABEL,
      revision: this.revision,
      uptimeS: Math.round((Date.now() - this.startedAt) / 1000),
      cpuPercentSinceLastRead: elapsedMs > 0 ? Number((((usage.user + usage.system) / 1000 / elapsedMs) * 100).toFixed(2)) : null,
      memory: { rssMb: round1(mem.rss / 1048576), heapUsedMb: round1(mem.heapUsed / 1048576), heapTotalMb: round1(mem.heapTotal / 1048576), externalMb: round1(mem.external / 1048576) },
      eventLoopDelayMs: { p50: round1(this.loopDelay.percentile(50) / 1e6), p99: round1(this.loopDelay.percentile(99) / 1e6), max: round1(this.loopDelay.max / 1e6) },
      publish: { deltasPublished: this.metrics.deltasPublished, publishMs: summarize(this.metrics.publishMs.toArray(), 3), deltaBytes: summarize(this.metrics.deltaBytes.toArray(), 0), deltaChangedSensors: summarize(this.metrics.deltaSensorCounts.toArray(), 0) },
      snapshot: { built: this.metrics.snapshotsBuilt, bytes: summarize(this.metrics.snapshotBytes.toArray(), 0) },
      scheduler: {
        concurrency: this.params.concurrency,
        maxGlobalInFlight: this.scheduler.maxGlobalInFlight,
        maxPerDeviceInFlight: this.scheduler.maxPerDeviceInFlight,
        timeouts: this.scheduler.timeouts,
        pollLatencyMs: { FAST: summarize(lat.FAST), MEDIUM: summarize(lat.MEDIUM), SLOW: summarize(lat.SLOW) },
        sessions: [...this.scheduler.sessions.values()].map((s) => ({ deviceId: s.deviceId, pollsOk: s.pollsOk, pollsFailed: s.pollsFailed, coalesced: s.coalesced, consecutiveTimeouts: s.consecutiveTimeouts })),
      },
      historian: { ...this.historian.health(), accepted: this.historian.accepted, written: this.historian.written, batches: this.historian.batches, maxDepth: this.historian.maxDepth, nearOverflowEvents: this.historian.nearOverflowEvents, gapMarkerList: this.historian.gapMarkers.slice(-10) },
      jobs: { active: this.activeJobs.length, completed: this.jobsCompleted, aborted: this.jobsAborted, refusedSecondJobs: this.refusedSecondJobs, acceptedSecondJobs: this.acceptedSecondJobs, refusals: this.jobRefusals },
      commands: cmd,
      pumpStopCommandMs: summarize(this.metrics.pumpStopLatencyMs.toArray(), 3),
      invariants: { violations: this.violationCount, recent: this.violations.slice(-10) },
      trendPoints: this.trend.length,
      configRevision: this.config.revision,
    };
  }
}
