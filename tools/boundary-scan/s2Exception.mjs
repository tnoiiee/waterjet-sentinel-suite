// Stage 0.4C-K2 - the narrowly scoped boundary-scan S2 exception (Owner approved for this Stage).
//
// S2 forbids a non-loopback IPv4 literal in the Product tree. The only exception permitted is the
// six approved TEST_CONFIGURATION addresses, and ONLY inside the single canonical TC Node configuration
// source file named below. It is not a range, not a prefix, not a wildcard and not a second source:
// the approved addresses are enumerated as octets, so this file itself contains no dotted literal.
//
// Matching is exact: the repository-relative path must equal the pinned path character for character,
// with no normalisation that could map another spelling onto it, and the address must equal one of
// the six enumerated dotted forms exactly. Everything else keeps its S2 finding.

/** The one repository-relative path that may carry the approved Test addresses. */
export const S2_EXCEPTION_PATH = 'packages/commissioning/src/tcNodeConfiguration.mjs';

const EXCEPTION_PREFIX = Object.freeze([192, 0, 2]);
const EXCEPTION_LAST_OCTETS = Object.freeze([11, 12, 13, 14, 15, 16]);

/** The six exact dotted-decimal addresses the exception covers. Derived, never a range. */
export function s2ExceptionAddresses() {
  return Object.freeze(EXCEPTION_LAST_OCTETS.map((last) => [...EXCEPTION_PREFIX, last].join('.')));
}

/**
 * True only when `relPath` is exactly the canonical path AND `address` is exactly one of the six
 * approved addresses. Any other path, any spelling variant of the path, or any other address is false.
 */
export function isS2ExceptionAddress(relPath, address) {
  if (typeof relPath !== 'string' || typeof address !== 'string') return false;
  if (relPath !== S2_EXCEPTION_PATH) return false;
  return s2ExceptionAddresses().includes(address);
}
