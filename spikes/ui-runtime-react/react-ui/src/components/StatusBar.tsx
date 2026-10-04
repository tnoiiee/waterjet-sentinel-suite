// WJSS Stage 0.2.1A — Top status bar + synthetic close-request control.
// The close guard is an operational usability control only. It is not a safety protection
// and is never hardware fail-safe.
import { useState } from 'react';
import type { CloseRequestEvaluation } from '../../../contracts/operational';
import { requestClose } from '../store/commands';
import { useSlice } from '../store/hooks';
import styles from './Operations.module.css';

export function StatusBar({ onToggleDiagnostics }: { onToggleDiagnostics: () => void }) {
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
      <span className={styles.brand}>WJSS Operations</span>
      <span className={styles.syntheticTag}>SYNTHETIC SPIKE — NOT PRODUCTION</span>
      <span className={`${styles.pill} ${styles[`conn_${conn.state}`]}`} data-testid="conn-state">
        {conn.state}
      </span>
      <span className={styles.pill}>rev {meta.revision < 0 ? '-' : meta.revision}</span>
      <span className={styles.pill}>config r{config?.revision ?? '-'}</span>
      <span className={styles.pill} data-testid="pump-state">
        Pump {pump?.state ?? '-'}
        {pump?.ready ? ' · ready' : ''}
      </span>
      <span className={styles.pill}>{job ? `Job ${job.phase} ${job.targetSensorId}` : 'No active job'}</span>
      <span className={`${styles.pill} ${alarms && alarms.activeUnack + alarms.clearedUnack > 0 ? styles.pillAlarm : ''}`}>
        Alarms {alarms ? alarms.activeUnack + alarms.activeAck + alarms.clearedUnack : 0}
      </span>
      <span className={`${styles.pill} ${devicesDown ? styles.pillWarn : ''}`}>Devices {comm ? comm.devices.length - devicesDown : 0}/{comm?.devices.length ?? 0} online</span>
      <span className={styles.spacer} />
      <button type="button" className={styles.btn} onClick={onToggleDiagnostics} aria-keyshortcuts="D">
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
