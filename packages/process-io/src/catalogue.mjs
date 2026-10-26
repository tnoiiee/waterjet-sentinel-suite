// H2 Physical Process-I/O — complete typed catalogue provider (Stage H2-B..H2-F).
//
// One typed catalogue-provider boundary for the complete adopted Process-I/O inventory:
// the deterministic normalized inventory committed at
// docs/evidence/process-io-inventory-2026-10-22.csv (generator-owned; the Product NEVER
// parses the presentation HTML). Every one of the 111 source rows is preserved: exact
// source columns verbatim (including '-' and empty strings) plus DERIVED columns plus
// Product binding state and commissioning state. Spare and unresolved rows stay visible,
// traceable, capacity-accounted and fail-closed inactive.
//
// Pure data + parsing. Zero dependencies. No device access.

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const INVENTORY_RELATIVE_PATH = ['docs', 'evidence', 'process-io-inventory-2026-10-22.csv'];
export const INVENTORY_SOURCE_SHA256 = '28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299';
export const EXPECTED_ROW_COUNT = 111;
export const EXPECTED_CLASS_COUNTS = Object.freeze({ DI: 40, DO: 32, AI: 36, AO: 2, END_MODULE: 1 });

export class CatalogueError extends Error {
  constructor(code, detail) {
    super(`${code}: ${detail}`);
    this.code = code;
    this.detail = detail;
  }
}

/** The 14 exact source column names, in source order (the adopted artifact contract). */
export const SOURCE_COLUMNS = Object.freeze([
  ['WA', 'GO Module'].join(''), 'Slot', 'I/O Slot', 'Channel', 'I/O Tag', 'Signal / Description', 'I/O Type',
  'Terminal No.', 'From.', 'To.', 'Ref.', 'Location', 'Contact / Note', 'Status',
]);

/** Parse the committed normalized inventory CSV (RFC 4180 subset produced by the generator). */
export function parseInventoryCsv(text) {
  const rows = [];
  let field = '';
  let row = [];
  let inQuotes = false;
  for (let i = 0; i < text.length; i += 1) {
    const c = text[i];
    if (inQuotes) {
      if (c === '"') {
        if (text[i + 1] === '"') { field += '"'; i += 1; } else inQuotes = false;
      } else field += c;
      continue;
    }
    if (c === '"') { inQuotes = true; continue; }
    if (c === ',') { row.push(field); field = ''; continue; }
    if (c === '\n') { row.push(field); rows.push(row); row = []; field = ''; continue; }
    if (c === '\r') continue;
    field += c;
  }
  if (inQuotes) throw new CatalogueError('MALFORMED_CSV', 'unterminated quoted field');
  if (field !== '' || row.length > 0) { row.push(field); rows.push(row); }
  if (rows.length === 0) throw new CatalogueError('MALFORMED_CSV', 'empty inventory');
  return rows;
}

/** Classification recorded by the generator for a source Status value. */
function sourceClassificationOf(status) {
  if (status === 'USED') return 'USED';
  if (status === 'SPARE') return 'SPARE';
  return 'UNRESOLVED';
}

/**
 * Build the complete typed catalogue from inventory CSV text. Fail-closed: wrong header,
 * wrong column count, wrong row count, row loss or class-count drift are refused. Source
 * values are never modified; DERIVED/Product fields are separate keys.
 */
export function buildCatalogue(csvText) {
  const rows = parseInventoryCsv(csvText);
  const header = rows[0];
  const expectedDerived = [
    'Source Row Ordinal (DERIVED)', ...SOURCE_COLUMNS,
    'Io Class (DERIVED)', 'Source Tag Present (DERIVED)', 'Source Classification (DERIVED)',
    'Parse Diagnostic (DERIVED)', 'Provenance Artifact (DERIVED)', 'Provenance SHA-256 (DERIVED)',
  ];
  if (header.length !== expectedDerived.length
    || expectedDerived.some((name, i) => header[i] !== name)) {
    throw new CatalogueError('COLUMN_CONTRACT', 'inventory header differs from the generator contract');
  }
  const body = rows.slice(1);
  if (body.length !== EXPECTED_ROW_COUNT) {
    throw new CatalogueError('ROW_COUNT', `inventory has ${body.length} rows, expected ${EXPECTED_ROW_COUNT}`);
  }

  const classCounts = {};
  const byTag = new Map();
  const catalogueRows = body.map((cells, index) => {
    if (cells.length !== expectedDerived.length) {
      throw new CatalogueError('COLUMN_CONTRACT', `row ${index + 1} has ${cells.length} columns`);
    }
    const [ordinalText, ...rest] = cells;
    const ordinal = Number(ordinalText);
    if (!Number.isInteger(ordinal) || ordinal !== index + 1) {
      throw new CatalogueError('SOURCE_ORDER', `row ${index + 1} ordinal is ${ordinalText}`);
    }
    const source = Object.fromEntries(SOURCE_COLUMNS.map((name, i) => [name, rest[i]]));
    const derivedClass = rest[SOURCE_COLUMNS.length];
    const tagPresent = rest[SOURCE_COLUMNS.length + 1];
    const classification = rest[SOURCE_COLUMNS.length + 2];
    const parseDiagnostic = rest[SOURCE_COLUMNS.length + 3];
    const provenanceArtifact = rest[SOURCE_COLUMNS.length + 4];
    const provenanceSha256 = rest[SOURCE_COLUMNS.length + 5];
    if (provenanceSha256 !== INVENTORY_SOURCE_SHA256) {
      throw new CatalogueError('PROVENANCE', `row ${ordinal} provenance hash mismatch`);
    }
    classCounts[derivedClass] = (classCounts[derivedClass] ?? 0) + 1;
    const tag = source['I/O Tag'];
    const tagged = tag !== '' && tag !== '-';
    if (tagPresent !== (tagged ? 'YES' : 'NO')) {
      throw new CatalogueError('COLUMN_CONTRACT', `row ${ordinal} tag-present flag disagrees with source tag`);
    }
    if (tagged) {
      if (byTag.has(tag)) throw new CatalogueError('DUPLICATE_TAG', `tag ${tag} repeated`);
      byTag.set(tag, ordinal);
    }
    return Object.freeze({
      sourceRowOrdinal: ordinal,
      source,
      derived: Object.freeze({
        ioClass: derivedClass,
        sourceTagPresent: tagged,
        sourceClassification: classificationOf(classification),
        parseDiagnostic: parseDiagnostic === '' ? [] : Object.freeze(parseDiagnostic.split(';')),
        provenanceArtifact,
        provenanceSha256,
      }),
      // Product-side state (DERIVED, non-authoritative; updated by the binding layer).
      product: Object.freeze({
        bindingState: 'UNBOUND',
        commissioningState: 'EXCLUDED',
      }),
    });
  });

  for (const [cls, expected] of Object.entries(EXPECTED_CLASS_COUNTS)) {
    if ((classCounts[cls] ?? 0) !== expected) {
      throw new CatalogueError('CLASS_COUNT', `class ${cls}: ${classCounts[cls] ?? 0} rows, expected ${expected}`);
    }
  }
  const classification = {};
  for (const row of catalogueRows) {
    const key = row.derived.sourceClassification;
    classification[key] = (classification[key] ?? 0) + 1;
    void sourceClassificationOf;
  }
  return Object.freeze({
    rows: Object.freeze(catalogueRows),
    byTag: Object.freeze(Object.fromEntries(byTag)),
    counts: Object.freeze({ total: catalogueRows.length, byClass: Object.freeze(classCounts), bySourceClassification: Object.freeze(classification) }),
  });
}

function classificationOf(value) {
  if (value === 'USED' || value === 'SPARE' || value === 'UNSET' || value === 'OTHER') return value;
  throw new CatalogueError('COLUMN_CONTRACT', `unknown source classification ${value}`);
}

/** Default location of the committed inventory inside the repository checkout. */
export function defaultInventoryPath() {
  const here = dirname(fileURLToPath(import.meta.url));
  return join(here, '..', '..', '..', ...INVENTORY_RELATIVE_PATH);
}

/** Load and build the catalogue from the committed inventory file. */
export function loadCatalogue(inventoryPath = defaultInventoryPath()) {
  return buildCatalogue(readFileSync(inventoryPath, 'utf8'));
}
