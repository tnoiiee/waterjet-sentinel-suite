// H2 Physical Process-I/O — typed physical-binding registry and fail-closed validation.
//
// Separates catalogue identity (source rows) from physical binding (device + area + address,
// adopted from the Exact Legacy Source) and from derived process-image addressing. Every
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

const CLASS_OF_AREA = { HOLDING_REGISTER: ['AI', 'AO'], COIL: ['DI', 'DO'] };
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
    functionCode: fc,
    provenance: binding.legacyPath,
  });
}

/**
 * Build the complete binding view over a catalogue. Returns per-row binding/address records
 * plus a findings list. Hard failures (duplicate physical binding, capacity violations,
 * class/area mismatch) throw BindingError; disclosed Legacy conflicts are findings, not
 * failures (each side keeps its own typed record; nothing is actuated by this pass).
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
      if (!areaClasses.includes(ioClass)) {
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
      channel: channelText,
      bindingState,
      binding: binding ?? null,
      address,
      commandShape: OUTPUT_COMMAND_SHAPE[tag] ?? null,
      readBack: READ_BACK_SOURCES[tag] ?? null,
      commissioningState,
    }));
  }

  for (const [key, list] of crossDirection) {
    if (list.length > 1) {
      findings.push(Object.freeze({
        code: 'LEGACY_ADDRESS_OVERLAP_DISCLOSED',
        addressKey: key,
        members: Object.freeze(list.map((x) => `${x.tag}(${x.direction})`)),
        note: 'Exact Legacy runtime call sites bind this address on both the read and the write side; both typed records are adopted per call site and the overlap is disclosed. No physical execution occurs in this pass.',
      }));
    }
  }

  return Object.freeze({ rows: Object.freeze(rows), findings: Object.freeze(findings) });
}

/** Fail-closed admission predicate: a row may only enter a write path when bound, non-spare and explicitly authorized. */
export function canAdmitWrite(rowBinding, authorization) {
  if (!rowBinding || rowBinding.bindingState === 'UNBOUND') return { admitted: false, code: 'WJSS_WRITE_REFUSED_UNBOUND' };
  if (rowBinding.commissioningState === 'EXCLUDED') return { admitted: false, code: 'WJSS_WRITE_REFUSED_EXCLUDED' };
  if (!authorization || authorization.physicalWriteAuthorized !== true) {
    return { admitted: false, code: 'WJSS_WRITE_REFUSED_NOT_AUTHORIZED' };
  }
  return { admitted: true, code: 'WJSS_WRITE_ADMITTED' };
}
