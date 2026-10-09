// Stage 0.4B-1 — mapping validation.
//
// Validates Tag bindings against the ordered rack and the Module Profiles.
// Output is a deterministic list of issues. Nothing here changes the
// configuration, and nothing here assigns an address.

import { CHANNEL_TYPE, DIRECTION, FORBIDDEN_BINDING_KEYS, POLARITY, CONTACT, SEVERITY } from './constants.mjs';
import { getProfile } from './moduleProfiles.mjs';
import { getTagDef, REQUIRED_TAG_NAMES, isPressureRole } from './tagCatalogue.mjs';

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
      const code = def.role === 'PUMP_PRESSURE' || def.role === 'MAIN_VALVE_PRESSURE' || def.role === 'VALVE_OUTLET_PRESSURE'
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
    const pump = roles.includes('PUMP_PRESSURE');
    const valve = roles.some((r) => r === 'MAIN_VALVE_PRESSURE' || r === 'VALVE_OUTLET_PRESSURE');
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
