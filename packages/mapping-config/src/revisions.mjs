// Stage 0.4B-1 — deterministic revisions and fingerprints.
//
//   RackTopologyRevision        ordered ModuleInstanceId + model (order matters)
//   TagMappingRevision          bindings sorted by tagName; scaling included
//   ModuleProfileRevision       the whole Module Profile catalogue
//   DerivedAddressManifestFingerprint
//                               rack + mapping + profile revisions + derived
//                               address states (+ rule id when present)
//
// Excluded by construction: timestamps, UI selection or hover state, browser
// identifiers, object property order, and anything not listed above.

import { fingerprintOf } from './canonical.mjs';
import { listProfiles } from './moduleProfiles.mjs';
import { AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, evidenceSetFingerprint } from './processImageEvidence.mjs';

export function rackTopologyRevision(modules) {
  return fingerprintOf({
    kind: 'RackTopology',
    modules: modules.map((m) => ({ moduleInstanceId: m.moduleInstanceId, modelNumber: m.modelNumber })),
  });
}

export function tagMappingRevision(bindings) {
  const rows = bindings
    .map((b) => ({
      tagName: b.tagName,
      moduleInstanceId: b.moduleInstanceId,
      channel: b.channel,
      enabled: b.enabled !== false,
      engineering: b.engineering ?? null,
      activePolarity: b.activePolarity ?? null,
      contactType: b.contactType ?? null,
      declaredSourceIdentity: b.declaredSourceIdentity ?? null,
    }))
    .sort((a, b) => a.tagName.localeCompare(b.tagName));
  return fingerprintOf({ kind: 'TagMapping', bindings: rows });
}

export function moduleProfileRevision() {
  return fingerprintOf({ kind: 'ModuleProfiles', profiles: listProfiles() });
}

/** ProcessImageEvidenceRevision: fingerprint of the evidence content (sources, observations, notes). */
export function processImageEvidenceRevision(evidence = AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE) {
  return evidenceSetFingerprint(evidence);
}

export function derivedAddressManifestFingerprint({ rackRevision, mappingRevision, profileRevision, evidenceRevision, addresses }) {
  return fingerprintOf({
    kind: 'DerivedAddressManifest',
    rackRevision,
    mappingRevision,
    profileRevision,
    evidenceRevision,
    ruleId: addresses.ruleId,
    entries: addresses.entries.map((e) => ({
      tagName: e.tagName,
      moduleInstanceId: e.moduleInstanceId,
      channel: e.channel,
      state: e.state,
      area: e.area,
      bitOffsetAbsolute: e.bitOffsetAbsolute,
      byteOffset: e.byteOffset,
      wordOffset: e.wordOffset,
      bitIndex: e.bitIndex,
      wordCount: e.wordCount,
      channelOffset: e.channelOffset,
      bitWidth: e.bitWidth,
      processImageOrder: e.processImageOrder,
      reasons: [...e.reasons],
    })),
  });
}

/** Short display form of a full digest. Display only; never used for comparison. */
export function shortRevision(digest) {
  return digest.slice(0, 12);
}
