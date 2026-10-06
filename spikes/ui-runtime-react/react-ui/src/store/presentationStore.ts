// WJSS Stage 0.2.1A — React presentation store (external store for useSyncExternalStore).
//
// SYNTHETIC SPIKE. Not a general-purpose state package. Rules:
//   - Map keyed by Sensor ID; whole-record replacement per changed Sensor
//   - per-Sensor subscriptions: a Delta notifies only the Sensors it contains
//   - panel slices notify only when present in the Delta
//   - a Delta is applied only if previousRevision === current revision; otherwise 'gap'
//   - a Snapshot replaces everything (no merge with stale cache, no inference)
//   - the trend is a bounded ring buffer
//   - selection is UI-local and is NOT stored here

import type {
  ActiveCleaningJobState,
  AlarmSummary,
  CommunicationHealth,
  OperationalDelta,
  OperationalSnapshot,
  PublishedConfigurationRevision,
  PumpState,
  QueueSummary,
  RuntimeHealth,
  SensorPresentationState,
  SequenceState,
  TrendPoint,
  Wall,
  WallMapSlot,
  WallSummary,
} from '../../../contracts/operational';
import { RingBuffer } from '../lib/ringBuffer';

type Listener = () => void;

export type ConnectionState = 'CONNECTING' | 'LIVE' | 'DISCONNECTED' | 'RESYNCING';

export interface ConnectionInfo {
  state: ConnectionState;
  lastMessageAt: number | null;
  lastSnapshotAt: number | null;
  reconnects: number;
  gaps: number;
  snapshots: number;
  deltas: number;
  lastMessageBytes: number;
  lastLagMs: number | null;
}

export interface Meta {
  revision: number;
  generatedAt: string | null;
  hasSnapshot: boolean;
}

export interface TrendView {
  seriesNames: string[];
  capacity: number;
  version: number;
  points: TrendPoint[];
}

export interface Slices {
  meta: Meta;
  /** Per wall: logical rows (top to bottom), each row's slots in wall-column order. From Snapshot `wallMap` only. */
  layout: WallLayout;
  config: PublishedConfigurationRevision | null;
  walls: WallSummary[];
  activeJob: ActiveCleaningJobState | null;
  pump: PumpState | null;
  queue: QueueSummary | null;
  /** AutoSequence / critical Pump event / last Job outcome (runtime-authoritative). */
  sequence: SequenceState | null;
  alarms: AlarmSummary | null;
  communication: CommunicationHealth | null;
  runtime: RuntimeHealth | null;
  trend: TrendView;
  connection: ConnectionInfo;
}
export type SliceKey = keyof Slices;

export type ApplyResult = 'applied' | 'gap' | 'duplicate';

const DELTA_SLICES = ['config', 'walls', 'activeJob', 'pump', 'queue', 'sequence', 'alarms', 'communication', 'runtime'] as const;
export type WallLayout = Record<Wall, WallMapSlot[][]>;
const EMPTY_LAYOUT: WallLayout = { LEFT: [], REAR: [], RIGHT: [], FRONT: [] };

/** Groups the runtime-supplied wall map into rows. No wall rules or ID generation here. */
export function layoutFromWallMap(wallMap: readonly WallMapSlot[] | undefined): WallLayout {
  const out: WallLayout = { LEFT: [], REAR: [], RIGHT: [], FRONT: [] };
  for (const slot of wallMap ?? []) {
    const rows = out[slot.wall];
    (rows[slot.wallRow - 1] ??= []).push(slot);
  }
  for (const rows of Object.values(out)) for (const row of rows) row?.sort((a, b) => a.wallColumn - b.wallColumn);
  return out;
}
const layoutKey = (l: WallLayout) => JSON.stringify(Object.values(l).map((rows) => rows.map((r) => r.map((x) => `${x.slotId}:${x.slotType}:${x.sensorId ?? x.equipmentId}`))));

export class PresentationStore {
  private sensors = new Map<string, SensorPresentationState>();
  private sensorListeners = new Map<string, Set<Listener>>();
  private sliceListeners = new Map<SliceKey, Set<Listener>>();
  private trendRing: RingBuffer<TrendPoint>;
  private slices: Slices;

  /** Diagnostics counters (observable in tests and in the Diagnostics overlay). */
  readonly stats = {
    sensorNotifications: 0,
    lastDeltaSensorNotifications: 0,
    sliceNotifications: 0,
    deltasApplied: 0,
    snapshotsApplied: 0,
    gapsDetected: 0,
    duplicatesIgnored: 0,
  };

  constructor(trendCapacity = 600) {
    this.trendRing = new RingBuffer<TrendPoint>(trendCapacity);
    this.slices = {
      meta: { revision: -1, generatedAt: null, hasSnapshot: false },
      layout: EMPTY_LAYOUT,
      config: null,
      walls: [],
      activeJob: null,
      pump: null,
      queue: null,
      sequence: null,
      alarms: null,
      communication: null,
      runtime: null,
      trend: { seriesNames: [], capacity: trendCapacity, version: 0, points: [] },
      connection: { state: 'CONNECTING', lastMessageAt: null, lastSnapshotAt: null, reconnects: 0, gaps: 0, snapshots: 0, deltas: 0, lastMessageBytes: 0, lastLagMs: null },
    };
  }

  // ---------------------------------------------------------------- reads
  getSensor = (id: string): SensorPresentationState | undefined => this.sensors.get(id);
  getSlice = <K extends SliceKey>(key: K): Slices[K] => this.slices[key];
  get revision(): number {
    return this.slices.meta.revision;
  }
  get sensorCount(): number {
    return this.sensors.size;
  }
  get trendLength(): number {
    return this.trendRing.length;
  }

  // ---------------------------------------------------------------- subscriptions
  subscribeSensor = (id: string, cb: Listener): (() => void) => {
    let set = this.sensorListeners.get(id);
    if (!set) this.sensorListeners.set(id, (set = new Set()));
    set.add(cb);
    return () => {
      set!.delete(cb);
      if (set!.size === 0) this.sensorListeners.delete(id);
    };
  };
  subscribe = (key: SliceKey, cb: Listener): (() => void) => {
    let set = this.sliceListeners.get(key);
    if (!set) this.sliceListeners.set(key, (set = new Set()));
    set.add(cb);
    return () => set!.delete(cb);
  };
  listenerCount(id: string): number {
    return this.sensorListeners.get(id)?.size ?? 0;
  }

  private notifySensor(id: string): number {
    const set = this.sensorListeners.get(id);
    if (!set) return 0;
    for (const cb of [...set]) cb();
    this.stats.sensorNotifications += set.size;
    return set.size;
  }
  private notifySlice(key: SliceKey): void {
    const set = this.sliceListeners.get(key);
    if (!set) return;
    this.stats.sliceNotifications += set.size;
    for (const cb of [...set]) cb();
  }
  private setSlice<K extends SliceKey>(key: K, value: Slices[K], pending: Set<SliceKey>): void {
    this.slices = { ...this.slices, [key]: value };
    pending.add(key);
  }

  // ---------------------------------------------------------------- writes
  /** Full replacement. Used on initial connect, reconnect, and after a revision gap. */
  applySnapshot(s: OperationalSnapshot, bytes = 0): void {
    const pending = new Set<SliceKey>();
    const oldIds = [...this.sensors.keys()];
    this.sensors = new Map(s.sensors.map((r) => [r.sensorId, r]));
    const layout = layoutFromWallMap(s.wallMap);
    if (layoutKey(layout) !== layoutKey(this.slices.layout)) this.setSlice('layout', layout, pending);
    this.setSlice('meta', { revision: s.revision, generatedAt: s.generatedAt, hasSnapshot: true }, pending);
    this.setSlice('config', s.config, pending);
    this.setSlice('walls', s.walls, pending);
    this.setSlice('activeJob', s.activeJob, pending);
    this.setSlice('pump', s.pump, pending);
    this.setSlice('queue', s.queue, pending);
    this.setSlice('sequence', s.sequence ?? null, pending);
    this.setSlice('alarms', s.alarms, pending);
    this.setSlice('communication', s.communication, pending);
    this.setSlice('runtime', s.runtime, pending);
    if (this.trendRing.capacity !== s.trend.capacity) this.trendRing = new RingBuffer<TrendPoint>(s.trend.capacity);
    this.trendRing.clear();
    for (const p of s.trend.points) this.trendRing.push(p);
    this.setSlice('trend', { seriesNames: s.trend.seriesNames, capacity: s.trend.capacity, version: this.slices.trend.version + 1, points: this.trendRing.toArray() }, pending);
    const now = Date.now();
    const c = this.slices.connection;
    this.setSlice('connection', { ...c, state: 'LIVE', lastMessageAt: now, lastSnapshotAt: now, snapshots: c.snapshots + 1, lastMessageBytes: bytes, lastLagMs: lag(s.generatedAt, now) }, pending);
    this.stats.snapshotsApplied += 1;
    // Notify every Sensor once (new and removed ids alike), then slices.
    const ids = new Set([...oldIds, ...this.sensors.keys()]);
    for (const id of ids) this.notifySensor(id);
    for (const k of pending) this.notifySlice(k);
  }

  /** Incremental application. Returns 'gap' without applying anything when revisions do not chain. */
  applyDelta(d: OperationalDelta, bytes = 0): ApplyResult {
    const meta = this.slices.meta;
    if (meta.hasSnapshot && d.revision <= meta.revision) {
      this.stats.duplicatesIgnored += 1;
      return 'duplicate';
    }
    if (!meta.hasSnapshot || d.previousRevision !== meta.revision) {
      this.stats.gapsDetected += 1;
      return 'gap';
    }
    const pending = new Set<SliceKey>();
    const changedIds: string[] = [];
    if (d.sensors) {
      for (const r of d.sensors) {
        this.sensors.set(r.sensorId, r);
        changedIds.push(r.sensorId);
      }
    }
    for (const k of DELTA_SLICES) {
      if (k in d) this.setSlice(k, (d as unknown as Record<string, unknown>)[k] as never, pending);
    }
    if (d.trendPoint) {
      this.trendRing.push(d.trendPoint);
      const t = this.slices.trend;
      this.setSlice('trend', { ...t, version: t.version + 1, points: this.trendRing.toArray() }, pending);
    }
    this.setSlice('meta', { revision: d.revision, generatedAt: d.generatedAt, hasSnapshot: true }, pending);
    const now = Date.now();
    const c = this.slices.connection;
    this.setSlice('connection', { ...c, state: 'LIVE', lastMessageAt: now, deltas: c.deltas + 1, lastMessageBytes: bytes, lastLagMs: lag(d.generatedAt, now) }, pending);
    this.stats.deltasApplied += 1;
    // Batched notification: all records are applied first, then listeners run once.
    let n = 0;
    for (const id of changedIds) n += this.notifySensor(id);
    this.stats.lastDeltaSensorNotifications = n;
    for (const k of pending) this.notifySlice(k);
    return 'applied';
  }

  setConnection(patch: Partial<ConnectionInfo>): void {
    const pending = new Set<SliceKey>();
    this.setSlice('connection', { ...this.slices.connection, ...patch }, pending);
    for (const k of pending) this.notifySlice(k);
  }
}

function lag(generatedAt: string, now: number): number | null {
  const t = Date.parse(generatedAt);
  return Number.isNaN(t) ? null : now - t;
}
