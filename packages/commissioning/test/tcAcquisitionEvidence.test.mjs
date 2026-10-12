// Stage 0.4C-K3 - evidence gate contract of tcAcquisitionEvidence.mjs.
//
// Groups follow the Stage brief: A baseline preservation, B evidence classification, C protocol and
// port honesty, D process-image evidence and candidate derivation, E wire-break boundary, F read-only
// acquisition contract (communication state, freshness, FrontRearDelta), H boundary and regression
// (static source scan). Every test is pure in-memory: no socket, no probe, no clock, no device.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import * as index from '../src/index.mjs';
import {
  ACQUISITION_COMMUNICATION_STATE, ACQUISITION_COMMUNICATION_STATE_VALUES, ACQUISITION_SAMPLE_FIELDS,
  CANONICAL_TC_ACQUISITION_EVIDENCE, CLAIM_TOPIC, DECODE_STATUS, DIAGNOSTIC_CATEGORY, DERIVATION_STATE,
  EVIDENCE_CLASS, EVIDENCE_CLASS_VALUES, EVIDENCE_SCOPE, FIELD_STATUS, FORBIDDEN_DECISION_STATUSES,
  FRESHNESS_STATE, FRESHNESS_THRESHOLDS, LIVE_COMMUNICATION_STATES, PORT_DECISION_STATUS,
  PROTOCOL_DECISION_STATUS, SAMPLE_PROVENANCE, SAMPLE_TIME_FIELDS, SOURCE_AVAILABILITY, SOURCE_KIND,
  TC_ACQUISITION_EVIDENCE_SCHEMA_VERSION, VERIFICATION_STATUS,
  SOURCE_COMPLETENESS, SOURCE_BASIS, PROTOCOL_CANDIDATE_STATUS, OWNER_EVIDENCE_PENDING, CONNECTED_STORAGE_NOT_AUTHORIZED,
  CONTENT_NOT_RETRIEVED, NOT_PROVIDED, RECORD_TO_RECORD_CONSISTENCY_ONLY, RECORD_TO_RECORD_CONSISTENCY_MEANING,
  buildTcAcquisitionEvidenceView, canonicalTcAcquisitionEvidenceText, classifyFreshness,
  recordToRecordConsistency, createTcAcquisitionEvidenceSet, deriveCandidateProcessImage,
  evidenceOnlyCommunicationState, frontRearDeltaPermitted, tcAcquisitionEvidenceFingerprint,
  validateAcquisitionSample, validateTcAcquisitionEvidence,
} from '../src/tcAcquisitionEvidence.mjs';
import {
  CANONICAL_TC_NODE_CONFIGURATION, PORT_STATUS, PROTOCOL_STATUS, REACHABILITY_STATUS, ACQUISITION_STATUS,
  buildTcNodeConfigurationView, validateTcNodeConfiguration,
} from '../src/tcNodeConfiguration.mjs';
import { PROTECTED_BASELINE, parseSchedule } from '../src/schedule.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC = join(HERE, '..', 'src', 'tcAcquisitionEvidence.mjs');
const SOURCE = readFileSync(SRC, 'utf8');
const SCHEDULE = parseSchedule(readFileSync(join(HERE, '..', '..', '..', 'docs', 'evidence', 'tc-drawing-schedule-rev1.csv'), 'utf8'));
const CANON = CANONICAL_TC_ACQUISITION_EVIDENCE;
const clone = (v) => JSON.parse(JSON.stringify(v));
const codes = (result) => [...new Set(result.issues.map((i) => i.code))];
/** Rebuilds a mutated copy of the canonical set through the same constructor. */
const variant = (mutate) => { const c = clone(CANON); mutate(c); return createTcAcquisitionEvidenceSet(c); };
const claim = (set, id) => set.claims.find((c) => c.claimId === id);
/**
 * Mutator producing a synthetic, fully readable copy of the canonical set. It exists only to prove that
 * the derivation arithmetic is reachable for a synthetic set with complete sources; it is never the
 * canonical position and it is never authoritative.
 */
const completeSynthetic = (c) => {
  c.synthetic = true;
  c.evidenceSetId = 'SYN-COMPLETE';
  for (const src of c.sources) { src.readableInCurrentReview = true; src.evidenceStatus = 'SYNTHETIC_TEST_ONLY'; }
  for (const cl of c.claims) cl.sourceCompleteness = SOURCE_COMPLETENESS.SOURCE_COMPLETE_IN_CURRENT_REVIEW;
  c.protocolDecision.status = PROTOCOL_DECISION_STATUS.PROTOCOL_CANDIDATE;
  c.protocolDecision.evidenceStatus = 'SYNTHETIC_TEST_ONLY';
};
const syntheticComplete = () => variant(completeSynthetic);

// --------------------------------------------------------------------------------------- A baseline

test('A: the canonical Node configuration is untouched: six Nodes, exact identities, unresolved port and protocol', () => {
  const view = buildTcNodeConfigurationView(CANONICAL_TC_NODE_CONFIGURATION);
  assert.equal(validateTcNodeConfiguration(CANONICAL_TC_NODE_CONFIGURATION).valid, true);
  // The six literals live ONLY in tcNodeConfiguration.mjs (the scoped S2 exception); this test derives
  // the expected identities from the approved documentation-range prefix and never spells them.
  const prefix = '192.0.2.';
  assert.deepEqual(view.nodes.map((n) => [n.nodeId, n.cabinetId, n.ipAddress]),
    [1, 2, 3, 4, 5, 6].map((i) => [`TC-NODE-0${i}`, `CAB${i}`, `${prefix}${10 + i}`]));
  for (const n of view.nodes) {
    assert.equal(n.portStatus, PORT_STATUS);
    assert.equal(n.protocolStatus, PROTOCOL_STATUS);
    assert.equal(n.reachabilityStatus, REACHABILITY_STATUS.NOT_TESTED);
    assert.equal(n.acquisitionStatus, ACQUISITION_STATUS.NOT_IMPLEMENTED);
  }
  assert.equal(PROTECTED_BASELINE.MODULE_COUNT, 30);
  assert.equal(PROTECTED_BASELINE.PHYSICAL_CHANNEL_COUNT, 240);
});

test('A: the canonical evidence set validates with zero issues and is deeply frozen', () => {
  const result = validateTcAcquisitionEvidence(CANON);
  assert.deepEqual(result, { valid: true, issueCount: 0, issues: [] });
  assert.equal(CANON.schemaVersion, TC_ACQUISITION_EVIDENCE_SCHEMA_VERSION);
  assert.equal(CANON.synthetic, false);
  assert.equal(Object.isFrozen(CANON.claims[0]), true);
  assert.throws(() => { CANON.portDecision.candidate = { value: 1 }; }, TypeError);
});

test('A: the canonical text and fingerprint are deterministic', () => {
  const digest = (t) => createHash('sha256').update(t, 'utf8').digest('hex');
  assert.equal(canonicalTcAcquisitionEvidenceText(CANON), canonicalTcAcquisitionEvidenceText(clone(CANON)));
  assert.equal(tcAcquisitionEvidenceFingerprint(CANON, digest), tcAcquisitionEvidenceFingerprint(CANON, digest));
  assert.match(tcAcquisitionEvidenceFingerprint(CANON, digest), /^[0-9a-f]{64}$/);
});

// ----------------------------------------------------------------------------- B evidence classes

test('B: exactly the ten approved evidence classes exist and every canonical claim uses one of them', () => {
  assert.deepEqual(EVIDENCE_CLASS_VALUES, ['OWNER_CONFIRMED', 'MANUFACTURER_EVIDENCE', 'DRAWING_EVIDENCE', 'REPOSITORY_CONTRACT',
    'CANDIDATE_UNVERIFIED', 'SYNTHETIC_TEST_ONLY', 'OWNER_INPUT_PENDING', 'HARDWARE_VERIFICATION_PENDING', 'CONFLICTING_EVIDENCE', 'NOT_APPLICABLE']);
  for (const c of CANON.claims) {
    assert.ok(EVIDENCE_CLASS_VALUES.includes(c.evidenceClass), c.claimId);
    assert.ok(Object.values(EVIDENCE_SCOPE).includes(c.scope), c.claimId);
    assert.ok(Object.values(VERIFICATION_STATUS).includes(c.verification), c.claimId);
    assert.notEqual(c.verification, VERIFICATION_STATUS.HARDWARE_VERIFIED, `${c.claimId} cannot be hardware verified in this Stage`);
    assert.notEqual(c.verification, VERIFICATION_STATUS.FIELD_VERIFIED, `${c.claimId} cannot be field verified in this Stage`);
  }
  assert.equal(new Set(CANON.claims.map((c) => c.claimId)).size, CANON.claims.length, 'claim ids are unique');
});

test('B: the canonical classification of the key claims is exactly as the evidence allows', () => {
  const expect = {
    'CL-01-TOPOLOGY-OWNER': [EVIDENCE_CLASS.OWNER_CONFIRMED, EVIDENCE_SCOPE.OWNER_DECISION],
    'CL-02-TOPOLOGY-DRAWING': [EVIDENCE_CLASS.DRAWING_EVIDENCE, EVIDENCE_SCOPE.DRAWING_LEVEL],
    'CL-03-FACTORY-DEFAULT': [EVIDENCE_CLASS.OWNER_CONFIRMED, EVIDENCE_SCOPE.OWNER_DECISION],
    'CL-04-PROTOCOL': [EVIDENCE_CLASS.OWNER_INPUT_PENDING, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-06-PORT': [EVIDENCE_CLASS.OWNER_INPUT_PENDING, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-07-CHANNEL-COUNT': [EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-08-CHANNEL-DATA-WIDTH': [EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-09-STATUS-ON-FIELD-NETWORK': [EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-10-BYTE-ORDER': [EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-11-WORD-ORDER': [EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-12-ENCODING': [EVIDENCE_CLASS.MANUFACTURER_EVIDENCE, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-14-WIRE-BREAK': [EVIDENCE_CLASS.CONFLICTING_EVIDENCE, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-15-MODULE-ORDER': [EVIDENCE_CLASS.DRAWING_EVIDENCE, EVIDENCE_SCOPE.DRAWING_LEVEL],
    'CL-16-PROCESS-IMAGE-ORDER-RULE': [EVIDENCE_CLASS.CANDIDATE_UNVERIFIED, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-18-FIRMWARE': [EVIDENCE_CLASS.HARDWARE_VERIFICATION_PENDING, EVIDENCE_SCOPE.INSTANCE_LEVEL],
    'CL-20-FRESHNESS-THRESHOLDS': [EVIDENCE_CLASS.OWNER_INPUT_PENDING, EVIDENCE_SCOPE.OWNER_DECISION],
    'CL-21-REACHABILITY': [EVIDENCE_CLASS.HARDWARE_VERIFICATION_PENDING, EVIDENCE_SCOPE.INSTANCE_LEVEL],
    'CL-22-CANDIDATE-WORD-ORDINALS': [EVIDENCE_CLASS.REPOSITORY_CONTRACT, EVIDENCE_SCOPE.MODEL_LEVEL],
    'CL-23-SYNTHETIC-WIRE-BREAK': [EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY, EVIDENCE_SCOPE.SYNTHETIC],
    'CL-24-RECORD-TRUNCATION': [EVIDENCE_CLASS.OWNER_INPUT_PENDING, EVIDENCE_SCOPE.OWNER_DECISION],
  };
  for (const [id, [cls, scope]] of Object.entries(expect)) {
    const c = claim(CANON, id);
    assert.ok(c, id);
    assert.equal(c.evidenceClass, cls, id);
    assert.equal(c.scope, scope, id);
  }
  assert.equal(claim(CANON, 'CL-03-FACTORY-DEFAULT').value.instanceConfigurationExport, 'NOT_PROVIDED');
  assert.equal(claim(CANON, 'CL-21-REACHABILITY').value.probesPerformed, 0);
});

test('B: every claim carries its source basis and completeness; nothing transcribed from an unreadable document is complete in this review', () => {
  for (const c of CANON.claims) {
    assert.ok(Object.values(SOURCE_BASIS).includes(c.sourceBasis), c.claimId);
    assert.ok(Object.values(SOURCE_COMPLETENESS).includes(c.sourceCompleteness), c.claimId);
    if ([SOURCE_BASIS.MANUFACTURER_DOCUMENT_TRANSCRIPTION_0B, SOURCE_BASIS.DRAWING_TRANSCRIPTION_0B, SOURCE_BASIS.REVIEWER_PARAPHRASE_0B].includes(c.sourceBasis)) {
      assert.notEqual(c.sourceCompleteness, SOURCE_COMPLETENESS.SOURCE_COMPLETE_IN_CURRENT_REVIEW, c.claimId);
    }
  }
  const complete = CANON.claims.filter((c) => c.sourceCompleteness === SOURCE_COMPLETENESS.SOURCE_COMPLETE_IN_CURRENT_REVIEW).map((c) => c.claimId).sort();
  assert.deepEqual(complete, ['CL-01-TOPOLOGY-OWNER', 'CL-03-FACTORY-DEFAULT', 'CL-21-REACHABILITY', 'CL-22-CANDIDATE-WORD-ORDINALS', 'CL-23-SYNTHETIC-WIRE-BREAK']);
  assert.equal(claim(CANON, 'CL-04-PROTOCOL').sourceCompleteness, SOURCE_COMPLETENESS.SOURCE_COMPLETENESS_INSUFFICIENT);
  assert.equal(claim(CANON, 'CL-14-WIRE-BREAK').sourceCompleteness, SOURCE_COMPLETENESS.SOURCE_COMPLETENESS_INSUFFICIENT);
  assert.equal(claim(CANON, 'CL-07-CHANNEL-COUNT').sourceCompleteness, SOURCE_COMPLETENESS.SOURCE_NOT_READABLE_IN_CURRENT_REVIEW);
  assert.equal(claim(CANON, 'CL-22-CANDIDATE-WORD-ORDINALS').value.classification, RECORD_TO_RECORD_CONSISTENCY_ONLY);
  // Overstating completeness for a transcription is refused; a missing position is refused.
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-07-CHANNEL-COUNT').sourceCompleteness = SOURCE_COMPLETENESS.SOURCE_COMPLETE_IN_CURRENT_REVIEW; }))).includes('SOURCE_COMPLETENESS_OVERSTATED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { delete claim(c, 'CL-07-CHANNEL-COUNT').sourceCompleteness; }))).includes('UNKNOWN_SOURCE_COMPLETENESS'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-07-CHANNEL-COUNT').sourceBasis = 'HEARSAY'; }))).includes('UNKNOWN_SOURCE_BASIS'));
});

test('B: no instance-level field is resolved by model-level or drawing evidence', () => {
  for (const c of CANON.claims.filter((x) => x.scope === EVIDENCE_SCOPE.INSTANCE_LEVEL)) {
    assert.ok([EVIDENCE_CLASS.HARDWARE_VERIFICATION_PENDING, EVIDENCE_CLASS.OWNER_INPUT_PENDING].includes(c.evidenceClass), c.claimId);
  }
  const bad = variant((c) => { claim(c, 'CL-10-BYTE-ORDER').scope = EVIDENCE_SCOPE.INSTANCE_LEVEL; });
  assert.ok(codes(validateTcAcquisitionEvidence(bad)).includes('MODEL_EVIDENCE_APPLIED_AS_INSTANCE'));
});

test('B: sources are identified, hashes are recorded with their basis, and no source was present as bytes in this session', () => {
  assert.equal(CANON.sources.length, 9);
  for (const s of CANON.sources) {
    assert.ok(Object.values(SOURCE_KIND).includes(s.kind), s.sourceId);
    assert.ok(Object.values(SOURCE_AVAILABILITY).includes(s.availability), s.sourceId);
    if (s.sha256) { assert.match(s.sha256, /^[0-9a-f]{64}$/); assert.equal(s.hashBasis, 'RECORDED_IN_0B_REVIEW_NOT_RECOMPUTED_IN_K3'); }
    assert.notEqual(s.kind, SOURCE_KIND.HARDWARE_OBSERVATION, 'no hardware observation exists');
    assert.notEqual(s.kind, SOURCE_KIND.ENGINEERING_TOOL_EXPORT, 'no engineering-tool export exists');
  }
  const manuals = CANON.sources.filter((s) => s.kind === SOURCE_KIND.MANUFACTURER_DOCUMENT);
  assert.equal(manuals.length, 3);
  const drawing = CANON.sources.filter((s) => s.kind === SOURCE_KIND.DRAWING);
  assert.equal(drawing.length, 1);
  // Owner ruling (PR #21): the Owner documents were not in hand. No "located" claim, no connector metadata.
  for (const m of [...manuals, ...drawing]) {
    assert.equal(m.availability, SOURCE_AVAILABILITY.NOT_AVAILABLE_IN_SESSION, m.sourceId);
    assert.equal(m.storageAccess, CONNECTED_STORAGE_NOT_AUTHORIZED, m.sourceId);
    assert.equal(m.contentRetrieval, CONTENT_NOT_RETRIEVED, m.sourceId);
    assert.equal(m.evidenceStatus, OWNER_EVIDENCE_PENDING, m.sourceId);
    assert.equal(m.readableInCurrentReview, false, m.sourceId);
  }
  assert.equal(Object.hasOwn(SOURCE_AVAILABILITY, 'OWNER_SOURCE_LOCATED_NOT_EXTRACTED'), false);
  const profile = CANON.sources.find((s) => s.sourceId === 'SRC-REPO-PROCESS-IMAGE-PROFILE-REV1');
  assert.equal(profile.availability, SOURCE_AVAILABILITY.COMMITTED_TRUNCATED);
  assert.equal(profile.readableInCurrentReview, 'TRUNCATED');
  // No connector-derived identity leaks into the model: no archive file name, byte size, digest or storage identifier.
  const text = JSON.stringify(CANON) + SOURCE;
  for (const token of ['WJSS_TC_EVIDENCE_BASELINE_REV1', '17345849', '17 345 849', '2807f158ced9a984dea288e5ab30e2d8', '.zip', 'drive.google', 'Drive', 'fileId']) {
    assert.equal(text.includes(token), false, `forbidden token ${token}`);
  }
});

test('B: the validator proves missing sources, unknown classes, model mismatches and revision mismatches; it repairs nothing', () => {
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-07-CHANNEL-COUNT').sourceIds = ['SRC-DOES-NOT-EXIST']; }))).includes('MISSING_EVIDENCE_SOURCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-07-CHANNEL-COUNT').evidenceClass = 'VERIFIED'; }))).includes('UNKNOWN_EVIDENCE_CLASS'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-01-TOPOLOGY-OWNER').value.headStationModel = '750-352'; }))).includes('HEAD_STATION_MODEL_MISMATCH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-01-TOPOLOGY-OWNER').value.tcModuleModel = '750-469'; }))).includes('MODULE_MODEL_MISMATCH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-01-TOPOLOGY-OWNER').value.tcModulesPerNode = 4; }))).includes('MODULE_COUNT_MISMATCH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-07-CHANNEL-COUNT').value.channelsPerModule = 4; }))).includes('CHANNEL_CAPACITY_MISMATCH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-07-CHANNEL-COUNT').sourceRevision = 'v9.9.9'; }))).includes('SOURCE_REVISION_MISMATCH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.rack[1].model = '750-469'; }))).includes('MODULE_MODEL_MISMATCH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.rack.splice(5, 1); }))).includes('MODULE_COUNT_MISMATCH'));
});

test('B: synthetic evidence can never be presented as actual, and unproven verified claims are refused', () => {
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-23-SYNTHETIC-WIRE-BREAK').evidenceClass = EVIDENCE_CLASS.MANUFACTURER_EVIDENCE; }))).includes('SYNTHETIC_PRESENTED_AS_ACTUAL'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-10-BYTE-ORDER').sourceIds.push('SRC-REPO-SYNTHETIC-SCENARIOS-K1'); }))).includes('SYNTHETIC_PRESENTED_AS_ACTUAL'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-10-BYTE-ORDER').verification = VERIFICATION_STATUS.HARDWARE_VERIFIED; }))).includes('HARDWARE_VERIFIED_CLAIM_WITHOUT_PROOF'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-18-FIRMWARE').verification = VERIFICATION_STATUS.FIELD_VERIFIED; }))).includes('FIELD_VERIFIED_CLAIM_WITHOUT_PROOF'));
  const synthetic = createTcAcquisitionEvidenceSet({ evidenceSetId: 'SYN', synthetic: true, sources: [CANON.sources.find((s) => s.kind === SOURCE_KIND.MANUFACTURER_DOCUMENT)] });
  assert.ok(codes(validateTcAcquisitionEvidence(synthetic)).includes('ACTUAL_SOURCE_IN_SYNTHETIC_SET'));
});

// ---------------------------------------------------------------------- C protocol and port honesty

test('C: the protocol decision is PROTOCOL_UNRESOLVED, Owner evidence pending, candidate WITHDRAWN, source completeness insufficient; nothing is inferred', () => {
  const pd = CANON.protocolDecision;
  assert.equal(pd.status, PROTOCOL_DECISION_STATUS.PROTOCOL_UNRESOLVED);
  assert.equal(pd.evidenceStatus, OWNER_EVIDENCE_PENDING);
  assert.equal(pd.candidateStatus, PROTOCOL_CANDIDATE_STATUS.WITHDRAWN);
  assert.equal(pd.sourceCompleteness, SOURCE_COMPLETENESS.SOURCE_COMPLETENESS_INSUFFICIENT);
  assert.equal(pd.candidate, null);
  assert.equal(pd.approvedForTestAdapter, false);
  assert.equal(pd.adapterAuthorized, false);
  assert.equal(pd.acquisitionAuthorized, false);
  assert.equal(pd.activated, false);
  assert.equal(pd.ownerApproval, 'OWNER_INPUT_PENDING');
  const cl = claim(CANON, 'CL-04-PROTOCOL');
  assert.equal(cl.evidenceClass, EVIDENCE_CLASS.OWNER_INPUT_PENDING);
  assert.equal(cl.value.candidateStatus, PROTOCOL_CANDIDATE_STATUS.WITHDRAWN);
  // The seven required statements of the non-authoritative evidence note.
  const note = cl.value.evidenceNote;
  assert.equal(note.originalDocumentReadableInCurrentReview, false);
  assert.equal(note.committedProfileSectionTruncated, true);
  assert.equal(note.asInstalledApplicabilityVerified, false);
  assert.equal(note.firmwareVerified, false);
  assert.equal(note.configurationVerified, false);
  assert.equal(note.portResolved, false);
  assert.equal(note.protocolCandidateRetained, false);
  assert.equal(note.adapterAuthorized, false);
  assert.equal(note.acquisitionAuthorized, false);
  // No named candidate reference survives anywhere in the model or its source.
  const text = JSON.stringify(CANON) + SOURCE;
  assert.equal(text.includes('TC-K3-PROTOCOL-CANDIDATE'), false);
  assert.equal(text.includes('CL-04-PROTOCOL-NAMED'), false);
  assert.doesNotMatch(text, /\b502\b/);
  assert.deepEqual(FORBIDDEN_DECISION_STATUSES, ['PROTOCOL_ACTIVATED', 'HARDWARE_CONNECTED', 'ACQUISITION_VERIFIED', 'PROTOCOL_SELECTED']);
});

test('C: a protocol cannot be selected, approved, activated or retained as a candidate without complete readable evidence and Owner approval', () => {
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.status = PROTOCOL_DECISION_STATUS.APPROVED_FOR_TEST_ADAPTER; }))).includes('PROTOCOL_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.approvedForTestAdapter = true; }))).includes('PROTOCOL_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.adapterAuthorized = true; }))).includes('PROTOCOL_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.acquisitionAuthorized = true; }))).includes('PROTOCOL_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.activated = true; }))).includes('PROTOCOL_ACTIVATION_CLAIM_REFUSED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.status = 'PROTOCOL_ACTIVATED'; }))).includes('PROTOCOL_ACTIVATION_CLAIM_REFUSED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.evidenceStatus = 'OWNER_EVIDENCE_RECEIVED'; }))).includes('PROTOCOL_EVIDENCE_STATUS_OVERSTATED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision.candidateStatus = PROTOCOL_CANDIDATE_STATUS.PROTOCOL_CANDIDATE; }))).includes('PROTOCOL_CANDIDATE_STATUS_INCONSISTENT'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.protocolDecision = null; }))).includes('PROTOCOL_DECISION_MISSING'));
  // Re-introducing a candidate on the Stage 0B transcription (documents not readable, profile truncated) is refused.
  const reintroduced = (extra = () => {}) => variant((c) => {
    c.protocolDecision.candidateStatus = PROTOCOL_CANDIDATE_STATUS.PROTOCOL_CANDIDATE;
    c.protocolDecision.candidate = { candidateRef: 'X', candidateStatus: PROTOCOL_CANDIDATE_STATUS.PROTOCOL_CANDIDATE, claimId: 'CL-04-PROTOCOL', sourceIds: ['SRC-MANUAL-750-362'], writeOperationRequired: false, ownerApproval: 'OWNER_INPUT_PENDING' };
    extra(c);
  });
  assert.ok(codes(validateTcAcquisitionEvidence(reintroduced())).includes('PROTOCOL_CANDIDATE_WITHOUT_READABLE_SOURCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(reintroduced((c) => { c.protocolDecision.sourceCompleteness = SOURCE_COMPLETENESS.SOURCE_COMPLETE_IN_CURRENT_REVIEW; }))).includes('PROTOCOL_CANDIDATE_WITHOUT_READABLE_SOURCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(reintroduced((c) => { c.protocolDecision.candidate.sourceIds = []; }))).includes('PROTOCOL_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(reintroduced((c) => { c.protocolDecision.candidate.sourceIds = ['SRC-REPO-SYNTHETIC-SCENARIOS-K1']; }))).includes('SYNTHETIC_PRESENTED_AS_ACTUAL'));
  assert.ok(codes(validateTcAcquisitionEvidence(reintroduced((c) => { c.protocolDecision.candidate.ownerApproval = 'APPROVED'; }))).includes('PROTOCOL_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(reintroduced((c) => { c.protocolDecision.candidate.writeOperationRequired = true; }))).includes('WRITE_OPERATION_REFUSED'));
  const v = buildTcAcquisitionEvidenceView();
  assert.equal(v.decisions.protocol.candidateRetained, false);
  assert.equal(v.decisions.protocol.candidateRef, null);
  assert.equal(v.decisions.protocol.candidateStatus, PROTOCOL_CANDIDATE_STATUS.WITHDRAWN);
  assert.equal(v.decisions.protocol.evidenceStatus, OWNER_EVIDENCE_PENDING);
  assert.equal(v.decisions.protocol.sourceCompleteness, SOURCE_COMPLETENESS.SOURCE_COMPLETENESS_INSUFFICIENT);
  assert.equal(v.decisions.protocol.evidenceNote.protocolCandidateRetained, false);
  assert.ok(v.missingEvidenceRequests.some((r) => r.blocks.includes('PROTOCOL_UNRESOLVED')));
});

test('C: the port is PORT_UNRESOLVED with no candidate; zero, blank, default or unsourced values are refused', () => {
  assert.equal(CANON.portDecision.status, PORT_DECISION_STATUS.PORT_UNRESOLVED);
  assert.equal(CANON.portDecision.candidate, null);
  assert.equal(CANON.portDecision.reason, 'NO_EVIDENCE_NAMES_THE_PORT');
  const cand = (value, extra = {}) => variant((c) => { c.portDecision.status = PORT_DECISION_STATUS.PORT_CANDIDATE_UNVERIFIED; c.portDecision.candidate = { value, sourceIds: ['SRC-MANUAL-750-362'], basis: 'DOCUMENT_STATEMENT', ownerApproval: 'OWNER_INPUT_PENDING', ...extra }; });
  assert.ok(codes(validateTcAcquisitionEvidence(cand(0))).includes('PORT_VALUE_INVALID'));
  assert.ok(codes(validateTcAcquisitionEvidence(cand(''))).includes('PORT_VALUE_INVALID'));
  assert.ok(codes(validateTcAcquisitionEvidence(cand(70000))).includes('PORT_VALUE_INVALID'));
  assert.ok(codes(validateTcAcquisitionEvidence(cand(1024, { sourceIds: [] }))).includes('PORT_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(cand(1024, { basis: 'DEFAULT' }))).includes('PORT_DEFAULT_ASSUMED'));
  assert.ok(codes(validateTcAcquisitionEvidence(cand(1024, { basis: 'well_known' }))).includes('PORT_DEFAULT_ASSUMED'));
  assert.ok(codes(validateTcAcquisitionEvidence(cand(1024, { ownerApproval: 'APPROVED' }))).includes('PORT_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.portDecision.status = PORT_DECISION_STATUS.PORT_APPROVED; }))).includes('PORT_SELECTED_WITHOUT_EVIDENCE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.portDecision.status = PORT_DECISION_STATUS.PORT_CANDIDATE_UNVERIFIED; }))).includes('PORT_SELECTED_WITHOUT_EVIDENCE'));
  // A properly sourced, non-default candidate is accepted as PORT_CANDIDATE_UNVERIFIED and nothing more.
  assert.equal(validateTcAcquisitionEvidence(cand(1024)).valid, true);
});

test('C: the view shows PORT_UNRESOLVED and PROTOCOL_UNRESOLVED on every Node and zero approvals in the totals', () => {
  const view = buildTcAcquisitionEvidenceView();
  for (const n of view.nodes) {
    assert.equal(n.portStatus, 'PORT_UNRESOLVED');
    assert.equal(n.protocolStatus, 'PROTOCOL_UNRESOLVED');
  }
  assert.equal(view.decisions.port.candidate, null);
  assert.equal(view.decisions.protocol.approvedForTestAdapter, false);
  assert.equal(view.totals.protocolsApproved, 0);
  assert.equal(view.totals.portsApproved, 0);
});

// ------------------------------------------------------------------------- D process-image evidence

test('D: the canonical derivation is DERIVATION_BLOCKED: no module, word, start, end, offset or range is produced', () => {
  const d = deriveCandidateProcessImage();
  assert.equal(d.state, DERIVATION_STATE.DERIVATION_BLOCKED);
  assert.equal(d.candidateStatus, null);
  assert.equal(d.addressStatus, FIELD_STATUS.ADDRESS_UNRESOLVED);
  assert.equal(d.perNodeInputWords, null);
  assert.equal(d.perNodeInputBits, null);
  assert.deepEqual(d.modules, []);
  assert.equal(d.authoritative, false);
  assert.equal(d.activationPossible, false);
  assert.ok(d.blockers.includes('protocol: PROTOCOL_UNRESOLVED'));
  assert.ok(d.blockers.includes(`protocol evidence: ${OWNER_EVIDENCE_PENDING}`));
  assert.ok(d.blockers.some((b) => b.includes(OWNER_EVIDENCE_PENDING) && b.includes('SRC-MANUAL-750-498')));
  assert.ok(d.blockers.some((b) => b.includes(SOURCE_COMPLETENESS.SOURCE_NOT_READABLE_IN_CURRENT_REVIEW)));
  const text = JSON.stringify(d);
  assert.doesNotMatch(text, /startWord|endWord|candidateWordOrdinal|"words"/);
  assert.deepEqual(deriveCandidateProcessImage(), d, 'the blocked result is deterministic');
  // No numeric offset field exists in the evidence set itself.
  for (const m of CANON.rack) for (const key of Object.keys(m)) assert.doesNotMatch(key, /offset|address|startWord/i);
  // Each Owner-named blocker blocks on its own, starting from the synthetic complete set.
  const blockedBy = (mutate) => deriveCandidateProcessImage(variant((c) => { completeSynthetic(c); mutate(c); })).state;
  assert.equal(blockedBy(() => {}), DERIVATION_STATE.CANDIDATE_DERIVED, 'the synthetic complete set derives');
  assert.equal(blockedBy((c) => { claim(c, 'CL-07-CHANNEL-COUNT').sourceCompleteness = SOURCE_COMPLETENESS.SOURCE_COMPLETENESS_INSUFFICIENT; }), DERIVATION_STATE.DERIVATION_BLOCKED);
  assert.equal(blockedBy((c) => { claim(c, 'CL-15-MODULE-ORDER').sourceCompleteness = SOURCE_COMPLETENESS.SOURCE_NOT_READABLE_IN_CURRENT_REVIEW; }), DERIVATION_STATE.DERIVATION_BLOCKED);
  assert.equal(blockedBy((c) => { claim(c, 'CL-16-PROCESS-IMAGE-ORDER-RULE').evidenceClass = EVIDENCE_CLASS.OWNER_INPUT_PENDING; }), DERIVATION_STATE.DERIVATION_BLOCKED);
  assert.equal(blockedBy((c) => { c.sources.find((x) => x.sourceId === 'SRC-MANUAL-750-362').evidenceStatus = OWNER_EVIDENCE_PENDING; }), DERIVATION_STATE.DERIVATION_BLOCKED);
  assert.equal(blockedBy((c) => { c.protocolDecision.status = PROTOCOL_DECISION_STATUS.PROTOCOL_UNRESOLVED; }), DERIVATION_STATE.DERIVATION_BLOCKED);
  assert.equal(blockedBy((c) => { c.protocolDecision.evidenceStatus = OWNER_EVIDENCE_PENDING; }), DERIVATION_STATE.DERIVATION_BLOCKED);
});

test('D: the derivation arithmetic stays reachable only for a synthetic complete set, and is never authoritative', () => {
  const d = deriveCandidateProcessImage(syntheticComplete());
  assert.equal(d.state, DERIVATION_STATE.CANDIDATE_DERIVED);
  assert.equal(d.synthetic, true);
  assert.equal(d.candidateStatus, 'CANDIDATE_UNVERIFIED');
  assert.equal(d.addressStatus, FIELD_STATUS.ADDRESS_UNRESOLVED);
  assert.equal(d.authoritative, false);
  assert.equal(d.activationPossible, false);
  assert.equal(d.overlap, false);
  assert.equal(d.modules.length, 5);
  assert.equal(d.modules.reduce((n, m) => n + m.words, 0), d.perNodeInputWords);
  assert.deepEqual(d.modules.map((m) => m.drawingTag), ['-A102.1', '-A102.2', '-A102.3', '-A102.4', '-A102.5']);
  assert.deepEqual(d.nonProcessModules.map((m) => [m.rackSlot, m.role, m.processWidthBits, m.addressStatus]), [[0, 'HEAD_STATION', 0, 'NOT_APPLICABLE'], [6, 'END_MODULE', 0, 'NOT_APPLICABLE']]);
  assert.deepEqual(d.modules[0].processImageOrderBasis, ['CL-15-MODULE-ORDER', 'CL-16-PROCESS-IMAGE-ORDER-RULE']);
  assert.deepEqual(deriveCandidateProcessImage(syntheticComplete()), d, 'the derivation is deterministic');
  // A synthetic derivation is marked synthetic in the view and still has no authority.
  const v = buildTcAcquisitionEvidenceView(syntheticComplete());
  assert.equal(v.synthetic, true);
  assert.equal(v.processImageCandidate.authoritative, false);
  assert.equal(v.processImageCandidate.activationPossible, false);
});

test('D: RECORD_TO_RECORD_CONSISTENCY_ONLY compares two repository records and exposes no range, start or offset', () => {
  const result = recordToRecordConsistency(CANON, SCHEDULE.rows);
  assert.equal(result.classification, RECORD_TO_RECORD_CONSISTENCY_ONLY);
  assert.equal(result.compared, 240);
  assert.equal(result.mismatches, 0);
  assert.equal(result.consistent, true);
  assert.equal(result.reason, null);
  assert.deepEqual(result.meaning, RECORD_TO_RECORD_CONSISTENCY_MEANING);
  assert.equal(RECORD_TO_RECORD_CONSISTENCY_MEANING.length, 7);
  for (const k of ['hardwareVerified', 'establishesProcessImageStart', 'establishesModuleOffsets', 'authorizesAdapter', 'authorizesAcquisition']) assert.equal(result[k], false, k);
  assert.deepEqual(Object.keys(result).sort(), ['authorizesAcquisition', 'authorizesAdapter', 'classification', 'compared', 'consistent', 'establishesModuleOffsets', 'establishesProcessImageStart', 'hardwareVerified', 'meaning', 'mismatches', 'reason']);
  assert.doesNotMatch(JSON.stringify(result), /startWord|endWord|wordOffset|byteOffset|"modules"|wordOrdinal|\b[0-9]+-[0-9]+\b/);
  // A disagreeing record is reported, never repaired.
  const altered = SCHEDULE.rows.map((r, i) => (i === 7 ? { ...r, wordOrdinal: r.wordOrdinal + 1 } : r));
  assert.deepEqual([recordToRecordConsistency(CANON, altered).mismatches, recordToRecordConsistency(CANON, altered).consistent], [1, false]);
  assert.equal(recordToRecordConsistency(CANON, []).reason, 'NO_ROWS');
  assert.equal(recordToRecordConsistency(variant((c) => { claim(c, 'CL-08-CHANNEL-DATA-WIDTH').value = null; }), SCHEDULE.rows).reason, 'STRUCTURAL_NUMBERS_UNAVAILABLE');
});

test('D: incomplete, conflicting or unresolved evidence blocks derivation: no number is produced', () => {
  const blocked = (mutate) => { const d = deriveCandidateProcessImage(variant((c) => { completeSynthetic(c); mutate(c); })); assert.equal(d.state, DERIVATION_STATE.DERIVATION_BLOCKED); assert.equal(d.modules.length, 0); assert.equal(d.perNodeInputWords, null); assert.equal(d.addressStatus, FIELD_STATUS.ADDRESS_UNRESOLVED); return d.blockers; };
  assert.ok(blocked((c) => { claim(c, 'CL-08-CHANNEL-DATA-WIDTH').value = null; }).some((b) => /data width/.test(b)));
  assert.ok(blocked((c) => { claim(c, 'CL-08-CHANNEL-DATA-WIDTH').value.dataBitsPerChannel = 12; }).some((b) => /16-bit/.test(b)));
  assert.ok(blocked((c) => { claim(c, 'CL-08-CHANNEL-DATA-WIDTH').evidenceClass = EVIDENCE_CLASS.CONFLICTING_EVIDENCE; }).some((b) => /CONFLICTING/.test(b)));
  assert.ok(blocked((c) => { claim(c, 'CL-16-PROCESS-IMAGE-ORDER-RULE').evidenceClass = EVIDENCE_CLASS.OWNER_INPUT_PENDING; }).some((b) => /ordering rule/.test(b)));
  assert.ok(blocked((c) => { c.processImageBasis.moduleOrderClaimId = 'CL-99-NONE'; }).some((b) => /module order/.test(b)));
  assert.ok(blocked((c) => { claim(c, 'CL-15-MODULE-ORDER').value.physicalOrderAfterHeadStation.reverse(); }).some((b) => /differ/.test(b)));
  assert.ok(blocked((c) => { claim(c, 'CL-17-NON-PROCESS-MODULES').value.headStationProcessWidthBits = 16; }).some((b) => /non-process/.test(b)));
  // Two claims disagreeing on the width: neither is picked.
  assert.ok(blocked((c) => { c.claims.push({ ...clone(claim(c, 'CL-08-CHANNEL-DATA-WIDTH')), claimId: 'CL-08B', value: { ...claim(c, 'CL-08-CHANNEL-DATA-WIDTH').value, dataBitsPerChannel: 32 } }); }).some((b) => /conflicting claims/.test(b)));
});

test('D: the validator proves width, status, byte order, word order, encoding, offset and overlap problems', () => {
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-08-CHANNEL-DATA-WIDTH').value.dataBitsPerChannel = null; }))).includes('PROCESS_DATA_WIDTH_INCOMPLETE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { delete claim(c, 'CL-08-CHANNEL-DATA-WIDTH').value.statusBitsPerChannel; }))).includes('STATUS_INFORMATION_MISSING'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.processImageBasis.statusLocationClaimId = null; }))).includes('STATUS_INFORMATION_MISSING'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.claims.push({ ...clone(claim(c, 'CL-08-CHANNEL-DATA-WIDTH')), claimId: 'X', value: { ...claim(c, 'CL-08-CHANNEL-DATA-WIDTH').value, dataBitsPerChannel: 32 } }); }))).includes('CONFLICTING_VALUE_WIDTH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.claims.push({ ...clone(claim(c, 'CL-10-BYTE-ORDER')), claimId: 'X', value: { byteOrder: 'HIGH_BYTE_FIRST' } }); }))).includes('CONFLICTING_BYTE_ORDER'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.claims.push({ ...clone(claim(c, 'CL-11-WORD-ORDER')), claimId: 'X', value: { wordOrder: 'LOW_WORD_FIRST' } }); }))).includes('CONFLICTING_WORD_ORDER'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-12-ENCODING').value.scalePerDigit = 'unknown'; }))).includes('ENCODING_MISSING_OR_AMBIGUOUS'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.processImageBasis.encodingClaimId = null; }))).includes('ENCODING_MISSING_OR_AMBIGUOUS'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.rack[1].wordOffset = 8; }))).includes('MANUAL_NUMERIC_OFFSET_REFUSED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-15-MODULE-ORDER').value.startWord = 0; }))).includes('MANUAL_NUMERIC_OFFSET_REFUSED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.rack[2].processModulePosition = 1; }))).includes('PROCESS_MODULE_POSITION_NOT_SEQUENTIAL'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.processImageBasis.byteOrderClaimId = 'CL-07-CHANNEL-COUNT'; }))).includes('PROCESS_IMAGE_BASIS_TOPIC_MISMATCH'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.rack[1].channelCount = 16; }))).includes('CHANNEL_CAPACITY_MISMATCH'));
});

test('D: the view keeps byte order, word order, status location and encoding UNRESOLVED while showing their model-level candidates', () => {
  const v = buildTcAcquisitionEvidenceView();
  assert.equal(v.statuses.byteOrder.status, FIELD_STATUS.BYTE_ORDER_UNRESOLVED);
  assert.equal(v.statuses.byteOrder.candidate, 'LOW_BYTE_FIRST');
  assert.equal(v.statuses.byteOrder.candidateScope, EVIDENCE_SCOPE.MODEL_LEVEL);
  assert.equal(v.statuses.wordOrder.status, FIELD_STATUS.WORD_ORDER_UNRESOLVED);
  assert.equal(v.statuses.wordOrder.candidate, 'NOT_APPLICABLE_SINGLE_WORD');
  assert.equal(v.statuses.statusLocation.status, FIELD_STATUS.STATUS_LOCATION_UNRESOLVED);
  assert.equal(v.statuses.statusLocation.candidate, 'NOT_ON_FIELD_NETWORK');
  assert.equal(v.statuses.encoding.status, FIELD_STATUS.ENCODING_UNRESOLVED);
  assert.match(v.statuses.encoding.candidate, /TWOS_COMPLEMENT 0\.1 degC\/digit TYPE_K/);
  assert.equal(v.statuses.processImage, FIELD_STATUS.PROCESS_IMAGE_DERIVATION_BLOCKED);
  assert.equal(v.statuses.address, FIELD_STATUS.ADDRESS_UNRESOLVED);
  assert.equal(v.statuses.firmware, FIELD_STATUS.FIRMWARE_NOT_VERIFIED);
  assert.equal(v.processImageCandidate.state, DERIVATION_STATE.DERIVATION_BLOCKED);
  assert.deepEqual(v.processImageCandidate.modules, []);
  assert.equal(v.processImageCandidate.perNodeInputWords, null);
  assert.equal(v.totals.candidateInputWordsPerNode, null);
  assert.doesNotMatch(JSON.stringify(v), /startWord|endWord|candidateWordOrdinal|PROCESS_IMAGE_CANDIDATE_UNVERIFIED/);
  // The canonical set never shows a candidate even if a blocker were lifted: only a synthetic set can.
  const liftedButCanonical = buildTcAcquisitionEvidenceView(variant((c) => { completeSynthetic(c); c.synthetic = false; }));
  assert.equal(liftedButCanonical.statuses.processImage, FIELD_STATUS.PROCESS_IMAGE_DERIVATION_BLOCKED);
  assert.deepEqual(liftedButCanonical.processImageCandidate.modules, []);
  // Conflicting evidence hides the candidate but keeps the status.
  const conflicted = buildTcAcquisitionEvidenceView(variant((c) => { claim(c, 'CL-10-BYTE-ORDER').evidenceClass = EVIDENCE_CLASS.CONFLICTING_EVIDENCE; }));
  assert.equal(conflicted.statuses.byteOrder.candidate, null);
  assert.equal(conflicted.statuses.byteOrder.status, FIELD_STATUS.BYTE_ORDER_UNRESOLVED);
});

// ---------------------------------------------------------------------------- E wire-break boundary

test('E: wire-break decoding stays WIRE_BREAK_DECODING_BLOCKED, the claim is CONFLICTING_EVIDENCE, and no rule is accepted without complete evidence', () => {
  assert.equal(CANON.wireBreakDecoding.status, FIELD_STATUS.WIRE_BREAK_DECODING_BLOCKED);
  assert.equal(CANON.wireBreakDecoding.requiredEvidence.length, 8);
  assert.deepEqual(CANON.wireBreakDecoding.provided, []);
  const wb = claim(CANON, 'CL-14-WIRE-BREAK');
  assert.equal(wb.value.wireBreak.dataHex, wb.value.deactivatedChannel.dataHex);
  assert.equal(wb.value.distinguishableByDataWordAlone, false);
  // Owner ruling (PR #21): classification, completeness, observation and a record that keeps the legs apart.
  assert.equal(CANON.wireBreakDecoding.evidenceClassification, EVIDENCE_CLASS.CONFLICTING_EVIDENCE);
  assert.equal(CANON.wireBreakDecoding.sourceCompleteness, SOURCE_COMPLETENESS.SOURCE_COMPLETENESS_INSUFFICIENT);
  assert.equal(CANON.wireBreakDecoding.hardwareObservationStatus, NOT_PROVIDED);
  assert.equal(CANON.wireBreakDecoding.physicalDecoderImplemented, false);
  assert.equal(wb.value.hardwareObservationStatus, NOT_PROVIDED);
  const rec = wb.value.evidenceRecord;
  assert.equal(rec.manufacturerDocumentTranscription.length, 2);
  assert.ok(rec.manufacturerDocumentTranscription.every((t) => /as transcribed in Stage 0B/.test(t)));
  assert.ok(rec.reviewerParaphrase.some((t) => /reviewer wording/.test(t)));
  assert.match(rec.asInstalledConfiguration, /^MISSING/);
  assert.match(rec.fieldObservation, /^MISSING/);
  assert.equal(rec.ambiguity, 'WIRE_BREAK_VS_DEACTIVATED_CHANNEL_INDISTINGUISHABLE_BY_DATA_WORD');
  assert.equal(rec.originalDocumentReadableInCurrentReview, false);
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.wireBreakDecoding.evidenceClassification = EVIDENCE_CLASS.MANUFACTURER_EVIDENCE; }))).includes('WIRE_BREAK_CLASSIFICATION_OVERSTATED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.wireBreakDecoding.sourceCompleteness = SOURCE_COMPLETENESS.SOURCE_COMPLETE_IN_CURRENT_REVIEW; }))).includes('SOURCE_COMPLETENESS_OVERSTATED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.wireBreakDecoding.hardwareObservationStatus = 'OBSERVED'; }))).includes('HARDWARE_VERIFIED_CLAIM_WITHOUT_PROOF'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.wireBreakDecoding.physicalDecoderImplemented = true; }))).includes('PHYSICAL_WIRE_BREAK_DECODER_REFUSED'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.wireBreakDecoding.status = 'WIRE_BREAK_DECODING_DEFINED'; }))).includes('WIRE_BREAK_EVIDENCE_INCOMPLETE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.wireBreakDecoding.status = 'WIRE_BREAK_DECODING_DEFINED'; c.wireBreakDecoding.provided = [...c.wireBreakDecoding.requiredEvidence]; }))).includes('WIRE_BREAK_EVIDENCE_INCOMPLETE'), 'listing the items is not proof: an observation source is still required');
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { claim(c, 'CL-14-WIRE-BREAK').value.distinguishableByDataWordAlone = true; }))).includes('WIRE_BREAK_EVIDENCE_INCOMPLETE'));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.wireBreakDecoding = null; }))).includes('WIRE_BREAK_POSITION_MISSING'));
  assert.equal(Object.hasOwn(DIAGNOSTIC_CATEGORY, 'WIRE_BREAK'), false, 'no confirmed physical wire-break category exists');
  assert.equal(DIAGNOSTIC_CATEGORY.WIRE_BREAK_DECODING_BLOCKED, 'WIRE_BREAK_DECODING_BLOCKED');
});

test('E: the raw value 0x7FFF yields no classification, no fault, no alarm, no trip, no interlock, no queue, no dispatch, no validity and no control anywhere in the module', () => {
  // No diagnostic category, decode status or sample field is derived from 0x7FFF.
  assert.equal(Object.values(DIAGNOSTIC_CATEGORY).includes('WIRE_BREAK'), false);
  assert.equal(Object.values(DECODE_STATUS).some((v) => /WIRE_BREAK$/.test(v)), false);
  // The module source carries 0x7FFF only inside the evidence record (string data), never in a conditional.
  const lines = SOURCE.split('\n');
  const conditional = lines.filter((l) => /0x7FFF|0x7fff|32767/.test(l) && /\b(if|switch|case|\?|===|!==|==|>=|<=)\b|===|!==|\?/.test(l));
  assert.deepEqual(conditional, [], 'no branch, comparison or ternary on 0x7FFF');
  assert.doesNotMatch(SOURCE, /0x7FFF\s*\)\s*\{|=== *0x7FFF|0x7FFF *===|32767/i);
  for (const word of ['fault', 'alarm', 'trip', 'interlock', 'dispatch']) {
    const near = lines.filter((l) => /0x7FFF/i.test(l) && new RegExp(word, 'i').test(l));
    assert.deepEqual(near, [], `0x7FFF is never tied to ${word}`);
  }
  // A sample carrying the value is simply a sample: the contract gives it no special meaning.
  const withValue = validateAcquisitionSample(sample({ rawPayloadHex: '0x7FFF' }));
  const withOther = validateAcquisitionSample(sample({ rawPayloadHex: '0x03E8' }));
  assert.deepEqual(withValue.issues.map((i) => i.code), withOther.issues.map((i) => i.code), 'the value changes nothing');
  assert.equal(JSON.stringify(withValue).includes('WIRE_BREAK'), false);
});

test('E: the synthetic wire-break scenario is classified SYNTHETIC_TEST_ONLY and the view says so', () => {
  const v = buildTcAcquisitionEvidenceView();
  assert.equal(v.statuses.wireBreakDecoding.status, FIELD_STATUS.WIRE_BREAK_DECODING_BLOCKED);
  assert.equal(v.statuses.wireBreakDecoding.syntheticScenarioClaimId, 'CL-23-SYNTHETIC-WIRE-BREAK');
  assert.equal(v.claims.find((c) => c.claimId === 'CL-23-SYNTHETIC-WIRE-BREAK').evidenceClass, EVIDENCE_CLASS.SYNTHETIC_TEST_ONLY);
  for (const n of v.nodes) assert.equal(n.wireBreakDecodingStatus, FIELD_STATUS.WIRE_BREAK_DECODING_BLOCKED);
});

// --------------------------------------------------------------------- F read-only acquisition contract

const sample = (overrides = {}) => ({
  nodeId: 'TC-NODE-03', cabinetId: 'CAB3', readCycleId: 'cycle-0001', moduleInstance: 2, channel: 5, direction: 'INPUT',
  receivedTime: 1000, deviceSampleTime: null, decodeTime: 1001, publicationTime: 1002,
  rawPayloadHex: '0x03E8', decodeStatus: DECODE_STATUS.DECODED, currentValueValid: true, engineeringValue: 100, engineeringUnit: 'degC',
  diagnosticCategory: DIAGNOSTIC_CATEGORY.NONE, communicationState: ACQUISITION_COMMUNICATION_STATE.ONLINE_READ_ONLY,
  freshnessState: FRESHNESS_STATE.FRESH, lastValidValue: null, lastValidValueTime: null, provenance: SAMPLE_PROVENANCE.SYNTHETIC_TEST_ONLY,
  ...overrides,
});
const sampleCodes = (s) => validateAcquisitionSample(s).issues.map((i) => i.code);

test('F: the contract names the identity, the separate times, the read cycle, the raw payload, decode, validity, diagnostics, state, freshness and provenance; a well-formed synthetic sample validates', () => {
  for (const f of ['nodeId', 'cabinetId', 'readCycleId', 'moduleInstance', 'channel', 'rawPayloadHex', 'decodeStatus', 'currentValueValid', 'engineeringValue', 'diagnosticCategory', 'communicationState', 'freshnessState', 'provenance', ...SAMPLE_TIME_FIELDS.filter((t) => t !== 'lastValidValueTime'), 'lastValidValue', 'lastValidValueTime']) {
    assert.ok(ACQUISITION_SAMPLE_FIELDS.includes(f), f);
  }
  assert.deepEqual(SAMPLE_TIME_FIELDS, ['receivedTime', 'deviceSampleTime', 'decodeTime', 'publicationTime', 'lastValidValueTime']);
  assert.deepEqual(validateAcquisitionSample(sample()), { valid: true, issueCount: 0, issues: [] });
});

test('F: write, command and configuration fields are refused by name; unknown fields are refused; missing fields are proven', () => {
  for (const key of ['writeValue', 'command', 'outputWord', 'setpoint', 'configApply', 'firmwareUpdate', 'restart', 'activateChannel', 'pollInterval', 'connectNow']) {
    assert.ok(sampleCodes(sample({ [key]: 1 })).includes('WRITE_OR_COMMAND_FIELD_REFUSED'), key);
  }
  assert.ok(sampleCodes(sample({ extra: 1 })).includes('UNAPPROVED_SAMPLE_FIELD'));
  const s = sample(); delete s.readCycleId;
  assert.ok(sampleCodes(s).includes('MISSING_SAMPLE_FIELD'));
  assert.ok(sampleCodes(null).includes('SAMPLE_NOT_AN_OBJECT'));
});

test('F: identity is exact: unknown Node, mismatched Cabinet, module or Channel out of range, and no fallback', () => {
  assert.ok(sampleCodes(sample({ nodeId: 'TC-NODE-07' })).includes('UNKNOWN_NODE_IDENTITY'));
  assert.ok(sampleCodes(sample({ cabinetId: 'CAB4' })).includes('NODE_CABINET_PAIR_MISMATCH'));
  assert.ok(sampleCodes(sample({ moduleInstance: 6 })).includes('MODULE_INSTANCE_OUT_OF_RANGE'));
  assert.ok(sampleCodes(sample({ moduleInstance: 0 })).includes('MODULE_INSTANCE_OUT_OF_RANGE'));
  assert.ok(sampleCodes(sample({ channel: 9 })).includes('CHANNEL_OUT_OF_RANGE'));
  assert.ok(sampleCodes(sample({ direction: 'OUTPUT' })).includes('DIRECTION_NOT_INPUT'));
  assert.ok(sampleCodes(sample({ engineeringUnit: 'K' })).includes('ENGINEERING_UNIT_NOT_APPROVED'));
  assert.ok(sampleCodes(sample({ rawPayloadHex: '1000' })).includes('RAW_PAYLOAD_NOT_HEX_WORD'));
});

test('F: provenance is required and physical provenance does not exist in this Stage', () => {
  assert.ok(sampleCodes(sample({ provenance: null })).includes('PROVENANCE_MISSING_OR_UNKNOWN'));
  assert.ok(sampleCodes(sample({ provenance: 'DEVICE' })).includes('PROVENANCE_MISSING_OR_UNKNOWN'));
  assert.ok(sampleCodes(sample({ provenance: SAMPLE_PROVENANCE.PHYSICAL_ACQUISITION })).includes('PHYSICAL_PROVENANCE_NOT_AVAILABLE_IN_STAGE'));
  assert.ok(sampleCodes(sample({ provenance: SAMPLE_PROVENANCE.PHYSICAL_ACQUISITION, currentValueValid: false, engineeringValue: null, decodeStatus: DECODE_STATUS.NOT_DECODED, diagnosticCategory: DIAGNOSTIC_CATEGORY.SYNTHETIC_WIRE_BREAK })).includes('SYNTHETIC_DIAGNOSTIC_WITH_NON_SYNTHETIC_PROVENANCE'));
});

test('F: an invalid or stale sample carries no numeric current value, and a valid sample needs decode, no diagnostic, freshness and an online link', () => {
  assert.ok(sampleCodes(sample({ currentValueValid: false })).includes('NUMERIC_VALUE_ON_INVALID_SAMPLE'));
  assert.equal(validateAcquisitionSample(sample({ currentValueValid: false, engineeringValue: null, decodeStatus: DECODE_STATUS.DECODE_RULE_UNRESOLVED, diagnosticCategory: DIAGNOSTIC_CATEGORY.DECODE_UNRESOLVED })).valid, true);
  assert.ok(sampleCodes(sample({ engineeringValue: null })).includes('VALID_SAMPLE_WITHOUT_NUMERIC_VALUE'));
  assert.ok(sampleCodes(sample({ decodeStatus: DECODE_STATUS.NOT_DECODED })).includes('VALID_SAMPLE_NOT_DECODED'));
  assert.ok(sampleCodes(sample({ diagnosticCategory: DIAGNOSTIC_CATEGORY.OUT_OF_RANGE })).includes('VALID_SAMPLE_WITH_DIAGNOSTIC'));
  assert.ok(sampleCodes(sample({ freshnessState: FRESHNESS_STATE.STALE })).includes('VALID_SAMPLE_NOT_FRESH'));
  assert.ok(sampleCodes(sample({ freshnessState: FRESHNESS_STATE.STALE })).includes('STALE_SAMPLE_WITHOUT_STALE_DIAGNOSTIC'));
  assert.ok(sampleCodes(sample({ communicationState: ACQUISITION_COMMUNICATION_STATE.CONFIGURED_NOT_TESTED })).includes('VALID_SAMPLE_WITHOUT_ONLINE_LINK'));
  // Last Valid Value stays historical with its own time.
  assert.ok(sampleCodes(sample({ currentValueValid: false, engineeringValue: null, decodeStatus: DECODE_STATUS.NOT_DECODED, diagnosticCategory: DIAGNOSTIC_CATEGORY.STALE, freshnessState: FRESHNESS_STATE.STALE, lastValidValue: 99.5 })).includes('LAST_VALID_VALUE_WITHOUT_TIME'));
  assert.equal(validateAcquisitionSample(sample({ currentValueValid: false, engineeringValue: null, decodeStatus: DECODE_STATUS.NOT_DECODED, diagnosticCategory: DIAGNOSTIC_CATEGORY.STALE, freshnessState: FRESHNESS_STATE.STALE, lastValidValue: 99.5, lastValidValueTime: 900 })).valid, true);
});

test('F: the communication state model has the nine states, the live states are named, and the K3 surface derives only CONFIGURED_NOT_TESTED or NOT_CONFIGURED', () => {
  assert.deepEqual(ACQUISITION_COMMUNICATION_STATE_VALUES, ['NOT_CONFIGURED', 'CONFIGURED_NOT_TESTED', 'CONNECTING', 'ONLINE_READ_ONLY', 'RECONNECTING',
    'COMMUNICATION_LOSS', 'PROTOCOL_MISMATCH', 'CONFIGURATION_MISMATCH', 'ACQUISITION_BLOCKED']);
  assert.deepEqual(LIVE_COMMUNICATION_STATES, ['CONNECTING', 'ONLINE_READ_ONLY', 'RECONNECTING', 'COMMUNICATION_LOSS']);
  assert.equal(evidenceOnlyCommunicationState(true), 'CONFIGURED_NOT_TESTED');
  assert.equal(evidenceOnlyCommunicationState(false), 'NOT_CONFIGURED');
  const v = buildTcAcquisitionEvidenceView();
  for (const n of v.nodes) {
    assert.equal(n.communicationState, 'CONFIGURED_NOT_TESTED');
    assert.equal(LIVE_COMMUNICATION_STATES.includes(n.communicationState), false);
  }
  assert.equal(v.communicationStateModel.currentNodeState, 'CONFIGURED_NOT_TESTED');
});

test('F: freshness needs explicit numeric thresholds; the approved thresholds are OWNER_INPUT_PENDING, so classification is UNDETERMINED and never FRESH', () => {
  assert.deepEqual(FRESHNESS_THRESHOLDS, { agingAfterMs: 'OWNER_INPUT_PENDING', staleAfterMs: 'OWNER_INPUT_PENDING', lastValidValueRetentionMs: 'OWNER_INPUT_PENDING' });
  assert.deepEqual(classifyFreshness({ receivedTime: null, evaluationTime: 10, thresholds: FRESHNESS_THRESHOLDS }), { state: 'NEVER_RECEIVED', reason: 'NO_SAMPLE_RECEIVED' });
  assert.deepEqual(classifyFreshness({ receivedTime: 0, evaluationTime: 10, thresholds: FRESHNESS_THRESHOLDS }), { state: 'FRESHNESS_UNDETERMINED', reason: 'THRESHOLD_UNRESOLVED' });
  assert.deepEqual(classifyFreshness({ receivedTime: 0, evaluationTime: 10, thresholds: {} }), { state: 'FRESHNESS_UNDETERMINED', reason: 'THRESHOLD_UNRESOLVED' });
  assert.deepEqual(classifyFreshness({ receivedTime: 0, evaluationTime: 10, thresholds: { agingAfterMs: 500, staleAfterMs: 500 } }), { state: 'FRESHNESS_UNDETERMINED', reason: 'THRESHOLD_UNRESOLVED' });
  // With explicit Owner thresholds the arithmetic is deterministic and the evaluation time is an input, not a clock.
  const t = { agingAfterMs: 500, staleAfterMs: 2000 };
  assert.equal(classifyFreshness({ receivedTime: 0, evaluationTime: 499, thresholds: t }).state, 'FRESH');
  assert.equal(classifyFreshness({ receivedTime: 0, evaluationTime: 500, thresholds: t }).state, 'AGING');
  assert.equal(classifyFreshness({ receivedTime: 0, evaluationTime: 2000, thresholds: t }).state, 'STALE');
  assert.equal(classifyFreshness({ receivedTime: 10, evaluationTime: 5, thresholds: t }).reason, 'TIME_BASIS_INVALID');
});

test('F: FrontRearDelta is permitted only from two current valid sides; a stale or invalid side blocks it and is named', () => {
  const ok = sample();
  assert.deepEqual(frontRearDeltaPermitted(ok, ok), { permitted: true, reasons: [] });
  assert.deepEqual(frontRearDeltaPermitted(ok, sample({ currentValueValid: false, engineeringValue: null })), { permitted: false, reasons: ['REAR_INVALID'] });
  assert.deepEqual(frontRearDeltaPermitted(sample({ freshnessState: FRESHNESS_STATE.STALE }), ok), { permitted: false, reasons: ['FRONT_STALE'] });
  assert.deepEqual(frontRearDeltaPermitted(null, null), { permitted: false, reasons: ['FRONT_INVALID', 'REAR_INVALID'] });
});

// ------------------------------------------------------------------------- view and requests

test('view: six Nodes with the committed identities, honest statuses, zero verification counts and a false authority block', () => {
  const v = buildTcAcquisitionEvidenceView();
  assert.equal(v.evidenceStage, '0.4C-K3');
  assert.equal(v.evidenceMode, 'EVIDENCE_AND_CONTRACT_ONLY');
  assert.equal(v.validation.valid, true);
  assert.deepEqual(v.nodes.map((n) => n.ipAddress), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.ipAddress));
  for (const n of v.nodes) {
    assert.equal(n.reachabilityStatus, 'REACHABILITY_NOT_TESTED');
    assert.equal(n.acquisitionStatus, 'ACQUISITION_NOT_IMPLEMENTED');
    assert.equal(n.hardwareVerificationStatus, 'HARDWARE_VERIFICATION_PENDING');
    assert.equal(n.fieldVerificationStatus, 'FIELD_VERIFICATION_PENDING');
    assert.equal(n.hardwareAccessStatus, 'HARDWARE_ACCESS_NOT_AUTHORIZED');
    assert.equal(n.processImageStatus, 'PROCESS_IMAGE_DERIVATION_BLOCKED');
  }
  assert.deepEqual([v.totals.nodes, v.totals.thermocoupleModules, v.totals.physicalTcChannels, v.totals.candidateInputWordsPerNode], [6, 30, 240, null]);
  assert.deepEqual([v.totals.reachabilityTested, v.totals.acquisitionVerified, v.totals.hardwareVerified, v.totals.fieldVerified], [0, 0, 0, 0]);
  for (const [k, val] of Object.entries(v.authority)) if (k !== 'statements') assert.equal(val, false, k);
  assert.ok(v.authority.statements.includes('NO SOCKET'));
  assert.equal(v.claimSummary.total, 24);
  assert.equal(v.claimSummary.byClass.CONFLICTING_EVIDENCE, 1);
  assert.equal(v.claimSummary.byClass.SYNTHETIC_TEST_ONLY, 1);
});

test('view: the missing-evidence requests are exact, each names what it blocks, and all are OWNER_INPUT_PENDING', () => {
  const v = buildTcAcquisitionEvidenceView();
  assert.equal(v.missingEvidenceRequests.length, 11);
  for (const r of v.missingEvidenceRequests) {
    assert.match(r.requestId, /^REQ-K3-\d\d$/);
    assert.ok(r.item.length > 20);
    assert.ok(r.blocks.length > 0);
    assert.equal(r.status, 'OWNER_INPUT_PENDING');
  }
  assert.ok(v.missingEvidenceRequests.some((r) => r.blocks.includes('PORT')));
  assert.ok(v.missingEvidenceRequests.some((r) => r.blocks.includes('APPROVED_FOR_TEST_ADAPTER')));
  assert.ok(codes(validateTcAcquisitionEvidence(variant((c) => { c.missingEvidenceRequests[0].blocks = []; }))).includes('MISSING_EVIDENCE_REQUEST_INCOMPLETE'));
});

// ---------------------------------------------------------------------- H boundary and regression

test('H: the module imports only the schedule and the Node configuration, no Node built-in and no network, clock or process API', () => {
  const imports = [...SOURCE.matchAll(/^import[^;]*from\s+'([^']+)';/gm)].map((m) => m[1]);
  assert.deepEqual(imports, ['./schedule.mjs', './tcNodeConfiguration.mjs']);
  assert.equal(/from\s+'node:/.test(SOURCE), false);
  for (const forbidden of [/\bnet\b\s*\./, /\bdgram\b/, /\bdns\b/, /\bfetch\s*\(/, /XMLHttpRequest/, /WebSocket/, /\bSocket\b/, /createConnection/, /Date\.now/, /setTimeout/, /setInterval/, /process\.env/, /\blocalStorage\b/]) {
    assert.equal(forbidden.test(SOURCE), false, String(forbidden));
  }
});

test('H: the module spells no port number, no vendor or transport protocol name, no credential key and no non-loopback address literal', () => {
  assert.equal(/\b502\b/.test(SOURCE), false, 'no well-known port literal');
  assert.equal(/\b(tcp|udp)\b/i.test(SOURCE), false, 'no transport name');
  assert.equal(/\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/.test(SOURCE), false, 'no address literal: the Node identities come from tcNodeConfiguration.mjs');
  assert.equal(/(credential|username|token)\s*[:=]/i.test(SOURCE), false);
  const text = JSON.stringify(buildTcAcquisitionEvidenceView());
  assert.equal(/\b502\b/.test(text), false);
  assert.equal(/https?:\/\//.test(text), false);
  assert.equal(/(ONLINE_READ_ONLY|COMMUNICATION_LOSS|CONNECTING|RECONNECTING)"\s*,\s*"processImageStatus/.test(text), false, 'no Node carries a live state');
});

test('H: index.mjs re-exports the K3 surface and no export name matches the K2 transport-vocabulary policy', () => {
  for (const name of ['CANONICAL_TC_ACQUISITION_EVIDENCE', 'validateTcAcquisitionEvidence', 'deriveCandidateProcessImage', 'buildTcAcquisitionEvidenceView',
    'validateAcquisitionSample', 'classifyFreshness', 'ACQUISITION_COMMUNICATION_STATE', 'FRESHNESS_STATE', 'EVIDENCE_CLASS', 'CLAIM_TOPIC']) {
    assert.equal(typeof index[name] === 'function' || typeof index[name] === 'object', true, name);
  }
  for (const name of Object.keys(index)) assert.doesNotMatch(name, /socket|fetch|modem|transport/i);
  assert.equal(CLAIM_TOPIC.PORT, 'PORT');
});

test('H: the current-facing records carry no connector-derived archive identity and no retained candidate, and do carry the Owner evidence disclosure', () => {
  const docs = join(HERE, '..', '..', '..', 'docs');
  const read = (p) => readFileSync(p, 'utf8').replace(/\s+/g, ' '); // hard-wrapped Markdown is compared as one line
  const checkpoint = read(join(docs, 'STAGE_0.4C-K3_CHECKPOINT.md'));
  const register = read(join(docs, 'TC_EVIDENCE_SOURCE_REGISTER.md'));
  const registerK3 = register.slice(register.indexOf(' ## 14.'));
  assert.ok(registerK3.length > 100, 'register section 14 exists');
  const changelog = read(join(HERE, '..', '..', '..', 'CHANGELOG.md'));
  const k3Start = changelog.search(/## [0-9-]+ — Stage 0\.4C-K3 Physical TC Connectivity/);
  assert.ok(k3Start >= 0, 'CHANGELOG has a K3 entry');
  const k3Rest = changelog.slice(k3Start);
  const changelogK3 = k3Rest.slice(0, k3Rest.indexOf(' ## ', 1) > 0 ? k3Rest.indexOf(' ## ', 1) : undefined);
  const currentState = read(join(docs, 'CURRENT_STATE.md'));
  const currentHead = currentState.slice(0, currentState.indexOf('HISTORICAL') > 0 ? currentState.indexOf('HISTORICAL') : 4000);
  assert.match(currentHead, /Stage 0\.4C-K3/);
  const disclosure = [
    'An Owner evidence archive was reported as located through an unapproved connected-storage metadata query.',
    'Archive content was not retrieved or read.',
    'The archive is not accepted as evidence for this checkpoint.',
    'Owner authorization is required before any future connected-storage access.',
  ];
  const forbidden = ['WJSS_TC_EVIDENCE_BASELINE_REV1', '17345849', '17 345 849', '2807f158ced9a984dea288e5ab30e2d8', 'OWNER_SOURCE_LOCATED_NOT_EXTRACTED', 'TC-K3-PROTOCOL-CANDIDATE', 'individual PDF', 'compareCandidateWithSchedule'];
  for (const [name, text] of [['K3 checkpoint', checkpoint], ['register section 14', registerK3], ['CHANGELOG K3 entry', changelogK3], ['CURRENT_STATE head', currentHead]]) {
    for (const token of forbidden) assert.equal(text.includes(token), false, `${name} must not contain ${token}`);
    for (const sentence of disclosure) assert.ok(text.includes(sentence), `${name} must contain: ${sentence}`);
    assert.match(text, /WITHDRAWN/, `${name} records the withdrawn candidate`);
    assert.match(text, /DERIVATION_BLOCKED|DERIVATION BLOCKED/, `${name} records the blocked derivation`);
    assert.match(text, /RECORD_TO_RECORD_CONSISTENCY_ONLY/, `${name} classifies the arithmetic literally`);
  }
  // No word range or start for the canonical set in the current-facing records.
  for (const [name, text] of [['K3 checkpoint', checkpoint], ['CHANGELOG K3 entry', changelogK3], ['CURRENT_STATE head', currentHead]]) {
    assert.equal(/40 (candidate )?input words per Node|words 8\(n|\b0-7\b|\b32-39\b/.test(text), false, `${name} exposes no range`);
  }
});
