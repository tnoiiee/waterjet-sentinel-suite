// One-Node Physical TC Read-Only Product Integration — the canonical CAB1 mapping projection and
// the CANDIDATE decode rule for a physical read.
//
// This module is the ONE source of the CAB1 measurement projection. It is DERIVED from the committed
// Drawing rev1 schedule through parseSchedule: there is no second hand-maintained mapping table, so
// the schedule remains the single evidence source and a hand-typed offset cannot appear.
//
// Boundary: configuration and presentation evidence only. This module opens no socket, probes no
// address, contacts no device, reads no process image and holds no control authority. It contains NO
// network address of any kind — not a field coupler address, not a workstation address, not a
// retired legacy configuration-field value, and not a plant subnet. The Owner-local endpoint is
// confidential deployment information and is never represented here.
//
// Evidence honesty: every decoding statement below is a CANDIDATE. The committed schedule records a
// DRAWING_INTERNAL_CONFLICT state on some CAB1 rows and that state is preserved verbatim; a conflict
// row is never promoted to fully verified. The 0x7FFF and 0xF830 words are LEGACY candidates whose
// manufacturer meaning is NOT VERIFIED. Nothing here is a commissioned or accepted value.

/** The one-Node logical identity of this slice. An identity, never an address. */
export const CAB1_NODE_ID = 'TC-NODE-01';
export const CAB1_CABINET_ID = 'CAB1';
export const CAB1_CABINET_TAG = '+SDSCAB1';

/** The canonical physical image of one Node: five 8-channel modules, 40 register words. */
export const CAB1_MODULE_COUNT = 5;
export const CAB1_CHANNELS_PER_MODULE = 8;
export const CAB1_WORD_COUNT = 40;
export const CAB1_ASSIGNED_OFFSETS = 36;
export const CAB1_SPARE_OFFSETS = 4;
export const CAB1_SENSOR_COUNT = 18;
export const CAB1_MEASUREMENT_COUNT = 36;

/** The authorized read profile shape. Public-safe: it carries no endpoint. */
export const PHYSICAL_READ_PROFILE = Object.freeze({
  functionCode: 'FC03',
  functionLabel: 'Read Holding Registers',
  registerArea: 'HOLDING_REGISTERS',
  port: 502,
  unitIdentifier: 1,
  startAddress: 0,
  quantity: CAB1_WORD_COUNT,
  registerWidthBits: 16,
  registersPerChannel: 1,
  registersPerSensor: 2,
  addressConvention: 'ZERO_BASED',
});

/**
 * Candidate decode states. A decoded number is a CANDIDATE, never a verified measurement: the
 * scaling, the signed representation, the byte order and the sentinel meanings are all unverified
 * until physical evidence is recorded under a separate authorization.
 */
export const CANDIDATE_DECODE_STATE = Object.freeze({
  CANDIDATE_VALUE: 'DECODE_CANDIDATE_VALUE',
  LEGACY_WIRE_BREAK: 'DECODE_LEGACY_WIRE_BREAK_CANDIDATE',
  LEGACY_REVERSE_POLARITY: 'DECODE_LEGACY_RV_P_CANDIDATE',
});

export const CANDIDATE_DECODE_STATE_VALUES = Object.freeze(Object.values(CANDIDATE_DECODE_STATE));

/** Legacy sentinel candidates supplied by the Owner. Manufacturer meaning remains NOT VERIFIED. */
export const LEGACY_WIRE_BREAK_WORD = 0x7FFF;
export const LEGACY_REVERSE_POLARITY_WORD = 0xF830;

/** Candidate scaling divisor: signed raw / 10.0 degC. CANDIDATE_UNVERIFIED. */
export const CANDIDATE_SCALING_DIVISOR = 10.0;

/** The measurement side order of a Sensor: Front immediately precedes Rear. */
export const SIDE_ORDER = Object.freeze(['F', 'R']);
export const PHYSICAL_SIDE_LABELS = Object.freeze({ F: 'Front', R: 'Rear' });

const HEX = (word) => `0x${word.toString(16).toUpperCase().padStart(4, '0')}`;

/**
 * Classifies one raw word under the CANDIDATE rule and returns its honest presentation.
 *
 * The legacy sentinel candidates are classified BEFORE any numeric decode, so 0x7FFF can never be
 * rendered as an ordinary temperature and 0xF830 can never be rendered as an ordinary -200.0 degC.
 * Both keep their raw evidence and their legacy classification, and both stay quarantined.
 */
export function classifyPhysicalWord(rawWord) {
  if (!Number.isInteger(rawWord) || rawWord < 0 || rawWord > 0xFFFF) {
    throw new RangeError('A physical raw word must be an integer from 0 through 65535.');
  }

  if (rawWord === LEGACY_WIRE_BREAK_WORD) {
    return Object.freeze({
      rawWord,
      rawHex: HEX(rawWord),
      decodeState: CANDIDATE_DECODE_STATE.LEGACY_WIRE_BREAK,
      candidateValue: null,
      candidateUnit: null,
      label: 'LEGACY WIRE-BREAK CANDIDATE',
      presentAsOrdinaryTemperature: false,
      manufacturerMeaning: 'NOT_VERIFIED',
    });
  }

  if (rawWord === LEGACY_REVERSE_POLARITY_WORD) {
    return Object.freeze({
      rawWord,
      rawHex: HEX(rawWord),
      decodeState: CANDIDATE_DECODE_STATE.LEGACY_REVERSE_POLARITY,
      candidateValue: null,
      candidateUnit: null,
      label: 'LEGACY RV/P CANDIDATE',
      presentAsOrdinaryTemperature: false,
      manufacturerMeaning: 'NOT_VERIFIED',
    });
  }

  // Candidate rule only: signed Int16 of the raw word, divided by ten. No clamp, no correction and no
  // substitution. `unchecked` is expressed here as the explicit two's-complement fold, because the
  // arithmetic must be identical to the C# `unchecked((short)word)` on every runtime.
  const signed = rawWord >= 0x8000 ? rawWord - 0x10000 : rawWord;
  return Object.freeze({
    rawWord,
    rawHex: HEX(rawWord),
    decodeState: CANDIDATE_DECODE_STATE.CANDIDATE_VALUE,
    signedRaw: signed,
    candidateValue: signed / CANDIDATE_SCALING_DIVISOR,
    candidateUnit: 'degC',
    label: 'DECODE CANDIDATE',
    presentAsOrdinaryTemperature: true,
    manufacturerMeaning: 'NOT_VERIFIED',
  });
}

/**
 * Builds the canonical CAB1 measurement projection from the committed Drawing rev1 schedule rows.
 *
 * Exactly 40 rows come back, one per physical word offset 0-39, and every row carries only public-safe
 * drawing evidence: identity, module, channel, offset, Sensor, side and the preserved row state. There
 * is no endpoint field, no retired legacy address field, and no inferred cabinet mapping: the Cabinet and
 * Node identity are read from the schedule itself, and a row whose identity is not CAB1/TC-NODE-01 is
 * refused rather than silently accepted.
 */
export function buildCab1Mapping(scheduleRows) {
  const rows = [];
  for (const row of scheduleRows) {
    if (row.cabinet !== CAB1_CABINET_ID) continue;
    if (row.nodeId !== CAB1_NODE_ID) {
      throw new Error(`CAB1 mapping refused: schedule row ${row.scheduleRow} names Node '${row.nodeId}'.`);
    }
    rows.push(row);
  }

  rows.sort((a, b) => a.wordOrdinal - b.wordOrdinal);

  if (rows.length !== CAB1_WORD_COUNT) {
    throw new Error(`CAB1 mapping refused: expected ${CAB1_WORD_COUNT} offsets, found ${rows.length}.`);
  }

  const seenOffsets = new Set();
  const measurements = rows.map((row, index) => {
    const offset = row.wordOrdinal;
    if (!Number.isInteger(offset) || offset !== index) {
      throw new Error(`CAB1 mapping refused: offsets must be unique and contiguous from 0; got ${offset} at index ${index}.`);
    }
    if (seenOffsets.has(offset)) throw new Error(`CAB1 mapping refused: duplicate offset ${offset}.`);
    seenOffsets.add(offset);

    // The schedule's own evidence state is preserved verbatim. A conflict row is NEVER promoted to a
    // fully verified mapping; spare rows stay spare and can never acquire a Sensor identity.
    const spare = row.rowState === 'SPARE_UNMAPPED' || row.sensorId === null;
    if (spare) {
      if (offset < CAB1_ASSIGNED_OFFSETS) {
        throw new Error(`CAB1 mapping refused: offset ${offset} is inside the assigned range but carries no Sensor.`);
      }
      return Object.freeze({
        offset,
        moduleInstance: row.moduleInstance,
        moduleModel: row.moduleModel,
        channel: row.channel,
        sensorId: null,
        side: null,
        sideLabel: null,
        rowState: row.rowState,
        templateCompatibility: row.templateCompatibility,
        evidenceRef: row.evidenceRef,
        spare: true,
        drawingConflict: false,
        mappingEvidenceState: 'SPARE_UNMAPPED',
      });
    }

    if (offset >= CAB1_ASSIGNED_OFFSETS) {
      throw new Error(`CAB1 mapping refused: spare offset ${offset} must never map to a Sensor.`);
    }

    const drawingConflict = row.rowState === 'DRAWING_INTERNAL_CONFLICT';
    return Object.freeze({
      offset,
      moduleInstance: row.moduleInstance,
      moduleModel: row.moduleModel,
      channel: row.channel,
      sensorId: row.sensorId,
      side: row.side,
      sideLabel: row.side ? PHYSICAL_SIDE_LABELS[row.side] : null,
      rowState: row.rowState,
      templateCompatibility: row.templateCompatibility,
      evidenceRef: row.evidenceRef,
      spare: false,
      // A Drawing conflict is surfaced, never smoothed away: the operator must see that this
      // measurement's mapping is not fully verified at Drawing level.
      drawingConflict,
      mappingEvidenceState: drawingConflict ? 'DRAWING_INTERNAL_CONFLICT' : 'DRAWING_VERIFIED',
    });
  });

  const assigned = measurements.filter((m) => !m.spare);
  const spare = measurements.filter((m) => m.spare);
  const sensorIds = [...new Set(assigned.map((m) => m.sensorId))].sort();

  if (assigned.length !== CAB1_ASSIGNED_OFFSETS) {
    throw new Error(`CAB1 mapping refused: expected ${CAB1_ASSIGNED_OFFSETS} assigned offsets, found ${assigned.length}.`);
  }
  if (spare.length !== CAB1_SPARE_OFFSETS) {
    throw new Error(`CAB1 mapping refused: expected ${CAB1_SPARE_OFFSETS} spare offsets, found ${spare.length}.`);
  }
  if (sensorIds.length !== CAB1_SENSOR_COUNT) {
    throw new Error(`CAB1 mapping refused: expected ${CAB1_SENSOR_COUNT} unique Sensors, found ${sensorIds.length}.`);
  }

  const sensors = sensorIds.map((sensorId) => {
    const own = assigned.filter((m) => m.sensorId === sensorId);
    if (own.length !== 2) throw new Error(`CAB1 mapping refused: Sensor ${sensorId} must own exactly one Front and one Rear measurement.`);
    const front = own.find((m) => m.side === 'F');
    const rear = own.find((m) => m.side === 'R');
    if (!front || !rear) throw new Error(`CAB1 mapping refused: Sensor ${sensorId} must carry both measurement sides.`);
    // The committed schedule interleaves Front then Rear; the projection asserts it rather than assumes it.
    if (rear.offset !== front.offset + 1) {
      throw new Error(`CAB1 mapping refused: Sensor ${sensorId} Rear offset must immediately follow its Front offset.`);
    }
    return Object.freeze({
      sensorId,
      frontOffset: front.offset,
      rearOffset: rear.offset,
      drawingConflict: front.drawingConflict || rear.drawingConflict,
      mappingEvidenceState: (front.drawingConflict || rear.drawingConflict) ? 'DRAWING_INTERNAL_CONFLICT' : 'DRAWING_VERIFIED',
    });
  });

  const conflictRows = assigned.filter((m) => m.drawingConflict);
  return Object.freeze({
    nodeId: CAB1_NODE_ID,
    cabinetId: CAB1_CABINET_ID,
    cabinetTag: CAB1_CABINET_TAG,
    wordCount: CAB1_WORD_COUNT,
    assignedOffsetCount: CAB1_ASSIGNED_OFFSETS,
    spareOffsetCount: CAB1_SPARE_OFFSETS,
    sensorCount: CAB1_SENSOR_COUNT,
    measurementCount: CAB1_MEASUREMENT_COUNT,
    // The node-level summary is the honest aggregate: CAB1 is NOT fully drawing-verified because two
    // of its assigned rows carry a Drawing internal conflict.
    mappingEvidenceState: conflictRows.length > 0 ? 'DRAWING_INTERNAL_CONFLICT' : 'DRAWING_VERIFIED',
    drawingConflictOffsets: Object.freeze(conflictRows.map((m) => m.offset)),
    readProfile: PHYSICAL_READ_PROFILE,
    measurements: Object.freeze(measurements),
    sensors: Object.freeze(sensors),
  });
}

/**
 * Projects one published 40-word physical payload onto the canonical CAB1 mapping.
 *
 * The projection is pure: it never invents a word, never repairs a missing one and never converts a
 * failure into a value. A payload of the wrong length is refused. Every measurement returns the raw
 * word, the candidate decode and the preserved mapping evidence state together, so no caller can
 * present a candidate number without its evidence.
 */
export function projectCab1Payload(mapping, rawWords) {
  if (!Array.isArray(rawWords) || rawWords.length !== CAB1_WORD_COUNT) {
    throw new Error(`CAB1 projection refused: expected exactly ${CAB1_WORD_COUNT} raw words.`);
  }

  const byOffset = new Map();
  for (const measurement of mapping.measurements) {
    const rawWord = rawWords[measurement.offset];
    if (!Number.isInteger(rawWord) || rawWord < 0 || rawWord > 0xFFFF) {
      throw new Error(`CAB1 projection refused: offset ${measurement.offset} is not a 16-bit register word.`);
    }
    byOffset.set(measurement.offset, Object.freeze({
      offset: measurement.offset,
      moduleInstance: measurement.moduleInstance,
      moduleModel: measurement.moduleModel,
      channel: measurement.channel,
      sensorId: measurement.sensorId,
      side: measurement.side,
      sideLabel: measurement.sideLabel,
      spare: measurement.spare,
      rowState: measurement.rowState,
      drawingConflict: measurement.drawingConflict,
      mappingEvidenceState: measurement.mappingEvidenceState,
      ...classifyPhysicalWord(rawWord),
    }));
  }

  return Object.freeze({
    measurements: Object.freeze(mapping.measurements.map((m) => byOffset.get(m.offset))),
    sensors: mapping.sensors,
    mappingEvidenceState: mapping.mappingEvidenceState,
    drawingConflictOffsets: mapping.drawingConflictOffsets,
    readProfile: mapping.readProfile,
    wordCount: CAB1_WORD_COUNT,
  });
}
