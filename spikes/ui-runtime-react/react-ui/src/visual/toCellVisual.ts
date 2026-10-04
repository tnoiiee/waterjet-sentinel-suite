// WJSS Stage 0.2.1A — THE single process-to-visual mapping for a Sensor cell.
//
// Every Sensor-cell style decision is made here and nowhere else. Seven independent
// dimensions, each on its own visual channel:
//   1. DIRTY / CLEANER classification -> background hue
//   2. Dirty Score intensity          -> background shade (lightness)
//   3. Data quality                   -> quality marker / pattern (amber marker for UNCERTAIN)
//   4. Queue state                    -> badge
//   5. Selection (UI-local)           -> cyan outer ring
//   6. Active Cleaning Job target     -> high-contrast double outline
//   7. Alarm state                    -> bright magenta border + icon
//
// Dirty red is a PROCESS-CONDITION colour. It is never used for alarm severity, and the
// alarm colour is never used for process condition. BAD / STALE / DISABLED never receive
// the ordinary Dirty or Cleaner background.
// Colours are spike choices only; Production HMI palette remains [OPEN].

import type { AlarmState, QueueState, SensorPresentationState } from '../../../contracts/operational';

export const PALETTE = Object.freeze({
  dirtyHue: 4,
  cleanerHue: 145,
  neutral: '#5b6068',
  neutralDisabled: '#3d4148',
  uncertainMarker: '#ffb300',
  alarm: '#ff2fd2',
  selected: '#00e5ff',
  jobOuter: '#ffffff',
  jobInner: '#000000',
});

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

const clamp01 = (v: number) => Math.min(1, Math.max(0, v));

export function toCellVisual(s: SensorPresentationState, ctx: { selected: boolean; threshold: number }): CellVisual {
  const { threshold } = ctx;
  let process: ProcessVisual = 'NEUTRAL';
  let background: string = s.quality === 'DISABLED' ? PALETTE.neutralDisabled : PALETTE.neutral;
  let intensity = 0;
  let detailNote: string | null = null;

  const neutralQuality = s.quality === 'BAD' || s.quality === 'STALE' || s.quality === 'DISABLED';
  // Score used for shade: current for GOOD, last validated for UNCERTAIN.
  const shadeScore = s.quality === 'UNCERTAIN' ? s.lastValidatedScore : s.dirtyScore;

  if (!neutralQuality && s.classification !== 'NOT_CLASSIFIED' && shadeScore !== null) {
    if (s.classification === 'DIRTY') {
      process = 'DIRTY';
      intensity = clamp01((shadeScore - threshold) / Math.max(1, 100 - threshold));
      background = `hsl(${PALETTE.dirtyHue} 72% ${Math.round(50 - intensity * 24)}%)`;
    } else {
      process = 'CLEANER';
      intensity = clamp01(shadeScore / Math.max(1, threshold));
      background = `hsl(${PALETTE.cleanerHue} 42% ${Math.round(24 + intensity * 20)}%)`;
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
