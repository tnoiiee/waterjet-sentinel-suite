// Stage 0.4B-2 — process-image evidence foundation. Evidence tests E1–E13.
//
// Every numeric address in these tests comes from the SYNTHETIC TEST RULE helper (test/helpers/syntheticEvidence.mjs).
// It exercises the arithmetic only. It is not vendor evidence, not the real address map, and not a default.
// No primary process-image document has been provided, so no test here claims verified hardware evidence.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, createEvidenceSet, validateEvidenceSet, evidenceSetFingerprint,
} from '../src/processImageEvidence.mjs';
import { buildEvidenceReport } from '../src/evidenceReport.mjs';
import { deriveAddresses } from '../src/addressDerivation.mjs';
import { deriveRackView } from '../src/rack.mjs';
import { DraftSession, createConfiguration } from '../src/draftSession.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';
import { validateMapping } from '../src/mappingValidation.mjs';
import { importWorkbook } from '../src/nodeImport.mjs';
import { canonicalJson } from '../src/canonical.mjs';
import { listProfiles } from '../src/moduleProfiles.mjs';
import { SYNTHETIC_TEST_EVIDENCE, SYNTHETIC_SOURCE_ID, syntheticEvidenceInput, withoutCell } from './helpers/syntheticEvidence.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = join(HERE, '..', '..', '..');
const WORKBOOK = join(REPO, 'T8_IO_Card_Mapping.xlsx');
const WORKBOOK_SHA256 = '4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e';

const cfg = () => syntheticExampleConfiguration();
const derive = (evidence, config = cfg()) => deriveAddresses(deriveRackView(config.modules), config.bindings, evidence);
const byTag = (result) => new Map(result.entries.map((e) => [e.tagName, e]));
const obs = (value, state = 'VERIFIED_PRIMARY_SOURCE', sourceId = SYNTHETIC_SOURCE_ID) => ({ value, state, sourceId, note: 'test' });
const enabledEntries = (result) => result.entries.filter((e) => e.state !== 'NOT_ACTIVE');

// ----------------------------------------------------------------------------- E1, E2

test('E1 profiles start incomplete: the authoritative evidence set has no verified value and no manufacturer', () => {
  const rackView = deriveRackView(cfg().modules);
  const report = buildEvidenceReport(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, rackView);
  assert.equal(report.synthetic, false);
  assert.equal(report.authoritative, false);
  assert.equal(report.addressCapable, false);
  assert.equal(report.head.evidenceState, 'NOT_PROVIDED');
  assert.equal(report.head.completeness, 'INCOMPLETE');
  for (const m of report.modules) {
    assert.equal(m.evidenceState, 'NOT_PROVIDED', m.moduleInstanceId);
    assert.equal(m.completeness, 'INCOMPLETE', m.moduleInstanceId);
    assert.equal(m.manufacturer, null, 'no manufacturer is recorded without a source');
    assert.equal(m.processInputWidth, null);
    assert.equal(m.processOutputWidth, null);
    assert.deepEqual(m.processImageOrder, { state: 'NOT_VERIFIED', value: null });
  }
  // The eight target identities each have a record, and none carries an observation.
  assert.deepEqual(
    [AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.headStation.modelNumber, ...AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.modules.map((m) => m.modelNumber)].sort(),
    ['750-362', '750-430', '750-471', '750-530', '750-554', '750-600', '750-601', '750-613', '750-362'].sort(),
  );
  for (const rec of [AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.headStation, ...AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.modules]) {
    assert.deepEqual(rec.cells, {}, `${rec.modelNumber} has no observation`);
  }
  assert.equal(validateEvidenceSet(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE).length, 0);
  // Every catalogue model has an evidence record, so a missing record cannot hide an unverified model.
  const recorded = new Set(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.modules.map((m) => m.modelNumber));
  for (const p of listProfiles()) assert.ok(recorded.has(p.modelNumber), p.modelNumber);
});

test('E2 incomplete profiles produce ADDRESS_UNRESOLVED with explicit reasons and no numeric value, with or without an explicit evidence argument', () => {
  const config = cfg();
  for (const evidence of [null, AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE]) {
    const result = derive(evidence, config);
    assert.equal(result.verified, false);
    assert.equal(result.ruleId, null);
    assert.equal(enabledEntries(result).length, 26);
    for (const e of enabledEntries(result)) {
      assert.equal(e.state, 'ADDRESS_UNRESOLVED', e.tagName);
      for (const k of ['bitOffsetAbsolute', 'byteOffset', 'wordOffset', 'bitIndex', 'displayNotation']) assert.equal(e[k], null, `${e.tagName} ${k}`);
      for (const r of ['HEAD_STATION_PROFILE_NOT_VERIFIED', 'NO_VERIFIED_PROCESS_IMAGE_RULE', 'PROCESS_IMAGE_ORDER_NOT_VERIFIED',
        'MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED', 'BYTE_ORDER_NOT_VERIFIED', 'WORD_ORDER_NOT_VERIFIED']) {
        assert.ok(e.reasons.includes(r), `${e.tagName} ${r}`);
      }
    }
    assert.ok(byTag(result).get('IV1_OUTLET_PRESSURE').reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'));
    assert.equal(byTag(result).get('IV1_LOWER_LIMIT').reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'), false);
  }
  const v = new DraftSession(cfg()).validate();
  assert.equal(v.activationReady, false);
  assert.equal(v.activationAuthorized, false);
  assert.equal(v.unresolvedAddressCount, 26);
});

// ----------------------------------------------------------------------------- E3

test('E3 ProcessModulePosition is never accepted as ProcessImageOrder', () => {
  // (a) A head-station mapping order given as the topology ordinal, or as any number, is refused and not honoured.
  for (const value of ['PROCESS_MODULE_POSITION', 1, 3]) {
    const input = syntheticEvidenceInput();
    input.headStation.cells.moduleMappingOrder = [obs(value)];
    const set = createEvidenceSet(input);
    assert.ok(validateEvidenceSet(set).some((i) => i.code === 'EVIDENCE_VALUE_INVALID' && /moduleMappingOrder/.test(i.message)), String(value));
    assert.equal(buildEvidenceReport(set, deriveRackView(cfg().modules)).head.completeness, 'INCOMPLETE');
    assert.ok(enabledEntries(derive(set)).every((e) => e.state === 'ADDRESS_UNRESOLVED'), 'no address from a refused order');
  }
  // (b) With a verified (synthetic) rule the derived order is its own number, different from the topology ordinal.
  const report = buildEvidenceReport(SYNTHETIC_TEST_EVIDENCE, deriveRackView(cfg().modules));
  const di1 = report.modules.find((m) => m.moduleInstanceId === 'DI-MODULE-01');
  assert.equal(di1.processModulePosition, 1, 'topology ordinal: first channel module in Slot order');
  assert.deepEqual(di1.processImageOrder, { state: 'DERIVED', value: 4 }, 'derived order: after the three analog modules');
  assert.notEqual(di1.processImageOrder.value, di1.processModulePosition);
  // (c) Without a verified head-station rule the order stays unverified although ProcessModulePosition exists.
  const bare = buildEvidenceReport(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, deriveRackView(cfg().modules));
  const bareDi1 = bare.modules.find((m) => m.moduleInstanceId === 'DI-MODULE-01');
  assert.equal(bareDi1.processModulePosition, 1);
  assert.deepEqual(bareDi1.processImageOrder, { state: 'NOT_VERIFIED', value: null });
  // (d) No derived entry exposes the ordinal as an address field.
  for (const e of derive(SYNTHETIC_TEST_EVIDENCE).entries) assert.equal('processModulePosition' in e, false, e.tagName);
});

// ----------------------------------------------------------------------------- E4, E5, E6, E7

test('E4 conflicting evidence blocks derivation and says so', () => {
  const head = syntheticEvidenceInput();
  head.headStation.cells.wordWidthBits.push(obs(32));
  const headResult = derive(createEvidenceSet(head));
  assert.ok(enabledEntries(headResult).every((e) => e.state === 'ADDRESS_UNRESOLVED'), 'a head-station conflict blocks every address');
  assert.ok(headResult.entries.every((e) => e.state === 'NOT_ACTIVE' || e.reasons.includes('CONFLICTING_PROCESS_IMAGE_EVIDENCE')));

  const mod = syntheticEvidenceInput();
  mod.modules.find((m) => m.modelNumber === '750-471').cells.channelDataBits.push(obs(8));
  const modResult = derive(createEvidenceSet(mod));
  for (const e of enabledEntries(modResult)) {
    assert.equal(e.state, 'ADDRESS_UNRESOLVED', e.tagName);
    assert.ok(e.reasons.includes('CONFLICTING_PROCESS_IMAGE_EVIDENCE'), e.tagName);
  }
  const report = buildEvidenceReport(createEvidenceSet(mod), deriveRackView(cfg().modules));
  assert.equal(report.modules.find((m) => m.modelNumber === '750-471').evidenceState, 'CONFLICTING_EVIDENCE');

  // A declared conflict is a conflict, whatever the values.
  const declared = syntheticEvidenceInput();
  declared.modules.find((m) => m.modelNumber === '750-430').cells.byteOrder.push({ value: null, state: 'CONFLICTING_EVIDENCE', sourceId: SYNTHETIC_SOURCE_ID, note: 'declared' });
  const dec = byTag(derive(createEvidenceSet(declared)));
  assert.equal(dec.get('IV1_LOWER_LIMIT').state, 'ADDRESS_UNRESOLVED');
});

test('E5 missing status-byte information blocks the affected analog mappings', () => {
  const result = byTag(derive(withoutCell('750-471', 'statusByte')));
  for (const n of [1, 2, 3, 4, 5, 6, 7, 8]) {
    const e = result.get(`IV${n}_OUTLET_PRESSURE`);
    assert.equal(e.state, 'ADDRESS_UNRESOLVED', e.tagName);
    assert.ok(e.reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'), e.tagName);
    assert.equal(e.bitOffsetAbsolute, null);
  }
  assert.ok(result.get('PUMP_INLET_PRESSURE').reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'));
  // Digital offsets follow the analog words, so they are unresolved too, but carry no status-byte reason of their own.
  const lower = result.get('IV1_LOWER_LIMIT');
  assert.equal(lower.state, 'ADDRESS_UNRESOLVED');
  assert.equal(lower.reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'), false);
  assert.ok(lower.reasons.includes('MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED'));
  // An ENABLED status byte whose placement the engine cannot compute also stays unresolved.
  const input = syntheticEvidenceInput();
  input.modules.find((m) => m.modelNumber === '750-471').cells.statusByte = [obs({ behavior: 'ENABLED', inputBits: 32, outputBits: 0, placement: 'INTERLEAVED_PER_CHANNEL' })];
  const interleaved = byTag(derive(createEvidenceSet(input)));
  assert.equal(interleaved.get('IV1_OUTLET_PRESSURE').state, 'ADDRESS_UNRESOLVED');
  assert.ok(interleaved.get('IV1_OUTLET_PRESSURE').reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'));
});

test('E6 a missing head-station rule blocks all numeric addresses', () => {
  for (const cell of ['processImageGrouping', 'moduleMappingOrder', 'wordWidthBits', 'maxProcessImageWords']) {
    const result = derive(withoutCell('HEAD', cell));
    assert.ok(enabledEntries(result).length > 0);
    for (const e of enabledEntries(result)) {
      assert.equal(e.state, 'ADDRESS_UNRESOLVED', `${cell} ${e.tagName}`);
      assert.equal(e.bitOffsetAbsolute, null);
      assert.ok(e.reasons.includes('HEAD_STATION_PROFILE_NOT_VERIFIED'), `${cell} ${e.tagName}`);
    }
    assert.equal(result.ruleId, null);
  }
  // Order cells name the order reason; a head station for another model is not accepted for this rack.
  assert.ok(derive(withoutCell('HEAD', 'moduleMappingOrder')).entries[0].reasons.includes('PROCESS_IMAGE_ORDER_NOT_VERIFIED'));
  const other = syntheticEvidenceInput();
  other.headStation.modelNumber = '750-600';
  assert.ok(enabledEntries(derive(createEvidenceSet(other))).every((e) => e.state === 'ADDRESS_UNRESOLVED'));
  // A verified grouping the engine cannot compute is unresolved, never approximated.
  const mixed = syntheticEvidenceInput();
  mixed.headStation.cells.processImageGrouping = [obs('SLOT_ORDER_MIXED')];
  assert.ok(enabledEntries(derive(createEvidenceSet(mixed))).every((e) => e.state === 'ADDRESS_UNRESOLVED'));
});

test('E7 unverified byte or word order blocks the affected module only', () => {
  const full = byTag(derive(SYNTHETIC_TEST_EVIDENCE));
  for (const [cell, reason] of [['byteOrder', 'BYTE_ORDER_NOT_VERIFIED'], ['wordOrder', 'WORD_ORDER_NOT_VERIFIED']]) {
    const result = byTag(derive(withoutCell('750-471', cell)));
    for (const n of [1, 2, 3, 4, 5, 6, 7, 8]) {
      const e = result.get(`IV${n}_OUTLET_PRESSURE`);
      assert.equal(e.state, 'ADDRESS_UNRESOLVED', `${cell} IV${n}`);
      assert.ok(e.reasons.includes(reason), `${cell} IV${n}`);
    }
    // The layout does not depend on byte or word order, so the digital limits keep their derived addresses.
    const lower = result.get('IV1_LOWER_LIMIT');
    assert.equal(lower.state, 'DERIVED');
    assert.equal(lower.bitOffsetAbsolute, full.get('IV1_LOWER_LIMIT').bitOffsetAbsolute);
  }
  // An unverified (provided but not verified) value is never used.
  const input = syntheticEvidenceInput();
  input.modules.find((m) => m.modelNumber === '750-471').cells.byteOrder = [obs('BIG_ENDIAN', 'PROVIDED_UNVERIFIED')];
  const unverified = byTag(derive(createEvidenceSet(input)));
  assert.equal(unverified.get('IV1_OUTLET_PRESSURE').state, 'ADDRESS_UNRESOLVED');
  assert.ok(unverified.get('IV1_OUTLET_PRESSURE').reasons.includes('BYTE_ORDER_NOT_VERIFIED'));
});

// ----------------------------------------------------------------------------- E8, E9

test('E8 a complete synthetic test profile derives deterministic, non-overlapping addresses', () => {
  const a = derive(SYNTHETIC_TEST_EVIDENCE);
  const b = derive(createEvidenceSet(syntheticEvidenceInput()));
  assert.equal(canonicalJson(a), canonicalJson(b), 'two builds give identical results');
  const derived = a.entries.filter((e) => e.state === 'DERIVED');
  assert.equal(derived.length, 26);
  const bits = new Set();
  for (const e of derived) {
    const width = e.bitWidth;
    assert.equal(width, e.bitIndex === null ? 16 : 1);
    for (let k = 0; k < width; k += 1) {
      assert.equal(bits.has(e.bitOffsetAbsolute + k), false, `overlap at ${e.bitOffsetAbsolute + k}`);
      bits.add(e.bitOffsetAbsolute + k);
    }
    assert.equal(e.synthetic, true, 'every entry is marked synthetic');
  }
  const m = byTag(a);
  assert.equal(m.get('PUMP_INLET_PRESSURE').bitOffsetAbsolute, 0);
  assert.equal(m.get('PUMP_OUTLET_PRESSURE').bitOffsetAbsolute, 16);
  assert.equal(m.get('IV1_UPPER_LIMIT').bitOffsetAbsolute, 192);
  assert.equal(m.get('IV1_LOWER_LIMIT').displayNotation, 'INPUT-W12.1');
  assert.deepEqual([m.get('PUMP_OUTLET_PRESSURE').byteOrder, m.get('PUMP_OUTLET_PRESSURE').wordOrder], ['BIG_ENDIAN', 'HIGH_WORD_FIRST']);
});

test('E9 a synthetic profile cannot become authoritative, verified, activation-ready or serialized as the rack profile', () => {
  const result = derive(SYNTHETIC_TEST_EVIDENCE);
  assert.equal(result.synthetic, true);
  assert.equal(result.verified, false);
  const report = buildEvidenceReport(SYNTHETIC_TEST_EVIDENCE, deriveRackView(cfg().modules));
  assert.equal(report.synthetic, true);
  assert.equal(report.authoritative, false);
  const session = new DraftSession(cfg(), { evidence: SYNTHETIC_TEST_EVIDENCE });
  const v = session.validate();
  assert.equal(v.unresolvedAddressCount, 0, 'the synthetic rule derives numbers');
  assert.equal(v.activationReady, false, 'but a synthetic rule never makes a Draft ready');
  assert.equal(v.activationAuthorized, false);
  assert.match(JSON.parse(session.saveDraftRevision().text).revisions.processImageEvidenceRevision, /^[0-9a-f]{64}$/);

  // A synthetic source is refused in a set that is not marked synthetic, and the reverse.
  const faked = syntheticEvidenceInput();
  faked.synthetic = false;
  const fakedSet = createEvidenceSet(faked);
  assert.ok(validateEvidenceSet(fakedSet).some((i) => i.code === 'SYNTHETIC_SOURCE_MISMATCH'));
  assert.ok(enabledEntries(derive(fakedSet)).every((e) => e.state === 'ADDRESS_UNRESOLVED'));
  const real = syntheticEvidenceInput();
  real.sources = [{ sourceId: SYNTHETIC_SOURCE_ID, evidenceType: 'PRIMARY_DOCUMENT', documentTitle: 'x', documentSha256: null, documentRevision: 'r1' }];
  assert.ok(validateEvidenceSet(createEvidenceSet(real)).some((i) => i.code === 'SYNTHETIC_SOURCE_MISMATCH'));

  // The default Draft, the shipped evidence and the package source carry no synthetic rule.
  assert.equal(new DraftSession(cfg()).evidence, AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE);
  assert.equal(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.synthetic, false);
  assert.equal(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.sources.some((s) => s.evidenceType === 'SYNTHETIC_TEST_RULE'), false);
  assert.ok(new DraftSession(cfg()).validate().addresses.entries.every((e) => e.bitOffsetAbsolute === null));
  const src = join(HERE, '..', 'src');
  for (const f of readdirSync(src).filter((n) => n.endsWith('.mjs'))) {
    assert.doesNotMatch(readFileSync(join(src, f), 'utf8'), /ARITHMETIC-ONLY|syntheticEvidence/, `${f} carries a synthetic rule`);
  }
});

// ----------------------------------------------------------------------------- E10, E11, E12, E13

test('E10 fingerprints change when relevant evidence changes and not when it does not', () => {
  const base = evidenceSetFingerprint(SYNTHETIC_TEST_EVIDENCE);
  assert.match(base, /^[0-9a-f]{64}$/);
  assert.equal(evidenceSetFingerprint(createEvidenceSet(syntheticEvidenceInput())), base, 'rebuilt, same content');
  const changed = syntheticEvidenceInput();
  changed.headStation.cells.wordWidthBits = [obs(32)];
  const changedSet = createEvidenceSet(changed);
  assert.notEqual(evidenceSetFingerprint(changedSet), base);
  const rev = (set) => new DraftSession(cfg(), { evidence: set }).revisions();
  const r0 = rev(SYNTHETIC_TEST_EVIDENCE);
  const r1 = rev(changedSet);
  assert.notEqual(r1.processImageEvidenceRevision, r0.processImageEvidenceRevision);
  assert.notEqual(r1.derivedAddressManifestFingerprint, r0.derivedAddressManifestFingerprint);
  assert.equal(r1.rackTopologyRevision, r0.rackTopologyRevision);
  assert.equal(r1.tagMappingRevision, r0.tagMappingRevision);
  // Per-profile fingerprints follow their own record only.
  const rackView = deriveRackView(cfg().modules);
  const fp = (set, model) => buildEvidenceReport(set, rackView).modules.find((m) => m.modelNumber === model).fingerprint;
  const alt = syntheticEvidenceInput();
  alt.modules.find((m) => m.modelNumber === '750-471').cells.channelDataBits = [obs(16), obs(16)];
  const altSet = createEvidenceSet(alt);
  assert.equal(fp(altSet, '750-430'), fp(SYNTHETIC_TEST_EVIDENCE, '750-430'), 'an unrelated profile is unchanged');
  alt.modules.find((m) => m.modelNumber === '750-471').cells.channelDataBits = [obs(16), { ...obs(8), state: 'PROVIDED_UNVERIFIED' }];
  assert.notEqual(fp(createEvidenceSet(alt), '750-471'), fp(SYNTHETIC_TEST_EVIDENCE, '750-471'));
  // The default evidence revision is stable and differs from the synthetic one.
  assert.equal(new DraftSession(cfg()).revisions().processImageEvidenceRevision, evidenceSetFingerprint(AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE));
  assert.notEqual(new DraftSession(cfg()).revisions().processImageEvidenceRevision, r0.processImageEvidenceRevision);
});

test('E11 numeric manual address input remains refused, in the Draft and in evidence', () => {
  const session = new DraftSession(cfg());
  for (const key of ['address', 'byteOffset', 'wordOffset', 'bitOffsetAbsolute', 'bitIndex', 'displayNotation', 'channelOffset', 'wordCount']) {
    const r = session.setBinding('PUMP_OUTLET_PRESSURE', { [key]: 4 });
    assert.equal(r.ok, false, key);
    assert.equal(r.refusal.code, 'MANUAL_ADDRESS_REFUSED', key);
  }
  assert.equal(session.undoStack.length, 0);
  const c = cfg();
  const bad = c.bindings.map((b) => (b.tagName === 'IV1_UPPER_LIMIT' ? { ...b, byteOffset: 2 } : b));
  assert.ok(validateMapping(c.modules, bad, c.additionalTags).some((i) => i.code === 'MANUAL_ADDRESS_REFUSED' && i.severity === 'ERROR'));
  // Evidence cannot carry an address either: the cell is refused and nothing is derived from it.
  const input = syntheticEvidenceInput();
  input.modules.find((m) => m.modelNumber === '750-430').cells.wordOffset = [obs(4)];
  input.modules.find((m) => m.modelNumber === '750-430').cells.bitOffsetAbsolute = [obs(4)];
  const issues = validateEvidenceSet(createEvidenceSet(input));
  assert.equal(issues.filter((i) => i.code === 'MANUAL_ADDRESS_REFUSED').length, 2);
  assert.equal(derive(createEvidenceSet(input)).entries.find((e) => e.tagName === 'IV1_LOWER_LIMIT').bitOffsetAbsolute,
    byTag(derive(SYNTHETIC_TEST_EVIDENCE)).get('IV1_LOWER_LIMIT').bitOffsetAbsolute, 'the refused cell changed nothing');
  // No evidence cell is an offset, and the Draft exposes no address setter.
  assert.equal(session.setAddress, undefined);
});

test('E12 a reorder recalculates the derived manifest and the review model, and Undo restores them', () => {
  for (const evidence of [AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, SYNTHETIC_TEST_EVIDENCE]) {
    const s = new DraftSession(cfg(), { evidence });
    const before = s.revisions();
    const reportBefore = s.evidenceReport();
    assert.equal(s.moveModule('AI-MODULE-03', 5).ok, true);
    const after = s.revisions();
    assert.notEqual(after.derivedAddressManifestFingerprint, before.derivedAddressManifestFingerprint);
    assert.notEqual(after.rackTopologyRevision, before.rackTopologyRevision);
    assert.equal(after.processImageEvidenceRevision, before.processImageEvidenceRevision, 'the evidence itself is unchanged');
    const ai3 = (r) => r.modules.find((m) => m.moduleInstanceId === 'AI-MODULE-03');
    assert.equal(ai3(reportBefore).rackSlot, 8);
    assert.equal(ai3(s.evidenceReport()).rackSlot, 6);
    assert.notEqual(ai3(s.evidenceReport()).processModulePosition, ai3(reportBefore).processModulePosition);
    assert.ok(s.undo());
    assert.deepEqual(s.revisions(), before);
  }
  const s = new DraftSession(cfg(), { evidence: SYNTHETIC_TEST_EVIDENCE });
  s.moveModule('AI-MODULE-03', 5);
  const iv8 = s.validate().addresses.entries.find((e) => e.tagName === 'IV8_OUTLET_PRESSURE');
  assert.equal(iv8.bitOffsetAbsolute, 16, 'the derived address follows the new Slot order');
});

test('E13 the authoritative Workbook is unchanged and still imports as before, with unresolved addresses', () => {
  const bytes = readFileSync(WORKBOOK);
  assert.equal(createHash('sha256').update(bytes).digest('hex'), WORKBOOK_SHA256);
  const owner = AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE.sources.find((s) => s.sourceId === 'OWNER-WORKBOOK');
  assert.equal(owner.documentSha256, WORKBOOK_SHA256, 'the evidence set names the exact workbook bytes');
  assert.equal(owner.evidenceType, 'OWNER_WORKBOOK_FACT');
  const r = importWorkbook(bytes);
  assert.equal(r.modules.length, 23, 'physical rack Slots');
  assert.equal(r.bindings.filter((b) => b.enabled).length, 26, 'enabled default bindings, no seed');
  assert.deepEqual(r.reservedInventory.summary.byDirection, { INPUT: 17, OUTPUT: 1 });
  assert.equal(r.reservedInventory.summary.total, 18);
  const config = createConfiguration({ label: r.label, modules: r.modules, bindings: r.bindings, declaredChannelCounts: r.declaredChannelCounts, additionalTags: r.additionalTags });
  const session = new DraftSession(config);
  const v = session.validate();
  assert.equal(v.unresolvedAddressCount, 26);
  assert.equal(v.activationReady, false);
  assert.equal(v.activationAuthorized, false);
  assert.ok(v.addresses.entries.filter((e) => e.state !== 'NOT_ACTIVE').every((e) => e.state === 'ADDRESS_UNRESOLVED' && e.bitOffsetAbsolute === null));
  // Every model of the real rack has an evidence record, and the rack's evidence review is complete for none of them.
  const report = session.evidenceReport();
  assert.equal(report.summary.completeProfiles, 0);
  assert.equal(report.summary.derivedModules, 0);
  assert.equal(report.head.completeness, 'INCOMPLETE');
  // ProcessModulePosition is a topology ordinal and is unchanged by the evidence work.
  const positions = report.modules.map((m) => m.processModulePosition).filter((p) => p !== null);
  assert.deepEqual(positions, positions.map((_, i) => i + 1));
});

// ----------------------------------------------------------------------------- source handling

test('verified claims need a usable source; otherwise they are demoted and reported, never honoured', () => {
  const make = (mutate) => {
    const input = syntheticEvidenceInput();
    mutate(input);
    return createEvidenceSet(input);
  };
  const missing = make((i) => { i.headStation.cells.wordWidthBits = [obs(16, 'VERIFIED_PRIMARY_SOURCE', 'NO-SUCH-SOURCE')]; });
  assert.ok(validateEvidenceSet(missing).some((i) => i.code === 'EVIDENCE_SOURCE_MISSING'));
  assert.ok(enabledEntries(derive(missing)).every((e) => e.state === 'ADDRESS_UNRESOLVED'));

  // A non-synthetic set: a primary-document claim needs a SHA-256 or a revision, and the right source type.
  const realSet = (source, state) => createEvidenceSet({
    evidenceSetId: 'TEST-NON-SYNTHETIC', synthetic: false, sources: [source],
    headStation: { modelNumber: '750-362', cells: { wordWidthBits: [{ value: 16, state, sourceId: 'S1' }] } },
    modules: [],
  });
  const noId = { sourceId: 'S1', evidenceType: 'PRIMARY_DOCUMENT', documentTitle: 'Document without identity', documentSha256: null, documentRevision: null };
  assert.ok(validateEvidenceSet(realSet(noId, 'VERIFIED_PRIMARY_SOURCE')).some((i) => i.code === 'EVIDENCE_SOURCE_UNIDENTIFIED'));
  const exportSource = { ...noId, evidenceType: 'IO_CHECK_EXPORT', documentRevision: 'r1' };
  assert.ok(validateEvidenceSet(realSet(exportSource, 'VERIFIED_PRIMARY_SOURCE')).some((i) => i.code === 'EVIDENCE_SOURCE_TYPE_MISMATCH'));
  const badHash = { ...noId, documentSha256: 'xyz' };
  assert.ok(validateEvidenceSet(realSet(badHash, 'PROVIDED_UNVERIFIED')).some((i) => i.code === 'EVIDENCE_SOURCE_SHA256_INVALID'));
  // A demoted claim is reported as PROVIDED_UNVERIFIED, which is not address-capable.
  const rep = buildEvidenceReport(realSet(noId, 'VERIFIED_PRIMARY_SOURCE'), deriveRackView(cfg().modules));
  assert.equal(rep.head.evidenceState, 'PROVIDED_UNVERIFIED');
  assert.equal(rep.addressCapable, false);
});

test('a size limit that the layout exceeds is unresolved, never truncated', () => {
  const tight = createEvidenceSet(syntheticEvidenceInput({ maxWords: { input: 4, output: 64 } }));
  const result = byTag(derive(tight));
  assert.equal(result.get('PUMP_OUTLET_PRESSURE').state, 'ADDRESS_UNRESOLVED');
  assert.ok(result.get('PUMP_OUTLET_PRESSURE').reasons.includes('PROCESS_IMAGE_SIZE_EXCEEDED'));
});

test('a module whose channel data width contradicts its total width is blocked as conflicting evidence', () => {
  const input = syntheticEvidenceInput();
  input.modules.find((m) => m.modelNumber === '750-471').cells.processWidthBits = [obs({ input: 48, output: 0 })];
  const result = byTag(derive(createEvidenceSet(input)));
  assert.equal(result.get('IV1_OUTLET_PRESSURE').state, 'ADDRESS_UNRESOLVED');
  assert.ok(result.get('IV1_OUTLET_PRESSURE').reasons.includes('CONFLICTING_PROCESS_IMAGE_EVIDENCE'));
});
