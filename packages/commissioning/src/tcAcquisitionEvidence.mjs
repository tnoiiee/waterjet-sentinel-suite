// Stage 0.4C-K3 - physical TC connectivity and protocol EVIDENCE gate.
//
// This module is the ONE canonical evidence model for the future read-only physical Thermocouple
// acquisition adapter. It classifies every relevant claim, records the protocol and port decision
// position, models the process-image evidence per Node, derives a deterministic CANDIDATE process
// image where the evidence allows it, and defines the read-only acquisition contract, the
// communication-state model and the freshness semantics. It is evidence and contract ONLY.
//
// Boundary (absolute for this Stage): this module opens no socket, probes no address or port, names
// no protocol by vendor vocabulary, reads no process image, contacts no device, carries no credential,
// invents no port, no offset, no timeout and no wire-break rule. Nothing in it can activate Hardware.
// The committed Node configuration (tcNodeConfiguration.mjs) is NOT modified by this module: the six
// Nodes stay REACHABILITY_NOT_TESTED / ACQUISITION_NOT_IMPLEMENTED with PORT and PROTOCOL UNRESOLVED.
//
// Repository rule S3: Product source must not spell vendor or transport vocabulary. The field-network
// protocol named by the head-station evidence is therefore referred to by an opaque candidate
// reference; its name is recorded in the documentation (docs/evidence/tc-process-image-profile-rev1.md
// and docs/STAGE_0.4C-K3_CHECKPOINT.md), exactly as schedule.mjs addresses the evidence word column.
//
// Browser-safe: no Node built-in imports, no I/O, no clock, no random source.

import { PROTECTED_BASELINE } from './schedule.mjs';
import {
  ACQUISITION_STATUS, APPROVED_NODE_IDS, CANONICAL_TC_NODE_CONFIGURATION, EXPECTED_TOPOLOGY,
  FIELD_VERIFICATION_STATUS, HARDWARE_ACCESS_STATUS, HARDWARE_VERIFICATION_STATUS, PORT_STATUS,
  PROTOCOL_STATUS, REACHABILITY_STATUS, validateTcNodeConfiguration,
} from './tcNodeConfiguration.mjs';

export const TC_ACQUISITION_EVIDENCE_SCHEMA_VERSION = 'wjss.tc-acquisition-evidence/1';
export const TC_ACQUISITION_EVIDENCE_ID = 'TC-ACQUISITION-EVIDENCE-K3-REV1';
export const EVIDENCE_STAGE = '0.4C-K3';
export const EVIDENCE_MODE = 'EVIDENCE_AND_CONTRACT_ONLY';

/** Self-naming enum builder (every value equals its name). See quality.mjs for why it is used. */
const selfNamed = (names) => Object.freeze(Object.fromEntries(names.map((name) => [name, name])));

// ------------------------------------------------------------------------------------------ vocabulary

/** The ten approved evidence classifications. Every claim carries exactly one. */
export const EVIDENCE_CLASS = selfNamed([
  'OWNER_CONFIRMED', 'MANUFACTURER_EVIDENCE', 'DRAWING_EVIDENCE', 'REPOSITORY_CONTRACT', 'CANDIDATE_UNVERIFIED',
  'SYNTHETIC_TEST_ONLY', 'OWNER_INPUT_PENDING', 'HARDWARE_VERIFICATION_PENDING', 'CONFLICTING_EVIDENCE', 'NOT_APPLICABLE',
]);
export const EVIDENCE_CLASS_VALUES = Object.freeze(Object.values(EVIDENCE_CLASS));

/** Evidence layers, kept apart: a model-level fact never resolves an instance-level field. */
export const EVIDENCE_SCOPE = selfNamed(['MODEL_LEVEL', 'DRAWING_LEVEL', 'INSTANCE_LEVEL', 'SYNTHETIC', 'OWNER_DECISION']);

/** What kind of thing a source is. HARDWARE_OBSERVATION and ENGINEERING_TOOL_EXPORT exist as vocabulary only: none is held. */
export const SOURCE_KIND = selfNamed([
  'OWNER_STATEMENT', 'MANUFACTURER_DOCUMENT', 'DRAWING', 'REPOSITORY_RECORD', 'SYNTHETIC_TEST_RULE',
  'ENGINEERING_TOOL_EXPORT', 'HARDWARE_OBSERVATION',
]);

/** Where the bytes of a source were during this Stage. A source not in the sandbox is never hashed here. */
export const SOURCE_AVAILABILITY = selfNamed([
  'COMMITTED_IN_REPOSITORY', 'COMMITTED_TRUNCATED', 'OWNER_SOURCE_LOCATED_NOT_EXTRACTED', 'NOT_AVAILABLE_IN_SESSION',
]);

/** Verification vocabulary. Only the first three may appear in this Stage; the last two need proof. */
export const VERIFICATION_STATUS = selfNamed([
  'NOT_HARDWARE_VERIFIED', 'HARDWARE_VERIFICATION_PENDING', 'FIELD_VERIFICATION_PENDING', 'HARDWARE_VERIFIED', 'FIELD_VERIFIED',
]);

export const PROTOCOL_DECISION_STATUS = selfNamed(['PROTOCOL_UNRESOLVED', 'PROTOCOL_CANDIDATE', 'APPROVED_FOR_TEST_ADAPTER']);
export const PORT_DECISION_STATUS = selfNamed(['PORT_UNRESOLVED', 'PORT_CANDIDATE_UNVERIFIED', 'PORT_APPROVED']);
/** Statuses that no evidence Stage may ever produce. They need a later Scope Gate and Hardware proof. */
export const FORBIDDEN_DECISION_STATUSES = Object.freeze(['PROTOCOL_ACTIVATED', 'HARDWARE_CONNECTED', 'ACQUISITION_VERIFIED', 'PROTOCOL_SELECTED']);

export const BYTE_ORDER = selfNamed(['LOW_BYTE_FIRST', 'HIGH_BYTE_FIRST']);
export const WORD_ORDER = selfNamed(['LOW_WORD_FIRST', 'HIGH_WORD_FIRST', 'NOT_APPLICABLE_SINGLE_WORD']);
export const STATUS_LOCATION = selfNamed(['LOCAL_BUS_PER_CHANNEL', 'FIELD_NETWORK_INLINE', 'FIELD_NETWORK_SEPARATE_AREA', 'NOT_ON_FIELD_NETWORK']);
export const DIRECTION = selfNamed(['INPUT', 'OUTPUT', 'NONE']);
export const MODULE_ROLE = selfNamed(['HEAD_STATION', 'TC_INPUT_MODULE', 'END_MODULE']);

/** Field-level resolution statuses shown on the read-only surface. UNRESOLVED never hides a candidate. */
export const FIELD_STATUS = selfNamed([
  'BYTE_ORDER_UNRESOLVED', 'WORD_ORDER_UNRESOLVED', 'STATUS_LOCATION_UNRESOLVED', 'ENCODING_UNRESOLVED',
  'WIRE_BREAK_DECODING_BLOCKED', 'PROCESS_IMAGE_CANDIDATE_UNVERIFIED', 'PROCESS_IMAGE_DERIVATION_BLOCKED',
  'ADDRESS_UNRESOLVED', 'FIRMWARE_NOT_VERIFIED', 'FRESHNESS_THRESHOLDS_UNRESOLVED',
]);

export const CANDIDATE_STATUS = selfNamed(['CANDIDATE_UNVERIFIED']);
export const DERIVATION_STATE = selfNamed(['CANDIDATE_DERIVED', 'DERIVATION_BLOCKED']);

// ----------------------------------------------------------------------------- communication model

/**
 * Future-facing communication states. They exist as a CONTRACT model only. In this Stage no link is
 * attempted, so the only state a configured Node can honestly carry is CONFIGURED_NOT_TESTED.
 */
export const ACQUISITION_COMMUNICATION_STATE = selfNamed([
  'NOT_CONFIGURED', 'CONFIGURED_NOT_TESTED', 'CONNECTING', 'ONLINE_READ_ONLY', 'RECONNECTING',
  'COMMUNICATION_LOSS', 'PROTOCOL_MISMATCH', 'CONFIGURATION_MISMATCH', 'ACQUISITION_BLOCKED',
]);
export const ACQUISITION_COMMUNICATION_STATE_VALUES = Object.freeze(Object.values(ACQUISITION_COMMUNICATION_STATE));
/** States that imply a link was attempted. None may appear as a current Node state on the K3 surface. */
export const LIVE_COMMUNICATION_STATES = Object.freeze([
  ACQUISITION_COMMUNICATION_STATE.CONNECTING, ACQUISITION_COMMUNICATION_STATE.ONLINE_READ_ONLY,
  ACQUISITION_COMMUNICATION_STATE.RECONNECTING, ACQUISITION_COMMUNICATION_STATE.COMMUNICATION_LOSS,
]);

/** The only communication state the K3 evidence surface may show for a Node: derived from configuration validity. */
export function evidenceOnlyCommunicationState(configurationValid) {
  return configurationValid ? ACQUISITION_COMMUNICATION_STATE.CONFIGURED_NOT_TESTED : ACQUISITION_COMMUNICATION_STATE.NOT_CONFIGURED;
}

// -------------------------------------------------------------------------------- freshness model

export const FRESHNESS_STATE = selfNamed(['NEVER_RECEIVED', 'FRESH', 'AGING', 'STALE', 'FRESHNESS_UNDETERMINED']);
/** The five time stamps the contract keeps apart. None of them is interchangeable with another. */
export const SAMPLE_TIME_FIELDS = Object.freeze(['receivedTime', 'deviceSampleTime', 'decodeTime', 'publicationTime', 'lastValidValueTime']);
export const THRESHOLD_PENDING = 'OWNER_INPUT_PENDING';
/** No stale timeout is approved. Every threshold is an Owner input, never a number in this source. */
export const FRESHNESS_THRESHOLDS = Object.freeze({
  agingAfterMs: THRESHOLD_PENDING,
  staleAfterMs: THRESHOLD_PENDING,
  lastValidValueRetentionMs: THRESHOLD_PENDING,
});

/**
 * Deterministic freshness classification. Every input is explicit: the received time, the
 * evaluation time and the thresholds. Unresolved thresholds yield FRESHNESS_UNDETERMINED, never a
 * default, and an undetermined sample is never FRESH.
 */
export function classifyFreshness({ receivedTime, evaluationTime, thresholds }) {
  if (receivedTime === null || receivedTime === undefined) return Object.freeze({ state: FRESHNESS_STATE.NEVER_RECEIVED, reason: 'NO_SAMPLE_RECEIVED' });
  const aging = thresholds?.agingAfterMs;
  const stale = thresholds?.staleAfterMs;
  const positiveInt = (n) => Number.isInteger(n) && n > 0;
  if (!positiveInt(aging) || !positiveInt(stale) || aging >= stale) {
    return Object.freeze({ state: FRESHNESS_STATE.FRESHNESS_UNDETERMINED, reason: 'THRESHOLD_UNRESOLVED' });
  }
  if (!Number.isFinite(receivedTime) || !Number.isFinite(evaluationTime) || evaluationTime < receivedTime) {
    return Object.freeze({ state: FRESHNESS_STATE.FRESHNESS_UNDETERMINED, reason: 'TIME_BASIS_INVALID' });
  }
  const age = evaluationTime - receivedTime;
  if (age >= stale) return Object.freeze({ state: FRESHNESS_STATE.STALE, reason: 'AGE_AT_OR_BEYOND_STALE_THRESHOLD' });
  if (age >= aging) return Object.freeze({ state: FRESHNESS_STATE.AGING, reason: 'AGE_AT_OR_BEYOND_AGING_THRESHOLD' });
  return Object.freeze({ state: FRESHNESS_STATE.FRESH, reason: 'WITHIN_AGING_THRESHOLD' });
}

// --------------------------------------------------------------------- read-only acquisition contract

export const DECODE_STATUS = selfNamed(['NOT_DECODED', 'DECODE_RULE_UNRESOLVED', 'DECODE_FAILED', 'DECODED']);
/** Diagnostic categories. There is NO confirmed physical wire-break category: that decoding is blocked. */
export const DIAGNOSTIC_CATEGORY = selfNamed([
  'NONE', 'OUT_OF_RANGE', 'COMMUNICATION_LOSS', 'STALE', 'CHANNEL_DISABLED', 'DECODE_UNRESOLVED',
  'WIRE_BREAK_DECODING_BLOCKED', 'SYNTHETIC_WIRE_BREAK',
]);
/** Sample provenance. PHYSICAL_ACQUISITION is vocabulary for a later Stage and is refused here. */
export const SAMPLE_PROVENANCE = selfNamed(['SYNTHETIC_TEST_ONLY', 'PHYSICAL_ACQUISITION']);

export const ACQUISITION_SAMPLE_FIELDS = Object.freeze([
  'nodeId', 'cabinetId', 'readCycleId', 'moduleInstance', 'channel', 'direction',
  'receivedTime', 'deviceSampleTime', 'decodeTime', 'publicationTime',
  'rawPayloadHex', 'decodeStatus', 'currentValueValid', 'engineeringValue', 'engineeringUnit',
  'diagnosticCategory', 'communicationState', 'freshnessState', 'lastValidValue', 'lastValidValueTime', 'provenance',
]);

/** Any key with write, command or configuration meaning is refused by name, whatever its value. */
const FORBIDDEN_SAMPLE_KEY = /(write|command|output|setpoint|config|firmware|restart|reboot|activat|clean|motion|control|apply|poll|connect)/i;

const APPROVED_PAIRS = new Map(CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => [n.nodeId, n.cabinetId]));
const HEX_WORD = /^0x[0-9A-F]{4}$/;

/**
 * Validates one acquisition sample against the read-only contract. Returns issue codes only. It
 * never repairs a sample, never substitutes a Node, module or Channel, and never promotes a Last
 * Valid Value into the current value.
 */
export function validateAcquisitionSample(sample) {
  const issues = [];
  const add = (code, field) => issues.push(Object.freeze({ code, field }));
  if (!sample || typeof sample !== 'object' || Array.isArray(sample)) return summarise([Object.freeze({ code: 'SAMPLE_NOT_AN_OBJECT', field: null })]);
  for (const key of Object.keys(sample)) {
    if (FORBIDDEN_SAMPLE_KEY.test(key)) add('WRITE_OR_COMMAND_FIELD_REFUSED', key);
    else if (!ACQUISITION_SAMPLE_FIELDS.includes(key)) add('UNAPPROVED_SAMPLE_FIELD', key);
  }
  for (const key of ACQUISITION_SAMPLE_FIELDS) if (!(key in sample)) add('MISSING_SAMPLE_FIELD', key);
  if (issues.length > 0) return summarise(issues);

  // Exact identity. No fallback to another Node, Cabinet, module or Channel.
  if (!APPROVED_PAIRS.has(sample.nodeId)) add('UNKNOWN_NODE_IDENTITY', 'nodeId');
  else if (APPROVED_PAIRS.get(sample.nodeId) !== sample.cabinetId) add('NODE_CABINET_PAIR_MISMATCH', 'cabinetId');
  if (!Number.isInteger(sample.moduleInstance) || sample.moduleInstance < 1 || sample.moduleInstance > EXPECTED_TOPOLOGY.thermocoupleModuleCount) add('MODULE_INSTANCE_OUT_OF_RANGE', 'moduleInstance');
  if (!Number.isInteger(sample.channel) || sample.channel < 1 || sample.channel > EXPECTED_TOPOLOGY.channelsPerThermocoupleModule) add('CHANNEL_OUT_OF_RANGE', 'channel');
  if (sample.direction !== DIRECTION.INPUT) add('DIRECTION_NOT_INPUT', 'direction');
  if (typeof sample.readCycleId !== 'string' || sample.readCycleId === '') add('READ_CYCLE_ID_MISSING', 'readCycleId');

  // Provenance is required, and physical provenance does not exist in this Stage.
  if (!Object.values(SAMPLE_PROVENANCE).includes(sample.provenance)) add('PROVENANCE_MISSING_OR_UNKNOWN', 'provenance');
  else if (sample.provenance === SAMPLE_PROVENANCE.PHYSICAL_ACQUISITION) add('PHYSICAL_PROVENANCE_NOT_AVAILABLE_IN_STAGE', 'provenance');

  if (!Object.values(DECODE_STATUS).includes(sample.decodeStatus)) add('UNKNOWN_DECODE_STATUS', 'decodeStatus');
  if (!Object.values(DIAGNOSTIC_CATEGORY).includes(sample.diagnosticCategory)) add('UNKNOWN_DIAGNOSTIC_CATEGORY', 'diagnosticCategory');
  if (!ACQUISITION_COMMUNICATION_STATE_VALUES.includes(sample.communicationState)) add('UNKNOWN_COMMUNICATION_STATE', 'communicationState');
  if (!Object.values(FRESHNESS_STATE).includes(sample.freshnessState)) add('UNKNOWN_FRESHNESS_STATE', 'freshnessState');
  if (sample.rawPayloadHex !== null && (typeof sample.rawPayloadHex !== 'string' || !HEX_WORD.test(sample.rawPayloadHex))) add('RAW_PAYLOAD_NOT_HEX_WORD', 'rawPayloadHex');
  if (sample.engineeringUnit !== PROTECTED_BASELINE.TEMPERATURE_UNIT) add('ENGINEERING_UNIT_NOT_APPROVED', 'engineeringUnit');
  if (typeof sample.currentValueValid !== 'boolean') add('CURRENT_VALIDITY_NOT_BOOLEAN', 'currentValueValid');

  // Validity is the ONLY gate for a numeric current value.
  const hasValue = sample.engineeringValue !== null && sample.engineeringValue !== undefined;
  if (sample.currentValueValid === false && hasValue) add('NUMERIC_VALUE_ON_INVALID_SAMPLE', 'engineeringValue');
  if (sample.currentValueValid === true) {
    if (!hasValue || typeof sample.engineeringValue !== 'number' || !Number.isFinite(sample.engineeringValue)) add('VALID_SAMPLE_WITHOUT_NUMERIC_VALUE', 'engineeringValue');
    if (sample.decodeStatus !== DECODE_STATUS.DECODED) add('VALID_SAMPLE_NOT_DECODED', 'decodeStatus');
    if (sample.diagnosticCategory !== DIAGNOSTIC_CATEGORY.NONE) add('VALID_SAMPLE_WITH_DIAGNOSTIC', 'diagnosticCategory');
    if (![FRESHNESS_STATE.FRESH, FRESHNESS_STATE.AGING].includes(sample.freshnessState)) add('VALID_SAMPLE_NOT_FRESH', 'freshnessState');
    if (sample.communicationState !== ACQUISITION_COMMUNICATION_STATE.ONLINE_READ_ONLY) add('VALID_SAMPLE_WITHOUT_ONLINE_LINK', 'communicationState');
  }
  if (sample.freshnessState === FRESHNESS_STATE.STALE && sample.diagnosticCategory !== DIAGNOSTIC_CATEGORY.STALE) add('STALE_SAMPLE_WITHOUT_STALE_DIAGNOSTIC', 'diagnosticCategory');
  if (sample.diagnosticCategory === DIAGNOSTIC_CATEGORY.SYNTHETIC_WIRE_BREAK && sample.provenance !== SAMPLE_PROVENANCE.SYNTHETIC_TEST_ONLY) add('SYNTHETIC_DIAGNOSTIC_WITH_NON_SYNTHETIC_PROVENANCE', 'diagnosticCategory');

  // Last Valid Value is historical. It needs its own time and never stands in for the current value.
  if (sample.lastValidValue !== null) {
    if (typeof sample.lastValidValue !== 'number' || !Number.isFinite(sample.lastValidValue)) add('LAST_VALID_VALUE_NOT_NUMERIC', 'lastValidValue');
    if (sample.lastValidValueTime === null || sample.lastValidValueTime === undefined) add('LAST_VALID_VALUE_WITHOUT_TIME', 'lastValidValueTime');
  }
  return summarise(issues);
}

/**
 * FrontRearDelta authority: a differential may be formed only from two CURRENT, VALID sides. A stale,
 * invalid or Last-Valid side blocks the differential; the reason names the side.
 */
export function frontRearDeltaPermitted(frontSample, rearSample) {
  const reasons = [];
  for (const [side, sample] of [['FRONT', frontSample], ['REAR', rearSample]]) {
    if (!sample || sample.currentValueValid !== true) reasons.push(`${side}_INVALID`);
    else if (sample.freshnessState === FRESHNESS_STATE.STALE) reasons.push(`${side}_STALE`);
  }
  return Object.freeze({ permitted: reasons.length === 0, reasons: Object.freeze(reasons) });
}

// ------------------------------------------------------------------------------ canonical sources

const SRC = Object.freeze({
  OWNER_BRIEF: 'SRC-OWNER-BRIEF-K3',
  NODE_CONFIG: 'SRC-REPO-TC-NODE-CONFIGURATION-K2',
  PROFILE: 'SRC-REPO-PROCESS-IMAGE-PROFILE-REV1',
  SCHEDULE: 'SRC-REPO-DRAWING-SCHEDULE-REV1',
  SCENARIOS: 'SRC-REPO-SYNTHETIC-SCENARIOS-K1',
  DRAWING: 'SRC-DRAWING-SDS-CABINETS-REV1A',
  MANUAL_498: 'SRC-MANUAL-750-498',
  MANUAL_362: 'SRC-MANUAL-750-362',
  MANUAL_600: 'SRC-MANUAL-750-600',
});

/**
 * The SHA-256 values below were computed in the Stage 0.4C-0B review from bytes present at that time
 * and are recorded in docs/TC_EVIDENCE_SOURCE_REGISTER.md section 13.2. They were NOT recomputed in
 * K3 (the archive was located on the Owner-connected source but not extracted), so each is marked as
 * recorded, not re-verified.
 */
const HASH_BASIS = 'RECORDED_IN_0B_REVIEW_NOT_RECOMPUTED_IN_K3';

const CANONICAL_SOURCES = [
  { sourceId: SRC.OWNER_BRIEF, kind: SOURCE_KIND.OWNER_STATEMENT, title: 'Stage 0.4C-K3 Owner brief: approved topology, factory-default modules, Test configuration', revision: '0.4C-K3', sha256: null, hashBasis: null, availability: SOURCE_AVAILABILITY.COMMITTED_IN_REPOSITORY, defaultClass: EVIDENCE_CLASS.OWNER_CONFIRMED },
  { sourceId: SRC.NODE_CONFIG, kind: SOURCE_KIND.REPOSITORY_RECORD, title: 'Repository record: canonical six-Node TC Test configuration (Stage 0.4C-K2 module)', revision: 'TC-NODE-CONFIGURATION-TEST-REV1', sha256: null, hashBasis: null, availability: SOURCE_AVAILABILITY.COMMITTED_IN_REPOSITORY, defaultClass: EVIDENCE_CLASS.REPOSITORY_CONTRACT },
  { sourceId: SRC.PROFILE, kind: SOURCE_KIND.REPOSITORY_RECORD, title: 'Repository record: TC process-image profile REV1 (manual and drawing facts recorded in the 0B review; sections after section 3 are TRUNCATED in the committed record)', revision: 'BASELINE_REV1', sha256: null, hashBasis: null, availability: SOURCE_AVAILABILITY.COMMITTED_TRUNCATED, defaultClass: EVIDENCE_CLASS.REPOSITORY_CONTRACT },
  { sourceId: SRC.SCHEDULE, kind: SOURCE_KIND.REPOSITORY_RECORD, title: 'Repository record: TC drawing schedule REV1 (240 rows; derived model-level word ordinal column)', revision: 'BASELINE_REV1', sha256: null, hashBasis: null, availability: SOURCE_AVAILABILITY.COMMITTED_IN_REPOSITORY, defaultClass: EVIDENCE_CLASS.REPOSITORY_CONTRACT },
  { sourceId: SRC.SCENARIOS, kind: SOURCE_KIND.SYNTHETIC_TEST_RULE, title: 'Repository record: synthetic Preview scenario SIMULATED_WIRE_BREAK (Stage 0.4C-K1 module)', revision: '0.4C-K1', sha256: null, hashBasis: null, availability: SOURCE_AVAILABILITY.COMMITTED_IN_REPOSITORY, defaultClass: EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY },
  { sourceId: SRC.DRAWING, kind: SOURCE_KIND.DRAWING, title: 'Combined six-cabinet drawing (+SDSCAB1 … +SDSCAB6), REV 1A, 16 MAR 2026, DRAWING_PENDING_REVISION', revision: 'REV 1A', sha256: '08db6f2afd72f0f8a185bfcd30844e7221a332a030bbdade46dafe8c141d6b98', hashBasis: HASH_BASIS, availability: SOURCE_AVAILABILITY.OWNER_SOURCE_LOCATED_NOT_EXTRACTED, defaultClass: EVIDENCE_CLASS.DRAWING_EVIDENCE },
  { sourceId: SRC.MANUAL_498, kind: SOURCE_KIND.MANUFACTURER_DOCUMENT, title: '750-498 product manual', revision: 'v1.0.0 (Nov 2019)', sha256: 'ed66534d10b0fe95e62c0241cefa4e6057c2cbca060d97a14a6961241611fe83', hashBasis: HASH_BASIS, availability: SOURCE_AVAILABILITY.OWNER_SOURCE_LOCATED_NOT_EXTRACTED, defaultClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE },
  { sourceId: SRC.MANUAL_362, kind: SOURCE_KIND.MANUFACTURER_DOCUMENT, title: '750-362 head-station product manual', revision: 'v1.1.1 (Apr 2022)', sha256: '85e72f83eedcf60a5b4abdd4c38b88ed546e3601da4b80bd7c19eb57d6c427bb', hashBasis: HASH_BASIS, availability: SOURCE_AVAILABILITY.OWNER_SOURCE_LOCATED_NOT_EXTRACTED, defaultClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE },
  { sourceId: SRC.MANUAL_600, kind: SOURCE_KIND.MANUFACTURER_DOCUMENT, title: '750-600 end-module product manual', revision: 'v1.3.0', sha256: 'c390260c5d6041ad271fe6bb23d1f814b6cc3ed9cc6d34f673e70e4f9c604b46', hashBasis: HASH_BASIS, availability: SOURCE_AVAILABILITY.OWNER_SOURCE_LOCATED_NOT_EXTRACTED, defaultClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE },
];

// ------------------------------------------------------------------------------- canonical claims

export const CLAIM_TOPIC = selfNamed([
  'TOPOLOGY', 'FACTORY_DEFAULT', 'PROTOCOL', 'HEAD_STATION_SESSION', 'PORT', 'MODULE_CHANNEL_COUNT',
  'CHANNEL_DATA_WIDTH', 'STATUS_LOCATION', 'BYTE_ORDER', 'WORD_ORDER', 'ENCODING', 'INVALID_REPRESENTATION',
  'WIRE_BREAK', 'MODULE_ORDER', 'PROCESS_IMAGE_ORDER_RULE', 'NON_PROCESS_MODULE', 'FIRMWARE', 'CONVERSION_TIME',
  'FRESHNESS', 'REACHABILITY', 'CANDIDATE_WORD_ORDINALS', 'SYNTHETIC_SCENARIO', 'REPOSITORY_RECORD_INTEGRITY',
]);

const MANUAL_VIA_PROFILE = 'Manual fact as recorded in the committed REV1 profile; the manual itself was not re-read in K3.';

const CANONICAL_CLAIMS = [
  { claimId: 'CL-01-TOPOLOGY-OWNER', topic: CLAIM_TOPIC.TOPOLOGY, scope: EVIDENCE_SCOPE.OWNER_DECISION, evidenceClass: EVIDENCE_CLASS.OWNER_CONFIRMED, sourceIds: [SRC.OWNER_BRIEF, SRC.NODE_CONFIG], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { headStationModel: EXPECTED_TOPOLOGY.couplerModel, tcModuleModel: EXPECTED_TOPOLOGY.thermocoupleModuleModel, tcModulesPerNode: EXPECTED_TOPOLOGY.thermocoupleModuleCount, channelsPerModule: EXPECTED_TOPOLOGY.channelsPerThermocoupleModule, endModuleModel: EXPECTED_TOPOLOGY.endModuleModel, nodes: PROTECTED_BASELINE.NODE_COUNT },
    note: 'Owner-approved physical cabinet topology: 1 head station, 5 TC input modules, 1 end module per cabinet; 30 TC modules and 240 physical Channels in total. As-installed state is not verified.' },
  { claimId: 'CL-02-TOPOLOGY-DRAWING', topic: CLAIM_TOPIC.TOPOLOGY, scope: EVIDENCE_SCOPE.DRAWING_LEVEL, evidenceClass: EVIDENCE_CLASS.DRAWING_EVIDENCE, sourceIds: [SRC.DRAWING, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { headStationTag: '-A101', tcModuleTags: ['-A102.1', '-A102.2', '-A102.3', '-A102.4', '-A102.5'], endModuleTag: '-A110', cabinetTags: ['+SDSCAB1', '+SDSCAB2', '+SDSCAB3', '+SDSCAB4', '+SDSCAB5', '+SDSCAB6'] },
    note: 'As drawn (REV 1A, pending revision). Cabinet-to-Node correspondence is by ordinal (ASSUMED_ORDINAL_CORRESPONDENCE). As-installed state is not verified.' },
  { claimId: 'CL-03-FACTORY-DEFAULT', topic: CLAIM_TOPIC.FACTORY_DEFAULT, scope: EVIDENCE_SCOPE.OWNER_DECISION, evidenceClass: EVIDENCE_CLASS.OWNER_CONFIRMED, sourceIds: [SRC.OWNER_BRIEF], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { allTcModulesFactoryDefault: true, instanceConfigurationExport: 'NOT_PROVIDED' },
    note: 'Owner statement: all 30 TC modules use factory-default settings. No engineering-tool export confirms it per instance; no non-default per-Channel setting is invented.' },
  { claimId: 'CL-04-PROTOCOL-NAMED', topic: CLAIM_TOPIC.PROTOCOL, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, sourceIds: [SRC.DRAWING, SRC.MANUAL_362, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { candidateRef: 'TC-K3-PROTOCOL-CANDIDATE-01', namedBy: [EVIDENCE_CLASS.DRAWING_EVIDENCE, EVIDENCE_CLASS.MANUFACTURER_EVIDENCE], nameRecordedIn: 'repository TC process-image profile REV1, sections 1 and 3 (documentation only)' },
    note: 'The drawing device list and the head-station manual name one field-network protocol for the 750-362. It is a CANDIDATE only: applicability to the actual head stations (firmware, configuration) is unverified and the Owner has not approved it.' },
  { claimId: 'CL-05-SESSION-SEMANTICS', topic: CLAIM_TOPIC.HEAD_STATION_SESSION, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.OWNER_INPUT_PENDING, sourceIds: [SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: null,
    note: 'Connection and session semantics of the head station (connection limits, timeouts, keep-alive, read operation detail) were recorded in profile section 4, which is truncated in the committed file. No session rule is assumed.' },
  { claimId: 'CL-06-PORT', topic: CLAIM_TOPIC.PORT, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.OWNER_INPUT_PENDING, sourceIds: [SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: null,
    note: 'No committed evidence names the network port of the candidate protocol. No default and no well-known value is assumed.' },
  { claimId: 'CL-07-CHANNEL-COUNT', topic: CLAIM_TOPIC.MODULE_CHANNEL_COUNT, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, sourceIds: [SRC.MANUAL_498, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { channelsPerModule: 8 },
    note: `750-498: eight thermocouple input Channels per module. ${MANUAL_VIA_PROFILE}` },
  { claimId: 'CL-08-CHANNEL-DATA-WIDTH', topic: CLAIM_TOPIC.CHANNEL_DATA_WIDTH, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, sourceIds: [SRC.MANUAL_498, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { dataBitsPerChannel: 16, statusBitsPerChannel: 8, statusLocation: STATUS_LOCATION.LOCAL_BUS_PER_CHANNEL, direction: DIRECTION.INPUT },
    note: `Per Channel: one 16-bit data word and one 8-bit status byte on the local bus (profile section 2, manual p32). ${MANUAL_VIA_PROFILE}` },
  { claimId: 'CL-09-STATUS-ON-FIELD-NETWORK', topic: CLAIM_TOPIC.STATUS_LOCATION, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, sourceIds: [SRC.MANUAL_362, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { candidate: STATUS_LOCATION.NOT_ON_FIELD_NETWORK, basis: 'HEAD_STATION_CLASS_RULE', moduleEnumeratedInHeadStationManual: false },
    note: 'Head-station class rule (profile section 3 step 4): an analog input module exposes only its 16-bit data words on the field network; the status byte is not accessible there. The head-station manual module table does not enumerate the 750-498, so this stays a candidate.' },
  { claimId: 'CL-10-BYTE-ORDER', topic: CLAIM_TOPIC.BYTE_ORDER, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, sourceIds: [SRC.MANUAL_498, SRC.MANUAL_362, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { byteOrder: BYTE_ORDER.LOW_BYTE_FIRST },
    note: 'Model level: input byte 0 = low byte, byte 1 = high byte on the local bus (profile section 2); the head station builds its field-network words word-aligned in low-byte-first form (profile section 3 step 3). Instance behaviour is not verified.' },
  { claimId: 'CL-11-WORD-ORDER', topic: CLAIM_TOPIC.WORD_ORDER, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, sourceIds: [SRC.MANUAL_498, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { wordOrder: WORD_ORDER.NOT_APPLICABLE_SINGLE_WORD },
    note: 'A thermocouple value is one 16-bit word at model level, so no multi-word order applies. It remains a candidate until the instance value width is verified.' },
  { claimId: 'CL-12-ENCODING', topic: CLAIM_TOPIC.ENCODING, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, sourceIds: [SRC.MANUAL_498, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { numberFormat: 'TWOS_COMPLEMENT', scalePerDigit: 0.1, unit: PROTECTED_BASELINE.TEMPERATURE_UNIT, sensorTypeFactoryDefault: 'TYPE_K', referencePoints: [{ hex: '0x0000', value: 0 }, { hex: '0x03E8', value: 100 }], range: { minimum: -200, maximum: 1370 }, appliesOnlyIf: 'CL-03-FACTORY-DEFAULT' },
    note: `Factory-default Type K, two's complement, 0.1 degree per digit (profile section 2). Applies only while the factory-default statement holds for the instance. ${MANUAL_VIA_PROFILE}` },
  { claimId: 'CL-13-INVALID-REPRESENTATION', topic: CLAIM_TOPIC.INVALID_REPRESENTATION, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, sourceIds: [SRC.MANUAL_498, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { underrange: { dataHex: '0xF830', localStatusHex: '0x41' }, overrange: { dataHex: '0x3584', localStatusHex: '0x42' }, coldJunctionViolation: { dataHex: '0x8000', localStatusHex: '0x00' }, statusVisibleOnFieldNetwork: 'UNRESOLVED' },
    note: 'Model-level clamp and diagnostic representations (profile section 2, manual Table 27). The local status byte is not known to be visible on the field network (CL-09).' },
  { claimId: 'CL-14-WIRE-BREAK', topic: CLAIM_TOPIC.WIRE_BREAK, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.CONFLICTING_EVIDENCE, sourceIds: [SRC.MANUAL_498, SRC.MANUAL_362, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { wireBreak: { dataHex: '0x7FFF', localStatusHex: '0x60' }, deactivatedChannel: { dataHex: '0x7FFF', localStatusHex: null }, distinguishableByDataWordAlone: false, statusByteOnFieldNetwork: 'UNRESOLVED' },
    note: 'Wire break and a deactivated Channel share the data word 0x7FFF; they differ only in the local status byte, which the head-station class rule says is not on the field network. A data word alone therefore cannot prove wire break. Decoding is BLOCKED until instance evidence resolves it.' },
  { claimId: 'CL-15-MODULE-ORDER', topic: CLAIM_TOPIC.MODULE_ORDER, scope: EVIDENCE_SCOPE.DRAWING_LEVEL, evidenceClass: EVIDENCE_CLASS.DRAWING_EVIDENCE, sourceIds: [SRC.DRAWING, SRC.SCHEDULE, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { physicalOrderAfterHeadStation: ['-A102.1', '-A102.2', '-A102.3', '-A102.4', '-A102.5'], endModuleLast: '-A110' },
    note: 'Physical module order as drawn. The as-installed order and the engineering-tool order are not verified.' },
  { claimId: 'CL-16-PROCESS-IMAGE-ORDER-RULE', topic: CLAIM_TOPIC.PROCESS_IMAGE_ORDER_RULE, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, sourceIds: [SRC.MANUAL_362, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { rule: 'ANALOG_INPUT_MODULES_FIRST_IN_PHYSICAL_ORDER_FROM_WORD_ZERO', wordAligned: true, moduleEnumeratedInHeadStationManual: false },
    note: 'Head-station class rule (profile section 3 steps 1 to 3). The 750-498 is not enumerated in the head-station manual module table, so the rule is applied as a candidate, not as verified instance evidence.' },
  { claimId: 'CL-17-NON-PROCESS-MODULES', topic: CLAIM_TOPIC.NON_PROCESS_MODULE, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, sourceIds: [SRC.MANUAL_600, SRC.MANUAL_362, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { headStationProcessWidthBits: 0, endModuleProcessWidthBits: 0 },
    note: 'The end module terminates the internal bus and carries no process data; the head station defines the image and consumes no process width of its own.' },
  { claimId: 'CL-18-FIRMWARE', topic: CLAIM_TOPIC.FIRMWARE, scope: EVIDENCE_SCOPE.INSTANCE_LEVEL, evidenceClass: EVIDENCE_CLASS.HARDWARE_VERIFICATION_PENDING, sourceIds: [], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: null,
    note: 'Firmware and hardware revision of the six head stations and thirty modules are unknown. Manual applicability to the instances is therefore not proven.' },
  { claimId: 'CL-19-CONVERSION-TIME', topic: CLAIM_TOPIC.CONVERSION_TIME, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, sourceIds: [SRC.MANUAL_498, SRC.PROFILE], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { maxConversionTimePerChannelMs: 100, condition: 'NOTCH_FILTER_ON_FACTORY_DEFAULT' },
    note: 'Model-level conversion time. It informs a future freshness decision; it is not a stale threshold.' },
  { claimId: 'CL-20-FRESHNESS-THRESHOLDS', topic: CLAIM_TOPIC.FRESHNESS, scope: EVIDENCE_SCOPE.OWNER_DECISION, evidenceClass: EVIDENCE_CLASS.OWNER_INPUT_PENDING, sourceIds: [], verification: VERIFICATION_STATUS.FIELD_VERIFICATION_PENDING,
    value: FRESHNESS_THRESHOLDS,
    note: 'No stale timeout, aging threshold or Last-Valid retention is approved. The Preview-only 30 s threshold of the quality module is a Preview display rule, not a field value.' },
  { claimId: 'CL-21-REACHABILITY', topic: CLAIM_TOPIC.REACHABILITY, scope: EVIDENCE_SCOPE.INSTANCE_LEVEL, evidenceClass: EVIDENCE_CLASS.HARDWARE_VERIFICATION_PENDING, sourceIds: [SRC.NODE_CONFIG], verification: VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
    value: { reachabilityStatus: REACHABILITY_STATUS.NOT_TESTED, probesPerformed: 0 },
    note: 'No ping, socket, port probe or name resolution has been performed against any Test address.' },
  { claimId: 'CL-22-CANDIDATE-WORD-ORDINALS', topic: CLAIM_TOPIC.CANDIDATE_WORD_ORDINALS, scope: EVIDENCE_SCOPE.MODEL_LEVEL, evidenceClass: EVIDENCE_CLASS.REPOSITORY_CONTRACT, sourceIds: [SRC.SCHEDULE, SRC.PROFILE], verification: VERIFICATION_STATUS.NOT_HARDWARE_VERIFIED,
    value: { perNodeInputWords: 40, basis: 'DERIVED_MODEL_LEVEL' },
    note: 'The committed schedule carries a model-level candidate word ordinal per physical Channel (0 to 39 per Node). It is a repository record of the 0B derivation, not Hardware verification.' },
  { claimId: 'CL-23-SYNTHETIC-WIRE-BREAK', topic: CLAIM_TOPIC.SYNTHETIC_SCENARIO, scope: EVIDENCE_SCOPE.SYNTHETIC, evidenceClass: EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY, sourceIds: [SRC.SCENARIOS], verification: VERIFICATION_STATUS.NOT_HARDWARE_VERIFIED,
    value: { scenarioKey: 'SIMULATED_WIRE_BREAK' },
    note: 'The Preview wire-break scenario is synthetic. It is not physical decoding evidence and can never become authoritative.' },
  { claimId: 'CL-24-RECORD-TRUNCATION', topic: CLAIM_TOPIC.REPOSITORY_RECORD_INTEGRITY, scope: EVIDENCE_SCOPE.OWNER_DECISION, evidenceClass: EVIDENCE_CLASS.OWNER_INPUT_PENDING, sourceIds: [SRC.PROFILE], verification: VERIFICATION_STATUS.NOT_HARDWARE_VERIFIED,
    value: { truncatedRecords: ['TC process-image profile REV1', 'TC schedule validation REV1'] },
    note: 'Both committed records end with a truncation marker. The lost profile sections held the head-station session and port facts. Restoration requires the Owner-held manuals; nothing lost is reconstructed from memory.' },
];

// ---------------------------------------------------------------------- canonical missing evidence

const CANONICAL_MISSING_EVIDENCE = [
  { requestId: 'REQ-K3-01', category: 1, item: 'Head-station (750-362) manual section on the field-network session: port, connection limits, timeouts, read operation', blocks: ['PORT', 'PROTOCOL applicability and session semantics', 'reconnect semantics'] },
  { requestId: 'REQ-K3-02', category: 2, item: 'Manufacturer statement that the 750-498 status byte is, or is not, visible on the field network through the 750-362 (the head-station module table does not enumerate the 750-498)', blocks: ['status location', 'wire-break decoding', 'invalid-value decoding'] },
  { requestId: 'REQ-K3-03', category: 3, item: 'Actual controller or engineering-tool configuration export for each of the six cabinets (module order, Channel enable, sensor type, diagnostics, scaling)', blocks: ['factory-default confirmation per instance', 'instance process-image mapping', 'wire-break decoding'] },
  { requestId: 'REQ-K3-04', category: 6, item: 'Firmware and hardware revision of each head station and each TC module', blocks: ['manual applicability to the instances'] },
  { requestId: 'REQ-K3-05', category: 5, item: 'Actual process-image mapping as shown by the engineering tool for one cabinet (and confirmation that all six are identical)', blocks: ['candidate offset verification', 'ProcessImageOrder'] },
  { requestId: 'REQ-K3-06', category: 9, item: 'Raw input words observed under controlled, known-temperature conditions (with the reading method recorded)', blocks: ['byte order verification', 'encoding verification'] },
  { requestId: 'REQ-K3-07', category: 10, item: 'Controlled wire-break / open-circuit observation per Channel type: raw word and any status visible, against a deactivated Channel and against a communication loss', blocks: ['physical wire-break rule'] },
  { requestId: 'REQ-K3-08', category: 7, item: 'Revised (as-built) cabinet and network drawing with the cabinet-to-Node assignment confirmed', blocks: ['ASSUMED_ORDINAL_CORRESPONDENCE', 'as-installed module order'] },
  { requestId: 'REQ-K3-09', category: 11, item: 'Owner decision on freshness thresholds (aging, stale, Last-Valid retention)', blocks: ['freshness semantics', 'stale presentation'] },
  { requestId: 'REQ-K3-10', category: 2, item: 'Restoration of the truncated committed records (profile sections after section 3; validation summary after its truncation marker) from the Owner-held manuals', blocks: ['repository evidence for PORT and session semantics'] },
  { requestId: 'REQ-K3-11', category: 11, item: 'Owner approval decision on the protocol candidate TC-K3-PROTOCOL-CANDIDATE-01', blocks: ['APPROVED_FOR_TEST_ADAPTER'] },
];

// ------------------------------------------------------------------------- evidence set construction

function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.values(value).forEach(deepFreeze);
    Object.freeze(value);
  }
  return value;
}
const clone = (v) => JSON.parse(JSON.stringify(v));

/** The per-Node rack template: fixed slot order, roles and drawing tags. Identical for every Node as drawn. */
function rackTemplate() {
  const modules = [];
  modules.push({ rackSlot: 0, role: MODULE_ROLE.HEAD_STATION, model: EXPECTED_TOPOLOGY.couplerModel, drawingTag: '-A101', processModulePosition: null, direction: DIRECTION.NONE, channelCount: 0 });
  for (let i = 1; i <= EXPECTED_TOPOLOGY.thermocoupleModuleCount; i += 1) {
    modules.push({ rackSlot: i, role: MODULE_ROLE.TC_INPUT_MODULE, model: EXPECTED_TOPOLOGY.thermocoupleModuleModel, drawingTag: `-A102.${i}`, processModulePosition: i, direction: DIRECTION.INPUT, channelCount: EXPECTED_TOPOLOGY.channelsPerThermocoupleModule });
  }
  modules.push({ rackSlot: EXPECTED_TOPOLOGY.thermocoupleModuleCount + 1, role: MODULE_ROLE.END_MODULE, model: EXPECTED_TOPOLOGY.endModuleModel, drawingTag: '-A110', processModulePosition: null, direction: DIRECTION.NONE, channelCount: 0 });
  return modules;
}

/**
 * Builds an immutable evidence set. Nothing is defaulted: a missing claim stays missing and shows up
 * in validation. The canonical set below is built through this same function.
 */
export function createTcAcquisitionEvidenceSet(input) {
  const c = clone(input);
  if (!c.evidenceSetId || typeof c.evidenceSetId !== 'string') throw new Error('evidence set needs an evidenceSetId');
  return deepFreeze({
    schemaVersion: TC_ACQUISITION_EVIDENCE_SCHEMA_VERSION,
    evidenceSetId: c.evidenceSetId,
    synthetic: c.synthetic === true,
    sources: c.sources ?? [],
    claims: c.claims ?? [],
    rack: c.rack ?? rackTemplate(),
    processImageBasis: c.processImageBasis ?? {},
    protocolDecision: c.protocolDecision ?? null,
    portDecision: c.portDecision ?? null,
    wireBreakDecoding: c.wireBreakDecoding ?? null,
    missingEvidenceRequests: c.missingEvidenceRequests ?? [],
  });
}

/** Which claim resolves which process-image basis field. Derivation reads ONLY these. */
const CANONICAL_PROCESS_IMAGE_BASIS = Object.freeze({
  channelCountClaimId: 'CL-07-CHANNEL-COUNT',
  channelDataWidthClaimId: 'CL-08-CHANNEL-DATA-WIDTH',
  statusLocationClaimId: 'CL-09-STATUS-ON-FIELD-NETWORK',
  byteOrderClaimId: 'CL-10-BYTE-ORDER',
  wordOrderClaimId: 'CL-11-WORD-ORDER',
  encodingClaimId: 'CL-12-ENCODING',
  moduleOrderClaimId: 'CL-15-MODULE-ORDER',
  orderingRuleClaimId: 'CL-16-PROCESS-IMAGE-ORDER-RULE',
  nonProcessModuleClaimId: 'CL-17-NON-PROCESS-MODULES',
  wireBreakClaimId: 'CL-14-WIRE-BREAK',
});

/** THE canonical K3 evidence set. Every projection and every test derives from it. */
export const CANONICAL_TC_ACQUISITION_EVIDENCE = createTcAcquisitionEvidenceSet({
  evidenceSetId: TC_ACQUISITION_EVIDENCE_ID,
  synthetic: false,
  sources: CANONICAL_SOURCES,
  claims: CANONICAL_CLAIMS,
  processImageBasis: CANONICAL_PROCESS_IMAGE_BASIS,
  protocolDecision: {
    status: PROTOCOL_DECISION_STATUS.PROTOCOL_UNRESOLVED,
    candidate: {
      candidateRef: 'TC-K3-PROTOCOL-CANDIDATE-01',
      candidateStatus: PROTOCOL_DECISION_STATUS.PROTOCOL_CANDIDATE,
      claimId: 'CL-04-PROTOCOL-NAMED',
      sourceIds: [SRC.DRAWING, SRC.MANUAL_362],
      applicabilityToHeadStation: 'MODEL_LEVEL_ONLY',
      sessionSemantics: 'NOT_AVAILABLE_IN_SESSION',
      readOperationKnown: 'MODEL_LEVEL_ONLY',
      writeOperationRequired: false,
      ownerApproval: 'OWNER_INPUT_PENDING',
    },
    approvedForTestAdapter: false,
    activated: false,
  },
  portDecision: {
    status: PORT_DECISION_STATUS.PORT_UNRESOLVED,
    candidate: null,
    claimId: 'CL-06-PORT',
    reason: 'NO_EVIDENCE_NAMES_THE_PORT',
    ownerApproval: 'OWNER_INPUT_PENDING',
  },
  wireBreakDecoding: {
    status: FIELD_STATUS.WIRE_BREAK_DECODING_BLOCKED,
    claimId: 'CL-14-WIRE-BREAK',
    requiredEvidence: ['actual module behaviour', 'Channel configuration', 'raw and status representation on the field network', 'configured wire-break behaviour',
      'distinction from out-of-range', 'distinction from communication loss', 'distinction from disabled Channel', 'distinction from stale data'],
    provided: [],
    syntheticScenarioClaimId: 'CL-23-SYNTHETIC-WIRE-BREAK',
  },
  missingEvidenceRequests: CANONICAL_MISSING_EVIDENCE,
});

// ------------------------------------------------------------------------------------ validation

function summarise(issues) {
  return Object.freeze({ valid: issues.length === 0, issueCount: issues.length, issues: Object.freeze(issues) });
}

const INSTANCE_RESOLVING_KINDS = Object.freeze([SOURCE_KIND.ENGINEERING_TOOL_EXPORT, SOURCE_KIND.HARDWARE_OBSERVATION]);
const MANUAL_OFFSET_KEY = /^(wordOffset|byteOffset|startWord|startByte|startBit|address|ioOffset|offset|wordAddress|byteAddress)$/i;
const PORT_DEFAULT_BASES = Object.freeze(['DEFAULT', 'ASSUMED', 'WELL_KNOWN', 'GUESSED']);

/**
 * Validates an evidence set. It proves issues; it never repairs, never selects the first of two
 * conflicting claims and never derives through an unresolved field. Issues carry a code and a path.
 */
export function validateTcAcquisitionEvidence(set) {
  const issues = [];
  const add = (code, path) => issues.push(Object.freeze({ code, path }));
  if (!set || typeof set !== 'object') return summarise([Object.freeze({ code: 'EVIDENCE_SET_NOT_AN_OBJECT', path: '' })]);
  const synthetic = set.synthetic === true;

  // Sources
  const sources = new Map();
  (set.sources ?? []).forEach((s, i) => {
    const at = `sources[${i}]`;
    if (!s || typeof s.sourceId !== 'string' || s.sourceId === '' || sources.has(s.sourceId)) { add('EVIDENCE_SOURCE_DUPLICATE_OR_UNNAMED', at); return; }
    sources.set(s.sourceId, s);
    if (!Object.values(SOURCE_KIND).includes(s.kind)) add('UNKNOWN_SOURCE_KIND', at);
    if (!Object.values(SOURCE_AVAILABILITY).includes(s.availability)) add('UNKNOWN_SOURCE_AVAILABILITY', at);
    if (!EVIDENCE_CLASS_VALUES.includes(s.defaultClass)) add('UNKNOWN_EVIDENCE_CLASS', `${at}.defaultClass`);
    if (typeof s.title !== 'string' || s.title === '') add('EVIDENCE_SOURCE_UNIDENTIFIED', at);
    if (s.sha256 !== null && s.sha256 !== undefined && !/^[0-9a-f]{64}$/i.test(s.sha256)) add('EVIDENCE_SOURCE_HASH_INVALID', at);
    if (s.sha256 && !s.hashBasis) add('EVIDENCE_SOURCE_HASH_WITHOUT_BASIS', at);
    if (s.kind === SOURCE_KIND.SYNTHETIC_TEST_RULE && !synthetic && s.defaultClass !== EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY) add('SYNTHETIC_SOURCE_PRESENTED_AS_ACTUAL', at);
    if (synthetic && s.kind !== SOURCE_KIND.SYNTHETIC_TEST_RULE) add('ACTUAL_SOURCE_IN_SYNTHETIC_SET', at);
  });

  // Claims
  const claims = new Map();
  const byTopic = new Map();
  (set.claims ?? []).forEach((c, i) => {
    const at = `claims[${i}]`;
    if (!c || typeof c.claimId !== 'string' || c.claimId === '' || claims.has(c.claimId)) { add('CLAIM_DUPLICATE_OR_UNNAMED', at); return; }
    claims.set(c.claimId, c);
    if (!EVIDENCE_CLASS_VALUES.includes(c.evidenceClass)) add('UNKNOWN_EVIDENCE_CLASS', `${at}.evidenceClass`);
    if (!Object.values(EVIDENCE_SCOPE).includes(c.scope)) add('UNKNOWN_EVIDENCE_SCOPE', `${at}.scope`);
    if (!Object.values(CLAIM_TOPIC).includes(c.topic)) add('UNKNOWN_CLAIM_TOPIC', `${at}.topic`);
    if (!Object.values(VERIFICATION_STATUS).includes(c.verification)) add('UNKNOWN_VERIFICATION_STATUS', `${at}.verification`);
    const list = byTopic.get(c.topic) ?? []; list.push(c); byTopic.set(c.topic, list);
    const cited = Array.isArray(c.sourceIds) ? c.sourceIds : [];
    for (const id of cited) if (!sources.has(id)) add('MISSING_EVIDENCE_SOURCE', `${at}.sourceIds`);
    const kinds = cited.map((id) => sources.get(id)?.kind).filter(Boolean);
    const positive = [EVIDENCE_CLASS.OWNER_CONFIRMED, EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, EVIDENCE_CLASS.DRAWING_EVIDENCE, EVIDENCE_CLASS.REPOSITORY_CONTRACT, EVIDENCE_CLASS.CANDIDATE_UNVERIFIED];
    if (positive.includes(c.evidenceClass) && cited.length === 0) add('CLAIM_WITHOUT_SOURCE', at);
    if (c.evidenceClass === EVIDENCE_CLASS.MANUFACTURER_EVIDENCE && !kinds.includes(SOURCE_KIND.MANUFACTURER_DOCUMENT)) add('MANUFACTURER_CLASS_WITHOUT_MANUFACTURER_SOURCE', at);
    if (c.evidenceClass === EVIDENCE_CLASS.DRAWING_EVIDENCE && !kinds.includes(SOURCE_KIND.DRAWING)) add('DRAWING_CLASS_WITHOUT_DRAWING_SOURCE', at);
    if (c.evidenceClass === EVIDENCE_CLASS.OWNER_CONFIRMED && !kinds.includes(SOURCE_KIND.OWNER_STATEMENT)) add('OWNER_CLASS_WITHOUT_OWNER_SOURCE', at);
    // Model-level evidence must stay model-level. Only an export or an observation resolves an instance field.
    if (c.scope === EVIDENCE_SCOPE.INSTANCE_LEVEL && [EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, EVIDENCE_CLASS.DRAWING_EVIDENCE].includes(c.evidenceClass)) add('MODEL_EVIDENCE_APPLIED_AS_INSTANCE', at);
    if (c.scope === EVIDENCE_SCOPE.INSTANCE_LEVEL && c.value !== null && c.value !== undefined && !kinds.some((k) => INSTANCE_RESOLVING_KINDS.includes(k))
      && ![EVIDENCE_CLASS.HARDWARE_VERIFICATION_PENDING, EVIDENCE_CLASS.OWNER_INPUT_PENDING].includes(c.evidenceClass)) add('INSTANCE_CLAIM_WITHOUT_INSTANCE_SOURCE', at);
    // Synthetic stays synthetic.
    if (c.scope === EVIDENCE_SCOPE.SYNTHETIC && c.evidenceClass !== EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY) add('SYNTHETIC_PRESENTED_AS_ACTUAL', at);
    if (c.evidenceClass === EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY && c.scope !== EVIDENCE_SCOPE.SYNTHETIC) add('SYNTHETIC_PRESENTED_AS_ACTUAL', at);
    if (kinds.includes(SOURCE_KIND.SYNTHETIC_TEST_RULE) && c.evidenceClass !== EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY) add('SYNTHETIC_PRESENTED_AS_ACTUAL', at);
    // Verified claims need proof.
    if (c.verification === VERIFICATION_STATUS.HARDWARE_VERIFIED && !kinds.includes(SOURCE_KIND.HARDWARE_OBSERVATION)) add('HARDWARE_VERIFIED_CLAIM_WITHOUT_PROOF', at);
    if (c.verification === VERIFICATION_STATUS.FIELD_VERIFIED && !kinds.includes(SOURCE_KIND.HARDWARE_OBSERVATION)) add('FIELD_VERIFIED_CLAIM_WITHOUT_PROOF', at);
    if (c.verification === VERIFICATION_STATUS.FIELD_VERIFIED && c.scope !== EVIDENCE_SCOPE.INSTANCE_LEVEL) add('FIELD_VERIFIED_CLAIM_WITHOUT_PROOF', at);
    // Revision pinning, when a claim pins one.
    if (c.sourceRevision !== undefined) {
      const pinned = cited.map((id) => sources.get(id)?.revision);
      if (!pinned.includes(c.sourceRevision)) add('SOURCE_REVISION_MISMATCH', at);
    }
    // Manual numeric offsets are refused wherever they appear in a claim value.
    if (c.value && typeof c.value === 'object') {
      for (const key of Object.keys(c.value)) if (MANUAL_OFFSET_KEY.test(key)) add('MANUAL_NUMERIC_OFFSET_REFUSED', `${at}.value.${key}`);
    }
  });

  // Topology claims must match the approved model exactly.
  for (const c of byTopic.get(CLAIM_TOPIC.TOPOLOGY) ?? []) {
    const v = c.value ?? {};
    if (v.headStationModel !== undefined && v.headStationModel !== EXPECTED_TOPOLOGY.couplerModel) add('HEAD_STATION_MODEL_MISMATCH', c.claimId);
    if (v.tcModuleModel !== undefined && v.tcModuleModel !== EXPECTED_TOPOLOGY.thermocoupleModuleModel) add('MODULE_MODEL_MISMATCH', c.claimId);
    if (v.endModuleModel !== undefined && v.endModuleModel !== EXPECTED_TOPOLOGY.endModuleModel) add('MODULE_MODEL_MISMATCH', c.claimId);
    if (v.tcModulesPerNode !== undefined && v.tcModulesPerNode !== EXPECTED_TOPOLOGY.thermocoupleModuleCount) add('MODULE_COUNT_MISMATCH', c.claimId);
    if (v.channelsPerModule !== undefined && v.channelsPerModule !== EXPECTED_TOPOLOGY.channelsPerThermocoupleModule) add('CHANNEL_CAPACITY_MISMATCH', c.claimId);
    if (Array.isArray(v.tcModuleTags) && v.tcModuleTags.length !== EXPECTED_TOPOLOGY.thermocoupleModuleCount) add('MODULE_COUNT_MISMATCH', c.claimId);
  }
  for (const c of byTopic.get(CLAIM_TOPIC.MODULE_CHANNEL_COUNT) ?? []) {
    if (c.value?.channelsPerModule !== EXPECTED_TOPOLOGY.channelsPerThermocoupleModule) add('CHANNEL_CAPACITY_MISMATCH', c.claimId);
  }

  // Conflicts between non-synthetic positive claims on one field block that field. Nothing is picked.
  const conflicting = (topic, pick) => {
    const values = new Set();
    for (const c of byTopic.get(topic) ?? []) {
      if (c.evidenceClass === EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY || c.value === null || c.value === undefined) continue;
      const v = pick(c.value);
      if (v !== undefined) values.add(JSON.stringify(v));
    }
    return values.size > 1;
  };
  if (conflicting(CLAIM_TOPIC.CHANNEL_DATA_WIDTH, (v) => v.dataBitsPerChannel)) add('CONFLICTING_VALUE_WIDTH', CLAIM_TOPIC.CHANNEL_DATA_WIDTH);
  if (conflicting(CLAIM_TOPIC.BYTE_ORDER, (v) => v.byteOrder)) add('CONFLICTING_BYTE_ORDER', CLAIM_TOPIC.BYTE_ORDER);
  if (conflicting(CLAIM_TOPIC.WORD_ORDER, (v) => v.wordOrder)) add('CONFLICTING_WORD_ORDER', CLAIM_TOPIC.WORD_ORDER);
  if (conflicting(CLAIM_TOPIC.ENCODING, (v) => [v.numberFormat, v.scalePerDigit])) add('ENCODING_MISSING_OR_AMBIGUOUS', CLAIM_TOPIC.ENCODING);
  if (conflicting(CLAIM_TOPIC.MODULE_ORDER, (v) => v.physicalOrderAfterHeadStation)) add('CONFLICTING_MODULE_ORDER', CLAIM_TOPIC.MODULE_ORDER);

  // Process-image basis: each referenced claim must exist and be on the right topic.
  const basis = set.processImageBasis ?? {};
  const basisTopic = {
    channelCountClaimId: CLAIM_TOPIC.MODULE_CHANNEL_COUNT, channelDataWidthClaimId: CLAIM_TOPIC.CHANNEL_DATA_WIDTH, statusLocationClaimId: CLAIM_TOPIC.STATUS_LOCATION,
    byteOrderClaimId: CLAIM_TOPIC.BYTE_ORDER, wordOrderClaimId: CLAIM_TOPIC.WORD_ORDER, encodingClaimId: CLAIM_TOPIC.ENCODING, moduleOrderClaimId: CLAIM_TOPIC.MODULE_ORDER,
    orderingRuleClaimId: CLAIM_TOPIC.PROCESS_IMAGE_ORDER_RULE, nonProcessModuleClaimId: CLAIM_TOPIC.NON_PROCESS_MODULE, wireBreakClaimId: CLAIM_TOPIC.WIRE_BREAK,
  };
  for (const [field, topic] of Object.entries(basisTopic)) {
    const id = basis[field];
    if (id === undefined || id === null) { add(field === 'channelDataWidthClaimId' ? 'PROCESS_DATA_WIDTH_INCOMPLETE' : field === 'statusLocationClaimId' ? 'STATUS_INFORMATION_MISSING' : field === 'encodingClaimId' ? 'ENCODING_MISSING_OR_AMBIGUOUS' : 'PROCESS_IMAGE_BASIS_INCOMPLETE', `processImageBasis.${field}`); continue; }
    const c = claims.get(id);
    if (!c) add('PROCESS_IMAGE_BASIS_CLAIM_MISSING', `processImageBasis.${field}`);
    else if (c.topic !== topic) add('PROCESS_IMAGE_BASIS_TOPIC_MISMATCH', `processImageBasis.${field}`);
  }
  const widthClaim = claims.get(basis.channelDataWidthClaimId);
  if (widthClaim && (!Number.isInteger(widthClaim.value?.dataBitsPerChannel) || widthClaim.value.dataBitsPerChannel <= 0)) add('PROCESS_DATA_WIDTH_INCOMPLETE', basis.channelDataWidthClaimId);
  if (widthClaim && !Number.isInteger(widthClaim.value?.statusBitsPerChannel)) add('STATUS_INFORMATION_MISSING', basis.channelDataWidthClaimId);
  const encodingClaim = claims.get(basis.encodingClaimId);
  if (encodingClaim && (!encodingClaim.value?.numberFormat || typeof encodingClaim.value?.scalePerDigit !== 'number')) add('ENCODING_MISSING_OR_AMBIGUOUS', basis.encodingClaimId);

  // Rack: fixed order, no manual offsets, exact counts.
  const rack = Array.isArray(set.rack) ? set.rack : [];
  const tc = rack.filter((m) => m.role === MODULE_ROLE.TC_INPUT_MODULE);
  if (tc.length !== EXPECTED_TOPOLOGY.thermocoupleModuleCount) add('MODULE_COUNT_MISMATCH', 'rack');
  if (rack.filter((m) => m.role === MODULE_ROLE.HEAD_STATION).length !== 1) add('HEAD_STATION_COUNT_MISMATCH', 'rack');
  if (rack.filter((m) => m.role === MODULE_ROLE.END_MODULE).length !== 1) add('END_MODULE_COUNT_MISMATCH', 'rack');
  rack.forEach((m, i) => {
    const at = `rack[${i}]`;
    for (const key of Object.keys(m)) if (MANUAL_OFFSET_KEY.test(key)) add('MANUAL_NUMERIC_OFFSET_REFUSED', `${at}.${key}`);
    if (m.rackSlot !== i) add('RACK_SLOT_ORDER_BROKEN', at);
    if (m.role === MODULE_ROLE.TC_INPUT_MODULE) {
      if (m.model !== EXPECTED_TOPOLOGY.thermocoupleModuleModel) add('MODULE_MODEL_MISMATCH', at);
      if (m.channelCount !== EXPECTED_TOPOLOGY.channelsPerThermocoupleModule) add('CHANNEL_CAPACITY_MISMATCH', at);
      if (m.direction !== DIRECTION.INPUT) add('DIRECTION_NOT_INPUT', at);
    } else {
      if (m.role === MODULE_ROLE.HEAD_STATION && m.model !== EXPECTED_TOPOLOGY.couplerModel) add('HEAD_STATION_MODEL_MISMATCH', at);
      if (m.role === MODULE_ROLE.END_MODULE && m.model !== EXPECTED_TOPOLOGY.endModuleModel) add('MODULE_MODEL_MISMATCH', at);
      if (m.channelCount !== 0 || m.processModulePosition !== null) add('NON_PROCESS_MODULE_WITH_PROCESS_DATA', at);
    }
  });
  tc.forEach((m, i) => { if (m.processModulePosition !== i + 1) add('PROCESS_MODULE_POSITION_NOT_SEQUENTIAL', `rack.tc[${i}]`); });

  // Protocol decision honesty.
  const pd = set.protocolDecision;
  if (!pd) add('PROTOCOL_DECISION_MISSING', 'protocolDecision');
  else {
    if (FORBIDDEN_DECISION_STATUSES.includes(pd.status) || pd.activated === true) add('PROTOCOL_ACTIVATION_CLAIM_REFUSED', 'protocolDecision.status');
    else if (pd.status !== PROTOCOL_DECISION_STATUS.PROTOCOL_UNRESOLVED) add('PROTOCOL_SELECTED_WITHOUT_EVIDENCE', 'protocolDecision.status');
    if (pd.approvedForTestAdapter === true) add('PROTOCOL_SELECTED_WITHOUT_EVIDENCE', 'protocolDecision.approvedForTestAdapter');
    if (pd.candidate) {
      const cl = claims.get(pd.candidate.claimId);
      const ids = Array.isArray(pd.candidate.sourceIds) ? pd.candidate.sourceIds : [];
      const kinds = ids.map((id) => sources.get(id)?.kind);
      if (!cl || cl.topic !== CLAIM_TOPIC.PROTOCOL || ids.length === 0 || !ids.every((id) => sources.has(id))) add('PROTOCOL_SELECTED_WITHOUT_EVIDENCE', 'protocolDecision.candidate');
      else if (!kinds.some((k) => [SOURCE_KIND.DRAWING, SOURCE_KIND.MANUFACTURER_DOCUMENT, SOURCE_KIND.OWNER_STATEMENT, SOURCE_KIND.ENGINEERING_TOOL_EXPORT].includes(k))) add('PROTOCOL_SELECTED_WITHOUT_EVIDENCE', 'protocolDecision.candidate.sourceIds');
      if (kinds.includes(SOURCE_KIND.SYNTHETIC_TEST_RULE)) add('SYNTHETIC_PRESENTED_AS_ACTUAL', 'protocolDecision.candidate.sourceIds');
      if (pd.candidate.candidateStatus !== PROTOCOL_DECISION_STATUS.PROTOCOL_CANDIDATE) add('PROTOCOL_SELECTED_WITHOUT_EVIDENCE', 'protocolDecision.candidate.candidateStatus');
      if (pd.candidate.writeOperationRequired !== false) add('WRITE_OPERATION_REFUSED', 'protocolDecision.candidate.writeOperationRequired');
      if (pd.candidate.ownerApproval !== 'OWNER_INPUT_PENDING') add('PROTOCOL_SELECTED_WITHOUT_EVIDENCE', 'protocolDecision.candidate.ownerApproval');
    }
  }

  // Port decision honesty: no value without evidence, never a default, never zero or blank.
  const po = set.portDecision;
  if (!po) add('PORT_DECISION_MISSING', 'portDecision');
  else {
    if (po.status === PORT_DECISION_STATUS.PORT_APPROVED || FORBIDDEN_DECISION_STATUSES.includes(po.status)) add('PORT_SELECTED_WITHOUT_EVIDENCE', 'portDecision.status');
    if (po.candidate === null || po.candidate === undefined) {
      if (po.status !== PORT_DECISION_STATUS.PORT_UNRESOLVED) add('PORT_SELECTED_WITHOUT_EVIDENCE', 'portDecision.status');
    } else {
      const cand = po.candidate;
      if (po.status !== PORT_DECISION_STATUS.PORT_CANDIDATE_UNVERIFIED) add('PORT_SELECTED_WITHOUT_EVIDENCE', 'portDecision.status');
      if (!Number.isInteger(cand.value) || cand.value <= 0 || cand.value > 65535) add('PORT_VALUE_INVALID', 'portDecision.candidate.value');
      const ids = Array.isArray(cand.sourceIds) ? cand.sourceIds : [];
      if (ids.length === 0 || !ids.every((id) => sources.has(id))) add('PORT_SELECTED_WITHOUT_EVIDENCE', 'portDecision.candidate.sourceIds');
      if (PORT_DEFAULT_BASES.includes(String(cand.basis).toUpperCase())) add('PORT_DEFAULT_ASSUMED', 'portDecision.candidate.basis');
      if (cand.ownerApproval !== 'OWNER_INPUT_PENDING') add('PORT_SELECTED_WITHOUT_EVIDENCE', 'portDecision.candidate.ownerApproval');
    }
  }

  // Wire-break decoding: a rule needs complete evidence; otherwise it stays BLOCKED.
  const wb = set.wireBreakDecoding;
  if (!wb) add('WIRE_BREAK_POSITION_MISSING', 'wireBreakDecoding');
  else if (wb.status !== FIELD_STATUS.WIRE_BREAK_DECODING_BLOCKED) {
    const required = Array.isArray(wb.requiredEvidence) ? wb.requiredEvidence : [];
    const provided = Array.isArray(wb.provided) ? wb.provided : [];
    const cl = claims.get(wb.claimId);
    const kinds = (cl?.sourceIds ?? []).map((id) => sources.get(id)?.kind);
    if (required.length === 0 || !required.every((r) => provided.includes(r)) || !kinds.includes(SOURCE_KIND.HARDWARE_OBSERVATION) || cl?.evidenceClass === EVIDENCE_CLASS.CONFLICTING_EVIDENCE) {
      add('WIRE_BREAK_EVIDENCE_INCOMPLETE', 'wireBreakDecoding.status');
    }
  }
  const wbClaim = claims.get(set.processImageBasis?.wireBreakClaimId);
  if (wbClaim?.value?.distinguishableByDataWordAlone === true && !(wbClaim.sourceIds ?? []).some((id) => sources.get(id)?.kind === SOURCE_KIND.HARDWARE_OBSERVATION)) add('WIRE_BREAK_EVIDENCE_INCOMPLETE', wbClaim.claimId);

  // Missing-evidence requests must be exact.
  (set.missingEvidenceRequests ?? []).forEach((r, i) => {
    if (!r || typeof r.requestId !== 'string' || typeof r.item !== 'string' || r.item === '' || !Array.isArray(r.blocks) || r.blocks.length === 0) add('MISSING_EVIDENCE_REQUEST_INCOMPLETE', `missingEvidenceRequests[${i}]`);
  });

  // Derivation issues (overlap) are proven by the derivation itself.
  const derived = deriveCandidateProcessImageUnchecked(set, claims);
  if (derived.overlap) add('OVERLAPPING_DERIVED_PLACEMENT', 'derivation');
  return summarise(issues);
}

// ------------------------------------------------------------------------------------- derivation

const DERIVATION_READY_CLASSES = Object.freeze([
  EVIDENCE_CLASS.OWNER_CONFIRMED, EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, EVIDENCE_CLASS.DRAWING_EVIDENCE,
  EVIDENCE_CLASS.REPOSITORY_CONTRACT, EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY,
]);

function deriveCandidateProcessImageUnchecked(set, claims) {
  const basis = set.processImageBasis ?? {};
  const blockers = [];
  const need = (field, label) => {
    const c = claims.get(basis[field]);
    if (!c) { blockers.push(`${label}: no claim`); return null; }
    if (c.evidenceClass === EVIDENCE_CLASS.CONFLICTING_EVIDENCE) { blockers.push(`${label}: CONFLICTING_EVIDENCE`); return null; }
    if (!DERIVATION_READY_CLASSES.includes(c.evidenceClass) || c.value === null || c.value === undefined) { blockers.push(`${label}: ${c.evidenceClass}`); return null; }
    return c;
  };
  const count = need('channelCountClaimId', 'Channel count');
  const width = need('channelDataWidthClaimId', 'Channel data width');
  const order = need('moduleOrderClaimId', 'module order');
  const rule = need('orderingRuleClaimId', 'process-image ordering rule');
  const nonProcess = need('nonProcessModuleClaimId', 'non-process modules');
  const byteOrder = claims.get(basis.byteOrderClaimId);
  const wordOrder = claims.get(basis.wordOrderClaimId);
  if (byteOrder?.evidenceClass === EVIDENCE_CLASS.CONFLICTING_EVIDENCE) blockers.push('byte order: CONFLICTING_EVIDENCE');
  if (wordOrder?.evidenceClass === EVIDENCE_CLASS.CONFLICTING_EVIDENCE) blockers.push('word order: CONFLICTING_EVIDENCE');

  // Conflicts on the width or order topics block derivation outright: nothing is picked.
  const sameTopic = (topic, pick) => {
    const seen = new Set();
    for (const c of claims.values()) {
      if (c.topic !== topic || c.value === null || c.value === undefined || c.evidenceClass === EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY) continue;
      const v = pick(c.value); if (v !== undefined) seen.add(JSON.stringify(v));
    }
    return seen.size > 1;
  };
  if (sameTopic(CLAIM_TOPIC.CHANNEL_DATA_WIDTH, (v) => v.dataBitsPerChannel)) blockers.push('Channel data width: conflicting claims');
  if (sameTopic(CLAIM_TOPIC.MODULE_ORDER, (v) => v.physicalOrderAfterHeadStation)) blockers.push('module order: conflicting claims');

  const dataBits = width?.value?.dataBitsPerChannel;
  const channels = count?.value?.channelsPerModule;
  if (width && (!Number.isInteger(dataBits) || dataBits <= 0 || dataBits % 16 !== 0)) blockers.push('Channel data width: not a positive whole number of 16-bit words');
  if (count && (!Number.isInteger(channels) || channels <= 0)) blockers.push('Channel count: not a positive integer');
  if (nonProcess && (nonProcess.value.headStationProcessWidthBits !== 0 || nonProcess.value.endModuleProcessWidthBits !== 0)) blockers.push('non-process modules: a non-zero process width was claimed');
  const rack = Array.isArray(set.rack) ? set.rack : [];
  const tcModules = rack.filter((m) => m.role === MODULE_ROLE.TC_INPUT_MODULE);
  if (order && Array.isArray(order.value.physicalOrderAfterHeadStation)) {
    const tags = tcModules.map((m) => m.drawingTag);
    if (JSON.stringify(tags) !== JSON.stringify(order.value.physicalOrderAfterHeadStation)) blockers.push('module order: the rack order and the drawing order differ');
  }
  if (rule && rule.value.rule !== 'ANALOG_INPUT_MODULES_FIRST_IN_PHYSICAL_ORDER_FROM_WORD_ZERO') blockers.push('process-image ordering rule: the engine has no arithmetic for this rule');

  if (blockers.length > 0) {
    return Object.freeze({
      state: DERIVATION_STATE.DERIVATION_BLOCKED, candidateStatus: null, addressStatus: FIELD_STATUS.ADDRESS_UNRESOLVED,
      blockers: Object.freeze(blockers), modules: Object.freeze([]), nonProcessModules: Object.freeze(rack.filter((m) => m.role !== MODULE_ROLE.TC_INPUT_MODULE).map(nonProcessView)),
      perNodeInputWords: null, perNodeInputBits: null, overlap: false, basisClaimIds: Object.freeze([]),
    });
  }

  // Deterministic placement: fixed rack order, word-aligned, every number derived, nothing typed.
  const wordsPerModule = (channels * dataBits) / 16;
  let cursor = 0;
  const placed = [];
  const used = new Set();
  let overlap = false;
  tcModules.forEach((m, i) => {
    const startWord = cursor;
    const channelWords = [];
    for (let ch = 1; ch <= channels; ch += 1) {
      const word = startWord + (ch - 1) * (dataBits / 16);
      if (used.has(word)) overlap = true;
      used.add(word);
      channelWords.push(Object.freeze({ channel: ch, candidateWordOrdinal: word }));
    }
    placed.push(Object.freeze({
      rackSlot: m.rackSlot, processModulePosition: m.processModulePosition, drawingTag: m.drawingTag, model: m.model, role: m.role,
      direction: DIRECTION.INPUT, processImageOrder: i + 1, processImageOrderBasis: Object.freeze([basis.moduleOrderClaimId, basis.orderingRuleClaimId]),
      channelCount: channels, dataBitsPerChannel: dataBits, moduleProcessWidthBits: channels * dataBits,
      startWord, endWord: startWord + wordsPerModule - 1, words: wordsPerModule, channels: Object.freeze(channelWords),
      candidateStatus: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED, verification: VERIFICATION_STATUS.NOT_HARDWARE_VERIFIED,
    }));
    cursor += wordsPerModule;
  });
  return Object.freeze({
    state: DERIVATION_STATE.CANDIDATE_DERIVED, candidateStatus: CANDIDATE_STATUS.CANDIDATE_UNVERIFIED,
    // The ADDRESS stays unresolved for activation purposes: a candidate is not a verified address.
    addressStatus: FIELD_STATUS.ADDRESS_UNRESOLVED, blockers: Object.freeze([]),
    modules: Object.freeze(placed), nonProcessModules: Object.freeze(rack.filter((m) => m.role !== MODULE_ROLE.TC_INPUT_MODULE).map(nonProcessView)),
    perNodeInputWords: cursor, perNodeInputBits: cursor * 16, overlap,
    basisClaimIds: Object.freeze([basis.channelCountClaimId, basis.channelDataWidthClaimId, basis.moduleOrderClaimId, basis.orderingRuleClaimId, basis.nonProcessModuleClaimId]),
  });
}

function nonProcessView(m) {
  return Object.freeze({ rackSlot: m.rackSlot, role: m.role, model: m.model, drawingTag: m.drawingTag, processWidthBits: 0, processModulePosition: null, addressStatus: EVIDENCE_CLASS.NOT_APPLICABLE });
}

/**
 * Derives the CANDIDATE process image of one Node (identical for all six as drawn). Every offset is
 * computed from the evidence basis; none is typed. The result is CANDIDATE_UNVERIFIED at best and
 * can never activate anything: it carries no connection, no protocol and no device identity.
 */
export function deriveCandidateProcessImage(set = CANONICAL_TC_ACQUISITION_EVIDENCE) {
  const claims = new Map((set.claims ?? []).filter((c) => c && typeof c.claimId === 'string').map((c) => [c.claimId, c]));
  const derived = deriveCandidateProcessImageUnchecked(set, claims);
  return Object.freeze({
    ...derived,
    synthetic: set.synthetic === true,
    // A synthetic derivation exercises the arithmetic only. It is never authoritative.
    authoritative: false,
    activationPossible: false,
    verificationBlockers: Object.freeze([
      'instance configuration export not provided (REQ-K3-03)',
      'process-image mapping of the engineering tool not provided (REQ-K3-05)',
      'the 750-498 is not enumerated in the head-station manual module table (REQ-K3-02)',
      'firmware and hardware revision unknown (REQ-K3-04)',
    ]),
  });
}

/**
 * Compares the derived candidate with the schedule's recorded word ordinal per physical Channel.
 * Agreement is a CONSISTENCY check between two repository records, never Hardware verification.
 */
export function compareCandidateWithSchedule(candidate, rows) {
  if (candidate.state !== DERIVATION_STATE.CANDIDATE_DERIVED) return Object.freeze({ compared: 0, mismatches: 0, consistent: false, reason: 'NO_CANDIDATE' });
  const byModule = new Map(candidate.modules.map((m) => [m.processModulePosition, m]));
  let compared = 0;
  let mismatches = 0;
  for (const row of rows) {
    const position = Number.parseInt(String(row.moduleInstance ?? '').split('.').pop(), 10);
    const m = byModule.get(position);
    const ch = m?.channels.find((c) => c.channel === row.channel);
    if (!ch) continue;
    compared += 1;
    if (ch.candidateWordOrdinal !== row.wordOrdinal) mismatches += 1;
  }
  return Object.freeze({ compared, mismatches, consistent: compared > 0 && mismatches === 0, reason: compared === 0 ? 'NO_ROWS' : null });
}

// --------------------------------------------------------------------------------------- the view

const claimById = (set, id) => (set.claims ?? []).find((c) => c.claimId === id) ?? null;

function fieldStatus(set, claimId, unresolvedStatus, pick) {
  const c = claimById(set, claimId);
  const candidate = c && c.value !== null && c.value !== undefined && c.evidenceClass !== EVIDENCE_CLASS.CONFLICTING_EVIDENCE ? pick(c.value) : null;
  return Object.freeze({
    status: unresolvedStatus,
    candidate: candidate ?? null,
    candidateClass: c?.evidenceClass ?? null,
    candidateScope: c?.scope ?? null,
    claimId: c?.claimId ?? null,
    verification: c?.verification ?? VERIFICATION_STATUS.HARDWARE_VERIFICATION_PENDING,
  });
}

/**
 * The read-only K3 projection. It merges the committed K2 Node identities (never restated) with the
 * K3 evidence position. Every status it shows is a fact of this Stage: nothing is reachable, nothing
 * is acquired, nothing is verified, and no Node carries a live communication state.
 */
export function buildTcAcquisitionEvidenceView(set = CANONICAL_TC_ACQUISITION_EVIDENCE) {
  const validation = validateTcAcquisitionEvidence(set);
  const configuration = validateTcNodeConfiguration(CANONICAL_TC_NODE_CONFIGURATION);
  const candidate = deriveCandidateProcessImage(set);
  const communicationState = evidenceOnlyCommunicationState(configuration.valid);
  const processImageStatus = candidate.state === DERIVATION_STATE.CANDIDATE_DERIVED ? FIELD_STATUS.PROCESS_IMAGE_CANDIDATE_UNVERIFIED : FIELD_STATUS.PROCESS_IMAGE_DERIVATION_BLOCKED;
  const byteOrder = fieldStatus(set, set.processImageBasis?.byteOrderClaimId, FIELD_STATUS.BYTE_ORDER_UNRESOLVED, (v) => v.byteOrder);
  const wordOrder = fieldStatus(set, set.processImageBasis?.wordOrderClaimId, FIELD_STATUS.WORD_ORDER_UNRESOLVED, (v) => v.wordOrder);
  const statusLocation = fieldStatus(set, set.processImageBasis?.statusLocationClaimId, FIELD_STATUS.STATUS_LOCATION_UNRESOLVED, (v) => v.candidate);
  const encoding = fieldStatus(set, set.processImageBasis?.encodingClaimId, FIELD_STATUS.ENCODING_UNRESOLVED, (v) => `${v.numberFormat} ${v.scalePerDigit} ${v.unit}/digit ${v.sensorTypeFactoryDefault}`);
  const wireBreak = set.wireBreakDecoding ?? { status: FIELD_STATUS.WIRE_BREAK_DECODING_BLOCKED };
  const byClass = Object.fromEntries(EVIDENCE_CLASS_VALUES.map((k) => [k, 0]));
  for (const c of set.claims ?? []) if (byClass[c.evidenceClass] !== undefined) byClass[c.evidenceClass] += 1;
  const pd = set.protocolDecision;
  const po = set.portDecision;

  return Object.freeze({
    schemaVersion: TC_ACQUISITION_EVIDENCE_SCHEMA_VERSION,
    evidenceSetId: set.evidenceSetId,
    evidenceStage: EVIDENCE_STAGE,
    evidenceMode: EVIDENCE_MODE,
    synthetic: set.synthetic === true,
    validation: Object.freeze({ valid: validation.valid, issueCount: validation.issueCount, issueCodes: Object.freeze([...new Set(validation.issues.map((i) => i.code))].sort()) }),
    configurationValidation: Object.freeze({ valid: configuration.valid, issueCount: configuration.issueCount }),
    decisions: Object.freeze({
      protocol: Object.freeze({
        status: pd?.status ?? PROTOCOL_DECISION_STATUS.PROTOCOL_UNRESOLVED,
        candidateRef: pd?.candidate?.candidateRef ?? null,
        candidateStatus: pd?.candidate?.candidateStatus ?? null,
        candidateNamedBy: Object.freeze([...(claimById(set, pd?.candidate?.claimId)?.value?.namedBy ?? [])]),
        candidateSourceIds: Object.freeze([...(pd?.candidate?.sourceIds ?? [])]),
        applicabilityToHeadStation: pd?.candidate?.applicabilityToHeadStation ?? null,
        sessionSemantics: pd?.candidate?.sessionSemantics ?? null,
        writeOperationRequired: pd?.candidate?.writeOperationRequired ?? null,
        ownerApproval: pd?.candidate?.ownerApproval ?? 'OWNER_INPUT_PENDING',
        approvedForTestAdapter: pd?.approvedForTestAdapter === true,
        activated: pd?.activated === true,
      }),
      port: Object.freeze({
        status: po?.status ?? PORT_DECISION_STATUS.PORT_UNRESOLVED,
        candidate: po?.candidate ?? null,
        reason: po?.reason ?? null,
        ownerApproval: po?.ownerApproval ?? 'OWNER_INPUT_PENDING',
      }),
    }),
    statuses: Object.freeze({
      processImage: processImageStatus,
      address: candidate.addressStatus,
      byteOrder, wordOrder, statusLocation, encoding,
      wireBreakDecoding: Object.freeze({ status: wireBreak.status, requiredEvidence: Object.freeze([...(wireBreak.requiredEvidence ?? [])]), provided: Object.freeze([...(wireBreak.provided ?? [])]), syntheticScenarioClaimId: wireBreak.syntheticScenarioClaimId ?? null }),
      firmware: FIELD_STATUS.FIRMWARE_NOT_VERIFIED,
      freshnessThresholds: FIELD_STATUS.FRESHNESS_THRESHOLDS_UNRESOLVED,
      hardwareVerification: HARDWARE_VERIFICATION_STATUS.PENDING,
      fieldVerification: FIELD_VERIFICATION_STATUS.PENDING,
      hardwareAccess: HARDWARE_ACCESS_STATUS.NOT_AUTHORIZED,
    }),
    processImageCandidate: Object.freeze({
      state: candidate.state,
      candidateStatus: candidate.candidateStatus,
      addressStatus: candidate.addressStatus,
      perNodeInputWords: candidate.perNodeInputWords,
      perNodeInputBits: candidate.perNodeInputBits,
      overlap: candidate.overlap,
      authoritative: candidate.authoritative,
      activationPossible: candidate.activationPossible,
      basisClaimIds: candidate.basisClaimIds,
      blockers: candidate.blockers,
      verificationBlockers: candidate.verificationBlockers,
      modules: candidate.modules,
      nonProcessModules: candidate.nonProcessModules,
    }),
    nodes: Object.freeze(CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => Object.freeze({
      ordinal: n.ordinal,
      nodeId: n.nodeId,
      cabinetId: n.cabinetId,
      ipAddress: n.ipAddress,
      addressEnvironment: n.addressEnvironment,
      portStatus: PORT_STATUS,
      protocolStatus: PROTOCOL_STATUS,
      reachabilityStatus: REACHABILITY_STATUS.NOT_TESTED,
      acquisitionStatus: ACQUISITION_STATUS.NOT_IMPLEMENTED,
      communicationState,
      processImageStatus,
      byteOrderStatus: byteOrder.status,
      wordOrderStatus: wordOrder.status,
      wireBreakDecodingStatus: wireBreak.status,
      firmwareStatus: FIELD_STATUS.FIRMWARE_NOT_VERIFIED,
      hardwareVerificationStatus: HARDWARE_VERIFICATION_STATUS.PENDING,
      fieldVerificationStatus: FIELD_VERIFICATION_STATUS.PENDING,
      hardwareAccessStatus: HARDWARE_ACCESS_STATUS.NOT_AUTHORIZED,
    }))),
    sources: Object.freeze((set.sources ?? []).map((s) => Object.freeze({
      sourceId: s.sourceId, kind: s.kind, title: s.title, revision: s.revision, availability: s.availability,
      hashRecorded: Boolean(s.sha256), hashBasis: s.hashBasis ?? null, defaultClass: s.defaultClass,
    }))),
    claims: Object.freeze((set.claims ?? []).map((c) => Object.freeze({
      claimId: c.claimId, topic: c.topic, scope: c.scope, evidenceClass: c.evidenceClass, verification: c.verification,
      sourceIds: Object.freeze([...(c.sourceIds ?? [])]), note: c.note,
    }))),
    claimSummary: Object.freeze({ total: (set.claims ?? []).length, byClass: Object.freeze(byClass) }),
    missingEvidenceRequests: Object.freeze((set.missingEvidenceRequests ?? []).map((r) => Object.freeze({ ...r, blocks: Object.freeze([...r.blocks]), status: 'OWNER_INPUT_PENDING' }))),
    communicationStateModel: Object.freeze({
      states: ACQUISITION_COMMUNICATION_STATE_VALUES,
      liveStates: LIVE_COMMUNICATION_STATES,
      currentNodeState: communicationState,
      note: 'Contract model only. No link is attempted in this Stage; no Node is ONLINE and no COMMUNICATION_LOSS is observed.',
    }),
    freshness: Object.freeze({ thresholds: FRESHNESS_THRESHOLDS, timeFields: SAMPLE_TIME_FIELDS, states: Object.freeze(Object.values(FRESHNESS_STATE)) }),
    totals: Object.freeze({
      nodes: APPROVED_NODE_IDS.length,
      cabinets: APPROVED_NODE_IDS.length,
      thermocoupleModules: PROTECTED_BASELINE.MODULE_COUNT,
      physicalTcChannels: PROTECTED_BASELINE.PHYSICAL_CHANNEL_COUNT,
      candidateInputWordsPerNode: candidate.perNodeInputWords,
      evidenceSources: (set.sources ?? []).length,
      claims: (set.claims ?? []).length,
      missingEvidenceRequests: (set.missingEvidenceRequests ?? []).length,
      reachabilityTested: 0,
      acquisitionVerified: 0,
      hardwareVerified: 0,
      fieldVerified: 0,
      protocolsApproved: 0,
      portsApproved: 0,
    }),
    authority: Object.freeze({
      statements: Object.freeze(['SIMULATION / PREVIEW', 'EVIDENCE AND CONTRACT FOUNDATION ONLY', 'NO NETWORK PROBE', 'NO SOCKET',
        'NO PHYSICAL ACQUISITION', 'NO DEVICE WRITE', 'NO TEST_HARDWARE', 'NO PRODUCTION', 'FIELD VERIFICATION PENDING']),
      hardwareAccess: false,
      networkProbe: false,
      socketOpened: false,
      deviceWrite: false,
      activation: false,
      productionMode: false,
      fieldVerified: false,
      protocolActivated: false,
      acquisitionVerified: false,
    }),
  });
}

/** Deterministic text of an evidence set: object keys sorted recursively, array order kept. */
export function canonicalTcAcquisitionEvidenceText(set) {
  const sorted = (value) => {
    if (Array.isArray(value)) return value.map(sorted);
    if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map((k) => [k, sorted(value[k])]));
    return value;
  };
  return JSON.stringify(sorted(set));
}

/** Deterministic fingerprint. The caller supplies the hex digest so this module stays dependency-free. */
export function tcAcquisitionEvidenceFingerprint(set, digestHex) {
  return digestHex(canonicalTcAcquisitionEvidenceText(set));
}
