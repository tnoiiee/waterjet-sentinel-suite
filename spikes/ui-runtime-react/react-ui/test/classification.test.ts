// WJSS Stage 0.2.1A — classification rules (shared module used by the harness).
import { describe, expect, it } from 'vitest';
import { classifyScore, classifySensor } from '../../contracts/classify.mjs';

describe('Dirty threshold boundary (threshold from synthetic published config)', () => {
  it('score > threshold is DIRTY, score <= threshold is CLEANER', () => {
    expect(classifyScore(50, 50)).toBe('CLEANER');
    expect(classifyScore(50.01, 50)).toBe('DIRTY');
    expect(classifyScore(49.99, 50)).toBe('CLEANER');
    expect(classifyScore(0, 50)).toBe('CLEANER');
    expect(classifyScore(100, 50)).toBe('DIRTY');
  });
  it('uses the threshold passed in (configuration revision), not a constant', () => {
    expect(classifyScore(60, 70)).toBe('CLEANER');
    expect(classifyScore(60, 55)).toBe('DIRTY');
  });
});

describe('quality rules', () => {
  it('GOOD classifies the current value and updates last validated', () => {
    expect(classifySensor({ quality: 'GOOD', score: 51, lastValidatedScore: 10, threshold: 50 })).toEqual({ classification: 'DIRTY', basis: 'CURRENT', updatesLastValidated: true });
  });
  it('UNCERTAIN keeps the last validated classification; the new value never changes it', () => {
    const r = classifySensor({ quality: 'UNCERTAIN', score: 95, lastValidatedScore: 20, threshold: 50 });
    expect(r).toEqual({ classification: 'CLEANER', basis: 'LAST_VALIDATED', updatesLastValidated: false });
    const r2 = classifySensor({ quality: 'UNCERTAIN', score: 5, lastValidatedScore: 80, threshold: 50 });
    expect(r2.classification).toBe('DIRTY');
  });
  it('UNCERTAIN without a last validated value is NOT_CLASSIFIED', () => {
    expect(classifySensor({ quality: 'UNCERTAIN', score: 70, lastValidatedScore: null, threshold: 50 })).toEqual({ classification: 'NOT_CLASSIFIED', basis: 'NONE', updatesLastValidated: false });
  });
  it.each(['BAD', 'STALE', 'DISABLED'] as const)('%s is NOT_CLASSIFIED regardless of score', (quality) => {
    for (const score of [0, 50, 51, 100, null]) {
      expect(classifySensor({ quality, score, lastValidatedScore: 80, threshold: 50 })).toEqual({ classification: 'NOT_CLASSIFIED', basis: 'NONE', updatesLastValidated: false });
    }
  });
});
