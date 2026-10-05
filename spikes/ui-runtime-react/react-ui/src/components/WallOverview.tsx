// WJSS Stage 0.2.1A — Four-wall Sensor overview (U-shaped plan view: Rear top, Left/Right sides,
// Front bottom). Subscribes only to layout, config, and wall summaries; Sensor cells subscribe
// individually. Wall grids are rendered from the runtime Snapshot `wallMap` (6 logical rows per
// wall, wall columns in logical-column order). No Sensor IDs or wall rules are defined here.
import { memo, type CSSProperties } from 'react';
import type { Wall, WallMapSlot, WallSummary } from '../../../contracts/operational';
import { useSlice } from '../store/hooks';
import { SensorCell } from './SensorCell';
import styles from './Operations.module.css';

interface Props {
  selectedId: string | null;
  onSelect: (id: string) => void;
}

const WALL_LABEL: Record<Wall, string> = { LEFT: 'Left wall', REAR: 'Rear wall', RIGHT: 'Right wall', FRONT: 'Front wall' };
const CANNON_LABEL: Record<string, string> = { CANNON_REAR: 'Rear Cannon', CANNON_FRONT: 'Front Cannon' };

/** Cannon equipment slot: neutral, not a Sensor, not selectable, not focusable. */
function CannonSlot({ slot }: { slot: WallMapSlot }) {
  const label = CANNON_LABEL[slot.equipmentId ?? ''] ?? slot.equipmentId ?? 'Cannon';
  return (
    <div
      className={styles.cannon}
      role="img"
      aria-label={`${label} — equipment slot, not a Sensor`}
      title={`${label} — equipment slot, not a Sensor`}
      data-slot-type="CANNON"
      data-equipment-id={slot.equipmentId ?? undefined}
      data-logical-row={slot.logicalRow}
      data-logical-column={slot.logicalColumn}
    >
      <span className={styles.cannonGlyph} aria-hidden="true">
        ⊕
      </span>
      <span className={styles.cannonText} aria-hidden="true">
        {slot.wall === 'REAR' ? 'C-R' : slot.wall === 'FRONT' ? 'C-F' : 'C'}
      </span>
    </div>
  );
}

const WallPanel = memo(function WallPanel({ wall, rows, threshold, selectedId, onSelect, summary }: { wall: Wall; rows: WallMapSlot[][]; threshold: number; selectedId: string | null; onSelect: (id: string) => void; summary?: WallSummary }) {
  const slots = rows.flat();
  const cols = Math.max(0, ...rows.map((r) => r.length));
  const logical = slots.map((s) => s.logicalColumn);
  const span = logical.length ? `cols ${Math.min(...logical)}–${Math.max(...logical)}` : '';
  return (
    <section className={`${styles.wall} ${styles[`wall_${wall}`]}`} aria-label={WALL_LABEL[wall]} data-wall={wall} data-wall-columns={cols} data-wall-rows={rows.length}>
      <header className={styles.wallHeader}>
        <span>
          {WALL_LABEL[wall]} <span className={styles.wallSpan}>{span}</span>
        </span>
        {summary && (
          <span className={styles.wallCounts}>
            D {summary.dirty} · C {summary.cleaner} · N/C {summary.notClassified}
          </span>
        )}
      </header>
      <div className={styles.cells} style={{ '--wall-cols': cols } as CSSProperties}>
        {rows.map((row, r) => (
          <div key={r} className={styles.wallRow} role="presentation" data-wall-row={r + 1}>
            {row.map((slot) =>
              slot.slotType === 'SENSOR' && slot.sensorId ? (
                <SensorCell key={slot.slotId} sensorId={slot.sensorId} threshold={threshold} selected={slot.sensorId === selectedId} onSelect={onSelect} />
              ) : (
                <CannonSlot key={slot.slotId} slot={slot} />
              ),
            )}
          </div>
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
  const all = Object.values(layout).flatMap((rows) => rows.flat());
  const sensorCount = all.filter((s) => s.slotType === 'SENSOR').length;
  const cannonCount = all.filter((s) => s.slotType === 'CANNON').length;
  const ownsSelection = (w: Wall) => (selectedId && layout[w].some((row) => row.some((s) => s.sensorId === selectedId)) ? selectedId : null);
  return (
    <div className={styles.overview} data-testid="wall-overview">
      {(['REAR', 'LEFT', 'RIGHT', 'FRONT'] as Wall[]).map((w) => (
        <WallPanel key={w} wall={w} rows={layout[w]} threshold={threshold} selectedId={ownsSelection(w)} onSelect={onSelect} summary={summary(w)} />
      ))}
      <div className={styles.overviewCenter}>
        <div className={styles.legendTitle} data-testid="map-totals">
          {sensorCount} Sensor locations · {cannonCount} Cannon slots (synthetic)
        </div>
        <ul className={styles.legend}>
          <li><span className={styles.swDirty} /> Dirty (process)</li>
          <li><span className={styles.swCleaner} /> Cleaner (process)</li>
          <li><span className={styles.swNeutral} /> Not classified</li>
          <li><span className={styles.swUncertain} /> Uncertain marker</li>
          <li><span className={styles.swAlarm} /> Alarm border</li>
          <li><span className={styles.swSelected} /> Selected</li>
          <li><span className={styles.swJob} /> Active Job</li>
          <li><span className={styles.swCannon} /> Cannon (equipment)</li>
        </ul>
        <div className={styles.thresholdNote}>Dirty threshold {threshold} (synthetic config rev {config?.revision ?? '-'})</div>
      </div>
    </div>
  );
}
