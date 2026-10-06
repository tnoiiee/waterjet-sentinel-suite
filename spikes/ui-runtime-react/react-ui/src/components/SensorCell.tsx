// WJSS Stage 0.2.1A — Sensor cell. Subscribes to exactly one Sensor; memoised.
import { memo } from 'react';
import { useSensor } from '../store/hooks';
import { toCellVisual, type CellVisual } from '../visual/toCellVisual';
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
    v.process === 'NEUTRAL' && (v.qualityMarker === 'NONE' || v.qualityMarker === 'UNCERTAIN_AMBER') ? styles.notClassified : '',
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
      data-slot-type="SENSOR"
      data-process={v.process}
      data-quality={s.quality}
      data-alarm={v.alarm.state}
      onClick={() => onSelect(sensorId)}
    >
      {/* Zone 1: Sensor ID across the full usable cell width. No marker shares this row. */}
      <span className={styles.index} data-part="id">
        {s.sensorId}
      </span>
      {/* Zone 2: reserved quality-marker zone at the right end of the value row. */}
      <span className={styles.markerZone} data-part="marker-zone" aria-hidden="true">
        {v.qualityMarker !== 'NONE' && <QualityGlyph marker={v.qualityMarker} />}
      </span>
      {/* Zone 3: value, centred (symmetric spacer | value | marker zone). */}
      <span className={styles.score} data-part="value">
        {v.scoreText}
      </span>
      {/* Zone 4: bottom rail — alarm icon left, queue badge right. */}
      <span className={styles.rail} data-part="rail">
        {v.alarm.icon ? <AlarmGlyph /> : <span />}
        {v.queueBadge && (
          <span className={styles.badge} title={v.queueBadge.title} data-queue={s.queueState} data-part="queue-badge">
            {v.queueBadge.code}
          </span>
        )}
      </span>
    </button>
  );
}

/** Warning triangle with exclamation mark (alarm). Shape plus colour, never colour alone. */
function AlarmGlyph() {
  return (
    <svg className={styles.alarmIcon} data-part="alarm-marker" viewBox="0 0 12 10" width="12" height="10" aria-hidden="true" focusable="false">
      <path d="M6 0.4 11.6 9.6H0.4Z" fill="var(--alarm)" stroke="var(--alarm-ink)" strokeWidth="0.8" strokeLinejoin="round" />
      <path d="M6 3.2V6.3" stroke="var(--alarm-ink)" strokeWidth="1.4" strokeLinecap="round" />
      <circle cx="6" cy="8" r="0.8" fill="var(--alarm-ink)" />
    </svg>
  );
}

/** Data-quality marker: amber dot (UNCERTAIN), cross (BAD), clock (STALE), slashed circle (DISABLED). */
function QualityGlyph({ marker }: { marker: Exclude<CellVisual['qualityMarker'], 'NONE'> }) {
  const common = { viewBox: '0 0 10 10', width: 8, height: 8, 'aria-hidden': true, focusable: false, 'data-part': 'quality-marker', 'data-marker': marker } as const;
  switch (marker) {
    case 'UNCERTAIN_AMBER':
      return (
        <svg {...common}>
          <circle cx="5" cy="5" r="4.2" fill="var(--uncertain)" stroke="var(--alarm-ink)" strokeWidth="0.8" />
        </svg>
      );
    case 'BAD_HATCH':
      return (
        <svg {...common}>
          <path d="M1.5 1.5 8.5 8.5M8.5 1.5 1.5 8.5" stroke="var(--text-strong)" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      );
    case 'STALE_CLOCK':
      return (
        <svg {...common}>
          <circle cx="5" cy="5" r="4" fill="none" stroke="var(--text-strong)" strokeWidth="1.2" />
          <path d="M5 2.6V5.2L6.8 6.3" fill="none" stroke="var(--text-strong)" strokeWidth="1.2" strokeLinecap="round" />
        </svg>
      );
    case 'DISABLED_SLASH':
      return (
        <svg {...common}>
          <circle cx="5" cy="5" r="4" fill="none" stroke="var(--text-strong)" strokeWidth="1.2" />
          <path d="M2.2 7.8 7.8 2.2" stroke="var(--text-strong)" strokeWidth="1.2" />
        </svg>
      );
  }
}

export const SensorCell = memo(SensorCellImpl);
