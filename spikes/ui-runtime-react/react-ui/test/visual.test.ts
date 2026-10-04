// WJSS Stage 0.2.1A — single visual mapping: seven independent dimensions,
// Dirty-vs-Alarm independence, neutral presentation for BAD/STALE/DISABLED.
import { describe, expect, it } from 'vitest';
import { PALETTE, toCellVisual } from '../src/visual/toCellVisual';
import { makeSensor } from './helpers';

const ctx = { selected: false, threshold: 50 };

describe('toCellVisual', () => {
  it('Dirty background is a process colour and is independent of alarm state', () => {
    const dirty = makeSensor({ sensorId: 'SYN-LEFT-01', dirtyScore: 80, lastValidatedScore: 80, classification: 'DIRTY' });
    const dirtyAlarm = { ...dirty, alarmState: 'ACTIVE_UNACK' as const, alarmSeverity: 'HIGH' as const };
    const a = toCellVisual(dirty, ctx);
    const b = toCellVisual(dirtyAlarm, ctx);
    expect(a.background).toBe(b.background);
    expect(a.alarm.borderColor).toBeNull();
    expect(b.alarm.borderColor).toBe(PALETTE.alarm);
    expect(b.background).not.toContain(PALETTE.alarm);
  });

  it('an alarm on a Cleaner Sensor does not turn it red', () => {
    const v = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', alarmState: 'ACTIVE_UNACK', alarmSeverity: 'HIGH' }), ctx);
    expect(v.process).toBe('CLEANER');
    expect(v.background).toContain(`hsl(${PALETTE.cleanerHue}`);
    expect(v.alarm.icon).toBe('!');
  });

  it('cleared-unacknowledged alarm uses a dashed alarm border', () => {
    const v = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', alarmState: 'CLEARED_UNACK' }), ctx);
    expect(v.alarm.borderStyle).toBe('dashed');
  });

  it.each(['BAD', 'STALE', 'DISABLED'] as const)('%s is neutral, never Dirty/Cleaner background', (quality) => {
    const v = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', quality, classification: 'NOT_CLASSIFIED', classificationBasis: 'NONE', dirtyScore: 90 }), ctx);
    expect(v.process).toBe('NEUTRAL');
    expect([PALETTE.neutral, PALETTE.neutralDisabled]).toContain(v.background);
    expect(v.scoreText).toBe('--');
    expect(v.qualityMarker).not.toBe('NONE');
  });

  it('defensive: a BAD record wrongly marked DIRTY still renders neutral', () => {
    const v = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', quality: 'BAD', classification: 'DIRTY', dirtyScore: 90 }), ctx);
    expect(v.process).toBe('NEUTRAL');
  });

  it('UNCERTAIN shows last validated classification with amber marker and detail note', () => {
    const v = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', quality: 'UNCERTAIN', classification: 'DIRTY', classificationBasis: 'LAST_VALIDATED', dirtyScore: 10, lastValidatedScore: 70 }), ctx);
    expect(v.process).toBe('DIRTY');
    expect(v.qualityMarker).toBe('UNCERTAIN_AMBER');
    expect(v.scoreText).toBe('70');
    expect(v.detailNote).toMatch(/last validated classification/i);
  });

  it('UNCERTAIN without last validated value is neutral NOT_CLASSIFIED with marker', () => {
    const v = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', quality: 'UNCERTAIN', classification: 'NOT_CLASSIFIED', classificationBasis: 'NONE', lastValidatedScore: null, lastValidatedAt: null }), ctx);
    expect(v.process).toBe('NEUTRAL');
    expect(v.qualityMarker).toBe('UNCERTAIN_AMBER');
  });

  it('intensity follows Dirty Score', () => {
    const lo = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', classification: 'DIRTY', dirtyScore: 55 }), ctx);
    const hi = toCellVisual(makeSensor({ sensorId: 'SYN-LEFT-01', classification: 'DIRTY', dirtyScore: 95 }), ctx);
    expect(hi.intensity).toBeGreaterThan(lo.intensity);
    expect(hi.background).not.toBe(lo.background);
  });

  it('selection, active job, queue badge, and alarm are independent channels', () => {
    const base = makeSensor({ sensorId: 'SYN-LEFT-01', classification: 'DIRTY', dirtyScore: 70 });
    const all = toCellVisual({ ...base, queueState: 'ACTIVE', isActiveJobTarget: true, alarmState: 'ACTIVE_ACK' }, { selected: true, threshold: 50 });
    const none = toCellVisual(base, ctx);
    expect(all.background).toBe(none.background);
    expect(all.selectedRing && all.activeJobOutline).toBe(true);
    expect(all.queueBadge?.code).toBe('J');
    expect(all.alarm.state).toBe('ACTIVE_ACK');
    expect(none.selectedRing || none.activeJobOutline).toBe(false);
    expect(none.queueBadge).toBeNull();
  });
});
