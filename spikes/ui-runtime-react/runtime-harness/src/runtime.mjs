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

// ---------------------------------------------------------------------------------------------
// Mandatory Safe Return and Main Pump critical handling (Owner critical decision).
//
// SYNTHETIC PROOF ONLY — not a physical Pump, protection relay, VFD, Isolation Valve, axis, motion
// profile, or Production interlock. No safety or motion certification is claimed. The state names
// below are synthetic spike names; Production enum / outcome names are NOT approved.
//  * Every Cleaning Job ends through Mandatory Safe Return, whatever the trigger:
//    SR1 stop normal Cleaning / water command -> SR2 command Isolation Valve closed -> SR3 confirm
//    closed -> SR4 command axis return to Standby -> SR5 confirm Standby -> SR6 finalize outcome ->
//    SR7 release Active Job ownership -> SR8 only then may later sequencing be considered.
//  * The Job stays the Active Job throughout; COMPLETED / ABORTED exist only after SR5.
//  * Main Pump unexpected stop / trip: AutoSequence -> CRITICAL_SUSPENDED (no dispatch, no new Job,
//    no automatic Resume), GlobalQueue frozen unchanged, Active Job -> Safe Return, blocking modal.
// ---------------------------------------------------------------------------------------------
export const JOB_LIFECYCLE = Object.freeze({
  RUNNING: 'RUNNING',
  ABORTING: 'ABORTING',
  CLOSE_VALVE: 'SAFE_RETURN_CLOSE_VALVE',
  VERIFY_VALVE: 'SAFE_RETURN_VERIFY_VALVE_CLOSED',
  TO_STANDBY: 'SAFE_RETURN_TO_STANDBY',
  VERIFY_STANDBY: 'SAFE_RETURN_VERIFY_STANDBY',
  FAILED: 'SAFE_RETURN_FAILED',
});
const LIFECYCLE_STEP = Object.freeze({
  ABORTING: ['SR1', 'Aborting — normal Cleaning and water command stopped'],
  SAFE_RETURN_CLOSE_VALVE: ['SR2', 'Mandatory Safe Return — Isolation Valve close commanded'],
  SAFE_RETURN_VERIFY_VALVE_CLOSED: ['SR3', 'Mandatory Safe Return — confirming Isolation Valve closed'],
  SAFE_RETURN_TO_STANDBY: ['SR4', 'Mandatory Safe Return — axis returning to Standby'],
  SAFE_RETURN_VERIFY_STANDBY: ['SR5', 'Mandatory Safe Return — confirming Standby Position'],
  SAFE_RETURN_FAILED: ['SR_FAILED', 'Mandatory Safe Return FAILED — Active Job retained'],
});
export const SAFE_RETURN_TRIGGERS = Object.freeze({
  CLEANING_COMPLETE: 'SYN_CLEANING_PHASES_COMPLETE',
  OPERATOR_ABORT: 'SYN_OPERATOR_ABORT',
  CANCELLED: 'SYN_CANCELLED',
  FAILURE: 'SYN_FAILURE',
  PUMP_UNEXPECTED_STOP: 'SYN_PUMP_UNEXPECTED_STOP',
  PUMP_TRIP: 'SYN_PUMP_TRIP',
  COMMANDED_PUMP_STOP: 'SYN_COMMANDED_PUMP_STOP',
  TEST_RESET: 'SYN_TEST_RESET',
});
const ABORT_CAUSES = Object.freeze({ OPERATOR_ABORT: 'OPERATOR_ABORT', CANCELLED: 'CANCELLED', FAILURE: 'FAILURE' });
export const CRITICAL_KINDS = Object.freeze({ UNEXPECTED_STOP: 'MAIN_PUMP_UNEXPECTED_STOP', TRIP: 'MAIN_PUMP_TRIP' });
export const CRITICAL_SCENARIOS = Object.freeze(['pump-stop-no-job', 'pump-trip-no-job', 'pump-trip-p1', 'pump-trip-p4', 'normal-completion-safe-return']);
const OUTCOME_LOG_CAPACITY = 20;
/** Commands refused while the AutoSequence is CRITICAL_SUSPENDED (queue frozen, no new Job). */
const SUSPENSION_REFUSED = new Set(['enqueue', 'dequeue', 'review-job', 'visual-preset', 'queue-mixed-sources', 'dispatch-head', 'start-job', 'second-job-attempt', 'pump-start', 'reset-sensor']);
const defaultSafeReturnConfig = () => ({ valveFeedbackDelayMs: 0, standbyFeedbackDelayMs: 0, valveFeedback: 'NORMAL', standbyFeedback: 'NORMAL' });
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
    // Mandatory Safe Return / critical handling (synthetic proof).
    this.evidenceSeq = 0; // monotonic synthetic evidence sequence index (ordering proof)
    this.srConfig = defaultSafeReturnConfig(); // synthetic feedback behaviour for the next Safe Return
    this.pumpFault = null; // Main Pump unexpected stop / trip critical event (synthetic)
    this.criticalSeq = 0;
    this.criticalSuspended = false; // AutoSequence CRITICAL_SUSPENDED (sequence authority, never a queue state)
    this.criticalAt = null; // { dispatchSeq, queueRevision } at suspension (invariant evidence)
    this.lastJobOutcome = null;
    this.jobOutcomeLog = [];
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
      const jetting = this.isJetting(this.activeJobs[0], Date.now());
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
        if (this.isJetting(this.activeJobs[0], Date.now())) pr.trueScore = pr.trueScore * 0.8;
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
    // Critical suspension freezes the GlobalQueue unchanged (FIFO preserved); no entry state added.
    if (this.criticalSuspended) return this.rejectAdmission('CRITICAL_SUSPENDED');
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
    if (this.criticalSuspended) return; // frozen during critical suspension
    for (const s of this.sensors) {
      if (this.queue.length >= QUEUE_CAPACITY) return;
      const cur = this.acq.get(s.sensorId).cur;
      if (!cur || cur.basis !== 'CURRENT' || cur.classification !== 'DIRTY') continue;
      if (this.isQueued(s.sensorId) || this.activeJobs[0]?.targetSensorId === s.sensorId || this.notAdmitted.has(s.sensorId)) continue;
      this.admit(s.sensorId, QUEUE_REASONS.DIRTY_SCORE);
    }
  }
  currentPhaseIndex(job, now) {
    if (job.safeReturn) return job.safeReturn.phaseIndexAtTrigger; // Cleaning phase frozen at SR1
    if (job.heldPhaseIndex !== undefined) return job.heldPhaseIndex;
    return Math.floor((now - job.startedAtMs) / this.params.jobPhaseMs);
  }
  /** Synthetic water output: only a RUNNING Job in P4 jets. Never during Safe Return. */
  isJetting(job, now) {
    return Boolean(job) && job.lifecycle === JOB_LIFECYCLE.RUNNING && this.currentPhaseIndex(job, now) === 3;
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
    if (this.criticalSuspended) {
      this.jobRefusals.CRITICAL_SUSPENDED = (this.jobRefusals.CRITICAL_SUSPENDED ?? 0) + 1;
      return { accepted: false, reason: 'CRITICAL_SUSPENDED' };
    }
    // Synthetic conservative choice: no Job is created while the Pump is not ready (no Job ever
    // waits for the Pump). The Production rule is OWNER DECISION REQUIRED (decision matrix A).
    if (!this.pumpReady()) {
      this.jobRefusals.PUMP_NOT_READY = (this.jobRefusals.PUMP_NOT_READY ?? 0) + 1;
      return { accepted: false, reason: 'PUMP_NOT_READY' };
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
    const job = { jobId, targetSensorId: head.sensorId, jetId: s.jetId, valveId: s.valveId, origin, startedAtMs: now, dispatch: record, lifecycle: JOB_LIFECYCLE.RUNNING, safeReturn: null };
    if (hold) {
      job.heldPhaseIndex = REVIEW_JOB_PHASE;
      job.startedAtMs = now - REVIEW_JOB_PHASE * this.params.jobPhaseMs;
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
  // ---- Mandatory Safe Return (synthetic proof; see the header comment above)
  srEvent(job, step, event, atMs) {
    this.evidenceSeq += 1;
    job.safeReturn.events.push({ seq: this.evidenceSeq, step, event, at: iso(atMs) });
    return this.evidenceSeq;
  }
  /** SR1: stop normal Cleaning / water command; the Job stays Active. */
  enterSafeReturn(job, trigger, atMs) {
    const complete = trigger === SAFE_RETURN_TRIGGERS.CLEANING_COMPLETE;
    const idx = clamp(this.currentPhaseIndex(job, atMs), 0, PHASES.length - 1);
    const c = this.srConfig;
    job.safeReturn = {
      trigger,
      pendingOutcome: complete ? 'COMPLETED' : 'ABORTED',
      phaseIndexAtTrigger: idx,
      cleaningPhasesComplete: complete,
      startedAtMs: atMs,
      stepEnteredAtMs: atMs,
      immediate: false,
      valveFeedback: c.valveFeedback,
      standbyFeedback: c.standbyFeedback,
      valveFeedbackDelayMs: c.valveFeedbackDelayMs,
      standbyFeedbackDelayMs: c.standbyFeedbackDelayMs,
      valve: { valveId: job.valveId, command: 'NOT_COMMANDED', commandSeq: null, commandAtMs: null, feedback: 'NOT_CONFIRMED', feedbackSeq: null, feedbackAtMs: null },
      axis: { command: 'NOT_COMMANDED', commandSeq: null, commandAtMs: null, standby: 'NOT_CONFIRMED', standbySeq: null, standbyAtMs: null },
      events: [],
      failure: null,
      triggerSeq: null,
      stopSeq: null,
    };
    const sr = job.safeReturn;
    sr.triggerSeq = this.srEvent(job, null, `TRIGGER_ACCEPTED ${trigger}`, atMs);
    sr.stopSeq = this.srEvent(job, 'SR1', 'NORMAL_CLEANING_AND_WATER_COMMAND_STOPPED', atMs);
    if (complete) this.srCloseValve(job, atMs);
    else job.lifecycle = JOB_LIFECYCLE.ABORTING;
  }
  /** SR2: command the Isolation Valve closed (always before any axis return command). */
  srCloseValve(job, atMs) {
    const sr = job.safeReturn;
    sr.valve.command = 'CLOSE_COMMANDED';
    sr.valve.commandAtMs = atMs;
    sr.valve.commandSeq = this.srEvent(job, 'SR2', 'ISOLATION_VALVE_CLOSE_COMMANDED', atMs);
    job.lifecycle = JOB_LIFECYCLE.CLOSE_VALVE;
    sr.stepEnteredAtMs = atMs;
  }
  failSafeReturn(job, reason, atMs) {
    const sr = job.safeReturn;
    if (job.lifecycle === JOB_LIFECYCLE.VERIFY_VALVE) sr.valve.feedback = 'ABSENT';
    if (job.lifecycle === JOB_LIFECYCLE.VERIFY_STANDBY) sr.axis.standby = 'ABSENT';
    sr.failure = { reason, atLifecycle: job.lifecycle, atMs, seq: this.srEvent(job, null, `SAFE_RETURN_FAILED ${reason}`, atMs) };
    // Synthetic failure state: no outcome is finalized, Active Job ownership is retained, no
    // further dispatch. The Production failure policy is OWNER DECISION REQUIRED.
    job.lifecycle = JOB_LIFECYCLE.FAILED;
  }
  /**
   * Deterministic step machine. Event times are the step thresholds (not the call time), so the
   * evidence is identical however often it is polled. `immediate` (synthetic test scaffolding)
   * runs the same ordered steps with zero synthetic durations.
   */
  advanceSafeReturn(job, now) {
    const L = JOB_LIFECYCLE;
    const sr = job.safeReturn;
    // Immediate (test scaffolding): never earlier than the latest recorded step time.
    if (sr.immediate) now = Math.max(now, sr.stepEnteredAtMs, sr.valve.commandAtMs ?? 0, sr.axis.commandAtMs ?? 0);
    for (let guard = 0; guard < 12 && this.activeJobs[0] === job; guard += 1) {
      const step = sr.immediate ? 0 : this.params.safeReturnStepMs;
      const fb = sr.immediate ? 0 : this.params.safeReturnFeedbackMs;
      switch (job.lifecycle) {
        case L.ABORTING: {
          const at = sr.stepEnteredAtMs + step;
          if (now < at) return;
          this.srCloseValve(job, at);
          break;
        }
        case L.CLOSE_VALVE: {
          const at = sr.stepEnteredAtMs + step;
          if (now < at) return;
          job.lifecycle = L.VERIFY_VALVE;
          sr.stepEnteredAtMs = at;
          break;
        }
        case L.VERIFY_VALVE: {
          if (sr.valveFeedback === 'ABSENT') {
            const at = sr.valve.commandAtMs + (sr.immediate ? 0 : this.params.safeReturnTimeoutMs);
            if (now >= at) this.failSafeReturn(job, 'ISOLATION_VALVE_CLOSED_FEEDBACK_ABSENT', at);
            return;
          }
          const at = Math.max(sr.stepEnteredAtMs, sr.valve.commandAtMs + fb + (sr.immediate ? 0 : sr.valveFeedbackDelayMs));
          if (now < at) return;
          sr.valve.feedback = 'CLOSED_CONFIRMED';
          sr.valve.feedbackAtMs = at;
          sr.valve.feedbackSeq = this.srEvent(job, 'SR3', 'ISOLATION_VALVE_CLOSED_CONFIRMED', at);
          // SR4 is issued only after SR3 confirmation.
          sr.axis.command = 'RETURN_COMMANDED';
          sr.axis.commandAtMs = at;
          sr.axis.commandSeq = this.srEvent(job, 'SR4', 'AXIS_RETURN_TO_STANDBY_COMMANDED', at);
          job.lifecycle = L.TO_STANDBY;
          sr.stepEnteredAtMs = at;
          break;
        }
        case L.TO_STANDBY: {
          const at = sr.stepEnteredAtMs + step;
          if (now < at) return;
          job.lifecycle = L.VERIFY_STANDBY;
          sr.stepEnteredAtMs = at;
          break;
        }
        case L.VERIFY_STANDBY: {
          if (sr.standbyFeedback === 'ABSENT') {
            const at = sr.axis.commandAtMs + (sr.immediate ? 0 : this.params.safeReturnTimeoutMs);
            if (now >= at) this.failSafeReturn(job, 'STANDBY_POSITION_FEEDBACK_ABSENT', at);
            return;
          }
          const at = Math.max(sr.stepEnteredAtMs, sr.axis.commandAtMs + fb + (sr.immediate ? 0 : sr.standbyFeedbackDelayMs));
          if (now < at) return;
          sr.axis.standby = 'STANDBY_CONFIRMED';
          sr.axis.standbyAtMs = at;
          sr.axis.standbySeq = this.srEvent(job, 'SR5', 'STANDBY_POSITION_CONFIRMED', at);
          this.finalizeJob(job, at);
          return;
        }
        default:
          return;
      }
    }
  }
  /** SR6 finalize the outcome, SR7 release Active Job ownership, SR8 record the sequencing gate. */
  finalizeJob(job, atMs) {
    const sr = job.safeReturn;
    const outcome = sr.pendingOutcome;
    const outcomeSeq = this.srEvent(job, 'SR6', `JOB_OUTCOME_${outcome}`, atMs);
    if (outcome === 'COMPLETED') {
      const pr = this.proc.get(job.targetSensorId);
      pr.trueScore = this.rng.range(5, 15);
      if (pr.target !== null) pr.target = pr.trueScore;
      pr.lastCleanAt = atMs;
      this.jobsCompleted += 1;
    } else {
      this.jobsAborted += 1;
    }
    const releaseSeq = this.srEvent(job, 'SR7', 'ACTIVE_JOB_RELEASED', atMs);
    this.activeJobs.length = 0;
    this.lastJobEnd = outcome === 'COMPLETED' ? 'COMPLETED' : sr.trigger;
    const seqState = this.autoSequenceState();
    const mayConsider = !['OFF', 'PAUSED', 'CRITICAL_SUSPENDED'].includes(seqState);
    this.srEvent(job, 'SR8', mayConsider ? 'LATER_DISPATCH_MAY_BE_CONSIDERED' : `LATER_DISPATCH_NOT_PERMITTED_${seqState}`, atMs);
    const d = job.dispatch;
    this.lastJobOutcome = Object.freeze({
      synthetic: true,
      jobId: job.jobId,
      targetSensorId: job.targetSensorId,
      dispatchId: d.dispatchId,
      queueRevisionBefore: d.queueRevisionBefore,
      queueRevisionAfter: d.queueRevisionAfter,
      queueEntryId: d.queueEntryId,
      phaseAtTrigger: PHASES[sr.phaseIndexAtTrigger][0],
      trigger: sr.trigger,
      cleaningPhasesComplete: sr.cleaningPhasesComplete,
      outcome,
      valveId: job.valveId,
      valveCloseCommandSeq: sr.valve.commandSeq,
      valveClosedConfirmedSeq: sr.valve.feedbackSeq,
      axisReturnCommandSeq: sr.axis.commandSeq,
      standbyConfirmedSeq: sr.axis.standbySeq,
      outcomeSeq,
      releaseSeq,
      autoSequenceAtRelease: seqState,
      finalizedAt: iso(atMs),
      events: Object.freeze(sr.events.map((e) => Object.freeze({ ...e }))),
    });
    this.jobOutcomeLog.push(this.lastJobOutcome);
    if (this.jobOutcomeLog.length > OUTCOME_LOG_CAPACITY) this.jobOutcomeLog.shift();
    this.updateCritical(atMs);
  }
  /**
   * End the Active Job through Mandatory Safe Return (never an instant removal). Returns true when
   * a Job existed. A second trigger during Safe Return is recorded and does not restart it.
   */
  abortJob(trigger, { immediate = false } = {}) {
    const job = this.activeJobs[0];
    if (!job) return false;
    const now = Date.now();
    if (!job.safeReturn) this.enterSafeReturn(job, trigger, now);
    else this.srEvent(job, null, `ADDITIONAL_TRIGGER_NOTED ${trigger}`, now);
    if (immediate) {
      job.safeReturn.immediate = true;
      this.advanceSafeReturn(job, now);
    }
    return true;
  }
  /**
   * Synthetic test scaffolding (presets, scenario preparation, test reset): restore the synthetic
   * feedback devices, then run the ordered Safe Return with zero synthetic durations. The Job still
   * passes SR1..SR8 in order and ends ABORTED with the given trigger.
   */
  syntheticResetJob(trigger = SAFE_RETURN_TRIGGERS.TEST_RESET) {
    this.srConfig = defaultSafeReturnConfig();
    const job = this.activeJobs[0];
    if (!job) return false;
    const sr = job.safeReturn;
    if (sr) {
      sr.valveFeedback = 'NORMAL';
      sr.standbyFeedback = 'NORMAL';
      if (job.lifecycle === JOB_LIFECYCLE.FAILED) {
        job.lifecycle = sr.failure.atLifecycle;
        this.srEvent(job, null, 'SYN_TEST_RESET_FEEDBACK_RESTORED', Date.now());
      }
    }
    return this.abortJob(trigger, { immediate: true });
  }
  advanceJob(now) {
    const job = this.activeJobs[0];
    if (!job) return;
    if (job.safeReturn) {
      this.advanceSafeReturn(job, now);
      return;
    }
    if (job.heldPhaseIndex !== undefined) return; // held review Job: frozen until ended
    const endAt = job.startedAtMs + PHASES.length * this.params.jobPhaseMs;
    if (now >= endAt) {
      // Cleaning phases complete != Job complete: Mandatory Safe Return first.
      this.enterSafeReturn(job, SAFE_RETURN_TRIGGERS.CLEANING_COMPLETE, endAt);
      this.advanceSafeReturn(job, now);
    }
  }
  /**
   * AutoSequence state (sequence authority, never a queue entry state). CRITICAL_SUSPENDED takes
   * precedence and persists until an authorised Resume (future work) — here only the synthetic
   * test reset leaves it. PUMP_NOT_READY: the AutoSequence waits; no Job is created and no queue
   * entry changes (the Production readiness rule is OWNER DECISION REQUIRED).
   */
  autoSequenceState() {
    if (this.criticalSuspended) return 'CRITICAL_SUSPENDED';
    if (!this.autoJobs) return 'OFF';
    if (this.autoSequencePaused) return 'PAUSED';
    if (this.activeJobs.length) return 'JOB_ACTIVE';
    if (!this.pumpReady()) return 'PUMP_NOT_READY';
    if (!this.queue.length) return 'QUEUE_EMPTY';
    return 'READY_TO_DISPATCH';
  }
  /** AutoSequence: dispatch queue Position 1 only, only when READY_TO_DISPATCH. */
  autoDispatch() {
    if (this.autoSequenceState() !== 'READY_TO_DISPATCH') return null;
    return this.dispatchHead('SYN_AUTO_SEQUENCE');
  }

  // ---- Main Pump critical events (synthetic)
  /** Unexpected stop or trip: High-severity critical event. Never a normal Job completion. */
  pumpCritical(kind) {
    if (this.pumpFault?.conditionActive) return { accepted: false, reason: 'CRITICAL_CONDITION_ALREADY_ACTIVE' };
    const now = Date.now();
    const trip = kind === 'TRIP';
    this.setPump(trip ? 'TRIPPED' : 'STOPPED', now);
    this.pump.stopRequestedAt = null;
    // Stop dispatch immediately; no new Job; GlobalQueue frozen unchanged.
    this.criticalSuspended = true;
    this.criticalAt = { dispatchSeq: this.dispatchSeq, queueRevision: this.queueRevision };
    this.criticalSeq += 1;
    const alarmId = this.raiseAlarm({
      code: trip ? 'SYN-MAIN-PUMP-TRIP' : 'SYN-MAIN-PUMP-UNEXPECTED-STOP',
      text: trip ? 'Main Pump tripped (synthetic critical event)' : 'Main Pump stopped unexpectedly (synthetic critical event)',
      severity: 'HIGH',
    });
    const job = this.activeJobs[0] ?? null;
    const trigger = trip ? SAFE_RETURN_TRIGGERS.PUMP_TRIP : SAFE_RETURN_TRIGGERS.PUMP_UNEXPECTED_STOP;
    const phaseAtEvent = job ? PHASES[clamp(this.currentPhaseIndex(job, now), 0, PHASES.length - 1)][0] : null;
    if (job) {
      if (!job.safeReturn) this.enterSafeReturn(job, trigger, now);
      else this.srEvent(job, null, `CRITICAL_EVENT_DURING_SAFE_RETURN ${trigger}`, now);
    }
    this.evidenceSeq += 1;
    this.pumpFault = {
      eventId: `SYN-CRIT-${String(this.criticalSeq).padStart(4, '0')}`,
      kind: trip ? CRITICAL_KINDS.TRIP : CRITICAL_KINDS.UNEXPECTED_STOP,
      raisedAtMs: now,
      evidenceSeq: this.evidenceSeq,
      conditionActive: true,
      clearedAtMs: null,
      acknowledged: false,
      acknowledgedAtMs: null,
      alarmId,
      jobId: job?.jobId ?? null,
      targetSensorId: job?.targetSensorId ?? null,
      phaseAtEvent,
      safeReturnRequired: Boolean(job),
      modalOpen: true,
      modalClosedAtMs: null,
    };
    return { accepted: true, detail: { eventId: this.pumpFault.eventId, autoSequence: this.autoSequenceState(), jobId: job?.jobId ?? null, lifecycle: job?.lifecycle ?? null, queueOrder: this.queueOrder } };
  }
  /** Synthetic condition clear. Does not restart the Pump, does not Resume, does not dispatch. */
  clearPumpCondition() {
    const f = this.pumpFault;
    if (!f?.conditionActive) return { accepted: false, reason: 'NO_ACTIVE_CRITICAL_CONDITION' };
    const now = Date.now();
    f.conditionActive = false;
    f.clearedAtMs = now;
    if (this.pump.state === 'TRIPPED') this.setPump('STOPPED', now);
    this.clearAlarm(f.alarmId);
    this.updateCritical(now);
    return { accepted: true, detail: { modalOpen: f.modalOpen, autoSequence: this.autoSequenceState() } };
  }
  /** Acknowledges the Alarm presentation only (not a clear, not a Resume, not a Job outcome). */
  ackCritical() {
    const f = this.pumpFault;
    if (!f) return { accepted: false, reason: 'NO_CRITICAL_EVENT' };
    if (f.acknowledged) return { accepted: false, reason: 'ALREADY_ACKNOWLEDGED' };
    this.ackAlarm(f.alarmId);
    this.markCriticalAcknowledged(Date.now());
    this.updateCritical(Date.now());
    return { accepted: true, detail: { modalOpen: f.modalOpen, conditionActive: f.conditionActive, autoSequence: this.autoSequenceState() } };
  }
  markCriticalAcknowledged(now) {
    const f = this.pumpFault;
    if (f && !f.acknowledged) {
      f.acknowledged = true;
      f.acknowledgedAtMs = now;
    }
  }
  criticalSafeReturnComplete() {
    const f = this.pumpFault;
    return Boolean(f) && (!f.safeReturnRequired || this.activeJobs[0]?.jobId !== f.jobId);
  }
  /** Modal closes only when: condition cleared AND acknowledged AND Safe Return complete (if any). */
  updateCritical(now) {
    const f = this.pumpFault;
    if (!f || !f.modalOpen) return;
    if (!f.conditionActive && f.acknowledged && this.criticalSafeReturnComplete()) {
      f.modalOpen = false;
      f.modalClosedAtMs = now;
    }
  }
  /**
   * Synthetic test reset of the critical scenario (review tooling only) — NOT a Resume. Restores
   * the synthetic devices, ends any Job through the ordered Safe Return, removes the synthetic
   * critical event, restarts the synthetic Pump, and leaves the AutoSequence IDLE.
   */
  resetCritical() {
    const now = Date.now();
    if (this.activeJobs.length) this.syntheticResetJob(SAFE_RETURN_TRIGGERS.TEST_RESET);
    if (this.activeJobs.length) return { accepted: false, reason: 'SAFE_RETURN_NOT_COMPLETED' };
    if (this.pumpFault) this.alarms.delete(this.pumpFault.alarmId);
    this.pumpFault = null;
    this.criticalSuspended = false;
    this.criticalAt = null;
    this.srConfig = defaultSafeReturnConfig();
    if (this.pump.state !== 'RUNNING' && this.pump.state !== 'STARTING') this.setPump('STARTING', now);
    this.pump.stopRequestedAt = null;
    this.autoJobs = false;
    return { accepted: true, detail: { autoSequence: this.autoSequenceState(), note: 'Synthetic test reset — not a Resume' } };
  }
  /** Deterministic synthetic critical review scenarios (test controls). */
  criticalScenario(name) {
    if (!CRITICAL_SCENARIOS.includes(name)) return { accepted: false, reason: 'UNKNOWN_CRITICAL_SCENARIO' };
    if (this.criticalSuspended || this.pumpFault) return { accepted: false, reason: 'CRITICAL_SCENARIO_ACTIVE' };
    const keepConfig = { ...this.srConfig };
    if (this.activeJobs.length) this.syntheticResetJob(SAFE_RETURN_TRIGGERS.TEST_RESET);
    this.srConfig = keepConfig; // feedback-delay review settings apply to the scenario's Safe Return
    if (name === 'pump-stop-no-job') return this.pumpCritical('UNEXPECTED_STOP');
    if (name === 'pump-trip-no-job') return this.pumpCritical('TRIP');
    if (!this.queue.length) {
      const cand = this.sensors.find((x) => !this.notAdmitted.has(x.sensorId));
      this.admit(cand.sensorId, QUEUE_REASONS.OPERATOR);
    }
    const r = this.dispatchHead('SYN_CRITICAL_SCENARIO');
    if (!r.accepted) return r;
    const job = this.activeJobs[0];
    const now = Date.now();
    const ms = this.params.jobPhaseMs;
    if (name === 'pump-trip-p4') job.startedAtMs = now - Math.round(3.5 * ms); // synthetic time shift into P4
    if (name === 'normal-completion-safe-return') {
      job.startedAtMs = now - (PHASES.length * ms - 2000); // P6 ends in 2 s, then Safe Return
      return { accepted: true, detail: { jobId: job.jobId, dispatch: r.detail.dispatch } };
    }
    const t = this.pumpCritical('TRIP');
    return { ...t, detail: { ...t.detail, dispatch: r.detail.dispatch } };
  }
  advancePump(now) {
    if (this.pump.state === 'STARTING' && now - this.pump.changedAt >= this.params.pumpStartMs) this.setPump('RUNNING', now);
    if (this.pump.state === 'STOPPING' && now - this.pump.changedAt >= this.params.pumpStopMs) this.setPump('STOPPED', now);
  }
  setPump(state, now) {
    // States: RUNNING, STARTING, STOPPING, STOPPED, TRIPPED (synthetic).
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
    if (this.pumpFault && targets.some((a) => a.alarmId === this.pumpFault.alarmId)) {
      this.markCriticalAcknowledged(Date.now()); // keep the critical event in sync (ack != clear)
    }
    for (const a of targets) {
      if (a.state === 'ACTIVE_UNACK') a.state = 'ACTIVE_ACK';
      else if (a.state === 'CLEARED_UNACK') this.alarms.delete(a.alarmId);
    }
    return targets.length;
  }
  clearAlarm(alarmId) {
    // The critical Pump Alarm clears only through the synthetic condition clear (pump-fault-clear).
    const targets = (alarmId ? [this.alarms.get(alarmId)].filter(Boolean) : [...this.alarms.values()]).filter((a) => !(this.pumpFault?.conditionActive && a.alarmId === this.pumpFault.alarmId));
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
    const sr = job.safeReturn;
    const phaseStart = sr ? sr.startedAtMs : job.startedAtMs + idx * this.params.jobPhaseMs;
    const step = LIFECYCLE_STEP[job.lifecycle] ?? null;
    return {
      jobId: job.jobId,
      targetSensorId: job.targetSensorId,
      jetId: job.jetId,
      valveId: job.valveId,
      phase: PHASES[idx][0],
      phaseLabel: step ? step[1] : PHASES[idx][1],
      phaseIndex: idx,
      startedAt: iso(job.startedAtMs),
      phaseStartedAt: iso(phaseStart),
      phaseProgress: sr ? 0 : Math.round(clamp((now - phaseStart) / this.params.jobPhaseMs, 0, 1) * 10) / 10,
      lifecycle: job.lifecycle,
      cleaningPhase: sr ? (sr.cleaningPhasesComplete ? 'CLEANING_PHASES_COMPLETE' : 'CLEANING_STOPPED') : 'IN_PROGRESS',
      dispatch: job.dispatch,
      safeReturn: sr
        ? {
            synthetic: true,
            step: step ? step[0] : null,
            trigger: sr.trigger,
            pendingOutcome: sr.pendingOutcome,
            phaseAtTrigger: PHASES[sr.phaseIndexAtTrigger][0],
            startedAt: iso(sr.startedAtMs),
            valve: { valveId: sr.valve.valveId, command: sr.valve.command, commandSeq: sr.valve.commandSeq, feedback: sr.valve.feedback, feedbackSeq: sr.valve.feedbackSeq },
            axis: { command: sr.axis.command, commandSeq: sr.axis.commandSeq, standby: sr.axis.standby, standbySeq: sr.axis.standbySeq },
            failure: sr.failure ? { reason: sr.failure.reason, atLifecycle: sr.failure.atLifecycle, at: iso(sr.failure.atMs), seq: sr.failure.seq } : null,
            events: sr.events.map((e) => ({ ...e })),
          }
        : null,
    };
  }
  /** AutoSequence, critical event and last Job outcome (synthetic sequence authority view). */
  buildSequence() {
    const f = this.pumpFault;
    const job = this.activeJobs[0];
    return {
      synthetic: true,
      autoSequence: this.autoSequenceState(),
      critical: f
        ? {
            eventId: f.eventId,
            kind: f.kind,
            severity: 'HIGH',
            raisedAt: iso(f.raisedAtMs),
            evidenceSeq: f.evidenceSeq,
            conditionActive: f.conditionActive,
            clearedAt: iso(f.clearedAtMs),
            acknowledged: f.acknowledged,
            acknowledgedAt: iso(f.acknowledgedAtMs),
            alarmId: f.alarmId,
            jobId: f.jobId,
            targetSensorId: f.targetSensorId,
            phaseAtEvent: f.phaseAtEvent,
            safeReturnRequired: f.safeReturnRequired,
            safeReturnComplete: this.criticalSafeReturnComplete(),
            safeReturnFailed: Boolean(f.safeReturnRequired && job?.jobId === f.jobId && job.lifecycle === JOB_LIFECYCLE.FAILED),
            modalOpen: f.modalOpen,
            modalClosedAt: iso(f.modalClosedAtMs),
          }
        : null,
      safeReturnConfig: { ...this.srConfig },
      lastJobOutcome: this.lastJobOutcome,
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
    this.updateCritical(now);
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
      sequence: this.buildSequence(),
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
      sequence: s.sequence.value,
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
    if (this.activeJobs[0]?.targetSensorId === sensorId && this.activeJobs[0].heldPhaseIndex !== undefined) this.syntheticResetJob(SAFE_RETURN_TRIGGERS.TEST_RESET);
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
    if (job?.safeReturn) return { accepted: false, reason: 'SAFE_RETURN_IN_PROGRESS', detail: { jobId: job.jobId, lifecycle: job.lifecycle } };
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
      // Synthetic review reset includes the synthetic critical test reset (never a Resume).
      if (this.criticalSuspended || this.pumpFault) this.resetCritical();
      const ids = [...(this.reviewTouched ?? [])];
      for (const id of ids) this.resetReviewSensor(id);
      if (this.activeJobs[0]?.heldPhaseIndex !== undefined || this.activeJobs[0]?.safeReturn) this.syntheticResetJob(SAFE_RETURN_TRIGGERS.TEST_RESET);
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
    this.syntheticResetJob(SAFE_RETURN_TRIGGERS.TEST_RESET);
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
    this.syntheticResetJob(SAFE_RETURN_TRIGGERS.TEST_RESET);
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
  /** valve close cmd < valve closed confirmed < axis return cmd < standby confirmed < outcome < release. */
  checkSafeReturnOrder(events, jobId, complete = false) {
    const order = ['SR1', 'SR2', 'SR3', 'SR4', 'SR5', 'SR6', 'SR7', 'SR8'];
    const seqOf = (step) => events.find((e) => e.step === step)?.seq ?? null;
    let prev = -1;
    let gap = false;
    for (const step of order) {
      const q = seqOf(step);
      if (q === null) {
        gap = true;
        continue;
      }
      if (gap || q <= prev) {
        this.violation(`Safe Return order violated for ${jobId} at ${step}`);
        return;
      }
      prev = q;
    }
    if (complete && gap) this.violation(`Safe Return incomplete for released ${jobId}`);
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
    // Mandatory Safe Return ordering and critical-suspension invariants (synthetic proof).
    if (job?.safeReturn) this.checkSafeReturnOrder(job.safeReturn.events, job.jobId);
    if (this.lastJobOutcome && this.lastJobOutcome !== this.lastCheckedOutcome) {
      this.checkSafeReturnOrder(this.lastJobOutcome.events, this.lastJobOutcome.jobId, true);
      this.lastCheckedOutcome = this.lastJobOutcome;
    }
    if (this.criticalSuspended && this.criticalAt) {
      if (this.dispatchSeq !== this.criticalAt.dispatchSeq) this.violation('dispatch during critical suspension');
      if (this.queueRevision !== this.criticalAt.queueRevision) this.violation('GlobalQueue changed during critical suspension');
    }
    if (job && job.lifecycle !== JOB_LIFECYCLE.RUNNING && !job.safeReturn) this.violation(`job ${job.jobId} lifecycle without Safe Return record`);
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
    // Critical suspension: the GlobalQueue is frozen unchanged and no Job may be created.
    if (this.criticalSuspended && SUSPENSION_REFUSED.has(name) && !(name === 'visual-preset' && p.preset === 'reset') && !(name === 'review-job' && p.enabled === false)) {
      return { accepted: false, reason: 'CRITICAL_SUSPENDED', detail: { autoSequence: 'CRITICAL_SUSPENDED' } };
    }
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
          return { accepted: held ? this.syntheticResetJob(SAFE_RETURN_TRIGGERS.TEST_RESET) : false, reason: held ? null : 'NOT_JOB_TARGET' };
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
      case 'abort-job': {
        // Synthetic abort cause -> Mandatory Safe Return (the Job stays Active until SR7).
        const cause = p.cause ?? 'OPERATOR_ABORT';
        if (!ABORT_CAUSES[cause]) return { accepted: false, reason: 'INVALID_ABORT_CAUSE' };
        const trigger = SAFE_RETURN_TRIGGERS[cause];
        const job = this.activeJobs[0];
        const ok = this.abortJob(trigger, { immediate: p.immediate === true });
        return ok ? { accepted: true, detail: { jobId: job.jobId, trigger, lifecycle: this.activeJobs[0]?.lifecycle ?? 'RELEASED' } } : { accepted: false, reason: 'NO_ACTIVE_JOB' };
      }
      case 'pump-start': {
        if (this.pump.state === 'RUNNING' || this.pump.state === 'STARTING') return { accepted: false, reason: 'ALREADY_RUNNING' };
        if (this.pumpFault?.conditionActive) return { accepted: false, reason: 'CRITICAL_CONDITION_ACTIVE' };
        this.setPump('STARTING', Date.now());
        this.pump.stopRequestedAt = null;
        return { accepted: true };
      }
      case 'pump-stop': {
        // Expected commanded stop (category A): not a fault, no critical modal. An Active Job
        // still ends through the timed Mandatory Safe Return; no new Job while the Pump is down.
        if (['STOPPED', 'STOPPING', 'TRIPPED'].includes(this.pump.state)) return { accepted: false, reason: 'ALREADY_STOPPED' };
        const now = Date.now();
        const hadJob = this.abortJob(SAFE_RETURN_TRIGGERS.COMMANDED_PUMP_STOP);
        this.setPump('STOPPING', now);
        this.pump.stopRequestedAt = now;
        return { accepted: true, detail: { safeReturn: hadJob, lifecycle: this.activeJobs[0]?.lifecycle ?? null } };
      }
      case 'pump-unexpected-stop':
        return this.pumpCritical('UNEXPECTED_STOP');
      case 'pump-trip':
        return this.pumpCritical('TRIP');
      case 'pump-fault-clear':
        return this.clearPumpCondition();
      case 'critical-alarm-ack':
        return this.ackCritical();
      case 'critical-reset':
        return this.resetCritical();
      case 'critical-scenario':
        return this.criticalScenario(p.scenario);
      case 'safe-return-config': {
        // Synthetic feedback behaviour for subsequent Safe Returns (review tooling only).
        const next = { ...this.srConfig };
        for (const k of ['valveFeedbackDelayMs', 'standbyFeedbackDelayMs']) {
          if (p[k] === undefined) continue;
          const v = Number(p[k]);
          if (!Number.isFinite(v) || v < 0 || v > 15000) return { accepted: false, reason: 'INVALID_DELAY' };
          next[k] = v;
        }
        for (const k of ['valveFeedback', 'standbyFeedback']) {
          if (p[k] === undefined) continue;
          if (!['NORMAL', 'ABSENT'].includes(p[k])) return { accepted: false, reason: 'INVALID_FEEDBACK_MODE' };
          next[k] = p[k];
        }
        this.srConfig = next;
        return { accepted: true, detail: { safeReturnConfig: { ...next } } };
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
