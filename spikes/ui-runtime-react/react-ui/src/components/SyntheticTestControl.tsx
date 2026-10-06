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
import type { SequenceControls } from '../../../contracts/operational';
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
    group: 'Active Job (isolated test preparation — not an operator Start)',
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
// Critical Main Pump / Mandatory Safe Return review controls (synthetic proof only — never a real
// Pump, protection relay, VFD, Isolation Valve, axis or interlock). Ten Owner-listed controls plus
// a valve-feedback-absent control for reviewing the SAFE_RETURN_FAILED presentation.
const CRITICAL_CONTROLS: Control[] = [
  { id: 'crit-stop-no-job', label: '1 · Pump unexpected stop (no Job)', command: 'critical-scenario', params: { scenario: 'pump-stop-no-job' } },
  { id: 'crit-trip-no-job', label: '2 · Pump trip (no Job)', command: 'critical-scenario', params: { scenario: 'pump-trip-no-job' } },
  { id: 'crit-trip-p1', label: '3 · Pump trip during Job P1', command: 'critical-scenario', params: { scenario: 'pump-trip-p1' } },
  { id: 'crit-trip-p4', label: '4 · Pump trip during Job P4', command: 'critical-scenario', params: { scenario: 'pump-trip-p4' } },
  { id: 'crit-normal-sr', label: '5 · Normal completion → Safe Return', command: 'critical-scenario', params: { scenario: 'normal-completion-safe-return' } },
  { id: 'crit-valve-delay', label: '6 · Delay valve closed feedback (next SR)', command: 'safe-return-config', params: { valveFeedbackDelayMs: 6000 } },
  { id: 'crit-standby-delay', label: '7 · Delay Standby feedback (next SR)', command: 'safe-return-config', params: { standbyFeedbackDelayMs: 6000 } },
  { id: 'crit-clear', label: '8 · Clear Pump fault condition', command: 'pump-fault-clear', params: {} },
  { id: 'crit-ack', label: '9 · Acknowledge critical Alarm', command: 'critical-alarm-ack', params: {} },
  { id: 'crit-reset', label: '10 · Reset critical scenario (test only — not a Resume)', command: 'critical-review-reset', params: {} },
  { id: 'crit-valve-absent', label: 'Valve closed feedback absent (next SR → failure review)', command: 'safe-return-config', params: { valveFeedback: 'ABSENT' } },
];

// Synthetic AutoSequence controls (Owner final closeout). Runtime-authoritative: availability and
// the disabled reason come from sequence.controls; the UI only renders them and sends ONE request
// per click. Never a target parameter: the AutoSequence always dispatches GlobalQueue Position 1.
// Not the Production operator-control model (roles / Pause / Resume policy: OWNER DECISION REQUIRED).
interface SequenceControl extends Control {
  key: keyof SequenceControls;
}
const SEQUENCE_CONTROLS: SequenceControl[] = [
  { id: 'start', key: 'start', label: 'START AUTOSEQUENCE', command: 'autosequence-start', params: {} },
  { id: 'pause', key: 'pauseAfterCurrentJob', label: 'PAUSE AFTER CURRENT JOB', command: 'autosequence-pause-after-current-job', params: {} },
  { id: 'resume', key: 'resume', label: 'RESUME AUTOSEQUENCE', command: 'autosequence-resume', params: {} },
  { id: 'abort', key: 'abortActiveJob', label: 'ABORT ACTIVE JOB', command: 'abort-active-job', params: {} },
  { id: 'reset', key: 'resetCritical', label: 'RESET CRITICAL SCENARIO', command: 'critical-review-reset', params: {} },
  { id: 'pump-start', key: 'pumpStart', label: 'START PUMP (synthetic signal)', command: 'pump-start', params: {} },
];
export const SEQUENCE_REASON_TEXT: Record<string, string> = {
  CRITICAL_RESET_REQUIRED: 'Critical scenario active — clear, acknowledge, complete Safe Return, then RESET CRITICAL SCENARIO',
  CRITICAL_SUSPENDED: 'AutoSequence CRITICAL_SUSPENDED — no Pause / Resume until the critical scenario is reset',
  ACTIVE_JOB_PRESENT: 'An Active Job exists',
  ALREADY_RUNNING: 'AutoSequence already RUNNING',
  PAUSED_USE_RESUME: 'AutoSequence is paused — use RESUME AUTOSEQUENCE',
  PUMP_NOT_READY: 'Pump not ready',
  QUEUE_EMPTY: 'GlobalQueue is empty',
  AUTOSEQUENCE_OFF: 'AutoSequence is OFF',
  PAUSE_ALREADY_REQUESTED: 'Pause already requested — takes effect after the current Job',
  ALREADY_PAUSED: 'AutoSequence already PAUSED',
  PAUSE_REQUESTED_JOB_ACTIVE: 'Pause requested — wait for the current Job to finish Safe Return',
  NOT_PAUSED: 'Only available from PAUSED',
  NO_ACTIVE_JOB: 'No Active Job',
  SAFE_RETURN_IN_PROGRESS: 'Mandatory Safe Return already in progress',
  NO_CRITICAL_SCENARIO: 'No critical scenario to reset',
  PUMP_CONDITION_ACTIVE: 'Pump stop / trip condition still active — clear it first',
  CRITICAL_NOT_ACKNOWLEDGED: 'Critical Alarm not acknowledged',
  SAFE_RETURN_FAILED: 'Safe Return failed — OWNER DECISION REQUIRED (no reset)',
  SAFE_RETURN_INCOMPLETE: 'Mandatory Safe Return not complete — Active Job still held',
  PUMP_ALREADY_RUNNING: 'Pump already running / starting',
};

// Queue → Job control without a Sensor: dispatches queue Position 1 only (head-only, atomic).
const DISPATCH_HEAD: Control = { id: 'dispatch-head', label: 'Dispatch queue head (Position 1)', command: 'dispatch-head', params: {} };

export function SyntheticTestControl({ selectedId }: { selectedId: string | null }) {
  const conn = useSlice('connection');
  const live = conn.state === 'LIVE';
  const sensor = useSensor(selectedId ?? '');
  const seq = useSlice('sequence');
  const queue = useSlice('queue');
  const pump = useSlice('pump');
  const job = useSlice('activeJob');
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

  const generalReason = !live ? 'Disconnected — controls disabled (nothing is queued)' : availability === 'disabled' ? 'Off — start the harness with --synthetic-test-controls' : availability === 'error' ? 'Unavailable' : availability === 'checking' ? 'Checking…' : pending ? 'Request in progress' : null;
  const seqButton = (c: SequenceControl) => {
    const g = seq?.controls?.[c.key];
    const enabled = ready && Boolean(g?.enabled);
    const why = generalReason ?? (g ? (g.enabled ? 'Available' : (SEQUENCE_REASON_TEXT[g.reason ?? ''] ?? g.reason ?? 'Unavailable')) : 'Awaiting runtime state');
    return (
      <div key={c.id} className={styles.ascRow} data-enabled={enabled}>
        <button type="button" className={`${styles.ctrlBtn} ${styles.ascBtn}`} disabled={!enabled} onClick={() => void run(c, false)} data-testid={`asc-${c.id}`} aria-describedby={`asc-${c.id}-reason`}>
          <span className={styles.ctrlSyn}>SYN</span> · {c.label}
        </button>
        <span id={`asc-${c.id}-reason`} className={enabled ? styles.ascReasonOk : styles.ascReason} data-testid={`asc-${c.id}-reason`}>
          {enabled ? 'Available' : why}
        </span>
      </div>
    );
  };
  const crit = seq?.critical ?? null;
  const sr = job?.safeReturn ?? null;
  const srText = job ? (sr ? `${job.lifecycle}${sr.step ? ` · ${sr.step}` : ''}` : 'Not started (Job RUNNING)') : seq?.lastJobOutcome ? `Complete · last ${seq.lastJobOutcome.jobId} ${seq.lastJobOutcome.outcome}` : '—';

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
      <div className={styles.stcGroup} data-testid="asc-group" aria-label="Synthetic AutoSequence control">
        <div className={styles.stcGroupLabel}>Synthetic AutoSequence control (head-only · not the Production control model)</div>
        <dl className={styles.ascFacts} data-testid="asc-facts">
          <dt>AutoSequence</dt>
          <dd data-testid="asc-mode">{seq ? `${seq.mode} · ${seq.autoSequence}` : '—'}</dd>
          <dt>Active Job</dt>
          <dd data-testid="asc-job">{job ? `${job.jobId} · ${job.targetSensorId}` : 'None'}</dd>
          <dt>Queue head</dt>
          <dd data-testid="asc-head">{queue?.entries[0]?.sensorId ?? '—'}</dd>
          <dt>Queue count</dt>
          <dd data-testid="asc-count">{queue ? `${queue.totalQueued} / ${queue.capacity}` : '—'}</dd>
          <dt>Pump</dt>
          <dd data-testid="asc-pump">{pump ? `${pump.state} · ${pump.ready ? 'READY' : 'NOT READY'}` : '—'}</dd>
          <dt>Critical</dt>
          <dd data-testid="asc-critical">{crit ? `${crit.conditionActive ? 'CONDITION ACTIVE' : 'CLEARED'} · ${crit.acknowledged ? 'ACKNOWLEDGED' : 'NOT ACKNOWLEDGED'}` : 'None'}</dd>
          <dt>Safe Return</dt>
          <dd data-testid="asc-sr">{srText}</dd>
        </dl>
        <div className={styles.ascList}>{SEQUENCE_CONTROLS.map(seqButton)}</div>
      </div>
      <div className={styles.stcGroup}>
        <div className={styles.stcGroupLabel}>Presets (selected Sensor)</div>
        <div className={styles.stcGrid}>
          {PRESETS.map((c) => button(c, true))}
          {button(RESET_PRESET, false)}
        </div>
      </div>
      <div className={styles.stcGroup} data-testid="stc-critical-group">
        <div className={styles.stcGroupLabel}>Critical Pump / Safe Return (synthetic proof only)</div>
        <div className={styles.stcGrid}>{CRITICAL_CONTROLS.map((c) => button(c, false))}</div>
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
