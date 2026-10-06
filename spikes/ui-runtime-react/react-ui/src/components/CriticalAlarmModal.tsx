// WJSS Stage 0.2.1A — High-severity blocking modal for the synthetic Main Pump critical event.
//
// SYNTHETIC SPIKE. Owner critical Pump decision: an unexpected Main Pump stop or trip is a High
// Critical event. The runtime is authoritative for everything shown here (sequence.critical,
// activeJob.safeReturn, sequence.lastJobOutcome); the UI never infers a state.
//   * role="alertdialog", aria-modal, labelled title + description, focus trapped inside.
//   * The rest of the Operations page is `inert` while the modal is open (see App.tsx).
//   * No close (X) button and Escape does not dismiss. The only action is Acknowledge.
//   * Acknowledge is NOT a clear and NOT a Resume. The modal closes only when the runtime reports
//     condition cleared AND acknowledged AND Mandatory Safe Return complete (if a Job was active).
//   * After closing, the AutoSequence remains CRITICAL_SUSPENDED. There is no Resume button
//     (Resume authority: OWNER DECISION REQUIRED).
//   * Not colour-only (text states everywhere), no flashing / animation, polite announcements.
// Not a safety function, not an interlock, not certified. Production behaviour is NOT VERIFIED.
import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import type { ActiveCleaningJobState, CriticalPumpEvent, JobOutcomeRecord } from '../../../contracts/operational';
import { acknowledgeCriticalAlarm } from '../store/commands';
import { useSlice } from '../store/hooks';
import styles from './CriticalAlarmModal.module.css';

const KIND_TITLE: Record<CriticalPumpEvent['kind'], string> = {
  MAIN_PUMP_TRIP: 'MAIN PUMP TRIPPED',
  MAIN_PUMP_UNEXPECTED_STOP: 'MAIN PUMP STOPPED UNEXPECTEDLY',
};
const SR_STEPS: { id: string; label: string }[] = [
  { id: 'SR1', label: 'Cleaning / water stopped' },
  { id: 'SR2', label: 'Isolation Valve close commanded' },
  { id: 'SR3', label: 'Isolation Valve closed confirmed' },
  { id: 'SR4', label: 'Axis return to Standby commanded' },
  { id: 'SR5', label: 'Standby Position confirmed' },
];
const time = (iso: string | null) => (iso ? new Date(iso).toLocaleTimeString() : '--');

type SrView =
  | { kind: 'none' }
  | { kind: 'active'; job: ActiveCleaningJobState }
  | { kind: 'released'; outcome: JobOutcomeRecord }
  | { kind: 'unknown' };

function srView(c: CriticalPumpEvent, job: ActiveCleaningJobState | null, outcome: JobOutcomeRecord | null): SrView {
  if (!c.safeReturnRequired) return { kind: 'none' };
  if (job && job.jobId === c.jobId && job.safeReturn) return { kind: 'active', job };
  if (outcome && outcome.jobId === c.jobId) return { kind: 'released', outcome };
  return { kind: 'unknown' };
}

/** Index of the last completed SR step (0 = none), from runtime evidence only. */
function stepsDone(v: SrView): number {
  if (v.kind === 'released') return 5;
  if (v.kind !== 'active') return 0;
  const done = new Set(v.job.safeReturn?.events.filter((e) => e.step).map((e) => e.step));
  let n = 0;
  for (const s of SR_STEPS) if (done.has(s.id as never)) n += 1;
  return n;
}

function srSummary(v: SrView): string {
  if (v.kind === 'none') return 'Not required — no Active Job at the event';
  if (v.kind === 'unknown') return 'Awaiting runtime evidence';
  if (v.kind === 'released') return `COMPLETE — Standby confirmed · Job ${v.outcome.jobId} released · outcome ${v.outcome.outcome}`;
  const sr = v.job.safeReturn!;
  if (sr.step === 'SR_FAILED') return `FAILED — ${sr.failure?.reason ?? 'unknown'} · Active Job retained · OWNER DECISION REQUIRED`;
  return `IN PROGRESS — ${sr.step} · ${v.job.phaseLabel}`;
}

export function CriticalAlarmModal() {
  const seq = useSlice('sequence');
  const job = useSlice('activeJob');
  const conn = useSlice('connection');
  const c = seq?.critical ?? null;
  const open = Boolean(c?.modalOpen);
  const dialogRef = useRef<HTMLDivElement>(null);
  const ackRef = useRef<HTMLButtonElement>(null);
  const [pending, setPending] = useState(false);
  const [ackError, setAckError] = useState<string | null>(null);
  const live = conn.state === 'LIVE';
  const canAck = open && live && !pending && c !== null && !c.acknowledged;

  // Initial focus and focus containment (focus may also sit in the spike Diagnostics drawer,
  // which is opt-in synthetic review tooling rendered above the modal).
  useEffect(() => {
    if (!open) return;
    const target = ackRef.current && !ackRef.current.disabled ? ackRef.current : dialogRef.current;
    target?.focus();
    const onFocusIn = (e: FocusEvent) => {
      const t = e.target as Node | null;
      if (!t || dialogRef.current?.contains(t)) return;
      if ((t as Element).closest?.('[data-testid="diagnostics"]')) return;
      (ackRef.current && !ackRef.current.disabled ? ackRef.current : dialogRef.current)?.focus();
    };
    document.addEventListener('focusin', onFocusIn);
    return () => document.removeEventListener('focusin', onFocusIn);
  }, [open]);
  // When the acknowledge button becomes disabled, keep focus inside the dialog.
  useEffect(() => {
    if (open && !canAck && document.activeElement === ackRef.current) dialogRef.current?.focus();
  }, [open, canAck]);

  if (!open || !c) return null;
  const v = srView(c, job, seq?.lastJobOutcome ?? null);
  const done = stepsDone(v);
  const failed = v.kind === 'active' && v.job.safeReturn?.step === 'SR_FAILED';
  const sr = v.kind === 'active' ? v.job.safeReturn : null;
  const valve = sr ? `${sr.valve.valveId} · ${sr.valve.command.replace('_', ' ')} · ${sr.valve.feedback.replaceAll('_', ' ')}` : v.kind === 'released' ? `${v.outcome.valveId} · CLOSE COMMANDED · CLOSED CONFIRMED` : '--';
  const axis = sr ? `${sr.axis.command.replace('_', ' ')} · ${sr.axis.standby.replaceAll('_', ' ')}` : v.kind === 'released' ? 'RETURN COMMANDED · STANDBY CONFIRMED' : '--';
  const announce = `Critical alarm. ${KIND_TITLE[c.kind]}. Condition ${c.conditionActive ? 'active' : 'cleared'}. ${c.acknowledged ? 'Acknowledged' : 'Not acknowledged'}. Safe Return ${srSummary(v)}.`;

  const onKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Escape') {
      // Escape never dismisses the critical modal.
      e.preventDefault();
      e.stopPropagation();
      return;
    }
    if (e.key === 'Tab') {
      // Single actionable control: keep focus on it (or on the dialog when it is disabled).
      e.preventDefault();
      (canAck ? ackRef.current : dialogRef.current)?.focus();
    }
  };
  const onAck = async () => {
    if (!canAck) return;
    setPending(true);
    setAckError(null);
    try {
      const r = await acknowledgeCriticalAlarm();
      if (!r.accepted) setAckError(r.reason ?? 'refused');
    } catch (err) {
      setAckError(String((err as Error).message));
    } finally {
      setPending(false);
    }
  };

  return (
    <div className={styles.backdrop} data-testid="critical-backdrop">
      <div
        ref={dialogRef}
        className={styles.dialog}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="critical-title"
        aria-describedby="critical-desc critical-response"
        tabIndex={-1}
        onKeyDown={onKeyDown}
        data-testid="critical-modal"
        data-kind={c.kind}
        data-condition={c.conditionActive ? 'ACTIVE' : 'CLEARED'}
        data-acknowledged={c.acknowledged}
        data-safe-return={v.kind === 'active' ? (sr?.step ?? '') : v.kind === 'released' ? 'COMPLETE' : v.kind === 'none' ? 'NOT_REQUIRED' : 'UNKNOWN'}
      >
        <header className={styles.header}>
          <span className={styles.badge}>CRITICAL ALARM</span>
          <span className={styles.severity}>HIGH · synthetic</span>
        </header>
        <h2 id="critical-title" className={styles.title} data-testid="critical-title">
          CRITICAL ALARM — {KIND_TITLE[c.kind]}
        </h2>
        <p id="critical-desc" className={styles.desc}>
          Synthetic Main Pump critical event {c.eventId}. Cleaning output and dispatch are stopped; the AutoSequence is suspended. No Job starts automatically.
        </p>
        <dl className={styles.facts} data-testid="critical-facts">
          <dt>Raised</dt>
          <dd data-testid="critical-time">{time(c.raisedAt)}</dd>
          <dt>Condition</dt>
          <dd data-testid="critical-condition">{c.conditionActive ? 'ACTIVE' : `CLEARED at ${time(c.clearedAt)}`}</dd>
          <dt>Acknowledgement</dt>
          <dd data-testid="critical-ack-state">{c.acknowledged ? `ACKNOWLEDGED at ${time(c.acknowledgedAt)}` : 'NOT ACKNOWLEDGED'}</dd>
          <dt>AutoSequence</dt>
          <dd data-testid="critical-autosequence">{seq?.autoSequence.replace('_', ' ')} — no automatic Resume</dd>
          <dt>Job at event</dt>
          <dd data-testid="critical-job">{c.jobId ? `${c.jobId} · target ${c.targetSensorId} · phase ${c.phaseAtEvent}` : 'No Active Job at the event'}</dd>
          <dt>Mandatory Safe Return</dt>
          <dd data-testid="critical-sr" className={failed ? styles.failed : undefined}>
            {srSummary(v)}
          </dd>
          <dt>Isolation Valve</dt>
          <dd data-testid="critical-valve">{valve}</dd>
          <dt>Axis / Standby</dt>
          <dd data-testid="critical-axis">{axis}</dd>
        </dl>
        {v.kind !== 'none' && (
          <ol className={styles.steps} aria-label="Mandatory Safe Return steps" data-testid="critical-sr-steps">
            {SR_STEPS.map((s, i) => {
              const state = i < done ? 'done' : i === done && !failed && v.kind === 'active' ? 'current' : failed && i === done ? 'failed' : 'todo';
              return (
                <li key={s.id} data-step-state={state} className={styles[`step_${state}`]} aria-current={state === 'current' ? 'step' : undefined}>
                  <span className={styles.stepMark} aria-hidden="true">
                    {state === 'done' ? '✓' : state === 'failed' ? '✕' : state === 'current' ? '›' : '·'}
                  </span>
                  <b>{s.id}</b> {s.label}
                  <span className={styles.srOnly}> — {state === 'done' ? 'done' : state === 'current' ? 'in progress' : state === 'failed' ? 'failed' : 'pending'}</span>
                </li>
              );
            })}
          </ol>
        )}
        <div id="critical-response" className={styles.response}>
          <div className={styles.responseTitle}>Required response</div>
          <ul data-testid="critical-response">
            <li>Cleaning stopped.</li>
            <li>AutoSequence suspended.</li>
            <li>Inspect the Main Pump and the process.</li>
            <li>Do not resume until the cause is cleared and resumption is authorised.</li>
          </ul>
        </div>
        <p className={styles.rule} data-testid="critical-close-rule">
          This dialog closes only when the condition is cleared, Mandatory Safe Return is complete and the alarm is acknowledged. Acknowledge does not clear the condition and does not resume.
        </p>
        <footer className={styles.footer}>
          {!live && <span className={styles.note}>Connection lost — showing the last received state; acknowledge unavailable.</span>}
          {ackError && (
            <span className={styles.note} data-testid="critical-ack-error">
              Acknowledge refused ({ackError})
            </span>
          )}
          <button ref={ackRef} type="button" className={styles.ack} onClick={() => void onAck()} disabled={!canAck} data-testid="critical-ack">
            {c.acknowledged ? 'Acknowledged' : pending ? 'Acknowledging…' : 'Acknowledge'}
          </button>
        </footer>
        <p className={styles.srOnly} aria-live="polite" data-testid="critical-live">
          {announce}
        </p>
      </div>
    </div>
  );
}
