// Stage 0.4B-2 — read-only presentation model for the Mapping review surface.
//
// Pure functions over already-derived data. Nothing here changes a Draft, a binding, a revision or a derived
// address: filters, grouping, density and expansion choose which rows are SHOWN, never what they ARE. The
// functions copy what they need and never write back to their inputs.

import { CHANNEL_TYPE, SEVERITY } from './constants.mjs';
import { getTagDef } from './tagCatalogue.mjs';

export const DENSITY = Object.freeze({ COMFORTABLE: 'comfortable', COMPACT: 'compact' });
export const DEFAULT_DENSITY = DENSITY.COMFORTABLE;

export const GROUP_PUMP = 'Pump';
export const GROUP_OTHER = 'Other workbook inputs';
export const GROUP_RESERVED = 'Reserved / Owner pending';
export const IV_GROUPS = Object.freeze(Array.from({ length: 8 }, (_, i) => `IV${i + 1}`));
/** Display order of the equipment groups. */
export const GROUP_ORDER = Object.freeze([GROUP_PUMP, ...IV_GROUPS, GROUP_OTHER, GROUP_RESERVED]);

const READ_ONLY_ROLE = 'READ-ONLY WORKBOOK INPUT';

/** Equipment group of a mapped tag: Pump, IV1..IV8, or Other workbook inputs. #n maps directly to IVn. */
export function equipmentGroupOf(tagName, def) {
  if (def) {
    if (def.role === 'PUMP_INLET_PRESSURE' || def.role === 'PUMP_OUTLET_PRESSURE') return GROUP_PUMP;
    if (Number.isInteger(def.pairIndex) && def.pairIndex >= 1 && def.pairIndex <= 8) return `IV${def.pairIndex}`;
  }
  return GROUP_OTHER;
}

/** Filters, in display order. `all` is the default. */
export const TAG_FILTERS = Object.freeze([
  { key: 'all', label: 'All rows' },
  { key: 'runtime', label: 'Enabled Runtime bindings' },
  { key: 'readonly', label: 'Read-only workbook inputs' },
  { key: 'pending', label: 'Owner-input-pending / reserved' },
  { key: 'analog', label: 'Analog inputs' },
  { key: 'digital', label: 'Digital inputs' },
  { key: 'pump', label: 'Pump' },
  { key: 'iv', label: 'IV1-IV8' },
  { key: 'unresolved', label: 'Warnings / unresolved only' },
]);

const PREDICATES = Object.freeze({
  all: () => true,
  runtime: (r) => r.kind === 'binding' && r.enabled,
  // Listed workbook inputs without a catalogue identity, and listed bindings that are not enabled.
  readonly: (r) => r.kind === 'binding' && (!r.enabled || r.group === GROUP_OTHER),
  pending: (r) => r.kind === 'reserved',
  analog: (r) => r.signalClass === 'ANALOG_INPUT',
  digital: (r) => r.signalClass === 'DIGITAL_INPUT',
  pump: (r) => r.group === GROUP_PUMP,
  iv: (r) => IV_GROUPS.includes(r.group),
  unresolved: (r) => r.kind === 'reserved' || r.addressUnresolved || r.issueErrors > 0 || r.issueWarnings > 0,
});

/**
 * Builds one display row per binding entry and per reserved placeholder row.
 *   entries          derived address entries (deriveAddresses().entries)
 *   bindings         Draft bindings
 *   additionalTags   read-only tag definitions from the import
 *   issues           validation issues (rack + mapping); counted per tag
 *   reservedRows     reservedInventory.rows (workbook placeholders; never bindings)
 */
export function buildTagRows({ entries, bindings, additionalTags = {}, issues = [], reservedRows = [] }) {
  const bindingByTag = new Map(bindings.map((b) => [b.tagName, b]));
  const errors = new Map();
  const warnings = new Map();
  for (const i of issues) {
    if (!i.tagName) continue;
    if (i.severity === SEVERITY.ERROR) errors.set(i.tagName, (errors.get(i.tagName) ?? 0) + 1);
    else if (i.severity === SEVERITY.WARNING) warnings.set(i.tagName, (warnings.get(i.tagName) ?? 0) + 1);
  }
  const rows = entries.map((e) => {
    const b = bindingByTag.get(e.tagName) ?? {};
    const def = getTagDef(e.tagName);
    const extra = additionalTags[e.tagName];
    const channelType = def ? def.channelType : extra?.channelType ?? null;
    return {
      kind: 'binding',
      key: e.tagName,
      tag: e.tagName,
      role: def ? def.role : READ_ONLY_ROLE,
      group: equipmentGroupOf(e.tagName, def),
      moduleInstanceId: e.moduleInstanceId ?? null,
      channel: e.channel ?? null,
      enabled: b.enabled !== false,
      analog: channelType === CHANNEL_TYPE.ANALOG,
      signalClass: channelType === CHANNEL_TYPE.ANALOG ? 'ANALOG_INPUT' : channelType === CHANNEL_TYPE.DIGITAL ? 'DIGITAL_INPUT' : 'OTHER',
      activePolarity: b.activePolarity ?? null,
      contactType: b.contactType ?? null,
      addressState: e.state,
      addressUnresolved: e.state === 'ADDRESS_UNRESOLVED',
      displayNotation: e.displayNotation ?? null,
      bitOffsetAbsolute: e.bitOffsetAbsolute ?? null,
      reasons: [...e.reasons],
      issueErrors: errors.get(e.tagName) ?? 0,
      issueWarnings: warnings.get(e.tagName) ?? 0,
    };
  });
  for (const x of reservedRows) {
    const output = x.direction === 'OUTPUT';
    rows.push({
      kind: 'reserved',
      key: x.workbookTag,
      tag: x.workbookTag,
      role: output ? 'RESERVED · OWNER INPUT PENDING · OUTPUT NOT AUTHORIZED' : 'RESERVED · OWNER INPUT PENDING',
      group: GROUP_RESERVED,
      moduleInstanceId: x.moduleInstanceId ?? null,
      channel: x.channel ?? null,
      enabled: false,
      analog: false,
      // A reserved row is classified by its workbook identifier prefix; an output is neither analog nor digital input.
      signalClass: output ? 'OUTPUT' : /^AI-/.test(x.workbookTag) ? 'ANALOG_INPUT' : /^DI-/.test(x.workbookTag) ? 'DIGITAL_INPUT' : 'OTHER',
      activePolarity: null,
      contactType: null,
      addressState: x.addressStatus,
      addressUnresolved: true,
      displayNotation: null,
      bitOffsetAbsolute: null,
      reasons: [],
      issueErrors: 0,
      issueWarnings: 0,
      direction: x.direction,
    });
  }
  return rows;
}

export function filterRows(rows, filterKey) {
  const predicate = PREDICATES[filterKey];
  if (!predicate) throw new Error(`unknown filter ${filterKey}`);
  return rows.filter(predicate);
}

/** Count of rows each filter would show, in TAG_FILTERS order. */
export function filterCounts(rows) {
  return TAG_FILTERS.map((f) => ({ key: f.key, label: f.label, count: rows.filter(PREDICATES[f.key]).length }));
}

/**
 * Groups rows by equipment in GROUP_ORDER. Empty groups are omitted. Rows keep their incoming order inside a
 * group. Returns [{ group, rows }].
 */
export function groupRows(rows) {
  const out = [];
  for (const group of GROUP_ORDER) {
    const members = rows.filter((r) => r.group === group);
    if (members.length > 0) out.push({ group, rows: members });
  }
  return out;
}

/** Compact address summary. An unresolved address is never presented as a success state. */
export function compactAddressSummary(row) {
  if (row.kind === 'reserved') return 'ADDRESS UNRESOLVED · no binding';
  if (row.addressState === 'DERIVED') return `${row.displayNotation} (zero-based bit ${row.bitOffsetAbsolute})`;
  if (row.addressState === 'NOT_ACTIVE') return 'not active';
  const n = row.reasons.length;
  return `ADDRESS UNRESOLVED · ${n} ${n === 1 ? 'reason' : 'reasons'}`;
}

/** Issue status shown beside the compact address, so an ERROR is never hidden by the compact form. */
export function issueStatusText(row) {
  if (row.issueErrors > 0) return `ERROR × ${row.issueErrors}`;
  if (row.issueWarnings > 0) return `WARNING × ${row.issueWarnings}`;
  return 'OK';
}

/** Placeholder summary: input and output rows are shown separately and then added. */
export function placeholderSummary(reservedInventory) {
  const by = reservedInventory?.summary?.byDirection ?? { INPUT: 0, OUTPUT: 0 };
  const input = by.INPUT ?? 0;
  const output = by.OUTPUT ?? 0;
  const total = input + output;
  return {
    input,
    output,
    total,
    text: `Placeholder input rows ${input} + placeholder output rows ${output} = ${total} total Owner-input-pending rows`,
  };
}

// ------------------------------------------------------------------ validation grouping

/** Validation groups in fixed display order. ERRORS is first and is shown even when it is empty. */
export const VALIDATION_GROUPS = Object.freeze([
  { key: 'errors', label: 'Errors' },
  { key: 'profiles', label: 'Module profiles incomplete' },
  { key: 'ranges', label: 'Engineering ranges pending' },
  { key: 'owner', label: 'Owner identities pending' },
  { key: 'addresses', label: 'Unresolved addresses' },
  { key: 'other', label: 'Other warnings' },
]);

/**
 * Groups validation findings into a deterministic summary. Counts first, details second. Nothing is dropped:
 * every ERROR is in `errors`; every warning is in exactly one warning group.
 *   issues           rack + mapping issues
 *   addressEntries   derived address entries
 *   reservedSummary  reservedInventory.summary (or null)
 *   importIssues     workbook-import findings about pending Owner identities (never errors)
 */
export function groupValidation({ issues, addressEntries, reservedSummary = null, importIssues = [] }) {
  const sorted = [...issues].sort((a, b) => `${a.code}|${a.tagName ?? a.moduleInstanceId ?? ''}`.localeCompare(`${b.code}|${b.tagName ?? b.moduleInstanceId ?? ''}`));
  const bucket = { errors: [], profiles: [], ranges: [], owner: [], addresses: [], other: [] };
  for (const i of sorted) {
    if (i.severity === SEVERITY.ERROR) bucket.errors.push(describeIssue(i));
    else if (i.code === 'MODULE_PROFILE_INCOMPLETE') bucket.profiles.push(describeIssue(i));
    else if (i.code === 'ENGINEERING_RANGE_UNCONFIGURED') bucket.ranges.push(describeIssue(i));
    else if (i.code === 'SIGNAL_IDENTITY_UNRESOLVED' || i.code === 'OWNER_INPUT_PENDING') bucket.owner.push(describeIssue(i));
    else bucket.other.push(describeIssue(i));
  }
  for (const i of importIssues) bucket.owner.push(describeIssue(i));
  for (const e of addressEntries) {
    if (e.state === 'ADDRESS_UNRESOLVED') {
      bucket.addresses.push({ code: 'ADDRESS_UNRESOLVED', severity: SEVERITY.WARNING, text: `${e.tagName}: ${e.reasons.length} ${e.reasons.length === 1 ? 'reason' : 'reasons'}` });
    }
  }
  const pending = reservedSummary ? reservedSummary.total : 0;
  return VALIDATION_GROUPS.map((g) => ({
    key: g.key,
    label: g.label,
    // The Owner-identity count is the number of reserved placeholder rows; the detail list holds issue records, if any.
    count: g.key === 'owner' ? Math.max(pending, bucket.owner.length) : bucket[g.key].length,
    severity: g.key === 'errors' ? SEVERITY.ERROR : SEVERITY.WARNING,
    items: bucket[g.key],
  }));
}

function describeIssue(i) {
  return { code: i.code, severity: i.severity, text: `${i.code} — ${i.message}` };
}
