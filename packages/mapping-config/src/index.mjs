// Stage 0.4B-1 — browser-safe public entry point of the simulation-only Mapping Configuration package.
// Contains NO Node built-in imports. The Excel importer (Node only) is exported from nodeImport.mjs.
export * from './constants.mjs';
export { getProfile, listProfiles, profileCatalogueCanonical } from './moduleProfiles.mjs';
export { buildModuleInstances, deriveRackView, validateRack, moveModule } from './rack.mjs';
export { listSimulationTags, getTagDef, REQUIRED_TAG_NAMES, pressureTagForWj } from './tagCatalogue.mjs';
export { validateMapping, summariseIssues } from './mappingValidation.mjs';
export { deriveAddresses } from './addressDerivation.mjs';
export { canonicalJson, fingerprintOf } from './canonical.mjs';
export {
  rackTopologyRevision, tagMappingRevision, derivedAddressManifestFingerprint, shortRevision,
} from './revisions.mjs';
export { createConfiguration, DraftSession } from './draftSession.mjs';
export { activeProvider, selectProvider, REFUSED_PROVIDER_NAMES } from './providerPolicy.mjs';
export { syntheticExampleConfiguration } from './syntheticExample.mjs';
export { normalizeLimit } from './limitNormalization.mjs';
