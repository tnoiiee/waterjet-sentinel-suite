// WJSS Stage 0.2.1A — Compact GlobalQueue source-reason display (spike, presentation only).
// Maps a runtime source-reason code to a short operator label for the queue preview. The full
// code stays available as a tooltip. This never changes queue behaviour, order, or membership.

export interface CompactReason {
  /** Short label shown in the queue row, e.g. "DIRTY SCORE", "TEMP + TIME". */
  label: string;
  /** Full, human-readable reason for the tooltip. */
  full: string;
}

export function compactReason(code: string | null | undefined): CompactReason {
  const raw = (code ?? '').trim();
  if (!raw) return { label: '--', full: 'No source reason' };
  const bare = raw.replace(/^SYN_/, '');
  const full = `${bare.replaceAll('_', ' ').toLowerCase()} (${raw})`;
  const hasTemp = /TEMP/.test(bare);
  const hasTime = /TIME|ELAPSED|INTERVAL|SINCE_CLEAN/.test(bare);
  let label: string;
  if (hasTemp && hasTime) label = 'TEMP + TIME';
  else if (hasTemp) label = 'TEMP';
  else if (/TIME_DUE/.test(bare)) label = 'TIME DUE';
  else if (hasTime) label = 'TIME';
  else if (/DIRTY_SCORE/.test(bare)) label = 'DIRTY SCORE';
  else if (/OPERATOR/.test(bare)) label = 'OPERATOR';
  else if (/SCENARIO/.test(bare)) label = 'SCENARIO';
  else label = bare.replaceAll('_', ' ').slice(0, 14);
  return { label, full };
}
