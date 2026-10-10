// Stage 0.4B-1 — address-derivation engine.
//
// Canonical addresses use ONE convention: ZERO-BASED. Display notation is
// derived from the canonical value and is never stored or accepted as input.
//
// With no verified process-image rule (the state of the shipped Module Profile
// catalogue in this stage), every enabled binding is ADDRESS_UNRESOLVED with
// reasons. No offset, word or bit number is produced.
//
// A rule object may be supplied only by a verified source. Tests use a clearly
// labelled SYNTHETIC rule to check the arithmetic: analog data first, in rack
// order, one word per channel; digital bits packed after the analog words, 16
// bits per word. This rule is NOT a vendor specification and is never used to
// report a real address.

import { ADDRESS_STATE, CHANNEL_TYPE, DIRECTION, REASON } from './constants.mjs';
import { getProfile } from './moduleProfiles.mjs';

const WORD_BITS = 16;

function unresolved(binding, module, profile, reasons) {
  return Object.freeze({
    tagName: binding.tagName,
    moduleInstanceId: binding.moduleInstanceId,
    channel: binding.channel,
    state: ADDRESS_STATE.UNRESOLVED,
    area: profile && profile.direction !== DIRECTION.NONE ? profile.direction : null,
    reasons: Object.freeze([...reasons]),
    bitOffsetAbsolute: null,
    byteOffset: null,
    wordOffset: null,
    bitIndex: null,
    wordCount: null,
    channelOffset: null,
    displayNotation: null,
    rackSlot: module ? module.rackSlot : null,
  });
}

/**
 * Derives addresses for the enabled bindings.
 *   rackView   ordered output of deriveRackView (carries rackSlot per module)
 *   bindings   mapping bindings
 *   rules      null (no verified rule) or a rule object (see header)
 */
export function deriveAddresses(rackView, bindings, rules = null) {
  const slotOf = new Map(rackView.map((r) => [r.moduleInstanceId, r]));
  const entries = new Map();

  const enabled = bindings.filter((b) => b.enabled !== false);
  for (const b of bindings) {
    if (b.enabled === false) {
      entries.set(b.tagName, Object.freeze({
        tagName: b.tagName, moduleInstanceId: b.moduleInstanceId, channel: b.channel,
        state: 'NOT_ACTIVE', area: null, reasons: Object.freeze([]), bitOffsetAbsolute: null,
        byteOffset: null, wordOffset: null, bitIndex: null, wordCount: null, channelOffset: null,
        displayNotation: null, rackSlot: slotOf.get(b.moduleInstanceId)?.rackSlot ?? null,
      }));
    }
  }

  if (!rules) {
    for (const b of enabled) {
      const module = slotOf.get(b.moduleInstanceId);
      const profile = module ? getProfile(module.modelNumber) : null;
      const reasons = [REASON.HEAD_STATION_PROFILE_NOT_VERIFIED, REASON.NO_VERIFIED_PROCESS_IMAGE_RULE];
      if (profile) {
        reasons.push(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED);
        if (profile.channelType === CHANNEL_TYPE.ANALOG) reasons.push(REASON.MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED);
      }
      entries.set(b.tagName, unresolved(b, module, profile, reasons));
    }
    return finish(entries, null);
  }

  // ----- rule-driven arithmetic (verified rules only) -----
  const placed = new Map();
  for (const area of [DIRECTION.INPUT, DIRECTION.OUTPUT]) {
    const members = rackView.filter((r) => {
      const p = getProfile(r.modelNumber);
      return p && p.direction === area && p.channelCount > 0 && rules.moduleBits[r.modelNumber];
    });
    const maxWords = rules.maxWordsByArea[area];

    let wordCursor = 0;
    const analogSpans = [];
    for (const m of members) {
      const bits = rules.moduleBits[m.modelNumber];
      if (bits.kind !== CHANNEL_TYPE.ANALOG) continue;
      const p = getProfile(m.modelNumber);
      for (let ch = 1; ch <= p.channelCount; ch += 1) {
        analogSpans.push({ m, ch, wordIndex: wordCursor });
        wordCursor += 1;
      }
    }
    let bitCursor = wordCursor * WORD_BITS;
    const digitalSpans = [];
    for (const m of members) {
      const bits = rules.moduleBits[m.modelNumber];
      if (bits.kind !== CHANNEL_TYPE.DIGITAL) continue;
      const p = getProfile(m.modelNumber);
      for (let ch = 1; ch <= p.channelCount; ch += 1) {
        digitalSpans.push({ m, ch, bit: bitCursor });
        bitCursor += 1;
      }
    }
    const totalWords = Math.ceil(bitCursor / WORD_BITS);
    const exceeded = totalWords > maxWords;

    const occupied = new Set();
    const place = (span, kind) => {
      const b = enabled.find((x) => x.moduleInstanceId === span.m.moduleInstanceId && x.channel === span.ch);
      if (!b) return;
      const module = slotOf.get(span.m.moduleInstanceId);
      const profile = getProfile(span.m.modelNumber);
      if (exceeded) {
        entries.set(b.tagName, unresolved(b, module, profile, [REASON.PROCESS_IMAGE_SIZE_EXCEEDED]));
        return;
      }
      const abs = kind === 'analog' ? span.wordIndex * WORD_BITS : span.bit;
      for (let k = 0; k < (kind === 'analog' ? WORD_BITS : 1); k += 1) {
        if (occupied.has(abs + k)) throw new Error(`internal: overlapping process-image span at bit ${abs + k}`);
        occupied.add(abs + k);
      }
      const wordOffset = Math.floor(abs / WORD_BITS);
      const bitIndex = kind === 'analog' ? null : abs % WORD_BITS;
      const e = Object.freeze({
        tagName: b.tagName, moduleInstanceId: b.moduleInstanceId, channel: b.channel,
        state: ADDRESS_STATE.DERIVED, area, reasons: Object.freeze([]),
        bitOffsetAbsolute: abs, byteOffset: Math.floor(abs / 8), wordOffset, bitIndex,
        wordCount: 1, channelOffset: span.ch - 1, rackSlot: module.rackSlot,
        displayNotation: bitIndex === null ? `${area}-W${wordOffset}` : `${area}-W${wordOffset}.${bitIndex}`,
        ruleId: rules.ruleId,
      });
      entries.set(b.tagName, e);
      placed.set(b.tagName, e);
    };
    analogSpans.forEach((s) => place(s, 'analog'));
    digitalSpans.forEach((s) => place(s, 'digital'));
  }

  for (const b of enabled) {
    if (!entries.has(b.tagName)) {
      const module = slotOf.get(b.moduleInstanceId);
      const profile = module ? getProfile(module.modelNumber) : null;
      entries.set(b.tagName, unresolved(b, module, profile, [REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED]));
    }
  }
  return finish(entries, rules.ruleId);
}

function finish(entries, ruleId) {
  const list = [...entries.values()].sort((a, b) => a.tagName.localeCompare(b.tagName));
  return Object.freeze({ ruleId, verified: ruleId !== null, entries: Object.freeze(list) });
}

export function isDerived(entry) {
  return entry.state === ADDRESS_STATE.DERIVED;
}
