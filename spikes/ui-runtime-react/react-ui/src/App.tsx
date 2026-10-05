// WJSS Stage 0.2.1A — Operations vertical slice (one page only). SYNTHETIC SPIKE.
import { useCallback, useEffect, useState } from 'react';
import { useSlice } from './store/hooks';
import { StatusBar } from './components/StatusBar';
import { WallOverview } from './components/WallOverview';
import { ActiveJobPanel, AlarmStrip, ConnectionBanner, QueuePreview, SensorDetail } from './components/Panels';
import { PressureTrend } from './components/PressureTrend';
import { CameraPlaceholder } from './components/CameraPlaceholder';
import { DiagnosticsOverlay, useDiagnosticsHook } from './components/DiagnosticsOverlay';
import styles from './components/Operations.module.css';

export function App({ showTrend = true }: { showTrend?: boolean }) {
  // Selection is UI-local; it is never sent to the runtime and survives Deltas and Snapshots.
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [diagOpen, setDiagOpen] = useState(false);
  const conn = useSlice('connection');
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
      <DiagnosticsOverlay open={diagOpen} onClose={closeDiag} />
    </div>
  );
}
