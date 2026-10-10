// Stage 0.4B-2 — Module Profile catalogue (I/O module models 750-xxx).
//
// Each profile records only the facts that were verified from manufacturer
// datasheet excerpts, plus the facts that are structurally fixed by the
// Simulation mapping model. Anything not verified is recorded in `missing`
// and the profile status stays INCOMPLETE. No process-image offset, width or
// address is invented here. `addressRule` is null for every profile in this
// stage, so no numeric address can be derived from the catalogue alone.

import { CATEGORY, DIRECTION, CHANNEL_TYPE, SIGNAL } from './constants.mjs';
import { canonicalJson } from './canonical.mjs';

const PROFILE_SOURCES = Object.freeze({
  COUPLER_DATASHEET: 'Fieldbus coupler datasheet (4th generation): input and output process image maximum 1020 words each',
  ANALOG_INPUT_DATASHEET: 'Analog input datasheet: 4 x 16-bit data; 4 x 8-bit control/status (optional)',
  ANALOG_OUTPUT_DATASHEET: 'Analog output datasheet: 2 x 16-bit data; 2 x 8-bit control/status (optional)',
  DIGITAL_MODULE_PRODUCT_PAGE: 'Digital module product page: channel count and 24 VDC rating only; bit width not verified',
  NONE: 'No datasheet excerpt reviewed for this model in Stage 0.4B-1',
});

function profile(fields) {
  return Object.freeze({
    ...fields,
    profileStatus: 'INCOMPLETE',
    addressRule: null,
    missing: Object.freeze(fields.missing),
  });
}

const COMMON_MISSING = Object.freeze([
  'head-station process-image ordering rule',
  'alignment and word/byte packing rule',
  'per-channel offset rule',
]);

const PROFILES = Object.freeze([
  profile({
    modelNumber: '750-362',
    displayName: 'Fieldbus Coupler',
    category: CATEGORY.COUPLER,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    contributesProcessData: 'NOT_VERIFIED',
    startConstraint: 'MUST_BE_FIRST',
    endConstraint: null,
    reorderable: false,
    sourceNote: PROFILE_SOURCES.COUPLER_DATASHEET,
    missing: Object.freeze(['process-data membership of the coupler', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-601',
    displayName: 'Power Supply Module',
    category: CATEGORY.SUPPLY,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    contributesProcessData: 'NOT_VERIFIED',
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.NONE,
    missing: Object.freeze(['datasheet excerpt for this model', 'process-data membership']),
  }),
  profile({
    modelNumber: '750-430',
    displayName: '8-channel Digital Input',
    category: CATEGORY.DIGITAL_INPUT,
    direction: DIRECTION.INPUT,
    channelCount: 8,
    channelType: CHANNEL_TYPE.DIGITAL,
    supportedSignals: Object.freeze([SIGNAL.DIGITAL_24_VDC]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    contributesProcessData: 'NOT_VERIFIED',
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
    missing: Object.freeze(['bit width per channel', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-530',
    displayName: '8-channel Digital Output',
    category: CATEGORY.DIGITAL_OUTPUT,
    direction: DIRECTION.OUTPUT,
    channelCount: 8,
    channelType: CHANNEL_TYPE.DIGITAL,
    supportedSignals: Object.freeze([SIGNAL.DIGITAL_24_VDC]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    contributesProcessData: 'NOT_VERIFIED',
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
    missing: Object.freeze(['bit width per channel', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-613',
    displayName: 'System Power Supply Module',
    category: CATEGORY.SUPPLY,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    contributesProcessData: 'NOT_VERIFIED',
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.NONE,
    missing: Object.freeze(['datasheet excerpt for this model', 'process-data membership']),
  }),
  profile({
    modelNumber: '750-471',
    displayName: '4-channel Analog Input',
    category: CATEGORY.ANALOG_INPUT,
    direction: DIRECTION.INPUT,
    channelCount: 4,
    channelType: CHANNEL_TYPE.ANALOG,
    supportedSignals: Object.freeze([SIGNAL.CURRENT_4_20_MA, SIGNAL.CURRENT_0_20_MA]),
    processImage: Object.freeze({ inputBitsPerChannel: 16, outputBitsPerChannel: null, statusLayout: 'OPTIONAL_NOT_CONFIGURED' }),
    contributesProcessData: 'VERIFIED_DATA_WIDTH',
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.ANALOG_INPUT_DATASHEET,
    missing: Object.freeze(['status byte setting (optional, not configured)', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-554',
    displayName: '2-channel Analog Output',
    category: CATEGORY.ANALOG_OUTPUT,
    direction: DIRECTION.OUTPUT,
    channelCount: 2,
    channelType: CHANNEL_TYPE.ANALOG,
    supportedSignals: Object.freeze([SIGNAL.CURRENT_4_20_MA]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: 16, statusLayout: 'OPTIONAL_NOT_CONFIGURED' }),
    contributesProcessData: 'VERIFIED_DATA_WIDTH',
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.ANALOG_OUTPUT_DATASHEET,
    missing: Object.freeze(['status byte setting (optional, not configured)', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-600',
    displayName: 'End Module',
    category: CATEGORY.END,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    contributesProcessData: 'NOT_VERIFIED',
    startConstraint: null,
    endConstraint: 'MUST_BE_LAST',
    reorderable: false,
    sourceNote: PROFILE_SOURCES.NONE,
    missing: Object.freeze(['datasheet excerpt for this model', 'process-data membership']),
  }),
]);

const BY_MODEL = new Map(PROFILES.map((p) => [p.modelNumber, p]));

/** Returns the profile for a model, or null when the model is unknown. */
export function getProfile(modelNumber) {
  return BY_MODEL.get(modelNumber) ?? null;
}

export function listProfiles() {
  return PROFILES;
}

/** Canonical JSON of the whole catalogue. The ModuleProfileRevision fingerprint is built from this in revisions.mjs. */
export function profileCatalogueCanonical() {
  return canonicalJson(PROFILES);
}
