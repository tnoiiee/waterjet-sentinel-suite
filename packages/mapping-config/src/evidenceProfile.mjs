// Stage 0.4B-2 — Verified process-image evidence foundation.
// Pure data: no I/O, no device access, no provider switches.
//
// Distinguishes:
//   1. Owner workbook facts
//   2. Primary vendor evidence
//   3. Head-station process-image rules
//   4. Module process-data profiles
//   5. Module configuration or status-byte settings
//   6. Derived process-image order
//   7. Derived read-only address and offset
//   8. Unresolved evidence state
//
// Never calculates numeric addresses from an assumed generic rule.
// A Module Profile may become address-capable only when all required evidence
// is verified. Until then, addresses remain ADDRESS_UNRESOLVED.

import { EVIDENCE_STATE, REASON } from './constants.mjs';

/**
 * Creates an Evidence Record representing vendor or export evidence.
 */
export function createEvidenceRecord({
  sourceDocumentIdentity = null,
  sourceDocumentSha256 = null,
  manufacturer = 'WAGO',
  model = null,
  evidenceType = null,
  processInputWidth = null,
  processOutputWidth = null,
  fillerBits = 0,
  fillerBytes = 0,
  diagnosticBytes = 0,
  statusByteBehavior = 'NOT_VERIFIED',
  byteOrder = null,
  wordOrder = null,
  processImageGrouping = null,
  moduleMappingOrder = null,
  profileCompleteness = 'INCOMPLETE',
  verificationState = EVIDENCE_STATE.NOT_PROVIDED,
  unresolvedReasons = [],
  evidenceNote = '',
  effectiveDate = null,
  documentRevision = null,
} = {}) {
  return Object.freeze({
    sourceDocumentIdentity,
    sourceDocumentSha256,
    manufacturer,
    model,
    evidenceType,
    processInputWidth,
    processOutputWidth,
    fillerBits,
    fillerBytes,
    diagnosticBytes,
    statusByteBehavior,
    byteOrder,
    wordOrder,
    processImageGrouping,
    moduleMappingOrder,
    profileCompleteness,
    verificationState,
    unresolvedReasons: Object.freeze([...unresolvedReasons]),
    evidenceNote,
    effectiveDate,
    documentRevision,
  });
}

/**
 * Evaluates whether a module's evidence is address-capable.
 */
export function evaluateEvidenceCapability(evidence, { isAnalog = false } = {}) {
  const reasons = [];
  if (!evidence) {
    return {
      isCapable: false,
      reasons: Object.freeze([
        REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED,
        REASON.NO_VERIFIED_PROCESS_IMAGE_RULE,
      ]),
    };
  }

  if (evidence.verificationState === EVIDENCE_STATE.CONFLICTING_EVIDENCE) {
    reasons.push(REASON.CONFLICTING_PROCESS_IMAGE_EVIDENCE);
  }

  const isVerifiedSource =
    evidence.verificationState === EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE ||
    evidence.verificationState === EVIDENCE_STATE.VERIFIED_WAGO_IO_CHECK_EXPORT;

  if (!isVerifiedSource || evidence.profileCompleteness !== 'COMPLETE') {
    reasons.push(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED);
  }

  if (isAnalog) {
    if (evidence.statusByteBehavior === 'NOT_VERIFIED' || evidence.statusByteBehavior == null || evidence.statusByteBehavior === 'OPTIONAL_NOT_CONFIGURED') {
      reasons.push(REASON.MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED);
    }
  }

  if (!evidence.processImageGrouping && !evidence.moduleMappingOrder) {
    reasons.push(REASON.PROCESS_IMAGE_ORDER_NOT_VERIFIED);
  }

  if (!evidence.byteOrder) {
    reasons.push(REASON.BYTE_ORDER_NOT_VERIFIED);
  }

  if (!evidence.wordOrder) {
    reasons.push(REASON.WORD_ORDER_NOT_VERIFIED);
  }

  return {
    isCapable: reasons.length === 0,
    reasons: Object.freeze([...new Set(reasons)]),
  };
}

/**
 * Creates head-station evidence for the coupler (e.g. 750-362).
 */
export function createHeadStationEvidence({
  sourceDocumentIdentity = null,
  sourceDocumentSha256 = null,
  manufacturer = 'WAGO',
  model = '750-362',
  evidenceType = null,
  byteOrder = null,
  wordOrder = null,
  processImageGrouping = null,
  maxInputWords = null,
  maxOutputWords = null,
  verificationState = EVIDENCE_STATE.NOT_PROVIDED,
  unresolvedReasons = [
    REASON.HEAD_STATION_PROFILE_NOT_VERIFIED,
    REASON.NO_VERIFIED_PROCESS_IMAGE_RULE,
    REASON.PROCESS_IMAGE_ORDER_NOT_VERIFIED,
    REASON.BYTE_ORDER_NOT_VERIFIED,
    REASON.WORD_ORDER_NOT_VERIFIED,
  ],
  evidenceNote = 'Coupler profile not verified against primary head-station specification',
  effectiveDate = null,
  documentRevision = null,
} = {}) {
  const isVerified =
    verificationState === EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE ||
    verificationState === EVIDENCE_STATE.VERIFIED_WAGO_IO_CHECK_EXPORT;

  return Object.freeze({
    sourceDocumentIdentity,
    sourceDocumentSha256,
    manufacturer,
    model,
    evidenceType,
    byteOrder,
    wordOrder,
    processImageGrouping,
    maxInputWords,
    maxOutputWords,
    profileCompleteness: isVerified ? 'COMPLETE' : 'INCOMPLETE',
    verificationState,
    unresolvedReasons: Object.freeze(isVerified ? [] : [...unresolvedReasons]),
    evidenceNote,
    effectiveDate,
    documentRevision,
  });
}
