// Stage 0.4B-3 — module roles, actual-rack evidence and the read-only candidate process image. Tests R1–R23.
//
// These tests prove the Owner rulings of this Stage:
//   * a Power Supply, the System Power Supply and the End Module contribute no Application Process I/O data, so they
//     have no ProcessModulePosition, no ProcessImageOrder and no Process I/O address. Their address is
//     NOT_APPLICABLE with reason NON_PROCESS_DATA_MODULE — they are not waiting for an address;
//   * RackSlot, the actual-rack configuration-tool position and ProcessModulePosition are three different fields and
//     are never aliased to each other;
//   * actual-rack settings are instance- and Channel-scoped and are never propagated;
//   * the candidate process image stays CANDIDATE_UNVERIFIED and every address stays ADDRESS_UNRESOLVED until the
//     actual Process Data image and the actual field-network mapping are supplied.
//
// No test here claims verified hardware evidence, and no test derives a numeric address: there is none.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  AUTHORITATIVE_RACK_MODELS, AUTHORITATIVE_RACK_TOPOLOGY, AUTHORITATIVE_WORKBOOK_SHA256, INSTANCE_CHANNEL_EVIDENCE,
  MISSING_ACTUAL_RACK_EVIDENCE, actualRackEvidenceFor, authoritativeRackInstances, channelEvidenceFor,
  matchesAuthoritativeRack,
} from '../src/actualRackEvidence.mjs';
import { buildCandidateProcessImage } from '../src/candidateProcessImage.mjs';
import { buildEvidenceReport } from '../src/evidenceReport.mjs';
import { deriveAddresses } from '../src/addressDerivation.mjs';
import { buildModuleInstances, deriveRackView, validateRack } from '../src/rack.mjs';
import { getProfile, listProfiles } from '../src/moduleProfiles.mjs';
import { AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE } from '../src/processImageEvidence.mjs';
import { DraftSession, createConfiguration } from '../src/draftSession.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';
import { activeProvider, selectProvider } from '../src/providerPolicy.mjs';
import { importWorkbook } from '../src/nodeImport.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = join(HERE, '..', '..', '..');
const PKG_SRC = join(REPO, 'packages', 'mapping-config', 'src');
const UI_SRC = join(REPO, 'apps', 'mapping-config');
const WORKBOOK = join(REPO, 'T8_IO_Card_Mapping.xlsx');

// Assembled from fragments so this test file does not itself contain vendor vocabulary (boundary rule S3).
const FORBIDDEN_TERMS = ['mo' + 'db' + 'us', 'wa' + 'go', 'ga' + 'lil'];

const MODULES = buildModuleInstances(AUTHORITATIVE_RACK_MODELS);
const RACK = actualRackEvidenceFor(MODULES);
const VIEW = deriveRackView(MODULES, RACK);
const REPORT = buildEvidenceReport(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, VIEW);
const CANDIDATE = buildCandidateProcessImage(VIEW);
const byId = (id) => VIEW.find((v) => v.moduleInstanceId === id);
const reportOf = (id) => REPORT.modules.find((m) => m.moduleInstanceId === id);
const candidateOf = (id) => CANDIDATE.modules.find((m) => m.moduleInstanceId === id);

// --------------------------------------------------------------------------- R1–R6 non-process modules

test('R1 750-601 (Power Supply) has no ProcessModulePosition', () => {
  assert.equal(getProfile('750-601').role, 'POWER_SUPPLY');
  assert.equal(byId('SUPPLY-01').processModulePosition, null);
  assert.equal(byId('SUPPLY-01').processModulePositionStatus, 'NON_PROCESS_DATA_MODULE');
  assert.equal(reportOf('SUPPLY-01').processModulePosition, null);
  assert.equal(candidateOf('SUPPLY-01'), undefined, 'a Power Supply is not a candidate process-image module');
  assert.equal(CANDIDATE.nonProcessModules.find((m) => m.moduleInstanceId === 'SUPPLY-01').processModulePosition, null);
});

test('R2 750-613 (System Power Supply) has no ProcessModulePosition', () => {
  assert.equal(getProfile('750-613').role, 'SYSTEM_POWER_SUPPLY');
  assert.equal(byId('SUPPLY-02').processModulePosition, null);
  assert.equal(byId('SUPPLY-02').processModulePositionStatus, 'NON_PROCESS_DATA_MODULE');
  assert.equal(reportOf('SUPPLY-02').processModulePosition, null);
  assert.equal(CANDIDATE.nonProcessModules.find((m) => m.moduleInstanceId === 'SUPPLY-02').processModulePosition, null);
});

test('R3 750-600 (End Module) has no ProcessModulePosition', () => {
  assert.equal(getProfile('750-600').role, 'END_MODULE');
  assert.equal(byId('END-MODULE-01').processModulePosition, null);
  assert.equal(byId('END-MODULE-01').processModulePositionStatus, 'NON_PROCESS_DATA_MODULE');
  assert.equal(reportOf('END-MODULE-01').processModulePosition, null);
  assert.deepEqual(reportOf('END-MODULE-01').processImageOrder, { state: 'NOT_APPLICABLE', value: null });
});

test('R4 the head station and the three non-process modules use AddressStatus NOT_APPLICABLE, never ADDRESS_UNRESOLVED', () => {
  for (const id of ['COUPLER-01', 'SUPPLY-01', 'SUPPLY-02', 'END-MODULE-01']) {
    assert.equal(byId(id).addressStatus, 'NOT_APPLICABLE', id);
    assert.equal(reportOf(id).addressState, 'NOT_APPLICABLE', id);
    assert.equal(reportOf(id).addressReason, 'NON_PROCESS_DATA_MODULE', id);
    assert.equal(reportOf(id).evidenceState, 'NOT_APPLICABLE', id);
    assert.equal(reportOf(id).completeness, 'NOT_APPLICABLE', id);
    assert.notEqual(reportOf(id).addressState, 'ADDRESS_UNRESOLVED', id);
  }
  // The review never counts them as an unresolved address.
  assert.equal(REPORT.summary.notApplicableModules, 4);
  assert.equal(REPORT.summary.unresolvedModules, 19);
  assert.equal(REPORT.summary.modules, 23);
});

test('R5 the reason for a non-process module is NON_PROCESS_DATA_MODULE, not a pending evidence reason', () => {
  for (const id of ['COUPLER-01', 'SUPPLY-01', 'SUPPLY-02', 'END-MODULE-01']) {
    assert.deepEqual(reportOf(id).reasons, ['NON_PROCESS_DATA_MODULE'], id);
    assert.deepEqual(byId(id).addressReasons, ['NON_PROCESS_DATA_MODULE'], id);
    assert.equal(CANDIDATE.nonProcessModules.find((m) => m.moduleInstanceId === id).addressReason, 'NON_PROCESS_DATA_MODULE');
  }
  // Rack validation does not warn that a Power Supply address stays unresolved.
  const issues = validateRack(MODULES);
  for (const i of issues.filter((x) => x.code === 'MODULE_PROFILE_INCOMPLETE')) {
    assert.doesNotMatch(i.moduleInstanceId, /SUPPLY-|END-MODULE-|COUPLER-/);
    assert.doesNotMatch(i.message, /750-601|750-613|750-600|750-362/);
  }
});

test('R6 no non-process module consumes Input or Output process width', () => {
  for (const id of ['COUPLER-01', 'SUPPLY-01', 'SUPPLY-02', 'END-MODULE-01']) {
    const v = byId(id);
    assert.equal(v.processDataContribution, 'NONE', id);
    assert.equal(v.processInputWidthBits, 0, id);
    assert.equal(v.processOutputWidthBits, 0, id);
    assert.equal(reportOf(id).processInputWidthBits, 0, id);
    assert.equal(reportOf(id).processOutputWidthBits, 0, id);
    assert.equal(reportOf(id).processInputWidth, '0 bit', id);
    assert.equal(reportOf(id).processOutputWidth, '0 bit', id);
    const nonProcess = CANDIDATE.nonProcessModules.find((m) => m.moduleInstanceId === id);
    assert.equal(nonProcess.inputWidthBits, 0, id);
    assert.equal(nonProcess.outputWidthBits, 0, id);
  }
  // No candidate channel belongs to a non-process module.
  const nonProcessIds = new Set(CANDIDATE.nonProcessModules.map((m) => m.moduleInstanceId));
  for (const c of CANDIDATE.channels) assert.equal(nonProcessIds.has(c.moduleInstanceId), false, c.moduleInstanceId);
});

// --------------------------------------------------------------------------- R7–R10 the three positions

test('R7 RackSlot retains all 23 physical modules, one-based, Power Supply and End modules included', () => {
  assert.equal(VIEW.length, 23);
  assert.deepEqual(VIEW.map((v) => v.rackSlot), Array.from({ length: 23 }, (_, i) => i + 1));
  assert.equal(VIEW.some((v) => v.rackSlot === 0), false, 'there is no RackSlot 0');
  assert.deepEqual(AUTHORITATIVE_RACK_TOPOLOGY.map((r) => r.rackSlot), VIEW.map((v) => v.rackSlot));
  assert.equal(byId('SUPPLY-01').rackSlot, 2);
  assert.equal(byId('SUPPLY-02').rackSlot, 12);
  assert.equal(byId('END-MODULE-01').rackSlot, 23);
});

test('R8 the configuration-tool position is never treated as a RackSlot', () => {
  // The two sequences differ, so equality anywhere would mean aliasing.
  const rackSlots = VIEW.map((v) => v.rackSlot);
  const ioCheck = VIEW.map((v) => v.ioCheckPosition);
  assert.deepEqual(rackSlots, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23]);
  assert.deepEqual(ioCheck, [null, null, 1, 2, 3, 4, 5, 6, 7, 8, 9, null, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]);
  for (const v of VIEW) {
    if (v.ioCheckPosition !== null && v.rackSlot > 2) assert.notEqual(v.ioCheckPosition, v.rackSlot, v.moduleInstanceId);
  }
  // No field of the rack view holds an address, and the tool position is not one.
  for (const v of VIEW) {
    for (const key of Object.keys(v)) assert.doesNotMatch(key, /offset|address(?!Status|Reason)/i, key);
  }
  // Reordering the Draft moves the RackSlot but never rewrites the actual-rack position of the instance.
  const moved = [...MODULES];
  const ai1 = moved.findIndex((m) => m.moduleInstanceId === 'AI-MODULE-01');
  moved.splice(ai1, 1);
  moved.splice(3, 0, { moduleInstanceId: 'AI-MODULE-01', modelNumber: '750-471' });
  const movedView = deriveRackView(moved, actualRackEvidenceFor(moved));
  const movedAi1 = movedView.find((v) => v.moduleInstanceId === 'AI-MODULE-01');
  assert.equal(movedAi1.rackSlot, 4, 'the Draft RackSlot moved');
  assert.equal(movedAi1.ioCheckPosition, 10, 'the actual-rack position belongs to the instance and did not move');
});

test('R9 750-600 may have a configuration-tool position while contributing no process data', () => {
  const end = byId('END-MODULE-01');
  assert.equal(end.ioCheckPosition, 20);
  assert.equal(end.ioCheckPositionState, 'VERIFIED_ACTUAL_RACK_SCREENSHOT');
  assert.equal(end.processDataContribution, 'NONE');
  assert.equal(end.processModulePosition, null);
  assert.equal(end.addressStatus, 'NOT_APPLICABLE');
  assert.equal(end.channelCapacity, 0);
  assert.equal(CANDIDATE.nonProcessModules.find((m) => m.moduleInstanceId === 'END-MODULE-01').ioCheckPosition, 20);
});

test('R10 the first 750-471 maps RackSlot 13, ProcessModulePosition 10 and configuration-tool position 10 as three separate fields', () => {
  const ai1 = byId('AI-MODULE-01');
  assert.equal(ai1.rackSlot, 13);
  assert.equal(ai1.processModulePosition, 10);
  assert.equal(ai1.ioCheckPosition, 10);
  assert.equal(ai1.processModulePositionStatus, 'PROVISIONAL_SLOT_ORDER');
  assert.equal(ai1.ioCheckPositionState, 'VERIFIED_ACTUAL_RACK_SCREENSHOT');
  const c = candidateOf('AI-MODULE-01');
  assert.equal(c.rackSlot, 13);
  assert.equal(c.processModulePosition, 10);
  assert.equal(c.ioCheckPosition, 10);
  // ProcessImageOrder is a fourth concept: it is a candidate here, and it is neither of the three above.
  assert.equal(c.processImageOrder.state, 'CANDIDATE_UNVERIFIED');
  assert.notEqual(c.processImageOrder.value, c.rackSlot);
  assert.notEqual(c.processImageOrder.value, c.processModulePosition);
});

// --------------------------------------------------------------------------- R11–R13 evidence scope

test('R11 the Pos.10 Channel 3 settings apply only to that instance and that Channel', () => {
  assert.equal(INSTANCE_CHANNEL_EVIDENCE.length, 1);
  const c = INSTANCE_CHANNEL_EVIDENCE[0];
  assert.equal(c.moduleInstanceId, 'AI-MODULE-01');
  assert.equal(c.ioCheckPosition, 10);
  assert.equal(c.rackSlot, 13);
  assert.equal(c.channel, 3);
  assert.equal(c.scope, 'INSTANCE_AND_CHANNEL_ONLY');
  assert.equal(c.evidenceState, 'VERIFIED_ACTUAL_RACK_SCREENSHOT');
  assert.deepEqual(c.appliesTo, { sameModuleOtherChannels: false, otherInstancesOfSameModel: false, otherModels: false });
  assert.equal(c.settings.signalType, '4-20 mA');
  assert.equal(c.settings.inputFilter, 'Off');
  assert.equal(c.settings.channelDiagnosis, 'On');
  assert.equal(c.settings.upperUserLimit, 32767);
  assert.equal(c.settings.lowerUserLimit, -32768);
  // The report repeats the scope statement, so the UI cannot show it as a general setting.
  assert.equal(REPORT.channelEvidence.length, 1);
  assert.match(REPORT.channelEvidence[0].note, /this instance and this Channel only/);
});

test('R12 no setting is propagated to another Channel or another 750-471 instance without evidence', () => {
  const evidence = channelEvidenceFor(MODULES);
  const covered = new Set(evidence.map((c) => `${c.moduleInstanceId}:${c.channel}`));
  assert.deepEqual([...covered], ['AI-MODULE-01:3']);
  // Every other Channel of every 750-471 instance has no setting evidence at all.
  for (const v of VIEW.filter((x) => x.modelNumber === '750-471')) {
    for (let ch = 1; ch <= 4; ch += 1) {
      if (v.moduleInstanceId === 'AI-MODULE-01' && ch === 3) continue;
      assert.equal(covered.has(`${v.moduleInstanceId}:${ch}`), false, `${v.moduleInstanceId} ch${ch}`);
    }
  }
  // The 750-554 has no Channel setting evidence either.
  assert.equal(evidence.some((c) => c.modelNumber === '750-554'), false);
  // Instance evidence is stored per instance: nine instances, nine records, each with its own position.
  const instances = RACK.instances.filter((r) => r.modelNumber === '750-471');
  assert.equal(instances.length, 9);
  assert.deepEqual(instances.map((r) => r.moduleInstanceId), [
    'AI-MODULE-01', 'AI-MODULE-02', 'AI-MODULE-03', 'AI-MODULE-04', 'AI-MODULE-05',
    'AI-MODULE-06', 'AI-MODULE-07', 'AI-MODULE-08', 'AI-MODULE-09',
  ]);
  assert.equal(new Set(instances.map((r) => r.ioCheckPosition)).size, 9, 'each instance keeps its own position');
});

test('R13 a revision difference does not change process width without explicit profile evidence', () => {
  const instances = RACK.instances.filter((r) => r.modelNumber === '750-471');
  const legible = instances.filter((r) => r.displayedVersion !== null);
  assert.equal(legible.length, 1, 'only the shown instance has a legible version');
  assert.equal(legible[0].moduleInstanceId, 'AI-MODULE-01');
  assert.equal(legible[0].displayedVersion, '01.01.46(04)');
  // The unqualified version field is not asserted to be a software or a hardware revision.
  assert.equal(legible[0].softwareRevision, null);
  assert.equal(legible[0].hardwareRevision, null);
  assert.equal(legible[0].revisionEvidenceState, 'PROVIDED_UNVERIFIED');
  for (const r of instances.filter((x) => x.displayedVersion === null)) {
    assert.equal(r.revisionEvidenceState, 'NOT_PROVIDED');
    assert.match(r.revisionNote, /never inferred/);
  }
  // Widths are model-level: identical for all nine instances whatever their revision evidence says.
  for (const c of CANDIDATE.modules.filter((m) => m.modelNumber === '750-471')) {
    assert.equal(c.channelCount, 4);
    assert.equal(c.bitsPerChannel, 16);
    assert.equal(c.moduleBits, 64);
  }
  // The profile catalogue is keyed by model only, so a revision cannot select a different width.
  assert.equal(getProfile.length, 1);
  assert.equal(getProfile('750-471'), getProfile('750-471'));
  assert.equal(listProfiles().filter((p) => p.modelNumber === '750-471').length, 1);
});

// --------------------------------------------------------------------------- R14–R18 manual process-data facts

test('R14 750-430 contributes 8 input bits', () => {
  const facts = getProfile('750-430').manualFacts;
  assert.equal(facts.facts.channelCount, 8);
  assert.equal(facts.facts.bitsPerChannel, 1);
  assert.equal(facts.facts.packing, 'BIT_PACKED');
  assert.equal(facts.facts.processInputBits, 8);
  assert.equal(facts.facts.processOutputBits, 0);
  assert.equal(facts.scope, 'MODEL_LEVEL_ONLY');
  assert.equal(facts.evidenceState, 'PROVIDED_UNVERIFIED', 'the manual is outside the repository and unhashed');
  assert.equal(facts.availability, 'OWNER_PROVIDED_OUTSIDE_REPOSITORY');
  assert.equal(facts.documentSha256, null, 'no hash is invented');
  for (const c of CANDIDATE.modules.filter((m) => m.modelNumber === '750-430')) {
    assert.equal(c.channelCount, 8);
    assert.equal(c.bitsPerChannel, 1);
    assert.equal(c.moduleBits, 8);
    assert.equal(c.area, 'INPUT');
  }
});

test('R15 750-530 contributes 8 output bits and no input bit', () => {
  const facts = getProfile('750-530').manualFacts.facts;
  assert.equal(facts.channelCount, 8);
  assert.equal(facts.bitsPerChannel, 1);
  assert.equal(facts.processOutputBits, 8);
  assert.equal(facts.processInputBits, 0);
  for (const c of CANDIDATE.modules.filter((m) => m.modelNumber === '750-530')) {
    assert.equal(c.channelCount, 8);
    assert.equal(c.moduleBits, 8);
    assert.equal(c.area, 'OUTPUT');
  }
});

test('R16 750-471 contributes four 16-bit input values', () => {
  const facts = getProfile('750-471').manualFacts.facts;
  assert.equal(facts.channelCount, 4);
  assert.equal(facts.bitsPerChannel, 16);
  assert.equal(facts.processInputBits, 64);
  assert.equal(facts.processOutputBits, 0);
  assert.equal(facts.channelSettingsInstanceSpecific, true);
  const channels = CANDIDATE.channels.filter((c) => c.moduleInstanceId === 'AI-MODULE-01');
  assert.deepEqual(channels.map((c) => c.channel), [1, 2, 3, 4]);
  for (const c of channels) {
    assert.equal(c.bitWidth, 16);
    assert.equal(c.area, 'INPUT');
  }
});

test('R17 750-554 contributes two 16-bit output values', () => {
  const facts = getProfile('750-554').manualFacts.facts;
  assert.equal(facts.channelCount, 2);
  assert.equal(facts.bitsPerChannel, 16);
  assert.equal(facts.processOutputBits, 32);
  assert.equal(facts.processInputBits, 0);
  const channels = CANDIDATE.channels.filter((c) => c.moduleInstanceId === 'AO-MODULE-01');
  assert.deepEqual(channels.map((c) => c.channel), [1, 2]);
  for (const c of channels) {
    assert.equal(c.bitWidth, 16);
    assert.equal(c.area, 'OUTPUT');
  }
});

test('R18 output process-image width creates no write authority', () => {
  assert.equal(getProfile('750-530').manualFacts.facts.writeAuthority, false);
  assert.equal(getProfile('750-554').manualFacts.facts.writeAuthority, false);
  assert.equal(CANDIDATE.writeAuthority, false);
  for (const m of CANDIDATE.modules.filter((x) => x.area === 'OUTPUT')) assert.equal(m.writeAuthority, false);
  for (const c of CANDIDATE.channels.filter((x) => x.area === 'OUTPUT')) assert.equal(c.writeAuthority, false);
  // The Draft session still refuses an address key, and the package still exposes no write path.
  const session = new DraftSession(createConfiguration({
    label: 'test', modules: MODULES.map((m) => ({ ...m })), bindings: [],
  }));
  const refused = session.setBinding('IV1_OUTLET_PRESSURE', { address: 100 });
  assert.equal(refused.ok, false);
  assert.equal(refused.refusal.code, 'MANUAL_ADDRESS_REFUSED');
});

// --------------------------------------------------------------------------- R19–R21 candidate and addresses

test('R19 a manual-derived candidate mapping stays CANDIDATE_UNVERIFIED and is never authoritative', () => {
  assert.equal(CANDIDATE.status, 'CANDIDATE_UNVERIFIED');
  assert.equal(CANDIDATE.authoritative, false);
  assert.equal(CANDIDATE.verified, false);
  assert.equal(CANDIDATE.hardwareReady, false);
  assert.equal(CANDIDATE.basis.widths.evidenceState, 'PROVIDED_UNVERIFIED');
  assert.equal(CANDIDATE.basis.widths.availability, 'OWNER_PROVIDED_OUTSIDE_REPOSITORY');
  assert.equal(CANDIDATE.basis.ordering.state, 'CANDIDATE_UNVERIFIED');
  assert.equal(CANDIDATE.modules.length, 19, 'the 19 Process I/O modules');
  assert.equal(CANDIDATE.channels.length, 110, '5x8 + 9x4 + 4x8 + 2 channels');
  for (const m of CANDIDATE.modules) assert.equal(m.verificationStatus, 'CANDIDATE_UNVERIFIED');
  for (const c of CANDIDATE.channels) assert.equal(c.verificationStatus, 'CANDIDATE_UNVERIFIED');
  // The evidence report itself is not authoritative and cannot derive an address.
  assert.equal(REPORT.authoritative, false);
  assert.equal(REPORT.addressCapable, false);
  assert.equal(REPORT.summary.derivedModules, 0);
});

test('R20 no offset is invented: every candidate offset stays unresolved until the actual Process Data and mapping arrive', () => {
  for (const m of CANDIDATE.modules) {
    assert.equal(m.offsets.state, 'OFFSET_UNRESOLVED');
    assert.equal(m.offsets.byteOffset, null);
    assert.equal(m.offsets.wordOffset, null);
    assert.equal(m.offsets.bitOffset, null);
    assert.ok(m.offsets.reasons.includes('ACTUAL_PROCESS_DATA_NOT_PROVIDED'));
    assert.ok(m.offsets.reasons.includes('ACTUAL_MAPPING_NOT_CROSS_CHECKED'));
  }
  for (const c of CANDIDATE.channels) {
    assert.equal(c.offsets.state, 'OFFSET_UNRESOLVED');
    assert.deepEqual([c.offsets.byteOffset, c.offsets.wordOffset, c.offsets.bitOffset], [null, null, null]);
  }
  for (const area of [CANDIDATE.areas.INPUT, CANDIDATE.areas.OUTPUT]) assert.equal(area.offsets.state, 'OFFSET_UNRESOLVED');
  // Every enabled binding of the authoritative rack is still ADDRESS_UNRESOLVED with no numeric value.
  const bindings = [
    { tagName: 'PUMP_INLET_PRESSURE', moduleInstanceId: 'AI-MODULE-01', channel: 1, enabled: true },
    { tagName: 'IV1_OUTLET_PRESSURE', moduleInstanceId: 'AI-MODULE-02', channel: 1, enabled: true },
    { tagName: 'IV1_LOWER_LIMIT', moduleInstanceId: 'DI-MODULE-01', channel: 1, enabled: true },
    { tagName: 'AO-CHANNEL-1', moduleInstanceId: 'AO-MODULE-01', channel: 1, enabled: true },
  ];
  const derived = deriveAddresses(VIEW, bindings, AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE);
  assert.equal(derived.verified, false);
  assert.equal(derived.ruleId, null);
  for (const e of derived.entries) {
    assert.equal(e.state, 'ADDRESS_UNRESOLVED', e.tagName);
    assert.equal(e.bitOffsetAbsolute, null, e.tagName);
    assert.equal(e.byteOffset, null, e.tagName);
    assert.equal(e.wordOffset, null, e.tagName);
  }
  // The five evidence requests are recorded, and the blocking statement is kept.
  assert.deepEqual(MISSING_ACTUAL_RACK_EVIDENCE.map((m) => m.request), ['A', 'B', 'C', 'D', 'E']);
  assert.match(MISSING_ACTUAL_RACK_EVIDENCE[0].requiredToShow.join(' '), /complete Input process image/);
  assert.match(MISSING_ACTUAL_RACK_EVIDENCE[4].subject, /head-station/);
});

test('R21 manual address entry stays refused in the Draft, the evidence set and the package surface', () => {
  const session = new DraftSession(createConfiguration({
    label: 'test', modules: MODULES.map((m) => ({ ...m })), bindings: [],
  }));
  for (const key of ['address', 'byteOffset', 'wordOffset', 'bitOffset', 'displayNotation', 'processImageOffset']) {
    const r = session.setBinding('IV1_OUTLET_PRESSURE', { [key]: 1 });
    assert.equal(r.ok, false, key);
    assert.equal(r.refusal.code, 'MANUAL_ADDRESS_REFUSED', key);
  }
  // A candidate is read-only: it has no setter and no address field to write.
  assert.throws(() => { CANDIDATE.status = 'AUTHORITATIVE'; }, TypeError);
  assert.equal(CANDIDATE.status, 'CANDIDATE_UNVERIFIED');
  const src = readFileSync(join(PKG_SRC, 'candidateProcessImage.mjs'), 'utf8');
  assert.doesNotMatch(src, /export function (set|write|apply|commit)/);
});

// --------------------------------------------------------------------------- R22 workbook and topology parity

test('R22 the authoritative workbook is unchanged and the recorded topology matches it exactly', { skip: existsSync(WORKBOOK) ? false : 'workbook not present in this checkout' }, () => {
  const bytes = readFileSync(WORKBOOK);
  assert.equal(createHash('sha256').update(bytes).digest('hex'), AUTHORITATIVE_WORKBOOK_SHA256);
  const r = importWorkbook(bytes);
  assert.equal(r.modules.length, 23);
  assert.deepEqual(r.modules.map((m) => m.modelNumber), AUTHORITATIVE_RACK_MODELS);
  assert.deepEqual(r.modules.map((m) => m.moduleInstanceId), MODULES.map((m) => m.moduleInstanceId));
  // The rack derived from the workbook bytes is the rack the topology record describes.
  const workbookView = deriveRackView(r.modules, actualRackEvidenceFor(r.modules));
  assert.deepEqual(workbookView.map((v) => [v.rackSlot, v.moduleInstanceId, v.modelNumber, v.processModulePosition]),
    VIEW.map((v) => [v.rackSlot, v.moduleInstanceId, v.modelNumber, v.processModulePosition]));
  assert.equal(matchesAuthoritativeRack(r.modules), true);
  assert.equal(matchesAuthoritativeRack(syntheticExampleConfiguration().modules), false, 'actual-rack evidence is not applied to another rack');
});

// --------------------------------------------------------------------------- R23 no new capability

test('R23 no field-network protocol, hardware profile activation or write behaviour is added', () => {
  const files = [
    ...readdirSync(PKG_SRC).filter((f) => f.endsWith('.mjs')).map((f) => join(PKG_SRC, f)),
    join(UI_SRC, 'server.mjs'), join(UI_SRC, 'public', 'app.mjs'), join(UI_SRC, 'public', 'index.html'),
  ];
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    const lower = text.toLowerCase();
    for (const term of FORBIDDEN_TERMS) assert.equal(lower.includes(term), false, `${file} must not contain ${term}`);
    // No device access, no transport API and no write route.
    assert.doesNotMatch(text, /from 'node:net'|require\('net'\)|createConnection|XMLHttpRequest|WebSocket|EventSource|new Socket/, file);
    // A hardware profile name may appear only where it is refused. The Stage 0.4B-3 modules never name one.
    if (!file.endsWith('providerPolicy.mjs')) {
      assert.doesNotMatch(text, /TEST_HARDWARE|PRODUCTION/, file);
    }
  }
  const policy = readFileSync(join(PKG_SRC, 'providerPolicy.mjs'), 'utf8');
  assert.match(policy, /REFUSED = Object\.freeze\(\['TEST_HARDWARE', 'PRODUCTION', 'HARDWARE', 'DEVICE'\]\)/,
    'the hardware profiles stay in the refusal list');
  const server = readFileSync(join(UI_SRC, 'server.mjs'), 'utf8');
  assert.match(server, /UI_BIND_HOST = '127\.0\.0\.1'/, 'the server still binds to loopback only');
  assert.doesNotMatch(server, /'(POST|PUT|PATCH|DELETE)'/);
  // The provider policy still refuses everything but SIMULATOR.
  assert.equal(activeProvider(), 'SIMULATOR');
  for (const name of ['TEST_HARDWARE', 'PRODUCTION', 'HARDWARE', 'DEVICE']) {
    const r = selectProvider(name);
    assert.equal(r.ok, false, name);
  }
  // The UI still offers no device control of any kind.
  const markup = readFileSync(join(UI_SRC, 'public', 'index.html'), 'utf8');
  assert.doesNotMatch(markup, /<button[^>]*>[^<]*(connect|poll|write|activate|start|stop|command)/i);
});

// --------------------------------------------------------------------------- defect-class sweep

test('S-R1 no profile definition hard-codes a physical Slot, a configuration-tool position or an address', () => {
  for (const p of listProfiles()) {
    const json = JSON.stringify(p).toLowerCase();
    for (const key of ['rackslot', 'iocheckposition', 'processmoduleposition', 'processimageorder', 'address', 'offset', 'slot']) {
      assert.equal(json.includes(`"${key}"`), false, `${p.modelNumber} must not carry ${key}`);
    }
    assert.equal(p.addressRule, null, p.modelNumber);
  }
});

test('S-R2 actual-rack evidence is instance-level, never stored at model level only', () => {
  assert.equal(RACK.applicable, true);
  assert.equal(RACK.instances.length, 23, 'one record per physical module instance');
  const nineAi = RACK.instances.filter((r) => r.modelNumber === '750-471');
  // Nine instances of one model keep nine distinct records: the model alone cannot hold them.
  assert.equal(new Set(nineAi.map((r) => JSON.stringify([r.moduleInstanceId, r.rackSlot, r.ioCheckPosition]))).size, 9);
  assert.equal(getProfile('750-471').manualFacts.scope, 'MODEL_LEVEL_ONLY');
  assert.equal(INSTANCE_CHANNEL_EVIDENCE[0].scope, 'INSTANCE_AND_CHANNEL_ONLY');
  // A synthetic rack gets no actual-rack evidence, so instance evidence can never be shown for the wrong rack.
  const synthetic = syntheticExampleConfiguration();
  const notApplicable = actualRackEvidenceFor(synthetic.modules);
  assert.equal(notApplicable.applicable, false);
  assert.equal(channelEvidenceFor(synthetic.modules).length, 0);
  const syntheticView = deriveRackView(synthetic.modules, notApplicable);
  for (const v of syntheticView) {
    assert.equal(v.ioCheckPosition, null);
    assert.equal(v.actualRackEvidenceState, 'NOT_APPLICABLE_TO_THIS_RACK');
  }
});

test('S-R3 actual-rack evidence never feeds address derivation', () => {
  // Derivation is unchanged whether or not the actual-rack evidence is attached to the view.
  const withEvidence = deriveAddresses(VIEW, [{ tagName: 'IV1_OUTLET_PRESSURE', moduleInstanceId: 'AI-MODULE-01', channel: 3, enabled: true }]);
  const without = deriveAddresses(deriveRackView(MODULES), [{ tagName: 'IV1_OUTLET_PRESSURE', moduleInstanceId: 'AI-MODULE-01', channel: 3, enabled: true }]);
  assert.deepEqual(withEvidence.entries.map((e) => [e.state, e.reasons]), without.entries.map((e) => [e.state, e.reasons]));
  assert.equal(withEvidence.verified, false);
  // The channel evidence for Channel 3 does not make Channel 3 derivable.
  assert.equal(withEvidence.entries[0].state, 'ADDRESS_UNRESOLVED');
  assert.equal(withEvidence.entries[0].bitOffsetAbsolute, null);
  // The evidence source type of actual-rack evidence supports no VERIFIED_* claim.
  const src = readFileSync(join(PKG_SRC, 'processImageEvidence.mjs'), 'utf8');
  assert.match(src, /SOURCE_TYPE_FOR_STATE = Object\.freeze\(\{[\s\S]*?\}\)/);
  assert.equal(/\[EVIDENCE_STATE\.[A-Z_]+\]: EVIDENCE_TYPE\.ACTUAL_RACK_SCREENSHOT/.test(src), false,
    'no verified state may cite an actual-rack screenshot');
});

test('S-R4 no candidate or synthetic mapping is presented as actual or verified', () => {
  assert.match(CANDIDATE.label, /CANDIDATE PROCESS IMAGE · UNVERIFIED · NOT AUTHORITATIVE/);
  assert.equal(CANDIDATE.synthetic, undefined, 'a candidate is not the synthetic test rule');
  assert.equal(REPORT.synthetic, false);
  assert.equal(REPORT.authoritative, false);
  // The review surface states the candidate status itself, so a reader cannot mistake it for the real map.
  const ui = readFileSync(join(UI_SRC, 'public', 'app.mjs'), 'utf8');
  assert.match(ui, /candidate\.status/);
  assert.match(ui, /authoritative: \$\{candidate\.authoritative \? 'yes' : 'no'\}/);
});
