// WJSS Stage 0.2.1A — Operations panels: Sensor Detail, Active Cleaning Job, GlobalQueue
// preview, Alarm strip, Connection banner. Each subscribes only to the slice it shows.
import type { JobPhase } from '../../../contracts/operational';
import { useSensor, useSlice } from '../store/hooks';
import { toCellVisual } from '../visual/toCellVisual';
import styles from './Operations.module.css';

export function SensorDetail({ sensorId }: { sensorId: string | null }) {
  return (
    <section className={styles.panel} aria-label="Sensor detail" data-testid="sensor-detail">
      <h2 className={styles.panelTitle}>Sensor detail</h2>
      {sensorId ? <SensorDetailBody sensorId={sensorId} /> : <p className={styles.muted}>Select a Sensor cell.</p>}
    </section>
  );
}

function SensorDetailBody({ sensorId }: { sensorId: string }) {
  const s = useSensor(sensorId);
  const config = useSlice('config');
  if (!s) return <p className={styles.muted}>No data for {sensorId}.</p>;
  const v = toCellVisual(s, { selected: true, threshold: config?.dirtyThreshold ?? 50 });
  return (
    <>
      <dl className={styles.kv}>
        <dt>Sensor</dt>
        <dd data-testid="detail-id">{s.sensorId}</dd>
        <dt>Wall / index</dt>
        <dd>
          {s.wall} / {s.index}
        </dd>
        <dt>Classification</dt>
        <dd data-testid="detail-class">
          {s.classification.replace('_', ' ')}
          {s.classificationBasis === 'LAST_VALIDATED' ? ' (last validated)' : ''}
        </dd>
        <dt>Dirty Score</dt>
        <dd>{s.dirtyScore === null ? '--' : s.dirtyScore.toFixed(1)}</dd>
        <dt>Last validated</dt>
        <dd>
          {s.lastValidatedScore === null ? '--' : s.lastValidatedScore.toFixed(1)}
          {s.lastValidatedAt ? ` @ ${new Date(s.lastValidatedAt).toLocaleTimeString()}` : ''}
        </dd>
        <dt>Quality</dt>
        <dd data-testid="detail-quality">
          {s.quality}
          {s.qualityReason ? ` (${s.qualityReason})` : ''}
        </dd>
        <dt>Queue</dt>
        <dd>{s.queueState}</dd>
        <dt>Alarm</dt>
        <dd>{s.alarmState.replace('_', ' ')}</dd>
        <dt>Device / channels</dt>
        <dd>
          {s.deviceId} · {s.tcChannels.join(', ')}
        </dd>
      </dl>
      {v.detailNote && (
        <p className={styles.note} data-testid="detail-note">
          {v.detailNote}
        </p>
      )}
      <p className={styles.synthetic}>SYNTHETIC SPIKE DATA — NOT A PRODUCTION VALUE</p>
    </>
  );
}

const PHASES: JobPhase[] = ['P1', 'P2', 'P3', 'P4', 'P5', 'P6'];

export function ActiveJobPanel() {
  const job = useSlice('activeJob');
  return (
    <section className={styles.panel} aria-label="Active Cleaning Job" data-testid="active-job">
      <h2 className={styles.panelTitle}>Active Cleaning Job</h2>
      {job ? (
        <>
          <div className={styles.jobHead}>
            <strong>{job.jobId}</strong> → {job.targetSensorId} · {job.jetId} / {job.valveId}
          </div>
          <ol className={styles.phases}>
            {PHASES.map((p, i) => (
              <li key={p} className={i < job.phaseIndex ? styles.phaseDone : i === job.phaseIndex ? styles.phaseCurrent : styles.phaseTodo} aria-current={i === job.phaseIndex ? 'step' : undefined}>
                {p}
              </li>
            ))}
          </ol>
          <div className={styles.phaseLabel}>{job.phaseLabel}</div>
          <div className={styles.progress}>
            <div style={{ width: `${Math.round(job.phaseProgress * 100)}%` }} />
          </div>
        </>
      ) : (
        <p className={styles.muted}>No active Cleaning Job.</p>
      )}
      <p className={styles.footnote}>At most one Cleaning Job may be active. Synthetic workload only.</p>
    </section>
  );
}

function fmtAge(s: number): string {
  if (s < 3600) return `${Math.floor(s / 60)} min`;
  if (s < 86400) return `${(s / 3600).toFixed(1)} h`;
  return `${(s / 86400).toFixed(1)} d`;
}

export function QueuePreview() {
  const q = useSlice('queue');
  return (
    <section className={styles.panel} aria-label="GlobalQueue preview" data-testid="queue-preview">
      <h2 className={styles.panelTitle}>
        GlobalQueue preview <span className={styles.muted}>({q?.totalQueued ?? 0} queued, first 8, FIFO)</span>
      </h2>
      <table className={styles.table}>
        <thead>
          <tr>
            <th>#</th>
            <th>Sensor</th>
            <th>Source reason</th>
            <th>Score</th>
            <th>Since clean</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {(q?.entries ?? []).map((e) => (
            <tr key={e.sensorId}>
              <td>{e.position}</td>
              <td>{e.sensorId}</td>
              <td className={styles.reason}>{e.sourceReason.replace(/^SYN_/, '').replaceAll('_', ' ').toLowerCase()}</td>
              <td>{e.dirtyScore === null ? '--' : e.dirtyScore.toFixed(1)}</td>
              <td>{fmtAge(e.secondsSinceLastClean)}</td>
              <td>
                <span className={`${styles.chip} ${styles[`st_${e.status}`]}`}>{e.status}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

export function AlarmStrip() {
  const a = useSlice('alarms');
  const items = a?.items.slice(0, 4) ?? [];
  return (
    <section className={styles.alarmStrip} aria-label="Alarm strip" data-testid="alarm-strip">
      <span className={styles.alarmCounts}>
        Alarms — active unack {a?.activeUnack ?? 0} · active ack {a?.activeAck ?? 0} · cleared, ack required {a?.clearedUnack ?? 0}
      </span>
      {items.map((it) => (
        <span key={it.alarmId} className={`${styles.alarmItem} ${styles[`al_${it.state}`]}`} data-alarm-state={it.state}>
          <b>!</b> {it.code} {it.sensorId ?? it.deviceId ?? ''} — {it.state === 'CLEARED_UNACK' ? 'cleared, acknowledgement required' : it.state === 'ACTIVE_ACK' ? 'active, acknowledged' : 'active, unacknowledged'}
        </span>
      ))}
    </section>
  );
}

export function ConnectionBanner() {
  const c = useSlice('connection');
  if (c.state === 'LIVE') return null;
  const since = c.lastMessageAt ? new Date(c.lastMessageAt).toLocaleTimeString() : 'never';
  return (
    <div className={styles.connBanner} role="status" data-testid="connection-banner" data-state={c.state}>
      {c.state === 'CONNECTING' && 'Connecting to synthetic runtime…'}
      {c.state === 'RESYNCING' && 'Revision gap detected — requesting a fresh authoritative Snapshot…'}
      {c.state === 'DISCONNECTED' && `Connection lost. Showing the last received state (${since}). Values are not updated or inferred until a fresh Snapshot arrives.`}
    </div>
  );
}
