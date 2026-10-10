// Stage 0.4B-1 — pump and pressure boundary tests (Owner clarification, 2026-10-10).
//
// AI-002 = Pump Inlet Pressure (PumpInletPressureBar), AI-003 = Pump Outlet Pressure (PumpOutletPressureBar).
// AI-004..AI-011 = IV1..IV8 outlet pressure. These are distinct physical measurements. The pre-P1 Pump-ready
// gate reads AI-003 only. Synthetic workbook only, plus one Owner-local check gated on an env path.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { importWorkbook } from '../src/workbookImport.mjs';
import { validateMapping } from '../src/mappingValidation.mjs';
import { DraftSession } from '../src/draftSession.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';
import {
  listSimulationTags, getTagDef, pressureTagForWj, duplicateCanonicalIdentities, REQUIRED_TAG_NAMES,
  PUMP_READY_GATE_TAG, PUMP_READY_SOURCE_IDENTITY, OWNER_PUMP_SOURCE_TAGS, isPumpRole,
} from '../src/tagCatalogue.mjs';
import { SIGNAL, CHANNEL_TYPE, DIRECTION } from '../src/constants.mjs';
import { buildWorkbook, exampleRows, exampleSeed } from './helpers/syntheticWorkbook.mjs';

const seed = exampleSeed();
const errorCodes = (issues) => issues.filter((i) => i.severity === 'ERROR').map((i) => i.code).sort();
const importRows = (rows, options = { bindingSeed: seed }) => importWorkbook(buildWorkbook({ rows }), options);

// ---------------------------------------------------------------- catalogue

test('catalogue: AI-002 is the Pump Inlet and AI-003 the Pump Outlet, with the Owner canonical identities', () => {
  const inlet = getTagDef('PUMP_INLET_PRESSURE');
  const outlet = getTagDef('PUMP_OUTLET_PRESSURE');
  assert.equal(inlet.displayName, 'Pump Inlet Pressure');
  assert.equal(inlet.canonicalIdentity, 'PumpInletPressureBar');
  assert.equal(inlet.workbookTag, 'AI-002');
  assert.equal(inlet.sourceIdentity, 'PUMP_INLET');
  assert.equal(inlet.readinessRole, 'PUMP_INLET_DIAGNOSTIC', 'the inlet is a diagnostic, never the Pump-ready gate');
  assert.equal(inlet.confirmedEngineeringRange, null, 'the inlet range is UNCONFIGURED');
  assert.equal(outlet.displayName, 'Pump Outlet Pressure');
  assert.equal(outlet.canonicalIdentity, 'PumpOutletPressureBar');
  assert.equal(outlet.workbookTag, 'AI-003');
  assert.equal(outlet.sourceIdentity, 'PUMP_OUTLET');
  assert.equal(outlet.readinessRole, 'PUMP_READY_GATE');
  assert.deepEqual([outlet.confirmedEngineeringRange.min, outlet.confirmedEngineeringRange.max, outlet.confirmedEngineeringRange.unit], [0, 40, 'bar']);
});

test('catalogue: the Pump-ready gate is the Pump Outlet only, and is not configurable', () => {
  assert.equal(PUMP_READY_GATE_TAG, 'PUMP_OUTLET_PRESSURE');
  assert.equal(PUMP_READY_SOURCE_IDENTITY, 'PUMP_OUTLET');
  assert.equal(getTagDef(PUMP_READY_GATE_TAG).workbookTag, OWNER_PUMP_SOURCE_TAGS.PUMP_OUTLET_PRESSURE);
  assert.equal(OWNER_PUMP_SOURCE_TAGS.PUMP_INLET_PRESSURE, 'AI-002');
  assert.equal(OWNER_PUMP_SOURCE_TAGS.PUMP_OUTLET_PRESSURE, 'AI-003');
  assert.equal(Object.isFrozen(OWNER_PUMP_SOURCE_TAGS), true);
});

test('catalogue: canonical identities are unique, and no pump or main-valve entry carries an IV pair index', () => {
  const all = listSimulationTags();
  assert.deepEqual(duplicateCanonicalIdentities(all), []);
  for (const t of all) {
    if (isPumpRole(t.role) || t.role === 'MAIN_VALVE_PRESSURE') assert.equal(t.pairIndex, null, t.tagName);
  }
  assert.equal(getTagDef('MAIN_VALVE_OUTLET_PRESSURE').workbookTag, null, 'the main valve has no workbook measurement');
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

test('parity: PumpReady compares the sample source to PressureSample.PumpOutletSource (= mapping PUMP_OUTLET), and the Runtime names no inlet source', () => {
  const src = readFileSync(new URL('../../application/Runtime/Sequencing/SequencingPressure.cs', import.meta.url), 'utf8');
  const outletSource = /PumpOutletSource\s*=\s*"([A-Z_]+)"/.exec(src)?.[1];
  assert.equal(outletSource, PUMP_READY_SOURCE_IDENTITY);
  assert.match(src, /PumpReady\(.*\)\s*=>\s*\s*Valid\(sample\) && sample!\.SourceId == PressureSample\.PumpOutletSource/s);
  assert.equal(src.includes('PUMP_INLET'), false, 'the Runtime pressure gate names no inlet source');
});

// ---------------------------------------------------------------- import

test('import: AI-002 binds Pump Inlet and AI-003 binds Pump Outlet, on distinct channels, from the Owner table', () => {
  const r = importRows(exampleRows(), { bindingSeed: {} });
  const inlet = r.bindings.find((b) => b.tagName === 'PUMP_INLET_PRESSURE');
  const outlet = r.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  assert.equal(inlet.sourceWorkbookTag, 'AI-002');
  assert.equal(outlet.sourceWorkbookTag, 'AI-003');
  assert.deepEqual([inlet.moduleInstanceId, inlet.channel], ['AI-MODULE-01', 1]);
  assert.deepEqual([outlet.moduleInstanceId, outlet.channel], ['AI-MODULE-01', 2]);
  assert.equal(inlet.declaredSourceIdentity, 'PUMP_INLET');
  assert.equal(outlet.declaredSourceIdentity, 'PUMP_OUTLET');
  assert.equal(inlet.engineering, null, 'inlet range UNCONFIGURED');
  assert.deepEqual(outlet.engineering, { min: 0, max: 40, unit: 'bar' });
});

test('import: the pump measurements bind even with no seed, and the outlet is never the inlet row', () => {
  const r = importRows(exampleRows(), { bindingSeed: {} });
  assert.ok(r.bindings.some((b) => b.tagName === 'PUMP_INLET_PRESSURE'));
  assert.ok(r.bindings.some((b) => b.tagName === 'PUMP_OUTLET_PRESSURE'));
  assert.equal(r.bindings.filter((b) => b.sourceWorkbookTag === 'AI-002').length, 1);
  assert.equal(r.bindings.filter((b) => b.sourceWorkbookTag === 'AI-003').length, 1);
});

test('import: the synthetic workbook gives no ERROR other than the main valve, which has no source', () => {
  const r = importRows(exampleRows());
  const errors = r.issues.filter((i) => i.severity === 'ERROR');
  assert.deepEqual(errors.map((i) => `${i.code}:${i.tagName}`), ['REQUIRED_TAG_MISSING:MAIN_VALVE_OUTLET_PRESSURE']);
  assert.equal(r.bindings.some((b) => b.tagName === 'MAIN_VALVE_OUTLET_PRESSURE'), false, 'the main valve is not aliased to AI-003');
});

test('import: a seed that gives the pump outlet the inlet source is refused, and no binding is created for it', () => {
  const bad = { ...seed, PUMP_OUTLET_PRESSURE: { source: 'AI-002', declaredSourceIdentity: 'PUMP_OUTLET' } };
  const r = importRows(exampleRows(), { bindingSeed: bad });
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_MISMATCH' && i.tagName === 'PUMP_OUTLET_PRESSURE'));
  const outlet = r.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  assert.equal(outlet, undefined, 'no partial or substituted pump binding');
  assert.equal(r.bindings.find((b) => b.tagName === 'PUMP_INLET_PRESSURE').sourceWorkbookTag, 'AI-002', 'the inlet keeps its own row');
});

test('import: a seed that swaps the inlet to the outlet row is refused, with no alias', () => {
  const bad = { ...seed, PUMP_INLET_PRESSURE: { source: 'AI-003', declaredSourceIdentity: 'PUMP_INLET' } };
  const r = importRows(exampleRows(), { bindingSeed: bad });
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_MISMATCH' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(r.bindings.some((b) => b.tagName === 'PUMP_INLET_PRESSURE'), false);
  assert.equal(r.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
});

test('import: a pump row whose text carries an IV ordinal (#n) is refused, so it can never resolve to an IV', () => {
  const r = importRows(exampleRows().map((row) => {
    if (row[3] !== 'AI-003') return row;
    const copy = row.slice(); copy[4] = 'Pressure transmitter - pump outlet #3'; return copy;
  }));
  assert.ok(r.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' && i.tagName === 'PUMP_OUTLET_PRESSURE'));
  assert.equal(r.bindings.some((b) => b.tagName === 'PUMP_OUTLET_PRESSURE'), false);
});

test('import: a pump row that is not an analog pressure transmitter is refused, with no binding', () => {
  const r = importRows(exampleRows().map((row) => {
    if (row[3] !== 'AI-002') return row;
    const copy = row.slice(); copy[5] = 'DI (24 VDC.)'; return copy;
  }));
  assert.ok(r.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(r.bindings.some((b) => b.tagName === 'PUMP_INLET_PRESSURE'), false);
});

test('import: a missing AI-002 row is refused and the outlet is NOT used as a fallback for the inlet', () => {
  const rows = exampleRows().filter((row) => row[3] !== 'AI-002');
  const r = importRows(rows);
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_NOT_FOUND' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(r.bindings.some((b) => b.tagName === 'PUMP_INLET_PRESSURE'), false);
  assert.equal(r.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
});

test('import: a workbook text that differs from the Owner wording is a WARNING for the Owner to confirm, not a silent change', () => {
  const r = importRows(exampleRows().map((row) => {
    if (row[3] === 'AI-003') { const c = row.slice(); c[4] = 'Pressure Transmitter Main Valve Outlet'; return c; }
    if (row[3] === 'AI-002') { const c = row.slice(); c[4] = 'Pressure Transmitter'; return c; }
    return row;
  }));
  const warnings = r.issues.filter((i) => i.code === 'PUMP_SOURCE_LABEL_UNCONFIRMED').map((i) => i.tagName).sort();
  assert.deepEqual(warnings, ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']);
  assert.ok(r.bindings.some((b) => b.tagName === 'PUMP_OUTLET_PRESSURE'), 'the binding still follows the Owner table');
});

// ---------------------------------------------------------------- validation (boundaries)

const cfgBindings = () => syntheticExampleConfiguration().bindings;
const withPatch = (tagName, patch) => cfgBindings().map((b) => (b.tagName === tagName ? { ...b, ...patch } : b));
const cfgModules = () => syntheticExampleConfiguration().modules;

test('validation: a Pump Outlet bound to the AI-002 measurement is refused, and the gate reads the wrong source', () => {
  const issues = validateMapping(cfgModules(), withPatch('PUMP_OUTLET_PRESSURE', { sourceWorkbookTag: 'AI-002' }));
  const codes = errorCodes(issues);
  assert.ok(codes.includes('PUMP_SOURCE_MISMATCH'));
  assert.ok(codes.includes('PUMP_READY_GATE_SOURCE_INVALID'), 'the Pump-ready gate must read AI-003 only');
  assert.ok(codes.includes('PUMP_INLET_OUTLET_ALIAS'), 'AI-002 and AI-003 must not resolve to one physical binding');
});

test('validation: the Pump-ready gate that resolves to an IV pressure measurement is refused', () => {
  const issues = validateMapping(cfgModules(), withPatch('PUMP_OUTLET_PRESSURE', { sourceWorkbookTag: 'EX-AI-03' }));
  const codes = errorCodes(issues);
  assert.ok(codes.includes('PUMP_READY_GATE_SOURCE_INVALID'));
  assert.ok(codes.includes('PUMP_IV_CROSS_BINDING'), 'a pump tag may not share an IV pressure measurement');
});

test('validation: an IVn pressure that resolves to the Pump Outlet measurement is refused', () => {
  const issues = validateMapping(cfgModules(), withPatch('IV1_OUTLET_PRESSURE', { sourceWorkbookTag: 'AI-003' }));
  assert.ok(errorCodes(issues).includes('PUMP_IV_CROSS_BINDING'));
});

test('validation: a pump tag that declares an IV identity is refused (PAIRED_IV_IDENTITY_MISMATCH)', () => {
  const issues = validateMapping(cfgModules(), withPatch('PUMP_INLET_PRESSURE', { declaredSourceIdentity: 'IV1_OUTLET' }));
  assert.ok(errorCodes(issues).includes('PAIRED_IV_IDENTITY_MISMATCH'));
});

test('validation: a pump inlet on the outlet channel is refused as a shared channel', () => {
  const outlet = cfgBindings().find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  const issues = validateMapping(cfgModules(), withPatch('PUMP_INLET_PRESSURE', { moduleInstanceId: outlet.moduleInstanceId, channel: outlet.channel }));
  assert.ok(errorCodes(issues).includes('DUPLICATE_CHANNEL_BINDING'));
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
  const issues = validateMapping(cfgModules(), bindings, { EXTRA_PRESSURE: extra });
  assert.ok(errorCodes(issues).includes('CANONICAL_IDENTITY_DUPLICATED'));
});

test('validation: without a pump inlet binding, no other tag fills in for it (no fallback)', () => {
  const issues = validateMapping(cfgModules(), cfgBindings().filter((b) => b.tagName !== 'PUMP_INLET_PRESSURE'));
  assert.ok(issues.some((i) => i.code === 'REQUIRED_TAG_MISSING' && i.tagName === 'PUMP_INLET_PRESSURE'));
  const outlet = issues.some((i) => i.code === 'PUMP_READY_GATE_SOURCE_INVALID');
  assert.equal(outlet, false, 'the Pump-ready gate is unaffected by a missing inlet');
});

test('validation: the synthetic example is clean of pressure-boundary errors', () => {
  const issues = validateMapping(cfgModules(), cfgBindings());
  const boundary = ['PUMP_SOURCE_MISMATCH', 'PUMP_READY_GATE_SOURCE_INVALID', 'PUMP_IV_CROSS_BINDING', 'PUMP_INLET_OUTLET_ALIAS',
    'PHYSICAL_SOURCE_ALIAS', 'PAIRED_IV_IDENTITY_MISMATCH', 'CANONICAL_IDENTITY_DUPLICATED', 'PUMP_READY_GATE_MISCONFIGURED'];
  assert.deepEqual(errorCodes(issues).filter((c) => boundary.includes(c)), []);
});

// ---------------------------------------------------------------- Draft

test('Draft: the Pump Inlet and Pump Outlet channels are fixed; enabling is still editable', () => {
  const session = new DraftSession(syntheticExampleConfiguration());
  for (const tag of ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']) {
    const moved = session.setBinding(tag, { moduleInstanceId: 'AI-MODULE-02' });
    assert.equal(moved.refusal.code, 'WORKBOOK_IDENTITY_FIXED', tag);
  }
  assert.equal(session.setBinding('PUMP_INLET_PRESSURE', { enabled: false }).ok, true);
  assert.equal(session.undoStack.length, 1, 'only the accepted edit enters history');
});

// ---------------------------------------------------------------- Owner-local

test('Owner-local: the authoritative workbook binds AI-002 and AI-003 to the pump measurements, with no alias',
  { skip: process.env.MAPPING_EXCEL_DEFAULT_PATH ? false : 'NOT VERIFIED IN ARENA: set MAPPING_EXCEL_DEFAULT_PATH to a workbook outside the repository' },
  () => {
    const r = importWorkbook(readFileSync(process.env.MAPPING_EXCEL_DEFAULT_PATH), { bindingSeed: {} });
    const inlet = r.bindings.find((b) => b.tagName === 'PUMP_INLET_PRESSURE');
    const outlet = r.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
    assert.equal(inlet.sourceWorkbookTag, 'AI-002');
    assert.equal(outlet.sourceWorkbookTag, 'AI-003');
    assert.equal(r.bindings.some((b) => b.tagName === 'MAIN_VALVE_OUTLET_PRESSURE'), false);
    assert.equal(r.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' || i.code === 'PUMP_SOURCE_NOT_FOUND'), false);
    assert.ok(REQUIRED_TAG_NAMES.includes('PUMP_INLET_PRESSURE'));
  });
