// Stage 0.4B-3 — read-only CANDIDATE process image.
//
// A candidate is what the rack would look like IF the manual facts and the visible actual-rack sequence hold. It is
// not an address map. Every part of it is labelled CANDIDATE_UNVERIFIED, and it is never authoritative, never
// hardware-ready and never a basis for a write.
//
// What a candidate is built from, and what each part is worth:
//   topology      the authoritative Owner workbook (committed, SHA-256 recorded): RackSlot, model, ModuleInstanceId.
//   widths        the Owner-supplied primary manuals: channels per module and data bits per channel. The manuals are
//                 outside the repository and unhashed, so these facts are PROVIDED_UNVERIFIED model capabilities.
//   ordering      the visible actual-rack configuration-tool sequence: which module follows which. It is
//                 actual-rack evidence for the sequence, but it is NOT the head-station process-image rule. The real
//                 grouping (for example analog-before-digital), the word width and the alignment rule are unverified,
//                 so the candidate order may change.
//
// What a candidate does NOT contain: any byte offset, word offset or bit offset. The actual Process Data screen and
// the head-station I/O Config have not been supplied, so every offset field is present, null and OFFSET_UNRESOLVED.
// A candidate offset would be a guess, and this package does not guess.
//
// Pure data and arithmetic. No I/O, no device access, no write path.

import {
  ADDRESS_STATE, CANDIDATE_STATUS, DIRECTION, EVIDENCE_AVAILABILITY, EVIDENCE_STATE, NOT_APPLICABLE,
  PROCESS_DATA_CONTRIBUTION, REASON,
} from './constants.mjs';
import { AUTHORITATIVE_WORKBOOK_SHA256, MISSING_ACTUAL_RACK_EVIDENCE, UNVERIFIED_UNTIL_SUPPLIED } from './actualRackEvidence.mjs';
import { getProfile } from './moduleProfiles.mjs';

export const OFFSET_STATE = Object.freeze({ UNRESOLVED: 'OFFSET_UNRESOLVED' });

const CANDIDATE_ORDER_BASIS = 'ACTUAL_RACK_VISIBLE_SEQUENCE';

const OFFSET_REASONS = Object.freeze([
  REASON.ACTUAL_PROCESS_DATA_NOT_PROVIDED,
  REASON.ACTUAL_MAPPING_NOT_CROSS_CHECKED,
  REASON.NO_VERIFIED_PROCESS_IMAGE_RULE,
  REASON.PROCESS_IMAGE_ORDER_NOT_VERIFIED,
]);

const noOffsets = () => Object.freeze({
  state: OFFSET_STATE.UNRESOLVED,
  byteOffset: null,
  wordOffset: null,
  bitOffset: null,
  reasons: OFFSET_REASONS,
});

/**
 * Builds the read-only candidate process image of a rack.
 *
 * @param rackView  output of deriveRackView, ideally with actual-rack evidence attached. Every position field is
 *                  read from the view, never recomputed here, so RackSlot, ioCheckPosition and
 *                  ProcessModulePosition stay separate.
 */
export function buildCandidateProcessImage(rackView) {
  const contributors = [];
  const nonProcess = [];

  for (const v of rackView) {
    const profile = getProfile(v.modelNumber);
    const manual = profile ? profile.manualFacts : null;
    const contributes = v.processDataContribution !== PROCESS_DATA_CONTRIBUTION.NONE && profile && profile.channelCount > 0;
    if (!contributes) {
      // Power Supply, System Power Supply, End Module and head station: no process data, no position, no address.
      nonProcess.push(Object.freeze({
        moduleInstanceId: v.moduleInstanceId,
        modelNumber: v.modelNumber,
        role: v.role,
        rackSlot: v.rackSlot,
        ioCheckPosition: v.ioCheckPosition ?? null,
        processModulePosition: null,
        processImageOrder: { state: NOT_APPLICABLE, value: null },
        processDataContribution: PROCESS_DATA_CONTRIBUTION.NONE,
        inputWidthBits: 0,
        outputWidthBits: 0,
        addressStatus: ADDRESS_STATE.NOT_APPLICABLE,
        addressReason: REASON.NON_PROCESS_DATA_MODULE,
        verificationStatus: NOT_APPLICABLE,
      }));
      continue;
    }
    contributors.push({
      view: v,
      profile,
      manual,
      area: profile.direction === DIRECTION.INPUT ? DIRECTION.INPUT : DIRECTION.OUTPUT,
      bitsPerChannel: manual ? manual.facts.bitsPerChannel : null,
      moduleBits: manual
        ? (profile.direction === DIRECTION.INPUT ? manual.facts.processInputBits : manual.facts.processOutputBits)
        : null,
    });
  }

  // Candidate order per image area, in the visible actual-rack sequence. It is a candidate ordinal only: the
  // head-station grouping rule is not verified, so the real ProcessImageOrder may differ.
  const areas = {};
  for (const area of [DIRECTION.INPUT, DIRECTION.OUTPUT]) {
    const members = contributors.filter((c) => c.area === area)
      .sort((a, b) => (a.view.ioCheckPosition ?? a.view.rackSlot) - (b.view.ioCheckPosition ?? b.view.rackSlot)
        || a.view.rackSlot - b.view.rackSlot);
    members.forEach((c, i) => { c.order = Object.freeze({ state: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED, value: i + 1, basis: CANDIDATE_ORDER_BASIS }); });
    areas[area] = Object.freeze({
      area,
      candidateModules: members.length,
      candidateChannels: members.reduce((n, c) => n + c.profile.channelCount, 0),
      orderingBasis: CANDIDATE_ORDER_BASIS,
      orderingState: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED,
      offsets: noOffsets(),
      note: 'Candidate order follows the visible actual-rack configuration-tool sequence. The head-station grouping '
        + 'rule, the word width and the alignment rule are not verified, so the real ProcessImageOrder may differ.',
    });
  }

  const modules = contributors.map((c) => Object.freeze({
    moduleInstanceId: c.view.moduleInstanceId,
    modelNumber: c.view.modelNumber,
    role: c.view.role,
    rackSlot: c.view.rackSlot,
    ioCheckPosition: c.view.ioCheckPosition ?? null,
    processModulePosition: c.view.processModulePosition,
    processImageOrder: c.order,
    area: c.area,
    processDataContribution: c.view.processDataContribution,
    channelCount: c.profile.channelCount,
    bitsPerChannel: c.bitsPerChannel,
    moduleBits: c.moduleBits,
    offsets: noOffsets(),
    evidenceBasis: Object.freeze([
      `OWNER-WORKBOOK (${EVIDENCE_AVAILABILITY.COMMITTED_IN_REPOSITORY}, SHA-256 ${AUTHORITATIVE_WORKBOOK_SHA256})`,
      `${c.manual.sourceId} (${c.manual.availability}, ${c.manual.evidenceState})`,
      c.view.actualRackEvidenceState === null || c.view.actualRackEvidenceState === undefined
        ? 'ACTUAL-RACK SEQUENCE (NOT_APPLICABLE_TO_THIS_RACK)'
        : `OWNER-ACTUAL-RACK-SCREENSHOT (${c.view.actualRackEvidenceState})`,
    ]),
    verificationStatus: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED,
    addressStatus: ADDRESS_STATE.UNRESOLVED,
    addressReason: REASON.ACTUAL_MAPPING_NOT_CROSS_CHECKED,
    writeAuthority: false,
  }));

  const channels = [];
  for (const m of modules) {
    for (let channel = 1; channel <= m.channelCount; channel += 1) {
      channels.push(Object.freeze({
        moduleInstanceId: m.moduleInstanceId,
        modelNumber: m.modelNumber,
        rackSlot: m.rackSlot,
        ioCheckPosition: m.ioCheckPosition,
        processModulePosition: m.processModulePosition,
        processImageOrder: m.processImageOrder,
        area: m.area,
        channel,
        bitWidth: m.bitsPerChannel,
        offsets: noOffsets(),
        evidenceBasis: m.evidenceBasis,
        verificationStatus: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED,
        addressStatus: ADDRESS_STATE.UNRESOLVED,
        addressReason: REASON.ACTUAL_MAPPING_NOT_CROSS_CHECKED,
        writeAuthority: false,
      }));
    }
  }

  return Object.freeze({
    status: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED,
    label: 'CANDIDATE PROCESS IMAGE · UNVERIFIED · NOT AUTHORITATIVE',
    authoritative: false,
    verified: false,
    hardwareReady: false,
    writeAuthority: false,
    basis: Object.freeze({
      topology: Object.freeze({
        sourceId: 'OWNER-WORKBOOK', availability: EVIDENCE_AVAILABILITY.COMMITTED_IN_REPOSITORY,
        workbookSha256: AUTHORITATIVE_WORKBOOK_SHA256,
      }),
      widths: Object.freeze({
        sourceIdPrefix: 'OWNER-PRIMARY-MANUAL-', availability: EVIDENCE_AVAILABILITY.OWNER_PROVIDED_OUTSIDE_REPOSITORY,
        evidenceState: EVIDENCE_STATE.PROVIDED_UNVERIFIED,
        note: 'Manual facts are model capabilities. They are not actual-rack settings and they do not place a module '
          + 'in the process image.',
      }),
      ordering: Object.freeze({
        basis: CANDIDATE_ORDER_BASIS, state: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED,
        note: 'The visible sequence is actual-rack evidence for the order of modules. It is not the head-station '
          + 'process-image rule.',
      }),
    }),
    areas: Object.freeze(areas),
    modules: Object.freeze(modules),
    channels: Object.freeze(channels),
    nonProcessModules: Object.freeze(nonProcess),
    missingEvidence: MISSING_ACTUAL_RACK_EVIDENCE,
    unverifiedUntilSupplied: UNVERIFIED_UNTIL_SUPPLIED,
    reasons: OFFSET_REASONS,
  });
}
