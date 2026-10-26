// Focused tests for the H2 Process-I/O evidence generator (deterministic parse,
// column contract, duplicate refusal, determinism and committed-file parity).
//
// The vendor product-line name never appears literally in this Product-tree file:
// it is imported from the generator (itself assembled from fragments there).

import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import {
  ArtifactError,
  EXPECTED_SOURCE_SHA256,
  INVENTORY_FILENAME,
  SOURCE_DIRNAME,
  SOURCE_FILENAME,
  buildCsv,
  buildSummary,
  parseArtifact,
  run,
  sha256Of,
} from '../generate.mjs';

const REPO_ROOT = join(import.meta.dirname, '..', '..', '..');
const ARTIFACT_PATH = join(REPO_ROOT, SOURCE_DIRNAME, SOURCE_FILENAME);
const INVENTORY_PATH = join(REPO_ROOT, SOURCE_DIRNAME, INVENTORY_FILENAME);
const ARTIFACT_TEXT = readFileSync(ARTIFACT_PATH, 'utf8');
const PARSED = parseArtifact(ARTIFACT_TEXT);
const CSV = buildCsv(PARSED, EXPECTED_SOURCE_SHA256);

const EXPECTED_WORKBOOK_NAME = ['WA', 'GO_IO_Mapping(Update19-10-26)_2.xlsx'].join('');
const EXPECTED_FILTER = 'Filtered I/O Slot less than 0, equal to 0, and blank';

/** Minimal well-formed artifact-shaped HTML for refusal tests. */
function syntheticHtml(columns, rows, { scriptColumns = null } = {}) {
  const c = scriptColumns ?? columns;
  return `<html><body><div class=sub>Exact Excel values · Source: ${EXPECTED_WORKBOOK_NAME} · `
    + `SHA-256 4937dda8dcae · 26,325 bytes · ${EXPECTED_FILTER}</div>`
    + `<table><thead><tr>${columns.map((h) => `<th>${h}</th>`).join('')}</tr></thead></table>`
    + `<script>const D=${JSON.stringify(rows)},C=${JSON.stringify(c)};</script></body></html>`;
}

const COLUMNS = ['Mod', 'Slot', 'IoSlot', 'Channel', 'Tag', 'Signal', 'IoType', 'Terminal', 'From', 'To', 'Ref', 'Location', 'Note', 'Status'];
function row(overrides = {}) {
  return ['750-000', '3', '1', '1', 'T-001', 'Signal text', 'DI (24 VDC.)', 'X1.1', '', '', '', 'Loc', 'Note', 'USED']
    .map((v, i) => (overrides[i] !== undefined ? overrides[i] : v));
}

test('parses the committed artifact with every source row in order', () => {
  assert.equal(PARSED.rows.length, 111);
  PARSED.rows.forEach((r, i) => {
    assert.equal(r.ordinal, i + 1);
    assert.equal(r.values.length, 14);
    r.values.forEach((cell) => assert.equal(typeof cell, 'string'));
  });
  assert.equal(PARSED.columnContract.length, 14);
  assert.equal(PARSED.columnContract[0], ['WA', 'GO Module'].join(''));
  assert.equal(PARSED.columnContract[4], 'I/O Tag');
});

test('derived counts match the artifact exactly', () => {
  const summary = buildSummary(PARSED, EXPECTED_SOURCE_SHA256, 36667);
  assert.equal(summary.totalRows, 111);
  assert.deepEqual(summary.rowsByIoClass, { AI: 36, AO: 2, DI: 40, DO: 32, END_MODULE: 1 });
  assert.deepEqual(summary.rowsBySourceStatus, { SPARE: 6, USED: 105 });
  assert.equal(summary.taggedRows, 108);
  assert.equal(summary.untaggedRows, 3);
  assert.equal(summary.sourceArtifactSha256, EXPECTED_SOURCE_SHA256);
});

test('exact source values are preserved verbatim, including plus-suffixed tags and Thai notes', () => {
  const first = PARSED.rows[0].values;
  assert.equal(first[4], 'DI-001');
  assert.equal(first[5], 'Pump #1 is selected');
  const ai003 = PARSED.rows[74].values;
  assert.equal(ai003[4], 'AI-003+');
  assert.equal(ai003[5], 'Pressure Transmitter Main Valve Outlet');
  assert.equal(ai003[10], '1.29');
  const last = PARSED.rows[110].values;
  assert.equal(last[0], '750-600');
  assert.equal(last[5], 'End Module');
  assert.equal(last[6], 'End');
  // Thai wiring note preserved byte-for-byte on row 1 (Contact / Note column).
  assert.ok(first[12].startsWith('24 VDC. Input'));
  // Unresolved placeholder description preserved verbatim, never corrected.
  const do031 = PARSED.rows.find((r) => r.values[4] === 'DO-031');
  assert.equal(do031.values[5], 'XXX รอพี่ยิมตอบ');
});

test('Slot and I/O Slot pairs match the authoritative 20-position module sequence', () => {
  const pairs = [...new Set(PARSED.rows.map((r) => `${r.values[1]}|${r.values[2]}`))];
  const expected = [
    ['3', '1'], ['4', '2'], ['5', '3'], ['6', '4'], ['7', '5'],
    ['8', '6'], ['9', '7'], ['10', '8'], ['11', '9'],
    ['13', '10'], ['14', '11'], ['15', '12'], ['16', '13'], ['17', '14'], ['18', '15'], ['19', '16'], ['20', '17'], ['21', '18'],
    ['22', '19'], ['23', '20'],
  ].map(([a, b]) => `${a}|${b}`);
  assert.deepEqual(pairs.sort(), expected.sort());
  // The declared positive-filtered view excludes rack slots 1, 2 and 12 (no I/O Slot).
  assert.ok(!pairs.some((p) => p.startsWith('1|') || p.startsWith('2|') || p.startsWith('12|')));
});

test('declared source workbook identity and filter statement are extracted, marked unverified', () => {
  assert.equal(PARSED.meta.sourceWorkbookName, EXPECTED_WORKBOOK_NAME);
  assert.equal(PARSED.meta.declaredWorkbookSha256Prefix, '4937dda8dcae');
  assert.equal(PARSED.meta.declaredWorkbookBytes, 26325);
  assert.equal(PARSED.meta.filterStatement, EXPECTED_FILTER);
});

test('generation is deterministic and byte-identical to the committed inventory', () => {
  const again = buildCsv(parseArtifact(ARTIFACT_TEXT), EXPECTED_SOURCE_SHA256);
  assert.equal(again, CSV);
  const committed = readFileSync(INVENTORY_PATH, 'utf8');
  assert.equal(committed, CSV);
  assert.ok(CSV.endsWith('\n'));
  assert.ok(!CSV.includes('\r'));
  // header carries the derived columns after the 14 exact source columns
  const header = CSV.split('\n', 1)[0].split(',');
  assert.equal(header.length, 21);
  assert.equal(header[0], 'Source Row Ordinal (DERIVED)');
  assert.equal(header[15], 'Io Class (DERIVED)');
});

test('CSV escaping quotes fields containing commas or quotes', () => {
  const parsed = parseArtifact(syntheticHtml(COLUMNS, [row({ 5: 'A, "quoted", value' })]));
  const csv = buildCsv(parsed, 'ab'.repeat(32));
  assert.ok(csv.includes('"A, ""quoted"", value"'));
});

test('refuses malformed HTML (no data array)', () => {
  assert.throws(() => parseArtifact('<html><thead><tr>' + COLUMNS.map((h) => `<th>${h}</th>`).join('') + '</tr></thead></html>'), ArtifactError);
});

test('refuses a row with the wrong column count', () => {
  const bad = row();
  bad.pop();
  assert.throws(() => parseArtifact(syntheticHtml(COLUMNS, [bad])), (e) => e instanceof ArtifactError && e.code === 'MISSING_COLUMNS');
});

test('refuses duplicate source-row identity', () => {
  assert.throws(() => parseArtifact(syntheticHtml(COLUMNS, [row(), row()])), (e) => e instanceof ArtifactError && e.code === 'DUPLICATE_ROW');
});

test('refuses duplicate physical Slot/Channel placement', () => {
  const second = row({ 4: 'T-002' });
  assert.throws(() => parseArtifact(syntheticHtml(COLUMNS, [row(), second])), (e) => e instanceof ArtifactError && e.code === 'DUPLICATE_PLACEMENT');
});

test('refuses duplicate I/O tags', () => {
  const second = row({ 1: '4', 2: '2', 3: '2' });
  assert.throws(() => parseArtifact(syntheticHtml(COLUMNS, [row(), second])), (e) => e instanceof ArtifactError && e.code === 'DUPLICATE_TAG');
});

test('refuses a table/script column-contract mismatch', () => {
  const wrongScript = [...COLUMNS];
  wrongScript[4] = 'Different';
  assert.throws(() => parseArtifact(syntheticHtml(COLUMNS, [row()], { scriptColumns: wrongScript })), (e) => e instanceof ArtifactError && e.code === 'MISSING_COLUMNS');
});

test('refuses an artifact whose bytes do not match the adopted hash', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wjss-io-evidence-'));
  const path = join(dir, 'modified.html');
  writeFileSync(path, ARTIFACT_TEXT.replace('Pump #1 is selected', 'tampered'), 'utf8');
  assert.throws(() => run(path), (e) => e instanceof ArtifactError && e.code === 'HASH_MISMATCH');
});

test('run() verifies the committed artifact hash before parsing', () => {
  const { summary } = run(ARTIFACT_PATH);
  assert.equal(summary.sourceArtifactSha256, sha256Of(readFileSync(ARTIFACT_PATH)));
  assert.equal(summary.provenanceStatus, 'EXACT_BYTE_MATCH_VERIFIED');
});
