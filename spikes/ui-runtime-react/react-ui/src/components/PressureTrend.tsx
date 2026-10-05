// WJSS Stage 0.2.1A — Compact pressure trend (uPlot 1.6.32).
// uPlot is approved for the SPIKE ONLY. The Production chart library remains [OPEN].
// One chart instance for the component lifetime; data updated with setData(); no animation;
// point markers disabled; nulls render as visible gaps; ready band, setpoint, and synthetic
// Job / Alarm overlays drawn in a draw hook. Units are synthetic (syn-units).
import { useEffect, useRef } from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import type { TrendPoint } from '../../../contracts/operational';
import { useSlice } from '../store/hooks';
import styles from './Operations.module.css';

const COLORS = ['#4fc3f7', '#ffd54f', '#ba68c8', '#81c784'];
const AXIS_FONT = '11px system-ui, "Segoe UI", sans-serif';

/** Plot size that fits the bounded host: the legend height is subtracted so the chart plus its
 *  legend never exceed the bottom-row height. Falls back to 150 px when layout is unavailable. */
function plotSize(el: HTMLElement, u: uPlot | null): { width: number; height: number } {
  const legend = u?.root.querySelector<HTMLElement>('.u-legend');
  const legendH = legend ? legend.offsetHeight : 22;
  const h = el.clientHeight > 0 ? el.clientHeight - legendH - 2 : 150;
  return { width: Math.max(200, el.clientWidth), height: Math.max(80, Math.floor(h)) };
}

export function toColumns(points: TrendPoint[]): uPlot.AlignedData {
  const n = points.length;
  const t = new Array<number>(n);
  const s = [0, 1, 2, 3].map(() => new Array<number | null>(n));
  const sp = new Array<number>(n);
  for (let i = 0; i < n; i += 1) {
    const p = points[i];
    t[i] = p.t;
    for (let k = 0; k < 4; k += 1) s[k][i] = p.series[k];
    sp[i] = p.setpoint;
  }
  return [t, ...s, sp] as uPlot.AlignedData;
}

export function PressureTrend() {
  const trend = useSlice('trend');
  const pump = useSlice('pump');
  const host = useRef<HTMLDivElement>(null);
  const plot = useRef<uPlot | null>(null);
  const pointsRef = useRef<TrendPoint[]>([]);
  const bandRef = useRef<[number, number]>([90, 110]);
  pointsRef.current = trend.points;
  if (pump) bandRef.current = [pump.readyBandLow, pump.readyBandHigh];

  // Create once.
  useEffect(() => {
    const el = host.current;
    if (!el) return;
    const names = trend.seriesNames.length ? trend.seriesNames : ['S1', 'S2', 'S3', 'S4'];
    const opts: uPlot.Options = {
      ...plotSize(el, null),
      legend: { show: true, live: false },
      cursor: { show: true, points: { show: false } },
      scales: { x: { time: true }, y: { range: [0, 130] } },
      padding: [6, 8, 0, 0],
      axes: [
        { stroke: '#9aa0a6', grid: { stroke: '#2a2e35' }, font: AXIS_FONT, size: 26, gap: 3 },
        { stroke: '#9aa0a6', grid: { stroke: '#2a2e35' }, font: AXIS_FONT, size: 34, gap: 3 },
      ],
      series: [
        {},
        ...names.slice(0, 4).map((label, i) => ({ label, stroke: COLORS[i], width: 1.25, points: { show: false }, spanGaps: false })),
        { label: 'Setpoint (syn)', stroke: '#e8eaed', dash: [4, 4], width: 1, points: { show: false } },
      ],
      hooks: {
        drawClear: [
          (u: uPlot) => {
            const ctx = u.ctx;
            const { left, top, width, height } = u.bbox;
            const [lo, hi] = bandRef.current;
            const y1 = u.valToPos(hi, 'y', true);
            const y2 = u.valToPos(lo, 'y', true);
            ctx.save();
            ctx.fillStyle = 'rgba(129, 199, 132, 0.10)';
            ctx.fillRect(left, y1, width, y2 - y1);
            // Synthetic Job overlay (cyan spans) and Alarm overlay (magenta ticks).
            const pts = pointsRef.current;
            const xs = u.data[0] as number[];
            for (let i = 0; i < pts.length && i < xs.length; i += 1) {
              const x0 = u.valToPos(xs[i], 'x', true);
              const x1 = i + 1 < xs.length ? u.valToPos(xs[i + 1], 'x', true) : x0 + 1;
              if (pts[i].jobActive) {
                ctx.fillStyle = 'rgba(0, 229, 255, 0.07)';
                ctx.fillRect(x0, top, Math.max(1, x1 - x0), height);
              }
              if (pts[i].alarmActive) {
                ctx.fillStyle = 'rgba(255, 47, 210, 0.8)';
                ctx.fillRect(x0, top + height - 4, Math.max(1, x1 - x0), 4);
              }
            }
            ctx.restore();
          },
        ],
      },
    };
    plot.current = new uPlot(opts, toColumns(pointsRef.current), el);
    // Re-fit once the legend exists, then on every host resize (same instance, no recreation).
    plot.current.setSize(plotSize(el, plot.current));
    const ro = new ResizeObserver(() => {
      if (plot.current) plot.current.setSize(plotSize(el, plot.current));
    });
    ro.observe(el);
    return () => {
      ro.disconnect();
      plot.current?.destroy();
      plot.current = null;
    };
  }, []);

  // Update data only (no instance recreation).
  useEffect(() => {
    plot.current?.setData(toColumns(trend.points));
  }, [trend.version, trend.points]);

  return (
    <section className={`${styles.panel} ${styles.trendPanel}`} aria-label="Pressure trend" data-testid="pressure-trend">
      <h2 className={styles.panelTitle}>
        Pressure trend <span className={styles.muted}>(synthetic units · {trend.points.length}/{trend.capacity} s · ready band shaded · gaps = no data)</span>
      </h2>
      <div ref={host} className={styles.trendHost} />
    </section>
  );
}
