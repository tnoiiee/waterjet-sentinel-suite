// Stage 0.4C-K1 - the Commissioning Monitor presentation model.
//
// Pure functions over already-derived data. Filters, density and selection choose which rows are
// SHOWN and how they look. They never change the Mapping revision, the Runtime revision, the
// commissioning evidence, the Product topology or any device state: this module holds no state at
// all and copies what it needs.

import { ROW_CLASS, scheduleCounts } from './schedule.mjs';
import { COMMISSIONING_STATE, DIAGNOSTIC_REASON, presentMeasurement, QUALITY } from './quality.mjs';

export const DENSITY = Object.freeze({ COMFORTABLE: 'comfortable', COMPACT: 'compact' });
export const DEFAULT_DENSITY = DENSITY.COMFORTABLE;

/** Row classes as presented. Each carries a text marker so state never relies on colour alone. */
export const ROW_CLASS_PRESENTATION = Object.freeze({
  PRODUCT_COMPATIBLE: Object.freeze({ text: 'Product-compatible measurement', marker: '[PC]', className: 'pc' }),
  DRAWING_INTERNAL_CONFLICT: Object.freeze({ text: 'Drawing internal conflict', marker: '[IC]', className: 'ic' }),
  PRODUCT_TOPOLOGY_CONFLICT: Object.freeze({ text: 'Product-topology conflict', marker: '[TC]', className: 'tc' }),
  UNASSIGNED: Object.freeze({ text: 'Unassigned physical Channel', marker: '[UN]', className: 'un' }),
});

export const FIELD_VERIFICATION_TEXT = 'FIELD VERIFICATION PENDING';

/**
 * The full filter set required by the Stage. Each entry is presentation only.
 * `match` receives the row view and returns whether the row is shown.
 */
export const FILTERS = Object.freeze([
  { key: 'all', label: 'All', match: () => true },
  { key: 'product-compatible', label: 'Product-compatible', match: (v) => v.row.productCompatible },
  { key: 'drawing-verified', label: 'Drawing verified', match: (v) => v.row.drawingVerified },
  { key: 'drawing-internal-conflict', label: 'Drawing internal conflict', match: (v) => v.row.rowClass === ROW_CLASS.DRAWING_INTERNAL_CONFLICT },
  { key: 'product-topology-conflict', label: 'Product-topology conflict', match: (v) => v.row.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT },
  { key: 'unassigned', label: 'Unassigned', match: (v) => v.row.rowClass === ROW_CLASS.UNASSIGNED },
  { key: 'quality-good', label: 'GOOD', match: (v) => v.quality === QUALITY.GOOD },
  { key: 'quality-uncertain', label: 'UNCERTAIN', match: (v) => v.quality === QUALITY.UNCERTAIN },
  { key: 'quality-bad', label: 'BAD', match: (v) => v.quality === QUALITY.BAD },
  { key: 'quality-stale', label: 'STALE', match: (v) => v.quality === QUALITY.STALE },
  { key: 'communication-loss', label: 'COMMUNICATION_LOSS', match: (v) => v.diagnosticReason === DIAGNOSTIC_REASON.COMMUNICATION_LOSS },
  { key: 'simulated-wire-break', label: 'Simulated wire break', match: (v) => v.diagnosticReason === DIAGNOSTIC_REASON.WIRE_BREAK },
  { key: 'commissioning-not-tested', label: 'NOT_TESTED', match: (v) => v.commissioningState === COMMISSIONING_STATE.NOT_TESTED },
  { key: 'commissioning-in-progress', label: 'IN_PROGRESS', match: (v) => v.commissioningState === COMMISSIONING_STATE.IN_PROGRESS },
  { key: 'commissioning-pass', label: 'PASS', match: (v) => v.commissioningState === COMMISSIONING_STATE.PASS },
  { key: 'commissioning-fail', label: 'FAIL', match: (v) => v.commissioningState === COMMISSIONING_STATE.FAIL },
  { key: 'commissioning-partial', label: 'PARTIAL', match: (v) => v.commissioningState === COMMISSIONING_STATE.PARTIAL },
  { key: 'commissioning-blocked', label: 'BLOCKED', match: (v) => v.commissioningState === COMMISSIONING_STATE.BLOCKED },
]);

export const FILTER_KEYS = Object.freeze(FILTERS.map((f) => f.key));

export function getFilter(key) {
  const filter = FILTERS.find((f) => f.key === key);
  if (!filter) throw new Error(`unknown filter '${key}'`);
  return filter;
}

/**
 * Builds the overview row for one schedule row plus its observation and workflow state.
 * Every field the Stage requires is present; nothing is inferred from a neighbouring row.
 */
export function buildRowView(row, observation, sessionState = null) {
  if (!observation) throw new Error(`row ${row.physicalChannelKey} has no observation`);
  const measurement = presentMeasurement(observation);
  const rowClass = ROW_CLASS_PRESENTATION[row.rowClass];
  if (!rowClass) throw new Error(`row ${row.physicalChannelKey} has no presentation class`);
  const commissioningState = sessionState?.stateOf(row) ?? (row.passBlockedReason ? COMMISSIONING_STATE.BLOCKED : COMMISSIONING_STATE.NOT_TESTED);
  return Object.freeze({
    key: row.physicalChannelKey,
    row,
    // Identity columns.
    sensorId: row.sensorId,
    drawingSide: row.side,
    productMeasurementIdentity: row.measurementId,
    node: row.nodeId,
    module: row.moduleInstance,
    physicalChannel: row.channel,
    terminal: row.polePlusDest,
    drawingPage: row.tablePage,
    drawingDiagramPage: row.diagramPage,
    mappingRevision: 'BASELINE_REV1',
    drawingStatus: row.rowState,
    drawingVerificationState: row.rowState,
    productCompatibility: row.templateCompatibility,
    productCompatible: row.productCompatible,
    fieldVerificationState: measurement.fieldVerification,
    fieldVerificationText: FIELD_VERIFICATION_TEXT,
    // Observed (synthetic) columns.
    rawValue: measurement.raw.text,
    engineeringValue: measurement.current.text,
    engineeringUnit: measurement.current.unit,
    quality: observation.quality,
    qualityMarker: measurement.quality.marker,
    qualitySeverity: measurement.quality.severity,
    diagnosticReason: observation.diagnosticReason,
    diagnosticText: measurement.diagnostic.text,
    currentValueValid: measurement.current.valid,
    invalidPresentation: measurement.invalidPresentation,
    lastValidValue: measurement.lastValid.text,
    lastValidTimestamp: measurement.lastValid.timestamp,
    lastValidLabel: measurement.lastValid.label,
    observationTimestamp: observation.observationTimestamp,
    valueAge: measurement.age.text,
    valueAgeMs: measurement.age.milliseconds,
    valueAgeExceedsPreviewThreshold: measurement.age.exceedsPreviewStaleThreshold,
    nodeCommunicationState: observation.nodeCommunicationState,
    scenarioSource: observation.scenarioSource,
    provenance: measurement.provenance,
    simulatedDiagnostic: measurement.simulatedDiagnostic,
    simulatedDiagnosticLabel: measurement.simulatedDiagnosticLabel,
    // Commissioning columns.
    commissioningState,
    blockingReason: row.passBlockedReason,
    observationBlockingReason: observation.blockingReason ?? null,
    scenarioNote: observation.note ?? null,
    selectableAsProductSensor: observation.selectableAsProductSensor === true,
    rowClass: row.rowClass,
    rowClassText: rowClass.text,
    rowClassMarker: rowClass.marker,
    rowClassClassName: rowClass.className,
  });
}

export function buildRowViews(schedule, observations, sessionState = null) {
  return Object.freeze(schedule.rows.map((row) => buildRowView(row, observations.get(row.physicalChannelKey), sessionState)));
}

export function filterRows(viewRows, filterKey) {
  const filter = getFilter(filterKey);
  return Object.freeze(viewRows.filter((v) => filter.match(v)));
}

/** Per-filter counts for the filter buttons. Presentation only. */
export function filterCounts(viewRows) {
  return Object.freeze(Object.fromEntries(FILTERS.map((f) => [f.key, viewRows.filter((v) => f.match(v)).length])));
}

/**
 * Summary counts required by the Stage, all recomputed from the parsed evidence and the current
 * observations. Nothing here is cached from a previous filter selection.
 */
export function summaryCounts(schedule, viewRows) {
  const counts = scheduleCounts(schedule);
  const count = (predicate) => viewRows.filter(predicate).length;
  return Object.freeze({
    totalPhysicalChannels: counts.physicalChannels,
    mappedPhysicalChannels: counts.mappedChannels,
    unassignedPhysicalChannels: counts.unassignedChannels,
    productCompatibleMeasurements: counts.productCompatibleMeasurements,
    productSensors: counts.productSensors,
    drawingVerifiedRows: counts.drawingVerifiedRows,
    drawingInternalConflictRows: counts.drawingInternalConflictRows,
    productTopologyConflictRows: counts.productTopologyConflictRows,
    conflictRows: counts.drawingInternalConflictRows + counts.productTopologyConflictRows,
    unresolvedRows: counts.unresolvedRows,
    good: count((v) => v.quality === QUALITY.GOOD),
    uncertain: count((v) => v.quality === QUALITY.UNCERTAIN),
    bad: count((v) => v.quality === QUALITY.BAD),
    stale: count((v) => v.quality === QUALITY.STALE),
    disabled: count((v) => v.quality === QUALITY.DISABLED),
    communicationLoss: count((v) => v.diagnosticReason === DIAGNOSTIC_REASON.COMMUNICATION_LOSS),
    simulatedWireBreak: count((v) => v.diagnosticReason === DIAGNOSTIC_REASON.WIRE_BREAK),
    outOfRange: count((v) => v.diagnosticReason === DIAGNOSTIC_REASON.OUT_OF_RANGE),
    notTested: count((v) => v.commissioningState === COMMISSIONING_STATE.NOT_TESTED),
    inProgress: count((v) => v.commissioningState === COMMISSIONING_STATE.IN_PROGRESS),
    passed: count((v) => v.commissioningState === COMMISSIONING_STATE.PASS),
    failed: count((v) => v.commissioningState === COMMISSIONING_STATE.FAIL),
    partial: count((v) => v.commissioningState === COMMISSIONING_STATE.PARTIAL),
    blocked: count((v) => v.commissioningState === COMMISSIONING_STATE.BLOCKED),
    currentValueValid: count((v) => v.currentValueValid === true),
    currentValueUnavailable: count((v) => v.currentValueValid !== true),
  });
}

/**
 * Loop-Test progress: a concise field-review summary over the population that can actually be loop
 * tested - the 216 Product-compatible measurements of the 108 Product Sensors (I7 and I16 included).
 * The 24 unassigned physical Channels are NOT loop-testable and are reported separately by the
 * evidence counts instead of being folded into a workflow state.
 *
 * Every number is derived from the parsed evidence and the current Preview session; nothing is
 * cached and no total is invented. BLOCKED stays evidence-derived: it is what the Drawing says about
 * a row, never something a person records.
 */
export function loopTestSummary(schedule, sessionState = null) {
  const counts = scheduleCounts(schedule);
  const rows = schedule.rows.filter((row) => row.productCompatible);
  const byState = Object.fromEntries(Object.values(COMMISSIONING_STATE).map((state) => [state, 0]));
  for (const row of rows) {
    const state = sessionState ? sessionState.stateOf(row) : null;
    const effective = state ?? (row.passBlockedReason ? COMMISSIONING_STATE.BLOCKED : COMMISSIONING_STATE.NOT_TESTED);
    byState[effective] += 1;
  }
  return Object.freeze({
    productSensors: counts.productSensors,
    productCompatibleMeasurements: counts.productCompatibleMeasurements,
    total: rows.length,
    byState: Object.freeze(byState),
    eligibleForPass: rows.filter((row) => !row.passBlockedReason).length,
    blockedFromPass: rows.filter((row) => row.passBlockedReason).length,
    notLoopTestable: schedule.rows.length - rows.length,
    note: 'Population: the Product-compatible measurements.'
      + ' The 24 unassigned physical Channels are not loop-testable and are excluded.',
  });
}

/** Legend entries for the four evidence classes plus the safety states. */
export function legend() {
  return Object.freeze([
    { marker: '[PC]', text: 'Product-compatible measurement (Product Sensor identity)' },
    { marker: '[IC]', text: 'Drawing internal conflict - BLOCKED, no PASS permitted' },
    { marker: '[TC]', text: 'Product-topology conflict - none in the corrected baseline (I7 and I16 are normal Product Sensors)' },
    { marker: '[UN]', text: 'Unassigned physical Channel - not a commissioned spare' },
    { marker: '[X]', text: 'BAD - current value unavailable, cause named' },
    { marker: '[~]', text: 'STALE - age beyond the Preview threshold, not shown as current' },
    { marker: '[?]', text: 'UNCERTAIN - presented and marked, no automatic action' },
    { marker: '[OK]', text: 'GOOD - current value valid' },
    { marker: '[OFF]', text: 'DISABLED (measurement quality) - MEASUREMENT UNAVAILABLE: channel not commissioned; Sensor operational Enabled/Disabled state is separate' },
  ]);
}
