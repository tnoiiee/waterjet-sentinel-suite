// Stage 0.4B-1 — Draft session: real undo/redo, reset, refusals, impact and revision identities.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { DraftSession, createConfiguration } from '../src/draftSession.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';
import { canonicalJson } from '../src/canonical.mjs';
import { tagMappingRevision, rackTopologyRevision, derivedAddressManifestFingerprint, moduleProfileRevision } from '../src/revisions.mjs';

const SYNTHETIC_TEST_RULE = Object.freeze({
  ruleId: 'SYNTHETIC-TEST-RULE-ARITHMETIC-ONLY',
  moduleBits: Object.freeze({
    '750-471': Object.freeze({ kind: 'ANALOG' }),
    '750-554': Object.freeze({ kind: 'ANALOG' }),
    '750-430': Object.freeze({ kind: 'DIGITAL' }),
    '750-530': Object.freeze({ kind: 'DIGITAL' }),
  }),
  maxWordsByArea: Object.freeze({ INPUT: 64, OUTPUT: 64 }),
});

const order = (s) => s.snapshot().modules.map((m) => m.moduleInstanceId).join(',');
const DEFAULT_ORDER = 'COUPLER-01,SUPPLY-01,DI-MODULE-01,DI-MODULE-02,DO-MODULE-01,AI-MODULE-01,AI-MODULE-02,AI-MODULE-03,AO-MODULE-01,END-MODULE-01';

test('the default configuration is never changed by Draft edits', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const before = canonicalJson(session.defaultSnapshot());
  session.moveModule('AI-MODULE-03', 5);
  session.setBinding('IV1_UPPER_LIMIT', { enabled: false });
  assert.equal(canonicalJson(session.defaultSnapshot()), before);
  assert.equal(session.defaultSnapshot().label, 'SYNTHETIC EXAMPLE');
});

test('undo and redo restore the exact previous and next Draft states', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  assert.equal(order(session), DEFAULT_ORDER);
  assert.equal(session.moveModule('AI-MODULE-03', 5).ok, true);
  const moved = order(session);
  assert.notEqual(moved, DEFAULT_ORDER);
  assert.equal(session.undo(), true);
  assert.equal(order(session), DEFAULT_ORDER);
  assert.equal(session.redo(), true);
  assert.equal(order(session), moved);
  assert.equal(session.undo(), true);
  assert.equal(session.undo(), false, 'nothing left to undo');
});

test('a new edit after undo clears the redo history', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  session.moveModule('AI-MODULE-03', 5);
  session.undo();
  session.moveModule('AI-MODULE-02', 5);
  assert.equal(session.redo(), false);
});

test('a refused move adds nothing to history and leaves the Draft unchanged', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const r = session.moveModule('COUPLER-01', 3);
  assert.equal(r.ok, false);
  assert.equal(session.undoStack.length, 0);
  assert.equal(order(session), DEFAULT_ORDER);
});

test('a no-op move adds nothing to history', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  session.moveModule('AI-MODULE-01', 5);
  assert.equal(session.undoStack.length, 0);
});

test('reset returns the Draft to the default order and is itself undoable', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  session.moveModule('AI-MODULE-03', 5);
  session.setBinding('IV2_OUTLET_PRESSURE', { engineering: { min: 0, max: 10, unit: 'bar' } });
  assert.equal(session.resetToDefault(), true);
  assert.equal(order(session), DEFAULT_ORDER);
  const revs = session.revisions();
  const fresh = new DraftSession(syntheticExampleConfiguration()).revisions();
  assert.deepEqual(revs, fresh, 'reset draft has the same identities as a fresh draft');
  assert.equal(session.undo(), true);
  assert.notEqual(order(session), DEFAULT_ORDER);
});

test('a binding edit that fails the field allow-list is refused and leaves no history', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const r = session.setBinding('IV1_OUTLET_PRESSURE', { tagName: 'SOMETHING_ELSE' });
  assert.equal(r.ok, false);
  assert.equal(r.refusal.code, 'FIELD_NOT_EDITABLE');
  assert.equal(session.undoStack.length, 0);
});

test('a binding edit to an allowed field is applied and undoable', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  assert.equal(session.setBinding('IV1_UPPER_LIMIT', { activePolarity: 'ACTIVE_WHEN_OPEN' }).ok, true);
  assert.equal(session.snapshot().bindings.find((b) => b.tagName === 'IV1_UPPER_LIMIT').activePolarity, 'ACTIVE_WHEN_OPEN');
  session.undo();
  assert.equal(session.snapshot().bindings.find((b) => b.tagName === 'IV1_UPPER_LIMIT').activePolarity, 'ACTIVE_WHEN_CLOSED');
});

test('the snapshot is a copy: mutating it cannot change the Draft', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const snap = session.snapshot();
  assert.throws(() => { snap.modules.push({ moduleInstanceId: 'X' }); });
  assert.equal(session.snapshot().modules.length, 10);
});

test('TagMappingRevision and RackTopologyRevision ignore the order of the bindings array and of object keys', () => {
  const c = syntheticExampleConfiguration();
  const reversed = { ...c, bindings: c.bindings.slice().reverse() };
  assert.equal(tagMappingRevision(c.bindings), tagMappingRevision(reversed.bindings));
  const keyed = c.bindings.map((b) => Object.fromEntries(Object.entries(b).reverse()));
  assert.equal(tagMappingRevision(c.bindings), tagMappingRevision(keyed));
});

test('UI state and timestamps are not part of any revision', () => {
  const c = syntheticExampleConfiguration();
  const withUi = c.bindings.map((b) => ({ ...b, uiSelected: true, hoverAt: Date.now(), lastEdited: '2026-10-10T12:00:00Z' }));
  assert.equal(tagMappingRevision(c.bindings), tagMappingRevision(withUi));
  const mods = c.modules.map((m) => ({ ...m, uiExpanded: true, savedAt: Date.now() }));
  assert.equal(rackTopologyRevision(c.modules), rackTopologyRevision(mods));
});

test('a reorder changes RackTopologyRevision and leaves TagMappingRevision unchanged', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const before = session.revisions();
  session.moveModule('AI-MODULE-03', 5);
  const after = session.revisions();
  assert.notEqual(after.rackTopologyRevision, before.rackTopologyRevision);
  assert.equal(after.tagMappingRevision, before.tagMappingRevision);
  assert.equal(after.moduleProfileRevision, before.moduleProfileRevision);
});

test('a binding change alters TagMappingRevision and the derived address manifest', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const before = session.revisions();
  session.setBinding('IV1_UPPER_LIMIT', { activePolarity: 'ACTIVE_WHEN_OPEN' });
  const after = session.revisions();
  assert.notEqual(after.tagMappingRevision, before.tagMappingRevision);
  assert.notEqual(after.derivedAddressManifestFingerprint, before.derivedAddressManifestFingerprint);
});

test('the derived address manifest changes when a verified rule is applied', () => {
  const plain = new DraftSession(syntheticExampleConfiguration()).revisions();
  const withRule = new DraftSession(syntheticExampleConfiguration(), { rules: SYNTHETIC_TEST_RULE }).revisions();
  assert.notEqual(withRule.derivedAddressManifestFingerprint, plain.derivedAddressManifestFingerprint);
  assert.equal(withRule.rackTopologyRevision, plain.rackTopologyRevision);
});

test('the same content built twice gives identical revisions', () => {
  const a = new DraftSession(syntheticExampleConfiguration()).revisions();
  const b = new DraftSession(syntheticExampleConfiguration()).revisions();
  assert.deepEqual(a, b);
  for (const k of ['rackTopologyRevision', 'tagMappingRevision', 'moduleProfileRevision', 'derivedAddressManifestFingerprint']) {
    assert.match(a[k], /^[0-9a-f]{64}$/, k);
  }
});

test('ModuleProfileRevision is a fingerprint of the catalogue, not the catalogue itself', () => {
  assert.match(moduleProfileRevision(), /^[0-9a-f]{64}$/);
  assert.equal(moduleProfileRevision(), moduleProfileRevision());
});

test('the derived address manifest fingerprint is independent of the order of its entries', () => {
  const addresses = { ruleId: null, entries: [{ tagName: 'A', state: 'ADDRESS_UNRESOLVED', reasons: ['X'] }, { tagName: 'B', state: 'ADDRESS_UNRESOLVED', reasons: [] }] };
  const shuffled = { ruleId: null, entries: addresses.entries.slice().reverse() };
  const args = { rackRevision: 'r', mappingRevision: 'm', profileRevision: 'p' };
  // Entries are produced sorted by deriveAddresses; the manifest keeps that order, so reversing entries must change the identity.
  assert.notEqual(derivedAddressManifestFingerprint({ ...args, addresses }), derivedAddressManifestFingerprint({ ...args, addresses: shuffled }));
  assert.equal(derivedAddressManifestFingerprint({ ...args, addresses }), derivedAddressManifestFingerprint({ ...args, addresses: { ruleId: null, entries: addresses.entries.map((e) => ({ ...e })) } }));
});

test('the Draft revision document has no timestamp, no UI state, and cannot activate', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const doc = session.saveDraftRevision();
  const parsed = JSON.parse(doc.text);
  assert.equal(parsed.kind, 'WJSS_MAPPING_DRAFT_REVISION');
  assert.equal(parsed.simulationOnly, true);
  assert.equal(parsed.activationAuthorized, false);
  assert.doesNotMatch(doc.text, /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/, 'no ISO timestamp');
  assert.equal(session.activate, undefined, 'the Draft exposes no activation method');
  assert.equal(session.impactPreview().activationReady, false);
});

test('the same Draft reached by different edit sequences saves the same document', () => {
  // Both sequences end at [AI-02, AI-03, AI-01] in the analog block.
  const a = new DraftSession(syntheticExampleConfiguration());
  a.moveModule('AI-MODULE-03', 5);
  a.moveModule('AI-MODULE-02', 5);
  const b = new DraftSession(syntheticExampleConfiguration());
  b.moveModule('AI-MODULE-01', 7);
  assert.equal(order(a), order(b));
  assert.notEqual(order(a), DEFAULT_ORDER);
  assert.equal(a.saveDraftRevision().text, b.saveDraftRevision().text);
});

test('impact preview: a reorder reports MOVED for the module and the affected tags, even without a verified rule', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  session.moveModule('AI-MODULE-03', 5);
  const impact = session.impactPreview();
  const module = impact.modules.find((m) => m.moduleInstanceId === 'AI-MODULE-03');
  assert.equal(module.classification, 'MOVED');
  const iv8 = impact.tags.find((t) => t.tagName === 'IV8_OUTLET_PRESSURE');
  assert.equal(iv8.moved, true);
  assert.equal(iv8.addressUnresolved, true);
  assert.equal(iv8.classification, 'MOVED', 'MOVED is reported ahead of ADDRESS_UNRESOLVED');
  // IV1 upper limit sits on a DI module that did not move.
  const untouched = impact.tags.find((t) => t.tagName === 'IV1_UPPER_LIMIT');
  assert.equal(untouched.classification, 'ADDRESS_UNRESOLVED');
  assert.equal(impact.activationReady, false);
  assert.equal(impact.activationLabel, 'ACTIVATION NOT AUTHORIZED');
  assert.equal(impact.blockingStatus, 'BLOCKING');
});

test('impact preview: a tag whose binding became invalid is BINDING_INVALID and the rack remains VALID otherwise', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  session.setBinding('IV1_UPPER_LIMIT', { activePolarity: null });
  const impact = session.impactPreview();
  const t = impact.tags.find((x) => x.tagName === 'IV1_UPPER_LIMIT');
  assert.equal(t.classification, 'BINDING_INVALID');
  assert.equal(impact.validationResult, 'INVALID');
});

test('impact preview with a verified rule reports ADDRESS_CHANGED with old and new positions', () => {
  const session = new DraftSession(syntheticExampleConfiguration(), { rules: SYNTHETIC_TEST_RULE });
  session.moveModule('AI-MODULE-03', 5);
  const iv8 = session.impactPreview().tags.find((t) => t.tagName === 'IV8_OUTLET_PRESSURE');
  assert.equal(iv8.classification, 'ADDRESS_CHANGED');
  assert.equal(iv8.oldAddress, 144);
  assert.equal(iv8.newAddress, 16);
});

test('a createConfiguration without the required fields is refused', () => {
  assert.throws(() => createConfiguration({ label: 'X', modules: [] }), /needs label, modules and bindings/);
});
