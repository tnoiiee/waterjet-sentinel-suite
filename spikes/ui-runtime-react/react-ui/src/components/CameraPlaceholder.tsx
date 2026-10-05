// WJSS Stage 0.2.1A — Camera placeholder. Locally authored SVG only: no external URL, no
// real camera, no video stream, no plant image.
import styles from './Operations.module.css';

export function CameraPlaceholder() {
  return (
    <section className={`${styles.panel} ${styles.cameraPanel}`} aria-label="Camera placeholder" data-testid="camera-placeholder">
      <h2 className={styles.panelTitle}>Camera</h2>
      <svg className={styles.camera} viewBox="0 0 320 180" preserveAspectRatio="xMidYMid meet" role="img" aria-label="Camera placeholder: no camera connected">
        <defs>
          <pattern id="wjss-cam-stripes" width="16" height="16" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
            <rect width="16" height="16" fill="#1d2127" />
            <rect width="8" height="16" fill="#23282f" />
          </pattern>
        </defs>
        <rect x="0" y="0" width="320" height="180" fill="url(#wjss-cam-stripes)" />
        <rect x="1" y="1" width="318" height="178" fill="none" stroke="#5b6068" strokeDasharray="6 4" />
        <g fill="none" stroke="#9aa0a6" strokeWidth="2">
          <rect x="128" y="62" width="48" height="34" rx="4" />
          <path d="M176 72 l18 -10 v34 l-18 -10 z" />
        </g>
        <text x="160" y="122" textAnchor="middle" fill="#e8eaed" fontSize="12" fontFamily="system-ui, sans-serif">
          CAMERA PLACEHOLDER — SYNTHETIC
        </text>
        <text x="160" y="140" textAnchor="middle" fill="#9aa0a6" fontSize="10" fontFamily="system-ui, sans-serif">
          No camera connected · no video source configured
        </text>
      </svg>
    </section>
  );
}
