// Stage 0.4B-1 — mapping validation.
//
// Validates Tag bindings against the ordered rack and the Module Profiles.
// Output is a deterministic list of issues. Nothing here changes the
// configuration, and nothing here assigns an address.

import { CHANNEL_TYPE, DIRECTION, FORBIDDEN_BINDING_KEYS, POLARITY, CONTACT, SEVERITY } from './constants.mjs';
import { getProfile } from './moduleProfiles.mjs';
import {
  getTagDef, REQUIRED_TAG_NAMES, isPressureRole, isPumpRole, duplicateCanonicalIdentities,
  PUMP_READY_GATE_TAG, PUMP_READY_SOURCE_IDENTITY, OWNER_PUMP_SOURCE_TAGS,
} from './tagCatalogue.mjs';

function issue(code, severity, message, where = {}) {
  return Object.freeze({ code, severity, message, ...where });
}

function isPositiveInt(v) {
  return Number.isInteger(v) && v >= 1;
}

/**
 * Validates bindings.
 *   modules          ordered [{moduleInstanceId, modelNumber}]
 *   bindings         [{tagName, moduleInstanceId, channel, enabled, engineering, activePolarity, contactType, declaredSourceIdentity}]
 *   additionalTags   optional map tagName -> read-only tag definition (from the import)
 */
export function validateMapping(modules, bindings, additionalTags = {}) {
  const issues = [];
  const moduleById = new Map(modules.map((m) => [m.moduleInstanceId, m]));
  const defFor = (tagName) => getTagDef(tagName) ?? additionalTags[tagName] ?? null;

  const enabledByChannel = new Map();
  const seenTags = new Set();

  for (const b of bindings) {
    const where = { tagName: b.tagName, moduleInstanceId: b.moduleInstanceId, channel: b.channel };

    for (const key of FORBIDDEN_BINDING_KEYS) {
      if (Object.prototype.hasOwnProperty.call(b, key)) {
        issues.push(issue('MANUAL_ADDRESS_REFUSED', SEVERITY.ERROR,
          `binding ${b.tagName} carries '${key}'; addresses are derived and read-only`, where));
      }
    }

    if (seenTags.has(b.tagName)) {
      issues.push(issue('DUPLICATE_TAG_BINDING', SEVERITY.ERROR, `tag ${b.tagName} is bound more than once`, where));
    }
    seenTags.add(b.tagName);

    const def = defFor(b.tagName);
    if (!def) {
      issues.push(issue('UNKNOWN_TAG', SEVERITY.ERROR, `tag ${b.tagName} is not in the Simulation/Runtime catalogue`, where));
      continue;
    }

    if (def.direction === DIRECTION.OUTPUT) {
      issues.push(issue('OUTPUT_BINDING_REFUSED', SEVERITY.ERROR,
        `output tag ${b.tagName} is NOT AUTHORIZED FOR MAPPING IN READ-ONLY STAGE`, where));
      continue;
    }

    const mod = moduleById.get(b.moduleInstanceId);
    if (!mod) {
      issues.push(issue('MODULE_NOT_FOUND', SEVERITY.ERROR, `module ${b.moduleInstanceId} does not exist`, where));
      continue;
    }
    const profile = getProfile(mod.modelNumber);
    if (!profile) {
      issues.push(issue('UNKNOWN_MODEL_PROFILE', SEVERITY.ERROR, `module ${b.moduleInstanceId} has no Module Profile`, where));
      continue;
    }

    if (!isPositiveInt(b.channel) || b.channel > profile.channelCount) {
      issues.push(issue('CHANNEL_OUT_OF_RANGE', SEVERITY.ERROR,
        `channel ${b.channel} is outside 1..${profile.channelCount} for ${b.moduleInstanceId}`, where));
      continue;
    }

    if (profile.direction !== def.direction) {
      issues.push(issue('DIRECTION_MISMATCH', SEVERITY.ERROR,
        `tag ${b.tagName} needs ${def.direction}; ${b.moduleInstanceId} is ${profile.direction}`, where));
    }

    if (profile.channelType !== def.channelType) {
      let code = 'SIGNAL_TYPE_MISMATCH';
      if (def.channelType === CHANNEL_TYPE.ANALOG && profile.channelType === CHANNEL_TYPE.DIGITAL) {
        code = isPressureRole(def.role) ? 'PRESSURE_TO_DIGITAL_REFUSED' : 'SIGNAL_TYPE_MISMATCH';
      } else if (def.channelType === CHANNEL_TYPE.DIGITAL && profile.channelType === CHANNEL_TYPE.ANALOG) {
        code = def.role === 'VALVE_UPPER_LIMIT' || def.role === 'VALVE_LOWER_LIMIT' ? 'LIMIT_TO_ANALOG_REFUSED' : 'SIGNAL_TYPE_MISMATCH';
      }
      issues.push(issue(code, SEVERITY.ERROR,
        `tag ${b.tagName} (${def.channelType}) cannot bind to ${profile.channelType} channel`, where));
    } else if (!profile.supportedSignals.includes(def.signal)) {
      issues.push(issue('SIGNAL_TYPE_MISMATCH', SEVERITY.ERROR,
        `tag ${b.tagName} signal ${def.signal} is not supported by ${mod.modelNumber}`, where));
    }

    if (b.declaredSourceIdentity !== undefined && b.declaredSourceIdentity !== def.sourceIdentity) {
      const code = isPumpRole(def.role) || def.role === 'VALVE_OUTLET_PRESSURE'
        ? 'PAIRED_IV_IDENTITY_MISMATCH' : 'SOURCE_IDENTITY_MISMATCH';
      issues.push(issue(code, SEVERITY.ERROR,
        `tag ${b.tagName} declares source ${b.declaredSourceIdentity}; expected ${def.sourceIdentity}`, where));
    }

    if (b.enabled !== false) {
      const key = `${b.moduleInstanceId}#${b.channel}`;
      if (!enabledByChannel.has(key)) enabledByChannel.set(key, []);
      enabledByChannel.get(key).push({ def, b });
    }

    if (def.channelType === CHANNEL_TYPE.ANALOG && b.enabled !== false) {
      if (b.engineering == null) {
        issues.push(issue('ENGINEERING_RANGE_UNCONFIGURED', SEVERITY.WARNING,
          `${b.tagName} engineering range is UNCONFIGURED; topology may be validated, engineering-unit acquisition is not ready`, where));
      } else {
        const { min, max, unit } = b.engineering;
        if (!Number.isFinite(min) || !Number.isFinite(max) || !(min < max) || typeof unit !== 'string' || unit.trim() === '') {
          issues.push(issue('INVALID_ENGINEERING_RANGE', SEVERITY.ERROR,
            `${b.tagName} engineering range must be finite, min < max, with a unit`, where));
        }
      }
    }

    if (def.channelType === CHANNEL_TYPE.DIGITAL && b.enabled !== false) {
      if (b.activePolarity == null) {
        issues.push(issue('POLARITY_NOT_EXPLICIT', SEVERITY.ERROR,
          `${b.tagName} active polarity must be set explicitly`, where));
      } else if (b.activePolarity !== POLARITY.ACTIVE_WHEN_CLOSED && b.activePolarity !== POLARITY.ACTIVE_WHEN_OPEN) {
        issues.push(issue('POLARITY_INVALID', SEVERITY.ERROR, `${b.tagName} polarity ${b.activePolarity} is not recognised`, where));
      }
      if (b.contactType == null) {
        issues.push(issue('CONTACT_TYPE_UNKNOWN', SEVERITY.WARNING,
          `${b.tagName} NO/NC contact information is not recorded`, where));
      } else if (b.contactType !== CONTACT.NO && b.contactType !== CONTACT.NC) {
        issues.push(issue('CONTACT_TYPE_INVALID', SEVERITY.ERROR, `${b.tagName} contact type ${b.contactType} is not recognised`, where));
      }
    }
  }

  for (const [key, entries] of enabledByChannel) {
    if (entries.length < 2) continue;
    const [moduleInstanceId, channel] = key.split('#');
    const roles = entries.map((e) => e.def.role);
    const pump = roles.some((r) => isPumpRole(r));
    const valve = roles.some((r) => r === 'VALVE_OUTLET_PRESSURE');
    const where = { moduleInstanceId, channel: Number(channel), tagName: entries.map((e) => e.b.tagName).join(' + ') };
    if (pump && valve) {
      issues.push(issue('PUMP_VALVE_CHANNEL_SHARED', SEVERITY.ERROR, 'Pump pressure and Valve pressure cannot share a Channel', where));
    }
    issues.push(issue('DUPLICATE_CHANNEL_BINDING', SEVERITY.ERROR,
      `Channel ${channel} of ${moduleInstanceId} is bound by more than one enabled tag`, where));
  }

  // Owner rule: all limit inputs share one explicit ContactPolarity. A single channel may differ
  // only when its binding records polarityOverride: true (a future Owner-authorised revision).
  const limitBindings = bindings.filter((b) => {
    const def = defFor(b.tagName);
    return def && (def.role === 'VALVE_UPPER_LIMIT' || def.role === 'VALVE_LOWER_LIMIT') && b.enabled !== false;
  });
  const basePolarities = new Set(limitBindings.filter((b) => b.polarityOverride !== true).map((b) => b.activePolarity));
  if (basePolarities.size > 1) {
    issues.push(issue('LIMIT_POLARITY_NOT_UNIFORM', SEVERITY.ERROR,
      `limit inputs use ${[...basePolarities].map(String).sort().join(' and ')}; one ContactPolarity is required unless a channel records an override`));
  }

  const pressureSources = new Map();
  for (const b of bindings) {
    const def = defFor(b.tagName);
    if (!def || !isPressureRole(def.role) || b.enabled === false) continue;
    if (pressureSources.has(def.sourceIdentity)) {
      issues.push(issue('ALIAS_SOURCE_IDENTITY', SEVERITY.ERROR,
        `source ${def.sourceIdentity} is used by two pressure tags`, { tagName: b.tagName }));
    }
    pressureSources.set(def.sourceIdentity, b.tagName);
  }

  issues.push(...pressureBoundaryIssues(bindings, defFor));

  const boundNames = new Set(bindings.map((b) => b.tagName));
  for (const name of REQUIRED_TAG_NAMES) {
    if (!boundNames.has(name)) {
      issues.push(issue('REQUIRED_TAG_MISSING', SEVERITY.ERROR, `required tag ${name} has no binding`, { tagName: name }));
    }
  }
  for (const b of bindings) {
    if (REQUIRED_TAG_NAMES.includes(b.tagName) && b.enabled === false) {
      issues.push(issue('REQUIRED_TAG_DISABLED', SEVERITY.WARNING, `required tag ${b.tagName} is disabled`, { tagName: b.tagName }));
    }
  }

  return sortIssues(issues);
}

/**
 * Pressure boundary rules (Owner clarification, 2026-10-10). AI-002 (Pump Inlet), AI-003 (Pump Outlet) and
 * AI-004..AI-011 (IV1..IV8 outlet) are distinct physical measurements. No alias, fallback or derivation may cross them.
 * Only enabled bindings are compared, as in the existing alias check.
 */
function pressureBoundaryIssues(bindings, defFor) {
  const out = [];
  const live = bindings
    .filter((b) => b.enabled !== false)
    .map((b) => ({ b, def: defFor(b.tagName) }))
    .filter((x) => x.def && isPressureRole(x.def.role));

  // 1. Each pump measurement keeps its Owner-assigned workbook identifier.
  for (const { b, def } of live) {
    if (!isPumpRole(def.role) || b.sourceWorkbookTag === undefined) continue;
    const expected = OWNER_PUMP_SOURCE_TAGS[def.tagName];
    if (b.sourceWorkbookTag !== expected) {
      out.push(issue('PUMP_SOURCE_MISMATCH', SEVERITY.ERROR,
        `${b.tagName} is bound to ${b.sourceWorkbookTag}; the Owner clarification assigns ${expected}`, { tagName: b.tagName }));
    }
  }

  // 2. One physical measurement serves at most one pressure identity.
  const bySource = new Map();
  for (const x of live) {
    if (x.b.sourceWorkbookTag === undefined || x.b.sourceWorkbookTag === null) continue;
    if (!bySource.has(x.b.sourceWorkbookTag)) bySource.set(x.b.sourceWorkbookTag, []);
    bySource.get(x.b.sourceWorkbookTag).push(x);
  }
  for (const [source, group] of bySource) {
    if (group.length < 2) continue;
    const pumps = group.filter((g) => isPumpRole(g.def.role));
    const ivs = group.filter((g) => g.def.role === 'VALVE_OUTLET_PRESSURE');
    let code = 'PHYSICAL_SOURCE_ALIAS';
    if (pumps.length > 0 && ivs.length > 0) code = 'PUMP_IV_CROSS_BINDING';
    else if (pumps.length > 1) code = 'PUMP_INLET_OUTLET_ALIAS';
    out.push(issue(code, SEVERITY.ERROR,
      `workbook measurement ${source} resolves to ${group.map((g) => g.b.tagName).join(' and ')}; each measurement serves one pressure identity`,
      { tagName: group.map((g) => g.b.tagName).join(' + ') }));
  }

  // 3. The Pump-ready gate reads the Pump Outlet (AI-003 / PUMP_OUTLET) only. It never reads AI-002 or an IV.
  const gateDef = getTagDef(PUMP_READY_GATE_TAG);
  if (!gateDef || gateDef.role !== 'PUMP_OUTLET_PRESSURE' || gateDef.sourceIdentity !== PUMP_READY_SOURCE_IDENTITY) {
    out.push(issue('PUMP_READY_GATE_MISCONFIGURED', SEVERITY.ERROR,
      'the Pump-ready gate must read the Pump Outlet catalogue entry only', { tagName: PUMP_READY_GATE_TAG }));
  }
  for (const { b } of live) {
    if (b.tagName !== PUMP_READY_GATE_TAG) continue;
    const gateWrong = (b.sourceWorkbookTag !== undefined && b.sourceWorkbookTag !== OWNER_PUMP_SOURCE_TAGS.PUMP_OUTLET_PRESSURE)
      || (b.declaredSourceIdentity !== undefined && b.declaredSourceIdentity !== PUMP_READY_SOURCE_IDENTITY);
    if (gateWrong) {
      out.push(issue('PUMP_READY_GATE_SOURCE_INVALID', SEVERITY.ERROR,
        `the Pump-ready gate reads ${b.sourceWorkbookTag ?? b.declaredSourceIdentity}; it must read AI-003 / PUMP_OUTLET only`, { tagName: b.tagName }));
    }
  }

  // 4. IVn pressure pairs with WJn by ordinal and source identity. Pump pressures never pair with a WJ.
  for (const { b, def } of live) {
    if (def.role !== 'VALVE_OUTLET_PRESSURE') continue;
    const n = Number(/^IV([1-8])_OUTLET_PRESSURE$/.exec(def.tagName)?.[1]);
    if (def.pairIndex !== n || def.sourceIdentity !== `IV${n}_OUTLET`) {
      out.push(issue('PAIRED_IV_IDENTITY_MISMATCH', SEVERITY.ERROR,
        `${b.tagName} is not the paired IV${n} pressure`, { tagName: b.tagName }));
    }
  }

  // 5. Canonical runtime identities are unique across the bound definitions.
  const defs = [...new Map(bindings.map((b) => [b.tagName, defFor(b.tagName)]).filter(([, d]) => d))
    .values()].filter((d) => typeof d.canonicalIdentity === 'string');
  for (const id of duplicateCanonicalIdentities(defs)) {
    out.push(issue('CANONICAL_IDENTITY_DUPLICATED', SEVERITY.ERROR, `canonical identity ${id} is used by more than one tag`));
  }

  return out;
}

function sortIssues(list) {
  const rank = { ERROR: 0, WARNING: 1, INFO: 2 };
  return list.slice().sort((a, b) =>
    (rank[a.severity] - rank[b.severity])
    || a.code.localeCompare(b.code)
    || String(a.tagName ?? '').localeCompare(String(b.tagName ?? ''))
    || String(a.moduleInstanceId ?? '').localeCompare(String(b.moduleInstanceId ?? ''))
    || String(a.channel ?? 0).localeCompare(String(b.channel ?? 0)));
}

export function summariseIssues(issues) {
  const out = { errors: 0, warnings: 0, info: 0, byCode: {} };
  for (const i of issues) {
    if (i.severity === SEVERITY.ERROR) out.errors += 1;
    else if (i.severity === SEVERITY.WARNING) out.warnings += 1;
    else out.info += 1;
    out.byCode[i.code] = (out.byCode[i.code] ?? 0) + 1;
  }
  return out;
}
