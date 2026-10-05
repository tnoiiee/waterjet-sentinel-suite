// WJSS Stage 0.2.1A — Operations panels: Sensor Detail, Active Cleaning Job, GlobalQueue
// preview, Alarm strip, Connection banner. Each subscribes only to the slice it shows.
import type { JobPhase } from '../../../contracts/operational';
import { useSensor, useSlice } from '../store/hooks';
import { toCellVisual } from '../visual/toCellVisual';
import { compactReason } from '../visual/queueReason';
import styles from './Operations.module.css';

export function SensorDetail({ sensorId }: { sensorId: string | null }) {
  return (
    <section className={`${styles.panel} ${styles.detailPanel}`} aria-label="Sensor detail" data-testid="sensor-detail">
      <h2 className={styles.panelTitle}>Sensor detail</h2>
      {sensorId ? <SensorDetailBody sensorId={sensorId} /> : <p className={styles.muted}>Select a Sensor cell.</p>}
    </section>
  );
}

/** One inspector row; the full value is always available as a tooltip when it is truncated. */
function Row({ label, value, testId }: { label: string; value: string; testId?: string }) {
  return (
    <>
      <dt>{label}</dt>
      <dd data-testid={testId} title={value}>
        {value}
      </dd>
    </>
  );
}

function SensorDetailBody({ sensorId }: { sensorId: string }) {
  const s = useSensor(sensorId);
  const config = useSlice('config');
  if (!s) return <p className={styles.muted}>No data for {sensorId}.</p>;
  const v = toCellVisual(s, { selected: true, threshold: config?.dirtyThreshold ?? 50 });
  const classification = `${s.classification.replace('_', ' ')}${s.classificationBasis === 'LAST_VALIDATED' ? ' (last validated)' : ''}`;
  const lastValidated = `${s.lastValidatedScore === null ? '--' : s.lastValidatedScore.toFixed(1)}${s.lastValidatedAt ? ` @ ${new Date(s.lastValidatedAt).toLocaleTimeString()}` : ''}`;
  return (
    <>
      <div className={styles.inspector} data-testid="detail-inspector">
        <dl className={styles.inspectorCol} data-testid="detail-col-a">
          <Row label="Sensor" value={s.sensorId} testId="detail-id" />
          <Row label="Wall / position" value={`${s.wall} · logical column ${s.logicalColumn} · logical row ${s.logicalRow}`} testId="detail-position" />
          <Row label="Classification" value={classification} testId="detail-class" />
          <Row label="Dirty Score" value={s.dirtyScore === null ? '--' : s.dirtyScore.toFixed(1)} />
          <Row label="Quality" value={`${s.quality}${s.qualityReason ? ` (${s.qualityReason})` : ''}`} testId="detail-quality" />
          <Row label="Last validated" value={lastValidated} />
        </dl>
        <dl className={styles.inspectorCol} data-testid="detail-col-b">
          <Row label="Queue" value={s.queueState} />
          <Row label="Alarm" value={s.alarmState.replace('_', ' ')} />
          <Row label="Device" value={s.deviceId} testId="detail-device" />
          <Row label="TC_F channel" value={s.tcFrontChannel} testId="detail-tcf" />
          <Row label="TC_R channel" value={s.tcRearChannel} testId="detail-tcr" />
          <Row label="Data" value="SYNTHETIC — not a Production value" />
        </dl>
      </div>
      {v.detailNote && (
        <p className={styles.noteLine} data-testid="detail-note" title={v.detailNote}>
          {v.detailNote}
        </p>
      )}
    </>
  );
}

const PHASES: JobPhase[] = ['P1', 'P2', 'P3', 'P4', 'P5', 'P6'];

export function ActiveJobPanel() {
  const job = useSlice('activeJob');
  return (
    <section className={`${styles.panel} ${styles.jobPanel}`} aria-label="Active Cleaning Job" data-testid="active-job">
      <h2 className={styles.panelTitle}>Active Cleaning Job</h2>
      {job ? (
        <>
          <div className={styles.jobHead} title={`${job.jobId} → ${job.targetSensorId} · Water Jet ${job.jetId} · Isolation Valve ${job.valveId}`}>
            <strong>{job.jobId}</strong> → <strong>{job.targetSensorId}</strong> · Jet {job.jetId} · Valve {job.valveId}
          </div>
          <ol className={styles.phases}>
            {PHASES.map((p, i) => (
              <li key={p} className={i < job.phaseIndex ? styles.phaseDone : i === job.phaseIndex ? styles.phaseCurrent : styles.phaseTodo} aria-current={i === job.phaseIndex ? 'step' : undefined}>
                {p}
              </li>
            ))}
          </ol>
          <div className={styles.jobStatus} data-testid="job-status">
            <span className={styles.phaseLabel} title={job.phaseLabel}>
              {job.phase} · {job.phaseLabel}
            </span>
            <div className={styles.progress} role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(job.phaseProgress * 100)}>
              <div style={{ width: `${Math.round(job.phaseProgress * 100)}%` }} />
            </div>
            <span>{Math.round(job.phaseProgress * 100)}%</span>
            <span className={styles.muted}>started {new Date(job.startedAt).toLocaleTimeString()}</span>
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
    <section className={`${styles.panel} ${styles.queuePanel}`} aria-label="GlobalQueue preview" data-testid="queue-preview">
      <h2 className={styles.panelTitle}>
        GlobalQueue preview <span className={styles.muted}>({q?.totalQueued ?? 0} queued, first 8, FIFO)</span>
      </h2>
      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <colgroup>
            <col className={styles.colPos} />
            <col className={styles.colSensor} />
            <col />
            <col className={styles.colScore} />
            <col className={styles.colAge} />
            <col className={styles.colStatus} />
          </colgroup>
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
            {(q?.entries ?? []).map((e) => {
              const r = compactReason(e.sourceReason);
              return (
                <tr key={e.sensorId} data-queue-position={e.position}>
                  <td>{e.position}</td>
                  <td className={styles.sensorCol}>{e.sensorId}</td>
                  <td className={styles.reason} title={r.full} data-testid="queue-reason">
                    {r.label}
                  </td>
                  <td>{e.dirtyScore === null ? '--' : e.dirtyScore.toFixed(1)}</td>
                  <td>{fmtAge(e.secondsSinceLastClean)}</td>
                  <td>
                    <span className={`${styles.chip} ${styles[`st_${e.status}`]}`}>{e.status}</span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
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
