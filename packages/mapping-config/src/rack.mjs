// Stage 0.4B-1 — rack model: stable module instance identity, RackSlot,
// ProcessModulePosition, rack validation and reorder.
//
// RackSlot is the physical slot (1-based index in the rack order).
// ProcessModulePosition is the one-based Slot-order ordinal among physical rack
// modules that contribute process data (channelCount > 0). It is for topology
// display, stable physical ordering, reorder preview and diagnostics. It is NOT
// a ProcessImageOrder, byte offset, word offset or register number, and it is
// never used as a process-image offset or address (Stage 0.4B-2).
// ModuleInstanceId is independent of position and survives reorder.

import { CATEGORY, SEVERITY } from './constants.mjs';
import { getProfile } from './moduleProfiles.mjs';

const ID_PREFIX = Object.freeze({
  [CATEGORY.COUPLER]: 'COUPLER',
  [CATEGORY.SUPPLY]: 'SUPPLY',
  [CATEGORY.DIGITAL_INPUT]: 'DI-MODULE',
  [CATEGORY.DIGITAL_OUTPUT]: 'DO-MODULE',
  [CATEGORY.ANALOG_INPUT]: 'AI-MODULE',
  [CATEGORY.ANALOG_OUTPUT]: 'AO-MODULE',
  [CATEGORY.END]: 'END-MODULE',
});

/**
 * Builds module instances from an ordered list of model numbers. IDs are
 * assigned per category in rack order: COUPLER-01, SUPPLY-01, SUPPLY-02, ...
 * Throws when a model has no profile, so an unknown model cannot enter a rack.
 */
export function buildModuleInstances(modelNumbers) {
  const counters = new Map();
  return modelNumbers.map((modelNumber) => {
    const profile = getProfile(modelNumber);
    if (!profile) throw new Error(`no Module Profile for model ${modelNumber}`);
    const prefix = ID_PREFIX[profile.category];
    const n = (counters.get(prefix) ?? 0) + 1;
    counters.set(prefix, n);
    return { moduleInstanceId: `${prefix}-${String(n).padStart(2, '0')}`, modelNumber };
  });
}

/**
 * Derived, read-only view of an ordered rack. Each entry carries RackSlot,
 * ProcessModulePosition, category and address status. Nothing here is editable.
 */
export function deriveRackView(modules) {
  let processOrdinal = 0;
  return modules.map((m, index) => {
    const profile = getProfile(m.modelNumber);
    const hasChannels = profile ? profile.channelCount > 0 : false;
    if (hasChannels) processOrdinal += 1;
    return Object.freeze({
      moduleInstanceId: m.moduleInstanceId,
      modelNumber: m.modelNumber,
      rackSlot: index + 1,
      processModulePosition: hasChannels ? processOrdinal : null,
      processModulePositionStatus: hasChannels ? 'PROVISIONAL_SLOT_ORDER' : 'NOT_A_CHANNEL_MODULE',
      category: profile ? profile.category : null,
      displayName: profile ? profile.displayName : null,
      channelCapacity: profile ? profile.channelCount : 0,
      profileStatus: profile ? profile.profileStatus : 'UNKNOWN_MODEL',
      addressStatus: hasChannels ? 'ADDRESS_UNRESOLVED' : 'NOT_APPLICABLE',
      addressReasons: hasChannels ? (profile ? [...profile.missing] : ['UNKNOWN_MODEL_PROFILE']) : [],
    });
  });
}

function issue(code, severity, message, extra = {}) {
  return Object.freeze({ code, severity, message, ...extra });
}

/**
 * Rack validation. `declaredChannelCounts` (optional) maps moduleInstanceId to
 * the channel count recorded in the source; a mismatch with the profile is an
 * error, because the profile is the only authority for channel capacity.
 */
export function validateRack(modules, declaredChannelCounts = {}) {
  const issues = [];
  if (modules.length === 0) {
    issues.push(issue('RACK_EMPTY', SEVERITY.ERROR, 'rack has no modules'));
    return issues;
  }

  const seen = new Set();
  for (const m of modules) {
    if (seen.has(m.moduleInstanceId)) {
      issues.push(issue('DUPLICATE_MODULE_INSTANCE_ID', SEVERITY.ERROR,
        `duplicate ModuleInstanceId ${m.moduleInstanceId}`, { moduleInstanceId: m.moduleInstanceId }));
    }
    seen.add(m.moduleInstanceId);

    const profile = getProfile(m.modelNumber);
    if (!profile) {
      issues.push(issue('UNKNOWN_MODEL_PROFILE', SEVERITY.ERROR,
        `module ${m.moduleInstanceId} has no Module Profile`, { moduleInstanceId: m.moduleInstanceId }));
      continue;
    }
    if (profile.profileStatus !== 'COMPLETE') {
      issues.push(issue('MODULE_PROFILE_INCOMPLETE', SEVERITY.WARNING,
        `Module Profile for ${m.modelNumber} is INCOMPLETE; addresses stay ADDRESS_UNRESOLVED`,
        { moduleInstanceId: m.moduleInstanceId, reasons: [...profile.missing] }));
    }
    const declared = declaredChannelCounts[m.moduleInstanceId];
    if (declared !== undefined && declared !== profile.channelCount) {
      issues.push(issue('CHANNEL_COUNT_MISMATCH', SEVERITY.ERROR,
        `${m.moduleInstanceId} declares ${declared} channels; profile has ${profile.channelCount}`,
        { moduleInstanceId: m.moduleInstanceId }));
    }
  }

  const couplers = modules.map((m, i) => ({ i, c: getProfile(m.modelNumber)?.category })).filter((x) => x.c === CATEGORY.COUPLER);
  if (couplers.length !== 1) {
    issues.push(issue('COUPLER_COUNT', SEVERITY.ERROR, `rack must contain exactly one Coupler (found ${couplers.length})`));
  } else if (couplers[0].i !== 0) {
    issues.push(issue('COUPLER_NOT_FIRST', SEVERITY.ERROR, 'Coupler must be the first module'));
  }

  const ends = modules.map((m, i) => ({ i, c: getProfile(m.modelNumber)?.category })).filter((x) => x.c === CATEGORY.END);
  if (ends.length !== 1) {
    issues.push(issue('END_COUNT', SEVERITY.ERROR, `rack must contain exactly one End Module (found ${ends.length})`));
  } else if (ends[0].i !== modules.length - 1) {
    issues.push(issue('END_NOT_LAST', SEVERITY.ERROR, 'End Module must be the last module'));
  }
  return issues;
}

/**
 * Moves one module to `toIndex` and returns a new ordered array. The source is
 * not modified. A move is refused when the module is fixed by its profile or
 * when the result would violate the Coupler/End constraints. Only the codes of
 * the refusal are returned; no partial state is produced.
 */
export function moveModule(modules, moduleInstanceId, toIndex) {
  const from = modules.findIndex((m) => m.moduleInstanceId === moduleInstanceId);
  if (from < 0) return refusal('MODULE_NOT_FOUND', `no module ${moduleInstanceId}`);
  if (!Number.isInteger(toIndex) || toIndex < 0 || toIndex >= modules.length) {
    return refusal('TARGET_INDEX_OUT_OF_RANGE', `target index ${toIndex} is outside the rack`);
  }
  const moving = modules[from];
  const profile = getProfile(moving.modelNumber);
  if (!profile || profile.reorderable === false) {
    return refusal('REORDER_REFUSED_FIXED_MODULE', `${moduleInstanceId} is fixed by its Module Profile`);
  }
  if (from === toIndex) return { ok: true, modules: modules.slice(), refusal: null };

  const next = modules.slice();
  next.splice(from, 1);
  next.splice(toIndex, 0, moving);

  const blocking = validateRack(next).filter((x) => x.severity === SEVERITY.ERROR
    && (x.code === 'COUPLER_NOT_FIRST' || x.code === 'END_NOT_LAST'
      || x.code === 'COUPLER_COUNT' || x.code === 'END_COUNT'));
  if (blocking.length > 0) {
    return refusal(blocking[0].code, blocking[0].message);
  }
  return { ok: true, modules: next, refusal: null };
}

function refusal(code, message) {
  return Object.freeze({ ok: false, modules: null, refusal: Object.freeze({ code, message }) });
}
