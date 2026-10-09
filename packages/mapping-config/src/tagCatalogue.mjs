// Stage 0.4B-1 — Simulation/Runtime Tag catalogue (transport-independent).
//
// These identities are Runtime names, not hardware addresses. Source identities
// (PUMP_OUTLET, MAIN_VALVE_OUTLET, IVn_OUTLET) match the identities already used
// by the Runtime. Pump pressure and Isolation Valve pressure are separate
// identities. There is no alias, fallback or averaging between them.
//
// No engineering range is assumed for any tag except the pump range, which the
// Owner confirmed as domain information. Every other range is UNCONFIGURED.

import { CHANNEL_TYPE, DIRECTION, SIGNAL } from './constants.mjs';

const IV_IDS = Object.freeze([1, 2, 3, 4, 5, 6, 7, 8]);

function analogPressure(tagName, role, sourceIdentity, pairIndex, extra = {}) {
  return Object.freeze({
    tagName,
    role,
    direction: DIRECTION.INPUT,
    channelType: CHANNEL_TYPE.ANALOG,
    signal: SIGNAL.CURRENT_4_20_MA,
    sourceIdentity,
    pairIndex,
    readOnlyStage: true,
    confirmedEngineeringRange: null,
    ...extra,
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
    confirmedEngineeringRange: null,
  });
}

const PRESSURE_TAGS = Object.freeze([
  analogPressure('PUMP_OUTLET_PRESSURE', 'PUMP_PRESSURE', 'PUMP_OUTLET', null, {
    confirmedEngineeringRange: Object.freeze({ min: 0, max: 40, unit: 'bar', basis: 'OWNER_CONFIRMED_DOMAIN_INFORMATION' }),
  }),
  analogPressure('MAIN_VALVE_OUTLET_PRESSURE', 'MAIN_VALVE_PRESSURE', 'MAIN_VALVE_OUTLET', null),
  ...IV_IDS.map((n) => analogPressure(`IV${n}_OUTLET_PRESSURE`, 'VALVE_OUTLET_PRESSURE', `IV${n}_OUTLET`, n)),
]);

const LIMIT_TAGS = Object.freeze(IV_IDS.flatMap((n) => [
  digitalLimit(`IV${n}_UPPER_LIMIT`, 'VALVE_UPPER_LIMIT', n),
  digitalLimit(`IV${n}_LOWER_LIMIT`, 'VALVE_LOWER_LIMIT', n),
]));

const SIMULATION_TAGS = Object.freeze([...PRESSURE_TAGS, ...LIMIT_TAGS]);
const BY_NAME = new Map(SIMULATION_TAGS.map((t) => [t.tagName, t]));

/**
 * WJn uses the pressure of IVn. This is a naming relation only: it returns the
 * Runtime tag name and never a channel, address or fallback. Pump and Valve
 * pressures are never returned here.
 */
export function pressureTagForWj(n) {
  if (!IV_IDS.includes(n)) throw new Error(`no WJ${n}: pressure relation is defined for WJ1..WJ8 only`);
  return `IV${n}_OUTLET_PRESSURE`;
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
  return role === 'PUMP_PRESSURE' || role === 'MAIN_VALVE_PRESSURE' || role === 'VALVE_OUTLET_PRESSURE';
}
