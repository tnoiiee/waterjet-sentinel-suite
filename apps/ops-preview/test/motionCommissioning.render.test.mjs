// Stage 0.4C-K2 - real-page contract of the read-only Motion Commissioning section.
//
// The REAL public/app.mjs runs against the REAL Preview host through the stub DOM. This proves the
// application logic and the rendered text; it does not prove CSS, layout or a real browser.
//
// The Runtime is deliberately absent in this harness (the default transport refuses it), so the
// section must report that honestly: the Owner-confirmed design renders, and the Runtime half shows
// an explicit unavailable state instead of an invented projection.

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { allText, bootApp, cellTexts, html, rowsOf } from './helpers/fakeDom.mjs';
import { MOTION_WATER_JET_BINDINGS } from '../../../packages/commissioning/src/index.mjs';

let app;
let closed = false;

before(async () => {
  app = await bootApp({});
});

after(() => { if (!closed) app.close(); });

const section = () => html.slice(html.indexOf('<section id="motion-commissioning"'),
  html.indexOf('</section>', html.indexOf('<section id="motion-commissioning"')));

const tableOf = (id) => app.byId[id].childNodes.find((node) => node.tagName === 'table');
const bodyRows = (id) => tableOf(id).childNodes.find((node) => node.tagName === 'tbody').childNodes;
const headerCells = (id) => cellTexts(tableOf(id).childNodes.find((node) => node.tagName === 'thead').childNodes[0]);
const column = (id, label) => {
  const index = headerCells(id).indexOf(label);
  assert.notEqual(index, -1, `column '${label}' must exist in ${id}`);
  return bodyRows(id).map((row) => cellTexts(row)[index]);
};

test('the page boots ready and the motion projection is requested as a local GET', () => {
  assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
  assert.equal(app.byId['boot-error'].hidden, true);
  const projection = app.requests.find((r) => r.path === '/api/motion-commissioning');
  assert.ok(projection, 'the motion projection must be requested at boot');
  assert.equal(projection.status, 200);
  assert.equal(projection.method, 'GET');
  for (const request of app.requests) {
    assert.equal(request.method, 'GET', request.path);
    assert.equal(request.path.startsWith('/'), true, 'every request is a local route');
  }
});

test('the section is in the served page, carries the boundary text and offers no command control', () => {
  const markup = section();
  assert.match(markup, /Motion Commissioning/);
  assert.match(markup, /NO HARDWARE ACCESS \/ NO MOTION COMMAND/);
  assert.match(markup, /id="motion-gate-status"/);
  assert.match(html, /href="#motion-commissioning">Motion Commissioning</);
  // The only control in the section refreshes the read-only Runtime view. There is no Jog, Move,
  // Home, Stop, brake, encoder-reset or connect control on this page.
  const buttons = markup.match(/<button/g) ?? [];
  assert.equal(buttons.length, 1, 'exactly one read-only refresh control');
  assert.match(markup, /id="motion-refresh"/);
  for (const forbidden of ['jog', 'move', 'home', 'release brake', 'reset encoder', 'connect']) {
    assert.doesNotMatch(markup.toLowerCase(), new RegExp(`>\\s*${forbidden}\\s*<`), `no '${forbidden}' control`);
  }
});

test('the authorization gate is rendered as CLOSED with its reason, never as a bare code', () => {
  const gate = app.byId['motion-gate-status'].textContent;
  assert.match(gate, /Authorization gate: CLOSED \(Closed\)/);
  assert.match(gate, /MOTION_COMMISSIONING_GATE_NOT_OPEN/);
  assert.match(gate, /The separate motion commissioning execution gate is not open/);
  assert.match(gate, /refused before any hardware path/i);
});

test('the summary counts render, with nothing homed and nothing ready', () => {
  const cells = rowsOf(app.byId, 'motion-summary');
  const text = cells.map(allText).join(' | ');
  assert.match(text, /Motion controllers 4/);
  assert.match(text, /Controller axes 16/);
  assert.match(text, /Water Jets mapped 8/);
  assert.match(text, /Axes homed 0/);
  assert.match(text, /Controllers ready 0 of 4/);
  assert.match(text, /Motion commands issued 0/);
  assert.match(text, /Authorization gate CLOSED/);
});

test('all eight Water Jets render with the Owner-confirmed allocation and the axis role wording', () => {
  assert.deepEqual(column('motion-waterjets-body', 'Water Jet'), MOTION_WATER_JET_BINDINGS.map(([id]) => id));
  assert.deepEqual(column('motion-waterjets-body', 'Cabinet'), MOTION_WATER_JET_BINDINGS.map(([, cabinet]) => cabinet));
  assert.deepEqual(column('motion-waterjets-body', 'Controller'), MOTION_WATER_JET_BINDINGS.map(([, cabinet]) => `MC-${cabinet}`));
  const horizontal = column('motion-waterjets-body', 'Horizontal Axis');
  const vertical = column('motion-waterjets-body', 'Vertical Axis');
  MOTION_WATER_JET_BINDINGS.forEach(([, , h, v], index) => {
    // A and C read as the Horizontal Axis; B and D as the Vertical Axis. No other wording appears.
    assert.equal(horizontal[index], `${h} - ${h === 'A' || h === 'C' ? 'Horizontal Axis' : 'Vertical Axis'}`);
    assert.equal(vertical[index], `${v} - ${v === 'A' || v === 'C' ? 'Horizontal Axis' : 'Vertical Axis'}`);
  });
  const statuses = column('motion-waterjets-body', 'Mapping status');
  assert.equal(statuses[0], 'CONFIRMED_OWNER (Confirmed by the Owner)');
  assert.match(statuses[1], /DRAWING_REV1_CONFLICT_WJ2_WJ4/);
});

test('all four controllers render, none configured, none connected, none ready', () => {
  const header = headerCells('motion-controllers-body');
  assert.deepEqual(header, ['Attribute', 'MC-WSB1', 'MC-WSB2', 'MC-WSB3', 'MC-WSB4']);
  const rowFor = (label) => {
    const row = bodyRows('motion-controllers-body').find((r) => cellTexts(r)[0] === label);
    assert.ok(row, `row '${label}' must render`);
    return cellTexts(row).slice(1);
  };
  assert.deepEqual(rowFor('Cabinet'), ['WSB1', 'WSB2', 'WSB3', 'WSB4']);
  assert.deepEqual(rowFor('Dispatch ready'), ['FALSE', 'FALSE', 'FALSE', 'FALSE']);
  for (const value of rowFor('Endpoint')) assert.equal(value, 'ENDPOINT_NOT_CONFIGURED (Not configured)');
  for (const value of rowFor('Communication')) assert.equal(value, 'NOT_CONNECTED (Not connected)');
  for (const value of rowFor('Blocked by')) assert.match(value, /MOTION_COMMISSIONING_GATE_NOT_OPEN/);
  for (const value of rowFor('Identity basis')) assert.match(value, /Inferred from the confirmed mapping/);
});

test('all sixteen axes render: not homed, brake unknown, absolute move refused', () => {
  const rows = bodyRows('motion-axes-body');
  assert.equal(rows.length, 16);
  const roles = column('motion-axes-body', 'Axis role');
  assert.equal(roles.filter((role) => role === 'Horizontal Axis').length, 8);
  assert.equal(roles.filter((role) => role === 'Vertical Axis').length, 8);
  for (const value of column('motion-axes-body', 'Home')) assert.equal(value, 'NOT_HOMED (Not homed)');
  for (const value of column('motion-axes-body', 'Brake command')) assert.equal(value, 'None');
  for (const value of column('motion-axes-body', 'Brake state')) assert.equal(value, 'UNKNOWN (Unknown)');
  for (const value of column('motion-axes-body', 'Motion state')) assert.equal(value, 'UNKNOWN (Unknown)');
  for (const value of column('motion-axes-body', 'Absolute move')) assert.equal(value, 'REFUSED (Refused)');
  for (const value of column('motion-axes-body', 'Commissioning')) assert.equal(value, 'Not commissioned');
});

test('the brake output plan renders A-1, B-2, C-3, D-4', () => {
  assert.deepEqual(column('motion-brake-body', 'Controller axis'), ['A', 'B', 'C', 'D']);
  assert.deepEqual(column('motion-brake-body', 'Brake output'), ['Output 1', 'Output 2', 'Output 3', 'Output 4']);
  for (const value of column('motion-brake-body', 'Allocation basis')) assert.equal(value, 'Owner confirmed');
});

test('the three limit inputs of every axis render as unconfirmed and unread', () => {
  const header = headerCells('motion-limits-body');
  assert.deepEqual(header.slice(0, 3), ['Controller axis', 'Axis role', 'Sense']);
  assert.deepEqual(header.slice(3, 6), ['Reverse', 'Home', 'Forward']);
  for (const role of ['Reverse', 'Home', 'Forward']) {
    for (const value of column('motion-limits-body', role)) assert.equal(value, 'UNKNOWN (Unknown)');
  }
  for (const value of column('motion-limits-body', 'Polarity')) assert.equal(value, 'Unconfirmed');
});

test('every motion intent is rendered as refused by the closed gate', () => {
  const intents = column('motion-intents-body', 'Motion intent');
  assert.equal(intents.length, 6);
  assert.ok(intents.includes('JOG (Jog)'));
  assert.ok(intents.includes('RESET_ENCODER (Reset encoder)'));
  for (const value of column('motion-intents-body', 'Command state')) assert.equal(value, 'Refused');
  for (const value of column('motion-intents-body', 'Refused by (gate)')) {
    assert.equal(value, 'MOTION_COMMISSIONING_GATE_NOT_OPEN');
  }
});

test('the Jog Speed renders as a display value in controller units that is not executable', () => {
  const text = allText(app.byId['motion-jog-body']);
  assert.match(text, /Jog Speed: 10 controller units/);
  assert.match(text, /Legacy candidate, unverified/);
  assert.match(text, /NOT EXECUTABLE/);
  assert.doesNotMatch(text, /mm\/s/);
});

test('the mapping conflict, the not-verified claims and the legend render', () => {
  const conflicts = rowsOf(app.byId, 'motion-conflicts').map(allText).join(' | ');
  assert.match(conflicts, /MOTION-CONFLICT-01/);
  assert.match(conflicts, /WJ2, WJ4/);
  assert.match(conflicts, /OWNER_DECISION_REQUIRED/);

  const notVerified = rowsOf(app.byId, 'motion-not-verified').map(allText).join(' | ');
  for (const claim of ['NO_CONTROLLER_CONTACTED', 'NO_MOTION_COMMAND_ISSUED', 'NO_UNIT_CONVERSION_VERIFIED']) {
    assert.ok(notVerified.includes(claim), claim);
  }

  const legend = rowsOf(app.byId, 'motion-legend').map(allText).join(' | ');
  for (const reason of ['MOTION_COMMISSIONING_GATE_NOT_OPEN', 'GCLIB_NOT_INSTALLED', 'BRAKE_STATE_UNKNOWN',
    'HARDWARE_COMMANDS_NOT_AUTHORIZED']) {
    assert.ok(legend.includes(reason), reason);
  }
  assert.ok(legend.split(' | ').length >= 10, 'every refusal reason is worded in the legend');
});

test('at boot the Runtime Snapshot is not read, and the design still renders in full', () => {
  const runtimeText = app.byId['motion-runtime-body'].childNodes.map(allText).join(' ');
  assert.match(runtimeText, /NOT_READ/);
  assert.match(runtimeText, /Use Refresh to read it/);
  assert.doesNotMatch(runtimeText, /fingerprint/i);
  // No boot request carries the Runtime read flag: the page never couples its boot to the Runtime.
  for (const request of app.requests.filter((r) => r.path.startsWith('/api/motion-commissioning'))) {
    assert.equal(request.path, '/api/motion-commissioning');
  }
  const authority = app.byId['motion-authority'].textContent;
  assert.match(authority, /NO HARDWARE ACCESS \/ NO MOTION COMMAND \/ NO BRAKE OUTPUT \/ NO HOME \/ NO MOVE \/ NO ENCODER RESET/);
  assert.match(authority, /NOT the Cleaning-process interlocks/);
});

test('the refresh control reads the Runtime once and reports an absent host honestly', async () => {
  const before = app.requests.filter((r) => r.path.startsWith('/api/motion-commissioning')).length;
  await app.byId['motion-refresh'].listeners.click[0]();
  const after = app.requests.filter((r) => r.path.startsWith('/api/motion-commissioning'));
  assert.equal(after.length, before + 1);
  for (const request of after) assert.equal(request.method, 'GET');
  assert.equal(after[after.length - 1].path, '/api/motion-commissioning?runtime=1');

  // The harness intentionally has no Runtime host: the section must report that, not invent a
  // projection and not keep a stale one.
  const runtimeText = app.byId['motion-runtime-body'].childNodes.map(allText).join(' ');
  assert.match(runtimeText, /RUNTIME_UNAVAILABLE/);
  assert.doesNotMatch(runtimeText, /fingerprint/i);
});
