// Stage 0.4B-1 — shared vocabulary for the simulation-only Mapping Configuration domain.
// Pure data: no I/O, no device access, no provider switches.

export const STAGE = 'Stage 0.4B-3';

export const PROVIDER = Object.freeze({ SIMULATOR: 'SIMULATOR' });

export const LABEL = Object.freeze({
  DEFAULT_FROM_EXCEL: 'DEFAULT FROM EXCEL',
  DRAFT: 'DRAFT',
  SIMULATION_ONLY: 'SIMULATION ONLY',
  NO_HARDWARE_ACCESS: 'NO HARDWARE ACCESS',
  NO_WRITE_CONTROL: 'NO WRITE CONTROL',
  NOT_HARDWARE_VERIFIED: 'NOT HARDWARE VERIFIED',
  VALID: 'VALID',
  INVALID: 'INVALID',
  ADDRESS_UNRESOLVED: 'ADDRESS UNRESOLVED',
  ACTIVATION_NOT_AUTHORIZED: 'ACTIVATION NOT AUTHORIZED',
});

export const CATEGORY = Object.freeze({
  COUPLER: 'COUPLER',
  SUPPLY: 'SUPPLY',
  DIGITAL_INPUT: 'DIGITAL_INPUT',
  DIGITAL_OUTPUT: 'DIGITAL_OUTPUT',
  ANALOG_INPUT: 'ANALOG_INPUT',
  ANALOG_OUTPUT: 'ANALOG_OUTPUT',
  END: 'END',
});

export const DIRECTION = Object.freeze({ NONE: 'NONE', INPUT: 'INPUT', OUTPUT: 'OUTPUT' });

// ---------------------------------------------------------------------------
// Stage 0.4B-3 — module roles and process-data contribution (Owner ruling).
//
// A ROLE decides whether a module contributes Application Process I/O data at all. Power Supply, System Power
// Supply and End modules never contribute: they have no Application Process Input, no Application Process Output,
// no ProcessModulePosition, no ProcessImageOrder and no Process I/O address. Their address status is
// NOT_APPLICABLE with reason NON_PROCESS_DATA_MODULE. They are NOT waiting for an address, so they are never
// ADDRESS_UNRESOLVED. The Fieldbus Coupler is the head station: it defines the node and process-image rules and is
// not a channel I/O module, so it is not counted as an input or output module either.
//
// A role is a MODEL-LEVEL fact. It never hard-codes a physical Slot, a configuration-tool position or an address.
// ---------------------------------------------------------------------------
export const MODULE_ROLE = Object.freeze({
  FIELDBUS_COUPLER: 'FIELDBUS_COUPLER',
  POWER_SUPPLY: 'POWER_SUPPLY',
  SYSTEM_POWER_SUPPLY: 'SYSTEM_POWER_SUPPLY',
  END_MODULE: 'END_MODULE',
  DIGITAL_INPUT_MODULE: 'DIGITAL_INPUT_MODULE',
  DIGITAL_OUTPUT_MODULE: 'DIGITAL_OUTPUT_MODULE',
  ANALOG_INPUT_MODULE: 'ANALOG_INPUT_MODULE',
  ANALOG_OUTPUT_MODULE: 'ANALOG_OUTPUT_MODULE',
});

/** Roles that can never carry Application Process I/O data, whatever the rack looks like. */
export const NON_PROCESS_DATA_ROLES = Object.freeze([
  MODULE_ROLE.FIELDBUS_COUPLER, MODULE_ROLE.POWER_SUPPLY, MODULE_ROLE.SYSTEM_POWER_SUPPLY, MODULE_ROLE.END_MODULE,
]);

/** What a model contributes to the Application Process Image. NONE means it contributes no bit and no word. */
export const PROCESS_DATA_CONTRIBUTION = Object.freeze({ NONE: 'NONE', INPUT: 'INPUT', OUTPUT: 'OUTPUT' });

/** Where an evidence document physically is. A document outside the repository is never hashed here. */
export const EVIDENCE_AVAILABILITY = Object.freeze({
  COMMITTED_IN_REPOSITORY: 'COMMITTED_IN_REPOSITORY',
  OWNER_PROVIDED_OUTSIDE_REPOSITORY: 'OWNER_PROVIDED_OUTSIDE_REPOSITORY',
});

/**
 * States of ACTUAL-RACK evidence (a configuration-tool screen read on the real rack). This vocabulary is separate
 * from EVIDENCE_STATE on purpose: actual-rack evidence in this Stage is instance- and Channel-scoped and never
 * feeds address derivation, so it can never make an address verified.
 */
export const ACTUAL_RACK_EVIDENCE_STATE = Object.freeze({
  NOT_PROVIDED: 'NOT_PROVIDED',
  PROVIDED_UNVERIFIED: 'PROVIDED_UNVERIFIED',
  VERIFIED_ACTUAL_RACK_SCREENSHOT: 'VERIFIED_ACTUAL_RACK_SCREENSHOT',
});

/** One shared spelling of "does not apply", used for evidence state, order state and address status alike. */
export const NOT_APPLICABLE = 'NOT_APPLICABLE';

/** Verification status of a derived CANDIDATE. Only CANDIDATE_UNVERIFIED is ever produced in Stage 0.4B-3. */
export const CANDIDATE_STATUS = Object.freeze({ CANDIDATE_UNVERIFIED: 'CANDIDATE_UNVERIFIED' });

export const CHANNEL_TYPE = Object.freeze({ ANALOG: 'ANALOG', DIGITAL: 'DIGITAL' });

export const SIGNAL = Object.freeze({
  CURRENT_4_20_MA: 'CURRENT_4_20_MA',
  CURRENT_0_20_MA: 'CURRENT_0_20_MA',
  DIGITAL_24_VDC: 'DIGITAL_24_VDC',
});

export const POLARITY = Object.freeze({
  ACTIVE_WHEN_CLOSED: 'ACTIVE_WHEN_CLOSED',
  ACTIVE_WHEN_OPEN: 'ACTIVE_WHEN_OPEN',
});

export const CONTACT = Object.freeze({ NO: 'NO', NC: 'NC' });

// Owner rule (Stage 0.4B-1): every IVn lower- and upper-limit input is ACTIVE_WHEN_CLOSED
// (contact open = limit not detected; contact closed = limit detected). This is ContactPolarity.
// It is separate from RawInputInversion, which is UNVERIFIED and not configured by default.
export const OWNER_LIMIT_CONTACT_POLARITY = 'ACTIVE_WHEN_CLOSED';
export const POLARITY_BASIS = Object.freeze({ OWNER_RULE: 'OWNER_RULE', EXPLICIT_SEED: 'EXPLICIT_SEED' });
export const RAW_INPUT_INVERSION = Object.freeze({ NOT_CONFIGURED: 'NOT_CONFIGURED', APPLIED_ONCE: 'APPLIED_ONCE', NOT_INVERTED: 'NOT_INVERTED' });

export const ADDRESS_STATE = Object.freeze({
  UNRESOLVED: 'ADDRESS_UNRESOLVED',
  DERIVED: 'DERIVED',
  // A module that contributes no Application Process I/O data has no address and never will.
  NOT_APPLICABLE: 'NOT_APPLICABLE',
});

// Reason codes attached to every unresolved address. They are explanations,
// never a numeric fallback.
export const REASON = Object.freeze({
  HEAD_STATION_PROFILE_NOT_VERIFIED: 'HEAD_STATION_PROFILE_NOT_VERIFIED',
  MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED: 'MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED',
  MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED: 'MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED',
  ENGINEERING_RANGE_UNCONFIGURED: 'ENGINEERING_RANGE_UNCONFIGURED',
  SIGNAL_IDENTITY_UNRESOLVED: 'SIGNAL_IDENTITY_UNRESOLVED',
  OWNER_INPUT_PENDING: 'OWNER_INPUT_PENDING',
  PROCESS_IMAGE_SIZE_EXCEEDED: 'PROCESS_IMAGE_SIZE_EXCEEDED',
  NO_VERIFIED_PROCESS_IMAGE_RULE: 'NO_VERIFIED_PROCESS_IMAGE_RULE',
  PROCESS_IMAGE_ORDER_NOT_VERIFIED: 'PROCESS_IMAGE_ORDER_NOT_VERIFIED',
  BYTE_ORDER_NOT_VERIFIED: 'BYTE_ORDER_NOT_VERIFIED',
  WORD_ORDER_NOT_VERIFIED: 'WORD_ORDER_NOT_VERIFIED',
  CONFLICTING_PROCESS_IMAGE_EVIDENCE: 'CONFLICTING_PROCESS_IMAGE_EVIDENCE',
  // Stage 0.4B-3. A Power Supply, System Power Supply, End Module or head station has no Process I/O address and
  // will never have one. This is an explanation, never a pending state.
  NON_PROCESS_DATA_MODULE: 'NON_PROCESS_DATA_MODULE',
  // Stage 0.4B-3. The actual configuration-tool Process Data screen has not been supplied, so the real input and
  // output images, their offsets, module positions and any status or control fields are unknown.
  ACTUAL_PROCESS_DATA_NOT_PROVIDED: 'ACTUAL_PROCESS_DATA_NOT_PROVIDED',
  // Stage 0.4B-3. A candidate that was derived from manuals only has not been cross-checked against the actual
  // field-network mapping, so no numeric address in it is verified.
  ACTUAL_MAPPING_NOT_CROSS_CHECKED: 'ACTUAL_MAPPING_NOT_CROSS_CHECKED',
});

// Stage 0.4B-2 — process-image evidence vocabulary. An evidence state describes what is known about ONE
// evidence value. Only the two VERIFIED_* states can ever support a derived address.
export const EVIDENCE_STATE = Object.freeze({
  NOT_PROVIDED: 'NOT_PROVIDED',
  PROVIDED_UNVERIFIED: 'PROVIDED_UNVERIFIED',
  VERIFIED_PRIMARY_SOURCE: 'VERIFIED_PRIMARY_SOURCE',
  VERIFIED_IO_CHECK_EXPORT: 'VERIFIED_IO_CHECK_EXPORT',
  CONFLICTING_EVIDENCE: 'CONFLICTING_EVIDENCE',
  INCOMPLETE_PROFILE: 'INCOMPLETE_PROFILE',
});
// Profile-level only: every required value is verified, but from more than one kind of source.
export const VERIFIED_MIXED_SOURCES = 'VERIFIED_MIXED_SOURCES';

export const EVIDENCE_TYPE = Object.freeze({
  OWNER_WORKBOOK_FACT: 'OWNER_WORKBOOK_FACT',
  PRIMARY_DOCUMENT: 'PRIMARY_DOCUMENT',
  IO_CHECK_EXPORT: 'IO_CHECK_EXPORT',
  // Stage 0.4B-3: a screen read on the actual rack. No VERIFIED_* observation state maps to this type, so
  // actual-rack evidence can never on its own support a derived address.
  ACTUAL_RACK_SCREENSHOT: 'ACTUAL_RACK_SCREENSHOT',
  SYNTHETIC_TEST_RULE: 'SYNTHETIC_TEST_RULE',
});

export const IMPACT = Object.freeze({
  UNCHANGED: 'UNCHANGED',
  MOVED: 'MOVED',
  ADDRESS_CHANGED: 'ADDRESS_CHANGED',
  BINDING_INVALID: 'BINDING_INVALID',
  ADDRESS_UNRESOLVED: 'ADDRESS_UNRESOLVED',
  BLOCKED: 'BLOCKED',
});

export const SEVERITY = Object.freeze({ ERROR: 'ERROR', WARNING: 'WARNING', INFO: 'INFO' });

// Forbidden binding keys: a canonical or display address is never user input.
export const FORBIDDEN_BINDING_KEYS = Object.freeze([
  'address', 'canonicalAddress', 'displayAddress', 'byteOffset', 'wordOffset',
  'bitOffset', 'absoluteBitOffset', 'processImageOffset',
  'bitOffsetAbsolute', 'bitIndex', 'displayNotation', 'channelOffset', 'wordCount', 'bitWidth',
]);
