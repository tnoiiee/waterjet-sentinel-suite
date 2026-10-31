// Focused tests — complete catalogue: 111-row parity, class counts, source order, verbatim
// source preservation, spare/unresolved visibility, fail-closed refusal of row loss and
// provenance tampering.

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import {
  loadCatalogue, buildCatalogue, parseInventoryCsv, CatalogueError,
  EXPECTED_ROW_COUNT, EXPECTED_CLASS_COUNTS, defaultInventoryPath,
} from '../src/catalogue.mjs';

const INVENTORY_TEXT = readFileSync(defaultInventoryPath(), 'utf8');
const catalogue = loadCatalogue();

test('catalogue: exactly 111 rows, no row loss', () => {
  assert.equal(catalogue.counts.total, EXPECTED_ROW_COUNT);
  assert.equal(catalogue.rows.length, 111);
});

test('catalogue: DI/DO/AI/AO/end-module count parity', () => {
  assert.deepEqual({ ...catalogue.counts.byClass }, { ...EXPECTED_CLASS_COUNTS });
});

test('catalogue: exact source-order preservation (ordinals 1..111 with anchors)', () => {
  catalogue.rows.forEach((row, i) => assert.equal(row.sourceRowOrdinal, i + 1));
  assert.equal(catalogue.rows[0].source['I/O Tag'], 'DI-001');
  assert.equal(catalogue.rows[74].source['I/O Tag'], 'AI-003+');
  assert.equal(catalogue.rows[74].source['Signal / Description'], 'Pressure Transmitter Main Valve Outlet');
  assert.equal(catalogue.rows[110].source[['WA', 'GO Module'].join('')], '750-600');
  assert.equal(catalogue.rows[110].derived.ioClass, 'END_MODULE');
});

test('catalogue: source values preserved verbatim (plus suffix, Thai note, unresolved text)', () => {
  assert.equal(catalogue.rows[catalogue.byTag['AI-002+'] - 1].source['I/O Tag'], 'AI-002+');
  assert.ok(catalogue.rows[0].source['Contact / Note'].startsWith('24 VDC. Input'));
  assert.equal(catalogue.rows[catalogue.byTag['DO-031'] - 1].source['Signal / Description'], 'XXX รอพี่ยิมตอบ');
});

test('catalogue: spare and unresolved rows visible, classified and fail-closed', () => {
  assert.equal(catalogue.counts.bySourceClassification.SPARE, 6);
  assert.equal(catalogue.counts.bySourceClassification.USED, 105);
  const spareRows = catalogue.rows.filter((r) => r.derived.sourceClassification === 'SPARE');
  assert.equal(spareRows.length, 6);
  for (const row of spareRows) assert.equal(row.product.commissioningState, 'EXCLUDED');
});

test('catalogue: refuses row loss', () => {
  const lines = INVENTORY_TEXT.split('\n');
  const lost = [...lines.slice(0, 50), ...lines.slice(51)].join('\n');
  assert.throws(() => buildCatalogue(lost), (e) => e instanceof CatalogueError && e.code === 'ROW_COUNT');
});

test('catalogue: refuses a corrupted provenance hash', () => {
  const tampered = INVENTORY_TEXT.replaceAll('28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299', '0'.repeat(64));
  assert.throws(() => buildCatalogue(tampered), (e) => e instanceof CatalogueError && e.code === 'PROVENANCE');
});

test('catalogue: refuses a reordered source order', () => {
  const tampered = INVENTORY_TEXT.replace('1,750-430,3,1,1,DI-001', '2,750-430,3,1,1,DI-001');
  assert.throws(() => buildCatalogue(tampered), (e) => e instanceof CatalogueError && e.code === 'SOURCE_ORDER');
});

test('catalogue: refuses a header contract change', () => {
  const tampered = INVENTORY_TEXT.replace('Source Row Ordinal (DERIVED)', 'Ordinal');
  assert.throws(() => buildCatalogue(tampered), (e) => e instanceof CatalogueError && e.code === 'COLUMN_CONTRACT');
});

test('catalogue: CSV parser round-trips quoted fields', () => {
  const rows = parseInventoryCsv('a,b\n"v,1","q""2"\n');
  assert.deepEqual(rows[1], ['v,1', 'q"2']);
});
