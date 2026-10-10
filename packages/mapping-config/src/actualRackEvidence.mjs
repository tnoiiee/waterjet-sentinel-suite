// Stage 0.4B-3 — authoritative physical topology and ACTUAL-RACK evidence (instance level).
//
// Three position concepts are kept apart and are never aliased to each other:
//
//   rackSlot              the physical position from the authoritative Owner workbook. One-based, physical, and
//                         inclusive of the Power Supply and End modules. There is no RackSlot 0. The authoritative
//                         rack has 23 physical slots.
//   ioCheckPosition       the "Pos." number shown by the vendor configuration tool for the real rack. It is an
//                         internal-bus configuration position: it may omit Power Supply modules from its visible
//                         sequence and it may include the End Module. It is NOT a RackSlot, NOT a
//                         ProcessModulePosition, NOT a ProcessImageOrder and never an address.
//                         (The Owner's Stage ruling names this field after the vendor tool. Boundary rule S3
//                         forbids vendor vocabulary in the Product tree, so the field carries a neutral name here;
//                         the mapping between the two names is recorded in the Stage checkpoint document.)
//   processModulePosition the one-based ordinal among modules that contribute Application Process I/O data. It is
//                         derived by rack.mjs from the rack order, it excludes the head station and the Power
//                         Supply and End modules, and it is not an order, an offset or an address.
//
// Actual-rack evidence in this Stage is PARTIAL. It records only what the supplied configuration-tool screen
// shows, per ModuleInstance and — for settings — per Channel. Nothing here is generalised to another instance,
// another Channel or another model, and nothing here feeds address derivation: the actual Process Data image and
// the actual field-network mapping have not been supplied.
//
// Pure data. No I/O, no Node built-ins, no device access.

import {
  ACTUAL_RACK_EVIDENCE_STATE, ADDRESS_STATE, EVIDENCE_AVAILABILITY, EVIDENCE_TYPE, REASON,
} from './constants.mjs';
import { getProfile } from './moduleProfiles.mjs';
import { buildModuleInstances, deriveRackView } from './rack.mjs';
import { canonicalJson, fingerprintOf } from './canonical.mjs';

/** SHA-256 of the authoritative workbook the topology below was read from. The workbook itself is unchanged. */
export const AUTHORITATIVE_WORKBOOK_SHA256 = '4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e';

export const ACTUAL_RACK_SOURCE = Object.freeze({
  sourceId: 'OWNER-ACTUAL-RACK-SCREENSHOT',
  evidenceType: EVIDENCE_TYPE.ACTUAL_RACK_SCREENSHOT,
  documentTitle: 'Actual-rack configuration-tool screens supplied by the Owner (rack/module sequence and one 750-471 configuration screen)',
  documentSha256: null,
  documentRevision: null,
  effectiveDate: null,
  manufacturer: null,
  availability: EVIDENCE_AVAILABILITY.OWNER_PROVIDED_OUTSIDE_REPOSITORY,
  note: 'Supplied by the Owner outside the repository and not committed. The image files are not present in this '
    + 'workspace, so no SHA-256 is recorded and none is invented. The visible values below are the values the Owner '
    + 'documented for the supplied screens. Plant-specific imagery is never committed or reproduced.',
});

// --------------------------------------------------------------------------- authoritative physical topology

// Owner ruling plus the authoritative workbook: 23 physical RackSlots, one-based, Power Supply and End modules
// included. `ioCheckPosition` is the position of the same physical module in the supplied visible configuration-tool
// sequence; null means the supplied visible sequence contains no position entry for it. The visible sequence
// Pos. 01..20 is fully accounted for by the 5 digital input, 4 digital output, 9 analog input, 1 analog output and
// 1 End module entries, so no visible position entry remains for the head station or the two power supplies.
const TOPOLOGY_ROWS = Object.freeze([
  { rackSlot: 1, modelNumber: '750-362', ioCheckPosition: null },
  { rackSlot: 2, modelNumber: '750-601', ioCheckPosition: null },
  { rackSlot: 3, modelNumber: '750-430', ioCheckPosition: 1 },
  { rackSlot: 4, modelNumber: '750-430', ioCheckPosition: 2 },
  { rackSlot: 5, modelNumber: '750-430', ioCheckPosition: 3 },
  { rackSlot: 6, modelNumber: '750-430', ioCheckPosition: 4 },
  { rackSlot: 7, modelNumber: '750-430', ioCheckPosition: 5 },
  { rackSlot: 8, modelNumber: '750-530', ioCheckPosition: 6 },
  { rackSlot: 9, modelNumber: '750-530', ioCheckPosition: 7 },
  { rackSlot: 10, modelNumber: '750-530', ioCheckPosition: 8 },
  { rackSlot: 11, modelNumber: '750-530', ioCheckPosition: 9 },
  { rackSlot: 12, modelNumber: '750-613', ioCheckPosition: null },
  { rackSlot: 13, modelNumber: '750-471', ioCheckPosition: 10 },
  { rackSlot: 14, modelNumber: '750-471', ioCheckPosition: 11 },
  { rackSlot: 15, modelNumber: '750-471', ioCheckPosition: 12 },
  { rackSlot: 16, modelNumber: '750-471', ioCheckPosition: 13 },
  { rackSlot: 17, modelNumber: '750-471', ioCheckPosition: 14 },
  { rackSlot: 18, modelNumber: '750-471', ioCheckPosition: 15 },
  { rackSlot: 19, modelNumber: '750-471', ioCheckPosition: 16 },
  { rackSlot: 20, modelNumber: '750-471', ioCheckPosition: 17 },
  { rackSlot: 21, modelNumber: '750-471', ioCheckPosition: 18 },
  { rackSlot: 22, modelNumber: '750-554', ioCheckPosition: 19 },
  { rackSlot: 23, modelNumber: '750-600', ioCheckPosition: 20 },
]);

const NOT_IN_VISIBLE_SEQUENCE = Object.freeze({
  state: ACTUAL_RACK_EVIDENCE_STATE.NOT_PROVIDED,
  note: 'The supplied visible configuration-tool sequence contains no position entry for this module. It remains a '
    + 'physical module of the authoritative workbook topology; a missing position entry does not remove it.',
});

const IN_VISIBLE_SEQUENCE = Object.freeze({
  state: ACTUAL_RACK_EVIDENCE_STATE.VERIFIED_ACTUAL_RACK_SCREENSHOT,
  note: 'Position entry visible in the supplied actual-rack configuration-tool screen.',
});

function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value);
    for (const v of Object.values(value)) deepFreeze(v);
  }
  return value;
}

/** The authoritative physical topology: 23 RackSlots with model, role and configuration-tool position. */
export const AUTHORITATIVE_RACK_TOPOLOGY = deepFreeze(TOPOLOGY_ROWS.map((row) => {
  const profile = getProfile(row.modelNumber);
  return {
    rackSlot: row.rackSlot,
    modelNumber: row.modelNumber,
    role: profile.role,
    displayName: profile.displayName,
    ioCheckPosition: row.ioCheckPosition,
    ioCheckPositionState: row.ioCheckPosition === null ? NOT_IN_VISIBLE_SEQUENCE.state : IN_VISIBLE_SEQUENCE.state,
    ioCheckPositionNote: row.ioCheckPosition === null ? NOT_IN_VISIBLE_SEQUENCE.note : IN_VISIBLE_SEQUENCE.note,
    workbookSha256: AUTHORITATIVE_WORKBOOK_SHA256,
  };
}));

/** Model numbers of the authoritative rack, in RackSlot order. */
export const AUTHORITATIVE_RACK_MODELS = Object.freeze(AUTHORITATIVE_RACK_TOPOLOGY.map((r) => r.modelNumber));

/**
 * The authoritative rack as module instances, with ModuleInstanceId, RackSlot, role, configuration-tool position
 * and ProcessModulePosition kept in separate fields. Built through the same rack functions the importer uses, so the
 * instance ids match the workbook import exactly.
 */
export function authoritativeRackInstances() {
  const modules = buildModuleInstances(AUTHORITATIVE_RACK_MODELS);
  const view = deriveRackView(modules);
  return deepFreeze(view.map((v, i) => {
    const row = AUTHORITATIVE_RACK_TOPOLOGY[i];
    if (row.rackSlot !== v.rackSlot || row.modelNumber !== v.modelNumber) {
      throw new Error(`internal: topology and rack view disagree at slot ${v.rackSlot}`);
    }
    return {
      moduleInstanceId: v.moduleInstanceId,
      modelNumber: v.modelNumber,
      role: v.role,
      rackSlot: v.rackSlot,
      ioCheckPosition: row.ioCheckPosition,
      ioCheckPositionState: row.ioCheckPositionState,
      ioCheckPositionNote: row.ioCheckPositionNote,
      processModulePosition: v.processModulePosition,
      processModulePositionStatus: v.processModulePositionStatus,
      processDataContribution: v.processDataContribution,
      addressStatus: v.addressStatus,
      addressReason: v.addressStatus === ADDRESS_STATE.NOT_APPLICABLE ? REASON.NON_PROCESS_DATA_MODULE : null,
      // Revisions are recorded per instance and only when legible. Nothing is inferred from another instance.
      softwareRevision: null,
      hardwareRevision: null,
      displayedType: row.modelNumber === '750-471' && row.ioCheckPosition === 10 ? '4AI U/I Diff Galv' : null,
      displayedVersion: row.modelNumber === '750-471' && row.ioCheckPosition === 10 ? '01.01.46(04)' : null,
      revisionEvidenceState: row.modelNumber === '750-471' && row.ioCheckPosition === 10
        ? ACTUAL_RACK_EVIDENCE_STATE.PROVIDED_UNVERIFIED
        : ACTUAL_RACK_EVIDENCE_STATE.NOT_PROVIDED,
      revisionNote: row.modelNumber === '750-471' && row.ioCheckPosition === 10
        ? 'The supplied screen shows one unqualified version field. It is recorded verbatim and is NOT asserted to '
          + 'be either the software or the hardware revision. The supplied evidence indicates that the installed '
          + 'modules of this model do not all share one revision, so no revision is generalised to another instance.'
        : 'No revision is legible for this instance in the supplied evidence. A revision is never inferred.',
      evidenceState: row.ioCheckPositionState,
      evidenceNote: row.ioCheckPositionNote,
      sourceId: ACTUAL_RACK_SOURCE.sourceId,
    };
  }));
}

const AUTHORITATIVE_INSTANCES = authoritativeRackInstances();
const INSTANCE_BY_ID = new Map(AUTHORITATIVE_INSTANCES.map((r) => [r.moduleInstanceId, r]));

/**
 * True when the given rack is made of exactly the authoritative module instances. The test is by instance identity,
 * NOT by order: a Draft reorder moves a RackSlot, but the actual-rack evidence of an instance is a fact about the
 * real rack and stays with that instance.
 */
export function matchesAuthoritativeRack(modules) {
  if (!Array.isArray(modules) || modules.length !== AUTHORITATIVE_INSTANCES.length) return false;
  const seen = new Set();
  for (const m of modules) {
    if (!m || seen.has(m.moduleInstanceId)) return false;
    seen.add(m.moduleInstanceId);
    const known = INSTANCE_BY_ID.get(m.moduleInstanceId);
    if (!known || known.modelNumber !== m.modelNumber) return false;
  }
  return seen.size === AUTHORITATIVE_INSTANCES.length;
}

/**
 * Actual-rack evidence for a rack, keyed by ModuleInstanceId. It is attached to the INSTANCE, not to the current
 * RackSlot: a Draft reorder moves a module but never changes what the real rack shows. When the rack is not the
 * authoritative rack (for example the synthetic example) no actual-rack evidence applies.
 */
export function actualRackEvidenceFor(modules) {
  if (!matchesAuthoritativeRack(modules)) {
    return Object.freeze({
      applicable: false,
      note: 'Actual-rack evidence describes the authoritative 23-module rack only. It is not applied to any other '
        + 'rack, including the synthetic example.',
      byInstance: Object.freeze(new Map()),
      instances: Object.freeze([]),
    });
  }
  return Object.freeze({
    applicable: true,
    note: 'Partial actual-rack evidence: the visible configuration-tool sequence and one 750-471 configuration screen.',
    byInstance: INSTANCE_BY_ID,
    instances: AUTHORITATIVE_INSTANCES,
  });
}

// --------------------------------------------------------------------------- instance and Channel settings

/**
 * The only Channel-level actual-rack setting evidence supplied in this Stage. It verifies exactly one Channel of one
 * instance. It is NOT generalised to Channel 1, 2 or 4 of the same module, to any other 750-471 instance, or to any
 * 750-554 Channel.
 */
export const INSTANCE_CHANNEL_EVIDENCE = deepFreeze([
  {
    moduleInstanceId: 'AI-MODULE-01',
    modelNumber: '750-471',
    rackSlot: 13,
    ioCheckPosition: 10,
    channel: 3,
    scope: 'INSTANCE_AND_CHANNEL_ONLY',
    evidenceState: ACTUAL_RACK_EVIDENCE_STATE.VERIFIED_ACTUAL_RACK_SCREENSHOT,
    displayedType: '4AI U/I Diff Galv',
    displayedVersion: '01.01.46(04)',
    settings: Object.freeze({
      signalType: '4-20 mA',
      inputFilter: 'Off',
      channelDiagnosis: 'On',
      diagnosisOverload: 'On',
      diagnosisWireBreak: 'On',
      diagnosisMeasuringRangeOverflow: 'On',
      diagnosisMeasuringRangeUnderflow: 'On',
      diagnosisUpperUserLimitExceeded: 'On',
      diagnosisLowerUserLimitUndershot: 'On',
      upperUserLimit: 32767,
      lowerUserLimit: -32768,
    }),
    appliesTo: Object.freeze({
      sameModuleOtherChannels: false,
      otherInstancesOfSameModel: false,
      otherModels: false,
    }),
    sourceId: ACTUAL_RACK_SOURCE.sourceId,
    note: 'Verified for this instance and this Channel only. These settings are not evidence for any other Channel '
      + 'or instance, and they do not by themselves place the module in the process image: the Process Data image, '
      + 'the module Common settings, the Scaling screen and the head-station I/O Config are still missing.',
  },
]);

/** Channel-level actual-rack evidence that applies to the given rack (empty unless the rack is authoritative). */
export function channelEvidenceFor(modules) {
  if (!matchesAuthoritativeRack(modules)) return Object.freeze([]);
  return INSTANCE_CHANNEL_EVIDENCE;
}

// --------------------------------------------------------------------------- what is still missing

/** The actual-rack evidence requests recorded by Stage 0.4B-3. None of them blocks the module-role corrections. */
export const MISSING_ACTUAL_RACK_EVIDENCE = deepFreeze([
  {
    request: 'A',
    subject: 'Configuration-tool Process Data screen',
    requiredToShow: Object.freeze([
      'complete Input process image', 'complete Output process image', 'offsets or word positions',
      'module positions', 'Channels', 'any status/control fields',
    ]),
    blocks: Object.freeze([REASON.ACTUAL_PROCESS_DATA_NOT_PROVIDED]),
  },
  {
    request: 'B',
    subject: '750-471 Common settings',
    requiredToShow: Object.freeze(['Data format', 'status-information setting', 'module-level process-data options']),
    blocks: Object.freeze([REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED]),
  },
  {
    request: 'C',
    subject: '750-471 Scaling screen',
    requiredToShow: Object.freeze(['raw representation', 'user scaling', 'channel-specific scaling where applicable']),
    blocks: Object.freeze([REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED]),
  },
  {
    request: 'D',
    subject: '750-554 Settings',
    requiredToShow: Object.freeze(['data format', 'signal mode', 'Channel settings', 'output scaling']),
    blocks: Object.freeze([REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED]),
  },
  {
    request: 'E',
    subject: '750-362 head-station information',
    requiredToShow: Object.freeze([
      'exact firmware revision', 'exact hardware revision', 'I/O Config', 'field-network mapping where available',
    ]),
    blocks: Object.freeze([REASON.HEAD_STATION_PROFILE_NOT_VERIFIED, REASON.ACTUAL_MAPPING_NOT_CROSS_CHECKED]),
  },
]);

/** Statement recorded until request A and request E are supplied. */
export const UNVERIFIED_UNTIL_SUPPLIED = Object.freeze([
  'The actual final field-network mapping remains unverified.',
  'Numeric addresses must not be marked verified.',
  'Hardware test activation remains blocked.',
]);

/** Fingerprint of the actual-rack evidence record. It changes when any recorded observation changes. */
export function actualRackEvidenceFingerprint() {
  return fingerprintOf({
    kind: 'ActualRackEvidence',
    topology: AUTHORITATIVE_RACK_TOPOLOGY,
    instances: AUTHORITATIVE_INSTANCES,
    channelEvidence: INSTANCE_CHANNEL_EVIDENCE,
    source: ACTUAL_RACK_SOURCE,
  });
}

/** Canonical JSON of the topology (used by tests and by the checkpoint record). */
export function topologyCanonical() {
  return canonicalJson(AUTHORITATIVE_RACK_TOPOLOGY);
}
