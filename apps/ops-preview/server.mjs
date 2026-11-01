// Stage 0.4C-K2 - the local, read-only host of the Kiosk Preview route (Stage 0.4C-K1 Preview
// plus the Stage 0.4C-K2 TC Node configuration projection).
//
//   GET /                                 the Operations route shell (with the Commissioning Monitor)
//   GET /app.mjs, /styles.css             UI assets (fixed whitelist)
//   GET /pkg/<module>.mjs                 browser-safe modules of packages/commissioning/src
//   GET /api/health                       local liveness for the Kiosk shell reconnect logic
//   GET /api/boundary                     the Stage boundary statement shown on screen
//   GET /api/commissioning/schedule       the merged 240-row drawing-derived schedule, read-only
//   GET /api/commissioning/scenarios      the thirteen deterministic synthetic scenarios
//                                         (ten protected + three paired thermal demonstrations)
//   GET /api/commissioning/observations   one scenario's synthetic observations (never a device read)
//   GET /api/operations/summary           node communication and commissioning progress summary
//   GET /api/sensor-map                   the public-safe synthetic sensor-map example, read-only
//   GET /api/tc-node-configuration        the six-Node TC configuration projection, read-only (no probe,
//                                         no socket, no device; configuration evidence only)
//   GET /api/tc-acquisition-evidence      the Stage 0.4C-K3 evidence and contract projection, read-only
//                                         (classification, decision status, candidate process image;
//                                         no probe, no socket, no device, no live communication state)
//   GET /api/tc-acquisition               presentation-only projection of the Runtime Snapshot;
//                                         Runtime status distinguishes a pending from failed session;
//                                         never a source of acquisition state or device access.
//                                         Accepts BOTH quarantined TC variants: the deterministic
//                                         non-Hardware record and the one-Node physical READ-ONLY
//                                         record, whose CAB1 measurement projection travels with it.
//   GET /api/tc-physical-mapping          the canonical CAB1 Drawing-rev1 mapping projection, read-only
//                                         (40 offsets, 18 Sensors, preserved conflict and spare states;
//                                         no endpoint, no legacy IP field, no device access)
//   GET /api/motion-commissioning/catalogue
//                                         the read-only STATIC motion commissioning catalogue: the
//                                         Owner-confirmed topology (four controllers, sixteen axes,
//                                         the eight canonical Water Jet allocations, brake and limit
//                                         plans), the controller model, the Horizontal/Vertical axis
//                                         roles with their physical axis letters, every configuration
//                                         and commissioning status, the mapping provenance, the
//                                         recorded Drawing evidence notes, the candidate values and
//                                         the authorization gate. It reads NO Runtime and no device.
//                                         The static catalogue is deliberately NOT a Snapshot member
//                                         and NOT a Delta member: it does not change with a revision,
//                                         so it is never re-sent with every Snapshot.
//
// Every other method is refused with 405, and there is no route that writes, connects, polls, reads
// a device, changes the Mapping, or commands anything. The server binds to 127.0.0.1 only: the bind
// host is a constant and no environment variable, argument or configuration can change it. The
// Preview workflow state lives in the browser session and is never persisted here.

import { createHash } from 'node:crypto';
import { createServer } from 'node:http';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, extname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  PreviewCommissioningSession, buildObservations, buildRowViews, commissioningProgress,
  nodeCommunicationSummary, parseSchedule, scheduleCounts, scenarioCatalogue, summariseObservations,
  COLUMN_COUNT, PROTECTED_BASELINE, SCENARIO_KEYS,
  CANONICAL_TC_NODE_CONFIGURATION, TC_NODE_CONFIGURATION_ID, buildTcNodeConfigurationView,
  tcNodeConfigurationFingerprint,
  CANONICAL_TC_ACQUISITION_EVIDENCE, buildTcAcquisitionEvidenceView, recordToRecordConsistency,
  tcAcquisitionEvidenceFingerprint, requireTopologyInvariants,
  buildCab1Mapping, projectCab1Payload, CAB1_NODE_ID, CAB1_CABINET_ID, PHYSICAL_READ_PROFILE,
  motionCommissioningDesign, MOTION_ALL_REFUSAL_REASONS,
} from '../../packages/commissioning/src/index.mjs';
import { validTcAcquisition, validDeterministicTcAcquisition } from '../../packages/contracts/wjss-contracts-ts/validate.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = join(HERE, '..', '..');
const PUBLIC_DIR = join(HERE, 'public');
const PKG_SRC = join(REPO, 'packages', 'commissioning', 'src');
const SCHEDULE_PATH = join(REPO, 'docs', 'evidence', 'tc-drawing-schedule-rev1.csv');
const SENSOR_MAP_PATH = join(REPO, 'config', 'examples', 'sensor-map.example.json');

/** Stage identity of this Preview. Displayed on every screen and returned by every API route. */
export const STAGE_ID = '0.4C-K2';
export const PREVIEW_MODE = 'SIMULATION / PREVIEW';

/** S6: the only bind host. A constant, never read from the environment or from arguments. */
export const UI_BIND_HOST = '127.0.0.1';

/** The approved local origin, assembled from the loopback literal so no other host can be written. */
export const LOOPBACK_ORIGIN = 'http://127.0.0.1';
export const DEFAULT_PORT = 5187;
// The separate Runtime host's existing loopback API port. Only the port may be overridden;
// requests cannot supply a host, path or origin. This is not a device endpoint.
export const DEFAULT_RUNTIME_PORT = 5181;

const PUBLIC_FILES = new Set(['index.html', 'app.mjs', 'styles.css']);
const MIME = Object.freeze({
  '.html': 'text/html; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
});
const SECURITY_HEADERS = Object.freeze({
  'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
  'X-Content-Type-Options': 'nosniff',
  'Referrer-Policy': 'no-referrer',
  'Cache-Control': 'no-store',
});

const BROWSER_MODULES = new Set(readdirSync(PKG_SRC).filter((f) => f.endsWith('.mjs')));

/** The Stage boundary statement. It is data, so the screen and the tests read the same words. */
export const BOUNDARY = Object.freeze({
  stageId: STAGE_ID,
  mode: PREVIEW_MODE,
  statements: Object.freeze([
    'SIMULATION / PREVIEW',
    'NO HARDWARE ACCESS',
    'NO DEVICE WRITE',
    'FIELD VERIFICATION PENDING',
  ]),
  hardwareConnection: false,
  deviceWriteAuthority: false,
  liveTransportImplemented: false,
  fieldVerified: false,
  approvedOrigin: LOOPBACK_ORIGIN,
  bindHost: UI_BIND_HOST,
  evidenceBaseline: 'BASELINE_REV1',
  drawingStatus: 'DRAWING_PENDING_REVISION',
  scheduleRows: PROTECTED_BASELINE.PHYSICAL_CHANNEL_COUNT,
  notes: Object.freeze([
    'The Commissioning Monitor uses synthetic, read-only observations.',
    'The wire-break scenario is simulated: physical wire-break decoding is blocked in the baseline.',
    'Preview verdicts are local to the review session and approve nothing.',
  ]),
});

let cachedEvidence = null;

/** Loads and parses the committed evidence once. Read-only: nothing is ever written back. */
export function loadEvidence() {
  if (!cachedEvidence) {
    const schedule = parseSchedule(readFileSync(SCHEDULE_PATH, 'utf8'));
    const counts = scheduleCounts(schedule);
    const sensorMap = JSON.parse(readFileSync(SENSOR_MAP_PATH, 'utf8'));
    // Central topology invariant (Owner critical topology correction): the served Sensor catalogue,
    // the physical schedule and the Sensor sequence must agree on 108 Sensors / 216 assigned / 240
    // physical / 24 unassigned / no NON_SENSOR_GAP. Disagreement refuses the whole evidence load:
    // no surface may present a count another layer contradicts.
    const topology = requireTopologyInvariants({ sensorMap, schedule, scheduleCounts: counts });
    cachedEvidence = Object.freeze({ schedule, counts, sensorMap, topology });
  }
  return cachedEvidence;
}

/**
 * The serialisable shape of a schedule row. It carries exactly the fields the presentation layer
 * reads, so the browser can rebuild the same row views with the SAME package code instead of a
 * second implementation of the rules.
 */
function serialiseRow(row) {
  return {
    scheduleRow: row.scheduleRow,
    physicalChannelKey: row.physicalChannelKey,
    cabinet: row.cabinet,
    cabinetTag: row.cabinetTag,
    nodeId: row.nodeId,
    tablePage: row.tablePage,
    diagramPage: row.diagramPage,
    moduleInstance: row.moduleInstance,
    moduleModel: row.moduleModel,
    channel: row.channel,
    sensorId: row.sensorId,
    side: row.side,
    polePlusDest: row.polePlusDest,
    rowState: row.rowState,
    templateCompatibility: row.templateCompatibility,
    rowClass: row.rowClass,
    measurementId: row.measurementId,
    drawingVerified: row.drawingVerified,
    productCompatible: row.productCompatible,
    passBlockedReason: row.passBlockedReason,
    wordOrdinal: row.wordOrdinal,
  };
}

function schedulePayload() {
  const { schedule, counts } = loadEvidence();
  return {
    label: schedule.label,
    columnCount: COLUMN_COUNT,
    mappingRevision: 'BASELINE_REV1',
    counts,
    rows: schedule.rows.map(serialiseRow),
  };
}

function observationsPayload(url) {
  const { schedule } = loadEvidence();
  const scenarioKey = url.searchParams.get('scenario') ?? 'NORMAL_GOOD';
  const tick = Number.parseInt(url.searchParams.get('tick') ?? '0', 10);
  const nodeId = url.searchParams.get('node');
  if (!SCENARIO_KEYS.includes(scenarioKey)) return { status: 404, body: { error: `unknown scenario '${scenarioKey}'` } };
  if (!Number.isInteger(tick) || tick < 0) return { status: 400, body: { error: 'tick must be a non-negative integer' } };
  let observations;
  try {
    observations = buildObservations(schedule, { scenarioKey, tick, nodeId });
  } catch (err) {
    return { status: 400, body: { error: err.message } };
  }
  const session = new PreviewCommissioningSession({ sessionIndex: 0 });
  const views = buildRowViews(schedule, observations, session);
  return {
    status: 200,
    body: {
      scenarioKey,
      tick,
      nodeId,
      summary: summariseObservations(observations),
      nodeSummary: nodeCommunicationSummary(schedule, observations),
      commissioningProgress: commissioningProgress(schedule, session),
      // The raw synthetic observations travel with the derived views so the browser applies the
      // Preview workflow to the same records the server derived, with no second rule set.
      observations: Object.fromEntries(observations),
      views: views.map((v) => ({
        key: v.key,
        sensorId: v.sensorId,
        drawingSide: v.drawingSide,
        productMeasurementIdentity: v.productMeasurementIdentity,
        node: v.node,
        module: v.module,
        physicalChannel: v.physicalChannel,
        terminal: v.terminal,
        drawingVerificationState: v.drawingVerificationState,
        productCompatibility: v.productCompatibility,
        rawValue: v.rawValue,
        engineeringValue: v.engineeringValue,
        engineeringUnit: v.engineeringUnit,
        quality: v.quality,
        qualityMarker: v.qualityMarker,
        diagnosticReason: v.diagnosticReason,
        diagnosticText: v.diagnosticText,
        currentValueValid: v.currentValueValid,
        lastValidValue: v.lastValidValue,
        lastValidLabel: v.lastValidLabel,
        lastValidTimestamp: v.lastValidTimestamp,
        observationTimestamp: v.observationTimestamp,
        valueAge: v.valueAge,
        valueAgeExceedsPreviewThreshold: v.valueAgeExceedsPreviewThreshold,
        nodeCommunicationState: v.nodeCommunicationState,
        commissioningState: v.commissioningState,
        blockingReason: v.blockingReason,
        observationBlockingReason: v.observationBlockingReason,
        scenarioNote: v.scenarioNote,
        fieldVerificationState: v.fieldVerificationState,
        fieldVerificationText: v.fieldVerificationText,
        rowClass: v.rowClass,
        rowClassText: v.rowClassText,
        rowClassMarker: v.rowClassMarker,
        rowClassClassName: v.rowClassClassName,
        simulatedDiagnostic: v.simulatedDiagnostic,
        simulatedDiagnosticLabel: v.simulatedDiagnosticLabel,
        scenarioSource: v.scenarioSource,
      })),
    },
  };
}

function sensorMapPayload() {
  const { sensorMap } = loadEvidence();
  const slots = sensorMap.logicalMatrix.slots;
  return {
    label: sensorMap._label,
    logicalMatrix: {
      columns: sensorMap.logicalMatrix.columns,
      rows: sensorMap.logicalMatrix.rows,
      sensorLocations: sensorMap.logicalMatrix.sensorLocations,
      thermocoupleChannels: sensorMap.logicalMatrix.thermocoupleChannels,
      sensorsPerWall: sensorMap.logicalMatrix.sensorsPerWall,
    },
    slotCount: slots.length,
    sensorSlotCount: slots.filter((s) => s.positionKind === 'SENSOR').length,
    // Owner critical topology correction: every logical position is a Product Sensor (I7 and I16
    // included). The invariant check at load time guarantees this count is 0.
    nonSensorGapSlotCount: slots.filter((s) => s.positionKind !== 'SENSOR').length,
    topologyInvariants: { ok: loadEvidence().topology.ok, authoritative: loadEvidence().topology.authoritative },
    walls: ['LEFT', 'REAR', 'RIGHT', 'FRONT'].map((wall) => ({
      wall,
      slots: slots.filter((s) => s.wall === wall).map((s) => ({
        slotId: s.slotId,
        positionKind: s.positionKind,
        logicalColumn: s.logicalColumn,
        logicalRow: s.logicalRow,
        wallColumn: s.wallColumn,
        wallRow: s.wallRow,
        sensorId: s.sensorId,
        // No equipment identity is forwarded: no Water Jet identity may ever be derived from a
        // Sensor wall position here.
        tcChannels: s.tcChannels ?? null,
      })),
    })),
    note: 'Public-safe synthetic example of the logical matrix. It is not a Production configuration.',
  };
}

/**
 * The six-Node TC configuration projection. It is derived ONLY from the canonical configuration and
 * its validation: no request input reaches it, and it names no file path, environment value, port,
 * protocol, credential or Production address. Its statuses are honest facts of this Stage.
 */
export function tcNodeConfigurationPayload() {
  const view = buildTcNodeConfigurationView(CANONICAL_TC_NODE_CONFIGURATION);
  const digest = (text) => createHash('sha256').update(text, 'utf8').digest('hex');
  const fingerprint = tcNodeConfigurationFingerprint(CANONICAL_TC_NODE_CONFIGURATION, digest);
  return {
    stageId: STAGE_ID,
    mode: PREVIEW_MODE,
    configurationId: TC_NODE_CONFIGURATION_ID,
    revision: { fingerprintSha256: fingerprint, shortRevision: fingerprint.slice(0, 12) },
    ...view,
  };
}

/**
 * The Stage 0.4C-K3 evidence projection. Derived ONLY from the canonical evidence set, the canonical
 * Node configuration and the committed schedule (for a record-to-record consistency check). No
 * request input reaches it; it names no file path, environment value, port value, protocol name or
 * credential, and no Node in it carries a live communication state. The Preview identity stays
 * STAGE_ID (0.4C-K2) by Owner rule; the evidence Stage is reported separately.
 */
export function tcAcquisitionEvidencePayload() {
  const view = buildTcAcquisitionEvidenceView(CANONICAL_TC_ACQUISITION_EVIDENCE);
  const digest = (text) => createHash('sha256').update(text, 'utf8').digest('hex');
  const fingerprint = tcAcquisitionEvidenceFingerprint(CANONICAL_TC_ACQUISITION_EVIDENCE, digest);
  const { schedule } = loadEvidence();
  // RECORD_TO_RECORD_CONSISTENCY_ONLY: two repository records compared; no range, start or offset is
  // produced and no derivation result is exposed while the process image is DERIVATION_BLOCKED.
  const consistency = recordToRecordConsistency(CANONICAL_TC_ACQUISITION_EVIDENCE, schedule.rows);
  return {
    stageId: STAGE_ID,
    mode: PREVIEW_MODE,
    revision: { fingerprintSha256: fingerprint, shortRevision: fingerprint.slice(0, 12) },
    recordToRecordConsistency: consistency,
    ...view,
  };
}

const record = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);

/**
 * Reads the Runtime host's fixed loopback Snapshot route and, only if its TC state is null,
 * the fixed Runtime status route for a same-revision pending/failure diagnostic. There is no
 * acquisition client, retry, fallback or successful state construction in this process.
 * Transport errors and malformed answers are explicit states, never substituted values.
 */
export async function tcAcquisitionPayload({ runtimeFetch = globalThis.fetch,
  runtimePort = process.env.WJSS_API_PORT ?? DEFAULT_RUNTIME_PORT } = {}) {
  const port = String(runtimePort);
  if (!/^\d+$/.test(port) || Number(port) < 1 || Number(port) > 65535) {
    throw new RangeError('The local Runtime API port must be an integer from 1 through 65535.');
  }
  const origin = new URL(LOOPBACK_ORIGIN);
  origin.port = port;
  const url = new URL('/api/v1/snapshot', origin);
  const envelope = (httpStatus, status, revision = null, tcAcquisition = null) => ({
    status: httpStatus, body: { stageId: STAGE_ID, mode: PREVIEW_MODE, status, revision, tcAcquisition },
  });

  let response;
  try {
    response = await runtimeFetch(url.href, {
      method: 'GET', headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(1200),
    });
  } catch {
    return envelope(503, 'RUNTIME_UNAVAILABLE');
  }
  if (!response?.ok) return envelope(503, 'RUNTIME_UNAVAILABLE');

  let snapshot;
  try { snapshot = await response.json(); }
  catch { return envelope(502, 'INVALID_RUNTIME_RESPONSE'); }
  if (!record(snapshot) || snapshot.kind !== 'snapshot' || snapshot.schema !== 'wjss.snapshot/2'
    || snapshot.apiVersion !== 1 || !Number.isSafeInteger(snapshot.revision) || snapshot.revision < 1
    || !Object.hasOwn(snapshot, 'tcAcquisition')) return envelope(502, 'INVALID_RUNTIME_RESPONSE');
  if (snapshot.tcAcquisition === null) {
    // A null Snapshot distinguishes no TC state from a published read, but not pending from
    // failed. The existing Runtime status carries that host-owned diagnostic. Refuse a mismatched
    // revision (a tick raced these two reads); never invent a result from a stale status answer.
    let statusResponse;
    try {
      statusResponse = await runtimeFetch(new URL('/api/v1/runtime', origin).href, {
        method: 'GET', headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(1200),
      });
    } catch { return envelope(503, 'RUNTIME_UNAVAILABLE'); }
    if (!statusResponse?.ok) return envelope(503, 'RUNTIME_UNAVAILABLE');
    let runtimeStatus;
    try { runtimeStatus = await statusResponse.json(); }
    catch { return envelope(502, 'INVALID_RUNTIME_RESPONSE'); }
    // Either the deterministic SIMULATOR run or the explicit one-Node physical READ-ONLY run is
    // acceptable here, and the reported profile must be the one the Runtime actually reports: the
    // projection never relabels a physical run as SIMULATOR or the reverse. An unrecognised profile
    // is refused rather than echoed.
    const knownProfile = runtimeStatus?.profile === 'SIMULATOR'
      || runtimeStatus?.profile === 'TEST_HARDWARE_READ_ONLY';
    if (!record(runtimeStatus) || runtimeStatus.currentRevision !== snapshot.revision
      || !knownProfile || runtimeStatus.ready !== false) {
      return envelope(502, 'INVALID_RUNTIME_RESPONSE');
    }
    if (runtimeStatus.readinessCode === 'TC_ACQUISITION_UNAVAILABLE')
      return envelope(200, 'ACQUISITION_FAILED', snapshot.revision);
    if (runtimeStatus.readinessCode === 'TC_ACQUISITION_NOT_PUBLISHED')
      return envelope(200, 'NOT_PUBLISHED', snapshot.revision);
    return envelope(503, 'RUNTIME_UNAVAILABLE');
  }
  if (!validTcAcquisition(snapshot.tcAcquisition)) return envelope(502, 'INVALID_RUNTIME_RESPONSE');

  const tc = snapshot.tcAcquisition;
  if (tc.physical) return physicalAcquisitionEnvelope(envelope, snapshot, tc);

  return envelope(200, 'PUBLISHED', snapshot.revision, {
    node: tc.nodeId,
    cabinet: tc.cabinetId,
    provenance: tc.provenance,
    physical: tc.physical,
    quarantine: tc.quarantined,
    acquisitionState: tc.acquisitionState,
    acquisitionTimestampUtc: tc.acquiredAtUtc,
    copiedWordCount: tc.copiedWordCount,
    words: tc.rawWords.slice(),
    quality: tc.quality,
    diagnosticState: tc.diagnosticState,
    mappingStatus: tc.mappingStatus,
    decodeStatus: tc.decodeStatus,
    notVerified: tc.notVerified.slice(),
  });
}

/**
 * Presentation-only projection of a published PHYSICAL quarantined record for the one-Node slice.
 *
 * Every value below is CANDIDATE evidence. The projection therefore carries the honest labels with
 * the numbers: PHYSICAL, QUARANTINED, DECODE CANDIDATE and NOT COMMISSIONED travel together, and the
 * legacy sentinel candidates (0x7FFF, 0xF830) never present as ordinary temperatures because
 * `projectCab1Payload` classifies them before any numeric decode. The endpoint is not represented in
 * the record and is not represented here; a withheld value is reported as unavailable, never as a
 * stale or fabricated number.
 */
function physicalAcquisitionEnvelope(envelope, snapshot, tc) {
  const { schedule } = loadEvidence();
  const mapping = buildCab1Mapping(schedule.rows);
  const projection = projectCab1Payload(mapping, tc.rawWords);

  const sensors = projection.sensors.map((sensor) => {
    const front = projection.measurements.find((m) => m.offset === sensor.frontOffset);
    const rear = projection.measurements.find((m) => m.offset === sensor.rearOffset);
    return {
      sensorId: sensor.sensorId,
      front: { offset: front.offset, ...decodeView(front) },
      rear: { offset: rear.offset, ...decodeView(rear) },
      drawingConflict: sensor.drawingConflict,
      mappingEvidenceState: sensor.mappingEvidenceState,
      // A paired delta is only meaningful when BOTH sides carry an ordinary candidate number; the
      // legacy sentinels have no numeric meaning, so no delta is fabricated for them.
      frontRearDeltaC: (typeof front.candidateValue === 'number' && typeof rear.candidateValue === 'number')
        ? Number((front.candidateValue - rear.candidateValue).toFixed(4))
        : null,
      deltaAvailable: typeof front.candidateValue === 'number' && typeof rear.candidateValue === 'number',
    };
  });

  return envelope(200, 'PUBLISHED', snapshot.revision, {
    node: tc.nodeId,
    cabinet: tc.cabinetId,
    provenance: tc.provenance,
    physical: true,
    quarantine: true,
    acquisitionState: tc.acquisitionState,
    acquisitionTimestampUtc: tc.acquiredAtUtc,
    copiedWordCount: tc.copiedWordCount,
    words: tc.rawWords.slice(),
    quality: tc.quality,
    diagnosticState: tc.diagnosticState,
    mappingStatus: tc.mappingStatus,
    decodeStatus: tc.decodeStatus,
    notVerified: tc.notVerified.slice(),
    // Presentational labels, so a consumer cannot render a candidate number without them.
    labels: ['PHYSICAL', 'QUARANTINED', 'DECODE CANDIDATE', 'NOT COMMISSIONED'],
    // The redacted identity only. There is no endpoint field in any TC response, by construction.
    endpointIdentity: tc.physicalEvidence?.endpointIdentity ?? null,
    commissioningState: tc.physicalEvidence?.commissioningState ?? null,
    mappingEvidenceState: tc.physicalEvidence?.mappingEvidenceState ?? null,
    readProfile: tc.physicalEvidence?.readProfile ? { ...tc.physicalEvidence.readProfile } : null,
    candidateDecode: {
      rule: 'unchecked((short)word) / 10.0',
      unit: 'degC',
      state: 'DECODE_CANDIDATE_NOT_VERIFIED',
      signedRepresentation: 'NOT_VERIFIED',
      byteOrder: 'NOT_VERIFIED',
      scaling: 'NOT_VERIFIED',
    },
    wordCount: projection.wordCount,
    sensorCount: sensors.length,
    measurementCount: projection.measurements.filter((m) => !m.spare).length,
    spareOffsetCount: projection.measurements.filter((m) => m.spare).length,
    sensors,
    spareOffsets: projection.measurements.filter((m) => m.spare).map((m) => m.offset),
    boundary: BOUNDARY,
  });
}

/**
 * One measurement as the browser sees it. `candidateValue` is null for the legacy sentinel
 * candidates: their manufacturer meaning is unverified, so their numeric field stays empty and their
 * legacy label is shown instead of a temperature.
 */
function decodeView(measurement) {
  return {
    moduleInstance: measurement.moduleInstance,
    moduleModel: measurement.moduleModel,
    channel: measurement.channel,
    sensorId: measurement.sensorId,
    side: measurement.side,
    sideLabel: measurement.sideLabel,
    rawWord: measurement.rawWord,
    rawHex: measurement.rawHex,
    decodeState: measurement.decodeState,
    label: measurement.label,
    candidateValue: measurement.candidateValue,
    candidateUnit: measurement.candidateUnit,
    manufacturerMeaning: measurement.manufacturerMeaning,
    presentAsOrdinaryTemperature: measurement.presentAsOrdinaryTemperature,
    mappingEvidenceState: measurement.mappingEvidenceState,
    drawingConflict: measurement.drawingConflict,
    rowState: measurement.rowState,
    spare: measurement.spare,
  };
}

function send(res, status, body, type, extra = {}) {
  res.writeHead(status, { ...SECURITY_HEADERS, 'Content-Type': type, ...extra });
  res.end(body);
}

/**
 * The canonical CAB1 Drawing-rev1 mapping projection, derived from the committed schedule. It carries
 * the read-profile SHAPE (function code, register area, port, unit, start, quantity) and never an
 * endpoint value: the Owner-local address is not represented in this process, this projection or any
 * response. Mapping-only output is useful precisely when no Runtime is running.
 */
export function tcPhysicalMappingPayload() {
  const { schedule } = loadEvidence();
  const mapping = buildCab1Mapping(schedule.rows);
  return {
    stageId: STAGE_ID,
    mode: PREVIEW_MODE,
    node: mapping.nodeId,
    cabinet: mapping.cabinetId,
    cabinetTag: mapping.cabinetTag,
    wordCount: mapping.wordCount,
    assignedOffsetCount: mapping.assignedOffsetCount,
    spareOffsetCount: mapping.spareOffsetCount,
    sensorCount: mapping.sensorCount,
    measurementCount: mapping.measurementCount,
    mappingEvidenceState: mapping.mappingEvidenceState,
    drawingConflictOffsets: mapping.drawingConflictOffsets.slice(),
    readProfile: { ...mapping.readProfile },
    sensors: mapping.sensors.map((s) => ({ ...s })),
    measurements: mapping.measurements.map((m) => ({
      offset: m.offset,
      moduleInstance: m.moduleInstance,
      moduleModel: m.moduleModel,
      channel: m.channel,
      sensorId: m.sensorId,
      side: m.side,
      sideLabel: m.sideLabel,
      spare: m.spare,
      rowState: m.rowState,
      drawingConflict: m.drawingConflict,
      mappingEvidenceState: m.mappingEvidenceState,
    })),
    boundary: BOUNDARY,
  };
}

/**
 * The read-only STATIC motion commissioning catalogue.
 *
 * One payload, one authority: the Owner-confirmed data the Application model publishes. It carries
 * the topology (four controllers, sixteen axes, the eight canonical Water Jet allocations), the
 * controller model, the Horizontal / Vertical axis roles with their physical axis letters, the brake
 * output identity, every configuration and commissioning status, the mapping provenance, the
 * recorded Drawing evidence notes, the candidate values and the authorization gate state.
 *
 * It is STATIC: composed from Owner-confirmed data alone. It therefore reads NO Runtime and contacts
 * NO controller, and it never depends on a device being reachable.
 *
 * It is deliberately NOT a member of the Runtime Snapshot and NOT a member of a Runtime Delta: the
 * Snapshot stays reserved for dynamic observed motion state (connection state, axis state, command /
 * encoder position, moving / stopped, Reverse / Home / Forward, brake commanded and observed, active
 * command lifecycle, fault and refusal, timestamps, quality), none of which is authorized in this
 * revision. Serving the catalogue from its own route keeps every Snapshot small and every revision
 * cheap instead of re-sending the same static content with them.
 *
 * Nothing here authorizes anything. The gate is CLOSED in this software revision and every motion
 * intent is refused before any hardware path is reached.
 */
export function motionCataloguePayload() {
  return {
    status: 200,
    body: {
      stageId: STAGE_ID,
      mode: PREVIEW_MODE,
      catalogue: motionCommissioningDesign(),
      // The refusal vocabulary travels with the catalogue, so the screen can never show a refusal as
      // a bare code with no wording.
      refusalReasons: [...MOTION_ALL_REFUSAL_REASONS],
      // Where the catalogue does and does not live. Stated in the payload, not only in a comment: a
      // consumer that expects the Snapshot to carry it is told, by the payload itself.
      placement: {
        route: '/api/motion-commissioning/catalogue',
        static: true,
        carriedBySnapshot: false,
        carriedByDelta: false,
        runtimeReadRequired: false,
      },
    },
  };
}

/**
 * Vocabulary of the H3-A Cleaning Job Workspace document, mirrored from
 * packages/contracts/TimeDelay.cs and packages/contracts/DispatchWorkspace.cs. It is used ONLY to
 * refuse a document this Preview cannot render truthfully: it is not a second decision authority,
 * and no delay, countdown or timeline value is ever derived here.
 */
const COUNTDOWN_LIFECYCLES = new Set(['NONE', 'PENDING_DECISION', 'ACTIVE_FROZEN',
  'COMPLETED_AWAITING_DISPATCH', 'DISPATCHED', 'CANCELLED', 'SUPERSEDED', 'FAULTED']);
const TIMELINE_STAGES = ['PRECHECK', 'MOVE_TO_P1', 'OPEN_IV', 'P2', 'P3', 'P4', 'P5', 'P6', 'CLOSE_IV',
  'MANDATORY_SAFE_RETURN', 'COMPLETE', 'NEXT_DISPATCH_COUNTDOWN', 'DISPATCHED'];
const TIMELINE_STATES = new Set(['PENDING', 'CURRENT', 'COMPLETE', 'FAILED']);
const DELAY_MODES = new Set(['MANUAL_FIXED', 'AUTO_RH_LINEAR_FROZEN']);
const BOILER_CONDITIONS = new Set(['VERY_CLEAN', 'NORMAL_CLEAN', 'MEDIUM_DIRTY', 'VERY_DIRTY']);
const DELAY_ZONES = new Set(['UPPER', 'LOWER']);
const QUALITIES = new Set(['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED']);

const isRecord = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
const isSafeNonNegative = (value) => Number.isSafeInteger(value) && value >= 0;
const isNonEmptyString = (value) => typeof value === 'string' && value.trim().length > 0;

function validRhInput(value) {
  return isRecord(value)
    && typeof value.rhTemperature === 'number' && Number.isFinite(value.rhTemperature)
    && QUALITIES.has(value.quality)
    && isNonEmptyString(value.observedAt)
    && Number.isSafeInteger(value.generation) && value.generation > 0;
}

function validDecision(value) {
  if (value === null) return true;
  if (!isRecord(value)) return false;
  const strings = ['decisionId', 'queueEntryId', 'nextSensorId', 'canonicalWaterJetId', 'effectiveWaterJetId',
    'effectiveWaterJetProvenance', 'isolationValveId', 'cabinetId', 'controllerId', 'horizontalAxisId',
    'verticalAxisId', 'configurationVersion', 'createdAt'];
  if (!strings.every((key) => isNonEmptyString(value[key]))) return false;
  if (!DELAY_MODES.has(value.mode) || !BOILER_CONDITIONS.has(value.boilerCondition)) return false;
  if (!DELAY_ZONES.has(value.zone)) return false;
  if (!isSafeNonNegative(value.countdownGeneration) || value.countdownGeneration < 1) return false;
  if (!isSafeNonNegative(value.queueGeneration)) return false;
  if (!isSafeNonNegative(value.nextSensorOrderWall) || value.nextSensorOrderWall < 1) return false;
  for (const key of ['manualDelaySeconds', 'rhInputWindowSeconds', 'autoDelayMinSeconds', 'autoDelayMaxSeconds',
    'baseDelaySeconds', 'appliedDelaySeconds']) {
    if (!isSafeNonNegative(value[key])) return false;
  }
  for (const key of ['rhTempMin', 'rhTempMax', 'zoneFactor']) {
    if (typeof value[key] !== 'number' || !Number.isFinite(value[key])) return false;
  }
  if (value.appliedDelaySeconds < 0 || value.baseDelaySeconds < 0 || value.zoneFactor < 0) return false;
  if (value.rhInput !== null && value.rhInput !== undefined && !validRhInput(value.rhInput)) return false;
  // MANUAL_FIXED freezes no RH reading and applies no zone factor: a document claiming otherwise is
  // refused rather than displayed as a truthful decision.
  if (value.mode === 'MANUAL_FIXED' && (value.rhInput !== null || value.zoneFactor !== 1)) return false;
  return true;
}

function validConfiguration(value) {
  if (value === null) return true;
  if (!isRecord(value)) return false;
  if (!DELAY_MODES.has(value.mode) || !BOILER_CONDITIONS.has(value.activeBoilerCondition)) return false;
  if (!isNonEmptyString(value.configurationVersion)) return false;
  if (!isSafeNonNegative(value.manualDelaySeconds) || !isSafeNonNegative(value.rhInputWindowSeconds)) return false;
  if (!(typeof value.rhTempMin === 'number' && Number.isFinite(value.rhTempMin))) return false;
  if (!(typeof value.rhTempMax === 'number' && Number.isFinite(value.rhTempMax))) return false;
  if (value.rhTempMin >= value.rhTempMax) return false;
  if (!Array.isArray(value.boilerRanges) || value.boilerRanges.length === 0) return false;
  return value.boilerRanges.every((range) => isRecord(range)
    && BOILER_CONDITIONS.has(range.condition)
    && isSafeNonNegative(range.autoDelayMinSeconds)
    && isSafeNonNegative(range.autoDelayMaxSeconds)
    && range.autoDelayMinSeconds <= range.autoDelayMaxSeconds);
}

function validPreview(value) {
  if (!isRecord(value)) return false;
  if (typeof value.available !== 'boolean' || typeof value.affectsExecution !== 'boolean') return false;
  if (value.affectsExecution !== false) return false;
  if (!DELAY_MODES.has(value.mode) || !DELAY_ZONES.has(value.zone)) return false;
  if (typeof value.zoneFactor !== 'number' || !Number.isFinite(value.zoneFactor) || value.zoneFactor < 0) return false;
  if (typeof value.refusalCode !== 'string' || value.refusalCode.length === 0) return false;
  if (value.boilerCondition !== null && value.boilerCondition !== undefined
    && !BOILER_CONDITIONS.has(value.boilerCondition)) return false;
  const optionalNumbers = ['rhTemperature', 'clampedRhTemperature', 'mappedDelaySeconds'];
  for (const key of optionalNumbers) {
    if (value[key] !== null && value[key] !== undefined
      && !(typeof value[key] === 'number' && Number.isFinite(value[key]))) return false;
  }
  for (const key of ['baseDelaySeconds', 'appliedDelaySeconds']) {
    if (value[key] !== null && value[key] !== undefined && !isSafeNonNegative(value[key])) return false;
  }
  return true;
}

function validTimeline(value) {
  if (!isRecord(value)) return false;
  if (!TIMELINE_STAGES.includes(value.stage)) return false;
  if (!Array.isArray(value.steps) || value.steps.length !== TIMELINE_STAGES.length) return false;
  let current = 0;
  for (let index = 0; index < value.steps.length; index += 1) {
    const step = value.steps[index];
    if (!isRecord(step)) return false;
    if (step.stage !== TIMELINE_STAGES[index]) return false;
    if (step.ordinal !== index + 1) return false;
    if (!isNonEmptyString(step.label)) return false;
    if (!TIMELINE_STATES.has(step.state)) return false;
    if (step.state === 'CURRENT' || step.state === 'FAILED') current += 1;
  }
  return current === 1;
}

/**
 * Strict validator of the Runtime's H3-A workspace document. A document this Preview cannot render
 * truthfully is refused as INVALID_RUNTIME_RESPONSE: nothing is repaired, defaulted or filled in.
 */
export function validDispatchWorkspace(doc) {
  if (!isRecord(doc)) return false;
  if (doc.kind !== 'dispatch-workspace' || doc.schema !== 'wjss.dispatch-workspace/1') return false;
  if (doc.apiVersion !== 1) return false;
  if (!Number.isSafeInteger(doc.revision) || doc.revision < 1) return false;
  if (!isNonEmptyString(doc.generatedAt) || !isNonEmptyString(doc.stageMarker)) return false;
  if (typeof doc.simulation !== 'string' || doc.simulation.length === 0) return false;
  if (!isRecord(doc.queue)) return false;
  if (doc.queue.capacity !== 8) return false;
  if (!Array.isArray(doc.queue.entries) || doc.queue.entries.length > 8) return false;
  if (doc.queue.totalQueued !== doc.queue.entries.length) return false;
  if (!isSafeNonNegative(doc.queue.revision)) return false;
  const entriesOk = doc.queue.entries.every((entry, index) => isRecord(entry)
    && entry.position === index + 1
    && isNonEmptyString(entry.entryId)
    && isNonEmptyString(entry.sensorId)
    && isNonEmptyString(entry.sourceReason)
    && isSafeNonNegative(entry.secondsSinceLastClean));
  if (!entriesOk) return false;
  if (doc.activeJob !== null && doc.activeJob !== undefined) {
    if (!isRecord(doc.activeJob) || !isNonEmptyString(doc.activeJob.jobId)
      || !isNonEmptyString(doc.activeJob.targetSensorId)) return false;
  }
  if (!isRecord(doc.countdown)) return false;
  if (!COUNTDOWN_LIFECYCLES.has(doc.countdown.lifecycle)) return false;
  if (!isSafeNonNegative(doc.countdown.generation)) return false;
  if (!isSafeNonNegative(doc.countdown.remainingSeconds)) return false;
  if (typeof doc.countdown.frozen !== 'boolean' || typeof doc.countdown.dispatchAdmissible !== 'boolean') return false;
  if (!validDecision(doc.countdown.decision ?? null)) return false;
  if (doc.countdown.frozen && !doc.countdown.decision) return false;
  if (doc.countdown.dispatchAdmissible && doc.countdown.remainingSeconds !== 0) return false;
  if (!validTimeline(doc.timeline)) return false;
  if (!validConfiguration(doc.configuration ?? null)) return false;
  return validPreview(doc.preview);
}

/**
 * Reads the Runtime host's read-only Cleaning Job Workspace route. The Preview performs ONE GET and
 * never polls, retries or caches: a transport error, a malformed answer or an unrecognized document
 * is an explicit state, never substituted data and never a fabricated countdown.
 */
export async function dispatchWorkspacePayload({ runtimeFetch = globalThis.fetch,
  runtimePort = process.env.WJSS_API_PORT ?? DEFAULT_RUNTIME_PORT } = {}) {
  const port = String(runtimePort);
  if (!/^\d+$/.test(port) || Number(port) < 1 || Number(port) > 65535) {
    throw new RangeError('The local Runtime API port must be an integer from 1 through 65535.');
  }
  const origin = new URL(LOOPBACK_ORIGIN);
  origin.port = port;
  const envelope = (httpStatus, status, revision = null, workspace = null) => ({
    status: httpStatus,
    body: { stageId: STAGE_ID, mode: PREVIEW_MODE, status, revision, workspace },
  });

  let response;
  try {
    response = await runtimeFetch(new URL('/api/v1/dispatch-workspace', origin).href, {
      method: 'GET', headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(1200),
    });
  } catch {
    return envelope(503, 'RUNTIME_UNAVAILABLE');
  }
  if (!response?.ok) return envelope(503, 'RUNTIME_UNAVAILABLE');

  let document;
  try { document = await response.json(); }
  catch { return envelope(502, 'INVALID_RUNTIME_RESPONSE'); }
  if (!validDispatchWorkspace(document)) return envelope(502, 'INVALID_RUNTIME_RESPONSE');

  return envelope(200, 'PUBLISHED', document.revision, document);
}

export function createPreviewServer({ runtimeFetch = globalThis.fetch,
  runtimePort = process.env.WJSS_API_PORT ?? DEFAULT_RUNTIME_PORT } = {}) {
  return createServer(async (req, res) => {
    if (req.method !== 'GET' && req.method !== 'HEAD') {
      return send(res, 405, 'method not allowed', 'text/plain; charset=utf-8', { Allow: 'GET, HEAD' });
    }
    const url = new URL(req.url ?? '/', LOOPBACK_ORIGIN);
    const path = decodeURIComponent(url.pathname);
    try {
      if (path === '/' || path === '/index.html') {
        return send(res, 200, readFileSync(join(PUBLIC_DIR, 'index.html')), MIME['.html']);
      }
      if (path.startsWith('/pkg/')) {
        const name = path.slice('/pkg/'.length);
        if (!BROWSER_MODULES.has(name)) return send(res, 404, 'not found', 'text/plain; charset=utf-8');
        return send(res, 200, readFileSync(join(PKG_SRC, name)), MIME[extname(name)]);
      }
      if (path === '/api/health') {
        return send(res, 200, JSON.stringify({ status: 'ok', stageId: STAGE_ID, mode: PREVIEW_MODE, hardware: false }), MIME['.json']);
      }
      if (path === '/api/boundary') {
        return send(res, 200, JSON.stringify(BOUNDARY), MIME['.json']);
      }
      if (path === '/api/commissioning/schedule') {
        return send(res, 200, JSON.stringify(schedulePayload()), MIME['.json']);
      }
      if (path === '/api/commissioning/scenarios') {
        return send(res, 200, JSON.stringify({ scenarios: scenarioCatalogue() }), MIME['.json']);
      }
      if (path === '/api/commissioning/observations') {
        const { status, body } = observationsPayload(url);
        return send(res, status, JSON.stringify(body), MIME['.json']);
      }
      if (path === '/api/operations/summary') {
        const { schedule } = loadEvidence();
        const observations = buildObservations(schedule, { scenarioKey: 'NORMAL_GOOD', tick: 0 });
        return send(res, 200, JSON.stringify({
          stageId: STAGE_ID,
          mode: PREVIEW_MODE,
          nodeSummary: nodeCommunicationSummary(schedule, observations),
          commissioningProgress: commissioningProgress(schedule, null),
          boundary: BOUNDARY,
        }), MIME['.json']);
      }
      if (path === '/api/sensor-map') {
        return send(res, 200, JSON.stringify(sensorMapPayload()), MIME['.json']);
      }
      if (path === '/api/tc-node-configuration') {
        return send(res, 200, JSON.stringify(tcNodeConfigurationPayload()), MIME['.json']);
      }
      if (path === '/api/tc-acquisition-evidence') {
        return send(res, 200, JSON.stringify(tcAcquisitionEvidencePayload()), MIME['.json']);
      }
      if (path === '/api/tc-physical-mapping') {
        return send(res, 200, JSON.stringify(tcPhysicalMappingPayload()), MIME['.json']);
      }
      if (path === '/api/operations/dispatch') {
        const { status, body } = await dispatchWorkspacePayload({ runtimeFetch, runtimePort });
        return send(res, status, JSON.stringify(body), MIME['.json']);
      }
      if (path === '/api/tc-acquisition') {
        const { status, body } = await tcAcquisitionPayload({ runtimeFetch, runtimePort });
        return send(res, status, JSON.stringify(body), MIME['.json']);
      }
      if (path === '/api/motion-commissioning/catalogue') {
        // The static catalogue route: no Runtime read, no query parameter, no dynamic state. The
        // Snapshot is reserved for dynamic observed motion state, none of which is authorized yet.
        const { status, body } = motionCataloguePayload();
        return send(res, status, JSON.stringify(body), MIME['.json']);
      }
      const asset = path.slice(1);
      if (PUBLIC_FILES.has(asset)) {
        return send(res, 200, readFileSync(join(PUBLIC_DIR, asset)), MIME[extname(asset)]);
      }
      return send(res, 404, 'not found', 'text/plain; charset=utf-8');
    } catch (err) {
      // Never echo internal detail to the browser; the local console keeps it.
      console.error('ops-preview:', err.message);
      return send(res, 500, 'preview data unavailable', 'text/plain; charset=utf-8');
    }
  });
}

if (process.argv[1] && import.meta.url === new URL(`file://${process.argv[1]}`).href) {
  const port = Number(process.env.OPS_PREVIEW_PORT ?? DEFAULT_PORT);
  const server = createPreviewServer();
  server.listen(port, UI_BIND_HOST, () => {
    const bound = server.address();
    console.log(`Kiosk Preview (${PREVIEW_MODE}, stage ${STAGE_ID}) listening on ${bound.address}:${bound.port}`);
    console.log('SIMULATION / PREVIEW - NO HARDWARE ACCESS - NO DEVICE WRITE - FIELD VERIFICATION PENDING');
  });
}
