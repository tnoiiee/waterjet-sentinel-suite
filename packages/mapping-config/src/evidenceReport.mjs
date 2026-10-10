// Stage 0.4B-3 — read-only evidence review model.
//
// Flattens an evidence evaluation into plain data for the review surface. Every value is either verified evidence or
// an explicit state. Nothing here is editable, and no value is a default.
//
// The report keeps four things apart, each in its own field:
//   rackSlot               physical position from the authoritative workbook
//   ioCheckPosition        position in the visible actual-rack configuration-tool sequence (instance-level evidence)
//   processModulePosition  one-based ordinal among modules that contribute Application Process I/O data
//   processImageOrder      the order used by the process-image rule (verified only; NOT_VERIFIED otherwise)
//
// A module that contributes no Application Process I/O data — the head station, a Power Supply, the System Power
// Supply or the End Module — reports NOT_APPLICABLE with reason NON_PROCESS_DATA_MODULE. It is never reported as
// ADDRESS_UNRESOLVED, because it is not waiting for an address.

import { EVIDENCE_STATE, NOT_APPLICABLE, REASON } from './constants.mjs';
import { resolveProcessImage } from './processImageEvidence.mjs';
import { channelEvidenceFor } from './actualRackEvidence.mjs';

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
 * @param rackView  output of deriveRackView for the rack being reviewed (with actual-rack evidence when it applies)
 */
export function buildEvidenceReport(evidence, rackView) {
  const image = resolveProcessImage(evidence, rackView);
  const modules = image.modules.map((m) => {
    const width = m.cells.processWidthBits;
    const notApplicable = m.applicable === false;
    return {
      moduleInstanceId: m.moduleInstanceId,
      modelNumber: m.modelNumber,
      role: m.role,
      rackSlot: m.rackSlot,
      // Separate fields: the configuration-tool position is never shown as a RackSlot or as a ProcessModulePosition.
      ioCheckPosition: m.ioCheckPosition ?? null,
      ioCheckPositionState: m.ioCheckPositionState ?? null,
      actualRackEvidenceState: m.actualRackEvidenceState ?? null,
      processModulePosition: m.processModulePosition,
      processDataContribution: m.processDataContribution,
      manufacturer: m.manufacturer,
      evidenceState: m.evidenceState,
      completeness: notApplicable ? NOT_APPLICABLE : m.complete ? 'COMPLETE' : 'INCOMPLETE',
      applicable: m.applicable !== false,
      sourceIds: [...m.sourceIds],
      // Verified process width, and separately the unverified manual capability. They are never merged.
      processInputWidth: notApplicable ? '0 bit' : widthText(width, 'input'),
      processOutputWidth: notApplicable ? '0 bit' : widthText(width, 'output'),
      processInputWidthBits: notApplicable ? 0 : m.processInputWidthBits,
      processOutputWidthBits: notApplicable ? 0 : m.processOutputWidthBits,
      manualFacts: m.manualFacts
        ? {
          sourceId: m.manualFacts.sourceId,
          availability: m.manualFacts.availability,
          evidenceState: m.manualFacts.evidenceState,
          scope: m.manualFacts.scope,
          processInputBits: m.manualFacts.facts.processInputBits ?? 0,
          processOutputBits: m.manualFacts.facts.processOutputBits ?? 0,
          note: m.manualFacts.note,
        }
        : null,
      diagnosticState: notApplicable ? NOT_APPLICABLE : m.channelModule ? cellSummary(m, ['diagnosticBytes', 'fillerBits']) : NOT_APPLICABLE,
      statusByteState: notApplicable ? NOT_APPLICABLE : m.analog ? cellSummary(m, ['statusByte']) : NOT_APPLICABLE,
      byteWordOrderState: notApplicable ? NOT_APPLICABLE : m.channelModule ? cellSummary(m, ['byteOrder', 'wordOrder']) : NOT_APPLICABLE,
      processImageOrder: { ...m.processImageOrder },
      addressState: m.address.state,
      // A non-process module states why no address applies; it is not an unresolved address.
      addressReason: m.address.state === NOT_APPLICABLE ? REASON.NON_PROCESS_DATA_MODULE : null,
      reasons: [...m.address.reasons],
      fingerprint: m.fingerprint,
      evidenceNote: m.evidenceNote,
    };
  });
  const notApplicableCount = modules.filter((m) => !m.applicable).length;
  const completeCount = modules.filter((m) => m.completeness === 'COMPLETE').length;
  const channelEvidence = channelEvidenceFor(rackView);
  return {
    evidenceSetId: image.evidenceSetId,
    label: image.label,
    synthetic: image.synthetic,
    authoritative: image.authoritative,
    fingerprint: image.fingerprint,
    addressCapable: image.addressCapable,
    head: {
      modelNumber: image.head.modelNumber,
      role: 'FIELDBUS_COUPLER',
      processDataContribution: 'NONE',
      addressState: NOT_APPLICABLE,
      addressReason: REASON.NON_PROCESS_DATA_MODULE,
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
      availability: s.availability ?? null,
    })),
    issues: image.issues.map((i) => ({ code: i.code, severity: i.severity, message: i.message, path: i.path })),
    modules,
    // Instance- and Channel-scoped actual-rack evidence. It never generalises to another Channel or instance.
    channelEvidence: channelEvidence.map((c) => ({ ...c, settings: { ...c.settings }, appliesTo: { ...c.appliesTo } })),
    summary: {
      modules: modules.length,
      completeProfiles: completeCount,
      incompleteProfiles: modules.length - completeCount - notApplicableCount,
      notApplicableModules: notApplicableCount,
      derivedModules: modules.filter((m) => m.addressState === 'DERIVED').length,
      unresolvedModules: modules.filter((m) => m.addressState === 'ADDRESS_UNRESOLVED').length,
    },
  };
}
