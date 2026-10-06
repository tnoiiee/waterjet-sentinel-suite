// WJSS Stage 0.2.1A — Operations panels: Sensor Detail, Active Cleaning Job, GlobalQueue
// preview, Alarm strip, Connection banner. Each subscribes only to the slice it shows.
import type React from 'react';
import type { ActiveCleaningJobState, JobPhase } from '../../../contracts/operational';
import { useSensor, useSlice } from '../store/hooks';
import { toCellVisual } from '../visual/toCellVisual';
import { compactReason } from '../visual/queueReason';
import styles from './Operations.module.css';

export function SensorDetail({ sensorId }: { sensorId: string | null }) {
  return (
    <section className={`${styles.panel} ${styles.detailPanel}`} aria-label="Sensor detail" data-testid="sensor-detail">
      {sensorId ? (
        <SensorDetailBody sensorId={sensorId} />
      ) : (
        <>
          <header className={styles.cardHeader}>
            <h2 className={styles.panelTitle}>Sensor detail</h2>
          </header>
          <p className={styles.muted}>Select a Sensor cell.</p>
        </>
      )}
    </section>
  );
}

type Emphasis = 'strong' | 'normal' | 'low';

/** One inspector row; the full value is always available as a tooltip when it is truncated. */
function Row({ label, value, testId, emphasis = 'normal', tone }: { label: string; value: string; testId?: string; emphasis?: Emphasis; tone?: 'alarm' | 'uncertain' }) {
  return (
    <>
      <dt>{label}</dt>
      <dd data-testid={testId} title={value} data-emphasis={emphasis} className={`${styles[`em_${emphasis}`]} ${tone ? styles[`tone_${tone}`] : ''}`}>
        {tone === 'alarm' && (
          <span className={styles.inlineGlyph} aria-hidden="true">
            ▲{' '}
          </span>
        )}
        {value}
      </dd>
    </>
  );
}

/** A labelled group inside the inspector (no nested card). */
function Group({ title, testId, children }: { title: string; testId: string; children: React.ReactNode }) {
  return (
    <div className={styles.inspectorGroup} role="group" aria-label={title} data-testid={testId}>
      <div className={styles.groupLabel}>{title}</div>
      <dl className={styles.inspectorCol}>{children}</dl>
    </div>
  );
}

function SensorDetailBody({ sensorId }: { sensorId: string }) {
  const s = useSensor(sensorId);
  const config = useSlice('config');
  if (!s)
    return (
      <>
        <header className={styles.cardHeader}>
          <h2 className={styles.panelTitle}>Sensor detail</h2>
        </header>
        <p className={styles.muted}>No data for {sensorId}.</p>
      </>
    );
  const v = toCellVisual(s, { selected: true, threshold: config?.dirtyThreshold ?? 50 });
  const classification = `${s.classification.replace('_', ' ')}${s.classificationBasis === 'LAST_VALIDATED' ? ' (last validated)' : ''}`;
  const lastValidated = `${s.lastValidatedScore === null ? '--' : s.lastValidatedScore.toFixed(1)}${s.lastValidatedAt ? ` @ ${new Date(s.lastValidatedAt).toLocaleTimeString()}` : ''}`;
  return (
    <>
      <header className={styles.cardHeader}>
        <h2 className={styles.panelTitle}>Sensor detail</h2>
        <span className={styles.detailId} data-testid="detail-id">
          {s.sensorId}
        </span>
        <span className={styles.detailClass} data-process={v.process} data-testid="detail-class-chip">
          {s.classification.replace('_', ' ')}
        </span>
        <span className={styles.headerSpacer} />
        <span className={styles.syntheticNote} title="SYNTHETIC — not a Production value">
          synthetic data
        </span>
      </header>
      <div className={styles.inspector} data-testid="detail-inspector">
        <div className={styles.inspectorStack} data-testid="detail-col-a">
          <Group title="Process" testId="detail-group-process">
            <Row label="Classification" value={classification} testId="detail-class" emphasis="strong" />
            <Row label="Dirty Score" value={s.dirtyScore === null ? '--' : s.dirtyScore.toFixed(1)} testId="detail-score" emphasis="strong" />
            <Row label="Last validated" value={lastValidated} emphasis="low" />
          </Group>
          <Group title="Location" testId="detail-group-location">
            <Row label="Wall / position" value={`${s.wall} · logical column ${s.logicalColumn} · logical row ${s.logicalRow}`} testId="detail-position" />
          </Group>
        </div>
        <div className={styles.inspectorStack} data-testid="detail-col-b">
          <Group title="State" testId="detail-group-state">
            <Row label="Quality" value={`${s.quality}${s.qualityReason ? ` (${s.qualityReason})` : ''}`} testId="detail-quality" emphasis="strong" tone={s.quality === 'GOOD' ? undefined : 'uncertain'} />
            <Row label="Queue" value={s.queueState} testId="detail-queue" emphasis="strong" />
            <Row label="Alarm" value={s.alarmState.replace('_', ' ')} testId="detail-alarm" emphasis="strong" tone={s.alarmState === 'NONE' ? undefined : 'alarm'} />
          </Group>
          <Group title="Source" testId="detail-group-source">
            <Row label="Device" value={s.deviceId} testId="detail-device" emphasis="low" />
            <dt>TC_F / TC_R</dt>
            <dd className={styles.em_low} data-emphasis="low">
              <span data-testid="detail-tcf" title={s.tcFrontChannel}>
                {s.tcFrontChannel}
              </span>
              {' / '}
              <span data-testid="detail-tcr" title={s.tcRearChannel}>
                {s.tcRearChannel}
              </span>
            </dd>
          </Group>
        </div>
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

/** Elapsed time since an ISO timestamp, as m:ss or h:mm:ss (presentation only). */
export function fmtElapsed(fromIso: string, nowMs: number = Date.now()): string {
  const total = Math.max(0, Math.floor((nowMs - Date.parse(fromIso)) / 1000));
  if (!Number.isFinite(total)) return '--';
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const sec = String(total % 60).padStart(2, '0');
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${sec}` : `${m}:${sec}`;
}

export function ActiveJobPanel() {
  const job = useSlice('activeJob');
  const pct = job ? Math.round(job.phaseProgress * 100) : 0;
  return (
    <section className={`${styles.panel} ${styles.jobPanel}`} aria-label="Active Cleaning Job" data-testid="active-job">
      <header className={styles.cardHeader}>
        <h2 className={styles.panelTitle}>Active Cleaning Job</h2>
        {job && (
          <span className={styles.jobIdChip} data-testid="job-id">
            {job.jobId}
          </span>
        )}
      </header>
      {job ? (
        <>
          <dl className={styles.jobFacts} data-testid="job-facts">
            <div>
              <dt>Target Sensor</dt>
              <dd className={styles.jobTarget} data-testid="job-target">
                {job.targetSensorId}
              </dd>
            </div>
            <div>
              <dt>Water Jet</dt>
              <dd title={job.jetId}>{job.jetId}</dd>
            </div>
            <div>
              <dt>Isolation Valve</dt>
              <dd title={job.valveId}>{job.valveId}</dd>
            </div>
          </dl>
          <ol className={styles.phases} aria-label="Job phases">
            {PHASES.map((p, i) => (
              <li
                key={p}
                className={i < job.phaseIndex ? styles.phaseDone : i === job.phaseIndex ? styles.phaseCurrent : styles.phaseTodo}
                aria-current={i === job.phaseIndex ? 'step' : undefined}
                data-phase-state={i < job.phaseIndex ? 'done' : i === job.phaseIndex ? 'current' : 'future'}
              >
                {i < job.phaseIndex && (
                  <span className={styles.phaseCheck} aria-hidden="true">
                    ✓{' '}
                  </span>
                )}
                {p}
              </li>
            ))}
          </ol>
          {job.safeReturn ? (
            <SafeReturnStatus job={job} />
          ) : (
            <div className={styles.jobStatus} data-testid="job-status" data-lifecycle={job.lifecycle}>
              <span className={styles.phaseLabel} title={job.phaseLabel}>
                <b>{job.phase}</b> {job.phaseLabel}
              </span>
              <div className={styles.progress} role="progressbar" aria-label="Phase progress" aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct}>
                <div style={{ width: `${pct}%` }} />
              </div>
              <span className={styles.num}>{pct}%</span>
              <span className={styles.jobElapsed} title={`Started ${new Date(job.startedAt).toLocaleTimeString()}`} data-testid="job-elapsed">
                elapsed <span className={styles.num}>{fmtElapsed(job.startedAt)}</span>
              </span>
            </div>
          )}
        </>
      ) : (
        <p className={styles.muted}>No active Cleaning Job.</p>
      )}
      <p className={styles.footnote}>At most one Cleaning Job may be active. Synthetic workload only.</p>
    </section>
  );
}

/**
 * Mandatory Safe Return status (runtime evidence only). The Job remains the Active Job until
 * Standby is confirmed; no outcome is shown until the runtime finalizes it.
 */
function SafeReturnStatus({ job }: { job: ActiveCleaningJobState }) {
  const sr = job.safeReturn!;
  const valve = `${sr.valve.command === 'CLOSE_COMMANDED' ? 'close commanded' : 'not commanded'} · ${sr.valve.feedback.replaceAll('_', ' ').toLowerCase()}`;
  const axis = `${sr.axis.command === 'RETURN_COMMANDED' ? 'return commanded' : 'not commanded'} · ${sr.axis.standby.replaceAll('_', ' ').toLowerCase()}`;
  return (
    <div className={styles.jobStatus} data-testid="job-status" data-lifecycle={job.lifecycle}>
      <span className={styles.phaseLabel} title={`${job.phaseLabel} · Isolation Valve ${valve} · Axis ${axis}`} data-testid="job-safe-return" data-sr-step={sr.step ?? ''}>
        <b>{sr.step === 'SR_FAILED' ? 'SR FAILED' : sr.step}</b> {job.phaseLabel} · Valve {valve} · Axis {axis}
      </span>
      <span className={styles.jobElapsed} title={`Trigger ${sr.trigger} at ${new Date(sr.startedAt).toLocaleTimeString()}`}>
        {sr.trigger.replace('SYN_', '').replaceAll('_', ' ').toLowerCase()}
      </span>
    </div>
  );
}

function fmtAge(s: number): string {
  if (s < 3600) return `${Math.floor(s / 60)} min`;
  if (s < 86400) return `${(s / 3600).toFixed(1)} h`;
  return `${(s / 86400).toFixed(1)} d`;
}

/**
 * Bounded synthetic GlobalQueue (Owner domain correction). Every row is a ready-to-dispatch entry:
 * presence means READY, so there is no per-entry status column or chip. The Status column was
 * removed without replacement — Source reason already has its own column, and no entry age /
 * queued-since value exists in the Runtime contract. Never Production scheduling.
 */
export function QueuePreview() {
  const q = useSlice('queue');
  const count = q?.entries.length ?? 0;
  const capacity = q?.capacity ?? 8;
  return (
    <section className={`${styles.panel} ${styles.queuePanel}`} aria-label="GlobalQueue (synthetic)" data-testid="queue-preview" data-queue-count={count} data-queue-capacity={capacity}>
      <header className={styles.cardHeader}>
        <h2 className={styles.panelTitle} data-testid="queue-title">
          GlobalQueue · synthetic
        </h2>
        <span className={styles.cardMeta} data-testid="queue-count">
          <span className={styles.num}>{count}</span> / <span className={styles.num}>{capacity}</span> queued · FIFO · not Production scheduling
        </span>
      </header>
      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <colgroup>
            <col className={styles.colPos} />
            <col className={styles.colSensor} />
            <col />
            <col className={styles.colScore} />
            <col className={styles.colAge} />
          </colgroup>
          <thead>
            <tr>
              <th className={styles.numCol}>Pos</th>
              <th>Sensor</th>
              <th>Source reason</th>
              <th className={styles.numCol}>Score</th>
              <th className={styles.numCol}>Since clean</th>
            </tr>
          </thead>
          <tbody>
            {(q?.entries ?? []).map((e) => {
              const r = compactReason(e.sourceReason);
              return (
                <tr key={e.entryId} data-queue-position={e.position} data-queue-entry={e.entryId}>
                  <td className={styles.numCol}>{e.position}</td>
                  <td className={styles.sensorCol}>{e.sensorId}</td>
                  <td className={styles.reason} title={r.full} data-testid="queue-reason">
                    {r.label}
                  </td>
                  <td className={styles.numCol}>{e.dirtyScore === null ? '--' : e.dirtyScore.toFixed(1)}</td>
                  <td className={styles.numCol}>{fmtAge(e.secondsSinceLastClean)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

const ALARM_TEXT: Record<string, { condition: string; response: string }> = {
  ACTIVE_UNACK: { condition: 'active, unacknowledged', response: 'acknowledge and inspect' },
  ACTIVE_ACK: { condition: 'active, acknowledged', response: 'monitor until cleared' },
  CLEARED_UNACK: { condition: 'cleared, acknowledgement required', response: 'acknowledge' },
};

export function AlarmStrip() {
  const a = useSlice('alarms');
  const items = a?.items.slice(0, 3) ?? [];
  const total = a ? a.activeUnack + a.activeAck + a.clearedUnack : 0;
  return (
    <section className={`${styles.alarmStrip} ${total ? styles.alarmStripActive : ''}`} aria-label="Alarm strip" data-testid="alarm-strip" data-alarm-count={total}>
      {total === 0 ? (
        <span className={styles.alarmNone}>
          <span className={styles.okDot} aria-hidden="true" />
          No active alarms · no acknowledgement required
        </span>
      ) : (
        <>
          <span className={styles.alarmCounts}>
            <span className={styles.num}>{a?.activeUnack ?? 0}</span> active unack · <span className={styles.num}>{a?.activeAck ?? 0}</span> active ack ·{' '}
            <span className={styles.num}>{a?.clearedUnack ?? 0}</span> cleared, ack required
          </span>
          {items.map((it) => {
            const t = ALARM_TEXT[it.state] ?? { condition: it.state, response: 'review' };
            return (
              <span key={it.alarmId} className={`${styles.alarmItem} ${styles[`al_${it.state}`]}`} data-alarm-state={it.state}>
                <span className={styles.alarmGlyph} aria-hidden="true">
                  ▲
                </span>
                <b>{it.code}</b> {it.sensorId ?? it.deviceId ?? ''} — {t.condition} · <span className={styles.alarmResponse}>Response: {t.response}</span>
              </span>
            );
          })}
        </>
      )}
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
