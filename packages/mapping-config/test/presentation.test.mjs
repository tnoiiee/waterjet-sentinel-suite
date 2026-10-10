// Stage 0.4B-2 — presentation model. Filters, grouping, summaries and validation grouping are read-only views.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DEFAULT_DENSITY, DENSITY, GROUP_ORDER, TAG_FILTERS, buildTagRows, compactAddressSummary, equipmentGroupOf, filterCounts,
  filterRows, groupRows, groupValidation, issueStatusText, placeholderSummary,
} from '../src/presentation.mjs';
import { DraftSession, createConfiguration } from '../src/draftSession.mjs';
import { importWorkbook } from '../src/nodeImport.mjs';
import { canonicalJson } from '../src/canonical.mjs';
import { syntheticExampleConfiguration } from '../src/syntheticExample.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const workbook = importWorkbook(readFileSync(join(HERE, '..', '..', '..', 'T8_IO_Card_Mapping.xlsx')));
const config = createConfiguration({
  label: workbook.label, modules: workbook.modules, bindings: workbook.bindings,
  declaredChannelCounts: workbook.declaredChannelCounts, additionalTags: workbook.additionalTags,
});

function view(session = new DraftSession(config)) {
  const v = session.validate();
  const rows = buildTagRows({
    entries: v.addresses.entries,
    bindings: session.snapshot().bindings,
    additionalTags: config.additionalTags,
    issues: [...v.rackIssues, ...v.mappingIssues],
    reservedRows: workbook.reservedInventory.rows,
  });
  return { v, rows };
}

test('U1 Comfortable is the default density and the two options are the only ones', () => {
  assert.equal(DEFAULT_DENSITY, 'comfortable');
  assert.deepEqual(Object.values(DENSITY), ['comfortable', 'compact']);
});

test('U2 every filter returns a deterministic, correct subset of the authoritative rack', () => {
  const { v, rows } = view();
  const count = (key) => filterRows(rows, key).length;
  assert.equal(rows.length, v.addresses.entries.length + 18, 'one row per entry plus the 18 reserved rows');
  assert.equal(count('all'), rows.length);
  assert.equal(count('runtime'), 26, 'enabled Runtime bindings');
  assert.equal(count('pending'), 18, 'reserved / Owner-input-pending rows');
  assert.equal(count('pump'), 2);
  assert.equal(count('iv'), 24, 'IV1-IV8: pressure + lower + upper each');
  assert.equal(filterRows(rows, 'analog').every((r) => r.signalClass === 'ANALOG_INPUT'), true);
  assert.equal(filterRows(rows, 'digital').every((r) => r.signalClass === 'DIGITAL_INPUT'), true);
  assert.ok(filterRows(rows, 'readonly').every((r) => r.kind === 'binding' && (!r.enabled || r.group === 'Other workbook inputs')));
  assert.ok(filterRows(rows, 'unresolved').length >= 26 + 18, 'every unresolved binding and every reserved row');
  assert.throws(() => filterRows(rows, 'no-such-filter'), /unknown filter/);
  assert.deepEqual(filterCounts(rows).map((f) => f.key), TAG_FILTERS.map((f) => f.key));
  // The reserved output DO-031 is neither an analog nor a digital input.
  const do031 = rows.find((r) => r.tag === 'DO-031');
  assert.equal(do031.signalClass, 'OUTPUT');
  assert.equal(filterRows(rows, 'analog').includes(do031) || filterRows(rows, 'digital').includes(do031), false);
});

test('U3 Pump and IV grouping follow the #n = IVn rule; reserved rows form their own group', () => {
  const { rows } = view();
  const groups = groupRows(rows);
  assert.deepEqual(groups.map((g) => g.group), GROUP_ORDER.filter((g) => groups.some((x) => x.group === g)));
  const named = (name) => groups.find((g) => g.group === name).rows.map((r) => r.tag).sort();
  assert.deepEqual(named('Pump'), ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']);
  for (let n = 1; n <= 8; n += 1) {
    assert.deepEqual(named(`IV${n}`), [`IV${n}_LOWER_LIMIT`, `IV${n}_OUTLET_PRESSURE`, `IV${n}_UPPER_LIMIT`], `IV${n}`);
  }
  const reserved = groups.find((g) => g.group === 'Reserved / Owner pending');
  assert.equal(reserved.rows.length, 18);
  assert.ok(reserved.rows.every((r) => r.kind === 'reserved'));
  for (const g of groups.filter((x) => x.group !== 'Reserved / Owner pending')) assert.ok(g.rows.every((r) => r.kind === 'binding'), g.group);
  for (const tag of ['DI-037', 'DO-031', 'AI-020', 'AI-035']) assert.ok(reserved.rows.some((r) => r.tag === tag), tag);
  assert.equal(equipmentGroupOf('SOMETHING_ELSE', null), 'Other workbook inputs');
});

test('U4 filters, grouping and summaries never alter the Draft, the default configuration or any revision', () => {
  const session = new DraftSession(config);
  const before = { draft: canonicalJson(session.snapshot()), def: canonicalJson(session.defaultSnapshot()), rev: canonicalJson(session.revisions()), undo: session.undoStack.length };
  const { v, rows } = view(session);
  const frozen = canonicalJson(rows);
  for (const f of TAG_FILTERS) groupRows(filterRows(rows, f.key));
  filterCounts(rows);
  groupValidation({ issues: [...v.rackIssues, ...v.mappingIssues], addressEntries: v.addresses.entries, reservedRows: workbook.reservedInventory.rows });
  placeholderSummary(workbook.reservedInventory);
  assert.equal(canonicalJson(rows), frozen, 'the rows themselves are untouched');
  assert.equal(canonicalJson(session.snapshot()), before.draft);
  assert.equal(canonicalJson(session.defaultSnapshot()), before.def);
  assert.equal(canonicalJson(session.revisions()), before.rev);
  assert.equal(session.undoStack.length, before.undo);
  assert.equal(canonicalJson(workbook.reservedInventory).length > 0, true);
});

test('U5 the placeholder summary separates input and output rows and adds them', () => {
  const s = placeholderSummary(workbook.reservedInventory);
  assert.deepEqual([s.input, s.output, s.total], [17, 1, 18]);
  assert.match(s.text, /input rows 17 \+ placeholder output rows 1 = 18 total Owner-input-pending rows/);
  assert.deepEqual(placeholderSummary(null), { input: 0, output: 0, total: 0, text: 'Placeholder input rows 0 + placeholder output rows 0 = 0 total Owner-input-pending rows' });
});

test('U6 the compact address summary counts reasons and an Error is never hidden by it', () => {
  const { rows } = view();
  const outlet = rows.find((r) => r.tag === 'PUMP_OUTLET_PRESSURE');
  assert.equal(compactAddressSummary(outlet), `ADDRESS UNRESOLVED · ${outlet.reasons.length} reasons`);
  assert.ok(outlet.reasons.length >= 4);
  assert.doesNotMatch(compactAddressSummary(outlet), /HEAD_STATION|MODULE_|NOT_VERIFIED/, 'full reasons are not repeated in the row');
  assert.equal(compactAddressSummary({ kind: 'binding', addressState: 'ADDRESS_UNRESOLVED', reasons: ['X'] }), 'ADDRESS UNRESOLVED · 1 reason');
  assert.equal(compactAddressSummary({ kind: 'reserved' }), 'ADDRESS UNRESOLVED · no binding');
  // An invalid binding: the issue status sits next to the compact address and reads ERROR.
  const c = syntheticExampleConfiguration();
  const session = new DraftSession(c);
  session.setBinding('IV1_UPPER_LIMIT', { activePolarity: null });
  const v = session.validate();
  const bad = buildTagRows({ entries: v.addresses.entries, bindings: session.snapshot().bindings, issues: [...v.rackIssues, ...v.mappingIssues] })
    .find((r) => r.tag === 'IV1_UPPER_LIMIT');
  assert.equal(bad.addressUnresolved, true);
  assert.match(compactAddressSummary(bad), /^ADDRESS UNRESOLVED · \d+ reasons$/);
  assert.match(issueStatusText(bad), /^ERROR × \d+$/);
  assert.equal(issueStatusText({ issueErrors: 0, issueWarnings: 2 }), 'WARNING × 2');
  assert.equal(issueStatusText({ issueErrors: 0, issueWarnings: 0 }), 'OK');
});

test('U7 validation is grouped in a fixed order with counts first, the same for any input order, and nothing is dropped', () => {
  const { v } = view();
  const issues = [...v.rackIssues, ...v.mappingIssues];
  const args = { addressEntries: v.addresses.entries, reservedRows: workbook.reservedInventory.rows };
  const a = groupValidation({ issues, ...args });
  const b = groupValidation({ issues: [...issues].reverse(), ...args });
  assert.equal(canonicalJson(a), canonicalJson(b), 'deterministic');
  assert.deepEqual(a.map((g) => g.label), ['Errors', 'Module profiles incomplete', 'Engineering ranges pending', 'Owner identities pending', 'Unresolved addresses', 'Other warnings']);
  const count = (key) => a.find((g) => g.key === key).count;
  assert.equal(count('errors'), 0);
  assert.equal(count('owner'), 18);
  assert.equal(count('addresses'), 26);
  assert.ok(count('profiles') > 0);
  assert.equal(a.reduce((n, g) => n + (g.key === 'owner' || g.key === 'addresses' ? 0 : g.items.length), 0), issues.length, 'every validation issue is in exactly one group');
  assert.equal(a.find((g) => g.key === 'owner').items.length, 18, 'one Owner-pending item per placeholder row');
  // Errors are never dropped or merged away.
  const withError = groupValidation({ issues: [{ code: 'X', severity: 'ERROR', message: 'm' }, ...issues], ...args });
  assert.equal(withError.find((g) => g.key === 'errors').count, 1);
  assert.equal(withError.find((g) => g.key === 'errors').severity, 'ERROR');
});

test('U8 the presentation module touches no device, network or storage and carries no theme state', () => {
  const src = readFileSync(join(HERE, '..', 'src', 'presentation.mjs'), 'utf8');
  assert.doesNotMatch(src, /fetch\(|XMLHttpRequest|WebSocket|localStorage|sessionStorage|node:/);
});
