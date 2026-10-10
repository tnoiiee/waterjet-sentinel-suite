// Stage 0.4B-2 — address-derivation engine (profile driven).
//
// Canonical addresses use ONE convention: ZERO-BASED. Display notation is derived from the canonical value and is
// never stored or accepted as input. Addresses are read-only and are never typed by a user.
//
// The engine takes an evidence set (processImageEvidence.mjs). With the authoritative evidence set, which holds no
// verified process-image evidence, every enabled binding is ADDRESS_UNRESOLVED with explicit reasons. No offset,
// word or bit number is produced, and none is guessed.
//
// A numeric address appears only when the head-station rule, the module process-data profile, the status-byte
// setting and the byte and word order of the affected module are all verified, and the layout is computable.
// Tests may supply a clearly labelled SYNTHETIC TEST RULE evidence set. Its entries are marked synthetic, the
// result is never `verified`, and it can never make a configuration activation-ready.
//
// ProcessModulePosition is never read here. Order comes from the verified head-station mapping order.

import { ADDRESS_STATE, CHANNEL_TYPE, DIRECTION, REASON } from './constants.mjs';
import { getProfile } from './moduleProfiles.mjs';
import { AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, ADDRESS_ENTRY_STATE, resolveProcessImage } from './processImageEvidence.mjs';

function entryBase(binding, module, profile) {
  return {
    tagName: binding.tagName,
    moduleInstanceId: binding.moduleInstanceId,
    channel: binding.channel,
    area: profile && profile.direction !== DIRECTION.NONE ? profile.direction : null,
    bitOffsetAbsolute: null,
    byteOffset: null,
    wordOffset: null,
    bitIndex: null,
    wordCount: null,
    channelOffset: null,
    displayNotation: null,
    rackSlot: module ? module.rackSlot : null,
  };
}

function unresolved(binding, module, profile, reasons) {
  return Object.freeze({
    ...entryBase(binding, module, profile),
    state: ADDRESS_STATE.UNRESOLVED,
    reasons: Object.freeze([...new Set(reasons)].sort()),
  });
}

/**
 * Derives addresses for the enabled bindings.
 *   rackView   ordered output of deriveRackView (carries rackSlot per module)
 *   bindings   mapping bindings
 *   evidence   an evidence set; null means the authoritative set (no verified evidence)
 */
export function deriveAddresses(rackView, bindings, evidence = null) {
  const set = evidence ?? AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE;
  const image = resolveProcessImage(set, rackView);
  const slotOf = new Map(rackView.map((r) => [r.moduleInstanceId, r]));
  const entries = new Map();

  for (const b of bindings) {
    if (b.enabled === false) {
      entries.set(b.tagName, Object.freeze({
        ...entryBase(b, slotOf.get(b.moduleInstanceId), null),
        state: 'NOT_ACTIVE', area: null, reasons: Object.freeze([]),
      }));
    }
  }

  for (const b of bindings.filter((x) => x.enabled !== false)) {
    const module = slotOf.get(b.moduleInstanceId);
    const profile = module ? getProfile(module.modelNumber) : null;
    const m = module ? image.byInstance.get(module.moduleInstanceId) : null;
    if (!m || !m.channelModule || !Number.isInteger(b.channel) || b.channel < 1 || b.channel > profile.channelCount) {
      entries.set(b.tagName, unresolved(b, module, profile, [
        ...image.head.reasons, REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED,
      ]));
      continue;
    }
    if (m.address.state !== ADDRESS_ENTRY_STATE.DERIVED) {
      entries.set(b.tagName, unresolved(b, module, profile, m.address.reasons));
      continue;
    }
    const wordBits = image.wordBits;
    const analog = profile.channelType === CHANNEL_TYPE.ANALOG;
    const abs = m.address.startBit + (b.channel - 1) * m.channelDataBits;
    const wordOffset = Math.floor(abs / wordBits);
    const bitIndex = analog ? null : abs % wordBits;
    entries.set(b.tagName, Object.freeze({
      ...entryBase(b, module, profile),
      state: ADDRESS_STATE.DERIVED,
      area: profile.direction,
      reasons: Object.freeze([]),
      bitOffsetAbsolute: abs,
      byteOffset: Math.floor(abs / 8),
      wordOffset,
      bitIndex,
      wordCount: Math.max(1, Math.ceil(m.channelDataBits / wordBits)),
      channelOffset: b.channel - 1,
      displayNotation: bitIndex === null ? `${profile.direction}-W${wordOffset}` : `${profile.direction}-W${wordOffset}.${bitIndex}`,
      processImageOrder: m.processImageOrder.value,
      byteOrder: m.byteOrder,
      wordOrder: m.wordOrder,
      ruleId: set.evidenceSetId,
      synthetic: image.synthetic,
    }));
  }

  // Overlap is an internal invariant: a derived layout must never place two channels on the same bit.
  const occupied = new Set();
  for (const e of entries.values()) {
    if (e.state !== ADDRESS_STATE.DERIVED) continue;
    const width = e.bitIndex === null ? e.wordCount * image.wordBits : 1;
    for (let k = 0; k < width; k += 1) {
      const key = `${e.area}:${e.bitOffsetAbsolute + k}`;
      if (occupied.has(key)) throw new Error(`internal: overlapping process-image span at ${key}`);
      occupied.add(key);
    }
  }

  const list = [...entries.values()].sort((a, b) => a.tagName.localeCompare(b.tagName));
  const ruleId = image.head.verified ? set.evidenceSetId : null;
  return Object.freeze({
    ruleId,
    verified: ruleId !== null && !image.synthetic,
    synthetic: image.synthetic,
    entries: Object.freeze(list),
  });
}

export function isDerived(entry) {
  return entry.state === ADDRESS_STATE.DERIVED;
}
