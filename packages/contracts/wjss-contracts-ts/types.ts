/**
 * WJSS Stage 0.3A-1 — structural TypeScript mirror of `Wjss.Contracts`.
 *
 * C# records are AUTHORITATIVE (ADR-0014, draft). This mirror is hand-authored
 * for the future React production workspace (apps/ui, Stage 0.3A-4) and is
 * held honest ONLY through the golden fixtures under ../fixtures/: the .NET
 * fixture generator (tests/integration) is the regeneration authority, the
 * validator (validate.mjs) enforces the structural invariants below.
 *
 * STRUCTURAL ONLY: no queue eligibility policy, no alarm policy, no Safe
 * Return failure outcome policy, no re-queue policy, no roles/permissions, no
 * Production values. Fields documented as opaque strings (trigger/outcome/
 * reasonCode) are open Owner decisions and must NOT be narrowed to enums
 * before the Owner answers the decision matrices.
 */

export const SNAPSHOT_SCHEMA = 'wjss.snapshot/1' as const;
export const DELTA_SCHEMA = 'wjss.delta/1' as const;
export const API_VERSION = 1 as const;

export type Wall = 'LEFT' | 'REAR' | 'RIGHT' | 'FRONT';
export type SlotType = 'SENSOR' | 'CANNON';
export type Quality = 'GOOD' | 'UNCERTAIN' | 'BAD' | 'STALE' | 'DISABLED';
export type Classification = 'DIRTY' | 'CLEANER' | 'NOT_CLASSIFIED';
export type ClassificationBasis = 'CURRENT' | 'LAST_VALIDATED' | 'NONE';
/** Presence in the queue means READY. There are no BLOCKED / HELD / WAITING_* entry states. */
export type QueueState = 'NONE' | 'QUEUED' | 'ACTIVE';
export type AlarmState = 'NONE' | 'ACTIVE_UNACK' | 'ACTIVE_ACK' | 'CLEARED_UNACK';
export type AlarmSeverity = 'LOW' | 'MEDIUM' | 'HIGH';
export type JobPhase = 'P1' | 'P2' | 'P3' | 'P4' | 'P5' | 'P6';
export type JobLifecycle =
  | 'RUNNING'
  | 'ABORTING'
  | 'SAFE_RETURN_CLOSE_VALVE'
  | 'SAFE_RETURN_VERIFY_VALVE_CLOSED'
  | 'SAFE_RETURN_TO_STANDBY'
  | 'SAFE_RETURN_VERIFY_STANDBY'
  | 'SAFE_RETURN_FAILED';
export type SafeReturnStep = 'SR1' | 'SR2' | 'SR3' | 'SR4' | 'SR5' | 'SR6' | 'SR7' | 'SR8' | 'SR_FAILED';
export type AutoSequenceState =
  | 'CRITICAL_SUSPENDED'
  | 'OFF'
  | 'PAUSE_REQUESTED'
  | 'PAUSED'
  | 'JOB_ACTIVE'
  | 'PUMP_NOT_READY'
  | 'QUEUE_EMPTY'
  | 'READY_TO_DISPATCH';
export type AutoSequenceMode = 'OFF' | 'RUNNING' | 'PAUSE_REQUESTED' | 'PAUSED' | 'CRITICAL_SUSPENDED';
export type PumpRunState = 'STOPPED' | 'STARTING' | 'RUNNING' | 'STOPPING' | 'TRIPPED';
export type CriticalPumpKind = 'MAIN_PUMP_UNEXPECTED_STOP' | 'MAIN_PUMP_TRIP';
export type DeviceLinkState = 'ONLINE' | 'TIMEOUT' | 'RECOVERING' | 'STALE_SOURCE';
export type DeviceProfile = 'SIMULATOR' | 'TEST_HARDWARE' | 'PRODUCTION';

/** Label of the safe-return leg command/feedback states (presentation vocabulary; policy names pending). */
export type ValveCommandLabel = 'NOT_COMMANDED' | 'CLOSE_COMMANDED';
export type ValveFeedbackLabel = 'NOT_CONFIRMED' | 'CLOSED_CONFIRMED' | 'ABSENT';
export type AxisCommandLabel = 'NOT_COMMANDED' | 'RETURN_COMMANDED';
export type StandbyLabel = 'NOT_CONFIRMED' | 'STANDBY_CONFIRMED' | 'ABSENT';

export interface WallMapSlot {
  slotId: string;
  slotType: SlotType;
  wall: Wall;
  /** 1-18 across the matrix. */
  logicalColumn: number;
  /** 1-6, top to bottom (G+2xx, G+1xx, G, H, I, J). */
  logicalRow: number;
  /** 1-based inside the wall (LEFT/RIGHT 1-4, REAR/FRONT 1-5). */
  wallColumn: number;
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
  /** Configuration-derived scan ordinal (deployment data; synthetic in fixtures). */
  scanOrder: number;
  deviceId: string;
  tcFrontChannel: string;
  tcRearChannel: string;
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

export interface SafeReturnEvent {
  /** Global monotonic evidence index; strictly increasing (ordering proof). */
  seq: number;
  step: SafeReturnStep | null;
  event: string;
  at: string;
}

export interface SafeReturnValveLeg {
  valveId: string;
  command: ValveCommandLabel;
  commandSeq: number | null;
  feedback: ValveFeedbackLabel;
  feedbackSeq: number | null;
}

export interface SafeReturnAxisLeg {
  command: AxisCommandLabel;
  commandSeq: number | null;
  standby: StandbyLabel;
  standbySeq: number | null;
}

export interface SafeReturnFailure {
  reason: string;
  atLifecycle: JobLifecycle;
  at: string;
  seq: number;
}

export interface SafeReturnState {
  step: SafeReturnStep | null;
  /** Opaque trigger label. Production trigger NAMES are an open Owner decision. */
  trigger: string;
  /** Opaque outcome label finalized at SR6. Production outcome NAMES are an open Owner decision. */
  pendingOutcome: string;
  phaseAtTrigger: JobPhase;
  startedAt: string;
  valve: SafeReturnValveLeg;
  axis: SafeReturnAxisLeg;
  failure: SafeReturnFailure | null;
  events: SafeReturnEvent[];
}

export interface DispatchRecord {
  dispatchId: string;
  queueRevisionBefore: number;
  queueRevisionAfter: number;
  queueEntryId: string;
  /** Always 1: head-only dispatch, no scan-forward. */
  positionBefore: 1;
  sensorId: string;
  sourceReason: string;
  jobId: string;
  origin: string;
  dispatchedAt: string;
}

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
  lifecycle: JobLifecycle;
  cleaningPhase: string;
  dispatch: DispatchRecord;
  safeReturn: SafeReturnState | null;
}

export interface JobOutcomeRecord {
  jobId: string;
  targetSensorId: string;
  dispatchId: string;
  queueRevisionBefore: number;
  queueRevisionAfter: number;
  queueEntryId: string;
  phaseAtTrigger: JobPhase;
  trigger: string;
  cleaningPhasesComplete: boolean;
  /** Production outcome names pending Owner decision; opaque string. */
  outcome: string;
  valveId: string;
  valveCloseCommandSeq: number;
  valveClosedConfirmedSeq: number;
  axisReturnCommandSeq: number;
  standbyConfirmedSeq: number;
  outcomeSeq: number;
  releaseSeq: number;
  autoSequenceAtRelease: AutoSequenceState;
  finalizedAt: string;
  events: SafeReturnEvent[];
}

export interface SequenceControlAvailability {
  enabled: boolean;
  reason: string | null;
}

export interface SequenceControls {
  start: SequenceControlAvailability;
  pauseAfterCurrentJob: SequenceControlAvailability;
  resume: SequenceControlAvailability;
  abortActiveJob: SequenceControlAvailability;
  resetCritical: SequenceControlAvailability;
  pumpStart: SequenceControlAvailability;
}

export interface CriticalPumpEvent {
  eventId: string;
  kind: CriticalPumpKind;
  severity: 'HIGH';
  raisedAt: string;
  evidenceSeq: number;
  conditionActive: boolean;
  clearedAt: string | null;
  acknowledged: boolean;
  acknowledgedAt: string | null;
  alarmId: string;
  jobId: string | null;
  targetSensorId: string | null;
  phaseAtEvent: JobPhase | null;
  safeReturnRequired: boolean;
  safeReturnComplete: boolean;
  safeReturnFailed: boolean;
  modalOpen: boolean;
  modalClosedAt: string | null;
}

export interface SequenceState {
  autoSequence: AutoSequenceState;
  mode: AutoSequenceMode;
  controls: SequenceControls;
  critical: CriticalPumpEvent | null;
  lastJobOutcome: JobOutcomeRecord | null;
}

export interface PumpState {
  state: PumpRunState;
  pressure: number | null;
  setpoint: number;
  readyBandLow: number;
  readyBandHigh: number;
  ready: boolean;
  stopRequestedAt: string | null;
}

export interface QueueEntry {
  position: number;
  entryId: string;
  sensorId: string;
  sourceReason: string;
  dirtyScore: number | null;
  secondsSinceLastClean: number;
}

/** The whole bounded GlobalQueue. At most 8 entries PHYSICALLY; no hidden overflow. */
export interface QueueSummary {
  label: string;
  capacity: 8;
  totalQueued: number;
  revision: number;
  entries: QueueEntry[];
  autoSequence: AutoSequenceState;
  lastDispatch: DispatchRecord | null;
}

export interface AlarmItem {
  alarmId: string;
  code: string;
  text: string;
  severity: AlarmSeverity;
  state: 'ACTIVE_UNACK' | 'ACTIVE_ACK' | 'CLEARED_UNACK';
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
  /** false at Stage 0.3A: no Historian exists; the UI must show NOT WIRED, never "healthy". */
  wired: boolean;
  depth: number;
  capacity: number;
  nearOverflow: boolean;
  rejected: number;
  lastBatchLatencyMs: number | null;
  gapMarkers: number;
}

export interface RuntimeHealth {
  uptimeSeconds: number;
  sseClients: number;
  invariantViolations: number;
  acceptedSecondJobs: number;
  refusedSecondJobs: number;
  historian: HistorianHealth;
  currentRevision: number;
  stageMarker: string;
}

export interface PublishedConfigurationRevision {
  revision: number;
  publishedAt: string;
  label: string;
  dirtyThreshold: number;
  staleThresholdMs: number;
}

export interface TrendPoint {
  /** Unix epoch seconds. */
  t: number;
  /** Fixed-width tuple: null = data gap, never interpolated. */
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
  apiVersion: typeof API_VERSION;
  revision: number;
  generatedAt: string;
  config: PublishedConfigurationRevision;
  deviceProfile: DeviceProfile;
  /** 108 slots: 106 SENSOR + 2 CANNON. Snapshot-only; NEVER in a Delta. */
  wallMap: WallMapSlot[];
  sensors: SensorPresentationState[];
  walls: WallSummary[];
  activeJob: ActiveCleaningJobState | null;
  pump: PumpState;
  queue: QueueSummary;
  sequence: SequenceState;
  alarms: AlarmSummary;
  communication: CommunicationHealth;
  runtime: RuntimeHealth;
  trend: TrendWindow;
  /** Optional review marker; PROVISIONAL fixtures carry it until .NET regeneration replaces it. */
  fixtureStatus?: string;
}

/**
 * Absent key = unchanged. There is NO `activeJob: null` encoding in the
 * Product Delta (spike encoding, rejected by ADR-0014): clearing the Active
 * Job sets `activeJobCleared: true` while omitting `activeJob`.
 */
export interface OperationalDelta {
  kind: 'delta';
  schema: typeof DELTA_SCHEMA;
  apiVersion: typeof API_VERSION;
  /** Must equal the consumer's current revision; otherwise it is a GAP: apply nothing, close, re-snapshot. */
  previousRevision: number;
  revision: number;
  generatedAt: string;
  config?: PublishedConfigurationRevision;
  sensors?: SensorPresentationState[];
  walls?: WallSummary[];
  activeJob?: ActiveCleaningJobState;
  activeJobCleared?: true;
  pump?: PumpState;
  queue?: QueueSummary;
  sequence?: SequenceState;
  alarms?: AlarmSummary;
  communication?: CommunicationHealth;
  runtime?: RuntimeHealth;
  trendPoint?: TrendPoint;
  fixtureStatus?: string;
}

export interface CommandRequest {
  command: string;
  params?: Record<string, string>;
}

export interface CommandOutcome {
  accepted: boolean;
  command: string;
  reasonCode: string | null;
  detail: string | null;
}

export interface CloseRequestEvaluation {
  allowed: boolean;
  reasons: Array<'ACTIVE_JOB' | 'PUMP_RUNNING' | string>;
  note: string;
}

export interface HealthLivePayload {
  status: 'ALIVE';
  host: string;
  deviceProfile: DeviceProfile;
  runtimeImplemented: boolean;
  stageMarker: string;
}

export interface HealthReadyPayload {
  status: string;
  code: string;
  detail: string;
  stageMarker: string;
}
