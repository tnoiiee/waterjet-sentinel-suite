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
export type QueueState = 'NONE' | 'READY' | 'HELD' | 'BLOCKED' | 'EXCLUDED' | 'ACTIVE';
export type AlarmState = 'NONE' | 'ACTIVE_UNACK' | 'ACTIVE_ACK' | 'CLEARED_UNACK';
export type AlarmSeverity = 'LOW' | 'MEDIUM' | 'HIGH';

export interface SensorPresentationState {
  sensorId: string;
  wall: Wall;
  index: number;
  deviceId: string;
  tcChannels: [string, string];
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

export type QueueEntryStatus = 'READY' | 'HELD' | 'BLOCKED' | 'EXCLUDED';

export interface QueueEntry {
  position: number;
  sensorId: string;
  sourceReason: string;
  dirtyScore: number | null;
  secondsSinceLastClean: number;
  status: QueueEntryStatus;
}

export interface QueueSummary {
  totalQueued: number;
  entries: QueueEntry[];
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
