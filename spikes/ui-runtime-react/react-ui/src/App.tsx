// WJSS Stage 0.2.1A — Operations vertical slice (one page only). SYNTHETIC SPIKE.
import { useCallback, useEffect, useState } from 'react';
import { useSlice } from './store/hooks';
import { StatusBar } from './components/StatusBar';
import { WallOverview } from './components/WallOverview';
import { ActiveJobPanel, AlarmStrip, ConnectionBanner, QueuePreview, SensorDetail } from './components/Panels';
import { PressureTrend } from './components/PressureTrend';
import { CameraPlaceholder } from './components/CameraPlaceholder';
import { DiagnosticsOverlay, useDiagnosticsHook } from './components/DiagnosticsOverlay';
import { CriticalAlarmModal } from './components/CriticalAlarmModal';
import styles from './components/Operations.module.css';

export function App({ showTrend = true }: { showTrend?: boolean }) {
  // Selection is UI-local; it is never sent to the runtime and survives Deltas and Snapshots.
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [diagOpen, setDiagOpen] = useState(false);
  const conn = useSlice('connection');
  // Runtime-authoritative: the critical modal is open exactly when the runtime says so.
  const criticalOpen = useSlice('sequence')?.critical?.modalOpen === true;
  const onSelect = useCallback((id: string) => setSelectedId((cur) => (cur === id ? null : id)), []);
  const toggleDiag = useCallback(() => setDiagOpen((v) => !v), []);
  const closeDiag = useCallback(() => setDiagOpen(false), []);
  useDiagnosticsHook();
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.key === 'd' || e.key === 'D') && !(e.target instanceof HTMLInputElement)) toggleDiag();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [toggleDiag]);

  return (
    <div className={styles.app} data-connection={conn.state} data-testid="operations-page">
      {/* While the critical modal is open the whole Operations page is inert (no pointer / keyboard
          interaction, hidden from assistive tech). `display: contents` keeps the flex layout
          unchanged. The opt-in spike Diagnostics drawer (synthetic review tooling) stays outside. */}
      <div className={styles.inertScope} inert={criticalOpen} data-testid="operations-scope" data-inert={criticalOpen}>
        <StatusBar onToggleDiagnostics={toggleDiag} diagnosticsOpen={diagOpen} />
        <ConnectionBanner />
        <AlarmStrip />
        <main className={`${styles.main} ${conn.state === 'DISCONNECTED' ? styles.staleView : ''}`}>
          <div className={styles.overviewArea}>
            <WallOverview selectedId={selectedId} onSelect={onSelect} />
          </div>
          <div className={styles.sideArea}>
            <SensorDetail sensorId={selectedId} />
            <ActiveJobPanel />
            <QueuePreview />
          </div>
          <div className={styles.bottomArea}>
            {showTrend && <PressureTrend />}
            <CameraPlaceholder />
          </div>
        </main>
      </div>
      <CriticalAlarmModal />
      <DiagnosticsOverlay open={diagOpen} onClose={closeDiag} selectedId={selectedId} />
    </div>
  );
}
