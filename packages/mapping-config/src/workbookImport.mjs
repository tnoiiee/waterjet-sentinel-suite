// Stage 0.4B-1 — Excel-default import pipeline (generic).
//
// Reads an .xlsx workbook with Node built-ins only (zlib, crypto). It parses the
// complete sheet, preserves cell values as stored, and reports every issue
// instead of repairing it. Nothing in this module carries plant data: the
// workbook is supplied by path at run time, and the runtime-tag bindings are
// supplied by a separate local seed. Both files must live outside the Git
// working tree.

import { createHash } from 'node:crypto';
import { inflateRawSync } from 'node:zlib';
import { CHANNEL_TYPE, DIRECTION, CONTACT, SIGNAL, SEVERITY, OWNER_LIMIT_CONTACT_POLARITY, POLARITY_BASIS } from './constants.mjs';
import { getProfile } from './moduleProfiles.mjs';
import { buildModuleInstances, validateRack } from './rack.mjs';
import { validateMapping } from './mappingValidation.mjs';

const MAX_UNCOMPRESSED_BYTES = 32 * 1024 * 1024;
const EXPECTED_COLUMNS = Object.freeze([
  { key: 'slot', label: 'Slot', pattern: /^slot$/i },
  { key: 'module', label: 'Module', pattern: /module$/i },
  { key: 'channel', label: 'Channel', pattern: /^channel$/i },
  { key: 'tag', label: 'I/O Tag', pattern: /^i\/o tag$/i },
  { key: 'signal', label: 'Signal / Description', pattern: /^signal/i },
  { key: 'ioType', label: 'I/O Type', pattern: /^i\/o type$/i },
  { key: 'ref', label: 'Ref.', pattern: /^ref\.?$/i },
  { key: 'location', label: 'Location', pattern: /^location$/i },
  { key: 'note', label: 'Contact / Note', pattern: /^contact|^note/i },
  { key: 'status', label: 'Status', pattern: /^status$/i },
]);

function xmlDecode(s) {
  return s
    .replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'")
    .replace(/&#x([0-9a-fA-F]+);/g, (_, h) => String.fromCodePoint(parseInt(h, 16)))
    .replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(parseInt(d, 10)))
    .replace(/&amp;/g, '&');
}

/** Minimal ZIP reader (stored and deflated entries). Refuses oversized output. */
export function readZipEntries(buf) {
  const EOCD = 0x06054b50;
  let eocd = -1;
  for (let i = buf.length - 22; i >= Math.max(0, buf.length - 65557); i -= 1) {
    if (buf.readUInt32LE(i) === EOCD) { eocd = i; break; }
  }
  if (eocd < 0) throw new Error('not a ZIP container (end of central directory not found)');
  const count = buf.readUInt16LE(eocd + 10);
  let p = buf.readUInt32LE(eocd + 16);
  const entries = new Map();
  let total = 0;
  for (let n = 0; n < count; n += 1) {
    if (buf.readUInt32LE(p) !== 0x02014b50) throw new Error('corrupt central directory');
    const method = buf.readUInt16LE(p + 10);
    const compSize = buf.readUInt32LE(p + 20);
    const nameLen = buf.readUInt16LE(p + 28);
    const extraLen = buf.readUInt16LE(p + 30);
    const commentLen = buf.readUInt16LE(p + 32);
    const localOffset = buf.readUInt32LE(p + 42);
    const name = buf.toString('utf8', p + 46, p + 46 + nameLen);
    p += 46 + nameLen + extraLen + commentLen;

    if (buf.readUInt32LE(localOffset) !== 0x04034b50) throw new Error(`corrupt local header for ${name}`);
    const lNameLen = buf.readUInt16LE(localOffset + 26);
    const lExtraLen = buf.readUInt16LE(localOffset + 28);
    const start = localOffset + 30 + lNameLen + lExtraLen;
    const raw = buf.subarray(start, start + compSize);
    let data;
    if (method === 0) data = raw;
    else if (method === 8) data = inflateRawSync(raw, { maxOutputLength: MAX_UNCOMPRESSED_BYTES });
    else throw new Error(`unsupported ZIP method ${method} for ${name}`);
    total += data.length;
    if (total > MAX_UNCOMPRESSED_BYTES) throw new Error('uncompressed workbook exceeds the import limit');
    entries.set(name, data);
  }
  return entries;
}

export function parseSharedStrings(xml) {
  const out = [];
  const siRe = /<si\b[^>]*>([\s\S]*?)<\/si>/g;
  let m;
  while ((m = siRe.exec(xml)) !== null) {
    const parts = [];
    const tRe = /<t\b[^>]*>([\s\S]*?)<\/t>/g;
    let t;
    while ((t = tRe.exec(m[1])) !== null) parts.push(xmlDecode(t[1]));
    out.push(parts.join(''));
  }
  return out;
}

function colLetters(ref) {
  return /^([A-Z]+)/.exec(ref)[1];
}

/** Parses one worksheet into rows of raw cell text (null for empty cells). */
export function parseSheet(xml, sharedStrings) {
  const rows = [];
  let formulaCount = 0;
  const rowRe = /<row\b([^>]*)>([\s\S]*?)<\/row>/g;
  let rm;
  while ((rm = rowRe.exec(xml)) !== null) {
    const rowNumber = Number(/\br="(\d+)"/.exec(rm[1])?.[1]);
    const hidden = /\bhidden="1"/.test(rm[1]);
    const cells = {};
    const cellRe = /<c\b([^>]*?)(?:\/>|>([\s\S]*?)<\/c>)/g;
    let cm;
    while ((cm = cellRe.exec(rm[2])) !== null) {
      const attrs = cm[1];
      const ref = /\br="([A-Z]+\d+)"/.exec(attrs)?.[1];
      const type = /\bt="([^"]+)"/.exec(attrs)?.[1];
      const body = cm[2] ?? '';
      if (/<f\b/.test(body)) formulaCount += 1;
      let value = null;
      if (type === 'inlineStr') {
        value = [...body.matchAll(/<t\b[^>]*>([\s\S]*?)<\/t>/g)].map((x) => xmlDecode(x[1])).join('');
      } else {
        const v = /<v>([\s\S]*?)<\/v>/.exec(body)?.[1];
        if (v !== undefined) {
          if (type === 's') value = sharedStrings[Number(v)] ?? null;
          else value = xmlDecode(v);
        }
      }
      if (ref) cells[colLetters(ref)] = value;
    }
    rows.push({ rowNumber, hidden, cells });
  }
  return { rows, formulaCount };
}

function workbookSheets(entries) {
  const wb = entries.get('xl/workbook.xml')?.toString('utf8') ?? '';
  const rels = entries.get('xl/_rels/workbook.xml.rels')?.toString('utf8') ?? '';
  const relTarget = new Map();
  for (const m of rels.matchAll(/<Relationship\b[^>]*>/g)) {
    const id = /\bId="([^"]+)"/.exec(m[0])?.[1];
    const target = /\bTarget="([^"]+)"/.exec(m[0])?.[1];
    if (id && target) relTarget.set(id, target);
  }
  return [...wb.matchAll(/<sheet\b[^>]*>/g)].map((m) => {
    const name = xmlDecode(/\bname="([^"]+)"/.exec(m[0])?.[1] ?? '');
    const rid = /\br:id="([^"]+)"/.exec(m[0])?.[1];
    const target = relTarget.get(rid) ?? '';
    const path = target.startsWith('/') ? target.slice(1) : `xl/${target}`;
    const state = /\bstate="([^"]+)"/.exec(m[0])?.[1] ?? 'visible';
    return { name, path, state };
  });
}

function text(v) {
  return v == null ? '' : String(v).trim();
}

function dash(v) {
  const t = text(v);
  return t === '' || t === '-' ? null : t;
}

function parseIoType(ioType, channelType) {
  const t = text(ioType);
  if (/4\s*-\s*20/.test(t)) return { signal: SIGNAL.CURRENT_4_20_MA, channelType: CHANNEL_TYPE.ANALOG };
  if (/0\s*-\s*20/.test(t)) return { signal: SIGNAL.CURRENT_0_20_MA, channelType: CHANNEL_TYPE.ANALOG };
  if (/DI\s*\(24|DO\s*\(24/i.test(t)) return { signal: SIGNAL.DIGITAL_24_VDC, channelType: CHANNEL_TYPE.DIGITAL };
  return { signal: null, channelType };
}

/**
 * Imports the default configuration.
 *   buf          workbook bytes
 *   options.sheetName   required when the workbook has more than one visible sheet
 *   options.bindingSeed local seed: { runtimeTagName: { source: workbookTagId, engineering?, activePolarity?, declaredSourceIdentity? } }
 */
export function importWorkbook(buf, options = {}) {
  const issues = [];
  const entries = readZipEntries(buf);
  const sheets = workbookSheets(entries).filter((s) => s.state === 'visible');
  if (sheets.length === 0) throw new Error('workbook has no visible sheet');
  let sheet;
  if (options.sheetName) {
    sheet = sheets.find((s) => s.name === options.sheetName);
    if (!sheet) throw new Error(`sheet '${options.sheetName}' not found`);
  } else if (sheets.length === 1) {
    [sheet] = sheets;
  } else {
    throw new Error('workbook has several visible sheets; sheetName is required');
  }

  const shared = entries.has('xl/sharedStrings.xml')
    ? parseSharedStrings(entries.get('xl/sharedStrings.xml').toString('utf8'))
    : [];
  const sheetXml = entries.get(sheet.path)?.toString('utf8');
  if (sheetXml === undefined) throw new Error(`sheet part ${sheet.path} missing`);
  const { rows, formulaCount } = parseSheet(sheetXml, shared);
  const dimension = /<dimension ref="([^"]+)"/.exec(sheetXml)?.[1] ?? null;
  const mergeCount = (sheetXml.match(/<mergeCell\b/g) ?? []).length;
  const hiddenRows = rows.filter((r) => r.hidden).length;
  const hiddenCols = (sheetXml.match(/<col\b[^>]*hidden="1"/g) ?? []).length;

  const header = rows[0];
  const headerMismatch = [];
  const cols = {};
  EXPECTED_COLUMNS.forEach((c, i) => {
    const letter = String.fromCharCode(65 + i);
    const observed = text(header?.cells[letter]);
    cols[c.key] = letter;
    if (!c.pattern.test(observed)) headerMismatch.push({ column: letter, expected: c.label, observed });
  });
  if (headerMismatch.length > 0) {
    throw new Error(`header mismatch: ${JSON.stringify(headerMismatch)}`);
  }

  const dataRows = rows.slice(1).filter((r) => EXPECTED_COLUMNS.some((c) => text(r.cells[cols[c.key]]) !== ''));
  const records = [];
  const seenRowKeys = new Map();
  for (const r of dataRows) {
    const get = (key) => r.cells[cols[key]] ?? null;
    const rec = {
      rowNumber: r.rowNumber,
      slot: text(get('slot')),
      modelNumber: text(get('module')),
      channel: dash(get('channel')),
      tag: dash(get('tag')),
      signal: text(get('signal')),
      ioType: text(get('ioType')),
      ref: dash(get('ref')),
      location: dash(get('location')),
      note: dash(get('note')),
      status: text(get('status')),
    };
    const rowKey = JSON.stringify([rec.slot, rec.modelNumber, rec.channel, rec.tag, rec.signal, rec.ioType, rec.ref, rec.location, rec.note, rec.status]);
    if (seenRowKeys.has(rowKey)) {
      issues.push({ code: 'DUPLICATE_COMPLETE_ROW', severity: SEVERITY.ERROR, row: rec.rowNumber, message: `row ${rec.rowNumber} duplicates row ${seenRowKeys.get(rowKey)}` });
    } else {
      seenRowKeys.set(rowKey, rec.rowNumber);
    }
    records.push(rec);
  }

  // ----- structural checks -----
  for (const rec of records) {
    if (!/^\d+$/.test(rec.slot)) {
      issues.push({ code: 'MALFORMED_SLOT', severity: SEVERITY.ERROR, row: rec.rowNumber, message: `Slot '${rec.slot}' is not an integer` });
    }
    if (rec.modelNumber === '') {
      issues.push({ code: 'BLANK_MODULE', severity: SEVERITY.ERROR, row: rec.rowNumber, message: 'Module is blank' });
    } else if (!/^\d{3}-\d{3}$/.test(rec.modelNumber)) {
      issues.push({ code: 'MALFORMED_MODULE', severity: SEVERITY.ERROR, row: rec.rowNumber, message: `Module '${rec.modelNumber}' is not a model number` });
    } else if (!getProfile(rec.modelNumber)) {
      issues.push({ code: 'UNKNOWN_MODEL_PROFILE', severity: SEVERITY.ERROR, row: rec.rowNumber, message: `Module '${rec.modelNumber}' has no Module Profile` });
    }
    if (rec.channel !== null && !/^\d+$/.test(rec.channel)) {
      issues.push({ code: 'MALFORMED_CHANNEL', severity: SEVERITY.ERROR, row: rec.rowNumber, message: `Channel '${rec.channel}' is not an integer` });
    }
    if (!['USED', 'SPARE'].includes(rec.status)) {
      issues.push({ code: 'STATUS_UNRECOGNISED', severity: SEVERITY.ERROR, row: rec.rowNumber, message: `Status '${rec.status}' is not USED or SPARE` });
    }
    if (rec.status === 'USED' && rec.channel === null) {
      const p = getProfile(rec.modelNumber);
      if (p && p.channelCount > 0) {
        issues.push({ code: 'BLANK_CHANNEL', severity: SEVERITY.ERROR, row: rec.rowNumber, message: 'Channel is blank on a channel module' });
      }
    }
    if (rec.status === 'USED' && rec.channel !== null && rec.tag === null) {
      issues.push({ code: 'BLANK_IO_TAG', severity: SEVERITY.ERROR, row: rec.rowNumber, message: 'USED channel has no I/O Tag' });
    }
    if (rec.signal === '' ) {
      issues.push({ code: 'BLANK_SIGNAL', severity: SEVERITY.WARNING, row: rec.rowNumber, message: 'Signal / Description is blank' });
    }
    if (/XXX/.test(rec.signal)) {
      // Placeholder row: kept as USED, never bound, never renamed or reordered.
      issues.push({ code: 'SIGNAL_IDENTITY_UNRESOLVED', severity: SEVERITY.WARNING, row: rec.rowNumber, message: 'placeholder signal identity; kept USED, no Tag binding' });
      issues.push({ code: 'OWNER_INPUT_PENDING', severity: SEVERITY.WARNING, row: rec.rowNumber, message: 'Owner must supply the signal identity for this row' });
    }
  }

  const tagRows = new Map();
  for (const rec of records) {
    if (rec.tag === null) continue;
    if (!tagRows.has(rec.tag)) tagRows.set(rec.tag, []);
    tagRows.get(rec.tag).push(rec.rowNumber);
  }
  for (const [tag, list] of tagRows) {
    if (list.length > 1) issues.push({ code: 'DUPLICATE_IO_TAG', severity: SEVERITY.ERROR, tag, rows: list, message: `I/O Tag ${tag} appears more than once` });
  }

  const bySlot = new Map();
  for (const rec of records) {
    if (!/^\d+$/.test(rec.slot)) continue;
    const s = Number(rec.slot);
    if (!bySlot.has(s)) bySlot.set(s, { models: new Set(), channels: new Map(), rows: [] });
    const entry = bySlot.get(s);
    entry.models.add(rec.modelNumber);
    entry.rows.push(rec);
    if (rec.channel !== null && /^\d+$/.test(rec.channel)) {
      const c = Number(rec.channel);
      if (entry.channels.has(c)) {
        issues.push({ code: 'DUPLICATE_SLOT_CHANNEL', severity: SEVERITY.ERROR, slot: s, channel: c, message: `Slot ${s} Channel ${c} assigned twice` });
      }
      entry.channels.set(c, rec);
    }
  }
  const slotNumbers = [...bySlot.keys()].sort((a, b) => a - b);
  slotNumbers.forEach((s, i) => {
    if (s !== i + 1) issues.push({ code: 'RACK_SLOT_GAP', severity: SEVERITY.ERROR, slot: s, message: `expected slot ${i + 1}, found ${s}` });
  });
  for (const s of slotNumbers) {
    const e = bySlot.get(s);
    if (e.models.size !== 1) {
      issues.push({ code: 'SLOT_MODEL_CONFLICT', severity: SEVERITY.ERROR, slot: s, message: `slot ${s} lists ${e.models.size} models` });
      continue;
    }
    const modelNumber = [...e.models][0];
    const profile = getProfile(modelNumber);
    if (!profile) continue;
    const declared = e.channels.size;
    if (profile.channelCount > 0) {
      if (declared !== profile.channelCount) {
        issues.push({ code: 'CHANNEL_COUNT_MISMATCH', severity: SEVERITY.ERROR, slot: s, message: `slot ${s} lists ${declared} channels; profile has ${profile.channelCount}` });
      }
      for (let c = 1; c <= profile.channelCount; c += 1) {
        if (!e.channels.has(c)) {
          issues.push({ code: 'CHANNEL_MISSING', severity: SEVERITY.ERROR, slot: s, channel: c, message: `slot ${s} channel ${c} is not listed` });
        }
      }
    }
  }

  // ----- deterministic model -----
  const knownSlots = slotNumbers.filter((s) => {
    const e = bySlot.get(s);
    return e.models.size === 1 && getProfile([...e.models][0]) !== null;
  });
  const modules = buildModuleInstances(knownSlots.map((s) => [...bySlot.get(s).models][0]));
  const instanceBySlot = new Map(knownSlots.map((s, i) => [s, modules[i].moduleInstanceId]));
  const declaredChannelCounts = {};
  for (const s of slotNumbers) {
    const id = instanceBySlot.get(s);
    if (id) declaredChannelCounts[id] = bySlot.get(s).channels.size;
  }

  const seed = options.bindingSeed ?? {};
  const bindings = [];
  const seedIssues = [];
  for (const [runtimeTag, spec] of Object.entries(seed).sort(([a], [b]) => a.localeCompare(b))) {
    const row = records.find((r) => r.tag === spec.source);
    if (!row || row.channel === null) {
      seedIssues.push({ code: 'SEED_TAG_NOT_FOUND', severity: SEVERITY.ERROR, tagName: runtimeTag, message: `seed source for ${runtimeTag} is not a channel row in the workbook` });
      continue;
    }
    const slot = Number(row.slot);
    // A limit with no polarity in the seed takes the Owner rule, recorded explicitly with its basis.
    // Pressures never take a polarity. An explicit seed value is kept as written.
    const isLimit = /^IV[1-8]_(LOWER|UPPER)_LIMIT$/.test(runtimeTag);
    let activePolarity = spec.activePolarity ?? null;
    let polarityBasis = null;
    if (isLimit && activePolarity === null) {
      activePolarity = options.limitContactPolarity ?? OWNER_LIMIT_CONTACT_POLARITY;
      polarityBasis = POLARITY_BASIS.OWNER_RULE;
    } else if (isLimit) {
      polarityBasis = POLARITY_BASIS.EXPLICIT_SEED;
    }
    bindings.push({
      tagName: runtimeTag,
      moduleInstanceId: instanceBySlot.get(slot),
      channel: Number(row.channel),
      enabled: spec.enabled !== false,
      engineering: spec.engineering ?? null,
      activePolarity,
      polarityBasis,
      contactType: /\(NO\)/.test(row.signal) ? CONTACT.NO : null,
      declaredSourceIdentity: spec.declaredSourceIdentity,
      sourceWorkbookTag: spec.source,
    });
  }

  // Read-only additional input tags: only unambiguous input rows. They are
  // listed but disabled until an authorised mapping update enables them.
  const additionalTags = {};
  const seedSources = new Set(bindings.map((b) => b.sourceWorkbookTag));
  const classifications = { inputUnambiguous: 0, inputAmbiguous: 0, outputNotAuthorised: 0, reservedUnresolved: 0, nonChannel: 0, spare: 0, boundBySeed: 0 };
  for (const rec of records) {
    if (rec.status === 'SPARE') { classifications.spare += 1; continue; }
    if (rec.tag !== null && seedSources.has(rec.tag)) { classifications.boundBySeed += 1; continue; }
    if (rec.channel === null) { classifications.nonChannel += 1; continue; }
    const profile = getProfile(rec.modelNumber);
    if (!profile) continue;
    if (profile.direction === DIRECTION.OUTPUT) { classifications.outputNotAuthorised += 1; continue; }
    if (/XXX/.test(rec.signal)) { classifications.reservedUnresolved += 1; continue; }
    const io = parseIoType(rec.ioType, profile.channelType);
    const ambiguous = rec.tag === null || rec.signal === '' || io.signal === null || io.channelType !== profile.channelType
      || !profile.supportedSignals.includes(io.signal) || /^(DI|AI)$/i.test(rec.ioType);
    if (ambiguous) {
      classifications.inputAmbiguous += 1;
      seedIssues.push({ code: 'AMBIGUOUS_INPUT_DESCRIPTION', severity: SEVERITY.WARNING, row: rec.rowNumber, message: 'input row is not unambiguous; not offered as a read-only tag' });
      continue;
    }
    classifications.inputUnambiguous += 1;
    additionalTags[rec.tag] = Object.freeze({
      tagName: rec.tag,
      role: 'READ_ONLY_WORKBOOK_INPUT',
      direction: DIRECTION.INPUT,
      channelType: profile.channelType,
      signal: io.signal,
      sourceIdentity: rec.tag,
      pairIndex: null,
      readOnlyStage: true,
      confirmedEngineeringRange: null,
    });
    const slot = Number(rec.slot);
    bindings.push({
      tagName: rec.tag,
      moduleInstanceId: instanceBySlot.get(slot),
      channel: Number(rec.channel),
      enabled: false,
      engineering: null,
      activePolarity: null,
      contactType: /\(NO\)/.test(rec.signal) ? CONTACT.NO : null,
      sourceWorkbookTag: rec.tag,
    });
  }
  // Output and reserved rows are never bound (see classifications).

  bindings.sort((a, b) => a.tagName.localeCompare(b.tagName));
  const mappingIssues = validateMapping(modules, bindings, additionalTags);
  const rackIssues = validateRack(modules, declaredChannelCounts);
  const allIssues = [...issues, ...seedIssues, ...rackIssues, ...mappingIssues];

  const sha256 = createHash('sha256').update(buf).digest('hex');
  return Object.freeze({
    source: Object.freeze({ sha256, sizeBytes: buf.length, sheetName: sheet.name, dimension, dataRowCount: records.length,
      headerRowCount: 1, formulaCount, mergedCellCount: mergeCount, hiddenRowCount: hiddenRows, hiddenColumnCount: hiddenCols }),
    label: 'DEFAULT FROM EXCEL',
    modules,
    bindings,
    declaredChannelCounts,
    additionalTags,
    classifications,
    issues: allIssues.slice().sort((a, b) => a.code.localeCompare(b.code) || String(a.row ?? a.slot ?? '').localeCompare(String(b.row ?? b.slot ?? ''))),
    records,
    seedIssueCount: seedIssues.length,
    counts: countStatus(records),
  });
}

function countStatus(records) {
  const out = { USED: 0, SPARE: 0, total: records.length };
  for (const r of records) {
    if (r.status === 'USED') out.USED += 1;
    else if (r.status === 'SPARE') out.SPARE += 1;
  }
  return out;
}

