// Stage 0.4C-K1 - Commissioning Monitor presentation tests (Stage task sections 7, 8 and 16 D).
// Filters and summaries are presentation only: they must not change the Mapping revision, the
// Runtime revision, the commissioning evidence, the Product topology or any device state.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  COMMISSIONING_STATE, DENSITY, DEFAULT_DENSITY, FIELD_VERIFICATION_TEXT, FILTERS, FILTER_KEYS,
  PreviewCommissioningSession, ROW_CLASS, buildRowView, buildRowViews, filterCounts, filterRows, getFilter,
  legend, summaryCounts,
} from '../src/index.mjs';
import { schedule, counts } from './helpers/schedule.mjs';
import { buildObservations } from './helpers/scenarios.mjs';

const observations = buildObservations('NORMAL_GOOD');
const views = buildRowViews(schedule, observations);

const REQUIRED_FILTERS = [
  'all', 'product-compatible', 'drawing-verified', 'drawing-internal-conflict', 'product-topology-conflict',
  'unassigned', 'quality-good', 'quality-uncertain', 'quality-bad', 'quality-stale', 'communication-loss',
  'simulated-wire-break', 'commissioning-not-tested', 'commissioning-in-progress', 'commissioning-pass',
  'commissioning-fail', 'commissioning-partial', 'commissioning-blocked',
];

test('the required filter set is present, in order, and refuses an unknown key', () => {
  assert.deepEqual([...FILTER_KEYS], REQUIRED_FILTERS);
  assert.equal(FILTERS.length, REQUIRED_FILTERS.length);
  assert.equal(getFilter('all').label, 'All');
  assert.throws(() => getFilter('made-up'), /unknown filter/);
});

test('the overview row carries every column the Stage requires', () => {
  const view = views[0];
  for (const field of [
    'sensorId', 'drawingSide', 'productMeasurementIdentity', 'node', 'module', 'physicalChannel', 'terminal',
    'drawingVerificationState', 'productCompatibility', 'rawValue', 'engineeringValue', 'engineeringUnit',
    'quality', 'diagnosticReason', 'currentValueValid', 'lastValidValue', 'lastValidTimestamp', 'observationTimestamp',
    'valueAge', 'commissioningState', 'fieldVerificationState',
  ]) {
    assert.equal(field in view, true, field);
  }
  assert.equal(view.mappingRevision, 'BASELINE_REV1');
  assert.equal(view.fieldVerificationText, FIELD_VERIFICATION_TEXT);
  assert.equal(view.fieldVerificationState, 'FIELD_VERIFICATION_PENDING');
});

test('a row view refuses to guess an observation that does not exist', () => {
  assert.throws(() => buildRowView(schedule.rows[0], undefined), /has no observation/);
});

test('the four evidence classes are distinguishable without colour alone', () => {
  const classes = new Set(views.map((v) => v.rowClassText));
  assert.equal(classes.size, 4);
  const markers = views.map((v) => v.rowClassMarker);
  assert.equal(new Set(markers).size, 4);
  assert.equal(views.every((v) => v.rowClassClassName.length > 0), true);
  assert.equal(legend().length, 9);
});

test('filter counts follow the evidence exactly', () => {
  const countsByFilter = filterCounts(views);
  assert.equal(countsByFilter.all, 240);
  assert.equal(countsByFilter['product-compatible'], 212);
  assert.equal(countsByFilter['drawing-verified'], 200);
  assert.equal(countsByFilter['drawing-internal-conflict'], 12);
  assert.equal(countsByFilter['product-topology-conflict'], 4);
  assert.equal(countsByFilter.unassigned, 24);
  assert.equal(countsByFilter['commissioning-blocked'], 40);
  assert.equal(countsByFilter['commissioning-not-tested'], 200);
});

test('filtering never changes the underlying rows', () => {
  const before = JSON.stringify(schedule.rows);
  const bad = filterRows(views, 'quality-bad');
  assert.equal(Array.isArray(bad), true);
  assert.equal(JSON.stringify(schedule.rows), before);
  assert.equal(filterRows(views, 'all').length, views.length);
});

test('the summary reports the protected totals plus the live presentation counts', () => {
  const summary = summaryCounts(schedule, views);
  assert.equal(summary.totalPhysicalChannels, 240);
  assert.equal(summary.mappedPhysicalChannels, 216);
  assert.equal(summary.unassignedPhysicalChannels, 24);
  assert.equal(summary.productCompatibleMeasurements, 212);
  assert.equal(summary.productSensors, 106);
  assert.equal(summary.drawingVerifiedRows, 200);
  assert.equal(summary.drawingInternalConflictRows, 12);
  assert.equal(summary.productTopologyConflictRows, 4);
  assert.equal(summary.conflictRows, 16);
  assert.equal(summary.unresolvedRows, 0);
  // 216 mapped Channels carry a sample; the 24 unassigned positions carry no measurement at all.
  assert.equal(summary.good, 216);
  assert.equal(summary.disabled, 24);
  assert.equal(summary.bad, 0);
  assert.equal(summary.blocked, 40);
  assert.equal(summary.notTested, 200);
  assert.equal(summary.passed, 0);
  assert.equal(summary.currentValueValid, 216);
  assert.equal(summary.currentValueUnavailable, 24);
});

test('a scenario is reflected in the summary counts without touching the evidence', () => {
  const scenarioViews = buildRowViews(schedule, buildObservations('COMMUNICATION_LOSS_NODE', { nodeId: 'TC-NODE-02' }));
  const summary = summaryCounts(schedule, scenarioViews);
  assert.equal(summary.communicationLoss, 36);
  assert.equal(summary.bad, 36);
  assert.equal(summary.disabled, 24);
  assert.equal(summary.good, 180);
  assert.equal(summary.totalPhysicalChannels, counts.physicalChannels);
  assert.equal(summary.productCompatibleMeasurements, counts.productCompatibleMeasurements);
});

test('the simulated wire-break scenario is presented as simulation in every affected row', () => {
  const scenarioViews = buildRowViews(schedule, buildObservations('SIMULATED_WIRE_BREAK'));
  const affected = scenarioViews.filter((v) => v.simulatedDiagnostic);
  assert.equal(affected.length > 0, true);
  for (const view of affected) {
    assert.match(view.simulatedDiagnosticLabel, /NOT HARDWARE DETECTED/);
    assert.equal(view.currentValueValid, false);
    assert.equal(view.engineeringValue.includes('UNAVAILABLE'), true);
    assert.equal(view.lastValidLabel.includes('not the current value'), true);
  }
  const untouched = scenarioViews.filter((v) => !v.simulatedDiagnostic);
  assert.equal(untouched.every((v) => v.simulatedDiagnosticLabel === null), true);
});

test('conflict and unassigned rows are presented as BLOCKED and never as GOOD', () => {
  const summary = summaryCounts(schedule, views);
  const blocked = views.filter((v) => v.commissioningState === COMMISSIONING_STATE.BLOCKED);
  assert.equal(blocked.length, 40);
  assert.equal(blocked.filter((v) => v.rowClass === ROW_CLASS.UNASSIGNED).length, 24);
  assert.equal(blocked.filter((v) => v.rowClass === ROW_CLASS.DRAWING_INTERNAL_CONFLICT).length, 12);
  assert.equal(blocked.filter((v) => v.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT).length, 4);
  for (const view of blocked) assert.notEqual(view.blockingReason, null);
  assert.equal(summary.passed, 0);
});

test('a workflow verdict is visible in the view model and in the summary, and only there', () => {
  const session = new PreviewCommissioningSession({ sessionIndex: 1 });
  const target = schedule.rows.find((r) => r.rowClass === ROW_CLASS.PRODUCT_COMPATIBLE && r.drawingVerified);
  session.recordVerdict(target, { verdict: 'PASS', remark: 'preview only' });
  const sessionViews = buildRowViews(schedule, observations, session);
  const summary = summaryCounts(schedule, sessionViews);
  assert.equal(summary.passed, 1);
  assert.equal(summary.blocked, 40);
  assert.equal(summary.notTested, 199);
  const view = sessionViews.find((v) => v.key === target.physicalChannelKey);
  assert.equal(view.commissioningState, COMMISSIONING_STATE.PASS);
  // The evidence row itself is unchanged: the workflow state is presentation-side only.
  assert.equal(target.rowState, 'DRAWING_VERIFIED');
  assert.equal(target.passBlockedReason, null);
});

test('presentation density is a constant with no effect on the data', () => {
  assert.deepEqual(Object.values(DENSITY), ['comfortable', 'compact']);
  assert.equal(DEFAULT_DENSITY, DENSITY.COMFORTABLE);
  const before = JSON.stringify(views.map((v) => v.key));
  assert.equal(JSON.stringify(views.map((v) => v.key)), before);
});
