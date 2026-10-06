// WJSS Stage 0.2.1A — single classification implementation (shared by harness and tests).
// SYNTHETIC SPIKE LOGIC — NOT PRODUCTION CLASSIFICATION LOGIC.
//
// Approved spike rule:
//   GOOD                  -> classify current score; it becomes the last validated value.
//   UNCERTAIN             -> keep last validated classification (amber marker in UI);
//                            new uncertain values never change classification;
//                            no last validated value -> NOT_CLASSIFIED.
//   BAD / STALE / DISABLED-> NOT_CLASSIFIED (neutral presentation).
// Threshold comes from the Synthetic Published Configuration: score > threshold = DIRTY.

/** @param {number} score @param {number} threshold */
export function classifyScore(score, threshold) {
  return score > threshold ? 'DIRTY' : 'CLEANER';
}

/**
 * @param {{quality: string, score: number|null, lastValidatedScore: number|null, threshold: number}} input
 * @returns {{classification: 'DIRTY'|'CLEANER'|'NOT_CLASSIFIED', basis: 'CURRENT'|'LAST_VALIDATED'|'NONE', updatesLastValidated: boolean}}
 */
export function classifySensor({ quality, score, lastValidatedScore, threshold }) {
  if (quality === 'GOOD' && typeof score === 'number' && Number.isFinite(score)) {
    return { classification: classifyScore(score, threshold), basis: 'CURRENT', updatesLastValidated: true };
  }
  if (quality === 'UNCERTAIN') {
    if (typeof lastValidatedScore === 'number') {
      return { classification: classifyScore(lastValidatedScore, threshold), basis: 'LAST_VALIDATED', updatesLastValidated: false };
    }
    return { classification: 'NOT_CLASSIFIED', basis: 'NONE', updatesLastValidated: false };
  }
  return { classification: 'NOT_CLASSIFIED', basis: 'NONE', updatesLastValidated: false };
}
