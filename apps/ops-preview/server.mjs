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
} from '../../packages/commissioning/src/index.mjs';

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

function send(res, status, body, type, extra = {}) {
  res.writeHead(status, { ...SECURITY_HEADERS, 'Content-Type': type, ...extra });
  res.end(body);
}

export function createPreviewServer() {
  return createServer((req, res) => {
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
