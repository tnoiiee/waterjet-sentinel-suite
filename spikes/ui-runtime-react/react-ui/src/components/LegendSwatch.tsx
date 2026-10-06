// WJSS Stage 0.2.1A — dedicated, bounded Map legend swatch.
// Every symbol is drawn INSIDE a 16 x 16 SVG viewBox (inset rendering): no CSS outline,
// box-shadow, pseudo-element, or negative offset can extend past the swatch bounds, so nothing
// clips at the legend's left edge. Sensor-cell styles are intentionally NOT reused.
// Colours come from the central design tokens (CSS custom properties).

export type LegendKind = 'DIRTY' | 'CLEANER' | 'NOT_CLASSIFIED' | 'UNCERTAIN' | 'ALARM' | 'SELECTED' | 'ACTIVE_JOB' | 'WATER_JET';

const S = 16;

export function LegendSwatch({ kind }: { kind: LegendKind }) {
  return (
    <svg className="legend-swatch" viewBox={`0 0 ${S} ${S}`} width={S} height={S} aria-hidden="true" focusable="false" data-part="legend-swatch" data-kind={kind}>
      {body(kind)}
    </svg>
  );
}

/** Inset square used by the process swatches (2 px inset on every side). */
const square = (fill: string) => <rect x="2" y="2" width="12" height="12" rx="1.5" fill={fill} />;

function body(kind: LegendKind) {
  switch (kind) {
    case 'DIRTY':
      return square('var(--dirty-base)');
    case 'CLEANER':
      return square('var(--cleaner-base)');
    case 'NOT_CLASSIFIED':
      return (
        <>
          {square('var(--not-classified)')}
          <path d="M2.5 9.5 9.5 2.5M2.5 13.5 13.5 2.5M6.5 13.5 13.5 6.5" stroke="var(--quality-pattern)" strokeWidth="1.2" />
        </>
      );
    case 'UNCERTAIN':
      return (
        <>
          {square('var(--quality-neutral)')}
          <circle cx="11" cy="5" r="2.6" fill="var(--uncertain)" stroke="var(--alarm-ink)" strokeWidth="0.6" />
        </>
      );
    case 'ALARM':
      return (
        <>
          <path d="M8 2 14 13.5H2Z" fill="var(--alarm)" stroke="var(--alarm-ink)" strokeWidth="0.8" strokeLinejoin="round" />
          <path d="M8 6v3.6" stroke="var(--alarm-ink)" strokeWidth="1.5" strokeLinecap="round" />
          <circle cx="8" cy="11.6" r="0.9" fill="var(--alarm-ink)" />
        </>
      );
    case 'SELECTED':
      // Cyan ring fully inside the box (stroke centred 2 px from the edge).
      return (
        <>
          <rect x="4" y="4" width="8" height="8" rx="1" fill="var(--quality-neutral)" />
          <rect x="2" y="2" width="12" height="12" rx="2" fill="none" stroke="var(--selection)" strokeWidth="2" />
        </>
      );
    case 'ACTIVE_JOB':
      // White / dark / white double outline, inset.
      return (
        <>
          <rect x="1.5" y="1.5" width="13" height="13" rx="2" fill="none" stroke="var(--job-outer)" strokeWidth="1.2" />
          <rect x="3" y="3" width="10" height="10" rx="1.5" fill="none" stroke="var(--job-inner)" strokeWidth="1.2" />
          <rect x="4.5" y="4.5" width="7" height="7" rx="1" fill="var(--quality-neutral)" stroke="var(--job-outer)" strokeWidth="1.2" />
        </>
      );
    case 'WATER_JET':
      // Neutral equipment swatch with a nozzle cue (dashed circle + centre jet).
      return (
        <>
          <circle cx="8" cy="8" r="6" fill="var(--cannon-surface)" stroke="var(--cannon-border)" strokeWidth="1.2" strokeDasharray="2 1.4" />
          <path d="M8 4.5v4.2" stroke="var(--text)" strokeWidth="1.6" strokeLinecap="round" />
          <path d="M5.8 10.4h4.4" stroke="var(--text)" strokeWidth="1.4" strokeLinecap="round" />
        </>
      );
  }
}

/** Every supported legend kind (tests iterate this list). */
export const LEGEND_KINDS: readonly LegendKind[] = ['DIRTY', 'CLEANER', 'NOT_CLASSIFIED', 'UNCERTAIN', 'ALARM', 'SELECTED', 'ACTIVE_JOB', 'WATER_JET'];
