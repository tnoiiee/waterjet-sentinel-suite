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
import { DEFAULT_PARAMS, SYNTHETIC_LABEL, buildDevices, buildPollPlan, buildSensors, isOscillationSample, WALL_COUNTS } from './config.mjs';
import { isCannonId, wallMapSlots } from '../../contracts/sensorMap.mjs';
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

// ---------------------------------------------------------------------------------------------
// Synthetic GlobalQueue (Owner domain correction; supersedes the earlier unbounded queue model).
//
// SYNTHETIC — NOT PRODUCTION SCHEDULING. NOT ELIGIBLE FOR PRODUCTION PROMOTION.
//  * The GlobalQueue holds ready-to-dispatch entries only. Presence in the queue means READY;
//    there is no per-entry status (no BLOCKED / HELD / WAITING / EXCLUDED or synonyms).
//  * Physical capacity: at most QUEUE_CAPACITY unique entries. No hidden overflow, no preview cut.
//  * FIFO; only Position 1 is ever dispatched (no scan-forward). Dispatch is atomic: the head
//    entry is removed and exactly one Cleaning Job is created for that Sensor, linked by a
//    synthetic dispatch record. At most one Active Job; while it exists, dispatch is paused.
//  * Operator pause belongs to the AutoSequence; Pump readiness waits belong to the Job
//    pre-check (P1). Neither is a queue entry state.
//  * Admission is a deterministic synthetic generator (sources below). No Alarm / Quality /
//    equipment / interval eligibility rule is implemented — those await the Owner-approved
//    Queue Eligibility Decision Matrix (docs/spikes/queue-eligibility-decision-matrix.md).
//  * NOT IMPLEMENTED: 4 TempQueue + 4 TimeQueue split, dwell, production source ownership and
//    refill, Reject / Reorder, audit, recovery.
// ---------------------------------------------------------------------------------------------
export const QUEUE_CAPACITY = 8;
// Synthetic source reasons (spike codes; the contract field is a free string). A duplicate
// admission never changes the existing entry (de-duplication by Sensor ID, FIFO unchanged).
export const QUEUE_REASONS = Object.freeze({
  DIRTY_SCORE: 'SYN_DIRTY_SCORE_ABOVE_THRESHOLD',
  OPERATOR: 'SYN_OPERATOR_REQUEST',
  TEMP: 'SYN_TEMP_RISE',
  TIME_DUE: 'SYN_TIME_DUE',
  TEMP_AND_TIME: 'SYN_TEMP_AND_TIME',
});
// Scores used by the synthetic review controls (Set DIRTY / Set CLEANER).
const REVIEW_SCORE = Object.freeze({ DIRTY: 82, CLEANER: 18 });
// Held review job: frozen at P2 so the Owner can inspect it; never completes on its own.
const REVIEW_JOB_PHASE = 1;
// Synthetic demonstration reason for preset 6 (policy pending; never a production rule).
export const NOT_ADMITTED_DEMO_REASON = 'Reason pending Owner-approved eligibility policy (synthetic demonstration)';
const DISPATCH_LOG_CAPACITY = 50;
// Synthetic review presets (the eighth control is 'reset').
export const PRESETS = Object.freeze([
  'queued-dirty',
  'queued-cleaner-non-score',
  'selected-queued',
  'dispatched-head-job',
  'alarm-on-active-job',
  'alarm-not-admitted',
  'head-to-job-transition',
]);

export class SyntheticRuntime extends EventEmitter {
  constructor(overrides = {}) {
    super();
    const p = Object.freeze({ ...DEFAULT_PARAMS, ...overrides });
    this.params = p;
    // Independent seeded streams so the synthetic process workload does not depend on how many
    // polls the wall-clock scheduler happened to execute:
    //   rng       — process model (initial state, per-tick drift, job completion)
    //   noiseRng  — per-device register noise
    //   latencyRng — per-device simulated latency (inside SimDevice)
    this.rng = createRng(p.seed);
    this.startedAt = Date.now();
    this.sensors = buildSensors();
    this.sensorById = new Map(this.sensors.map((s) => [s.sensorId, s]));
    this.plan = buildPollPlan(this.sensors);
    this.wallMap = wallMapSlots();
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
    // Bounded synthetic GlobalQueue: [{ entryId, sensorId, reason }] in FIFO order.
    this.queue = [];
    this.queueRevision = 0;
    this.entrySeq = 0;
    this.dispatchSeq = 0;
    this.dispatchLog = []; // bounded synthetic dispatch evidence (not a production audit)
    this.lastDispatch = null;
    this.notAdmitted = new Map(); // sensorId -> reason; set only by an explicit test scenario
    this.autoSequencePaused = false; // AutoSequence (dispatch control) pause, not a queue state
    this.queueRejections = {};
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
    const deviceDefs = buildDevices();
    this.noiseRng = new Map(deviceDefs.map((d, i) => [d.deviceId, createRng(p.seed + 100 + i)]));
    const devices = deviceDefs.map(
      (d, i) =>
        new SimDevice(d.deviceId, {
          rng: createRng(p.seed + 1000 + i),
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
    const noise = this.noiseRng.get(entry.deviceId);
    if (entry.functionCategory === 'INPUT_REGISTERS' && entry.deviceId.startsWith('SYN-TC-')) {
      for (const s of this.sensors) {
        if (s.deviceId !== entry.deviceId) continue;
        const score = this.proc.get(s.sensorId).trueScore;
        s.channelOffsets.forEach((off) => {
          const temp = 100 + score * 1.5 + noise.range(-0.3, 0.3);
          regs[off] = Math.round(temp * 10);
        });
      }
    } else if (entry.id === 'SYN-PIO-01/fast/pressure') {
      const level = this.pumpLevel(Date.now());
      const jetting = this.activeJobs[0] && this.currentPhaseIndex(this.activeJobs[0], Date.now()) === 3;
      const offsets = [0, -3, jetting ? -12 : -6, -5];
      for (let i = 0; i < 4; i += 1) {
        const v = level * (this.params.pumpSetpoint + offsets[i]) + noise.range(-1.5, 1.5) * level;
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
        this.historian.offer({ t: result.sourceTimestamp, ch: s.tcFrontChannel, v: a });
        this.historian.offer({ t: result.sourceTimestamp, ch: s.tcRearChannel, v: b });
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
      for (const s of this.sensors) if (isOscillationSample(s)) this.proc.get(s.sensorId).oscillate = true;
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
        pr.trueScore = 50 + 2 * Math.sin(t * 1.3 + s.scanOrder);
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
  // ---- bounded synthetic queue primitives (every membership change bumps queueRevision)
  /** FIFO Sensor IDs (read-only view). */
  get queueOrder() {
    return this.queue.map((e) => e.sensorId);
  }
  isQueued(sensorId) {
    return this.queue.some((e) => e.sensorId === sensorId);
  }
  queueEntry(sensorId) {
    return this.queue.find((e) => e.sensorId === sensorId) ?? null;
  }
  rejectAdmission(reason) {
    this.queueRejections[reason] = (this.queueRejections[reason] ?? 0) + 1;
    return { admitted: false, reason };
  }
  /**
   * Admit one Sensor at the tail. Synthetic generator only: the caller is a synthetic source that
   * has prepared the Sensor as dispatch-ready. Never exceeds QUEUE_CAPACITY; duplicates are no-ops.
   */
  admit(sensorId, reason) {
    if (isCannonId(sensorId)) return this.rejectAdmission('CANNON_NOT_A_SENSOR');
    if (!this.sensorById.has(sensorId)) return this.rejectAdmission('UNKNOWN_SENSOR');
    const existing = this.queueEntry(sensorId);
    if (existing) return { admitted: false, reason: 'DUPLICATE', duplicate: true, owner: existing.reason, position: this.queue.indexOf(existing) + 1 };
    if (this.activeJobs[0]?.targetSensorId === sensorId) return this.rejectAdmission('ACTIVE_JOB_TARGET');
    if (this.notAdmitted.has(sensorId)) return this.rejectAdmission('NOT_ADMITTED_SYNTHETIC_DEMONSTRATION');
    if (this.queue.length >= QUEUE_CAPACITY) return this.rejectAdmission('QUEUE_FULL');
    this.entrySeq += 1;
    this.queue.push({ entryId: `SYN-QE-${String(this.entrySeq).padStart(5, '0')}`, sensorId, reason });
    this.queueRevision += 1;
    return { admitted: true, reason: null, owner: reason, position: this.queue.length };
  }
  /** Explicit synthetic removal (operator dequeue / scenario preparation). */
  removeFromQueue(sensorId) {
    const i = this.queue.findIndex((e) => e.sensorId === sensorId);
    if (i < 0) return null;
    const [entry] = this.queue.splice(i, 1);
    this.queueRevision += 1;
    return entry;
  }
  clearQueue() {
    if (!this.queue.length) return 0;
    const n = this.queue.length;
    this.queue.length = 0;
    this.queueRevision += 1;
    return n;
  }
  /**
   * Synthetic score source (deterministic refill). Scans Sensors in canonical scan order and admits
   * CURRENT-basis DIRTY Sensors at the tail until the queue holds QUEUE_CAPACITY entries. This is
   * the synthetic generator's preparation rule, not an eligibility policy. No automatic removal:
   * the removal policy is OWNER DECISION REQUIRED (entries leave by dispatch or explicit removal).
   */
  updateQueue() {
    for (const s of this.sensors) {
      if (this.queue.length >= QUEUE_CAPACITY) return;
      const cur = this.acq.get(s.sensorId).cur;
      if (!cur || cur.basis !== 'CURRENT' || cur.classification !== 'DIRTY') continue;
      if (this.isQueued(s.sensorId) || this.activeJobs[0]?.targetSensorId === s.sensorId || this.notAdmitted.has(s.sensorId)) continue;
      this.admit(s.sensorId, QUEUE_REASONS.DIRTY_SCORE);
    }
  }
  currentPhaseIndex(job, now) {
    if (job.heldPhaseIndex !== undefined) return job.heldPhaseIndex;
    if (job.preCheck === 'WAITING_FOR_PUMP') return 0;
    return Math.floor((now - job.startedAtMs) / this.params.jobPhaseMs);
  }
  /**
   * Atomic head-only dispatch: remove queue Position 1 and create exactly one Cleaning Job for that
   * Sensor, linked by a synthetic dispatch record. Never looks past Position 1. Pump readiness is a
   * Job pre-check wait (P1), not a dispatch condition and not a queue entry state.
   */
  dispatchHead(origin, { hold = false } = {}) {
    if (this.activeJobs.length >= 1) {
      this.refusedSecondJobs += 1;
      return { accepted: false, reason: 'ACTIVE_JOB_EXISTS' };
    }
    if (!this.queue.length) {
      this.jobRefusals.QUEUE_EMPTY = (this.jobRefusals.QUEUE_EMPTY ?? 0) + 1;
      return { accepted: false, reason: 'QUEUE_EMPTY' };
    }
    const now = Date.now();
    const queueRevisionBefore = this.queueRevision;
    const head = this.queue.shift(); // Position 1 only
    this.queueRevision += 1;
    const s = this.sensorById.get(head.sensorId);
    this.jobSeq += 1;
    this.dispatchSeq += 1;
    const jobId = `SYN-JOB-${String(this.jobSeq).padStart(4, '0')}`;
    const record = Object.freeze({
      dispatchId: `SYN-DSP-${String(this.dispatchSeq).padStart(4, '0')}`,
      synthetic: true,
      queueRevisionBefore,
      queueRevisionAfter: this.queueRevision,
      queueEntryId: head.entryId,
      positionBefore: 1,
      sensorId: head.sensorId,
      sourceReason: head.reason,
      jobId,
      origin,
      dispatchedAt: iso(now),
    });
    const job = { jobId, targetSensorId: head.sensorId, jetId: s.jetId, valveId: s.valveId, origin, startedAtMs: now, dispatch: record, preCheck: 'PASSED' };
    if (hold) {
      job.heldPhaseIndex = REVIEW_JOB_PHASE;
      job.startedAtMs = now - REVIEW_JOB_PHASE * this.params.jobPhaseMs;
    } else if (!this.pumpReady()) {
      job.preCheck = 'WAITING_FOR_PUMP'; // Job lifecycle wait (P1), never a queue entry state
    }
    this.activeJobs.push(job);
    this.lastDispatch = record;
    this.dispatchLog.push(record);
    if (this.dispatchLog.length > DISPATCH_LOG_CAPACITY) this.dispatchLog.shift();
    return { accepted: true, reason: null, detail: { jobId, dispatch: record } };
  }
  /**
   * Synthetic test preparation: make `sensorId` queue Position 1 so that it can be dispatched by
   * the head-only rule. Never reorders: if the Sensor is not already the head, the synthetic queue
   * is cleared and the Sensor is admitted as the only entry (keeping its reason if it was queued).
   */
  prepareHead(sensorId, reason = QUEUE_REASONS.OPERATOR) {
    if (this.queue[0]?.sensorId === sensorId) return { prepared: true, cleared: 0 };
    const keep = this.queueEntry(sensorId)?.reason ?? reason;
    this.notAdmitted.delete(sensorId);
    const cleared = this.clearQueue();
    const r = this.admit(sensorId, keep);
    return { prepared: r.admitted, cleared, reason: r.reason };
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
    if (job.preCheck === 'WAITING_FOR_PUMP') {
      if (!this.pumpReady()) return;
      job.preCheck = 'PASSED';
      job.startedAtMs = now;
    }
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
  autoSequenceState() {
    if (!this.autoJobs) return 'OFF';
    if (this.autoSequencePaused) return 'PAUSED';
    if (this.activeJobs.length) return 'JOB_ACTIVE';
    if (!this.queue.length) return 'QUEUE_EMPTY';
    return 'READY_TO_DISPATCH';
  }
  /** AutoSequence: dispatch queue Position 1 only, and only when no Job is active. */
  autoDispatch() {
    if (this.autoSequenceState() !== 'READY_TO_DISPATCH') return null;
    return this.dispatchHead('SYN_AUTO_SEQUENCE');
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
      slotType: 'SENSOR',
      wall: s.wall,
      logicalColumn: s.logicalColumn,
      logicalRow: s.logicalRow,
      wallColumn: s.wallColumn,
      wallRow: s.wallRow,
      scanOrder: s.scanOrder,
      deviceId: s.deviceId,
      tcFrontChannel: s.tcFrontChannel,
      tcRearChannel: s.tcRearChannel,
      dirtyScore: quality === 'BAD' || quality === 'DISABLED' ? null : acq.rawScore,
      lastValidatedScore: acq.lastValidatedScore,
      lastValidatedAt: iso(acq.lastValidatedAt),
      classification,
      classificationBasis: basis,
      quality,
      qualityReason: reason,
      sourceTimestamp: iso(acq.sourceTs),
      queueState: isTarget ? 'ACTIVE' : this.isQueued(s.sensorId) ? 'QUEUED' : 'NONE',
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
    const waiting = job.preCheck === 'WAITING_FOR_PUMP';
    const phaseStart = waiting ? job.startedAtMs : job.startedAtMs + idx * this.params.jobPhaseMs;
    return {
      jobId: job.jobId,
      targetSensorId: job.targetSensorId,
      jetId: job.jetId,
      valveId: job.valveId,
      phase: PHASES[idx][0],
      phaseLabel: waiting ? 'Pre-check — waiting for Pump (synthetic)' : PHASES[idx][1],
      phaseIndex: idx,
      startedAt: iso(job.startedAtMs),
      phaseStartedAt: iso(phaseStart),
      phaseProgress: waiting ? 0 : Math.round(clamp((now - phaseStart) / this.params.jobPhaseMs, 0, 1) * 10) / 10,
      preCheck: job.preCheck,
      dispatch: job.dispatch,
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
    // The whole physical queue (<= QUEUE_CAPACITY). No preview cut, no hidden overflow.
    return {
      synthetic: true,
      label: 'GlobalQueue · synthetic · FIFO · not Production scheduling',
      capacity: QUEUE_CAPACITY,
      totalQueued: this.queue.length,
      revision: this.queueRevision,
      entries: this.queue.map((e, i) => ({
        position: i + 1,
        entryId: e.entryId,
        sensorId: e.sensorId,
        sourceReason: e.reason,
        dirtyScore: this.acq.get(e.sensorId).rawScore,
        secondsSinceLastClean: Math.round((now - this.proc.get(e.sensorId).lastCleanAt) / 1000),
      })),
      autoSequence: this.autoSequenceState(),
      lastDispatch: this.lastDispatch,
      eligibilityDiagnostics: [...this.notAdmitted].map(([sensorId, reason]) => ({ sensorId, decision: 'NOT_ADMITTED', reason, synthetic: true })),
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
    this.autoDispatch();

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
      wallMap: this.wallMap,
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

  // ------------------------------------------------------------------ synthetic review helpers
  // Spike-only Owner review tooling (Diagnostics "SYNTHETIC TEST CONTROL"). Every helper acts on
  // the authoritative runtime state; the UI only sends one explicit command per click.
  /** Synthetic non-score source admission (TEMP / TIME / TEMP+TIME / OPERATOR). Bounded. */
  enqueueWithReason(sensorId, reasonKey) {
    const reason = QUEUE_REASONS[reasonKey];
    if (!reason || reasonKey === 'DIRTY_SCORE') return { accepted: false, reason: 'INVALID_QUEUE_REASON' };
    const r = this.admit(sensorId, reason);
    if (r.duplicate) return { accepted: true, detail: { queued: false, duplicate: true, owner: r.owner, position: r.position } };
    if (!r.admitted) return { accepted: false, reason: r.reason };
    return { accepted: true, detail: { queued: true, duplicate: false, owner: reason, position: r.position } };
  }
  /** Hold a synthetic score (process target) and apply it to the acquisition value now. */
  setSensorScore(sensorId, score) {
    const pr = this.proc.get(sensorId);
    const acq = this.acq.get(sensorId);
    if (score === null) {
      pr.target = null;
      return;
    }
    pr.target = score;
    pr.trueScore = score;
    acq.rawScore = score;
    acq.sourceTs = Date.now();
  }
  touch(sensorId) {
    (this.reviewTouched ??= new Set()).add(sensorId);
  }
  /** Restore one Sensor's synthetic review overrides (alarms, queue reasons, quality, score, job). */
  resetReviewSensor(sensorId) {
    const acq = this.acq.get(sensorId);
    acq.forcedQuality = null;
    this.setSensorScore(sensorId, null);
    for (const a of [...this.alarms.values()]) if (a.sensorId === sensorId) this.alarms.delete(a.alarmId);
    const entry = this.queueEntry(sensorId);
    if (entry && entry.reason !== QUEUE_REASONS.DIRTY_SCORE) this.removeFromQueue(sensorId);
    this.notAdmitted.delete(sensorId);
    if (this.activeJobs[0]?.targetSensorId === sensorId && this.activeJobs[0].heldPhaseIndex !== undefined) this.abortJob('SYN_REVIEW_RESET');
    this.reviewTouched?.delete(sensorId);
  }
  /**
   * Review Job (synthetic): the selected Sensor is prepared as queue Position 1 and then dispatched
   * by the head-only rule as a held (frozen) review job. Never retargets an existing Job: if a Job
   * for another Sensor is active the command is refused (end that Job first).
   */
  reviewJob(sensorId) {
    this.autoJobs = false;
    const job = this.activeJobs[0];
    if (job && job.targetSensorId !== sensorId) {
      this.refusedSecondJobs += 1;
      return { accepted: false, reason: 'ACTIVE_JOB_EXISTS', detail: { activeJobTarget: job.targetSensorId } };
    }
    if (job) {
      job.heldPhaseIndex = REVIEW_JOB_PHASE;
      return { accepted: true, detail: { jobId: job.jobId, held: true, dispatch: job.dispatch } };
    }
    const prep = this.prepareHead(sensorId);
    if (!prep.prepared) return { accepted: false, reason: prep.reason ?? 'NOT_PREPARED' };
    const r = this.dispatchHead('SYN_REVIEW_CONTROL', { hold: true });
    return r.accepted ? { ...r, detail: { ...r.detail, held: true, preparedHead: true, clearedEntries: prep.cleared } } : r;
  }
  sensorAlarmIds(sensorId) {
    return [...this.alarms.values()].filter((a) => a.sensorId === sensorId).map((a) => a.alarmId);
  }
  raiseReviewAlarm(sensorId) {
    const open = [...this.alarms.values()].find((a) => a.sensorId === sensorId && a.code === 'SYN-REVIEW-ALARM' && a.state !== 'CLEARED_UNACK');
    if (open) return open.alarmId;
    return this.raiseAlarm({ code: 'SYN-REVIEW-ALARM', text: `Synthetic review alarm ${sensorId}`, severity: 'HIGH', sensorId });
  }
  /** Two deterministic companion Sensors (canonical scan order) for multi-entry presets. */
  companions(sensorId) {
    return this.sensors
      .slice(60)
      .map((x) => x.sensorId)
      .filter((id) => id !== sensorId)
      .slice(0, 2);
  }
  /**
   * Deterministic synthetic review presets (one request = one preset). Each preset first prepares
   * a known state: AutoSequence off, any Job ended (synthetic abort — never retargeted), synthetic
   * queue cleared. Queue entries are then admitted by synthetic sources; Jobs are created only by
   * head-only dispatch. These are spike review states, not Production behaviour.
   */
  visualPreset(preset, sensorId) {
    const settle = () => {
      this.classifyAll(Date.now());
      this.updateQueue();
    };
    if (preset === 'reset') {
      const ids = [...(this.reviewTouched ?? [])];
      for (const id of ids) this.resetReviewSensor(id);
      if (this.activeJobs[0]?.heldPhaseIndex !== undefined) this.abortJob('SYN_REVIEW_RESET');
      this.notAdmitted.clear();
      this.clearQueue();
      this.autoSequencePaused = false;
      this.autoJobs = this.params.autoJobs;
      settle(); // deterministic refill by the synthetic score source, bounded to QUEUE_CAPACITY
      return { accepted: true, detail: { preset, restored: ids, autoJobs: this.autoJobs, queued: this.queue.length } };
    }
    if (!PRESETS.includes(preset)) return { accepted: false, reason: 'UNKNOWN_PRESET' };
    // Prepare: AutoSequence off, no Job, empty synthetic queue, selected Sensor restored.
    this.autoJobs = false;
    this.abortJob('SYN_PRESET_PREPARE');
    this.notAdmitted.clear();
    this.clearQueue();
    this.resetReviewSensor(sensorId);
    this.touch(sensorId);
    const [a, b] = this.companions(sensorId);
    const prepareCompanions = () => {
      for (const id of [a, b]) {
        this.resetReviewSensor(id);
        this.touch(id);
      }
      this.enqueueWithReason(a, 'TIME_DUE');
      this.enqueueWithReason(b, 'TEMP');
    };
    const detail = { preset, sensorId };
    switch (preset) {
      case 'queued-dirty':
        this.setSensorScore(sensorId, REVIEW_SCORE.DIRTY);
        this.classifyAll(Date.now());
        this.admit(sensorId, QUEUE_REASONS.DIRTY_SCORE); // score source, Position 1
        break;
      case 'queued-cleaner-non-score':
        this.setSensorScore(sensorId, REVIEW_SCORE.CLEANER);
        this.classifyAll(Date.now());
        this.admit(sensorId, QUEUE_REASONS.OPERATOR); // synthetic non-score source
        break;
      case 'selected-queued':
        this.setSensorScore(sensorId, REVIEW_SCORE.DIRTY);
        this.classifyAll(Date.now());
        this.resetReviewSensor(a);
        this.touch(a);
        this.enqueueWithReason(a, 'TIME_DUE'); // Position 1
        this.admit(sensorId, QUEUE_REASONS.DIRTY_SCORE); // selected Sensor at Position 2
        break;
      case 'dispatched-head-job':
      case 'alarm-on-active-job':
      case 'head-to-job-transition': {
        this.setSensorScore(sensorId, REVIEW_SCORE.DIRTY);
        this.classifyAll(Date.now());
        this.admit(sensorId, QUEUE_REASONS.DIRTY_SCORE); // Position 1
        prepareCompanions(); // Positions 2 and 3
        detail.queueBefore = this.queueOrder;
        let r;
        if (preset === 'head-to-job-transition') {
          // AutoSequence resumes and dispatches Position 1 now; the Job runs and, after it ends,
          // only the new head is the next candidate.
          this.autoJobs = true;
          r = this.autoDispatch();
        } else {
          r = this.dispatchHead('SYN_REVIEW_PRESET', { hold: true }); // held review Job
        }
        if (!r?.accepted) return { accepted: false, reason: r?.reason ?? 'NOT_DISPATCHED', detail };
        detail.jobId = r.detail.jobId;
        detail.dispatch = r.detail.dispatch;
        detail.queueAfter = this.queueOrder;
        if (preset === 'alarm-on-active-job') detail.alarmId = this.raiseReviewAlarm(sensorId);
        break;
      }
      case 'alarm-not-admitted':
        this.setSensorScore(sensorId, REVIEW_SCORE.DIRTY);
        detail.alarmId = this.raiseReviewAlarm(sensorId);
        // Synthetic demonstration only: the scenario explicitly marks the Sensor NOT ADMITTED.
        // No Alarm eligibility rule exists (OWNER DECISION REQUIRED).
        this.notAdmitted.set(sensorId, NOT_ADMITTED_DEMO_REASON);
        break;
      default:
        return { accepted: false, reason: 'UNKNOWN_PRESET' };
    }
    settle();
    const entry = this.queueEntry(sensorId);
    return {
      accepted: true,
      detail: { ...detail, queuePosition: entry ? this.queue.indexOf(entry) + 1 : null, queueOwner: entry?.reason ?? null, queued: this.queue.length, activeJobs: this.activeJobs.length },
    };
  }
  /**
   * Deterministic mixed-source GlobalQueue scenario (test evidence, not the default display).
   * Clears the synthetic queue, turns the AutoSequence off, then admits four canonical Sensors via
   * four non-score sources and one via the score source; the score source then refills up to the
   * capacity of 8 in canonical scan order. Bounded: never more than QUEUE_CAPACITY entries.
   */
  mixedQueueScenario() {
    this.autoJobs = false;
    this.abortJob('SYN_MIXED_QUEUE_SCENARIO');
    this.notAdmitted.clear();
    this.clearQueue();
    const pick = (i) => this.sensors[i].sensorId; // canonical scan order, no Production address
    const plan = [
      [pick(2), 'TIME_DUE'],
      [pick(30), 'TEMP_AND_TIME'],
      [pick(55), 'OPERATOR'],
      [pick(80), 'TEMP'],
    ];
    for (const [id] of plan) {
      this.acq.get(id).forcedQuality = null;
      this.touch(id);
    }
    for (const [id, key] of plan) this.enqueueWithReason(id, key);
    const dirtyId = pick(100);
    this.touch(dirtyId);
    this.setSensorScore(dirtyId, REVIEW_SCORE.DIRTY);
    this.classifyAll(Date.now());
    this.admit(dirtyId, QUEUE_REASONS.DIRTY_SCORE);
    this.updateQueue(); // deterministic refill to capacity (score source, scan order)
    return {
      accepted: true,
      detail: {
        explicit: plan.map(([sensorId, key]) => ({ sensorId, reason: QUEUE_REASONS[key] })),
        scoreSensor: dirtyId,
        entries: this.queue.map((e) => ({ sensorId: e.sensorId, reason: e.reason })),
        totalQueued: this.queue.length,
        capacity: QUEUE_CAPACITY,
      },
    };
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
    const ids = this.queueOrder;
    if (ids.length > QUEUE_CAPACITY) this.violation(`GlobalQueue exceeds capacity (${ids.length} > ${QUEUE_CAPACITY})`);
    if (new Set(ids).size !== ids.length) this.violation('duplicate GlobalQueue entry');
    if (ids.some((id) => isCannonId(id) || !this.sensorById.has(id))) this.violation('non-Sensor GlobalQueue entry');
    if (this.queueRevision < (this.lastQueueRevision ?? 0)) this.violation('queue revision not monotonic');
    this.lastQueueRevision = this.queueRevision;
    const job = this.activeJobs[0];
    const target = job?.targetSensorId;
    if (target && this.isQueued(target)) this.violation('active job target still queued');
    if (job && (!job.dispatch || job.dispatch.sensorId !== target || job.dispatch.positionBefore !== 1 || job.dispatch.jobId !== job.jobId)) {
      this.violation(`active job ${job.jobId} not linked to a Position 1 dispatch record`);
    }
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
    // Cannon slots are equipment, not Sensors: never a quality, alarm, queue, or job target.
    const sensorRefusal = (id) => (isCannonId(id) ? 'CANNON_NOT_A_SENSOR' : 'UNKNOWN_SENSOR');
    const deviceOk = (id) => this.devices.has(id);
    switch (name) {
      case 'set-dirty-mode': {
        if (!['normal', 'dirty30', 'dirty70', 'oscillate'].includes(p.mode)) return { accepted: false, reason: 'INVALID_MODE' };
        this.applyDirtyMode(p.mode);
        return { accepted: true };
      }
      case 'force-quality': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        this.touch(p.sensorId);
        if (p.quality !== null && !['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED'].includes(p.quality)) return { accepted: false, reason: 'INVALID_QUALITY' };
        this.acq.get(p.sensorId).forcedQuality = p.quality === 'GOOD' ? null : p.quality;
        return { accepted: true };
      }
      case 'quality-showcase': {
        // First four Sensors of the LEFT wall in canonical scan order (G+201..G+204).
        const ids = this.sensors.filter((x) => x.wall === 'LEFT').slice(0, 4).map((x) => x.sensorId);
        const map = Object.fromEntries(ids.map((id, i) => [id, ['UNCERTAIN', 'BAD', 'STALE', 'DISABLED'][i]]));
        for (const [id, q] of Object.entries(map)) this.acq.get(id).forcedQuality = p.enabled === false ? null : q;
        return { accepted: true, detail: map };
      }
      case 'disable-sensor': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
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
        if (p.sensorId && !sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        if (p.sensorId) this.touch(p.sensorId);
        const id = this.raiseAlarm({ code: 'SYN-SENSOR-ALARM', text: `Synthetic alarm ${p.sensorId ?? 'system'}`, severity: p.severity ?? 'HIGH', sensorId: p.sensorId ?? null });
        return { accepted: true, detail: { alarmId: id } };
      }
      case 'ack-alarm':
      case 'clear-alarm': {
        // Optional sensorId scopes the command to that Sensor's alarms (synthetic review).
        const fn = name === 'ack-alarm' ? (id) => this.ackAlarm(id) : (id) => this.clearAlarm(id);
        if (p.sensorId !== undefined) {
          if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
          const ids = this.sensorAlarmIds(p.sensorId);
          for (const id of ids) fn(id);
          return { accepted: true, detail: { affected: ids.length } };
        }
        return { accepted: true, detail: { affected: fn(p.alarmId) } };
      }
      case 'enqueue': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        this.touch(p.sensorId);
        return this.enqueueWithReason(p.sensorId, p.reason ?? 'OPERATOR');
      }
      case 'dequeue': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        const was = this.removeFromQueue(p.sensorId);
        // A Sensor that is still Dirty may be re-admitted at the tail by the synthetic score source.
        return { accepted: true, detail: { removed: was !== null, owner: was?.reason ?? null } };
      }
      case 'set-sensor-score': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        const mode = p.classification;
        if (!['DIRTY', 'CLEANER', null].includes(mode ?? null)) return { accepted: false, reason: 'INVALID_CLASSIFICATION' };
        this.touch(p.sensorId);
        this.setSensorScore(p.sensorId, mode ? REVIEW_SCORE[mode] : null);
        return { accepted: true, detail: { score: mode ? REVIEW_SCORE[mode] : null } };
      }
      case 'review-job': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        if (p.enabled === false) {
          const held = this.activeJobs[0]?.targetSensorId === p.sensorId;
          return { accepted: held ? this.abortJob('SYN_REVIEW_CLEAR') : false, reason: held ? null : 'NOT_JOB_TARGET' };
        }
        this.touch(p.sensorId);
        return this.reviewJob(p.sensorId);
      }
      case 'reset-sensor': {
        if (!sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        this.resetReviewSensor(p.sensorId);
        return { accepted: true };
      }
      case 'visual-preset': {
        if (p.preset !== 'reset' && !PRESETS.includes(p.preset)) return { accepted: false, reason: 'UNKNOWN_PRESET' };
        if (p.preset !== 'reset' && !sensorOk(p.sensorId)) return { accepted: false, reason: p.sensorId ? sensorRefusal(p.sensorId) : 'SENSOR_REQUIRED' };
        return this.visualPreset(p.preset, p.sensorId);
      }
      case 'queue-mixed-sources':
        return this.mixedQueueScenario();
      case 'pause-auto-sequence':
      case 'hold-queue':
        // AutoSequence (dispatch control) pause. Not a queue entry state: entries stay unchanged.
        this.autoSequencePaused = (p.paused ?? p.held) !== false;
        return { accepted: true, detail: { autoSequence: this.autoSequenceState() } };
      case 'auto-jobs':
        this.autoJobs = p.enabled !== false;
        return { accepted: true };
      case 'dispatch-head':
        // Synthetic manual dispatch of queue Position 1 (head-only, atomic).
        return this.dispatchHead('SYN_SCENARIO_COMMAND');
      case 'start-job': {
        // Synthetic scenario command. Never targets an arbitrary Sensor directly: the Sensor (if
        // given) is first prepared as queue Position 1, then the head is dispatched atomically.
        if (p.sensorId !== undefined && !sensorOk(p.sensorId)) return { accepted: false, reason: sensorRefusal(p.sensorId) };
        if (this.activeJobs.length) {
          this.refusedSecondJobs += 1;
          return { accepted: false, reason: 'ACTIVE_JOB_EXISTS' };
        }
        let prep = null;
        if (p.sensorId !== undefined) {
          this.touch(p.sensorId);
          prep = this.prepareHead(p.sensorId);
          if (!prep.prepared) return { accepted: false, reason: prep.reason ?? 'NOT_PREPARED' };
        }
        const r = this.dispatchHead('SYN_SCENARIO_COMMAND');
        return r.accepted ? { ...r, detail: { ...r.detail, preparedHead: prep !== null, clearedEntries: prep?.cleared ?? 0 } } : r;
      }
      case 'second-job-attempt': {
        // Ensure one job exists (via head-only dispatch), then attempt a concurrent second dispatch
        // and a second start-job. Both must be refused.
        let first = null;
        if (!this.activeJobs.length) {
          if (!this.queue.length) {
            const cand = this.sensors.find((s) => !isCannonId(s.sensorId));
            this.admit(cand.sensorId, QUEUE_REASONS.OPERATOR);
          }
          first = this.dispatchHead('SYN_SCENARIO_COMMAND');
        }
        if (!this.queue.length) {
          const other = this.sensors.find((s) => s.sensorId !== this.activeJobs[0]?.targetSensorId);
          this.admit(other.sensorId, QUEUE_REASONS.OPERATOR);
        }
        const second = this.dispatchHead('SYN_SCENARIO_SECOND_JOB');
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
      queue: { synthetic: true, capacity: QUEUE_CAPACITY, length: this.queue.length, revision: this.queueRevision, dispatches: this.dispatchSeq, autoSequence: this.autoSequenceState(), admissionRejections: this.queueRejections, recentDispatches: this.dispatchLog.slice(-10) },
      commands: cmd,
      pumpStopCommandMs: summarize(this.metrics.pumpStopLatencyMs.toArray(), 3),
      invariants: { violations: this.violationCount, recent: this.violations.slice(-10) },
      trendPoints: this.trend.length,
      configRevision: this.config.revision,
    };
  }
}
