// WJSS Stage 0.2.1A — SYNTHETIC TEST CONTROL (spike-only Owner review tooling).
//
// Lets the Owner freeze and inspect valid synthetic GlobalQueue / Active Job states. Corrected
// semantics (Owner domain correction): queue entries are ready-to-dispatch only (no status), at
// most 8; Jobs are created only by head-only atomic dispatch; no control retargets a Job to an
// arbitrary Sensor (Review Job prepares the selected Sensor as queue Position 1 first). Boundary:
//   * Available only when the harness runs with --synthetic-test-controls (endpoint 404 otherwise).
//   * The runtime stays authoritative: every control sends ONE synthetic scenario command; the
//     resulting state arrives through the normal Snapshot / Delta stream. No React-side state copy.
//   * The per-run token lives in a ref (memory only), is dropped whenever the connection is not
//     LIVE, and is re-fetched after reconnect. Nothing is queued or replayed while disconnected.
//   * Water Jet reference slots are never selectable, so they can never be targeted; the runtime
//     refuses them as well (CANNON_NOT_A_SENSOR).
// Not part of any Production build without a separate diagnostic-build decision.
import { useEffect, useRef, useState } from 'react';
import { fetchTestControls, sendSyntheticScenario, type ScenarioResult } from '../store/commands';
import { useSensor, useSlice } from '../store/hooks';
import styles from './Operations.module.css';

type Availability = 'checking' | 'enabled' | 'disabled' | 'error';

interface Control {
  id: string;
  label: string;
  command: string;
  params: Record<string, unknown>;
}

const SENSOR_CONTROLS: { group: string; items: Control[] }[] = [
  {
    group: 'Alarm',
    items: [
      { id: 'alarm-raise', label: 'Raise Alarm', command: 'raise-alarm', params: {} },
      { id: 'alarm-clear', label: 'Clear Alarm → ACK REQUIRED', command: 'clear-alarm', params: {} },
      { id: 'alarm-ack', label: 'Acknowledge Alarm', command: 'ack-alarm', params: {} },
    ],
  },
  {
    group: 'Queue',
    items: [
      { id: 'queue-temp', label: 'Admit via synthetic TEMP source', command: 'enqueue', params: { reason: 'TEMP' } },
      { id: 'queue-time', label: 'Admit via synthetic TIME source', command: 'enqueue', params: { reason: 'TIME_DUE' } },
      { id: 'queue-combined', label: 'Admit via synthetic TEMP + TIME source', command: 'enqueue', params: { reason: 'TEMP_AND_TIME' } },
      { id: 'queue-remove', label: 'Remove from GlobalQueue', command: 'dequeue', params: {} },
    ],
  },
  {
    group: 'Quality',
    items: [
      { id: 'q-good', label: 'Set GOOD', command: 'force-quality', params: { quality: 'GOOD' } },
      { id: 'q-uncertain', label: 'Set UNCERTAIN', command: 'force-quality', params: { quality: 'UNCERTAIN' } },
      { id: 'q-bad', label: 'Set BAD', command: 'force-quality', params: { quality: 'BAD' } },
      { id: 'q-stale', label: 'Set STALE', command: 'force-quality', params: { quality: 'STALE' } },
    ],
  },
  {
    group: 'Process',
    items: [
      { id: 'p-dirty', label: 'Set DIRTY', command: 'set-sensor-score', params: { classification: 'DIRTY' } },
      { id: 'p-cleaner', label: 'Set CLEANER', command: 'set-sensor-score', params: { classification: 'CLEANER' } },
    ],
  },
  {
    group: 'Active Job',
    items: [
      { id: 'job-set', label: 'Review Job (make queue head → dispatch)', command: 'review-job', params: { enabled: true } },
      { id: 'job-clear', label: 'End review Job', command: 'review-job', params: { enabled: false } },
    ],
  },
  {
    group: 'Sensor',
    items: [{ id: 'sensor-reset', label: 'Reset selected Sensor', command: 'reset-sensor', params: {} }],
  },
];

const PRESETS: Control[] = [
  { id: 'preset-queued-dirty', label: '1 · Queued DIRTY Sensor', command: 'visual-preset', params: { preset: 'queued-dirty' } },
  { id: 'preset-queued-cleaner', label: '2 · Queued CLEANER (synthetic non-score source)', command: 'visual-preset', params: { preset: 'queued-cleaner-non-score' } },
  { id: 'preset-selected-queued', label: '3 · Selected queued Sensor', command: 'visual-preset', params: { preset: 'selected-queued' } },
  { id: 'preset-dispatched-head', label: '4 · Dispatched head becomes Active Job', command: 'visual-preset', params: { preset: 'dispatched-head-job' } },
  { id: 'preset-alarm-active-job', label: '5 · Alarm on Active Job Sensor', command: 'visual-preset', params: { preset: 'alarm-on-active-job' } },
  { id: 'preset-alarm-not-admitted', label: '6 · Alarm Sensor not admitted (demo; policy pending)', command: 'visual-preset', params: { preset: 'alarm-not-admitted' } },
  { id: 'preset-head-to-job', label: '7 · Queue head → Job atomic transition', command: 'visual-preset', params: { preset: 'head-to-job-transition' } },
];
const RESET_PRESET: Control = { id: 'preset-reset', label: '8 · Reset', command: 'visual-preset', params: { preset: 'reset' } };
// Queue → Job control without a Sensor: dispatches queue Position 1 only (head-only, atomic).
const DISPATCH_HEAD: Control = { id: 'dispatch-head', label: 'Dispatch queue head (Position 1)', command: 'dispatch-head', params: {} };

export function SyntheticTestControl({ selectedId }: { selectedId: string | null }) {
  const conn = useSlice('connection');
  const live = conn.state === 'LIVE';
  const sensor = useSensor(selectedId ?? '');
  const tokenRef = useRef<string | null>(null);
  const [availability, setAvailability] = useState<Availability>('checking');
  const [pending, setPending] = useState(false);
  const [last, setLast] = useState<ScenarioResult | null>(null);

  // Token lifecycle: fetched only while LIVE; dropped as soon as the connection is not LIVE.
  useEffect(() => {
    if (!live) {
      tokenRef.current = null;
      return;
    }
    let cancelled = false;
    setAvailability('checking');
    fetchTestControls()
      .then((info) => {
        if (cancelled) return;
        tokenRef.current = info.enabled ? info.token : null;
        setAvailability(info.enabled ? 'enabled' : 'disabled');
      })
      .catch(() => !cancelled && setAvailability('error'));
    return () => {
      cancelled = true;
    };
  }, [live]);
  useEffect(() => () => void (tokenRef.current = null), []);

  const hasSensor = Boolean(selectedId && sensor && sensor.slotType === 'SENSOR');
  const ready = availability === 'enabled' && live && !pending && tokenRef.current !== null;
  const reasonDisabled = !live ? 'Disconnected — controls disabled (nothing is queued)' : availability === 'disabled' ? 'Off — start the harness with --synthetic-test-controls' : availability === 'error' ? 'Unavailable' : availability === 'checking' ? 'Checking…' : !hasSensor ? 'Select a Sensor on the map' : null;

  const run = async (c: Control, needsSensor: boolean) => {
    const token = tokenRef.current;
    if (!token || !live || pending) return;
    if (needsSensor && !hasSensor) return;
    setPending(true);
    try {
      setLast(await sendSyntheticScenario(token, c.command, needsSensor ? { ...c.params, sensorId: selectedId } : c.params));
    } catch (e) {
      setLast({ command: c.command, accepted: false, reason: String((e as Error).message) });
    } finally {
      setPending(false);
    }
  };

  const button = (c: Control, needsSensor: boolean) => (
    <button key={c.id} type="button" className={styles.ctrlBtn} disabled={!ready || (needsSensor && !hasSensor)} onClick={() => void run(c, needsSensor)} data-testid={`stc-${c.id}`} title={`Synthetic: ${c.label}`}>
      <span className={styles.ctrlSyn}>SYN</span> {c.label}
    </button>
  );

  return (
    <section className={styles.stc} aria-label="Synthetic test control" data-testid="synthetic-test-control" data-availability={availability} data-live={live}>
      <h3 className={styles.stcTitle}>SYNTHETIC TEST CONTROL</h3>
      <p className={styles.stcNote}>Spike review tooling only — every control is Synthetic. The runtime stays authoritative.</p>
      <div className={styles.stcTarget}>
        <span className={styles.stcTargetLabel}>Selected Sensor</span>
        <span className={styles.stcTargetId} data-testid="stc-selected">
          {hasSensor ? selectedId : '—'}
        </span>
      </div>
      {reasonDisabled && (
        <p className={styles.stcState} data-testid="stc-state">
          {reasonDisabled}
        </p>
      )}
      <div className={styles.stcGroup}>
        <div className={styles.stcGroupLabel}>Presets (selected Sensor)</div>
        <div className={styles.stcGrid}>
          {PRESETS.map((c) => button(c, true))}
          {button(RESET_PRESET, false)}
        </div>
      </div>
      <div className={styles.stcGroup}>
        <div className={styles.stcGroupLabel}>Queue → Job (head only)</div>
        <div className={styles.stcGrid}>{button(DISPATCH_HEAD, false)}</div>
      </div>
      {SENSOR_CONTROLS.map((g) => (
        <div key={g.group} className={styles.stcGroup}>
          <div className={styles.stcGroupLabel}>{g.group}</div>
          <div className={styles.stcGrid}>{g.items.map((c) => button(c, true))}</div>
        </div>
      ))}
      <p className={styles.stcResult} data-testid="stc-result" aria-live="polite">
        {last ? `${last.command}: ${last.accepted ? 'accepted' : `refused (${last.reason ?? 'unknown'})`}` : 'No synthetic command sent'}
      </p>
    </section>
  );
}
