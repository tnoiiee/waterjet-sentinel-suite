// Test helper — builds an obviously SYNTHETIC .xlsx workbook in memory.
// All tags, signals and locations are invented examples. The builder writes a
// minimal OOXML package (shared strings, one worksheet) inside a ZIP container,
// with stored or deflated entries, so the import pipeline is exercised end to end.

import { deflateRawSync, crc32 } from 'node:zlib';

const COLS = 'ABCDEFGHIJ';

function xmlEscape(s) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function zip(entries) {
  const locals = [];
  const centrals = [];
  let offset = 0;
  for (const { name, data, method } of entries) {
    const raw = method === 8 ? deflateRawSync(data) : data;
    const crc = crc32(data);
    const nameBuf = Buffer.from(name, 'utf8');
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0, 6);
    local.writeUInt16LE(method, 8);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(raw.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBuf.length, 26);
    locals.push(local, nameBuf, raw);
    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(method, 10);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(raw.length, 20);
    central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(nameBuf.length, 28);
    central.writeUInt32LE(offset, 42);
    centrals.push(central, nameBuf);
    offset += local.length + nameBuf.length + raw.length;
  }
  const cd = Buffer.concat(centrals);
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(entries.length, 8);
  eocd.writeUInt16LE(entries.length, 10);
  eocd.writeUInt32LE(cd.length, 12);
  eocd.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, cd, eocd]);
}

/**
 * rows: array of arrays. The first row is the header. A cell is a string (shared
 * string), a finite number (numeric cell) or null (empty).
 */
export function buildWorkbook({ rows, method = 0, sheetName = 'Sheet1', extraSheets = [] }) {
  const strings = [];
  const index = new Map();
  const sIdx = (s) => {
    if (!index.has(s)) { index.set(s, strings.length); strings.push(s); }
    return index.get(s);
  };
  const sheetXml = (rs) => {
    const body = rs.map((row, ri) => {
      const cells = row.map((v, ci) => {
        if (v === null || v === undefined) return '';
        const ref = `${COLS[ci]}${ri + 1}`;
        if (typeof v === 'number') return `<c r="${ref}"><v>${v}</v></c>`;
        return `<c r="${ref}" t="s"><v>${sIdx(String(v))}</v></c>`;
      }).join('');
      return `<row r="${ri + 1}">${cells}</row>`;
    }).join('');
    return `<?xml version="1.0" encoding="UTF-8"?><worksheet><dimension ref="A1:${COLS[Math.max(0, (rs[0]?.length ?? 1) - 1)]}${rs.length}"/><sheetData>${body}</sheetData></worksheet>`;
  };
  const sheets = [{ name: sheetName, rows }, ...extraSheets];
  const sheetXmls = sheets.map((s) => sheetXml(s.rows));
  const workbook = `<?xml version="1.0" encoding="UTF-8"?><workbook><sheets>${sheets.map((s, i) => `<sheet name="${xmlEscape(s.name)}" sheetId="${i + 1}" r:id="rId${i + 1}"${s.state ? ` state="${s.state}"` : ''}/>`).join('')}</sheets></workbook>`;
  const rels = `<?xml version="1.0" encoding="UTF-8"?><Relationships>${sheets.map((_, i) => `<Relationship Id="rId${i + 1}" Target="worksheets/sheet${i + 1}.xml"/>`).join('')}</Relationships>`;
  const shared = `<?xml version="1.0" encoding="UTF-8"?><sst>${strings.map((s) => `<si><t xml:space="preserve">${xmlEscape(s)}</t></si>`).join('')}</sst>`;
  const contentTypes = `<?xml version="1.0" encoding="UTF-8"?><Types><Default Extension="xml" ContentType="application/xml"/></Types>`;
  const entries = [
    { name: '[Content_Types].xml', data: Buffer.from(contentTypes), method },
    { name: 'xl/workbook.xml', data: Buffer.from(workbook), method },
    { name: 'xl/_rels/workbook.xml.rels', data: Buffer.from(rels), method },
    { name: 'xl/sharedStrings.xml', data: Buffer.from(shared), method },
    ...sheetXmls.map((x, i) => ({ name: `xl/worksheets/sheet${i + 1}.xml`, data: Buffer.from(x), method })),
  ];
  return zip(entries);
}

export const HEADER = Object.freeze(['Slot', 'Module', 'Channel', 'I/O Tag', 'Signal / Description', 'I/O Type', 'Ref.', 'Location', 'Contact / Note', 'Status']);

const r = (slot, module, channel, tag, signal, ioType, status, note = null) => [
  slot, module, channel, tag, signal, ioType, null, 'Example location', note, status,
];

/** A generic synthetic rack with the same structure as a 23-slot rack, shortened. */
export function exampleRows({ placeholder = false, ambiguous = false } = {}) {
  const rows = [HEADER];
  rows.push(r(1, '750-362', '-', '-', 'Example coupler', 'Coupler', 'USED'));
  rows.push(r(2, '750-601', '-', '-', 'Example supply', 'Supply', 'USED'));
  for (let c = 1; c <= 8; c += 1) rows.push(r(3, '750-430', c, `EX-DI-${String(c).padStart(2, '0')}`, `Example lower limit #${c} (NO)`, 'DI (24 VDC.)', 'USED'));
  for (let c = 1; c <= 8; c += 1) rows.push(r(4, '750-430', c, `EX-DI-${String(c + 8).padStart(2, '0')}`, `Example upper limit #${c} (NO)`, 'DI (24 VDC.)', 'USED'));
  for (let c = 1; c <= 8; c += 1) rows.push(r(5, '750-530', c, `EX-DO-${String(c).padStart(2, '0')}`, `Example output ${c}`, 'DO (24 VDC.)', 'USED'));
  for (let i = 0; i < 12; i += 1) {
    const slot = 6 + Math.floor(i / 4);
    const channel = (i % 4) + 1;
    const tag = `EX-AI-${String(i + 1).padStart(2, '0')}`;
    let signal = `Example pressure ${i + 1}`;
    let ioType = 'AI (4-20 mA. HART5)';
    if (i === 10) { signal = 'Example current input'; ioType = 'AI (0-20 mA.)'; }
    if (i === 11) { signal = placeholder ? 'XXX awaiting reply' : 'Example spare-like input'; ioType = 'AI (4-20 mA.)'; }
    if (ambiguous && i === 10) { ioType = 'AI'; }
    rows.push(r(slot, '750-471', channel, tag, signal, ioType, 'USED'));
  }
  rows.push(r(9, '750-554', 1, 'EX-AO-01', 'Example analog output', 'AO (4-20 mA.)', 'USED'));
  rows.push(r(9, '750-554', 2, null, 'SPARE CHANNEL', 'AO', 'SPARE'));
  rows.push(r(10, '750-600', '-', '-', 'Example end module', 'End', 'USED'));
  return rows;
}

/** Binding seed that maps runtime tag names to the synthetic workbook tag ids. */
export function exampleSeed({ polarity = null } = {}) {
  const seed = {
    PUMP_OUTLET_PRESSURE: { source: 'EX-AI-01', declaredSourceIdentity: 'PUMP_OUTLET', engineering: { min: 0, max: 40, unit: 'bar' } },
    MAIN_VALVE_OUTLET_PRESSURE: { source: 'EX-AI-02', declaredSourceIdentity: 'MAIN_VALVE_OUTLET' },
  };
  for (let n = 1; n <= 8; n += 1) {
    seed[`IV${n}_OUTLET_PRESSURE`] = { source: `EX-AI-${String(n + 2).padStart(2, '0')}`, declaredSourceIdentity: `IV${n}_OUTLET` };
    seed[`IV${n}_LOWER_LIMIT`] = { source: `EX-DI-${String(n).padStart(2, '0')}`, declaredSourceIdentity: `IV${n}_LOWER_LIMIT`, activePolarity: polarity };
    seed[`IV${n}_UPPER_LIMIT`] = { source: `EX-DI-${String(n + 8).padStart(2, '0')}`, declaredSourceIdentity: `IV${n}_UPPER_LIMIT`, activePolarity: polarity };
  }
  return seed;
}
