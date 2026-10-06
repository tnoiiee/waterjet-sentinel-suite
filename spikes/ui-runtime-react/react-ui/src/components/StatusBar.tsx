// WJSS Stage 0.2.1A — Top status bar + synthetic close-request control.
// The close guard is an operational usability control only. It is not a safety protection
// and is never hardware fail-safe.
import { useState } from 'react';
import type { CloseRequestEvaluation } from '../../../contracts/operational';
import { requestClose } from '../store/commands';
import { useSlice } from '../store/hooks';
import styles from './Operations.module.css';

export function StatusBar({ onToggleDiagnostics, diagnosticsOpen = false }: { onToggleDiagnostics: () => void; diagnosticsOpen?: boolean }) {
  const meta = useSlice('meta');
  const conn = useSlice('connection');
  const pump = useSlice('pump');
  const job = useSlice('activeJob');
  const alarms = useSlice('alarms');
  const comm = useSlice('communication');
  const config = useSlice('config');
  const [closeEval, setCloseEval] = useState<CloseRequestEvaluation | null>(null);
  const [closeError, setCloseError] = useState<string | null>(null);
  const devicesDown = comm?.devices.filter((d) => d.state !== 'ONLINE').length ?? 0;
  const alarmTotal = alarms ? alarms.activeUnack + alarms.activeAck + alarms.clearedUnack : 0;
  const alarmAttention = alarms ? alarms.activeUnack + alarms.clearedUnack : 0;

  const onClose = async () => {
    setCloseError(null);
    try {
      setCloseEval(await requestClose());
    } catch (e) {
      setCloseError(String((e as Error).message));
    }
  };

  return (
    <header className={styles.statusBar} data-testid="status-bar">
      <div className={styles.statusGroup} role="group" aria-label="Application" data-status-group="app">
        <span className={styles.brand}>WJSS Operations</span>
        <span className={styles.syntheticTag}>SYNTHETIC SPIKE — NOT PRODUCTION</span>
      </div>
      {/* Priority order: Alarm > Process (Job, Pump) > System (connection, devices) > Technical. */}
      <div className={styles.statusGroup} role="group" aria-label="Alarm" data-status-group="alarm">
        <span className={`${styles.pill} ${alarmAttention ? styles.pillAlarm : alarmTotal ? styles.pillAlarmAck : styles.pillOk}`} data-testid="alarm-state">
          {alarmTotal ? (
            <>
              <span className={styles.pillGlyph} aria-hidden="true">
                ▲
              </span>
              Alarms {alarmTotal}
              {alarmAttention ? ` · ${alarmAttention} need ack` : ' · acknowledged'}
            </>
          ) : (
            'Alarms 0'
          )}
        </span>
      </div>
      <div className={styles.statusGroup} role="group" aria-label="Process" data-status-group="process">
        <span className={`${styles.pill} ${job ? styles.pillJob : ''}`} data-testid="job-state">
          {job ? (
            <>
              Job {job.phase} → <b>{job.targetSensorId}</b>
            </>
          ) : (
            'No active job'
          )}
        </span>
        <span className={styles.pill} data-testid="pump-state">
          Pump {pump?.state ?? '-'}
          {pump?.ready ? ' · ready' : ''}
        </span>
      </div>
      <div className={styles.statusGroup} role="group" aria-label="System" data-status-group="system">
        <span className={`${styles.pill} ${styles[`conn_${conn.state}`]}`} data-testid="conn-state">
          {conn.state}
        </span>
        <span className={`${styles.pill} ${devicesDown ? styles.pillWarn : ''}`}>
          Devices {comm ? comm.devices.length - devicesDown : 0}/{comm?.devices.length ?? 0} online
        </span>
      </div>
      <div className={`${styles.statusGroup} ${styles.statusTech}`} role="group" aria-label="Technical" data-status-group="technical">
        <span className={styles.techItem}>rev {meta.revision < 0 ? '-' : meta.revision}</span>
        <span className={styles.techItem}>config r{config?.revision ?? '-'}</span>
      </div>
      <span className={styles.spacer} />
      <button type="button" className={`${styles.btn} ${diagnosticsOpen ? styles.btnActive : ''}`} onClick={onToggleDiagnostics} aria-keyshortcuts="D" aria-expanded={diagnosticsOpen} data-testid="diagnostics-toggle">
        Diagnostics
      </button>
      <button type="button" className={styles.btn} onClick={onClose} data-testid="close-request">
        Request close
      </button>
      {(closeEval || closeError) && (
        <div className={styles.dialog} role="dialog" aria-label="Close request result" data-testid="close-dialog">
          {closeError ? (
            <p>Close request failed: {closeError}</p>
          ) : closeEval!.allowed ? (
            <p data-testid="close-allowed">Close allowed (synthetic). No Active Job and Pump not running.</p>
          ) : (
            <p data-testid="close-refused">Close refused (synthetic): {closeEval!.reasons.map((r) => (r === 'ACTIVE_JOB' ? 'Active Cleaning Job' : 'Pump running')).join(' and ')}.</p>
          )}
          <p className={styles.footnote}>Operational usability control only — not a safety protection and not hardware fail-safe.</p>
          <button type="button" className={styles.btn} onClick={() => (setCloseEval(null), setCloseError(null))}>
            Dismiss
          </button>
        </div>
      )}
    </header>
  );
}
