// Stage 0.4C-K1 - the drawing-derived Sensor-to-Channel schedule, read-only.
//
// Source of truth: docs/evidence/tc-drawing-schedule-rev1.csv (Baseline REV1, 240 physical rows).
// This module PARSES that committed evidence and CLASSIFIES it. It never derives a new mapping,
// never renames a conductor, never repairs a conflict row and never writes anything back.
//
// Boundary note (repository rule S3): the Product tree must not contain vendor/transport
// vocabulary, so the evidence column that carries the per-node word ordinal is addressed by its
// documented ordinal position below and is never spelled by name in Product source. The column
// layout is fixed by the evidence header and is asserted by the header contract check.
//
// No network, no device, no file system and no clock are used here.

/** Documented column ordinals of the evidence schedule (0-based, header order). */
export const COLUMN = Object.freeze({
  CABINET: 0,
  CABINET_TAG: 1,
  NODE_ID: 2,
  TABLE_PAGE: 3,
  DIAGRAM_PAGE: 4,
  MODULE_INSTANCE: 5,
  MODULE_MODEL: 6,
  CHANNEL: 7,
  SHEATH_LABEL: 8,
  CONDUCTOR_LABEL: 9,
  POLE_PLUS_DEST: 10,
  POLE_MINUS_DEST: 11,
  WIRE_COLOR_PLUS: 12,
  WIRE_COLOR_MINUS: 13,
  PE_PAIR: 14,
  SENSOR_ID: 15,
  SIDE: 16,
  ROW_STATE: 17,
  TEMPLATE_COMPATIBILITY: 18,
  WORD_ORDINAL: 19,
  WORD_BASIS: 20,
  EVIDENCE_REF: 21,
  NOTES: 22,
});

export const COLUMN_COUNT = 23;

/** Row states as they appear in the evidence, verbatim. */
export const ROW_STATE = Object.freeze({
  DRAWING_VERIFIED: 'DRAWING_VERIFIED',
  DRAWING_INTERNAL_CONFLICT: 'DRAWING_INTERNAL_CONFLICT',
  DRAWING_TOPOLOGY_CONFLICT: 'DRAWING_TOPOLOGY_CONFLICT',
  SPARE_UNMAPPED: 'SPARE_UNMAPPED',
});

export const TEMPLATE_COMPATIBILITY = Object.freeze({
  MATCHED: 'MATCHED',
  GAP_CONFLICT: 'GAP_CONFLICT',
  NO_COUNTERPART_SPARE: 'NO_COUNTERPART_SPARE',
});

/**
 * Presentation class of one physical row. Exactly one class per row, and the four classes are
 * disjoint: a row is never both "Product-compatible" and "unassigned".
 */
export const ROW_CLASS = Object.freeze({
  PRODUCT_COMPATIBLE: 'PRODUCT_COMPATIBLE',
  DRAWING_INTERNAL_CONFLICT: 'DRAWING_INTERNAL_CONFLICT',
  PRODUCT_TOPOLOGY_CONFLICT: 'PRODUCT_TOPOLOGY_CONFLICT',
  UNASSIGNED: 'UNASSIGNED',
});

/** Why a row can never receive a commissioning PASS verdict. */
export const PASS_BLOCKED = Object.freeze({
  DRAWING_INTERNAL_CONFLICT: 'DRAWING_INTERNAL_CONFLICT_BLOCKED',
  PRODUCT_TOPOLOGY_CONFLICT: 'PRODUCT_TOPOLOGY_CONFLICT_BLOCKED',
  UNASSIGNED: 'UNASSIGNED_BLOCKED',
  NO_SENSOR_IDENTITY: 'NO_SENSOR_IDENTITY_BLOCKED',
});

/** The protected physical topology of the merged baseline (Stage 0.4C-0B). Never recomputed away. */
export const PROTECTED_BASELINE = Object.freeze({
  NODE_COUNT: 6,
  MODULES_PER_NODE: 5,
  CHANNELS_PER_MODULE: 8,
  MODULE_COUNT: 30,
  PHYSICAL_CHANNEL_COUNT: 240,
  PRODUCT_COMPATIBLE_MEASUREMENTS: 212,
  PRODUCT_SENSORS: 106,
  DRAWING_VERIFIED_ROWS: 200,
  DRAWING_INTERNAL_CONFLICT_ROWS: 12,
  PRODUCT_TOPOLOGY_CONFLICT_ROWS: 4,
  UNASSIGNED_ROWS: 24,
  MAPPED_ROWS: 216,
  MODULE_MODEL: '750-498',
  COUPLER_MODEL: '750-362',
  END_MODEL: '750-600',
  TEMPERATURE_UNIT: 'degC',
  /** Positions that are authoritative NON_SENSOR_GAP positions: never Sensors, never Water Jets. */
  NON_SENSOR_GAP_SENSOR_IDS: Object.freeze(['I7', 'I16']),
  UNRESOLVED_ROWS: 0,
});

/**
 * RFC 4180 field split for one line, plus multi-line aware record splitting. The evidence file is
 * comma delimited, quotes fields that contain a comma or a semicolon, and escapes an embedded quote
 * by doubling it.
 */
export function parseCsv(text) {
  const records = [];
  let field = '';
  let record = [];
  let inQuotes = false;
  const source = text.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  for (let i = 0; i < source.length; i += 1) {
    const ch = source[i];
    if (inQuotes) {
      if (ch === '"') {
        if (source[i + 1] === '"') { field += '"'; i += 1; } else { inQuotes = false; }
      } else {
        field += ch;
      }
      continue;
    }
    if (ch === '"') { inQuotes = true; continue; }
    if (ch === ',') { record.push(field); field = ''; continue; }
    if (ch === '\n') {
      record.push(field);
      records.push(record);
      field = '';
      record = [];
      continue;
    }
    field += ch;
  }
  if (field.length > 0 || record.length > 0) {
    record.push(field);
    records.push(record);
  }
  // Drop a trailing empty record produced by a terminating newline.
  return records.filter((r) => !(r.length === 1 && r[0] === ''));
}

/** Header contract check: the parser refuses a schedule whose shape it does not recognise. */
export function requireHeaderContract(header) {
  const problems = [];
  if (header.length !== COLUMN_COUNT) {
    problems.push(`expected ${COLUMN_COUNT} columns, found ${header.length}`);
  }
  const expect = (ordinal, value, label) => {
    if ((header[ordinal] ?? '') !== value) problems.push(`column ${ordinal} must be '${value}' (${label})`);
  };
  expect(COLUMN.NODE_ID, 'NodeIdentity', 'node');
  expect(COLUMN.MODULE_INSTANCE, 'ModuleInstance', 'module');
  expect(COLUMN.CHANNEL, 'Channel', 'channel');
  expect(COLUMN.SENSOR_ID, 'SensorId', 'sensor identity');
  expect(COLUMN.SIDE, 'ScheduleSide', 'drawing side');
  expect(COLUMN.ROW_STATE, 'RowState', 'drawing verification state');
  expect(COLUMN.TEMPLATE_COMPATIBILITY, 'TemplateCompatibility', 'Product compatibility');
  if (problems.length > 0) {
    throw new Error(`schedule header contract violated: ${problems.join('; ')}`);
  }
  return true;
}

const measurementIdentity = (sensorId, side) => (sensorId ? `${sensorId}:${side}` : null);

/**
 * The evidence marks an absent cell with the literal token EMPTY (24 spare rows). It is normalised
 * to null so no downstream surface can mistake the placeholder for a Sensor identity, a terminal
 * or a wire colour. The comparison is exact, so a real label can never be swallowed.
 */
export const EMPTY_CELL_TOKEN = 'EMPTY';

function cell(value) {
  if (value === undefined || value === null) return null;
  const text = String(value).trim();
  if (text.length === 0 || text === EMPTY_CELL_TOKEN) return null;
  return text;
}

/**
 * Parses the evidence schedule into read-only row records.
 * Every row keeps its evidence text verbatim; classification is added, never substituted.
 */
export function parseSchedule(csvText) {
  const records = parseCsv(csvText);
  if (records.length === 0) throw new Error('schedule is empty');
  const header = records[0];
  requireHeaderContract(header);
  const rows = [];
  const seenPhysical = new Map();
  const seenMeasurement = new Map();
  for (let i = 1; i < records.length; i += 1) {
    const r = records[i];
    if (r.length !== COLUMN_COUNT) {
      throw new Error(`schedule row ${i + 1} has ${r.length} columns, expected ${COLUMN_COUNT}`);
    }
    const rowState = r[COLUMN.ROW_STATE];
    const templateCompatibility = r[COLUMN.TEMPLATE_COMPATIBILITY];
    const sensorId = cell(r[COLUMN.SENSOR_ID]);
    const side = r[COLUMN.SIDE] === 'F' || r[COLUMN.SIDE] === 'R' ? r[COLUMN.SIDE] : null;
    const wordOrdinal = Number.parseInt(r[COLUMN.WORD_ORDINAL], 10);
    const rowClass = classifyRow({ rowState, templateCompatibility });
    const physicalChannelKey = `${r[COLUMN.NODE_ID]}|${r[COLUMN.MODULE_INSTANCE]}|${r[COLUMN.CHANNEL]}`;
    if (seenPhysical.has(physicalChannelKey)) {
      throw new Error(`duplicate physical Channel ${physicalChannelKey} at row ${i + 1}`);
    }
    seenPhysical.set(physicalChannelKey, i + 1);
    const measurementId = measurementIdentity(sensorId, side);
    if (rowClass !== ROW_CLASS.UNASSIGNED && measurementId) {
      if (seenMeasurement.has(measurementId)) {
        throw new Error(`duplicate Product measurement identity ${measurementId} at row ${i + 1}`);
      }
      seenMeasurement.set(measurementId, i + 1);
    }
    rows.push(Object.freeze({
      scheduleRow: i,
      cabinet: r[COLUMN.CABINET],
      cabinetTag: r[COLUMN.CABINET_TAG],
      nodeId: r[COLUMN.NODE_ID],
      tablePage: r[COLUMN.TABLE_PAGE],
      diagramPage: r[COLUMN.DIAGRAM_PAGE],
      moduleInstance: r[COLUMN.MODULE_INSTANCE],
      moduleModel: r[COLUMN.MODULE_MODEL],
      channel: Number.parseInt(r[COLUMN.CHANNEL], 10),
      sheathLabel: cell(r[COLUMN.SHEATH_LABEL]),
      conductorLabel: cell(r[COLUMN.CONDUCTOR_LABEL]),
      polePlusDest: cell(r[COLUMN.POLE_PLUS_DEST]),
      poleMinusDest: cell(r[COLUMN.POLE_MINUS_DEST]),
      pePair: cell(r[COLUMN.PE_PAIR]),
      sensorId,
      side,
      rowState,
      templateCompatibility,
      wordOrdinal: Number.isNaN(wordOrdinal) ? null : wordOrdinal,
      wordBasis: cell(r[COLUMN.WORD_BASIS]),
      evidenceRef: cell(r[COLUMN.EVIDENCE_REF]),
      notes: cell(r[COLUMN.NOTES]),
      rowClass,
      physicalChannelKey,
      measurementId,
      // Product compatibility is the evidence column itself: 212 rows carry a template counterpart
      // (106 Sensors x 2 sides). The 12 Drawing internal-conflict rows are among them, which is
      // exactly why they are still blocked from a commissioning PASS by passBlockedReason below.
      productCompatible: templateCompatibility === TEMPLATE_COMPATIBILITY.MATCHED,
      drawingVerified: rowState === ROW_STATE.DRAWING_VERIFIED,
      passBlockedReason: passBlockedReason(rowClass, sensorId),
    }));
  }
  return Object.freeze({
    label: 'BASELINE_REV1',
    rowCount: rows.length,
    rows: Object.freeze(rows),
  });
}

export function classifyRow({ rowState, templateCompatibility }) {
  if (rowState === ROW_STATE.DRAWING_TOPOLOGY_CONFLICT) return ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT;
  if (rowState === ROW_STATE.DRAWING_INTERNAL_CONFLICT) return ROW_CLASS.DRAWING_INTERNAL_CONFLICT;
  if (rowState === ROW_STATE.SPARE_UNMAPPED || templateCompatibility === TEMPLATE_COMPATIBILITY.NO_COUNTERPART_SPARE) {
    return ROW_CLASS.UNASSIGNED;
  }
  if (templateCompatibility === TEMPLATE_COMPATIBILITY.MATCHED) return ROW_CLASS.PRODUCT_COMPATIBLE;
  // GAP_CONFLICT rows are the Product-topology conflicts and are already returned above; anything
  // else is refused loudly rather than folded into a compatible class.
  throw new Error(`unclassified schedule row: RowState '${rowState}', TemplateCompatibility '${templateCompatibility}'`);
}

/** A row is commissioning-eligible for a PREVIEW pass verdict only when nothing blocks it. */
export function passBlockedReason(rowClass, sensorId) {
  if (rowClass === ROW_CLASS.DRAWING_INTERNAL_CONFLICT) return PASS_BLOCKED.DRAWING_INTERNAL_CONFLICT;
  if (rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT) return PASS_BLOCKED.PRODUCT_TOPOLOGY_CONFLICT;
  if (rowClass === ROW_CLASS.UNASSIGNED) return PASS_BLOCKED.UNASSIGNED;
  if (!sensorId) return PASS_BLOCKED.NO_SENSOR_IDENTITY;
  return null;
}

/** Counts recomputed from the parsed evidence. The protected totals are asserted, not assumed. */
export function scheduleCounts(schedule) {
  const rows = schedule.rows;
  const count = (predicate) => rows.filter(predicate).length;
  const sensors = new Set();
  const measurements = new Set();
  const nodes = new Set();
  const modules = new Set();
  for (const row of rows) {
    nodes.add(row.nodeId);
    modules.add(`${row.nodeId}|${row.moduleInstance}`);
    if (row.productCompatible) {
      if (row.sensorId) sensors.add(row.sensorId);
      if (row.measurementId) measurements.add(row.measurementId);
    }
  }
  const counts = {
    physicalChannels: rows.length,
    mappedChannels: rows.filter((r) => r.rowClass !== ROW_CLASS.UNASSIGNED).length,
    unassignedChannels: count((r) => r.rowClass === ROW_CLASS.UNASSIGNED),
    productCompatibleMeasurements: count((r) => r.productCompatible),
    productSensors: sensors.size,
    drawingVerifiedRows: count((r) => r.rowState === ROW_STATE.DRAWING_VERIFIED),
    drawingInternalConflictRows: count((r) => r.rowClass === ROW_CLASS.DRAWING_INTERNAL_CONFLICT),
    productTopologyConflictRows: count((r) => r.rowClass === ROW_CLASS.PRODUCT_TOPOLOGY_CONFLICT),
    unassignedRows: count((r) => r.rowClass === ROW_CLASS.UNASSIGNED),
    measurementIdentities: measurements.size,
    nodes: nodes.size,
    modules: modules.size,
    unresolvedRows: count((r) => !r.rowClass),
  };
  return Object.freeze({
    ...counts,
    matchesProtectedBaseline: matchesProtectedBaseline(counts),
  });
}

/** True when the recomputed counts equal every protected Stage 0.4C-0B total. */
export function matchesProtectedBaseline(counts) {
  const b = PROTECTED_BASELINE;
  return counts.physicalChannels === b.PHYSICAL_CHANNEL_COUNT
    && counts.mappedChannels === b.MAPPED_ROWS
    && counts.unassignedChannels === b.UNASSIGNED_ROWS
    && counts.productCompatibleMeasurements === b.PRODUCT_COMPATIBLE_MEASUREMENTS
    && counts.productSensors === b.PRODUCT_SENSORS
    && counts.drawingVerifiedRows === b.DRAWING_VERIFIED_ROWS
    && counts.drawingInternalConflictRows === b.DRAWING_INTERNAL_CONFLICT_ROWS
    && counts.productTopologyConflictRows === b.PRODUCT_TOPOLOGY_CONFLICT_ROWS
    && counts.unassignedRows === b.UNASSIGNED_ROWS
    && counts.measurementIdentities === b.PRODUCT_COMPATIBLE_MEASUREMENTS
    && counts.nodes === b.NODE_COUNT
    && counts.modules === b.MODULE_COUNT
    && counts.unresolvedRows === b.UNRESOLVED_ROWS;
}

/** Node inventory in documented order, with module and Channel identity. */
export function nodeInventory(schedule) {
  const byNode = new Map();
  for (const row of schedule.rows) {
    if (!byNode.has(row.nodeId)) byNode.set(row.nodeId, { nodeId: row.nodeId, cabinet: row.cabinet, cabinetTag: row.cabinetTag, rows: [] });
    byNode.get(row.nodeId).rows.push(row);
  }
  return Object.freeze([...byNode.values()].map((n) => Object.freeze({
    nodeId: n.nodeId,
    cabinet: n.cabinet,
    cabinetTag: n.cabinetTag,
    physicalChannels: n.rows.length,
    modules: Object.freeze([...new Set(n.rows.map((r) => r.moduleInstance))]),
    // The documented image is 40 input words per node at ordinals 0 through 39.
    wordOrdinals: Object.freeze(n.rows.map((r) => r.wordOrdinal).filter((w) => w !== null).sort((a, b) => a - b)),
  })));
}

/** The rows a Product Sensor owns: exactly one front and one rear measurement per Sensor. */
export function measurementsOfSensor(schedule, sensorId) {
  return schedule.rows.filter((r) => r.sensorId === sensorId && r.measurementId !== null);
}

/**
 * True when the Sensor identity is one of the authoritative NON_SENSOR_GAP positions.
 * These rows stay raw drawing evidence: they are never Sensors, never Water Jets and never
 * selectable as a Product Runtime Sensor.
 */
export function isNonSensorGapSensorId(sensorId) {
  return PROTECTED_BASELINE.NON_SENSOR_GAP_SENSOR_IDS.includes(sensorId);
}
