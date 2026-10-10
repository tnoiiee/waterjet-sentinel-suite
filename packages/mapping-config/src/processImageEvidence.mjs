// Stage 0.4B-2 — verified process-image evidence foundation.
//
// A numeric address may be derived only from VERIFIED evidence. This module models that evidence and decides,
// for a given rack, what is verified, what is missing and which layout follows. It never supplies a value of
// its own: every width, order and setting comes from an evidence observation that names a source document.
//
// Evidence layers (kept apart on purpose):
//   1. Owner workbook facts          sources of type OWNER_WORKBOOK_FACT (identity only; they carry no process data)
//   2. Primary documents             sources of type PRIMARY_DOCUMENT
//   3. Head-station rules            headStation record: grouping, mapping order, word width, image limits
//   4. Module process-data profiles  per model: process width, channel data width, filler, diagnostic bytes
//   5. Status-byte settings          per analog model
//   6. Byte and word order           per channel model
//   7. Derived process-image order   computed here from 3, never read from ProcessModulePosition
//   8. Derived read-only address     consumed by addressDerivation.mjs
//
// An observation is { value, state, sourceId, note }. A VERIFIED_* state is honoured only when the named source
// exists, has the matching evidence type and carries a SHA-256 or a document revision. Otherwise the observation
// is demoted to PROVIDED_UNVERIFIED and an issue is recorded. Two verified observations with different values are
// CONFLICTING_EVIDENCE and block derivation.
//
// SYNTHETIC TEST RULE sets (synthetic: true, sources of type SYNTHETIC_TEST_RULE) exist only to exercise the
// arithmetic in tests. They are never authoritative, never address-verified, and never default.
//
// Pure data and arithmetic. No I/O, no Node built-ins, no device access.

import {
  CATEGORY, CHANNEL_TYPE, DIRECTION, EVIDENCE_STATE, EVIDENCE_TYPE, FORBIDDEN_BINDING_KEYS, REASON,
  SEVERITY, VERIFIED_MIXED_SOURCES,
} from './constants.mjs';
import { canonicalJson, fingerprintOf } from './canonical.mjs';
import { getProfile } from './moduleProfiles.mjs';

// ------------------------------------------------------------------ vocabulary

export const GROUPING = Object.freeze({
  ANALOG_THEN_DIGITAL: 'ANALOG_THEN_DIGITAL',
  SLOT_ORDER_MIXED: 'SLOT_ORDER_MIXED',
});
// The only grouping and mapping order the derivation engine can compute. Any other value is valid evidence,
// but it keeps addresses unresolved because the engine has no arithmetic for it.
export const SUPPORTED_GROUPING = GROUPING.ANALOG_THEN_DIGITAL;
export const MAPPING_ORDER = Object.freeze({ RACK_SLOT_ASCENDING: 'RACK_SLOT_ASCENDING' });
export const STATUS_BEHAVIOR = Object.freeze({ NOT_PRESENT: 'NOT_PRESENT', DISABLED: 'DISABLED', ENABLED: 'ENABLED' });
export const STATUS_PLACEMENT = Object.freeze({
  AFTER_CHANNEL_DATA: 'AFTER_CHANNEL_DATA',
  BEFORE_CHANNEL_DATA: 'BEFORE_CHANNEL_DATA',
  INTERLEAVED_PER_CHANNEL: 'INTERLEAVED_PER_CHANNEL',
});
export const BYTE_ORDER = Object.freeze({ BIG_ENDIAN: 'BIG_ENDIAN', LITTLE_ENDIAN: 'LITTLE_ENDIAN' });
export const WORD_ORDER = Object.freeze({ HIGH_WORD_FIRST: 'HIGH_WORD_FIRST', LOW_WORD_FIRST: 'LOW_WORD_FIRST' });
export const ADDRESS_ENTRY_STATE = Object.freeze({ UNRESOLVED: 'ADDRESS_UNRESOLVED', DERIVED: 'DERIVED', NOT_APPLICABLE: 'NOT_APPLICABLE' });

const VERIFIED_STATES = Object.freeze([EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE, EVIDENCE_STATE.VERIFIED_IO_CHECK_EXPORT]);
const OBSERVATION_STATES = Object.freeze([
  EVIDENCE_STATE.NOT_PROVIDED, EVIDENCE_STATE.PROVIDED_UNVERIFIED, ...VERIFIED_STATES, EVIDENCE_STATE.CONFLICTING_EVIDENCE,
]);
const SOURCE_TYPE_FOR_STATE = Object.freeze({
  [EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE]: EVIDENCE_TYPE.PRIMARY_DOCUMENT,
  [EVIDENCE_STATE.VERIFIED_IO_CHECK_EXPORT]: EVIDENCE_TYPE.IO_CHECK_EXPORT,
});

const isVerified = (state) => VERIFIED_STATES.includes(state);
const isNonNegInt = (n) => Number.isInteger(n) && n >= 0;
const pair = (v) => v !== null && typeof v === 'object' && Object.keys(v).sort().join() === 'input,output'
  && isNonNegInt(v.input) && isNonNegInt(v.output);
const oneOf = (list) => (v) => (typeof v === 'string' && list.includes(v) ? null : `expected one of ${list.join(' | ')}`);

// Value validators. Each returns null when the value is acceptable, otherwise a message.
const CELL_DEFS = Object.freeze({
  // head station
  processImageGrouping: oneOf(Object.values(GROUPING)),
  moduleMappingOrder: (v) => {
    if (v === 'PROCESS_MODULE_POSITION' || typeof v === 'number') {
      return 'ProcessModulePosition is a topology ordinal and is not a ProcessImageOrder';
    }
    return oneOf(Object.values(MAPPING_ORDER))(v);
  },
  wordWidthBits: (v) => (Number.isInteger(v) && v > 0 && v % 8 === 0 ? null : 'expected a positive multiple of 8'),
  maxProcessImageWords: (v) => (pair(v) ? null : 'expected { input, output } non-negative integers'),
  // module
  processWidthBits: (v) => (pair(v) ? null : 'expected { input, output } non-negative integers (bits)'),
  channelDataBits: (v) => (Number.isInteger(v) && v > 0 ? null : 'expected a positive integer (bits per channel)'),
  fillerBits: (v) => (pair(v) ? null : 'expected { input, output } non-negative integers (bits)'),
  diagnosticBytes: (v) => (pair(v) ? null : 'expected { input, output } non-negative integers (bytes)'),
  statusByte: (v) => {
    if (v === null || typeof v !== 'object') return 'expected { behavior, inputBits, outputBits, placement }';
    if (Object.keys(v).sort().join() !== 'behavior,inputBits,outputBits,placement') return 'expected exactly behavior, inputBits, outputBits, placement';
    if (!Object.values(STATUS_BEHAVIOR).includes(v.behavior)) return 'unknown status-byte behavior';
    if (!isNonNegInt(v.inputBits) || !isNonNegInt(v.outputBits)) return 'status bits must be non-negative integers';
    if (v.behavior === STATUS_BEHAVIOR.ENABLED) {
      if (v.inputBits + v.outputBits === 0) return 'ENABLED status bytes need a non-zero width';
      if (!Object.values(STATUS_PLACEMENT).includes(v.placement)) return 'ENABLED status bytes need a placement';
    } else if (v.inputBits !== 0 || v.outputBits !== 0 || v.placement !== null) {
      return 'a status byte that is not ENABLED has no width and no placement';
    }
    return null;
  },
  byteOrder: oneOf(Object.values(BYTE_ORDER)),
  wordOrder: oneOf(Object.values(WORD_ORDER)),
});

export const HEAD_CELLS = Object.freeze(['processImageGrouping', 'moduleMappingOrder', 'wordWidthBits', 'maxProcessImageWords']);
export const MODULE_CELLS = Object.freeze(['processWidthBits', 'channelDataBits', 'fillerBits', 'diagnosticBytes', 'statusByte', 'byteOrder', 'wordOrder']);

const REASON_OF_CELL = Object.freeze({
  statusByte: REASON.MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED,
  byteOrder: REASON.BYTE_ORDER_NOT_VERIFIED,
  wordOrder: REASON.WORD_ORDER_NOT_VERIFIED,
});

/** The cells a module model must have verified before it can be address-capable. */
export function requiredModuleCells(profile) {
  if (!profile || profile.channelCount === 0) return ['processWidthBits'];
  const cells = ['processWidthBits', 'channelDataBits', 'fillerBits', 'diagnosticBytes'];
  if (profile.channelType === CHANNEL_TYPE.ANALOG) cells.push('statusByte');
  cells.push('byteOrder', 'wordOrder');
  return cells;
}

// ------------------------------------------------------------------ evidence set construction

function clone(v) { return JSON.parse(JSON.stringify(v)); }
function deepFreeze(v) {
  if (v && typeof v === 'object' && !Object.isFrozen(v)) {
    Object.freeze(v);
    for (const x of Object.values(v)) deepFreeze(x);
  }
  return v;
}

/**
 * Creates an immutable evidence set. The input is copied. No default value is ever added: a missing cell is
 * NOT_PROVIDED.
 */
export function createEvidenceSet(input) {
  const c = clone(input);
  if (!c.evidenceSetId || typeof c.evidenceSetId !== 'string') throw new Error('evidence set needs an evidenceSetId');
  return deepFreeze({
    evidenceSetId: c.evidenceSetId,
    label: c.label ?? c.evidenceSetId,
    synthetic: c.synthetic === true,
    sources: c.sources ?? [],
    headStation: c.headStation ?? null,
    modules: c.modules ?? [],
  });
}

const OWNER_WORKBOOK_SOURCE = Object.freeze({
  sourceId: 'OWNER-WORKBOOK',
  evidenceType: EVIDENCE_TYPE.OWNER_WORKBOOK_FACT,
  documentTitle: 'T8_IO_Card_Mapping.xlsx',
  documentSha256: '4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e',
  documentRevision: null,
  effectiveDate: null,
  manufacturer: null,
});

const AUTHORITATIVE_MODELS = Object.freeze(['750-362', '750-430', '750-530', '750-601', '750-613', '750-471', '750-554', '750-600']);
const NO_EVIDENCE_NOTE = 'No primary document or export has been provided. Nothing is verified.';

/**
 * The authoritative rack evidence: what the repository can honestly say today. The Owner workbook identifies the
 * rack, but it holds no process-image data, and no primary document has been provided. Every cell is therefore
 * NOT_PROVIDED and every address stays ADDRESS_UNRESOLVED. Manufacturer is not recorded until a source states it.
 */
export const AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE = createEvidenceSet({
  evidenceSetId: 'WJSS-PROCESS-IMAGE-EVIDENCE-AUTHORITATIVE',
  label: 'NO PRIMARY PROCESS-IMAGE EVIDENCE PROVIDED',
  synthetic: false,
  sources: [OWNER_WORKBOOK_SOURCE],
  headStation: { modelNumber: '750-362', manufacturer: null, evidenceNote: NO_EVIDENCE_NOTE, cells: {} },
  modules: AUTHORITATIVE_MODELS
    .map((modelNumber) => ({ modelNumber, manufacturer: null, evidenceNote: NO_EVIDENCE_NOTE, cells: {} })),
});

// ------------------------------------------------------------------ evidence preparation (rack independent)

const SHA256_RE = /^[0-9a-fA-F]{64}$/;
const prepared = new WeakMap();

function issue(code, message, path) {
  return Object.freeze({ code, severity: SEVERITY.WARNING, message, path });
}

function sourceProblem(set, src, state) {
  if (!src) return ['EVIDENCE_SOURCE_MISSING', 'a verified observation must name an existing source'];
  const synthetic = src.evidenceType === EVIDENCE_TYPE.SYNTHETIC_TEST_RULE;
  if (synthetic !== (set.synthetic === true)) {
    return ['SYNTHETIC_SOURCE_MISMATCH', 'SYNTHETIC TEST RULE sources are allowed only in a synthetic set, and a synthetic set accepts only those'];
  }
  if (!synthetic) {
    if (SOURCE_TYPE_FOR_STATE[state] !== src.evidenceType) {
      return ['EVIDENCE_SOURCE_TYPE_MISMATCH', `${state} needs a source of type ${SOURCE_TYPE_FOR_STATE[state]}`];
    }
    if (!src.documentSha256 && !src.documentRevision) {
      return ['EVIDENCE_SOURCE_UNIDENTIFIED', 'a verified source needs a SHA-256 or a document revision'];
    }
  }
  return null;
}

function resolveCell(set, sources, name, observations, path, issues) {
  const def = CELL_DEFS[name];
  const accepted = [];
  let declaredConflict = false;
  for (const [i, o] of (observations ?? []).entries()) {
    const at = `${path}.${name}[${i}]`;
    if (o === null || typeof o !== 'object' || !OBSERVATION_STATES.includes(o.state)) {
      issues.push(issue('OBSERVATION_STATE_INVALID', 'observation state is not a known evidence state', at));
      continue;
    }
    if (o.state === EVIDENCE_STATE.NOT_PROVIDED) continue;
    if (o.state === EVIDENCE_STATE.CONFLICTING_EVIDENCE) { declaredConflict = true; continue; }
    const bad = def(o.value);
    if (bad) {
      issues.push(issue('EVIDENCE_VALUE_INVALID', `${name}: ${bad}`, at));
      continue;
    }
    let state = o.state;
    if (isVerified(state)) {
      const problem = sourceProblem(set, sources.get(o.sourceId), state);
      if (problem) {
        issues.push(issue(problem[0], problem[1], at));
        state = EVIDENCE_STATE.PROVIDED_UNVERIFIED;
      }
    }
    accepted.push({ value: o.value, state, sourceId: o.sourceId ?? null });
  }
  const verified = accepted.filter((o) => isVerified(o.state));
  const distinct = new Set(verified.map((o) => canonicalJson(o.value)));
  const sourceIds = [...new Set(accepted.map((o) => o.sourceId).filter((s) => s))].sort();
  if (declaredConflict || distinct.size > 1) {
    return Object.freeze({ name, state: EVIDENCE_STATE.CONFLICTING_EVIDENCE, value: null, sourceIds });
  }
  if (verified.length > 0) {
    const state = verified.some((o) => o.state === EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE)
      ? EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE : EVIDENCE_STATE.VERIFIED_IO_CHECK_EXPORT;
    return Object.freeze({ name, state, value: clone(verified[0].value), sourceIds });
  }
  if (accepted.length > 0) {
    // An unverified value is never exposed to derivation.
    return Object.freeze({ name, state: EVIDENCE_STATE.PROVIDED_UNVERIFIED, value: null, sourceIds });
  }
  return Object.freeze({ name, state: EVIDENCE_STATE.NOT_PROVIDED, value: null, sourceIds });
}

function resolveRecord(set, sources, record, names, path, issues) {
  const cells = {};
  const given = record?.cells ?? {};
  for (const key of Object.keys(given)) {
    if (!names.includes(key)) {
      const forbidden = FORBIDDEN_BINDING_KEYS.includes(key);
      issues.push(issue(forbidden ? 'MANUAL_ADDRESS_REFUSED' : 'EVIDENCE_CELL_UNKNOWN',
        forbidden ? `'${key}' is derived and read-only; evidence cannot carry an address` : `'${key}' is not an evidence cell`, `${path}.${key}`));
    }
  }
  for (const name of names) cells[name] = resolveCell(set, sources, name, given[name], path, issues);
  return cells;
}

function validateSources(set, issues) {
  const sources = new Map();
  for (const [i, s] of set.sources.entries()) {
    const at = `sources[${i}]`;
    if (!s.sourceId || sources.has(s.sourceId)) issues.push(issue('EVIDENCE_SOURCE_DUPLICATE', 'sourceId must be present and unique', at));
    else sources.set(s.sourceId, s);
    if (!Object.values(EVIDENCE_TYPE).includes(s.evidenceType)) issues.push(issue('EVIDENCE_SOURCE_TYPE_UNKNOWN', 'unknown evidence type', at));
    if (!s.documentTitle) issues.push(issue('EVIDENCE_SOURCE_UNIDENTIFIED', 'a source needs a document title', at));
    if (s.documentSha256 && !SHA256_RE.test(s.documentSha256)) issues.push(issue('EVIDENCE_SOURCE_SHA256_INVALID', 'documentSha256 must be 64 hex characters', at));
  }
  return sources;
}

function prepare(set) {
  const hit = prepared.get(set);
  if (hit) return hit;
  const issues = [];
  const sources = validateSources(set, issues);
  const head = set.headStation
    ? resolveRecord(set, sources, set.headStation, HEAD_CELLS, 'headStation', issues)
    : resolveRecord(set, sources, null, HEAD_CELLS, 'headStation', issues);
  const modules = new Map();
  for (const [i, rec] of set.modules.entries()) {
    const at = `modules[${i}]`;
    if (!getProfile(rec.modelNumber)) { issues.push(issue('EVIDENCE_MODEL_UNKNOWN', `no Module Profile for ${rec.modelNumber}`, at)); continue; }
    if (modules.has(rec.modelNumber)) { issues.push(issue('EVIDENCE_MODEL_DUPLICATE', `duplicate record for ${rec.modelNumber}`, at)); continue; }
    modules.set(rec.modelNumber, { record: rec, cells: resolveRecord(set, sources, rec, MODULE_CELLS, at, issues) });
  }
  const out = Object.freeze({ issues: Object.freeze(issues), sources, head, modules });
  prepared.set(set, out);
  return out;
}

/** Evidence issues found in a set (invalid values, unusable sources, refused address keys). Verified claims that fail are demoted, never honoured. */
export function validateEvidenceSet(set) {
  return prepare(set).issues;
}

/** Fingerprint of the whole evidence content. It changes when any observation, source or note changes. */
export function evidenceSetFingerprint(set) {
  return fingerprintOf({ kind: 'ProcessImageEvidence', set });
}

function recordFingerprint(set, record, extra) {
  const ids = new Set();
  for (const obs of Object.values(record?.cells ?? {})) for (const o of obs) if (o.sourceId) ids.add(o.sourceId);
  return fingerprintOf({
    kind: 'ProcessImageEvidenceRecord',
    record: record ?? null,
    extra: extra ?? null,
    sources: set.sources.filter((s) => ids.has(s.sourceId)),
  });
}

// ------------------------------------------------------------------ rack evaluation and layout

function profileState(required, cells) {
  const states = required.map((n) => cells[n].state);
  if (states.includes(EVIDENCE_STATE.CONFLICTING_EVIDENCE)) return EVIDENCE_STATE.CONFLICTING_EVIDENCE;
  if (states.every(isVerified)) {
    const kinds = new Set(states);
    return kinds.size === 1 ? [...kinds][0] : VERIFIED_MIXED_SOURCES;
  }
  if (states.every((s) => s === EVIDENCE_STATE.NOT_PROVIDED)) return EVIDENCE_STATE.NOT_PROVIDED;
  if (states.some(isVerified)) return EVIDENCE_STATE.INCOMPLETE_PROFILE;
  return EVIDENCE_STATE.PROVIDED_UNVERIFIED;
}

function evaluateHead(set, prep, rackView) {
  const reasons = new Set();
  const cells = prep.head;
  const missing = HEAD_CELLS.filter((n) => !isVerified(cells[n].state));
  const conflict = HEAD_CELLS.some((n) => cells[n].state === EVIDENCE_STATE.CONFLICTING_EVIDENCE);
  const coupler = rackView.find((r) => r.category === CATEGORY.COUPLER);
  const modelMatches = Boolean(coupler && set.headStation && set.headStation.modelNumber === coupler.modelNumber);
  if (missing.length > 0 || !modelMatches) {
    reasons.add(REASON.HEAD_STATION_PROFILE_NOT_VERIFIED);
    reasons.add(REASON.NO_VERIFIED_PROCESS_IMAGE_RULE);
  }
  if (missing.some((n) => n === 'processImageGrouping' || n === 'moduleMappingOrder')) reasons.add(REASON.PROCESS_IMAGE_ORDER_NOT_VERIFIED);
  if (conflict) reasons.add(REASON.CONFLICTING_PROCESS_IMAGE_EVIDENCE);
  let supported = true;
  if (missing.length === 0 && modelMatches) {
    if (cells.processImageGrouping.value !== SUPPORTED_GROUPING || cells.moduleMappingOrder.value !== MAPPING_ORDER.RACK_SLOT_ASCENDING) {
      // Verified, but the engine has no arithmetic for it. Unresolved, never guessed.
      supported = false;
      reasons.add(REASON.PROCESS_IMAGE_ORDER_NOT_VERIFIED);
      reasons.add(REASON.NO_VERIFIED_PROCESS_IMAGE_RULE);
    }
  }
  const verified = missing.length === 0 && modelMatches && supported;
  return {
    modelNumber: set.headStation?.modelNumber ?? null,
    manufacturer: set.headStation?.manufacturer ?? null,
    evidenceNote: set.headStation?.evidenceNote ?? null,
    evidenceState: profileState(HEAD_CELLS, cells),
    complete: verified,
    missingCells: missing,
    modelMatchesRack: modelMatches,
    engineSupported: supported,
    reasons: [...reasons].sort(),
    sourceIds: [...new Set(HEAD_CELLS.flatMap((n) => cells[n].sourceIds))].sort(),
    fingerprint: recordFingerprint(set, set.headStation),
    verified,
    values: verified ? {
      grouping: cells.processImageGrouping.value, mappingOrder: cells.moduleMappingOrder.value,
      wordBits: cells.wordWidthBits.value, maxWords: cells.maxProcessImageWords.value,
    } : null,
  };
}

function evaluateModule(set, prep, view) {
  const profile = getProfile(view.modelNumber);
  const entry = prep.modules.get(view.modelNumber) ?? null;
  const required = requiredModuleCells(profile);
  const cells = entry ? entry.cells : Object.fromEntries(MODULE_CELLS.map((n) => [n, Object.freeze({ name: n, state: EVIDENCE_STATE.NOT_PROVIDED, value: null, sourceIds: [] })]));
  const layoutReasons = new Set();
  const orderReasons = new Set();
  let conflict = required.some((n) => cells[n].state === EVIDENCE_STATE.CONFLICTING_EVIDENCE);
  for (const n of required) {
    if (isVerified(cells[n].state)) continue;
    const r = REASON_OF_CELL[n] ?? REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED;
    (n === 'byteOrder' || n === 'wordOrder' ? orderReasons : layoutReasons).add(r);
  }
  const channelModule = Boolean(profile && profile.channelCount > 0);
  const analog = channelModule && profile.channelType === CHANNEL_TYPE.ANALOG;

  // Resolved span per direction, only when every required layout value is verified and consistent.
  let spans = null;
  if (layoutReasons.size === 0) {
    const width = cells.processWidthBits.value;
    if (!channelModule) {
      if (width.input !== 0 || width.output !== 0) {
        // A module without channels that contributes process data cannot be placed by this engine.
        layoutReasons.add(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED);
      } else {
        spans = { input: 0, output: 0 };
      }
    } else {
      const own = profile.direction === DIRECTION.INPUT ? 'input' : 'output';
      const other = own === 'input' ? 'output' : 'input';
      const cd = cells.channelDataBits.value;
      const filler = cells.fillerBits.value;
      const diag = cells.diagnosticBytes.value;
      const status = analog ? cells.statusByte.value : { behavior: STATUS_BEHAVIOR.NOT_PRESENT, inputBits: 0, outputBits: 0, placement: null };
      if (width[other] !== 0 || filler[other] !== 0 || diag[other] !== 0 || (status[`${other}Bits`] ?? 0) !== 0) {
        layoutReasons.add(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED);
        conflict = true;
      } else if (cd * profile.channelCount !== width[own]) {
        // Channel data width and total width disagree: the evidence contradicts itself.
        layoutReasons.add(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED);
        conflict = true;
      } else if (status.behavior === STATUS_BEHAVIOR.ENABLED && status.placement !== STATUS_PLACEMENT.AFTER_CHANNEL_DATA) {
        // Channel offsets would depend on a placement the engine does not compute.
        layoutReasons.add(REASON.MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED);
      } else {
        const span = width[own] + status[`${own}Bits`] + filler[own] + diag[own] * 8;
        spans = { input: own === 'input' ? span : 0, output: own === 'output' ? span : 0 };
      }
    }
  }
  if (conflict) layoutReasons.add(REASON.CONFLICTING_PROCESS_IMAGE_EVIDENCE);
  const sourceIds = [...new Set(required.flatMap((n) => cells[n].sourceIds))].sort();
  const state = profileState(required, cells);
  return {
    moduleInstanceId: view.moduleInstanceId,
    modelNumber: view.modelNumber,
    rackSlot: view.rackSlot,
    processModulePosition: view.processModulePosition,
    channelModule,
    analog,
    area: channelModule ? profile.direction : null,
    manufacturer: entry?.record.manufacturer ?? null,
    evidenceNote: entry?.record.evidenceNote ?? null,
    evidenceState: state,
    complete: layoutReasons.size === 0 && orderReasons.size === 0 && spans !== null,
    cells,
    requiredCells: required,
    layoutReasons: [...layoutReasons].sort(),
    orderReasons: [...orderReasons].sort(),
    conflict,
    spans,
    sourceIds,
    channelDataBits: channelModule && isVerified(cells.channelDataBits.state) ? cells.channelDataBits.value : null,
    byteOrder: orderReasons.has(REASON.BYTE_ORDER_NOT_VERIFIED) || !channelModule ? null : cells.byteOrder.value,
    wordOrder: orderReasons.has(REASON.WORD_ORDER_NOT_VERIFIED) || !channelModule ? null : cells.wordOrder.value,
    fingerprint: recordFingerprint(set, entry?.record ?? { modelNumber: view.modelNumber, cells: {} }),
    processImageOrder: { state: 'NOT_VERIFIED', value: null },
    start: null,
    address: null,
  };
}

/**
 * Resolves the process image of a rack from an evidence set. Pure and deterministic.
 * The result carries, for every rack module, its evidence state, its derived ProcessImageOrder and either a
 * start offset (bits, zero-based) or the reasons it has none. ProcessModulePosition is never an input.
 */
export function resolveProcessImage(evidenceSet, rackView) {
  const set = evidenceSet;
  const prep = prepare(set);
  const head = evaluateHead(set, prep, rackView);
  const mods = rackView.map((v) => evaluateModule(set, prep, v));
  const byId = new Map(mods.map((m) => [m.moduleInstanceId, m]));

  const globalBlockers = mods.filter((m) => !m.channelModule && m.layoutReasons.length > 0);
  const areas = {};
  for (const area of [DIRECTION.INPUT, DIRECTION.OUTPUT]) {
    const key = area === DIRECTION.INPUT ? 'input' : 'output';
    const members = mods.filter((m) => m.channelModule && m.area === area);
    const blockers = [...globalBlockers, ...members.filter((m) => m.layoutReasons.length > 0)];
    const info = { area, blockers, totalBits: null, totalWords: null, exceeded: false, layoutResolved: false };
    areas[area] = info;
    if (!head.verified || blockers.length > 0) continue;

    const wordBits = head.values.wordBits;
    // ANALOG_THEN_DIGITAL: analog modules first in rack order, each word aligned; digital bits packed after them.
    const analog = members.filter((m) => m.analog);
    const digital = members.filter((m) => !m.analog);
    let cursor = 0;
    let unaligned = null;
    for (const m of analog) {
      const span = m.spans[key];
      if (cursor % wordBits !== 0 || span % wordBits !== 0) unaligned = unaligned ?? m;
      m.start = { [key]: cursor };
      cursor += span;
    }
    if (unaligned) {
      // Alignment would have to be guessed. Block the area instead.
      info.blockers = [unaligned];
      unaligned.layoutReasons.push(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED);
      unaligned.layoutReasons.sort();
      continue;
    }
    for (const m of digital) {
      m.start = { [key]: cursor };
      cursor += m.spans[key];
    }
    info.layoutResolved = true;
    info.totalBits = cursor;
    info.totalWords = Math.ceil(cursor / wordBits);
    info.exceeded = info.totalWords > head.values.maxWords[key];
    [...analog, ...digital].forEach((m, i) => { m.processImageOrder = { state: 'DERIVED', value: i + 1 }; });
  }

  for (const m of mods) {
    if (!m.channelModule) { m.address = { state: ADDRESS_ENTRY_STATE.NOT_APPLICABLE, reasons: [], startBit: null }; continue; }
    const info = areas[m.area];
    const reasons = new Set([...m.layoutReasons, ...m.orderReasons]);
    if (!head.verified) {
      for (const r of head.reasons) reasons.add(r);
    } else if (info.blockers.length > 0) {
      reasons.add(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED);
      if (info.blockers.some((b) => b.conflict)) reasons.add(REASON.CONFLICTING_PROCESS_IMAGE_EVIDENCE);
    } else if (info.exceeded) {
      reasons.add(REASON.PROCESS_IMAGE_SIZE_EXCEEDED);
    }
    const derivable = reasons.size === 0 && Boolean(m.start);
    if (!m.start) m.processImageOrder = { state: 'NOT_VERIFIED', value: null };
    m.address = {
      state: derivable ? ADDRESS_ENTRY_STATE.DERIVED : ADDRESS_ENTRY_STATE.UNRESOLVED,
      reasons: [...reasons].sort(),
      startBit: derivable ? m.start[m.area === DIRECTION.INPUT ? 'input' : 'output'] : null,
    };
  }

  const addressCapable = head.verified && mods.some((m) => m.address.state === ADDRESS_ENTRY_STATE.DERIVED);
  const synthetic = set.synthetic === true;
  return Object.freeze({
    evidenceSetId: set.evidenceSetId,
    label: set.label,
    synthetic,
    // Only a non-synthetic set with a verified head station can be authoritative.
    authoritative: !synthetic && head.verified,
    fingerprint: evidenceSetFingerprint(set),
    head,
    modules: mods,
    byInstance: byId,
    areas,
    addressCapable,
    issues: prep.issues,
    sources: set.sources,
    wordBits: head.verified ? head.values.wordBits : null,
  });
}
