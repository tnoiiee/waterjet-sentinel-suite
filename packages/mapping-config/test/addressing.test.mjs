// Stage 0.4B-1 — derived address states, canonical zero-based arithmetic and pressure identities.
// The SYNTHETIC TEST RULE below exists only to exercise the arithmetic. It is not a vendor
// specification and it is never used to report a real address.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { deriveAddresses } from '../src/addressDerivation.mjs';
import { deriveRackView } from '../src/rack.mjs';
import { validateMapping } from '../src/mappingValidation.mjs';
import { DraftSession } from '../src/draftSession.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';
import { pressureTagForWj } from '../src/tagCatalogue.mjs';

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

const cfg = () => syntheticExampleConfiguration();
const byTag = (result) => new Map(result.entries.map((e) => [e.tagName, e]));

test('with no verified rule every enabled binding is ADDRESS_UNRESOLVED and has no numeric address', () => {
  const c = cfg();
  const result = deriveAddresses(deriveRackView(c.modules), c.bindings, null);
  assert.equal(result.verified, false);
  for (const e of result.entries) {
    assert.equal(e.state, 'ADDRESS_UNRESOLVED', e.tagName);
    assert.equal(e.bitOffsetAbsolute, null);
    assert.equal(e.byteOffset, null);
    assert.equal(e.wordOffset, null);
    assert.equal(e.bitIndex, null);
    assert.equal(e.displayNotation, null);
    assert.ok(e.reasons.includes('NO_VERIFIED_PROCESS_IMAGE_RULE'));
    assert.ok(e.reasons.includes('HEAD_STATION_PROFILE_NOT_VERIFIED'));
    assert.ok(e.reasons.includes('MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED'));
  }
});

test('analog channels also carry the status-byte reason; digital channels do not', () => {
  const c = cfg();
  const map = byTag(deriveAddresses(deriveRackView(c.modules), c.bindings, null));
  assert.ok(map.get('PUMP_OUTLET_PRESSURE').reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'));
  assert.equal(map.get('IV1_LOWER_LIMIT').reasons.includes('MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED'), false);
});

test('a disabled binding is NOT_ACTIVE and carries no reasons', () => {
  const c = cfg();
  const bindings = c.bindings.map((b) => (b.tagName === 'IV2_OUTLET_PRESSURE' ? { ...b, enabled: false } : b));
  const map = byTag(deriveAddresses(deriveRackView(c.modules), bindings, null));
  assert.equal(map.get('IV2_OUTLET_PRESSURE').state, 'NOT_ACTIVE');
  assert.deepEqual([...map.get('IV2_OUTLET_PRESSURE').reasons], []);
});

test('the SYNTHETIC TEST RULE gives zero-based analog-then-digital positions', () => {
  const c = cfg();
  const map = byTag(deriveAddresses(deriveRackView(c.modules), c.bindings, SYNTHETIC_TEST_RULE));
  const pump = map.get('PUMP_OUTLET_PRESSURE');
  assert.equal(pump.state, 'DERIVED');
  assert.equal(pump.wordOffset, 0);
  assert.equal(pump.bitOffsetAbsolute, 0);
  assert.equal(pump.bitIndex, null);
  assert.equal(pump.displayNotation, 'INPUT-W0');
  // Three analog modules x 4 channels = 12 words, so the first digital bit is 12 x 16 = 192.
  const iv8 = map.get('IV8_OUTLET_PRESSURE');
  assert.equal(iv8.wordOffset, 9);
  assert.equal(iv8.bitOffsetAbsolute, 144);
  // The synthetic example binds IVn UPPER to DI channel 1 and LOWER to DI channel 2 (catalogue order).
  const upper1 = map.get('IV1_UPPER_LIMIT');
  assert.equal(upper1.bitOffsetAbsolute, 192);
  assert.equal(upper1.wordOffset, 12);
  assert.equal(upper1.bitIndex, 0);
  assert.equal(upper1.displayNotation, 'INPUT-W12.0');
  const lower1 = map.get('IV1_LOWER_LIMIT');
  assert.equal(lower1.bitOffsetAbsolute, 193);
  assert.equal(lower1.bitIndex, 1);
  assert.equal(lower1.displayNotation, 'INPUT-W12.1');
});

test('the derived placements never overlap', () => {
  const c = cfg();
  const result = deriveAddresses(deriveRackView(c.modules), c.bindings, SYNTHETIC_TEST_RULE);
  const bits = new Set();
  for (const e of result.entries) {
    if (e.state !== 'DERIVED') continue;
    const width = e.bitIndex === null ? 16 : 1;
    for (let k = 0; k < width; k += 1) {
      assert.equal(bits.has(e.bitOffsetAbsolute + k), false, `overlap at ${e.bitOffsetAbsolute + k}`);
      bits.add(e.bitOffsetAbsolute + k);
    }
  }
});

test('a process image that exceeds the verified limit yields PROCESS_IMAGE_SIZE_EXCEEDED, not a truncated address', () => {
  const c = cfg();
  const tight = { ...SYNTHETIC_TEST_RULE, maxWordsByArea: { INPUT: 4, OUTPUT: 64 } };
  const map = byTag(deriveAddresses(deriveRackView(c.modules), c.bindings, tight));
  const pump = map.get('PUMP_OUTLET_PRESSURE');
  assert.equal(pump.state, 'ADDRESS_UNRESOLVED');
  assert.ok(pump.reasons.includes('PROCESS_IMAGE_SIZE_EXCEEDED'));
  assert.equal(pump.bitOffsetAbsolute, null);
});

test('a rule that leaves a module unmapped marks its bindings ADDRESS_UNRESOLVED', () => {
  const c = cfg();
  const partial = { ...SYNTHETIC_TEST_RULE, moduleBits: { ...SYNTHETIC_TEST_RULE.moduleBits, '750-471': undefined } };
  const map = byTag(deriveAddresses(deriveRackView(c.modules), c.bindings, partial));
  assert.equal(map.get('PUMP_OUTLET_PRESSURE').state, 'ADDRESS_UNRESOLVED');
  assert.ok(map.get('PUMP_OUTLET_PRESSURE').reasons.includes('MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED'));
});

test('a manual address key in a binding is refused by the Draft', () => {
  const session = new DraftSession(cfg());
  for (const key of ['bitOffsetAbsolute', 'byteOffset', 'wordOffset', 'displayAddress', 'canonicalAddress']) {
    const r = session.setBinding('PUMP_OUTLET_PRESSURE', { [key]: 4 });
    assert.equal(r.ok, false, key);
    assert.equal(r.refusal.code, 'MANUAL_ADDRESS_REFUSED', key);
  }
  assert.equal(session.undoStack.length, 0, 'a refused edit must not enter history');
});

test('a manual address key that reaches validation directly is an ERROR', () => {
  const c = cfg();
  const bindings = c.bindings.map((b) => (b.tagName === 'PUMP_OUTLET_PRESSURE' ? { ...b, wordOffset: 0 } : b));
  const issues = validateMapping(c.modules, bindings, c.additionalTags);
  assert.ok(issues.some((i) => i.code === 'MANUAL_ADDRESS_REFUSED' && i.severity === 'ERROR'));
});

test('a reorder moves derived addresses and the Draft reports ADDRESS_CHANGED for the affected tag', () => {
  const session = new DraftSession(cfg(), { rules: SYNTHETIC_TEST_RULE });
  const r = session.moveModule('AI-MODULE-03', 5);
  assert.equal(r.ok, true);
  const impact = session.impactPreview();
  const iv8 = impact.tags.find((t) => t.tagName === 'IV8_OUTLET_PRESSURE');
  assert.equal(iv8.classification, 'ADDRESS_CHANGED');
  assert.equal(iv8.oldAddress, 144);
  assert.equal(iv8.newAddress, 16);
  const pump = impact.tags.find((t) => t.tagName === 'PUMP_OUTLET_PRESSURE');
  // AI-MODULE-03 now occupies analog words 0..3, so AI-MODULE-01 channel 1 moves to word 4 (bit 64).
  assert.equal(pump.oldAddress, 0);
  assert.equal(pump.newAddress, 64);
  assert.equal(pump.classification, 'ADDRESS_CHANGED');
});

test('pump and IVn pressure identities are distinct; sharing a Channel or a source is refused', () => {
  const c = cfg();
  const iv1 = c.bindings.find((b) => b.tagName === 'IV1_OUTLET_PRESSURE');
  const pump = c.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  // IV1 placed on the pump channel: shared channel and duplicate channel.
  const shared = c.bindings.map((b) => (b.tagName === 'IV1_OUTLET_PRESSURE'
    ? { ...b, moduleInstanceId: pump.moduleInstanceId, channel: pump.channel } : b));
  const codes = validateMapping(c.modules, shared, c.additionalTags).map((i) => i.code);
  assert.ok(codes.includes('DUPLICATE_CHANNEL_BINDING'));
  // IV1 pressure declaring the pump source: identity mismatch, no silent alias.
  const alias = c.bindings.map((b) => (b.tagName === 'IV1_OUTLET_PRESSURE'
    ? { ...b, declaredSourceIdentity: 'PUMP_OUTLET' } : b));
  const aliasCodes = validateMapping(c.modules, alias, c.additionalTags).map((i) => i.code);
  assert.ok(aliasCodes.includes('PAIRED_IV_IDENTITY_MISMATCH'));
  assert.ok(iv1 && pump);
});

test('WJn uses the IVn pressure tag and nothing else; WJ0 and WJ9 are refused', () => {
  assert.equal(pressureTagForWj(1), 'IV1_OUTLET_PRESSURE');
  assert.equal(pressureTagForWj(8), 'IV8_OUTLET_PRESSURE');
  assert.throws(() => pressureTagForWj(0), /no WJ0/);
  assert.throws(() => pressureTagForWj(9), /no WJ9/);
});
