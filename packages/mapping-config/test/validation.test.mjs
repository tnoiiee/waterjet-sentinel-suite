// Stage 0.4B-1 — mapping validation, tag catalogue and provider policy.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validateMapping, summariseIssues } from '../src/mappingValidation.mjs';
import { listSimulationTags, getTagDef, REQUIRED_TAG_NAMES } from '../src/tagCatalogue.mjs';
import { selectProvider, activeProvider, REFUSED_PROVIDER_NAMES } from '../src/providerPolicy.mjs';
import { DraftSession } from '../src/draftSession.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';

const cfg = () => syntheticExampleConfiguration();
const codesOf = (issues, severity) => issues.filter((i) => !severity || i.severity === severity).map((i) => i.code);
const withBinding = (tagName, patch) => cfg().bindings.map((b) => (b.tagName === tagName ? { ...b, ...patch } : b));

test('the synthetic example is VALID with no ERROR, and is never activation-ready or authorised', () => {
  const session = new DraftSession(cfg());
  const v = session.validate();
  assert.equal(v.status, 'VALID');
  assert.equal(v.summary.errors, 0);
  assert.equal(v.activationReady, false);
  assert.equal(v.activationAuthorized, false);
  assert.equal(v.activationLabel, 'ACTIVATION NOT AUTHORIZED');
  assert.ok(v.blockingReasons.includes('NO_VERIFIED_PROCESS_IMAGE_RULE'));
  assert.equal(v.unresolvedAddressCount, 26);
});

test('the required Runtime tag set is exactly the pump pressures, the IVn pressures and the IVn limits', () => {
  // 2 pump pressures + 8 IVn pressures + 16 IVn limits. No Main Valve I/O tag is required (none exists in the workbook).
  assert.equal(REQUIRED_TAG_NAMES.length, 26);
  for (const n of [1, 2, 3, 4, 5, 6, 7, 8]) {
    assert.ok(REQUIRED_TAG_NAMES.includes(`IV${n}_OUTLET_PRESSURE`));
    assert.ok(REQUIRED_TAG_NAMES.includes(`IV${n}_LOWER_LIMIT`));
    assert.ok(REQUIRED_TAG_NAMES.includes(`IV${n}_UPPER_LIMIT`));
  }
  assert.ok(REQUIRED_TAG_NAMES.includes('PUMP_INLET_PRESSURE'));
  assert.ok(REQUIRED_TAG_NAMES.includes('PUMP_OUTLET_PRESSURE'));
  assert.equal(REQUIRED_TAG_NAMES.includes('MAIN_VALVE_OUTLET_PRESSURE'), false, 'no invented Main Valve required tag');
  assert.equal(getTagDef('MAIN_VALVE_OUTLET_PRESSURE'), null, 'no Main Valve catalogue entry');
});

test('the two pump transmitters are the only confirmed engineering ranges (0–40 bar); every other range is unconfigured', () => {
  for (const name of ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']) {
    const pump = getTagDef(name);
    assert.deepEqual([pump.confirmedEngineeringRange.min, pump.confirmedEngineeringRange.max, pump.confirmedEngineeringRange.unit], [0, 40, 'bar'], name);
  }
  for (const t of listSimulationTags()) {
    if (t.tagName === 'PUMP_INLET_PRESSURE' || t.tagName === 'PUMP_OUTLET_PRESSURE') continue;
    assert.equal(t.confirmedEngineeringRange, null, t.tagName);
  }
});

test('the catalogue has no output tag and no write-capable identity', () => {
  for (const t of listSimulationTags()) {
    assert.notEqual(t.direction, 'OUTPUT', t.tagName);
    assert.equal(t.readOnlyStage, true, t.tagName);
  }
});

test('the source identities for pump and valve pressure are distinct and never aliased', () => {
  const ids = listSimulationTags().filter((t) => /PRESSURE/.test(t.tagName)).map((t) => t.sourceIdentity);
  assert.equal(new Set(ids).size, ids.length);
  assert.equal(getTagDef('PUMP_OUTLET_PRESSURE').sourceIdentity, 'PUMP_OUTLET');
  assert.equal(listSimulationTags().some((t) => /MAIN_VALVE/.test(t.tagName) || /MAIN_VALVE/.test(t.sourceIdentity)), false);
});

test('an output tag bound through the import is refused and never mapped', () => {
  const c = cfg();
  const additional = { ...c.additionalTags, EX_DO_01: { tagName: 'EX_DO_01', direction: 'OUTPUT', channelType: 'DIGITAL', role: 'X' } };
  const bindings = [...c.bindings, { tagName: 'EX_DO_01', moduleInstanceId: 'DO-MODULE-01', channel: 1, enabled: false }];
  const codes = codesOf(validateMapping(c.modules, bindings, additional), 'ERROR');
  assert.ok(codes.includes('OUTPUT_BINDING_REFUSED'));
});

test('a digital limit bound to an analog channel is refused', () => {
  const c = cfg();
  const bindings = withBinding('IV1_LOWER_LIMIT', { moduleInstanceId: 'AI-MODULE-01', channel: 4 });
  assert.ok(codesOf(validateMapping(c.modules, bindings, c.additionalTags), 'ERROR').includes('LIMIT_TO_ANALOG_REFUSED'));
});

test('a pressure bound to a digital channel is refused', () => {
  const c = cfg();
  const bindings = withBinding('IV1_OUTLET_PRESSURE', { moduleInstanceId: 'DI-MODULE-01', channel: 1 });
  assert.ok(codesOf(validateMapping(c.modules, bindings, c.additionalTags), 'ERROR').includes('PRESSURE_TO_DIGITAL_REFUSED'));
});

test('a digital limit without an explicit polarity is an ERROR, never a default', () => {
  const c = cfg();
  const bindings = withBinding('IV3_UPPER_LIMIT', { activePolarity: null });
  assert.ok(codesOf(validateMapping(c.modules, bindings, c.additionalTags), 'ERROR').includes('POLARITY_NOT_EXPLICIT'));
});

test('all limit inputs share one explicit ContactPolarity unless a channel records an override', () => {
  const c = cfg();
  const mixed = withBinding('IV3_UPPER_LIMIT', { activePolarity: 'ACTIVE_WHEN_OPEN' });
  assert.ok(codesOf(validateMapping(c.modules, mixed, c.additionalTags), 'ERROR').includes('LIMIT_POLARITY_NOT_UNIFORM'));
  const overridden = withBinding('IV3_UPPER_LIMIT', { activePolarity: 'ACTIVE_WHEN_OPEN', polarityOverride: true });
  assert.equal(codesOf(validateMapping(c.modules, overridden, c.additionalTags), 'ERROR').includes('LIMIT_POLARITY_NOT_UNIFORM'), false);
  const uniform = validateMapping(c.modules, c.bindings, c.additionalTags);
  assert.equal(codesOf(uniform, 'ERROR').includes('LIMIT_POLARITY_NOT_UNIFORM'), false);
});

test('an unrecognised polarity or contact value is refused', () => {
  const c = cfg();
  assert.ok(codesOf(validateMapping(c.modules, withBinding('IV1_UPPER_LIMIT', { activePolarity: 'ALWAYS' }), c.additionalTags), 'ERROR').includes('POLARITY_INVALID'));
  assert.ok(codesOf(validateMapping(c.modules, withBinding('IV1_UPPER_LIMIT', { contactType: 'NO/NC' }), c.additionalTags), 'ERROR').includes('CONTACT_TYPE_INVALID'));
});

test('an unknown NO/NC contact is a warning, not silently NO', () => {
  const c = cfg();
  const issues = validateMapping(c.modules, withBinding('IV1_UPPER_LIMIT', { contactType: null }), c.additionalTags);
  assert.ok(codesOf(issues, 'WARNING').includes('CONTACT_TYPE_UNKNOWN'));
});

test('an analog channel with no engineering range is a warning; an inverted range is an ERROR', () => {
  const c = cfg();
  const unset = validateMapping(c.modules, withBinding('IV2_OUTLET_PRESSURE', { engineering: null }), c.additionalTags);
  assert.ok(codesOf(unset, 'WARNING').includes('ENGINEERING_RANGE_UNCONFIGURED'));
  const inverted = validateMapping(c.modules, withBinding('IV2_OUTLET_PRESSURE', { engineering: { min: 40, max: 0, unit: 'bar' } }), c.additionalTags);
  assert.ok(codesOf(inverted, 'ERROR').includes('INVALID_ENGINEERING_RANGE'));
});

test('a required tag left without a binding is an ERROR', () => {
  const c = cfg();
  const bindings = c.bindings.filter((b) => b.tagName !== 'IV5_OUTLET_PRESSURE');
  assert.ok(codesOf(validateMapping(c.modules, bindings, c.additionalTags), 'ERROR').includes('REQUIRED_TAG_MISSING'));
});

test('a disabled required tag is a warning and blocks activation readiness', () => {
  const c = cfg();
  const bindings = withBinding('IV4_LOWER_LIMIT', { enabled: false });
  const issues = validateMapping(c.modules, bindings, c.additionalTags);
  assert.ok(codesOf(issues, 'WARNING').includes('REQUIRED_TAG_DISABLED'));
});

test('a source identity that disagrees with the catalogue is refused, with the pressure-specific code for pressures', () => {
  const c = cfg();
  const pressure = validateMapping(c.modules, withBinding('IV6_OUTLET_PRESSURE', { declaredSourceIdentity: 'IV7_OUTLET' }), c.additionalTags);
  assert.ok(codesOf(pressure, 'ERROR').includes('PAIRED_IV_IDENTITY_MISMATCH'));
  const limit = validateMapping(c.modules, withBinding('IV6_LOWER_LIMIT', { declaredSourceIdentity: 'IV7_LOWER_LIMIT' }), c.additionalTags);
  assert.ok(codesOf(limit, 'ERROR').includes('SOURCE_IDENTITY_MISMATCH'));
});

test('an unknown tag, a missing module and an out-of-range channel are each refused', () => {
  const c = cfg();
  assert.ok(codesOf(validateMapping(c.modules, [...c.bindings, { tagName: 'NOT_A_TAG', moduleInstanceId: 'AI-MODULE-01', channel: 1, enabled: true }], c.additionalTags), 'ERROR').includes('UNKNOWN_TAG'));
  assert.ok(codesOf(validateMapping(c.modules, withBinding('IV1_OUTLET_PRESSURE', { moduleInstanceId: 'AI-MODULE-99' }), c.additionalTags), 'ERROR').includes('MODULE_NOT_FOUND'));
  assert.ok(codesOf(validateMapping(c.modules, withBinding('IV1_OUTLET_PRESSURE', { channel: 5 }), c.additionalTags), 'ERROR').includes('CHANNEL_OUT_OF_RANGE'));
});

test('two enabled tags on one Channel are refused; pump and valve pressure never share a Channel', () => {
  const c = cfg();
  const pump = c.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  const shared = withBinding('IV1_OUTLET_PRESSURE', { moduleInstanceId: pump.moduleInstanceId, channel: pump.channel });
  const codes = codesOf(validateMapping(c.modules, shared, c.additionalTags), 'ERROR');
  assert.ok(codes.includes('PUMP_VALVE_CHANNEL_SHARED'));
  assert.ok(codes.includes('DUPLICATE_CHANNEL_BINDING'));
});

test('the same tag bound twice is refused', () => {
  const c = cfg();
  const bindings = [...c.bindings, c.bindings[0]];
  assert.ok(codesOf(validateMapping(c.modules, bindings, c.additionalTags), 'ERROR').includes('DUPLICATE_TAG_BINDING'));
});

test('summariseIssues counts severities and codes deterministically', () => {
  const c = cfg();
  const issues = validateMapping(c.modules, withBinding('IV1_UPPER_LIMIT', { activePolarity: null }), c.additionalTags);
  const s = summariseIssues(issues);
  assert.equal(s.errors, issues.filter((i) => i.severity === 'ERROR').length);
  assert.equal(s.byCode.POLARITY_NOT_EXPLICIT, 1);
});

test('the provider is SIMULATOR only; hardware, production and network names are refused and leave SIMULATOR active', () => {
  assert.equal(activeProvider(), 'SIMULATOR');
  assert.equal(selectProvider('SIMULATOR').ok, true);
  for (const name of ['TEST_HARDWARE', 'PRODUCTION', 'HARDWARE', 'NETWORK_BRIDGE', 'some-device']) {
    const r = selectProvider(name);
    assert.equal(r.ok, false, name);
    assert.equal(r.provider, 'SIMULATOR', name);
    assert.ok(r.refusal.code.startsWith('PROVIDER_'), name);
  }
  assert.deepEqual([...REFUSED_PROVIDER_NAMES].sort(), ['DEVICE', 'HARDWARE', 'PRODUCTION', 'TEST_HARDWARE']);
});
