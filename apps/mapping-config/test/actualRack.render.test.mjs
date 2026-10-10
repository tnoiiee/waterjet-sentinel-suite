// Stage 0.4B-3 — the review surface for the AUTHORITATIVE 23-module rack, rendered by the real public/app.mjs
// against a stub DOM and the real server.
//
// The workbook bytes used here are a temporary copy of the authoritative workbook placed OUTSIDE the repository, as
// the server requires. The assertions are about how the UI presents roles and positions: RackSlot, the actual-rack
// configuration-tool position, ProcessModulePosition and the address state stay in separate columns, and a Power
// Supply or the End Module reads NOT APPLICABLE with reason NON_PROCESS_DATA_MODULE. Layout and real-browser
// behaviour are NOT exercised.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadConfiguration } from '../server.mjs';
import { allText, bootApp, html, walk } from './helpers/fakeDom.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = join(HERE, '..', '..', '..');
const WORKBOOK = join(REPO, 'T8_IO_Card_Mapping.xlsx');
const WORKBOOK_SHA256 = '4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e';

let app;
let tmp;
const by = () => app.byId;
const rows = (id) => by()[id].childNodes;
const cells = (row) => row.childNodes.map(allText);
const evidence = () => rows('evidence-body').map(cells);
const find = (re) => evidence().find((c) => re.test(c[0]));

before(async () => {
  tmp = mkdtempSync(join(tmpdir(), 'wjss-actual-rack-'));
  const xlsx = join(tmp, 'authoritative-copy.xlsx');
  copyFileSync(WORKBOOK, xlsx);
  app = await bootApp({ loader: () => loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: xlsx }), waitId: 'actual-rack-body' });
  for (let i = 0; i < 200 && rows('evidence-body').length === 0; i += 1) await new Promise((r) => setTimeout(r, 10));
});

after(() => {
  app.close();
  rmSync(tmp, { recursive: true, force: true });
});

test('A1 the authoritative rack renders one evidence row per physical module, all 23 of them', () => {
  assert.equal(rows('rack-body').length, 23, 'the physical rack keeps 23 slots');
  assert.equal(rows('evidence-body').length, 23, 'one evidence row per physical module');
  assert.equal(find(/750-362/)[1], '1');
  assert.equal(find(/750-600/)[1], '23');
  assert.match(allText(by()['evidence-summary']), /4 not applicable \(no process data\) of 23/);
});

test('A2 the first 750-471 shows RackSlot 13, Pos. 10 and ProcessModulePosition 10 as three separate columns', () => {
  const ai = find(/750-471 · AI-MODULE-01/);
  assert.equal(ai[1], '13', 'RackSlot');
  assert.equal(ai[2], 'Pos. 10', 'configuration-tool position');
  assert.equal(ai[3], '10', 'ProcessModulePosition');
  assert.equal(ai[4], 'ANALOG_INPUT_MODULE', 'role');
  assert.equal(ai[5], 'INPUT', 'process-data contribution');
  assert.equal(ai[6], 'NOT VERIFIED', 'no verified width');
  assert.equal(ai[7], 'PROVIDED_UNVERIFIED · INCOMPLETE');
  assert.equal(ai[10], 'VERIFIED_ACTUAL_RACK_SCREENSHOT', 'actual-rack evidence state');
  assert.equal(ai[11], 'NOT_VERIFIED', 'ProcessImageOrder is still not verified');
  assert.match(ai[12], /^ADDRESS UNRESOLVED · \d+ reasons$/);
  assert.match(ai[13], /ACTUAL_MAPPING_NOT_CROSS_CHECKED|BYTE_ORDER_NOT_VERIFIED/);
});

test('A3 the Power Supply modules and the End Module read NOT APPLICABLE with reason NON_PROCESS_DATA_MODULE', () => {
  for (const re of [/750-601/, /750-613/, /750-600/, /750-362/]) {
    const row = find(re);
    assert.equal(row[3], '—', `${re} has no ProcessModulePosition`);
    assert.equal(row[5], 'NONE (no process data)', re);
    assert.equal(row[6], '0 bit / 0 bit', re);
    assert.equal(row[7], 'NOT_APPLICABLE · NOT_APPLICABLE', re);
    assert.equal(row[12], 'NOT APPLICABLE', re);
    assert.equal(row[13], 'NON_PROCESS_DATA_MODULE', re);
    assert.doesNotMatch(row.join(' | '), /ADDRESS UNRESOLVED/, `${re} must not read ADDRESS UNRESOLVED`);
  }
  assert.equal(find(/750-601/)[4], 'POWER_SUPPLY');
  assert.equal(find(/750-613/)[4], 'SYSTEM_POWER_SUPPLY');
  assert.equal(find(/750-600/)[4], 'END_MODULE');
});

test('A4 the End Module shows a configuration-tool position while contributing no process data', () => {
  const end = find(/750-600/);
  assert.equal(end[2], 'Pos. 20', 'the End Module is a position entry in the visible sequence');
  assert.equal(end[5], 'NONE (no process data)');
  assert.equal(end[12], 'NOT APPLICABLE');
  // The two Power Supply modules are physical modules without a position entry in the supplied sequence.
  assert.equal(find(/750-601/)[2], 'not in the supplied sequence');
  assert.equal(find(/750-613/)[2], 'not in the supplied sequence');
  assert.equal(find(/750-601/)[1], '2', 'they keep their physical RackSlot');
  assert.equal(find(/750-613/)[1], '12');
});

test('A5 the actual-rack panel states that the Pos.10 Channel 3 evidence is limited to that instance and Channel', () => {
  const body = rows('actual-rack-body');
  assert.equal(body.length, 1, 'exactly one Channel-level actual-rack record');
  const row = cells(body[0]);
  assert.equal(row[0], 'AI-MODULE-01');
  assert.equal(row[1], '750-471');
  assert.equal(row[2], '13', 'RackSlot');
  assert.equal(row[3], 'Pos. 10');
  assert.equal(row[4], 'Channel 3');
  assert.equal(row[5], 'VERIFIED_ACTUAL_RACK_SCREENSHOT');
  assert.equal(row[6], '4AI U/I Diff Galv · 01.01.46(04)');
  assert.match(row[7], /signalType=4-20 mA/);
  assert.match(row[7], /inputFilter=Off/);
  assert.match(row[7], /channelDiagnosis=On/);
  assert.match(row[7], /upperUserLimit=32767/);
  assert.match(row[7], /lowerUserLimit=-32768/);
  assert.equal(row[8], 'this instance and this Channel only');
  const summary = allText(by()['actual-rack-summary']);
  assert.match(summary, /Partial actual-rack evidence applies to this rack/);
  assert.match(summary, /Pos\. 01 to Pos\. 20/);
  assert.match(summary, /2 Power Supply modules are physical rack modules but are not position entries/);
  assert.match(summary, /NOT APPLICABLE · Reason: NON_PROCESS_DATA_MODULE/);
  assert.match(summary, /a configuration-tool position is not process data and is not an address/);
  assert.match(summary, /Candidate process image: CANDIDATE_UNVERIFIED/);
  assert.match(summary, /authoritative: no · verified: no · hardware-ready: no · write authority: no/);
  assert.match(summary, /all byte, word and bit offsets unresolved/);
});

test('A6 the missing actual-rack evidence requests A to E are listed and the blocking statement is kept', () => {
  const text = allText(by()['actual-rack-missing']);
  assert.match(text, /Actual-rack evidence still required \(5\)/);
  for (const subject of ['Process Data screen', '750-471 Common settings', '750-471 Scaling screen', '750-554 Settings', 'head-station information']) {
    assert.match(text, new RegExp(subject.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
  }
  assert.match(text, /actual final field-network mapping remains unverified/);
  assert.match(text, /Numeric addresses must not be marked verified/);
  assert.match(text, /Hardware test activation remains blocked/);
});

test('A7 the new sections add no device control: every button remains a Draft or presentation control', () => {
  const buttons = [...html.matchAll(/<button\b[^>]*>([\s\S]*?)<\/button>/g)].map((m) => m[1].trim());
  for (const id of Object.keys(by())) walk(by()[id], (n) => { if (n.tagName === 'button') buttons.push(allText(n)); });
  const forbidden = /\b(connect|poll|read device|write|force|activate|command|start|stop|acknowledge|reset hardware|override|test_hardware|production)\b/i;
  for (const b of buttons) assert.doesNotMatch(b, forbidden, b);
  // No input, select or form control was added for a device or an address.
  assert.doesNotMatch(html, /<input\b|<select\b|<form\b/i);
  // The new section is read-only text and a table.
  const section = html.slice(html.indexOf('id="actual-rack"'), html.indexOf('id="validation"'));
  assert.doesNotMatch(section, /<button|<input|<form/i);
  assert.match(section, /read-only · partial · instance and Channel scoped/);
});

test('A8 the workbook copy used by this review is byte-identical to the authoritative workbook', async () => {
  const { createHash } = await import('node:crypto');
  const { readFileSync } = await import('node:fs');
  assert.equal(createHash('sha256').update(readFileSync(WORKBOOK)).digest('hex'), WORKBOOK_SHA256);
  assert.match(allText(by()['evidence-sources']), /SHA-256 4e0337e25c8377c0/);
});
