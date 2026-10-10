// Stage 0.4B-3 — Module Profile catalogue (I/O module models 750-xxx).
//
// A profile is MODEL-LEVEL. It records the role of a model, what it contributes to the Application Process Image,
// the facts documented in the Owner-supplied primary manuals, and what is still unverified. It never records a
// physical RackSlot, a configuration-tool position, a ProcessModulePosition, a ProcessImageOrder or an address:
// those are rack-level or instance-level facts and live elsewhere.
//
// Two kinds of fact are kept strictly apart:
//   manualFacts        model-level facts from the primary product manuals (Owner-supplied, outside the repository,
//                      therefore PROVIDED_UNVERIFIED and never hashed here). A manual capability is NOT an
//                      actual-rack setting and is never promoted to one.
//   missing            what is still unverified for the process image. Empty for a model that contributes no
//                      process data: such a model is not waiting for anything.
//
// `addressRule` is null for every profile, so no numeric address can be derived from the catalogue alone.

import {
  CATEGORY, CHANNEL_TYPE, DIRECTION, EVIDENCE_AVAILABILITY, EVIDENCE_STATE, MODULE_ROLE,
  PROCESS_DATA_CONTRIBUTION, REASON, SIGNAL, ADDRESS_STATE,
} from './constants.mjs';
import { canonicalJson } from './canonical.mjs';

const PROFILE_SOURCES = Object.freeze({
  COUPLER_DATASHEET: 'Fieldbus coupler datasheet (4th generation): input and output process image maximum 1020 words each',
  ANALOG_INPUT_DATASHEET: 'Analog input datasheet: 4 x 16-bit data; 4 x 8-bit control/status (optional)',
  ANALOG_OUTPUT_DATASHEET: 'Analog output datasheet: 2 x 16-bit data; 2 x 8-bit control/status (optional)',
  DIGITAL_MODULE_PRODUCT_PAGE: 'Digital module product page: channel count and 24 VDC rating only; bit width not verified',
  NONE: 'No primary manufacturer process-image evidence has been reviewed for this model.',
});

// Owner ruling (Stage 0.4B-3): a Power Supply, a System Power Supply and the End Module are not Process I/O
// modules. They contribute no bit and no word, they receive no ProcessModulePosition and no ProcessImageOrder, and
// their address status is NOT_APPLICABLE with reason NON_PROCESS_DATA_MODULE. They are not waiting for an address.
const NON_PROCESS_DATA = Object.freeze({
  contribution: PROCESS_DATA_CONTRIBUTION.NONE,
  inputWidthBits: 0,
  outputWidthBits: 0,
  addressStatus: ADDRESS_STATE.NOT_APPLICABLE,
  addressReason: REASON.NON_PROCESS_DATA_MODULE,
});

/**
 * Model-level process-data shape of a channel module. The widths stay null: the manual states a per-channel data
 * width, but the process-image width of an installed module also depends on Coupler and module settings that are
 * not verified in this Stage, so the model profile does not assert a rack width.
 */
function processData(contribution) {
  return Object.freeze({
    contribution,
    inputWidthBits: null,
    outputWidthBits: null,
    addressStatus: ADDRESS_STATE.UNRESOLVED,
    addressReason: null,
  });
}

/**
 * A primary-manual fact record. The manuals were supplied by the Owner and are NOT committed to this repository, so
 * no SHA-256 is recorded here (an unavailable file is never hashed and a hash is never invented) and the record is
 * PROVIDED_UNVERIFIED. `scope` states what the fact is allowed to describe: a model, never a Channel of an
 * installed instance.
 */
function manualFacts(modelNumber, facts, note) {
  return Object.freeze({
    sourceId: `OWNER-PRIMARY-MANUAL-${modelNumber}`,
    availability: EVIDENCE_AVAILABILITY.OWNER_PROVIDED_OUTSIDE_REPOSITORY,
    documentTitle: `Primary product manual ${modelNumber} (Owner-supplied; not committed to the repository)`,
    documentSha256: null,
    evidenceState: EVIDENCE_STATE.PROVIDED_UNVERIFIED,
    scope: 'MODEL_LEVEL_ONLY',
    facts: Object.freeze(facts),
    note,
  });
}

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
    role: MODULE_ROLE.FIELDBUS_COUPLER,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    processData: NON_PROCESS_DATA,
    startConstraint: 'MUST_BE_FIRST',
    endConstraint: null,
    reorderable: false,
    sourceNote: PROFILE_SOURCES.COUPLER_DATASHEET,
    manualFacts: manualFacts('750-362', {
      isHeadStation: true,
      channelCount: 0,
      processInputBits: 0,
      processOutputBits: 0,
      definesNodeAndProcessImageRules: true,
      serviceRegistersOutsideApplicationProcessImage: true,
    }, 'Head station. It defines the node and the process-image rules; it is not a channel I/O module, it receives '
      + 'no ProcessModulePosition and no normal channel address, and it is not counted as an input or output module. '
      + 'Management or diagnostic service registers, if any, sit outside the Application I/O process image and are '
      + 'outside this Stage; they are never modelled as Mapping Configuration I/O addresses.'),
    missing: Object.freeze([
      'exact firmware revision of the installed head station',
      'exact hardware revision of the installed head station',
      'I/O Config of the installed head station',
      'actual Process Data image and field-network mapping',
      ...COMMON_MISSING,
    ]),
  }),
  profile({
    modelNumber: '750-601',
    displayName: 'Power Supply Module',
    category: CATEGORY.SUPPLY,
    role: MODULE_ROLE.POWER_SUPPLY,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    processData: NON_PROCESS_DATA,
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.NONE,
    manualFacts: manualFacts('750-601', {
      channelCount: 0,
      processInputBits: 0,
      processOutputBits: 0,
      applicationProcessInput: false,
      applicationProcessOutput: false,
    }, 'Power Supply. Not a Process I/O module: no Application Process Input, no Application Process Output, no '
      + 'ProcessModulePosition, no ProcessImageOrder, no Input address and no Output address.'),
    // Nothing is pending for this model: there is no process-data cell that could become verified.
    missing: Object.freeze([]),
  }),
  profile({
    modelNumber: '750-430',
    displayName: '8-channel Digital Input',
    category: CATEGORY.DIGITAL_INPUT,
    role: MODULE_ROLE.DIGITAL_INPUT_MODULE,
    direction: DIRECTION.INPUT,
    channelCount: 8,
    channelType: CHANNEL_TYPE.DIGITAL,
    supportedSignals: Object.freeze([SIGNAL.DIGITAL_24_VDC]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    processData: processData(PROCESS_DATA_CONTRIBUTION.INPUT),
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
    manualFacts: manualFacts('750-430', {
      channelCount: 8,
      bitsPerChannel: 1,
      packing: 'BIT_PACKED',
      channelBitMap: 'DI1..DI8 map to bits 0..7',
      direction: DIRECTION.INPUT,
      processInputBits: 8,
      processOutputBits: 0,
    }, 'Eight digital input channels, bit-packed, one input bit per channel, input direction only. Manual capability '
      + 'only: it is not an actual-rack setting and it does not place the module in the process image.'),
    missing: Object.freeze(['bit width per channel', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-530',
    displayName: '8-channel Digital Output',
    category: CATEGORY.DIGITAL_OUTPUT,
    role: MODULE_ROLE.DIGITAL_OUTPUT_MODULE,
    direction: DIRECTION.OUTPUT,
    channelCount: 8,
    channelType: CHANNEL_TYPE.DIGITAL,
    supportedSignals: Object.freeze([SIGNAL.DIGITAL_24_VDC]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    processData: processData(PROCESS_DATA_CONTRIBUTION.OUTPUT),
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.DIGITAL_MODULE_PRODUCT_PAGE,
    manualFacts: manualFacts('750-530', {
      channelCount: 8,
      bitsPerChannel: 1,
      packing: 'BIT_PACKED',
      channelBitMap: 'DO1..DO8 map to bits 0..7',
      direction: DIRECTION.OUTPUT,
      processInputBits: 0,
      processOutputBits: 8,
      writeAuthority: false,
    }, 'Eight digital output channels, bit-packed, one output bit per channel, output direction only. An output '
      + 'process-image reservation creates no write authority: writes remain prohibited in this Stage.'),
    missing: Object.freeze(['bit width per channel', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-613',
    displayName: 'System Power Supply Module',
    category: CATEGORY.SUPPLY,
    role: MODULE_ROLE.SYSTEM_POWER_SUPPLY,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    processData: NON_PROCESS_DATA,
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.NONE,
    manualFacts: manualFacts('750-613', {
      channelCount: 0,
      processInputBits: 0,
      processOutputBits: 0,
      applicationProcessInput: false,
      applicationProcessOutput: false,
    }, 'System Power Supply. Not a Process I/O module: no Application Process Input, no Application Process Output, '
      + 'no ProcessModulePosition, no ProcessImageOrder, no Input address and no Output address.'),
    missing: Object.freeze([]),
  }),
  profile({
    modelNumber: '750-471',
    displayName: '4-channel Analog Input',
    category: CATEGORY.ANALOG_INPUT,
    role: MODULE_ROLE.ANALOG_INPUT_MODULE,
    direction: DIRECTION.INPUT,
    channelCount: 4,
    channelType: CHANNEL_TYPE.ANALOG,
    supportedSignals: Object.freeze([SIGNAL.CURRENT_4_20_MA, SIGNAL.CURRENT_0_20_MA]),
    processImage: Object.freeze({ inputBitsPerChannel: 16, outputBitsPerChannel: null, statusLayout: 'OPTIONAL_NOT_CONFIGURED' }),
    processData: processData(PROCESS_DATA_CONTRIBUTION.INPUT),
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.ANALOG_INPUT_DATASHEET,
    manualFacts: manualFacts('750-471', {
      channelCount: 4,
      bitsPerChannel: 16,
      direction: DIRECTION.INPUT,
      processInputBits: 64,
      processOutputBits: 0,
      statusRepresentation: 'COUPLER_AND_MODULE_CONFIGURATION_DEPENDENT',
      channelSettingsInstanceSpecific: true,
    }, 'Four analog input channels with one 16-bit process value per channel, input direction only. The '
      + 'status/diagnostic representation depends on Coupler and module configuration, and the actual Channel '
      + 'settings are instance-specific unless proven otherwise.'),
    missing: Object.freeze(['status byte setting (optional, not configured)', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-554',
    displayName: '2-channel Analog Output',
    category: CATEGORY.ANALOG_OUTPUT,
    role: MODULE_ROLE.ANALOG_OUTPUT_MODULE,
    direction: DIRECTION.OUTPUT,
    channelCount: 2,
    channelType: CHANNEL_TYPE.ANALOG,
    supportedSignals: Object.freeze([SIGNAL.CURRENT_4_20_MA]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: 16, statusLayout: 'OPTIONAL_NOT_CONFIGURED' }),
    processData: processData(PROCESS_DATA_CONTRIBUTION.OUTPUT),
    startConstraint: null,
    endConstraint: null,
    reorderable: true,
    sourceNote: PROFILE_SOURCES.ANALOG_OUTPUT_DATASHEET,
    manualFacts: manualFacts('750-554', {
      channelCount: 2,
      bitsPerChannel: 16,
      direction: DIRECTION.OUTPUT,
      processInputBits: 0,
      processOutputBits: 32,
      statusRepresentation: 'COUPLER_AND_MODULE_CONFIGURATION_DEPENDENT',
      writeAuthority: false,
    }, 'Two analog output channels with one 16-bit output value per channel, output direction only. There is no write '
      + 'authority in this Stage, and the representation remains subject to verified Coupler and module settings.'),
    missing: Object.freeze(['status byte setting (optional, not configured)', ...COMMON_MISSING]),
  }),
  profile({
    modelNumber: '750-600',
    displayName: 'End Module',
    category: CATEGORY.END,
    role: MODULE_ROLE.END_MODULE,
    direction: DIRECTION.NONE,
    channelCount: 0,
    channelType: null,
    supportedSignals: Object.freeze([]),
    processImage: Object.freeze({ inputBitsPerChannel: null, outputBitsPerChannel: null, statusLayout: 'NOT_VERIFIED' }),
    processData: NON_PROCESS_DATA,
    startConstraint: null,
    endConstraint: 'MUST_BE_LAST',
    reorderable: false,
    sourceNote: PROFILE_SOURCES.NONE,
    manualFacts: manualFacts('750-600', {
      channelCount: 0,
      processInputBits: 0,
      processOutputBits: 0,
      terminatesInternalBus: true,
      applicationProcessInput: false,
      applicationProcessOutput: false,
    }, 'End Module. It terminates the internal bus. Not a Process I/O module: no Application Process Input, no '
      + 'Application Process Output, no ProcessModulePosition, no ProcessImageOrder, no Input address and no Output '
      + 'address. It may still appear as a position entry in the configuration tool while contributing no process '
      + 'data at all.'),
    missing: Object.freeze([]),
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

/**
 * True when the model can never contribute Application Process I/O data. Such a model has no ProcessModulePosition,
 * no ProcessImageOrder and no Process I/O address: its address status is NOT_APPLICABLE, never ADDRESS_UNRESOLVED.
 */
export function isNonProcessDataModel(modelNumber) {
  const p = getProfile(modelNumber);
  return Boolean(p && p.processData.contribution === PROCESS_DATA_CONTRIBUTION.NONE);
}

/** Canonical JSON of the whole catalogue. The ModuleProfileRevision fingerprint is built from this in revisions.mjs. */
export function profileCatalogueCanonical() {
  return canonicalJson(PROFILES);
}
