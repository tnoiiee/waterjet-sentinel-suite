// Stage 0.4B-2 — read-only evidence review model.
//
// Flattens an evidence evaluation into plain data for the review surface. Every value is either verified evidence
// or an explicit state. Nothing here is editable, and no value is a default.

import { EVIDENCE_STATE } from './constants.mjs';
import { resolveProcessImage } from './processImageEvidence.mjs';

const isVerified = (s) => s === EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE || s === EVIDENCE_STATE.VERIFIED_IO_CHECK_EXPORT;

function widthText(cell, key) {
  return isVerified(cell.state) ? `${cell.value[key]} bit` : null;
}

function cellSummary(m, names) {
  const states = names.map((n) => m.cells[n].state);
  if (states.every(isVerified)) return 'VERIFIED';
  if (states.includes(EVIDENCE_STATE.CONFLICTING_EVIDENCE)) return EVIDENCE_STATE.CONFLICTING_EVIDENCE;
  if (states.every((s) => s === EVIDENCE_STATE.NOT_PROVIDED)) return EVIDENCE_STATE.NOT_PROVIDED;
  return EVIDENCE_STATE.INCOMPLETE_PROFILE;
}

/**
 * @param evidence  an evidence set
 * @param rackView  output of deriveRackView for the rack being reviewed
 */
export function buildEvidenceReport(evidence, rackView) {
  const image = resolveProcessImage(evidence, rackView);
  const modules = image.modules.map((m) => {
    const width = m.cells.processWidthBits;
    return {
      moduleInstanceId: m.moduleInstanceId,
      modelNumber: m.modelNumber,
      rackSlot: m.rackSlot,
      processModulePosition: m.processModulePosition,
      manufacturer: m.manufacturer,
      evidenceState: m.evidenceState,
      completeness: m.complete ? 'COMPLETE' : 'INCOMPLETE',
      sourceIds: [...m.sourceIds],
      processInputWidth: widthText(width, 'input'),
      processOutputWidth: widthText(width, 'output'),
      diagnosticState: m.channelModule ? cellSummary(m, ['diagnosticBytes', 'fillerBits']) : 'NOT_APPLICABLE',
      statusByteState: m.analog ? cellSummary(m, ['statusByte']) : 'NOT_APPLICABLE',
      byteWordOrderState: m.channelModule ? cellSummary(m, ['byteOrder', 'wordOrder']) : 'NOT_APPLICABLE',
      processImageOrder: { ...m.processImageOrder },
      addressState: m.address.state,
      reasons: [...m.address.reasons],
      fingerprint: m.fingerprint,
      evidenceNote: m.evidenceNote,
    };
  });
  const verifiedCount = modules.filter((m) => m.completeness === 'COMPLETE').length;
  return {
    evidenceSetId: image.evidenceSetId,
    label: image.label,
    synthetic: image.synthetic,
    authoritative: image.authoritative,
    fingerprint: image.fingerprint,
    addressCapable: image.addressCapable,
    head: {
      modelNumber: image.head.modelNumber,
      evidenceState: image.head.evidenceState,
      completeness: image.head.complete ? 'COMPLETE' : 'INCOMPLETE',
      missingCells: [...image.head.missingCells],
      reasons: [...image.head.reasons],
      sourceIds: [...image.head.sourceIds],
      fingerprint: image.head.fingerprint,
      evidenceNote: image.head.evidenceNote,
    },
    sources: image.sources.map((s) => ({
      sourceId: s.sourceId,
      evidenceType: s.evidenceType,
      documentTitle: s.documentTitle,
      documentSha256: s.documentSha256 ?? null,
      documentRevision: s.documentRevision ?? null,
      effectiveDate: s.effectiveDate ?? null,
      manufacturer: s.manufacturer ?? null,
    })),
    issues: image.issues.map((i) => ({ code: i.code, severity: i.severity, message: i.message, path: i.path })),
    modules,
    summary: {
      modules: modules.length,
      completeProfiles: verifiedCount,
      incompleteProfiles: modules.length - verifiedCount,
      derivedModules: modules.filter((m) => m.addressState === 'DERIVED').length,
      unresolvedModules: modules.filter((m) => m.addressState === 'ADDRESS_UNRESOLVED').length,
    },
  };
}
