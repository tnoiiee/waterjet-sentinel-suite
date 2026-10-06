// WJSS Stage 0.2.1A — Camera placeholder. Locally authored markup and SVG only: no external
// URL, no real camera, no video stream, no plant image.
import styles from './Operations.module.css';

export function CameraPlaceholder() {
  return (
    <section className={`${styles.panel} ${styles.cameraPanel}`} aria-label="Camera placeholder" data-testid="camera-placeholder">
      <header className={styles.cardHeader}>
        <h2 className={`${styles.panelTitle} ${styles.cameraTitle}`}>Camera</h2>
        <span className={styles.headerSpacer} />
        <span className={styles.placeholderBadge} data-testid="camera-badge">
          SYNTHETIC PLACEHOLDER
        </span>
      </header>
      <div className={styles.cameraBody} data-testid="camera-body">
        <svg className={styles.cameraIcon} viewBox="0 0 48 48" width="48" height="48" role="img" aria-label="Camera placeholder: no camera connected">
          <g fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round">
            <rect x="5" y="14" width="26" height="20" rx="3" />
            <path d="M31 21 43 14V34L31 27Z" />
            <path d="M6 42 42 6" />
          </g>
        </svg>
        <div className={styles.cameraState} data-testid="camera-state">
          NO SIGNAL
        </div>
        <div className={styles.cameraSub}>No video source configured</div>
      </div>
    </section>
  );
}
