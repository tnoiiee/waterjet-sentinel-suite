// H2 Physical Process-I/O — commissioning-surface view model (complete catalogue visibility).
//
// Pure projection over the typed catalogue + bindings + observations: filtering/searching by
// I/O class, source status, binding status, commissioning status, module, Slot, I/O Slot and
// equipment group; per-row display fields (identity, binding, derived address status,
// function-code status, communication status, observations, quality, diagnostic, freshness,
// commissioning status, provenance). Physical controls are never part of this model.
//
// Pure data. No I/O.

import { OWNER_RULINGS, ROW_RISK, ROW_BINDINGS, FUNCTION_CODES } from './legacyEvidence.mjs';

/** Equipment group of a row (DERIVED from source Location/Signal text + Owner rulings). */
export function equipmentGroupOf(row) {
  const tag = row.source['I/O Tag'];
  const location = row.source.Location;
  if (tag === '' || tag === '-') return row.derived.ioClass === 'END_MODULE' ? 'RACK_TOPOLOGY' : 'SPARE';
  if (OWNER_RULINGS['AI-002+'] && tag === 'AI-002+') return 'PUMP_PRESSURE';
  if (tag === 'AI-003+') return 'PUMP_PRESSURE';
  if (OWNER_RULINGS.ivPressureRows.includes(tag)) return 'IV_PRESSURE';
  if (OWNER_RULINGS.ivLowerLimitRows.includes(tag) || OWNER_RULINGS.ivUpperLimitRows.includes(tag)) return 'IV_LIMITS';
  if (/^AI-01[2-9]$/.test(tag)) return 'BLOWER_CURRENT';
  if (/^DI-0(0[1-5])$/.test(tag)) return 'PUMP_INVERTER';
  if (/^DO-0(0[1-7])$/.test(tag)) return 'PUMP_INVERTER';
  if (/^DI-0(2[1-9]|3[0-6])$/.test(tag)) return 'IV_LIMITS';
  if (/^DO-0(2[3-9]|30)$/.test(tag)) return 'IV_ACTUATION';
  if (location.startsWith('Water Tank')) return 'TANK_BANKS';
  if (location.startsWith('Pump')) return 'PUMP_AREA';
  if (location.startsWith('Boiler')) return 'BOILER_BANKS';
  if (location.startsWith('Inverter')) return 'PUMP_INVERTER';
  return 'OTHER';
}

const contains = (value, term) => String(value ?? '').toLowerCase().includes(String(term).toLowerCase());

/**
 * Build the commissioning view over catalogue + bindings. `filters` may include:
 * ioClass, sourceClassification, bindingState, commissioningState, module, slot, ioSlot,
 * equipmentGroup, search (matches tag/description/location/terminal).
 */
export function buildCommissioningView(catalogue, bindings, observations = {}, filters = {}) {
  const bindingByOrdinal = new Map(bindings.rows.map((b) => [b.sourceRowOrdinal, b]));
  const rows = [];
  for (const row of catalogue.rows) {
    const b = bindingByOrdinal.get(row.sourceRowOrdinal);
    const group = equipmentGroupOf(row);
    const observation = observations[row.source['I/O Tag']] ?? null;
    const view = Object.freeze({
      sourceRowOrdinal: row.sourceRowOrdinal,
      tag: row.source['I/O Tag'],
      description: row.source['Signal / Description'],
      module: row.source[['WA', 'GO Module'].join('')],
      slot: row.source.Slot,
      ioSlot: row.source['I/O Slot'],
      channel: row.source.Channel,
      terminal: row.source['Terminal No.'],
      location: row.source.Location,
      ioClass: row.derived.ioClass,
      sourceClassification: row.derived.sourceClassification,
      bindingState: b.bindingState,
      physicalBinding: b.binding ? Object.freeze({ deviceKey: b.binding.deviceKey, area: b.binding.area, address: b.binding.address, direction: b.binding.direction }) : null,
      derivedAddressState: b.address.state,
      functionCodeStatus: b.address.state === 'DERIVED_FROM_LEGACY_EVIDENCE' ? b.address.functionCode : 'UNRESOLVED',
      communicationStatus: observations.__communication ?? 'SIMULATION',
      rawObservation: observation ? (observation.rawWord ?? observation.rawBit ?? null) : null,
      engineeringObservation: observation?.engineering ?? null,
      outputState: observation?.outputState ?? null,
      quality: observation?.quality ?? null,
      diagnosticReason: observation?.diagnostic ?? null,
      freshness: observation?.freshness ?? null,
      commissioningState: b.commissioningState,
      equipmentGroup: group,
      actuationRisk: ROW_RISK[row.source['I/O Tag']] ?? (row.derived.ioClass === 'DO' || row.derived.ioClass === 'AO' ? 'OUTPUT_STATE_OBSERVATION' : 'READ_ONLY'),
      provenance: Object.freeze({
        artifact: row.derived.provenanceArtifact,
        sha256: row.derived.provenanceSha256,
        legacyPath: ROW_BINDINGS[row.source['I/O Tag']]?.legacyPath ?? null,
      }),
    });
    if (filters.ioClass && view.ioClass !== filters.ioClass) continue;
    if (filters.sourceClassification && view.sourceClassification !== filters.sourceClassification) continue;
    if (filters.bindingState && view.bindingState !== filters.bindingState) continue;
    if (filters.commissioningState && view.commissioningState !== filters.commissioningState) continue;
    if (filters.module && view.module !== filters.module) continue;
    if (filters.slot && view.slot !== filters.slot) continue;
    if (filters.ioSlot && view.ioSlot !== filters.ioSlot) continue;
    if (filters.equipmentGroup && view.equipmentGroup !== filters.equipmentGroup) continue;
    if (filters.search) {
      const t = filters.search;
      const hit = contains(view.tag, t) || contains(view.description, t) || contains(view.location, t) || contains(view.terminal, t);
      if (!hit) continue;
    }
    rows.push(view);
  }
  return Object.freeze({
    label: 'SIMULATION',
    total: rows.length,
    rows: Object.freeze(rows),
    functionCodes: FUNCTION_CODES,
  });
}
