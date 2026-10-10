// Stage 0.4B-1 — pump and pressure boundary tests (Owner clarification 2026-10-10, corrective review).
//
// AI-002 = Pump Inlet Pressure (PumpInletPressureBar), suction side before the Pump. Diagnostic only.
// AI-003 = Pump Outlet Pressure (PumpOutletPressureBar), Pump discharge. The only pre-P1 Pump-ready source.
// AI-004..AI-011 = IV1..IV8 outlet pressure, ordinal by '#n'. Valve diagnostics only.
//
// Identity is locked: the workbook identifier, source identity and canonical identity cannot be changed or aliased.
// Location is not locked: the Pump Slot/Channel is a workbook default that the Draft may change when compatible.
// No Main Valve I/O tag exists. Synthetic workbook tests run everywhere. Owner-local tests need a real workbook
// copy outside the repository (MAPPING_EXCEL_DEFAULT_PATH) and are skipped otherwise.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { importWorkbook } from '../src/workbookImport.mjs';
import { validateMapping } from '../src/mappingValidation.mjs';
import { DraftSession } from '../src/draftSession.mjs';
import { deriveAddresses } from '../src/addressDerivation.mjs';
import { deriveRackView } from '../src/rack.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';
import {
  listSimulationTags, getTagDef, pressureTagForWj, duplicateCanonicalIdentities, REQUIRED_TAG_NAMES,
  PUMP_READY_GATE_TAG, PUMP_READY_SOURCE_IDENTITY, OWNER_PUMP_SOURCE_TAGS, isPumpRole,
} from '../src/tagCatalogue.mjs';
import { SIGNAL, CHANNEL_TYPE, DIRECTION } from '../src/constants.mjs';
import { buildWorkbook, exampleRows, exampleSeed } from './helpers/syntheticWorkbook.mjs';

// Same arithmetic-only rule as addressing.test.mjs. It is a SYNTHETIC TEST RULE, not a verified process-image rule.
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

const seed = exampleSeed();
const errorCodes = (issues) => issues.filter((i) => i.severity === 'ERROR').map((i) => i.code).sort();
// Pump identity and label issues. An UNCONFIGURED range is a range warning, not an identity issue, and the
// Owner rule is that a range never blocks semantic mapping, so it is excluded here.
const pumpIssues = (issues) => issues.filter((i) => (/PUMP/.test(i.tagName ?? '') || /PUMP/.test(i.code))
  && i.code !== 'ENGINEERING_RANGE_UNCONFIGURED');
const importRows = (rows, options = { bindingSeed: seed }) => importWorkbook(buildWorkbook({ rows }), options);
const replaceRow = (rows, tag, patch) => rows.map((row) => {
  if (row[3] !== tag) return row;
  const copy = row.slice();
  if (patch.signal !== undefined) copy[4] = patch.signal;
  if (patch.ioType !== undefined) copy[5] = patch.ioType;
  return copy;
});
const cfgBindings = () => syntheticExampleConfiguration().bindings;
const cfgModules = () => syntheticExampleConfiguration().modules;
const withPatch = (tagName, patch) => cfgBindings().map((b) => (b.tagName === tagName ? { ...b, ...patch } : b));
const bindingOf = (bindings, tagName) => bindings.find((b) => b.tagName === tagName);

// ---------------------------------------------------------------- catalogue

test('catalogue: AI-002 is the Pump Inlet and AI-003 the Pump Outlet, with the Owner canonical identities and meanings', () => {
  const inlet = getTagDef('PUMP_INLET_PRESSURE');
  const outlet = getTagDef('PUMP_OUTLET_PRESSURE');
  assert.equal(inlet.workbookTag, 'AI-002');
  assert.equal(inlet.displayName, 'Pump Inlet Pressure');
  assert.equal(inlet.canonicalIdentity, 'PumpInletPressureBar');
  assert.equal(inlet.sourceIdentity, 'PUMP_INLET');
  assert.equal(inlet.readinessRole, 'PUMP_INLET_DIAGNOSTIC', 'the inlet is a diagnostic, never the Pump-ready gate');
  assert.equal(inlet.engineeringMeaning, 'Header Tank / suction-side pressure before the Pump. Read-only diagnostic and trend only. Not Pump-ready evidence.');
  assert.deepEqual(inlet.permittedUse, ['DIAGNOSTIC', 'TREND'], 'AI-002 is diagnostic and trend only');
  assert.equal(inlet.pumpReadyEvidence, false, 'AI-002 is never Pump-ready evidence');
  assert.equal(inlet.valveDiagnosticEvidence, false, 'AI-002 is never valve diagnostic evidence');
  assert.deepEqual(inlet.automaticUse, { alarm: false, trip: false, interlock: false }, 'AI-002 is not an alarm, trip or interlock input');
  assert.match(inlet.hardwareLimitation, /below approximately 1 bar/, 'the hardware limitation is recorded');
  assert.equal(outlet.workbookTag, 'AI-003');
  assert.equal(outlet.displayName, 'Pump Outlet Pressure');
  assert.equal(outlet.canonicalIdentity, 'PumpOutletPressureBar');
  assert.equal(outlet.sourceIdentity, 'PUMP_OUTLET');
  assert.equal(outlet.readinessRole, 'PUMP_READY_GATE');
  assert.equal(outlet.engineeringMeaning, 'Pump discharge pressure on the main discharge pipe, immediately after the Pump. The only pre-P1 Pump-ready pressure gate.');
  assert.deepEqual(outlet.permittedUse, ['PUMP_READY_GATE']);
  assert.equal(outlet.pumpReadyEvidence, true, 'AI-003 is the Pump-ready evidence');
});

test('catalogue: AI-002 and AI-003 are both 0–40 bar, Owner-confirmed; every other range is unconfigured', () => {
  for (const name of ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']) {
    const range = getTagDef(name).confirmedEngineeringRange;
    assert.deepEqual([range.min, range.max, range.unit], [0, 40, 'bar'], name);
    assert.equal(range.basis, 'OWNER_CONFIRMED_DOMAIN_INFORMATION', name);
  }
  for (const t of listSimulationTags()) {
    if (t.tagName === 'PUMP_INLET_PRESSURE' || t.tagName === 'PUMP_OUTLET_PRESSURE') continue;
    assert.equal(t.confirmedEngineeringRange, null, `${t.tagName} range is UNCONFIGURED`);
  }
});

test('catalogue: the Pump-ready gate is the Pump Outlet only, and the workbook identifiers are fixed', () => {
  assert.equal(PUMP_READY_GATE_TAG, 'PUMP_OUTLET_PRESSURE');
  assert.equal(PUMP_READY_SOURCE_IDENTITY, 'PUMP_OUTLET');
  assert.equal(getTagDef(PUMP_READY_GATE_TAG).workbookTag, 'AI-003');
  assert.deepEqual({ ...OWNER_PUMP_SOURCE_TAGS }, { PUMP_INLET_PRESSURE: 'AI-002', PUMP_OUTLET_PRESSURE: 'AI-003' });
  assert.equal(Object.isFrozen(OWNER_PUMP_SOURCE_TAGS), true);
});

test('catalogue: no Main Valve I/O tag exists, so none is required and none can be aliased to AI-003', () => {
  for (const t of listSimulationTags()) {
    assert.equal(/MAIN_VALVE/.test(t.tagName) || /MAIN_VALVE/.test(t.sourceIdentity) || /MAIN_VALVE/.test(t.role), false, t.tagName);
  }
  assert.equal(getTagDef('MAIN_VALVE_OUTLET_PRESSURE'), null);
  assert.equal(REQUIRED_TAG_NAMES.includes('MAIN_VALVE_OUTLET_PRESSURE'), false);
  assert.equal(REQUIRED_TAG_NAMES.length, 26, '2 pump pressures + 8 IVn pressures + 16 IVn limits');
});

test('catalogue: canonical identities are unique, and no pump entry carries an IV pair index', () => {
  const all = listSimulationTags();
  assert.deepEqual(duplicateCanonicalIdentities(all), []);
  for (const t of all) {
    if (isPumpRole(t.role)) assert.equal(t.pairIndex, null, t.tagName);
  }
});

test('catalogue: each IVn outlet pressure pairs with WJn by ordinal and source identity', () => {
  for (let n = 1; n <= 8; n += 1) {
    const def = getTagDef(`IV${n}_OUTLET_PRESSURE`);
    assert.equal(def.pairIndex, n, `IV${n} pairIndex`);
    assert.equal(def.sourceIdentity, `IV${n}_OUTLET`);
    assert.equal(pressureTagForWj(n), `IV${n}_OUTLET_PRESSURE`);
  }
  for (const n of [0, 9, 1.5]) assert.throws(() => pressureTagForWj(n), /WJ/, `WJ${n} has no IV pairing`);
});

test('parity: PumpReady compares the sample source to PumpOutletSource (= PUMP_OUTLET), and the Runtime names no inlet source', () => {
  const pressure = readFileSync(new URL('../../application/Runtime/Sequencing/SequencingPressure.cs', import.meta.url), 'utf8');
  assert.equal(/PumpOutletSource\s*=\s*"([A-Z_]+)"/.exec(pressure)?.[1], PUMP_READY_SOURCE_IDENTITY);
  assert.match(pressure, /PumpReady\(.*\)\s*=>\s*\s*Valid\(sample\) && sample!\.SourceId == PressureSample\.PumpOutletSource/s);
  assert.equal(pressure.includes('PUMP_INLET'), false, 'the Runtime pressure gate names no inlet source');
  assert.equal(pressure.includes('PumpInletSource'), false, 'no inlet source constant exists in the Runtime');
});

test('parity: the Runtime kernel admits only PUMP_OUTLET samples as pump pressure (AI-002 never enters the gate)', () => {
  const kernel = readFileSync(new URL('../../application/Runtime/Sequencing/SequencingKernel.cs', import.meta.url), 'utf8');
  assert.match(kernel, /e\.Pressure is \{ SourceId: PressureSample\.PumpOutletSource \} measured/);
});

// ---------------------------------------------------------------- import

test('import: AI-002 and AI-003 bind to their own channels at the workbook default, with the exact source text kept', () => {
  const r = importRows(exampleRows(), {});
  const inlet = bindingOf(r.bindings, 'PUMP_INLET_PRESSURE');
  const outlet = bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE');
  assert.equal(inlet.sourceWorkbookTag, 'AI-002');
  assert.equal(outlet.sourceWorkbookTag, 'AI-003');
  assert.deepEqual([inlet.moduleInstanceId, inlet.channel], ['AI-MODULE-01', 1]);
  assert.deepEqual([outlet.moduleInstanceId, outlet.channel], ['AI-MODULE-01', 2]);
  assert.equal(inlet.declaredSourceIdentity, 'PUMP_INLET');
  assert.equal(outlet.declaredSourceIdentity, 'PUMP_OUTLET');
  assert.deepEqual(inlet.engineering, { min: 0, max: 40, unit: 'bar' }, 'inlet range is the Owner-confirmed 0–40 bar');
  assert.deepEqual(outlet.engineering, { min: 0, max: 40, unit: 'bar' });
  assert.equal(inlet.sourceDescription, 'Example pressure transmitter - pump inlet', 'workbook text kept as evidence');
  assert.equal(outlet.sourceDescription, 'Example pressure transmitter - pump outlet');
});

test('import: the pump measurements bind from the Owner table even with no seed, and never to each other', () => {
  const r = importRows(exampleRows(), {});
  assert.equal(r.bindings.filter((b) => b.sourceWorkbookTag === 'AI-002').length, 1);
  assert.equal(r.bindings.filter((b) => b.sourceWorkbookTag === 'AI-003').length, 1);
});

test('import: the synthetic workbook gives no ERROR at all, because no invented Main Valve row is required', () => {
  const r = importRows(exampleRows());
  assert.deepEqual(errorCodes(r.issues), []);
});

test('import: no PUMP warning is raised when the description differs from the display name and the identifier matches the Owner table', () => {
  const rows = replaceRow(replaceRow(exampleRows(), 'AI-002', { signal: 'Pressure Transmitter' }), 'AI-003',
    { signal: 'Pressure Transmitter Main Valve Outlet' });
  const r = importRows(rows);
  assert.deepEqual(pumpIssues(r.issues), [], 'no pump issue of any severity');
  assert.equal(bindingOf(r.bindings, 'PUMP_INLET_PRESSURE').sourceWorkbookTag, 'AI-002');
  assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
  assert.equal(bindingOf(r.bindings, 'PUMP_INLET_PRESSURE').sourceDescription, 'Pressure Transmitter', 'exact text kept');
  assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE').sourceDescription, 'Pressure Transmitter Main Valve Outlet');
});

test('import: a description that names the other pump side is refused, because it conflicts with the Owner mapping', () => {
  const inletNamedOutlet = importRows(replaceRow(exampleRows(), 'AI-002', { signal: 'Example pressure transmitter - pump outlet' }));
  assert.ok(inletNamedOutlet.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(bindingOf(inletNamedOutlet.bindings, 'PUMP_INLET_PRESSURE'), undefined);
  const outletNamedInlet = importRows(replaceRow(exampleRows(), 'AI-003', { signal: 'Example pressure transmitter - pump inlet' }));
  assert.ok(outletNamedInlet.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' && i.tagName === 'PUMP_OUTLET_PRESSURE'));
  assert.equal(bindingOf(outletNamedInlet.bindings, 'PUMP_OUTLET_PRESSURE'), undefined);
});

test('import: a pump row whose text carries an IV ordinal (#n) is refused, so it can never resolve to an IV', () => {
  const r = importRows(replaceRow(exampleRows(), 'AI-003', { signal: 'Pressure transmitter - pump outlet #3' }));
  assert.ok(r.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' && i.tagName === 'PUMP_OUTLET_PRESSURE'));
  assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE'), undefined);
});

test('import: a pump row that is not an analog pressure row is refused, with no binding', () => {
  const r = importRows(replaceRow(exampleRows(), 'AI-002', { ioType: 'DI (24 VDC.)' }));
  assert.ok(r.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(bindingOf(r.bindings, 'PUMP_INLET_PRESSURE'), undefined);
});

test('import: a seed that gives the pump outlet the inlet source is refused, and no binding is created for it', () => {
  const bad = { ...seed, PUMP_OUTLET_PRESSURE: { source: 'AI-002', declaredSourceIdentity: 'PUMP_OUTLET' } };
  const r = importRows(exampleRows(), { bindingSeed: bad });
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_MISMATCH' && i.tagName === 'PUMP_OUTLET_PRESSURE'));
  assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE'), undefined, 'no partial or substituted pump binding');
  assert.equal(bindingOf(r.bindings, 'PUMP_INLET_PRESSURE').sourceWorkbookTag, 'AI-002', 'the inlet keeps its own row');
});

test('import: a seed that swaps the inlet to the outlet row is refused, with no alias', () => {
  const bad = { ...seed, PUMP_INLET_PRESSURE: { source: 'AI-003', declaredSourceIdentity: 'PUMP_INLET' } };
  const r = importRows(exampleRows(), { bindingSeed: bad });
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_MISMATCH' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(bindingOf(r.bindings, 'PUMP_INLET_PRESSURE'), undefined);
  assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
});

test('import: a missing AI-002 row is refused and the outlet is NOT used as a fallback for the inlet', () => {
  const r = importRows(exampleRows().filter((row) => row[3] !== 'AI-002'));
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_NOT_FOUND' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(bindingOf(r.bindings, 'PUMP_INLET_PRESSURE'), undefined);
  assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
});

test('import: a seeded Main Valve key is refused as UNKNOWN_TAG at import, creates no binding, and is not aliased to AI-003', () => {
  const r = importRows(exampleRows(), { bindingSeed: { ...seed, MAIN_VALVE_OUTLET_PRESSURE: { source: 'AI-003', declaredSourceIdentity: 'MAIN_VALVE_OUTLET' } } });
  assert.ok(r.issues.some((i) => i.code === 'UNKNOWN_TAG' && i.tagName === 'MAIN_VALVE_OUTLET_PRESSURE'));
  assert.equal(r.bindings.some((x) => /MAIN_VALVE/.test(x.tagName)), false);
  assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
  // Defence in depth: validation still refuses the tag if a binding for it ever reaches the validator.
  const forged = { ...bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE'), tagName: 'MAIN_VALVE_OUTLET_PRESSURE' };
  const issues = validateMapping(r.modules, [...r.bindings, forged], r.additionalTags);
  assert.ok(issues.some((i) => i.code === 'UNKNOWN_TAG' && i.tagName === 'MAIN_VALVE_OUTLET_PRESSURE'));
});

// ---------------------------------------------------------------- validation (semantic boundaries)

test('validation: the synthetic example is valid with no ERROR and carries no pressure-boundary error', () => {
  const issues = validateMapping(cfgModules(), cfgBindings());
  assert.deepEqual(errorCodes(issues), []);
});

test('validation: a Pump Outlet bound to the AI-002 measurement is refused, and the gate reads the wrong source', () => {
  const codes = errorCodes(validateMapping(cfgModules(), withPatch('PUMP_OUTLET_PRESSURE', { sourceWorkbookTag: 'AI-002' })));
  assert.ok(codes.includes('PUMP_SOURCE_MISMATCH'));
  assert.ok(codes.includes('PUMP_READY_GATE_SOURCE_INVALID'), 'the Pump-ready gate must read AI-003 only');
  assert.ok(codes.includes('PUMP_INLET_OUTLET_ALIAS'), 'AI-002 and AI-003 must not resolve to one physical measurement');
});

test('validation: the Pump-ready gate bound to an IV pressure measurement is refused', () => {
  const codes = errorCodes(validateMapping(cfgModules(), withPatch('PUMP_OUTLET_PRESSURE', { sourceWorkbookTag: 'EX-AI-03' })));
  assert.ok(codes.includes('PUMP_READY_GATE_SOURCE_INVALID'));
  assert.ok(codes.includes('PUMP_IV_CROSS_BINDING'));
});

test('validation: an IVn pressure that resolves to the Pump Outlet measurement is refused', () => {
  assert.ok(errorCodes(validateMapping(cfgModules(), withPatch('IV1_OUTLET_PRESSURE', { sourceWorkbookTag: 'AI-003' }))).includes('PUMP_IV_CROSS_BINDING'));
});

test('validation: a pump tag that declares an IV identity is refused (PAIRED_IV_IDENTITY_MISMATCH)', () => {
  assert.ok(errorCodes(validateMapping(cfgModules(), withPatch('PUMP_INLET_PRESSURE', { declaredSourceIdentity: 'IV1_OUTLET' })))
    .includes('PAIRED_IV_IDENTITY_MISMATCH'));
});

test('validation: a pump tag on the same enabled Channel as the other pump tag is refused', () => {
  const outlet = cfgBindings().find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  const codes = errorCodes(validateMapping(cfgModules(), withPatch('PUMP_INLET_PRESSURE', { moduleInstanceId: outlet.moduleInstanceId, channel: outlet.channel })));
  assert.ok(codes.includes('DUPLICATE_CHANNEL_BINDING'));
});

test('validation: a pump tag on the same enabled Channel as an IV pressure is refused (pump and valve never share)', () => {
  const pump = cfgBindings().find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  const codes = errorCodes(validateMapping(cfgModules(), withPatch('IV1_OUTLET_PRESSURE', { moduleInstanceId: pump.moduleInstanceId, channel: pump.channel })));
  assert.ok(codes.includes('PUMP_VALVE_CHANNEL_SHARED'));
  assert.ok(codes.includes('DUPLICATE_CHANNEL_BINDING'));
});

test('validation: a pump tag on a digital channel is refused as a type mismatch', () => {
  const di = cfgBindings().find((b) => b.tagName === 'IV1_UPPER_LIMIT');
  const codes = errorCodes(validateMapping(cfgModules(), withPatch('PUMP_INLET_PRESSURE', { moduleInstanceId: di.moduleInstanceId, channel: di.channel })));
  assert.ok(codes.includes('PRESSURE_TO_DIGITAL_REFUSED'));
});

test('validation: a pump tag beyond the module channel capacity is refused', () => {
  const codes = errorCodes(validateMapping(cfgModules(), withPatch('PUMP_INLET_PRESSURE', { channel: 5 })));
  assert.ok(codes.includes('CHANNEL_OUT_OF_RANGE'));
});

test('validation: a duplicated canonical runtime identity is refused', () => {
  const extra = Object.freeze({
    tagName: 'EXTRA_PRESSURE', role: 'OTHER_PRESSURE', direction: DIRECTION.INPUT, channelType: CHANNEL_TYPE.ANALOG,
    signal: SIGNAL.CURRENT_4_20_MA, sourceIdentity: 'EXTRA', pairIndex: null, canonicalIdentity: 'PumpOutletPressureBar',
  });
  const bindings = [...cfgBindings(), {
    tagName: 'EXTRA_PRESSURE', moduleInstanceId: 'AI-MODULE-03', channel: 4, enabled: true, engineering: null,
    activePolarity: null, contactType: null, declaredSourceIdentity: 'EXTRA',
  }];
  assert.ok(errorCodes(validateMapping(cfgModules(), bindings, { EXTRA_PRESSURE: extra })).includes('CANONICAL_IDENTITY_DUPLICATED'));
});

test('validation: without a pump inlet binding, no other tag fills in for it (no fallback)', () => {
  const issues = validateMapping(cfgModules(), cfgBindings().filter((b) => b.tagName !== 'PUMP_INLET_PRESSURE'));
  assert.ok(issues.some((i) => i.code === 'REQUIRED_TAG_MISSING' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(issues.some((i) => i.code === 'PUMP_READY_GATE_SOURCE_INVALID'), false, 'the gate is unaffected by a missing inlet');
});

test('validation: without a pump outlet binding, the gate is missing and nothing is substituted from AI-002 or an IV', () => {
  const remaining = cfgBindings().filter((b) => b.tagName !== 'PUMP_OUTLET_PRESSURE');
  const issues = validateMapping(cfgModules(), remaining);
  assert.ok(issues.some((i) => i.code === 'REQUIRED_TAG_MISSING' && i.tagName === 'PUMP_OUTLET_PRESSURE'));
  assert.equal(remaining.some((b) => b.sourceWorkbookTag === 'AI-003'), false, 'AI-003 is not re-bound under another name');
});

// ---------------------------------------------------------------- Draft (location editable, identity locked)

test('Draft: the Pump Inlet Slot/Channel can be changed to a compatible free channel, and the derived address follows', () => {
  const session = new DraftSession(syntheticExampleConfiguration(), { rules: SYNTHETIC_TEST_RULE });
  const before = session.impactPreview().tags.find((t) => t.tagName === 'PUMP_INLET_PRESSURE');
  assert.equal(before.oldAddress, 0);
  const moved = session.setBinding('PUMP_INLET_PRESSURE', { moduleInstanceId: 'AI-MODULE-03', channel: 3 });
  assert.equal(moved.ok, true);
  const b = bindingOf(session.snapshot().bindings, 'PUMP_INLET_PRESSURE');
  assert.deepEqual([b.moduleInstanceId, b.channel, b.sourceWorkbookTag], ['AI-MODULE-03', 3, 'AI-002']);
  const impact = session.impactPreview().tags.find((t) => t.tagName === 'PUMP_INLET_PRESSURE');
  assert.equal(impact.classification, 'ADDRESS_CHANGED');
  // AI-MODULE-03 is the third analog module, words 8..11; channel 3 is word 10, bit 160.
  assert.equal(impact.newAddress, 160);
  const derived = deriveAddresses(deriveRackView(session.snapshot().modules), session.snapshot().bindings, SYNTHETIC_TEST_RULE)
    .entries.find((e) => e.tagName === 'PUMP_INLET_PRESSURE');
  assert.equal(derived.bitOffsetAbsolute, 160);
  assert.equal(session.validate().status, 'VALID', 'a compatible move stays valid');
});

test('Draft: moving the Pump Inlet onto the Pump Outlet Channel is rejected by validation (physical duplicate)', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const outlet = bindingOf(session.snapshot().bindings, 'PUMP_OUTLET_PRESSURE');
  assert.equal(session.setBinding('PUMP_INLET_PRESSURE', { moduleInstanceId: outlet.moduleInstanceId, channel: outlet.channel }).ok, true);
  const v = session.validate();
  assert.equal(v.status, 'INVALID');
  assert.ok(errorCodes(v.mappingIssues).includes('DUPLICATE_CHANNEL_BINDING'));
});

test('Draft: moving a pump onto an IV pressure Channel is rejected by validation (pump and valve never share)', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const iv1 = bindingOf(session.snapshot().bindings, 'IV1_OUTLET_PRESSURE');
  session.setBinding('PUMP_OUTLET_PRESSURE', { moduleInstanceId: iv1.moduleInstanceId, channel: iv1.channel });
  assert.ok(errorCodes(session.validate().mappingIssues).includes('PUMP_VALVE_CHANNEL_SHARED'));
});

test('Draft: a pump moved to an incompatible channel type or beyond capacity is rejected by validation', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const di = bindingOf(session.snapshot().bindings, 'IV1_UPPER_LIMIT');
  session.setBinding('PUMP_INLET_PRESSURE', { moduleInstanceId: di.moduleInstanceId, channel: di.channel });
  assert.ok(errorCodes(session.validate().mappingIssues).includes('PRESSURE_TO_DIGITAL_REFUSED'));
  session.undo();
  session.setBinding('PUMP_INLET_PRESSURE', { channel: 5 });
  assert.ok(errorCodes(session.validate().mappingIssues).includes('CHANNEL_OUT_OF_RANGE'));
});

test('Draft: the pump source identity cannot be edited, so AI-002 and AI-003 cannot be swapped or aliased', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  const before = JSON.stringify(session.snapshot());
  for (const patch of [{ sourceWorkbookTag: 'AI-003' }, { declaredSourceIdentity: 'PUMP_OUTLET' }]) {
    const r = session.setBinding('PUMP_INLET_PRESSURE', patch);
    assert.equal(r.ok, false);
    assert.equal(r.refusal.code, 'FIELD_NOT_EDITABLE');
  }
  assert.equal(JSON.stringify(session.snapshot()), before, 'a refused edit leaves the Draft unchanged');
});

test('Draft: a pump Slot/Channel change is undoable', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  session.setBinding('PUMP_INLET_PRESSURE', { moduleInstanceId: 'AI-MODULE-03', channel: 3 });
  session.undo();
  const b = bindingOf(session.snapshot().bindings, 'PUMP_INLET_PRESSURE');
  assert.deepEqual([b.moduleInstanceId, b.channel], ['AI-MODULE-01', 1]);
});

// ---------------------------------------------------------------- Owner-local (real workbook, outside the repository)

const OWNER_LOCAL = process.env.MAPPING_EXCEL_DEFAULT_PATH
  ? false
  : 'NOT VERIFIED IN ARENA: set MAPPING_EXCEL_DEFAULT_PATH to a copy of the workbook outside the repository';

test('Owner-local: the authoritative workbook binds AI-002 and AI-003 independently, with no Main Valve and no pump warning',
  { skip: OWNER_LOCAL },
  () => {
    const r = importWorkbook(readFileSync(process.env.MAPPING_EXCEL_DEFAULT_PATH));
    assert.equal(bindingOf(r.bindings, 'PUMP_INLET_PRESSURE').sourceWorkbookTag, 'AI-002');
    assert.equal(bindingOf(r.bindings, 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
    assert.equal(r.bindings.some((b) => /MAIN_VALVE/.test(b.tagName)), false);
    assert.deepEqual(pumpIssues(r.issues), [], 'no pump issue of any severity');
  });

test('Owner-local: the workbook carries the Owner ordinal rules for AI-004..AI-011 and DI-021..DI-036 (read-only check)',
  { skip: OWNER_LOCAL },
  () => {
    const r = importWorkbook(readFileSync(process.env.MAPPING_EXCEL_DEFAULT_PATH));
    const byTag = new Map(r.records.filter((x) => x.tag).map((x) => [x.tag, x]));
    const ordinal = (rec) => { const hits = [...(rec?.signal ?? '').matchAll(/#/g)].length; const m = /#\s*(\d+)(?!\d)/.exec(rec?.signal ?? ''); return { hits, n: m ? Number(m[1]) : null }; };
    for (let n = 1; n <= 8; n += 1) {
      const tag = `AI-${String(n + 3).padStart(3, '0')}`;
      const rec = byTag.get(tag);
      assert.ok(rec && /^AI\b/i.test(rec.ioType) && /pressure\s+transmitter/i.test(rec.signal), `${tag} is an AI pressure transmitter`);
      assert.deepEqual(ordinal(rec), { hits: 1, n }, `${tag} carries #${n}`);
    }
    for (let n = 1; n <= 8; n += 1) {
      for (const [group, base] of [['LOWER', 20], ['UPPER', 28]]) {
        const tag = `DI-${String(base + n).padStart(3, '0')}`;
        const rec = byTag.get(tag);
        assert.equal(ordinal(rec).n, n, `${tag} carries #${n}`);
        assert.equal(new RegExp(`\\b${group}\\b`, 'i').test(rec.signal), true, `${tag} is ${group}`);
      }
    }
  });
