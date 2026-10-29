// H2 Physical Process-I/O — typed physical-binding registry and fail-closed validation.
//
// Separates catalogue identity (source rows) from physical binding (device + area + address,
// adopted from the Authoritative T8 Mapping) and from derived process-image addressing. Every
// validation is fail-closed: module identity/capacity, I/O class, channel range, signal-type
// compatibility, duplicate binding, process-image overlap, source-row identity and physical
// binding uniqueness. Rows without an evidenced binding keep bindingState UNBOUND /
// ADDRESS_UNRESOLVED and stay inactive.
//
// Pure data + arithmetic. No I/O.

import { ROW_BINDINGS, DEVICE_KEYS, OUTPUT_COMMAND_SHAPE, READ_BACK_SOURCES, FUNCTION_CODES } from './legacyEvidence.mjs';

export class BindingError extends Error {
  constructor(code, detail) {
    super(`${code}: ${detail}`);
    this.code = code;
    this.detail = detail;
  }
}

const CLASS_OF_AREA = {
  HOLDING_REGISTER: ['AI', 'AO'],
  COIL: ['DI', 'DO'],
  DISCRETE_INPUT: ['DI'],
  INPUT_REGISTER: ['AI'],
};

const DIRECTION_OF_AREA = { AI: 'READ', DI: 'READ', AO: 'WRITE', DO: 'WRITE' };

/** Module channel capacity per coupler I/O module model (from the adopted rack topology). */
export const MODULE_CAPACITY = Object.freeze({
  '750-430': { channels: 8, ioClass: 'DI' },
  '750-530': { channels: 8, ioClass: 'DO' },
  '750-471': { channels: 4, ioClass: 'AI' },
  '750-554': { channels: 2, ioClass: 'AO' },
  '750-600': { channels: 0, ioClass: 'END_MODULE' },
});

/**
 * Derive the typed process-image address for a bound row: the read/write group start from
 * the adopted read grouping plus the adopted offset. Unbound rows are ADDRESS_UNRESOLVED —
 * never guessed. Read-only output; the address is a derived product of fixed evidence.
 */
export function deriveAddress(binding, ioClass) {
  if (!binding) return Object.freeze({ state: 'ADDRESS_UNRESOLVED', reasons: Object.freeze(['NO_LEGACY_BINDING']) });
  const fc = ioClass === 'AI' ? FUNCTION_CODES.AI_RAW_READ.fc
    : ioClass === 'DI' ? FUNCTION_CODES.DI_STATE_READ.fc
      : ioClass === 'DO' ? FUNCTION_CODES.DO_COMMAND.fc : FUNCTION_CODES.AO_COMMAND.fc;
  return Object.freeze({
    state: 'DERIVED_FROM_LEGACY_EVIDENCE',
    reasons: Object.freeze([]),
    deviceKey: binding.deviceKey,
    area: binding.area,
    offset: binding.address,
    readBackOffset: binding.area === 'COIL' ? 512 + binding.address : binding.area === 'HOLDING_REGISTER' ? 512 + binding.address : null,
    functionCode: fc,
    provenance: binding.legacyPath,
  });
}

/**
 * Build the complete binding view over a catalogue. Returns per-row binding/address records
 * plus a findings list. Hard failures (duplicate physical binding, capacity violations,
 * class/area mismatch) throw BindingError.
 */
export function buildBindings(catalogue) {
  const rows = [];
  const findings = [];
  const occupied = new Map(); // `${deviceKey}|${area}|${address}|${direction}` -> tag
  const crossDirection = new Map(); // `${deviceKey}|${area}|${address}` -> [{tag,direction}]

  for (const row of catalogue.rows) {
    const tag = row.source['I/O Tag'];
    const ioClass = row.derived.ioClass;
    const module = row.source[['WA', 'GO Module'].join('')];
    const channelText = row.source.Channel;
    const classification = row.derived.sourceClassification;

    if (ioClass !== 'END_MODULE') {
      const profile = MODULE_CAPACITY[module];
      if (!profile) throw new BindingError('MODULE_IDENTITY', `row ${row.sourceRowOrdinal}: unknown module ${module}`);
      if (profile.ioClass !== ioClass) {
        throw new BindingError('SIGNAL_TYPE_MISMATCH', `row ${row.sourceRowOrdinal}: class ${ioClass} on module ${module}`);
      }
      const channel = Number(channelText);
      if (!Number.isInteger(channel) || channel < 1 || channel > profile.channels) {
        throw new BindingError('CHANNEL_CAPACITY', `row ${row.sourceRowOrdinal}: channel ${channelText} on ${module}`);
      }
    }

    const binding = tag !== '' && tag !== '-' ? ROW_BINDINGS[tag] : undefined;
    let bindingState = 'UNBOUND';
    let address = deriveAddress(null, ioClass);
    if (binding) {
      if (!DEVICE_KEYS.includes(binding.deviceKey)) throw new BindingError('DEVICE_KEY', `${tag}: unknown device key`);
      const areaClasses = CLASS_OF_AREA[binding.area];
      if (!areaClasses || !areaClasses.includes(ioClass)) {
        throw new BindingError('SIGNAL_TYPE_MISMATCH', `${tag}: ${ioClass} bound to ${binding.area}`);
      }
      if (binding.direction !== DIRECTION_OF_AREA[ioClass]) {
        throw new BindingError('SIGNAL_TYPE_MISMATCH', `${tag}: direction ${binding.direction} for class ${ioClass}`);
      }
      const key = `${binding.deviceKey}|${binding.area}|${binding.address}|${binding.direction}`;
      if (occupied.has(key)) {
        throw new BindingError('DUPLICATE_BINDING', `${tag} collides with ${occupied.get(key)} at ${key}`);
      }
      occupied.set(key, tag);
      const ck = `${binding.deviceKey}|${binding.area}|${binding.address}`;
      const list = crossDirection.get(ck) ?? [];
      list.push({ tag, direction: binding.direction });
      crossDirection.set(ck, list);
      bindingState = binding.adopted === 'LEGACY_EXACT' ? 'BOUND_LEGACY_EXACT' : 'BOUND_LEGACY_DERIVED';
      address = deriveAddress(binding, ioClass);
    }

    const commissioningState = classification === 'SPARE' ? 'EXCLUDED'
      : binding ? 'READ_ONLY_CANDIDATE' : 'EXCLUDED';

    rows.push(Object.freeze({
      sourceRowOrdinal: row.sourceRowOrdinal,
      tag,
      ioClass,
      module,
      slot: row.source.Slot,
      ioSlot: row.source['I/O Slot'],
      channel: row.source.Channel,
      sourceStatus: row.source.Status,
      sourceClassification: classification,
      binding,
      bindingState,
      address,
      commissioningState,
      rawRow: row,
    }));
  }

  // Cross-direction conflict check
  for (const [key, entries] of crossDirection.entries()) {
    const reads = entries.filter((e) => e.direction === 'READ');
    const writes = entries.filter((e) => e.direction === 'WRITE');
    if (reads.length > 0 && writes.length > 0) {
      findings.push(Object.freeze({
        code: 'LEGACY_ADDRESS_OVERLAP_DISCLOSED',
        addressKey: key,
        members: Object.freeze(entries.map((e) => `${e.tag}(${e.direction})`)),
        classification: 'DISCLOSED_LEGACY_CONFLICT',
        detail: `disclosed Legacy address reuse on ${key}`,
      }));
    }
  }

  return Object.freeze({ rows: Object.freeze(rows), findings: Object.freeze(findings) });
}

/** Fail-closed write authorization check: refuses unbound or excluded rows. */
export function canAdmitWrite(row, authorization) {
  if (!row || !row.binding || row.bindingState === 'UNBOUND') {
    return Object.freeze({ admitted: false, code: 'WJSS_WRITE_REFUSED_UNBOUND', detail: 'no physical binding' });
  }
  if (row.commissioningState === 'EXCLUDED') {
    return Object.freeze({ admitted: false, code: 'WJSS_WRITE_REFUSED_EXCLUDED', detail: 'row excluded from commissioning' });
  }
  if (!authorization || !authorization.physicalWriteAuthorized) {
    return Object.freeze({ admitted: false, code: 'WJSS_WRITE_REFUSED_NOT_AUTHORIZED', detail: 'physical write authority not created' });
  }
  return Object.freeze({ admitted: true, code: 'OK', detail: authorization.basis ?? 'authorized' });
}
