// WJSS Stage 0.2.1A — THE single process-to-visual mapping for a Sensor cell.
//
// Every Sensor-cell style decision is made here and nowhere else. Seven independent
// dimensions, each on its own visual channel:
//   1. DIRTY / CLEANER classification -> background colour family (central CSS tokens)
//   2. Dirty Score intensity          -> background shade (color-mix of base and strong token)
//   3. Data quality                   -> top-right quality marker (amber dot for UNCERTAIN;
//                                        neutral background + pattern + glyph for BAD/STALE/DISABLED)
//   4. Queue state                    -> bottom-right queue badge
//   5. Selection (UI-local)           -> cyan outer ring
//   6. Active Cleaning Job target     -> high-contrast white double outline
//   7. Alarm state                    -> yellow warning border + bottom-left warning icon
//
// Dirty red is a PROCESS-CONDITION colour. It is never used for alarm severity, and the
// alarm colour is never used for process condition. BAD / STALE / DISABLED never receive
// the ordinary Dirty or Cleaner background.
// Colour VALUES live only in src/global.css (the single design-token location); this module
// references them by CSS custom property. Colours are spike choices only; the Production HMI
// palette remains [OPEN]. No standards certification is claimed.

import type { AlarmState, QueueState, SensorPresentationState } from '../../../contracts/operational';

export const PALETTE = Object.freeze({
  cleanerBase: 'var(--cleaner-base)',
  cleanerStrong: 'var(--cleaner-strong)',
  dirtyBase: 'var(--dirty-base)',
  dirtyStrong: 'var(--dirty-strong)',
  notClassified: 'var(--not-classified)',
  neutral: 'var(--quality-neutral)',
  neutralDisabled: 'var(--quality-disabled)',
  uncertainMarker: 'var(--uncertain)',
  alarm: 'var(--alarm)',
  selected: 'var(--selection)',
  jobOuter: 'var(--job-outer)',
  jobInner: 'var(--job-inner)',
});

function clamp01(v: number): number {
  return Math.min(1, Math.max(0, v));
}

/** Mix `pct` percent of `strong` into `base` (CSS Color 5 color-mix; Edge 111+). */
export function shade(base: string, strong: string, intensity: number): string {
  return `color-mix(in srgb, ${strong} ${Math.round(clamp01(intensity) * 100)}%, ${base})`;
}

export type ProcessVisual = 'DIRTY' | 'CLEANER' | 'NEUTRAL';
export type QualityMarker = 'NONE' | 'UNCERTAIN_AMBER' | 'BAD_HATCH' | 'STALE_CLOCK' | 'DISABLED_SLASH';

export interface CellVisual {
  process: ProcessVisual;
  background: string;
  intensity: number;
  qualityMarker: QualityMarker;
  queueBadge: { code: string; title: string } | null;
  selectedRing: boolean;
  activeJobOutline: boolean;
  alarm: { state: AlarmState; borderColor: string | null; borderStyle: 'solid' | 'dashed' | 'none'; icon: string | null };
  scoreText: string;
  ariaLabel: string;
  detailNote: string | null;
}

const QUEUE_BADGES: Record<Exclude<QueueState, 'NONE'>, { code: string; title: string }> = {
  READY: { code: 'R', title: 'Queued: Ready' },
  HELD: { code: 'H', title: 'Queued: Held' },
  BLOCKED: { code: 'B', title: 'Queued: Blocked' },
  EXCLUDED: { code: 'X', title: 'Queued: Excluded' },
  ACTIVE: { code: 'J', title: 'Active Cleaning Job target' },
};


export function toCellVisual(s: SensorPresentationState, ctx: { selected: boolean; threshold: number }): CellVisual {
  const { threshold } = ctx;
  let process: ProcessVisual = 'NEUTRAL';
  const neutralQualityBg = s.quality === 'DISABLED' ? PALETTE.neutralDisabled : PALETTE.neutral;
  let background: string =
    s.quality === 'BAD' || s.quality === 'STALE' || s.quality === 'DISABLED' ? neutralQualityBg : PALETTE.notClassified;
  let intensity = 0;
  let detailNote: string | null = null;

  const neutralQuality = s.quality === 'BAD' || s.quality === 'STALE' || s.quality === 'DISABLED';
  // Score used for shade: current for GOOD, last validated for UNCERTAIN.
  const shadeScore = s.quality === 'UNCERTAIN' ? s.lastValidatedScore : s.dirtyScore;

  if (!neutralQuality && s.classification !== 'NOT_CLASSIFIED' && shadeScore !== null) {
    if (s.classification === 'DIRTY') {
      process = 'DIRTY';
      intensity = clamp01((shadeScore - threshold) / Math.max(1, 100 - threshold));
      background = shade(PALETTE.dirtyBase, PALETTE.dirtyStrong, intensity);
    } else {
      process = 'CLEANER';
      intensity = clamp01(shadeScore / Math.max(1, threshold));
      background = shade(PALETTE.cleanerBase, PALETTE.cleanerStrong, intensity);
    }
  }

  let qualityMarker: QualityMarker = 'NONE';
  switch (s.quality) {
    case 'UNCERTAIN':
      qualityMarker = 'UNCERTAIN_AMBER';
      detailNote =
        s.classificationBasis === 'LAST_VALIDATED'
          ? `Showing the last validated classification (${s.classification}, score ${fmt(s.lastValidatedScore)}). The current value is UNCERTAIN and does not change classification.`
          : 'Data quality UNCERTAIN and no last validated value exists: NOT CLASSIFIED.';
      break;
    case 'BAD':
      qualityMarker = 'BAD_HATCH';
      detailNote = 'Data quality BAD: NOT CLASSIFIED.';
      break;
    case 'STALE':
      qualityMarker = 'STALE_CLOCK';
      detailNote = 'Data quality STALE: NOT CLASSIFIED.';
      break;
    case 'DISABLED':
      qualityMarker = 'DISABLED_SLASH';
      detailNote = 'Sensor DISABLED: NOT CLASSIFIED.';
      break;
    default:
      break;
  }

  const alarm: CellVisual['alarm'] =
    s.alarmState === 'NONE'
      ? { state: 'NONE', borderColor: null, borderStyle: 'none', icon: null }
      : s.alarmState === 'CLEARED_UNACK'
        ? { state: s.alarmState, borderColor: PALETTE.alarm, borderStyle: 'dashed', icon: '!' }
        : { state: s.alarmState, borderColor: PALETTE.alarm, borderStyle: 'solid', icon: '!' };

  const queueBadge = s.queueState === 'NONE' ? null : QUEUE_BADGES[s.queueState];
  const scoreText = neutralQuality || shadeScore === null ? '--' : String(Math.round(shadeScore));
  const ariaLabel = [
    s.sensorId,
    s.classification === 'NOT_CLASSIFIED' ? 'not classified' : s.classification.toLowerCase(),
    `quality ${s.quality.toLowerCase()}`,
    queueBadge ? queueBadge.title.toLowerCase() : null,
    s.isActiveJobTarget ? 'active job target' : null,
    s.alarmState !== 'NONE' ? `alarm ${s.alarmState.toLowerCase().replace('_', ' ')}` : null,
    ctx.selected ? 'selected' : null,
  ]
    .filter(Boolean)
    .join(', ');

  return {
    process,
    background,
    intensity,
    qualityMarker,
    queueBadge,
    selectedRing: ctx.selected,
    activeJobOutline: s.isActiveJobTarget,
    alarm,
    scoreText,
    ariaLabel,
    detailNote,
  };
}

function fmt(v: number | null): string {
  return v === null ? '--' : v.toFixed(1);
}
