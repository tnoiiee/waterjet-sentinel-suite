// WJSS Stage 0.2.1A — Four-wall Sensor overview (plan view: Rear top, Left/Right sides,
// Front bottom). Subscribes only to layout, config, and wall summaries; Sensor cells
// subscribe individually.
import { memo } from 'react';
import type { Wall, WallSummary } from '../../../contracts/operational';
import { useSlice } from '../store/hooks';
import { SensorCell } from './SensorCell';
import styles from './Operations.module.css';

interface Props {
  selectedId: string | null;
  onSelect: (id: string) => void;
}

const WALL_LABEL: Record<Wall, string> = { LEFT: 'Left wall', REAR: 'Rear wall', RIGHT: 'Right wall', FRONT: 'Front wall' };

const WallPanel = memo(function WallPanel({ wall, ids, threshold, selectedId, onSelect, summary }: { wall: Wall; ids: string[]; threshold: number; selectedId: string | null; onSelect: (id: string) => void; summary?: WallSummary }) {
  return (
    <section className={`${styles.wall} ${styles[`wall_${wall}`]}`} aria-label={WALL_LABEL[wall]} data-wall={wall}>
      <header className={styles.wallHeader}>
        <span>{WALL_LABEL[wall]}</span>
        {summary && (
          <span className={styles.wallCounts}>
            D {summary.dirty} · C {summary.cleaner} · N/C {summary.notClassified}
          </span>
        )}
      </header>
      <div className={styles.cells}>
        {ids.map((id) => (
          <SensorCell key={id} sensorId={id} threshold={threshold} selected={id === selectedId} onSelect={onSelect} />
        ))}
      </div>
    </section>
  );
});

export function WallOverview({ selectedId, onSelect }: Props) {
  const layout = useSlice('layout');
  const config = useSlice('config');
  const walls = useSlice('walls');
  const threshold = config?.dirtyThreshold ?? 50;
  const summary = (w: Wall) => walls.find((x) => x.wall === w);
  const total = layout.LEFT.length + layout.REAR.length + layout.RIGHT.length + layout.FRONT.length;
  return (
    <div className={styles.overview} data-testid="wall-overview">
      {(['REAR', 'LEFT', 'RIGHT', 'FRONT'] as Wall[]).map((w) => (
        <WallPanel key={w} wall={w} ids={layout[w]} threshold={threshold} selectedId={selectedId && layout[w].includes(selectedId) ? selectedId : null} onSelect={onSelect} summary={summary(w)} />
      ))}
      <div className={styles.overviewCenter}>
        <div className={styles.legendTitle}>{total} Sensor locations (synthetic)</div>
        <ul className={styles.legend}>
          <li><span className={styles.swDirty} /> Dirty (process)</li>
          <li><span className={styles.swCleaner} /> Cleaner (process)</li>
          <li><span className={styles.swNeutral} /> Not classified</li>
          <li><span className={styles.swUncertain} /> Uncertain marker</li>
          <li><span className={styles.swAlarm} /> Alarm border</li>
          <li><span className={styles.swSelected} /> Selected</li>
          <li><span className={styles.swJob} /> Active Job</li>
        </ul>
        <div className={styles.thresholdNote}>Dirty threshold {threshold} (synthetic config rev {config?.revision ?? '-'})</div>
      </div>
    </div>
  );
}
