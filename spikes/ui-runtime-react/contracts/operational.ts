/**
 * WJSS Stage 0.2.1A — synthetic spike contracts (Runtime -> UI presentation model).
 *
 * SYNTHETIC SPIKE CONTRACT — NOT A PRODUCTION CONTRACT.
 * Owner: Stage 0.2.1A spike. Removable with spikes/ui-runtime-react/.
 * The Node harness (runtime-harness/) produces these shapes; the React UI consumes them.
 * Production transport and contract format remain [OPEN].
 */

export const SNAPSHOT_SCHEMA = 'wjss.spike.snapshot/1' as const;
export const DELTA_SCHEMA = 'wjss.spike.delta/1' as const;

export type Wall = 'LEFT' | 'REAR' | 'RIGHT' | 'FRONT';
export type Quality = 'GOOD' | 'UNCERTAIN' | 'BAD' | 'STALE' | 'DISABLED';
export type Classification = 'DIRTY' | 'CLEANER' | 'NOT_CLASSIFIED';
export type ClassificationBasis = 'CURRENT' | 'LAST_VALIDATED' | 'NONE';
/**
 * Per-Sensor GlobalQueue membership (Owner domain correction). QUEUED = the Sensor has a
 * ready-to-dispatch entry in the GlobalQueue (presence means READY); ACTIVE = Active Job target;
 * NONE = not queued. There are no BLOCKED / HELD / WAITING / EXCLUDED entry states.
 */
export type QueueState = 'NONE' | 'QUEUED' | 'ACTIVE';
export type AlarmState = 'NONE' | 'ACTIVE_UNACK' | 'ACTIVE_ACK' | 'CLEARED_UNACK';
export type AlarmSeverity = 'LOW' | 'MEDIUM' | 'HIGH';

/**
 * One logical position of the 18-column x 6-row matrix (108 positions). Delivered once in the
 * Snapshot (`wallMap`); static, never in Deltas. The UI renders wall grids from these slots only.
 * CANNON slots are equipment, not Sensors: no Sensor ID, no Thermocouple channels, no score.
 */
export type SlotType = 'SENSOR' | 'CANNON';

export interface WallMapSlot {
  slotId: string;
  slotType: SlotType;
  wall: Wall;
  /** 1-18 across the whole matrix. */
  logicalColumn: number;
  /** 1-6, top to bottom (G+2xx, G+1xx, G, H, I, J). */
  logicalRow: number;
  /** 1-based column inside the wall (LEFT/RIGHT 1-4, REAR/FRONT 1-5). */
  wallColumn: number;
  /** 1-6 inside the wall; equals logicalRow (no rotation, no reversal). */
  wallRow: number;
  sensorId: string | null;
  equipmentId: string | null;
}

export interface SensorPresentationState {
  sensorId: string;
  slotType: 'SENSOR';
  wall: Wall;
  logicalColumn: number;
  logicalRow: number;
  wallColumn: number;
  wallRow: number;
  /** Synthetic spike scan order 1-106 (not a Production order). */
  scanOrder: number;
  deviceId: string;
  /** Synthetic Thermocouple channel identifiers (TC_F, TC_R). */
  tcFrontChannel: string;
  tcRearChannel: string;
  /** Folded from two Thermocouple channels. null when no current value. */
  dirtyScore: number | null;
  lastValidatedScore: number | null;
  lastValidatedAt: string | null;
  classification: Classification;
  classificationBasis: ClassificationBasis;
  quality: Quality;
  qualityReason: string | null;
  sourceTimestamp: string | null;
  queueState: QueueState;
  isActiveJobTarget: boolean;
  alarmState: AlarmState;
  alarmSeverity: AlarmSeverity | null;
}

export interface WallSummary {
  wall: Wall;
  total: number;
  dirty: number;
  cleaner: number;
  notClassified: number;
  uncertain: number;
  maxScore: number | null;
}

export type JobPhase = 'P1' | 'P2' | 'P3' | 'P4' | 'P5' | 'P6';

export interface ActiveCleaningJobState {
  jobId: string;
  targetSensorId: string;
  jetId: string;
  valveId: string;
  phase: JobPhase;
  phaseLabel: string;
  phaseIndex: number;
  startedAt: string;
  phaseStartedAt: string;
  phaseProgress: number;
  /** Job pre-check lifecycle (P1). Pump readiness waits belong here, never to the queue. */
  preCheck: 'PASSED' | 'WAITING_FOR_PUMP';
  /** Synthetic dispatch evidence linking this Job to the queue head it was created from. */
  dispatch: DispatchRecord;
}

/**
 * Synthetic dispatch record (spike evidence, NOT a production audit record). Created atomically
 * when queue Position 1 is removed and exactly one Cleaning Job is created for that Sensor.
 */
export interface DispatchRecord {
  dispatchId: string;
  synthetic: true;
  queueRevisionBefore: number;
  queueRevisionAfter: number;
  queueEntryId: string;
  /** Always 1: only the queue head is ever dispatched (no scan-forward). */
  positionBefore: 1;
  sensorId: string;
  sourceReason: string;
  jobId: string;
  origin: string;
  dispatchedAt: string;
}

export type PumpRunState = 'STOPPED' | 'STARTING' | 'RUNNING' | 'STOPPING';

export interface PumpState {
  state: PumpRunState;
  pressure: number | null;
  setpoint: number;
  readyBandLow: number;
  readyBandHigh: number;
  ready: boolean;
  stopRequestedAt: string | null;
}

/** One ready-to-dispatch GlobalQueue entry. No per-entry status: presence means READY. */
export interface QueueEntry {
  position: number;
  entryId: string;
  sensorId: string;
  sourceReason: string;
  dirtyScore: number | null;
  secondsSinceLastClean: number;
}

/** AutoSequence (dispatch control) state. Operator pause lives here, never in queue entries. */
export type AutoSequenceState = 'OFF' | 'PAUSED' | 'JOB_ACTIVE' | 'QUEUE_EMPTY' | 'READY_TO_DISPATCH';

/** Synthetic Queue Eligibility Diagnostics row (only when a test scenario explicitly sets it). */
export interface EligibilityDiagnostic {
  sensorId: string;
  decision: 'NOT_ADMITTED';
  reason: string;
  synthetic: true;
}

/**
 * The whole bounded synthetic GlobalQueue (FIFO). `entries.length <= capacity` (8) physically;
 * `totalQueued === entries.length` — there is no hidden overflow and no preview cut.
 */
export interface QueueSummary {
  synthetic: true;
  label: string;
  capacity: 8;
  totalQueued: number;
  /** Monotonic queue revision; bumps on every membership change (admit, remove, dispatch). */
  revision: number;
  entries: QueueEntry[];
  autoSequence: AutoSequenceState;
  lastDispatch: DispatchRecord | null;
  eligibilityDiagnostics: EligibilityDiagnostic[];
}

export interface AlarmItem {
  alarmId: string;
  code: string;
  text: string;
  severity: AlarmSeverity;
  state: Exclude<AlarmState, 'NONE'>;
  sensorId: string | null;
  deviceId: string | null;
  raisedAt: string;
}

export interface AlarmSummary {
  activeUnack: number;
  activeAck: number;
  clearedUnack: number;
  items: AlarmItem[];
}

export type DeviceLinkState = 'ONLINE' | 'TIMEOUT' | 'RECOVERING' | 'STALE_SOURCE';

export interface DeviceHealth {
  deviceId: string;
  state: DeviceLinkState;
  consecutiveTimeouts: number;
  lastSuccessAt: string | null;
  lastLatencyMs: number | null;
  pollsOk: number;
  pollsFailed: number;
}

export interface CommunicationHealth {
  devices: DeviceHealth[];
}

export interface HistorianHealth {
  depth: number;
  capacity: number;
  nearOverflow: boolean;
  rejected: number;
  lastBatchLatencyMs: number | null;
  delayMs: number;
  gapMarkers: number;
}

export interface RuntimeHealth {
  uptimeS: number;
  rssMb: number;
  heapUsedMb: number;
  eventLoopP99Ms: number;
  sseClients: number;
  historian: HistorianHealth;
  invariantViolations: number;
  acceptedSecondJobs: number;
  refusedSecondJobs: number;
}

export interface PublishedConfigurationRevision {
  revision: number;
  publishedAt: string;
  dirtyThreshold: number;
  staleThresholdMs: number;
  label: string;
}

export interface TrendPoint {
  t: number;
  series: [number | null, number | null, number | null, number | null];
  setpoint: number;
  jobActive: boolean;
  alarmActive: boolean;
}

export interface TrendWindow {
  capacity: number;
  seriesNames: [string, string, string, string];
  points: TrendPoint[];
}

export interface OperationalSnapshot {
  kind: 'snapshot';
  schema: typeof SNAPSHOT_SCHEMA;
  synthetic: true;
  revision: number;
  generatedAt: string;
  config: PublishedConfigurationRevision;
  /** 108 logical slots: 106 SENSOR + 2 CANNON. Snapshot-only. */
  wallMap: WallMapSlot[];
  sensors: SensorPresentationState[];
  walls: WallSummary[];
  activeJob: ActiveCleaningJobState | null;
  pump: PumpState;
  queue: QueueSummary;
  alarms: AlarmSummary;
  communication: CommunicationHealth;
  runtime: RuntimeHealth;
  trend: TrendWindow;
}

/** Absent key = unchanged. `activeJob: null` = job cleared. */
export interface OperationalDelta {
  kind: 'delta';
  schema: typeof DELTA_SCHEMA;
  synthetic: true;
  previousRevision: number;
  revision: number;
  generatedAt: string;
  config?: PublishedConfigurationRevision;
  sensors?: SensorPresentationState[];
  walls?: WallSummary[];
  activeJob?: ActiveCleaningJobState | null;
  pump?: PumpState;
  queue?: QueueSummary;
  alarms?: AlarmSummary;
  communication?: CommunicationHealth;
  runtime?: RuntimeHealth;
  trendPoint?: TrendPoint;
}

export interface SyntheticScenarioCommand {
  command: string;
  params?: Record<string, unknown>;
}

export interface CommandResult {
  accepted: boolean;
  command: string;
  reason?: string;
  detail?: unknown;
}

export interface CloseRequestEvaluation {
  allowed: boolean;
  reasons: Array<'ACTIVE_JOB' | 'PUMP_RUNNING'>;
  note: string;
}

export interface MeasurementSample {
  source: 'arena-harness' | 'owner-local-browser' | 'owner-local-process';
  t: string;
  metric: string;
  value: number;
  unit: string;
}
