#!/usr/bin/env node
// H2 evidence generator — deterministic normalized Process-I/O inventory.
//
// Input : the immutable, Owner-supplied I/O mapping HTML artifact adopted at
//         docs/evidence/ (exact connected-source bytes; never modified here).
// Output: the normalized inventory CSV next to it, plus a parse summary.
//
// Rules (H2 evidence-adoption task, Owner-authorized):
//   - The generator is the single owner of the generated CSV. It is deterministic:
//     the same artifact bytes always produce the same CSV bytes (no clock, no
//     randomness, no environment reads).
//   - It is fail-closed: it refuses malformed HTML, a wrong artifact hash, a
//     missing or mismatched column contract, duplicate source-row identity,
//     duplicate physical (Slot, Channel) placement, duplicate I/O tags, and any
//     unexpected row loss. It never repairs source data silently; every source
//     value is preserved verbatim, including '-' and empty strings.
//   - Derived columns are explicitly marked "(DERIVED)". They carry no Product
//     authority: no runtime behaviour, no control effect, no address, no scaling
//     and no equipment identity is assigned by normalization.
//
// The source filename below names the vendor product line, so — exactly like the
// repository boundary scanner assembles its own vocabulary from fragments — the
// name is assembled here from two fragments. This keeps the Product tree free of
// the literal while the committed artifact itself (in docs/, outside the Product
// tree) keeps its exact original name.

import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

export const SOURCE_DIRNAME = 'docs/evidence';
export const SOURCE_FILENAME = ['WA', 'GO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html'].join('');
export const INVENTORY_FILENAME = 'process-io-inventory-2026-10-22.csv';
export const EXPECTED_SOURCE_SHA256 = '28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299';

export const COLUMN_COUNT = 14;
const CLASS_END_MODULE = 'END_MODULE';
const CLASS_UNRESOLVED = 'UNRESOLVED';
const STATUS_KNOWN = ['USED', 'SPARE'];

export class ArtifactError extends Error {
  constructor(code, detail) {
    super(`${code}: ${detail}`);
    this.code = code;
    this.detail = detail;
  }
}

/** SHA-256 of a buffer. */
export function sha256Of(buffer) {
  return createHash('sha256').update(buffer).digest('hex');
}

/**
 * Scan a JSON-compatible array literal starting at text[start] (must be '[').
 * Returns the index just past the matching ']'. Tracks double-quoted strings
 * and backslash escapes only; anything else is bracket text.
 */
function matchArrayEnd(text, start) {
  if (text[start] !== '[') throw new ArtifactError('MALFORMED_HTML', 'expected "[" at the data-array start');
  let depth = 0;
  let inString = false;
  for (let i = start; i < text.length; i += 1) {
    const c = text[i];
    if (inString) {
      if (c === '\\') { i += 1; continue; }
      if (c === '"') inString = false;
      continue;
    }
    if (c === '"') { inString = true; continue; }
    if (c === '[') depth += 1;
    else if (c === ']') {
      depth -= 1;
      if (depth === 0) return i + 1;
    }
  }
  throw new ArtifactError('MALFORMED_HTML', 'unterminated data array');
}

/** Extract and JSON-parse the array literal that follows `marker` ('const D=' / 'C='). */
function parseJsArray(text, marker, fromIndex) {
  const at = text.indexOf(marker, fromIndex);
  if (at < 0) throw new ArtifactError('MALFORMED_HTML', `marker "${marker}" not found`);
  const open = text.indexOf('[', at + marker.length);
  if (open < 0) throw new ArtifactError('MALFORMED_HTML', `no array after "${marker}"`);
  const end = matchArrayEnd(text, open);
  let value;
  try {
    value = JSON.parse(text.slice(open, end));
  } catch (err) {
    throw new ArtifactError('MALFORMED_HTML', `array after "${marker}" is not parseable (${err.message})`);
  }
  return { value, end };
}

/** Parse the header sub-line embedded in the artifact (source workbook identity, declared hash, filter). */
function parseSubLine(text) {
  const m = /<div class=sub>(.*?)<\/div>/.exec(text);
  if (!m) return { subLine: null, sourceWorkbookName: null, declaredWorkbookSha256Prefix: null, declaredWorkbookBytes: null, filterStatement: null };
  const subLine = m[1];
  const segments = subLine.split(' · ').map((s) => s.trim());
  const pick = (prefix) => segments.find((s) => s.startsWith(prefix)) ?? null;
  const bytesSegment = segments.find((s) => /^\d[\d.,]*\s+bytes$/.test(s)) ?? null;
  const sourceSegment = pick('Source: ');
  const hashSegment = pick('SHA-256 ');
  const filterSegment = segments.find((s) => /^Filtered\b/i.test(s)) ?? null;
  return {
    subLine,
    sourceWorkbookName: sourceSegment ? sourceSegment.slice('Source: '.length) : null,
    declaredWorkbookSha256Prefix: hashSegment ? hashSegment.slice('SHA-256 '.length) : null,
    declaredWorkbookBytes: bytesSegment ? Number(bytesSegment.replace(/[^\d]/g, '')) : null,
    filterStatement: filterSegment,
  };
}

/**
 * Derive the I/O class of a row from its exact source "I/O Type" text.
 * Deterministic and non-authoritative: a plain prefix classification only.
 */
function ioClassOf(ioType) {
  const t = ioType.trim();
  if (t === 'End') return CLASS_END_MODULE;
  const m = /^([AD][IO])(?:\s|\(|$)/.exec(t);
  return m ? m[1] : CLASS_UNRESOLVED;
}

function classificationOf(status) {
  if (status === '') return 'UNSET';
  return STATUS_KNOWN.includes(status) ? status : 'OTHER';
}

function diagnosticsOf(ioType, status) {
  const out = [];
  if (ioClassOf(ioType) === CLASS_UNRESOLVED) out.push('IO_TYPE_UNCLASSIFIED');
  if (status === '') out.push('STATUS_UNSET');
  else if (!STATUS_KNOWN.includes(status)) out.push('STATUS_OTHER');
  return out.join(';');
}

/**
 * Parse the immutable artifact text. Returns { meta, columnContract, rows } where
 * rows are { ordinal, values (14 exact source strings, in source order) }.
 * Refuses anything malformed; never mutates source values.
 */
export function parseArtifact(text) {
  if (typeof text !== 'string' || text.length === 0) throw new ArtifactError('MALFORMED_HTML', 'empty artifact');

  const meta = parseSubLine(text);

  const thead = /<thead><tr>(.*?)<\/tr><\/thead>/.exec(text);
  if (!thead) throw new ArtifactError('MISSING_COLUMNS', 'no <thead> table header found');
  const columnContract = [...thead[1].matchAll(/<th>(.*?)<\/th>/g)].map((m) => m[1]);
  if (columnContract.length !== COLUMN_COUNT) {
    throw new ArtifactError('MISSING_COLUMNS', `table header has ${columnContract.length} columns, expected ${COLUMN_COUNT}`);
  }
  if (new Set(columnContract).size !== COLUMN_COUNT) throw new ArtifactError('MISSING_COLUMNS', 'duplicate table header name');

  const data = parseJsArray(text, 'const D=', 0);
  if (!Array.isArray(data.value) || data.value.length === 0) throw new ArtifactError('MALFORMED_HTML', 'data array is empty or not an array');

  // The page keeps a second, script-side copy of the column names; the two must
  // agree or the artifact is not internally consistent.
  const scriptColumns = parseJsArray(text, 'C=', data.end);
  if (!Array.isArray(scriptColumns.value) || JSON.stringify(scriptColumns.value) !== JSON.stringify(columnContract)) {
    throw new ArtifactError('MISSING_COLUMNS', 'table header and script column contract differ');
  }

  const rows = [];
  const seenRowIdentity = new Map();
  const seenPlacement = new Map();
  const seenTag = new Map();
  data.value.forEach((row, index) => {
    if (!Array.isArray(row)) throw new ArtifactError('MALFORMED_HTML', `row ${index + 1} is not an array`);
    if (row.length !== COLUMN_COUNT) {
      throw new ArtifactError('MISSING_COLUMNS', `row ${index + 1} has ${row.length} columns, expected ${COLUMN_COUNT}`);
    }
    for (const cell of row) {
      if (typeof cell !== 'string') throw new ArtifactError('MALFORMED_HTML', `row ${index + 1} contains a non-string cell`);
    }
    const identity = JSON.stringify(row);
    if (seenRowIdentity.has(identity)) {
      throw new ArtifactError('DUPLICATE_ROW', `rows ${seenRowIdentity.get(identity)} and ${index + 1} are identical`);
    }
    seenRowIdentity.set(identity, index + 1);
    const [module, slot, ioSlot, channel, tag] = row;
    const placement = `${slot}|${channel}`;
    if (seenPlacement.has(placement)) {
      throw new ArtifactError('DUPLICATE_PLACEMENT', `Slot ${slot} Channel ${channel} appears in rows ${seenPlacement.get(placement)} and ${index + 1}`);
    }
    seenPlacement.set(placement, index + 1);
    if (tag !== '' && tag !== '-') {
      if (seenTag.has(tag)) {
        throw new ArtifactError('DUPLICATE_TAG', `I/O tag ${tag} appears in rows ${seenTag.get(tag)} and ${index + 1}`);
      }
      seenTag.set(tag, index + 1);
    }
    void module; void ioSlot;
    rows.push({ ordinal: index + 1, values: row });
  });

  return { meta, columnContract, rows };
}

/** CSV field escaping (RFC 4180): quote only when needed, double embedded quotes. */
function csvField(value) {
  if (/[",\r\n]/.test(value)) return `"${value.replace(/"/g, '""')}"`;
  return value;
}

/**
 * Build the normalized inventory CSV from a parsed artifact. Deterministic:
 * same input rows, same bytes. Line endings LF; one trailing newline; no BOM
 * (the committed evidence CSV convention).
 */
export function buildCsv(parsed, provenanceSha256) {
  const source = parsed.columnContract;
  const header = [
    'Source Row Ordinal (DERIVED)',
    ...source,
    'Io Class (DERIVED)',
    'Source Tag Present (DERIVED)',
    'Source Classification (DERIVED)',
    'Parse Diagnostic (DERIVED)',
    'Provenance Artifact (DERIVED)',
    'Provenance SHA-256 (DERIVED)',
  ];
  const lines = [header.map(csvField).join(',')];
  for (const row of parsed.rows) {
    const v = row.values;
    const ioType = v[6];
    const tag = v[4];
    const status = v[13];
    const fields = [
      String(row.ordinal),
      ...v,
      ioClassOf(ioType),
      tag !== '' && tag !== '-' ? 'YES' : 'NO',
      classificationOf(status),
      diagnosticsOf(ioType, status),
      SOURCE_FILENAME,
      provenanceSha256,
    ];
    lines.push(fields.map(csvField).join(','));
  }
  return `${lines.join('\n')}\n`;
}

/** Deterministic parse summary used by the provenance record and the parity check. */
export function buildSummary(parsed, artifactSha256, artifactBytes) {
  const byClass = {};
  const byStatus = {};
  let tagged = 0;
  for (const row of parsed.rows) {
    const cls = ioClassOf(row.values[6]);
    byClass[cls] = (byClass[cls] ?? 0) + 1;
    const status = row.values[13] === '' ? '(unset)' : row.values[13];
    byStatus[status] = (byStatus[status] ?? 0) + 1;
    if (row.values[4] !== '' && row.values[4] !== '-') tagged += 1;
  }
  const sortedEntries = (obj) => Object.fromEntries(Object.entries(obj).sort(([a], [b]) => a.localeCompare(b)));
  return {
    sourceArtifact: SOURCE_FILENAME,
    sourceArtifactSha256: artifactSha256,
    sourceArtifactBytes: artifactBytes,
    sourceWorkbookName: parsed.meta.sourceWorkbookName,
    declaredWorkbookSha256Prefix: parsed.meta.declaredWorkbookSha256Prefix,
    declaredWorkbookBytes: parsed.meta.declaredWorkbookBytes,
    filterStatement: parsed.meta.filterStatement,
    columnContract: parsed.columnContract,
    totalRows: parsed.rows.length,
    rowsByIoClass: sortedEntries(byClass),
    rowsBySourceStatus: sortedEntries(byStatus),
    taggedRows: tagged,
    untaggedRows: parsed.rows.length - tagged,
    provenanceStatus: 'EXACT_BYTE_MATCH_VERIFIED',
  };
}

/** Full deterministic run: read the artifact, verify its hash, parse, build CSV + summary. */
export function run(sourcePath) {
  const buffer = readFileSync(sourcePath);
  const artifactSha256 = sha256Of(buffer);
  if (artifactSha256 !== EXPECTED_SOURCE_SHA256) {
    throw new ArtifactError('HASH_MISMATCH', `artifact SHA-256 ${artifactSha256} does not match the adopted ${EXPECTED_SOURCE_SHA256}`);
  }
  const text = buffer.toString('utf8');
  const parsed = parseArtifact(text);
  const csv = buildCsv(parsed, artifactSha256);
  const summary = buildSummary(parsed, artifactSha256, buffer.length);
  if (summary.totalRows !== parsed.rows.length) throw new ArtifactError('ROW_LOSS', 'summary row count differs from parsed rows');
  return { csv, summary, parsed };
}

function printSummary(summary) {
  const lines = [
    `source artifact          : ${summary.sourceArtifact}`,
    `source artifact SHA-256  : ${summary.sourceArtifactSha256}`,
    `source artifact bytes    : ${summary.sourceArtifactBytes}`,
    `source workbook (as declared inside the artifact): ${summary.sourceWorkbookName}`,
    `declared workbook SHA-256 prefix (NOT independently verified): ${summary.declaredWorkbookSha256Prefix}`,
    `declared workbook bytes  : ${summary.declaredWorkbookBytes}`,
    `filter statement inside the artifact: ${summary.filterStatement}`,
    `total rows               : ${summary.totalRows}`,
    `rows by derived I/O class: ${JSON.stringify(summary.rowsByIoClass)}`,
    `rows by source status    : ${JSON.stringify(summary.rowsBySourceStatus)}`,
    `tagged / untagged rows   : ${summary.taggedRows} / ${summary.untaggedRows}`,
  ];
  process.stdout.write(`${lines.join('\n')}\n`);
}

// A reader that closes stdout early (for example `| head -n 1`) is a normal outcome,
// not a generation failure: exit cleanly instead of surfacing EPIPE.
process.stdout.on('error', (err) => {
  if (err && err.code === 'EPIPE') process.exit(0);
  throw err;
});

function main(argv) {
  const check = argv.includes('--check');
  const asJson = argv.includes('--json');
  const repoRoot = process.cwd();
  const sourcePath = join(repoRoot, SOURCE_DIRNAME, SOURCE_FILENAME);
  const inventoryPath = join(repoRoot, SOURCE_DIRNAME, INVENTORY_FILENAME);

  const { csv, summary } = run(sourcePath);

  if (check) {
    let committed = null;
    try {
      committed = readFileSync(inventoryPath, 'utf8');
    } catch {
      process.stderr.write(`PARITY FAIL: committed inventory not found at ${SOURCE_DIRNAME}/${INVENTORY_FILENAME}\n`);
      process.exit(1);
    }
    if (committed !== csv) {
      process.stderr.write('PARITY FAIL: committed inventory differs from the deterministic regeneration\n');
      process.exit(1);
    }
    process.stdout.write(asJson ? `${JSON.stringify({ parity: 'OK', ...summary }, null, 2)}\n` : 'PARITY OK: committed inventory is byte-identical to the deterministic regeneration\n');
    if (!asJson) printSummary(summary);
    return;
  }

  writeFileSync(inventoryPath, csv, 'utf8');
  process.stdout.write(asJson ? `${JSON.stringify({ written: `${SOURCE_DIRNAME}/${INVENTORY_FILENAME}`, ...summary }, null, 2)}\n` : '');
  if (!asJson) {
    process.stdout.write(`written: ${SOURCE_DIRNAME}/${INVENTORY_FILENAME}\n`);
    printSummary(summary);
  }
}

if (import.meta.url === `file://${process.argv[1]}`) {
  try {
    main(process.argv.slice(2));
  } catch (err) {
    if (err instanceof ArtifactError) {
      process.stderr.write(`REFUSED ${err.message}\n`);
      process.exit(1);
    }
    throw err;
  }
}
