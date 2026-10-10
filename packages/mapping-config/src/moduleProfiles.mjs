// Stage 0.4B-2 — Module Profile catalogue (I/O module models 750-xxx).
//
// Each profile records only the facts that were verified from manufacturer
// datasheet excerpts, plus the facts that are structurally fixed by the
// Simulation mapping model. Anything not verified is recorded in `missing`
// and `unresolvedReasons`, and the profile status stays INCOMPLETE. No
// process-image offset, width or address is invented here. `addressRule` is
// null for every profile in this stage, so no numeric address can be derived
// from the catalogue alone.

import { CATEGORY, DIRECTION, CHANNEL_TYPE, SIGNAL, EVIDENCE_STATE } from './constants.mjs';
import { canonicalJson } from './canonical.mjs';
import { createEvidenceRecord, createHeadStationEvidence } from './evidenceProfile.mjs';

const PROFILE_SOURCES = Object.freeze({
  COUPLER_DATASHEET: 'Fieldbus coupler datasheet (4th generation): input and output process image maximum 1020 words each',
  ANALOG_INPUT_DATASHEET: 'Analog input datasheet: 4 x 16-bit data; 4 x 8-bit control/status (optional)',
  ANALOG_OUTPUT_DATASHEET: 'Analog output datasheet: 2 x 16-bit data; 2 x 8-bit control/status (optional)',
  DIGITAL_MODULE_PRODUCT_PAGE: 'Digital module product page: channel count and 24 VDC rating only; bit width not verified',
  NONE: 'No datasheet excerpt reviewed for this model in Stage 0.4B',
});

const COMMON_MISSING = Object.freeze([
  'head-station process-image ordering rule',
  'alignment and word/byte packing rule',
  'per-channel offset rule',
]);

function buildProfiles() {
  return Object.freeze([
    Object.freeze({
      modelNumber: '750-362',
      displayName: 'Fieldbus Coupler',
      manufacturer: 'WAGO',
      category: CATEGORY.COUPLER,
      direction: DIRECTION.NONE,
      channelCount: 0,
      channelType: null,
      supportedSignals: Object.freeze([]),
      processImage: Object.freeze({
        inputBitsPerChannel: null,
        outputBitsPerChannel: null,
        statusLayout: 'NOT_VERIFIED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'NOT_VERIFIED',
      startConstraint: 'MUST_BE_FIRST',
      endConstraint: null,
      reorderable: false,
      sourceNote: PROFILE_SOURCES.COUPLER_DATASHEET,
      missing: Object.freeze(['process-data membership of the coupler', ...COMMON_MISSING]),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createHeadStationEvidence({
        model: '750-362',
        sourceDocumentIdentity: 'Fieldbus Coupler 750-362 Datasheet Excerpt',
        evidenceType: 'COUPLER_DATASHEET_EXCERPT',
        verificationState: EVIDENCE_STATE.PROVIDED_UNVERIFIED,
        evidenceNote: PROFILE_SOURCES.COUPLER_DATASHEET,
      }),
    }),
    Object.freeze({
      modelNumber: '750-601',
      displayName: 'Power Supply Module',
      manufacturer: 'WAGO',
      category: CATEGORY.SUPPLY,
      direction: DIRECTION.NONE,
      channelCount: 0,
      channelType: null,
      supportedSignals: Object.freeze([]),
      processImage: Object.freeze({
        inputBitsPerChannel: null,
        outputBitsPerChannel: null,
        statusLayout: 'NOT_VERIFIED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'NOT_VERIFIED',
      startConstraint: null,
      endConstraint: null,
      reorderable: true,
      sourceNote: PROFILE_SOURCES.NONE,
      missing: Object.freeze(['datasheet excerpt for this model', 'process-data membership']),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createEvidenceRecord({
        model: '750-601',
        manufacturer: 'WAGO',
        verificationState: EVIDENCE_STATE.NOT_PROVIDED,
        evidenceNote: PROFILE_SOURCES.NONE,
        unresolvedReasons: ['datasheet excerpt for this model', 'process-data membership'],
      }),
    }),
    Object.freeze({
      modelNumber: '750-430',
      displayName: '8-channel Digital Input',
      manufacturer: 'WAGO',
      category: CATEGORY.DIGITAL_INPUT,
      direction: DIRECTION.INPUT,
      channelCount: 8,
      channelType: CHANNEL_TYPE.DIGITAL,
      supportedSignals: Object.freeze([SIGNAL.DIGITAL_24_VDC]),
      processImage: Object.freeze({
        inputBitsPerChannel: null,
        outputBitsPerChannel: null,
        statusLayout: 'NOT_VERIFIED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'NOT_VERIFIED',
      startConstraint: null,
      endConstraint: null,
      reorderable: true,
      sourceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
      missing: Object.freeze(['bit width per channel', ...COMMON_MISSING]),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createEvidenceRecord({
        model: '750-430',
        manufacturer: 'WAGO',
        evidenceType: 'PRODUCT_PAGE_EXCERPT',
        verificationState: EVIDENCE_STATE.PROVIDED_UNVERIFIED,
        evidenceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
        unresolvedReasons: ['bit width per channel', ...COMMON_MISSING],
      }),
    }),
    Object.freeze({
      modelNumber: '750-530',
      displayName: '8-channel Digital Output',
      manufacturer: 'WAGO',
      category: CATEGORY.DIGITAL_OUTPUT,
      direction: DIRECTION.OUTPUT,
      channelCount: 8,
      channelType: CHANNEL_TYPE.DIGITAL,
      supportedSignals: Object.freeze([SIGNAL.DIGITAL_24_VDC]),
      processImage: Object.freeze({
        inputBitsPerChannel: null,
        outputBitsPerChannel: null,
        statusLayout: 'NOT_VERIFIED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'NOT_VERIFIED',
      startConstraint: null,
      endConstraint: null,
      reorderable: true,
      sourceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
      missing: Object.freeze(['bit width per channel', ...COMMON_MISSING]),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createEvidenceRecord({
        model: '750-530',
        manufacturer: 'WAGO',
        evidenceType: 'PRODUCT_PAGE_EXCERPT',
        verificationState: EVIDENCE_STATE.PROVIDED_UNVERIFIED,
        evidenceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
        unresolvedReasons: ['bit width per channel', ...COMMON_MISSING],
      }),
    }),
    Object.freeze({
      modelNumber: '750-613',
      displayName: 'System Power Supply Module',
      manufacturer: 'WAGO',
      category: CATEGORY.SUPPLY,
      direction: DIRECTION.NONE,
      channelCount: 0,
      channelType: null,
      supportedSignals: Object.freeze([]),
      processImage: Object.freeze({
        inputBitsPerChannel: null,
        outputBitsPerChannel: null,
        statusLayout: 'NOT_VERIFIED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'NOT_VERIFIED',
      startConstraint: null,
      endConstraint: null,
      reorderable: true,
      sourceNote: PROFILE_SOURCES.NONE,
      missing: Object.freeze(['datasheet excerpt for this model', 'process-data membership']),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createEvidenceRecord({
        model: '750-613',
        manufacturer: 'WAGO',
        verificationState: EVIDENCE_STATE.NOT_PROVIDED,
        evidenceNote: PROFILE_SOURCES.NONE,
        unresolvedReasons: ['datasheet excerpt for this model', 'process-data membership'],
      }),
    }),
    Object.freeze({
      modelNumber: '750-471',
      displayName: '4-channel Analog Input',
      manufacturer: 'WAGO',
      category: CATEGORY.ANALOG_INPUT,
      direction: DIRECTION.INPUT,
      channelCount: 4,
      channelType: CHANNEL_TYPE.ANALOG,
      supportedSignals: Object.freeze([SIGNAL.CURRENT_4_20_MA, SIGNAL.CURRENT_0_20_MA]),
      processImage: Object.freeze({
        inputBitsPerChannel: 16,
        outputBitsPerChannel: null,
        statusLayout: 'OPTIONAL_NOT_CONFIGURED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'VERIFIED_DATA_WIDTH',
      startConstraint: null,
      endConstraint: null,
      reorderable: true,
      sourceNote: PROFILE_SOURCES.ANALOG_INPUT_DATASHEET,
      missing: Object.freeze(['status byte setting (optional, not configured)', ...COMMON_MISSING]),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createEvidenceRecord({
        model: '750-471',
        manufacturer: 'WAGO',
        evidenceType: 'DATASHEET_EXCERPT',
        processInputWidth: 64,
        statusByteBehavior: 'OPTIONAL_NOT_CONFIGURED',
        verificationState: EVIDENCE_STATE.PROVIDED_UNVERIFIED,
        evidenceNote: PROFILE_SOURCES.ANALOG_INPUT_DATASHEET,
        unresolvedReasons: ['status byte setting (optional, not configured)', ...COMMON_MISSING],
      }),
    }),
    Object.freeze({
      modelNumber: '750-554',
      displayName: '2-channel Analog Output',
      manufacturer: 'WAGO',
      category: CATEGORY.ANALOG_OUTPUT,
      direction: DIRECTION.OUTPUT,
      channelCount: 2,
      channelType: CHANNEL_TYPE.ANALOG,
      supportedSignals: Object.freeze([SIGNAL.CURRENT_4_20_MA]),
      processImage: Object.freeze({
        inputBitsPerChannel: null,
        outputBitsPerChannel: 16,
        statusLayout: 'OPTIONAL_NOT_CONFIGURED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'VERIFIED_DATA_WIDTH',
      startConstraint: null,
      endConstraint: null,
      reorderable: true,
      sourceNote: PROFILE_SOURCES.ANALOG_OUTPUT_DATASHEET,
      missing: Object.freeze(['status byte setting (optional, not configured)', ...COMMON_MISSING]),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createEvidenceRecord({
        model: '750-554',
        manufacturer: 'WAGO',
        evidenceType: 'DATASHEET_EXCERPT',
        processOutputWidth: 32,
        statusByteBehavior: 'OPTIONAL_NOT_CONFIGURED',
        verificationState: EVIDENCE_STATE.PROVIDED_UNVERIFIED,
        evidenceNote: PROFILE_SOURCES.ANALOG_OUTPUT_DATASHEET,
        unresolvedReasons: ['status byte setting (optional, not configured)', ...COMMON_MISSING],
      }),
    }),
    Object.freeze({
      modelNumber: '750-600',
      displayName: 'End Module',
      manufacturer: 'WAGO',
      category: CATEGORY.END,
      direction: DIRECTION.NONE,
      channelCount: 0,
      channelType: null,
      supportedSignals: Object.freeze([]),
      processImage: Object.freeze({
        inputBitsPerChannel: null,
        outputBitsPerChannel: null,
        statusLayout: 'NOT_VERIFIED',
        fillerBits: 0,
        diagnosticBytes: 0,
      }),
      contributesProcessData: 'NOT_VERIFIED',
      startConstraint: null,
      endConstraint: 'MUST_BE_LAST',
      reorderable: false,
      sourceNote: PROFILE_SOURCES.NONE,
      missing: Object.freeze(['datasheet excerpt for this model', 'process-data membership']),
      profileStatus: 'INCOMPLETE',
      addressRule: null,
      evidence: createEvidenceRecord({
        model: '750-600',
        manufacturer: 'WAGO',
        verificationState: EVIDENCE_STATE.NOT_PROVIDED,
        evidenceNote: PROFILE_SOURCES.NONE,
        unresolvedReasons: ['datasheet excerpt for this model', 'process-data membership'],
      }),
    }),
  ]);
}

let activeProfiles = buildProfiles();
let byModel = new Map(activeProfiles.map((p) => [p.modelNumber, p]));

/** Returns the profile for a model, or null when the model is unknown. */
export function getProfile(modelNumber) {
  return byModel.get(modelNumber) ?? null;
}

export function listProfiles() {
  return activeProfiles;
}

/** Canonical JSON of the whole catalogue. The ModuleProfileRevision fingerprint is built from this in revisions.mjs. */
export function profileCatalogueCanonical() {
  return canonicalJson(activeProfiles);
}
