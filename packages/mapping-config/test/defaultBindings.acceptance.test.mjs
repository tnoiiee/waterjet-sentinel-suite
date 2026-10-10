// Stage 0.4B-1 — authoritative default bindings: acceptance items D01–D15 (Owner ruling 2026-10-10).
// Runs on an authoritative-shaped SYNTHETIC workbook (no plant data). No seed is used anywhere in this file, so the
// 26 defaults are proven to come from the workbook alone. Each refusal case mutates a copy of the rows only.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { importWorkbook } from '../src/workbookImport.mjs';
import { canonicalJson } from '../src/canonical.mjs';
import { getTagDef, REQUIRED_TAG_NAMES, PUMP_READY_GATE_TAG, PUMP_READY_SOURCE_IDENTITY } from '../src/tagCatalogue.mjs';
import { FORBIDDEN_BINDING_KEYS } from '../src/constants.mjs';
import { deriveAddresses } from '../src/addressDerivation.mjs';
import { deriveRackView } from '../src/rack.mjs';
import { buildWorkbook, authoritativeShapedRows } from './helpers/syntheticWorkbook.mjs';

const pad3 = (n) => String(n).padStart(3, '0');
const importRows = (rows, options) => importWorkbook(buildWorkbook({ rows }), options);
const errorIssues = (r) => r.issues.filter((i) => i.severity === 'ERROR');
const byTag = (r, tagName) => r.bindings.find((b) => b.tagName === tagName);
// Row layout of the synthetic helper: [slot, model, channel, workbookTag, signal, ioType, status].
const withSignal = (rows, workbookTag, signal) => rows.map((row) => (row[3] === workbookTag ? [...row.slice(0, 4), signal, ...row.slice(5)] : row));
const OWNER_IV_PRESSURE = Array.from({ length: 8 }, (_, i) => [`IV${i + 1}_OUTLET_PRESSURE`, `AI-${pad3(i + 4)}`]);
const OWNER_IV_LOWER = Array.from({ length: 8 }, (_, i) => [`IV${i + 1}_LOWER_LIMIT`, `DI-${pad3(i + 21)}`]);
const OWNER_IV_UPPER = Array.from({ length: 8 }, (_, i) => [`IV${i + 1}_UPPER_LIMIT`, `DI-${pad3(i + 29)}`]);

test('D01 the authoritative workbook loads 26 enabled default bindings with NO seed and no ERROR issue', () => {
  const r = importRows(authoritativeShapedRows());
  assert.equal(r.bindings.length, 26);
  assert.equal(r.bindings.filter((b) => b.enabled).length, 26);
  assert.deepEqual(errorIssues(r), []);
  assert.equal(r.seedIssueCount, 0);
});

test('D02 AI-002 is PUMP_INLET_PRESSURE and AI-003 is PUMP_OUTLET_PRESSURE, each on its own workbook channel', () => {
  const r = importRows(authoritativeShapedRows());
  const inlet = byTag(r, 'PUMP_INLET_PRESSURE');
  const outlet = byTag(r, 'PUMP_OUTLET_PRESSURE');
  assert.equal(inlet.sourceWorkbookTag, 'AI-002');
  assert.equal(outlet.sourceWorkbookTag, 'AI-003');
  assert.equal(inlet.channel, 1, 'AI-002 is channel 1 of the first AI module');
  assert.equal(outlet.channel, 2, 'AI-003 is channel 2 of the first AI module');
  assert.notEqual(inlet.sourceWorkbookTag, outlet.sourceWorkbookTag, 'no shared physical source');
});

test('D03 IV1..IV8 outlet pressure bind to AI-004..AI-011 under the Owner ordinal rule (#n = IVn)', () => {
  const r = importRows(authoritativeShapedRows());
  for (const [tag, source] of OWNER_IV_PRESSURE) {
    assert.equal(byTag(r, tag)?.sourceWorkbookTag, source, `${tag} <- ${source}`);
  }
});

test('D04 IV1..IV8 Lower limits bind to DI-021..DI-028 and Upper limits to DI-029..DI-036, with #n = IVn', () => {
  const r = importRows(authoritativeShapedRows());
  for (const [tag, source] of [...OWNER_IV_LOWER, ...OWNER_IV_UPPER]) {
    assert.equal(byTag(r, tag)?.sourceWorkbookTag, source, `${tag} <- ${source}`);
  }
});

test('D05 a pressure row whose #n is not IVn is refused (PRESSURE_IV_LABEL_MISMATCH) and IV2 is not bound at all', () => {
  const rows = withSignal(authoritativeShapedRows(), 'AI-005', 'Example pressure transmitter #7');
  const r = importRows(rows);
  assert.ok(r.issues.some((i) => i.code === 'PRESSURE_IV_LABEL_MISMATCH' && i.tagName === 'IV2_OUTLET_PRESSURE'));
  assert.equal(byTag(r, 'IV2_OUTLET_PRESSURE'), undefined, 'no partial binding for the refused pressure');
  assert.equal(byTag(r, 'IV1_OUTLET_PRESSURE').sourceWorkbookTag, 'AI-004', 'nothing repaired on a neighbour');
});

test('D06 a Lower limit whose #n is not IVn is refused (LIMIT_IV_LABEL_MISMATCH); the refused limit is not bound', () => {
  const rows = withSignal(authoritativeShapedRows(), 'DI-024', 'Example lower limit #5 (NO)');
  const r = importRows(rows);
  assert.ok(r.issues.some((i) => i.code === 'LIMIT_IV_LABEL_MISMATCH' && i.tagName === 'IV4_LOWER_LIMIT'));
  assert.equal(byTag(r, 'IV4_LOWER_LIMIT'), undefined);
  assert.equal(byTag(r, 'IV5_LOWER_LIMIT').sourceWorkbookTag, 'DI-025', 'IV5 keeps its own source');
});

test('D07 an Upper limit with an out-of-range #9 is refused (LIMIT_IV_LABEL_MISMATCH), never clamped to IV8', () => {
  const rows = withSignal(authoritativeShapedRows(), 'DI-029', 'Example upper limit #9 (NO)');
  const r = importRows(rows);
  assert.ok(r.issues.some((i) => i.code === 'LIMIT_IV_LABEL_MISMATCH' && i.tagName === 'IV1_UPPER_LIMIT'));
  assert.equal(byTag(r, 'IV1_UPPER_LIMIT'), undefined);
  assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === 'DI-029' && b.enabled !== false), false, 'no enabled binding uses DI-029');
});

test('D08 a limit row carrying two #n labels is refused, not repaired by choosing the first one', () => {
  const rows = withSignal(authoritativeShapedRows(), 'DI-023', 'Example lower limit #3 (NO) ref #3');
  const r = importRows(rows);
  assert.ok(r.issues.some((i) => i.code === 'LIMIT_IV_LABEL_MISMATCH' && i.tagName === 'IV3_LOWER_LIMIT'));
  assert.equal(byTag(r, 'IV3_LOWER_LIMIT'), undefined);
});

test('D09 a missing default source refuses only that tag (DEFAULT_SOURCE_NOT_FOUND); the neighbouring IVn is not used', () => {
  const r = importRows(authoritativeShapedRows({ omit: ['DI-025'] }));
  assert.ok(r.issues.some((i) => i.code === 'DEFAULT_SOURCE_NOT_FOUND' && i.tagName === 'IV5_LOWER_LIMIT'));
  assert.equal(byTag(r, 'IV5_LOWER_LIMIT'), undefined);
  assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === 'DI-024' && b.tagName === 'IV5_LOWER_LIMIT'), false);
  assert.equal(byTag(r, 'IV4_LOWER_LIMIT').sourceWorkbookTag, 'DI-024');
});

test('D10 a pump row whose description names the other pump side is refused (PUMP_PRESSURE_LABEL_CONFLICT), not repaired', () => {
  const rows = withSignal(authoritativeShapedRows(), 'AI-002', 'Example pressure transmitter - pump outlet');
  const r = importRows(rows);
  assert.ok(r.issues.some((i) => i.code === 'PUMP_PRESSURE_LABEL_CONFLICT' && i.tagName === 'PUMP_INLET_PRESSURE'));
  assert.equal(byTag(r, 'PUMP_INLET_PRESSURE'), undefined);
  assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === 'AI-002' && b.enabled !== false), false, 'no enabled binding uses AI-002');
});

test('D11 the Pump-ready gate reads PUMP_OUTLET only: AI-003 is its sole source, and no pump inlet or IVn feeds it', () => {
  const r = importRows(authoritativeShapedRows());
  assert.equal(PUMP_READY_GATE_TAG, 'PUMP_OUTLET_PRESSURE');
  assert.equal(PUMP_READY_SOURCE_IDENTITY, 'PUMP_OUTLET');
  assert.equal(byTag(r, PUMP_READY_GATE_TAG).sourceWorkbookTag, 'AI-003');
  assert.equal(getTagDef('PUMP_INLET_PRESSURE').role, 'PUMP_INLET_PRESSURE');
  assert.notEqual(getTagDef('PUMP_INLET_PRESSURE').sourceIdentity, PUMP_READY_SOURCE_IDENTITY);
  for (const [tag] of OWNER_IV_PRESSURE) assert.notEqual(getTagDef(tag).sourceIdentity, PUMP_READY_SOURCE_IDENTITY);
});

test('D12 no Main Valve pressure exists in the catalogue, the required set or the default bindings', () => {
  const r = importRows(authoritativeShapedRows());
  assert.equal(getTagDef('MAIN_VALVE_OUTLET_PRESSURE'), null);
  assert.equal(REQUIRED_TAG_NAMES.some((n) => /MAIN_VALVE/.test(n)), false);
  assert.equal(r.bindings.some((b) => /MAIN_VALVE/.test(b.tagName)), false);
  assert.notEqual(byTag(r, PUMP_READY_GATE_TAG).sourceWorkbookTag, undefined);
});

test('D13 a seed that adds MAIN_VALVE_OUTLET_PRESSURE is refused as UNKNOWN_TAG and creates no binding', () => {
  const seed = { MAIN_VALVE_OUTLET_PRESSURE: { source: 'AI-003', declaredSourceIdentity: 'MAIN_VALVE_OUTLET' } };
  const r = importRows(authoritativeShapedRows(), { bindingSeed: seed });
  assert.ok(r.issues.some((i) => i.code === 'UNKNOWN_TAG' && i.tagName === 'MAIN_VALVE_OUTLET_PRESSURE'));
  assert.equal(r.bindings.some((b) => /MAIN_VALVE/.test(b.tagName)), false);
});

test('D14 the defaults need no seed: an import with no seed equals an import with an empty seed, byte for byte', () => {
  const rows = authoritativeShapedRows();
  const noSeed = importRows(rows);
  const emptySeed = importRows(rows, { bindingSeed: {} });
  assert.equal(canonicalJson(noSeed.bindings), canonicalJson(emptySeed.bindings));
  assert.equal(noSeed.bindings.length, 26);
});

test('D15 every default binding is ADDRESS_UNRESOLVED, and no numeric address or offset is stored on a binding', () => {
  const r = importRows(authoritativeShapedRows());
  for (const b of r.bindings) {
    for (const key of FORBIDDEN_BINDING_KEYS) assert.equal(Object.prototype.hasOwnProperty.call(b, key), false, `${b.tagName} has no ${key}`);
  }
  const addresses = deriveAddresses(deriveRackView(r.modules), r.bindings, null);
  assert.equal(addresses.verified, false);
  assert.equal(addresses.entries.length, 26);
  for (const e of addresses.entries) {
    assert.equal(e.state, 'ADDRESS_UNRESOLVED', e.tagName);
    assert.equal(e.byteOffset, null, e.tagName);
    assert.equal(e.bitOffsetAbsolute, null, e.tagName);
  }
});
