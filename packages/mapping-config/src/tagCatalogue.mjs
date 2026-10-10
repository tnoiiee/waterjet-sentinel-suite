// Stage 0.4B-1 — Simulation/Runtime Tag catalogue (transport-independent).
//
// These identities are Runtime names, not hardware addresses. Source identities
// (PUMP_INLET, PUMP_OUTLET, MAIN_VALVE_OUTLET, IVn_OUTLET) are the identities used
// by the Runtime. Owner clarification (2026-10-10), authoritative for meaning:
//   AI-002  Pressure Transmitter - Pump inlet pressure   -> PumpInletPressureBar
//           Pressure in the Header Tank / suction piping before the Pump. NOT Pump-ready.
//   AI-003  Pressure Transmitter - Pump outlet pressure  -> PumpOutletPressureBar
//           Pressure after the Pump, before the Main Valve. The ONLY pre-P1 Pump-ready input.
//   AI-004..AI-011  IV1..IV8 outlet pressure, ordinal by '#n' (valve diagnostics only).
// Pump inlet, pump outlet, main valve and IVn pressure are distinct physical measurements.
// There is no alias, fallback, averaging or derivation between them.
//
// tagName is the Draft and seed key and is kept unchanged. canonicalIdentity is the runtime
// identity named by the Owner and must be unique across the catalogue.
//
// No engineering range is assumed for any tag except the pump outlet range, which the Owner
// confirmed as domain information. Every other range is UNCONFIGURED.

import { CHANNEL_TYPE, DIRECTION, SIGNAL } from './constants.mjs';

const IV_IDS = Object.freeze([1, 2, 3, 4, 5, 6, 7, 8]);

/** The single Pump-ready gate input. It is fixed in code, not configurable (no new configurability). */
export const PUMP_READY_GATE_TAG = 'PUMP_OUTLET_PRESSURE';
/** Runtime source identity of the Pump-ready gate (matches PressureSample.PumpOutletSource in the Runtime). */
export const PUMP_READY_SOURCE_IDENTITY = 'PUMP_OUTLET';
/** Owner-confirmed workbook identifiers of the pump measurements. Fixed; a seed cannot change them. */
export const OWNER_PUMP_SOURCE_TAGS = Object.freeze({
  PUMP_INLET_PRESSURE: 'AI-002',
  PUMP_OUTLET_PRESSURE: 'AI-003',
});

function analogPressure(tagName, role, sourceIdentity, pairIndex, meta = {}) {
  return Object.freeze({
    tagName,
    role,
    direction: DIRECTION.INPUT,
    channelType: CHANNEL_TYPE.ANALOG,
    signal: SIGNAL.CURRENT_4_20_MA,
    sourceIdentity,
    pairIndex,
    readOnlyStage: true,
    displayName: meta.displayName ?? tagName,
    canonicalIdentity: meta.canonicalIdentity ?? tagName,
    workbookTag: meta.workbookTag ?? null,
    physicalDescription: meta.physicalDescription ?? null,
    engineeringMeaning: meta.engineeringMeaning ?? null,
    readinessRole: meta.readinessRole ?? 'NONE',
    confirmedEngineeringRange: meta.confirmedEngineeringRange ?? null,
  });
}

function digitalLimit(tagName, role, pairIndex) {
  return Object.freeze({
    tagName,
    role,
    direction: DIRECTION.INPUT,
    channelType: CHANNEL_TYPE.DIGITAL,
    signal: SIGNAL.DIGITAL_24_VDC,
    sourceIdentity: tagName,
    pairIndex,
    readOnlyStage: true,
    displayName: tagName,
    canonicalIdentity: tagName,
    workbookTag: null,
    confirmedEngineeringRange: null,
  });
}

const PRESSURE_TAGS = Object.freeze([
  analogPressure('PUMP_INLET_PRESSURE', 'PUMP_INLET_PRESSURE', 'PUMP_INLET', null, {
    displayName: 'Pump Inlet Pressure',
    canonicalIdentity: 'PumpInletPressureBar',
    workbookTag: OWNER_PUMP_SOURCE_TAGS.PUMP_INLET_PRESSURE,
    physicalDescription: 'Pressure Transmitter - Pump inlet pressure',
    engineeringMeaning: 'Pressure in the Header Tank / suction piping before water enters the Pump. Not the Pump-ready pressure.',
    readinessRole: 'PUMP_INLET_DIAGNOSTIC',
  }),
  analogPressure('PUMP_OUTLET_PRESSURE', 'PUMP_OUTLET_PRESSURE', 'PUMP_OUTLET', null, {
    displayName: 'Pump Outlet Pressure',
    canonicalIdentity: 'PumpOutletPressureBar',
    workbookTag: OWNER_PUMP_SOURCE_TAGS.PUMP_OUTLET_PRESSURE,
    physicalDescription: 'Pressure Transmitter - Pump outlet pressure',
    engineeringMeaning: 'Pressure after the Pump on the main discharge piping, before the Main Valve / distribution toward the Water Jets.',
    readinessRole: 'PUMP_READY_GATE',
    confirmedEngineeringRange: Object.freeze({ min: 0, max: 40, unit: 'bar', basis: 'OWNER_CONFIRMED_DOMAIN_INFORMATION' }),
  }),
  analogPressure('MAIN_VALVE_OUTLET_PRESSURE', 'MAIN_VALVE_PRESSURE', 'MAIN_VALVE_OUTLET', null, {
    displayName: 'Main Valve Outlet Pressure',
    engineeringMeaning: 'No workbook measurement is assigned. It is NOT aliased to AI-003 (the Pump Outlet).',
  }),
  ...IV_IDS.map((n) => analogPressure(`IV${n}_OUTLET_PRESSURE`, 'VALVE_OUTLET_PRESSURE', `IV${n}_OUTLET`, n, {
    displayName: `IV${n} Outlet Pressure`,
    engineeringMeaning: `Isolation Valve ${n} outlet pressure, paired with WJ${n}. Valve diagnostics only.`,
  })),
]);

const LIMIT_TAGS = Object.freeze(IV_IDS.flatMap((n) => [
  digitalLimit(`IV${n}_UPPER_LIMIT`, 'VALVE_UPPER_LIMIT', n),
  digitalLimit(`IV${n}_LOWER_LIMIT`, 'VALVE_LOWER_LIMIT', n),
]));

const SIMULATION_TAGS = Object.freeze([...PRESSURE_TAGS, ...LIMIT_TAGS]);
const BY_NAME = new Map(SIMULATION_TAGS.map((t) => [t.tagName, t]));

/** Pump inlet and pump outlet roles. Neither is a valve or IV role. */
export function isPumpRole(role) {
  return role === 'PUMP_INLET_PRESSURE' || role === 'PUMP_OUTLET_PRESSURE';
}

/**
 * WJn uses the pressure of IVn. This is a naming relation only: it returns the
 * Runtime tag name and never a channel, address or fallback. Pump and Main Valve
 * pressures are never returned here.
 */
export function pressureTagForWj(n) {
  if (!IV_IDS.includes(n)) throw new Error(`no WJ${n}: pressure relation is defined for WJ1..WJ8 only`);
  return `IV${n}_OUTLET_PRESSURE`;
}

/** Returns the canonical identities that occur more than once in the given definitions. */
export function duplicateCanonicalIdentities(defs) {
  const seen = new Map();
  for (const d of defs) seen.set(d.canonicalIdentity, (seen.get(d.canonicalIdentity) ?? 0) + 1);
  return [...seen].filter(([, n]) => n > 1).map(([id]) => id).sort();
}

/** Tags that every valid Draft must carry a binding for (no required tag may be dropped). */
export const REQUIRED_TAG_NAMES = Object.freeze(SIMULATION_TAGS.map((t) => t.tagName));

export function listSimulationTags() {
  return SIMULATION_TAGS;
}

export function getTagDef(tagName) {
  return BY_NAME.get(tagName) ?? null;
}

export function isPressureRole(role) {
  return isPumpRole(role) || role === 'MAIN_VALVE_PRESSURE' || role === 'VALVE_OUTLET_PRESSURE';
}
