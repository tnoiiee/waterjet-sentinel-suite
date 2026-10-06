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

/** Canvas cannot read CSS custom properties directly; trend colours are resolved once from the
 *  central design tokens in global.css (with the same values as fallback for jsdom). Series
 *  colours differ in luminance as well as hue. */
const TOKEN_FALLBACK: Record<string, string> = {
  '--trend-s1': '#8fd3ff',
  '--trend-s2': '#f4c542',
  '--trend-s3': '#c4a3ff',
  '--trend-s4': '#4fbf7f',
  '--trend-grid': 'rgba(255, 255, 255, 0.07)',
  '--trend-axis': '#b3b9c0',
  '--trend-band': 'rgba(127, 207, 155, 0.16)',
  '--trend-band-edge': 'rgba(127, 207, 155, 0.55)',
  '--trend-setpoint': '#e3e6ea',
  '--alarm': '#ffc531',
  '--selection': '#36d6f0',
};
export function token(name: string): string {
  const v = typeof document !== 'undefined' ? getComputedStyle(document.documentElement).getPropertyValue(name).trim() : '';
  return v || TOKEN_FALLBACK[name] || '#888';
}
export const TREND_LINE_WIDTH = 1.75;
// Canvas axes use the same self-hosted family as the rest of the UI (Google Sans).
const AXIS_FONT = '12px "Google Sans", "Segoe UI", system-ui, sans-serif';
const fmtVal = (v: number | null | undefined) => (v === null || v === undefined || !Number.isFinite(v) ? '--' : v.toFixed(1));

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
    const colors = ['--trend-s1', '--trend-s2', '--trend-s3', '--trend-s4'].map(token);
    const grid = token('--trend-grid');
    const axis = token('--trend-axis');
    const band = token('--trend-band');
    const bandEdge = token('--trend-band-edge');
    const alarmColor = token('--alarm');
    const opts: uPlot.Options = {
      ...plotSize(el, null),
      legend: { show: true, live: false },
      cursor: { show: true, points: { show: false } },
      scales: { x: { time: true }, y: { range: [0, 130] } },
      padding: [6, 8, 0, 0],
      axes: [
        { stroke: axis, grid: { stroke: grid, width: 1 }, ticks: { stroke: grid }, font: AXIS_FONT, size: 28, gap: 3 },
        { stroke: axis, grid: { stroke: grid, width: 1 }, ticks: { stroke: grid }, font: AXIS_FONT, size: 38, gap: 3 },
      ],
      series: [
        {},
        ...names.slice(0, 4).map((label, i) => ({ label, stroke: colors[i], width: TREND_LINE_WIDTH, points: { show: false }, spanGaps: false })),
        { label: 'Setpoint (syn)', stroke: token('--trend-setpoint'), dash: [6, 4], width: 1.25, points: { show: false } },
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
            ctx.fillStyle = band;
            ctx.fillRect(left, y1, width, y2 - y1);
            // Ready-band edges: thin lines make the band readable without a heavy fill.
            ctx.fillStyle = bandEdge;
            ctx.fillRect(left, Math.round(y1), width, 1);
            ctx.fillRect(left, Math.round(y2), width, 1);
            // Synthetic Job overlay (faint cyan spans) and Alarm overlay (alarm-yellow ticks).
            const pts = pointsRef.current;
            const xs = u.data[0] as number[];
            for (let i = 0; i < pts.length && i < xs.length; i += 1) {
              const x0 = u.valToPos(xs[i], 'x', true);
              const x1 = i + 1 < xs.length ? u.valToPos(xs[i + 1], 'x', true) : x0 + 1;
              if (pts[i].jobActive) {
                ctx.fillStyle = 'rgba(54, 214, 240, 0.06)';
                ctx.fillRect(x0, top, Math.max(1, x1 - x0), height);
              }
              if (pts[i].alarmActive) {
                ctx.fillStyle = alarmColor;
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

  const last = trend.points.length ? trend.points[trend.points.length - 1] : null;
  const names = trend.seriesNames.length ? trend.seriesNames : ['S1', 'S2', 'S3', 'S4'];
  return (
    <section className={`${styles.panel} ${styles.trendPanel}`} aria-label="Pressure trend" data-testid="pressure-trend">
      <header className={styles.cardHeader}>
        <h2 className={`${styles.panelTitle} ${styles.trendTitle}`}>Pressure trend</h2>
        <span className={styles.trendSummary} data-testid="trend-summary">
          {names.slice(0, 4).map((n, i) => (
            <span key={n} className={styles.trendNow}>
              {n} <b className={styles.num}>{fmtVal(last?.series[i])}</b>
            </span>
          ))}
          <span className={styles.trendNow}>
            setpoint <b className={styles.num}>{fmtVal(last?.setpoint)}</b>
          </span>
          <span className={styles.trendNow}>
            ready band <b className={styles.num}>{bandRef.current[0]}–{bandRef.current[1]}</b>
          </span>
        </span>
        <span className={styles.headerSpacer} />
        <span className={styles.cardMeta}>
          syn-units · <span className={styles.num}>{trend.points.length}/{trend.capacity}</span> s · gaps = no data
        </span>
      </header>
      <div ref={host} className={styles.trendHost} />
    </section>
  );
}
