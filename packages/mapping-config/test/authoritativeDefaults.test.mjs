// Stage 0.4B-1 — authoritative default bindings (Owner ruling 2026-10-10).
//
// The 26 default bindings derive from explicit workbook identifiers and Owner ordinal rules, so the import loads
// them with NO seed. The seed is an optional authorised override. These proofs run in Arena on an
// authoritative-shaped SYNTHETIC workbook (no plant data). The Owner-local block reads the real workbook only when
// MAPPING_EXCEL_DEFAULT_PATH points at a copy outside the repository; it never needs MAPPING_BINDING_SEED_PATH.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { importWorkbook } from '../src/workbookImport.mjs';
import { DraftSession } from '../src/draftSession.mjs';
import { deriveAddresses } from '../src/addressDerivation.mjs';
import { deriveRackView } from '../src/rack.mjs';
import { canonicalJson } from '../src/canonical.mjs';
import {
  getTagDef, listSimulationTags, REQUIRED_TAG_NAMES, OWNER_DEFAULT_TAG_NAMES, OWNER_PLACEHOLDER_ROWS,
  ownerDefaultSourceFor, OWNER_PUMP_SOURCE_TAGS,
} from '../src/tagCatalogue.mjs';
import { buildWorkbook, authoritativeShapedRows } from './helpers/syntheticWorkbook.mjs';

const pad3 = (n) => String(n).padStart(3, '0');
const errorCodes = (issues) => issues.filter((i) => i.severity === 'ERROR').map((i) => i.code).sort();
const byTag = (bindings, tagName) => bindings.find((b) => b.tagName === tagName);
const importRows = (rows, options) => importWorkbook(buildWorkbook({ rows }), options);
// The Owner ordinal rule, written out independently of the catalogue, so a catalogue error cannot hide itself.
const OWNER_RULE = new Map([
  ['PUMP_INLET_PRESSURE', 'AI-002'],
  ['PUMP_OUTLET_PRESSURE', 'AI-003'],
  ...[1, 2, 3, 4, 5, 6, 7, 8].flatMap((n) => [
    [`IV${n}_OUTLET_PRESSURE`, `AI-${pad3(n + 3)}`],
    [`IV${n}_LOWER_LIMIT`, `DI-${pad3(20 + n)}`],
    [`IV${n}_UPPER_LIMIT`, `DI-${pad3(28 + n)}`],
  ]),
]);

// ---------------------------------------------------------------- the 26 defaults load without a seed

test('defaults: the authoritative-shaped workbook loads 26 default bindings with NO seed, all enabled, with no ERROR', () => {
  const r = importRows(authoritativeShapedRows());
  // Unbound unambiguous input rows are listed as disabled read-only bindings; the synthetic rack has none.
  assert.equal(r.bindings.length, 26);
  assert.deepEqual(r.bindings.map((b) => b.tagName).sort(), [...OWNER_DEFAULT_TAG_NAMES].sort());
  assert.ok(r.bindings.every((b) => b.enabled === true), 'every default is enabled');
  assert.deepEqual(errorCodes(r.issues), [], 'no ERROR of any kind');
});

test('defaults: no REQUIRED_TAG_MISSING for the 26 explicit identities, and the required set is exactly those 26', () => {
  const r = importRows(authoritativeShapedRows());
  assert.equal(r.issues.filter((i) => i.code === 'REQUIRED_TAG_MISSING').length, 0);
  assert.equal(REQUIRED_TAG_NAMES.length, 26);
  assert.deepEqual([...REQUIRED_TAG_NAMES].sort(), [...OWNER_DEFAULT_TAG_NAMES].sort());
});

test('defaults: each default binds to its explicit identifier under the Owner ordinal rule, at its workbook channel', () => {
  const r = importRows(authoritativeShapedRows());
  for (const [tagName, source] of OWNER_RULE) {
    const b = byTag(r.bindings, tagName);
    assert.ok(b, `${tagName} is bound`);
    assert.equal(b.sourceWorkbookTag, source, `${tagName} <- ${source}`);
    assert.equal(ownerDefaultSourceFor(tagName), source, `catalogue rule for ${tagName}`);
  }
  // The IVn pressure and limits are the same ordinal n.
  assert.equal(byTag(r.bindings, 'IV3_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-006');
  assert.equal(byTag(r.bindings, 'IV3_LOWER_LIMIT').sourceWorkbookTag, 'DI-023');
  assert.equal(byTag(r.bindings, 'IV8_UPPER_LIMIT').sourceWorkbookTag, 'DI-036');
});

test('defaults: limit defaults take the Owner ACTIVE_WHEN_CLOSED rule with the OWNER_RULE basis, and no polarity reaches a pressure', () => {
  const r = importRows(authoritativeShapedRows());
  for (const b of r.bindings) {
    if (/_LIMIT$/.test(b.tagName)) {
      assert.equal(b.activePolarity, 'ACTIVE_WHEN_CLOSED', b.tagName);
      assert.equal(b.polarityBasis, 'OWNER_RULE', b.tagName);
    } else {
      assert.equal(b.activePolarity, null, b.tagName);
    }
  }
});

test('defaults: no pump source reaches an IVn binding, and no IVn source reaches a pump binding', () => {
  const r = importRows(authoritativeShapedRows());
  const ivSources = r.bindings.filter((b) => /^IV/.test(b.tagName)).map((b) => b.sourceWorkbookTag);
  assert.equal(ivSources.includes('AI-002') || ivSources.includes('AI-003'), false);
  const pumpSources = r.bindings.filter((b) => /^PUMP_/.test(b.tagName)).map((b) => b.sourceWorkbookTag);
  assert.deepEqual(pumpSources.sort(), ['AI-002', 'AI-003']);
});

// ---------------------------------------------------------------- the 18 placeholders stay unbound

test('defaults: the 18 placeholder rows (DI-037, DO-031, AI-020..AI-035) are never bound and never listed as tags', () => {
  assert.equal(OWNER_PLACEHOLDER_ROWS.length, 18);
  const r = importRows(authoritativeShapedRows());
  for (const tag of OWNER_PLACEHOLDER_ROWS) {
    assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === tag), false, `${tag} is not a binding source`);
    assert.equal(Object.prototype.hasOwnProperty.call(r.additionalTags, tag), false, `${tag} is not listed as a tag`);
    const rec = r.records.find((x) => x.tag === tag);
    assert.ok(rec, `${tag} is present in the workbook`);
    assert.match(rec.signal, /XXX/, `${tag} is a placeholder row`);
  }
  assert.equal(r.classifications.reservedUnresolved, 17, 'DI-037 and AI-020..AI-035 are reserved, not bindable');
});

test('defaults: a placeholder row cannot be bound through a seed override (PLACEHOLDER_ROW_REFUSED), and nothing is substituted', () => {
  const r = importRows(authoritativeShapedRows(), {
    bindingSeed: { IV1_OUTLET_PRESSURE: { source: 'AI-020', declaredSourceIdentity: 'IV1_OUTLET' } },
  });
  assert.ok(r.issues.some((i) => i.code === 'PLACEHOLDER_ROW_REFUSED' && i.tagName === 'IV1_OUTLET_PRESSURE'));
  assert.equal(byTag(r.bindings, 'IV1_OUTLET_PRESSURE'), undefined, 'no binding is created for the refused entry');
  assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === 'AI-020'), false);
});

// ---------------------------------------------------------------- missing sources are refused, never filled in

test('defaults: a missing default source is refused with DEFAULT_SOURCE_NOT_FOUND, and the neighbouring IVn is not used', () => {
  const r = importRows(authoritativeShapedRows({ omit: ['AI-010'] }));
  assert.ok(r.issues.some((i) => i.code === 'DEFAULT_SOURCE_NOT_FOUND' && i.tagName === 'IV7_OUTLET_PRESSURE'));
  assert.equal(byTag(r.bindings, 'IV7_OUTLET_PRESSURE'), undefined);
  assert.ok(r.issues.some((i) => i.code === 'REQUIRED_TAG_MISSING' && i.tagName === 'IV7_OUTLET_PRESSURE'));
  assert.equal(r.bindings.filter((b) => b.sourceWorkbookTag === 'AI-011').length, 1, 'AI-011 stays IV8 only');
  assert.equal(byTag(r.bindings, 'IV8_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-011');
});

test('defaults: a missing AI-002 is refused with PUMP_SOURCE_NOT_FOUND, and AI-003 is never used as the inlet', () => {
  const r = importRows(authoritativeShapedRows({ omit: ['AI-002'] }));
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_NOT_FOUND' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(byTag(r.bindings, 'PUMP_INLET_PRESSURE'), undefined);
  assert.equal(byTag(r.bindings, 'PUMP_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-003');
  assert.equal(r.bindings.filter((b) => b.sourceWorkbookTag === 'AI-003').length, 1, 'AI-003 is not aliased to the inlet');
});

test('pump signal: a pump transmitter that is not 4-20 mA is refused (PUMP_SIGNAL_MISMATCH), and no binding is created', () => {
  const rows = authoritativeShapedRows().map((row) => (row[3] === 'AI-002' ? [...row.slice(0, 5), 'AI (0-20 mA.)', ...row.slice(6)] : row));
  const r = importRows(rows);
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SIGNAL_MISMATCH' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(byTag(r.bindings, 'PUMP_INLET_PRESSURE'), undefined);
});

test('pump source: a seed that gives the Pump Outlet the AI-002 source is refused (PUMP_SOURCE_MISMATCH), with no AI-002 alias', () => {
  const r = importRows(authoritativeShapedRows(), { bindingSeed: { PUMP_OUTLET_PRESSURE: { source: 'AI-002' } } });
  assert.ok(r.issues.some((i) => i.code === 'PUMP_SOURCE_MISMATCH' && i.tagName === 'PUMP_OUTLET_PRESSURE'));
  assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === 'AI-002' && b.tagName !== 'PUMP_INLET_PRESSURE'), false);
});

// ---------------------------------------------------------------- pumps: 4-20 mA, 0–40 bar, distinct, Main Valve absent

test('pump ranges: AI-002 and AI-003 are both 4-20 mA, 0–40 bar in the catalogue and in the imported bindings', () => {
  const r = importRows(authoritativeShapedRows());
  for (const name of ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']) {
    const def = getTagDef(name);
    assert.equal(def.signal, 'CURRENT_4_20_MA', `${name} is a 4-20 mA input`);
    const range = def.confirmedEngineeringRange;
    assert.deepEqual([range.min, range.max, range.unit], [0, 40, 'bar'], name);
    assert.deepEqual(byTag(r.bindings, name).engineering, { min: 0, max: 40, unit: 'bar' }, `${name} binding`);
  }
  assert.equal(r.issues.some((i) => i.code === 'PUMP_SIGNAL_MISMATCH'), false, 'the workbook pump rows are 4-20 mA');
  assert.equal(OWNER_PUMP_SOURCE_TAGS.PUMP_INLET_PRESSURE, 'AI-002');
  assert.equal(OWNER_PUMP_SOURCE_TAGS.PUMP_OUTLET_PRESSURE, 'AI-003');
});

test('pump roles: AI-002 is diagnostic and trend only, never Pump-ready, valve diagnostic, alarm, trip or interlock', () => {
  const inlet = getTagDef('PUMP_INLET_PRESSURE');
  assert.deepEqual(inlet.permittedUse, ['DIAGNOSTIC', 'TREND']);
  assert.equal(inlet.readinessRole, 'PUMP_INLET_DIAGNOSTIC');
  assert.equal(inlet.pumpReadyEvidence, false);
  assert.equal(inlet.valveDiagnosticEvidence, false);
  assert.deepEqual(inlet.automaticUse, { alarm: false, trip: false, interlock: false });
});

test('pump roles: AI-003 is the only pre-P1 Pump-ready pressure gate, and no IVn is Pump-ready evidence', () => {
  const ready = listSimulationTags().filter((t) => t.pumpReadyEvidence);
  assert.deepEqual(ready.map((t) => t.tagName), ['PUMP_OUTLET_PRESSURE']);
  assert.deepEqual(getTagDef('PUMP_OUTLET_PRESSURE').permittedUse, ['PUMP_READY_GATE']);
  assert.equal(getTagDef('PUMP_OUTLET_PRESSURE').readinessRole, 'PUMP_READY_GATE');
  for (const t of listSimulationTags().filter((x) => /^IV/.test(x.tagName))) {
    assert.equal(Boolean(t.pumpReadyEvidence), false, t.tagName);
    assert.equal(Boolean(t.valveDiagnosticEvidence), t.tagName.endsWith('_OUTLET_PRESSURE'), t.tagName);
  }
});

test('Main Valve: no Main Valve pressure tag exists in the catalogue, the required set, the bindings or the additional tags', () => {
  assert.equal(getTagDef('MAIN_VALVE_OUTLET_PRESSURE'), null);
  assert.equal(REQUIRED_TAG_NAMES.some((n) => /MAIN_VALVE/.test(n)), false);
  const r = importRows(authoritativeShapedRows());
  assert.equal(r.bindings.some((b) => /MAIN_VALVE/.test(b.tagName)), false);
  assert.equal(Object.keys(r.additionalTags).some((k) => /MAIN_VALVE/.test(k)), false);
});

// ---------------------------------------------------------------- addresses and determinism

test('addresses: every enabled default is ADDRESS_UNRESOLVED with no numeric address, and the session stays unverified', () => {
  const r = importRows(authoritativeShapedRows());
  const cfg = { label: r.label, modules: r.modules, bindings: r.bindings, declaredChannelCounts: r.declaredChannelCounts, additionalTags: r.additionalTags };
  const addresses = deriveAddresses(deriveRackView(r.modules), r.bindings, null);
  assert.equal(addresses.verified, false);
  assert.equal(addresses.entries.length, 26);
  for (const e of addresses.entries) {
    assert.equal(e.state, 'ADDRESS_UNRESOLVED', e.tagName);
    assert.equal(e.byteOffset, null, e.tagName);
    assert.equal(e.bitOffsetAbsolute, null, e.tagName);
  }
  const session = new DraftSession(cfg);
  assert.equal(session.validate().status, 'VALID', 'the defaults are a valid mapping, with addresses still unresolved');
});

test('determinism: the same workbook imports to the same canonical configuration, twice and stored or deflated', () => {
  const snapshot = (r) => canonicalJson({
    label: r.label, modules: r.modules, bindings: r.bindings, additionalTags: r.additionalTags,
    declaredChannelCounts: r.declaredChannelCounts, classifications: r.classifications, counts: r.counts,
    issues: r.issues.map((i) => ({ code: i.code, severity: i.severity, tagName: i.tagName ?? null })),
  });
  const rows = authoritativeShapedRows();
  const first = snapshot(importWorkbook(buildWorkbook({ rows, method: 0 })));
  const again = snapshot(importWorkbook(buildWorkbook({ rows, method: 0 })));
  const deflated = snapshot(importWorkbook(buildWorkbook({ rows, method: 8 })));
  assert.equal(again, first, 'a second import is identical');
  assert.equal(deflated, first, 'stored and deflated copies of the same content are identical');
});

// ---------------------------------------------------------------- Owner-local: the real workbook, outside the repository

const LOCAL = process.env.MAPPING_EXCEL_DEFAULT_PATH;
const OWNER_LOCAL = LOCAL
  ? false
  : 'NOT VERIFIED IN ARENA: set MAPPING_EXCEL_DEFAULT_PATH to a copy of the workbook outside the repository (no seed needed)';

test('Owner-local: the authoritative workbook loads the 26 defaults with NO seed and no REQUIRED_TAG_MISSING',
  { skip: OWNER_LOCAL },
  () => {
    const r = importWorkbook(readFileSync(LOCAL));
    const enabled = r.bindings.filter((b) => b.enabled);
    assert.equal(enabled.length, 26, 'the 26 defaults are the enabled bindings');
    assert.deepEqual(enabled.map((b) => b.tagName).sort(), [...OWNER_DEFAULT_TAG_NAMES].sort());
    assert.equal(r.issues.filter((i) => i.code === 'REQUIRED_TAG_MISSING').length, 0, 'no missing required tag');
    assert.equal(r.issues.filter((i) => i.code === 'DEFAULT_SOURCE_NOT_FOUND').length, 0);
    assert.equal(r.issues.filter((i) => i.code === 'PUMP_SOURCE_NOT_FOUND').length, 0);
    for (const [tagName, source] of OWNER_RULE) assert.equal(byTag(r.bindings, tagName).sourceWorkbookTag, source, tagName);
  });

test('Owner-local: the 18 placeholder rows are unbound, the pumps are 4-20 mA and 0–40 bar, and no Main Valve tag exists',
  { skip: OWNER_LOCAL },
  () => {
    const r = importWorkbook(readFileSync(LOCAL));
    for (const tag of OWNER_PLACEHOLDER_ROWS) {
      assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === tag), false, `${tag} is unbound`);
    }
    assert.equal(OWNER_PLACEHOLDER_ROWS.filter((t) => r.records.find((x) => x.tag === t && /XXX/.test(x.signal))).length, 18);
    for (const name of ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']) {
      assert.deepEqual(byTag(r.bindings, name).engineering, { min: 0, max: 40, unit: 'bar' }, name);
    }
    assert.equal(r.bindings.some((b) => /MAIN_VALVE/.test(b.tagName)), false);
    assert.equal(r.issues.some((i) => i.code === 'PUMP_SIGNAL_MISMATCH'), false);
  });

test('Owner-local: the import is deterministic on the real workbook (two runs identical)',
  { skip: OWNER_LOCAL },
  () => {
    const buf = readFileSync(LOCAL);
    const snap = (r) => canonicalJson({ bindings: r.bindings, additionalTags: r.additionalTags, classifications: r.classifications, issues: r.issues.map((i) => [i.code, i.severity, i.tagName ?? null]) });
    assert.equal(snap(importWorkbook(buf)), snap(importWorkbook(buf)));
  });
