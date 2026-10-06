// WJSS Stage 0.2.1A — Four-wall Sensor overview (U-shaped plan view: Rear top, Left/Right sides,
// Front bottom). Subscribes only to layout, config, and wall summaries; Sensor cells subscribe
// individually. Wall grids are rendered from the runtime Snapshot `wallMap` (6 logical rows per
// wall, wall columns in logical-column order). No Sensor IDs or wall rules are defined here.
// Placement (Rear top, Left / Right sides, Front bottom, compact center summary) is pure CSS
// driven by the design tokens in ../global.css.
import { memo, type CSSProperties } from 'react';
import type { Wall, WallMapSlot, WallSummary } from '../../../contracts/operational';
import { useSlice } from '../store/hooks';
import { SensorCell } from './SensorCell';
import { LegendSwatch, type LegendKind } from './LegendSwatch';
import styles from './Operations.module.css';

interface Props {
  selectedId: string | null;
  onSelect: (id: string) => void;
}

const WALL_LABEL: Record<Wall, string> = { LEFT: 'Left wall', REAR: 'Rear wall', RIGHT: 'Right wall', FRONT: 'Front wall' };
// Visible terminology: "Water Jet reference slot". The internal identifiers CANNON_REAR /
// CANNON_FRONT and slotType 'CANNON' are retained legacy spike contract identifiers (no contract
// migration). These two map positions are logical reference slots, not a count of Water Jets
// (the domain has eight Water Jets); no Production Water Jet number is assigned or guessed.
const WATER_JET_LABEL: Record<string, string> = { CANNON_REAR: 'Rear Water Jet reference slot', CANNON_FRONT: 'Front Water Jet reference slot' };
const WATER_JET_SIDE: Record<string, string> = { REAR: 'REAR', FRONT: 'FRONT' };

/** Water Jet reference slot: neutral equipment, not a Sensor, not selectable, not focusable. */
function WaterJetSlot({ slot }: { slot: WallMapSlot }) {
  const label = WATER_JET_LABEL[slot.equipmentId ?? ''] ?? 'Water Jet reference slot';
  return (
    <div
      className={styles.cannon}
      role="img"
      aria-label={`${label} — equipment, not a Sensor`}
      title={`${label} — equipment, not a Sensor (logical I${slot.logicalColumn})`}
      data-slot-type="CANNON"
      data-equipment-id={slot.equipmentId ?? undefined}
      data-logical-row={slot.logicalRow}
      data-logical-column={slot.logicalColumn}
      data-testid={`wj-slot-${(WATER_JET_SIDE[slot.wall] ?? slot.wall).toLowerCase()}`}
    >
      <span className={styles.cannonGlyph} aria-hidden="true" data-part="wj-code">
        WJ
      </span>
      <span className={styles.cannonText} aria-hidden="true" data-part="wj-side">
        {WATER_JET_SIDE[slot.wall] ?? slot.wall}
      </span>
    </div>
  );
}

const LEGEND: [LegendKind, string][] = [
  ['DIRTY', 'Dirty'],
  ['CLEANER', 'Cleaner'],
  ['NOT_CLASSIFIED', 'Not classified'],
  ['UNCERTAIN', 'Uncertain'],
  ['ALARM', 'Alarm'],
  ['SELECTED', 'Selected'],
  ['ACTIVE_JOB', 'Active Job'],
  ['WATER_JET', 'Water Jet'],
];

const WallPanel = memo(function WallPanel({ wall, rows, threshold, selectedId, onSelect, summary }: { wall: Wall; rows: WallMapSlot[][]; threshold: number; selectedId: string | null; onSelect: (id: string) => void; summary?: WallSummary }) {
  const slots = rows.flat();
  const cols = Math.max(0, ...rows.map((r) => r.length));
  const logical = slots.map((s) => s.logicalColumn);
  const span = logical.length ? `cols ${Math.min(...logical)}–${Math.max(...logical)}` : '';
  return (
    <section className={`${styles.wall} ${styles[`wall_${wall}`]}`} aria-label={WALL_LABEL[wall]} data-wall={wall} data-wall-columns={cols} data-wall-rows={rows.length}>
      <header className={styles.wallHeader}>
        <span title={`${WALL_LABEL[wall]} — logical ${span}`}>
          <span className={styles.wallTitle}>{WALL_LABEL[wall]}</span>
          <span className={styles.wallSpan}>{span.replace('cols ', '')}</span>
        </span>
        {summary && (
          <span className={styles.wallCounts} title={`Dirty ${summary.dirty} · Cleaner ${summary.cleaner} · Not classified ${summary.notClassified}`}>
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
                <WaterJetSlot key={slot.slotId} slot={slot} />
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
      <div className={styles.uSurface} data-testid="u-surface">
        {(['REAR', 'LEFT', 'RIGHT', 'FRONT'] as Wall[]).map((w) => (
          <WallPanel key={w} wall={w} rows={layout[w]} threshold={threshold} selectedId={ownsSelection(w)} onSelect={onSelect} summary={summary(w)} />
        ))}
        <div className={styles.overviewCenter} data-testid="map-center">
          <div data-testid="map-totals">
            <div className={styles.totalsRow}>
              <span className={styles.legendTitle} data-testid="map-sensor-count">
                {sensorCount} Sensors
              </span>
              <span className={styles.thresholdNote} data-testid="map-threshold" title={`Dirty threshold ${threshold} · synthetic config r${config?.revision ?? '-'}`}>
                Threshold {threshold} · r{config?.revision ?? '-'}
              </span>
            </div>
            {/* Two logical map reference slots — never "2 Water Jets" (the domain has eight). */}
            <div className={styles.legendSub} data-testid="map-cannon-count">
              {cannonCount} Water Jet reference slots · synthetic
            </div>
          </div>
          <ul className={styles.legend} aria-label="Map legend" data-testid="map-legend">
            {LEGEND.map(([kind, label]) => (
              <li key={kind} className={styles.legendItem} data-legend={kind}>
                <span className={styles.legendIcon}>
                  <LegendSwatch kind={kind} />
                </span>
                <span className={styles.legendLabel} data-part="legend-label">
                  {label}
                </span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}
