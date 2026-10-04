// WJSS Stage 0.2.1A — Sensor cell. Subscribes to exactly one Sensor; memoised.
import { memo } from 'react';
import { useSensor } from '../store/hooks';
import { toCellVisual } from '../visual/toCellVisual';
import { renderCounter } from '../diagnostics/renderCounter';
import styles from './SensorCell.module.css';

export interface SensorCellProps {
  sensorId: string;
  selected: boolean;
  threshold: number;
  onSelect: (sensorId: string) => void;
}

function SensorCellImpl({ sensorId, selected, threshold, onSelect }: SensorCellProps) {
  renderCounter.record(sensorId);
  const s = useSensor(sensorId);
  if (!s) return <button type="button" className={styles.cell} disabled aria-label={`${sensorId} no data`} />;
  const v = toCellVisual(s, { selected, threshold });
  const classes = [
    styles.cell,
    styles[`q_${v.qualityMarker}`],
    v.selectedRing ? styles.selected : '',
    v.activeJobOutline ? styles.activeJob : '',
    v.alarm.state !== 'NONE' ? styles.alarm : '',
    v.alarm.borderStyle === 'dashed' ? styles.alarmCleared : '',
  ]
    .filter(Boolean)
    .join(' ');
  return (
    <button
      type="button"
      className={classes}
      style={{ background: v.background }}
      aria-label={v.ariaLabel}
      aria-pressed={selected}
      title={v.ariaLabel}
      data-sensor-id={sensorId}
      data-process={v.process}
      data-quality={s.quality}
      data-alarm={v.alarm.state}
      onClick={() => onSelect(sensorId)}
    >
      <span className={styles.index}>{String(s.index).padStart(2, '0')}</span>
      <span className={styles.score}>{v.scoreText}</span>
      {v.queueBadge && (
        <span className={styles.badge} title={v.queueBadge.title} data-queue={s.queueState}>
          {v.queueBadge.code}
        </span>
      )}
      {v.alarm.icon && (
        <span className={styles.alarmIcon} aria-hidden="true">
          {v.alarm.icon}
        </span>
      )}
      {v.qualityMarker === 'UNCERTAIN_AMBER' && <span className={styles.uncertainMarker} aria-hidden="true" />}
    </button>
  );
}

export const SensorCell = memo(SensorCellImpl);
