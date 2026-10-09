// Stage 0.4B-1 — provider policy for the Mapping Configuration surface.
//
// SIMULATOR is the only accepted provider. Every other name is refused,
// including the hardware test and production names. Selecting a provider
// opens no connection and performs no I/O.

import { PROVIDER } from './constants.mjs';

// Named refusals. Any other name is refused as PROVIDER_UNKNOWN_REFUSED, so the list is not the only guard.
const REFUSED = Object.freeze(['TEST_HARDWARE', 'PRODUCTION', 'HARDWARE', 'DEVICE']);

export function activeProvider() {
  return PROVIDER.SIMULATOR;
}

/** Returns { ok, provider, refusal }. No provider other than SIMULATOR is ever accepted. */
export function selectProvider(name) {
  if (name === PROVIDER.SIMULATOR) {
    return Object.freeze({ ok: true, provider: PROVIDER.SIMULATOR, refusal: null });
  }
  const code = REFUSED.includes(String(name).toUpperCase()) ? 'PROVIDER_REFUSED_IN_STAGE' : 'PROVIDER_UNKNOWN_REFUSED';
  return Object.freeze({
    ok: false,
    provider: PROVIDER.SIMULATOR,
    refusal: Object.freeze({ code, message: `provider '${name}' is not authorised in Stage 0.4B-1; SIMULATOR remains active` }),
  });
}

export const REFUSED_PROVIDER_NAMES = REFUSED;
