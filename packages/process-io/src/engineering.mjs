// H2 Physical Process-I/O — typed engineering conversion and digital interpretation.
//
// Exact Legacy formulas (clamped linear map chains) are applied to raw observations to
// produce engineering observations. No engineering value is fabricated where the scaling
// is unresolved (raw-only rows keep engineeringValue UNRESOLVED). 0-20 mA rows never get a
// live-zero assumption. Invalid inputs (raw outside 0..32767) are typed diagnostics.
//
// Pure arithmetic. No I/O.

import { ANALOG_CONVERSIONS, ANALOG_OUTPUT_CONVERSION, legacyMap, DIGITAL_INTERPRETATION } from './legacyEvidence.mjs';

export const RAW_MIN = 0;
export const RAW_MAX = 32767;

/** Apply one conversion step chain exactly as the Legacy call site does. */
export function applySteps(raw, steps) {
  let value = raw;
  for (const step of steps) {
    if (step.type !== 'legacyMap') throw new Error(`unknown step ${step.type}`);
    value = legacyMap(value, step.inMin, step.inMax, step.outMin, step.outMax);
  }
  return value;
}

/**
 * Typed analog-input engineering observation for a bound AI row.
 * Returns raw + every Legacy output (level/percent etc.) + quality/diagnostic; rows whose
 * scaling is unresolved return engineeringValue null with scaling UNRESOLVED.
 */
export function analogEngineering(tag, rawWord, at, sequence) {
  const raw = Number(rawWord);
  const base = { tag, rawWord: raw, at, sequence, scaling: 'UNRESOLVED', engineering: Object.freeze({}), diagnostic: 'NONE' };
  if (!Number.isInteger(raw) || raw < RAW_MIN || raw > RAW_MAX) {
    return Object.freeze({ ...base, quality: 'BAD', diagnostic: 'ANALOG_RAW_OUT_OF_RANGE' });
  }
  const conv = ANALOG_CONVERSIONS[tag];
  if (!conv || conv.outputs.length === 0) {
    return Object.freeze({ ...base, quality: 'UNCERTAIN', scaling: 'UNRESOLVED', diagnostic: conv ? 'ENGINEERING_SCALING_UNRESOLVED' : 'NO_CONVERSION_EVIDENCE' });
  }
  const engineering = {};
  for (const out of conv.outputs) {
    engineering[out.name] = Object.freeze({ value: applySteps(raw, out.steps), unit: out.unit });
  }
  return Object.freeze({ ...base, quality: 'GOOD', scaling: 'LEGACY_EXACT', engineering: Object.freeze(engineering) });
}

/** Inverse of the AO conversion: engineering mA -> raw word (clamped; out-of-range refused upstream). */
export function analogOutputRaw(tag, engineeringValue) {
  const conv = ANALOG_OUTPUT_CONVERSION[tag];
  if (!conv) return { ok: false, code: 'AO_CONVERSION_UNRESOLVED' };
  const v = Number(engineeringValue);
  if (!Number.isFinite(v) || v < conv.engineeringMin || v > conv.engineeringMax) {
    return { ok: false, code: 'AO_ENGINEERING_OUT_OF_RANGE' };
  }
  return { ok: true, raw: Math.round(applySteps(v, conv.steps)) };
}

/**
 * Typed digital-input observation. Raw bit is always preserved; interpreted state uses the
 * exact Legacy runtime meaning (named-state-true). Rows without a Legacy interpretation keep
 * interpretedState UNRESOLVED and create no control effect.
 */
export function digitalObservation(tag, rawBit, at, sequence) {
  const raw = rawBit === true;
  const interpreted = DIGITAL_INTERPRETATION.default.rawTrueMeaning === 'NAMED_STATE_TRUE'
    ? (raw ? 'ACTIVE' : 'INACTIVE') : 'UNRESOLVED';
  return Object.freeze({
    tag,
    rawBit: raw,
    interpretedState: interpreted,
    polaritySource: 'LEGACY_RUNTIME_NAMED_STATE_TRUE',
    at,
    sequence,
    quality: 'GOOD',
    diagnostic: 'NONE',
  });
}
