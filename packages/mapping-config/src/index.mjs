// Stage 0.4B-3 — browser-safe public entry point of the simulation-only Mapping Configuration package.
// Contains NO Node built-in imports. The Excel importer (Node only) is exported from nodeImport.mjs.
export * from './constants.mjs';
export { getProfile, listProfiles, profileCatalogueCanonical, isNonProcessDataModel } from './moduleProfiles.mjs';
export { buildModuleInstances, deriveRackView, validateRack, moveModule } from './rack.mjs';
export { listSimulationTags, getTagDef, REQUIRED_TAG_NAMES, pressureTagForWj } from './tagCatalogue.mjs';
export { validateMapping, summariseIssues } from './mappingValidation.mjs';
export { deriveAddresses } from './addressDerivation.mjs';
export { canonicalJson, fingerprintOf } from './canonical.mjs';
export {
  rackTopologyRevision, tagMappingRevision, moduleProfileRevision, processImageEvidenceRevision,
  derivedAddressManifestFingerprint, shortRevision,
} from './revisions.mjs';
export { createConfiguration, DraftSession } from './draftSession.mjs';
export { activeProvider, selectProvider, REFUSED_PROVIDER_NAMES } from './providerPolicy.mjs';
export { syntheticExampleConfiguration } from './syntheticExample.mjs';
export { normalizeLimit } from './limitNormalization.mjs';
export {
  AUTHORITATIVE_PROCESS_IMAGE_EVIDENCE, createEvidenceSet, validateEvidenceSet, evidenceSetFingerprint,
  resolveProcessImage, requiredModuleCells, HEAD_CELLS, MODULE_CELLS, GROUPING, MAPPING_ORDER,
  STATUS_BEHAVIOR, STATUS_PLACEMENT, BYTE_ORDER, WORD_ORDER, SUPPORTED_GROUPING, ADDRESS_ENTRY_STATE,
} from './processImageEvidence.mjs';
export { buildEvidenceReport } from './evidenceReport.mjs';
export {
  AUTHORITATIVE_RACK_TOPOLOGY, AUTHORITATIVE_RACK_MODELS, AUTHORITATIVE_WORKBOOK_SHA256, ACTUAL_RACK_SOURCE,
  INSTANCE_CHANNEL_EVIDENCE, MISSING_ACTUAL_RACK_EVIDENCE, UNVERIFIED_UNTIL_SUPPLIED, authoritativeRackInstances,
  matchesAuthoritativeRack, actualRackEvidenceFor, channelEvidenceFor, actualRackEvidenceFingerprint,
  topologyCanonical,
} from './actualRackEvidence.mjs';
export { buildCandidateProcessImage, OFFSET_STATE } from './candidateProcessImage.mjs';
export {
  DENSITY, DEFAULT_DENSITY, TAG_FILTERS, GROUP_ORDER, VALIDATION_GROUPS, buildTagRows, filterRows, filterCounts,
  groupRows, compactAddressSummary, issueStatusText, placeholderSummary, groupValidation, equipmentGroupOf,
} from './presentation.mjs';
