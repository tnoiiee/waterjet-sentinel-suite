// Stage 0.4B-1 — Simulation/Runtime Tag catalogue (transport-independent).
//
// These identities are Runtime names, not hardware addresses. Source identities (PUMP_INLET, PUMP_OUTLET,
// IVn_OUTLET) are the identities used by the Runtime. Owner clarification (2026-10-10), authoritative for meaning:
//   AI-002  canonical PumpInletPressureBar  - Pump Inlet Pressure.
//           Header Tank / suction-side pressure before the Pump. Diagnostic only. NOT Pump-ready.
//   AI-003  canonical PumpOutletPressureBar - Pump Outlet Pressure.
//           Pump discharge pressure used by the pre-P1 Pump-ready gate. The ONLY Pump-ready input.
//   AI-004..AI-011  IV1..IV8 outlet pressure, ordinal by '#n' (valve diagnostics only).
//
// Identity and location are separate. The Owner decides the workbook identifier and the canonical identity.
// The physical module, Slot and Channel come from the workbook default and may be changed in the Draft when the
// change is compatible. The workbook description is kept as source evidence and is never used as the identity.
//
// Authoritative default bindings (Owner ruling 2026-10-10) are derived from explicit workbook identifiers and
// Owner ordinal rules, so they load without a seed: AI-002, AI-003, AI-004..AI-011 (IV1..IV8 outlet pressure),
// DI-021..DI-028 (IV1..IV8 Lower limit) and DI-029..DI-036 (IV1..IV8 Upper limit). 26 bindings in total.
// The 18 placeholder rows (DI-037, DO-031, AI-020..AI-035) are never default bindings.
//
// No Main Valve I/O tag is defined. The Owner's mention of a Main Valve describes the water path, not an I/O point.
// Nothing here is a Main Valve command, feedback, pressure measurement or required row.
//
// Pump inlet, pump outlet and IVn pressure are distinct physical measurements. There is no alias, fallback,
// averaging or derivation between them.
//
// tagName is the Draft and seed key and is kept unchanged. canonicalIdentity is the runtime identity named by the
// Owner and must be unique across the catalogue. Both pump transmitters are 4-20 mA, 0-40 bar (Owner ruling
// 2026-10-10, Owner-confirmed basis). Every other engineering range is UNCONFIGURED.

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

/** Owner-confirmed engineering range for BOTH pump transmitters (ruling 2026-10-10). */
const OWNER_PUMP_RANGE = Object.freeze({ min: 0, max: 40, unit: 'bar', basis: 'OWNER_CONFIRMED_DOMAIN_INFORMATION' });

const pad3 = (n) => String(n).padStart(3, '0');
const IV_DEFAULT_SOURCE = (n) => ({
  [`IV${n}_OUTLET_PRESSURE`]: `AI-${pad3(n + 3)}`,
  [`IV${n}_LOWER_LIMIT`]: `DI-${pad3(20 + n)}`,
  [`IV${n}_UPPER_LIMIT`]: `DI-${pad3(28 + n)}`,
});
const OWNER_DEFAULT_SOURCE = Object.freeze({
  ...OWNER_PUMP_SOURCE_TAGS,
  ...Object.assign({}, ...[1, 2, 3, 4, 5, 6, 7, 8].map(IV_DEFAULT_SOURCE)),
});

/**
 * The 26 authoritative default bindings, in order: pump inlet, pump outlet, then for each IVn its outlet pressure,
 * Lower limit and Upper limit. Each is derived from an explicit workbook identifier and an Owner ordinal rule.
 */
export const OWNER_DEFAULT_TAG_NAMES = Object.freeze([
  'PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE',
  ...[1, 2, 3, 4, 5, 6, 7, 8].flatMap((n) => [`IV${n}_OUTLET_PRESSURE`, `IV${n}_LOWER_LIMIT`, `IV${n}_UPPER_LIMIT`]),
]);

/** The workbook identifier that the Owner rules give a default tag, or null. */
export function ownerDefaultSourceFor(tagName) {
  return OWNER_DEFAULT_SOURCE[tagName] ?? null;
}

/**
 * The 18 placeholder rows (USED / RESERVED, SignalIdentity UNRESOLVED, TagBinding UNBOUND, OwnerInputStatus
 * OWNER_INPUT_PENDING, AddressStatus ADDRESS_UNRESOLVED). They are never FREE and never auto-bound.
 */
export const OWNER_PLACEHOLDER_ROWS = Object.freeze([
  'DI-037', 'DO-031',
  ...Array.from({ length: 16 }, (_, i) => `AI-${String(20 + i).padStart(3, '0')}`),
]);

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
    engineeringMeaning: meta.engineeringMeaning ?? null,
    readinessRole: meta.readinessRole ?? 'NONE',
    confirmedEngineeringRange: meta.confirmedEngineeringRange ?? null,
    permittedUse: Object.freeze([...(meta.permittedUse ?? [])]),
    pumpReadyEvidence: meta.pumpReadyEvidence === true,
    valveDiagnosticEvidence: meta.valveDiagnosticEvidence === true,
    // No alarm, trip or interlock use is defined for any pressure in Stage 0.4B-1.
    automaticUse: Object.freeze({ alarm: false, trip: false, interlock: false }),
    hardwareLimitation: meta.hardwareLimitation ?? null,
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
    engineeringMeaning: 'Header Tank / suction-side pressure before the Pump. Read-only diagnostic and trend only. Not Pump-ready evidence.',
    readinessRole: 'PUMP_INLET_DIAGNOSTIC',
    confirmedEngineeringRange: OWNER_PUMP_RANGE,
    permittedUse: ['DIAGNOSTIC', 'TREND'],
    pumpReadyEvidence: false,
    valveDiagnosticEvidence: false,
    hardwareLimitation: 'Expected installed-system pressure is below approximately 1 bar, a small part of the 0-40 bar span. '
      + 'Accepted hardware-selection limitation. A future hardware review may consider a lower-range or compound transmitter. '
      + 'No NPSH, cavitation or precise suction-protection claim is made.',
  }),
  analogPressure('PUMP_OUTLET_PRESSURE', 'PUMP_OUTLET_PRESSURE', 'PUMP_OUTLET', null, {
    displayName: 'Pump Outlet Pressure',
    canonicalIdentity: 'PumpOutletPressureBar',
    workbookTag: OWNER_PUMP_SOURCE_TAGS.PUMP_OUTLET_PRESSURE,
    engineeringMeaning: 'Pump discharge pressure on the main discharge pipe, immediately after the Pump. The only pre-P1 Pump-ready pressure gate.',
    readinessRole: 'PUMP_READY_GATE',
    confirmedEngineeringRange: OWNER_PUMP_RANGE,
    permittedUse: ['PUMP_READY_GATE'],
    pumpReadyEvidence: true,
    valveDiagnosticEvidence: false,
  }),
  ...IV_IDS.map((n) => analogPressure(`IV${n}_OUTLET_PRESSURE`, 'VALVE_OUTLET_PRESSURE', `IV${n}_OUTLET`, n, {
    displayName: `IV${n} Outlet Pressure`,
    engineeringMeaning: `Isolation Valve ${n} outlet pressure, paired with WJ${n}. Valve diagnostics only.`,
    permittedUse: ['VALVE_DIAGNOSTIC'],
    pumpReadyEvidence: false,
    valveDiagnosticEvidence: true,
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
 * Runtime tag name and never a channel, address or fallback. Pump pressures are never returned here.
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
  return isPumpRole(role) || role === 'VALVE_OUTLET_PRESSURE';
}
