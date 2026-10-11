// Stage 0.4C-K1 - browser-safe public entry point of the simulation-only Sensor Commissioning
// Monitor model. Contains NO Node built-in imports and no I/O of any kind: the evidence text is
// supplied by the caller, and every derived value is a pure function of that text plus an integer
// preview clock tick.
//
// Nothing in this package reads a device, opens a socket, writes a file, or holds control authority.

export {
  COLUMN, COLUMN_COUNT, ROW_STATE, TEMPLATE_COMPATIBILITY, ROW_CLASS, PASS_BLOCKED, PROTECTED_BASELINE,
  parseCsv, requireHeaderContract, parseSchedule, classifyRow, passBlockedReason, scheduleCounts,
  matchesProtectedBaseline, nodeInventory, measurementsOfSensor, isNonSensorGapSensorId,
} from './schedule.mjs';

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
  syntheticRawValue, previewTimestamp, PREVIEW_EPOCH_MS, PREVIEW_TICK_MS,
} from './scenarios.mjs';

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
