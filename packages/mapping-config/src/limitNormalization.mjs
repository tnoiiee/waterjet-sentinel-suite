// Stage 0.4B-1 — limit normalization (pure; not wired to Runtime or to any device).
//
//   physical contact state  →  ContactPolarity  →  [RawInputInversion, if verified]  →  LimitDetected
//
// ContactPolarity is the Owner rule (ACTIVE_WHEN_CLOSED for IVn limits). RawInputInversion covers any
// inversion configured in the module, coupler, wiring, adapter or software profile. It is UNVERIFIED and
// NOT_CONFIGURED by default, so no inversion is assumed. A wire break is not detectable from a normally
// open contact alone, so wireBreakDetectable is always false.

import { POLARITY, RAW_INPUT_INVERSION } from './constants.mjs';

/**
 * @param {object} input
 *   contactClosed    boolean, the physical contact state
 *   activePolarity   ACTIVE_WHEN_CLOSED or ACTIVE_WHEN_OPEN (explicit; never defaulted here)
 *   inversion        null (not configured, the default) or { verified: true, inverted: boolean }
 */
export function normalizeLimit({ contactClosed, activePolarity, inversion = null }) {
  if (typeof contactClosed !== 'boolean') throw new Error('contactClosed must be a boolean');
  if (activePolarity !== POLARITY.ACTIVE_WHEN_CLOSED && activePolarity !== POLARITY.ACTIVE_WHEN_OPEN) {
    throw new Error('ContactPolarity must be explicit: ACTIVE_WHEN_CLOSED or ACTIVE_WHEN_OPEN');
  }
  const contactDetected = activePolarity === POLARITY.ACTIVE_WHEN_CLOSED ? contactClosed : !contactClosed;

  let rawInputInversion = RAW_INPUT_INVERSION.NOT_CONFIGURED;
  let limitDetected = contactDetected;
  if (inversion !== null) {
    if (typeof inversion !== 'object' || inversion.verified !== true) {
      throw new Error('RawInputInversion is not verified and cannot be applied');
    }
    // Applied exactly once, here, and nowhere else in the chain.
    if (inversion.inverted === true) {
      limitDetected = !contactDetected;
      rawInputInversion = RAW_INPUT_INVERSION.APPLIED_ONCE;
    } else {
      rawInputInversion = RAW_INPUT_INVERSION.NOT_INVERTED;
    }
  }

  return Object.freeze({
    limitDetected,
    steps: Object.freeze({
      contactState: contactClosed ? 'CLOSED' : 'OPEN',
      contactPolarity: activePolarity,
      rawInputInversion,
    }),
    wireBreakDetectable: false,
  });
}
