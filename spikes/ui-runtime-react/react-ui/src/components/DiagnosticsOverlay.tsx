// WJSS Stage 0.2.1A — Spike Diagnostics drawer and browser-side measurement hook.
// The drawer floats (position: fixed) below the top status area, never consumes layout space,
// never changes document scroll dimensions, scrolls internally, and can be closed.
// Exposes window.__WJSS_SPIKE_DIAG__() for the Owner-local Playwright soak sampler.
import { useEffect, useState } from 'react';
import { renderCounter } from '../diagnostics/renderCounter';
import { useSlice, useStore } from '../store/hooks';
import type { PresentationStore } from '../store/presentationStore';
import styles from './Operations.module.css';

export interface DiagSample {
  t: number;
  revision: number;
  connection: string;
  snapshots: number;
  deltas: number;
  gaps: number;
  reconnects: number;
  lastMessageBytes: number;
  lastLagMs: number | null;
  sensorCellRendersTotal: number;
  lastDeltaSensorNotifications: number;
  trendPoints: number;
  domNodes: number;
  longTasks: number;
  longTaskMsTotal: number;
  jsHeapUsedMb: number | null;
}

let longTasks = 0;
let longTaskMs = 0;
let observerInstalled = false;

function installLongTaskObserver() {
  if (observerInstalled || typeof PerformanceObserver === 'undefined') return;
  observerInstalled = true;
  try {
    const po = new PerformanceObserver((list) => {
      for (const e of list.getEntries()) {
        longTasks += 1;
        longTaskMs += e.duration;
      }
    });
    po.observe({ type: 'longtask', buffered: true });
  } catch {
    /* longtask not supported in this engine */
  }
}

export function sampleDiagnostics(store: PresentationStore): DiagSample {
  const c = store.getSlice('connection');
  const mem = (performance as unknown as { memory?: { usedJSHeapSize: number } }).memory;
  return {
    t: Date.now(),
    revision: store.revision,
    connection: c.state,
    snapshots: c.snapshots,
    deltas: c.deltas,
    gaps: c.gaps,
    reconnects: c.reconnects,
    lastMessageBytes: c.lastMessageBytes,
    lastLagMs: c.lastLagMs,
    sensorCellRendersTotal: renderCounter.sensorCellRenders,
    lastDeltaSensorNotifications: store.stats.lastDeltaSensorNotifications,
    trendPoints: store.trendLength,
    domNodes: typeof document !== 'undefined' ? document.getElementsByTagName('*').length : 0,
    longTasks,
    longTaskMsTotal: Math.round(longTaskMs),
    jsHeapUsedMb: mem ? Math.round((mem.usedJSHeapSize / 1048576) * 10) / 10 : null,
  };
}

export function useDiagnosticsHook() {
  const store = useStore();
  useEffect(() => {
    installLongTaskObserver();
    (window as unknown as Record<string, unknown>).__WJSS_SPIKE_DIAG__ = () => sampleDiagnostics(store);
  }, [store]);
}

export function DiagnosticsOverlay({ open, onClose }: { open: boolean; onClose?: () => void }) {
  const store = useStore();
  const runtime = useSlice('runtime');
  const [sample, setSample] = useState<DiagSample | null>(null);
  const [rate, setRate] = useState(0);
  useEffect(() => {
    if (!open) return;
    let prev = sampleDiagnostics(store);
    setSample(prev);
    const id = setInterval(() => {
      const s = sampleDiagnostics(store);
      setRate(((s.deltas - prev.deltas) * 1000) / Math.max(1, s.t - prev.t));
      prev = s;
      setSample(s);
    }, 1000);
    return () => clearInterval(id);
  }, [open, store]);
  if (!open || !sample) return null;
  return (
    <aside className={styles.diag} aria-label="Spike diagnostics" data-testid="diagnostics">
      <div className={styles.diagHeader}>
        <h2 className={styles.panelTitle}>Spike diagnostics (synthetic)</h2>
        {onClose && (
          <button type="button" className={styles.btn} onClick={onClose} aria-label="Close diagnostics" data-testid="diagnostics-close">
            Close
          </button>
        )}
      </div>
      <div className={styles.diagBody}>
        <dl className={styles.kv}>
          <dt>Revision</dt>
          <dd>{sample.revision}</dd>
          <dt>Connection</dt>
          <dd>{sample.connection}</dd>
          <dt>Snapshots / Deltas</dt>
          <dd>
            {sample.snapshots} / {sample.deltas} ({rate.toFixed(2)}/s)
          </dd>
          <dt>Gaps / reconnects</dt>
          <dd>
            {sample.gaps} / {sample.reconnects}
          </dd>
          <dt>Last message</dt>
          <dd>{(sample.lastMessageBytes / 1024).toFixed(1)} KiB</dd>
          <dt>Lag (generatedAt→applied)</dt>
          <dd>{sample.lastLagMs ?? '--'} ms</dd>
          <dt>Cell renders (total)</dt>
          <dd>{sample.sensorCellRendersTotal}</dd>
          <dt>Sensor notifications (last Delta)</dt>
          <dd>{sample.lastDeltaSensorNotifications}</dd>
          <dt>Trend points</dt>
          <dd>{sample.trendPoints}</dd>
          <dt>DOM nodes</dt>
          <dd>{sample.domNodes}</dd>
          <dt>Long tasks</dt>
          <dd>
            {sample.longTasks} ({sample.longTaskMsTotal} ms)
          </dd>
          <dt>JS heap</dt>
          <dd>{sample.jsHeapUsedMb ?? 'n/a'} MB</dd>
          <dt>Harness RSS / loop p99</dt>
          <dd>
            {runtime?.rssMb ?? '--'} MB / {runtime?.eventLoopP99Ms ?? '--'} ms
          </dd>
          <dt>Historian depth</dt>
          <dd>
            {runtime?.historian.depth ?? '--'} / {runtime?.historian.capacity ?? '--'}
            {runtime?.historian.nearOverflow ? ' · NEAR OVERFLOW' : ''} · rejected {runtime?.historian.rejected ?? 0}
          </dd>
          <dt>Invariant violations</dt>
          <dd>{runtime?.invariantViolations ?? '--'}</dd>
        </dl>
      </div>
    </aside>
  );
}
