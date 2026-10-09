// Stage 0.4B-1 — workbook import tests (synthetic workbook only, plus one Owner-local check).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { importWorkbook, parseSharedStrings, parseSheet, readZipEntries } from '../src/workbookImport.mjs';
import { canonicalJson } from '../src/canonical.mjs';
import { buildWorkbook, exampleRows, exampleSeed } from './helpers/syntheticWorkbook.mjs';

const seed = exampleSeed();
const importExample = (opts = {}) => importWorkbook(buildWorkbook({ rows: exampleRows(opts) }), { bindingSeed: seed });

test('imports the synthetic rack topology in slot order with stable module identities', () => {
  const r = importExample();
  assert.deepEqual(r.modules.map((m) => m.moduleInstanceId), [
    'COUPLER-01', 'SUPPLY-01', 'DI-MODULE-01', 'DI-MODULE-02', 'DO-MODULE-01',
    'AI-MODULE-01', 'AI-MODULE-02', 'AI-MODULE-03', 'AO-MODULE-01', 'END-MODULE-01',
  ]);
  assert.deepEqual(r.modules.map((m) => m.modelNumber), [
    '750-362', '750-601', '750-430', '750-430', '750-530', '750-471', '750-471', '750-471', '750-554', '750-600',
  ]);
  assert.equal(r.label, 'DEFAULT FROM EXCEL');
});

test('USED and SPARE status is preserved from the rows', () => {
  const r = importExample();
  assert.deepEqual(r.counts, { USED: 40, SPARE: 1, total: 41 });
  const spare = r.records.filter((x) => x.status === 'SPARE');
  assert.equal(spare.length, 1);
  assert.equal(spare[0].channel, '2');
});

test('ambiguous input descriptions are flagged and are not offered as read-only tags', () => {
  const r = importExample({ ambiguous: true });
  assert.ok(r.issues.some((i) => i.code === 'AMBIGUOUS_INPUT_DESCRIPTION'));
  assert.equal(r.additionalTags['EX-AI-11'], undefined);
  assert.ok(r.classifications.inputAmbiguous >= 1);
});

test('placeholder identity rows stay USED and unbound', () => {
  const r = importExample({ placeholder: true });
  assert.ok(r.issues.some((i) => i.code === 'SIGNAL_IDENTITY_UNRESOLVED'));
  assert.ok(r.issues.some((i) => i.code === 'OWNER_INPUT_PENDING'));
  assert.equal(r.classifications.reservedUnresolved, 1);
  assert.equal(r.bindings.some((b) => b.tagName === 'EX-AI-12' || b.sourceWorkbookTag === 'EX-AI-12'), false);
  assert.equal(r.records.find((x) => x.tag === 'EX-AI-12').status, 'USED');
});

test('output rows are classified NOT AUTHORIZED and never bound', () => {
  const r = importExample();
  assert.equal(r.classifications.outputNotAuthorised, 9);
  assert.equal(r.bindings.some((b) => /^EX-(DO|AO)/.test(b.sourceWorkbookTag ?? '')), false);
  assert.equal(Object.keys(r.additionalTags).some((t) => t.startsWith('EX-DO')), false);
});

test('the seeded bindings resolve to the expected module instances and channels', () => {
  const r = importExample();
  const pump = r.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  assert.deepEqual([pump.moduleInstanceId, pump.channel], ['AI-MODULE-01', 1]);
  const iv8 = r.bindings.find((b) => b.tagName === 'IV8_OUTLET_PRESSURE');
  assert.deepEqual([iv8.moduleInstanceId, iv8.channel], ['AI-MODULE-03', 2]);
  const lower1 = r.bindings.find((b) => b.tagName === 'IV1_LOWER_LIMIT');
  assert.equal(lower1.contactType, 'NO');
});

test('import is deterministic: identical bytes give an identical canonical result', () => {
  const buf = buildWorkbook({ rows: exampleRows() });
  const a = importWorkbook(buf, { bindingSeed: seed });
  const b = importWorkbook(buf, { bindingSeed: seed });
  assert.equal(canonicalJson({ ...a, records: undefined }), canonicalJson({ ...b, records: undefined }));
});

test('deflated ZIP entries parse the same as stored entries', () => {
  const stored = importWorkbook(buildWorkbook({ rows: exampleRows(), method: 0 }), { bindingSeed: seed });
  const deflated = importWorkbook(buildWorkbook({ rows: exampleRows(), method: 8 }), { bindingSeed: seed });
  assert.equal(canonicalJson({ ...stored, source: undefined, records: undefined }),
    canonicalJson({ ...deflated, source: undefined, records: undefined }));
});

test('a duplicate complete row is reported', () => {
  const rows = exampleRows();
  rows.push(rows[3].slice());
  const r = importWorkbook(buildWorkbook({ rows }), { bindingSeed: seed });
  assert.ok(r.issues.some((i) => i.code === 'DUPLICATE_COMPLETE_ROW'));
});

test('a duplicate Slot/Channel assignment is reported', () => {
  const rows = exampleRows();
  rows.push([3, '750-430', 1, 'EX-DI-99', 'Duplicate channel', 'DI (24 VDC.)', 'USED', null]);
  const r = importWorkbook(buildWorkbook({ rows }), { bindingSeed: seed });
  assert.ok(r.issues.some((i) => i.code === 'DUPLICATE_SLOT_CHANNEL'));
});

test('a duplicate I/O Tag is reported', () => {
  const rows = exampleRows();
  rows.push([9, '750-554', 2, 'EX-DO-01', 'Duplicate tag', 'AO (4-20 mA.)', 'USED', null]);
  const r = importWorkbook(buildWorkbook({ rows }), { bindingSeed: seed });
  assert.ok(r.issues.some((i) => i.code === 'DUPLICATE_IO_TAG'));
});

test('blank and malformed Channel, Slot and Module values are reported, not repaired', () => {
  const rows = exampleRows();
  rows[4][2] = null;
  rows[5][0] = 'three';
  rows[6][1] = 'not-a-model';
  const r = importWorkbook(buildWorkbook({ rows }), { bindingSeed: seed });
  const codes = new Set(r.issues.map((i) => i.code));
  assert.ok(codes.has('BLANK_CHANNEL'));
  assert.ok(codes.has('MALFORMED_SLOT'));
  assert.ok(codes.has('MALFORMED_MODULE'));
});

test('an unknown module model is reported and is not put into the rack', () => {
  const rows = exampleRows();
  rows.push([11, '999-999', 1, 'EX-XX-01', 'Unknown module', 'DI', 'USED', null]);
  const r = importWorkbook(buildWorkbook({ rows }), { bindingSeed: seed });
  assert.ok(r.issues.some((i) => i.code === 'MALFORMED_MODULE' || i.code === 'UNKNOWN_MODEL_PROFILE'));
  assert.equal(r.modules.some((m) => m.modelNumber === '999-999'), false);
});

test('an unknown model in the middle of the rack does not shift later module identities or bindings', () => {
  const rows = exampleRows();
  // Slot 5 is the DO module. Replace its model with one that has no profile.
  for (const row of rows.slice(1)) if (row[0] === 5) row[1] = '999-999';
  const r = importWorkbook(buildWorkbook({ rows }), { bindingSeed: seed });
  assert.ok(r.issues.some((i) => i.code === 'UNKNOWN_MODEL_PROFILE'));
  assert.equal(r.modules.some((m) => m.modelNumber === '750-530'), false);
  const pump = r.bindings.find((b) => b.tagName === 'PUMP_OUTLET_PRESSURE');
  assert.deepEqual([pump.moduleInstanceId, pump.channel], ['AI-MODULE-01', 1]);
  const iv8 = r.bindings.find((b) => b.tagName === 'IV8_OUTLET_PRESSURE');
  assert.deepEqual([iv8.moduleInstanceId, iv8.channel], ['AI-MODULE-03', 2]);
});

test('a header that does not match the expected columns is refused', () => {
  const rows = exampleRows();
  rows[0] = ['Slot', 'Module', 'Channel', 'Tag', 'Signal', 'Type', 'Ref.', 'Location', 'Note', 'Status'];
  assert.throws(() => importWorkbook(buildWorkbook({ rows }), { bindingSeed: seed }), /header mismatch/);
});

test('a multi-sheet workbook needs an explicit sheet name', () => {
  const buf = buildWorkbook({ rows: exampleRows(), extraSheets: [{ name: 'Notes', rows: [['x']] }] });
  assert.throws(() => importWorkbook(buf, { bindingSeed: seed }), /sheetName is required/);
  const r = importWorkbook(buf, { sheetName: 'Sheet1', bindingSeed: seed });
  assert.equal(r.source.sheetName, 'Sheet1');
});

test('a seed entry that points at a missing workbook tag is reported', () => {
  const bad = { ...seed, IV1_OUTLET_PRESSURE: { source: 'EX-NOPE', declaredSourceIdentity: 'IV1_OUTLET' } };
  const r = importWorkbook(buildWorkbook({ rows: exampleRows() }), { bindingSeed: bad });
  assert.ok(r.issues.some((i) => i.code === 'SEED_TAG_NOT_FOUND'));
});

test('all 16 IVn limits bind when each workbook row labels the same IV index and group', () => {
  const r = importExample();
  const limits = r.bindings.filter((b) => /^IV[1-8]_(LOWER|UPPER)_LIMIT$/.test(b.tagName));
  assert.equal(limits.length, 16);
  assert.equal(r.issues.some((i) => i.code === 'LIMIT_IV_LABEL_MISMATCH'), false);
});

test('an IVn limit bound to a row labelled for another IV is refused and not bound', () => {
  const bad = { ...seed, IV1_LOWER_LIMIT: { ...seed.IV1_LOWER_LIMIT, source: 'EX-DI-02' } };
  const r = importWorkbook(buildWorkbook({ rows: exampleRows() }), { bindingSeed: bad });
  assert.ok(r.issues.some((i) => i.code === 'LIMIT_IV_LABEL_MISMATCH' && i.tagName === 'IV1_LOWER_LIMIT'));
  assert.equal(r.bindings.some((b) => b.tagName === 'IV1_LOWER_LIMIT'), false);
});

test('an IVn upper limit bound to a lower-limit row is refused (the group must match)', () => {
  const bad = { ...seed, IV1_UPPER_LIMIT: { ...seed.IV1_UPPER_LIMIT, source: 'EX-DI-01' } };
  const r = importWorkbook(buildWorkbook({ rows: exampleRows() }), { bindingSeed: bad });
  assert.ok(r.issues.some((i) => i.code === 'LIMIT_IV_LABEL_MISMATCH' && i.tagName === 'IV1_UPPER_LIMIT'));
  assert.equal(r.bindings.some((b) => b.tagName === 'IV1_UPPER_LIMIT'), false);
});

test('source details are reported (hash, size, dimension, formula and merge counts)', () => {
  const r = importExample();
  assert.match(r.source.sha256, /^[0-9a-f]{64}$/);
  assert.ok(r.source.sizeBytes > 0);
  assert.equal(r.source.formulaCount, 0);
  assert.equal(r.source.mergedCellCount, 0);
  assert.equal(r.source.dataRowCount, 41);
});

test('the shared-string and sheet parsers handle entities and empty cells', () => {
  const strings = parseSharedStrings('<sst><si><t>A &amp; B</t></si><si><t>&lt;x&gt;</t></si></sst>');
  assert.deepEqual(strings, ['A & B', '<x>']);
  const { rows } = parseSheet('<worksheet><sheetData><row r="1"><c r="A1" t="s"><v>1</v></c><c r="B1"/></row></sheetData></worksheet>', strings);
  assert.deepEqual(rows[0].cells, { A: '<x>', B: null });
});

test('the ZIP reader refuses data that is not a ZIP container', () => {
  assert.throws(() => readZipEntries(Buffer.from('not a zip file at all, just text')), /not a ZIP container/);
});

// Owner-local exact check. It runs only when the Owner points the variable at a
// workbook OUTSIDE the repository. It is NOT run in Arena and is reported as
// NOT VERIFIED here. The assertions use only the topology stated in the Stage
// Gate and never print workbook contents.
const LOCAL = process.env.MAPPING_EXCEL_DEFAULT_PATH;
test('Owner-local: the attached workbook imports to the expected default topology',
  { skip: LOCAL ? false : 'NOT VERIFIED IN ARENA: set MAPPING_EXCEL_DEFAULT_PATH to a workbook outside the repository' },
  () => {
    const r = importWorkbook(readFileSync(LOCAL));
    const expected = [
      '750-362', '750-601', ...Array(5).fill('750-430'), ...Array(4).fill('750-530'), '750-613',
      ...Array(9).fill('750-471'), '750-554', '750-600',
    ];
    assert.deepEqual(r.modules.map((m) => m.modelNumber), expected);
    assert.equal(r.modules.length, 23);
    assert.deepEqual(Object.keys(r.counts).sort(), ['SPARE', 'USED', 'total']);
  });
