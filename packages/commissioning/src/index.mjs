// Stage 0.4C-K1 - browser-safe public entry point of the simulation-only Sensor Commissioning
// Monitor model. Contains NO Node built-in imports and no I/O of any kind: the evidence text is
// supplied by the caller, and every derived value is a pure function of that text plus an integer
// preview clock tick.
//
// Nothing in this package reads a device, opens a socket, writes a file, or holds control authority.

export {
  COLUMN, COLUMN_COUNT, ROW_STATE, TEMPLATE_COMPATIBILITY, ROW_CLASS, PASS_BLOCKED, PROTECTED_BASELINE,
  parseCsv, requireHeaderContract, parseSchedule, classifyRow, passBlockedReason, scheduleCounts,
  matchesProtectedBaseline, nodeInventory, measurementsOfSensor, isProductSensorId,
  PRODUCT_SENSOR_SEQUENCE,
} from './schedule.mjs';

export {
  AUTHORITATIVE_TOPOLOGY, TOPOLOGY_REFUSAL, validateSensorSequence, validateSensorCatalogue,
  validateSchedule, validateWallMapCounts, validateRuntimeCounts, validateTopologyInvariants,
  requireTopologyInvariants,
} from './topologyInvariants.mjs';

export {
  QUALITY, QUALITY_VALUES, DIAGNOSTIC_REASON, DIAGNOSTIC_REASON_VALUES, COMMISSIONING_STATE,
  COMMISSIONING_STATE_VALUES, RECORDABLE_VERDICT, FIELD_VERIFICATION_STATE, OBSERVATION_PROVENANCE,
  NODE_COMMUNICATION_STATE, PREVIEW_STALE_THRESHOLD_MS, PREVIEW_STABILIZATION_TICKS,
  CURRENT_VALUE_UNAVAILABLE_TEXT, HISTORICAL_LABEL, SIMULATED_DIAGNOSTIC_LABEL, requireQualityContract,
  isCurrentValueValid, isInvalidPresentation, diagnosticAllowedForQuality, presentQuality,
  presentDiagnosticReason, presentCurrentValue, presentLastValidValue, formatEngineeringValue,
  presentRawValue, presentValueAge, presentMeasurement,
} from './quality.mjs';

export {
  SCENARIOS, SCENARIO_KEYS, getScenario, scenarioCatalogue, buildObservations, syntheticEngineeringValue,
  syntheticRawValue, previewTimestamp, PREVIEW_EPOCH_MS, PREVIEW_TICK_MS, isPairedTemperatureRow,
  syntheticPairedReference, syntheticExpectedDifferential, syntheticPairedDifferential,
  syntheticPairedFrontTemperature,
} from './scenarios.mjs';

export {
  PAIRED_THERMAL_STATE, PAIRED_THERMAL_STATE_VALUES, DIFFERENTIAL_UNAVAILABLE_REASON,
  DIFFERENTIAL_UNAVAILABLE_REASON_VALUES, PAIRED_DIFFERENTIAL_MODE, PAIRED_DIFFERENTIAL_MODE_VALUES,
  FRONT_REAR_DELTA_LABEL, PAIRED_THERMAL_MARKER, PAIRED_THERMAL_GUIDANCE, PAIRED_THERMAL_EFFECT,
  THERMAL_SIDE, formatFrontRearDelta, buildThermalPair, buildThermalPairs, pairForRow,
  describeUnpairedMeasurement,
} from './thermalpair.mjs';

export {
  REQUIRED_OBSERVATION_FIELDS, requireObservationContract, summariseObservations, nodeCommunicationSummary,
  communicationLossIsNodeScoped, commissioningProgress,
} from './observation.mjs';

export {
  PreviewCommissioningSession, OBSERVATION_KIND, OBSERVATION_KIND_VALUES, PROVENANCE_APPROVAL_STATE,
  PROVENANCE_TESTER_PLACEHOLDER, PREVIEW_SESSION_ID_PREFIX, REMARK_MAX_LENGTH, previewSessionId,
  normalizeRemark, canRecordPass,
} from './workflow.mjs';

export {
  DENSITY, DEFAULT_DENSITY, ROW_CLASS_PRESENTATION, FIELD_VERIFICATION_TEXT, FILTERS, FILTER_KEYS,
  getFilter, buildRowView, buildRowViews, filterRows, filterCounts, summaryCounts, loopTestSummary,
  legend,
} from './presentation.mjs';

export {
  WALL_ORDER, WALL_LABELS, POSITION_KIND, MEASUREMENT_SIDE, SIDE_LABELS, MEASUREMENT_VIEW,
  MEASUREMENT_VIEW_VALUES, WALL_ROWS, WALL_MAP_EXPECTED, WALL_MAP_ORDER_TEXT, buildWallMap,
  visibleMeasurements, wallMeasurementKey, wallOrderIndex, wallOrderStep,
} from './wallmap.mjs';

export {
  TC_NODE_CONFIGURATION_SCHEMA_VERSION, TC_NODE_CONFIGURATION_ID, CONFIGURATION_MODE, ADDRESS_ENVIRONMENT,
  IP_ADDRESS_BASIS, PORT_STATUS, PROTOCOL_STATUS, CONFIGURATION_STATUS, REACHABILITY_STATUS, ACQUISITION_STATUS,
  HARDWARE_VERIFICATION_STATUS, FIELD_VERIFICATION_STATUS, HARDWARE_ACCESS_STATUS, EXPECTED_TOPOLOGY,
  EXPECTED_SYSTEM, NODE_FIELDS, CONFIGURATION_FIELDS, CANONICAL_TC_NODE_CONFIGURATION, APPROVED_NODE_IDS,
  APPROVED_CABINET_IDS, APPROVED_TEST_ADDRESSES, ipv4ClassificationCodes, approvedNodeForAddress,
  validateTcNodeConfiguration, systemTotals, buildTcNodeConfigurationView, canonicalTcNodeConfigurationText,
  tcNodeConfigurationFingerprint,
} from './tcNodeConfiguration.mjs';

export {
  CAB1_NODE_ID, CAB1_CABINET_ID, CAB1_CABINET_TAG, CAB1_MODULE_COUNT, CAB1_CHANNELS_PER_MODULE,
  CAB1_WORD_COUNT, CAB1_ASSIGNED_OFFSETS, CAB1_SPARE_OFFSETS, CAB1_SENSOR_COUNT, CAB1_MEASUREMENT_COUNT,
  PHYSICAL_READ_PROFILE, CANDIDATE_DECODE_STATE, CANDIDATE_DECODE_STATE_VALUES,
  LEGACY_WIRE_BREAK_WORD, LEGACY_REVERSE_POLARITY_WORD, CANDIDATE_SCALING_DIVISOR,
  SIDE_ORDER, PHYSICAL_SIDE_LABELS, classifyPhysicalWord, buildCab1Mapping, projectCab1Payload,
} from './tcPhysicalMapping.mjs';

export {
  TC_ACQUISITION_EVIDENCE_SCHEMA_VERSION, TC_ACQUISITION_EVIDENCE_ID, EVIDENCE_STAGE, EVIDENCE_MODE,
  EVIDENCE_CLASS, EVIDENCE_CLASS_VALUES, EVIDENCE_SCOPE, SOURCE_KIND, SOURCE_AVAILABILITY, VERIFICATION_STATUS,
  PROTOCOL_DECISION_STATUS, PORT_DECISION_STATUS, FORBIDDEN_DECISION_STATUSES, BYTE_ORDER, WORD_ORDER,
  STATUS_LOCATION, DIRECTION, MODULE_ROLE, FIELD_STATUS, CANDIDATE_STATUS, DERIVATION_STATE,
  ACQUISITION_COMMUNICATION_STATE, ACQUISITION_COMMUNICATION_STATE_VALUES, LIVE_COMMUNICATION_STATES,
  evidenceOnlyCommunicationState, FRESHNESS_STATE, SAMPLE_TIME_FIELDS, THRESHOLD_PENDING, FRESHNESS_THRESHOLDS,
  classifyFreshness, DECODE_STATUS, DIAGNOSTIC_CATEGORY, SAMPLE_PROVENANCE, ACQUISITION_SAMPLE_FIELDS,
  validateAcquisitionSample, frontRearDeltaPermitted, CLAIM_TOPIC, createTcAcquisitionEvidenceSet,
  CANONICAL_TC_ACQUISITION_EVIDENCE, validateTcAcquisitionEvidence, deriveCandidateProcessImage,
  recordToRecordConsistency, buildTcAcquisitionEvidenceView, canonicalTcAcquisitionEvidenceText,
  SOURCE_COMPLETENESS, SOURCE_BASIS, PROTOCOL_CANDIDATE_STATUS, OWNER_EVIDENCE_PENDING, CONNECTED_STORAGE_NOT_AUTHORIZED,
  CONTENT_NOT_RETRIEVED, NOT_PROVIDED, RECORD_TO_RECORD_CONSISTENCY_ONLY, RECORD_TO_RECORD_CONSISTENCY_MEANING,
  tcAcquisitionEvidenceFingerprint,
} from './tcAcquisitionEvidence.mjs';

export {
  MOTION_COMMISSIONING_DESIGN_SCHEMA, MOTION_COMMISSIONING_DESIGN_ID,
  MOTION_COMMISSIONING_DESIGN_ID_HISTORICAL_REV2, MOTION_COMMISSIONING_DESIGN_ID_HISTORICAL_REV1,
  HORIZONTAL_AXIS_WORDING,
  VERTICAL_AXIS_WORDING, MOTION_GATE_CLOSED, MOTION_GATE_REASON, MOTION_INTENTS,
  MOTION_STANDING_REFUSAL_REASONS, MOTION_ALL_REFUSAL_REASONS, MOTION_REFUSAL_WORDING,
  MOTION_INTENT_WORDING, MOTION_STATUS_WORDING, MOTION_CABINETS, MOTION_AXIS_IDS, MOTION_AXIS_ROLE,
  MOTION_AXIS_SENSE, MOTION_BRAKE_OUTPUT, MOTION_LIMIT_SIGNAL_ROLES, MOTION_WATER_JET_BINDINGS,
  MOTION_NOT_VERIFIED_CLAIMS, MOTION_NOT_VERIFIED_WORDING, MOTION_MAPPING_EVIDENCE_NOTES,
  MOTION_COMMISSIONING_DESIGN,
  motionStatusText, axisWording, axisPresentation, motionCommissioningDesign, canonicalMotionCommissioningText,
  motionCommissioningFingerprint, validateMotionCommissioningDesign,
} from './motionCommissioning.mjs';
